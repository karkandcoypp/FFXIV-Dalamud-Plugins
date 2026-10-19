using System.Collections.Generic;
using System.Numerics;
using Lumina.Excel.Sheets;

namespace EorzeaEventTracker;

internal readonly record struct ItemUiAsset(uint Icon, byte Rarity, string? Description);
internal readonly record struct QuestUiAsset(uint Available, uint Invalid);
internal static class GameUiAssets
{
    private static readonly Dictionary<uint, ItemUiAsset> Items = new();
    private static readonly Dictionary<uint, QuestUiAsset> Quests = new();
    public static ItemUiAsset Item(uint id)
    {
        if (id == 0) return default;
        if (Items.TryGetValue(id, out var cached)) return cached;
        if (!Plugin.Data.GetExcelSheet<Item>().TryGetRow(id, out var row)) return default;
        return Items[id] = new(row.Icon, row.Rarity, row.Description.ToString());
    }
    public static uint QuestIcon(uint id, bool blocked = false)
    {
        if (id == 0) return 0;
        if (!Quests.TryGetValue(id, out var asset))
        {
            if (!Plugin.Data.GetExcelSheet<Quest>().TryGetRow(id, out var quest)) return 0;
            if (Plugin.Data.GetExcelSheet<EventIconType>().TryGetRow(quest.EventIconType.RowId, out var icons))
                asset = new(icons.MapIconAvailable, icons.MapIconInvalid);
            if (asset.Available == 0) asset = new(quest.Icon, quest.Icon);
            Quests[id] = asset;
        }
        return blocked && asset.Invalid != 0 ? asset.Invalid : asset.Available;
    }
    public static Vector4 RarityColor(byte rarity) => rarity switch
    {
        2 => new(.50f, .80f, .52f, 1), // green dungeon equipment
        3 => new(.48f, .67f, .95f, 1), // blue rare equipment
        4 => new(.76f, .58f, .93f, 1), // purple relic equipment
        7 => new(.91f, .61f, .75f, 1), // pink aetherial equipment
        _ => new(.79f, .79f, .75f, 1),
    };
}
