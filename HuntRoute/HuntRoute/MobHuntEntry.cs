namespace HuntRoute;
public sealed class MobHuntEntry {
    public string? Name { get; init; }
    public string? TerritoryName { get; init; }
    public string? ExpansionName { get; init; }
    public uint ExpansionId { get; init; }
    public uint MapId { get; init; }
    public uint TerritoryType { get; init; }
    public uint MobHuntId { get; init; }
    public bool IsEliteMark { get; init; }
    public uint Generation {get;init;}
    public byte OrderId { get; init; }
    public byte BillNumber { get; init; }
    public byte MobIndex { get; init; }
    public uint NeededKills { get; init; }
    public uint Icon { get; init; }
}
