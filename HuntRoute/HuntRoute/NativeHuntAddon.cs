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
 private TextNode? heading,summary,pageLabel;
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
  heading=new TextNode{Position=new(24,90),Size=new(592,30),FontSize=20,String="Hunt Route",TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};AddNode(heading);
  summary=new TextNode{Position=new(24,128),Size=new(592,110),FontSize=15,LineSpacing=19,TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};AddNode(summary);
  portrait=new IconImageNode{Position=new(496,128),Size=new(112,112),IsVisible=false};AddNode(portrait);
  pageLabel=new TextNode{Size=new(240,24),FontSize=14};AddNode(pageLabel);
  for(int i=0;i<buttons.Length;i++){
   int slot=i;var button=new TextButtonNode{Size=new(592,40),NavIndex=i+1,OnClick=()=>Clicked(slot)};
   button.LabelNode.FontSize=16;button.LabelNode.LineSpacing=18;button.LabelNode.TextFlags=TextFlags.WordWrap|TextFlags.MultiLine;
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
 protected override void OnFinalize(AtkUnitBase* native){Array.Clear(buttons);heading=null;summary=null;portrait=null;pageLabel=null;links=[];}
 private void Clicked(int slot){
  if(modal()||slot<0||slot>=buttons.Length||buttons[slot] is not {IsEnabled:true}||commands[slot] is not {} command)return;
  focusedKey=keys[slot];
  if(command.Kind=="previous"||command.Kind=="next-page"){controller.Turn(command.Delta,controller.Build(mini).Buttons.Count);focusFirstBody=true;focusRequested=true;}
  else if(command.Kind=="close")Close();
  else execute(command);
  nextUpdate=0;
 }
 private void Refresh(AtkUnitBase* native){
  if(heading==null||summary==null||portrait==null||pageLabel==null)return;
  // Do not steal focus from the map or another game window on passive updates.
  bool ownsFocus=false;var global=AtkStage.Instance()->AtkInputManager->FocusedNode;
  for(int i=0;i<buttons.Length;i++)if(buttons[i] is {} b&&(global==(AtkResNode*)b||global==(AtkResNode*)b.FocusNode)){focusedKey=keys[i];ownsFocus=true;}
  var screen=controller.Build(mini);
  bool hasIcon=!mini&&screen.Icon!=0;float inner=mini?412:592;
  string title=NativeHuntLayout.FitLabel(screen.Title,110),description=NativeHuntLayout.FitLabel(screen.Summary,420);
  if(titleText!=title){heading.String=title;titleText=title;}
  if(summaryText!=description){summary.String=description;summaryText=description;}
  heading.Size=new(inner,80);summary.Size=new(hasIcon?456:inner,200);
  var layout=NativeHuntLayout.Build(screen,controller.Page,controller.Offset,heading.GetTextDrawSize(considerScale:false).Y,summary.GetTextDrawSize(considerScale:false).Y,mini);
  heading.Position=layout.Heading.Position;heading.Size=layout.Heading.Size;
  summary.Position=layout.Summary.Position;summary.Size=layout.Summary.Size;
  portrait.Position=layout.Portrait.Position;
  if(hasIcon&&icon!=screen.Icon){try{portrait.IconId=screen.Icon;icon=screen.Icon;}catch(Exception e){icon=screen.Icon;Service.PluginLog.Debug(e,"Портрет нативной цели недоступен");hasIcon=false;}}
  portrait.IsVisible=hasIcon&&portrait.IsTextureReady;
  if(portrait.IsVisible){var texture=portrait.ActualTextureSize;if(texture.X>0&&texture.Y>0){portrait.TextureSize=texture;portrait.Size=texture*Math.Min(112/texture.X,112/texture.Y);}}
  pageLabel.IsVisible=layout.PageText.Length>0;pageLabel.Position=layout.PageLabel.Position;pageLabel.String=layout.PageText;
  string identity=$"{controller.Page}:{string.Join('|',layout.Buttons.Select(b=>b.Button.Key))}";
  bool changed=screenIdentity!=identity;screenIdentity=identity;
  var used=new bool[buttons.Length];var items=new List<NativeNavItem>();
  foreach(var placement in layout.Buttons){
   int slot=placement.Slot;var spec=placement.Button;var button=buttons[slot]!;used[slot]=true;
   if(button.Position!=placement.Rect.Position)button.Position=placement.Rect.Position;
   if(button.Size!=placement.Rect.Size)button.Size=placement.Rect.Size;
   if(buttonText[slot]!=spec.Label){button.LabelNode.FontSize=16;button.LabelNode.LineSpacing=18;button.String=spec.Label;if(button.LabelNode.GetTextDrawSize(considerScale:false).Y>button.LabelNode.Height){button.LabelNode.FontSize=14;button.LabelNode.LineSpacing=16;}buttonText[slot]=spec.Label;}
   button.IsVisible=true;button.IsEnabled=spec.Enabled;
   keys[slot]=spec.Key;commands[slot]=spec.Action;items.Add(new(slot+1,placement.Row,placement.Column,spec.Key,spec.Enabled));
  }
  for(int i=0;i<buttons.Length;i++)if(!used[i]){buttons[i]!.IsVisible=false;keys[i]=null;commands[i]=null;}
  links=NativeHuntNavigation.Build(items);foreach(var link in links){var b=buttons[link.Index-1]!;b.NavIndex=link.Index;b.NavUp=link.Up;b.NavDown=link.Down;b.NavLeft=link.Left;b.NavRight=link.Right;}
  if(focusFirstBody){focusedKey=items.FirstOrDefault(n=>n.Row is >0 and <=6&&n.Enabled).Key;focusFirstBody=false;}
  if(ownsFocus&&(changed||links.All(n=>n.Key!=focusedKey)))focusRequested=true;
  if(Size!=layout.Size)SetWindowSize(layout.Size);
 }
}
