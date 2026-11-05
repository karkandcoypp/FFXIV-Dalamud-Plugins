namespace VendorProfit;

public sealed class BuyLimits
{
    public int MaximumLot = 50_000;
    public int Budget = 500_000;
    public int MinimumProfit = 100;
    public int MinimumRoi = 10;
    public int IntervalSeconds = 120;
    public int MaximumPurchases = 100;
    public int PurchaseDelaySeconds = 5;
    public void Normalize()
    {
        MaximumLot = Math.Clamp(MaximumLot, 1, 10_000_000);
        Budget = Math.Clamp(Budget, 1, 100_000_000);
        MinimumProfit = Math.Clamp(MinimumProfit, 1, 10_000_000);
        MinimumRoi = Math.Clamp(MinimumRoi, 1, 10000);
        IntervalSeconds = Math.Clamp(IntervalSeconds, 60, 1800);
        MaximumPurchases = Math.Clamp(MaximumPurchases, 1, 1000);
        PurchaseDelaySeconds = Math.Clamp(PurchaseDelaySeconds, 1, 15);
    }
}
public sealed record BuyEntry(DateTimeOffset At, ulong Listing, uint Item, uint Quantity, long Cost, long Profit, string Status);
public sealed class BuyLedger
{
    public ulong Character { get; set; }
    public long Charged { get; set; }
    public List<BuyEntry> Entries { get; set; } = [];
    public List<string> CheckedExternal { get; set; } = [];
}
public sealed record BuyContext(ulong Character, uint World, bool BoardOpen, bool Blocked, long Gil, uint FreeSlots);
public interface IBuyGateway
{
    bool Request(uint item);
    Lot[] FreshLots(uint item);
    bool Submit(Lot lot);
    int ItemCount(uint item, bool hq);
    void Clear();
}
public static class BuyPolicy
{
    public static bool Allowed(Quote quote, BuyLimits limits, BuyLedger ledger, BuyContext context)
    {
        return context.BoardOpen && !context.Blocked && context.Character != 0 && ledger.Character == context.Character &&
            context.World != 0 && context.FreeSlots > 0 && quote.Lot.Id != 0 && !quote.Lot.Mannequin && !quote.Lot.Damaged &&
            quote.Lot.Quantity > 0 && quote.Cost > 0 && quote.Cost <= limits.MaximumLot &&
            ledger.Charged >= 0 && ledger.Charged <= limits.Budget && quote.Cost <= limits.Budget - ledger.Charged &&
            ledger.Entries.Count < limits.MaximumPurchases && !ledger.Entries.Any(e => e.Listing == quote.Lot.Id) &&
            quote.Cost <= context.Gil && quote.Profit >= limits.MinimumProfit &&
            (decimal)quote.Profit * 100 >= (decimal)quote.Cost * limits.MinimumRoi;
    }
}

// Framework-thread state machine. The ledger is committed before any purchase can be submitted.
public sealed class AutoBuyer(IBuyGateway gateway, Action<BuyLedger> save)
{
    private readonly Queue<VendorItem> queue = new();
    private VendorItem? requested;
    private Quote? pending;
    private uint cycleWorld;
    private ulong cycleCharacter;
    private DateTimeOffset deadline, nextStep;
    private long gilBefore;
    private int countBefore;
    private Dictionary<ulong,uint>? externalTargets;
    private bool pausedForBoard;
    public bool Busy { get; private set; }
    public bool Halted { get; private set; }
    public bool HasPending => pending != null;
    public string Status { get; private set; } = "Автопокупка выключена.";
    public BuyLedger Ledger { get; private set; } = new();
    public List<string> Observations { get; } = [];
    public uint[] QueuedItems => queue.Select(i => i.Id).Distinct().ToArray();
    public void Attach(BuyLedger ledger)
    {
        if (HasPending) throw new InvalidOperationException("Покупка ещё ожидает подтверждения.");
        Stop("Готово к настройке."); Ledger = ledger;
    }
    public void Resume() { if (!HasPending) { Halted = false; pausedForBoard = false; Status = "Ожидание периодической проверки."; } }
    public void Wait(string reason) { if (!Busy && !HasPending && !Halted) Status = reason; }
    public void Stop(string reason)
    {
        Busy = false; Halted = true; pausedForBoard = false; queue.Clear(); requested = null; gateway.Clear(); Status = reason;
        // Never clear an unresolved purchase or refund its reserved budget automatically.
    }
    public void AbandonPending(string reason) { pending = null; Stop(reason); }
    public void Begin(IEnumerable<Suggestion> candidates, IReadOnlyDictionary<uint, VendorItem> catalog, BuyContext context, BuyLimits? limits = null, bool exactExternal = false)
    {
        if (Busy || Halted || HasPending || !context.BoardOpen || context.Character != Ledger.Character) return;
        limits ??= new();
        queue.Clear();
        var filtered = candidates.Where(s => s.World == context.World && (!exactExternal || s.Quote.Lot.Id != 0) && s.Quote.Cost > 0 && s.Quote.Cost <= limits.MaximumLot &&
                s.Quote.Cost <= limits.Budget - Ledger.Charged && s.Quote.Profit >= limits.MinimumProfit &&
                (decimal)s.Quote.Profit * 100 >= (decimal)s.Quote.Cost * limits.MinimumRoi)
            .OrderByDescending(s => s.Quote.Profit).ToArray();
        foreach (uint id in filtered.Select(s => s.Item.Id).Distinct().Take(20))
            if (catalog.TryGetValue(id, out var item) && !item.Equipment) queue.Enqueue(item);
        externalTargets = exactExternal ? filtered.Where(s => QueuedItems.Contains(s.Item.Id)).GroupBy(s => s.Quote.Lot.Id)
            .ToDictionary(g => g.Key, g => g.First().Item.Id) : null;
        cycleWorld = context.World; cycleCharacter = context.Character; Busy = queue.Count > 0;
        Status = Busy ? $"Свежая проверка кандидатов: {queue.Count}." : "Подходящих кандидатов нет; жду следующую проверку.";
        nextStep = DateTimeOffset.MinValue;
    }
    public void Tick(DateTimeOffset now, BuyContext context, BuyLimits limits, bool enabled, bool dryRun)
    {
        if (enabled && context.BoardOpen && pausedForBoard) Resume();
        if (pending != null)
        {
            if (context.Character != cycleCharacter || context.World != cycleWorld)
            { AbandonPending("Персонаж/сервер изменился. Покупка не подтверждена; сумма оставлена в лимите."); return; }
            int count = gateway.ItemCount(pending.Lot.Item, pending.Lot.Hq);
            if (gilBefore - context.Gil == pending.Cost && count - countBefore == pending.Lot.Quantity)
            {
                int last = Ledger.Entries.Count - 1;
                Ledger.Entries[last] = Ledger.Entries[last] with { Status = "Подтверждено" };
                try { save(Ledger); }
                catch { pending = null; Stop("Подтверждение не сохранено в журнал; покупки остановлены."); throw; }
                Status = $"Куплено {pending.Lot.Quantity} шт. за {pending.Cost:N0}; расчётная прибыль {pending.Profit:N0}.";
                externalTargets?.Remove(pending.Lot.Id);
                if (requested != null && Busy && !Halted && (externalTargets == null || externalTargets.Values.Contains(requested.Id))) queue.Enqueue(requested);
                pending = null; requested = null; gateway.Clear(); nextStep = now.AddSeconds(5);
                return;
            }
            if (now >= deadline)
            {
                Status = "Нет однозначного подтверждения покупки. Остановлено; сумма оставлена в лимите. Проверьте инвентарь и журнал.";
                pending = null; Stop(Status);
            }
            return;
        }
        if (!context.BoardOpen)
        {
            if (Busy) { Busy = false; queue.Clear(); requested = null; gateway.Clear(); pausedForBoard = true; Status = "Пауза до открытия аукциона."; }
            return;
        }
        if (!enabled || context.World != cycleWorld || context.Character != cycleCharacter)
        { if (Busy) Stop("Автопокупка приостановлена: сменён сервер или отключён режим."); return; }
        if (Ledger.Charged >= limits.Budget || Ledger.Entries.Count >= limits.MaximumPurchases)
        { Stop("Достигнут лимит расходов или количества покупок."); return; }
        if (context.FreeSlots == 0) { Stop("Нет свободных мест в сумках."); return; }
        if (!Busy || Halted) return;
        if (context.Blocked) { Status = "Закройте контекстное меню или диалог подтверждения в игре."; return; }
        if (now < nextStep)
        {
            if (requested != null) Status = $"Пауза перед покупкой: ещё {Math.Ceiling((nextStep-now).TotalSeconds):F0} сек. Затем повторная проверка цены и лимитов.";
            return;
        }
        if (requested == null)
        {
            if (queue.Count == 0) { Busy = false; Status = "Цикл завершён; жду следующую проверку."; return; }
            requested = queue.Dequeue();
            if (!gateway.Request(requested.Id)) { Stop("Игра не приняла запрос свежих лотов. Автопокупка остановлена."); return; }
            Status = "Запрашиваю свежие лоты: " + requested.Name;
            int delay = Math.Clamp(limits.PurchaseDelaySeconds, 1, 15);
            nextStep = now.AddSeconds(delay); deadline = nextStep.AddSeconds(15);
            return;
        }
        var quote = gateway.FreshLots(requested.Id).Select(l => Policy.Evaluate(requested, l)).OfType<Quote>()
            .Where(q => externalTargets == null || externalTargets.ContainsKey(q.Lot.Id))
            .Where(q => BuyPolicy.Allowed(q, limits, Ledger, context)).OrderByDescending(q => q.Profit).FirstOrDefault();
        if (quote == null)
        {
            if (now >= deadline) { Status = "Подтверждённых подходящих лотов нет: " + requested.Name; requested = null; gateway.Clear(); nextStep = now.AddSeconds(5); }
            return;
        }
        if (dryRun)
        {
            Status = $"Пробный режим: {requested.Name}, {quote.Lot.Quantity} шт., покупка {quote.Cost:N0}, прибыль {quote.Profit:N0}. Деньги не потрачены.";
            Observations.Add(Status); if (Observations.Count > 20) Observations.RemoveAt(0);
            requested = null; gateway.Clear(); nextStep = now.AddSeconds(5); return;
        }
        countBefore = gateway.ItemCount(quote.Lot.Item, quote.Lot.Hq);
        if (countBefore < 0) { Stop("Не удалось прочитать инвентарь."); return; }
        gilBefore = context.Gil;
        Ledger.Entries.Add(new(now, quote.Lot.Id, quote.Lot.Item, quote.Lot.Quantity, quote.Cost, quote.Profit, "Учтено в лимите, ожидается результат"));
        Ledger.Charged = checked(Ledger.Charged + quote.Cost);
        save(Ledger); // Failure here prevents the purchase. No silent budget reset after reload/crash.
        pending = quote; deadline = now.AddSeconds(15);
        if (!gateway.Submit(quote.Lot))
        { pending = null; Stop("Отправка покупки отклонена. Остановлено; сумма остаётся в лимите до ручной проверки."); }
        else Status = $"Ожидаю подтверждения покупки за {quote.Cost:N0} гил…";
    }
}
