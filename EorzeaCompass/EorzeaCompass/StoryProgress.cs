using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EorzeaCompass;

public enum StoryState { Unknown, NotStarted, InProgress, Completed }
public sealed record StoryDefinition(string Id, string Name, string Kind, uint Expansion, uint Genre,
    uint EndQuest, string EndQuestName, uint StartQuest = 0, bool CurrentChapter = false,
    string Scope = "", string ScopeRu = "", bool Segment = false, uint Achievement = 0, string AchievementName = "",
    uint[]? Milestones = null);
public sealed record Story(StoryDefinition Definition, uint[] QuestIds, uint[] ActivityIds, bool Valid);
public sealed record StoryProgress(Story Story, StoryState State, int Done, uint[] Route, NextStep? Next)
{
    public float Fraction => State == StoryState.Completed ? 1 : Route.Length == 0 ? 0 : (float)Done / Route.Length;
}
public sealed record ExpansionProgress(uint Id, string Name, int StoriesDone, int StoriesTotal,
    int StoriesUnknown, int ActivitiesUnlocked, int ActivitiesCleared, int ActivitiesTotal, int ActivitiesUnknown);

public static class StoryCatalog
{
    public static IReadOnlyList<Story> Build(IReadOnlyDictionary<uint, QuestNode> quests, IReadOnlyList<Activity> activities)
    {
        using var stream = typeof(StoryCatalog).Assembly.GetManifestResourceStream("EorzeaCompass.stories.json")
            ?? throw new InvalidDataException("Bundled story catalog missing");
        var definitions = JsonSerializer.Deserialize<StoryDefinition[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty story catalog");
        if (definitions.Select(d => d.Id).Distinct().Count() != definitions.Length) throw new InvalidDataException("Duplicate story IDs");
        return definitions.Select(d =>
        {
            var included = new HashSet<uint>();
            void Include(uint id)
            {
                if (!quests.TryGetValue(id, out var q) || q.Genre != d.Genre
                    || d.Segment && q.Expansion != d.Expansion || !included.Add(id) || id == d.StartQuest) return;
                foreach (var previous in q.Previous) Include(previous);
            }
            Include(d.EndQuest);
            foreach (var milestone in d.Milestones ?? []) Include(milestone);
            var valid = quests.TryGetValue(d.EndQuest, out var end) && end.Name == d.EndQuestName && end.Genre == d.Genre
                && (d.StartQuest == 0 || included.Contains(d.StartQuest)) && (d.Milestones ?? []).All(included.Contains);
            // This is a relation to a story, not proof that clearing a duty finishes its narrative.
            var linked = activities.Where(a => !a.Advanced && a.QuestIds.Any(included.Contains)).Select(a => a.Id).ToArray();
            return new Story(d, included.Order().ToArray(), linked, valid);
        }).OrderBy(s => s.Definition.Expansion).ThenBy(s => s.Definition.Kind).ThenBy(s => s.Definition.Id).ToArray();
    }

    public static StoryProgress Evaluate(Story story, IReadOnlyDictionary<uint, QuestNode> quests,
        IReadOnlyDictionary<uint, QuestObservation> observations, bool history = false,
        IReadOnlyDictionary<uint, bool>? achievements = null)
    {
        if (!story.Valid) return new(story, StoryState.Unknown, 0, [], null);
        var allowed = story.QuestIds.ToHashSet();
        var visited = new HashSet<uint>();
        var route = new List<uint>();
        var routePrevious = new Dictionary<uint, uint[]>();
        bool broken = false;
        int Evidence(uint id, HashSet<uint> seen)
        {
            if (!allowed.Contains(id) || !seen.Add(id) || !quests.TryGetValue(id, out var q)) return 0;
            var o = observations.GetValueOrDefault(id);
            return (o?.Accepted == true ? 100 : o?.Complete == true ? 1 : 0) + q.Previous.Sum(p => Evidence(p, seen));
        }
        void Visit(uint id, HashSet<uint> path)
        {
            if (!allowed.Contains(id) || visited.Contains(id)) return;
            if (!path.Add(id)) { broken = true; return; }
            if (!quests.TryGetValue(id, out var q)) { broken = true; return; }
            if (id != story.Definition.StartQuest)
            {
                var previous = q.Previous.Where(allowed.Contains).ToArray();
                if (q.Join == 2 && q.Previous.Any(p => !allowed.Contains(p) && observations.GetValueOrDefault(p)?.Complete == true)) previous = [];
                if (previous.Length > 1 && q.Join is not (1 or 2)) broken = true;
                if (q.Join == 2 && previous.Length > 1)
                {
                    // Prefer the actual completed/accepted branch; don't count mutually exclusive routes twice.
                    var branch = previous.OrderByDescending(p => observations.GetValueOrDefault(p)?.Complete == true)
                        .ThenByDescending(p => Evidence(p, new())).ThenBy(p => p).First();
                    routePrevious[id] = [branch];
                    Visit(branch, path);
                }
                else
                {
                    routePrevious[id] = previous;
                    foreach (var p in previous) Visit(p, path);
                }
            }
            path.Remove(id); visited.Add(id); route.Add(id);
        }
        foreach (var milestone in story.Definition.Milestones ?? []) Visit(milestone, new());
        Visit(story.Definition.EndQuest, new());
        if (story.Definition.Achievement != 0)
        {
            if (achievements?.GetValueOrDefault(story.Definition.Achievement) == true)
                return new(story, StoryState.Completed, route.Count, route.ToArray(), null);
            if (observations.GetValueOrDefault(story.Definition.EndQuest)?.Complete == true)
                return new(story, achievements?.ContainsKey(story.Definition.Achievement) == true ? StoryState.InProgress : StoryState.Unknown,
                    0, route.ToArray(), null);
        }
        if (observations.GetValueOrDefault(story.Definition.EndQuest)?.Complete == true)
            return new(story, StoryState.Completed, route.Count, route.ToArray(), null);
        if (broken || route.Count == 0 || route.Any(id => !observations.ContainsKey(id)))
            return new(story, StoryState.Unknown, route.Count(id => observations.GetValueOrDefault(id)?.Complete == true), route.ToArray(), null);
        // Only an actual descendant proves a repeatable prerequisite was passed.
        // A completed sibling in an AND branch must not prove the other branch.
        var inferred = new HashSet<uint>();
        var proven = new HashSet<uint>();
        void ProveAncestors(uint id)
        {
            if (!proven.Add(id)) return;
            foreach (var previous in routePrevious.GetValueOrDefault(id) ?? [])
            {
                if (quests[previous].Repeatable) inferred.Add(previous);
                ProveAncestors(previous);
            }
        }
        foreach (var id in route.Where(id => observations[id].Complete)) ProveAncestors(id);
        bool Passed(uint id) => observations[id].Complete || inferred.Contains(id);
        var done = route.Count(Passed);
        var active = route.FirstOrDefault(id => observations[id].Accepted && !Passed(id));
        var nextId = active != 0 ? active : route.First(id => !Passed(id));
        var started = history || done > 0 || active != 0;
        var effective = inferred.Count == 0 ? observations : observations.ToDictionary(p => p.Key,
            p => inferred.Contains(p.Key) ? p.Value with { Complete = true, Accepted = false } : p.Value);
        return new(story, started ? StoryState.InProgress : StoryState.NotStarted, done, route.ToArray(),
            ProgressResolver.Resolve(nextId, quests, effective));
    }

    public static IReadOnlyList<ExpansionProgress> Summarize(IEnumerable<StoryProgress> stories, IEnumerable<ActivityProgress> activities)
    {
        var s = stories.ToArray(); var a = activities.Where(p => !p.Activity.Advanced).ToArray();
        return a.Select(p => p.Activity.Expansion).Concat(s.Select(p => p.Story.Definition.Expansion)).Distinct().Order().Select(id =>
        {
            var ss = s.Where(p => p.Story.Definition.Expansion == id).ToArray();
            var aa = a.Where(p => p.Activity.Expansion == id).ToArray();
            var name = aa.FirstOrDefault()?.Activity.ExpansionName ?? "Expansion " + id;
            return new ExpansionProgress(id, name, ss.Count(p => p.State == StoryState.Completed), ss.Length,
                ss.Count(p => p.State == StoryState.Unknown), aa.Count(p => p.State is ActivityState.Unlocked or ActivityState.Cleared),
                aa.Count(p => p.State == ActivityState.Cleared), aa.Length, aa.Count(p => p.State == ActivityState.Unknown));
        }).ToArray();
    }
}
