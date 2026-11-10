using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public readonly record struct ShortcutCandidate(uint Id,string Name,Vector2 Position,uint GilCost);
public sealed record ShortcutOffer(uint RunId,byte Bill,byte Mob,uint TargetId,uint Territory,uint Aetheryte,string Name,uint GilCost,float DirectDistance,float FinalDistance,float DirectSeconds,float TeleportSeconds) {
    public byte OrderId {get;init;}
    public uint Generation {get;init;}
    public string ContractKey=>$"{Bill}:{OrderId}:{Generation}:{Mob}:{TargetId}";
    public float SavedSeconds => DirectSeconds-TeleportSeconds;
}
public static class ShortcutAdvisor {
    // Estimates use horizontal flight distance; terrain and altitude are not available here.
    public const float FlightSpeed=20,MinimumFlightSeconds=30,MinimumSavedSeconds=10,MinimumSavedFraction=.20f;
    public const uint MaximumCost=200;
    public static bool MatchesQuote(ShortcutOffer offered,ShortcutOffer current) =>
        current.RunId==offered.RunId && current.ContractKey==offered.ContractKey && current.Territory==offered.Territory
        && current.Aetheryte==offered.Aetheryte && current.GilCost<=offered.GilCost;
    public static ShortcutOffer? Evaluate(uint run,MobHuntEntry target,Vector2 player,Vector2 destination,IEnumerable<ShortcutCandidate> entries,float teleportOverheadSeconds=18,uint maximumCost=MaximumCost,float minimumSeconds=MinimumSavedSeconds,float minimumFraction=MinimumSavedFraction) {
        float direct=Vector2.Distance(player,destination);
        float directSeconds=direct/FlightSpeed;
        if(!float.IsFinite(directSeconds)||directSeconds<MinimumFlightSeconds || !float.IsFinite(teleportOverheadSeconds) || teleportOverheadSeconds<0) return null;
        var candidates=entries.Where(e=>e.GilCost<=maximumCost)
            .Select(e=>(Entry:e,Last:Vector2.Distance(e.Position,destination)))
            .Where(e=>float.IsFinite(e.Last) && directSeconds-(teleportOverheadSeconds+e.Last/FlightSpeed)>=Math.Max(minimumSeconds,directSeconds*minimumFraction))
            .OrderBy(e=>e.Last).ThenBy(e=>e.Entry.GilCost).ThenBy(e=>e.Entry.Id).ToArray();
        if(candidates.Length==0) return null;
        var chosen=candidates[0];
        return new(run,target.BillNumber,target.MobIndex,target.MobHuntId,target.TerritoryType,
            chosen.Entry.Id,chosen.Entry.Name,chosen.Entry.GilCost,direct,chosen.Last,directSeconds,teleportOverheadSeconds+chosen.Last/FlightSpeed){OrderId=target.OrderId,Generation=target.Generation};
    }
}
public sealed class ShortcutNotice {
    private uint run;
    private readonly HashSet<string> announced=[];
    public bool ShouldOpen(ShortcutOffer? offer) {
        if(offer==null) return false;
        if(run!=offer.RunId) { run=offer.RunId;announced.Clear(); }
        return announced.Add(offer.ContractKey);
    }
}

// Time from our accepted teleport request until the player is ready after loading.
// Add a small allowance for mounting again; ignore cancelled requests and unrelated zoning.
public sealed class ShortcutTiming {
    private long? started;
    private bool loaded;
    public float OverheadSeconds { get; private set; }=18;
    public void Begin(long now) { started=now;loaded=false; }
    public void Loading(long now) {
        if(started is not {} start) return;
        if(now<start || (!loaded && now-start>30000)) { Reset();return; }
        loaded=true;
    }
    public void Ready(long now) {
        if(started is not {} start) return;
        if(now-start>120000 || now<start) { Reset();return; }
        if(!loaded) return;
        float seconds=(now-start)/1000f;
        if(seconds>=3 && seconds<=120) OverheadSeconds=seconds+3;
        Reset();
    }
    public void Reset() { started=null;loaded=false; }
}
