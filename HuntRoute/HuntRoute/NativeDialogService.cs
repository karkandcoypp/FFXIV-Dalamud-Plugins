using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
namespace HuntRoute;

internal sealed class NativeDialogService : IDisposable {
    private readonly Task initialization;
    private readonly ConfirmationQueue queue=new();
    private NativeConfirmationAddon? addon;
    private bool disposed,failed;
    internal NativeDialogService(IDalamudPluginInterface pi) => initialization=KamiToolKitLibrary.InitializeAsync(pi);
    internal bool Initialized=>initialization.IsCompletedSuccessfully;
    internal bool InitializationFailed=>initialization.IsFaulted||initialization.IsCanceled;
    internal bool HasDialog=>queue.Active!=null||addon!=null;
    internal string? Error=>failed?"Не удалось открыть игровое подтверждение. Маршрут сохранён; проверьте журнал Dalamud.":null;
    internal void Offer(ConfirmationRequest request,bool repeat=false) { if(disposed||queue.Active?.Key==request.Key)return;if(repeat)queue.Retry(request);else queue.Offer(request); }
    internal void CancelAll(){queue.Clear();addon?.CloseQuietly();addon=null;}
    internal void Cancel(string owner) {
        if(queue.Cancel(owner)) { addon?.CloseQuietly();addon=null; }
    }
    internal void Pump() {
        if(disposed || !initialization.IsCompleted) return;
        if(!initialization.IsCompletedSuccessfully) {
            if(!failed) { failed=true;Service.PluginLog.Error(initialization.Exception!,"Не удалось подготовить игровой диалог Hunt Route"); }
            return;
        }
        if(queue.Active is {} current && !current.Valid()) { queue.Resolve(current.Key,false);addon?.CloseQuietly();addon=null; }
        if(addon!=null) return;
        bool available=Service.GameGui.GetAddonByName("SelectYesno").IsNull
            && Service.GameGui.GetAddonByName("HuntRouteConfirm").IsNull;
        var request=queue.Activate(available);
        if(request==null) return;
        try {
            addon=new NativeConfirmationAddon(request.Presentation ?? TravelDialogContent.Plain(request.Text),request.Ready,yes=>Answered(request.Key,yes)) {
                InternalName="HuntRouteConfirm",Title="Hunt Route",Size=new Vector2(TravelDialogLayout.DialogWidth,180),
                RememberClosePosition=false,EnableContextMenu=false,DisableClamping=false
            };
            addon.Open();request.OnShown?.Invoke();
        } catch(Exception e) {
            addon?.CloseQuietly();addon=null;
            queue.Resolve(request.Key,false);queue.Retry(request);
            Service.PluginLog.Error(e,"Не удалось открыть игровой диалог Hunt Route");
        }
    }
    private void Answered(string key,bool yes) {
        try {
            if(queue.Active?.Key!=key) return;
            var request=queue.Resolve(key,yes);var retired=addon;addon=null;
            if(request==null && retired==null) return;
            Service.Framework.RunOnTick(()=> {
                retired?.Dispose();
                if(disposed || request==null || !request.Valid()) return;
                if(!request.Ready() || !request.Accept()) queue.Retry(request);
            },delayTicks:1);
        } catch(Exception e) { Service.PluginLog.Error(e,"Ошибка ответа на игровой диалог Hunt Route"); }
    }
    public void Dispose() {
        disposed=true;queue.Clear();addon?.CloseQuietly();addon=null;
        if(initialization.IsCompletedSuccessfully) KamiToolKitLibrary.Dispose();
        else _=FinishInitializationAndDispose();
    }
    private async Task FinishInitializationAndDispose() {
        try { await initialization;await Service.Framework.RunOnFrameworkThread(KamiToolKitLibrary.Dispose); }
        catch(Exception e) { Service.PluginLog.Error(e,"Очистка native UI Hunt Route"); }
    }
}
internal sealed unsafe class NativeConfirmationAddon(TravelDialogContent content,Func<bool> ready,Action<bool> answer) : NativeAddon {
    private bool answered;
    private Vector2 measuredSize;
    private TextButtonNode? yesButton;
    protected override void OnSetup(AtkUnitBase* native,Span<AtkValue> values) {
        if(WindowNode is WindowNode window) {
            window.SubtitleNode.IsVisible=false;window.DividingLineNode.IsVisible=false;
            window.ShowConfigButton=false;window.ShowHelpButton=false;
        }
        var textNodes=new List<TextNode>();
        var layout=TravelDialogLayout.Build(content,(text,width,font)=> {
            var node=new TextNode { Size=new Vector2(width,4096),FontSize=font,LineSpacing=font+3,
                String=text };
            node.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);
            textNodes.Add(node);AddNode(node);return node.GetTextDrawSize(considerScale:false).Y;
        });
        for(int i=0;i<layout.Texts.Count;i++) { var spec=layout.Texts[i];var node=textNodes[i];node.Position=spec.Position;node.Size=spec.Size;node.TextColor=spec.Color; }
        yesButton=new TextButtonNode { Position=layout.Confirm.Position,Size=layout.Confirm.Size,String=content.ConfirmLabel,
            NavIndex=1,NavLeft=2,NavRight=2,OnClick=()=>Complete(true,true) };
        yesButton.LabelNode.FontSize=NativeHuntLayout.ButtonFont;
        var no=new TextButtonNode { Position=layout.Decline.Position,Size=layout.Decline.Size,String=content.DeclineLabel,
            NavIndex=2,NavLeft=1,NavRight=1,OnClick=()=>Complete(false,true) };
        no.LabelNode.FontSize=NativeHuntLayout.ButtonFont;
        AddNode(yesButton);AddNode(no);measuredSize=new(layout.Width,layout.Height);SetWindowSize(measuredSize);
        native->FocusNode=no;
    }
    protected override void OnShow(AtkUnitBase* native) {
        // Center the measured window after setup, accounting for the game's UI scale.
        var viewport=(Vector2)AtkStage.Instance()->ScreenSize;
        if(viewport.X<=16||viewport.Y<=16)return;
        SetWindowSize(measuredSize);
        float requested=native->GetScale();
        float fitted=TravelDialogLayout.FitScale(measuredSize,viewport,requested);
        if(fitted!=requested)native->SetScale(fitted,true);
        var origin=(viewport-measuredSize*fitted)/2;
        native->SetPosition((short)origin.X,(short)origin.Y);
    }
    protected override void OnUpdate(AtkUnitBase* native) { if(!answered && yesButton!=null) yesButton.IsEnabled=ready(); }
    protected override void OnHide(AtkUnitBase* native)=>Complete(false,false);
    protected override void OnFinalize(AtkUnitBase* native)=>Complete(false,false);
    private void Complete(bool yes,bool close) {
        if(answered || (yes&&!ready())) return;
        answered=true;if(close) Close();answer(yes);
    }
    internal void CloseQuietly() { answered=true;Dispose(); }
}
