using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public static class RoutePlanner {
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
    public static TravelDecision Decide(uint current,uint destination,bool ready,bool unlocked,bool busy) =>
        !ready || destination==0 ? TravelDecision.NotReady : current==destination ? TravelDecision.AlreadyThere
        : !unlocked ? TravelDecision.Locked : busy ? TravelDecision.Busy : TravelDecision.Teleport;
}
