using BagSense;
using Lumina;
using System.Diagnostics;

internal static class DataChecks
{
    public static void Run(string path)
    {
        using var game = new GameData(path);
        var timer = Stopwatch.StartNew();
        var catalog = new ItemCatalog(game.Excel);
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
        Check(catalog.MerchantItems > 1000, "Regular merchant index loaded");
        Check(catalog.RecipeItems > 1000, "Recipe ingredient index loaded");
        Check(catalog.ExchangeItems > 100, "Direct item-exchange index loaded");
        var copper = catalog.Search("Copper Ore").First(i => i.Name == "Copper Ore");
        Check(copper.Id > 0 && copper.Icon > 0 && !copper.Equipment, "Copper Ore resolves to a real material");
        Check(copper.RecipeUses.Any(r => r.Contains("Copper Ingot")), "Copper Ore is used to make Copper Ingot");
        Check(copper.BuyOffers.Any(o => o.Price > 0), "Copper Ore has a priced merchant source");
        Check(copper.SellPrice > 0, "Merchant purchase and sale prices remain separate");
        Check(catalog.Search(copper.Id.ToString()).Single().Id == copper.Id, "Exact item-ID search");
        Check(!catalog.Search("x").Any(), "Short search does not enumerate the item database");
        Check(catalog.Search("Iron").Count() <= 40, "Search is bounded to 40 results");
        Check(catalog.Find(uint.MaxValue) == null, "Unknown item has no fabricated metadata");
        var minion = catalog.Search("Wind-up Airship").First(i => i.Name == "Wind-up Airship");
        Check(minion.Icon > 0 && minion.Unlocked == null, "Catalog never fabricates character unlock state");
        var weapon = catalog.Search("Weathered Shortsword").First(i => i.Name == "Weathered Shortsword");
        Check(weapon.Equipment, "Equipment detection uses EquipSlotCategory");
        Check(catalog.Find(copper.Id) == copper, "Cached metadata preserves identity");
        Console.WriteLine($"PASS: {checks} installed-data checks; {catalog.MerchantItems} merchant, {catalog.RecipeItems} recipe and {catalog.ExchangeItems} exchange items; {timer.ElapsedMilliseconds} ms");
    }
}
