using HuntRoute;
static class NativeNavigationChecks {
 public static void Run(Action<bool,string> check){
  NativeNavItem[] items=[new(1,0,0,"hunt"),new(2,0,1,"boards"),new(3,0,2,"settings"),new(4,1,0,"primary"),new(5,2,0,"map"),new(10,7,0,"previous",false),new(11,7,1,"next"),new(12,8,0,"back"),new(13,8,1,"close")];
  var active=new NativeHuntScreen("Target","Zone",[
   new("primary","Pause",new("pause"),Emphasized:true),new("map","Map",new("map")),new("actions","Route",new("page")),new("targets","Targets",new("page"))
  ],123,new(1,3));
  foreach(bool mini in new[]{false,true}){
   var layout=NativeHuntLayout.Build(active,NativeHuntPage.Hunt,0,30,38,mini);
   check(layout.Size.X==(mini?300:420)&&layout.Size.Y<(mini?290:340),"Active native menus fit the compact footprint");
   foreach(var texture in new System.Numerics.Vector2[]{new(256,128),new(128,256),new(512,512)}){
    var image=layout.Portrait.FitImage(texture);
    var end=image.Position+image.Size;
    check(image.Position.X>=layout.Portrait.Position.X&&image.Position.Y>=layout.Portrait.Position.Y&&
      end.X<=layout.Portrait.Position.X+layout.Portrait.Size.X&&end.Y<=layout.Portrait.Position.Y+layout.Portrait.Size.Y&&
      (mini||end.X<layout.Size.X&&end.Y<layout.Size.Y),"Native portrait fits its allocated box and stays inside the window");
   }
   var map=layout.Buttons.Single(b=>b.Button.Key=="map");
   var actions=layout.Buttons.Single(b=>b.Button.Key=="actions");
   var primary=layout.Buttons.Single(b=>b.Button.Key=="primary");
   check(primary.Rect.Size.X<=200&&(mini||primary.Row==layout.Buttons.Single(b=>b.Button.Key=="targets").Row),"Primary action uses a compact button alongside the target list");
   var rightOfMap=actions;
   check(map.Row==rightOfMap.Row&&map.Rect.Position.X+map.Rect.Size.X<rightOfMap.Rect.Position.X,"Hunt actions fit side by side");
   check(!mini||primary.Row==map.Row,"Mini panel puts all three hunt actions in one row");
   check(actions.Row==primary.Row,"Route actions share the hunt action row");
   check(layout.ProgressTrack.Position.Y+layout.ProgressTrack.Size.Y<primary.Rect.Position.Y,"Hunt progress does not overlap the primary action");
   var paired=NativeHuntNavigation.Build(layout.Buttons.Select(b=>new NativeNavItem(b.Slot+1,b.Row,b.Column,b.Button.Key,b.Button.Enabled)));
   check(paired.Single(n=>n.Index==map.Slot+1).Right==rightOfMap.Slot+1,"D-pad right moves from Map to the adjacent action");
  }
  var settings=new NativeHuntScreen("Settings","",Enumerable.Range(0,6).Select(i=>new NativeHuntButton($"toggle:{i}","Setting",new("toggle"),Checked:i%2==0)).ToArray());
  var settingsLayout=NativeHuntLayout.Build(settings,NativeHuntPage.Settings,0,30,0);
  var settingLinks=NativeHuntNavigation.Build(settingsLayout.Buttons.Select(b=>new NativeNavItem(b.Slot+1,b.Row,b.Column,b.Button.Key,b.Button.Enabled)));
  check(settingsLayout.Buttons.Count(b=>b.Button.Checked.HasValue)==6&&settingsLayout.PageText=="","All six checkboxes fit on one settings screen");
  check(settingLinks.Single(n=>n.Key=="toggle:5").Down==settingLinks.Single(n=>n.Key=="advanced").Index,"Last checkbox navigates down to advanced settings");
  check(active.Progress!.Fraction==1f/3&&new NativeHuntProgress(5,3).Fraction==1&&new NativeHuntProgress(0,0).Fraction==0,"Progress fill clamps its endpoints");
  var graph=NativeHuntNavigation.Build(items);check(graph.Length==8&&graph.All(n=>n.Index!=10),"Disabled native pager omitted from focus graph");
  check(graph.Single(n=>n.Index==2).Right==3&&graph.Single(n=>n.Index==3).Right==1,"Native tabs navigate horizontally and wrap");
  check(graph.Single(n=>n.Index==3).Down==4&&graph.Single(n=>n.Index==5).Down==11,"Vertical navigation reaches content/pager without landing on disabled nodes");
  check(NativeHuntNavigation.RestoreFocus(graph,"map")==5,"Semantic focus retained after label update");
  check(NativeHuntNavigation.RestoreFocus(graph,"removed")==1&&NativeHuntNavigation.RestoreFocus([],"removed")==0,"Removed focus falls back to safe tab; empty graph is valid");
  check(NativeHuntNavigation.ClampPage(99,13)==2&&NativeHuntNavigation.ClampPage(2,1)==0&&NativeHuntNavigation.ClampPage(-4,0)==0,"Native page index clamps after contracts are removed");
  var screen=new NativeHuntScreen("Large title","Summary",Enumerable.Range(0,19).Select(i=>new NativeHuntButton("goal:"+i,new string('Ж',150),new("target",i.ToString()),i%3!=0)).ToArray(),123);
  foreach(bool mini in new[]{false,true})foreach(int page in new[]{-1,0,1,3,99}){
   var layout=NativeHuntLayout.Build(screen,NativeHuntPage.Targets,page,70,180,mini);
   bool Inside(NativeHuntRect r)=>r.Position.X>=0&&r.Position.Y>=0&&r.Position.X+r.Size.X<=layout.Size.X&&r.Position.Y+r.Size.Y<=layout.Size.Y;
   check(Inside(layout.Heading)&&Inside(layout.Summary)&&layout.Buttons.All(b=>Inside(b.Rect)),"Native long-text/paged/mini placements stay inside addon");
   check(layout.Buttons.Select(b=>b.Slot).Distinct().Count()==layout.Buttons.Count&&layout.Buttons.All(b=>b.Slot<NativeHuntLayout.ButtonCapacity),"Each native layout uses a unique bounded node slot");
   var nav=NativeHuntNavigation.Build(layout.Buttons.Select(b=>new NativeNavItem(b.Slot+1,b.Row,b.Column,b.Button.Key,b.Button.Enabled)));
   check((mini||nav.Any(n=>n.Key=="close"))&&nav.All(n=>layout.Buttons.Any(b=>b.Slot+1==n.Index&&b.Button.Enabled)),"Native pages always have an enabled close action and no focusable disabled items");
  }
  var random=new Random(2322);
  for(int sample=0;sample<200;sample++){
   var subset=items.Select(n=>n with {Enabled=n.Key=="close"||random.Next(2)==1,Visible=n.Key=="close"||random.Next(2)==1}).ToArray();
   var links=NativeHuntNavigation.Build(subset);var byId=links.ToDictionary(n=>n.Index);var visited=new HashSet<int>();var queue=new Queue<int>();queue.Enqueue(links[0].Index);
   while(queue.TryDequeue(out int node)){if(!visited.Add(node))continue;var link=byId[node];foreach(int next in new[]{link.Up,link.Down,link.Left,link.Right}){check(byId.ContainsKey(next),"Native direction targets an enabled visible node");queue.Enqueue(next);}}
   check(visited.Count==links.Length,"Every enabled native action remains reachable by controller");
  }
 }
}
