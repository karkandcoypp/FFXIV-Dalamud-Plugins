using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// Fly-by visits repeat automatically; only contract credit completes the hunt.
public sealed class EliteSearch {
    public const float VisitRadius=45;
    private List<Vector2> all=[],remaining=[];
    private Vector2? current,marked,lastVisited;
    private bool requireDeparture;
    public int Round { get; private set; }
    public Vector2? LockedPosition { get; private set; }
    public string Status => LockedPosition!=null ? "Элита найдена. Метка закреплена до выполнения контракта; при потере видимости сохраняется последняя позиция." : all.Count==0 ? "Нет известных точек. Обнаружение элиты остаётся включённым." : $"Поиск элиты · круг {Round} · осталось точек {remaining.Count+1}. "+(requireDeparture?"Точка уже проверена рядом с вами. Следующий визит засчитается после вылета и возвращения.":"Подлетите к метке — следующая точка выберется автоматически, если цель не обнаружена.");
    public void Start(IEnumerable<Vector2> points) { all=points.Where(Finite).Distinct().ToList();remaining=all.ToList();current=null;marked=null;lastVisited=null;requireDeparture=false;Round=all.Count==0?0:1;LockedPosition=null; }
    private static bool Finite(Vector2 point) => float.IsFinite(point.X)&&float.IsFinite(point.Y);
    public void Retry() => marked=null;
    public void Suspend() => Retry();
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
