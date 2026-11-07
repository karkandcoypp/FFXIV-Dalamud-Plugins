using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public readonly record struct ShortcutCandidate(uint Id,string Name,Vector2 Position,uint GilCost);
public sealed record ShortcutOffer(uint RunId,byte Bill,byte Mob,uint TargetId,uint Territory,uint Aetheryte,string Name,uint GilCost,float DirectDistance,float FinalDistance);
public static class ShortcutAdvisor {
    public const float MinimumDistance=1200,MinimumSaving=900,MaximumFinalLeg=300;
    public const uint MaximumCost=200;
    public static ShortcutOffer? Evaluate(uint run,MobHuntEntry target,Vector2 player,Vector2 destination,IEnumerable<ShortcutCandidate> entries) {
        float direct=Vector2.Distance(player,destination);
        if(!float.IsFinite(direct)||direct<MinimumDistance) return null;
        var candidates=entries.Where(e=>e.GilCost<=MaximumCost)
            .Select(e=>(Entry:e,Last:Vector2.Distance(e.Position,destination)))
            .Where(e=>float.IsFinite(e.Last)&&e.Last<=MaximumFinalLeg&&direct-e.Last>=MinimumSaving)
            .OrderBy(e=>e.Last).ThenBy(e=>e.Entry.GilCost).ThenBy(e=>e.Entry.Id).ToArray();
        if(candidates.Length==0) return null;
        var chosen=candidates[0];
        return new(run,target.BillNumber,target.MobIndex,target.MobHuntId,target.TerritoryType,
            chosen.Entry.Id,chosen.Entry.Name,chosen.Entry.GilCost,direct,chosen.Last);
    }
}
public sealed class ShortcutNotice {
    private uint run;
    private readonly HashSet<(byte,byte,uint)> announced=[];
    public bool ShouldOpen(ShortcutOffer? offer) {
        if(offer==null) return false;
        if(run!=offer.RunId) { run=offer.RunId;announced.Clear(); }
        return announced.Add((offer.Bill,offer.Mob,offer.TargetId));
    }
}
