using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
namespace HuntRoute;

internal sealed unsafe class NativeHuntAddon(NativeHuntController controller,Action<NativeHuntAction> execute,Action closed,Func<bool> modal,bool mini=false):NativeAddon {
 private readonly TextButtonNode?[] buttons=new TextButtonNode?[NativeHuntLayout.ButtonCapacity];
 private string?[] keys=new string?[NativeHuntLayout.ButtonCapacity];
 private NativeHuntAction?[] commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];
 private TextNode? heading,summary,pageLabel,progressLabel;
 private ColorImageNode? progressTrack,progressFill;
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
  foreach(var button in buttons)if(button!=null&&(node==(AtkResNode*)button||node==(AtkResNode*)button.FocusNode))return true;
  return false;
 }
 protected override void OnSetup(AtkUnitBase* native,Span<AtkValue> values){
  if(WindowNode is WindowNode window){window.ShowConfigButton=false;window.ShowHelpButton=false;}
  if(mini)native->DisableFocusOnShow=true;
  heading=new TextNode{FontSize=NativeHuntLayout.TitleFont,String="Hunt Route",TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};AddNode(heading);
  summary=new TextNode{FontSize=NativeHuntLayout.BodyFont,LineSpacing=16,TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};AddNode(summary);
  portrait=new IconImageNode{FitTexture=true,IsVisible=false};AddNode(portrait);
  progressLabel=new TextNode{FontSize=NativeHuntLayout.BodyFont,TextColor=new(.68f,.88f,.80f,1)};AddNode(progressLabel);
  progressTrack=new ColorImageNode{Color=new(.24f,.27f,.28f,1)};AddNode(progressTrack);
  progressFill=new ColorImageNode{Color=new(.38f,.76f,.64f,1)};AddNode(progressFill);
  pageLabel=new TextNode{Size=new(240,24),FontSize=NativeHuntLayout.BodyFont};AddNode(pageLabel);
  for(int i=0;i<buttons.Length;i++){
   int slot=i;var button=new TextButtonNode{NavIndex=i+1,OnClick=()=>Clicked(slot)};
   button.LabelNode.FontSize=NativeHuntLayout.ButtonFont;button.LabelNode.LineSpacing=16;button.LabelNode.TextFlags=TextFlags.WordWrap|TextFlags.MultiLine;
   buttons[i]=button;AddNode(button);
  }
  screenIdentity=null;keys=new string?[NativeHuntLayout.ButtonCapacity];commands=new NativeHuntAction?[NativeHuntLayout.ButtonCapacity];nextUpdate=0;focusRequested=!mini;focusFirstBody=false;icon=0;titleText=summaryText=null;Array.Clear(buttonText);
  Refresh(native);
 }
 protected override void OnShow(AtkUnitBase* native){if(!mini)focusRequested=true;}
 protected override void OnUpdate(AtkUnitBase* native){
  if(Environment.TickCount64>=nextUpdate){nextUpdate=Environment.TickCount64+200;Refresh(native);}
  if(focusRequested&&IsOpen&&!modal()){int index=NativeHuntNavigation.RestoreFocus(links,focusedKey);if(index>0)buttons[index-1]?.SetFocus();focusRequested=false;}
 }
 protected override void OnHide(AtkUnitBase* native)=>closed();
 protected override void OnFinalize(AtkUnitBase* native){Array.Clear(buttons);heading=null;summary=null;portrait=null;pageLabel=progressLabel=null;progressTrack=progressFill=null;links=[];}
 private void Clicked(int slot){
  if(modal()||slot<0||slot>=buttons.Length||buttons[slot] is not {IsEnabled:true}||commands[slot] is not {} command)return;
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
  for(int i=0;i<buttons.Length;i++)if(buttons[i] is {} b&&(global==(AtkResNode*)b||global==(AtkResNode*)b.FocusNode)){focusedKey=keys[i];ownsFocus=true;}
  var screen=controller.Build(mini);
  bool hasIcon=!mini&&screen.Icon!=0;float inner=NativeHuntLayout.ContentWidth(mini);
  string title=NativeHuntLayout.FitLabel(screen.Title,110),description=NativeHuntLayout.FitLabel(screen.Summary,420);
  if(titleText!=title){heading.String=title;titleText=title;}
  if(summaryText!=description){summary.String=description;summaryText=description;}
  heading.Size=new(inner,110);summary.Size=new(NativeHuntLayout.SummaryWidth(screen,mini),200);
  var layout=NativeHuntLayout.Build(screen,controller.Page,controller.Offset,heading.GetTextDrawSize(considerScale:false).Y,summary.GetTextDrawSize(considerScale:false).Y,mini);
  heading.Position=layout.Heading.Position;heading.Size=layout.Heading.Size;
  summary.Position=layout.Summary.Position;summary.Size=layout.Summary.Size;
  var progress=screen.Progress;
  progressLabel.IsVisible=progressTrack.IsVisible=progress!=null;
  progressFill.IsVisible=progress is {Fraction:>0};
  if(progress!=null){
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
  pageLabel.IsVisible=layout.PageText.Length>0;pageLabel.Position=layout.PageLabel.Position;pageLabel.String=layout.PageText;
  string identity=$"{controller.Page}:{string.Join('|',layout.Buttons.Select(b=>b.Button.Key))}";
  bool changed=screenIdentity!=identity;screenIdentity=identity;
  var used=new bool[buttons.Length];var items=new List<NativeNavItem>();
  foreach(var placement in layout.Buttons){
   int slot=placement.Slot;var spec=placement.Button;var button=buttons[slot]!;used[slot]=true;
   if(button.Position!=placement.Rect.Position)button.Position=placement.Rect.Position;
   if(button.Size!=placement.Rect.Size){button.Size=placement.Rect.Size;buttonText[slot]=null;}
   button.LabelNode.TextColor=spec.Emphasized?new(1,.85f,.55f,1):Vector4.One;
   if(buttonText[slot]!=spec.Label){button.LabelNode.FontSize=spec.Label.Contains('\n')?NativeHuntLayout.BodyFont:NativeHuntLayout.ButtonFont;button.LabelNode.LineSpacing=16;button.String=spec.Label;if(button.LabelNode.GetTextDrawSize(considerScale:false).Y>button.LabelNode.Height){button.LabelNode.FontSize=NativeHuntLayout.BodyFont;button.LabelNode.LineSpacing=16;}buttonText[slot]=spec.Label;}
   button.IsVisible=true;button.IsEnabled=spec.Enabled;
   keys[slot]=spec.Key;commands[slot]=spec.Action;items.Add(new(slot+1,placement.Row,placement.Column,spec.Key,spec.Enabled));
  }
  for(int i=0;i<buttons.Length;i++)if(!used[i]){buttons[i]!.IsVisible=false;keys[i]=null;commands[i]=null;}
  links=NativeHuntNavigation.Build(items);foreach(var link in links){var b=buttons[link.Index-1]!;b.NavIndex=link.Index;b.NavUp=link.Up;b.NavDown=link.Down;b.NavLeft=link.Left;b.NavRight=link.Right;}
  if(focusedKey==null&&controller.Page==NativeHuntPage.Hunt&&links.Any(n=>n.Key=="primary"))focusedKey="primary";
  if(focusFirstBody){focusedKey=items.FirstOrDefault(n=>n.Row is >0 and <=6&&n.Enabled).Key;focusFirstBody=false;}
  if(ownsFocus&&(changed||links.All(n=>n.Key!=focusedKey)))focusRequested=true;
  if(Size!=layout.Size)SetWindowSize(layout.Size);
 }
}
