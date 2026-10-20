namespace BagSense;

public static class DestinationPolicy
{
    public static string Key(ShopRoute route) => $"{route.ShopId}:{route.Location.Key}:{route.Menu}:"
        + string.Join(',', route.Requirements.OrderBy(r => r.Kind).ThenBy(r => r.Id).Select(r => $"{r.Kind}:{r.Id}"));
    public static IEnumerable<ShopRoute> Exchanges(ItemFacts item, string exchangeKey = "") =>
        item.ExchangeUses.Where(e => exchangeKey.Length == 0 || e.Key == exchangeKey)
            .SelectMany(e => e.Destinations).DistinctBy(Key);

    public static NpcLocation? ForPlan(ItemFacts item, PlanEntry entry, uint territory)
    {
        var exchange = item.ExchangeUses.FirstOrDefault(e => e.Key == entry.ExchangeKey)
            ?? (entry.ExchangeKey.Length == 0 ? item.ExchangeUses.FirstOrDefault() : null);
        var locations = entry.Action == PlanAction.Vendor ? item.SaleLocations
            : entry.Action == PlanAction.ExchangeReview && exchange != null ? exchange.Destinations
                .Where(r => entry.RouteKey.Length == 0 || Key(r) == entry.RouteKey).Select(r => r.Location).ToArray() : [];
        if (entry.DestinationKey.Length > 0)
            return locations.FirstOrDefault(l => l.Key == entry.DestinationKey && l.CanMap);
        return locations.Where(l => l.CanMap).OrderByDescending(l => l.Territory == territory).ThenBy(l => l.Npc).FirstOrDefault();
    }

    public static IEnumerable<RouteRequirement> Requirements(ExchangeUse exchange, ShopRoute route) =>
        route.Requirements.Concat(exchange.Conditions).DistinctBy(r => (r.Kind, r.Id, r.Name));

    public static int? Affordable(ExchangeUse exchange, InventorySnapshot snapshot)
    {
        if (exchange.FullCosts.Length == 0 || exchange.FullCosts.Any(c => c.ItemId == 0 || c.Count == 0 || c.Collectability > 0)) return null;
        // Currency tabs, crystal stores and unscanned containers are not part of this snapshot.
        if (exchange.FullCosts.Any(c => c.ItemId < 100)) return null;
        return exchange.FullCosts.GroupBy(c => c.ItemId).Min(g => (int)(snapshot.Quantity(g.Key) / Math.Max(1L, g.Sum(c => (long)c.Count))));
    }
}
