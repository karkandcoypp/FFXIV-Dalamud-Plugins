using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// Fly-by visits repeat automatically; only contract credit completes the hunt.
public readonly record struct EliteActor(ulong Id,Vector2 Position,bool Dead);
public sealed record EliteProgress(Vector2[] All,Vector2[] Remaining,Vector2? Current,Vector2? LastVisited,int Round,bool RequireDeparture);
public sealed class EliteSearch {
    public const float VisitRadius=45;
    private List<Vector2> all=[],remaining=[];
    private Vector2? current,marked,lastVisited;
    private bool requireDeparture;
    private ulong? actor;
    private readonly HashSet<ulong> deadActors=[];
    private long? deathRevision,lastObservation;
    private long deathElapsed;
    public bool Lost { get; private set; }
    public bool AwaitingCredit => deathRevision!=null;
    public ulong? ActorId => actor;
    public int Round { get; private set; }
    public Vector2? LockedPosition { get; private set; }
    public string Status => AwaitingCredit ? "Цель погибла — проверяем зачёт контракта." : Lost ? "Цель временно не видна. Сохранена последняя известная позиция." : LockedPosition!=null ? "Элита найдена. Метка закреплена до выполнения контракта; при потере видимости сохраняется последняя позиция." : all.Count==0 ? "Нет известных точек. Обнаружение элиты остаётся включённым." : $"Поиск элиты · круг {Round} · осталось точек {remaining.Count+1}. "+(requireDeparture?"Точка уже проверена рядом с вами. Следующий визит засчитается после вылета и возвращения.":"Подлетите к метке — следующая точка выберется автоматически, если цель не обнаружена.");
    public void Start(IEnumerable<Vector2> points) { all=points.Where(Finite).Distinct().ToList();remaining=all.ToList();current=null;marked=null;lastVisited=null;requireDeparture=false;Round=all.Count==0?0:1;LockedPosition=null;actor=null;Lost=false;deadActors.Clear();deathRevision=null;lastObservation=null;deathElapsed=0; }
    public EliteProgress Capture()=>new(all.ToArray(),remaining.ToArray(),current,lastVisited,Round,requireDeparture);
    public void Restore(EliteProgress progress) {
        if(progress.All==null || progress.Remaining==null || !all.SequenceEqual(progress.All) || progress.Round<1 || progress.Remaining.Any(p=>!all.Contains(p)) || progress.Current is {} point && !all.Contains(point))return;
        remaining=progress.Remaining.Distinct().ToList();current=progress.Current;lastVisited=progress.LastVisited;Round=progress.Round;requireDeparture=progress.RequireDeparture;marked=null;
    }
    private static bool Finite(Vector2 point) => float.IsFinite(point.X)&&float.IsFinite(point.Y);
    public void Retry() => marked=null;
    public void Suspend() { Retry();lastObservation=null; }
    public bool ReleaseLost() {
        if(!Lost || AwaitingCredit) return false;
        Unlock();return true;
    }
    private void Unlock() {
        LockedPosition=null;actor=null;Lost=false;deathRevision=null;deathElapsed=0;lastObservation=null;marked=null;
        if(current is {} point) { remaining.Add(point);current=null; }
    }
    public Vector2? Observe(Vector2 player,IReadOnlyList<EliteActor> actors,bool busy,long now,bool completed,long revision,long snapshotAt,bool ready=true) {
        if(completed) { Start([]);return null; }
        if(!ready) { Suspend();return null; }
        long delta=lastObservation is {} previous && now>=previous && now-previous<=1500 ? now-previous : 0;
        lastObservation=now;
        EliteActor? original=actor is {} id ? actors.Where(a=>a.Id==id).Cast<EliteActor?>().FirstOrDefault() : null;
        if(original is {Dead:false} live && Finite(live.Position)) {
            deathRevision=null;deathElapsed=0;Lost=false;
            return Tick(player,live.Position,busy);
        }
        if(original is {Dead:true} && deathRevision==null) { deathRevision=revision;deathElapsed=0;delta=0; }
        var replacement=actors.Where(a=>!a.Dead && a.Id!=0 && !deadActors.Contains(a.Id) && Finite(a.Position))
            .OrderBy(a=>Vector2.DistanceSquared(player,a.Position)).ThenBy(a=>a.Id).Cast<EliteActor?>().FirstOrDefault();
        if(replacement is {} next) {
            if(actor is {} previousActor && deathRevision!=null) deadActors.Add(previousActor);
            actor=next.Id;Lost=false;deathRevision=null;deathElapsed=0;
            return Tick(player,next.Position,busy);
        }
        if(deathRevision is {} atDeath) {
            if(now>=snapshotAt && now-snapshotAt<=1500) deathElapsed+=delta;
            if(deathElapsed>=3000 && revision>=atDeath+2 && now>=snapshotAt && now-snapshotAt<=1500) {
                if(actor is {} dead) deadActors.Add(dead);
                Unlock();return Tick(player,null,busy);
            }
        }
        Lost=LockedPosition!=null && deathRevision==null;
        return Tick(player,null,busy);
    }
    public Vector2? Tick(Vector2 player,Vector2? detected,bool busy) {
        // Detection wins even on the round boundary or when a late NPC appears at an old point.
        if(detected is {} found && Finite(found)) LockedPosition=found;
        if(LockedPosition is {} locked) return Mark(locked);
        SelectNearest(player);
        if(current is {} point && Finite(player)) {
            float distance=Vector2.Distance(player,point);
            if(distance>VisitRadius) requireDeparture=false;
            if(!busy && !requireDeparture && distance<=VisitRadius) {
                lastVisited=point;current=null;SelectNearest(player);
            }
        }
        return current is {} next ? Mark(next) : null;
    }
    private void SelectNearest(Vector2 player) {
        if(current!=null || all.Count==0 || !Finite(player)) return;
        bool restart=remaining.Count==0;
        if(restart) { remaining=all.ToList();Round++; }
        // Avoid routing straight back to the point just visited when other spawns exist.
        var candidates=restart && remaining.Count>1 ? remaining.Where(p=>p!=lastVisited) : remaining;
        current=candidates.MinBy(p=>Vector2.DistanceSquared(player,p));remaining.Remove(current.Value);
        requireDeparture=restart && Vector2.Distance(player,current.Value)<=VisitRadius;
    }
    private Vector2? Mark(Vector2 point) { if(marked is {} old && Vector2.DistanceSquared(old,point)<4) return null;marked=point;return point; }
}
