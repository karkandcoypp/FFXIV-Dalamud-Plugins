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
 public const float ActionHeight=32, ActionWidth=84, LongActionWidth=2*ActionWidth+Gap;
 public const float PortraitSize=108;
 private const float FooterHeight=30, FooterBottomInset=16;
 // Axis typography with slightly larger labels for legibility.
 public const byte TitleFont=18, BodyFont=14, ButtonFont=13, SecondaryFont=12;
 public static float ContentWidth(bool mini)=>(mini?300:420)-2*Margin;
 public static byte SummaryFont(NativeHuntPage view,bool mini)=>SecondaryFont;
 public static float SummaryWidth(NativeHuntScreen screen,bool mini)=>ContentWidth(mini)-(!mini&&screen.Icon!=0?PortraitSize+Gap:0);
 private static bool IsCompact(NativeHuntButton button)=>button.Board==null&&button.Goal==null&&button.Checked==null&&!button.Label.Contains('\n')&&button.Label.Length<=24;
 public static NativeHuntRect ButtonCaption(Vector2 position,Vector2 size,Vector2 measured){
  var fitted=Vector2.Min(Vector2.Max(Vector2.Zero,measured),new(Math.Max(0,size.X-16),Math.Max(0,size.Y)));
  var inset=(size-fitted)/2;
  // The game font's line box places the visible glyphs below its midpoint.
  inset.Y-=Math.Clamp(inset.Y-2,0,3);
  return new(position+inset,fitted);
 }
 public static float ButtonWidth(string label)=>label.Length<=11?ActionWidth:LongActionWidth;
 public static string FitLabel(string value,int length=64)=>value.Length<=length?value:string.Concat(value.EnumerateRunes().Take(length-1).Select(r=>r.ToString()))+"…";
 public static NativeHuntLayout Build(NativeHuntScreen screen,NativeHuntPage view,int requestedPage,float titleHeight,float summaryHeight,bool mini=false,Func<string,float>? measureButton=null){
  float Button(string label)=>Math.Max(ButtonWidth(label),measureButton==null?0:MathF.Ceiling(measureButton(label))+16);
  float Compact(NativeHuntButton button)=>Button(button.Label);
  float inner=ContentWidth(mini),width=inner+2*Margin,titleY=mini?38:37+ActionHeight+8;
  titleHeight=float.IsFinite(titleHeight)?Math.Clamp(titleHeight,22,110):22;
  summaryHeight=string.IsNullOrEmpty(screen.Summary)?0:float.IsFinite(summaryHeight)?Math.Clamp(summaryHeight,17,190):17;
  float summaryY=titleY+titleHeight+4;bool portrait=!mini&&screen.Icon!=0;
  float textWidth=SummaryWidth(screen,mini);
  float progressY=summaryY+summaryHeight+4;
  float bodyY=summaryY+Math.Max(portrait?Math.Max(0,PortraitSize-(summaryY-titleY)):0,summaryHeight+(screen.Progress!=null?(mini?22:30):0))+7;
  int page=mini?0:NativeHuntNavigation.ClampPage(requestedPage,screen.Buttons.Count);
  int pageCount=Math.Max(1,(screen.Buttons.Count+5)/6);
  var visible=screen.Buttons.Skip(page*6).Take(mini?3:6).ToArray();
  bool miniStrip=mini&&visible.Length>0&&visible.All(IsCompact);
  if(miniStrip){
   inner=Math.Max(inner,visible.Sum(Compact)+(visible.Length-1)*Gap);
   width=inner+2*Margin;
   textWidth=inner;
  }
  var buttons=new List<NativeHuntPlacement>();
  void Set(int slot,int row,int col,NativeHuntButton button,float x,float y,float w,float h)=>buttons.Add(new(slot,row,col,button with {Label=button.Board==null?FitLabel(button.Label):button.Label},new(new(x,y),new(w,h))));
  NativeHuntButton Page(string key,string label,NativeHuntPage p)=>new(key,label,new("page",p.ToString()),Emphasized:key.StartsWith("tab:",StringComparison.Ordinal)&&p==(view switch {NativeHuntPage.Boards=>NativeHuntPage.Boards,NativeHuntPage.Settings or NativeHuntPage.Advanced or NativeHuntPage.Number=>NativeHuntPage.Settings,_=>NativeHuntPage.Hunt}));
  bool settingsList=view==NativeHuntPage.Settings&&!mini;
  var close=new NativeHuntButton("close","Закрыть",new("close"));
  float closeWidth=Button(close.Label);
  bool huntActions=!mini&&view==NativeHuntPage.Hunt&&pageCount==1&&visible.Length>0&&visible.All(IsCompact);
  bool compactActive=false;
  if(!mini){
   string[] tabLabels=["Охота","Доски","Настройки"];
   float[] tabWidths=tabLabels.Select(Button).ToArray();
   bool activeActions=huntActions&&visible is [{Key:"primary",Action.Kind:"pause" or "resume"},{Key:"map"},{Key:"actions"},{Key:"targets"}]
    &&visible.Skip(1).Sum(Compact)+closeWidth+3*Gap<=inner;
   float primaryWidth=activeActions?Compact(visible[0]):0;
   if(activeActions&&measureButton!=null&&tabWidths.Sum()+primaryWidth+3*Gap>inner)
    tabWidths=tabLabels.Select(label=>Math.Max(72,MathF.Ceiling(measureButton(label))+16)).ToArray();
   compactActive=activeActions&&tabWidths.Sum()+primaryWidth+3*Gap<=inner;
   if(!compactActive)tabWidths=tabLabels.Select(Button).ToArray();
   float tabWidth=tabWidths.Max();
   if(!compactActive||3*tabWidth+primaryWidth+3*Gap<=inner)Array.Fill(tabWidths,tabWidth);
   Set(0,0,0,Page("tab:hunt","Охота",NativeHuntPage.Hunt),Margin,37,tabWidths[0],ActionHeight);
   Set(1,0,1,Page("tab:boards","Доски",NativeHuntPage.Boards),Margin+tabWidths[0]+Gap,37,tabWidths[1],ActionHeight);
   Set(2,0,2,Page("tab:settings","Настройки",NativeHuntPage.Settings),Margin+tabWidths[0]+tabWidths[1]+2*Gap,37,tabWidths[2],ActionHeight);
   if(compactActive)Set(3,0,3,visible[0],width-Margin-Compact(visible[0]),37,Compact(visible[0]),ActionHeight);
  }
  float controlWidth=mini?inner:4*ActionWidth+3*Gap;
  float controlX=Margin;
  var tableText=new List<NativeHuntText>();
  void Cell(string label,float x,float y,float w,Vector4 color,byte font=ButtonFont)=>tableText.Add(new(FitLabel(label,46),new(new(x,y),new(w,21)),color,font));
  string? lastZone=null;
  int rows=0;
  float bottom=bodyY;
  if(compactActive){
   float x=Margin;
   for(int i=1;i<visible.Length;i++){
    float w=Compact(visible[i]);Set(i+3,1,i-1,visible[i],x,bodyY,w,ActionHeight);x+=w+Gap;
   }
   Set(12,1,3,close,width-Margin-closeWidth,bodyY,closeWidth,ActionHeight);
   bottom=bodyY+ActionHeight+Gap;
  }else if(huntActions){
   var actions=visible.Append(close).ToArray();
   float Span(int start,int count)=>actions.Skip(start).Take(count).Sum(Compact)+(count-1)*Gap;
   bool Place(int start,int row){
    if(start==actions.Length){rows=row-1;return true;}
    int left=actions.Length-start;
    for(int count=Math.Min(3,left);count>=2;count--){
     if(left-count==1||Span(start,count)>inner)continue;
     if(!Place(start+count,row+1))continue;
     float x=Margin;
     for(int column=0;column<count;column++){
      int index=start+column;float w=Compact(actions[index]);
      Set(index==visible.Length?12:index+3,row,column,actions[index],x,bodyY+(row-1)*(ActionHeight+Gap),w,ActionHeight);
      x+=w+Gap;
     }
     return true;
    }
    return false;
   }
   huntActions=Place(0,1);
   if(huntActions)bottom=bodyY+rows*(ActionHeight+Gap);
  }
  for(int i=0;!huntActions&&i<visible.Length;i++){
   var button=visible[i];
   if(miniStrip&&i==0){
    float x=(width-visible.Sum(Compact)-(visible.Length-1)*Gap)/2;
    rows++;
    for(int column=0;column<visible.Length;column++){
     float actionWidth=Compact(visible[column]);
     Set(column+3,rows,column,visible[column],x,bottom,actionWidth,ActionHeight);
     x+=actionWidth+Gap;
    }
    bottom+=ActionHeight+Gap;
    break;
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
    Cell(board.Daily.Label,Margin+154,bottom+5,104,board.Daily.Color??Vector4.One,SecondaryFont);
    Cell(board.Weekly.Label,Margin+266,bottom+5,110,board.Weekly.Color??Vector4.One,SecondaryFont);
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
  if(pager){Set(9,7,0,new("previous","Пред.",new("previous",Delta:-1),page>0),controlX,bottom,pagerWidth,ActionHeight);Set(10,7,1,new("next-page","Далее",new("next-page",Delta:1),page+1<pageCount),controlX+controlWidth-pagerWidth,bottom,pagerWidth,ActionHeight);}
  float footer=bottom+(pager?ActionHeight+12:0);
  var back=view switch {NativeHuntPage.Number=>NativeHuntPage.Advanced,NativeHuntPage.Advanced=>NativeHuntPage.Settings,NativeHuntPage.Target or NativeHuntPage.Filters=>NativeHuntPage.Targets,NativeHuntPage.Expansions or NativeHuntPage.Zones=>NativeHuntPage.Filters,_=>NativeHuntPage.Hunt};
  if(settingsList)Set(11,8,0,Page("advanced","Параметры",NativeHuntPage.Advanced),controlX,footer,Button("Параметры"),FooterHeight);
  else if(!mini&&view!=NativeHuntPage.Hunt)Set(11,8,0,Page("back","Назад",back),controlX,footer,Button("Назад"),FooterHeight);
  if(!mini&&!huntActions)Set(12,8,1,close,width-Margin-closeWidth,footer,closeWidth,FooterHeight);
  return new(new(width,footer+(mini?4:huntActions?FooterBottomInset-Gap:FooterHeight+FooterBottomInset)),new(new(Margin,titleY),new(textWidth,titleHeight)),
   new(new(Margin,summaryY),new(textWidth,summaryHeight)),new(new(width-Margin-PortraitSize,titleY),new(PortraitSize,PortraitSize)),
   new(new(Margin,progressY),new(textWidth,18)),new(new(Margin,progressY+19),new(textWidth,mini?0:5)),
   new(new(controlX+(controlWidth-74)/2,bottom+6),new(74,22)),
   pager?$"Стр. {page+1} / {pageCount}":"",buttons,tableText);
 }
}
