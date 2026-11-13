using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace HuntRoute;

internal sealed class ShortcutNavigation {
    internal sealed record Distances(float Direct,IReadOnlyDictionary<uint,float> Entries);
    private const int MaxCandidates=4;
    private const int TimeoutMs=4000;
    private readonly Func<bool> isReady;
    private readonly Func<Vector3,bool,Vector3?> project;
    private readonly Func<Vector3,Vector3,CancellationToken,Task<List<Vector3>>> path;
    private readonly Action<double>? slowStep;
    private readonly List<(Task Task,CancellationTokenSource Token)> retired=[];
    private Task<List<Vector3>>? pending;
    private CancellationTokenSource? cancellation;
    private (uint Id,Vector3 Point,bool Known)[] candidates=[];
    private readonly Dictionary<uint,float> lengths=[];
    private string? context;
    private Vector3 origin,destination,target,start;
    private bool knownHeight,finished;
    private int step;
    private float direct;
    private Distances? result;
    private long started,retryAfter,readyUntil,stepAfter,transitionProbeAfter;

    internal ShortcutNavigation():this(
        ()=>Service.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc(),
        (point,known)=>Service.PluginInterface.GetIpcSubscriber<Vector3,float,float,Vector3?>("vnavmesh.Query.Mesh.NearestPoint").InvokeFunc(point,3,known?4:40),
        (from,to,token)=>Service.PluginInterface.GetIpcSubscriber<Vector3,Vector3,bool,CancellationToken,Task<List<Vector3>>>("vnavmesh.Nav.PathfindCancelable").InvokeFunc(from,to,true,token),
        ms=>Service.PluginLog.Debug("Hunt Route vnavmesh synchronous step: {Milliseconds:F1} ms",ms)) {}

    internal ShortcutNavigation(Func<bool> isReady,Func<Vector3,bool,Vector3?> project,
        Func<Vector3,Vector3,CancellationToken,Task<List<Vector3>>> path,Action<double>? slowStep=null) {
        this.isReady=isReady;this.project=project;this.path=path;this.slowStep=slowStep;
    }

    private bool requireMeshReload;

    internal void Reset(long now=0) {
        Retire();
        context=null;retryAfter=0;readyUntil=0;
        if(now!=0&&!requireMeshReload){transitionProbeAfter=now;requireMeshReload=true;}
    }

    internal void ObserveTransition(long now) {
        if(!requireMeshReload||now<transitionProbeAfter)return;
        transitionProbeAfter=now+100;
        try { if(!isReady())requireMeshReload=false; }
        catch(Exception){requireMeshReload=false;}
    }

    private void Retire() {
        if(cancellation!=null) {
            var cancelled=cancellation.CancelAsync();
            var retirement=pending==null?cancelled:Task.WhenAll(cancelled,pending);
            if(!retirement.IsCompleted)retired.Add((retirement,cancellation));
            else { if(retirement.IsFaulted)_=retirement.Exception;cancellation.Dispose(); }
        }
        cancellation=null;pending=null;result=null;
    }

    internal Distances? Read(string key,Vector3 from,Vector3 to,bool known,
        ShortcutCandidate[] entries,long now,out bool waiting) {
        waiting=false;
        retired.RemoveAll(old=> {
            if(!old.Task.IsCompleted)return false;
            if(old.Task.IsFaulted)_=old.Task.Exception;
            old.Token.Dispose();return true;
        });
        bool same=context==key&&knownHeight==known&&Vector3.DistanceSquared(origin,from)<=900&&Vector3.DistanceSquared(destination,to)<=225;
        if(!same&&context!=null) {
            bool changed=context!=key;
            Retire();context=null;
            if(changed){readyUntil=0;retryAfter=0;}
        }
        if(requireMeshReload){ObserveTransition(now);return null;}
        if(now<retryAfter||retired.Count>=2)return null;
        long timer=Stopwatch.GetTimestamp();
        try {
            if(now>=readyUntil) {
                if(!isReady()) { Retire();context=null;retryAfter=now+1500;return null; }
                readyUntil=now+500;
            }
            if(same&&finished) {
                if(result!=null||now-started<10000)return result;
                same=false;
            }
            if(same&&now-started>=TimeoutMs) {
                Retire();finished=true;return null;
            }
            if(!same) {
                // Keep one slot available for recovery, even if an old provider ignores cancellation.
                context=key;origin=from;destination=to;knownHeight=known;started=now;
                finished=false;step=0;stepAfter=now;lengths.Clear();
                candidates=entries.DistinctBy(e=>e.Id).OrderBy(e=>Vector2.DistanceSquared(e.Position,new(to.X,to.Z)))
                    .ThenBy(e=>e.Id).Take(MaxCandidates)
                    .Select(e=>(e.Id,new Vector3(e.Position.X,e.Height??to.Y,e.Position.Y),e.Height.HasValue)).ToArray();
            }
            if(now<stepAfter){waiting=true;return null;}
            stepAfter=now+1;
            if(step==0) {
                if(project(to,known) is not {} endpoint){finished=true;return null;}
                target=endpoint;step++;
            } else if(pending==null) {
                start=origin;
                if(step>1) {
                    var entry=candidates[step-2];
                    if(project(entry.Point,entry.Known) is not {} point) {
                        return NextCandidate(out waiting);
                    }
                    start=point;
                }
                cancellation=new CancellationTokenSource();
                pending=path(start,target,cancellation.Token);
            } else if(pending.IsCompleted) {
                float? distance=pending.IsCompletedSuccessfully?ShortcutAdvisor.PathLength(start,target,pending.Result):null;
                if(pending.IsFaulted)_=pending.Exception;
                cancellation?.Dispose();cancellation=null;pending=null;
                if(step==1) {
                    if(distance is not {} meters){finished=true;return null;}
                    direct=meters;
                } else if(distance is {} meters) lengths[candidates[step-2].Id]=meters;
                return NextCandidate(out waiting);
            }
            waiting=true;
            return null;
        } catch(Exception) {
            Retire();finished=true;return null;
        } finally {
            double elapsed=Stopwatch.GetElapsedTime(timer).TotalMilliseconds;
            if(elapsed>=2)slowStep?.Invoke(elapsed);
        }
    }

    private Distances? NextCandidate(out bool waiting) {
        step++;
        finished=step>candidates.Length+1;
        waiting=!finished;
        if(finished&&lengths.Count>0)result=new(direct,new Dictionary<uint,float>(lengths));
        return result;
    }
}
