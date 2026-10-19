using System.Collections.Generic;
using Dalamud.Configuration;

namespace EorzeaCompass;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Russian { get; set; } = true;
    public bool NotifyOnLogin { get; set; } = true;
    public bool OpenOnLogin { get; set; }
    public Dictionary<ulong, HashSet<uint>> Started { get; set; } = new();
    public Dictionary<ulong, HashSet<uint>> Hidden { get; set; } = new();
}
