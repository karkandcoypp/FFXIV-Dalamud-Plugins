using System;
using EorzeaEventTracker;

internal static class DeadlineDisplayChecks
{
    public static void Run()
    {
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var e = new EventInfo { StartsAt = now.AddDays(-1), EndsAt = now.AddHours(73) };
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Comfortable, "more than 72 hours is calm");
        e.EndsAt = now.AddHours(72);
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Soon, "72-hour boundary is amber");
        e.EndsAt = now.AddHours(24).AddTicks(1);
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Soon, "more than 24 hours is not urgent");
        e.EndsAt = now.AddHours(24);
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Urgent, "24-hour boundary is urgent");
        e.EndsAt = now;
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Ended, "exact deadline is ended");
        e.StartsAt = now.AddHours(1); e.EndsAt = now.AddHours(2);
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Upcoming, "future event never looks like an urgent ending");
        e.StartsAt = now.AddDays(-1); e.EndsAt = null;
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Unknown, "missing dates are not invented");
        var english = new Localizer(new() { Language = "en" });
        var russian = new Localizer(new() { Language = "ru" });
        e.EndsAt = now.AddMinutes(59);
        Check(DeadlinePolicy.Countdown(e, now, english) == "Ends in 59m", "short time does not show misleading zero days");
        e.EndsAt = now.AddSeconds(59);
        Check(DeadlinePolicy.Countdown(e, now, english) == "Less than a minute left", "last minute remains visible");
        Check(DeadlinePolicy.Countdown(e, now, russian) == "Осталось меньше минуты", "urgency is localized");
        e.EndsAt = now.ToOffset(TimeSpan.FromHours(3)).AddHours(24);
        Check(DeadlinePolicy.Status(e, now) == DeadlineStatus.Urgent, "time-zone offsets do not change the deadline boundary");
        Console.WriteLine("PASS: deadline urgency thresholds, upcoming/ended/unknown dates and localized countdowns");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
