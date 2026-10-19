using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace EorzeaCompass;

public enum RewardKind { Guaranteed, Choice }
public sealed record QuestReward(uint QuestId, uint ItemId, string Name, int Count, RewardKind Kind,
    string Category, int ItemLevel, int EquipLevel, uint Icon, string[] StoryIds);

public static class RewardCatalog
{
    // Only explicitly typed items with a positive quantity are actual listed rewards.
    // Zero-count script placeholders must never become guaranteed class-specific loot.
    public static bool IsListedItem(bool isItem, uint itemId, int count) => isItem && itemId != 0 && count > 0;
    public static string Status(QuestReward reward, QuestObservation? observation) => observation?.Complete == true
        ? reward.Kind == RewardKind.Choice ? "choice-completed" : "quest-completed"
        : observation == null ? "unknown" : "pending";

    public static IReadOnlyList<QuestReward> Build(IEnumerable<Quest> quests, IReadOnlyList<Story> stories)
    {
        var links = stories.SelectMany(s => s.QuestIds.Select(q => (Quest: q, Id: s.Definition.Id)))
            .GroupBy(x => x.Quest).ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToArray());
        var result = new List<QuestReward>();
        foreach (var q in quests.Where(q => links.ContainsKey(q.RowId)))
        {
            void Add(Item item, int count, RewardKind kind)
            {
                if (!IsListedItem(true, item.RowId, count) || string.IsNullOrWhiteSpace(item.Name.ToString())) return;
                result.Add(new(q.RowId, item.RowId, item.Name.ToString(), count, kind,
                    item.ItemUICategory.Value.Name.ToString(), (int)item.LevelItem.RowId,
                    item.EquipSlotCategory.RowId == 0 ? 0 : item.LevelEquip, item.Icon, links[q.RowId]));
            }
            for (int i = 0; i < q.Reward.Count; i++)
                if (IsListedItem(q.Reward[i].Is<Item>(), q.Reward[i].RowId, q.ItemCountReward[i])
                    && q.Reward[i].TryGetValue<Item>(out var item))
                    Add(item, q.ItemCountReward[i], RewardKind.Guaranteed);
            for (int i = 0; i < q.OptionalItemReward.Count; i++)
                if (q.OptionalItemReward[i].RowId != 0 && q.OptionalItemCountReward[i] > 0)
                    Add(q.OptionalItemReward[i].Value, q.OptionalItemCountReward[i], RewardKind.Choice);
        }
        return result.DistinctBy(r => (r.QuestId, r.ItemId, r.Kind)).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
