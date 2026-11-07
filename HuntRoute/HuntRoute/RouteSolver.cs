using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace HuntRoute;

public static class RouteSolver {
    // Held-Karp: shortest open route, including travel from one of the entry points.
    public static (int[] Order, int Entry) Solve(Vector2[] points, Vector2[] entries) {
        int n = points.Length;
        if (n == 0) return ([], -1);
        if(points.Concat(entries).Any(p=>!float.IsFinite(p.X)||!float.IsFinite(p.Y))) throw new ArgumentException("Non-finite route coordinates");
        if(n>18) {
            var pending=Enumerable.Range(0,n).ToList();
            int first=entries.Length==0?0:pending.MinBy(i=>entries.Min(e=>Vector2.Distance(e,points[i])));
            int entry=entries.Length==0?-1:Enumerable.Range(0,entries.Length).MinBy(i=>Vector2.Distance(entries[i],points[first]));
            var route=new List<int>{first}; pending.Remove(first);
            while(pending.Count>0) { int next=pending.MinBy(i=>Vector2.Distance(points[route[^1]],points[i])); route.Add(next); pending.Remove(next); }
            return (route.ToArray(),entry);
        }
        int states = 1 << n;
        var costs = new float[states * n]; Array.Fill(costs, float.PositiveInfinity);
        var previous = new int[costs.Length]; Array.Fill(previous, -1);
        var starts = new int[n];
        for (int i = 0; i < n; i++) {
            starts[i] = entries.Length == 0 ? -1 : Enumerable.Range(0, entries.Length).MinBy(e => Vector2.Distance(entries[e], points[i]));
            costs[(1 << i) * n + i] = starts[i] < 0 ? 0 : Vector2.Distance(entries[starts[i]], points[i]);
        }
        for (int mask = 1; mask < states; mask++) for (int end = 0; end < n; end++) {
            float cost = costs[mask * n + end]; if (!float.IsFinite(cost)) continue;
            for (int next = 0; next < n; next++) {
                if ((mask & (1 << next)) != 0) continue;
                int index = (mask | (1 << next)) * n + next;
                float candidate = cost + Vector2.Distance(points[end], points[next]);
                if (candidate < costs[index]) { costs[index] = candidate; previous[index] = end; }
            }
        }
        int final = Enumerable.Range(0, n).MinBy(i => costs[(states - 1) * n + i]);
        var order = new int[n]; int remaining = states - 1;
        for (int slot = n - 1; slot >= 0; slot--) {
            order[slot] = final; int parent = previous[remaining * n + final]; remaining ^= 1 << final; final = parent;
        }
        return (order, starts[order[0]]);
    }
}

public static class ResetClock {
    public static DateTimeOffset Period(DateTimeOffset now, bool weekly) {
        var utc = now.UtcDateTime;
        var boundary = new DateTimeOffset(utc.Date.AddHours(weekly ? 8 : 15), TimeSpan.Zero);
        if (weekly) boundary = boundary.AddDays(-((7 + (int)boundary.DayOfWeek - (int)DayOfWeek.Tuesday) % 7));
        if (boundary > now) boundary = boundary.AddDays(weekly ? -7 : -1);
        return boundary;
    }
    public static DateTimeOffset Next(DateTimeOffset now, bool weekly) => Period(now, weekly).AddDays(weekly ? 7 : 1);
}
