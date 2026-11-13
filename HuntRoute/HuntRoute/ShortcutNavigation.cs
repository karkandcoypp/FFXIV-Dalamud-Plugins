using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace HuntRoute;

internal sealed class ShortcutNavigation {
    internal sealed record Distances(float Direct,IReadOnlyDictionary<uint,float> Entries);
    private Task<Distances?>? pending;
    private string? context;
    private Vector3 origin;
    private Vector2 destination;
    private long started;

    internal Distances? Read(string key,Vector3 from,Vector2 to,ShortcutCandidate[] entries,long now,out bool waiting) {
        waiting=false;
        bool same=context==key&&Vector3.DistanceSquared(origin,from)<=900&&Vector2.DistanceSquared(destination,to)<=225;
        if(pending is {IsCompleted:false}) {
            waiting=true;
            return null;
        }
        if(same&&pending!=null) {
            var cached=pending.GetAwaiter().GetResult();
            if(cached!=null||now-started<10000)return cached;
        }
        if(pending!=null&&now-started<1500)return null;
        context=key;origin=from;destination=to;started=now;
        pending=Calculate(from,to,entries);
        waiting=!pending.IsCompleted;
        return waiting?null:pending.GetAwaiter().GetResult();
    }

    private static async Task<Distances?> Calculate(Vector3 from,Vector2 to,ShortcutCandidate[] entries) {
        try {
            var ipc=Service.PluginInterface;
            if(!ipc.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc())return null;
            var floor=ipc.GetIpcSubscriber<Vector3,bool,float,Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
            var path=ipc.GetIpcSubscriber<Vector3,Vector3,bool,CancellationToken,Task<List<Vector3>>>("vnavmesh.Nav.PathfindCancelable");
            Vector3? Floor(Vector2 point)=>floor.InvokeFunc(new(point.X,10000,point.Y),false,3);
            if(Floor(to) is not {} target)return null;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var starts=new List<(uint Id,Vector3 Position)>();
            foreach(var entry in entries) {
                if(Floor(entry.Position) is not {} start)return null;
                starts.Add((entry.Id,start));
            }
            var jobs=starts.Select(s=>(s.Id,From:s.Position,Path:path.InvokeFunc(s.Position,target,true,timeout.Token))).ToArray();
            var direct=path.InvokeFunc(from,target,true,timeout.Token);
            await Task.WhenAll(jobs.Select(j=>j.Path).Append(direct)).ConfigureAwait(false);
            if(ShortcutAdvisor.PathLength(from,target,direct.Result) is not {} length)return null;
            var distances=new Dictionary<uint,float>();
            foreach(var job in jobs){
                if(ShortcutAdvisor.PathLength(job.From,target,job.Path.Result) is not {} distance)return null;
                distances.Add(job.Id,distance);
            }
            return new(length,distances);
        }catch(Exception){return null;}
    }
}
