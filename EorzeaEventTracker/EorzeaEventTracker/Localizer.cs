using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace EorzeaEventTracker;

public sealed class Localizer
{
    private readonly CharacterSettings settings;
    private static readonly Dictionary<string, string> Russian = LoadRussian();
    public Localizer(CharacterSettings settings) => this.settings = settings;
    public bool IsRussian => settings.Language == "ru";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsRussian ? "ru-RU" : "en-US");
    public string Text(string text, EventInfo? context = null)
    {
        var names = context == null ? Array.Empty<string>() :
            (context.Tracking?.Rewards.Select(r => r.Name) ?? Enumerable.Empty<string>())
            .Concat(context.Tracking?.Quests.Select(q => q.Name) ?? Enumerable.Empty<string>())
            .Concat(context.Locations.Select(l => l.Npc)).Distinct().ToArray();
        // Item, NPC and quest names remain searchable in the English game, even with old translated feeds.
        if (names.Contains(text, StringComparer.Ordinal)) return text;
        var value = text;
        if (context?.Translations.TryGetValue(IsRussian ? "ru" : "en", out var onlineEntries) == true
            && onlineEntries.TryGetValue(text, out var online)) value = online;
        else if (IsRussian && Russian.TryGetValue(text, out var translated)) value = translated;
        if (context != null && context.Translations.TryGetValue(IsRussian ? "ru" : "en", out var translations))
        {
            foreach (var name in names.OrderByDescending(n => n.Length))
                if (translations.TryGetValue(name, out var alias) && !string.IsNullOrWhiteSpace(alias) && alias != name)
                    value = Regex.Replace(value, @"(?<!\w)" + Regex.Escape(alias) + @"(?!\w)", _ => name);
        }
        return value;
    }

    public string Format(string template, params object[] values) => string.Format(Culture, Text(template), values);

    public string RewardLabel(RewardProgress reward) => reward.State switch
    {
        RewardState.Owned => Format("In bags / equipped: {0}", reward.Quantity),
        _ => Text(reward.Label),
    };

    public string FeedStatus(string status, DateTimeOffset? updatedAt)
    {
        if (status.StartsWith("Updated ", StringComparison.Ordinal) && updatedAt.HasValue)
            return Format("Updated {0}", updatedAt.Value.ToLocalTime().ToString("g", Culture));
        if (status.StartsWith("Loaded ", StringComparison.Ordinal)) return Format("Loaded {0}", status[7..]);
        if (status.StartsWith("Offline:", StringComparison.Ordinal)) return Text("Offline: using cached feed");
        return Text(status);
    }

    private static Dictionary<string, string> LoadRussian()
    {
        using var resource = typeof(Localizer).Assembly.GetManifestResourceStream("EorzeaEventTracker.ru.json")
            ?? throw new InvalidOperationException("Russian language resource is missing");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(resource) ?? new();
    }
}
