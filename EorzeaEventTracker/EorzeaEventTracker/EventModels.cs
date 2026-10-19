using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Dalamud.Configuration;

namespace EorzeaEventTracker;

public sealed class EventFeed
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("generatedAt")] public DateTimeOffset GeneratedAt { get; set; }
    [JsonPropertyName("events")] public List<EventInfo> Events { get; set; } = new();
}

public sealed class EventInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("startsAt")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("endsAt")] public DateTimeOffset? EndsAt { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("requirements")] public List<string> Requirements { get; set; } = new();
    [JsonPropertyName("steps")] public List<string> Steps { get; set; } = new();
    [JsonPropertyName("rewards")] public List<string> Rewards { get; set; } = new();
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("notes")] public List<string> Notes { get; set; } = new();
    [JsonPropertyName("tracking")] public TrackingDefinition? Tracking { get; set; }
    [JsonPropertyName("imageUrl")] public string ImageUrl { get; set; } = "";
    [JsonPropertyName("translations")] public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = new();
    [JsonPropertyName("locations")] public List<EventLocation> Locations { get; set; } = new();

    public bool Active(DateTimeOffset now) => StartsAt <= now && EndsAt > now;
    public bool Upcoming(DateTimeOffset now) => StartsAt > now;
}

public sealed class EventLocation
{
    public string Id { get; set; } = "";
    public string Npc { get; set; } = "";
    public string Zone { get; set; } = "";
    public uint TerritoryId { get; set; }
    public uint MapId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

// Display and reward choices are owned by one ContentId, never a character name/world label.
public class CharacterSettings
{
    public bool NotifyOnLogin { get; set; } = true;
    public bool OpenOnLogin { get; set; }
    public bool ShowUnverified { get; set; } = true;
    public string Language { get; set; } = "en";
    public HashSet<uint> FavoriteRewards { get; set; } = new();
    public HashSet<uint> IgnoredRewards { get; set; } = new();
    public HashSet<string> HiddenEvents { get; set; } = new();
    public float UiScale { get; set; } = 1;
    public float BackgroundOpacity { get; set; } = 1;
    public bool NotifyFavoriteDeadlines { get; set; } = true;
    public bool ShowCompactWidget { get; set; }
    public bool RemainingOnly { get; set; }
    public bool FavoritesOnly { get; set; }
    public RewardSortMode RewardSort { get; set; }
    public RewardCategory RewardTypeFilter { get; set; }
    public HashSet<string> ExpandedObtainedEvents { get; set; } = new();
    public HashSet<string> ViewedEvents { get; set; } = new();
    private static string EventVisibilityKey(EventInfo e) => e.Id + ":" +
        (e.EndsAt?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown");
    public bool IsEventHidden(EventInfo e) => HiddenEvents.Contains(EventVisibilityKey(e));
    public void SetEventHidden(EventInfo e, bool hidden)
    {
        if (hidden) HiddenEvents.Add(EventVisibilityKey(e));
        else HiddenEvents.Remove(EventVisibilityKey(e));
    }
}

public sealed class Configuration : CharacterSettings, IPluginConfiguration
{
    public const string LegacyFeedUrl = "https://raw.githubusercontent.com/karkandcoypp/FFXIV-Ultrawide-Cutscenes/main/EorzeaEventTracker/config/events.json";
    public const string DefaultFeedUrl = "https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/EorzeaEventTracker/config/events.json";
    public int Version { get; set; } = 4;
    public string FeedUrl { get; set; } = DefaultFeedUrl;
    public Dictionary<string, bool> ManualCompletions { get; set; } = new();
    public Dictionary<string, HashSet<uint>> ObservedRewards { get; set; } = new();
    public Dictionary<string, HashSet<uint>> ManualRewards { get; set; } = new();
    public Dictionary<string, int> DeadlineRemindersSent { get; set; } = new();
    public Dictionary<string, CharacterSettings> Characters { get; set; } = new();
    // Inherited old global selections are retained for recovery, but never applied to an unknown owner.
    public CharacterSettings NewCharacterDefaults() => new()
    {
        Language = Language, UiScale = UiScale, BackgroundOpacity = BackgroundOpacity,
        NotifyOnLogin = NotifyOnLogin, OpenOnLogin = OpenOnLogin, ShowUnverified = ShowUnverified,
        NotifyFavoriteDeadlines = NotifyFavoriteDeadlines, ShowCompactWidget = ShowCompactWidget,
    };
    public CharacterSettings ForCharacter(ulong contentId)
    {
        if (contentId == 0) throw new System.ArgumentOutOfRangeException(nameof(contentId));
        var key = contentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!Characters.TryGetValue(key, out var profile) || profile == null) Characters[key] = profile = NewCharacterDefaults();
        return profile;
    }
}
