using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;

namespace EorzeaEventTracker;

public sealed class CharacterProgress
{
    private readonly Plugin plugin;
    public IReadOnlyDictionary<string, TrackingDefinition> Catalog { get; private set; }
    private readonly IReadOnlyDictionary<string, TrackingDefinition> bundledCatalog;
    private EventFeed? catalogFeed;
    public Dictionary<uint, bool> Quests { get; } = new();
    public Dictionary<uint, RewardProgress> Rewards { get; } = new();
    public bool Available { get; private set; }
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
            Rewards.Clear();
            var quantities = new Dictionary<uint, int>();
            foreach (var type in Containers)
                foreach (var item in Plugin.Inventory.GetInventoryItems(type))
                    if (!item.IsEmpty && item.Quantity > 0)
                        quantities[item.BaseItemId] = quantities.GetValueOrDefault(item.BaseItemId) + (int)item.Quantity;
            var observed = ProgressPolicy.ForCharacter(plugin.Settings.ObservedRewards, contentId);
            var definitions = feed.Events.Where(e => e.Active(DateTimeOffset.UtcNow) || e.Upcoming(DateTimeOffset.UtcNow))
                .Select(e => Catalog.GetValueOrDefault(e.Id)).OfType<TrackingDefinition>().ToList();
            var quests = Plugin.Data.GetExcelSheet<Quest>();
            foreach (var quest in definitions.SelectMany(d => d.Quests))
                if (quests.TryGetRow(quest.Id, out var row)) Quests[quest.Id] = Plugin.Unlocks.IsQuestCompleted(row);
            var items = Plugin.Data.GetExcelSheet<Item>();
            foreach (var reward in definitions.SelectMany(d => d.Rewards))
            {
                if (!items.TryGetRow(reward.Id, out var row)) { Rewards[reward.Id] = new(RewardState.Unknown); continue; }
                bool? unlocked = Plugin.Unlocks.IsItemUnlockable(row) ? Plugin.Unlocks.IsItemUnlocked(row) : null;
                var state = ProgressPolicy.Resolve(true, unlocked, quantities.GetValueOrDefault(reward.Id),
                    reward.EarnedByQuest != 0 && Quests.GetValueOrDefault(reward.EarnedByQuest)
                        && GuaranteesReward(reward.EarnedByQuest, reward.Id), observed.Contains(reward.Id));
                Rewards[reward.Id] = state;
                if (state.Confirmed && observed.Add(reward.Id)) dirty = true;
            }
            Available = true;
            if (dirty && now >= nextSave) { Flush(); nextSave = now.AddSeconds(10); }
        }
        catch (Exception ex)
        {
            Clear();
            if (now >= nextError) { Plugin.Log.Error(ex, "Character progress could not be read"); nextError = now.AddMinutes(1); }
        }
    }

    private void Clear() { Available = false; Quests.Clear(); Rewards.Clear(); }
    private static bool GuaranteesReward(uint questId, uint itemId)
    {
        if (!Plugin.Data.GetExcelSheet<Quest>().TryGetRow(questId, out var row)) return false;
        for (var i = 0; i < row.Reward.Count; i++)
            if (row.Reward[i].RowId == itemId && row.ItemCountReward[i] > 0) return true;
        return false;
    }
    public void Flush() { if (!dirty) return; plugin.Save(); dirty = false; }
}
