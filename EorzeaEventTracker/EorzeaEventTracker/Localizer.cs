using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace EorzeaEventTracker;

public sealed class Localizer
{
    private readonly Configuration settings;
    private static readonly Dictionary<string, string> Russian = LoadRussian();
    public Localizer(Configuration settings) => this.settings = settings;
    public bool IsRussian => settings.Language == "ru";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsRussian ? "ru-RU" : "en-US");
    public string Text(string text) => IsRussian && Russian.TryGetValue(text, out var translated) ? translated : text;
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
