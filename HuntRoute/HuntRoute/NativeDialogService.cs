using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
namespace HuntRoute;

internal sealed class NativeDialogService : IDisposable {
    private readonly Task initialization;
    private readonly ConfirmationQueue queue=new();
    private NativeConfirmationAddon? addon;
    private bool disposed,useFallback;
    private ConfirmationRequest? shownFallback;
    private uint queueGeneration;
    internal NativeDialogService(IDalamudPluginInterface pi) : this(KamiToolKitLibrary.InitializeAsync(pi)) {}
    internal NativeDialogService(Task initialization) => this.initialization=initialization;
    internal bool Initialized=>initialization.IsCompletedSuccessfully;
    internal bool InitializationFailed=>initialization.IsFaulted||initialization.IsCanceled;
    internal bool WasDeclined(string key)=>queue.IsDeclined(key);
    internal bool HasDialog=>queue.Active!=null||addon!=null;
    internal void Offer(ConfirmationRequest request,bool repeat=false) { if(disposed||queue.Active?.Key==request.Key)return;if(repeat)queue.Retry(request);else queue.Offer(request); }
    internal void CancelAll(){queueGeneration++;shownFallback=null;queue.Clear();addon?.CloseQuietly();addon=null;}
    internal void Cancel(string owner) {
        if(queue.Cancel(owner)) { addon?.CloseQuietly();addon=null; }
    }
    internal void Pump() {
        if(disposed || !initialization.IsCompleted) return;
        if(InitializationFailed&&!useFallback){
            useFallback=true;
            Service.PluginLog.Error("Не удалось подготовить игровой диалог Hunt Route: {Initialization}",initialization.Exception?.GetBaseException().Message??initialization.Status.ToString());
        }
        if(queue.Active is {} current && !current.Valid()) { queue.Resolve(current.Key,false,userResponse:false);addon?.CloseQuietly();addon=null; }
        if(queue.Active is {} blocked && !blocked.Ready()) {
            queue.Interrupt(blocked.Key);
            addon?.CloseQuietly();addon=null;shownFallback=null;
        }
        if(addon!=null||queue.Active!=null) return;
        bool available=Service.GameGui.GetAddonByName("SelectYesno").IsNull
            && Service.GameGui.GetAddonByName("HuntRouteConfirm").IsNull;
        var request=queue.Activate(available,Environment.TickCount64);
        if(request==null||useFallback) return;
        try {
            addon=new NativeConfirmationAddon(request.Presentation ?? TravelDialogContent.Plain(request.Text),request.Ready,yes=>Answered(request.Key,yes)) {
                InternalName="HuntRouteConfirm",Title="Hunt Route",Size=new Vector2(TravelDialogLayout.DialogWidth,180),
                RememberClosePosition=false,EnableContextMenu=false,DisableClamping=false
            };
            addon.Open();request.OnShown?.Invoke();
        } catch(Exception e) {
            addon?.CloseQuietly();addon=null;
            useFallback=true;
            Service.PluginLog.Error(e,"Не удалось открыть игровой диалог Hunt Route");
        }
    }
    internal ConfirmationRequest? PresentFallback(){
        if(disposed||!useFallback||queue.Active is not {} request||!request.Valid())return null;
        if(!ReferenceEquals(shownFallback,request)){
            shownFallback=request;
            request.OnShown?.Invoke();
        }
        return request;
    }
    internal void DrawFallback(){
        if(disposed||!useFallback||queue.Active is not {} active)return;
        ImGui.SetNextWindowSize(new(400,0),ImGuiCond.FirstUseEver);
        bool expanded=ImGui.Begin("Hunt Route · Подтверждение###HuntRouteConfirmFallback",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoCollapse);
        try {
            if(!expanded||PresentFallback() is not {} request)return;
            var content=request.Presentation??TravelDialogContent.Plain(request.Text);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+360);
            try{
                ImGui.TextWrapped(content.Title);
                if(content.Summary.Length>0)ImGui.TextWrapped(content.Summary);
                if(content.Destination.Length>0&&content.Destination!=content.Title)ImGui.TextWrapped(content.Destination);
                if(content.Context.Length>0)ImGui.TextWrapped(content.NextZone&&content.Fare.HasValue?$"Эфирит · {content.Context}":content.Context);
                if(content.Fare.HasValue)ImGui.TextUnformatted(content.FareLabel);
                if(content.Subject.Length>0)ImGui.TextWrapped(content.SubjectLabel.Length>0?$"{content.SubjectLabel} · {content.Subject}":content.Subject);
                if(content.Detail.Length>0)ImGui.TextWrapped(content.Detail);
                foreach(var metric in content.Metrics)ImGui.TextWrapped($"{metric.Label} · {metric.Value}");
                if(content.Footer.Length>0)ImGui.TextWrapped(content.Footer);
            }finally{ImGui.PopTextWrapPos();}
            ImGui.BeginDisabled(!request.Ready());
            bool accept=ImGui.Button(content.ConfirmLabel);
            ImGui.EndDisabled();
            ImGui.SameLine();
            bool decline=ImGui.Button(content.DeclineLabel);
            if(accept||decline)AnswerFallback(active.Key,accept);
        }finally{ImGui.End();}
    }
    internal void AnswerFallback(string key,bool yes){
        if(useFallback&&shownFallback?.Key==key&&ReferenceEquals(shownFallback,queue.Active))Answered(key,yes);
    }
    private void Answered(string key,bool? yes) {
        try {
            if(queue.Active?.Key!=key) return;
            ConfirmationRequest? request=null;
            if(yes is {} response) request=queue.Resolve(key,response);
            else queue.Interrupt(key);
            var retired=addon;
            shownFallback=null;
            uint generation=queueGeneration;
            if(request==null && retired==null) return;
            Service.Framework.RunOnTick(()=> {
                if(retired!=null&&ReferenceEquals(addon,retired)){
                    addon=null;
                    retired.Dispose();
                }
                if(disposed || generation!=queueGeneration || request==null || !request.Valid()) return;
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
internal sealed unsafe class NativeConfirmationAddon(TravelDialogContent content,Func<bool> ready,Action<bool?> answer) : NativeAddon {
    private void AddFixedNode(NodeBase node)=>NativeFixedLayout.Attach(this,node);
    private bool answered;
    private Vector2 measuredSize;
    private TextButtonNode? yesButton,noButton;
    private TextNode? yesCaption,noCaption;
    private IconImageNode? portrait;
    private TextNode? portraitCaption;
    private TextNode? fareText;
    private IconImageNode? fareIcon;
    private NativeHuntRect? portraitRect;

    protected override void OnSetup(AtkUnitBase* native,Span<AtkValue> values) {
        if(WindowNode is WindowNode window) {
            window.SubtitleNode.IsVisible=false;window.DividingLineNode.IsVisible=false;
            window.ShowConfigButton=false;window.ShowHelpButton=false;
        }
        var measureButton=new TextNode{FontSize=NativeHuntLayout.ButtonFont,Size=new(4096,64),IsVisible=false};
        AddFixedNode(measureButton);
        var textNodes=new List<TextNode>();
        var presentation=content;
        if(content.PortraitIcon!=0){
            // Draw the selected texture region at its native size before applying uniform node scaling.
            var image=new IconImageNode{WrapMode=KamiToolKit.Enums.WrapMode.None,IsVisible=false};
            AddFixedNode(image);
            try{image.IconId=content.PortraitIcon;portrait=image;}
            catch(Exception e){Service.PluginLog.Debug(e,"Портрет следующей цели недоступен");presentation=content with {PortraitIcon=0};}
        }
        var layout=TravelDialogLayout.Build(presentation,(text,width,font)=> {
            var node=new TextNode { Size=new Vector2(width,4096),FontSize=font,LineSpacing=font+3,
                String=text };
            node.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);
            textNodes.Add(node);AddFixedNode(node);return node.GetTextDrawSize(considerScale:false).Y;
        },label=>NativeButtonText.Measure(measureButton,label),title=> {
            measureButton.FontSize=NativeHuntLayout.TitleFont;
            try { return measureButton.GetTextDrawSize(title,considerScale:false).X; }
            finally { measureButton.FontSize=NativeHuntLayout.ButtonFont; }
        });
        for(int i=0;i<layout.Texts.Count;i++) { var spec=layout.Texts[i];var node=textNodes[i];node.Position=spec.Position;node.Size=spec.Size;node.TextColor=spec.Color;if(spec.Centered)node.AlignmentType=AlignmentType.Top; }
        fareText=presentation.Fare.HasValue?textNodes[^1]:null;
        if(fareText!=null) {
            fareIcon=new IconImageNode{IconId=1,Size=new(16,16),FitTexture=true,IsVisible=false};
            AddFixedNode(fareIcon);
        }
        portraitRect=layout.Portrait;
        if(portraitRect is {} portraitBounds&&presentation.NextZone) {
            for(int i=0;i<layout.Texts.Count;i++) {
                if(layout.Texts[i].Position.X==portraitBounds.Position.X&&layout.Texts[i].Position.Y>=portraitBounds.Position.Y+portraitBounds.Size.Y) {
                    portraitCaption=textNodes[i];
                    break;
                }
            }
        }
        yesButton=new TextButtonNode { Position=layout.Confirm.Position,Size=layout.Confirm.Size,String="",
            NavIndex=1,NavLeft=2,NavRight=2,OnClick=()=>Complete(true) };
        yesButton.BackgroundNode.Color=NativeHuntColors.PrimarySurface;
        yesButton.LabelNode.TextColor=NativeHuntColors.Attention;
        yesButton.LabelNode.FontSize=NativeHuntLayout.ButtonFont;
        var no=noButton=new TextButtonNode { Position=layout.Decline.Position,Size=layout.Decline.Size,String="",
            NavIndex=2,NavLeft=1,NavRight=1,OnClick=()=>Complete(false) };
        no.LabelNode.FontSize=NativeHuntLayout.ButtonFont;
        AddFixedNode(yesButton);AddFixedNode(no);
        yesCaption=NativeButtonText.CreateCaption();noCaption=NativeButtonText.CreateCaption();
        AddFixedNode(yesCaption);AddFixedNode(noCaption);
        measuredSize=new(layout.Width,layout.Height);
        SetWindowSize(measuredSize);
        RefreshPortrait();
        RefreshFare();
        RefreshCaptions();
        native->FocusNode=no.FocusNode;
    }
    protected override void OnShow(AtkUnitBase* native) {
        noButton?.SetFocus();
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
    protected override void OnUpdate(AtkUnitBase* native) {
        if(answered||yesButton==null||noButton==null)return;
        yesButton.IsEnabled=ready();
        RefreshPortrait();
        RefreshFare();
        RefreshCaptions();
    }
    private void RefreshFare(){
        if(fareIcon is not {IsTextureReady:true,IsVisible:false} icon||fareText is not {} text||content.Fare is not {} amount)return;
        string number=amount.ToString();
        float numberWidth=MathF.Ceiling(text.GetTextDrawSize(number,considerScale:false).X);
        if(numberWidth<=0||numberWidth+22>text.Size.X)return;
        float x=text.Position.X+(text.Size.X-numberWidth-22)/2;
        icon.Position=new(x+numberWidth+6,text.Position.Y+(text.Size.Y-16)/2);
        text.Position=new(x,text.Position.Y);
        text.Size=new(numberWidth+2,text.Size.Y);
        text.AlignmentType=AlignmentType.TopLeft;
        text.String=number;
        icon.IsVisible=true;
    }
    private void RefreshPortrait(){
        if(portrait is not {IsVisible:false,IsTextureReady:true} image||portraitRect is not {} bounds)return;
        var texture=image.ActualTextureSize;
        if(texture.X<=0||texture.Y<=0)return;
        var crop=ReadPortraitCrop(image,texture);
        var fitted=TravelDialogLayout.FitPortrait(bounds,crop.Size);
        image.TextureCoordinates=crop.Position;
        image.TextureSize=crop.Size;
        image.Position=fitted.Position;
        image.Size=crop.Size;
        image.Origin=Vector2.Zero;
        image.Scale=new Vector2(fitted.Size.X/crop.Size.X);
        if(portraitCaption is {} caption)
            caption.Position=TravelDialogLayout.PortraitCaptionPosition(bounds,fitted);
        float unusedHeight=TravelDialogLayout.UnusedPortraitHeight(bounds,fitted);
        if(unusedHeight>0&&yesButton is {} confirm&&noButton is {} decline) {
            var offset=new Vector2(0,unusedHeight);
            if(fareText is {} fare)fare.Position-=offset;
            if(fareIcon is {} coin)coin.Position-=offset;
            confirm.Position-=offset;
            decline.Position-=offset;
            measuredSize-=offset;
            SetWindowSize(measuredSize);
        }
        image.IsVisible=true;
    }
    private NativeHuntRect ReadPortraitCrop(IconImageNode image,Vector2 texture) {
        var full=new NativeHuntRect(Vector2.Zero,texture);
        try {
            var file=string.IsNullOrEmpty(image.TexturePath)?null:
                Service.DataManager.GetFile<Lumina.Data.Files.TexFile>(image.TexturePath);
            if(file==null) {
                var lookup=new Dalamud.Interface.Textures.GameIconLookup(content.PortraitIcon,itemHq:false,hiRes:true);
                string path=Service.TextureProvider.GetIconPath(lookup);
                file=Service.DataManager.GetFile<Lumina.Data.Files.TexFile>(path);
            }
            if(file==null)return full;
            return TravelDialogLayout.PortraitCrop(file.ImageData,file.Header.Width,file.Header.Height,texture);
        } catch(Exception e) {
            Service.PluginLog.Debug(e,"Не удалось обрезать прозрачные поля портрета");
            return full;
        }
    }
    private void RefreshCaptions(){
        if(yesButton==null||noButton==null||yesCaption==null||noCaption==null)return;
        yesButton.String=noButton.String="";
        yesCaption.IsVisible=noCaption.IsVisible=true;
        NativeButtonText.SetCaption(yesCaption,yesButton,content.ConfirmLabel,yesButton.IsEnabled?NativeHuntColors.Attention:NativeHuntColors.Muted);
        NativeButtonText.SetCaption(noCaption,noButton,content.DeclineLabel,Vector4.One);
    }
    protected override void OnHide(AtkUnitBase* native)=>Dismiss();
    protected override void OnFinalize(AtkUnitBase* native)=>Dismiss();
    private void Dismiss() {
        if(!answered) Complete(ready()?false:null);
    }
    private void Complete(bool? yes) {
        if(answered || (yes==true&&!ready())) return;
        // Keep gamepad focus until the input callback returns. The service closes the dialog on the next tick.
        answered=true;
        answer(yes);
    }
    internal void CloseQuietly() { answered=true;Dispose(); }
}
