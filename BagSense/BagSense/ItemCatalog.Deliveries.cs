using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace BagSense;
public sealed partial class ItemCatalog
{
    private void IndexDeliveries(ExcelModule excel)
    {
        void Add(uint item, DeliveryUse use)
        {
            if (item == 0 || use.Count <= 0) return;
            if (!deliveries.TryGetValue(item, out var list)) deliveries[item] = list = new();
            if (!list.Contains(use)) list.Add(use);
        }
        NpcLocation[] At(Level level) => World.Positions.GetValueOrDefault(level.Object.RowId)?
            .Where(p => p.Map == level.Map.RowId && p.Territory == level.Territory.RowId).ToArray() ?? [];
        var leves = excel.GetSheet<Leve>(Language.English);
        foreach (var craft in excel.GetSheet<CraftLeve>(Language.English))
        {
            if (!leves.TryGetRow(craft.Leve.RowId, out var leve) || leve.Name.IsEmpty) continue;
            // CraftLeve uses LevelLevemete for the delivery contact, as does Teamcraft.
            var contacts = leve.LevelLevemete.RowId == 0 ? [] : At(leve.LevelLevemete.Value);
            var itemRewards = new List<DeliveryReward>();
            if (leve.LeveRewardItem.IsValid)
            {
                var table = leve.LeveRewardItem.Value;
                for (int group = 0; group < table.LeveRewardItemGroup.Count; group++)
                {
                    if (!table.LeveRewardItemGroup[group].IsValid || table.ProbabilityPercent[group] == 0) continue;
                    var rewardGroup = table.LeveRewardItemGroup[group].Value;
                    for (int reward = 0; reward < rewardGroup.Item.Count; reward++)
                        if (rewardGroup.Item[reward].RowId != 0 && rewardGroup.Count[reward] > 0)
                            itemRewards.Add(new(rewardGroup.Item[reward].RowId, rewardGroup.Count[reward], rewardGroup.IsHQ[reward], table.ProbabilityPercent[group]));
                }
            }
            for (int i = 0; i < Math.Min(craft.Item.Count, craft.ItemCount.Count); i++)
                Add(craft.Item[i].RowId, new(leve.RowId, "Leve", leve.Name.ToString(), craft.ItemCount[i], "any",
                    $"{leve.ExpReward:N0} EXP · {leve.GilReward:N0} gil", contacts, false, craft.Repeats, ItemRewards: itemRewards.ToArray(), Requirements: $"Lv {leve.ClassJobLevel} · {leve.ClassJobCategory.Value.Name}"));
        }
        var supply = excel.GetSubrowSheet<SatisfactionSupply>(Language.English);
        foreach (var npc in excel.GetSheet<SatisfactionNpc>(Language.English))
        {
            if (npc.Npc.RowId == 0) continue;
            var positions = World.Positions.GetValueOrDefault(npc.Npc.RowId) ?? [];
            string name = npc.Npc.Value.Singular.ToString();
            foreach (var rank in npc.SatisfactionNpcParams)
            {
                // These are possible weekly items, never a claim about this week's request.
                if (rank.SupplyIndex <= 0 || !supply.TryGetRow((uint)rank.SupplyIndex, out var entries)) continue;
                foreach (var entry in entries)
                    if (entry.Item.RowId != 0 && entry.Reward.IsValid)
                    {
                        var reward = entry.Reward.Value;
                        string description = $"{reward.GilLow}–{reward.GilHigh} gil";
                        var currencies = reward.SatisfactionSupplyRewardData.Where(r => r.RewardCurrency != 0)
                            .Select(r => $"Scrips: {r.QuantityLow}–{r.QuantityHigh}");
                        description += " · " + string.Join(" / ", currencies);
                        Add(entry.Item.RowId, new(npc.RowId, "CustomDelivery", name, 1, "collectable", description,
                            positions, Collectability: entry.CollectabilityLow, Requirements: $"Lv {npc.LevelUnlock}"));
                    }
            }
        }
        var appraisers = World.Positions.Values.SelectMany(p => p).Where(p => p.Npc.Equals("Collectable Appraiser", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var group in excel.GetSubrowSheet<CollectablesShopItem>(Language.English))
            foreach (var entry in group)
            {
                if (entry.Item.RowId == 0 || !entry.CollectablesShopRefine.IsValid) continue;
                var refine = entry.CollectablesShopRefine.Value;
                string reward = "";
                if (entry.CollectablesShopRewardScrip.IsValid)
                {
                    var scrip = entry.CollectablesShopRewardScrip.Value;
                    reward = $"{refine.LowCollectability} → {scrip.LowReward} / {refine.MidCollectability} → {scrip.MidReward} / {refine.HighCollectability} → {scrip.HighReward} scrips";
                }
                Add(entry.Item.RowId, new(entry.RowId, "Collectable", "Collectables", 1, "collectable", reward, appraisers,
                    Collectability: refine.LowCollectability, Requirements: $"Lv {entry.LevelMin}–{entry.LevelMax}" + (entry.RequiredQuest.IsValid && !entry.RequiredQuest.Value.Name.IsEmpty ? $" · {entry.RequiredQuest.Value.Name}" : "")));
            }
    }
}
