using System.Numerics;
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
  uint currentTerritory=397; bool busy=false; int errors=0; MapLinkPayload? captured=null;
  Service.DataManager=new SheetServices(game);
  Service.ClientState=Proxy<IClientState>((m,_)=>m.Name switch {"get_IsLoggedIn"=>true,"get_TerritoryType"=>currentTerritory,_=>throw new Exception(m.Name)});
  Service.Condition=Proxy<ICondition>((_,_)=>busy);
  Service.ObjectTable=Proxy<IObjectTable>((m,_)=>m.Name=="get_LocalPlayer"?null:throw new Exception(m.Name));
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?Enumerable.Empty<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>().GetEnumerator():throw new Exception(m.Name));
  Service.Chat=Proxy<IChatGui>((_,_)=>{ errors++; return null; });
  var teleport=typeof(Plugin).GetMethod("Teleport",BindingFlags.Instance|BindingFlags.NonPublic)!;
  if(!(bool)teleport.Invoke(plugin,new object[]{71u})! || errors!=0) throw new Exception("Actual same-zone teleport wrapper did not bypass the native teleport"); checks++;
  busy=true;
  if(!(bool)teleport.Invoke(plugin,new object[]{71u})! || errors!=0) throw new Exception("Same-zone request failed while busy"); checks++;
  currentTerritory=401;
  if((bool)teleport.Invoke(plugin,new object[]{71u})! || errors!=1) throw new Exception("Locked out-of-zone teleport was not rejected"); checks++;
  var unlocked=Proxy<Dalamud.Game.ClientState.Aetherytes.IAetheryteEntry>((m,_)=>m.Name=="get_AetheryteId"?71u:throw new Exception(m.Name));
  Service.Aetherytes=Proxy<IAetheryteList>((m,_)=>m.Name=="GetEnumerator"?new[]{unlocked}.AsEnumerable().GetEnumerator():throw new Exception(m.Name));
  if((bool)teleport.Invoke(plugin,new object[]{71u})! || errors!=2) throw new Exception("Busy unlocked teleport was not rejected before native call"); checks++;
  typeof(Plugin).GetField("Ready",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,false);
  if((bool)teleport.Invoke(plugin,new object[]{71u})! || errors!=3) throw new Exception("Unready teleport was not rejected"); checks++;
  Service.GameGui=Proxy<IGameGui>((m,a)=> { if(m.Name!="OpenMapWithMapLink") throw new Exception(m.Name); captured=(MapLinkPayload)a![0]!; return m.ReturnType==typeof(bool)?true:null; });
  var highland=game.Excel.GetSheet<TerritoryType>().GetRow(397).Map.Value;
  Location.CreateMapMarker(397,highland.RowId,3481,"Archaeornis");
  if(captured==null || Vector2.Distance(MapUtil.WorldToMap(new(captured.RawX/1000f,captured.RawY/1000f),highland),Location.Database[3481].Coordinate)>.002f) throw new Exception("Actual map marker wrapper uses wrong coordinates"); checks++;
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
