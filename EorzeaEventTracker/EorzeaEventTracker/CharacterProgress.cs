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
    public IReadOnlyDictionary<string, TrackingDefinition> Catalog { get; }
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

    public CharacterProgress(Plugin plugin)
    {
        this.plugin = plugin;
        var file = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "tracking.json");
        var definitions = JsonSerializer.Deserialize<List<TrackingDefinition>>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        Catalog = definitions.ToDictionary(d => d.EventId);
    }

    public void Update()
    {
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
            var quests = Plugin.Data.GetExcelSheet<Quest>();
            foreach (var quest in Catalog.Values.SelectMany(d => d.Quests))
                if (quests.TryGetRow(quest.Id, out var row)) Quests[quest.Id] = Plugin.Unlocks.IsQuestCompleted(row);
            var items = Plugin.Data.GetExcelSheet<Item>();
            foreach (var reward in Catalog.Values.SelectMany(d => d.Rewards))
            {
                if (!items.TryGetRow(reward.Id, out var row)) { Rewards[reward.Id] = new(RewardState.Unknown); continue; }
                bool? unlocked = Plugin.Unlocks.IsItemUnlockable(row) ? Plugin.Unlocks.IsItemUnlocked(row) : null;
                var state = ProgressPolicy.Resolve(true, unlocked, quantities.GetValueOrDefault(reward.Id),
                    reward.EarnedByQuest != 0 && Quests.GetValueOrDefault(reward.EarnedByQuest), observed.Contains(reward.Id));
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
    public void Flush() { if (!dirty) return; plugin.Save(); dirty = false; }
}
