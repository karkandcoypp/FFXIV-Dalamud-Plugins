using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// Coordinates are world X/Z, distances in yalms. No movement or teleport is automated.
public sealed class EliteSearch {
    private List<Vector2> remaining=[];
    private Vector2? current,marked;
    private long? arrived;
    private bool visible;
    public string Status => visible ? "Элита обнаружена — метка на цели." : current==null ? "Все точки проверены. Можно повторить поиск: цель могла появиться снова." : $"Поиск элиты: осталось точек {remaining.Count+1}. Осмотрите место; метка сменится через 8 секунд рядом с точкой.";
    public void Start(IEnumerable<Vector2> points) { remaining=points.Where(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)).Distinct().ToList();current=null;marked=null;arrived=null;visible=false; }
    public void Retry() => marked=null;
    public void Skip() { if(visible) return;current=null;arrived=null;marked=null; }
    public Vector2? Tick(Vector2 player,Vector2? detected,long now,bool busy) {
        if(detected is {} found) { visible=true;arrived=null;return Mark(found); }
        if(visible) { visible=false;arrived=null;marked=null; }
        if(current==null && remaining.Count>0) { current=remaining.MinBy(p=>Vector2.DistanceSquared(player,p));remaining.Remove(current.Value); }
        if(current is not {} point) return null;
        if(busy || Vector2.Distance(player,point)>45) arrived=null;
        else { arrived??=now;if(now-arrived.Value>=8000) { Skip();return Tick(player,null,now,true); } }
        return Mark(point);
    }
    private Vector2? Mark(Vector2 point) { if(marked is {} old && Vector2.DistanceSquared(old,point)<4) return null;marked=point;return point; }
}
