using System;

namespace EorzeaEventTracker;

public enum DeadlineStatus { Unknown, Upcoming, Comfortable, Soon, Urgent, Ended }
public static class DeadlinePolicy
{
    public static DeadlineStatus Status(EventInfo e, DateTimeOffset now)
    {
        if (e.StartsAt == null || e.EndsAt == null) return DeadlineStatus.Unknown;
        if (e.Upcoming(now)) return DeadlineStatus.Upcoming;
        var remaining = e.EndsAt.Value - now;
        if (remaining <= TimeSpan.Zero) return DeadlineStatus.Ended;
        if (remaining <= TimeSpan.FromHours(24)) return DeadlineStatus.Urgent;
        if (remaining <= TimeSpan.FromHours(72)) return DeadlineStatus.Soon;
        return DeadlineStatus.Comfortable;
    }
    public static string Label(DeadlineStatus status) => status switch
    {
        DeadlineStatus.Comfortable => "Time to plan",
        DeadlineStatus.Soon => "Ending soon — within 3 days",
        DeadlineStatus.Urgent => "Last day — 24 hours or less",
        DeadlineStatus.Upcoming => "Upcoming event",
        DeadlineStatus.Ended => "Event ended",
        _ => "Dates unavailable",
    };
    public static string Countdown(EventInfo e, DateTimeOffset now, Localizer locale)
    {
        var status = Status(e, now);
        if (status == DeadlineStatus.Unknown) return locale.Text("Dates unavailable");
        if (status == DeadlineStatus.Ended) return locale.Text("Ended");
        var upcoming = status == DeadlineStatus.Upcoming;
        var remaining = (upcoming ? e.StartsAt!.Value : e.EndsAt!.Value) - now;
        if (remaining.TotalDays >= 1)
            return locale.Format(upcoming ? "Starts in {0}d {1}h" : "Ends in {0}d {1}h", (int)remaining.TotalDays, remaining.Hours);
        if (remaining.TotalHours >= 1)
            return locale.Format(upcoming ? "Starts in {0}h {1}m" : "Ends in {0}h {1}m", (int)remaining.TotalHours, remaining.Minutes);
        if (remaining.TotalMinutes >= 1)
            return locale.Format(upcoming ? "Starts in {0}m" : "Ends in {0}m", (int)remaining.TotalMinutes);
        return locale.Text(upcoming ? "Starts in less than a minute" : "Less than a minute left");
    }
}
