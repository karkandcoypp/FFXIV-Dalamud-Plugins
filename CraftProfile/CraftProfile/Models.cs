using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CraftProfile;

public sealed record MateriaData(int Slot, ushort TypeId, byte GradeIndex, uint? ItemId, string? Name, uint? StatId, int? Value);
public sealed record GearItem(int Slot, uint ItemId, string Name, bool Hq, uint ItemLevel, uint? Condition,
    List<MateriaData> Materia, int? NormalMateriaSlots, bool? AdvancedMelding);
public sealed record GearsetData(int Number, string Name, uint JobId, int ItemLevel, List<GearItem> Items, string Fingerprint);
public sealed record StatusData(uint Id, string Name, ushort Param, float RemainingSeconds);
public sealed record StatsData(int Craftsmanship, int Control, int Cp, int Gathering, int Perception, int Gp);
public sealed record JobObservation(uint JobId, int Level, DateTimeOffset CapturedAt, string Source,
    string GearFingerprint, bool LevelSynced, StatsData Stats, List<GearItem> Equipment, List<StatusData> Statuses, bool? SpecialistSoulEquipped = null)
{
    public bool UnbuffedConfirmed => !LevelSynced && Statuses.Count == 0;
}
public sealed record ProfessionData(uint JobId, string Abbreviation, string Name, int Level, int Experience,
    float? DesynthesisLevel, bool? SpecialistAssigned);
public sealed record BookData(uint Id, uint ItemId, string Name, bool? Unlocked);
public sealed record FolkloreData(uint Id, string Name, bool? Unlocked);
public sealed record AbilityData(uint Id, string Kind, string Name, uint JobId, int Level, bool? Unlocked,
    bool SpecialistRequired, uint QuestId);
public sealed record RecipeData(uint Id, uint ItemId, uint JobId, int Level, bool? Unlocked,
    uint BookId, int RequiredCraftsmanship, int RequiredControl, uint RequiredItemId, uint RequiredStatusId,
    int Difficulty, int Quality, int Durability, bool CanHq, bool Expert, bool SpecialistRequired,
    int Yield, Dictionary<uint, int> Materials, int MaterialQualityFactor, int ProgressDivider, int ProgressModifier, int QualityDivider, int QualityModifier);
public sealed record OwnedStack(string Container, int Slot, uint ItemId, string Name, int Quantity, bool Hq, bool Collectable);
public sealed record ContainerCoverage(string Container, bool Loaded, int Slots);
public sealed class ProfileDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string PluginVersion { get; set; } = "0.1.0.0";
    public string CharacterKey { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public uint HomeWorldId { get; set; }
    public uint CurrentWorldId { get; set; }
    public uint CurrentJobId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public bool RecipeCatalogComplete { get; set; }
    public List<ProfessionData> Professions { get; set; } = [];
    public Dictionary<uint, JobObservation> Observations { get; set; } = [];
    public Dictionary<uint, JobObservation> UnbuffedObservations { get; set; } = [];
    public List<GearsetData> SavedGearsets { get; set; } = [];
    public List<BookData> MasterBooks { get; set; } = [];
    public List<FolkloreData> Folklore { get; set; } = [];
    public List<AbilityData> Abilities { get; set; } = [];
    public Dictionary<uint, RecipeData> Recipes { get; set; } = [];
    public List<OwnedStack> Inventory { get; set; } = [];
    public List<ContainerCoverage> InventoryCoverage { get; set; } = [];
    public Dictionary<string, string> Limitations { get; set; } = new()
    {
        ["stats"] = "Observed only for equipped professions; saved gearsets are not live stat measurements.",
        ["unbuffed"] = "Confirmed only with no active statuses and no level sync; buffs are never subtracted speculatively.",
        ["retainers"] = "Unknown: retainer inventories and sale listings are not collected.",
        ["storage"] = "Only loaded listed containers are observed. Missing containers are unknown, never zero.",
        ["rotation"] = "Unknown: no macro simulation or HQ/synthesis success guarantee.",
        ["costs"] = "Food, medicine, travel, repair and crafting time need separate economic assumptions.",
        ["saved_gearsets"] = "Saved definitions may differ from actual inventory; equip each profession to confirm stats.",
    };
}
public sealed record SessionDocument(int SchemaVersion, bool LoggedIn, string? CharacterKey,
    DateTimeOffset UpdatedAt, string? ProfileFile, string? Error);

public static class ProfilePolicy
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict,
    };
    public static string Fingerprint(IEnumerable<GearItem> items)
    {
        // Condition and localized names do not change the equipment's stat identity.
        var value = string.Join("|", items.OrderBy(i => i.Slot).Select(i =>
            $"{i.Slot}:{i.ItemId}:{i.Hq}:" + string.Join(",", i.Materia.OrderBy(m => m.Slot).Select(m => $"{m.Slot}:{m.TypeId}:{m.GradeIndex}"))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
    public static bool IsProfession(uint job) => job is >= 8 and <= 18;
    public static void MergeObservation(ProfileDocument profile, JobObservation observation)
    {
        if (!IsProfession(observation.JobId)) return;
        profile.Observations[observation.JobId] = observation;
        if (observation.UnbuffedConfirmed) profile.UnbuffedObservations[observation.JobId] = observation;
        // A baseline of a different gearset must not masquerade as today's unbuffed stats.
        else if (profile.UnbuffedObservations.TryGetValue(observation.JobId, out var previous)
            && previous.GearFingerprint != observation.GearFingerprint)
            profile.UnbuffedObservations.Remove(observation.JobId);
    }
    public static ProfileDocument Load(string path, string characterKey)
    {
        if (File.Exists(path))
        {
            try
            {
                var value = JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllText(path), Json);
                if (value?.SchemaVersion == 1 && value.CharacterKey == characterKey) return value;
            }
            catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException) { }
        }
        return new() { CharacterKey = characterKey };
    }
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
