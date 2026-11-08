using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

// Value snapshots only: no game object pointers or changes to hunt state.
public sealed record WorldHuntGoal(string Key,uint NameId,string Name,uint Territory,uint Map,bool Elite,int Kills,uint Needed,bool Known=true,bool Excluded=false);
public readonly record struct WorldHuntActor(ulong Id,uint NameId,Vector3 Position,bool Dead);
public sealed record WorldHuntMarker(ulong ActorId,Vector3 Position,string Name,int Kills,uint Needed,bool Elite,bool Current,float Distance){
 public string Label=>(Current?"Цель · ":"")+(Elite?$"[B] {Name}":$"{Name} · {Kills}/{Needed}")+$" · {Distance:F0} м";
 public uint Color=>Current?0xffffff89:Elite?0xff88ff77:0xffdfc49e;
 public float Radius=>Current?9:Elite?8:5;
 public float Thickness=>Current?3:1.5f;
}
public static class WorldHuntMarkers {
 public static WorldHuntMarker[] Build(IEnumerable<WorldHuntGoal> goals,IReadOnlyList<WorldHuntActor> actors,Vector3 player,uint territory,uint map,HuntSettings settings,string? currentKey=null,ulong? currentActor=null){
  if(territory==0||map==0||!Finite(player)||!settings.WorldMarkers&&!settings.OrdinaryWorldMarkers)return [];
  var needed=goals.Where(g=>g.Known&&!g.Excluded&&g.Territory==territory&&g.Map==map&&g.NameId!=0&&g.Needed>0&&g.Kills>=0&&g.Kills<g.Needed)
   .Where(g=>g.Elite?settings.WorldMarkers:settings.OrdinaryWorldMarkers).GroupBy(g=>g.NameId)
   .ToDictionary(g=>g.Key,g=>g.OrderBy(g=>g.Key==currentKey?0:1).ThenBy(g=>g.Key,StringComparer.Ordinal).First());
  var result=new List<(WorldHuntMarker Marker,string Key)>();var seen=new HashSet<ulong>();
  foreach(var actor in actors){
   if(actor.Id==0||actor.Dead||!Finite(actor.Position)||!needed.TryGetValue(actor.NameId,out var goal))continue;
   float distance=Vector3.Distance(player,actor.Position);
   int limit=Math.Clamp(goal.Elite?settings.WorldDistance:settings.OrdinaryWorldDistance,50,500);
   if(!float.IsFinite(distance)||distance>limit||!seen.Add(actor.Id))continue;
   result.Add((new(actor.Id,actor.Position,goal.Name,goal.Kills,goal.Needed,goal.Elite,false,distance),goal.Key));
  }
  // Follow the same loaded instance as the navigator. Before it has selected an
  // instance, emphasize only the nearest candidate for its current requirement.
  ulong? highlight=currentActor??result.Where(p=>p.Key==currentKey).OrderBy(p=>p.Marker.Distance).ThenBy(p=>p.Marker.ActorId).Select(p=>(ulong?)p.Marker.ActorId).FirstOrDefault();
  return result.Select(p=>p.Marker with {Current=p.Key==currentKey&&p.Marker.ActorId==highlight})
   .OrderByDescending(m=>m.Current).ThenByDescending(m=>m.Elite).ThenBy(m=>m.Distance).ThenBy(m=>m.ActorId).ToArray();
 }
 private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
}
