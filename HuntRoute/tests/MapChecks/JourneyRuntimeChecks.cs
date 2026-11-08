using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
static class JourneyRuntimeChecks {
 static object? Call(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args);
 static void Set(object target,string name,object? value){var type=target.GetType();var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)f.SetValue(target,value);else type.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(target,value);}
 static T Get<T>(object target,string name){var type=target.GetType();var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);return (T)(f!=null?f.GetValue(target):type.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(target))!;}
 static T Proxy<T>(Func<MethodInfo,object?[]?,object?> call)where T:class{var proxy=DispatchProxy.Create<T,ServiceProxy>();((ServiceProxy)(object)proxy).Handler=call;return proxy;}
 public static int Run(GameData game){
  int checks=0;void Check(bool yes,string message){checks++;if(!yes)throw new Exception(message);}
  var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
  var session=new RouteSession();var search=new EliteSearch();Set(plugin,"Session",session);Set(plugin,"Search",search);Set(plugin,"Ready",true);Set(plugin,"character",123UL);
  var flow=Get<JourneyFlow>(plugin,"Flow");var store=Get<HuntStore>(plugin,"Store");
  var bridgeType=typeof(Plugin).Assembly.GetType("HuntRoute.NativeDialogService")!;
  var bridge=RuntimeHelpers.GetUninitializedObject(bridgeType);var queue=new ConfirmationQueue();Set(bridge,"queue",queue);Set(plugin,"NativeDialogs",bridge);Set(plugin,"NextZone",new NextZoneWindow(plugin));Set(plugin,"Shortcut",new ShortcutWindow(plugin));
  long clock=10000;Set(plugin,"Clock",(Func<long>)(()=>clock));Set(plugin,"FlightCapability",(Func<bool>)(()=>true));
  uint territory=397;var map=game.Excel.GetSheet<TerritoryType>().GetRow(territory).Map.Value;uint mapId=map.RowId;
  Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);
  bool combat=false,casting=false,unconscious=false;var position=new Vector3(0,0,0);IGameObject[] objects=[];
  var player=Proxy<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>((m,_)=>m.Name switch{"get_Position"=>position,"get_IsCasting"=>casting,_=>throw new Exception(m.Name)});
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name switch{"get_LocalPlayer"=>player,"GetEnumerator"=>objects.AsEnumerable().GetEnumerator(),_=>throw new Exception(m.Name)});
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name switch{"get_IsLoggedIn"=>true,"get_TerritoryType"=>territory,"get_MapId"=>mapId,_=>throw new Exception(m.Name)});
  bool Flag(ConditionFlag f)=>f==ConditionFlag.InCombat?combat:f==ConditionFlag.Casting?casting:f==ConditionFlag.Unconscious?unconscious:false;
  Service.Condition=Proxy<ICondition>((m,args)=>args is {Length:>0} ? args[0] is ConditionFlag[] flags?flags.Any(Flag):Flag((ConditionFlag)args[0]!) : false);
  Service.DataManager=new SheetServices(game);Service.PluginLog=Proxy<IPluginLog>((_,_)=>null);
  Service.Player=Proxy<IPlayerState>((m,_)=>m.Name switch{"get_IsLoaded"=>true,"get_ContentId"=>123UL,_=>throw new Exception(m.Name)});
  var entries=game.Excel.GetSheet<Aetheryte>().Where(a=>a.IsAetheryte).Select(a=>Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name switch{"get_AetheryteId"=>a.RowId,"get_GilCost"=>100u,_=>throw new Exception(m.Name)})).ToArray();
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?entries.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  MarkerStamp? marker=null;int writes=0,ports=0,opens=0;
  Set(plugin,"MarkerReader",(Func<MarkerStamp?>)(()=>marker));Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(stamp=>{marker=stamp;writes++;return true;}));Set(plugin,"TeleportTransport",(Func<uint,bool>)(_=>{ports++;return true;}));
  Service.GameGui=Proxy<IGameGui>((m,_)=>{if(m.Name=="OpenMapWithMapLink"){opens++;return true;}throw new Exception(m.Name);});
  Set(plugin,"ContextProvider",(Func<string>)(()=>territory+":"+mapId));
  MobHuntEntry Daily(uint id,byte mob,uint zone){var row=game.Excel.GetSubrowSheet<MobHuntOrder>().SelectMany(g=>g).Where(o=>o.Target.RowId!=0).Select(o=>o.Target.Value).First(t=>t.Name.RowId==id&&t.Map.Value.TerritoryType.RowId==zone);return new(){Name=row.Name.Value.Singular.ToString(),BillNumber=1,OrderId=1,MobIndex=mob,MobHuntId=id,NeededKills=1,TerritoryType=zone,MapId=row.Map.RowId,TerritoryName=row.Map.Value.PlaceName.Value.Name.ToString()};}
  var a=Daily(3481,0,397);var b=Daily(3479,1,397);var c=Daily(3494,2,401);
  var spawn=EliteSpawns.Sets.First(p=>p.Value.Territory==397);var elite=new MobHuntEntry{Name=spawn.Value.Name,BillNumber=5,OrderId=1,MobIndex=0,MobHuntId=spawn.Key,TerritoryType=397,MapId=spawn.Value.Map,NeededKills=1,TerritoryName=spawn.Value.Zone,IsEliteMark=true};
  var counts=new Dictionary<(byte,byte),int>();Set(plugin,"KillCounts",counts);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,c,elite});Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){a,b,elite},null,true),new(c.TerritoryName!,new(){c},null)});
  Call(plugin,"ResumeJourney");Check(flow.Active&&session.Target==a&&writes==1&&ports==0,"Main start in current zone flags target without teleport");uint run=session.RunId;
  IBattleNpc Npc(ulong id,uint name,Vector3 location,bool dead=false)=>Proxy<IBattleNpc>((m,_)=>m.Name switch{"get_GameObjectId"=>id,"get_NameId"=>name,"get_Position"=>location,"get_IsDead"=>dead,_=>throw new Exception(m.Name)});
  objects=[Npc(10,a.MobHuntId,new(10,0,0)),Npc(11,b.MobHuntId,new(20,0,0))];Call(plugin,"AdvanceRoute");Check(session.Target==a&&marker?.X==10,"Ordinary detection replaces anchor with nearest actual position");
  objects=[Npc(10,a.MobHuntId,new(12,0,0)),Npc(11,b.MobHuntId,new(1,0,0))];Call(plugin,"AdvanceRoute");Check(session.Target==a&&marker?.X==12,"Ordinary actor stays selected while another gets closer; position updates");
  flow.Manual=JourneyFlow.Key(a);objects=[Npc(20,elite.MobHuntId,new(2,0,0))];Call(plugin,"AdvanceRoute");Check(session.Target==a&&search.ActorId==null,"Manual ordinary goal cannot be preempted by detected elite");
  flow.Manual=null;Call(plugin,"AdvanceRoute");Check(session.Target==elite&&search.ActorId==20&&marker?.X==2,"Auto mode preempts ordinary target with live elite");
  objects=[];Call(plugin,"AdvanceRoute");Check(search.Lost&&search.ActorId==20,"Lost elite stays locked");
  objects=[Npc(20,elite.MobHuntId,new(2,0,0),true)];Set(plugin,"contractAt",clock);Call(plugin,"AdvanceRoute");Check(search.AwaitingCredit,"Actual controller observes dead locked actor before filtering");
  for(int step=1;step<=30;step++){clock+=100;Set(plugin,"contractAt",clock);Set(plugin,"contractRevision",(long)(step/10));Call(plugin,"AdvanceRoute");}
  Check(search.ActorId==null&&!search.AwaitingCredit,"Actual controller resumes search after uncredited death");
  objects=[Npc(21,elite.MobHuntId,new(3,0,0))];Call(plugin,"AdvanceRoute");Check(search.ActorId==21,"New live instance can lock after old death");
  counts[(5,0)]=1;objects=[];Call(plugin,"AdvanceRoute");Check(search.Round==0&&session.Target==a,"Credited elite clears search and returns to daily targets");
  counts[(1,0)]=1;counts[(1,1)]=1;Call(plugin,"AdvanceRoute");Check(session.Phase==RoutePhase.NextZone&&session.Suggested==c,"Completed zone selects first remaining other-zone target");
  var request=queue.Activate(true);Check(request?.Owner=="next","Actual controller queues next-zone dialog");
  Check(queue.Resolve(request!.Key,true)!.Accept(),"Confirmed next-zone action starts travel");Check(ports==1&&session.RunId==run&&session.Phase==RoutePhase.WaitingForZone,"Travel retains route identity and waits for arrival");
  clock+=10000;territory=c.TerritoryType;mapId=c.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);objects=[];Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");
  Check(session.Target==c&&session.Phase==RoutePhase.Following&&marker?.Map==c.MapId&&session.RunId==run,"Arrival automatically flags first target and continues same route");Check(opens==0,"Automatic tracking never opens game map");
  marker=new(territory,mapId,999,999);int before=writes;Call(plugin,"CheckMarker");Call(plugin,"AdvanceRoute");Check(flow.Pauses.HasFlag(PauseReason.Marker)&&writes==before,"External flag pauses navigation without overwriting it");
  flow.Pauses|=PauseReason.User;flow.Pauses&=~PauseReason.Marker;Call(plugin,"AdvanceRoute");Check(writes==before&&!flow.CanNavigate,"Independent user pause survives clearing marker pause");
  flow.Manual=JourneyFlow.Key(c);flow.Pauses=PauseReason.None;Set(plugin,"context","previous");territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Call(plugin,"ReconcileContext");Check(flow.Pauses.HasFlag(PauseReason.NoZone)&&flow.Manual==JourneyFlow.Key(c),"Manual target survives external zone change");
  flow.Manual=null;Set(plugin,"context","another");Call(plugin,"ReconcileContext");Check(session.Target==elite||session.Target==a||session.Target==b||flow.Pauses.HasFlag(PauseReason.NoZone),"Automatic context adaptation respects completed local goals");
  store.Settings.ResetNotifications=true;counts.Clear();Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)}});Set(plugin,"contractRevision",10L);flow.Active=true;flow.Pauses=PauseReason.None;Call(plugin,"CheckResetNotice");Check(Get<string?>(plugin,"NoticeText")?.Contains("новые контракты")==true,"Reset during active route produces only a passive indicator");
  flow.Pauses=PauseReason.User;queue.Clear();Call(plugin,"CheckResetNotice");request=queue.Activate(true);Check(request?.Owner=="pickup","Paused route may show new-contract choice");request!.OnShown!();queue.Resolve(request.Key,false);queue.Clear();Call(plugin,"CheckResetNotice");Check(queue.Activate(true)==null,"Shown reset offer does not repeat within period after Later");
  // Deferrals and explicit dialog reopening must not strand or silently restart the route.
  queue.Clear();flow.Pauses=PauseReason.None;flow.Manual=null;flow.Active=true;objects=[];session.Retarget(a);Set(plugin,"ownedMarker",null);
  Call(plugin,"Defer",a,false);Check(flow.Excluded(a)&&session.Target==b,"Manual deferral moves to another local target and stays unfinished");
  Call(plugin,"RestoreDeferred",a);Check(!flow.Excluded(a)&&session.Target==b,"Returning deferred target preserves current target");
  Call(plugin,"Defer",b,true);Check(session.Phase==RoutePhase.NextZone&&session.Suggested==c,"Deferring entire current zone offers next zone instead of waiting forever");
  Call(plugin,"RestoreDeferred",b);flow.Pauses=PauseReason.None;session.RequestNext(c);queue.Clear();Call(plugin,"OfferNext");
  request=queue.Activate(true);Check(request?.Owner=="travel","Inline next-zone action opens explicit travel proposal");queue.Resolve(request!.Key,false);
  Call(plugin,"OfferNext");request=queue.Activate(true);Check(request!=null,"Explicit action can reopen same proposal after Later");queue.Clear();
  var oldOrder=a;var newOrder=new MobHuntEntry{Name=a.Name,TerritoryName=a.TerritoryName,BillNumber=a.BillNumber,OrderId=a.OrderId,Generation=1,MobIndex=a.MobIndex,MobHuntId=a.MobHuntId,MapId=a.MapId,TerritoryType=a.TerritoryType,NeededKills=1};
  Set(plugin,"Mobs",new List<MobHuntEntry>{newOrder,b,c,elite});Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){newOrder,b,elite},null,true),new(c.TerritoryName!,new(){c},null)});
  session.Retarget(oldOrder);flow.Manual=JourneyFlow.Key(oldOrder);Call(plugin,"AdvanceRoute");Check(session.Target==newOrder&&flow.Manual==null&&session.RunId==run,"Replacing bill generation clears stale manual priority without breaking route");
  int beforeTimeout=ports;
  Set(plugin,"pendingJourney",new PendingJourney(397,401,c.MapId,clock-120000,JourneyFlow.Key(c),false));Call(plugin,"ReconcileTravel");Check(flow.Pauses.HasFlag(PauseReason.TravelError)&&ports==beforeTimeout,"Timeout pauses and never retries teleport automatically");
  flow.Pauses=PauseReason.None;Set(plugin,"pendingJourney",new PendingJourney(397,401,c.MapId,clock-5000,JourneyFlow.Key(c),false));Call(plugin,"ReconcileTravel");Check(flow.Pauses.HasFlag(PauseReason.TravelError),"Cancelled-before-first-cast request does not wait forever");
  flow.Pauses=PauseReason.None;Set(plugin,"pendingJourney",new PendingJourney(397,401,c.MapId,clock-9000,JourneyFlow.Key(c),false){CastingSeen=true,CastStopped=clock-2000});Call(plugin,"ReconcileTravel");Check(flow.Pauses.HasFlag(PauseReason.TravelError),"Observed cast cancellation produces retry state without automatic cast");
  flow.Pauses=PauseReason.None;Set(plugin,"pendingJourney",new PendingJourney(397,401,c.MapId,clock-9000,JourneyFlow.Key(c),false){CastingSeen=true,Loaded=true});session.Continue(c);counts[(1,2)]=1;
  territory=401;mapId=c.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"ownedMarker",null);int beforeCreditedArrival=writes;
  Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");Check(writes==beforeCreditedArrival&&session.Phase==RoutePhase.NextZone,"Goal credited during cast is not flagged again after arrival");
  var board=Plugin.Boards[0];territory=board.Territory;mapId=board.Map;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);queue.Clear();flow.Pauses=PauseReason.None;
  Call(plugin,"OfferBoard",board);request=queue.Activate(true);int beforeBoard=ports;
  Check(request?.Presentation?.Destination==game.Excel.GetSheet<TerritoryType>().GetRow(board.Territory).PlaceName.Value.Name.ToString() && request.Presentation.Context.Contains(game.Excel.GetSheet<Aetheryte>().GetRow(board.Aetheryte).PlaceName.Value.Name.ToString()),"Board proposal explicitly identifies actual city and aetheryte");
  Check(request!=null&&queue.Resolve(request.Key,true)!.Accept(),"Board proposal accepts already-in-city action");Check(ports==beforeBoard&&Get<PendingJourney?>(plugin,"pendingJourney")==null&&marker?.Map==board.Map,"Already-in-city board action only flags board and cannot create fake travel timeout");
  string folder=Path.Combine(Path.GetTempPath(),"HuntRoute-runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"state.json");
  try {
   Set(plugin,"stateFile",file);flow.Active=true;Call(plugin,"SaveJourney",true);
   var copy=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));Set(copy,"store",HuntStore.Load(file));Set(copy,"character",123UL);Set(copy,"Session",new RouteSession());Set(copy,"Search",new EliteSearch());Call(copy,"LoadJourney");
   var recovered=Get<JourneyFlow>(copy,"Flow");Check(recovered.Active&&recovered.Pauses==PauseReason.Restored&&!recovered.CanNavigate,"Real route save/load restores on pause with no transport or marker calls");
  }finally{foreach(var fileName in Directory.GetFiles(folder))File.Delete(fileName);Directory.Delete(folder);Set(plugin,"stateFile",null);}
  // A marker write failure is a different pause from temporarily unavailable goals.
  flow.Active=true;flow.Pauses=PauseReason.None;territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);counts.Clear();session.Retarget(newOrder);
  int failedWrites=0;Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(_=>{failedWrites++;return false;}));Call(plugin,"AdvanceRoute");Check(failedWrites==3&&flow.Pauses.HasFlag(PauseReason.FlagError),"Marker write failure has bounded retries and an explicit independent pause");
  Call(plugin,"AdvanceRoute");Check(failedWrites==3,"A marker failure is not automatically cleared merely because goals remain eligible");
  Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(stamp=>{marker=stamp;writes++;return true;}));flow.Pauses=PauseReason.Unavailable;Call(plugin,"AdvanceRoute");Check(!flow.Pauses.HasFlag(PauseReason.Unavailable),"Technical goal availability recovers automatically without clearing other pause kinds");
  // Gateway arrival is distinct from entering the actual hunting territory.
  var hinterTarget=game.Excel.GetSubrowSheet<MobHuntOrder>().SelectMany(g=>g).Where(o=>o.Target.RowId!=0).Select(o=>o.Target.Value).First(t=>t.Map.Value.TerritoryType.RowId==399&&Location.Database.ContainsKey(t.Name.RowId));
  var gateway=Daily(hinterTarget.Name.RowId,3,399);Set(plugin,"Mobs",new List<MobHuntEntry>{gateway});Set(plugin,"Routes",new List<ZoneRoute>{new(gateway.TerritoryName!,new(){gateway},null)});queue.Clear();flow.Pauses=PauseReason.None;flow.Manual=null;flow.Active=true;
  Call(plugin,"Travel",gateway);Check(Get<PendingJourney>(plugin,"pendingJourney").Destination==478,"Hinterlands trip expects actual Idyllshire teleport destination");
  clock+=10000;territory=478;mapId=game.Excel.GetSheet<TerritoryType>().GetRow(478).Map.RowId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Call(plugin,"ReconcileTravel");
  Check(flow.Pauses.HasFlag(PauseReason.NoZone)&&!flow.Pauses.HasFlag(PauseReason.TravelError)&&session.Target==gateway,"Idyllshire arrival pauses for the known gateway instead of reporting failed teleport");
  territory=399;mapId=gateway.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"context","town");Call(plugin,"ReconcileContext");Call(plugin,"AdvanceRoute");
  Check(flow.CanNavigate&&session.Phase==RoutePhase.Following&&marker?.Territory==399,"Crossing gateway resumes first hunt flag automatically");
  // Restore original bill fixture for the independent reset notification checks.
  Set(plugin,"Mobs",new List<MobHuntEntry>{newOrder,b,c,elite});Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){newOrder,b,elite},null,true),new(c.TerritoryName!,new(){c},null)});
  // A queued login offer must be deferred again if hunting resumes before the UI pump.
  queue.Clear();var savedNotice=Get<SavedJourney>(plugin,"CharacterSave");savedNotice.DailyNotice=0;flow.Active=true;flow.Pauses=PauseReason.User;Call(plugin,"CheckResetNotice");flow.Pauses=PauseReason.None;
  Check(queue.Activate(true)==null,"Queued reset dialog cannot interrupt a resumed active route");flow.Pauses=PauseReason.User;request=queue.Activate(true);Check(request?.Owner=="pickup","Deferred reset remains available at next explicit pause");queue.Clear();
  // The fast elite branch must not progress while the player is unconscious.
  queue.Clear();flow.Active=true;flow.Pauses=PauseReason.None;territory=397;mapId=elite.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);session.Retarget(elite);search.Start([new(0,0),new(200,0)]);Set(plugin,"searchKey",Call(plugin,"SearchIdentity",elite));counts.Clear();objects=[Npc(90,elite.MobHuntId,new(5,0,0))];unconscious=true;int beforeUnconscious=writes;
  Call(plugin,"UpdateEliteSearch");Check(search.ActorId==null&&writes==beforeUnconscious,"Fast search is suspended while unconscious and does not flag an encountered actor");
  unconscious=false;Set(plugin,"contractAt",clock);Call(plugin,"UpdateEliteSearch");Check(search.ActorId==90,"Fast detection resumes after recovering consciousness");
  counts[(5,0)]=1;unconscious=true;Call(plugin,"AdvanceRoute");Check(search.ActorId==null&&search.Round==0,"Authoritative credit clears elite search even before a busy/unconscious early return");unconscious=false;
  // Audit regressions exercise the actual controller, not only the pure models.
  queue.Clear();counts.Clear();objects=[];flow.Pauses=PauseReason.None;flow.Manual=null;flow.Active=true;Set(plugin,"nextTeleportAllowed",0L);Set(plugin,"pendingJourney",null);Set(plugin,"ownedMarker",null);
  int[] kills=[2,0,0,0,0];Check((bool)Call(plugin,"AcceptBillSnapshot",(byte)1,new BillSnapshot(true,1,kills),(byte)10)!,"Controller accepts initial observed bill");
  uint generation=Get<SavedJourney>(plugin,"CharacterSave").Contracts[1].Generation;clock+=1000;
  Check(!(bool)Call(plugin,"AcceptBillSnapshot",(byte)1,new BillSnapshot(false,0,[0,0,0,0,0]),(byte)10)!&&counts[(1,0)]==2&&Get<SavedJourney>(plugin,"CharacterSave").Contracts[1].Taken,"Controller preserves counters and obtained episode after single empty snapshot");
  session.Retarget(b);objects=[Npc(92,elite.MobHuntId,new(1,0,0))];int beforeUncertain=writes,beforeUncertainPorts=ports;Call(plugin,"AdvanceRoute");Call(plugin,"DetectorNavigation");Call(plugin,"Travel",c);
  Check(session.Target==b&&writes==beforeUncertain&&ports==beforeUncertainPorts,"Uncertain current bill freezes goal, automatic detection and transport until confirmed data");objects=[];
  clock+=1000;Call(plugin,"AcceptBillSnapshot",(byte)1,new BillSnapshot(true,1,kills),(byte)10);Check(Get<SavedJourney>(plugin,"CharacterSave").Contracts[1].Generation==generation,"Transient empty read cannot create false contract generation");
  Get<HashSet<byte>>(plugin,"unreadBills").Clear();counts.Clear();
  session.Retarget(elite);search.Start([new(0,0),new(200,0)]);Set(plugin,"searchKey",Call(plugin,"SearchIdentity",elite));objects=[Npc(91,elite.MobHuntId,new(4,0,0))];flow.Pauses=PauseReason.Marker|PauseReason.User;before=writes;
  Call(plugin,"AdvanceRoute");Check(search.ActorId==91&&writes==before&&flow.Pauses.HasFlag(PauseReason.User),"Paused controller detects elite without replacing user flag or clearing independent pause");
  objects=[];flow.Pauses=PauseReason.None;flow.Active=false;territory=401;mapId=c.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,elite});Call(plugin,"BuildRoutes");
  var calculated=Get<List<ZoneRoute>>(plugin,"Routes").Single();var first=calculated.Mobs.First(m=>!Get<Dictionary<string,string>>(flow,"Unavailable").ContainsKey(JourneyFlow.Key(m)));
  Check(calculated.Start!=null&&(EntryPoint?)Call(plugin,"EntryFor",first)==calculated.Start,"Calculated entry is retained for first target");
  // Force a valid non-nearest entry: transport must consume the reviewed plan.
  var alternatives=(List<EntryPoint>)Call(plugin,"Entries",a.MapId,a.TerritoryType)!;var planned=alternatives.Last();Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){a,b,elite},planned)});uint transported=0;
  Set(plugin,"TeleportTransport",(Func<uint,bool>)(id=>{transported=id;ports++;return true;}));flow.Active=true;session.Retarget(a);Set(plugin,"nextTeleportAllowed",0L);Call(plugin,"Travel",a);
  Check(transported==planned.Id,"Actual teleport consumes planned entry rather than independently selecting nearest anchor");
  Set(plugin,"pendingJourney",null);Set(plugin,"nextTeleportAllowed",0L);flow.Pauses=PauseReason.None;territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);session.Retarget(a);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,c,elite});counts.Clear();
  var anchor=MapCoordinates.ToWorld(Location.Database[a.MobHuntId].Coordinate,game.Excel.GetSheet<Map>().GetRow(a.MapId));position=new(anchor.X+5000,0,anchor.Y);
  var shortcut=(ShortcutOffer?)Call(plugin,"FindShortcut");Check(shortcut!=null,$"Long-flight regression fixture has a valid shortcut: phase={session.Phase}, pause={flow.Pauses}, ready={Get<bool>(plugin,"Ready")}, settings={store.Settings.Shortcuts}, overhead={Get<ShortcutTiming>(plugin,"Timing").OverheadSeconds}, point={position}, anchor={anchor}, entries={alternatives.Count}");queue.Clear();Call(Get<ShortcutWindow>(plugin,"Shortcut"),"Sync");request=queue.Activate(true);Check(request?.Owner=="shortcut","Shortcut controller queues reviewed proposal");
  var replacement=new MobHuntEntry{Name=a.Name,BillNumber=a.BillNumber,OrderId=a.OrderId,Generation=a.Generation+1,MobIndex=a.MobIndex,MobHuntId=a.MobHuntId,MapId=a.MapId,TerritoryType=a.TerritoryType,NeededKills=1};session.Retarget(replacement);
  Check(!request!.Valid(),"Same mob in a new contract generation invalidates open shortcut dialog");Service.Chat=Proxy<IChatGui>((_,_)=>null);int beforeStale=ports;
  Check(!(bool)Call(plugin,"AcceptShortcut",shortcut)!&&ports==beforeStale,"Stale shortcut acceptance cannot submit teleport for a replacement contract");queue.Clear();position=Vector3.Zero;
  // The union offer may remain valid when just one category became unavailable.
  savedNotice.DailyNotice=0;savedNotice.WeeklyNotice=0;flow.Active=false;flow.Pauses=PauseReason.None;Set(plugin,"contractRevision",20L);Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)},{5,new(true,false,0,1,false)}});Call(plugin,"CheckResetNotice");
  request=queue.Activate(true);Check(request?.Owner=="pickup","Combined daily and weekly offer exists");Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,true,1,1,false)},{5,new(true,false,0,1,false)}});
  Check(request!.Valid(),"Weekly category keeps union offer valid after daily pickup");request.OnShown!();Check(savedNotice.DailyNotice==0&&savedNotice.WeeklyNotice!=0,"Actual show records only category still presented as available");queue.Clear();
  string ioFolder=Path.Combine(Path.GetTempPath(),"HuntRoute-history-failure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ioFolder);
  try{
   string blocked=Path.Combine(ioFolder,"history.json");Directory.CreateDirectory(blocked);var history=new CompletionHistory();history.Observe(123,1,false,DateTimeOffset.UtcNow,new(true,true,1,1,false));history.Observe(123,1,false,DateTimeOffset.UtcNow,new(true,false,1,1,true));Set(plugin,"completionHistory",history);Set(plugin,"completionFile",blocked);
   Call(plugin,"SaveHistory",true);Check(Get<string?>(plugin,"HistorySaveError")!=null&&history.IsComplete(123,1,false,DateTimeOffset.UtcNow),"Write failure exposes persistent warning without altering confirmed completion");Directory.Delete(blocked);Call(plugin,"RetryPersistence");
   Check(Get<string?>(plugin,"HistorySaveError")==null&&CompletionHistory.Load(blocked).IsComplete(123,1,false,DateTimeOffset.UtcNow),"Explicit retry persists unchanged result and clears warning");
  }finally{Directory.Delete(ioFolder,true);Set(plugin,"completionFile",null);}
  // An invalid hi-res portrait must still try standard-res and never break the hunt UI.
  var picture=Proxy<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap>((m,_)=>m.Name switch{"get_Width"=>128,"get_Height"=>128,_=>throw new Exception(m.Name)});
  var immediate=Proxy<Dalamud.Interface.Textures.ISharedImmediateTexture>((m,_)=>m.Name=="GetWrapOrDefault"?picture:throw new Exception(m.Name));
  var attempts=new List<bool>();
  Service.TextureProvider=Proxy<ITextureProvider>((m,args)=>{if(m.Name!="GetFromGameIcon")throw new Exception(m.Name);var icon=(Dalamud.Interface.Textures.GameIconLookup)args![0]!;attempts.Add(icon.HiRes);if(icon.HiRes)throw new IOException("missing hi-res image");return immediate;});
  var preview=typeof(Plugin).Assembly.GetType("HuntRoute.MobPreview")!.GetMethod("Image",BindingFlags.NonPublic|BindingFlags.Static)!;
  Check(ReferenceEquals(preview.Invoke(null,new object[]{999001u}),picture)&&attempts.SequenceEqual(new[]{true,false}),"Portrait loader falls back to normal resolution after high-resolution failure");
  Service.TextureProvider=Proxy<ITextureProvider>((_,_)=>throw new IOException("no texture"));Check(preview.Invoke(null,new object[]{999002u})==null,"Missing portrait is isolated from route/interface logic");
  Console.WriteLine($"PASS {checks} integrated journey controller scenarios (fake clock/transport/map; real game sheets)");return checks;
 }
}
