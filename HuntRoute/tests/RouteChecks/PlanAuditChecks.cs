using HuntRoute;
using System.Numerics;
static class PlanAuditChecks {
 public static void Run(Action<bool,string> check){
  var plannedSession=new RouteSession();var plannedGoal=new MobHuntEntry{BillNumber=6,OrderId=2,MobIndex=0,MobHuntId=100,TerritoryType=612,NeededKills=1};
  plannedSession.PlanTravel(plannedGoal,new byte[]{6,7,8});uint plannedRun=plannedSession.RunId;
  for(int i=0;i<10;i++)plannedSession.Tick(new[]{plannedGoal},_=>false,_=>true,628);
  check(plannedSession.Phase==RoutePhase.AwaitingTravel&&plannedSession.RunId==plannedRun,"Unconfirmed city trip cannot become finished or pretend to be an accepted teleport");
  plannedSession.PlanTravel(plannedGoal,new byte[]{6,7,8});check(plannedSession.RunId==plannedRun,"Reopening planned trip preserves route identity");
  check(plannedSession.Tick(new[]{plannedGoal},_=>false,_=>true,612)==plannedGoal&&plannedSession.Phase==RoutePhase.Following,"Manual arrival in planned zone resumes following without teleport cast");
  var guard=new BillSnapshotGuard();
  BillSnapshot Taken(byte order,int kills=0)=>new(true,order,[kills,0,0,0,0]);
  check(guard.Observe(1,Taken(1,2),0,10),"Initial taken bill accepted");
  check(!guard.Observe(1,Taken(1,0),800,10)&&guard.Current(1)!.Kills[0]==2,"A single same-order counter regression cannot revive completed goals");
  check(guard.Observe(1,Taken(1,2),900,10),"An intact snapshot cancels counter regression");
  check(!guard.Observe(2,Taken(1,99),0,0),"Invalid readings cannot initialize a guard");
  check(guard.Observe(2,Taken(1,3),1000,10)&&!guard.Observe(2,Taken(1,0),2000,10)&&!guard.Observe(2,Taken(1,1),3000,10)&&guard.Observe(2,Taken(1,1),4000,10),"Only matching spaced lower counters confirm a correction");
  check(!guard.Observe(1,new(false,0,[0,0,0,0,0]),1000,10)&&guard.Current(1)!.Kills[0]==2,"Single empty snapshot preserves accepted bill and counters");
  check(guard.Observe(1,Taken(1,3),2000,10),"Valid recovery cancels candidate removal");
  check(!guard.Observe(1,new(false,1,[3,0,0,0,0]),3000,10),"Completion removal also needs confirmation");
  check(!guard.Observe(1,new(false,1,[3,0,0,0,0]),3100,10),"Repeated same-frame readings cannot confirm disappearance");
  check(guard.Observe(1,new(false,1,[3,0,0,0,0]),4000,10),"Second spaced completion snapshot confirms removal");
  check(guard.Observe(1,Taken(1),5000,10),"Same order may be taken again after confirmed removal");
  check(!guard.Observe(1,Taken(2),6000,10)&&guard.Current(1)!.Order==1,"Replacement does not destroy previous goal immediately");
  check(guard.Observe(1,Taken(2),7000,10),"Consistent replacement confirmed");
  check(!guard.Observe(1,Taken(99),8000,10)&&guard.Current(1)!.Order==2,"Invalid order cannot confirm contract change");
  var restored=new BillSnapshotGuard();check(!restored.Observe(1,new(false,0,[0,0,0,0,0]),1000,10,true,2),"Restored obtained bill is protected before first native observation");
  var points=new Dictionary<int,Vector2[]>{{1,[new(0,0),new(100,0),new(200,0)]},{2,[new(9999,0)]},{3,[]}};
  var plan=RoutePlanner.Zone(new[]{1,2,3},t=>t==2,_=>true,t=>points[t],[new(100,0),new(0,10)]);
  check(plan.Entry==1&&plan.Cost==210,"Elite-only entry minimizes known circuit, not nearest single spawn");
  check(RoutePlanner.Distance(new[]{1},t=>points[t],new(0,50))==250,"Estimate includes initial leg from player position");
  check(plan.Cost<9999&&plan.Order[0]==1,"Deferred and missing-coordinate goals do not inflate available circuit");
  var passive=new EliteSearch();passive.Start([new(0,0),new(200,0)]);
  check(passive.Observe(new(0,0),[],false,0,false,0,0,true,false)==null&&passive.Round==1&&passive.Capture().Current==null,"Passive observation cannot visit or advance a spawn");
  check(passive.Observe(new(0,0),[new(50,new(2,0),false)],false,100,false,0,100,true,false)==null&&passive.ActorId==50,"Passive detection locks actual elite without returning a flag write");
  passive.Observe(new(0,0),[],false,200,false,0,200,true,false);check(passive.Lost&&passive.ActorId==50,"Paused visibility loss retains detected elite");
  passive.Observe(new(0,0),[new(50,new(2,0),true)],false,300,false,0,300,true,false);
  passive.Observe(new(0,0),[],false,100000,false,100,100000,true,false);check(passive.AwaitingCredit,"Navigation pause cannot consume death grace");
  var target=new MobHuntEntry{BillNumber=1,OrderId=2,Generation=3,MobIndex=0,MobHuntId=99,TerritoryType=1};
  var offer=ShortcutAdvisor.Evaluate(1,target,new(2000,0),new(0,0),[new(1,"Entry",new(0,0),100)])!;
  var notice=new ShortcutNotice();check(offer.ContractKey==JourneyFlow.Key(target)&&notice.ShouldOpen(offer),"Shortcut carries complete contract identity");
  check(!notice.ShouldOpen(offer with {DirectDistance=3000}),"Moving within same contract does not reannounce shortcut");
  check(notice.ShouldOpen(offer with {Generation=4}),"New contract generation has independent shortcut notification");
  check(EmptyView.Describe(false,0,0,0,0,0).Kind==EmptyKind.Loading,"Loading empty state");
  check(EmptyView.Describe(true,0,0,0,0,0).Kind==EmptyKind.NoBills,"No-bill empty state");
  check(EmptyView.Describe(true,3,0,0,0,0).Kind==EmptyKind.Completed,"Completed empty state");
  check(EmptyView.Describe(true,3,3,3,0,0).Kind==EmptyKind.Deferred,"Deferred state never claims completion");
  check(EmptyView.Describe(true,3,3,1,2,0).Kind==EmptyKind.Unavailable,"Unavailable empty state has recovery action");
  check(EmptyView.Describe(true,3,3,0,0,0).Kind==EmptyKind.Filtered,"Filtered empty state has filter action");
  check(EmptyView.Describe(true,3,3,3,0,3).Kind==EmptyKind.None,"Visible deferred rows do not show empty state");
  string folder=Path.Combine(Path.GetTempPath(),"HuntRoute-independent-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try{
   string route=Path.Combine(folder,"route.json"),file=Path.Combine(folder,"settings.json");
   File.WriteAllText(route,"{\"Version\":1,\"Settings\":{\"Sound\":true,\"Mini\":true},\"Characters\":{}}");
   var store=HuntStore.Load(route);var settings=HuntSettingsFile.Load(file,store.Settings);
   check(settings.Settings.EliteSound&&settings.Settings.NextZoneSound&&settings.Settings.Mini,"Legacy combined settings migrate without losing preference");
   settings.Settings.NextZoneSound=false;settings.Save(file);store.Save(route);
   check(!File.ReadAllText(route).Contains("Settings"),"New route file no longer owns settings");
   File.WriteAllText(route,"broken route");check(HuntStore.Load(route)!=null&&HuntSettingsFile.Load(file,new()).Settings.EliteSound,"Corrupt route cannot erase independent settings");
   settings.Save(file);File.WriteAllText(file,"broken settings");var backup=HuntSettingsFile.Load(file,new());
   check(backup.Recovered&&backup.Settings.EliteSound&&!backup.Settings.NextZoneSound,"Settings recover independent sound preferences from backup");
   backup.Save(file);check(HuntSettingsFile.Load(file+".bak",new()).Settings.EliteSound,"Recovery save cannot overwrite valid settings backup");
   var history=new CompletionHistory();string historyFile=Path.Combine(folder,"history.json");history.Save(historyFile);File.WriteAllText(file,"broken again");HuntSettingsFile.Load(file,new());check(CompletionHistory.Load(historyFile).Version==1,"Corrupt settings leave completion history independent");
  }finally{Directory.Delete(folder,true);}
 }
}
