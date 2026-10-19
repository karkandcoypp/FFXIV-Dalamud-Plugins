using System;
using System.Collections.Generic;
using EorzeaEventTracker;

internal static class NextStepChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var e = new EventInfo { StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1), Locations = [new() { Id = "shop" }] };
        var d = new TrackingDefinition { CompletionQuestId = 70001, Quests = [new() { Id = 70001, Name = "Main quest" }],
            Rewards = [new() { Id = 1, Name = "Reward", Price = 100, CurrencyItemId = 10, LocationId = "shop" }] };
        var settings = new CharacterSettings();
        var quests = new Dictionary<uint, bool> { [70001] = false };
        var access = new Dictionary<uint, QuestAccess> { [70001] = new(15, true, 1, [], false, true) };
        var rewards = new Dictionary<uint, RewardProgress> { [1] = new(RewardState.NotSeen) };
        var balances = new Dictionary<uint, long> { [10] = 20 };
        bool ready = true;
        NextStep Step() => NextStepPolicy.Decide(e, d, now, ready, settings, quests, access, rewards, balances, id => access.GetValueOrDefault(id));
        Check(Step().Kind == NextStepKind.Quest, "unfinished accessible main quest comes first");
        access[70001] = access[70001] with { Accepted = true };
        Check(Step().Kind == NextStepKind.Accepted, "accepted quest points to journal, not starting NPC");
        access[70001] = access[70001] with { Accepted = false, LevelMet = false };
        Check(Step().Kind == NextStepKind.Level, "level blocker is actionable");
        access[70001] = access[70001] with { LevelMet = true, ClassMet = false, ClassName = "Disciples of War" };
        Check(Step().Kind == NextStepKind.Class, "class blocker is actionable");
        access[70001] = new(15, true, 2, [new(70002, "Route A", false), new(70003, "Route B", false)], false, true);
        Check(Step().Kind == NextStepKind.Choices && Step().Choices!.Count == 2, "OR routes never arbitrarily select one city");
        access[70001] = access[70001] with { Join = 1, Previous = [new(70002, "Unlock quest", false)] };
        access[70002] = new(1, true, 1, [], false, true);
        Check(Step().Kind == NextStepKind.Quest && Step().QuestId == 70002, "walk back to the actionable prerequisite");
        access[70002] = access[70002] with { Previous = [new(70001, "Main quest", false)] };
        Check(Step().Kind == NextStepKind.Requirements, "cyclic prerequisite data is bounded");
        quests[70001] = true;
        Check(Step().Kind == NextStepKind.ChooseRewards, "no favorites prompts a personal goal");
        settings.FavoriteRewards.Add(1);
        Check(Step().Kind == NextStepKind.Farm && Step().Budgets![0].Remaining == 80, "currency deficit is exact and does not suggest the shop");
        balances[10] = 100;
        Check(Step().Kind == NextStepKind.Buy && Step().LocationId == "shop", "affordable favorites point to their merchant");
        balances.Clear();
        Check(Step().Kind == NextStepKind.ReviewRewards, "unknown balance is not treated as zero or sufficient");
        rewards[1] = new(RewardState.Unknown);
        Check(Step().Kind == NextStepKind.ReviewRewards, "unknown acquisition never produces a farming claim");
        rewards[1] = new(RewardState.ManuallyObtained);
        Check(Step().Kind == NextStepKind.FavoritesComplete, "manual obtained counts as complete");
        settings.FavoriteRewards.Clear();
        Check(Step().Kind == NextStepKind.CollectionComplete, "all tracked rewards confirmed is distinct from event completion");
        settings.IgnoredRewards.Add(1);
        Check(Step().Kind == NextStepKind.ChooseRewards, "ignored rewards do not produce farming targets");
        ready = false;
        Check(Step().Kind == NextStepKind.Waiting, "new character waits for its own loaded data");
        ready = true;
        e.StartsAt = now.AddHours(1);
        Check(Step().Kind == NextStepKind.Upcoming, "future event never directs player to an unavailable NPC");
        e.StartsAt = now.AddDays(-1); e.EndsAt = now;
        Check(Step().Kind == NextStepKind.Ended, "ended event never directs player to an unavailable NPC");
        e.EndsAt = now.AddDays(1);
        // Automatic feeds may include prerequisite alternatives as tracked quests.
        d.CompletionQuestId = 0;
        d.Quests.Add(new() { Id = 70002, Name = "Unlock quest" });
        d.Quests.Add(new() { Id = 70003, Name = "Alternative route" });
        quests[70002] = true; quests[70003] = false;
        access[70001] = new(15, true, 2, [new(70002, "Unlock quest", true), new(70003, "Alternative route", false)], false, true);
        access.Remove(70002);
        Check(Step().Kind == NextStepKind.ChooseRewards, "unused OR alternative is not recommended after main chain is complete");
        var otherCharacter = new Dictionary<uint, bool> { [70001] = false };
        Check(NextStepPolicy.Decide(e, d, now, true, new(), otherCharacter, access, new Dictionary<uint, RewardProgress>(), balances, _ => null).Kind == NextStepKind.Quest,
            "another character gets its own unfinished objective");
        Console.WriteLine("PASS: next-step objectives, prerequisites, OR routes, currency, unknowns, dates and character isolation");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
