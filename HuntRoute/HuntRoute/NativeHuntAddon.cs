using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;

namespace HuntRoute;

internal sealed unsafe class NativeHuntAddon(NativeHuntController controller,Action<NativeHuntAction> execute,Action closed,Func<bool> modal,bool mini=false):NativeAddon {
 private void AddFixedNode(NodeBase node)=>NativeFixedLayout.Attach(this,node);
 private TextNode? buttonMeasure;
 private IconImageNode? gilIcon;
 private readonly TextNode?[] captions=new TextNode?[NativeHuntLayout.ButtonCapacity];
 private readonly TextButtonNode?[] buttons=new TextButtonNode?[NativeHuntLayout.ButtonCapacity];
 private readonly CheckboxNode?[] checks=new CheckboxNode?[NativeHuntLayout.ButtonCapacity];
 private ComponentNode? Control(int slot)=>checks[slot] is {IsVisible:true} check?check:buttons[slot];
 private static bool IsFocused(ComponentNode? node,AtkResNode* focus)=>node!=null&&focus!=null&&(focus==(AtkResNode*)node||focus==(AtkResNode*)node.FocusNode);
 private string?[] keys=new string?[NativeHuntLayout.ButtonCapacity];
 private NativeHuntAction?[] commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];
 private TextNode? heading,summary,pageLabel,progressLabel;
 private TextNode? eliteLabel;
 private ColorImageNode? eliteBorder,eliteBackground;
 private ColorImageNode? progressTrack,progressFill;
 private readonly TextNode?[] tableCells=new TextNode?[NativeHuntLayout.ButtonCapacity*3];
 private IconImageNode? portrait;
 private IReadOnlyList<NativeNavLink> links=[];
 private bool focusRequested,focusFirstBody;
 private object? pendingClick;
 private string? titleText,summaryText;
 private string? focusedKey;
 private string? screenIdentity;
 private uint icon;
 private long nextUpdate;
 private float requestedScale;
 internal void RequestFocus()=>focusRequested=true;
 internal bool OwnsFocus(){
  if(!IsOpen)return false;var node=AtkStage.Instance()->AtkInputManager->FocusedNode;if(node==null)return false;
  if(WindowNode is {} window&&node==(AtkResNode*)window.WindowHeaderFocusNode)return true;
  for(int i=0;i<buttons.Length;i++)if(IsFocused(Control(i),node))return true;
  return false;
 }
 protected override void OnSetup(AtkUnitBase* native,Span<AtkValue> values){
  if(WindowNode is WindowNode window){window.ShowConfigButton=false;window.ShowHelpButton=false;}
  if(mini)native->DisableFocusOnShow=true;
  buttonMeasure=new TextNode{FontSize=NativeHuntLayout.ButtonFont,Size=new(4096,64),IsVisible=false};AddFixedNode(buttonMeasure);
  gilIcon=GilDisplay.CreateIcon();AddFixedNode(gilIcon);
  heading=new TextNode{FontSize=NativeHuntLayout.TitleFont,String="Hunt Route"};heading.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);AddFixedNode(heading);
  eliteBorder=new ColorImageNode{Color=NativeHuntLayout.EliteBorder,IsVisible=false};AddFixedNode(eliteBorder);
  eliteBackground=new ColorImageNode{Color=NativeHuntLayout.EliteBackground,IsVisible=false};AddFixedNode(eliteBackground);
  eliteLabel=new TextNode{FontSize=NativeHuntLayout.SecondaryFont,TextColor=NativeHuntLayout.EliteGold,AlignmentType=AlignmentType.Center,String=NativeHuntLayout.EliteLabel,IsVisible=false};AddFixedNode(eliteLabel);
  summary=new TextNode{FontSize=NativeHuntLayout.BodyFont,LineSpacing=18};summary.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);AddFixedNode(summary);
  portrait=new IconImageNode{FitTexture=true,IsVisible=false};AddFixedNode(portrait);
  progressLabel=new TextNode{FontSize=NativeHuntLayout.SecondaryFont,TextColor=new(.68f,.88f,.80f,1)};AddFixedNode(progressLabel);
  progressTrack=new ColorImageNode{Color=new(.24f,.27f,.28f,1)};AddFixedNode(progressTrack);
  progressFill=new ColorImageNode{Color=new(.38f,.76f,.64f,1)};AddFixedNode(progressFill);
  pageLabel=new TextNode{Size=new(240,24),FontSize=NativeHuntLayout.SecondaryFont};AddFixedNode(pageLabel);

  for(int i=0;i<buttons.Length;i++){
   int slot=i;var button=new TextButtonNode{NavIndex=i+1,OnClick=()=>Clicked(slot)};
   button.LabelNode.FontSize=NativeHuntLayout.ButtonFont;button.LabelNode.LineSpacing=18;button.LabelNode.AddTextFlags(TextFlags.Ellipsis);
   buttons[i]=button;AddFixedNode(button);
   var caption=NativeButtonText.CreateCaption();captions[i]=caption;AddFixedNode(caption);
   if(i is >=3 and <=8){
    var check=new CheckboxNode{NavIndex=i+1,DisableAutoResize=true,IsVisible=false,OnClick=_=>Clicked(slot)};
    checks[i]=check;AddFixedNode(check);
   }
  }
  for(int i=0;i<tableCells.Length;i++){
   var cell=new TextNode{FontSize=NativeHuntLayout.ButtonFont,IsVisible=false};
   cell.AddTextFlags(TextFlags.Ellipsis);tableCells[i]=cell;AddFixedNode(cell);
  }
  requestedScale=0;screenIdentity=null;keys=new string?[NativeHuntLayout.ButtonCapacity];commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];nextUpdate=0;focusRequested=!mini;focusFirstBody=false;icon=0;titleText=summaryText=null;
  Refresh(native);
 }
 protected override void OnShow(AtkUnitBase* native){
  requestedScale=native->GetScale();
  FitViewport(native);
  if(!mini)focusRequested=true;
 }
 private void FitViewport(AtkUnitBase* native){
  if(requestedScale<=0)return;
  var viewport=(Vector2)AtkStage.Instance()->ScreenSize;
  if(viewport.X<=16||viewport.Y<=16)return;
  float fitted=TravelDialogLayout.FitScale(Size,viewport,requestedScale);
  if(native->GetScale()!=fitted)native->SetScale(fitted,true);
  var position=TravelDialogLayout.ClampPosition(new(native->X,native->Y),Size*fitted,viewport);
  if(native->X!=(short)position.X||native->Y!=(short)position.Y)native->SetPosition((short)position.X,(short)position.Y);
 }
 protected override void OnUpdate(AtkUnitBase* native){
  if(Environment.TickCount64>=nextUpdate){nextUpdate=Environment.TickCount64+200;Refresh(native);}
  if(focusRequested&&IsOpen&&!modal()){int index=NativeHuntNavigation.RestoreFocus(links,focusedKey);if(index>0){if(checks[index-1] is {IsVisible:true} check)check.SetFocus();else buttons[index-1]?.SetFocus();}focusRequested=false;}
 }
 protected override void OnHide(AtkUnitBase* native){pendingClick=null;closed();}
 protected override void OnFinalize(AtkUnitBase* native){Array.Clear(buttons);Array.Clear(captions);Array.Clear(checks);Array.Clear(tableCells);buttonMeasure=null;heading=null;summary=null;portrait=null;pageLabel=progressLabel=null;progressTrack=progressFill=null;links=[];}
 private void Clicked(int slot){
  if(pendingClick!=null||modal()||slot<0||slot>=buttons.Length||!(checks[slot] is {IsVisible:true} check?check.IsEnabled:buttons[slot] is {IsVisible:true,IsEnabled:true})||commands[slot] is not {} command)return;
  var key=keys[slot];
  var click=new object();
  pendingClick=click;
  // Actions may open a game window or close this addon. Let native input finish first.
  _=Service.Framework.RunOnTick(()=>{
   if(!ReferenceEquals(pendingClick,click))return;
   pendingClick=null;
   if(!IsOpen||modal()||keys[slot]!=key||commands[slot]!=command)return;
   ExecuteClick(command,key);
  },delayTicks:1);
 }
 private void ExecuteClick(NativeHuntAction command,string? key){
  focusedKey=key;
  if(command.Kind=="previous"||command.Kind=="next-page"){controller.Turn(command.Delta,NativeHuntLayout.PagedButtonCount(controller.Build(mini)));focusFirstBody=true;focusRequested=true;}
  else if(command.Kind=="close")Close();
  else execute(command);
  nextUpdate=0;
 }
 private void Refresh(AtkUnitBase* native){
  if(heading==null||summary==null||portrait==null||pageLabel==null||progressLabel==null||progressTrack==null||progressFill==null)return;
  // Do not steal focus from the map or another game window on passive updates.
  bool ownsFocus=false;var global=AtkStage.Instance()->AtkInputManager->FocusedNode;
  for(int i=0;i<buttons.Length;i++)if(IsFocused(Control(i),global)){focusedKey=keys[i];ownsFocus=true;}
  var screen=controller.Build(mini);
  bool hasIcon=!mini&&screen.Icon!=0;
  string title=NativeHuntLayout.FitLabel(screen.Title,110),description=NativeHuntLayout.FitLabel(screen.Summary,420);
  if(titleText!=title){heading.String=title;titleText=title;}
  bool gilSummary=NativeHuntLayout.CenterNumberText(controller.Page,mini)&&description.EndsWith(" гил",StringComparison.Ordinal);
  if(summaryText!=description||gilSummary){summary.String=description;summaryText=description;}
  if(gilIcon!=null)gilIcon.IsVisible=false;
  summary.FontSize=NativeHuntLayout.SummaryFont(controller.Page,mini);
  var textAlignment=NativeHuntLayout.CenterNumberText(controller.Page,mini)?AlignmentType.Top:AlignmentType.TopLeft;
  heading.AlignmentType=summary.AlignmentType=textAlignment;
  summary.LineSpacing=summary.FontSize+3;
  float textWidth=NativeHuntLayout.SummaryWidth(screen,mini);
  float titleWidth=0;
  if(screen.Elite){heading.Size=new(4096,110);titleWidth=heading.GetTextDrawSize(considerScale:false).X;}
  heading.Size=new(NativeHuntLayout.TitleWidth(screen,mini),110);summary.Size=new(textWidth,200);
  var layout=NativeHuntLayout.Build(screen,controller.Page,controller.Offset,heading.GetTextDrawSize(considerScale:false).Y,summary.GetTextDrawSize(considerScale:false).Y,mini,label=>NativeButtonText.Measure(buttonMeasure!,label),titleWidth);
  heading.TextColor=screen.Progress!=null?NativeHuntColors.Tracking:Vector4.One;
  summary.TextColor=screen.SummaryColor??NativeHuntColors.Muted;
  heading.Position=layout.Heading.Position;heading.Size=layout.Heading.Size;
  eliteBorder!.IsVisible=eliteBackground!.IsVisible=eliteLabel!.IsVisible=layout.EliteBadge.HasValue;
  if(layout.EliteBadge is {} badge){
   eliteBorder.Position=badge.Position;eliteBorder.Size=badge.Size;
   eliteBackground.Position=badge.Position+Vector2.One;eliteBackground.Size=badge.Size-2*Vector2.One;
   eliteLabel.Position=badge.Position;eliteLabel.Size=badge.Size;
  }
  summary.Position=layout.Summary.Position;summary.Size=layout.Summary.Size;
  if(gilSummary&&gilIcon!=null)GilDisplay.Place(gilIcon,summary,description[..^4],summary.Position,summary.Size);
  var progress=screen.Progress;
  progressLabel.IsVisible=progress!=null;
  progressTrack.IsVisible=!mini&&progress!=null;
  progressFill.IsVisible=!mini&&progress is {Fraction:>0};
  if(progress!=null){
   progressLabel.TextColor=progress.Fraction>=1?NativeHuntColors.Ready:Vector4.One;
   progressLabel.Position=layout.ProgressLabel.Position;progressLabel.Size=layout.ProgressLabel.Size;
   if(progressLabel.String.ToString()!=progress.Label)progressLabel.String=progress.Label;
   progressTrack.Position=progressFill.Position=layout.ProgressTrack.Position;
   progressTrack.Size=layout.ProgressTrack.Size;
   progressFill.Size=new(layout.ProgressTrack.Size.X*progress.Fraction,layout.ProgressTrack.Size.Y);
  }
  if(hasIcon&&icon!=screen.Icon){try{portrait.IconId=screen.Icon;icon=screen.Icon;}catch(Exception e){icon=screen.Icon;Service.PluginLog.Debug(e,"Портрет нативной цели недоступен");hasIcon=false;}}
  var texture=hasIcon&&portrait.IsTextureReady?portrait.ActualTextureSize:Vector2.Zero;
  var image=layout.Portrait.FitImage(texture);
  portrait.IsVisible=image.Size.X>0&&image.Size.Y>0;
  if(portrait.IsVisible){
   portrait.TextureSize=texture;
   portrait.Position=image.Position;
   portrait.Size=image.Size;
  }
  pageLabel.AlignmentType=AlignmentType.Center;pageLabel.IsVisible=layout.PageText.Length>0;pageLabel.Position=layout.PageLabel.Position;pageLabel.Size=layout.PageLabel.Size;pageLabel.String=layout.PageText;
  string identity=$"{controller.Page}:{string.Join('|',layout.Buttons.Select(b=>b.Button.Key))}";
  bool changed=screenIdentity!=identity;screenIdentity=identity;
  var used=new bool[buttons.Length];var items=new List<NativeNavItem>();
  foreach(var placement in layout.Buttons){
   int slot=placement.Slot;var spec=placement.Button;var button=buttons[slot]!;used[slot]=true;
   if(spec.Checked is {} value&&checks[slot] is {} check){
    button.IsVisible=false;captions[slot]!.IsVisible=false;check.IsVisible=true;check.IsEnabled=spec.Enabled;
    if(check.Size!=placement.Rect.Size)check.Size=placement.Rect.Size;
    check.Position=placement.Rect.Position;
    check.Label.TextColor=ownsFocus&&focusedKey==spec.Key?NativeHuntColors.Attention:Vector4.One;
    check.Label.Position=new(24,0);check.Label.Width=check.Width-28;
    if(check.Label.String.ToString()!=spec.Label)check.Label.String=spec.Label;
    if(check.IsChecked!=value){
     var onClick=check.OnClick;check.OnClick=null;
     check.IsChecked=value;check.OnClick=onClick;
    }
   }else{
    if(checks[slot] is {} hidden)hidden.IsVisible=false;
    if(button.Position!=placement.Rect.Position)button.Position=placement.Rect.Position;
    if(button.Size!=placement.Rect.Size)button.Size=placement.Rect.Size;
    button.String="";
    var caption=captions[slot]!;
    NativeButtonText.SetCaption(caption,button,spec.Board!=null||spec.Goal!=null?"":spec.Label,
     !spec.Enabled?NativeHuntColors.Muted:ownsFocus&&focusedKey==spec.Key?NativeHuntColors.Attention:spec.LabelColor??(spec.Emphasized?NativeHuntColors.Attention:Vector4.One));
    caption.IsVisible=spec.Board==null&&spec.Goal==null;
    if(spec.Key=="number:gil"&&gilIcon!=null&&spec.Label.EndsWith(" гил",StringComparison.Ordinal))
     GilDisplay.Place(gilIcon,caption,spec.Label[..^4],button.Position,button.Size);
    button.BackgroundNode.Color=spec.Key=="primary"&&spec.Emphasized?NativeHuntColors.PrimarySurface:Vector4.One;
    button.LabelNode.TextColor=spec.LabelColor??(spec.Emphasized?NativeHuntColors.Attention:Vector4.One);
    button.IsVisible=true;button.IsEnabled=spec.Enabled;
    button.BackgroundNode.IsVisible=spec.Board==null&&spec.Goal==null;
    button.LabelNode.AlignmentType=spec.Board==null?AlignmentType.Center:AlignmentType.Left;
   }
   keys[slot]=spec.Key;commands[slot]=spec.Action;items.Add(new(slot+1,placement.Row,placement.Column,spec.Key,spec.Enabled));
  }
  for(int i=0;i<buttons.Length;i++)if(!used[i]){buttons[i]!.IsVisible=false;captions[i]!.IsVisible=false;if(checks[i] is {} check)check.IsVisible=false;keys[i]=null;commands[i]=null;}
  for(int i=0;i<tableCells.Length;i++){
   var node=tableCells[i]!;
   node.IsVisible=i<layout.TableText.Count;
   if(!node.IsVisible)continue;
   var cell=layout.TableText[i];
   node.Position=cell.Rect.Position;node.Size=cell.Rect.Size;node.TextColor=cell.Color;node.FontSize=cell.Font;node.AlignmentType=cell.RightAligned?AlignmentType.Right:AlignmentType.Left;
   if(node.String.ToString()!=cell.Label)node.String=cell.Label;
  }
  var focused=layout.Buttons.FirstOrDefault(p=>p.Button.Enabled&&IsFocused(Control(p.Slot),global));
  links=NativeHuntNavigation.Build(items);foreach(var link in links){var b=Control(link.Index-1)!;b.NavIndex=link.Index;b.NavUp=link.Up;b.NavDown=link.Down;b.NavLeft=link.Left;b.NavRight=link.Right;}
  if(focusedKey==null&&controller.Page==NativeHuntPage.Hunt&&links.Any(n=>n.Key=="primary"))focusedKey="primary";
  if(focusFirstBody){focusedKey=items.FirstOrDefault(n=>n.Row>0&&n.Enabled&&n.Key is not ("filters" or "previous" or "next-page" or "back" or "close")).Key;focusFirstBody=false;}
  if(ownsFocus&&(changed||links.All(n=>n.Key!=focusedKey)))focusRequested=true;
  if(Size!=layout.Size)SetWindowSize(layout.Size);
  FitViewport(native);
 }
}


// Shared border for native controls. It leaves the control's surface and text visible.
internal static class NativeFixedLayout {
 internal static NodeFlags FixedFlags(NodeFlags flags)=>
  (flags&~(NodeFlags.AnchorTop|NodeFlags.AnchorBottom|NodeFlags.AnchorLeft|NodeFlags.AnchorRight))
   |NodeFlags.AnchorTop|NodeFlags.AnchorLeft;
 internal static void Attach(NativeAddon owner,NodeBase node){
  // The layout already computes absolute positions. Game window resizing must
  // not center, stretch or translate these nodes a second time.
  node.NodeFlags=FixedFlags(node.NodeFlags);
  owner.AddNode(node);
 }
}

internal static class NativeButtonText {
 internal static float Measure(TextNode node,string label){
  return Math.Max(node.GetTextDrawSize(label,considerScale:false).X,label.EnumerateRunes().Count()*9.5f);
 }
 internal static TextNode CreateCaption()=>new(){
  FontSize=NativeHuntLayout.ButtonFont,LineSpacing=18,
  AlignmentType=AlignmentType.TopLeft,IsVisible=false
 };
 internal static void SetCaption(TextNode caption,TextButtonNode button,string text,Vector4 color){
  caption.Size=new(4096,64);
  var rect=NativeHuntLayout.ButtonCaption(button.Position,button.Size,caption.GetTextDrawSize(text,considerScale:false));
  caption.Position=rect.Position;
  caption.Size=rect.Size;
  caption.TextColor=color;
  caption.String=text;
 }
}
