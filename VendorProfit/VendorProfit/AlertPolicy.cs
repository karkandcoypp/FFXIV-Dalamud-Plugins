namespace VendorProfit;

public static class AlertPolicy
{
    public static string Key(Suggestion s) => s.Quote.Lot.Id != 0 ? $"{s.World}:{s.Quote.Lot.Id}" :
        $"{s.World}:{s.Item.Id}:{s.Quote.Lot.Retainer}:{s.Quote.Lot.Price}:{s.Quote.Lot.Quantity}:{s.Quote.Lot.Hq}";
    public static Suggestion[] NewLots(ScanResult snapshot, uint world, bool chaos, DateTimeOffset now, int maxAge,
        int minProfit, int maxLot, int minRoi, IReadOnlySet<string> seen)
    {
        if (!Policy.Fresh(snapshot.Finished,now,2) || snapshot.Suggestions == null) return [];
        return snapshot.Suggestions.Where(s => (chaos || s.World == world) && s.World != 0 &&
            Policy.Fresh(s.Updated,now,maxAge) && !s.Item.Equipment && !seen.Contains(Key(s)) &&
            Policy.Evaluate(s.Item,s.Quote.Lot) is { } q && q.Cost == s.Quote.Cost && q.Revenue == s.Quote.Revenue &&
            q.Cost <= maxLot && q.Profit >= minProfit && (decimal)q.Profit*100 >= (decimal)q.Cost*minRoi)
            .DistinctBy(Key).OrderByDescending(s=>s.Quote.Profit).ToArray();
    }
}
