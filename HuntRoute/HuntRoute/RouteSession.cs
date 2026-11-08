using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

public enum RoutePhase { Idle,WaitingForZone,Following,NextZone,Finished,Changed }
public sealed class RouteSession {
    public RoutePhase Phase { get; private set; }
    public MobHuntEntry? Target { get; private set; }
    public MobHuntEntry? Suggested { get; private set; }
    public uint RunId { get; private set; }
    private int missingArrivalSnapshots;
    private List<uint> zoneOrder=[];
    private HashSet<byte> tiers=[];
    private (byte Bill,byte Order,uint Generation,byte Mob,uint Id)? marked;
    private static (byte,byte,uint,byte,uint) Key(MobHuntEntry m) => (m.BillNumber,m.OrderId,m.Generation,m.MobIndex,m.MobHuntId);
    public void Start(MobHuntEntry target,IEnumerable<byte> dailyTiers) {
        RunId++; missingArrivalSnapshots=0; zoneOrder.Clear(); tiers=dailyTiers.ToHashSet(); Target=target; Suggested=null; marked=null; Phase=RoutePhase.WaitingForZone;
    }
    public void Continue(MobHuntEntry target) { Target=target;Suggested=null;marked=null;missingArrivalSnapshots=0;Phase=RoutePhase.WaitingForZone; }
    public void Retarget(MobHuntEntry target) { Target=target;Suggested=null;marked=null;missingArrivalSnapshots=0;Phase=RoutePhase.Following; }
    public void RequestNext(MobHuntEntry next){Suggested=next;Phase=RoutePhase.NextZone;marked=null;}
    public void Suggest(MobHuntEntry? target) { if(Phase==RoutePhase.NextZone) { Suggested=target;if(target==null)Phase=RoutePhase.Finished; } }
    public void ZoneTransition() {
        if(Phase is RoutePhase.Following or RoutePhase.WaitingForZone) {
            Phase=RoutePhase.WaitingForZone; missingArrivalSnapshots=0; marked=null;
        }
    }
    public void RetryMarker() => marked=null;
    public void Stop() { Phase=RoutePhase.Idle; Target=null; Suggested=null; marked=null; missingArrivalSnapshots=0; tiers.Clear(); zoneOrder.Clear(); }
    public MobHuntEntry? Tick(IReadOnlyList<MobHuntEntry> ordered,System.Func<MobHuntEntry,bool> done,System.Func<MobHuntEntry,bool> hasPosition,uint territory) {
        if(Phase is RoutePhase.Idle or RoutePhase.Finished or RoutePhase.Changed || Target==null) return null;
        // Do not interpret transient pre-arrival bill data as a discarded contract.
        if(territory!=Target.TerritoryType) { Phase=RoutePhase.WaitingForZone; missingArrivalSnapshots=0; return null; }
        var active=ordered.FirstOrDefault(m=>Key(m)==Key(Target));
        if(active==null && !done(Target)) {
            if(Phase==RoutePhase.WaitingForZone && ++missingArrivalSnapshots<=3) return null;
            Phase=RoutePhase.Changed; Suggested=null; return null;
        }
        missingArrivalSnapshots=0;
        if(active!=null) Target=active;
        var candidates=ordered.Where(m=>tiers.Contains(m.BillNumber) && hasPosition(m)).OrderBy(m=>m.IsEliteMark).ToList();
        foreach(uint zone in candidates.Select(m=>m.TerritoryType).Distinct()) if(!zoneOrder.Contains(zone)) zoneOrder.Add(zone);
        if(!zoneOrder.Contains(Target.TerritoryType)) {
            int at=zoneOrder.FindIndex(z=>z>Target.TerritoryType);
            if(at<0) zoneOrder.Add(Target.TerritoryType); else zoneOrder.Insert(at,Target.TerritoryType);
        }
        if(done(Target)) {
            var next=candidates.FirstOrDefault(m=>m.TerritoryType==Target.TerritoryType && !done(m));
            if(next!=null) { Target=next; marked=null; }
            else {
                int current=zoneOrder.IndexOf(Target.TerritoryType);
                Suggested=Enumerable.Range(1,zoneOrder.Count).Select(i=>zoneOrder[(current+i)%zoneOrder.Count])
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

public sealed class NextZoneNotice {
    private (uint Run,uint From,uint To)? announced;
    public bool ShouldOpen(RouteSession session) {
        if(session.Phase!=RoutePhase.NextZone || session.Target==null || session.Suggested==null) return false;
        var key=(session.RunId,session.Target.TerritoryType,session.Suggested.TerritoryType);
        if(announced==key) return false;
        announced=key; return true;
    }
}
