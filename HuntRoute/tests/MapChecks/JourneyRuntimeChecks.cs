using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
static class JourneyRuntimeChecks {
 static Plugin CreatePlugin(){
  var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
  Set(plugin,"expansionCompletionNotice",Activator.CreateInstance(typeof(Plugin).Assembly.GetType("HuntRoute.ExpansionCompletionNotice")!));
  Set(plugin,"shortcutHeights",new Dictionary<string,float>());
  Set(plugin,"observedFares",new Dictionary<uint,IReadOnlyDictionary<uint,uint>>());
  return plugin;
 }
 static object? Call(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args);
 static void Set(object target,string name,object? value){var type=target.GetType();var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)f.SetValue(target,value);else type.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(target,value);}
 static T Get<T>(object target,string name){var type=target.GetType();var f=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);return (T)(f!=null?f.GetValue(target):type.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(target))!;}
 static T Proxy<T>(Func<MethodInfo,object?[]?,object?> call)where T:class{var proxy=DispatchProxy.Create<T,ServiceProxy>();((ServiceProxy)(object)proxy).Handler=call;return proxy;}
 static void CheckCompletionChat(bool manual=false,bool otherExpansion=false){
  var plugin=CreatePlugin();
  Set(plugin,"Ready",true);
  Set(plugin,"character",123UL);
  var states=new Dictionary<byte,BillState>();
  Set(plugin,"BillStates",states);
  var history=new CompletionHistory();
  Set(plugin,"completionHistory",history);
  var done=new BillState(true,false,1,1,true);
  var taken=new BillState(true,true,1,1,false);
  byte[] heavensward=[1,2,3,5];
  foreach(byte bill in heavensward)states[bill]=taken;
  states[14]=new(true,false,0,1,false); // Endwalker: daily only.
  states[13]=new(true,false,0,1,false); // Shadowbringers: weekly only.
  states[18]=new(true,false,0,1,false); // Dawntrail: both.
  states[21]=new(true,false,0,1,false);
  states[6]=taken; // Stormblood already taken.
  var printed=new List<SeString>();
  var owned=new MarkerStamp(10,20,100,200);
  MarkerStamp? current=manual?new MarkerStamp(10,20,300,400):owned;
  int cleared=0;
  Set(plugin,"ownedMarker",owned);
  Set(plugin,"ownedMarkerBill",otherExpansion?(byte)14:(byte)1);
  Set(plugin,"MarkerReader",(Func<MarkerStamp?>)(()=>current));
  Set(plugin,"MarkerClearer",(System.Action)(()=>{cleared++;current=null;}));
  int sounds=0;
  System.Action playSound=()=>sounds++;
  var previous=Service.Chat;
  try{
   Service.Chat=Proxy<IChatGui>((method,args)=>{
    if(method.Name!="Print"||args?[0] is not SeString message)throw new Exception("Unexpected completion notification channel");
    printed.Add(message);return null;
   });
   Call(plugin,"CheckExpansionCompletionNotices",playSound);
   if(printed.Count!=0||sounds!=0)throw new Exception("A taken bill is not a completed expansion");
   foreach(byte bill in heavensward){
    states[bill]=done;
    history.Observe(123,bill,bill==5,DateTimeOffset.UtcNow,done);
   }
   Call(plugin,"CheckExpansionCompletionNotices",playSound);
   Call(plugin,"CheckExpansionCompletionNotices",playSound);
   if(printed.Count!=1||sounds!=1)throw new Exception("Completion must print one chat message and play one sound");
   if(cleared!=(manual||otherExpansion?0:1)||((current==null)!=(!manual&&!otherExpansion)))
    throw new Exception("Completion must clear only its own map flag");
   var text=printed[0];
   if(text.Payloads.OfType<NewLinePayload>().Count()!=3 ||
      text.TextValue.Replace("\r\n","\n")!="[Hunt Route] Heavensward — выполнено\nShadowbringers — можно взять: элитный\nEndwalker — можно взять: обычные\nDawntrail — можно взять: обычные и элитный")
    throw new Exception("Chat status includes the wrong boards: "+text.TextValue);
   if(!text.Payloads.OfType<UIForegroundPayload>().Select(p=>p.ColorKey).SequenceEqual(new ushort[]{45,0,31,0,31,0,31,0}))
    throw new Exception("Completion and availability colors are wrong or leak into chat");
   Console.WriteLine("PASS one colored completion chat message with only available follow-up boards");
  }finally{Service.Chat=previous;}
 }
 private static void CheckShortcutNavigation(){
  var navigation=typeof(Plugin).Assembly.GetType("HuntRoute.ShortcutNavigation")!;
  int probes=0,projects=0,requests=0;
  bool ready=true;
  var oldPath=new TaskCompletionSource<List<Vector3>>();
  CancellationToken oldToken=default;
  var ground=new Vector3(400,-20,0);
  Func<Vector3,bool,Vector3?> project=(point,known)=>{
   projects++;
   if(known&&point.Y!=-20)throw new Exception("Known position must preserve the lower floor");
   return point.X==390?null:point;
  };
  Func<Vector3,Vector3,CancellationToken,Task<List<Vector3>>> path=(from,to,token)=>{
   if(++requests==1){oldToken=token;return oldPath.Task;}
   return Task.FromResult(new List<Vector3>{new(from.X,-20,400),new(to.X,-20,400),to});
  };
  var instance=Activator.CreateInstance(navigation,BindingFlags.Instance|BindingFlags.NonPublic,null,
   [new Func<bool>(()=>{probes++;return ready;}),project,path,null],null)!;
  ShortcutCandidate[] entries=[new(1,"Bad",new(390,0),100),new(2,"Good",new(380,0),100){Height=-20}];
  object? Read(string zone,long now,out bool waiting){
   object?[] args=[zone,new Vector3(0,-20,0),ground,true,entries,now,false];
   var result=Call(instance,"Read",args);
   waiting=(bool)args[6]!;
   return result;
  }
  Read("old",100,out _);
  for(int i=0;i<100;i++)Read("old",101,out _);
  if(probes!=1||projects!=1||requests!=1)throw new Exception("Repeated reads must reuse the job and step budget");
  if(Read("new",102,out bool waiting)!=null||!waiting||!oldToken.IsCancellationRequested)
   throw new Exception("Changing zones must cancel the old path and permit immediate fallback");
  object? fresh=null;
  for(int i=103;i<=112;i++)fresh=Read("new",i,out _);
  if(fresh==null||requests!=3||projects!=4)throw new Exception("A hung job or invalid entry must not block a valid mesh path");
  oldPath.SetResult([ground]);
  if(!ReferenceEquals(fresh,Read("new",120,out _)))throw new Exception("Late path must not replace the current result");
  ready=false;
  if(Read("new",800,out waiting)!=null||waiting)throw new Exception("Unavailable mesh must allow fallback immediately");
  Call(instance,"Reset",new object?[]{1000L});
  ready=true;
  if(Read("newer",2100,out waiting)!=null||waiting||requests!=3)throw new Exception("A provider still reporting the old mesh must allow only fallback");
  ready=false;
  Read("newer",2200,out _);
  ready=true;
  Read("newer",2300,out waiting);
  if(!waiting)throw new Exception("The next mesh must be queried after an observed reload");

  var stableFares=typeof(Plugin).GetMethod("StableFares",BindingFlags.Static|BindingFlags.NonPublic)!;
  var pluginFares=(IReadOnlyDictionary<uint,uint>)new Dictionary<uint,uint>{{1,100},{2,100}};
  object? result=null;
  var normal=Activator.CreateInstance(navigation,BindingFlags.Instance|BindingFlags.NonPublic,null,
   [new Func<bool>(()=>true),project,path,null],null)!;
  for(int tick=0;tick<=3000;tick+=100){
   if(tick%1000==0) {
    var next=(IReadOnlyDictionary<uint,uint>)stableFares.Invoke(null,[pluginFares,new Dictionary<uint,uint>{{2,100},{1,100}}])!;
    if(!ReferenceEquals(next,pluginFares))throw new Exception("Refreshing unchanged fares restarted the calculation");
    pluginFares=next;
   }
   result=Call(normal,"Read",["stable",new Vector3(0,-20,0),ground,true,entries,1000L+tick,false]);
  }
  if(result==null)throw new Exception("A shortcut must complete at the framework cadence across fare refreshes");
  if(ReferenceEquals(pluginFares,stableFares.Invoke(null,[pluginFares,new Dictionary<uint,uint>{{1,110},{2,100}}])))
   throw new Exception("A real fare change must invalidate the old quote");

  var queue=new ConfirmationQueue();
  bool measuring=false;
  var dialog=new ConfirmationRequest("shortcut","quote","quote",()=>true,()=>true,()=>true,DeclineKey:"contract",ConfirmReady:()=>!measuring);
  queue.Offer(dialog);
  if(queue.Activate(true)!=dialog)throw new Exception("Shortcut must open when ready");
  measuring=true;
  if(!dialog.Valid()||dialog.CanConfirm()||queue.Active!=dialog)throw new Exception("Refreshing a quote must preserve the window and disable confirmation");
  var quote=new ShortcutOffer(1,1,1,1,1,2,"Entry",100,1000,200,50,30);
  if(ShortcutAdvisor.SamePresentation(quote,quote with {GilCost=90})||
     ShortcutAdvisor.SamePresentation(quote,quote with {TeleportSeconds=25}))
   throw new Exception("A refreshed fare or saving must replace the displayed quote");
  queue.Resolve(dialog.Key,false);
  measuring=false;
  queue.Offer(dialog with {Key="updated"});
  if(queue.Activate(true)!=null)throw new Exception("Later must suppress refreshed offers");
  Console.WriteLine("PASS vnavmesh elevation, partial candidate failure, request budget, ignored cancellation, zone transitions and dialog refresh");
 }
 public static void RunOffline(GameData? game=null){
  CheckShortcutNavigation();
  CheckCompletionChat();
  CheckCompletionChat(manual:true);
  CheckCompletionChat(otherExpansion:true);
  static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
  var plugin=CreatePlugin();
  var priorList=Service.Aetherytes;
  try{
   Service.Aetherytes=Proxy<IAetheryteList>((_,_)=>throw new Exception("Snapshot lookup must not enumerate the native service"));
   Set(plugin,"fareSnapshot",new Dictionary<uint,uint>{{70,0},{75,100}});
   for(int i=0;i<100;i++){
    Check((uint)Call(plugin,"Price",70u)! ==0u&&(uint)Call(plugin,"Price",75u)! ==100u,"Cached quote preserves free and paid teleports");
    Check((bool)Call(plugin,"IsAetheryteUnlocked",75u)!&&!(bool)Call(plugin,"IsAetheryteUnlocked",76u)!,"Unlock checks use the same snapshot");
   }
   Set(plugin,"fareSnapshot",new Dictionary<uint,uint>{{75,250}});
   Check((uint)Call(plugin,"Price",75u)! ==250u&&(uint)Call(plugin,"Price",70u)! ==uint.MaxValue,"Replacement snapshot invalidates old fares and unlocks");
  }finally{Set(plugin,"fareSnapshot",null);Service.Aetherytes=priorList;}

  Set(plugin,"Ready",true);Set(plugin,"character",123UL);Set(plugin,"contractRevision",2L);
  Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>());
  Set(plugin,"Mobs",new List<MobHuntEntry>{new(){BillNumber=1,NeededKills=1}});
  Set(plugin,"BillStates",new Dictionary<byte,BillState>{{6,new(true,false,0,1,false)},{9,new(true,false,0,1,false)}});
  var bridge=RuntimeHelpers.GetUninitializedObject(typeof(Plugin).Assembly.GetType("HuntRoute.NativeDialogService")!);
  var queue=new ConfirmationQueue();Set(bridge,"queue",queue);Set(plugin,"NativeDialogs",bridge);
  Service.Condition=Proxy<ICondition>((_,_)=>false);
  Service.ObjectTable=Proxy<IObjectTable>((_,_)=>null);
  var flow=Get<JourneyFlow>(plugin,"Flow");
  var saved=Get<SavedJourney>(plugin,"CharacterSave");
  if(game!=null){
   var previousChat=Service.Chat;
   var previousData=Service.DataManager;
   var messages=new List<SeString>();
   bool duty=true;
   try{
    Service.DataManager=new SheetServices(game);
    Service.Chat=Proxy<IChatGui>((method,args)=>{if(method.Name=="Print"&&args?[0] is SeString message)messages.Add(message);return null;});
    Service.Condition=Proxy<ICondition>((_,args)=>duty&&args?[0] is ConditionFlag[] flags&&flags.Contains(ConditionFlag.BoundByDuty));
    Set(plugin,"CurrentTerritory",397u);
    Call(plugin,"CheckResetChatNotice");
    Check(messages.Count==0&&saved.DailyChatNotice==0&&saved.WeeklyChatNotice==0,"Duty defers the reset chat");
    duty=false;
    Call(plugin,"CheckResetChatNotice");Call(plugin,"CheckResetChatNotice");
    Check(messages.Count==1&&messages[0].TextValue.Contains("Stormblood — ежедневные и недельный элитный"),"Reset chat lists both available contract types once");
    saved.DailyChatNotice-=86400;
    Call(plugin,"CheckResetChatNotice");Call(plugin,"CheckResetChatNotice");
    Check(messages.Count==2&&messages[1].TextValue.Contains("Stormblood — ежедневные")&&!messages[1].TextValue.Contains("элитный"),"New daily period does not repeat weekly chat");
    Console.WriteLine("PASS reset chat duty suppression, open-world delivery and daily/weekly deduplication");
   }finally{
    Set(plugin,"CurrentTerritory",0u);
    Service.Chat=previousChat;Service.DataManager=previousData;Service.Condition=Proxy<ICondition>((_,_)=>false);
   }
  }
  Call(plugin,"CheckResetNotice");
  var login=queue.Activate(true)??throw new Exception("Login prompt missing");
  Check(login.Key.EndsWith(":taken"),"Unfinished contract must offer continuation");
  login.OnShown!();queue.Resolve(login.Key,false);
  Check(saved.DailyNotice==0&&saved.WeeklyNotice==0,"Continuation acknowledged categories it did not show");
  flow.Active=true;Call(plugin,"CheckResetNotice");
  Check(queue.Activate(true)==null,"Pickup prompt interrupts an active hunt");
  flow.Pauses=PauseReason.User;Call(plugin,"CheckResetNotice");
  var pickup=queue.Activate(true)??throw new Exception("New daily and weekly bills suppressed after login");
  Check(pickup.Key.StartsWith("pickup:"),"Expected new-contract prompt after pause");
  pickup.OnShown!();queue.Resolve(pickup.Key,false);
  Check(saved.DailyNotice!=0&&saved.WeeklyNotice!=0,"Showing pickup must acknowledge both available categories");
  Call(plugin,"CheckResetNotice");
  Check(queue.Activate(true)==null,"Later repeated pickup for the same periods");
  saved.DailyNotice-=86400;Call(plugin,"CheckResetNotice");
  var daily=queue.Activate(true)??throw new Exception("New daily period is still suppressed");
  Check(daily.Key.EndsWith(":0"),"New daily period incorrectly included acknowledged weekly bills");
  Console.WriteLine("PASS offline login, reset, pause and Later regressions");
  var fallback=Activator.CreateInstance(bridge.GetType(),BindingFlags.Instance|BindingFlags.NonPublic,null,
   [Task.FromException(new InvalidOperationException("Simulated native init fault"))],null)!;
  Service.PluginLog=Proxy<IPluginLog>((_,_)=>null);
  Service.GameGui=Proxy<IGameGui>((method,_)=>Activator.CreateInstance(method.ReturnType));
  System.Action? deferred=null;int accepts=0,shown=0;bool ready=true;
  Service.Framework=Proxy<IFramework>((method,args)=>{
   if(method.Name!="RunOnTick")throw new Exception(method.Name);
   deferred=(System.Action)args![0]!;return Task.CompletedTask;
  });
  ConfirmationRequest Request(string key)=>new("travel",key,"РўРµР»РµРїРѕСЂС‚?",()=>true,()=>ready,()=>{accepts++;return true;},OnShown:()=>shown++,DeclineKey:key);
  var later=Request("later");Call(fallback,"Offer",later,false);Call(fallback,"Pump");
  Check(ReferenceEquals(Call(fallback,"PresentFallback"),later)&&shown==1,"Initialization fault exposes the same request via classic confirmation");
  Call(fallback,"PresentFallback");Check(shown==1,"Fallback does not acknowledge every draw");
  Call(fallback,"AnswerFallback",later.Key,false);
  Check(accepts==0&&deferred==null&&(bool)Call(fallback,"WasDeclined",later.Key)!,"Later dismisses fallback without requesting teleport");
  var yes=Request("yes");Call(fallback,"Offer",yes,false);Call(fallback,"Pump");Call(fallback,"PresentFallback");
  Call(fallback,"AnswerFallback",yes.Key,true);Call(fallback,"AnswerFallback",yes.Key,true);
  Check(accepts==0&&deferred!=null,"Fallback confirm schedules the action on the game tick");
  deferred!();Check(accepts==1,"Fallback confirmation accepts exactly once");
  var busy=Request("busy");Call(fallback,"Offer",busy,false);Call(fallback,"Pump");Call(fallback,"PresentFallback");
  Call(fallback,"AnswerFallback",busy.Key,true);ready=false;deferred!();
  Check(accepts==1,"Fallback revalidates readiness before teleport");
  Call(fallback,"CancelAll");
  Console.WriteLine("PASS offline native initialization fault, classic confirmation and Later");
 }
 public static int Run(GameData game){
  int checks=0;void Check(bool yes,string message){checks++;if(!yes)throw new Exception(message);}
  var plugin=CreatePlugin();
  var session=new RouteSession();var search=new EliteSearch();Set(plugin,"Session",session);Set(plugin,"Search",search);Set(plugin,"Ready",true);Set(plugin,"character",123UL);Set(plugin,"loginNoticeShown",true);
  var flow=Get<JourneyFlow>(plugin,"Flow");var store=Get<HuntStore>(plugin,"Store");
  var bridgeType=typeof(Plugin).Assembly.GetType("HuntRoute.NativeDialogService")!;
  var bridge=RuntimeHelpers.GetUninitializedObject(bridgeType);var queue=new ConfirmationQueue();Set(bridge,"queue",queue);Set(plugin,"NativeDialogs",bridge);Set(plugin,"NextZone",new NextZoneWindow(plugin));Set(plugin,"Shortcut",new ShortcutWindow(plugin));
  long clock=10000;Set(plugin,"Clock",(Func<long>)(()=>clock));Set(plugin,"FlightCapability",(Func<bool>)(()=>true));
  uint territory=397;var map=game.Excel.GetSheet<TerritoryType>().GetRow(territory).Map.Value;uint mapId=map.RowId;
  Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);
  bool combat=false,casting=false,unconscious=false,loading=false,cutscene=false;var position=new Vector3(0,0,0);IGameObject[] objects=[];
  var player=Proxy<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>((m,_)=>m.Name switch{"get_Position"=>position,"get_IsCasting"=>casting,_=>throw new Exception(m.Name)});
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name switch{"get_LocalPlayer"=>player,"GetEnumerator"=>objects.AsEnumerable().GetEnumerator(),_=>throw new Exception(m.Name)});
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name switch{"get_IsLoggedIn"=>true,"get_TerritoryType"=>territory,"get_MapId"=>mapId,_=>throw new Exception(m.Name)});
  bool Flag(ConditionFlag f)=>f==ConditionFlag.InCombat?combat:f==ConditionFlag.Casting?casting:f==ConditionFlag.Unconscious?unconscious:f is ConditionFlag.BetweenAreas or ConditionFlag.BetweenAreas51?loading:f is ConditionFlag.WatchingCutscene or ConditionFlag.OccupiedInCutSceneEvent?cutscene:false;
  Service.Condition=Proxy<ICondition>((m,args)=>args is {Length:>0} ? args[0] is ConditionFlag[] flags?flags.Any(Flag):Flag((ConditionFlag)args[0]!) : false);
  Service.DataManager=new SheetServices(game);Service.PluginLog=Proxy<IPluginLog>((_,_)=>null);
  Service.Player=Proxy<IPlayerState>((m,_)=>m.Name switch{"get_IsLoaded"=>true,"get_ContentId"=>123UL,_=>throw new Exception(m.Name)});
  uint shortcutPrice=100;
  var entries=game.Excel.GetSheet<Aetheryte>().Where(a=>a.IsAetheryte).Select(a=>Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name switch{"get_AetheryteId"=>a.RowId,"get_GilCost"=>shortcutPrice,_=>throw new Exception(m.Name)})).ToArray();
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?entries.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  MarkerStamp? marker=null;int writes=0,ports=0,opens=0;
  Set(plugin,"MarkerReader",(Func<MarkerStamp?>)(()=>marker));Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(stamp=>{marker=stamp;writes++;return true;}));Set(plugin,"TeleportTransport",(Func<uint,bool>)(_=>{ports++;return true;}));
  bool mapOpens=true;int rawMapX=0;
  Service.GameGui=Proxy<IGameGui>((m,args)=>{if(m.Name=="OpenMapWithMapLink"){opens++;rawMapX=((Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload)args![0]!).RawX;return mapOpens;}throw new Exception(m.Name);});
  Set(plugin,"ContextProvider",(Func<string>)(()=>territory+":"+mapId));
  MobHuntEntry Daily(uint id,byte mob,uint zone){var row=game.Excel.GetSubrowSheet<MobHuntOrder>().SelectMany(g=>g).Where(o=>o.Target.RowId!=0).Select(o=>o.Target.Value).First(t=>t.Name.RowId==id&&t.Map.Value.TerritoryType.RowId==zone);return new(){Name=row.Name.Value.Singular.ToString(),BillNumber=1,OrderId=1,MobIndex=mob,MobHuntId=id,NeededKills=1,TerritoryType=zone,MapId=row.Map.RowId,TerritoryName=row.Map.Value.PlaceName.Value.Name.ToString()};}
  var a=Daily(3481,0,397);var b=Daily(3479,1,397);var c=Daily(3494,2,401);
  var spawn=EliteSpawns.Sets.First(p=>p.Value.Territory==397);var elite=new MobHuntEntry{Name=spawn.Value.Name,BillNumber=5,OrderId=1,MobIndex=0,MobHuntId=spawn.Key,TerritoryType=397,MapId=spawn.Value.Map,NeededKills=1,TerritoryName=spawn.Value.Zone,IsEliteMark=true};
  var counts=new Dictionary<(byte,byte),int>();Set(plugin,"KillCounts",counts);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,c,elite});Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){a,b,elite},null,true),new(c.TerritoryName!,new(){c},null)});
  Call(plugin,"ResumeJourney");Check(flow.Active&&session.Target==a&&writes==1&&ports==0,"Main start in current zone flags target without teleport");uint run=session.RunId;
  flow.Manual=JourneyFlow.Key(a);int beforeLocked=writes;
  Service.Aetherytes=Proxy<IAetheryteList>((_,_)=>Array.Empty<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>().AsEnumerable().GetEnumerator());
  Call(plugin,"Track",c);
  Check(session.Target==a&&flow.Manual==JourneyFlow.Key(a)&&writes==beforeLocked&&queue.Activate(true)==null,"Locked entry leaves old manual goal, flag and route intact");
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?entries.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  Call(plugin,"Track",c);
  Check(session.Target==c&&flow.Manual==JourneyFlow.Key(c)&&session.Phase==RoutePhase.AwaitingTravel&&queue.Activate(true)?.Owner=="travel",
   "Selecting the goal after unlocking sets a consistent manual target and offers travel");
  queue.Clear();Call(plugin,"Track",a);flow.Manual=null;
  IBattleNpc Npc(ulong id,uint name,Vector3 location,bool dead=false)=>Proxy<IBattleNpc>((m,_)=>m.Name switch{"get_GameObjectId"=>id,"get_NameId"=>name,"get_Position"=>location,"get_IsDead"=>dead,_=>throw new Exception(m.Name)});
  objects=[Npc(10,a.MobHuntId,new(10,0,0)),Npc(11,b.MobHuntId,new(20,0,0))];Call(plugin,"AdvanceRoute");Check(session.Target==a&&marker?.X==10,"Ordinary detection replaces anchor with nearest actual position");
  objects=[Npc(10,a.MobHuntId,new(12,0,0)),Npc(11,b.MobHuntId,new(1,0,0))];Call(plugin,"AdvanceRoute");Check(session.Target==a&&marker?.X==12,"Ordinary actor stays selected while another gets closer; position updates");
  objects=[Npc(10,a.MobHuntId,new(13,0,0)),Npc(12,a.MobHuntId,new(1,0,0))];Call(plugin,"AdvanceRoute");Check(marker?.X==12,"First short move stays inside flag tolerance");
  objects=[Npc(10,a.MobHuntId,new(14,0,0)),Npc(12,a.MobHuntId,new(1,0,0))];Call(plugin,"AdvanceRoute");Check(marker?.X==14,"Short moves accumulate from last confirmed flag");
  Call(plugin,"ShowMap",a);Check(Math.Abs(rawMapX-14000)<4,"Opening current target uses held actor even when a different same-name actor is closer");
  mapOpens=false;var priorFlag=Get<MarkerStamp?>(plugin,"ownedMarker");Call(plugin,"ShowMap",b);
  Check(flow.Pauses==PauseReason.None&&Get<MarkerStamp?>(plugin,"ownedMarker")==priorFlag,"Failed map open preserves flag ownership and pause");
  mapOpens=true;opens=0;
  Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(_=>false));objects=[Npc(10,a.MobHuntId,new(18,0,0))];Call(plugin,"AdvanceRoute");
  Check(flow.Pauses.HasFlag(PauseReason.FlagError)&&marker?.X==14,"Failed flag write pauses while preserving old marker");
  Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(stamp=>{marker=stamp;writes++;return true;}));Call(plugin,"ResumeJourney");
  Check(marker?.X==18&&!flow.Pauses.HasFlag(PauseReason.FlagError),"Resume retries the same unchanged actor after flag failure");
  var unknown=new MobHuntEntry{BillNumber=1,OrderId=1,MobIndex=4,MobHuntId=99999999,NeededKills=1,TerritoryType=territory,MapId=mapId};
  var originalMobs=Get<List<MobHuntEntry>>(plugin,"Mobs");originalMobs.Add(unknown);
  var localGoals=Get<List<ZoneRoute>>(plugin,"Routes").First().Mobs;localGoals.Add(unknown);
  objects=[Npc(13,unknown.MobHuntId,new(3,0,0))];Call(plugin,"Track",unknown);Check(session.Target==unknown,"Manual live-only target is tracked");
  objects=[Npc(10,a.MobHuntId,new(18,0,0))];Call(plugin,"RefreshLive");Call(plugin,"AdvanceRoute");Call(plugin,"ResumeJourney");
  Check(session.Target==unknown&&flow.Manual==JourneyFlow.Key(unknown)&&flow.Pauses.HasFlag(PauseReason.Unavailable),"Unavailable manual goal remains pinned despite another available target");
  objects=[Npc(13,unknown.MobHuntId,new(3,0,0))];Call(plugin,"AdvanceRoute");
  Check(session.Target==unknown&&session.Phase==RoutePhase.Following&&flow.Pauses==PauseReason.None&&marker?.X==3,"Manual goal resumes when it regains coordinates");
  objects=[];Call(plugin,"RefreshLive");Call(plugin,"AdvanceRoute");
  Check(flow.Pauses.HasFlag(PauseReason.Unavailable),"Manual target can become unavailable again");
  marker=new(territory,mapId,999,999);var playerFlag=marker;int beforeRecovery=writes;
  objects=[Npc(13,unknown.MobHuntId,new(6,0,0))];Call(plugin,"AdvanceRoute");
  Check(marker==playerFlag&&writes==beforeRecovery&&flow.Pauses==PauseReason.Marker&&session.Target==unknown,
   "Returning manual target preserves the player flag and pauses before writing");
  Call(plugin,"ResumeJourney");
  Check(marker?.X==6&&writes==beforeRecovery+1&&flow.Pauses==PauseReason.None,"Explicit resume restores the pinned target flag");
  originalMobs.Remove(unknown);localGoals.Remove(unknown);session.Retarget(a);
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
  Check(request?.Presentation?.Title==game.Excel.GetSheet<TerritoryType>().GetRow(board.Territory).PlaceName.Value.Name.ToString() && (request.Presentation.Destination==game.Excel.GetSheet<Aetheryte>().GetRow(board.Aetheryte).PlaceName.Value.Name.ToString()||request.Presentation.Title==game.Excel.GetSheet<Aetheryte>().GetRow(board.Aetheryte).PlaceName.Value.Name.ToString()),"Board proposal explicitly identifies actual city and aetheryte");
  Check(request!=null&&queue.Resolve(request.Key,true)!.Accept(),"Board proposal accepts already-in-city action");Check(ports==beforeBoard&&Get<PendingJourney?>(plugin,"pendingJourney")==null&&marker?.Map==board.Map,"Already-in-city board action only flags board and cannot create fake travel timeout");
  string folder=Path.Combine(Path.GetTempPath(),"HuntRoute-runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string file=Path.Combine(folder,"state.json");
  try {
   Set(plugin,"stateFile",file);flow.Active=true;Call(plugin,"SaveJourney",true);
   var copy=CreatePlugin();Set(copy,"store",HuntStore.Load(file));Set(copy,"character",123UL);Set(copy,"Session",new RouteSession());Set(copy,"Search",new EliteSearch());Call(copy,"LoadJourney");
   var recovered=Get<JourneyFlow>(copy,"Flow");Check(recovered.Active&&recovered.Pauses==PauseReason.Restored&&!recovered.CanNavigate,"Real route save/load restores on pause with no transport or marker calls");
   var durable=HuntStore.Load(file);
   durable.Characters[123].Deferred.Add("saved-deferred");durable.Characters[123].DailyNotice=42;
   durable.Characters[123].Contracts[1]=new(){Generation=7};
   durable.Characters[124]=new(){WeeklyNotice=99};
   durable.Save(file);
   string legacyFile=Path.Combine(folder,"journey-state.json"),migratedFile=Path.Combine(folder,"hunt-settings.json");
   var legacy=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
   legacy["Settings"]=System.Text.Json.JsonSerializer.SerializeToNode(new HuntSettings{Mini=true,MaxGil=321});
   File.WriteAllText(legacyFile,legacy.ToJsonString());
   Service.PluginInterface=Proxy<Dalamud.Plugin.IDalamudPluginInterface>((method,_)=>method.Name=="GetPluginConfigDirectory"?folder:throw new Exception(method.Name));
   var migrating=CreatePlugin();
   Set(migrating,"character",123UL);Set(migrating,"Session",new RouteSession());Set(migrating,"Search",new EliteSearch());
   using(var lockedLegacy=File.Open(legacyFile,FileMode.Open,FileAccess.Read,FileShare.None)){
    Call(migrating,"InitializeStore");Call(migrating,"SaveJourney",true);
    Check(Get<bool>(migrating,"settingsLoadPending")&&!File.Exists(migratedFile),"Locked legacy state defers settings migration without saving defaults");
   }
   Check((bool)Call(migrating,"RecoverJourneyStore")!,"Legacy route reloads after releasing lock");
   string blockedWrite=migratedFile+".tmp";
   Directory.CreateDirectory(blockedWrite);
   Get<SavedJourney>(migrating,"CharacterSave").DailyNotice=654;
   Call(migrating,"SaveJourney",true);
   Check(Get<string?>(migrating,"SettingsSaveError")!=null&&!File.Exists(migratedFile),"Simulated settings write failed");
   var retainedRoute=HuntStore.Load(legacyFile);
   Check(retainedRoute.Settings.Mini&&retainedRoute.Settings.MaxGil==321&&retainedRoute.Characters[123].DailyNotice==654,
    "Failed settings write preserves legacy settings while saving route changes");
   var restarted=CreatePlugin();
   Set(restarted,"character",123UL);Set(restarted,"Session",new RouteSession());Set(restarted,"Search",new EliteSearch());
   Call(restarted,"InitializeStore");
   Check(Get<HuntSettings>(restarted,"Settings").MaxGil==321,"Restart restores legacy settings after failed migration");
   Directory.Delete(blockedWrite);
   Call(restarted,"SaveJourney",true);
   using var migratedRoute=System.Text.Json.JsonDocument.Parse(File.ReadAllText(legacyFile));
   Check(!migratedRoute.RootElement.TryGetProperty("Settings",out _),"Legacy settings removed only after successful migration");
   var migrated=HuntSettingsFile.Load(migratedFile,new HuntSettings()).Settings;
   Check(migrated.Mini&&migrated.MaxGil==321&&!Get<bool>(migrating,"settingsLoadPending"),"Recovered migration persists the user's legacy settings");
   Set(copy,"store",new HuntStore());Set(copy,"stateFile",file);Set(copy,"stateLoadPending",true);Call(copy,"LoadJourney");
   using(var locked=File.Open(file,FileMode.Open,FileAccess.Read,FileShare.None)){
    Check(!(bool)Call(copy,"RecoverJourneyStore")!,"Unreadable route store stays pending");
    Call(copy,"SaveJourney",true);
   }
   Check(HuntStore.Load(file).Characters[123].DailyNotice==42,"Saving while read is pending does not replace the file");
   Check((bool)Call(copy,"RecoverJourneyStore")!&&Get<JourneyFlow>(copy,"Flow").Pauses==PauseReason.Restored,"Recovery reloads the saved route on pause");
   Call(copy,"SaveJourney",true);
   var saved=HuntStore.Load(file);
   Check(saved.Characters[123].Deferred.Contains("saved-deferred")&&saved.Characters[123].DailyNotice==42&&saved.Characters[123].Contracts[1].Generation==7&&saved.Characters[124].WeeklyNotice==99,"Recovery preserves both characters, contract identity and notices");
  }finally{foreach(var fileName in Directory.GetFiles(folder))File.Delete(fileName);Directory.Delete(folder);Set(plugin,"stateFile",null);}
  // A marker write failure is a different pause from temporarily unavailable goals.
  flow.Active=true;flow.Pauses=PauseReason.None;territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);counts.Clear();session.Retarget(newOrder);
  int failedWrites=0;Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(_=>{failedWrites++;return false;}));Call(plugin,"AdvanceRoute");Check(failedWrites==3&&flow.Pauses.HasFlag(PauseReason.FlagError),"Marker write failure has bounded retries and an explicit independent pause");
  Call(plugin,"AdvanceRoute");Check(failedWrites==3,"A marker failure is not automatically cleared merely because goals remain eligible");
  Set(plugin,"MarkerWriter",(Func<MarkerStamp,bool>)(stamp=>{marker=stamp;writes++;return true;}));flow.Pauses=PauseReason.Unavailable;Call(plugin,"AdvanceRoute");Check(!flow.Pauses.HasFlag(PauseReason.Unavailable),"Technical goal availability recovers automatically without clearing other pause kinds");
  // Gateway arrival is distinct from entering the actual hunting territory.
  var hinterTarget=game.Excel.GetSubrowSheet<MobHuntOrder>().SelectMany(g=>g).Where(o=>o.Target.RowId!=0).Select(o=>o.Target.Value).First(t=>t.Map.Value.TerritoryType.RowId==399&&Location.Database.ContainsKey(t.Name.RowId));
  var gateway=Daily(hinterTarget.Name.RowId,3,399);Set(plugin,"Mobs",new List<MobHuntEntry>{gateway});Set(plugin,"Routes",new List<ZoneRoute>{new(gateway.TerritoryName!,new(){gateway},null)});queue.Clear();flow.Pauses=PauseReason.None;flow.Manual=null;flow.Active=true;
  Call(plugin,"Track",gateway);var gatewayRequest=queue.Activate(true);
  Check(gatewayRequest?.Owner=="travel","Gateway target outside the territory offers reviewed travel");
  int beforeLocalGatewayPorts=ports;
  territory=399;mapId=gateway.MapId;
  Check(!gatewayRequest!.Valid()&&queue.Resolve(gatewayRequest.Key,true)==null,"Live entry into the target territory invalidates travel before the cached location refresh");
  Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);
  Call(plugin,"OfferPlannedTravel");
  Check(queue.Activate(true)==null&&ports==beforeLocalGatewayPorts&&session.Phase==RoutePhase.Following&&marker?.Territory==399,
   "Already in Hinterlands resumes local flag without teleporting back to Idyllshire");
  session.RequestNext(gateway);Call(Get<NextZoneWindow>(plugin,"NextZone"),"Sync");
  Check(queue.Activate(true)==null,"Next-zone dialog cannot be created for the current territory");
  territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);flow.Manual=null;
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
  objects=[];flow.Pauses=PauseReason.None;flow.Active=false;territory=401;mapId=c.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,elite});Call(plugin,"BuildRoutes",entries.ToDictionary(e=>e.AetheryteId,e=>e.GilCost));
  var calculated=Get<List<ZoneRoute>>(plugin,"Routes").Single();var first=calculated.Mobs.First(m=>!Get<Dictionary<string,string>>(flow,"Unavailable").ContainsKey(JourneyFlow.Key(m)));
  Check(calculated.Start!=null&&(EntryPoint?)Call(plugin,"EntryFor",first)==calculated.Start,"Calculated entry is retained for first target");
  // Force a valid non-nearest entry: transport must consume the reviewed plan.
  var alternatives=(List<EntryPoint>)Call(plugin,"Entries",a.MapId,a.TerritoryType)!;var planned=alternatives.Last();Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){a,b,elite},planned)});uint transported=0;
  Set(plugin,"TeleportTransport",(Func<uint,bool>)(id=>{transported=id;ports++;return true;}));flow.Active=true;session.Retarget(a);Set(plugin,"nextTeleportAllowed",0L);Call(plugin,"Travel",a);
  Check(transported==planned.Id,"Actual teleport consumes planned entry rather than independently selecting nearest anchor");
  Set(plugin,"pendingJourney",null);Set(plugin,"nextTeleportAllowed",0L);flow.Pauses=PauseReason.None;territory=397;mapId=a.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);session.Retarget(a);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,c,elite});counts.Clear();
  var anchor=MapCoordinates.ToWorld(Location.Database[a.MobHuntId].Coordinate,game.Excel.GetSheet<Map>().GetRow(a.MapId));position=new(anchor.X+5000,0,anchor.Y);
  var shortcut=(ShortcutOffer?)Call(plugin,"FindShortcut");Check(shortcut!=null,$"Long-flight regression fixture has a valid shortcut: phase={session.Phase}, pause={flow.Pauses}, ready={Get<bool>(plugin,"Ready")}, settings={store.Settings.Shortcuts}, overhead={Get<ShortcutTiming>(plugin,"Timing").OverheadSeconds}, point={position}, anchor={anchor}, entries={alternatives.Count}");queue.Clear();Call(Get<ShortcutWindow>(plugin,"Shortcut"),"Sync");request=queue.Activate(true);Check(request?.Owner=="shortcut","Shortcut controller queues reviewed proposal");
  shortcutPrice=150;Call(Get<ShortcutWindow>(plugin,"Shortcut"),"Sync");Check(!request!.Valid()&&queue.Resolve(request.Key,true)==null,"Price increase invalidates the active shortcut before acceptance");
  queue.Retry(request);Check(queue.Activate(true)?.Presentation?.Fare==150,"Only the refreshed shortcut can be shown");queue.Clear();shortcutPrice=100;
  var quoted=shortcut??throw new Exception("Missing quote");
  Check(!ShortcutAdvisor.MatchesQuote(quoted,quoted with {Aetheryte=quoted.Aetheryte+1}),"Switching optimal aetheryte invalidates old quote");
  string contextAtDeparture=territory+":"+mapId;
  var sameZone=new PendingJourney(territory,territory,mapId,clock,JourneyFlow.Key(a),false){Loaded=true,Origin=new(position.X,position.Z),ArrivalPoint=anchor,OriginContext=contextAtDeparture};
  Set(plugin,"pendingJourney",sameZone);int beforeUnrelated=writes;
  Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==sameZone&&writes==beforeUnrelated,"Unrelated load at origin does not confirm arrival or write a flag");
  clock+=5000;Call(plugin,"ReconcileTravel");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==null&&flow.Pauses.HasFlag(PauseReason.TravelError),"Unrelated load expires without leaving the route waiting indefinitely");
  Set(plugin,"pendingJourney",sameZone);
  position=new(anchor.X,0,anchor.Y);Set(plugin,"ContextProvider",(Func<string>)(()=>contextAtDeparture+":other-instance"));Call(plugin,"ReconcileTravel");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==null&&flow.Pauses.HasFlag(PauseReason.TravelError),"Wrong instance rejects arrival even near the destination");
  Set(plugin,"ContextProvider",(Func<string>)(()=>territory+":"+mapId));Set(plugin,"pendingJourney",sameZone);Set(plugin,"CurrentMap",mapId+1);Call(plugin,"ReconcileTravel");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==null&&flow.Pauses.HasFlag(PauseReason.TravelError),"Wrong map rejects arrival");
  Set(plugin,"CurrentMap",mapId);Set(plugin,"pendingJourney",sameZone);Call(plugin,"ReconcileTravel");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==null&&!flow.Pauses.HasFlag(PauseReason.TravelError),"Reaching destination on expected map and instance confirms arrival");
  position=new(anchor.X+5000,0,anchor.Y);session.Retarget(a);
  queue.Clear();Call(new ShortcutWindow(plugin),"Sync");var confirmedShortcut=queue.Activate(true);
  int portsBeforeShortcut=ports,writesBeforeShortcut=writes;uint shortcutRun=session.RunId;
  Check(confirmedShortcut?.Owner=="shortcut"&&queue.Resolve(confirmedShortcut.Key,true)?.Accept()==true,"Confirming a fresh shortcut submits the reviewed teleport");
  var actualJourney=Get<PendingJourney?>(plugin,"pendingJourney");
  var expectedEntry=alternatives.Single(entry=>entry.Id==quoted.Aetheryte);
  var expectedLanding=MapCoordinates.ToWorld(expectedEntry.Position,game.Excel.GetSheet<Map>().GetRow(a.MapId));
  Check(actualJourney is {ArrivalPoint: {} landing}&&landing==expectedLanding&&actualJourney.Map==mapId&&ports==portsBeforeShortcut+1&&transported==quoted.Aetheryte,
   "Accepted shortcut creates an arrival stamp for the submitted aetheryte");
  clock+=3500;Call(plugin,"OfferBoard",Plugin.Boards[0]);
  Check(queue.Activate(true)==null&&Get<PendingJourney?>(plugin,"pendingJourney")==actualJourney&&writes==writesBeforeShortcut,"Board travel cannot overwrite pending shortcut after cooldown");
  casting=true;Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");
  Check(writes==writesBeforeShortcut&&session.Phase==RoutePhase.WaitingForZone,"No target flag is written while shortcut is casting");
  loading=true;Call(plugin,"Update",(object?)null);loading=false;
  Check(actualJourney!.Loaded&&writes==writesBeforeShortcut,"Loading is observed without writing a target flag");
  casting=false;clock+=8000;position=new(expectedLanding.X,0,expectedLanding.Y);
  Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");
  Check(Get<PendingJourney?>(plugin,"pendingJourney")==null&&session.Target==a&&session.RunId==shortcutRun&&session.Phase==RoutePhase.Following
   &&writes==writesBeforeShortcut+1&&marker is {} firstFlag&&firstFlag.Matches(new(territory,mapId,anchor.X,anchor.Y)),
   "Arrival writes exactly one flag for the active goal and continues the same route");
  Call(plugin,"AdvanceRoute");Check(writes==writesBeforeShortcut+1&&ports==portsBeforeShortcut+1,"Next tick does not duplicate flag or teleport");
  position=new(anchor.X+5000,0,anchor.Y);
  Set(plugin,"ordinaryActor",(ulong?)777UL);Set(plugin,"ordinaryKind",JourneyFlow.Key(a));
  Set(plugin,"ordinaryPosition",(Vector2?)new Vector2(position.X,position.Z));
  Check(Call(plugin,"FindShortcut")==null,"A live tracked mob at the player cancels the otherwise beneficial static-anchor shortcut");
  Set(plugin,"ordinaryActor",null);Set(plugin,"ordinaryKind",null);Set(plugin,"ordinaryPosition",null);
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
  string recoveryFolder=Path.Combine(Path.GetTempPath(),"HuntRoute-history-replay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(recoveryFolder);
  try{
   string path=Path.Combine(recoveryFolder,"history.json");var now=DateTimeOffset.UtcNow;
   var oldHistory=new CompletionHistory();oldHistory.Observe(456,1,false,now,new(true,false,2,2,true));oldHistory.Save(path);
   Set(plugin,"completionFile",path);Set(plugin,"completionLoadPending",true);Set(plugin,"completionHistory",new CompletionHistory(){RequiresPickupEvidence=true});
   using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None)){
    Call(plugin,"ObserveCompletion",123UL,(byte)1,false,now,new BillState(true,true,1,1,false));
    Call(plugin,"ObserveCompletion",123UL,(byte)1,false,now,new BillState(true,false,1,1,true));
    Call(plugin,"SaveHistory",true);Check(Get<bool>(plugin,"completionLoadPending"),"Unread history remains pending while the file is locked");
   }
   Call(plugin,"SaveHistory",true);var recovered=CompletionHistory.Load(path);
   Check(recovered.IsComplete(123,1,false,now)&&recovered.IsComplete(456,1,false,now),"Recovery replays fresh pickup and completion while preserving another character");
  }finally{Directory.Delete(recoveryFolder,true);Set(plugin,"completionFile",null);}
  var repeatHistory=new CompletionHistory();var currentTime=DateTimeOffset.UtcNow;var lastDaily=currentTime.AddDays(-1);var lastWeekly=currentTime.AddDays(-7);
  var retained=new BillState(true,false,1,1,true);
  repeatHistory.Observe(123,1,false,lastDaily,retained);repeatHistory.Observe(123,5,true,lastWeekly,retained);
  repeatHistory.Observe(123,1,false,currentTime,retained);repeatHistory.Observe(123,5,true,currentTime,retained);
  Set(plugin,"completionHistory",repeatHistory);Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,retained},{5,retained}});
  var repeatBoard=Plugin.Boards[0];
  savedNotice.DailyNotice=0;savedNotice.WeeklyNotice=0;Call(plugin,"CheckResetNotice");
  Check(queue.Activate(true)?.Key.StartsWith("pickup:")==true,"Repeated available IDs trigger the new-contract notification");
  queue.Clear();
  foreach(bool weekly in new[]{false,true}){
   Check((bool)Call(plugin,"CanPickup",repeatBoard,weekly)!,"Previously completed same-ID bill becomes available after its reset");
   var progress=(ContractProgress)Call(plugin,"CollectionProgress",repeatBoard,weekly)!;
   Check(progress.NewSets==1&&progress.Completed==0,"Repeated IDs report a new set without false completion");
   byte bill=weekly?(byte)5:(byte)1;repeatHistory.Observe(123,bill,weekly,currentTime,new(true,true,1,1,false));repeatHistory.Observe(123,bill,weekly,currentTime,retained);
   Check(!(bool)Call(plugin,"CanPickup",repeatBoard,weekly)!&&repeatHistory.IsComplete(123,bill,weekly,currentTime,1),"Fresh pickup and completion supersede retained history");
  }
  Set(plugin,"completionHistory",new CompletionHistory(){RequiresPickupEvidence=true});
  Check(!(bool)Call(plugin,"CanPickup",repeatBoard,false)!,"A retained ID with unknown history cannot prove a repeat");
  // Login proposals are session-scoped, independently of persisted reset acknowledgement.
  queue.Clear();Set(plugin,"loginNoticeShown",false);Set(plugin,"Mobs",new List<MobHuntEntry>());Set(plugin,"completionHistory",new CompletionHistory());Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)}});flow.Active=false;flow.Pauses=PauseReason.None;
  savedNotice.DailyNotice=ResetClock.Period(DateTimeOffset.UtcNow,false).ToUnixTimeSeconds();combat=true;Call(plugin,"CheckResetNotice");
  Check(queue.Activate(true)==null,"Login offer waits until combat ends");combat=false;request=queue.Activate(true);
  Check(request?.Key.StartsWith("login:")==true&&request.Presentation?.ConfirmLabel=="К доскам","Login offers board choice even if current reset was acknowledged earlier");
  request!.OnShown!();queue.Resolve(request.Key,false);Call(plugin,"CheckResetNotice");Check(queue.Activate(true)==null,"Later does not repeat login offer during the same session");
  queue.Clear();Call(plugin,"LoadJourney");flow=Get<JourneyFlow>(plugin,"Flow");Set(plugin,"contractRevision",2L);Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)}});Call(plugin,"CheckResetNotice");request=queue.Activate(true);
  Check(request?.Key.StartsWith("login:")==true,"New login resets session suppression despite same character and daily period");
  queue.Clear();Set(plugin,"Mobs",new List<MobHuntEntry>{a});Call(plugin,"CheckResetNotice");request=queue.Activate(true);
  Check(request?.Presentation?.ConfirmLabel=="Продолжить","Obtained incomplete bills offer continuation instead of another board trip");
  savedNotice=Get<SavedJourney>(plugin,"CharacterSave");
  savedNotice.DailyNotice=0;request!.OnShown!();queue.Resolve(request.Key,false);Call(plugin,"CheckResetNotice");request=queue.Activate(true);
  Check(request?.Key.StartsWith("pickup:")==true&&savedNotice.DailyNotice==0,"Continuation must not acknowledge available contracts");
  queue.Clear();Set(plugin,"loginNoticeShown",false);Set(plugin,"Mobs",new List<MobHuntEntry>());Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,1,1,true)}});Call(plugin,"CheckResetNotice");request=queue.Activate(true);
  Check(request==null,"Completed contracts with no new bills suppress the login popup");
  savedNotice.DailyNotice=0;
  Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)}});
  Call(plugin,"CheckResetNotice");request=queue.Activate(true);
  Check(request?.Key.StartsWith("pickup:")==true,"New contracts can still trigger a reset notice after a quiet login");
  queue.Clear();
  Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,1,1,true)},{5,new(true,false,0,1,false)}});
  var oldBoardHistory=Get<CompletionHistory>(plugin,"History");var boardHistory=new CompletionHistory();
  boardHistory.Observe(123,1,false,DateTimeOffset.UtcNow,new(true,true,1,1,false));
  boardHistory.Observe(123,1,false,DateTimeOffset.UtcNow,new(true,false,1,1,true));
  Set(plugin,"completionHistory",boardHistory);
  var boardUi=new NativeHuntController(plugin);boardUi.Navigate(NativeHuntPage.Boards);
  var coloredBoard=boardUi.Build().Buttons.Single(b=>b.Key=="board:Heavensward").Board!;
  Check(coloredBoard.Daily is {Label:"выполнено",Color:{}}&&coloredBoard.Weekly is {Label:"можно взять",Color:{}}&&
   coloredBoard.Daily.Color!=coloredBoard.Weekly.Color,"Board presents completed daily and available weekly status in separate colors");
  Set(plugin,"completionHistory",oldBoardHistory);
  queue.Clear();Set(plugin,"loginNoticeShown",false);store.Settings.ResetNotifications=false;Call(plugin,"CheckResetNotice");Check(queue.Activate(true)==null,"Explicit notification opt-out remains respected");store.Settings.ResetNotifications=true;
  flow.Active=true;flow.Pauses=PauseReason.None;Call(plugin,"CheckResetNotice");Check(queue.Activate(true)==null,"A started route defers the login popup until paused");flow.Pauses=PauseReason.User;Call(plugin,"CheckResetNotice");request=queue.Activate(true);Check(request!=null,"Deferred login offer becomes available on manual pause");
  queue.Clear();Set(plugin,"character",124UL);Call(plugin,"LoadJourney");Set(plugin,"contractRevision",2L);Set(plugin,"BillStates",new Dictionary<byte,BillState>{{1,new(true,false,0,1,false)}});Call(plugin,"CheckResetNotice");request=queue.Activate(true);Check(request?.Key.StartsWith("login:124:")==true,"Another character has its own login offer");queue.Clear();
  // Reproduce completed HW route -> new daily bills -> Start from Kugane.
  flow=Get<JourneyFlow>(plugin,"Flow");counts=new(){[(1,0)]=(int)a.NeededKills};Set(plugin,"KillCounts",counts);flow.Active=false;flow.Pauses=PauseReason.None;flow.Manual=null;
  session.Start(a,Plugin.Boards.SelectMany(board=>board.Daily.Append(board.Weekly)));session.Tick(new[]{a},_=>true,_=>true,397);
  Check(session.Phase==RoutePhase.Finished,"Kugane regression starts from a finished old route");
  var stormblood=new List<MobHuntEntry>();
  foreach(var bill in new byte[]{6,7,8}){var type=game.Excel.GetSheet<MobHuntOrderType>().GetRow(bill);byte order=bill==6?(byte)2:bill==7?(byte)3:(byte)5;
   foreach(var row in game.Excel.GetSubrowSheet<MobHuntOrder>().GetRow(type.OrderStart.RowId+order-1u).Where(o=>o.Target.RowId!=0&&o.NeededKills>0)){var t=row.Target.Value;stormblood.Add(new(){BillNumber=bill,OrderId=order,Generation=1,MobIndex=(byte)row.SubrowId,MobHuntId=t.Name.RowId,Name=t.Name.Value.Singular.ToString(),MapId=t.Map.RowId,TerritoryType=t.Map.Value.TerritoryType.RowId,TerritoryName=t.Map.Value.PlaceName.Value.Name.ToString(),NeededKills=row.NeededKills});}
  }
  Set(plugin,"Mobs",stormblood);territory=628;mapId=370;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"nextTeleportAllowed",0L);Set(plugin,"pendingJourney",null);objects=[];queue.Clear();Call(plugin,"BuildRoutes",entries.ToDictionary(e=>e.AetheryteId,e=>e.GilCost));
  uint finishedRun=session.RunId;int beforeCityPorts=ports;Call(plugin,"ResumeJourney");Call(plugin,"AdvanceRoute");request=queue.Activate(true);
  Check(request?.Owner=="travel"&&flow.Active,"Starting newly obtained Stormblood bills from Kugane keeps teleport confirmation alive after controller update");
  Check(session.Phase==RoutePhase.AwaitingTravel&&session.Target is {} cityTarget&&cityTarget.BillNumber is 6 or 7 or 8&&session.RunId>finishedRun&&ports==beforeCityPorts,"City start establishes new target/run before confirmation but does not cast teleport");
  uint plannedRun=session.RunId;flow.Pauses=PauseReason.User;Check(!request!.Ready(),"Manual pause holds explicit planned trip without invalidating its identity");flow.Pauses=PauseReason.None;
  queue.Resolve(request.Key,false);for(int i=0;i<3;i++)Call(plugin,"AdvanceRoute");Check(queue.Activate(true)==null&&session.Phase==RoutePhase.AwaitingTravel&&flow.Active,"Later keeps planned route and does not automatically repeat confirmation");
  Call(plugin,"OfferPlannedTravel");request=queue.Activate(true);Check(request?.Owner=="travel"&&session.RunId==plannedRun,"Visible planned-trip action reopens confirmation without creating another route");
  Set(plugin,"TeleportTransport",(Func<uint,bool>)(_=>false));Check(!queue.Resolve(request!.Key,true)!.Accept()&&session.Phase==RoutePhase.AwaitingTravel&&Get<PendingJourney?>(plugin,"pendingJourney")==null,"Rejected game transport stays in confirmation state and cannot report a fake arrival");
  Set(plugin,"TeleportTransport",(Func<uint,bool>)(_=>{ports++;return true;}));Call(plugin,"OfferPlannedTravel");request=queue.Activate(true);Check(request!=null&&queue.Resolve(request.Key,true)!.Accept()&&ports==beforeCityPorts+1&&session.Phase==RoutePhase.WaitingForZone,"Accepted planned trip performs exactly one teleport and then waits for arrival");
  var arrivedGoal=session.Target!;clock+=10000;territory=arrivedGoal.TerritoryType;mapId=arrivedGoal.MapId;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"ownedMarker",null);Call(plugin,"ReconcileTravel");Call(plugin,"AdvanceRoute");
  Check(session.Phase==RoutePhase.Following&&session.RunId==plannedRun&&marker?.Map==mapId,"Arrival from Kugane flags first newly obtained goal and continues without another Start");queue.Clear();
  territory=628;mapId=370;Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"Mobs",new List<MobHuntEntry>{arrivedGoal});counts.Clear();session.PlanTravel(arrivedGoal,Plugin.Boards.SelectMany(board=>board.Daily.Append(board.Weekly)));Call(plugin,"BuildRoutes",entries.ToDictionary(e=>e.AetheryteId,e=>e.GilCost));Call(plugin,"OfferPlannedTravel");counts[(arrivedGoal.BillNumber,arrivedGoal.MobIndex)]=(int)arrivedGoal.NeededKills;int beforeLateCityCredit=ports;Call(plugin,"AdvanceRoute");
  Check(!flow.Active&&session.Phase==RoutePhase.Idle&&ports==beforeLateCityCredit&&queue.Activate(true)==null,"Credit before confirmation retires planned trip without stale popup or transport");
  // Read-only world overlay uses actual runtime bill, object and scene snapshots.
  flow=Get<JourneyFlow>(plugin,"Flow");flow.Active=true;flow.Pauses=PauseReason.Marker|PauseReason.User;counts=new();Set(plugin,"KillCounts",counts);Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,elite});territory=397;mapId=a.MapId;position=Vector3.Zero;
  store.Settings.WorldMarkers=true;store.Settings.OrdinaryWorldMarkers=true;store.Settings.OrdinaryWorldDistance=150;store.Settings.WorldDistance=300;
  objects=[Npc(100,a.MobHuntId,new(20,0,0)),Npc(106,a.MobHuntId,new(1,0,0)),Npc(101,b.MobHuntId,new(10,0,0)),Npc(102,elite.MobHuntId,new(30,0,0)),Npc(103,a.MobHuntId,new(1,0,0),true),Npc(104,999999,new(1,0,0)),Npc(105,a.MobHuntId,new(151,0,0))];
  session.Retarget(a);Set(plugin,"ordinaryActor",(ulong?)100UL);Set(plugin,"ordinaryKind",JourneyFlow.Key(a));int overlayWrites=writes,overlayPorts=ports,overlayOpens=opens;
  WorldHuntMarker[] Overlay()=>(WorldHuntMarker[])Call(plugin,"ReadWorldHuntMarkers")!;
  Check(Overlay().Length==3&&Overlay().Any(m=>m.ActorId==106)&&Overlay().All(m=>m.ActorId!=100&&!m.Current),"Runtime overlay marks the nearest required actors even if a farther ordinary actor is tracked");
  Check(Overlay().Single(m=>m.ActorId==106).Label.Contains("0/1"),"Runtime ordinary label uses authoritative counter");
  combat=true;Check(Overlay().Length==0,"Combat hides ordinary and elite world markers");
  combat=false;Check(Overlay().Length==3,"Leaving combat restores markers for remaining goals");
  counts[(1,0)]=1;Check(Overlay().All(m=>m.ActorId!=100&&m.ActorId!=106),"Actual kill-count completion removes all overlays of completed requirement");counts.Clear();
  store.Settings.WorldMarkers=false;Check(Overlay().Length==2&&Overlay().All(m=>!m.Elite),"Ordinary runtime overlay works when elite overlay disabled");store.Settings.WorldMarkers=true;
  store.Settings.OrdinaryWorldMarkers=false;Check(Overlay().Length==1&&Overlay()[0].Elite,"Ordinary runtime toggle does not hide required elite");store.Settings.OrdinaryWorldMarkers=true;
  flow.Deferred.Add(JourneyFlow.Key(b));Check(Overlay().All(m=>m.ActorId!=101),"Runtime manual deferral removes ordinary labels without counting completion");flow.Deferred.Clear();
  Get<HashSet<byte>>(plugin,"unreadBills").Add(1);Check(Overlay().Length==1&&Overlay()[0].Elite,"Unread ordinary bill hides its labels while valid elite remains visible");Get<HashSet<byte>>(plugin,"unreadBills").Clear();
  mapId=c.MapId;Check(Overlay().Length==0,"Actual game map, not stale cached map, controls overlay eligibility");mapId=a.MapId;
  loading=true;Check(Overlay().Length==0,"World overlay hidden during loading");loading=false;cutscene=true;Check(Overlay().Length==0,"World overlay hidden during cutscenes");cutscene=false;
  flow.Active=false;Check(Overlay().Length==3&&!Overlay().Any(m=>m.Current),"Required mobs remain labelled without starting route, with no stale active-instance emphasis");
  Check(writes==overlayWrites&&ports==overlayPorts&&opens==overlayOpens&&counts.Count==0&&flow.Pauses==(PauseReason.Marker|PauseReason.User),"World overlay cannot write flags, open map, submit teleport, count kills or clear independent pauses");
  // Native gamepad presentation uses the same controller commands as the old UI.
  Set(plugin,"CurrentTerritory",territory);Set(plugin,"CurrentMap",mapId);Set(plugin,"Routes",new List<ZoneRoute>{new(a.TerritoryName!,new(){a,b,elite},null,true)});
  var nativeUi=new NativeHuntController(plugin);int nativePorts=ports,nativeWrites=writes,nativeOpens=opens;uint nativeRun=session.RunId;var nativePauses=flow.Pauses;
  foreach(var page in Enum.GetValues<NativeHuntPage>()){
   nativeUi.Navigate(page);var screen=nativeUi.Build();Check(screen.Buttons.Select(b=>b.Key).Distinct().Count()==screen.Buttons.Count,"Native screen action identities stay unique: "+page);
  }
  Check(ports==nativePorts&&writes==nativeWrites&&opens==nativeOpens&&session.RunId==nativeRun&&flow.Pauses==nativePauses&&counts.Count==0,"Building all native views cannot navigate, cast, clear pauses or alter counters");
  nativeUi.Navigate(NativeHuntPage.Targets);var goalButton=nativeUi.Build().Buttons.First(bu=>bu.Action.Kind=="target");nativeUi.Execute(goalButton.Action);
  Check(nativeUi.Page==NativeHuntPage.Target&&nativeUi.Build().Buttons.Any(bu=>bu.Action.Kind=="track"),"Gamepad row selection opens explicit goal actions without starting a route");
  objects=[];flow.Active=false;flow.Pauses=PauseReason.Board;Set(plugin,"nextTeleportAllowed",0L);nativeUi.Execute(new("resume"));
  Check(flow.Active&&session.Phase==RoutePhase.Following&&nativeUi.Page==NativeHuntPage.Hunt&&ports==nativePorts,"Native Start resumes existing backend in current zone without teleport");
  nativeUi.Execute(new("pause"));Check(flow.Pauses.HasFlag(PauseReason.User),"Native Pause uses independent user-pause state");
  nativeUi.Execute(new("target",JourneyFlow.Key(b)));nativeUi.Execute(new("defer",JourneyFlow.Key(b)));Check(flow.Excluded(b)&&!(bool)Call(plugin,"Done",b)!&&nativeUi.Page==NativeHuntPage.Target,"Native deferral preserves incomplete bill and manual browsing");
  nativeUi.Execute(new("restore",JourneyFlow.Key(b)));Check(!flow.Excluded(b),"Native return action restores deferred goal");
  uint filterRun=session.RunId;nativeUi.Execute(new("filter-expansion","Stormblood"));nativeUi.Navigate(NativeHuntPage.Targets);
  Check(nativeUi.Build().Buttons.All(bu=>bu.Action.Kind!="target")&&session.RunId==filterRun,"Native expansion filter hides only rows and leaves hunt running");
  nativeUi.Execute(new("filter-reset"));Check(nativeUi.Build().Buttons.Any(bu=>bu.Action.Kind=="target"),"Native filter reset restores existing goal list");
  bool activeBeforePauseCheck=flow.Active;var pausesBeforePauseCheck=flow.Pauses;
  flow.Active=true;flow.Pauses=PauseReason.User;nativeUi.Navigate(NativeHuntPage.Actions);
  Check(nativeUi.Build().Buttons.All(bu=>bu.Key!="pause"),"Paused route hides the pause command");
  nativeUi.Execute(new("pause"));
  Check(flow.Pauses.HasFlag(PauseReason.User),"A stale pause command cannot resume hunting");
  flow.Active=activeBeforePauseCheck;flow.Pauses=pausesBeforePauseCheck;
  var settingsBefore=store.Settings.WorldMarkers;nativeUi.Execute(new("toggle","ordinary"));Check(!store.Settings.OrdinaryWorldMarkers&&store.Settings.WorldMarkers==settingsBefore,"Native ordinary overlay switch remains independent from elite overlay");nativeUi.Execute(new("toggle","ordinary"));
  nativeUi.Execute(new("step","ordinary-distance",-10000));Check(store.Settings.OrdinaryWorldDistance==50,"Native distance controls clamp to valid minimum");nativeUi.Execute(new("step","ordinary-distance",10000));Check(store.Settings.OrdinaryWorldDistance==500,"Native distance controls clamp to valid maximum");
  nativeUi.Execute(new("step","gil",-10000));Check(store.Settings.MaxGil==0,"Native gil editor supports free-only shortcuts");
  float secondsBefore=store.Settings.MinSeconds;nativeUi.Execute(new("edit-number","seconds"));nativeUi.Execute(new("step","seconds",1));Check(nativeUi.Page==NativeHuntPage.Number&&Math.Abs(store.Settings.MinSeconds-secondsBefore-.1f)<.001f,"Native numeric page adjusts fractional seconds and stays focused on value");
  Set(plugin,"settingsLoadPending",true);nativeUi.Navigate(NativeHuntPage.Settings);Check(nativeUi.Build().Buttons.Where(bu=>bu.Action.Kind=="toggle").All(bu=>!bu.Enabled),"Native options show unavailable persistence as disabled controls");
  bool preference=store.Settings.OrdinaryWorldMarkers;nativeUi.Execute(new("toggle","ordinary"));Check(store.Settings.OrdinaryWorldMarkers==preference,"Stale native setting command cannot overwrite unread settings");Set(plugin,"settingsLoadPending",false);
  nativeUi.Boards(true);Set(plugin,"BillStates",new Dictionary<byte,BillState>());Check(nativeUi.Build().Buttons.Where(bu=>bu.Action.Kind=="board").All(bu=>!bu.Enabled),"Native new-board picker disables categories with no confirmed availability");
  nativeUi.Boards(false);queue.Clear();nativePorts=ports;nativeUi.Execute(new("board","Stormblood"));request=queue.Activate(true);Check(request?.Owner=="travel"&&ports==nativePorts&&request.Presentation?.Title==game.Excel.GetSheet<TerritoryType>().GetRow(628).PlaceName.Value.Name.ToString(),"Native board button opens correct confirmation and does not cast on selection");queue.Clear();
  nativePorts=ports;nativeWrites=writes;Set(plugin,"Mobs",new List<MobHuntEntry>());nativeUi.Execute(goalButton.Action);nativeUi.Execute(new("track",goalButton.Action.Argument));Check(ports==nativePorts&&writes==nativeWrites,"Stale native goal selection/track command cannot act on a replacement bill");Set(plugin,"Mobs",new List<MobHuntEntry>{a,b,elite});
  var nativeServiceType=typeof(Plugin).Assembly.GetType("HuntRoute.NativeHuntUi")!;var nativeService=Activator.CreateInstance(nativeServiceType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{plugin,bridge},null)!;
  Call(nativeService,"CloseAll");Check(flow.Active,"Closing native main/mini presentation never stops hunting");((IDisposable)nativeService).Dispose();
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
