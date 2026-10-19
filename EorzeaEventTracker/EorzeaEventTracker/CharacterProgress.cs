using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace EorzeaEventTracker;

public sealed class CharacterProgress
{
    private readonly Plugin plugin;
    public IReadOnlyDictionary<string, TrackingDefinition> Catalog { get; private set; }
    private readonly IReadOnlyDictionary<string, TrackingDefinition> bundledCatalog;
    private EventFeed? catalogFeed;
    public Dictionary<uint, bool> Quests { get; } = new();
    public Dictionary<uint, QuestAccess> QuestAccess { get; } = new();
    public Dictionary<uint, RewardProgress> Rewards { get; } = new();
    public Dictionary<uint, RewardCategory> Categories { get; } = new();
    public Dictionary<uint, long> CurrencyBalances { get; } = new();
    private readonly Dictionary<uint, RewardProgress> automaticRewards = new();
    public bool Available { get; private set; }
    public bool ReadyForReminders => Available && contentId == Plugin.Player.ContentId
        && ReferenceEquals(catalogFeed, plugin.Feed.Feed);
    public string Character { get; private set; } = "Waiting for character";
    private ulong contentId;
    private DateTime nextScan;
    private DateTime readyAfter;
    private bool dirty;
    private DateTime nextSave;
    private DateTime nextError;
    // Deliberately exclude retainers and other remotely loaded containers.
    private static readonly GameInventoryType[] Containers = Enum.GetValues<GameInventoryType>()
        .Where(t => t.ToString() is "Inventory1" or "Inventory2" or "Inventory3" or "Inventory4" or "EquippedItems"
            || t.ToString().StartsWith("Armory", StringComparison.Ordinal)).ToArray();
    public string? BundledAsset(string eventId) => bundledCatalog.TryGetValue(eventId, out var definition) ? definition.Asset : null;

    public CharacterProgress(Plugin plugin)
    {
        this.plugin = plugin;
        var file = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "tracking.json");
        var definitions = JsonSerializer.Deserialize<List<TrackingDefinition>>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        bundledCatalog = definitions.ToDictionary(d => d.EventId);
        Catalog = FeedPolicy.Catalog(plugin.Feed.Feed, bundledCatalog);
    }

    public void Update()
    {
        var feed = plugin.Feed.Feed;
        if (!ReferenceEquals(catalogFeed, feed))
        {
            Catalog = FeedPolicy.Catalog(feed, bundledCatalog);
            catalogFeed = feed;
            Clear();
            nextScan = readyAfter > DateTime.UtcNow ? readyAfter : DateTime.UtcNow;
        }
        var player = Plugin.Player;
        if (!Plugin.Client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0)
        {
            Flush();
            contentId = 0;
            Clear();
            Character = "Log in to track this character";
            return;
        }
        if (contentId != player.ContentId)
        {
            Flush();
            contentId = player.ContentId;
            Clear();
            Character = player.CharacterName;
            readyAfter = DateTime.UtcNow.AddSeconds(5);
            nextScan = readyAfter;
        }
        var now = DateTime.UtcNow;
        if (now < nextScan) return;
        nextScan = now.AddSeconds(1);
        try
        {
            Quests.Clear();
            QuestAccess.Clear();
            Rewards.Clear();
            Categories.Clear();
            var quantities = new Dictionary<uint, int>();
            foreach (var type in Containers)
                foreach (var item in Plugin.Inventory.GetInventoryItems(type))
                    if (!item.IsEmpty && item.Quantity > 0)
                        quantities[item.BaseItemId] = quantities.GetValueOrDefault(item.BaseItemId) + (int)item.Quantity;
            var observed = ProgressPolicy.ForCharacter(plugin.Settings.ObservedRewards, contentId);
            var manual = ProgressPolicy.ForCharacter(plugin.Settings.ManualRewards, contentId);
            automaticRewards.Clear();
            var definitions = feed.Events.Where(e => e.Active(DateTimeOffset.UtcNow) || e.Upcoming(DateTimeOffset.UtcNow))
                .Select(e => Catalog.GetValueOrDefault(e.Id)).OfType<TrackingDefinition>().ToList();
            var quests = Plugin.Data.GetExcelSheet<Quest>();
            foreach (var quest in definitions.SelectMany(d => d.Quests))
                if (quests.TryGetRow(quest.Id, out var row))
                {
                    Quests[quest.Id] = Plugin.Unlocks.IsQuestCompleted(row);
                    QuestAccess[quest.Id] = GameQuestAccess.Read(quest.Id);
                }
            var items = Plugin.Data.GetExcelSheet<Item>();
            var actions = Plugin.Data.GetExcelSheet<ItemAction>();
            foreach (var reward in definitions.SelectMany(d => d.Rewards))
            {
                if (!items.TryGetRow(reward.Id, out var row))
                {
                    automaticRewards[reward.Id] = new(RewardState.Unknown);
                    Rewards[reward.Id] = ProgressPolicy.ApplyManual(new(RewardState.Unknown), manual.Contains(reward.Id));
                    continue;
                }
                var actionId = row.ItemAction.RowId != 0 && actions.TryGetRow(row.ItemAction.RowId, out var action) ? action.Action.RowId : 0;
                Categories[reward.Id] = RewardCategoryPolicy.Classify(row.EquipSlotCategory.RowId, row.ItemUICategory.RowId, actionId);
                bool? unlocked = Plugin.Unlocks.IsItemUnlockable(row) ? Plugin.Unlocks.IsItemUnlocked(row) : null;
                var state = ProgressPolicy.Resolve(true, unlocked, quantities.GetValueOrDefault(reward.Id),
                    reward.EarnedByQuest != 0 && Quests.GetValueOrDefault(reward.EarnedByQuest)
                        && GuaranteesReward(reward.EarnedByQuest, reward.Id), observed.Contains(reward.Id));
                automaticRewards[reward.Id] = state;
                Rewards[reward.Id] = ProgressPolicy.ApplyManual(state, manual.Contains(reward.Id));
                if (state.Confirmed && observed.Add(reward.Id)) dirty = true;
            }
            ScanCurrencies(definitions);
            Available = true;
            if (dirty && now >= nextSave) { Flush(); nextSave = now.AddSeconds(10); }
        }
        catch (Exception ex)
        {
            Clear();
            if (now >= nextError) { Plugin.Log.Error(ex, "Character progress could not be read"); nextError = now.AddMinutes(1); }
        }
    }

    public bool IsManuallyObtained(uint id) => ReadyForReminders && plugin.Settings.ManualRewards.TryGetValue(
        contentId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var rewards) && rewards.Contains(id);

    public void SetManuallyObtained(uint id, bool obtained)
    {
        if (!ReadyForReminders) return;
        var manual = ProgressPolicy.ForCharacter(plugin.Settings.ManualRewards, contentId);
        if (obtained) manual.Add(id); else manual.Remove(id);
        Rewards[id] = ProgressPolicy.ApplyManual(automaticRewards.GetValueOrDefault(id), obtained);
        plugin.Save();
    }

    private unsafe void ScanCurrencies(IEnumerable<TrackingDefinition> definitions)
    {
        CurrencyBalances.Clear();
        var currency = CurrencyManager.Instance();
        var inventory = InventoryManager.Instance();
        foreach (var id in definitions.SelectMany(d => d.Rewards).Where(r => r.Price > 0)
            .Select(FavoriteBudget.CurrencyId).Where(id => id != 0).Distinct())
        {
            if (!Plugin.Data.GetExcelSheet<Item>().TryGetRow(id, out _)) continue;
            if (currency != null && currency->HasItem(id)) CurrencyBalances[id] = currency->GetItemCount(id);
            // MGF lives in CurrencyManager, never treat an unavailable bucket as an empty bag.
            else if (id != 41629 && inventory != null)
            {
                var count = inventory->GetInventoryItemCount(id, false, false, false);
                if (count >= 0) CurrencyBalances[id] = count;
            }
        }
    }

    private void Clear() { CurrencyBalances.Clear(); Available = false; Categories.Clear(); QuestAccess.Clear(); Quests.Clear(); Rewards.Clear(); automaticRewards.Clear(); }
    private static bool GuaranteesReward(uint questId, uint itemId)
    {
        if (!Plugin.Data.GetExcelSheet<Quest>().TryGetRow(questId, out var row)) return false;
        for (var i = 0; i < row.Reward.Count; i++)
            if (row.Reward[i].RowId == itemId && row.ItemCountReward[i] > 0) return true;
        return false;
    }
    public void Flush() { if (!dirty) return; plugin.Save(); dirty = false; }
}
