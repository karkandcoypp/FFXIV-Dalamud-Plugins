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

// Story progress is narrative completion, independent from a duty unlock/clear.
var definition = new StoryDefinition("test", "Story", "Chronicles", 0, 1, 103, "Final");
var story = new EorzeaCompass.Story(definition, [101, 102, 103], [100], true);
var sq = new Dictionary<uint, QuestNode> {
    [101] = new(101,"Start",[],1,1,1,1,false),
    [102] = new(102,"Middle",[101],1,1,1,1,false),
    [103] = new(103,"Final",[102],1,1,1,1,false),
};
var so = sq.Keys.ToDictionary(id=>id,_=>new QuestObservation(false,false,true,true));
StoryProgress StoryResult() => StoryCatalog.Evaluate(story,sq,so);
Check(StoryResult().State == StoryState.NotStarted, "story not started with no local quest progress");
so[101] = so[101] with { Complete=true };
Check(StoryResult() is { State:StoryState.InProgress, Done:1, Next.QuestId:102 }, "unfinished story identifies next quest");
so[102] = so[102] with { Complete=true };
Check(StoryResult().State == StoryState.InProgress, "all earlier quests do not finish narrative");
var dutyCleared = new ActivityProgress(activity,ActivityState.Cleared,null,false);
var summary = StoryCatalog.Summarize([StoryResult()],[dutyCleared]).Single();
Check(summary.StoriesDone == 0 && summary.ActivitiesCleared == 1, "duty clear never substitutes for story completion");
so[103] = so[103] with { Complete=true };
Check(StoryResult() is { State:StoryState.Completed, Fraction:1 }, "explicit final quest confirms completion");
Check(StoryCatalog.Evaluate(story with { Valid=false },sq,so).State==StoryState.Unknown, "invalid catalog rule not treated as complete");
so.Clear();
Check(StoryResult().State==StoryState.Unknown, "missing character data not treated as not started");
foreach(var id in sq.Keys) so[id]=new(false,false,true,true);
Check(StoryCatalog.Evaluate(story,sq,so,true).State==StoryState.InProgress, "saved story acceptance survives abandonment");
sq[104]=new(104,"Alternative",[],1,1,1,1,false);
so[104]=new(true,false,true,true);
sq[102]=sq[102] with { Previous=[101,104],Join=2 };
var alternativeStory=story with { QuestIds=[101,102,103,104] };
var alternative=StoryCatalog.Evaluate(alternativeStory,sq,so);
Check(alternative.Route.Contains(104u) && !alternative.Route.Contains(101u) && alternative.Route.Length==3,"mutually exclusive quest branches counted once");
sq[105]=new(105,"Branch with active ancestor",[106],1,1,1,1,false);
sq[106]=new(106,"Active ancestor",[],1,1,1,1,false);
so[104]=so[104] with { Complete=false }; so[105]=new(false,false,true,true); so[106]=new(false,true,true,true);
sq[102]=sq[102] with { Previous=[101,105] };
var deeper=StoryCatalog.Evaluate(story with { QuestIds=[101,102,103,105,106] },sq,so);
Check(deeper.Route.Contains(106u) && deeper.Next?.QuestId==106,"active quest deep in an alternative branch selects the correct route");
Check(StoryCatalog.Summarize([],[]).Count==0,"empty journal has no invented progress");
var unknownSummary=StoryCatalog.Summarize([StoryCatalog.Evaluate(story with { Valid=false },sq,so)],[dutyCleared]).Single();
Check(unknownSummary.StoriesUnknown==1 && unknownSummary.StoriesDone==0,"unknown story progress remains explicit in overview");
Console.WriteLine($"PASS: {count} checks including story state and overview");

if (args.Length > 0)
{
    using var game = new GameData(args[0]);
    var catalog = new Catalog(game.Excel.GetSheet<Quest>(), game.Excel.GetSheet<ContentFinderCondition>());
    Check(catalog.Activities.Count > 200, "real game coverage");
    Check(catalog.Quests.Count > 5000, "full quest index");
    Check(catalog.Stories.Count >= 30, "story catalog coverage");
    Check(catalog.Stories.All(s=>s.Valid), "all explicit story endpoints match installed game");
    Check(catalog.Stories.All(s=>s.QuestIds.Contains(s.Definition.EndQuest)), "final quest belongs to story route");
    Check(catalog.Stories.Single(s=>s.Definition.Name=="Crystal Tower").Definition.EndQuest==66031,"Crystal Tower ending excludes optional epilogue");
    Check(catalog.Stories.Single(s=>s.Definition.Name=="Return to Ivalice").Definition.EndQuest==68725,"Ivalice ending excludes cooking side quests");
    foreach(var storyItem in catalog.Stories)
    {
        var observations=catalog.RelevantQuests.ToDictionary(id=>id,_=>new QuestObservation(false,false,true,true));
        var beginning=StoryCatalog.Evaluate(storyItem,catalog.Quests,observations);
        Check(beginning.State==StoryState.NotStarted,"fresh story "+storyItem.Definition.Name);
        observations[storyItem.Definition.EndQuest]=new(true,false,true,true);
        Check(StoryCatalog.Evaluate(storyItem,catalog.Quests,observations).State==StoryState.Completed,"endpoint proof "+storyItem.Definition.Name);
    }
    Check(catalog.Activities.Select(a => a.Id).Distinct().Count() == catalog.Activities.Count, "unique duties");
    Check(catalog.Activities.Count(a => a.Name.Equals("the Gilded Araya", StringComparison.OrdinalIgnoreCase)) == 1, "old fan event duplicate removed");
    Check(catalog.Activities.Single(a => a.Name.Equals("the Labyrinth of the Ancients", StringComparison.OrdinalIgnoreCase)).QuestIds.SequenceEqual(new uint[] { 66738 }), "Omega reference never treated as Crystal Tower unlock");
    foreach (var kind in Enum.GetValues<ActivityKind>())
        Check(catalog.Activities.Any(a => a.Kind == kind), "category " + kind);
    foreach (var a in catalog.Activities)
        Check(a.QuestIds.All(catalog.Quests.ContainsKey), "quest references " + a.Name);
    if (args.Length > 1) File.WriteAllText(args[1], JsonSerializer.Serialize(new { Quests = catalog.Quests.Count, Stories = catalog.Stories, Duties = catalog.Activities }, new JsonSerializerOptions { WriteIndented = true }));
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
