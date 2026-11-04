using VendorProfit;

static class AutoBuyChecks
{
    public static void Run(Action<bool,string> check)
    {
        var item = new VendorItem(8144, "Clear Demimateria III", 5000, false, 99, false);
        var lot = new Lot(77, 8144, "test", 100, 1, 5, false);
        var quote = Policy.Evaluate(item, lot)!;
        var limits = new BuyLimits();
        var context = new BuyContext(123, 80, true, false, 1_000_000, 40);
        BuyLedger Ledger() => new() { Character = 123 };
        check(BuyPolicy.Allowed(quote, limits, Ledger(), context), "auto: eligible fresh stack within all limits");
        check(!BuyPolicy.Allowed(quote, limits, Ledger(), context with { BoardOpen = false }), "auto: closed board forbids spending");
        check(!BuyPolicy.Allowed(quote, limits, Ledger(), context with { Blocked = true }), "auto: unrelated confirmation dialog forbids spending");
        check(!BuyPolicy.Allowed(quote, limits, Ledger(), context with { Character = 124 }), "auto: another character forbidden");
        check(!BuyPolicy.Allowed(quote, limits, Ledger(), context with { Gil = 104 }), "auto: insufficient gil forbidden");
        check(!BuyPolicy.Allowed(quote, limits, Ledger(), context with { FreeSlots = 0 }), "auto: full inventory forbidden");
        var restricted = new BuyLimits { MaximumLot = 104 };
        check(!BuyPolicy.Allowed(quote, restricted, Ledger(), context), "auto: stack cost includes tax in price cap");
        var spent = Ledger(); spent.Charged = limits.Budget - 104;
        check(!BuyPolicy.Allowed(quote, limits, spent, context), "auto: remaining budget cannot be exceeded");
        var attempted = Ledger(); attempted.Entries.Add(new(DateTimeOffset.UtcNow, lot.Id, lot.Item, 1, quote.Cost, quote.Profit, "pending"));
        check(!BuyPolicy.Allowed(quote, limits, attempted, context), "auto: previously attempted listing cannot repeat");
        check(!BuyPolicy.Allowed(quote with { Revenue = quote.Cost + 1 }, limits, Ledger(), context), "auto: low profit excluded");
        check(!BuyPolicy.Allowed(quote with { Cost = 2000, Revenue = 2100 }, limits, Ledger(), context), "auto: ROI floor enforced independently of absolute profit");

        var now = DateTimeOffset.UtcNow;
        var suggestion = new Suggestion(item, quote, now, false, World: 80);
        var catalog = new Dictionary<uint,VendorItem> { [item.Id] = item };
        (AutoBuyer Buyer, FakeBuyGateway Gateway, List<string> Calls) Buyer(bool failSave = false)
        {
            var calls = new List<string>(); var gateway = new FakeBuyGateway(lot, calls);
            var buyer = new AutoBuyer(gateway, _ => { calls.Add("save"); if (failSave) throw new IOException("disk failure"); });
            buyer.Attach(Ledger()); buyer.Resume(); buyer.Begin([suggestion], catalog, context);
            return (buyer,gateway,calls);
        }
        var dry = Buyer();
        dry.Buyer.Tick(now, context, limits, true, true);
        dry.Buyer.Tick(now.AddSeconds(1), context, limits, true, true);
        check(dry.Gateway.Submits == 0 && dry.Buyer.Ledger.Charged == 0 && dry.Buyer.Observations.Count == 1,
            "auto: dry run records opportunity but sends no purchase and reserves no gil");
        var real = Buyer();
        real.Buyer.Tick(now, context, limits, true, false);
        real.Buyer.Tick(now.AddMilliseconds(100), context, limits, true, false);
        check(real.Gateway.Submits == 0, "auto: no purchase immediately after a query");
        real.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        check(real.Gateway.Submits == 1 && real.Calls.IndexOf("save") < real.Calls.IndexOf("submit") && real.Buyer.Ledger.Charged == 105,
            "auto: durable budget reservation precedes exactly one purchase");
        real.Buyer.Tick(now.AddSeconds(2), context, limits, true, false);
        check(real.Gateway.Submits == 1 && real.Buyer.HasPending, "auto: no second purchase while receipt pending");
        real.Gateway.Count = 1;
        real.Buyer.Tick(now.AddSeconds(3), context with { Gil = context.Gil - 105 }, limits, true, false);
        check(!real.Buyer.HasPending && real.Buyer.Ledger.Entries.Single().Status == "Подтверждено", "auto: exact gil debit plus inventory delta confirms purchase");
        real.Gateway.CurrentLot = lot with { Id = 78 };
        real.Buyer.Tick(now.AddSeconds(9), context with { Gil = context.Gil - 105 }, limits, true, false);
        real.Buyer.Tick(now.AddSeconds(10), context with { Gil = context.Gil - 105 }, limits, true, false);
        check(real.Gateway.Requests == 2 && real.Gateway.Submits == 2 && real.Buyer.Ledger.Charged == 210,
            "auto: another stack of same item requires a new game query and fresh budget reservation");
        var timeout = Buyer();
        timeout.Buyer.Tick(now, context, limits, true, false);
        timeout.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        timeout.Buyer.Tick(now.AddSeconds(20), context, limits, true, false);
        check(timeout.Buyer.Halted && timeout.Buyer.Ledger.Charged == 105 && timeout.Gateway.Submits == 1,
            "auto: ambiguous receipt halts and never refunds worst-case spend");
        var fileError = Buyer(failSave: true);
        fileError.Buyer.Tick(now, context, limits, true, false);
        try { fileError.Buyer.Tick(now.AddSeconds(1), context, limits, true, false); }
        catch (IOException) { }
        check(fileError.Gateway.Submits == 0, "auto: journal write failure prevents purchase");
        var changed = Buyer(); changed.Gateway.CurrentLot = lot with { Price = 6000 };
        changed.Buyer.Tick(now, context, limits, true, false);
        changed.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        check(changed.Gateway.Submits == 0, "auto: fresh loss overrides profitable cached suggestion");
        var foreign = Buyer(); foreign.Buyer.Stop("stop"); foreign.Buyer.Resume();
        foreign.Buyer.Begin([suggestion with { World = 83 }], catalog, context);
        foreign.Buyer.Tick(now, context, limits, true, false);
        check(foreign.Gateway.Requests == 0 && foreign.Gateway.Submits == 0, "auto: Chaos offers from other worlds never trigger local purchases");
        var closing = Buyer(); closing.Buyer.Tick(now, context, limits, true, false);
        closing.Buyer.Tick(now.AddSeconds(1), context with { BoardOpen = false }, limits, true, false);
        check(!closing.Buyer.Busy && closing.Gateway.Submits == 0, "auto: closing board cancels unsent purchase");
        closing.Buyer.Tick(now.AddSeconds(2), context, limits, true, false);
        closing.Buyer.Begin([suggestion], catalog, context);
        check(closing.Buyer.Busy, "auto: reopening board can resume periodic mode after a normal pause");
        var off = Buyer(); off.Buyer.Tick(now, context, limits, false, false);
        check(off.Gateway.Submits == 0 && off.Gateway.Requests == 0, "auto: disabled mode sends neither requests nor purchases");
        var failedRequest = Buyer(); failedRequest.Gateway.RequestOk = false;
        failedRequest.Buyer.Tick(now, context, limits, true, false);
        check(failedRequest.Buyer.Halted && failedRequest.Gateway.Submits == 0, "auto: rejected game query halts before spending");
        var rejected = Buyer(); rejected.Gateway.SubmitOk = false;
        rejected.Buyer.Tick(now, context, limits, true, false); rejected.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        check(rejected.Buyer.Halted && rejected.Buyer.Ledger.Charged == 105, "auto: rejected submit preserves worst-case budget reservation");
        var moved = Buyer(); moved.Buyer.Tick(now, context, limits, true, false);
        moved.Buyer.Tick(now.AddSeconds(1), context with { World = 83 }, limits, true, false);
        check(moved.Gateway.Submits == 0 && moved.Buyer.Halted, "auto: moving worlds cancels pending query");
        var movedPending = Buyer(); movedPending.Buyer.Tick(now, context, limits, true, false);
        movedPending.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        movedPending.Buyer.Tick(now.AddSeconds(2), context with { World = 83 }, limits, true, false);
        check(!movedPending.Buyer.HasPending && movedPending.Buyer.Halted && movedPending.Buyer.Ledger.Charged == 105,
            "auto: world change cannot refund or repeat submitted purchase");
        var onlyGil = Buyer(); onlyGil.Buyer.Tick(now, context, limits, true, false);
        onlyGil.Buyer.Tick(now.AddSeconds(1), context, limits, true, false);
        onlyGil.Buyer.Tick(now.AddSeconds(2), context with { Gil = context.Gil - 105 }, limits, true, false);
        check(onlyGil.Buyer.HasPending, "auto: gil debit alone does not confirm a purchase");
        var onlyItems = Buyer(); onlyItems.Buyer.Tick(now, context, limits, true, false);
        onlyItems.Buyer.Tick(now.AddSeconds(1), context, limits, true, false); onlyItems.Gateway.Count = 1;
        onlyItems.Buyer.Tick(now.AddSeconds(2), context, limits, true, false);
        check(onlyItems.Buyer.HasPending, "auto: inventory increase alone does not confirm a purchase");
        var one = new BuyLimits { MaximumPurchases = 1 };
        check(!BuyPolicy.Allowed(quote with { Lot = lot with { Id = 100 } }, one, attempted, context), "auto: purchase-count ceiling is independent of gil budget");
        var gear = Buyer(); gear.Buyer.Stop("stop"); gear.Buyer.Resume();
        gear.Buyer.Begin([suggestion], new Dictionary<uint,VendorItem> { [item.Id] = item with { Equipment = true } }, context);
        check(!gear.Buyer.Busy && gear.Gateway.Requests == 0, "auto: equipment excluded from unattended purchase cycle");
    }
}
sealed class FakeBuyGateway(Lot lot, List<string> calls) : IBuyGateway
{
    public Lot CurrentLot = lot;
    public int Requests, Submits, Count;
    public bool RequestOk = true, SubmitOk = true;
    public bool Request(uint item) { Requests++; return RequestOk; }
    public Lot[] FreshLots(uint item) => [CurrentLot];
    public bool Submit(Lot selected) { calls.Add("submit"); Submits++; return SubmitOk; }
    public int ItemCount(uint item, bool hq) => Count;
    public void Clear() { }
}
