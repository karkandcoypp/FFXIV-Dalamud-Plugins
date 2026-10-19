using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace EorzeaCompass;

public sealed class Catalog
{
    public IReadOnlyDictionary<uint, QuestNode> Quests { get; }
    public IReadOnlyList<Activity> Activities { get; }
    public IReadOnlySet<uint> RelevantQuests { get; }
    public IReadOnlyList<Story> Stories { get; }

    public Catalog(ExcelSheet<Quest> questSheet, ExcelSheet<ContentFinderCondition> duties)
    {
        var quests = questSheet.Where(q => q.RowId > 65535 && !string.IsNullOrWhiteSpace(q.Name.ToString())).ToArray();
        Quests = quests.ToDictionary(q => q.RowId, q => new QuestNode(q.RowId, q.Name.ToString(),
            q.PreviousQuest.Where(p => p.RowId != 0).Select(p => p.RowId).ToArray(),
            q.Unknown7 != 0 ? (byte)0 : q.PreviousQuestJoin, q.ClassJobLevel[0], q.JournalGenre.RowId,
            q.ClassJobCategory0.RowId, IsPartial(q), q.JournalGenre.RowId != 0
                && q.JournalGenre.Value.JournalCategory.Value.JournalSection.RowId == 2));
        var unlocks = new Dictionary<uint, HashSet<uint>>();
        var scripted = new Dictionary<uint, HashSet<uint>>();
        var secondary = new Dictionary<uint, HashSet<uint>>();
        var questById = quests.ToDictionary(q => q.RowId);
        var unlockHints = new HashSet<uint>();
        void Add(Dictionary<uint, HashSet<uint>> map, uint instance, uint quest)
        {
            if (instance == 0) return;
            if (!map.TryGetValue(instance, out var ids)) map[instance] = ids = new();
            ids.Add(quest);
        }
        foreach (var q in quests)
        {
            // Removed/replaced quests remain in the sheet but have no journal genre.
            if (q.JournalGenre.RowId == 0) continue;
            Add(unlocks, q.InstanceContentUnlock.RowId, q.RowId);
            if (q.IsRepeatable) continue;
            foreach (var p in q.QuestParams)
            {
                var instruction = p.ScriptInstruction.ToString();
                if (instruction.StartsWith("UNLOCK_IMAGE", StringComparison.Ordinal)
                    || instruction.StartsWith("UNLOCK_ADD_NEW_CONTENT", StringComparison.Ordinal)) unlockHints.Add(q.RowId);
                // Numbered later entries may mention unrelated duties (e.g. Omega mentions Crystal Tower).
                if (instruction == "INSTANCEDUNGEON0")
                    Add(scripted, p.ScriptArg, q.RowId);
                else if (instruction.StartsWith("INSTANCEDUNGEON", StringComparison.Ordinal))
                    Add(secondary, p.ScriptArg, q.RowId);
            }
        }
        var activities = new List<Activity>();
        foreach (var d in duties)
        {
            if (d.ContentLinkType != 1 || d.Content.RowId == 0 || !d.IsInDutyFinder
                || d.ContentType.RowId is not (2 or 4 or 5) || string.IsNullOrWhiteSpace(d.Name.ToString())
                || d.Name.ToString().StartsWith("Special Event", StringComparison.Ordinal)) continue;
            var name = d.Name.ToString();
            var kind = d.ContentType.RowId switch { 2 => ActivityKind.Dungeon, 4 => ActivityKind.Trial,
                _ => d.AllianceRoulette ? ActivityKind.AllianceRaid : ActivityKind.Raid };
            var exact = unlocks.GetValueOrDefault(d.Content.RowId);
            var candidates = exact ?? scripted.GetValueOrDefault(d.Content.RowId) ?? new();
            if (exact == null && candidates.Any(unlockHints.Contains)) candidates = candidates.Where(unlockHints.Contains).ToHashSet();
            // Secondary references need matching expansion/level and either a story quest or unlock UI evidence.
            // They never override a primary link: Omega's mention of an ARR raid is not an ARR unlock route.
            if (candidates.Count == 0 && secondary.TryGetValue(d.Content.RowId, out var alternatives))
                candidates = alternatives.Where(id => (unlockHints.Contains(id)
                        || questById[id].JournalGenre.Value.JournalCategory.Value.JournalSection.RowId <= 2)
                    && questById[id].Expansion.RowId == d.RequiredExVersion.RowId
                    && questById[id].ClassJobLevel[0] <= d.ClassJobLevelRequired).ToHashSet();
            // Prefer the earliest quest mentioning this duty. Later references often send players back to it.
            var roots = candidates.Where(id => !HasAncestor(id, candidates, new HashSet<uint>())).Order().ToArray();
            var advanced = d.HighEndDuty || name.Contains("(Extreme)", StringComparison.OrdinalIgnoreCase)
                || name.Contains("(Savage)", StringComparison.OrdinalIgnoreCase)
                || name.Contains("(Unreal)", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("The Minstrel's Ballad:", StringComparison.OrdinalIgnoreCase);
            activities.Add(new(d.RowId, d.Content.RowId, name, kind, d.RequiredExVersion.RowId,
                d.RequiredExVersion.Value.Name.ToString(), d.ClassJobLevelRequired, d.ItemLevelRequired,
                advanced, roots, exact != null ? "Quest.InstanceContentUnlock" : roots.Length > 0 ? "Quest.INSTANCEDUNGEON" : "unmapped"));
        }
        // The sheet retains the old fan-event Araya alongside its released version.
        // Drop an unlinked duplicate only when a quest-linked duty of the same name exists.
        Activities = activities.Where(a => a.QuestIds.Length != 0 || !activities.Any(other => other.Id != a.Id
                && other.QuestIds.Length > 0 && string.Equals(a.Name, other.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.Expansion).ThenBy(a => a.Level).ThenBy(a => a.Id).ToArray();
        var relevant = new HashSet<uint>();
        Stories = StoryCatalog.Build(Quests, Activities);
        void Include(uint id)
        {
            if (!relevant.Add(id) || !Quests.TryGetValue(id, out var q)) return;
            foreach (var p in q.Previous) Include(p);
        }
        foreach (var id in Activities.SelectMany(a => a.QuestIds)) Include(id);
        foreach (var id in Stories.SelectMany(s => s.QuestIds)) Include(id);
        RelevantQuests = relevant;
    }

    private bool HasAncestor(uint id, HashSet<uint> candidates, HashSet<uint> seen)
    {
        if (!seen.Add(id) || !Quests.TryGetValue(id, out var q)) return false;
        return q.Previous.Any(p => candidates.Contains(p) || HasAncestor(p, candidates, seen));
    }

    private static bool IsPartial(Quest q) => q.Unknown7 != 0
        || q.QuestLock.Any(p => p.RowId != 0) || q.InstanceContent.Any(p => p.RowId != 0)
        || q.ClassJobCategory1.RowId != 0 || q.ClassJobLevel[1] != 0
        || q.GrandCompany.RowId != 0 || q.GrandCompanyRank.RowId != 0
        || q.BeastTribe.RowId != 0 || q.SatisfactionNpc.RowId != 0 || q.IsHouseRequired
        || q.MountRequired.RowId != 0 || q.ClassJobRequired.RowId != 0
        || q.BellStart != q.BellEnd || q.Festival.RowId != 0 || q.LevelMax != 0;
}
