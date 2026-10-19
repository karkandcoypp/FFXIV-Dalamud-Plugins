using System;
using EorzeaEventTracker;

internal static class QuestAccessChecks
{
    public static void Run()
    {
        QuestRequirement[] noneDone = [new(66043, "The Gridanian Envoy", false), new(66064, "The Ul’dahn Envoy", false), new(66082, "The Lominsan Envoy", false)];
        QuestRequirement[] oneDone = [noneDone[0], noneDone[1] with { Complete = true }, noneDone[2]];
        Check(QuestAccess.MeetsPrevious(2, noneDone) == false, "OR prerequisites block when no route is complete");
        Check(QuestAccess.MeetsPrevious(2, oneDone) == true, "one city envoy is sufficient; never require all three");
        Check(QuestAccess.MeetsPrevious(1, oneDone) == false, "AND prerequisites require every quest");
        Check(QuestAccess.MeetsPrevious(1, [oneDone[1]]) == true, "single completed prerequisite passes");
        Check(QuestAccess.MeetsPrevious(9, noneDone) == null, "unknown join operator remains unknown");
        Check(QuestAccess.MeetsPrevious(0, []) == true, "no prerequisites is not a blocker");
        var access = new QuestAccess(15, true, 2, oneDone, false, true);
        Check(!access.Blocked && !access.Partial, "level and one envoy satisfy checked prerequisites");
        Check((access with { LevelMet = false }).Blocked, "low current-class level blocks access");
        Check((access with { ClassMet = false }).Blocked, "wrong class blocks access");
        Check((access with { OtherConditions = true }).Partial, "additional conditions are not silently declared satisfied");
        Check(!(access with { Known = false, LevelMet = false }).Blocked && (access with { Known = false }).Partial, "unavailable data is unknown, not a false locked state");
        Check(!(access with { Accepted = true, LevelMet = false }).Blocked, "accepted quest is not relocked by changing to a low-level class");
        Console.WriteLine("PASS: quest access OR/AND, levels, classes and unknown conditions");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
