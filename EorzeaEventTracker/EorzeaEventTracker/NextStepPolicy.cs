using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public enum NextStepKind { Waiting, Upcoming, Ended, Unmapped, Quest, Accepted, Level, Class, Choices, Requirements, Farm, Buy, ReviewRewards, ChooseRewards, FavoritesComplete, CollectionComplete, TrackedQuestsComplete }
public sealed record NextStep(NextStepKind Kind, string Name = "", uint QuestId = 0, int Level = 0,
    string ClassName = "", IReadOnlyList<string>? Choices = null, IReadOnlyList<CurrencyBudget>? Budgets = null,
    string LocationId = "", bool Partial = false);

public static class NextStepPolicy
{
    public static NextStep Decide(EventInfo e, TrackingDefinition? definition, DateTimeOffset now, bool ready,
        CharacterSettings preferences, IReadOnlyDictionary<uint, bool> quests,
        IReadOnlyDictionary<uint, QuestAccess> access, IReadOnlyDictionary<uint, RewardProgress> rewards,
        IReadOnlyDictionary<uint, long> balances, Func<uint, QuestAccess?> prerequisite)
    {
        if (e.Upcoming(now)) return new(NextStepKind.Upcoming);
        if (!e.Active(now)) return new(NextStepKind.Ended);
        if (!ready) return new(NextStepKind.Waiting);
        if (definition == null || definition.Quests.Count == 0 && definition.Rewards.Count == 0) return new(NextStepKind.Unmapped);
        // Prerequisite alternatives included in a feed are not independent event objectives.
        var prerequisites = definition.Quests.Select(q => access.GetValueOrDefault(q.Id)).OfType<QuestAccess>().SelectMany(a => a.Previous).Select(p => p.Id).ToHashSet();
        var objectives = definition.CompletionQuestId != 0
            ? definition.Quests.Where(q => q.Id == definition.CompletionQuestId).ToList()
            : definition.Quests.Where(q => !prerequisites.Contains(q.Id)).ToList();
        if (definition.Quests.Count > 0 && objectives.Count == 0) return new(NextStepKind.Unmapped);
        foreach (var quest in objectives)
        {
            if (!quests.TryGetValue(quest.Id, out var done)) return new(NextStepKind.Requirements, quest.Name, quest.Id);
            if (done) continue;
            return Resolve(quest.Id, quest.Name, access.GetValueOrDefault(quest.Id), prerequisite, new HashSet<uint>(), 0);
        }
        if (definition.Rewards.Count == 0) return new(NextStepKind.TrackedQuestsComplete);
        var pending = FavoriteBudget.Pending(definition.Rewards, preferences.FavoriteRewards, preferences.IgnoredRewards, rewards);
        var favorites = definition.Rewards.Where(r => preferences.FavoriteRewards.Contains(r.Id) && !preferences.IgnoredRewards.Contains(r.Id)).ToList();
        if (pending.Count > 0)
        {
            if (pending.Any(r => !rewards.TryGetValue(r.Id, out var state) || state.State == RewardState.Unknown))
                return new(NextStepKind.ReviewRewards);
            // A quest-earned favorite can require a side quest even after the main event objective.
            var earned = pending.FirstOrDefault(r => r.EarnedByQuest != 0 && !quests.GetValueOrDefault(r.EarnedByQuest));
            if (earned != null)
            {
                var quest = definition.Quests.FirstOrDefault(q => q.Id == earned.EarnedByQuest);
                if (quest != null) return Resolve(quest.Id, quest.Name, access.GetValueOrDefault(quest.Id), prerequisite, new HashSet<uint>(), 0);
                return new(NextStepKind.ReviewRewards);
            }
            var budgets = FavoriteBudget.Calculate(pending, balances);
            if (budgets.Any(b => b.Remaining > 0)) return new(NextStepKind.Farm, Budgets: budgets, Partial: budgets.Any(b => b.Balance == null) || pending.Any(r => r.Price == 0 && r.EarnedByQuest == 0));
            if (pending.Any(r => r.Price == 0 && r.EarnedByQuest == 0) || budgets.Any(b => b.Balance == null))
                return new(NextStepKind.ReviewRewards);
            // All known currency totals are affordable; only then recommend a merchant.
            var buy = pending.FirstOrDefault(r => r.Price > 0 && e.Locations.Any(l => l.Id == r.LocationId));
            return buy != null ? new(NextStepKind.Buy, buy.Name, LocationId: buy.LocationId)
                : new(NextStepKind.ReviewRewards);
        }
        if (favorites.Count > 0) return new(NextStepKind.FavoritesComplete);
        var included = definition.Rewards.Where(r => !preferences.IgnoredRewards.Contains(r.Id)).ToList();
        if (included.Count > 0 && included.All(r => rewards.TryGetValue(r.Id, out var state) && state.Confirmed))
            return new(NextStepKind.CollectionComplete);
        return new(NextStepKind.ChooseRewards);
    }

    private static NextStep Resolve(uint id, string name, QuestAccess? access,
        Func<uint, QuestAccess?> lookup, HashSet<uint> path, int depth)
    {
        if (!path.Add(id) || depth >= 8 || access is not { Known: true }) return new(NextStepKind.Requirements, name, id);
        if (access.Accepted) return new(NextStepKind.Accepted, name, id);
        var previousMet = QuestAccess.MeetsPrevious(access.Join, access.Previous);
        if (previousMet == false)
        {
            var missing = access.Previous.Where(p => !p.Complete).ToList();
            if (access.Join == 2 && missing.Count > 1)
                return new(NextStepKind.Choices, name, id, Choices: missing.Select(p => p.Name).ToList());
            var first = missing.First();
            return Resolve(first.Id, first.Name, lookup(first.Id), lookup, path, depth + 1);
        }
        if (!access.ClassMet) return new(NextStepKind.Class, name, id, ClassName: access.ClassName);
        if (!access.LevelMet) return new(NextStepKind.Level, name, id, Level: access.Level);
        if (previousMet == null) return new(NextStepKind.Requirements, name, id);
        return new(NextStepKind.Quest, name, id, Partial: access.Partial);
    }
}
