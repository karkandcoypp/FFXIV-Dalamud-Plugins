using System.Text.Json;
using BagSense;
using Lumina;
internal static class BundleChecks
{
    public static void Run(string gamePath, string dataPath)
    {
        using var game = new GameData(gamePath);
        var catalog = new ItemCatalog(game.Excel, p => game.GetFile<Lumina.Data.Files.LgbFile>(p));
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(Path.Combine(dataPath, "manifest.json")))!;
        var database = DatabaseCatalog.Decode(manifest, File.ReadAllBytes(Path.Combine(dataPath, manifest.File)));
        catalog.ApplyDatabase(database);
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
        var bundle = catalog.Bundles(47746).Single(b => b.Kind == "Quest" && b.QuestId == 70855);
        Check(bundle.Parts.Length == 6 && bundle.Parts.All(p => p.Count == 3) && bundle.Exact, "Arcane Artistry exposes six distinct three-item inputs at the same hand-in objective");
        Check(bundle.Contacts!.Any(c => c.Npc == "Gerolt" && c.CanMap), "Set recipient is Gerolt with map, rather than the quest issuer");
        Check(DemiatmaGuide.All.All(g => catalog.Find(g.Id)?.Name == g.Name && g.Events.Length >= 4), "Reviewed drop guides match all six installed demiatma identities");
        Check(DemiatmaGuide.Find(47746)?.Zone == "Yak T'el", "Malachite guide includes Yak T'el FATEs");
        Check(catalog.Bundles(7851).Any(b => b.Kind == "Research" && b.Parts.Count(p => p.Name.StartsWith("Atma of ")) == 12 && b.Parts.Any(p => p.Name.Contains("Zenith"))), "Atma bundles retain twelve types and the required weapon as one exchange option");
        Check(catalog.Bundles(9509).Count(b => b.Kind == "Research") > 5, "Zodiac upgrade variants remain separate sets");
        var copper = catalog.Bundles(5106).Where(b => b.Kind == "Recipe").ToArray();
        Check(copper.Length > 0 && copper.All(b => b.Parts.Any(p => p.ItemId == 5106)), "Recipe ingredients are linked by recipe ID, not inferred from matching names");
        Check(catalog.Bundles(47866).Any(b => b.Kind == "Exchange" && b.Parts.Length == 3), "Aetherspun Gold bundle keeps the complete simultaneous three-item price");
        var before = catalog.Bundles(47746);
        catalog.ApplyDatabase(database with { Needs = database.Needs.Where(n => n.QuestId != 70855).ToArray() });
        Check(!ReferenceEquals(before, catalog.Bundles(47746)), "Database refresh invalidates bundle cache");
        catalog.ApplyDatabase(database);
        var all = catalog.Browse("", 0, 100000).SelectMany(i => catalog.Bundles(i.Id)).DistinctBy(b => b.Key).ToArray();
        Check(all.All(b => b.Parts.Length > 0 && b.Parts.All(p => p.Count is null or > 0)), "Full catalog set audit rejects empty or nonpositive requirements");
        Console.WriteLine("SET COVERAGE: " + JsonSerializer.Serialize(all.GroupBy(b => b.Kind).ToDictionary(g => g.Key, g => g.Count())));
        Console.WriteLine($"PASS: {checks} installed set checks; {all.Length} distinct sets; {all.Count(b => b.Kind == "Quest" && b.Exact)} quest objective sets");
    }
}
