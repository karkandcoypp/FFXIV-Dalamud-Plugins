using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public static class EventCompletionPolicy
{
    public static bool IsComplete(TrackingDefinition? definition, IReadOnlyDictionary<uint, bool> quests) =>
        definition is { CompletionQuestId: > 0 } && quests.TryGetValue(definition.CompletionQuestId, out var done) && done;

    public static bool ShouldAutoOpen(EventFeed feed, IReadOnlyDictionary<string, TrackingDefinition> catalog,
        IReadOnlyDictionary<uint, bool> quests, bool ready, bool showUnverified, DateTimeOffset now) =>
        ready && feed.Events.Any(e => e.Active(now) && (showUnverified || e.Verified)
            && !IsComplete(catalog.GetValueOrDefault(e.Id), quests));
}
