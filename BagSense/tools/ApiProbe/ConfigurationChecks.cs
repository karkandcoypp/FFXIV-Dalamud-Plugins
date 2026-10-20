using BagSense;
using Newtonsoft.Json;

internal static class ConfigurationChecks
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var config = new Configuration { Russian = false, Scale = 1.25f };
        var a = config.ForCharacter(123);
        var b = config.ForCharacter(456);
        a.Edit(5106).Note = "Для реликта / for a relic";
        a.Edit(5106).KeepQuantity = 20;
        a.Goals.Add(new() { Name = "Цель", Items = new() { [5106] = 30 } });
        a.Plan.Add(new() { ItemId = 5111, Quantity = 3, BaselineQuantity = 10, Action = PlanAction.Vendor, ObservedDecrease = true });
        Check(b.Decisions.Count == 0 && b.Goals.Count == 0 && b.Plan.Count == 0, "Characters do not share collections");
        Check(ReferenceEquals(a, config.ForCharacter(123)), "A character reuses its own decisions");
        bool zeroRejected = false;
        try { config.ForCharacter(0); } catch (ArgumentOutOfRangeException) { zeroRejected = true; }
        Check(zeroRejected, "Logged-out content ID cannot own data");
        var restored = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(config))!;
        Check(restored.ForCharacter(123).Decision(5106).Note == a.Decision(5106).Note, "Dalamud JSON serializer preserves Unicode notes");
        Check(restored.ForCharacter(123).Goals[0].Items[5106] == 30, "Dalamud JSON serializer preserves goals and numeric keys");
        Check(restored.ForCharacter(123).Plan[0].ObservedDecrease && restored.ForCharacter(123).Plan[0].Action == PlanAction.Vendor, "Stale plans survive restart without reactivation");
        Check(!restored.Russian && restored.Scale == 1.25f, "Language and scale survive restart");
        Check(restored.ForCharacter(456).Decisions.Count == 0, "Second character stays independent after restart");
        restored.ForCharacter(123).Goals[0].Completed = true;
        Check(CleanupPolicy.Reservation(restored.ForCharacter(123), 5106) == 20, "Completing a restored goal retains personal stock");
        Console.WriteLine($"PASS: {checks} actual-configuration / Newtonsoft persistence checks");
    }
}
