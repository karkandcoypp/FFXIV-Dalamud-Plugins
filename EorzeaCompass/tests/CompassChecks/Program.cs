using EorzeaCompass;
using Lumina;
using Lumina.Excel.Sheets;
using System.Text.Json;

var count = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); count++; }
var quests = new Dictionary<uint, QuestNode>();
var obs = new Dictionary<uint, QuestObservation>();
void Q(uint id, uint[] previous, byte join = 1, bool partial = false, bool complete = false, bool accepted = false, bool level = true, bool job = true)
{
    quests[id] = new(id, "Quest " + id, previous, join, 50, 1, 1, partial);
    obs[id] = new(complete, accepted, level, job);
}
NextStep Resolve(uint id) => ProgressResolver.Resolve(id, quests, obs);
Q(1, []); Q(2, [1]); Q(3, [2]);
Check(Resolve(3) is { QuestId: 1, Ready: true }, "first unfinished ancestor");
obs[1] = obs[1] with { Complete = true };
Check(Resolve(3) is { QuestId: 2, Ready: true }, "completed prerequisites skipped");
obs[2] = obs[2] with { Accepted = true, LevelMet = false };
Check(Resolve(3) is { QuestId: 2, Ready: true, Accepted: true }, "accepted quest overrides reconstructed level");
Q(4, [1, 2], 2);
Check(Resolve(4) is { QuestId: 4, Ready: true }, "OR prerequisite already satisfied");
Q(5, [], level: false); Q(6, []); Q(7, [5, 6], 2);
Check(Resolve(7) is { QuestId: 6, Ready: true }, "OR chooses available branch");
Q(8, [5, 6], 1);
Check(Resolve(8) is { QuestId: 6, Ready: true }, "AND can advance one prerequisite");
Q(9, [1], 7);
Check(Resolve(9) is { Ready: false, Partial: true }, "unknown join never guessed");
Q(10, [11]); Q(11, [10]);
Check(Resolve(10) is { Ready: false, Partial: true, Reason: "cycle" }, "cycle bounded");
Q(12, [999]);
Check(Resolve(12) is { Ready: false, Partial: true }, "missing ancestor not offered");
Q(13, [], partial: true);
Check(Resolve(13) is { Ready: false, Partial: true }, "additional conditions excluded from suggestions");
Q(14, [], job: false);
Check(Resolve(14).Reason == "class", "wrong job explained");
Check(Resolve(5).Reason == "level", "level requirement explained");
Q(15, [2], partial: true);
Check(Resolve(15) is { QuestId: 15, Ready: false, Partial: true }, "sequence or partial conditions not turned into completion requirements");
var activity = new Activity(100, 100, "Duty", ActivityKind.Raid, 0, "ARR", 50, 90, false, [3], "test");
var visibleRow = new ActivityProgress(activity, ActivityState.NotStarted, null, false);
Check(CompassSelection.KeepVisible(999, [visibleRow]) == 100, "filter change replaces stale detail selection");
Check(CompassSelection.KeepVisible(100, []) == 0, "empty results clear detail selection");
Check(CompassSelection.KeepVisible(100, [visibleRow]) == 100, "refresh preserves visible selection");
Check(ProgressResolver.Evaluate(activity, false, false, false, quests, obs).State == ActivityState.NotStarted, "shared completed MSQ not evidence of started duty");
quests[3] = quests[3] with { StoryChain = true };
Check(ProgressResolver.HasChainProgress(activity, quests, obs), "same storyline accepted prerequisite restores unfinished chain");
quests[3] = quests[3] with { StoryChain = false };
quests[3] = quests[3] with { StoryChain = true, Genre = 2 };
Check(!ProgressResolver.HasChainProgress(activity, quests, obs), "another genre not mistaken for started storyline");
quests[3] = quests[3] with { StoryChain = false, Genre = 1 };
Check(ProgressResolver.Evaluate(activity, false, false, true, quests, obs).State == ActivityState.InProgress, "observed history survives abandonment");
Check(ProgressResolver.Evaluate(activity, true, false, true, quests, obs).State == ActivityState.Unlocked, "unlock distinct from clear");
Check(ProgressResolver.Evaluate(activity, false, true, false, quests, obs).State == ActivityState.Cleared, "clear is strongest evidence");
Check(ProgressResolver.Evaluate(activity with { QuestIds = [] }, false, false, false, quests, obs).Next is { Ready: false, Reason: "unmapped" }, "unmapped duties still cataloged");
Check(ProgressResolver.Evaluate(activity with { QuestIds = [5, 6] }, false, false, false, quests, obs).Next is { Ready: false, Reason: "ambiguous" }, "ambiguous route not recommended");
Check(ProgressResolver.Evaluate(activity with { QuestIds = [1, 2] }, false, false, false, quests, obs).Next is { QuestId: 2, Accepted: true }, "accepted quest selects correct alternative");
obs[3] = obs[3] with { Complete = true };
Check(Resolve(3) is { Ready: false, Reason: "completed" }, "completed unlock quest with locked duty not offered again");
Console.WriteLine($"PASS: {count} progress checks");

if (args.Length > 0)
{
    using var game = new GameData(args[0]);
    var catalog = new Catalog(game.Excel.GetSheet<Quest>(), game.Excel.GetSheet<ContentFinderCondition>());
    Check(catalog.Activities.Count > 200, "real game coverage");
    Check(catalog.Quests.Count > 5000, "full quest index");
    Check(catalog.Activities.Select(a => a.Id).Distinct().Count() == catalog.Activities.Count, "unique duties");
    Check(catalog.Activities.Count(a => a.Name.Equals("the Gilded Araya", StringComparison.OrdinalIgnoreCase)) == 1, "old fan event duplicate removed");
    Check(catalog.Activities.Single(a => a.Name.Equals("the Labyrinth of the Ancients", StringComparison.OrdinalIgnoreCase)).QuestIds.SequenceEqual(new uint[] { 66738 }), "Omega reference never treated as Crystal Tower unlock");
    foreach (var kind in Enum.GetValues<ActivityKind>())
        Check(catalog.Activities.Any(a => a.Kind == kind), "category " + kind);
    foreach (var a in catalog.Activities)
        Check(a.QuestIds.All(catalog.Quests.ContainsKey), "quest references " + a.Name);
    if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(new { Quests = catalog.Quests.Count, Duties = catalog.Activities }, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var name in new[] { "Sastasha", "the Labyrinth of the Ancients", "Halatali", "Alexander - The Fist of the Father", "the Royal City of Rabanastre", "the Singularity Reactor", "the Royal Menagerie", "the Dying Gasp", "the Final Day", "the Interphos" })
    {
        var duty = catalog.Activities.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"ROUTE {name}: {string.Join(',', duty?.QuestIds ?? [])}");
        Check(duty is { QuestIds.Length: 1 }, "known route " + name);
    }
    Console.WriteLine($"CATALOG: {catalog.Quests.Count} quests, {catalog.Activities.Count} duties, {catalog.Activities.Count(a => !a.Advanced)} standard, {catalog.Activities.Count(a => a.QuestIds.Length == 1)} single quest links, {catalog.Activities.Count(a => a.QuestIds.Length == 0)} unmapped, {catalog.Activities.Count(a => a.QuestIds.Length > 1)} ambiguous");
    foreach (var group in catalog.Activities.GroupBy(a => a.Kind)) Console.WriteLine($"  {group.Key}: {group.Count()}");
    if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(new { Quests = catalog.Quests.Count, Duties = catalog.Activities }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"PASS: {count} total checks including installed game catalog");
}
