using System.Numerics;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Utility;
using HuntRoute;
using Lumina;
using Lumina.Excel.Sheets;
class Program {
 static int checks;
 static void CheckPoint(uint territory,Map map,Vector2 expected,string label) {
  var link=MapCoordinates.CreateLink(territory,map,expected);
  var world=new Vector2(link.RawX/1000f,link.RawY/1000f);
  var result=MapUtil.WorldToMap(world,map);
  checks++;
  if(Vector2.Distance(result,expected)>.002f) throw new Exception($"Map flag mismatch {label}: expected={expected} actual={result}");
 }
 static void Main(string[] args) {
  string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
  AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(home,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home,n.Name+".dll")):null;
  Run(args);
 }
 static void Run(string[] args) {
  using var game=new GameData(args[0]);
  var seen=new HashSet<(uint,uint)>();
  foreach(var group in game.Excel.GetSubrowSheet<MobHuntOrder>()) foreach(var order in group) {
   if(order.Target.RowId==0) continue;
   var target=order.Target.Value;
   if(!Location.Database.TryGetValue(target.Name.RowId,out var location) || !seen.Add((target.Name.RowId,target.Map.RowId))) continue;
   CheckPoint(target.Map.Value.TerritoryType.RowId,target.Map.Value,location.Coordinate,target.Name.Value.Singular.ToString());
  }
  Console.WriteLine($"Daily target map flags checked: {checks}");
  foreach(var board in Plugin.Boards) CheckPoint(board.Territory,game.Excel.GetSheet<TerritoryType>().GetRow(board.Territory).Map.Value,board.Position,board.Name+" board");
  var spawns=JsonSerializer.Deserialize<Dictionary<uint,float[][]>>(File.ReadAllText(args[1]))!;
  foreach(var (territory,points) in spawns) {
   var map=game.Excel.GetSheet<TerritoryType>().GetRow(territory).Map.Value;
   foreach(var p in points) CheckPoint(territory,map,new Vector2(p[0],p[1]),"B-rank spawn");
  }
  // The old bug collapsed these widely separated targets near the centre of the map.
  var sampleMap=game.Excel.GetSheet<TerritoryType>().GetRow(397).Map.Value;
  var p1=MapCoordinates.CreateLink(397,sampleMap,new(15,12));
  var p2=MapCoordinates.CreateLink(397,sampleMap,new(32,24));
  if(Vector2.Distance(new(p1.RawX/1000f,p1.RawY/1000f),new(p2.RawX/1000f,p2.RawY/1000f))<500) throw new Exception("Distinct target flags still collapsed");
  Console.WriteLine($"PASS: {checks} raw map-link round trips across daily targets, hunt boards and B-rank points; distinct-target regression passed.");
 }
}
