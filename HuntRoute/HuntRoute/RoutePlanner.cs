using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public static class RoutePlanner {
    // Daily anchors first, then the known spawn circuit. This estimates flying
    // distance only, never an invented duration for finding an elite.
    public static float Distance<T>(IEnumerable<T> order,Func<T,Vector2[]> points,Vector2? origin) {
        float cost=0;var last=origin;
        foreach(var target in order){var remaining=points(target).Distinct().ToList();
            while(remaining.Count>0){var next=last is {} here?remaining.MinBy(p=>Vector2.DistanceSquared(here,p)):remaining[0];if(last is {} from)cost+=Vector2.Distance(from,next);last=next;remaining.Remove(next);}
        }
        return cost;
    }
    public static (T[] Order,int Entry,float Cost) Zone<T>(IReadOnlyList<T> targets,Func<T,bool> excluded,Func<T,bool> elite,Func<T,Vector2[]> points,Vector2[] origins) {
        T[]? best=null;int entry=-1;float cost=float.PositiveInfinity;
        for(int i=0;i<Math.Max(1,origins.Length);i++){
            var available=targets.Where(t=>!excluded(t)&&points(t).Length>0).ToArray();
            var daily=available.Where(t=>!elite(t)).ToArray();
            var (ordinary,_)=Order(daily,_=>false,t=>points(t).FirstOrDefault(),origins.Length==0?[]:[origins[i]]);
            var ordered=ordinary.Concat(available.Where(elite)).ToArray();
            float candidate=Distance(ordered,points,origins.Length==0?null:origins[i]);
            if(best==null||candidate<cost){best=ordered;entry=origins.Length==0?-1:i;cost=candidate;}
        }
        best??=[];var used=best.ToHashSet();
        return (best.Concat(targets.Where(t=>!used.Contains(t))).ToArray(),entry,cost);
    }
    public static (T[] Order,int Entry) Order<T>(IReadOnlyList<T> targets,Func<T,bool> done,Func<T,Vector2?> position,Vector2[] origins) {
        var remaining=Enumerable.Range(0,targets.Count).Where(i=>!done(targets[i]) && position(targets[i]).HasValue).ToArray();
        var groups=remaining.GroupBy(i=>position(targets[i])!.Value).ToArray();
        var (route,entry)=RouteSolver.Solve(groups.Select(g=>g.Key).ToArray(),origins);
        var ordered=route.SelectMany(i=>groups[i]).ToArray();
        var used=ordered.ToHashSet();
        return (ordered.Concat(Enumerable.Range(0,targets.Count).Where(i=>!used.Contains(i)).OrderBy(i=>done(targets[i])))
            .Select(i=>targets[i]).ToArray(),entry);
    }
}
public enum TravelDecision { Teleport,AlreadyThere,NotReady,Locked,Busy }
public static class TravelPolicy {
    public static TravelDecision Decide(uint current,uint destination,bool ready,bool unlocked,bool busy,bool allowSameZone=false) =>
        !ready || destination==0 ? TravelDecision.NotReady : current==destination && !allowSameZone ? TravelDecision.AlreadyThere
        : !unlocked ? TravelDecision.Locked : busy ? TravelDecision.Busy : TravelDecision.Teleport;
}
