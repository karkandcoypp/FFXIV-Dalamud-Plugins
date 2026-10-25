namespace VendorProfit;

public sealed record VendorItem(uint Id, string Name, uint Npc, bool CanHq, uint Stack, bool Equipment);
public sealed record Lot(ulong Id, uint Item, string Retainer, uint Price, uint Quantity, uint Tax, bool Hq,
    bool Mannequin = false, bool Damaged = false, bool Conservative = false);
public sealed record RenderedRow(uint Price, uint Quantity, long Cost, string Retainer);
public sealed record Quote(Lot Lot, long Cost, long Revenue)
{
    public long Profit => Revenue - Cost;
}
public sealed record Suggestion(VendorItem Item, Quote Quote, DateTimeOffset Updated, bool EstimatedTax);
public static class Policy
{
    // Historical opportunities are item IDs only, never historical prices. New discoveries are promoted locally.
    private static readonly uint[] SeedIds = [8144, 8143, 8142, 14934, 5711, 33929, 20696, 25189, 9615, 5678,
        48156, 26735, 5700, 6362, 20695, 6380, 6355, 35034, 33918, 6454, 18502, 41768, 45005, 6363,
        27362, 41757, 33921, 5718, 6398, 27365, 39865, 41769, 37284, 19530, 5723, 20692, 31985,
        6368, 19882, 20397, 25187, 5722, 27957, 6379, 36552, 22489, 6395, 6391, 6399, 37823,
        19561, 6389, 33917, 33941, 18021, 18024, 18254, 5710, 18669, 19566, 18022, 5695, 6373,
        52449, 6350, 25188, 36630, 38273, 9360, 33928, 44146, 27728, 36373, 31553, 12652, 15908,
        13637, 1875, 31326, 3480, 18025, 41761, 45979, 44108, 33907, 17021, 48204, 19918, 5683, 19568];
    public static VendorItem[] QuickItems(IReadOnlyDictionary<uint, VendorItem> catalog, IEnumerable<uint> learned) =>
        learned.Concat(SeedIds).Distinct().Where(id => catalog.TryGetValue(id, out var item) && !item.Equipment)
            .Take(100).Select(id => catalog[id]).ToArray();
    public static RenderedRow? ParseRow(IReadOnlyList<string> orderedTexts)
    {
        // Three numeric columns in visual order: price, quantity, tax-inclusive total; then retainer.
        var numeric = orderedTexts.Where(s => !s.Any(char.IsLetter)).Select(NormalizeNumber).Where(s => s.Length > 0).ToArray();
        if (numeric.Length != 3 || !uint.TryParse(numeric[0], out var price) || !uint.TryParse(numeric[1], out var quantity) ||
            !long.TryParse(numeric[2], out var cost) || price == 0 || quantity == 0 || cost < (long)price * quantity) return null;
        string? retainer = orderedTexts.LastOrDefault(s => s.Any(char.IsLetter));
        return string.IsNullOrWhiteSpace(retainer) ? null : new(price, quantity, cost, retainer.Trim());
    }
    public static Quote? FromRow(VendorItem item, RenderedRow row, IReadOnlyList<Lot> native)
    {
        var matches = native.Where(l => l.Item == item.Id && l.Price == row.Price && l.Quantity == row.Quantity &&
            (long)l.Price * l.Quantity + l.Tax == row.Cost).ToArray();
        if (matches.Length == 0 || matches.Any(l => l.Mannequin || l.Damaged)) return null;
        // Native data has no normal retainer name. Exact displayed money/quantity suffice for NQ's lower bound.
        // HQ value is used only if all indistinguishable native records agree on quality.
        bool conservative = matches.Select(l => l.Hq).Distinct().Count() > 1;
        var lot = matches[0] with { Retainer = row.Retainer, Hq = !conservative && matches[0].Hq, Conservative = conservative };
        return Evaluate(item, lot);
    }
    public static long NpcPrice(uint price, bool hq) => hq ? ((long)price * 11 + 9) / 10 : price;
    public static Quote? Evaluate(VendorItem item, Lot lot)
    {
        if (lot.Item != item.Id || item.Npc == 0 || lot.Quantity == 0 || lot.Quantity > item.Stack ||
            lot.Price == 0 || lot.Mannequin || lot.Damaged || (lot.Hq && !item.CanHq)) return null;
        return new(lot, (long)lot.Price * lot.Quantity + lot.Tax, NpcPrice(item.Npc, lot.Hq) * lot.Quantity);
    }
    public static bool Fresh(DateTimeOffset updated, DateTimeOffset now, int minutes) =>
        updated <= now.AddMinutes(2) && updated >= now.AddMinutes(-minutes);

    // Match rendered contents, not the packet order: the UI may sort or recycle its renderers.
    // Duplicate fingerprints are ambiguous (e.g. NQ and HQ at identical prices) and must not be marked.
    public static Lot? Match(IReadOnlyList<Lot> lots, IReadOnlyCollection<string> texts)
    {
        var matches = lots.Where(l => texts.Contains(l.Retainer) && texts.Contains(l.Quantity.ToString()) &&
            texts.Contains(((long)l.Price * l.Quantity + l.Tax).ToString())).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    public static string NormalizeNumber(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}
