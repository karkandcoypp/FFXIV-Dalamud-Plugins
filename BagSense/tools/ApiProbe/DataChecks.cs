using BagSense;
using Lumina;
using System.Diagnostics;

internal static class DataChecks
{
    public static void Run(string path)
    {
        using var game = new GameData(path);
        var timer = Stopwatch.StartNew();
        var catalog = new ItemCatalog(game.Excel, path => game.GetFile<Lumina.Data.Files.LgbFile>(path));
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
        var mountReward = catalog.Find(6001)!;
        Check(mountReward.Reward is { Kind: "Mount", CollectionId: 1, PreviewIcon: 68001 }, "Chocobo Whistle resolves to the actual mount and its journal artwork");
        Check(catalog.Find(41079)!.ExchangeUses.Any(e => catalog.Find(e.RewardId)?.Reward?.Kind == "Mount"), "Aloalo Coin has a mount reward with explained collection metadata");
        Check(catalog.Find(9829)!.Reward?.Kind == "Card", "Triple Triad cards are explicitly identified");
        Check(weaponReward(), "Equipment reward metadata explains level and job");
        bool weaponReward() => catalog.Find(1601)?.Reward?.Kind == "Equipment" && catalog.Find(1601)!.Reward!.Detail.Contains("iLvl");
        var minion = catalog.Search("Wind-up Airship").First(i => i.Name == "Wind-up Airship");
        Check(minion.Icon > 0 && minion.Unlocked == null, "Catalog never fabricates character unlock state");
        var weapon = catalog.Search("Weathered Shortsword").First(i => i.Name == "Weathered Shortsword");
        Check(weapon.Equipment, "Equipment detection uses EquipSlotCategory");
        Check(catalog.Find(copper.Id) == copper, "Cached metadata preserves identity");
        Check(copper.BuyOffers.SelectMany(o => o.Destinations).Any(r => r.Location.CanMap), "Copper Ore merchant listings resolve to concrete map targets");
        Check(copper.QuestUses.Any(q => q.QuestId == 66135 && !q.Reward && q.Contacts.Any(l => l.CanMap)), "My First Pickaxe is a requested-item use, with a quest contact");
        Check(catalog.World.Merchants.Any(m => m.Npc == "Bango Zango" && m.CanMap), "A verified regular merchant can be used for ordinary gil sales");
        var grip = catalog.Find(23165)!;
        Check(grip.ExchangeUses.Any(e => e.Destinations.Any(r => r.Location.Npc == "E-Una-Kotor" && r.Location.CanMap)), "Aetherpool Grip maps to E-Una-Kotor through the named weapon-shop instruction");
        Check(grip.ExchangeUses.Any(e => e.FullCosts.Length > 1), "Weapon upgrade exchanges retain all simultaneous costs");
        var potsherd = catalog.Find(41078)!;
        Check(potsherd.ExchangeUses.Any(e => e.Destinations.Any(r => r.Location.Npc == "Trisassant" && r.Location.CanMap)), "Aloalo Potsherd maps to Trisassant");
        Check(potsherd.ExchangeUses.Any(e => e.Conditions.Any(r => r.Kind == "quest" && r.Id == 70325)), "Aloalo shop access retains its quest requirement");
        var gold = catalog.Find(47866)!;
        foreach (var location in gold.ExchangeUses.SelectMany(e => e.Destinations).Select(r => r.Location).Distinct())
            Console.WriteLine($"GOLD NPC: {location.Npc} / {location.Zone} ({location.X:0.0}, {location.Y:0.0})");
        Check(gold.ExchangeUses.Any(e => e.FullCosts.Length == 3), "Aetherspun Gold upgrade shows all three costs instead of a partial price");
        Check(gold.ExchangeUses.Any(e => e.Destinations.Any(r => r.Location.CanMap)), "Aetherspun Gold has a confirmed NPC and map destination");
        Check(catalog.World.Shops.Values.SelectMany(r => r).All(r => r.Location.CanMap), "All exposed NPC map coordinates are finite and belong to a valid map/territory pair");
        Check(WorldCatalog.IsRequestedItemInstruction("RITEM0") && !WorldCatalog.IsRequestedItemInstruction("ITEM0")
            && !WorldCatalog.IsRequestedItemInstruction("LOC_ITEM1"), "Quest inputs are identified by semantic script names, not arbitrary matching numbers");
        Console.WriteLine($"Located shops: {catalog.World.LocatedShops}; regular merchant locations: {catalog.World.Merchants.Length}");
        Console.WriteLine($"PASS: {checks} installed-data checks; {catalog.MerchantItems} merchant, {catalog.RecipeItems} recipe and {catalog.ExchangeItems} exchange items; {timer.ElapsedMilliseconds} ms");
    }
}
