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

    public bool Active(DateTimeOffset now) => StartsAt <= now && EndsAt > now;
    public bool Upcoming(DateTimeOffset now) => StartsAt > now;
}

public sealed class Configuration : IPluginConfiguration
{
    public const string LegacyFeedUrl = "https://raw.githubusercontent.com/karkandcoypp/FFXIV-Ultrawide-Cutscenes/main/EorzeaEventTracker/config/events.json";
    public const string DefaultFeedUrl = "https://raw.githubusercontent.com/karkandcoypp/FFXIV-Dalamud-Plugins/main/EorzeaEventTracker/config/events.json";
    public int Version { get; set; } = 3;
    public bool NotifyOnLogin { get; set; } = true;
    public bool OpenOnLogin { get; set; }
    public bool ShowUnverified { get; set; } = true;
    public string Language { get; set; } = "en";
    public string FeedUrl { get; set; } = DefaultFeedUrl;
    public Dictionary<string, bool> ManualCompletions { get; set; } = new();
    public Dictionary<string, HashSet<uint>> ObservedRewards { get; set; } = new();
}
