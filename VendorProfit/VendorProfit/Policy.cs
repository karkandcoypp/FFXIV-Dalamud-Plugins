namespace VendorProfit;

public sealed record VendorItem(uint Id, string Name, uint Npc, bool CanHq, uint Stack, bool Equipment);
public sealed record Lot(ulong Id, uint Item, string Retainer, uint Price, uint Quantity, uint Tax, bool Hq,
    bool Mannequin = false, bool Damaged = false);
public sealed record Quote(Lot Lot, long Cost, long Revenue)
{
    public long Profit => Revenue - Cost;
}
public sealed record Suggestion(VendorItem Item, Quote Quote, DateTimeOffset Updated, bool EstimatedTax);
public static class Policy
{
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
