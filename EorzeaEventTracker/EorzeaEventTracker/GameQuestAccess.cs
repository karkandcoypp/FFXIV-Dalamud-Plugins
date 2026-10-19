using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace EorzeaEventTracker;

internal static class GameQuestAccess
{
    public static QuestAccess Read(uint id)
    {
        try { return ReadCore(id); }
        catch { return new(0, true, 0, Array.Empty<QuestRequirement>(), true, true, false); }
    }

    private static unsafe QuestAccess ReadCore(uint id)
    {
        var sheet = Plugin.Data.GetExcelSheet<Quest>(ClientLanguage.English);
        if (!sheet.TryGetRow(id, out var row)) return new(0, true, 0, Array.Empty<QuestRequirement>(), true, true, false);
        var previous = new List<QuestRequirement>();
        var partial = row.Unknown7 != 0;
        foreach (var reference in row.PreviousQuest)
        {
            if (reference.RowId == 0) continue;
            if (!sheet.TryGetRow(reference.RowId, out var quest)) { partial = true; continue; }
            previous.Add(new(quest.RowId, quest.Name.ToString(), Plugin.Unlocks.IsQuestCompleted(quest)));
        }
        // A nonzero sequence can permit an unfinished prerequisite; don't claim it blocks access.
        var join = row.Unknown7 != 0 ? (byte)0 : row.PreviousQuestJoin;
        var category = row.ClassJobCategory0;
        var classMet = true;
        var className = "";
        if (category.RowId != 0 && category.RowId != 1)
        {
            var job = Plugin.Data.GetExcelSheet<ClassJob>(ClientLanguage.English).GetRow(Plugin.Player.ClassJob.RowId);
            className = Plugin.Data.GetExcelSheet<ClassJobCategory>(ClientLanguage.English).GetRow(category.RowId).Name.ToString();
            var property = typeof(ClassJobCategory).GetProperty(job.Abbreviation.ToString());
            if (property?.GetValue(category.Value) is bool allowed) classMet = allowed;
            else partial = true;
        }
        // These conditions have additional game-specific semantics. Keep the result explicitly partial.
        partial |= row.QuestLock.Any(q => q.RowId != 0) || row.InstanceContent.Any(q => q.RowId != 0)
            || row.ClassJobCategory1.RowId != 0 || row.ClassJobLevel[1] != 0
            || row.GrandCompany.RowId != 0 || row.GrandCompanyRank.RowId != 0
            || row.BeastTribe.RowId != 0 || row.SatisfactionNpc.RowId != 0 || row.IsHouseRequired
            || row.MountRequired.RowId != 0 || row.ClassJobRequired.RowId != 0
            || row.BellStart != row.BellEnd || row.Festival.RowId != 0 || row.LevelMax != 0;
        return new(row.ClassJobLevel[0], Plugin.Player.Level >= row.ClassJobLevel[0], join, previous, partial, classMet, true, className, row.Festival.RowId != 0, QuestManager.Instance() != null && QuestManager.Instance()->IsQuestAccepted(id));
    }

    public static EventLocation? Location(uint id)
    {
        try { return LocationCore(id); }
        catch { return null; }
    }

    private static EventLocation? LocationCore(uint id)
    {
        if (!Plugin.Data.GetExcelSheet<Quest>(ClientLanguage.English).TryGetRow(id, out var quest)
            || !Plugin.Data.GetExcelSheet<Level>().TryGetRow(quest.IssuerLocation.RowId, out var level)
            || level.Type != 8 || level.Object.RowId != quest.IssuerStart.RowId
            || !Plugin.Data.GetExcelSheet<Map>().TryGetRow(level.Map.RowId, out var map)
            || map.SizeFactor == 0 || map.TerritoryType.RowId != level.Territory.RowId) return null;
        float Coordinate(float world, short offset) => (float)(41 / (map.SizeFactor / 100d)
            * (((world + offset) * map.SizeFactor / 100d + 1024) / 2048) + 1);
        var npc = Plugin.Data.GetExcelSheet<ENpcResident>(ClientLanguage.English);
        return new EventLocation { Id = "quest-" + id, MapId = map.RowId, TerritoryId = level.Territory.RowId,
            Npc = npc.TryGetRow(level.Object.RowId, out var resident) ? resident.Singular.ToString() : "",
            Zone = Plugin.Data.GetExcelSheet<Map>(ClientLanguage.English).GetRow(map.RowId).PlaceName.Value.Name.ToString(),
            X = Coordinate(level.X, map.OffsetX), Y = Coordinate(level.Z, map.OffsetY) };
    }
}


