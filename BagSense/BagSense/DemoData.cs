namespace BagSense;

public static class DemoData
{
    // Deliberately synthetic IDs: sample decisions cannot refer to real character items.
    public static (InventorySnapshot Snapshot, CharacterData Data) Create()
    {
        var items = new Dictionary<uint, ItemFacts>
        {
            [900001] = new(900001, "Copper Ore (sample)", 0, "Material", "", SellPrice: 1, Offers: [new("Material Supplier", 2, false)], Recipes: ["Copper Ingot"]),
            [900002] = new(900002, "Iron Ore (sample)", 0, "Material", "", SellPrice: 1, Offers: [new("Material Supplier", 18, false)], Recipes: ["Iron Ingot"]),
            [900003] = new(900003, "Wind-up Friend (sample)", 0, "Minion", "", Unlocked: false),
            [900004] = new(900004, "Adventurer's Coat (sample)", 0, "Body", "", Equipment: true, SellPrice: 45, Desynth: true),
            [900005] = new(900005, "Favorite Hat (sample)", 0, "Head", "", Equipment: true, SellPrice: 30),
            [900006] = new(900006, "Old Token (sample)", 0, "Other", "", Exchanges: [new("Token Exchange", "A reward to review", 10)]),
            [900007] = new(900007, "Special Ore (sample)", 0, "Material", "", SellPrice: 2, Recipes: ["A special recipe"]),
            [900008] = new(900008, "Melded Ring (sample)", 0, "Ring", "", Equipment: true, SellPrice: 20, Desynth: true),
        };
        InventoryStack[] stacks = [new(900001, ContainerKind.Bag, 0, 35), new(900002, ContainerKind.Bag, 1, 12),
            new(900002, ContainerKind.Bag, 2, 3), new(900003, ContainerKind.Bag, 3, 1), new(900004, ContainerKind.Armoury, 0, 1),
            new(900005, ContainerKind.Armoury, 1, 1), new(900006, ContainerKind.Bag, 4, 7), new(900007, ContainerKind.Bag, 5, 8),
            new(900008, ContainerKind.Armoury, 2, 1, ValuableInstance: true)];
        var snapshot = new InventorySnapshot(ulong.MaxValue, "Demo / демонстрация", DateTimeOffset.UtcNow, stacks, items,
            new Dictionary<uint, string[]> { [900005] = ["My leveling gear"] }, true, 140);
        var data = new CharacterData();
        data.Goals.Add(new() { Name = "First crafting project / Первый крафт", Items = new() { [900001] = 20, [900007] = 10 } });
        data.Edit(900001).Note = "For ingots / Для слитков";
        CleanupPolicy.AddToPlan(snapshot, data, CleanupPolicy.Evaluate(snapshot, data, 900002, DateTimeOffset.UtcNow), PlanAction.Vendor, 15);
        return (snapshot, data);
    }
}
