using System;
using System.Collections.Generic;
using EorzeaEventTracker;

internal static class EventCompletionChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var feed = new EventFeed();
        var catalog = new Dictionary<string, TrackingDefinition>();
        var quests = new Dictionary<uint, bool>();
        bool Open(bool ready = true, bool partial = true) => EventCompletionPolicy.ShouldAutoOpen(feed, catalog, quests, ready, partial, now);
        Check(!Open(), "empty feed never auto-opens");
        var e = new EventInfo { Id = "wake", Verified = true, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1) };
        feed.Events.Add(e);
        catalog[e.Id] = new TrackingDefinition { CompletionQuestId = 71004 };
        quests[71004] = false;
        Check(Open() && !Open(ready: false), "unfinished active event opens only after character data is ready");
        quests[71004] = true;
        Check(!Open(), "completed main quest suppresses auto-opening without requiring every reward");
        feed.Events.Add(new EventInfo { Id = "fall", Verified = true, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1) });
        catalog["fall"] = new TrackingDefinition { CompletionQuestId = 70337 };
        quests[70337] = false;
        Check(Open(), "one unfinished event still opens when another event is complete");
        quests[70337] = true;
        Check(!Open(), "all active main quests completed suppresses auto-opening");
        quests.Clear();
        Check(Open(), "unknown completion is not falsely treated as completed on another character");
        feed.Events.RemoveAt(1);
        e.StartsAt = now.AddHours(1);
        Check(!Open(), "upcoming event alone does not auto-open");
        e.StartsAt = now.AddDays(-1); e.EndsAt = now;
        Check(!Open(), "event ending now is no longer active");
        e.EndsAt = now.AddDays(1); e.Verified = false;
        Check(!Open(partial: false), "hidden partial events cannot cause auto-opening");
        catalog.Clear();
        Check(Open(), "new unmapped active event is not assumed completed");
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
