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
