using System.Text.Json;
using VendorProfit;

static class StatisticsChecks
{
    public static void Run(Action<bool,string> check)
    {
        var now=DateTimeOffset.UtcNow;
        var item=new VendorItem(8144,"Clear Demimateria III",5000,false,99,false);
        var request=new PurchaseRequest(now,123,80,700,8144,3,4532,679,false);
        var tracker=new PurchaseReceiptTracker();
        check(tracker.Confirm(123,80,8144,3,now)==null,"stats: a receipt without a captured request is not a purchase with known cost");
        tracker.Request(request);
        check(tracker.Confirm(123,80,8144,3,now.AddSeconds(1))==request,"stats: matching server receipt confirms captured purchase");
        check(tracker.Confirm(123,80,8144,3,now.AddSeconds(2))==null,"stats: repeated receipt cannot double count");
        foreach(var scenario in new[]{(124ul,80u,8144u,3u,1),(123ul,83u,8144u,3u,1),(123ul,80u,8145u,3u,1),
            (123ul,80u,8144u,0u,1),(123ul,80u,8144u,2u,1),(123ul,80u,8144u,3u,31)})
        {
            tracker.Request(request);
            check(tracker.Confirm(scenario.Item1,scenario.Item2,scenario.Item3,scenario.Item4,now.AddSeconds(scenario.Item5))==null,
                "stats: mismatched character/world/item/quantity or expired response excluded");
        }
        tracker.Request(request with {Hq=true});
        check(tracker.Confirm(123,80,1_008_144,3,now)!=null,"stats: encoded HQ receipt accepted only for HQ request");
        tracker.Request(request);
        check(tracker.Confirm(123,80,1_008_144,3,now)==null,"stats: HQ receipt cannot match NQ request");
        tracker.Request(request);tracker.Clear();
        check(tracker.Confirm(123,80,8144,3,now)==null,"stats: session changes clear pending requests");
        tracker.Request(request);
        check(!tracker.Request(request with {At=now.AddSeconds(1),Listing=701}) && tracker.Confirm(123,80,8144,3,now.AddSeconds(2))==null,
            "stats: overlapping same-item requests cannot misattribute a receipt to another price");
        check(tracker.Request(request with {At=now.AddSeconds(32)}) && tracker.Confirm(123,80,8144,3,now.AddSeconds(33))!=null,
            "stats: unambiguous purchases resume after the overlap timeout");
        var trade=TradeStatistics.FromReceipt(request,item)!;
        check(trade.Cost==14275 && trade.ExpectedRevenue==15000,"stats: complete stack cost includes buyer tax once");
        check(trade.RealizedProfit==0 && trade.RemainingProfit==725 && trade.RemainingCost==14275,"stats: unsold stock is not realized revenue or profit");
        var part=TradeStatistics.MarkSold(trade,1);
        check(part.SoldRevenue==5000 && part.SoldCost==4758 && part.RealizedProfit==242 && part.RemainingCost==9517,
            "stats: partial NPC sale allocates whole-stack cost including tax");
        var complete=TradeStatistics.MarkSold(part,2);
        check(complete.RealizedProfit==725 && complete.RemainingCost==0 && complete.RemainingProfit==0,
            "stats: completing partial sales assigns every gil of cost exactly once");
        bool rejected=false;try{TradeStatistics.MarkSold(part,3);}catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"stats: sale cannot exceed unmarked quantity");
        check(TradeStatistics.FromReceipt(request,item with {Equipment=true})==null,"stats: unknown equipment condition excluded");
        check(TradeStatistics.FromReceipt(request with {Price=5000},item)==null,"stats: unprofitable unrelated purchases excluded");
        var hq=TradeStatistics.FromReceipt(request with {Hq=true},item with {CanHq=true})!;
        check(hq.ExpectedRevenue==16500,"stats: HQ vendor revenue uses the same verified price contract");
        var book=new TradeBook{Character=123};
        check(TradeStatistics.Add(book,trade) && !TradeStatistics.Add(book,trade),"stats: listing deduplication survives repeated imports/events");
        var restored=JsonSerializer.Deserialize<TradeBook>(JsonSerializer.Serialize(book))!;
        check(TradeStatistics.Valid(restored,123) && !TradeStatistics.Add(restored,trade),"stats: saved history retains purchase identity");
        check(!TradeStatistics.Valid(restored,124),"stats: another character cannot reuse purchase history");
        var old=new BuyEntry(now,701,8144,3,14275,725,"Подтверждено");
        check(TradeStatistics.Import(old,item.Name) is {World:0,Hq:null,ExpectedRevenue:15000},"stats: legacy confirmed purchases imported without inventing world or HQ");
        check(TradeStatistics.Import(old with {Status="Ожидается"},item.Name)==null,"stats: attempted or ambiguous auto-buys never become confirmed statistics");
        var backfilled=TradeStatistics.Import(old with {Listing=trade.Listing},item.Name)!;
        var migrated=new TradeBook{Character=123,Purchases=[TradeStatistics.MarkSold(backfilled,1)]};
        check(TradeStatistics.Add(migrated,trade) && migrated.Purchases.Count==1 && migrated.Purchases[0].World==80 && migrated.Purchases[0].SoldQuantity==1,
            "stats: later receipt enriches a legacy record without duplicating it or losing sale marks");
        var soldRestored=JsonSerializer.Deserialize<TradeBook>(JsonSerializer.Serialize(migrated))!;
        check(TradeStatistics.Valid(soldRestored,123) && soldRestored.Purchases[0].RealizedProfit==242,
            "stats: partial sales and exact allocated tax survive save/reload");
        check(!TradeStatistics.Valid(new TradeBook{Schema=2,Character=123},123),"stats: unknown statistics schemas cannot be overwritten");
        check(!TradeStatistics.Valid(new TradeBook{Character=123,Purchases=[trade,trade]},123),"stats: duplicated history entries cannot inflate totals");
        check(TradeStatistics.FromReceipt(request with {Quantity=100},item)==null && TradeStatistics.FromReceipt(request with {Listing=0},item)==null,
            "stats: invalid stack size and missing listing identity excluded");
        check(!TradeStatistics.Valid(trade with {SoldQuantity=4}) && !TradeStatistics.Valid(trade with {Quantity=0}),"stats: malformed sold quantities rejected before calculating totals");
        var cash=TradeStatistics.Csv(new[]{complete with {Name="Material, \"HQ\""},trade with {Listing=702,Name="=formula"}});
        check(cash.Contains("\"Material, \"\"HQ\"\"\"") && cash.Contains("\"'=formula\"") && cash.Contains("ManuallyConfirmedRevenue"),
            "stats: CSV quotes names, neutralizes formulas and labels manually confirmed sales");
    }
}
