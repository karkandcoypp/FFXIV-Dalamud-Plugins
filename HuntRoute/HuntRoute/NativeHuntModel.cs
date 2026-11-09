using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
namespace HuntRoute;

public enum NativeHuntPage { Hunt,Targets,Boards,Settings,Target,Actions,Advanced,Filters,Expansions,Zones,Number }
public sealed record NativeHuntAction(string Kind,string Argument="",int Delta=0);
public sealed record NativeHuntButton(string Key,string Label,NativeHuntAction Action,bool Enabled=true,bool Emphasized=false,NativeBoardRow? Board=null,Vector4? LabelColor=null);
public sealed record NativeBoardStatus(string Label,Vector4? Color=null);
public sealed record NativeBoardRow(string Name,NativeBoardStatus Daily,NativeBoardStatus Weekly){
 public const string DailyPrefix="Ежедневные: ", WeeklyPrefix="Еженедельный: ";
 public string Label=>$"{Name}\n{DailyPrefix}{Daily.Label}\n{WeeklyPrefix}{Weekly.Label}";
}
public sealed record NativeHuntScreen(string Title,string Summary,IReadOnlyList<NativeHuntButton> Buttons,uint Icon=0,NativeHuntProgress? Progress=null,Vector4? SummaryColor=null);
public static class NativeHuntColors {
 public static readonly Vector4 Ready=new(.50f,1,.64f,1), Attention=new(1,.83f,.48f,1), Tracking=new(.62f,.84f,1,1), Muted=new(.78f,.81f,.85f,1);
}
public sealed record NativeHuntProgress(int Kills,uint Required){
 public string Label=>$"Побеждено  {Kills} / {Required}";
 public float Fraction=>Required>0?Math.Clamp((float)Kills/Required,0,1):0;
}
public readonly record struct NativeNavItem(int Index,int Row,int Column,string Key,bool Enabled=true,bool Visible=true);
public readonly record struct NativeNavLink(int Index,int Up,int Down,int Left,int Right,string Key);
public static class NativeHuntNavigation {
 public const int PageSize=6;
 public static int ClampPage(int page,int count)=>Math.Clamp(page,0,Math.Max(0,(count-1)/PageSize));
 public static NativeNavLink[] Build(IEnumerable<NativeNavItem> source){
  var items=source.Where(n=>n.Visible&&n.Enabled).OrderBy(n=>n.Row).ThenBy(n=>n.Column).ThenBy(n=>n.Index).ToArray();
  if(items.Any(n=>n.Index is <1 or >254)||items.Select(n=>n.Index).Distinct().Count()!=items.Length||items.Select(n=>n.Key).Distinct().Count()!=items.Length)throw new ArgumentException("Invalid native navigation identities");
  int Vertical(NativeNavItem item,bool down){
   var rows=items.Select(n=>n.Row).Distinct().Order().ToArray();int at=Array.IndexOf(rows,item.Row);int next=rows[(at+(down?1:rows.Length-1))%rows.Length];
   return items.Where(n=>n.Row==next).OrderBy(n=>Math.Abs(n.Column-item.Column)).ThenBy(n=>n.Index).First().Index;
  }
  int Horizontal(NativeNavItem item,bool right){var row=items.Where(n=>n.Row==item.Row).ToArray();int at=Array.FindIndex(row,n=>n.Index==item.Index);return row[(at+(right?1:row.Length-1))%row.Length].Index;}
  return items.Select(n=>new NativeNavLink(n.Index,Vertical(n,false),Vertical(n,true),Horizontal(n,false),Horizontal(n,true),n.Key)).ToArray();
 }
 public static int RestoreFocus(IReadOnlyList<NativeNavLink> links,string? key){int match=links.FirstOrDefault(n=>n.Key==key).Index;return match>0?match:links.Count==0?0:links[0].Index;}
}

public readonly record struct NativeHuntRect(Vector2 Position,Vector2 Size){
 public NativeHuntRect FitImage(Vector2 texture){
  if(!float.IsFinite(texture.X)||!float.IsFinite(texture.Y)||texture.X<=0||texture.Y<=0)return new(Position,Vector2.Zero);
  var fitted=texture*Math.Min(Size.X/texture.X,Size.Y/texture.Y);
  return new(Position+(Size-fitted)/2,fitted);
 }
}
public sealed record NativeHuntPlacement(int Slot,int Row,int Column,NativeHuntButton Button,NativeHuntRect Rect);
public sealed record NativeHuntLayout(Vector2 Size,NativeHuntRect Heading,NativeHuntRect Summary,NativeHuntRect Portrait,NativeHuntRect ProgressLabel,NativeHuntRect ProgressTrack,NativeHuntRect PageLabel,string PageText,IReadOnlyList<NativeHuntPlacement> Buttons){
 public const int ButtonCapacity=13;
 private const float Margin=16,Gap=6;
 public const float ActionHeight=28, ActionWidth=112, LongActionWidth=164;
 public const float PortraitSize=68;
 // Axis sizes from the stock MobHunt text and ButtonA component.
 public const byte TitleFont=18, BodyFont=14, ButtonFont=12;
 public static float ContentWidth(bool mini)=>(mini?340:420)-2*Margin;
 public static float SummaryWidth(NativeHuntScreen screen,bool mini)=>ContentWidth(mini)-(!mini&&screen.Icon!=0?PortraitSize+Gap:0);
 private static bool IsSecondary(NativeHuntButton button)=>button.Key is "primary" or "map" or "actions" or "targets" or "message";
 public static string FitLabel(string value,int length=64)=>value.Length<=length?value:string.Concat(value.EnumerateRunes().Take(length-1).Select(r=>r.ToString()))+"…";
 public static NativeHuntLayout Build(NativeHuntScreen screen,NativeHuntPage view,int requestedPage,float titleHeight,float summaryHeight,bool mini=false){
  float inner=ContentWidth(mini),width=inner+2*Margin,titleY=mini?38:71;
  titleHeight=float.IsFinite(titleHeight)?Math.Clamp(titleHeight,22,110):22;
  summaryHeight=string.IsNullOrEmpty(screen.Summary)?0:float.IsFinite(summaryHeight)?Math.Clamp(summaryHeight,17,190):17;
  float summaryY=titleY+titleHeight+4;bool portrait=!mini&&screen.Icon!=0;
  float textWidth=SummaryWidth(screen,mini);
  float progressY=summaryY+summaryHeight+4;
  float bodyY=summaryY+Math.Max(portrait?PortraitSize:0,summaryHeight+(screen.Progress!=null?30:0))+7;
  int page=mini?0:NativeHuntNavigation.ClampPage(requestedPage,screen.Buttons.Count);
  int pageCount=Math.Max(1,(screen.Buttons.Count+5)/6);
  var visible=screen.Buttons.Skip(page*6).Take(mini?3:6).ToArray();
  if(!mini&&view==NativeHuntPage.Hunt&&visible is [{Key:"primary"},{Key:"map"},{Key:"actions"},{Key:"targets"},..])
   (visible[1],visible[2],visible[3])=(visible[3],visible[1],visible[2]);var buttons=new List<NativeHuntPlacement>();
  void Set(int slot,int row,int col,NativeHuntButton button,float x,float y,float w,float h)=>buttons.Add(new(slot,row,col,button with {Label=button.Board==null?FitLabel(button.Label):button.Label},new(new(x,y),new(w,h))));
  NativeHuntButton Page(string key,string label,NativeHuntPage p)=>new(key,label,new("page",p.ToString()),Emphasized:key.StartsWith("tab:",StringComparison.Ordinal)&&p==(view switch {NativeHuntPage.Boards=>NativeHuntPage.Boards,NativeHuntPage.Settings or NativeHuntPage.Advanced or NativeHuntPage.Number=>NativeHuntPage.Settings,_=>NativeHuntPage.Hunt}));
  if(!mini){
   Set(0,0,0,Page("tab:hunt","Охота",NativeHuntPage.Hunt),Margin,37,84,ActionHeight);
   Set(1,0,1,Page("tab:boards","Доски",NativeHuntPage.Boards),Margin+90,37,84,ActionHeight);
   Set(2,0,2,Page("tab:settings","Настройки",NativeHuntPage.Settings),Margin+180,37,112,ActionHeight);
  }
  int rows=0;
  float bottom=bodyY;
  for(int i=0;i<visible.Length;i++){
   var button=visible[i];
   bool pair=(mini||view==NativeHuntPage.Hunt)&&(!mini||button.Key!="primary")&&i+1<visible.Length&&IsSecondary(button)&&IsSecondary(visible[i+1]);
   float h=button.Board!=null?64:button.Label.Length>54?56:button.Label.Contains('\n')||button.Label.Length>40?44:ActionHeight;
   float CompactWidth(NativeHuntButton item)=>item.Label.Length>13?LongActionWidth:ActionWidth;
   float first=button.Board!=null?Math.Min(inner,336)
    :((mini||view==NativeHuntPage.Hunt)&&IsSecondary(button))||button.Key=="boards-all"?CompactWidth(button):inner;
   Set(i+3,++rows,0,button,Margin,bottom,first,h);
   if(pair){i++;Set(i+3,rows,1,visible[i],Margin+first+Gap,bottom,CompactWidth(visible[i]),h);}
   bottom+=h+Gap;
  }
  bool pager=!mini&&pageCount>1;
  const float pagerWidth=126,footerWidth=96;
  if(pager){Set(9,7,0,new("previous","Предыдущая",new("previous",Delta:-1),page>0),Margin,bottom,pagerWidth,30);Set(10,7,1,new("next-page","Следующая",new("next-page",Delta:1),page+1<pageCount),width-Margin-pagerWidth,bottom,pagerWidth,30);}
  float footer=bottom+(pager?38:0);
  var back=view switch {NativeHuntPage.Number=>NativeHuntPage.Advanced,NativeHuntPage.Advanced=>NativeHuntPage.Settings,NativeHuntPage.Target or NativeHuntPage.Filters=>NativeHuntPage.Targets,NativeHuntPage.Expansions or NativeHuntPage.Zones=>NativeHuntPage.Filters,_=>NativeHuntPage.Hunt};
  if(!mini&&view!=NativeHuntPage.Hunt)Set(11,8,0,Page("back","Назад",back),Margin,footer,footerWidth,ActionHeight);
  Set(12,8,1,new("close","Закрыть",new("close")),width-Margin-footerWidth,footer,footerWidth,ActionHeight);
  return new(new(width,footer+36),new(new(Margin,titleY),new(inner,titleHeight)),
   new(new(Margin,summaryY),new(textWidth,summaryHeight)),new(new(width-Margin-PortraitSize,summaryY),new(PortraitSize,PortraitSize)),
   new(new(Margin,progressY),new(textWidth,18)),new(new(Margin,progressY+19),new(textWidth,5)),
   new(new(Margin+pagerWidth+Gap,bottom+6),new(inner-2*(pagerWidth+Gap),22)),
   pager?$"Стр. {page+1} / {pageCount}":"",buttons);
 }
}
