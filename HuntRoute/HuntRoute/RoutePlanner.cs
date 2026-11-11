using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public static class RoutePlanner {
    public const float GilPerSecond=10;
    public static IOrderedEnumerable<T> RankZones<T>(IEnumerable<T> zones,Func<T,bool> local,Func<T,uint> fare,Func<T,float> distance,Func<T,uint> id) =>
        zones.OrderByDescending(local).ThenBy(fare).ThenBy(distance).ThenBy(id);

    public readonly record struct Stop(uint Zone,uint Region,uint Entry,uint Fare,float FlightDistance);
    private sealed record Path(int[] Order,ulong Visited,double Score);

    // Compare complete paths with a bounded search. Prices beyond the first hop
    // are estimates; 10 gil/second balances travel time against teleport spend.
    public static int[] PlanJourney(IReadOnlyList<Stop> stops,uint zone,uint region,float overhead,Func<uint,uint,uint?> observed) {
        const int beamWidth=48;
        if(stops.Count==0)return [];
        if(stops.Count>64)return RankZones(Enumerable.Range(0,stops.Count),_=>false,i=>stops[i].Fare,i=>stops[i].FlightDistance,i=>stops[i].Zone).ToArray();
        var beam=new List<Path>{new([],0,0)};
        for(int depth=0;depth<stops.Count;depth++){
            var next=new List<Path>();
            foreach(var path in beam){
                var last=depth==0?new Stop(zone,region,0,0,0):stops[path.Order[^1]];
                bool SameRegion(Stop to)=>last.Region!=0&&to.Region==last.Region;
                bool grouped=Enumerable.Range(0,stops.Count).Any(i=>(path.Visited&(1UL<<i))==0&&SameRegion(stops[i]));
                for(int i=0;i<stops.Count;i++){
                    ulong bit=1UL<<i;var to=stops[i];
                    if((path.Visited&bit)!=0||grouped&&!SameRegion(to))continue;
                    uint fare=depth==0?to.Fare:observed(last.Zone,to.Entry)??(SameRegion(to)?200u:850u);
                    double seconds=overhead+to.FlightDistance/ShortcutAdvisor.FlightSpeed;
                    double regionChange=last.Region!=0&&to.Region!=0&&!SameRegion(to)?200:0;
                    next.Add(new([..path.Order,i],path.Visited|bit,path.Score+fare+GilPerSecond*seconds+regionChange+seconds/(depth+1)));
                }
            }
            beam=next.OrderBy(p=>p.Score).DistinctBy(p=>(p.Visited,p.Order[^1])).Take(beamWidth).ToList();
        }
        return beam[0].Order;
    }

    // Daily anchors first, then the known spawn circuit. This estimates flying
    // distance only, never an invented duration for finding an elite.
    public static float Distance<T>(IEnumerable<T> order,Func<T,Vector2[]> points,Vector2? origin) {
        float cost=0;var last=origin;
        foreach(var target in order){var remaining=points(target).Distinct().ToList();
            while(remaining.Count>0){var next=last is {} here?remaining.MinBy(p=>Vector2.DistanceSquared(here,p)):remaining[0];if(last is {} from)cost+=Vector2.Distance(from,next);last=next;remaining.Remove(next);}
        }
        return cost;
    }
    public static (T[] Order,int Entry,float Cost) Zone<T>(IReadOnlyList<T> targets,Func<T,bool> excluded,Func<T,bool> elite,Func<T,Vector2[]> points,Vector2[] origins,IReadOnlyList<uint>? fares=null,float gilPerSecond=0) {
        if(fares!=null&&fares.Count!=origins.Length)throw new ArgumentException("Each origin needs a fare",nameof(fares));
        uint bestFare=uint.MaxValue;
        T[]? best=null;int entry=-1;float cost=float.PositiveInfinity;
        for(int i=0;i<Math.Max(1,origins.Length);i++){
            var available=targets.Where(t=>!excluded(t)&&points(t).Length>0).ToArray();
            var daily=available.Where(t=>!elite(t)).ToArray();
            var (ordinary,_)=Order(daily,_=>false,t=>points(t).FirstOrDefault(),origins.Length==0?[]:[origins[i]]);
            var ordered=ordinary.Concat(available.Where(elite)).ToArray();
            float candidate=Distance(ordered,points,origins.Length==0?null:origins[i]);
            uint fare=fares is {Count:>0}?fares[i]:0;
            double score=fare+gilPerSecond*candidate/ShortcutAdvisor.FlightSpeed;
            double bestScore=bestFare+gilPerSecond*cost/ShortcutAdvisor.FlightSpeed;
            if(best==null||score<bestScore||score==bestScore&&candidate<cost){best=ordered;entry=origins.Length==0?-1:i;cost=candidate;bestFare=fare;}
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
