using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace VendorProfit;

public sealed record PurchaseRequest(DateTimeOffset At, ulong Character, uint World, ulong Listing,
    uint Item, uint Quantity, uint Price, uint Tax, bool Hq);

public sealed class PurchaseReceiptTracker
{
    private PurchaseRequest? pending;
    private DateTimeOffset ambiguousUntil;
    public bool Request(PurchaseRequest request)
    {
        if (request.At < ambiguousUntil) return false;
        if (pending != null && request.At - pending.At <= TimeSpan.FromSeconds(30))
        { pending = null; ambiguousUntil = request.At.AddSeconds(30); return false; }
        pending = request; return true;
    }
    public void Clear() { pending = null; ambiguousUntil = default; }
    public PurchaseRequest? Confirm(ulong character, uint world, uint item, uint quantity, DateTimeOffset at)
    {
        var request = pending; pending = null;
        if (request == null || request.Character != character || request.World != world ||
            request.Quantity != quantity || quantity == 0 || at < request.At || at - request.At > TimeSpan.FromSeconds(30)) return null;
        bool matches = item == request.Item || request.Hq && item == request.Item + 1_000_000u;
        return matches ? request : null;
    }
}

public sealed record TradePurchase(ulong Listing, DateTimeOffset At, uint World, uint Item, string Name,
    bool? Hq, uint Quantity, long Cost, long ExpectedRevenue, uint SoldQuantity = 0, string Source = "Market Board")
{
    [JsonIgnore] public long SoldRevenue => checked(ExpectedRevenue / Quantity * SoldQuantity);
    [JsonIgnore] public long SoldCost => (long)((decimal)Cost * SoldQuantity / Quantity);
    [JsonIgnore] public long RealizedProfit => SoldRevenue - SoldCost;
    [JsonIgnore] public long RemainingCost => Cost - SoldCost;
    [JsonIgnore] public long RemainingProfit => ExpectedRevenue - SoldRevenue - RemainingCost;
}

public sealed class TradeBook
{
    public int Schema { get; set; } = 1;
    public ulong Character { get; set; }
    public List<TradePurchase> Purchases { get; set; } = [];
}

public static class TradeStatistics
{
    public static TradePurchase? FromReceipt(PurchaseRequest request, VendorItem item)
    {
        if (request.Character == 0 || request.World == 0 || request.Listing == 0 || item.Equipment) return null;
        var lot = new Lot(request.Listing, request.Item, "", request.Price, request.Quantity, request.Tax, request.Hq);
        var quote = Policy.Evaluate(item, lot);
        return quote is { Profit: > 0 } ? new(request.Listing, request.At, request.World, item.Id, item.Name,
            request.Hq, request.Quantity, quote.Cost, quote.Revenue) : null;
    }
    public static bool Valid(TradePurchase? p) => p != null && p.Listing != 0 && p.Item != 0 && !string.IsNullOrWhiteSpace(p.Name) &&
        p.Quantity > 0 && p.SoldQuantity <= p.Quantity && p.Cost > 0 && p.ExpectedRevenue > p.Cost &&
        p.ExpectedRevenue <= 100_000_000_000_000 &&
        p.ExpectedRevenue % p.Quantity == 0 && p.At != default;
    public static bool Valid(TradeBook book, ulong character) => character != 0 && book.Schema == 1 && book.Character == character &&
        book.Purchases != null && book.Purchases.Count <= 50_000 && book.Purchases.All(Valid) &&
        book.Purchases.Select(p => p.Listing).Distinct().Count() == book.Purchases.Count;
    public static bool Add(TradeBook book, TradePurchase purchase)
    {
        if (!Valid(purchase)) return false;
        int existing = book.Purchases.FindIndex(p => p.Listing == purchase.Listing);
        if (existing >= 0)
        {
            var old = book.Purchases[existing];
            if (old.Source != "Старый журнал" || purchase.Source != "Market Board" ||
                old.Item != purchase.Item || old.Quantity != purchase.Quantity || old.Cost != purchase.Cost || old.ExpectedRevenue != purchase.ExpectedRevenue) return false;
            book.Purchases[existing] = purchase with { SoldQuantity = old.SoldQuantity };
            return true;
        }
        if (book.Purchases.Count >= 50_000) throw new InvalidDataException("Достигнут предел истории; сохраните CSV перед продолжением учёта.");
        book.Purchases.Add(purchase); return true;
    }
    public static TradePurchase? Import(BuyEntry entry, string name)
    {
        if (entry.Status != "Подтверждено" || entry.Profit <= 0) return null;
        var purchase = new TradePurchase(entry.Listing, entry.At, 0, entry.Item, name, null, entry.Quantity,
            entry.Cost, checked(entry.Cost + entry.Profit), Source: "Старый журнал");
        return Valid(purchase) ? purchase : null;
    }
    public static TradePurchase MarkSold(TradePurchase purchase, uint additionalQuantity)
    {
        if (!Valid(purchase) || additionalQuantity == 0 || additionalQuantity > purchase.Quantity - purchase.SoldQuantity)
            throw new ArgumentOutOfRangeException(nameof(additionalQuantity));
        return purchase with { SoldQuantity = purchase.SoldQuantity + additionalQuantity };
    }
    public static string Csv(IEnumerable<TradePurchase> purchases)
    {
        string Field(string text)
        {
            if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("Listing,PurchaseTime,World,Item,Name,HQ,Quantity,CostWithTax,ExpectedNpcRevenue,SoldQuantity,ManuallyConfirmedRevenue,RealizedProfit,RemainingCost,RemainingProfit,Source\r\n");
        foreach (var p in purchases)
            csv.AppendLine(string.Join(',', new[] { p.Listing.ToString(), p.At.ToString("o", CultureInfo.InvariantCulture),
                p.World.ToString(), p.Item.ToString(), p.Name, p.Hq?.ToString() ?? "unknown", p.Quantity.ToString(),
                p.Cost.ToString(), p.ExpectedRevenue.ToString(), p.SoldQuantity.ToString(), p.SoldRevenue.ToString(),
                p.RealizedProfit.ToString(), p.RemainingCost.ToString(), p.RemainingProfit.ToString(), p.Source }.Select(Field)));
        return csv.ToString();
    }
}
