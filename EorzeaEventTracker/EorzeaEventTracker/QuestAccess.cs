using System;
using System.Collections.Generic;
using System.Linq;

namespace EorzeaEventTracker;

public sealed record QuestRequirement(uint Id, string Name, bool Complete);
public sealed record QuestAccess(int Level, bool LevelMet, byte Join, IReadOnlyList<QuestRequirement> Previous,
    bool OtherConditions, bool ClassMet, bool Known = true, string ClassName = "", bool Seasonal = false, bool Accepted = false)
{
    // Unknown operators are never interpreted as AND or OR.
    public static bool? MeetsPrevious(byte join, IReadOnlyList<QuestRequirement> previous) =>
        previous.Count == 0 ? true : join switch
        {
            1 => previous.All(q => q.Complete),
            2 => previous.Any(q => q.Complete),
            _ => null,
        };
    public bool Blocked => Known && !Accepted && (!LevelMet || !ClassMet || MeetsPrevious(Join, Previous) == false);
    public bool Partial => !Known || OtherConditions || MeetsPrevious(Join, Previous) == null;
}
