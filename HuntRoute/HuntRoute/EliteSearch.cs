using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// Absence in ObjectTable cannot prove a spawn empty: only explicit confirmation advances it.
public sealed class EliteSearch {
    private List<Vector2> remaining=[];
    private Vector2? current,marked;
    public Vector2? LockedPosition { get; private set; }
    public bool CanSkip { get; private set; }
    public string Status => LockedPosition!=null ? "Элита найдена. Метка закреплена до выполнения контракта; при потере видимости сохраняется последняя позиция." : current==null ? "Все точки проверены. Обнаружение остаётся включённым; можно повторить поиск." : CanSkip ? "Цель пока не обнаружена. Дождитесь прогрузки или подтвердите: здесь пусто." : $"Поиск элиты: осталось точек {remaining.Count+1}. Подойдите к метке и приземлитесь для проверки.";
    public void Start(IEnumerable<Vector2> points) { remaining=points.Where(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)).Distinct().ToList();current=null;marked=null;LockedPosition=null;CanSkip=false; }
    public void Retry() => marked=null;
    public void Suspend() { CanSkip=false;Retry(); }
    public bool Skip() { if(!CanSkip || LockedPosition!=null) return false;current=null;marked=null;CanSkip=false;return true; }
    public Vector2? Tick(Vector2 player,Vector2? detected,bool busy) {
        CanSkip=false;
        if(detected is {} found && float.IsFinite(found.X) && float.IsFinite(found.Y)) LockedPosition=found;
        if(LockedPosition is {} locked) return Mark(locked);
        if(current==null && remaining.Count>0) { current=remaining.MinBy(p=>Vector2.DistanceSquared(player,p));remaining.Remove(current.Value); }
        if(current is not {} point) return null;
        CanSkip=!busy && Vector2.Distance(player,point)<=45;
        return Mark(point);
    }
    private Vector2? Mark(Vector2 point) { if(marked is {} old && Vector2.DistanceSquared(old,point)<4) return null;marked=point;return point; }
}
