using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaCompass;

public enum ActivityKind { Dungeon, Trial, Raid, AllianceRaid }
public enum ActivityState { Unknown, NotStarted, InProgress, Unlocked, Cleared }
public sealed record QuestNode(uint Id, string Name, uint[] Previous, byte Join, int Level,
    uint Genre, uint ClassCategory, bool Partial, bool StoryChain = false);
public sealed record Activity(uint Id, uint InstanceId, string Name, ActivityKind Kind, uint Expansion,
    string ExpansionName, int Level, int ItemLevel, bool Advanced, uint[] QuestIds, string Source);
public sealed record QuestObservation(bool Complete, bool Accepted, bool LevelMet, bool ClassMet);
public sealed record NextStep(uint QuestId, bool Accepted, bool Ready, bool Partial, string Reason);
public sealed record ActivityProgress(Activity Activity, ActivityState State, NextStep? Next, bool HasHistory);

public static class ProgressResolver
{
    public static bool HasChainProgress(Activity activity, IReadOnlyDictionary<uint, QuestNode> quests,
        IReadOnlyDictionary<uint, QuestObservation> observations)
    {
        foreach (var root in activity.QuestIds)
        {
            if (observations.GetValueOrDefault(root)?.Accepted == true) return true;
            if (!quests.TryGetValue(root, out var first) || !first.StoryChain) continue;
            var seen = new HashSet<uint>();
            var pending = new Stack<uint>(first.Previous);
            while (pending.TryPop(out var id))
            {
                if (!seen.Add(id) || !quests.TryGetValue(id, out var q) || q.Genre != first.Genre) continue;
                if (observations.GetValueOrDefault(id) is { Complete: true } or { Accepted: true }) return true;
                foreach (var previous in q.Previous) pending.Push(previous);
            }
        }
        return false;
    }
    // The game uses 1=AND and 2=OR. Unknown joins and sequence prerequisites stay uncertain.
    public static bool? MeetsPrevious(byte join, IReadOnlyList<bool> values) => values.Count == 0 ? true : join switch
    {
        1 => values.All(v => v), 2 => values.Any(v => v), _ => null,
    };

    public static NextStep Resolve(uint id, IReadOnlyDictionary<uint, QuestNode> quests,
        IReadOnlyDictionary<uint, QuestObservation> observations)
        => Walk(id, quests, observations, new HashSet<uint>());

    private static NextStep Walk(uint id, IReadOnlyDictionary<uint, QuestNode> quests,
        IReadOnlyDictionary<uint, QuestObservation> observations, HashSet<uint> path)
    {
        if (!path.Add(id)) return new(id, false, false, true, "cycle");
        try
        {
            if (!quests.TryGetValue(id, out var q) || !observations.TryGetValue(id, out var o))
                return new(id, false, false, true, "missing");
            if (o.Complete) return new(id, false, false, true, "completed");
            // The acceptance flag is stronger evidence than reconstructed offer requirements.
            if (o.Accepted) return new(id, true, true, false, "accepted");
            if (q.Join is not (1 or 2) && q.Previous.Length > 0)
                return new(id, false, false, true, "conditions");
            var known = q.Previous.All(observations.ContainsKey);
            var met = MeetsPrevious(q.Join, q.Previous.Select(p => observations.GetValueOrDefault(p)?.Complete == true).ToArray());
            if (q.Partial && met != true) return new(id, false, false, true, "conditions");
            if (met == false)
            {
                var candidates = q.Previous.Where(p => observations.GetValueOrDefault(p)?.Complete != true)
                    .Select(p => Walk(p, quests, observations, path)).ToArray();
                // For OR requirements show a usable alternative; for AND show an available next prerequisite.
                return candidates.OrderByDescending(c => c.Ready).ThenBy(c => c.Partial).ThenBy(c => c.QuestId).First();
            }
            if (!known || met == null || q.Partial) return new(id, false, false, true, "conditions");
            if (!o.LevelMet) return new(id, false, false, false, "level");
            if (!o.ClassMet) return new(id, false, false, false, "class");
            return new(id, false, true, false, "offer");
        }
        finally { path.Remove(id); }
    }

    public static ActivityProgress Evaluate(Activity activity, bool unlocked, bool cleared, bool history,
        IReadOnlyDictionary<uint, QuestNode> quests, IReadOnlyDictionary<uint, QuestObservation> observations)
    {
        history |= HasChainProgress(activity, quests, observations);
        if (cleared) return new(activity, ActivityState.Cleared, null, history);
        if (unlocked) return new(activity, ActivityState.Unlocked, null, history);
        var active = activity.QuestIds.Where(q => observations.GetValueOrDefault(q)?.Accepted == true).ToArray();
        if (active.Length == 1) return new(activity, ActivityState.InProgress, Resolve(active[0], quests, observations), history);
        if (activity.QuestIds.Length != 1) return new(activity, history || active.Length > 0 ? ActivityState.InProgress : ActivityState.NotStarted,
            new(0, false, false, true, activity.QuestIds.Length == 0 ? "unmapped" : "ambiguous"), history);
        var root = activity.QuestIds[0];
        var step = Resolve(root, quests, observations);
        var accepted = observations.GetValueOrDefault(root)?.Accepted == true;
        // A completed shared prerequisite does not imply that this activity was started.
        return new(activity, history || accepted ? ActivityState.InProgress : ActivityState.NotStarted, step, history);
    }
}
