using System.Text.Json;
using BagSense;
using Lumina;
internal static class AuditChecks
{
    public static void Run(string gamePath, string dataPath, string outputPath)
    {
        using var game = new GameData(gamePath);
        var catalog = new ItemCatalog(game.Excel, p => game.GetFile<Lumina.Data.Files.LgbFile>(p));
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(Path.Combine(dataPath, "manifest.json")))!;
        catalog.ApplyDatabase(DatabaseCatalog.Decode(manifest, File.ReadAllBytes(Path.Combine(dataPath, manifest.File))));
        var rows = catalog.Browse("", 0, 100000).Select(row =>
        {
            var item = catalog.Find(row.Id) ?? throw new Exception("Missing item " + row.Id);
            bool linked = item.CraftingLinks.Length > 0 || item.ExchangeUses.Length > 0 || item.QuestUses.Any(q => !q.Reward)
                || item.DeliveryUses.Length > 0 || item.Reward?.Kind is "Mount" or "Minion" or "Music" or "Card" or "Emote" or "Hairstyle";
            string tier = linked ? "Linked" : item.Role is not ("" or "Unresolved") ? "CategoryExplained" : "NeedsResearch";
            return new { item.Id, item.Name, item.Category, Tier = tier, item.Role, Recipes = item.CraftingLinks.Length,
                Exchanges = item.ExchangeUses.Length, Quests = item.QuestUses.Length, Deliveries = item.DeliveryUses.Length,
                Sources = item.AcquisitionSources.Length + item.AcquisitionTrades.Length + item.BuyOffers.Length,
                Wiki = "https://ffxiv.consolegameswiki.com/wiki/" + Uri.EscapeDataString(item.Name.Replace(' ', '_')) };
        }).ToArray();
        var report = new { Count = rows.Length, Tiers = rows.GroupBy(r => r.Tier).ToDictionary(g => g.Key, g => g.Count()),
            WithSources = rows.Count(r => r.Sources > 0), Source = manifest.SourceCommit, Items = rows };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report));
        Console.WriteLine(JsonSerializer.Serialize(new { report.Count, report.Tiers, report.WithSources }));
        var demiatma = catalog.Find(47744)!;
        var need = demiatma.QuestUses.Single(q => !q.Reward && q.QuestId == 70855);
        if (need.RequiredCount != 3 || !need.Contacts.Any(n => n.Npc == "Gerolt" && n.CanMap)) throw new Exception("Demiatma regression: quantity or actual hand-in map missing");
        var gate = catalog.QuestGates[70855];
        if (gate.Level != 100 || !gate.Previous.Any(p => p.Id == 70847)) throw new Exception("Arcane Artistry prerequisites missing");
        if (!demiatma.AcquisitionSources.Any(s => s.Name == "Urqopacha")) throw new Exception("Demiatma FATE source missing");
        Console.WriteLine("PASS: every catalog item audited; Azurite Demiatma links to 3-item Gerolt hand-in, level 100, Unfamiliar Territory and FATE sources");
    }
}
