using System;
using EorzeaEventTracker;

internal static class EnglishNameChecks
{
    public static void Run()
    {
        var e = new EventInfo { Title = "Event", Tracking = new() { Rewards = new() { new() { Id = 1, Name = "Gentlebean Parka" } } } };
        e.Locations.Add(new() { Id = "npc", Npc = "Lewena" });
        e.Translations["ru"] = new()
        {
            ["Event"] = "Событие", ["Gentlebean Parka"] = "Нежная парка с бобами", ["Lewena"] = "Левена",
            ["Quest"] = "Квест", ["Talk to Lewena for Gentlebean Parka."] = "Левена выдаёт Нежная парка с бобами.",
        };
        var locale = new Localizer(new Configuration { Language = "ru" });
        Check(locale.Text("Gentlebean Parka", e) == "Gentlebean Parka", "item title stays English despite older online translation");
        Check(locale.Text("Lewena", e) == "Lewena", "NPC title stays English in lists and map tooltips");
        Check(locale.Text("Event", e) == "Событие" && locale.Text("Quest", e) == "Квест", "event and quest translations remain available");
        Check(locale.Text("Talk to Lewena for Gentlebean Parka.", e) == "Lewena выдаёт Gentlebean Parka.", "known translated entity names are restored inside translated descriptions");
        Check(locale.Text("Settings") == "Настройки", "Russian UI is preserved");
    }
    private static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        Console.WriteLine("PASS: " + text);
    }
}
