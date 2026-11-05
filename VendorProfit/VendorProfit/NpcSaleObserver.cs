namespace VendorProfit;

public readonly record struct StockKey(uint Item, bool Hq);
public sealed record BuybackStock(StockKey Key, uint Quantity, long UnitPrice);
public sealed record NpcSaleIntent(StockKey Key, uint Quantity, long UnitPrice, int Slot);
public sealed record NpcShopSnapshot(DateTimeOffset At, ulong Character, uint World, nint Shop, long Gil,
    Dictionary<StockKey,long> Stock, BuybackStock[] Buyback, NpcSaleIntent? Intent = null);
public sealed record NpcStockChange(StockKey Key, uint Quantity, long UnitPrice, bool Buyback);

// Read-only evidence matcher: a shop operation plus exact inventory AND gil changes.
public sealed class NpcSaleObserver
{
    private NpcShopSnapshot? baseline;
    private DateTimeOffset? changingSince;
    private NpcSaleIntent? lastIntent;
    private DateTimeOffset lastIntentAt;
    private readonly List<NpcSaleIntent> intents = [];
    public bool Skipped { get; private set; }
    public void Reset(){baseline=null;changingSince=null;lastIntent=null;intents.Clear();}
    public NpcStockChange[] Observe(NpcShopSnapshot? frame)
    {
        Skipped=false;
        if(frame==null){Reset();return [];}
        if(baseline==null || frame.Character!=baseline.Character || frame.World!=baseline.World || frame.Shop!=baseline.Shop)
        {Reset();baseline=frame;lastIntent=frame.Intent;return [];}
        if(frame.Intent!=null && frame.Intent!=lastIntent){intents.Add(frame.Intent);lastIntentAt=frame.At;}
        if(frame.At-lastIntentAt>TimeSpan.FromSeconds(5))intents.Clear();
        lastIntent=frame.Intent;
        var changes=baseline.Stock.Keys.Union(frame.Stock.Keys).Select(k=>(Key:k,Delta:frame.Stock.GetValueOrDefault(k)-baseline.Stock.GetValueOrDefault(k)))
            .Where(p=>p.Delta!=0).ToArray();
        long gil=frame.Gil-baseline.Gil;
        var added=Difference(frame.Buyback,baseline.Buyback);
        var removed=Difference(baseline.Buyback,frame.Buyback);
        if(changes.Length==0 && gil==0 && added.Length==0 && removed.Length==0)
        {baseline=frame;changingSince=null;return [];}
        changingSince ??= frame.At;
        bool selling=gil>0 && changes.Length>0 && changes.All(c=>c.Delta<0);
        bool buyingBack=gil<0 && changes.Length>0 && changes.All(c=>c.Delta>0);
        var output=new List<NpcStockChange>();long total=0;
        if(selling || buyingBack)
        {
            foreach(var c in changes)
            {
                long count=Math.Abs(c.Delta);
                if(count>uint.MaxValue){output.Clear();break;}
                var proof=(selling?added:removed).Where(b=>b.Key==c.Key).ToArray();
                var intent=selling?intents.Where(i=>i.Key==c.Key).ToArray():[];
                var prices=proof.Select(p=>p.UnitPrice).Concat(intent.Select(i=>i.UnitPrice)).Distinct().ToArray();
                if(prices.Length!=1 || prices[0]<=0 || proof.Length==0 && intent.Sum(i=>(long)i.Quantity)<count)
                {output.Clear();break;}
                total=checked(total+count*prices[0]);
                output.Add(new(c.Key,(uint)count,prices[0],buyingBack));
            }
            if(output.Count==changes.Length && total==Math.Abs(gil))
            {baseline=frame;changingSince=null;intents.Clear();return output.ToArray();}
        }
        // Item and gil packets can arrive in separate frames. Never infer a sale from only one.
        if(frame.At-changingSince.Value>TimeSpan.FromSeconds(5))
        {Skipped=true;baseline=frame;changingSince=null;intents.Clear();}
        return [];
    }
    private static BuybackStock[] Difference(BuybackStock[] current,BuybackStock[] previous)
    {
        var counts=previous.GroupBy(b=>b).ToDictionary(g=>g.Key,g=>g.Count());var result=new List<BuybackStock>();
        foreach(var b in current)
            if(counts.GetValueOrDefault(b)>0)counts[b]--;else result.Add(b);
        return result.ToArray();
    }
    public static uint Apply(TradeBook book,NpcStockChange sale)
    {
        uint left=sale.Quantity;
        var eligible=book.Purchases.Where(p=>p.Item==sale.Key.Item && (p.Hq==null || p.Hq==sale.Key.Hq) &&
            p.ExpectedRevenue/p.Quantity==sale.UnitPrice);
        var ordered=sale.Buyback?eligible.OrderByDescending(p=>p.At):eligible.OrderBy(p=>p.At);
        foreach(var p in ordered.ToArray())
        {
            uint available=sale.Buyback?p.AutoSoldQuantity:p.Quantity-p.SoldQuantity;
            uint count=Math.Min(left,available);if(count==0)continue;
            int index=book.Purchases.IndexOf(p);
            book.Purchases[index]=p with {SoldQuantity=sale.Buyback?p.SoldQuantity-count:p.SoldQuantity+count,
                AutoSoldQuantity=sale.Buyback?p.AutoSoldQuantity-count:p.AutoSoldQuantity+count};
            left-=count;if(left==0)break;
        }
        return sale.Quantity-left;
    }
}
