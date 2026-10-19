namespace EorzeaEventTracker;

public static class EventDiscoveryPolicy
{
    public static string Key(EventInfo e) => e.Id + ":" + (e.StartsAt?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown")
        + ":" + (e.EndsAt?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown");
    public static bool IsNew(CharacterSettings settings, EventInfo e) => !settings.ViewedEvents.Contains(Key(e));
    public static bool MarkViewed(CharacterSettings settings, EventInfo e) => settings.ViewedEvents.Add(Key(e));
}
