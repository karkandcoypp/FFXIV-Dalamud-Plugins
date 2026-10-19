using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EorzeaEventTracker;

public enum FeedOrigin { None, Bundled, Cache, Remote }
public sealed record FeedLoadInfo(FeedOrigin Origin = FeedOrigin.None, bool Fallback = true,
    DateTimeOffset? LastAttempt = null, DateTimeOffset? LastSuccess = null, string DataSource = "",
    string? Error = null, bool Refreshing = false, string Status = "Not loaded");
public enum DataConfidence { Confirmed, Partial, Cached, Stale }
public static class FeedHealth
{
    public static DataConfidence Confidence(EventInfo e, EventFeed feed, FeedLoadInfo load, DateTimeOffset now)
    {
        bool Old(DateTimeOffset? date) => date is { } value && (value < now.AddHours(-48) || value > now.AddMinutes(10));
        if (Old(feed.GeneratedAt == DateTimeOffset.MinValue ? null : feed.GeneratedAt)
            || e.Validation?.Status == "stale" || e.Automation?.Status is "stale" or "retained"
            || e.Validation?.Status == "passed" && Old(e.Validation.CheckedAt)) return DataConfidence.Stale;
        if (load.Fallback || load.Origin is FeedOrigin.Cache or FeedOrigin.Bundled) return DataConfidence.Cached;
        return e.Verified && e.Validation is { Status: "passed", CheckedAt: not null }
            && feed.GeneratedAt != DateTimeOffset.MinValue ? DataConfidence.Confirmed : DataConfidence.Partial;
    }
    public static string SafeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return "(local / unset)";
        return uri.GetLeftPart(UriPartial.Authority).Replace(uri.UserInfo.Length > 0 ? uri.UserInfo + "@" : "\u0000", "") + uri.AbsolutePath;
    }
    public static string SafeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return "";
        var value = Regex.Replace(error, @"https?://[^\s""<>]+", m => SafeUrl(m.Value));
        value = Regex.Replace(value, @"[A-Za-z]:\\[^\r\n""<>]+", "[local path]");
        return value.Length <= 1500 ? value : value[..1500];
    }
    public static string Report(string version, string requestedUrl, EventFeed feed, FeedLoadInfo load) =>
        JsonSerializer.Serialize(new
        {
            plugin = "EorzeaEventTracker", version, capturedAt = DateTimeOffset.UtcNow,
            requestedSource = SafeUrl(requestedUrl), dataSource = SafeUrl(load.DataSource),
            source = load.Origin.ToString(), fallback = load.Fallback, refreshing = load.Refreshing,
            lastAttempt = load.LastAttempt, lastSuccessfulLoad = load.LastSuccess, feedPublishedAt = feed.GeneratedAt,
            lastError = SafeError(load.Error), schemaVersion = feed.SchemaVersion,
            events = feed.Events.Count,
            validation = feed.Events.GroupBy(e => e.Validation?.Status ?? "not-checked").ToDictionary(g => g.Key, g => g.Count())
        }, new JsonSerializerOptions { WriteIndented = true });
}
