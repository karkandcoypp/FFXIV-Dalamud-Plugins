using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
namespace HuntRoute;

public enum NativeHuntPage { Hunt,Targets,Boards,Settings,Target,Actions,Advanced,Filters,Expansions,Zones,Number }
public sealed record NativeHuntAction(string Kind,string Argument="",int Delta=0);
public sealed record NativeHuntButton(string Key,string Label,NativeHuntAction Action,bool Enabled=true,bool Emphasized=false,NativeBoardRow? Board=null,Vector4? LabelColor=null,bool? Checked=null,NativeGoalRow? Goal=null);
public sealed record NativeBoardStatus(string Label,Vector4? Color=null);
public sealed record NativeBoardRow(string Name,NativeBoardStatus Daily,NativeBoardStatus Weekly);
public sealed record NativeGoalRow(string Zone,string Name,string Count);
public sealed record NativeHuntScreen(string Title,string Summary,IReadOnlyList<NativeHuntButton> Buttons,uint Icon=0,NativeHuntProgress? Progress=null,Vector4? SummaryColor=null);
public static class NativeHuntColors {
 public static readonly Vector4 Focus=new(.80f,.92f,1,1), PrimarySurface=new(.65f,.81f,1,1);
 public static readonly Vector4 Ready=new(.50f,1,.64f,1), Attention=new(1,.83f,.48f,1), Tracking=new(.62f,.84f,1,1), Muted=new(.78f,.81f,.85f,1);
}
public sealed record NativeHuntProgress(int Kills,uint Required){
 public string Label=>$"Прогресс  {Kills} / {Required}";
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
public sealed record NativeHuntText(string Label,NativeHuntRect Rect,Vector4 Color,byte Font);
public sealed record NativeHuntLayout(Vector2 Size,NativeHuntRect Heading,NativeHuntRect Summary,NativeHuntRect Portrait,NativeHuntRect ProgressLabel,NativeHuntRect ProgressTrack,NativeHuntRect PageLabel,string PageText,IReadOnlyList<NativeHuntPlacement> Buttons,IReadOnlyList<NativeHuntText> TableText){
 public const int ButtonCapacity=13;
 private const float Margin=16,Gap=6;
 public const float ActionHeight=26, ActionWidth=84, LongActionWidth=2*ActionWidth+Gap;
 public const float PortraitSize=68;
 // Axis typography with slightly larger labels for legibility.
 public const byte TitleFont=18, BodyFont=15, ButtonFont=14;
 public static float ContentWidth(bool mini)=>(mini?300:420)-2*Margin;
 public static byte SummaryFont(NativeHuntPage view,bool mini)=>!mini&&view==NativeHuntPage.Boards?(byte)13:BodyFont;
 public static float SummaryWidth(NativeHuntScreen screen,bool mini)=>ContentWidth(mini)-(!mini&&screen.Icon!=0?PortraitSize+Gap:0);
 private static bool IsCompact(NativeHuntButton button)=>button.Board==null&&button.Goal==null&&button.Checked==null&&!button.Label.Contains('\n')&&button.Label.Length<=24;
 public static float ButtonWidth(string label)=>label.Length<=11?ActionWidth:LongActionWidth;
 public static string FitLabel(string value,int length=64)=>value.Length<=length?value:string.Concat(value.EnumerateRunes().Take(length-1).Select(r=>r.ToString()))+"…";
 public static NativeHuntLayout Build(NativeHuntScreen screen,NativeHuntPage view,int requestedPage,float titleHeight,float summaryHeight,bool mini=false,Func<string,float>? measureButton=null){
  float Button(string label)=>Math.Max(ButtonWidth(label),measureButton==null?0:MathF.Ceiling(measureButton(label))+16);
  float Compact(NativeHuntButton button)=>Button(button.Label);
  float inner=ContentWidth(mini),width=inner+2*Margin,titleY=mini?38:71;
  titleHeight=float.IsFinite(titleHeight)?Math.Clamp(titleHeight,22,110):22;
  summaryHeight=string.IsNullOrEmpty(screen.Summary)?0:float.IsFinite(summaryHeight)?Math.Clamp(summaryHeight,17,190):17;
  float summaryY=titleY+titleHeight+4;bool portrait=!mini&&screen.Icon!=0;
  float textWidth=SummaryWidth(screen,mini);
  float progressY=summaryY+summaryHeight+4;
  float bodyY=summaryY+Math.Max(portrait?PortraitSize:0,summaryHeight+(screen.Progress!=null?(mini?22:30):0))+7;
  int page=mini?0:NativeHuntNavigation.ClampPage(requestedPage,screen.Buttons.Count);
  int pageCount=Math.Max(1,(screen.Buttons.Count+5)/6);
  var visible=screen.Buttons.Skip(page*6).Take(mini?3:6).ToArray();
  bool huntStrip=!mini&&view==NativeHuntPage.Hunt&&visible is [{Key:"primary"},{Key:"map"},{Key:"actions"},{Key:"targets"},..]
   &&visible.Take(4).Sum(b=>Compact(b))+3*Gap<=inner;
  var buttons=new List<NativeHuntPlacement>();
  void Set(int slot,int row,int col,NativeHuntButton button,float x,float y,float w,float h)=>buttons.Add(new(slot,row,col,button with {Label=button.Board==null?FitLabel(button.Label):button.Label},new(new(x,y),new(w,h))));
  NativeHuntButton Page(string key,string label,NativeHuntPage p)=>new(key,label,new("page",p.ToString()),Emphasized:key.StartsWith("tab:",StringComparison.Ordinal)&&p==(view switch {NativeHuntPage.Boards=>NativeHuntPage.Boards,NativeHuntPage.Settings or NativeHuntPage.Advanced or NativeHuntPage.Number=>NativeHuntPage.Settings,_=>NativeHuntPage.Hunt}));
  bool settingsList=view==NativeHuntPage.Settings&&!mini;
  if(!mini){
   float tabWidth=new[]{"Охота","Доски","Настройки"}.Max(Button);
   float tabsX=Margin;
   Set(0,0,0,Page("tab:hunt","Охота",NativeHuntPage.Hunt),tabsX,37,tabWidth,ActionHeight);
   Set(1,0,1,Page("tab:boards","Доски",NativeHuntPage.Boards),tabsX+tabWidth+Gap,37,tabWidth,ActionHeight);
   Set(2,0,2,Page("tab:settings","Настройки",NativeHuntPage.Settings),tabsX+2*(tabWidth+Gap),37,tabWidth,ActionHeight);
  }
  float controlWidth=mini?inner:4*ActionWidth+3*Gap;
  float controlX=Margin;
  var tableText=new List<NativeHuntText>();
  void Cell(string label,float x,float y,float w,Vector4 color,byte font=ButtonFont)=>tableText.Add(new(FitLabel(label,46),new(new(x,y),new(w,21)),color,font));
  string? lastZone=null;
  int rows=0;
  float bottom=bodyY;
  for(int i=0;i<visible.Length;i++){
   var button=visible[i];
   if(mini&&i==0&&visible.All(IsCompact)&&visible.Length*visible.Max(Compact)+(visible.Length-1)*Gap<=inner){
    float miniActionWidth=visible.Max(Compact);
    float x=(width-visible.Length*miniActionWidth-(visible.Length-1)*Gap)/2;
    rows++;
    for(int column=0;column<visible.Length;column++)
     Set(column+3,rows,column,visible[column],x+column*(miniActionWidth+Gap),bottom,miniActionWidth,ActionHeight);
    bottom+=ActionHeight+Gap;
    break;
   }
   if(huntStrip&&i==0){
    float x=Margin;
    rows++;
    for(int column=0;column<4;column++){
     float w=Compact(visible[column]);
     Set(column+3,rows,column,visible[column],x,bottom,w,ActionHeight);
     x+=w+Gap;
    }
    i=3;bottom+=ActionHeight+Gap;
    continue;
   }
   if(button.Board is {} board){
    if(i==0){
     Cell("Дополнение",Margin+8,bottom,140,NativeHuntColors.Muted,14);
     Cell("Ежедневные",Margin+154,bottom,104,NativeHuntColors.Muted,14);
     Cell("Еженедельный",Margin+266,bottom,110,NativeHuntColors.Muted,14);
     bottom+=25;
    }
    Set(i+3,++rows,0,button,Margin,bottom,inner,28);
    Cell(board.Name,Margin+8,bottom+5,140,NativeHuntColors.Tracking,16);
    Cell(board.Daily.Label,Margin+154,bottom+5,104,board.Daily.Color??Vector4.One);
    Cell(board.Weekly.Label,Margin+266,bottom+5,110,board.Weekly.Color??Vector4.One);
    bottom+=32;
    continue;
   }
   if(button.Goal is {} goal){
    if(view==NativeHuntPage.Zones){
     Set(i+3,++rows,0,button,controlX,bottom,controlWidth,28);
     Cell(goal.Name,controlX+8,bottom+5,controlWidth-16,Vector4.One);
     bottom+=30;
     continue;
    }
    if(goal.Zone!=lastZone){
     lastZone=goal.Zone;
     Cell(goal.Zone,Margin+8,bottom,inner-16,NativeHuntColors.Tracking);
     bottom+=22;
    }
    Set(i+3,++rows,0,button,Margin,bottom,inner,28);
    Cell(goal.Name,Margin+16,bottom+5,inner-120,button.LabelColor??Vector4.One);
    Cell(goal.Count,width-Margin-98,bottom+5,90,button.LabelColor??NativeHuntColors.Muted);
    bottom+=30;
    continue;
   }
   if(button.Checked.HasValue){
    Set(i+3,++rows,0,button,controlX,bottom,controlWidth,24);
    bottom+=28;
    continue;
   }
   if(view==NativeHuntPage.Number){
    const int columns=3;
    float x=(width-(columns*ActionWidth+(columns-1)*Gap))/2;
    int count=Math.Min(columns,visible.Length-i);
    rows++;
    for(int column=0;column<count;column++)
     Set(i+column+3,rows,column,visible[i+column],x+column*(ActionWidth+Gap),bottom,ActionWidth,ActionHeight);
    i+=count-1;
    bottom+=ActionHeight+Gap;
    continue;
   }
   float gridWidth=mini?3*ActionWidth+2*Gap:controlWidth;
   float gridX=controlX;
   if(!button.Label.Contains('\n')&&button.Label.Length<=25){
    int count=0;
    float used=0;
    rows++;
    while(i+count<visible.Length){
     var item=visible[i+count];
     if(item.Board!=null||item.Goal!=null||item.Checked.HasValue||item.Label.Contains('\n')||item.Label.Length>25)break;
     float w=Math.Max(view==NativeHuntPage.Target?LongActionWidth:0,Compact(item));
     if(used+w>gridWidth)break;
     Set(i+count+3,rows,count,item,gridX+used,bottom,w,ActionHeight);
     used+=w+Gap;count++;
    }
    i+=count-1;
    bottom+=ActionHeight+Gap;
    continue;
   }
   float h=button.Label.Length>54?56:44;
   Set(i+3,++rows,0,button,gridX,bottom,gridWidth,h);
   bottom+=h+Gap;
  }
  bool pager=!mini&&pageCount>1;
  float pagerWidth=Math.Max(Button("Пред."),Button("Далее"));
  float footerWidth=new[]{"Назад","Закрыть",settingsList?"Параметры":""}.Max(Button);
  if(pager){Set(9,7,0,new("previous","Пред.",new("previous",Delta:-1),page>0),controlX,bottom,pagerWidth,ActionHeight);Set(10,7,1,new("next-page","Далее",new("next-page",Delta:1),page+1<pageCount),controlX+controlWidth-pagerWidth,bottom,pagerWidth,ActionHeight);}
  float footer=bottom+(pager?38:0);
  var back=view switch {NativeHuntPage.Number=>NativeHuntPage.Advanced,NativeHuntPage.Advanced=>NativeHuntPage.Settings,NativeHuntPage.Target or NativeHuntPage.Filters=>NativeHuntPage.Targets,NativeHuntPage.Expansions or NativeHuntPage.Zones=>NativeHuntPage.Filters,_=>NativeHuntPage.Hunt};
  if(settingsList)Set(11,8,0,Page("advanced","Параметры",NativeHuntPage.Advanced),controlX,footer,footerWidth,ActionHeight);
  else if(!mini&&view!=NativeHuntPage.Hunt)Set(11,8,0,Page("back","Назад",back),controlX,footer,footerWidth,ActionHeight);
  if(!mini)Set(12,8,1,new("close","Закрыть",new("close")),controlX+controlWidth-footerWidth,footer,footerWidth,ActionHeight);
  return new(new(width,footer+(mini?4:36)),new(new(Margin,titleY),new(inner,titleHeight)),
   new(new(Margin,summaryY),new(textWidth,summaryHeight)),new(new(width-Margin-PortraitSize,summaryY),new(PortraitSize,PortraitSize)),
   new(new(Margin,progressY),new(textWidth,18)),new(new(Margin,progressY+19),new(textWidth,mini?0:5)),
   new(new(controlX+(controlWidth-74)/2,bottom+6),new(74,22)),
   pager?$"Стр. {page+1} / {pageCount}":"",buttons,tableText);
 }
}
