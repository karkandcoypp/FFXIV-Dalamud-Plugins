using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

public enum RoutePhase { Idle,WaitingForZone,Following,NextZone,Finished,Changed }
public sealed class RouteSession {
    public RoutePhase Phase { get; private set; }
    public MobHuntEntry? Target { get; private set; }
    public MobHuntEntry? Suggested { get; private set; }
    private HashSet<byte> tiers=[];
    private (byte Bill,byte Mob,uint Id)? marked;
    private static (byte,byte,uint) Key(MobHuntEntry m) => (m.BillNumber,m.MobIndex,m.MobHuntId);
    public void Start(MobHuntEntry target,IEnumerable<byte> dailyTiers) {
        tiers=dailyTiers.ToHashSet(); Target=target; Suggested=null; marked=null; Phase=RoutePhase.WaitingForZone;
    }
    public void RetryMarker() => marked=null;
    public void Stop() { Phase=RoutePhase.Idle; Target=null; Suggested=null; marked=null; tiers.Clear(); }
    public MobHuntEntry? Tick(IReadOnlyList<MobHuntEntry> ordered,System.Func<MobHuntEntry,bool> done,System.Func<MobHuntEntry,bool> hasPosition,uint territory) {
        if(Phase is RoutePhase.Idle or RoutePhase.Finished or RoutePhase.Changed || Target==null) return null;
        var active=ordered.FirstOrDefault(m=>Key(m)==Key(Target));
        if(active==null) { Phase=RoutePhase.Changed; Suggested=null; return null; }
        Target=active;
        if(territory!=Target.TerritoryType) { Phase=RoutePhase.WaitingForZone; return null; }
        var candidates=ordered.Where(m=>!m.IsEliteMark && tiers.Contains(m.BillNumber) && hasPosition(m)).ToList();
        if(done(Target)) {
            var next=candidates.FirstOrDefault(m=>m.TerritoryType==Target.TerritoryType && !done(m));
            if(next!=null) { Target=next; marked=null; }
            else {
                var zones=candidates.Select(m=>m.TerritoryType).Distinct().ToList();
                int current=zones.IndexOf(Target.TerritoryType);
                Suggested=Enumerable.Range(1,zones.Count).Select(i=>zones[(current+i+zones.Count)%zones.Count])
                    .Select(z=>candidates.FirstOrDefault(m=>m.TerritoryType==z && !done(m))).FirstOrDefault(m=>m!=null);
                Phase=Suggested==null?RoutePhase.Finished:RoutePhase.NextZone;
                return null;
            }
        }
        Phase=RoutePhase.Following; Suggested=null;
        if(marked==Key(Target)) return null;
        marked=Key(Target); return Target;
    }
}
