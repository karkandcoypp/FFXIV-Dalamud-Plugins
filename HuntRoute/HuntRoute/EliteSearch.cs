using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;
// Streaming is observed conservatively; absence is never treated as contract completion.
public sealed class EliteSearch {
    public const long InspectionMilliseconds=5000;
    public const long MaxObservationGap=1500;
    public const int MinimumObservations=4;
    private List<Vector2> remaining=[];
    private Vector2? current,marked;
    private Vector3? lastPlayer;
    private long? firstObservation,lastObservation;
    private int observations;
    public Vector2? LockedPosition { get; private set; }
    public string Status => LockedPosition!=null ? "Элита найдена. Метка закреплена до выполнения контракта; при потере видимости сохраняется последняя позиция." : current==null ? "Все точки проверены. Обнаружение остаётся включённым; можно повторить поиск." : firstObservation!=null ? "Проверка прогрузки: метка сменится автоматически после стабильного наблюдения без цели." : $"Поиск элиты: осталось точек {remaining.Count+1}. Приблизьтесь к метке и приземлитесь для автоматической проверки.";
    public void Start(IEnumerable<Vector2> points) { remaining=points.Where(p=>Finite(p)).Distinct().ToList();current=null;marked=null;LockedPosition=null;ResetObservation(); }
    private static bool Finite(Vector2 point) => float.IsFinite(point.X)&&float.IsFinite(point.Y);
    private void ResetObservation() { firstObservation=null;lastObservation=null;lastPlayer=null;observations=0; }
    public void Retry() { marked=null;ResetObservation(); }
    public void Suspend() => Retry();
    public Vector2? Tick(Vector2 player,Vector2? detected,bool busy,long now,float height=0) {
        if(detected is {} found && Finite(found)) LockedPosition=found;
        if(LockedPosition is {} locked) { ResetObservation();return Mark(locked); }
        SelectNearest(player);
        if(current is not {} point) return null;
        if(busy || !Finite(player) || !float.IsFinite(height) || Vector2.Distance(player,point)>45) ResetObservation();
        else {
            var position=new Vector3(player.X,height,player.Y);
            // A loading gap, clock rollback or fast approach cannot count as stable observation.
            if(lastObservation is {} previous && (now<=previous || now-previous>MaxObservationGap || lastPlayer is {} old && Vector3.Distance(old,position)>6)) ResetObservation();
            firstObservation??=now;lastObservation=now;lastPlayer=position;observations++;
            if(now-firstObservation.Value>=InspectionMilliseconds && observations>=MinimumObservations) {
                current=null;marked=null;ResetObservation();SelectNearest(player);
            }
        }
        return current is {} next ? Mark(next) : null;
    }
    private void SelectNearest(Vector2 player) {
        if(current!=null || remaining.Count==0 || !Finite(player)) return;
        current=remaining.MinBy(p=>Vector2.DistanceSquared(player,p));remaining.Remove(current.Value);
    }
    private Vector2? Mark(Vector2 point) { if(marked is {} old && Vector2.DistanceSquared(old,point)<4) return null;marked=point;return point; }
}
