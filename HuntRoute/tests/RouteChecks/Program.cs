using System.Numerics;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
int checks=0;
void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
float Cost(int[] order, Vector2[] p, Vector2[] e, int start) => order.Length==0 ? 0 : (start<0?0:Vector2.Distance(e[start],p[order[0]]))+order.Zip(order.Skip(1),(a,b)=>Vector2.Distance(p[a],p[b])).Sum();
IEnumerable<int[]> Permute(int[] x) { if(x.Length==0) { yield return []; yield break; } foreach(int i in x) foreach(var tail in Permute(x.Where(v=>v!=i).ToArray())) yield return new[]{i}.Concat(tail).ToArray(); }
var random=new Random(args.Length<2?731:int.Parse(args[1]));
for(int test=0;test<35;test++) {
 int n=1+test%7;
 var p=Enumerable.Range(0,n).Select(_=>new Vector2(random.Next(40),random.Next(40))).ToArray();
 var e=Enumerable.Range(0,test%4).Select(_=>new Vector2(random.Next(40),random.Next(40))).ToArray();
 var route=RouteSolver.Solve(p,e);
 Check(route.Order.Distinct().Count()==n,"Targets lost or duplicated");
 float optimal=Permute(Enumerable.Range(0,n).ToArray()).Min(o=>e.Length==0?Cost(o,p,e,-1):Enumerable.Range(0,e.Length).Min(s=>Cost(o,p,e,s)));
 Check(Math.Abs(Cost(route.Order,p,e,route.Entry)-optimal)<.001,"Route differs from exhaustive optimum");
}
Check(RouteSolver.Solve([],[]).Entry==-1,"Empty route");
var example=RouteSolver.Solve([new(1,1),new(39,39),new(2,1)],[new(0,0)]);
Check(example.Order.SequenceEqual(new[]{0,2,1}),"Nearby targets separated by a far target");
var expansionNotice=new ExpansionCompletionNotice();
ContractProgress pendingDaily=new(3,3,2,0), finishedDaily=new(3,0,3,0), pendingWeekly=new(1,1,0,0), finishedWeekly=new(1,0,1,0);
Check(!expansionNotice.Observe("Endwalker",finishedDaily,finishedWeekly,true),"Already complete on login");
Check(!expansionNotice.Observe("Endwalker",pendingDaily,pendingWeekly,true),"Incomplete contracts notified");
Check(!expansionNotice.Observe("Endwalker",finishedDaily,pendingWeekly,true),"Weekly target still pending");
Check(!expansionNotice.Observe("Endwalker",finishedDaily,finishedWeekly,false),"Unreliable snapshot notified");
Check(!expansionNotice.Observe("Stormblood",finishedDaily,finishedWeekly,true),"Expansions share completion state");
Check(expansionNotice.Observe("Endwalker",finishedDaily,finishedWeekly,true),"Missing completion notice");
Check(!expansionNotice.Observe("Endwalker",finishedDaily,finishedWeekly,true),"Repeated completion notice");
Check(!expansionNotice.Observe("Endwalker",pendingDaily,finishedWeekly,true),"Repeat cycle notified early");
Check(expansionNotice.Observe("Endwalker",finishedDaily,finishedWeekly,true),"Next reset cycle did not rearm");
Check(!expansionNotice.Observe("Endwalker",pendingDaily,default,true),"Pending with locked weekly");
expansionNotice.Reset();
Check(!expansionNotice.Observe("Endwalker",finishedDaily,default,true),"Completion notice leaked across characters");
Check(!expansionNotice.Observe("Endwalker",pendingDaily,default,true)&&expansionNotice.Observe("Endwalker",finishedDaily,default,true),"Locked weekly blocked daily completion");
var daily=DateTimeOffset.Parse("2026-11-07T15:00:00Z");
var archiveHistory=new CompletionHistory();
var archiveTaken=new BillState(true,true,1,1,false);
var archiveCredited=archiveTaken with {Completed=true};
var archiveClosed=archiveCredited with {Obtained=false};
archiveHistory.Observe(123,14,false,daily,archiveTaken);
Check(archiveHistory.ArchivedOrder(123,14,false,daily,archiveTaken)==0,"Uncredited goals cannot enter the archive");
archiveHistory.Observe(123,14,false,daily,archiveCredited);
Check(archiveHistory.ArchivedOrder(123,14,false,daily,archiveCredited)==0,"A held contract must not duplicate its live goals");
archiveHistory.Observe(123,14,false,daily,archiveClosed);
Check(archiveHistory.ArchivedOrder(123,14,false,daily,archiveClosed)==1,"A credited contract remains in the goal list after closing");
Check(archiveHistory.ArchivedOrder(123,14,false,daily.AddDays(1),archiveClosed)==0,"A previous period does not count after reset");
Check(archiveHistory.ArchivedOrder(456,14,false,daily,archiveClosed)==0,"Closed goals must not leak between characters");
Check(ResetClock.Next(daily.AddTicks(-1),false)==daily,"Before daily reset");
Check(ResetClock.Period(daily,false)==daily,"At daily reset");
Check(ResetClock.Next(daily,false)==daily.AddDays(1),"After daily reset");
var weekly=DateTimeOffset.Parse("2026-11-10T08:00:00Z");
Check(ResetClock.Next(weekly.AddTicks(-1),true)==weekly,"Before weekly reset");
Check(ResetClock.Period(weekly,true)==weekly,"At weekly reset");
Check(ResetClock.Next(weekly,true)==weekly.AddDays(7),"After weekly reset");
Check(ResetClock.Period(daily.ToOffset(TimeSpan.FromHours(3)),false)==daily,"Timezone conversion");
var p15=Enumerable.Range(0,15).Select(i=>new Vector2(i%4,i/4)).ToArray();
Check(RouteSolver.Solve(p15,[Vector2.Zero]).Order.Length==15,"Full daily bill set");
// Acquisition state must be recoverable from a fresh game snapshot, without an observed pickup.
var ids=new byte[]{18,19,20};
ContractProgress Summary(params BillState[] bills) => ContractProgress.Summarize(ids,ids.Zip(bills).ToDictionary(p=>p.First,p=>p.Second));
var taken=new BillState(true,true,4,4,false);
var absent=new BillState(true,false,0,4,false);
Check(Summary(taken,taken,taken).Label=="Контракты взяты","Already-held bills after plugin startup");
Check(Summary(taken,taken,taken).Taken==3,"No local pickup history required");
Check(Summary(taken,taken,absent).Label=="Взято 2 из 3","Partial daily acquisition");
Check(Summary(absent,absent,absent).Label=="Контракты не взяты","No held bills");
Check(Summary(taken with {Completed=true},taken with {Completed=true},taken with {Completed=true}).Label=="Контракты выполнены","Completed current bills");
Check(Summary(taken with {AvailableId=8},taken,taken).NewSets==1,"Old active bill versus new board set");
Check(Summary(taken with {AvailableId=8},taken,taken).Label.Contains("есть новые"),"Old bills must not hide a new set");
Check(Summary(taken with {AvailableId=0},taken,taken).Taken==3,"Unknown available IDs do not hide actual held bills");
Check(Summary(taken with {Obtained=false},absent,absent).Taken==0,"Retained ID alone is not an active contract");
Check(Summary(taken with {ObtainedId=0},absent,absent).Taken==0,"Empty obtained ID cannot count as held");
Check(Summary(default,default,default).Total==0,"Locked contracts");
Check(Summary(taken,taken,default).Label=="Контракты взяты","Only unlocked tiers count");
Check(ContractProgress.Summarize(new byte[]{21},new Dictionary<byte,BillState>{{21,taken}}).Taken==1,"Weekly acquisition");
Check(ContractProgress.Summarize(new byte[]{21},new Dictionary<byte,BillState>{{21,absent}}).Taken==0,"Character switch with no bills");
Check(Summary(taken with {Completed=true,AvailableId=8},taken,taken).Label.Contains("есть новые"),"Retained completion cannot suppress a new board set");
// Travel policy: same-zone requests never cast, even when busy or the entry is locked.
Check(TravelPolicy.Decide(397,397,true,true,false)==TravelDecision.AlreadyThere,"Same-zone teleport suppressed");
Check(TravelPolicy.Decide(397,397,true,false,true)==TravelDecision.AlreadyThere,"Same-zone marker needs no unlocked teleport");
Check(TravelPolicy.Decide(397,401,true,true,false)==TravelDecision.Teleport,"Different-zone unlocked teleport");
Check(TravelPolicy.Decide(397,401,true,false,false)==TravelDecision.Locked,"Locked teleport denied");
Check(TravelPolicy.Decide(397,401,true,true,true)==TravelDecision.Busy,"Busy or pending teleport denied");
Check(TravelPolicy.Decide(397,401,false,true,false)==TravelDecision.NotReady,"Logged-out state denied");
Check(TravelPolicy.Decide(397,0,true,true,false)==TravelDecision.NotReady,"Missing destination denied");
Check(!BillState.FromGame(true,true,255,255,false,18).Taken,"Invalid bill IDs cannot leak into another tier");
Check(!BillState.FromGame(true,true,2,255,false,18).HasNewSet,"Invalid available ID is unknown, not a new bill");
var routePoints=new[]{new Vector2(1,0),new Vector2(30,0),new Vector2(2,0),new Vector2(2,0)};
var filtered=RoutePlanner.Order(new[]{0,1,2,3},i=>i==0,i=>(Vector2?)routePoints[i],new[]{new Vector2(29,0)});
Check(filtered.Order.SequenceEqual(new[]{1,2,3,0}),"Route begins at player anchor and moves completed target to end");
var unknown=RoutePlanner.Order(new[]{0,1,2},_=>false,i=>i==1?null:(Vector2?)routePoints[i],new[]{Vector2.Zero});
Check(unknown.Order.SequenceEqual(new[]{0,2,1}),"Unknown positions do not distort known route");
Check(RouteSolver.Solve(Enumerable.Range(0,25).Select(i=>new Vector2(i,0)).ToArray(),new[]{Vector2.Zero}).Order.Distinct().Count()==25,"Large route remains bounded and usable");
try { RouteSolver.Solve(new[]{new Vector2(float.NaN,1)},new[]{Vector2.Zero}); Check(false,"Non-finite route accepted"); } catch(ArgumentException) { Check(true,"Non-finite route rejected"); }
// Follow a daily route across zones without requesting any automatic teleport.
MobHuntEntry Target(byte index,uint id,uint zone)=>new(){BillNumber=1,MobIndex=index,MobHuntId=id,TerritoryType=zone,NeededKills=1};
var targetA=Target(0,3481,397);var targetB=Target(1,3479,397);var targetC=Target(2,3494,401);
var ordered=new[]{targetA,targetB,targetC};var finished=new HashSet<uint>();var follow=new RouteSession();
follow.Start(targetA,new byte[]{1});
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,418)==null && follow.Phase==RoutePhase.WaitingForZone,"No map flag in an unrelated zone before arrival");
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==targetA,"First marker after arrival");
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==null,"Do not rewrite map flag every tick");
follow.RetryMarker();Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==targetA,"Retry marker after map agent is temporarily unavailable");
finished.Add(targetA.MobHuntId);
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==targetB,"Advance automatically after required kills");
finished.Add(targetB.MobHuntId);
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==null && follow.Phase==RoutePhase.NextZone && follow.Suggested==targetC,"Offer next zone without automatic travel or out-of-zone marker");
follow.Start(targetC,new byte[]{1});
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,397)==null,"Next-zone marker waits for arrival");
Check(follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,401)==targetC,"Continue marking after user-approved zone change");
finished.Add(targetC.MobHuntId);follow.Tick(ordered,m=>finished.Contains(m.MobHuntId),_=>true,401);
Check(follow.Phase==RoutePhase.Finished,"Route completion");
follow.Start(targetA,new byte[]{1});follow.Tick(ordered,_=>false,_=>true,397);follow.Tick(new[]{targetB,targetC},_=>false,_=>true,397);
Check(follow.Phase==RoutePhase.Changed,"Stop on replaced or discarded contract");
follow.Stop();Check(follow.Tick(ordered,_=>false,_=>true,397)==null,"Stopped route never sets a marker");
// Completion must still propose the next zone if the game removed the completed bill from the target list.
var completion=new RouteSession();completion.Start(targetA,new byte[]{1});
completion.Tick(ordered,_=>false,_=>true,397);
completion.Tick(new[]{targetC},m=>m.TerritoryType==397,_=>true,397);
Check(completion.Phase==RoutePhase.NextZone && completion.Suggested==targetC,"Offer next zone after completed target disappears");
var notice=new NextZoneNotice();
Check(notice.ShouldOpen(completion),"Open next-zone offer immediately, independently of main-window visibility");
Check(!notice.ShouldOpen(completion),"Do not reopen dismissed offer on every tick");
completion.Tick(new[]{targetC},m=>m.TerritoryType==397,_=>true,397);
Check(!notice.ShouldOpen(completion),"Unchanged next-zone state does not repeat offer");
var sameZoneReplacement=new MobHuntEntry{BillNumber=targetC.BillNumber,MobIndex=targetC.MobIndex,Generation=targetC.Generation+1,TerritoryType=targetC.TerritoryType};
completion.Suggest(sameZoneReplacement);
Check(notice.ShouldOpen(completion,70,100)&&!notice.ShouldOpen(completion,70,100),"Replacement contract in the same zone gets exactly one new proposal");
Check(notice.ShouldOpen(completion,71,100),"Changed destination aetheryte can replace the proposal");
completion.Start(targetA,new byte[]{1});completion.Tick(ordered,m=>m.TerritoryType==397,_=>true,397);
Check(notice.ShouldOpen(completion),"A new route run can offer the same zone again");
completion.Stop();Check(!notice.ShouldOpen(completion),"Do not offer a teleport after route stop");
// A single Start (the next-zone button) must survive loading and resume the whole route on arrival.
var transit=new RouteSession();transit.Start(targetC,new byte[]{1});uint transitRun=transit.RunId;
transit.ZoneTransition();
Check(transit.Tick(System.Array.Empty<MobHuntEntry>(),_=>false,_=>true,397)==null && transit.Phase==RoutePhase.WaitingForZone,"No cancellation or flag from empty source-zone snapshot during teleport");
Check(transit.Tick(System.Array.Empty<MobHuntEntry>(),_=>false,_=>true,401)==null && transit.Phase==RoutePhase.WaitingForZone,"Wait for bill data immediately after arrival");
Check(transit.Tick(ordered,_=>false,_=>true,401)==targetC && transit.Phase==RoutePhase.Following,"First correct target is flagged without a second Start");
Check(transit.RunId==transitRun,"Arrival must not require restarting the route");
transit.RetryMarker();Check(transit.Tick(ordered,_=>false,_=>true,401)==targetC,"Retry first marker when map agent was not ready");
Check(transit.Tick(ordered,_=>false,_=>true,401)==null,"Do not repeat first marker after success");
var targetD=Target(3,3495,401);
Check(transit.Tick(ordered.Append(targetD).ToArray(),m=>m==targetC,_=>true,401)==targetD,"Continue to the second target of the new zone automatically");
transit.Start(targetC,new byte[]{1});
for(int i=0;i<4;i++) transit.Tick(System.Array.Empty<MobHuntEntry>(),_=>false,_=>true,401);
Check(transit.Phase==RoutePhase.Changed,"Arrival grace period cannot conceal a permanently discarded contract");
transit.Stop();Check(transit.Phase==RoutePhase.Idle && transit.Target==null,"Real logout or stop clears the route");
// Same-zone teleport preserves the active (not first) goal and continues through the whole zone.
var intra=new RouteSession();var intraA=new MobHuntEntry{BillNumber=1,MobIndex=0,MobHuntId=10001,TerritoryType=397,NeededKills=2};
var intraB=new MobHuntEntry{BillNumber=1,MobIndex=1,MobHuntId=10002,TerritoryType=397,NeededKills=3};var intraC=Target(2,10003,397);
var intraNext=Target(3,10004,401);var intraOrdered=new[]{intraA,intraB,intraC,intraNext};
var counts=new Dictionary<uint,int>();bool Killed(MobHuntEntry m)=>counts.GetValueOrDefault(m.MobHuntId)>=m.NeededKills;
intra.Start(intraA,new byte[]{1});uint intraRun=intra.RunId;
Check(intra.Tick(intraOrdered,Killed,_=>true,397)==intraA,"Initial zone target flagged");
counts[intraA.MobHuntId]=1;
Check(intra.Tick(intraOrdered,Killed,_=>true,397)==null && intra.Target==intraA,"Partial kills do not advance marker");
counts[intraA.MobHuntId]=2;
Check(intra.Tick(intraOrdered,Killed,_=>true,397)==intraB,"Full first pack advances to second mob");
intra.ZoneTransition();
Check(intra.Target==intraB && intra.RunId==intraRun && intra.Phase==RoutePhase.WaitingForZone,"Intra-zone teleport keeps second mob and route identity");
Check(intra.Tick(Array.Empty<MobHuntEntry>(),Killed,_=>true,397)==null,"Wait for same-zone arrival snapshot");
// Arrival changes distance order; the selected goal must still win.
var arrivalOrder=new[]{intraC,intraB,intraA,intraNext};
Check(intra.Tick(arrivalOrder,Killed,_=>true,397)==intraB && intra.Phase==RoutePhase.Following,"Same-zone arrival immediately reflags selected second mob without Start");
intra.RetryMarker();
Check(intra.Tick(arrivalOrder,Killed,_=>true,397)==intraB,"Unavailable map agent retries same target");
Check(intra.Tick(arrivalOrder,Killed,_=>true,397)==null,"Successful arrival flag is not spammed");
counts[intraB.MobHuntId]=2;
Check(intra.Tick(arrivalOrder,Killed,_=>true,397)==null && intra.Target==intraB,"Second pack remains until all three kills");
counts[intraB.MobHuntId]=3;
Check(intra.Tick(new[]{intraC,intraNext},Killed,_=>true,397)==intraC,"Completed disappearing pack advances to remaining zone mob");
intra.ZoneTransition();counts[intraC.MobHuntId]=1;
Check(intra.Tick(new[]{intraNext},Killed,_=>true,397)==null && intra.Phase==RoutePhase.NextZone && intra.Suggested==intraNext,"Completion during arrival offers next zone without stale local flag");
Check(intra.RunId==intraRun,"Whole same-zone sequence uses a single Start");
var allLocal=new RouteSession();allLocal.Start(intraA,new byte[]{1});
Check(allLocal.Tick(intraOrdered.Take(3).ToArray(),Killed,_=>true,397)==null && allLocal.Phase==RoutePhase.Finished,"All local packs done ends route without revisiting completed mobs");
// Long-flight shortcuts are rare suggestions, never automatic same-zone casts.
Check(TravelPolicy.Decide(397,397,true,true,false,true)==TravelDecision.Teleport,"Explicit same-zone acceptance permits exactly the requested teleport");
Check(TravelPolicy.Decide(397,397,true,true,true,true)==TravelDecision.Busy,"Explicit same-zone acceptance still respects busy state");
Check(TravelPolicy.Decide(397,397,true,false,false,true)==TravelDecision.Locked,"Explicit same-zone acceptance cannot bypass unlocks");
var cheap=new ShortcutCandidate(71,"Nearby aetheryte",new Vector2(1900,0),100);
ShortcutOffer? Shortcut(Vector2 player,params ShortcutCandidate[] entries)=>ShortcutAdvisor.Evaluate(1,targetA,player,new Vector2(2000,0),entries);
Check(Shortcut(Vector2.Zero,cheap) is {GilCost:100,FinalDistance:100},"Far target with nearby cheap aetheryte is offered");
Check(Shortcut(new Vector2(1500,0),cheap)==null,"Do not offer short flights");
Check(Shortcut(Vector2.Zero,cheap with {GilCost=201})==null,"Do not suggest expensive intra-zone teleports");
Check(Shortcut(Vector2.Zero,cheap with {Position=new Vector2(1500,0)})!=null,"A useful last leg over 300m is allowed");
Check(Shortcut(new Vector2(800,0),cheap with {Position=new Vector2(1700,0)})!=null,"Offer saves enough time after teleport overhead");
Check(Shortcut(Vector2.Zero)==null,"No unlocked candidate means no offer");
Check(Shortcut(new Vector2(float.NaN,0),cheap)==null,"No shortcut during invalid-position frames");
var shortcutNotice=new ShortcutNotice();var suggestedShortcut=Shortcut(Vector2.Zero,cheap)!;
Check(shortcutNotice.ShouldOpen(suggestedShortcut)&&!shortcutNotice.ShouldOpen(suggestedShortcut),"Do not repeat shortcut offer after decline");
Check(shortcutNotice.ShouldOpen(suggestedShortcut with {RunId=2}),"A new route can offer a shortcut anew");
var gnome=ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(1094,0),new[]{new ShortcutCandidate(1,"Mord Souq",new(654,0),100)});
var wallGoal=new Vector3(400,0,0);
var wallRoute=ShortcutAdvisor.PathLength(Vector3.Zero,wallGoal,[new(0,0,400),new(400,0,400),wallGoal]);
ShortcutCandidate[] wallEntries=[new(1,"Near",new(390,0),100)];
Check(wallRoute==1200&&ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(400,0),wallEntries)==null&&
      ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(400,0),wallEntries,routeDistance:wallRoute,routeFromAetherytes:new Dictionary<uint,float>{{1,10}}) is {UsesNavmesh:true,Aetheryte:1},
      "A wall detour makes a teleport worthwhile even when the straight-line estimate does not");
Check(ShortcutAdvisor.PathLength(Vector3.Zero,wallGoal,[new(200,0,0)])==null,"Partial paths fall back to the straight-line estimate");
Check(gnome is not null && Math.Abs(gnome.SavedSeconds-14.7f)<.01f,"Gnome regression: 1094m direct, 440m final saves 14.7s");
Check(ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(1094,0),new[]{new ShortcutCandidate(1,"Mord Souq",new(654,0),100)},30)==null,"Slow loading removes unprofitable suggestion");
Check(ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(2000,0),new[]{cheap},float.NaN)==null,"Invalid timing cannot produce an offer");
Check(Shortcut(Vector2.Zero,cheap with {Position=new Vector2(float.NaN,0)})==null,"Invalid aetheryte cannot produce an offer");
var timing=new ShortcutTiming();timing.Begin(1000);timing.Ready(6000);
Check(timing.OverheadSeconds==18,"No loading does not train timing");
timing.Loading(7000);timing.Ready(13000);
Check(timing.OverheadSeconds==15,"Observed cast and loading plus mount allowance update estimate");
timing.Begin(20000);timing.Reset();timing.Loading(30000);timing.Ready(50000);
Check(timing.OverheadSeconds==15,"Reset cannot count unrelated loading");
timing.Begin(60000);timing.Loading(65000);timing.Ready(190000);
Check(timing.OverheadSeconds==15,"Expired sample ignored");
timing.Begin(200000);timing.Loading(235000);timing.Ready(245000);
Check(timing.OverheadSeconds==15,"Cancelled cast cannot train timing from a later loading screen");
Check(ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(5000,0),new[]{new ShortcutCandidate(1,"Marginal",new(700,0),100)})==null,"Long flights still require twenty percent savings");
Check(ShortcutAdvisor.Evaluate(1,targetA,Vector2.Zero,new(600,0),new[]{new ShortcutCandidate(1,"Near",new(590,0),100)})!=null,"Thirty-second flight with ten-second saving qualifies");
// Native UI requests serialize with other dialogs and execute an affirmative response once.
var confirmations=new ConfirmationQueue();bool requestValid=true,requestReady=false;int accepted=0;
var question=new ConfirmationRequest("next","next-1","Continue?",()=>requestValid,()=>requestReady,()=>{accepted++;return true;});
confirmations.Offer(question);
Check(confirmations.Activate(true)==null,"Busy character defers native confirmation");
requestReady=true;Check(confirmations.Activate(false)==null,"An existing game dialog defers our dialog");
Check(confirmations.Activate(true)==question,"Open native request once UI and character are ready");
Check(confirmations.Resolve("unrelated-game-dialog",true)==null && confirmations.Active==question,"Never consume another dialog response");
var approved=confirmations.Resolve(question.Key,true);Check(approved!=null && approved.Accept() && accepted==1,"Yes executes exactly one approved action");
Check(confirmations.Resolve(question.Key,true)==null && accepted==1,"Duplicate native response cannot teleport twice");
confirmations.Offer(question);Check(confirmations.Activate(true)==null,"Do not repeatedly reopen answered request");
var declined=question with {Key="declined"};confirmations.Offer(declined);confirmations.Activate(true);
Check(confirmations.Resolve(declined.Key,false)==null && accepted==1,"No/close does not teleport");
var stale=question with {Key="stale"};confirmations.Offer(stale);confirmations.Activate(true);requestValid=false;
Check(confirmations.Resolve(stale.Key,true)==null && accepted==1,"Changed route invalidates a previously displayed Yes");
requestValid=true;confirmations.Offer(question with {Owner="shortcut",Key="shortcut-1"});confirmations.Offer(question with {Key="next-2"});
Check(confirmations.Activate(true)?.Owner=="next","Next-zone transition has priority over shortcut");
confirmations.Cancel("next");Check(confirmations.Activate(true)?.Owner=="shortcut","Cancelling one request does not remove another owner");
confirmations.Clear();Check(confirmations.Active==null && confirmations.Activate(true)==null,"Plugin unload clears confirmation queue");
if(args.Length>0) {
 using var game=new GameData(args[0]);
 foreach(uint id in new uint[]{70,111,133,182,216,75}) { var a=game.Excel.GetSheet<Aetheryte>().GetRow(id); Console.WriteLine($"Aetheryte {id}: {a.PlaceName.Value.Name}, territory={a.Territory.RowId}, map={a.Territory.Value.Map.RowId}"); }
 foreach(uint id in new uint[]{418,628,819,962,1185}) { var t=game.Excel.GetSheet<TerritoryType>().GetRow(id); Console.WriteLine($"Territory {id}: {t.PlaceName.Value.Name}, map={t.Map.RowId}"); }
 var dataset=System.Text.Json.JsonSerializer.Deserialize<Dictionary<uint,EliteSpawnSet>>(File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../data/elite-spawns.json"))))!;
 int weeklyTargets=0;
 foreach(var t in game.Excel.GetSheet<MobHuntOrderType>().Where(t=>t.RowId<22 && t.Type==2)) {
   foreach(var orders in game.Excel.GetSubrowSheet<MobHuntOrder>().Where(o=>o.RowId>=t.OrderStart.RowId && o.RowId<t.OrderStart.RowId+t.OrderAmount)) {
     foreach(var order in orders) {
       if(order.Target.RowId==0) continue;
       var target=order.Target.Value; uint territory=target.Map.Value.TerritoryType.RowId;
       Check(dataset.ContainsKey(target.Name.RowId),$"Missing elite spawn zone {territory}"); weeklyTargets++;
     }
   }
 }
 foreach(var pair in dataset) {
   var territory=game.Excel.GetSheet<TerritoryType>().GetRow(pair.Value.Territory); var map=territory.Map.Value;
   string id=map.Id.ToString(); string texture=$"ui/map/{id}/{id.Replace("/", "")}_m.tex";
   Check(game.FileExists(texture),$"Missing map texture {texture}");
   foreach(var point in pair.Value.Points) Check(point.Length==2 && point.All(float.IsFinite) && point.All(v=>v>0 && v<100),"Invalid spawn coordinate");
 }
 Console.WriteLine($"Validated {weeklyTargets} weekly targets, {dataset.Values.Select(p=>p.Territory).Distinct().Count()} spawn maps, {dataset.Values.Sum(p=>p.Points.Length)} possible B-rank points and all map textures.");
}


// Complete same-zone policy matrix: no hidden cast when readiness, unlocks or busy state reject it.
foreach(bool ready in new[]{false,true})foreach(bool unlocked in new[]{false,true})foreach(bool busy in new[]{false,true})foreach(bool allow in new[]{false,true})foreach(bool same in new[]{false,true}) {
 var expected=!ready?TravelDecision.NotReady:same&&!allow?TravelDecision.AlreadyThere:!unlocked?TravelDecision.Locked:busy?TravelDecision.Busy:TravelDecision.Teleport;
 Check(TravelPolicy.Decide(397,same?397u:401u,ready,unlocked,busy,allow)==expected,"Complete teleport policy matrix");
}
var largePoints=Enumerable.Range(0,24).Select(i=>new Vector2(i*13%41,i*7%37)).ToArray();
var largeRoute=RouteSolver.Solve(largePoints,new[]{Vector2.Zero});
Check(largeRoute.Order.Length==24 && largeRoute.Order.Distinct().Count()==24 && largeRoute.Entry==0,"Fallback route includes every target exactly once");
Console.WriteLine($"PASS extended audit: {checks} checks, random seed {(args.Length<2?731:int.Parse(args[1]))}");

var retained=BillState.FromGame(true,false,7,7,true,10);
Check(Summary(retained,retained,retained).AllCompleted,"Completed removed bills still count for today");
Check(Summary(retained,retained,retained).Taken==0 && Summary(retained,retained,retained).Completed==3,"Do not confuse completed with held bills");
Check(Summary(retained,retained,absent).Completed==2 && !Summary(retained,retained,absent).AllCompleted,"Partial completion does not check the entire expansion");
Check(!Summary(retained with {AvailableId=8},retained with {AvailableId=8},retained with {AvailableId=8}).AllCompleted,"New daily sets clear today's completion check");
Check(!Summary(retained with {AvailableId=0},retained,retained).AllCompleted,"Unknown available set cannot assert completion today");
Check(!BillCompletion.IsComplete(0,10,new[]{new BillRequirement(0,1)},_=>5),"Retained zero ID cannot be completed");
Check(!BillCompletion.IsComplete(11,10,new[]{new BillRequirement(0,1)},_=>5),"Invalid retained ID cannot be completed");
Check(!BillCompletion.IsComplete(7,10,Array.Empty<BillRequirement>(),_=>5),"Empty target list is not completion");
Check(BillCompletion.IsComplete(7,10,new[]{new BillRequirement(0,1),new BillRequirement(1,3)},i=>i==0?1:3),"All retained kill requirements establish completion");
Check(!BillCompletion.IsComplete(7,10,new[]{new BillRequirement(0,1),new BillRequirement(1,3)},_=>1),"Abandoned partial bill is not completion");
Check(!BillCompletion.IsComplete(7,10,new[]{new BillRequirement(5,1)},_=>1),"Invalid kill slot is rejected");
Console.WriteLine($"PASS completion audit: {checks} checks");

var hecatomb=EliteSpawns.Sets.Single(p=>p.Value.Name=="Juggler Hecatomb");
var worm=EliteSpawns.Sets.Single(p=>p.Value.Name=="Worm of the Well");
Vector2[] ElitePoints(KeyValuePair<uint,EliteSpawnSet> entry)=>EliteSpawns.For(new(){MobHuntId=entry.Key,MapId=entry.Value.Map,TerritoryType=entry.Value.Territory,IsEliteMark=true});
Check(hecatomb.Value.Territory==worm.Value.Territory && ElitePoints(hecatomb).Length==9 && ElitePoints(worm).Length==9,"Amh Araeng uses nine points per selected B-rank, not the old zone union of eighteen");
Check(!ElitePoints(hecatomb).Intersect(ElitePoints(worm)).Any(),"Changing weekly mob does not reuse the other mark's points");
Check(EliteSpawns.For(new(){MobHuntId=hecatomb.Key,MapId=hecatomb.Value.Map,TerritoryType=hecatomb.Value.Territory}).Length==0,"Daily mob cannot request elite points");
Check(EliteSpawns.For(new(){MobHuntId=uint.MaxValue,MapId=hecatomb.Value.Map,TerritoryType=hecatomb.Value.Territory,IsEliteMark=true}).Length==0,"Unknown elite never falls back to unrelated zone points");
Console.WriteLine($"PASS final audit: {checks} checks");

// Persistence and exact reset boundaries use a controlled clock, never the host timezone.
var beforeReset=DateTimeOffset.Parse("2026-11-07T14:59:59Z");var afterReset=beforeReset.AddSeconds(1);
var history=new CompletionHistory();const ulong characterA=1234567890123456789UL,characterB=1234567890123456790UL;
var doneBill=BillState.FromGame(true,false,7,7,true,10);
foreach(byte tier in new byte[]{14,15,16})history.Observe(characterA,tier,false,beforeReset,doneBill);
history.Observe(characterA,17,true,beforeReset,doneBill);
Check(new byte[]{14,15,16}.All(i=>history.IsComplete(characterA,i,false,beforeReset)),"Endwalker completion captured per daily tier");
Check(!history.IsComplete(characterB,14,false,beforeReset),"Another character cannot inherit completion");
Check(!history.IsComplete(characterA,10,false,beforeReset),"Another expansion cannot inherit completion");
Check(!history.IsComplete(0,14,false,beforeReset),"Missing character ID cannot read history");
Check(!history.IsComplete(characterA,14,false,afterReset),"Daily check clears exactly at 15:00 UTC");
Check(history.IsComplete(characterA,17,true,afterReset),"Daily reset does not clear weekly completion");
for(int i=0;i<10;i++)history.Observe(characterA,14,false,afterReset.AddSeconds(i),doneBill);
Check(!history.IsComplete(characterA,14,false,afterReset.AddSeconds(10)),"Stale retained counters cannot re-award today's check after reset");
var activeNew=BillState.FromGame(true,true,8,8,false,10);
history.Observe(characterA,14,false,afterReset.AddSeconds(11),activeNew);
history.Observe(characterA,14,false,afterReset.AddSeconds(12),activeNew with {Obtained=false,Completed=true});
Check(history.IsComplete(characterA,14,false,afterReset.AddSeconds(13)),"Newly picked current-day bill can complete after reset");
Check(!history.IsComplete(characterA,15,false,afterReset.AddSeconds(13)),"Completing one tier does not finish remaining tiers");
var tuesday=DateTimeOffset.Parse("2026-11-10T08:00:00Z");
Check(history.IsComplete(characterA,17,true,tuesday.AddSeconds(-1)) && !history.IsComplete(characterA,17,true,tuesday),"Weekly boundary is Tuesday 08:00 UTC");
history.Observe(characterA,17,true,tuesday,doneBill);Check(!history.IsComplete(characterA,17,true,tuesday),"Stale weekly counters cannot re-award after weekly reset");
var localMidnight=DateTimeOffset.Parse("2026-11-08T00:00:00+03:00");
Check(history.IsComplete(characterA,14,false,localMidnight),"Local midnight does not clear daily progress");
Check(!history.IsComplete(characterA,14,false,localMidnight,9),"Different current set suppresses cached completion");
var heldHistory=new CompletionHistory();heldHistory.Observe(characterA,14,false,beforeReset,activeNew);
heldHistory.Observe(characterA,14,false,afterReset,activeNew);
heldHistory.Observe(characterA,14,false,afterReset.AddSeconds(1),activeNew with {Completed=true,Obtained=false});
Check(!heldHistory.IsComplete(characterA,14,false,afterReset.AddSeconds(2)),"Carried unfinished old bill cannot re-arm itself at reset");
var repeatedHistory=new CompletionHistory();repeatedHistory.Observe(characterA,14,false,beforeReset,doneBill);
repeatedHistory.Observe(characterA,14,false,afterReset,doneBill with {Obtained=true,Completed=false});
repeatedHistory.Observe(characterA,14,false,afterReset.AddSeconds(1),doneBill);
Check(repeatedHistory.IsComplete(characterA,14,false,afterReset.AddSeconds(2)),"Observed fresh pickup permits a legitimately repeated order ID");
string historyFolder=Path.Combine(AppContext.BaseDirectory,"history-check-"+Guid.NewGuid().ToString("N"));string historyPath=Path.Combine(historyFolder,"completion-history.json");
try {
 var fullExpansion=new CompletionHistory();foreach(byte tier in new byte[]{14,15,16})fullExpansion.Observe(characterA,tier,false,beforeReset,doneBill);
 fullExpansion.Save(historyPath);var allRestart=CompletionHistory.Load(historyPath);
 Check(new byte[]{14,15,16}.All(i=>allRestart.IsComplete(characterA,i,false,beforeReset)),"Full expansion 3/3 survives actual save and fresh load");
 history.Save(historyPath);var restarted=CompletionHistory.Load(historyPath);
 Check(!restarted.Dirty && restarted.IsComplete(characterA,14,false,localMidnight),"Atomic file round-trip preserves completion across process restart");
 Check(restarted.IsComplete(characterA,14,false,localMidnight,0),"Saved completion survives temporarily empty game data on login");
 Check(!restarted.IsComplete(characterB,14,false,localMidnight),"Serialization preserves 64-bit character isolation");
 Check(!restarted.IsComplete(characterA,14,false,afterReset.AddDays(1)),"Restart after daily reset cannot restore expired check");
 restarted.Observe(characterA,14,false,afterReset.AddDays(1),activeNew with {Completed=true,Obtained=false});
 Check(!restarted.IsComplete(characterA,14,false,afterReset.AddDays(1)),"Stale same-order counters after next restart do not resurrect check");
 restarted.Observe(characterB,10,false,localMidnight,doneBill);restarted.Save(historyPath);
 var anotherRestart=CompletionHistory.Load(historyPath);
 Check(anotherRestart.IsComplete(characterB,10,false,localMidnight) && !anotherRestart.IsComplete(characterB,14,false,localMidnight),"Each character and expansion survives independent restart");
 anotherRestart.Observe(characterB,10,false,localMidnight,doneBill);
 Check(!anotherRestart.Dirty,"Unchanged snapshots do not rewrite history every scan");
 var previous=File.ReadAllText(historyPath);anotherRestart.Observe(characterB,11,false,localMidnight,doneBill);
 using(var locked=new FileStream(historyPath,FileMode.Open,FileAccess.Read,FileShare.None)) {
  try { anotherRestart.Save(historyPath);throw new Exception("Locked target unexpectedly overwritten"); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { }
 }
 Check(anotherRestart.Dirty && File.ReadAllText(historyPath)==previous,"Failed atomic save preserves previous history for retry");
 anotherRestart.Save(historyPath);Check(!anotherRestart.Dirty,"Retry commits unsaved completion");
 File.WriteAllText(historyPath,"{\"Version\":1,\"Characters\":null}");
 try { CompletionHistory.Load(historyPath);throw new Exception("Invalid history accepted"); } catch(InvalidDataException) { checks++; }
} finally {
 if(File.Exists(historyPath))File.Delete(historyPath);
 if(File.Exists(historyPath+".tmp"))File.Delete(historyPath+".tmp");
 if(File.Exists(historyPath+".bak"))File.Delete(historyPath+".bak");
 if(Directory.Exists(historyFolder))Directory.Delete(historyFolder);
}
Console.WriteLine($"PASS persisted completion audit: {checks} checks");

// Dialog presentation changes must not change the confirmation queue or travel callbacks.
float MeasureDialog(string text,float width,uint font) => text.Split('\n').Sum(line=>Math.Max(1,(int)Math.Ceiling(line.Length*font*.62f/width)))*(font+4);
var portraitPixels=new byte[8*8*4];
for(int y=3;y<=4;y++)
 for(int x=1;x<=6;x++)portraitPixels[(y*8+x)*4+3]=255;
portraitPixels[3]=1; // Ignore near-transparent pixels at the texture edge.
var croppedPortrait=TravelDialogLayout.PortraitCrop(portraitPixels,8,8);
Check(croppedPortrait.Position==new Vector2(1,3)&&croppedPortrait.Size==new Vector2(6,2),"Portrait crop keeps the entire visible silhouette and removes transparent margins");
Check(TravelDialogLayout.PortraitCrop(new byte[8*8*4],8,8).Size==new Vector2(8,8),"Empty texture uses the full portrait bounds");
var scaledCrop=TravelDialogLayout.PortraitCrop(portraitPixels,8,8,new(16,16));
Check(scaledCrop.Position==new Vector2(2,6)&&scaledCrop.Size==new Vector2(12,4),"Portrait crop follows the loaded icon resolution");
var portraitArea=new NativeHuntRect(new(222,38),new(192,192));
var fittedPortrait=portraitArea.FitImage(croppedPortrait.Size);
Check(fittedPortrait.Size==new Vector2(192,64)
 &&TravelDialogLayout.PortraitCaptionPosition(portraitArea,fittedPortrait)==new Vector2(222,170),
 "Cropped silhouette fills the portrait width and its caption sits four units below it");
var wideBounds=new NativeHuntRect(Vector2.Zero,new(304,192));
var widePortrait=TravelDialogLayout.FitPortrait(wideBounds,new(300,100));
var squarePortrait=TravelDialogLayout.FitPortrait(wideBounds,new(100,100));
Check(widePortrait.Size.X>250&&Math.Abs(widePortrait.Size.X/widePortrait.Size.Y-3)<0.001f
 &&Math.Abs(widePortrait.Size.X*widePortrait.Size.Y-squarePortrait.Size.X*squarePortrait.Size.Y)<0.01f,
 "Wide and square silhouettes have comparable area without distorting the texture");
var tallPortrait=TravelDialogLayout.FitPortrait(wideBounds,new(100,300));
Check(widePortrait.Position.Y==wideBounds.Position.Y
 &&TravelDialogLayout.UnusedPortraitHeight(wideBounds,widePortrait)>100
 &&TravelDialogLayout.UnusedPortraitHeight(wideBounds,tallPortrait)==0,
 "Only short silhouettes remove vertical space before the caption and buttons");
foreach(var silhouette in new[]{new Vector2(406,225),new Vector2(119,236)}) {
 var dialog=TravelDialogLayout.Build(TravelDialogContent.Next("","Test zone","Test target",1),(_,_,font)=>font+4);
 var portraitBounds=dialog.Portrait!.Value;
 var fitted=TravelDialogLayout.FitPortrait(portraitBounds,silhouette);
 float reduction=TravelDialogLayout.UnusedPortraitHeight(portraitBounds,fitted);
 var label=dialog.Texts.Single(t=>t.Text=="Test target");
 float labelBottom=TravelDialogLayout.PortraitCaptionPosition(portraitBounds,fitted).Y+label.Size.Y;
 Check(labelBottom<dialog.Confirm.Position.Y-reduction&&labelBottom<dialog.Decline.Position.Y-reduction,
  "Compacted dialog buttons remain below the caption for wide and tall portraits");
}
var nextDialog=TravelDialogContent.Next("Kozama'uka","Yak T'el","mourner");
var shortcutDialog=TravelDialogContent.Shortcut(new(1,10,0,1,815,1,"Mord Souq",100,1094,440,54.7f,40),"gnome");
foreach(bool nextHunt in new[]{false,true}){
 var results=TravelDialogContent.Completion("Endwalker","Мешки орехов",210,22500,22,"24 мин 36 с",nextHunt);
 var resultsLayout=TravelDialogLayout.Build(results,MeasureDialog);
 Check(results.Metrics.Select(m=>m.Value).SequenceEqual(new[]{"210","22 500","22","24 мин 36 с"})&&
       results.SingleButton!=nextHunt&&results.DeclineLabel=="Закрыть"&&
       results.ConfirmLabel==(nextHunt?"Новая охота":"Закрыть")&&
       resultsLayout.Texts.All(t=>t.Position.Y+t.Size.Y<resultsLayout.Confirm.Position.Y),
       "Expansion results fit above the appropriate actions");
}
Check(nextDialog.Destination=="Yak T'el" && nextDialog.Subject=="mourner" && nextDialog.Metrics.Length==0,"Next-zone dialog names the correct zone and first mob");
Check(shortcutDialog.Destination=="Mord Souq → gnome" && shortcutDialog.Fare==100,"Intra-zone dialog names the actual aetheryte, cost and target");
Check(shortcutDialog.Summary=="(на ~15 с быстрее)" && shortcutDialog.Metrics.Length==0,"Savings are a separate rounded estimate");
foreach(var content in new[]{nextDialog,shortcutDialog,nextDialog with {Destination=new string('W',95),Subject=new string('Ж',120)},TravelDialogContent.Plain(new string('Я',180))}) {
 var layout=TravelDialogLayout.Build(content,MeasureDialog);
 Check(layout.Width==TravelDialogLayout.DialogWidth && layout.Height>100 && new[]{layout.Confirm,layout.Decline}.All(b=>b.Position.X>=16&&b.Position.X+b.Size.X<=layout.Width-16&&b.Position.Y+b.Size.Y<=layout.Height-12)&&layout.Confirm.Position.X+layout.Confirm.Size.X<layout.Decline.Position.X,"Native dialog reserves visible buttons and margins");
 var viewport=new Vector2(640,360);var dialogSize=new Vector2(layout.Width,layout.Height);
 float fitted=TravelDialogLayout.FitScale(dialogSize,viewport,2);
 Check(dialogSize.X*fitted<=viewport.X-15&&dialogSize.Y*fitted<=viewport.Y-15,"Scaled confirmation fits inside a short viewport");
 Check(TravelDialogLayout.FitScale(dialogSize,new(1920,1080),1)==1,"Confirmation keeps normal UI scale when it fits");
 foreach(var text in layout.Texts)Check(text.Position.X>=16 && text.Position.Y>=32 && text.Position.X+text.Size.X<=layout.Width-16 && text.Position.Y+text.Size.Y<layout.Confirm.Position.Y,"Wrapped dialog text remains within content bounds");
}
foreach(var loginPresentation in new[]{TravelDialogContent.ResumeHunt(),TravelDialogContent.Pickup()}) {
 var layout=TravelDialogLayout.Build(loginPresentation,MeasureDialog);
 Check(!loginPresentation.HasSubject&&loginPresentation.Footer.Length==0&&layout.Texts.Count<=3&&layout.Height<175,"Login dialog keeps only title, optional status and actions");
}
try { TravelDialogLayout.Build(nextDialog,(_,_,_)=>float.NaN);throw new Exception("Invalid measurement accepted"); }catch(ArgumentException){checks++;}
var retryQueue=new ConfirmationQueue();
var broken=new ConfirmationRequest("travel","broken","",()=>true,()=>true,()=>true);
var independent=new ConfirmationRequest("pickup","independent","",()=>true,()=>true,()=>true);
retryQueue.Offer(broken);
Check(retryQueue.Activate(true,100)==broken,"Failed dialog regression starts with travel");
retryQueue.Resolve(broken.Key,false);retryQueue.RetryOpen(broken,100);retryQueue.Offer(independent);
Check(retryQueue.Activate(true,101)==independent,"Cooling down a failed dialog leaves lower-priority notices available");
retryQueue.Resolve(independent.Key,false);
Check(retryQueue.Activate(true,10099)==null&&retryQueue.Activate(true,10100)==broken,"Failed dialog retries only after the cooldown");
var tallSize=new Vector2(420,672);var smallViewport=new Vector2(1280,720);
float tallScale=TravelDialogLayout.FitScale(tallSize,smallViewport,1.25f);
var tallPosition=TravelDialogLayout.ClampPosition(new(200,200),tallSize*tallScale,smallViewport);
Check(tallScale<1.25f&&tallPosition.Y+tallSize.Y*tallScale<=smallViewport.Y,"Tall native window fits and is clamped into a short viewport");
var pricesQueue=new ConfirmationQueue();
bool oldQuoteValid=true;
var oldQuote=new ConfirmationRequest("next","next:100","",()=>oldQuoteValid,()=>true,()=>true,DeclineKey:"next:target");
var newQuote=oldQuote with {Key="next:200",Valid=()=>true};
pricesQueue.Offer(oldQuote);Check(pricesQueue.Activate(true)==oldQuote,"Initial quote opens");
oldQuoteValid=false;pricesQueue.Resolve(oldQuote.Key,false,userResponse:false);pricesQueue.Offer(newQuote);
Check(pricesQueue.Activate(true)==newQuote,"Automatic price invalidation allows a replacement quote");
pricesQueue.Resolve(newQuote.Key,false);pricesQueue.Offer(newQuote with {Key="next:300"});
Check(pricesQueue.Activate(true)==null,"Later suppresses later prices for the same target");
var otherTargetQuote=newQuote with {Key="next:other:300",DeclineKey="next:other"};
pricesQueue.Offer(otherTargetQuote);Check(pricesQueue.Activate(true)==otherTargetQuote,"Later leaves replacement targets eligible");
var presentedQueue=new ConfirmationQueue();int presentedAccepted=0;
var presented=new ConfirmationRequest("next","presented","Legacy text",()=>true,()=>true,()=>{presentedAccepted++;return true;},nextDialog);
presentedQueue.Offer(presented);var activePresentation=presentedQueue.Activate(true);
Check(activePresentation?.Presentation==nextDialog && presentedAccepted==0,"Building/showing structured presentation cannot teleport");
Check(presentedQueue.Resolve(presented.Key,false)==null && presentedAccepted==0,"Later declines the structured dialog without teleporting");
Console.WriteLine($"PASS styled dialog audit: {checks} checks");
foreach(string owner in new[]{"travel","next","shortcut"}){
    var interrupted=new ConfirmationQueue();
    bool combat=false,valid=true;
    int teleports=0;
    var offer=new ConfirmationRequest(owner,owner+":combat","",()=>valid,()=>!combat,()=>{teleports++;return true;},DeclineKey:owner+":target");
    interrupted.Offer(offer);
    Check(interrupted.Activate(true)==offer,"Teleport confirmation opens before combat");
    combat=true;
    interrupted.Interrupt(offer.Key);
    Check(interrupted.Activate(true)==null&&!interrupted.IsDeclined(offer.DeclineKey!)&&teleports==0,"Combat suspends confirmation without accepting or declining it");
    combat=false;
    Check(interrupted.Activate(true)==offer&&teleports==0,"Evade restores the confirmation without teleporting");
    interrupted.Resolve(offer.Key,false);
    interrupted.Offer(offer);
    Check(interrupted.Activate(true)==null,"Explicit Later still suppresses the confirmation");
    interrupted.Clear();
    interrupted.Offer(offer);
    Check(interrupted.Activate(true)==offer,"Second combat scenario starts with an active offer");
    combat=true;
    interrupted.Interrupt(offer.Key);
    valid=false;
    combat=false;
    Check(interrupted.Activate(true)==null&&teleports==0,"Invalidated offer does not return after combat");
}
var searchAudit=new EliteSearch();
searchAudit.Start([new(100,0),new(0,0),new(0,0),new(200,0)]);
Check(searchAudit.Tick(new(-100,0),null,false)==new Vector2(0,0),"Fly-by route starts nearest unique point");
Check(searchAudit.Tick(new(-46,0),null,false)==null,"Outside visit radius retains point");
Check(searchAudit.Tick(new(-45,0),null,false)==new Vector2(100,0),"Entering radius advances immediately without stopping or timed observations");
Check(searchAudit.Tick(new(100,0),new Vector2(112,4),false)==new Vector2(112,4),"Detection on fly-by tick prevents skipping the elite");
for(int i=0;i<120;i++) Check(searchAudit.Tick(new(100,0),null,i%2==0)==null && searchAudit.LockedPosition==new Vector2(112,4),"Lost visibility never releases detected elite lock");
searchAudit.Suspend();Check(searchAudit.Tick(new(0,0),null,false)==new Vector2(112,4),"Loading reapplies detected lock");
Check(searchAudit.Tick(new(0,0),new Vector2(120,8),true)==new Vector2(120,8),"Detected moving elite updates during combat");
searchAudit.Retry();Check(searchAudit.Tick(new(0,0),null,false)==new Vector2(120,8),"Marker retry preserves latest elite position");
searchAudit.Start([new(0,0),new(100,0)]);
Check(searchAudit.LockedPosition==null,"New route clears previous lock");
Check(searchAudit.Tick(new(0,0),null,true)==new Vector2(0,0),"Combat/casting hold unobserved point");
Check(searchAudit.Tick(new(0,0),null,false)==new Vector2(100,0),"Leaving combat allows immediate fly-by advancement");
Check(searchAudit.Tick(new(100,0),null,false)==new Vector2(0,0) && searchAudit.Round==2,"Last visited spawn automatically starts next round away from previous point");
Check(searchAudit.Tick(new(100,0),new Vector2(120,0),false)==new Vector2(120,0),"Detection continues after visiting every candidate");
searchAudit.Start([new(0,0),new(1,0),new(2,0)]);
Check(searchAudit.Tick(new(0,0),null,false)==new Vector2(1,0),"Only one point advances per fresh scan");
Check(searchAudit.Tick(new(0,0),new Vector2(3,0),false)==new Vector2(3,0),"Fresh detection can interrupt a cluster of nearby points");
searchAudit.Start([new(float.NaN,0),new(float.PositiveInfinity,0)]);
Check(searchAudit.Tick(new(0,0),null,false)==null,"Invalid spawn coordinates ignored");
for(int scenario=0;scenario<100;scenario++) {
 var search=new EliteSearch();search.Start([new(0,0),new(200,0)]);
 Vector2? expectedLock=null;
 for(int step=0;step<200;step++) {
  bool busy=random.Next(3)==0;var player=new Vector2(random.Next(-10,220),0);
  Vector2? detection=random.Next(8)==0?new Vector2(random.Next(250),random.Next(20)):null;
  if(detection!=null) expectedLock=detection;
  if(random.Next(12)==0) search.Suspend();
  if(random.Next(12)==0) search.Retry();
  var marker=search.Tick(player,detection,busy);
  if(expectedLock!=null) {
   Check(search.LockedPosition==expectedLock,"Randomized fly-by interruptions never unlock elite");
   Check(marker==null || marker==expectedLock,"Randomized flag stays on latest observed elite");
  } else Check(search.LockedPosition==null,"Absence cannot create a fake detection");
 }
}
Console.WriteLine($"PASS fly-by search and locked-elite audit: {checks}");
// Regression: an elite can stream in at a previous spawn after the next flag was emitted.
foreach(int delay in new[]{0,1,2,10,100,1000}) {
 var late=new EliteSearch();late.Start([new(0,0),new(200,0),new(400,0)]);
 Check(late.Tick(new(-100,0),null,false)==new Vector2(0,0),"Late-stream fixture flags initial spawn");
 Check(late.Tick(new(0,0),null,false)==new Vector2(200,0),"Late-stream fixture already emitted next-spawn flag");
 for(int i=0;i<delay;i++) Check(late.Tick(new(100,0),null,false)==null && late.LockedPosition==null,"No visibility timeout after advancing a point");
 var actual=new Vector2(8,6);
 Check(late.Tick(new(100,0),actual,false)==actual && late.LockedPosition==actual,"Late-loaded elite at previous spawn replaces next-spawn flag immediately");
 for(int i=0;i<100;i++) Check(late.Tick(new(400,0),null,i%2==0)==null && late.LockedPosition==actual,"Reaching further spawn and losing visibility never releases late detection");
 late.Suspend();Check(late.Tick(new(400,0),null,false)==actual,"Loading reapplies late-found elite flag");
 var moved=actual+new Vector2(15,0);
 Check(late.Tick(new(400,0),moved,true)==moved,"Late-found elite still updates actual position in combat");
 late.Retry();Check(late.Tick(new(400,0),null,false)==moved,"Late-found elite survives marker retry");
}
var lateAfterExhaustion=new EliteSearch();lateAfterExhaustion.Start([new(0,0)]);
lateAfterExhaustion.Tick(new(-100,0),null,false);lateAfterExhaustion.Tick(new(0,0),null,false);
Check(lateAfterExhaustion.Round==2,"Late exhaustion fixture automatically started next round");
Check(lateAfterExhaustion.Tick(new(100,0),new Vector2(4,8),false)==new Vector2(4,8),"Late detection after final visited point still emits elite flag");
Check(lateAfterExhaustion.Tick(new(100,0),null,false)==null && lateAfterExhaustion.LockedPosition==new Vector2(4,8),"Final-point late detection remains locked");
var sameFlag=new EliteSearch();sameFlag.Start([new(0,0),new(200,0)]);sameFlag.Tick(new(0,0),null,false);
Check(sameFlag.Tick(new(100,0),new Vector2(200,0),false)==null && sameFlag.LockedPosition==new Vector2(200,0),"Elite coinciding with existing flag locks without redundant marker");
Check(sameFlag.Tick(new(200,0),null,false)==null && sameFlag.LockedPosition==new Vector2(200,0),"Coincident late detection cannot be skipped on arrival");
Console.WriteLine($"PASS late streaming after spawn advancement audit: {checks}");
// Repeat search automatically without marker churn or repeatedly consuming a nearby spawn.
var circles=new EliteSearch();circles.Start([new(0,0),new(200,0),new(400,0)]);
Check(circles.Round==1 && circles.Tick(new(-100,0),null,false)==new Vector2(0,0),"Round one starts at nearest spawn");
Check(circles.Tick(new(0,0),null,false)==new Vector2(200,0),"First round visits second spawn");
Check(circles.Tick(new(200,0),null,false)==new Vector2(400,0),"First round visits final spawn");
Check(circles.Tick(new(400,0),null,false)==new Vector2(200,0) && circles.Round==2,"New round avoids immediately revisiting its last spawn");
Check(circles.Tick(new(200,0),null,false)==new Vector2(0,0),"Repeated round starts with nearest other spawn");
Check(circles.Tick(new(0,0),null,false)==new Vector2(400,0),"Repeated round still includes previous-round final point");
Check(circles.Tick(new(400,0),new Vector2(405,3),false)==new Vector2(405,3) && circles.Round==2,"Detection wins on repeated-round boundary");
for(int tick=0;tick<100;tick++) Check(circles.Tick(new(0,0),null,false)==null && circles.Round==2 && circles.LockedPosition==new Vector2(405,3),"Detected elite stops all repeat rounds until completion");
var singleton=new EliteSearch();singleton.Start([new(0,0)]);singleton.Tick(new(-100,0),null,false);singleton.Tick(new(0,0),null,false);
Check(singleton.Round==2,"Single spawn automatically schedules a second visit");
for(int tick=0;tick<100;tick++) Check(singleton.Tick(new(0,0),null,false)==null && singleton.Round==2,"Stationary single-point route does not spam flags or churn rounds");
singleton.Tick(new(100,0),null,false);singleton.Tick(new(0,0),null,false);
Check(singleton.Round==3,"Single-point automatic repeat counts a fresh departure and return");
var nearby=new EliteSearch();nearby.Start([new(0,0),new(5,0)]);nearby.Tick(new(0,0),null,false);nearby.Tick(new(0,0),null,false);
for(int tick=0;tick<100;tick++) Check(nearby.Tick(new(0,0),null,false)==null && nearby.Round==2,"Nearby point cluster cannot spin through repeated rounds on the spot");
Check(nearby.Tick(new(0,0),new Vector2(3,3),false)==new Vector2(3,3),"Stationary repeat guard never blocks late elite detection");
var absentData=new EliteSearch();absentData.Start([]);
for(int tick=0;tick<100;tick++) Check(absentData.Tick(new(0,0),null,false)==null && absentData.Round==0,"Empty spawn data never invents repeat rounds");
Check(absentData.Tick(new(0,0),new Vector2(2,2),true)==new Vector2(2,2),"Detection works even when spawn data is absent");
var manyCircles=new EliteSearch();manyCircles.Start([new(0,0),new(200,0),new(400,0)]);
var nextCirclePoint=manyCircles.Tick(new(-100,0),null,false);
for(int visit=0;visit<300;visit++) {
 Check(nextCirclePoint!=null,"Automatic repeated route always has a next point");
 nextCirclePoint=manyCircles.Tick(nextCirclePoint!.Value,null,false);
}
Check(manyCircles.Round==101,"One hundred complete circles restart automatically without a manual action");
Console.WriteLine($"PASS automatic repeat-circle audit: {checks}");
var eliteRoute=new RouteSession();
var normalTarget=new MobHuntEntry{BillNumber=1,MobIndex=0,MobHuntId=1,TerritoryType=100,NeededKills=1};
var eliteTarget=new MobHuntEntry{BillNumber=2,MobIndex=0,MobHuntId=2,TerritoryType=100,NeededKills=1,IsEliteMark=true};
var otherTarget=new MobHuntEntry{BillNumber=1,MobIndex=1,MobHuntId=3,TerritoryType=200,NeededKills=1};
var completed=new HashSet<uint>{1};
eliteRoute.Start(normalTarget,[1,2]);
Check(eliteRoute.Tick([normalTarget,otherTarget,eliteTarget],m=>completed.Contains(m.MobHuntId),_=>true,100)==eliteTarget,"Weekly elite blocks departure after daily targets");
Check(eliteRoute.Phase==RoutePhase.Following && eliteRoute.Suggested==null,"No next-zone prompt before elite completion");
completed.Add(2);eliteRoute.Tick([normalTarget,otherTarget,eliteTarget],m=>completed.Contains(m.MobHuntId),_=>true,100);
Check(eliteRoute.Phase==RoutePhase.NextZone && eliteRoute.Suggested==otherTarget,"Next zone available after elite completion");
Console.WriteLine($"PASS elite search and combined route: {checks}");

var eliteNotice=new NextZoneNotice();
Check(eliteNotice.ShouldOpen(eliteRoute),"Elite death immediately produces next-zone notice");
Check(!eliteNotice.ShouldOpen(eliteRoute),"Elite death notice is not duplicated");
eliteRoute.Start(otherTarget,[1,2]);
Check(eliteRoute.Tick([otherTarget],_=>false,_=>true,100)==null && eliteRoute.Phase==RoutePhase.WaitingForZone,"Accepted next-zone route waits through teleport");
eliteRoute.ZoneTransition();
Check(eliteRoute.Tick([otherTarget],_=>false,_=>true,200)==otherTarget && eliteRoute.Phase==RoutePhase.Following,"Arrival marks first target and resumes without restart");
Check(eliteRoute.Tick([otherTarget],_=>false,_=>true,200)==null,"Arrival marker emitted once");
// Completed weekly bill may disappear from active snapshots; retained kills still advance the route.
eliteRoute.Start(eliteTarget,[1,2]);
Check(eliteRoute.Tick([otherTarget],m=>m.MobHuntId==eliteTarget.MobHuntId,_=>true,100)==null && eliteRoute.Suggested==otherTarget,"Removed completed weekly bill still offers next zone");
Console.WriteLine($"PASS immediate elite completion and arrival: {checks}");

LifecycleChecks.Run(Check);

Console.WriteLine($"PASS total route/state checks: {checks}");
