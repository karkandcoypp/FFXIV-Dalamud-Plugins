using HuntRoute;
using System.Numerics;
static class LifecycleChecks {
 public static void Run(Action<bool,string> check) {
  var s=new EliteSearch();s.Start([new(0,0),new(200,0)]);
  check(s.Observe(new(-100,0),[new(10,new(2,4),false)],false,0,false,0,0)==new Vector2(2,4),"Actor instance locks");
  s.Observe(new(0,0),[],false,100,false,0,0);check(s.Lost && s.ActorId==10,"Unobserved disappearance preserves instance");
  s.Observe(new(0,0),[new(11,new(8,4),true)],false,200,false,0,0);check(!s.AwaitingCredit && s.ActorId==10,"Unrelated corpse cannot release lock");
  s.Observe(new(0,0),[new(10,new(2,4),true)],false,300,false,0,0);check(s.AwaitingCredit,"Observed death waits for credit");
  for(int i=1;i<=29;i++)s.Observe(new(0,0),[],false,300+i*100,false,i/10,300+i*100);
  check(s.ActorId==10,"No premature release before grace");
  s.Observe(new(0,0),[],false,3300,false,3,3300);check(s.ActorId==null && !s.AwaitingCredit,"Uncredited death automatically resumes search");
  s.Observe(new(0,0),[new(10,new(2,4),true)],false,3400,false,3,3300);check(!s.AwaitingCredit,"Repeated corpse cannot restart grace");
  s.Observe(new(0,0),[new(12,new(8,4),false)],false,3500,false,3,3300);check(s.ActorId==12,"New instance may lock");
  s.Observe(new(0,0),[],false,3600,true,4,3600);check(s.ActorId==null && s.Round==0,"Late credit stops all search");
  s.Start([new(0,0)]);s.Observe(new(0,0),[new(20,new(1,1),false)],false,0,false,0,0);
  s.Observe(new(0,0),[new(20,new(1,1),true)],false,100,false,0,0);s.Suspend();
  s.Observe(new(0,0),[],false,100000,false,2,100000);check(s.AwaitingCredit,"Loading gap does not consume grace");
  for(int i=1;i<=40;i++)s.Observe(new(0,0),[],false,100000+i*100,false,2,0);
  check(s.AwaitingCredit,"Stale contract snapshots cannot infer missing credit");
  s.Observe(new(0,0),[new(20,new(1,1),false)],false,105000,false,3,105000);check(!s.AwaitingCredit && !s.Lost,"Reappearing live instance cancels death suspicion");
  s.Observe(new(0,0),[],false,105100,false,3,105000);check(s.ReleaseLost() && s.ActorId==null,"Explicit lost-target release resumes search");
  var flow=new JourneyFlow{Active=true,Pauses=PauseReason.User|PauseReason.Marker};
  flow.Pauses&=~PauseReason.Marker;check(!flow.CanNavigate,"Clearing marker pause does not clear user pause");
  var mob=new MobHuntEntry{BillNumber=1,OrderId=2,MobIndex=0,MobHuntId=100,TerritoryType=10};
  flow.Deferred.Add(JourneyFlow.Key(mob));check(flow.Excluded(mob),"Manual deferral keys include contract generation");
  var nextOrder=new MobHuntEntry{BillNumber=1,OrderId=3,MobIndex=0,MobHuntId=100,TerritoryType=10};check(!flow.Excluded(nextOrder),"Old deferral does not hide a new generation");
  var saved=new SavedJourney{Active=true,Target=JourneyFlow.Key(mob),Manual=JourneyFlow.Key(mob),Deferred=flow.Deferred.ToHashSet()};
  var restored=new JourneyFlow();restored.Load(saved);check(restored.Active && !restored.CanNavigate && restored.Pauses==PauseReason.Restored,"Persisted route always restores paused");
  var pending=new PendingJourney(1,2,3,1000,"target",false);check(!pending.Expired(120999)&&pending.Expired(121000),"Travel timeout uses exact monotonic boundary");
  var settings=new HuntSettings{MinSeconds=float.NaN,MinPercent=101};settings.Validate();check(settings.MinSeconds==10&&settings.MinPercent==20,"Invalid settings recover valid defaults");
  string folder=Path.Combine(Path.GetTempPath(),"HuntRoute-state-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"state.json");
  try {
   var store=new HuntStore();store.Characters[42]=saved;store.Save(path);store.Characters[99]=new(){Active=false};store.Save(path);
   var read=HuntStore.Load(path);check(read.Characters.Count==2 && read.Characters[42].Manual==saved.Manual,"Route persistence is separate for characters");
   File.WriteAllText(path,"broken");read=HuntStore.Load(path);check(read.Characters.ContainsKey(42)&&!read.Characters.ContainsKey(99),"Corrupt state is quarantined and backup restored");
   check(Directory.GetFiles(folder,"*.invalid-*").Length==1,"Corrupt source retained for diagnosis");
   read.Save(path);check(HuntStore.Load(path+".bak").Characters.ContainsKey(42),"First recovery save cannot overwrite valid backup with corrupted source");
  }finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
  string historyDirectory=Path.Combine(Path.GetTempPath(),"HuntRoute-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(historyDirectory);string historyFile=Path.Combine(historyDirectory,"history.json");
  try{var history=new CompletionHistory();history.Save(historyFile);history.Save(historyFile);File.WriteAllText(historyFile,"bad");var recovered=CompletionHistory.LoadRecovering(historyFile);check(recovered.Recovered,"Completion history recovers with explicit recovery status");recovered.Save(historyFile);check(CompletionHistory.Load(historyFile+".bak")!=null,"Completion history backup survives recovery save");File.Delete(historyFile+".bak");File.WriteAllText(historyFile,"bad again");recovered=CompletionHistory.LoadRecovering(historyFile);recovered.Observe(10,1,false,DateTimeOffset.UtcNow,new(true,false,1,1,true));check(!recovered.IsComplete(10,1,false,DateTimeOffset.UtcNow),"Lost history does not manufacture today's completion from ambiguous retained counts");}finally{foreach(string file in Directory.GetFiles(historyDirectory))File.Delete(file);Directory.Delete(historyDirectory);}
  var cycle=new EliteSearch();cycle.Start([new(0,0),new(200,0)]);cycle.Tick(new(0,0),null,false);var progress=cycle.Capture();
  var reload=new EliteSearch();reload.Start([new(0,0),new(200,0)]);reload.Restore(progress);check(reload.Capture().Current==progress.Current&&reload.ActorId==null,"Circle progress restores without NPC identities");
  PlanAuditChecks.Run(check);
  WorldMarkerChecks.Run(check);
  Console.WriteLine("PASS actor death, delayed credit, visibility, pause, travel and persistence lifecycle");
 }
}
