using System.Collections.Generic;

namespace EorzeaEventTracker;

public sealed class TrackingDefinition
{
    public string EventId { get; set; } = "";
    public string Asset { get; set; } = "";
    public List<TrackedQuest> Quests { get; set; } = new();
    public List<TrackedReward> Rewards { get; set; } = new();
}
public sealed class TrackedQuest
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public uint Icon { get; set; }
}
public sealed class TrackedReward
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public uint Icon { get; set; }
    public int Price { get; set; }
    public uint EarnedByQuest { get; set; }
}

public enum RewardState { Unknown, NotSeen, NotUnlocked, SeenPreviously, QuestRewardEarned, Owned, Unlocked }
public readonly record struct RewardProgress(RewardState State, int Quantity = 0)
{
    public bool Confirmed => State is RewardState.SeenPreviously or RewardState.QuestRewardEarned or RewardState.Owned or RewardState.Unlocked;
    public string Label => State switch
    {
        RewardState.Unlocked => "Unlocked",
        RewardState.Owned => $"In bags / equipped: {Quantity}",
        RewardState.QuestRewardEarned => "Earned from quest",
        RewardState.SeenPreviously => "Previously obtained",
        RewardState.NotUnlocked => "Not unlocked",
        RewardState.NotSeen => "Not detected",
        _ => "Waiting for character data",
    };
}

public static class ProgressPolicy
{
    public static RewardProgress Resolve(bool available, bool? unlocked, int quantity, bool questEarned, bool observed)
    {
        if (!available) return new(RewardState.Unknown);
        if (unlocked == true) return new(RewardState.Unlocked);
        if (quantity > 0) return new(RewardState.Owned, quantity);
        if (questEarned) return new(RewardState.QuestRewardEarned);
        if (observed) return new(RewardState.SeenPreviously);
        return new(unlocked == false ? RewardState.NotUnlocked : RewardState.NotSeen);
    }

    // Content ID partitions evidence; never reuse another character's observations.
    public static HashSet<uint> ForCharacter(Dictionary<string, HashSet<uint>> history, ulong contentId)
    {
        var key = contentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!history.TryGetValue(key, out var items)) history[key] = items = new();
        return items;
    }
}
