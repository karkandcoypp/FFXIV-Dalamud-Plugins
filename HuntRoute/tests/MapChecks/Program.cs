using System.Numerics;
using Action=System.Action;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Utility;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
class Program {
 static int checks; static int links;
 static void CheckPoint(uint territory,Map map,Vector2 expected,string label) {
  var link=MapCoordinates.CreateLink(territory,map,expected);
  var world=new Vector2(link.RawX/1000f,link.RawY/1000f);
  var result=MapUtil.WorldToMap(world,map);
  checks++; links++;
  if(Vector2.Distance(result,expected)>.002f) throw new Exception($"Map flag mismatch {label}: expected={expected} actual={result}");
 }
 static void Main(string[] args) {
  string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
  AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(home,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home,n.Name+".dll")):null;
  Run(args);
 }
 static void Run(string[] args) {
  using var game=new GameData(args[0]);
  var seen=new HashSet<(uint,uint)>(); var maps=new HashSet<uint>(); var icons=new HashSet<uint>();
  foreach(var group in game.Excel.GetSubrowSheet<MobHuntOrder>()) foreach(var order in group) {
   if(order.Target.RowId==0) continue;
   var target=order.Target.Value; if(target.Icon!=0) icons.Add(target.Icon);
   if(!Location.Database.TryGetValue(target.Name.RowId,out var location) || !seen.Add((target.Name.RowId,target.Map.RowId))) continue;
   maps.Add(target.Map.RowId); CheckPoint(target.Map.Value.TerritoryType.RowId,target.Map.Value,location.Coordinate,target.Name.Value.Singular.ToString());
  }
  Console.WriteLine($"Daily target map flags checked: {checks}");
  foreach(var board in Plugin.Boards) CheckPoint(board.Territory,game.Excel.GetSheet<TerritoryType>().GetRow(board.Territory).Map.Value,board.Position,board.Name+" board");
  var spawns=JsonSerializer.Deserialize<Dictionary<uint,float[][]>>(File.ReadAllText(args[1]))!;
  foreach(var (territory,points) in spawns) {
   var map=game.Excel.GetSheet<TerritoryType>().GetRow(territory).Map.Value; maps.Add(map.RowId);
   foreach(var p in points) CheckPoint(territory,map,new Vector2(p[0],p[1]),"B-rank spawn");
  }
  foreach(uint icon in icons) {
   string directory=$"ui/icon/{icon/1000*1000:D6}/{icon:D6}";
   if(!game.FileExists(directory+".tex") && !game.FileExists(directory+"_hr1.tex")) throw new Exception($"Missing target portrait {icon}");
   checks++;
  }
  foreach(var board in Plugin.Boards) {
   var a=game.Excel.GetSheet<Aetheryte>().GetRow(board.Aetheryte);
   var map=game.Excel.GetSheet<TerritoryType>().GetRow(board.Territory).Map.Value;
   if(!a.IsAetheryte || a.Territory.RowId!=board.Territory || map.RowId!=board.Map) throw new Exception($"Board destination mismatch {board.Name}");
   checks++;
  }
  // Independent world-object fixtures: validate each city AND the actual board, not a nearby NPC.
  var boardObjects=new[]{("Foundation",5892509u,2005909u),("Kugane",6928982u,2008655u),("The Crystarium",7959481u,2010340u),("Old Sharlayan",8950392u,2012236u),("Tuliyollal",10577299u,2014155u)};
  for(int i=0;i<Plugin.Boards.Length;i++) {
   var b=Plugin.Boards[i];var (city,levelId,objectId)=boardObjects[i];
   var level=game.Excel.GetSheet<Level>().GetRow(levelId);var a=game.Excel.GetSheet<Aetheryte>().GetRow(b.Aetheryte);
   if(level.Object.RowId!=objectId || level.Territory.RowId!=b.Territory || level.Map.RowId!=b.Map || a.PlaceName.Value.Name.ToString()!=city) throw new Exception($"Wrong board object/city: {b.Name}");checks++;
   var point=MapUtil.WorldToMap(new Vector2(level.X,level.Z),level.Map.Value);
   if(Vector2.Distance(point,b.Position)>.001f)throw new Exception($"Board marker misses actual object: {b.Name}: expected {point}, actual {b.Position}");checks++;
   foreach(byte tier in b.Daily.Append(b.Weekly)) {
    var type=game.Excel.GetSheet<MobHuntOrderType>().GetRow(tier);
    if(type.Type!=(tier==b.Weekly?2:1))throw new Exception($"Wrong daily/weekly tier: {b.Name}:{tier}");checks++;
    for(uint order=0;order<type.OrderAmount;order++)foreach(var o in game.Excel.GetSubrowSheet<MobHuntOrder>().GetRow(type.OrderStart.RowId+order)) {
     if(o.Target.RowId==0)continue;var target=o.Target.Value;
     if(target.Map.Value.TerritoryType.Value.ExVersion.RowId!=(uint)i+1)throw new Exception($"Bill belongs to wrong expansion: {b.Name}");checks++;
     if(tier!=b.Weekly && !Location.Database.ContainsKey(target.Name.RowId))throw new Exception($"Missing daily target coordinates: {target.Name.RowId}");checks++;
    }
   }
  }
  foreach(uint mapId in maps) {
   var map=game.Excel.GetSheet<Map>().GetRow(mapId);
   foreach(var pixel in new[]{Vector2.Zero,new Vector2(512,1548),new Vector2(1024,1024),new Vector2(2048,2048)}) {
    var restored=MapCoordinates.ToTexturePixel(MapCoordinates.FromTexturePixel(pixel,map),map);
    if(Vector2.Distance(restored,pixel)>.01f) throw new Exception($"Map pixel round trip failed for {mapId}"); checks++;
   }
   if(game.Excel.GetSubrowSheet<MapMarker>().TryGetRow(map.MapMarkerRange,out var markers)) foreach(var marker in markers.Where(m=>m.DataType==3)) {
    if(!game.Excel.GetSheet<Aetheryte>().TryGetRow(marker.DataKey.RowId,out var a)||!a.IsAetheryte||a.Territory.RowId!=map.TerritoryType.RowId) continue;
    var coordinate=MapCoordinates.FromTexturePixel(new(marker.X,marker.Y),map);
    CheckPoint(map.TerritoryType.RowId,map,coordinate,a.PlaceName.Value.Name.ToString());
   }
  }
  foreach(var t in game.Excel.GetSheet<MobHuntOrderType>().Where(t=>t.RowId<22)) {
   for(uint id=1;id<=t.OrderAmount;id++) {
    if(!game.Excel.GetSubrowSheet<MobHuntOrder>().TryGetRow(t.OrderStart.RowId+id-1,out var rows) || !rows.Any()) throw new Exception($"Invalid bill range {t.RowId}:{id}");
    if(rows.Any(r=>r.SubrowId>=5)) throw new Exception($"Kill-count index outside bounds {t.RowId}:{id}"); checks++;
   }
  }
  var cloud=game.Excel.GetSheet<TerritoryType>().GetRow(401).Map.Value;
  if(Vector2.Distance(MapCoordinates.FromTexturePixel(new(442,1548),cloud),new(10.3f,33.6f))>.03f) throw new Exception("Aetheryte projection differs from Camp Cloudtop landmark");
  try { MapCoordinates.CreateLink(397,cloud,new(10,10)); throw new Exception("Wrong-zone map accepted"); } catch(ArgumentException) { checks++; }
  try { MapCoordinates.ToWorld(new(float.NaN,1),cloud); throw new Exception("NaN map point accepted"); } catch(ArgumentOutOfRangeException) { checks++; }
  Console.WriteLine($"Portrait files validated: {icons.Count}; map projections audited: {maps.Count}");
  // The old bug collapsed these widely separated targets near the centre of the map.
  var sampleMap=game.Excel.GetSheet<TerritoryType>().GetRow(397).Map.Value;
  var p1=MapCoordinates.CreateLink(397,sampleMap,new(15,12));
  var p2=MapCoordinates.CreateLink(397,sampleMap,new(32,24));
  if(Vector2.Distance(new(p1.RawX/1000f,p1.RawY/1000f),new(p2.RawX/1000f,p2.RawY/1000f))<500) throw new Exception("Distinct target flags still collapsed");
  // Exercise the actual teleport wrapper without any bound native game functions.
  var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
  typeof(Plugin).GetField("Ready",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,true);
  Service.DataManager=new SheetServices(game);
  var entryMethod=typeof(Plugin).GetMethod("Entries",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var allAetherytes=game.Excel.GetSheet<Aetheryte>().Where(a=>a.IsAetheryte).Select(a=>a.RowId).ToArray();
  var unlockedEntries=allAetherytes.Select(id=>Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name=="get_AetheryteId"?id:throw new Exception(m.Name))).ToArray();
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?unlockedEntries.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  foreach(uint mapId in maps) {
   var row=game.Excel.GetSheet<Map>().GetRow(mapId);
   var entries=(List<EntryPoint>)entryMethod.Invoke(plugin,new object[]{mapId,row.TerritoryType.RowId})!;
   if(entries.Select(e=>e.Id).Distinct().Count()!=entries.Count)throw new Exception($"Duplicate aetheryte: {mapId}");checks++;
   foreach(var entry in entries) {
    if(game.Excel.GetSheet<Aetheryte>().GetRow(entry.Id).Territory.RowId!=row.TerritoryType.RowId)throw new Exception("Cross-zone aetheryte leaked");checks++;
    var marker=game.Excel.GetSubrowSheet<MapMarker>().GetRow(row.MapMarkerRange).First(m=>m.DataType==3 && m.DataKey.RowId==entry.Id);
    var expectedWorld=(new Vector2(marker.X,marker.Y)-new Vector2(1024))/(row.SizeFactor/100f)-new Vector2(row.OffsetX,row.OffsetY);
    if(Vector2.Distance(MapCoordinates.ToWorld(entry.Position,row),expectedWorld)>.01f)throw new Exception("Entry point world coordinate mismatch");checks++;
   }
   if(((List<EntryPoint>)entryMethod.Invoke(plugin,new object[]{mapId,uint.MaxValue})!).Count!=0)throw new Exception("Mismatched map/territory accepted");checks++;
  }
  // Exercise the production route builder for first/middle/last bill sets of every expansion.
  typeof(Plugin).GetField("KillCounts",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,new Dictionary<(byte,byte),int>());
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name=="get_LocalPlayer"?null:throw new Exception(m.Name));
  var build=typeof(Plugin).GetMethod("BuildRoutes",BindingFlags.Instance|BindingFlags.NonPublic)!;
  foreach(var board in Plugin.Boards)foreach(int sample in new[]{0,1,2}) {
   var mobs=new List<MobHuntEntry>();
   foreach(byte tier in board.Daily) {
    var type=game.Excel.GetSheet<MobHuntOrderType>().GetRow(tier);uint offset=sample==0?0:sample==1?(uint)type.OrderAmount/2:(uint)type.OrderAmount-1;
    foreach(var o in game.Excel.GetSubrowSheet<MobHuntOrder>().GetRow(type.OrderStart.RowId+offset)) {
     var t=o.Target.Value;mobs.Add(new(){BillNumber=tier,MobIndex=(byte)o.SubrowId,MobHuntId=t.Name.RowId,MapId=t.Map.RowId,TerritoryType=t.Map.Value.TerritoryType.RowId,NeededKills=o.NeededKills});
    }
   }
   typeof(Plugin).GetField("Mobs",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,mobs);build.Invoke(plugin,null);
   var routes=(List<ZoneRoute>)typeof(Plugin).GetField("Routes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;
   if(routes.Sum(r=>r.Mobs.Count)!=mobs.Count)throw new Exception("Production route lost targets");checks++;
   foreach(var route in routes) {
    var first=route.Mobs[0];var entries=(List<EntryPoint>)entryMethod.Invoke(plugin,new object[]{first.MapId,first.TerritoryType})!;
    if(route.Start is {} entry && !entries.Any(e=>e.Id==entry.Id) && !(first.TerritoryType==399 && entry.Id==75))throw new Exception("Invalid route entry point");checks++;
    if(entries.Count>0 && route.Start==null)throw new Exception("Route ignored available unlocked aetherytes");checks++;
   }
  }
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?Array.Empty<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>().AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  foreach(uint mapId in maps) {
   var row=game.Excel.GetSheet<Map>().GetRow(mapId);
   if(((List<EntryPoint>)entryMethod.Invoke(plugin,new object[]{mapId,row.TerritoryType.RowId})!).Count!=0)throw new Exception("Locked aetheryte was offered");checks++;
  }
  uint currentTerritory=397; bool busy=false; int errors=0; MapLinkPayload? captured=null;
  Service.DataManager=new SheetServices(game);
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name switch {"get_IsLoggedIn"=>true,"get_TerritoryType"=>currentTerritory,_=>throw new Exception(m.Name)});
  Service.Condition=Proxy<ICondition>((_,_)=>busy);
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name=="get_LocalPlayer"?null:throw new Exception(m.Name));
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?Enumerable.Empty<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>().GetEnumerator():throw new Exception(m.Name));
  Service.Chat=Proxy<IChatGui>((_,_)=>{ errors++; return null; });
  var teleport=typeof(Plugin).GetMethod("Teleport",BindingFlags.Instance|BindingFlags.NonPublic)!;
  if(!(bool)teleport.Invoke(plugin,new object[]{71u,false})! || errors!=0) throw new Exception("Actual same-zone teleport wrapper did not bypass the native teleport"); checks++;
  busy=true;
  if(!(bool)teleport.Invoke(plugin,new object[]{71u,false})! || errors!=0) throw new Exception("Same-zone request failed while busy"); checks++;
  currentTerritory=401;
  if((bool)teleport.Invoke(plugin,new object[]{71u,false})! || errors!=1) throw new Exception("Locked out-of-zone teleport was not rejected"); checks++;
  var unlocked=Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name=="get_AetheryteId"?71u:throw new Exception(m.Name));
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?new[]{unlocked}.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  if((bool)teleport.Invoke(plugin,new object[]{71u,false})! || errors!=2) throw new Exception("Busy unlocked teleport was not rejected before native call"); checks++;
  typeof(Plugin).GetField("Ready",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,false);
  if((bool)teleport.Invoke(plugin,new object[]{71u,false})! || errors!=3) throw new Exception("Unready teleport was not rejected"); checks++;
  Service.GameGui=Proxy<IGameGui>((m,a)=> { if(m.Name!="OpenMapWithMapLink") throw new Exception(m.Name); captured=(MapLinkPayload)a![0]!; return m.ReturnType==typeof(bool)?true:null; });
  var highland=game.Excel.GetSheet<TerritoryType>().GetRow(397).Map.Value;
  Location.CreateMapMarker(397,highland.RowId,3481,"Archaeornis");
  if(captured==null || Vector2.Distance(MapUtil.WorldToMap(new(captured.RawX/1000f,captured.RawY/1000f),highland),Location.Database[3481].Coordinate)>.002f) throw new Exception("Actual map marker wrapper uses wrong coordinates"); checks++;
  // The real update path must retain a pending next-zone target while the local actor is unavailable.
  var session=new RouteSession();var arrivalTarget=new MobHuntEntry{BillNumber=1,MobIndex=0,MobHuntId=3481,TerritoryType=397,NeededKills=1};
  session.Start(arrivalTarget,new byte[]{1});
  session.Tick(new[]{arrivalTarget},_=>false,_=>true,397);uint loadingRun=session.RunId;
  if(session.Phase!=RoutePhase.Following) throw new Exception("Expected active following before intra-zone loading");checks++;
  typeof(Plugin).GetField("Session",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,session);
  Service.Condition=Proxy<ICondition>((_,_)=>true);
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name=="get_IsLoggedIn"?false:throw new Exception(m.Name));
  var update=typeof(Plugin).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)!;
  update.Invoke(plugin,new object?[]{null});
  if(session.Target!=arrivalTarget || session.Phase!=RoutePhase.WaitingForZone) throw new Exception("Route lost on active loading frame");checks++;
  Service.Condition=Proxy<ICondition>((_,_)=>false);
  update.Invoke(plugin,new object?[]{null});
  if(session.Target!=arrivalTarget || session.Phase!=RoutePhase.WaitingForZone) throw new Exception("Route lost when actor/logged-in state temporarily disappeared");checks++;
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name=="get_IsLoggedIn"?true:throw new Exception(m.Name));
  Service.Player=Proxy<IPlayerState>((m,_)=>m.Name=="get_IsLoaded"?false:throw new Exception(m.Name));
  update.Invoke(plugin,new object?[]{null});
  if(session.Target!=arrivalTarget) throw new Exception("Route lost when PlayerState was not loaded");checks++;
  if(session.RunId!=loadingRun || session.Tick(new[]{arrivalTarget},_=>false,_=>true,397)!=arrivalTarget || session.Phase!=RoutePhase.Following) throw new Exception("Actual update loading cycle did not reflag target without Start");checks++;
  var addon=game.Excel.GetSheet<Addon>();
  if(addon.GetRow(576).Text.ToString()!="Yes" || addon.GetRow(577).Text.ToString()!="No") throw new Exception("Native button label IDs are incorrect");checks++;
  // Exercise the real native-dialog response bridge without allocating an addon or casting a teleport.
  bool validAnswer=true;int nativeAccepted=0,scheduled=0;Action? deferred=null;
  Service.Framework=Proxy<IFramework>((m,args)=> { if(m.Name!="RunOnTick") throw new Exception(m.Name);scheduled++;deferred=(Action)args![0]!;return Task.CompletedTask; });
  var dialogType=typeof(Plugin).Assembly.GetType("HuntRoute.NativeDialogService")!;
  var bridge=RuntimeHelpers.GetUninitializedObject(dialogType);var confirmationQueue=new ConfirmationQueue();
  dialogType.GetField("queue",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(bridge,confirmationQueue);
  var nativeRequest=new ConfirmationRequest("next","bridge-stale","Continue?",()=>validAnswer,()=>true,()=>{nativeAccepted++;return true;});
  confirmationQueue.Offer(nativeRequest);confirmationQueue.Activate(true);
  var answer=dialogType.GetMethod("Answered",BindingFlags.Instance|BindingFlags.NonPublic)!;
  answer.Invoke(bridge,new object[]{nativeRequest.Key,true});
  if(scheduled!=1||nativeAccepted!=0||deferred==null) throw new Exception("Native Yes must defer until after the UI callback unwinds");checks++;
  validAnswer=false;deferred();if(nativeAccepted!=0) throw new Exception("Changed route executed stale Yes");checks++;
  validAnswer=true;nativeRequest=nativeRequest with {Key="bridge-current"};confirmationQueue.Offer(nativeRequest);confirmationQueue.Activate(true);
  answer.Invoke(bridge,new object[]{nativeRequest.Key,true});answer.Invoke(bridge,new object[]{nativeRequest.Key,true});
  if(scheduled!=2) throw new Exception("Duplicate native response queued duplicate teleport");
  deferred!();if(nativeAccepted!=1) throw new Exception("Native Yes did not execute exactly once");checks++;
  // Real shortcut controller: combat must defer the offer, not consume and cancel it.
  var gnomeTarget=game.Excel.GetSubrowSheet<MobHuntOrder>().SelectMany(g=>g).Where(o=>o.Target.RowId!=0).Select(o=>o.Target.Value).First(t=>t.Name.Value.Singular.ToString()=="gnome");
  var gm=gnomeTarget.Map.Value;var playerWorld=MapCoordinates.ToWorld(new(23.4f,31.5f),gm);
  var playerProxy=Proxy<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>((m,_)=>m.Name switch {"get_Position"=>new Vector3(playerWorld.X,0,playerWorld.Y),"get_IsCasting"=>false,_=>throw new Exception(m.Name)});
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name=="get_LocalPlayer"?playerProxy:throw new Exception(m.Name));
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name switch {"get_IsLoggedIn"=>true,"get_TerritoryType"=>gm.TerritoryType.RowId,"get_MapId"=>gm.RowId,_=>throw new Exception(m.Name)});
  var priced=allAetherytes.Select(id=>Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name switch {"get_AetheryteId"=>id,"get_GilCost"=>100u,_=>throw new Exception(m.Name)})).ToArray();
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?priced.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  var gnome=new MobHuntEntry{BillNumber=10,MobIndex=0,MobHuntId=gnomeTarget.Name.RowId,MapId=gm.RowId,TerritoryType=gm.TerritoryType.RowId,NeededKills=2};
  var shortcutSession=new RouteSession();shortcutSession.Start(gnome,new byte[]{10});shortcutSession.Tick(new[]{gnome},_=>false,_=>true,gm.TerritoryType.RowId);
  typeof(Plugin).GetField("Session",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,shortcutSession);
  typeof(Plugin).GetField("Ready",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,true);
  typeof(Plugin).GetField("NativeDialogs",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,bridge);
  typeof(Plugin).GetField("KillCounts",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,new Dictionary<(byte,byte),int>());
  bool shortcutBusy=true;Service.Condition=Proxy<ICondition>((_,_)=>shortcutBusy);
  var controller=new ShortcutWindow(plugin);var sync=typeof(ShortcutWindow).GetMethod("Sync",BindingFlags.Instance|BindingFlags.NonPublic)!;
  sync.Invoke(controller,null);
  if(confirmationQueue.Activate(true)!=null)throw new Exception("Busy shortcut displayed prematurely");checks++;
  sync.Invoke(controller,null);shortcutBusy=false;sync.Invoke(controller,null);
  var queuedShortcut=confirmationQueue.Activate(true);
  if(queuedShortcut?.Owner!="shortcut")throw new Exception("Busy frame consumed useful shortcut forever");checks++;
  if(confirmationQueue.Resolve(queuedShortcut.Key,false)!=null)throw new Exception("No approved a shortcut");checks++;
  sync.Invoke(controller,null);if(confirmationQueue.Activate(true)!=null)throw new Exception("Declined shortcut repeated");checks++;
  Console.WriteLine($"PASS: {checks} map/data/portrait checks including {links} raw map-link round trips; actual teleport and map-marker wrapper regressions passed.");
 }
 static T Proxy<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class {
  var proxy=DispatchProxy.Create<T,ServiceProxy>();((ServiceProxy)(object)proxy).Handler=handler;return proxy;
 }
}
public class ServiceProxy:DispatchProxy {
 public Func<MethodInfo,object?[]?,object?> Handler=null!;
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
}

public sealed class SheetServices(GameData game):IDataManager {
 public Dalamud.Game.ClientLanguage Language=>Dalamud.Game.ClientLanguage.English;
 public GameData GameData=>game;
 public Lumina.Excel.ExcelModule Excel=>game.Excel;
 public bool HasModifiedGameDataFiles=>false;
 public Lumina.Excel.ExcelSheet<T> GetExcelSheet<T>(Dalamud.Game.ClientLanguage? language=null,string? name=null) where T:struct,Lumina.Excel.IExcelRow<T> =>game.Excel.GetSheet<T>();
 public Lumina.Excel.SubrowExcelSheet<T> GetSubrowExcelSheet<T>(Dalamud.Game.ClientLanguage? language=null,string? name=null) where T:struct,Lumina.Excel.IExcelSubrow<T> =>game.Excel.GetSubrowSheet<T>();
 public Lumina.Data.FileResource? GetFile(string path)=>throw new NotSupportedException();
 T IDataManager.GetFile<T>(string path)=>throw new NotSupportedException();
 Task<T> IDataManager.GetFileAsync<T>(string path,CancellationToken cancellationToken)=>throw new NotSupportedException();
 public bool FileExists(string path)=>game.FileExists(path);
}
