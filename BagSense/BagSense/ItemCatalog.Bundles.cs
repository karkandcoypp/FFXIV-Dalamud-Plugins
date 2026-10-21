namespace BagSense;

public sealed partial class ItemCatalog
{
    private readonly Dictionary<uint, ItemBundle[]> bundleCache = new();

    public static string ResearchBundleKey(ResearchUse use) => "research:" + use.WikiTitle + ":" + use.Context + ":" + string.Join(',', use.Costs!.Select(c => $"{c.ItemId}:{c.Count}:{c.Hq}:{c.Collectability}"))
        + "|" + use.Zone + "|" + string.Join(',', use.Npcs ?? []) + "|" + string.Join(',', use.Quests ?? []) + "|" + string.Join(';', use.Requirements ?? []);
    public ItemBundle[] Bundles(uint itemId)
    {
        if (bundleCache.TryGetValue(itemId, out var cached)) return cached;
        var item = Find(itemId);
        if (item == null) return [];
        var bundles = new List<ItemBundle>();
        foreach (var use in item.QuestUses.Where(q => !q.Reward).DistinctBy(q => q.QuestId))
        {
            var ids = (Database?.Needs.Where(n => n.QuestId == use.QuestId).Select(n => n.ItemId) ?? [])
                .Concat(World.QuestItems.Where(p => p.Value.Any(q => !q.Reward && q.QuestId == use.QuestId)).Select(p => p.Key)).Distinct();
            var inputs = ids.Select(id => (Item: Find(id), Use: QuestUses(id).FirstOrDefault(q => q.QuestId == use.QuestId && !q.Reward)))
                .Where(p => p.Item != null && p.Use != null).ToArray();
            if (inputs.Length < 2) continue;
            // A quest-wide association is not proof of simultaneous consumption. Explicit objective matches only.
            foreach (var stage in inputs.GroupBy(p => (p.Use!.TodoIndex, p.Use.CompleteSequence)))
            {
                var parts = stage.Select(p => new BundlePart(p.Item!.Id, p.Item.Name, p.Use!.RequiredCount, p.Use.Quality, Objective: p.Use.TodoIndex)).ToArray();
                bool exact = stage.Key.TodoIndex.HasValue && stage.All(p => p.Use!.Objective.Length > 0);
                bundles.Add(new($"quest:{use.QuestId}:{stage.Key.TodoIndex}:{stage.Key.CompleteSequence}", use.Name, "Quest", parts, use.QuestId,
                    stage.SelectMany(p => p.Use!.IssuerContact ? [] : p.Use.Contacts).DistinctBy(n => n.Key).ToArray(), exact, use.Name));
            }
        }
        foreach (var offer in item.ExchangeUses.Where(e => e.FullCosts.Length > 1))
            bundles.Add(new("exchange:" + offer.Key, offer.Reward + " · " + offer.Shop, "Exchange", offer.FullCosts.Select(c => new BundlePart(c.ItemId, c.Name, c.Count <= int.MaxValue ? (int)c.Count : null, Collectability: c.Collectability)).ToArray(),
                Contacts: offer.Destinations.Select(d => d.Location).DistinctBy(n => n.Key).ToArray(), ResultId: offer.RewardId));
        foreach (var use in item.ResearchUses.Where(u => u.Kind is "RelicExchange" or "WikiExchange" && u.Costs is { Length: > 1 }))
            bundles.Add(new(ResearchBundleKey(use), use.Context + (use.Zone.Length > 0 ? " · " + use.Zone : ""), "Research",
                use.Costs!.Select(c => new BundlePart(c.ItemId, c.Name, c.Count, c.Hq ? "hq" : "any", c.Collectability)).ToArray(),
                Exact: !(use.Requirements ?? []).Any(r => r.StartsWith("Unresolved additional cost")), Wiki: use.WikiTitle, ResultId: use.Results?.FirstOrDefault() ?? 0));
        foreach (var recipe in item.CraftingLinks.DistinctBy(r => r.RecipeId))
            if (RecipeIngredients.TryGetValue(recipe.RecipeId, out var costs) && costs.Length > 1)
                bundles.Add(new($"recipe:{recipe.RecipeId}", recipe.Result, "Recipe", costs.Select(c => new BundlePart(c.ItemId, c.Name, (int)c.Count)).ToArray(), ResultId: recipe.ResultId));
        return bundleCache[itemId] = bundles.DistinctBy(b => b.Key).Select(b => b with
        {
            Parts = b.Parts.Select(p => p with { Scanned = Find(p.ItemId)?.Role != "Currency"
                && !(items.TryGetRow(p.ItemId, out var row) && row.FilterGroup is 29 or 38 or 47 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59) }).ToArray()
        }).ToArray();
    }

    public ItemBundle ForCharacter(ItemBundle bundle, InventorySnapshot snapshot) => bundle.Kind != "Quest" ? bundle : bundle with
    {
        Parts = bundle.Parts.Select(p => Find(p.ItemId)?.QuestUses.FirstOrDefault(q => !q.Reward && q.QuestId == bundle.QuestId) is { } use
            ? BundlePolicy.QuestPart(p.ItemId, p.Name, use, snapshot, p.Scanned) : p).Where(p => p != null).Select(p => p!).ToArray()
    };
}
