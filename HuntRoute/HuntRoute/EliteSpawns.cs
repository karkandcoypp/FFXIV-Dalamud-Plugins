using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
namespace HuntRoute;

public sealed record EliteSpawnSet(string Name,string Zone,uint Territory,uint Map,float[][] Points);
public static class EliteSpawns {
    public static readonly IReadOnlyDictionary<uint,EliteSpawnSet> Sets=Load();
    private static Dictionary<uint,EliteSpawnSet> Load() {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("HuntRoute.elite-spawns.json")!;
        return JsonSerializer.Deserialize<Dictionary<uint,EliteSpawnSet>>(stream)!;
    }
    public static Vector2[] For(MobHuntEntry target) {
        if(!target.IsEliteMark || !Sets.TryGetValue(target.MobHuntId,out var entry) || entry.Territory!=target.TerritoryType || entry.Map!=target.MapId) return [];
        var points=new Vector2[entry.Points.Length];
        for(int i=0;i<points.Length;i++) {
            var p=entry.Points[i];
            if(p.Length!=2 || !float.IsFinite(p[0]) || !float.IsFinite(p[1])) return [];
            points[i]=new(p[0],p[1]);
        }
        return points;
    }
}
