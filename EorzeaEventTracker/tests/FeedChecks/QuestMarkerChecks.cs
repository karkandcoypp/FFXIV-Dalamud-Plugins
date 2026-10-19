using System;
using EorzeaEventTracker;

internal static class QuestMarkerChecks
{
    public static void Run()
    {
        Check(QuestMarkerPolicy.Offer(71220, 6) == 71221, "ordinary quest uses visually verified offer icon, not diagnostic family label");
        Check(QuestMarkerPolicy.Offer(71230, 6) == 71231, "locked ordinary quest uses the actual unavailable marker");
        Check(QuestMarkerPolicy.Offer(71340, 6) == 71341, "unlock quest uses visually verified plus marker");
        Check(QuestMarkerPolicy.Offer(71350, 6) == 71351, "locked unlock quest uses the actual unavailable plus marker");
        Check(QuestMarkerPolicy.Offer(0, 6) == 0 && QuestMarkerPolicy.Offer(71220, 0) == 0
            && QuestMarkerPolicy.Offer(71220, 1) == 0 && QuestMarkerPolicy.Offer(1000000, 6) == 0,
            "unknown or incomplete icon families fall back to text rather than a placeholder");
        Console.WriteLine("PASS: visually verified quest markers exclude diagnostic family textures");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
