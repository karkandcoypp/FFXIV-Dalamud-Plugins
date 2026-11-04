namespace VendorProfit;

public sealed record ScannerRequest(int Schema, uint World, string WorldName, VendorItem[] Items,
    int IntervalSeconds, int MaximumAgeMinutes, int MaximumLot, int MinimumProfit, int MinimumRoi);
public static class ExternalFeed
{
    // Upload time alone must not make an unchanged cached listing eligible for another game query.
    public static string Ticket(Suggestion s) => $"{s.World}:{s.Item.Id}:{s.Quote.Lot.Id}:{s.Quote.Lot.Price}:{s.Quote.Lot.Quantity}:{s.Quote.Lot.Tax}:{s.Quote.Lot.Hq}";
    public static Suggestion[] Select(ScanResult snapshot, uint world, DateTimeOffset now, IReadOnlySet<string> seen)
    {
        if (snapshot.Scope != "External" || snapshot.World != world || !Policy.Fresh(snapshot.Finished, now, 2) || snapshot.Suggestions == null) return [];
        return snapshot.Suggestions.Where(s => s.World == world && s.Quote.Lot.Item == s.Item.Id && s.Quote.Lot.Id != 0 &&
            !s.Item.Equipment && !seen.Contains(Ticket(s))).ToArray();
    }
}
