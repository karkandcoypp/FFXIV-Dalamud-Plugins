using Dalamud.Configuration;

namespace BagSense;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Russian { get; set; } = true;
    public float Scale { get; set; } = 1;
    public bool ShowEquipment { get; set; } = true;
    public bool ShowMaterials { get; set; } = true;
    public bool ShowCollectibles { get; set; } = true;
    public Dictionary<ulong, CharacterData> Characters { get; set; } = new();
    public CharacterData ForCharacter(ulong id)
    {
        if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (!Characters.TryGetValue(id, out var data)) Characters[id] = data = new();
        return data;
    }
}
