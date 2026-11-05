using System.Text.Json;
using VendorProfit;

static class NpcSaleChecks
{
    public static void Run(Action<bool,string> check)
    {
        var now=DateTimeOffset.UtcNow;var key=new StockKey(8144,false);
        NpcShopSnapshot Frame(int seconds,long count,long gil,BuybackStock[]? back=null,NpcSaleIntent? intent=null)=>
            new(now.AddSeconds(seconds),123,80,1,gil,new(){{key,count}},back??[],intent);
        var proof=new BuybackStock(key,2,100);
        var observer=new NpcSaleObserver();
        check(observer.Observe(Frame(0,10,1000)).Length==0,"NPC: opening shop establishes a baseline, not a historical sale");
        var sale=observer.Observe(Frame(1,8,1200,[proof]));
        check(sale is [{Quantity:2,UnitPrice:100,Buyback:false}],"NPC: buyback evidence plus exact inventory/gil changes confirms sale");
        check(observer.Observe(Frame(2,8,1200,[proof])).Length==0,"NPC: unchanged state cannot duplicate a sale");
        var refund=observer.Observe(Frame(3,10,1000));
        check(refund is [{Quantity:2,Buyback:true}],"NPC: removing buyback stock plus inventory/gil reversal confirms repurchase");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,8,1000,[proof])).Length==0,"NPC: inventory decrease alone never confirms revenue");
        check(observer.Observe(Frame(2,8,1200,[proof])).Length==1,"NPC: delayed gil packet joins the same inventory operation");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,10,1200)).Length==0,"NPC: gil receipt alone is not an NPC sale");
        observer.Observe(Frame(7,10,1200));
        check(observer.Skipped,"NPC: unresolved changes expire without inventing profit");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,8,1200)).Length==0,"NPC: discard plus unrelated gil does not qualify without shop sale evidence");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,12,800)).Length==0,"NPC: normal purchases from NPC are not mistaken for buyback");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,8,1250,[proof])).Length==0,"NPC: unrelated extra gil invalidates the receipt");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        check(observer.Observe(Frame(1,8,1200,[proof]) with {World=83}).Length==0,"NPC: world change resets evidence");
        observer.Observe(null);
        check(observer.Observe(Frame(2,8,1200,[proof])).Length==0,"NPC: reopening cannot count previous shop contents");
        var identical=Enumerable.Repeat(proof,10).ToArray();
        observer.Reset();observer.Observe(Frame(0,10,1000,identical));
        observer.Observe(Frame(1,10,1000,identical,new(key,2,100,4)));
        check(observer.Observe(Frame(2,8,1200,identical)).Length==1,"NPC: captured sale transaction covers a full identical buyback list");
        observer.Reset();observer.Observe(Frame(0,10,1000));
        observer.Observe(Frame(1,10,1000,intent:new(key,2,100,4)));
        observer.Observe(Frame(7,10,1000));
        check(observer.Observe(Frame(8,8,1200)).Length==0,"NPC: expired or cancelled sale intent cannot authorize future changes");
        var first=new TradePurchase(1,now,80,8144,"Demi",false,3,14275,15000);
        var second=new TradePurchase(2,now.AddSeconds(1),80,8144,"Demi",false,2,8000,10000);
        var book=new TradeBook{Character=123,Purchases=[first,second]};
        check(NpcSaleObserver.Apply(book,new(key,4,5000,false))==4 && book.Purchases[0].SoldQuantity==3 && book.Purchases[1].SoldQuantity==1,
            "NPC: merged inventory sales consume purchase lots FIFO");
        check(book.Purchases.Sum(p=>p.RealizedProfit)==1725 && book.Purchases.Sum(p=>p.RemainingCost)==4000,
            "NPC: automatic partial sale preserves exact tax-inclusive costs");
        check(NpcSaleObserver.Apply(book,new(key,1,5000,true))==1 && book.Purchases[1].SoldQuantity==0,
            "NPC: buyback reverses latest automatic allocation");
        check(book.Purchases.Sum(p=>p.RealizedProfit)==725,"NPC: repurchased units no longer count as realized profit");
        check(NpcSaleObserver.Apply(book,new(new(8144,true),1,5500,false))==0,"NPC: HQ sale cannot consume NQ history");
        check(NpcSaleObserver.Apply(book,new(key,1,4999,false))==0,"NPC: changed vendor value is not silently converted to expected revenue");
        check(NpcSaleObserver.Apply(book,new(key,99,5000,false))==2,"NPC: untracked surplus cannot exceed recorded purchases");
        var restored=JsonSerializer.Deserialize<TradeBook>(JsonSerializer.Serialize(book))!;
        check(TradeStatistics.Valid(restored,123) && restored.Purchases.All(p=>p.AutoSoldQuantity==p.SoldQuantity),"NPC: automatic allocation provenance survives restart");
        var manual=new TradeBook{Character=123,Purchases=[first with {SoldQuantity=1}]};
        check(NpcSaleObserver.Apply(manual,new(key,1,5000,true))==0,"NPC: buyback does not erase unrelated manual sale corrections");
        var item=new VendorItem(8144,"Demi",5000,false,99,false);var lot=new Lot(5,8144,"a",1,1,0,false);
        Suggestion Offer(uint world,long revenue)=>new(item,new(lot,1,revenue),now,false,world);
        var offers=new[]{Offer(83,1001),Offer(80,11),Offer(80,51),Offer(83,101)};
        var sorted=Policy.CurrentWorldFirst(offers,80,80);
        check(sorted[0].World==80 && sorted[0].Quote.Profit==50 && sorted[1].Quote.Profit==10 && sorted[2].Quote.Profit==1000,
            "world order: current world first, descending profit inside each priority group");
        check(Policy.CurrentWorldFirst(offers,83,80)[0].World==83,"world order: travelling changes priority without modifying prices");
        check(Policy.CurrentWorldFirst(new[]{Offer(0,5),Offer(83,10)},83,80)[0].World==83,
            "world order: legacy anchor-world offer is not relabelled as the visited world");
    }
}
