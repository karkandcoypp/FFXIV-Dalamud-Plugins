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
 private readonly TextButtonNode?[] buttons=new TextButtonNode?[NativeHuntLayout.ButtonCapacity];
 private readonly CheckboxNode?[] checks=new CheckboxNode?[NativeHuntLayout.ButtonCapacity];
 private ComponentNode? Control(int slot)=>checks[slot] is {IsVisible:true} check?check:buttons[slot];
 private static bool IsFocused(ComponentNode? node,AtkResNode* focus)=>node!=null&&focus!=null&&(focus==(AtkResNode*)node||focus==(AtkResNode*)node.FocusNode);
 private string?[] keys=new string?[NativeHuntLayout.ButtonCapacity];
 private NativeHuntAction?[] commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];
 private TextNode? heading,summary,pageLabel,progressLabel;
 private ColorImageNode? progressTrack,progressFill;
 private NativeFocusOutline? focusOutline;
 private readonly TextNode?[] tableCells=new TextNode?[NativeHuntLayout.ButtonCapacity*3];
 private IconImageNode? portrait;
 private IReadOnlyList<NativeNavLink> links=[];
 private bool focusRequested,focusFirstBody;
 private string? titleText,summaryText;
 private readonly string?[] buttonText=new string?[NativeHuntLayout.ButtonCapacity];
 private string? focusedKey;
 private string? screenIdentity;
 private uint icon;
 private long nextUpdate;
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
  heading=new TextNode{FontSize=NativeHuntLayout.TitleFont,String="Hunt Route"};heading.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);AddNode(heading);
  summary=new TextNode{FontSize=NativeHuntLayout.BodyFont,LineSpacing=18};summary.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);AddNode(summary);
  portrait=new IconImageNode{FitTexture=true,IsVisible=false};AddNode(portrait);
  progressLabel=new TextNode{FontSize=NativeHuntLayout.BodyFont,TextColor=new(.68f,.88f,.80f,1)};AddNode(progressLabel);
  progressTrack=new ColorImageNode{Color=new(.24f,.27f,.28f,1)};AddNode(progressTrack);
  progressFill=new ColorImageNode{Color=new(.38f,.76f,.64f,1)};AddNode(progressFill);
  pageLabel=new TextNode{Size=new(240,24),FontSize=NativeHuntLayout.BodyFont};AddNode(pageLabel);

  for(int i=0;i<buttons.Length;i++){
   int slot=i;var button=new TextButtonNode{NavIndex=i+1,OnClick=()=>Clicked(slot)};
   button.LabelNode.FontSize=NativeHuntLayout.ButtonFont;button.LabelNode.LineSpacing=18;button.LabelNode.AddTextFlags(TextFlags.WordWrap|TextFlags.MultiLine);
   buttons[i]=button;AddNode(button);
   if(i is >=3 and <=8){
    var check=new CheckboxNode{NavIndex=i+1,DisableAutoResize=true,IsVisible=false,OnClick=_=>Clicked(slot)};
    checks[i]=check;AddNode(check);
   }
  }
  for(int i=0;i<tableCells.Length;i++){
   var cell=new TextNode{FontSize=NativeHuntLayout.ButtonFont,IsVisible=false};
   cell.AddTextFlags(TextFlags.Ellipsis);tableCells[i]=cell;AddNode(cell);
  }
  focusOutline=new NativeFocusOutline(node=>AddNode(node));
  screenIdentity=null;keys=new string?[NativeHuntLayout.ButtonCapacity];commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];nextUpdate=0;focusRequested=!mini;focusFirstBody=false;icon=0;titleText=summaryText=null;Array.Clear(buttonText);
  Refresh(native);
 }
 protected override void OnShow(AtkUnitBase* native){if(!mini)focusRequested=true;}
 protected override void OnUpdate(AtkUnitBase* native){
  if(Environment.TickCount64>=nextUpdate){nextUpdate=Environment.TickCount64+200;Refresh(native);}
  if(focusRequested&&IsOpen&&!modal()){int index=NativeHuntNavigation.RestoreFocus(links,focusedKey);if(index>0){if(checks[index-1] is {IsVisible:true} check)check.SetFocus();else buttons[index-1]?.SetFocus();}focusRequested=false;}
 }
 protected override void OnHide(AtkUnitBase* native)=>closed();
 protected override void OnFinalize(AtkUnitBase* native){Array.Clear(buttons);Array.Clear(checks);Array.Clear(tableCells);focusOutline=null;heading=null;summary=null;portrait=null;pageLabel=progressLabel=null;progressTrack=progressFill=null;links=[];}
 private void Clicked(int slot){
  if(modal()||slot<0||slot>=buttons.Length||!(checks[slot] is {IsVisible:true} check?check.IsEnabled:buttons[slot] is {IsVisible:true,IsEnabled:true})||commands[slot] is not {} command)return;
  focusedKey=keys[slot];
  if(command.Kind=="previous"||command.Kind=="next-page"){controller.Turn(command.Delta,controller.Build(mini).Buttons.Count);focusFirstBody=true;focusRequested=true;}
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
  bool hasIcon=!mini&&screen.Icon!=0;float inner=NativeHuntLayout.ContentWidth(mini);
  string title=NativeHuntLayout.FitLabel(screen.Title,110),description=NativeHuntLayout.FitLabel(screen.Summary,420);
  if(titleText!=title){heading.String=title;titleText=title;}
  if(summaryText!=description){summary.String=description;summaryText=description;}
  summary.FontSize=NativeHuntLayout.SummaryFont(controller.Page,mini);
  summary.LineSpacing=summary.FontSize+3;
  heading.Size=new(inner,110);summary.Size=new(NativeHuntLayout.SummaryWidth(screen,mini),200);
  var layout=NativeHuntLayout.Build(screen,controller.Page,controller.Offset,heading.GetTextDrawSize(considerScale:false).Y,summary.GetTextDrawSize(considerScale:false).Y,mini);
  heading.TextColor=screen.Progress!=null?NativeHuntColors.Tracking:Vector4.One;
  summary.TextColor=screen.SummaryColor??NativeHuntColors.Muted;
  heading.Position=layout.Heading.Position;heading.Size=layout.Heading.Size;
  summary.Position=layout.Summary.Position;summary.Size=layout.Summary.Size;
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
    button.IsVisible=false;check.IsVisible=true;check.IsEnabled=spec.Enabled;
    if(check.Size!=placement.Rect.Size)check.Size=placement.Rect.Size;
    check.Position=placement.Rect.Position;
    check.Label.Position=new(8,0);check.Label.Width=check.Width-40;
    check.BoxForeground.Position=check.BoxBackground.Position=new(check.Width-28,2);
    if(check.Label.String.ToString()!=spec.Label)check.Label.String=spec.Label;
    if(check.IsChecked!=value){
     var onClick=check.OnClick;check.OnClick=null;
     check.IsChecked=value;check.OnClick=onClick;
    }
   }else{
    if(checks[slot] is {} hidden)hidden.IsVisible=false;
    if(button.Position!=placement.Rect.Position)button.Position=placement.Rect.Position;
    if(button.Size!=placement.Rect.Size){button.Size=placement.Rect.Size;buttonText[slot]=null;}
    button.LabelNode.X=8;button.LabelNode.Width=button.Width-16;
    button.BackgroundNode.Color=spec.Key=="primary"?NativeHuntColors.PrimarySurface:Vector4.One;
    button.LabelNode.TextColor=spec.LabelColor??(spec.Emphasized?NativeHuntColors.Attention:Vector4.One);
    if(buttonText[slot]!=spec.Label){button.LabelNode.FontSize=spec.Label.Contains('\n')?NativeHuntLayout.BodyFont:NativeHuntLayout.ButtonFont;button.LabelNode.LineSpacing=18;button.String=spec.Board!=null||spec.Goal!=null?"":spec.Label;if(button.LabelNode.GetTextDrawSize(considerScale:false).Y>button.LabelNode.Height){button.LabelNode.FontSize=NativeHuntLayout.BodyFont;button.LabelNode.LineSpacing=18;}buttonText[slot]=spec.Label;}
    button.IsVisible=true;button.IsEnabled=spec.Enabled;
    button.BackgroundNode.IsVisible=spec.Board==null&&spec.Goal==null;
    button.LabelNode.AlignmentType=spec.Board==null?AlignmentType.Center:AlignmentType.Left;
   }
   keys[slot]=spec.Key;commands[slot]=spec.Action;items.Add(new(slot+1,placement.Row,placement.Column,spec.Key,spec.Enabled));
  }
  for(int i=0;i<buttons.Length;i++)if(!used[i]){buttons[i]!.IsVisible=false;if(checks[i] is {} check)check.IsVisible=false;keys[i]=null;commands[i]=null;}
  for(int i=0;i<tableCells.Length;i++){
   var node=tableCells[i]!;
   node.IsVisible=i<layout.TableText.Count;
   if(!node.IsVisible)continue;
   var cell=layout.TableText[i];
   node.Position=cell.Rect.Position;node.Size=cell.Rect.Size;node.TextColor=cell.Color;node.FontSize=cell.Font;
   if(node.String.ToString()!=cell.Label)node.String=cell.Label;
  }
  var focused=layout.Buttons.FirstOrDefault(p=>p.Button.Enabled&&IsFocused(Control(p.Slot),global));
  focusOutline?.Show(focused?.Rect);
  links=NativeHuntNavigation.Build(items);foreach(var link in links){var b=Control(link.Index-1)!;b.NavIndex=link.Index;b.NavUp=link.Up;b.NavDown=link.Down;b.NavLeft=link.Left;b.NavRight=link.Right;}
  if(focusedKey==null&&controller.Page==NativeHuntPage.Hunt&&links.Any(n=>n.Key=="primary"))focusedKey="primary";
  if(focusFirstBody){focusedKey=items.FirstOrDefault(n=>n.Row is >0 and <=6&&n.Enabled).Key;focusFirstBody=false;}
  if(ownsFocus&&(changed||links.All(n=>n.Key!=focusedKey)))focusRequested=true;
  if(Size!=layout.Size)SetWindowSize(layout.Size);
 }
}


// Shared border for native controls. It leaves the control's surface and text visible.
internal sealed class NativeFocusOutline {
 private readonly ColorImageNode[] edges=new ColorImageNode[4];
 internal NativeFocusOutline(Action<ColorImageNode> attach){
  for(int i=0;i<edges.Length;i++){
   var edge=new ColorImageNode{Color=NativeHuntColors.Focus,IsVisible=false};
   edges[i]=edge;attach(edge);
  }
 }
 internal void Show(NativeHuntRect? rect){
  foreach(var edge in edges)edge.IsVisible=rect.HasValue;
  if(rect is not {} box)return;
  const float stroke=2;
  var p=box.Position;var s=box.Size;
  Place(0,p,new(s.X,stroke));
  Place(1,p+new Vector2(0,s.Y-stroke),new(s.X,stroke));
  Place(2,p,new(stroke,s.Y));
  Place(3,p+new Vector2(s.X-stroke,0),new(stroke,s.Y));
 }
 private void Place(int index,Vector2 position,Vector2 size){
  edges[index].Position=position;edges[index].Size=size;
 }
}
