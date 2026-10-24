using Dalamud.Game.Inventory;
using Dalamud.Game.Player;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NativePlayer = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using GameAction = Lumina.Excel.Sheets.Action;

namespace CraftProfile;

internal sealed class ProfileReader
{
    private Recipe[]? recipeRows;
    private int recipeCursor;
    private readonly Dictionary<uint, RecipeData> recipes = [];
    private string pendingIdentity = "";
    private static readonly GameInventoryType[] Containers = Enum.GetValues<GameInventoryType>().Where(t =>
        t.ToString() is "Inventory1" or "Inventory2" or "Inventory3" or "Inventory4" or "EquippedItems" or "Crystals" or "Currency" or "KeyItems"
        || t.ToString().StartsWith("Armory", StringComparison.Ordinal)
        || t.ToString() is "SaddleBag1" or "SaddleBag2" or "PremiumSaddleBag1" or "PremiumSaddleBag2" or "Cosmopouch1" or "Cosmopouch2").ToArray();
    public void Reset() { recipeRows = null; recipeCursor = 0; recipes.Clear(); pendingIdentity = ""; }
    public void RestartUnlocks() { recipeCursor = 0; recipes.Clear(); }
    public bool CatalogComplete => recipeRows != null && recipeCursor >= recipeRows.Length;
    public int RecipeCount => recipes.Count;
    // Amortize unlock inspection across frames; all native reads stay on the framework thread.
    public void AdvanceRecipes()
    {
        recipeRows ??= Plugin.Data.GetExcelSheet<Recipe>().Where(r => r.ItemResult.RowId != 0 && r.CraftType.RowId < 8).ToArray();
        int until = Math.Min(recipeCursor + 160, recipeRows.Length);
        for (; recipeCursor < until; recipeCursor++)
        {
            var row = recipeRows[recipeCursor];
            var level = row.RecipeLevelTable.Value;
            bool? unlocked;
            try { unlocked = Plugin.Unlocks.IsRecipeUnlocked(row); } catch { unlocked = null; }
            var materials = new Dictionary<uint, int>();
            for (int i = 0; i < row.AmountIngredient.Count; i++)
                if (row.AmountIngredient[i] > 0 && row.Ingredient[i].RowId is > 0 and < 1000000)
                    materials[row.Ingredient[i].RowId] = materials.GetValueOrDefault(row.Ingredient[i].RowId) + row.AmountIngredient[i];
            recipes[row.RowId] = new(row.RowId, row.ItemResult.RowId, row.CraftType.RowId + 8, level.ClassJobLevel,
                unlocked, row.SecretRecipeBook.RowId, row.RequiredCraftsmanship, row.RequiredControl,
                row.ItemRequired.RowId, row.StatusRequired.RowId, checked((int)(level.Difficulty * row.DifficultyFactor / 100)),
                checked((int)(level.Quality * row.QualityFactor / 100)), level.Durability * row.DurabilityFactor / 100,
                row.CanHq, row.IsExpert, row.IsSpecializationRequired, row.AmountResult, materials,
                row.MaterialQualityFactor, level.ProgressDivider, level.ProgressModifier, level.QualityDivider, level.QualityModifier);
        }
    }
    public unsafe ProfileDocument Read(ProfileDocument previous)
    {
        ulong owner = Plugin.Player.ContentId;
        if (!Plugin.Client.IsLoggedIn || !Plugin.Player.IsLoaded || owner == 0) throw new InvalidOperationException("Character unavailable");
        var native = NativePlayer.Instance();
        if (native == null || native->ContentId != owner || !native->IsLoaded) throw new InvalidOperationException("Player state unavailable");
        var result = new ProfileDocument
        {
            CharacterKey = owner.ToString(), CharacterName = Plugin.Player.CharacterName,
            CurrentWorldId = Plugin.Player.CurrentWorld.RowId, HomeWorldId = Plugin.Player.HomeWorld.RowId,
            CurrentJobId = Plugin.Player.ClassJob.RowId, GeneratedAt = DateTimeOffset.UtcNow,
            Observations = new(previous.Observations), UnbuffedObservations = new(previous.UnbuffedObservations),
            Recipes = new(recipes), RecipeCatalogComplete = CatalogComplete,
        };
        foreach (var job in Plugin.Data.GetExcelSheet<ClassJob>().Where(j => ProfilePolicy.IsProfession(j.RowId)))
        {
            bool? specialist = null;
            try { if (job.RowId <= 15) specialist = native->IsMeisterFlag(job.RowId); } catch { }
            result.Professions.Add(new(job.RowId, job.Abbreviation.ToString(), job.Name.ToString(),
                Plugin.Player.GetClassJobLevel(job), Plugin.Player.GetClassJobExperience(job),
                job.RowId <= 15 ? Plugin.Player.GetDesynthesisLevel(job) : null, specialist));
        }
        foreach (var book in Plugin.Data.GetExcelSheet<SecretRecipeBook>().Where(b => b.Item.RowId != 0))
        {
            bool? unlocked = null;
            try { unlocked = Plugin.Unlocks.IsSecretRecipeBookUnlocked(book); } catch { }
            result.MasterBooks.Add(new(book.RowId, book.Item.RowId, book.Item.Value.Name.ToString(), unlocked));
        }
        foreach (var book in Plugin.Data.GetExcelSheet<GatheringSubCategory>().Where(b => b.Item.RowId != 0))
        {
            bool? unlocked = null;
            try { unlocked = native->IsFolkloreBookUnlocked(book.Division); } catch { }
            result.Folklore.Add(new(book.RowId, book.FolkloreBook.ToString(), unlocked));
        }
        ReadInventory(result);
        result.SavedGearsets = ReadGearsets(owner);
        ReadAbilities(result);
        if (ProfilePolicy.IsProfession(result.CurrentJobId))
        {
            var equipped = Plugin.Inventory.GetInventoryItems(GameInventoryType.EquippedItems);
            var gear = new List<GearItem>();
            foreach (var item in equipped) if (!item.IsEmpty) gear.Add(ReadGear(item));
            var local = Plugin.Objects.LocalPlayer ?? throw new InvalidOperationException("Local character unavailable");
            var statuses = local.StatusList.Where(status => status.StatusId != 0).Select(status =>
                new StatusData(status.StatusId, status.GameData.Value.Name.ToString(), status.Param, status.RemainingTime)).ToList();
            var stats = new StatsData(Attribute(PlayerAttribute.Craftsmanship), Attribute(PlayerAttribute.Control),
                Attribute(PlayerAttribute.CraftingPoints), Attribute(PlayerAttribute.Gathering),
                Attribute(PlayerAttribute.Perception), Attribute(PlayerAttribute.GatheringPoints));
            var observation = new JobObservation(result.CurrentJobId, Plugin.Player.Level, result.GeneratedAt,
                "observed_equipped", ProfilePolicy.Fingerprint(gear), Plugin.Player.IsLevelSynced, stats, gear, statuses, result.CurrentJobId <= 15 ? native->IsMeisterFlagAndHasSoulStoneEquipped(result.CurrentJobId) : null);
            // Require two successive stable samples to avoid pairing new gear with old stats after a job switch.
            string identity = observation.JobId + ":" + observation.GearFingerprint + ":" + stats + ":" + observation.LevelSynced
                + ":" + string.Join(",", statuses.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.Param}"));
            if (identity == pendingIdentity) ProfilePolicy.MergeObservation(result, observation);
            pendingIdentity = identity;
        }
        if (Plugin.Player.ContentId != owner || Plugin.Player.ClassJob.RowId != result.CurrentJobId || !Plugin.Client.IsLoggedIn)
            throw new InvalidOperationException("Character or profession changed during capture; retrying");
        return result;
    }
    private static int Attribute(PlayerAttribute attribute) => Plugin.Player.GetAttribute(attribute);
    private static unsafe void ReadInventory(ProfileDocument profile)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) throw new InvalidOperationException("Inventory unavailable");
        foreach (var type in Containers)
        {
            var native = manager->GetInventoryContainer((InventoryType)(uint)type);
            bool loaded = native != null && native->IsLoaded;
            var slots = Plugin.Inventory.GetInventoryItems(type);
            loaded = loaded && slots.Length > 0;
            profile.InventoryCoverage.Add(new(type.ToString(), loaded, loaded ? slots.Length : 0));
            if (!loaded) continue;
            foreach (var item in slots)
            {
                if (item.IsEmpty || item.Quantity <= 0) continue;
                string name = Plugin.Data.GetExcelSheet<Item>().TryGetRow(item.BaseItemId, out var row) ? row.Name.ToString() : "";
                profile.Inventory.Add(new(type.ToString(), (int)item.InventorySlot, item.BaseItemId, name,
                    item.Quantity, item.IsHq, item.IsCollectable));
            }
        }
        if (profile.InventoryCoverage.Where(x => x.Container is "Inventory1" or "Inventory2" or "Inventory3" or "Inventory4").Sum(x => x.Slots) < 140
            || !profile.InventoryCoverage.Any(x => x.Container == "EquippedItems" && x.Loaded))
            throw new InvalidOperationException("Main inventory or equipment is not fully loaded; refusing empty snapshot");
    }
    private static GearItem ReadGear(GameInventoryItem item) => ReadGear((int)item.InventorySlot,
        item.BaseItemId, item.IsHq, item.Materia, item.MateriaGrade, item.Condition);
    private static GearItem ReadGear(int slot, uint itemId, bool hq, ReadOnlySpan<ushort> materia,
        ReadOnlySpan<byte> grades, uint? condition)
    {
        bool known = Plugin.Data.GetExcelSheet<Item>().TryGetRow(itemId, out var row);
        var melds = new List<MateriaData>();
        for (int i = 0; i < materia.Length; i++)
        {
            ushort type = materia[i]; byte grade = i < grades.Length ? grades[i] : (byte)0;
            if (type == 0) continue;
            uint? id = null, stat = null; string? name = null; int? value = null;
            if (Plugin.Data.GetExcelSheet<Materia>().TryGetRow(type, out var m) && grade < m.Item.Count && grade < m.Value.Count)
            {
                id = m.Item[grade].RowId; name = m.Item[grade].Value.Name.ToString();
                stat = m.BaseParam.RowId; value = m.Value[grade];
            }
            melds.Add(new(i, type, grade, id, name, stat, value));
        }
        return new(slot, itemId, known ? row.Name.ToString() : $"Item #{itemId}", hq, known ? row.LevelItem.RowId : 0, condition, melds,
            known ? row.MateriaSlotCount : null, known ? row.IsAdvancedMeldingPermitted : null);
    }
    private static unsafe List<GearsetData> ReadGearsets(ulong owner)
    {
        var module = RaptureGearsetModule.Instance();
        if (module == null || module->CharacterContentId != owner) throw new InvalidOperationException("Saved gearsets not loaded");
        var result = new List<GearsetData>();
        foreach (ref var entry in module->Entries)
        {
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0 || !ProfilePolicy.IsProfession(entry.ClassJob)) continue;
            var items = new List<GearItem>(); int slot = 0;
            foreach (ref var item in entry.Items)
            {
                uint id = item.ItemId % 1000000;
                if (id != 0) items.Add(ReadGear(slot, id, item.ItemId >= 1000000, item.Materia, item.MateriaGrades, null));
                slot++;
            }
            result.Add(new(entry.Id + 1, entry.NameString, entry.ClassJob, entry.ItemLevel, items, ProfilePolicy.Fingerprint(items)));
        }
        return result;
    }
    private static void ReadAbilities(ProfileDocument profile)
    {
        var levels = profile.Professions.ToDictionary(j => j.JobId, j => j.Level);
        foreach (var action in Plugin.Data.GetExcelSheet<GameAction>().Where(a => ProfilePolicy.IsProfession(a.ClassJob.RowId) && a.IsPlayerAction && !a.IsPvP))
        {
            bool? unlocked = null;
            try { unlocked = levels[action.ClassJob.RowId] >= action.ClassJobLevel && Plugin.Unlocks.IsActionUnlocked(action); } catch { }
            profile.Abilities.Add(new(action.RowId, "Action", action.Name.ToString(), action.ClassJob.RowId,
                action.ClassJobLevel, unlocked, false, 0));
        }
        foreach (var action in Plugin.Data.GetExcelSheet<CraftAction>().Where(a => ProfilePolicy.IsProfession(a.ClassJob.RowId)))
        {
            bool? unlocked = null;
            try { unlocked = levels[action.ClassJob.RowId] >= action.ClassJobLevel &&
                (action.QuestRequirement.RowId == 0 || Plugin.Unlocks.IsQuestCompleted(action.QuestRequirement.Value)); } catch { }
            profile.Abilities.Add(new(action.RowId, "CraftAction", action.Name.ToString(), action.ClassJob.RowId,
                action.ClassJobLevel, unlocked, action.Specialist, action.QuestRequirement.RowId));
        }
    }
}
