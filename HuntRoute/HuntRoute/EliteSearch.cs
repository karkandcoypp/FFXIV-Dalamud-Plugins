using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// A fly-by visit advances an undetected candidate. Only contract credit completes the hunt.
public sealed class EliteSearch {
    public const float VisitRadius=45;
    private List<Vector2> remaining=[];
    private Vector2? current,marked;
    public Vector2? LockedPosition { get; private set; }
    public string Status => LockedPosition!=null ? "Элита найдена. Метка закреплена до выполнения контракта; при потере видимости сохраняется последняя позиция." : current==null ? "Все точки проверены. Обнаружение остаётся включённым; можно повторить поиск." : $"Поиск элиты: осталось точек {remaining.Count+1}. Подлетите к метке — следующая точка выберется автоматически, если цель не обнаружена.";
    public void Start(IEnumerable<Vector2> points) { remaining=points.Where(Finite).Distinct().ToList();current=null;marked=null;LockedPosition=null; }
    private static bool Finite(Vector2 point) => float.IsFinite(point.X)&&float.IsFinite(point.Y);
    public void Retry() => marked=null;
    public void Suspend() => Retry();
    public Vector2? Tick(Vector2 player,Vector2? detected,bool busy) {
        // Detection wins before proximity, including on the exact tick a spawn would be skipped.
        if(detected is {} found && Finite(found)) LockedPosition=found;
        if(LockedPosition is {} locked) return Mark(locked);
        SelectNearest(player);
        if(current is {} point && !busy && Finite(player) && Vector2.Distance(player,point)<=VisitRadius) {
            current=null;marked=null;SelectNearest(player);
        }
        return current is {} next ? Mark(next) : null;
    }
    private void SelectNearest(Vector2 player) {
        if(current!=null || remaining.Count==0 || !Finite(player)) return;
        current=remaining.MinBy(p=>Vector2.DistanceSquared(player,p));remaining.Remove(current.Value);
    }
    private Vector2? Mark(Vector2 point) { if(marked is {} old && Vector2.DistanceSquared(old,point)<4) return null;marked=point;return point; }
}
