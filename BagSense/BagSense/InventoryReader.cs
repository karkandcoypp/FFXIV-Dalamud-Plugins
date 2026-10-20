using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace BagSense;

public static class InventoryReader
{
    private static readonly GameInventoryType[] Containers = Enum.GetValues<GameInventoryType>()
        .Where(t => t.ToString() is "Inventory1" or "Inventory2" or "Inventory3" or "Inventory4" or "EquippedItems"
            || (t.ToString().StartsWith("Armory", StringComparison.Ordinal) && t != GameInventoryType.ArmorySoulCrystal)).ToArray();

    public static InventorySnapshot Read(ItemCatalog catalog)
    {
        ulong id = Plugin.Player.ContentId;
        if (!Plugin.Client.IsLoggedIn || !Plugin.Player.IsLoaded || id == 0) throw new InvalidOperationException("No character loaded");
        var stacks = new List<InventoryStack>();
        int capacity = 0;
        foreach (var type in Containers)
        {
            var contents = Plugin.Inventory.GetInventoryItems(type);
            var kind = type == GameInventoryType.EquippedItems ? ContainerKind.Equipped
                : type.ToString().StartsWith("Armory", StringComparison.Ordinal) ? ContainerKind.Armoury : ContainerKind.Bag;
            if (kind == ContainerKind.Bag) capacity += contents.Length;
            foreach (var item in contents)
            {
                if (item.IsEmpty || item.Quantity <= 0) continue;
                bool special = item.IsRelic || item.GlamourId != 0;
                foreach (var materia in item.Materia) if (materia != 0) special = true;
                foreach (var stain in item.Stains) if (stain != 0) special = true;
                stacks.Add(new(item.BaseItemId, kind, (int)item.InventorySlot, item.Quantity, item.IsHq, item.IsCollectable, special));
            }
        }
        if (capacity < 140) throw new InvalidOperationException("Inventory containers are not fully loaded");
        var facts = new Dictionary<uint, ItemFacts>();
        var itemSheet = Plugin.Data.GetExcelSheet<Item>();
        foreach (var itemId in stacks.Select(s => s.ItemId).Distinct())
        {
            var fact = catalog.Find(itemId);
            if (fact == null) continue;
            bool? unlocked = null;
            if (itemSheet.TryGetRow(itemId, out var row) && Plugin.Unlocks.IsItemUnlockable(row))
                unlocked = Plugin.Unlocks.IsItemUnlocked(row);
            facts[itemId] = fact with { Unlocked = unlocked };
        }
        var (known, gearsets) = ReadGearsets(id);
        if (id != Plugin.Player.ContentId || !Plugin.Client.IsLoggedIn) throw new InvalidOperationException("Character changed during scan");
        return new(id, Plugin.Player.CharacterName, DateTimeOffset.UtcNow, stacks, facts, gearsets, known, capacity);
    }

    private static unsafe (bool Known, Dictionary<uint, string[]> Items) ReadGearsets(ulong characterId)
    {
        var found = new Dictionary<uint, HashSet<string>>();
        try
        {
            var module = RaptureGearsetModule.Instance();
            if (module == null || module->CharacterContentId != characterId) return (false, new());
            foreach (ref var entry in module->Entries)
            {
                if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0) continue;
                string name = entry.NameString;
                foreach (ref var item in entry.Items)
                {
                    Add(item.ItemId, name);
                    Add(item.GlamourId, name);
                }
            }
            return (true, found.ToDictionary(p => p.Key, p => p.Value.ToArray()));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "BagSense: gearsets unavailable; equipment stays protected");
            return (false, new());
        }
        void Add(uint item, string name)
        {
            uint id = item % 1_000_000;
            if (id == 0) return;
            if (!found.TryGetValue(id, out var names)) found[id] = names = new();
            names.Add(name);
        }
    }
}
