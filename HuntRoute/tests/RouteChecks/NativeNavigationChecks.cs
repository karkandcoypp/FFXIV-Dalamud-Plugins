using HuntRoute;
static class NativeNavigationChecks {
 public static void Run(Action<bool,string> check){
  NativeNavItem[] items=[new(1,0,0,"hunt"),new(2,0,1,"boards"),new(3,0,2,"settings"),new(4,1,0,"primary"),new(5,2,0,"map"),new(10,7,0,"previous",false),new(11,7,1,"next"),new(12,8,0,"back"),new(13,8,1,"close")];
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
   check(nav.Any(n=>n.Key=="close")&&nav.All(n=>layout.Buttons.Any(b=>b.Slot+1==n.Index&&b.Button.Enabled)),"Native pages always have an enabled close action and no focusable disabled items");
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
