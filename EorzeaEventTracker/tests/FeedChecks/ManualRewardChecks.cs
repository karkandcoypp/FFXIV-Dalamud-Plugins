using System;
using System.Text.Json;
using EorzeaEventTracker;

internal static class ManualRewardChecks
{
    public static void Run()
    {
        var settings = new Configuration();
        var first = ProgressPolicy.ForCharacter(settings.ManualRewards, 1);
        first.Add(52402);
        Check(!ProgressPolicy.ForCharacter(settings.ManualRewards, 2).Contains(52402), "manual marks are isolated per character");
        settings = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!;
        Check(ProgressPolicy.ForCharacter(settings.ManualRewards, 1).Contains(52402), "manual obtained marks survive restart");
        var automatic = new RewardProgress(RewardState.NotSeen);
        Check(ProgressPolicy.ApplyManual(automatic, true).State == RewardState.ManuallyObtained, "manual marks confirm previously undetected rewards");
        Check(ProgressPolicy.ApplyManual(automatic, true).Confirmed, "manual marks participate in reward counts and reminder completion");
        Check(!ProgressPolicy.ApplyManual(automatic, false).Confirmed, "clearing manual mark restores unconfirmed automatic state");
        var owned = new RewardProgress(RewardState.Owned, 2);
        Check(ProgressPolicy.ApplyManual(owned, false) == owned && ProgressPolicy.ApplyManual(owned, true) == owned, "manual changes never erase automatic evidence or quantity");
        Check(settings.ObservedRewards.Count == 0, "manual marks are separate from observed item history");
        Check(AppearancePolicy.Scale(settings) == 1 && AppearancePolicy.Opacity(settings) == 1, "existing configurations keep original appearance");
        settings.UiScale = 9; settings.BackgroundOpacity = -1;
        Check(AppearancePolicy.Scale(settings) == 1.75f && AppearancePolicy.Opacity(settings) == .4f, "appearance settings clamp to readable bounds");
        settings.UiScale = float.NaN; settings.BackgroundOpacity = float.PositiveInfinity;
        Check(AppearancePolicy.Scale(settings) == 1 && AppearancePolicy.Opacity(settings) == 1, "nonfinite appearance values use safe defaults");
        settings.UiScale = 1.25f; settings.BackgroundOpacity = .6f;
        settings = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(settings))!;
        Check(AppearancePolicy.Scale(settings) == 1.25f && AppearancePolicy.Opacity(settings) == .6f, "appearance choices persist across restart");
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
}
