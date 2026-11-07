using System;
using System.Numerics;
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
    internal void Offer(ConfirmationRequest request) { if(!disposed) queue.Offer(request); }
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
            addon=new NativeConfirmationAddon(request.Text,request.Ready,yes=>Answered(request.Key,yes)) {
                InternalName="HuntRouteConfirm",Title=string.Empty,Size=new Vector2(420,240),
                RememberClosePosition=false,EnableContextMenu=false,DisableClamping=false
            };
            addon.Open();
        } catch(Exception e) {
            addon?.CloseQuietly();addon=null;
            queue.Resolve(request.Key,false);queue.Retry(request);
            Service.PluginLog.Error(e,"Не удалось открыть игровой диалог Hunt Route");
        }
    }
    private void Answered(string key,bool yes) {
        try {
            if(queue.Active?.Key!=key) return;
            var request=queue.Resolve(key,yes);addon=null;
            if(request==null || disposed) return;
            Service.Framework.RunOnTick(()=> {
                if(disposed || !request.Valid()) return;
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
internal sealed unsafe class NativeConfirmationAddon(string text,Func<bool> ready,Action<bool> answer) : NativeAddon {
    private bool answered;
    private TextButtonNode? yesButton;
    protected override void OnSetup(AtkUnitBase* native,Span<AtkValue> values) {
        if(WindowNode is WindowNode window) {
            window.TitleNode.IsVisible=false;window.SubtitleNode.IsVisible=false;window.DividingLineNode.IsVisible=false;
            window.ShowConfigButton=false;window.ShowHelpButton=false;
        }
        var prompt=new TextNode { Position=new Vector2(24,24),Size=new Vector2(372,160),FontSize=14,
            LineSpacing=18,String=text,TextFlags=TextFlags.WordWrap|TextFlags.MultiLine };
        AddNode(prompt);
        float height=Math.Max(144,prompt.GetTextDrawSize().Y);
        prompt.Size=new Vector2(372,height);
        float y=24+height+12;
        yesButton=new TextButtonNode { Position=new Vector2(102,y),Size=new Vector2(100,28),TextId=576,SheetType=Lumina.Data.Parsing.Uld.NodeData.SheetType.Addon,
            NavIndex=1,NavLeft=2,NavRight=2,OnClick=()=>Complete(true,true) };
        var no=new TextButtonNode { Position=new Vector2(214,y),Size=new Vector2(100,28),TextId=577,SheetType=Lumina.Data.Parsing.Uld.NodeData.SheetType.Addon,
            NavIndex=2,NavLeft=1,NavRight=1,OnClick=()=>Complete(false,true) };
        AddNode(yesButton);AddNode(no);SetWindowSize(new Vector2(420,y+48));
        native->FocusNode=no;
    }
    protected override void OnUpdate(AtkUnitBase* native) { if(!answered && yesButton!=null) yesButton.IsEnabled=ready(); }
    protected override void OnHide(AtkUnitBase* native)=>Complete(false,false);
    protected override void OnFinalize(AtkUnitBase* native)=>Complete(false,false);
    private void Complete(bool yes,bool close) {
        if(answered || (yes&&!ready())) return;
        answered=true;if(close) Close();answer(yes);
    }
    internal void CloseQuietly() { answered=true;Close(); }
}
