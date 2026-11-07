using System.Numerics;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
int checks=0;
void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
float Cost(int[] order, Vector2[] p, Vector2[] e, int start) => order.Length==0 ? 0 : (start<0?0:Vector2.Distance(e[start],p[order[0]]))+order.Zip(order.Skip(1),(a,b)=>Vector2.Distance(p[a],p[b])).Sum();
IEnumerable<int[]> Permute(int[] x) { if(x.Length==0) { yield return []; yield break; } foreach(int i in x) foreach(var tail in Permute(x.Where(v=>v!=i).ToArray())) yield return new[]{i}.Concat(tail).ToArray(); }
var random=new Random(731);
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
var daily=DateTimeOffset.Parse("2026-11-07T15:00:00Z");
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
// Long-flight shortcuts are rare suggestions, never automatic same-zone casts.
Check(TravelPolicy.Decide(397,397,true,true,false,true)==TravelDecision.Teleport,"Explicit same-zone acceptance permits exactly the requested teleport");
Check(TravelPolicy.Decide(397,397,true,true,true,true)==TravelDecision.Busy,"Explicit same-zone acceptance still respects busy state");
Check(TravelPolicy.Decide(397,397,true,false,false,true)==TravelDecision.Locked,"Explicit same-zone acceptance cannot bypass unlocks");
var cheap=new ShortcutCandidate(71,"Nearby aetheryte",new Vector2(1900,0),100);
ShortcutOffer? Shortcut(Vector2 player,params ShortcutCandidate[] entries)=>ShortcutAdvisor.Evaluate(1,targetA,player,new Vector2(2000,0),entries);
Check(Shortcut(Vector2.Zero,cheap) is {GilCost:100,FinalDistance:100},"Far target with nearby cheap aetheryte is offered");
Check(Shortcut(new Vector2(1000,0),cheap)==null,"Do not offer short flights");
Check(Shortcut(Vector2.Zero,cheap with {GilCost=201})==null,"Do not suggest expensive intra-zone teleports");
Check(Shortcut(Vector2.Zero,cheap with {Position=new Vector2(1500,0)})==null,"Aetheryte must actually be close to the goal");
Check(Shortcut(new Vector2(800,0),cheap with {Position=new Vector2(1700,0)})!=null,"Threshold requires 1200m direct flight and 900m saving");
Check(Shortcut(Vector2.Zero)==null,"No unlocked candidate means no offer");
Check(Shortcut(new Vector2(float.NaN,0),cheap)==null,"No shortcut during invalid-position frames");
var shortcutNotice=new ShortcutNotice();var suggestedShortcut=Shortcut(Vector2.Zero,cheap)!;
Check(shortcutNotice.ShouldOpen(suggestedShortcut)&&!shortcutNotice.ShouldOpen(suggestedShortcut),"Do not repeat shortcut offer after decline");
Check(shortcutNotice.ShouldOpen(suggestedShortcut with {RunId=2}),"A new route can offer a shortcut anew");
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
 foreach(uint id in new uint[]{70,111,133,183,216,75}) { var a=game.Excel.GetSheet<Aetheryte>().GetRow(id); Console.WriteLine($"Aetheryte {id}: {a.PlaceName.Value.Name}, territory={a.Territory.RowId}, map={a.Territory.Value.Map.RowId}"); }
 foreach(uint id in new uint[]{418,628,819,962,1185}) { var t=game.Excel.GetSheet<TerritoryType>().GetRow(id); Console.WriteLine($"Territory {id}: {t.PlaceName.Value.Name}, map={t.Map.RowId}"); }
 var dataset=System.Text.Json.JsonSerializer.Deserialize<Dictionary<uint,float[][]>>(File.ReadAllText("publish-repo/HuntRoute/data/elite-spawns.json"))!;
 int weeklyTargets=0;
 foreach(var t in game.Excel.GetSheet<MobHuntOrderType>().Where(t=>t.RowId<22 && t.Type==2)) {
   foreach(var orders in game.Excel.GetSubrowSheet<MobHuntOrder>().Where(o=>o.RowId>=t.OrderStart.RowId && o.RowId<t.OrderStart.RowId+(t.RowId==4?17:12))) {
     foreach(var order in orders) {
       if(order.Target.RowId==0) continue;
       var target=order.Target.Value; uint territory=target.Map.Value.TerritoryType.RowId;
       Check(dataset.ContainsKey(territory),$"Missing elite spawn zone {territory}"); weeklyTargets++;
     }
   }
 }
 foreach(var pair in dataset) {
   var territory=game.Excel.GetSheet<TerritoryType>().GetRow(pair.Key); var map=territory.Map.Value;
   string id=map.Id.ToString(); string texture=$"ui/map/{id}/{id.Replace("/", "")}_m.tex";
   Check(game.FileExists(texture),$"Missing map texture {texture}");
   foreach(var point in pair.Value) Check(point.Length==2 && point.All(float.IsFinite) && point.All(v=>v>0 && v<100),"Invalid spawn coordinate");
 }
 Console.WriteLine($"Validated {weeklyTargets} weekly targets, {dataset.Count} spawn maps, {dataset.Values.Sum(p=>p.Length)} possible B-rank points and all map textures.");
}
Console.WriteLine($"PASS: {checks} route, reset and installed game data checks");
