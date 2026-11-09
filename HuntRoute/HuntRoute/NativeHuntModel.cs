using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
namespace HuntRoute;

public enum NativeHuntPage { Hunt,Targets,Boards,Settings,Target,Actions,Advanced,Filters,Expansions,Zones,Number }
public sealed record NativeHuntAction(string Kind,string Argument="",int Delta=0);
public sealed record NativeHuntButton(string Key,string Label,NativeHuntAction Action,bool Enabled=true);
public sealed record NativeHuntScreen(string Title,string Summary,IReadOnlyList<NativeHuntButton> Buttons,uint Icon=0);
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

public readonly record struct NativeHuntRect(Vector2 Position,Vector2 Size);
public sealed record NativeHuntPlacement(int Slot,int Row,int Column,NativeHuntButton Button,NativeHuntRect Rect);
public sealed record NativeHuntLayout(Vector2 Size,NativeHuntRect Heading,NativeHuntRect Summary,NativeHuntRect Portrait,NativeHuntRect PageLabel,string PageText,IReadOnlyList<NativeHuntPlacement> Buttons){
 public const int ButtonCapacity=13;
 public static string FitLabel(string value,int length=64)=>value.Length<=length?value:string.Concat(value.EnumerateRunes().Take(length-1).Select(r=>r.ToString()))+"…";
 public static NativeHuntLayout Build(NativeHuntScreen screen,NativeHuntPage view,int requestedPage,float titleHeight,float summaryHeight,bool mini=false){
  float width=mini?460:640,inner=width-48,titleY=mini?42:90;
  titleHeight=float.IsFinite(titleHeight)?Math.Clamp(titleHeight,26,80):26;
  summaryHeight=float.IsFinite(summaryHeight)?Math.Clamp(summaryHeight,19,190):19;
  float summaryY=titleY+titleHeight+8;bool portrait=!mini&&screen.Icon!=0;
  float bodyY=Math.Max(mini?136:200,summaryY+Math.Max(portrait?112:0,summaryHeight)+16);
  int page=mini?0:NativeHuntNavigation.ClampPage(requestedPage,screen.Buttons.Count);
  int pageCount=Math.Max(1,(screen.Buttons.Count+5)/6);
  var visible=screen.Buttons.Skip(page*6).Take(mini?3:6).ToArray();var buttons=new List<NativeHuntPlacement>();
  void Set(int slot,int row,int col,NativeHuntButton button,float x,float y,float w,float h)=>buttons.Add(new(slot,row,col,button with {Label=FitLabel(button.Label)},new(new(x,y),new(w,h))));
  NativeHuntButton Page(string key,string label,NativeHuntPage p)=>new(key,label,new("page",p.ToString()));
  if(!mini){Set(0,0,0,Page("tab:hunt","Охота",NativeHuntPage.Hunt),24,44,192,32);Set(1,0,1,Page("tab:boards","Доски",NativeHuntPage.Boards),224,44,192,32);Set(2,0,2,Page("tab:settings","Настройки",NativeHuntPage.Settings),424,44,192,32);}
  for(int i=0;i<visible.Length;i++)Set(i+3,i+1,0,visible[i],24,bodyY+i*48,inner,40);
  float bottom=bodyY+Math.Max(1,visible.Length)*48+8;bool pager=!mini&&pageCount>1;
  if(pager){Set(9,7,0,new("previous","Предыдущая",new("previous",Delta:-1),page>0),24,bottom,168,32);Set(10,7,1,new("next-page","Следующая",new("next-page",Delta:1),page+1<pageCount),448,bottom,168,32);}
  float footer=bottom+(pager?44:0);
  var back=view switch {NativeHuntPage.Number=>NativeHuntPage.Advanced,NativeHuntPage.Advanced=>NativeHuntPage.Settings,NativeHuntPage.Target or NativeHuntPage.Filters=>NativeHuntPage.Targets,NativeHuntPage.Expansions or NativeHuntPage.Zones=>NativeHuntPage.Filters,_=>NativeHuntPage.Hunt};
  if(!mini&&view!=NativeHuntPage.Hunt)Set(11,8,0,Page("back","Назад",back),24,footer,184,32);
  Set(12,8,1,new("close","Закрыть",new("close")),width-208,footer,184,32);
  return new(new(width,footer+52),new(new(24,titleY),new(inner,titleHeight)),new(new(24,summaryY),new(portrait?456:inner,summaryHeight)),new(new(496,summaryY),new(112,112)),new(new(208,bottom+7),new(240,24)),pager?$"Страница {page+1} / {pageCount}":"",buttons);
 }
}
