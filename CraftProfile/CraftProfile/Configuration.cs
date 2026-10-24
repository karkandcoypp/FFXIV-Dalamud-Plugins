using Dalamud.Configuration;
namespace CraftProfile;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Russian { get; set; } = true;
    public bool AutoExport { get; set; } = true;
    public int ExportSeconds { get; set; } = 15;
}
