using System.Collections.Concurrent;
using System.Text.Json;
using Dalamud.Game.Network.Structures;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private sealed record StatisticsSession(ulong Character, uint World);
    private sealed record PurchaseSignal(StatisticsSession Session, DateTimeOffset At, PurchaseRequest? Request, uint Item = 0, uint Quantity = 0);
    private volatile StatisticsSession? statisticsSession;
    private readonly ConcurrentQueue<PurchaseSignal> purchaseSignals = new();
    private readonly PurchaseReceiptTracker receiptTracker = new();
    private TradeBook statistics = new();
    private bool statisticsValid;
    private bool statisticsDirty;
    private DateTimeOffset nextStatisticsSave;
    private string statisticsStatus = "Учёт начнётся после входа персонажем.";
    private string StatisticsPath => ExternalPath($"statistics-{statistics.Character}.json");

    private void OnPurchaseRequested(IMarketBoardPurchaseHandler purchase)
    {
        var session = statisticsSession;
        if (session == null) return;
        uint item = purchase.CatalogId >= 1_000_000 && purchase.CatalogId < 2_000_000 ? purchase.CatalogId - 1_000_000 : purchase.CatalogId;
        var at = DateTimeOffset.UtcNow;
        purchaseSignals.Enqueue(new(session, at, new(at, session.Character, session.World, purchase.ListingId,
            item, purchase.ItemQuantity, purchase.PricePerUnit, purchase.TotalTax, purchase.IsHq)));
    }
    private void OnItemPurchased(IMarketBoardPurchase purchase)
    {
        var session = statisticsSession;
        if (session != null) purchaseSignals.Enqueue(new(session, DateTimeOffset.UtcNow, null, purchase.CatalogId, purchase.ItemQuantity));
    }
    private void SaveStatistics()
    {
        statisticsDirty = true;
        if (!statisticsValid || !TradeStatistics.Valid(statistics, statistics.Character)) throw new InvalidDataException("История статистики некорректна.");
        Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
        File.WriteAllText(StatisticsPath + ".tmp", JsonSerializer.Serialize(statistics));
        File.Move(StatisticsPath + ".tmp", StatisticsPath, true);
        statisticsDirty = false;
    }
    private void UpdateStatisticsSafely()
    {
        try { UpdateStatistics(); }
        catch (Exception e)
        {
            nextStatisticsSave = DateTimeOffset.UtcNow.AddSeconds(10);
            statisticsStatus = "Статистика пока не сохранена: " + e.Message;
            Log.Warning(e, "VendorProfit statistics update failed");
        }
    }
    private void UpdateStatistics()
    {
        ulong character = Client.IsLoggedIn && Player.IsLoaded ? Player.ContentId : 0;
        uint currentWorld = character == 0 ? 0 : Objects.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (statistics.Character != character)
        {
            statisticsSession = null; receiptTracker.Clear(); statisticsValid = false; statisticsDirty = false;
            statistics = new() { Character = character };
            if (character != 0)
            {
                try
                {
                    var file = new FileInfo(StatisticsPath);
                    if (file.Exists && file.Length > 30_000_000) throw new InvalidDataException("Слишком большой файл истории.");
                    if (file.Exists) statistics = JsonSerializer.Deserialize<TradeBook>(File.ReadAllText(file.FullName)) ?? throw new InvalidDataException("Пустой файл истории.");
                    statisticsValid = TradeStatistics.Valid(statistics, character);
                    if (!statisticsValid) throw new InvalidDataException("История не прошла проверку; исходный файл сохранён.");
                    statisticsStatus = "Ручные и автоматические покупки учитываются, пока плагин включён.";
                }
                catch (Exception e) { statistics = new() { Character = character }; statisticsStatus = "Учёт приостановлен: " + e.Message; Log.Warning(e, "VendorProfit statistics load failed"); }
            }
        }
        var previous = statisticsSession;
        statisticsSession = character != 0 && currentWorld != 0 && statisticsValid ? new(character, currentWorld) : null;
        if (previous?.World != currentWorld) receiptTracker.Clear();
        while (purchaseSignals.TryDequeue(out var signal))
        {
            if (!statisticsValid || character == 0 || signal.Session.Character != character || signal.Session.World != currentWorld) continue;
            if (signal.Request != null)
            {
                if (!receiptTracker.Request(signal.Request)) statisticsStatus = "Перекрывающиеся запросы покупки: неоднозначные ответы не учитываются.";
                continue;
            }
            var request = receiptTracker.Confirm(character, currentWorld, signal.Item, signal.Quantity, signal.At);
            if (request == null) continue;
            LoadCatalog();
            if (catalog!.TryGetValue(request.Item, out var item) && TradeStatistics.FromReceipt(request, item) is { } trade && TradeStatistics.Add(statistics, trade))
            { SaveStatistics(); statisticsStatus = $"Учтена покупка: {trade.Name}, {trade.Quantity} шт., {trade.Cost:N0} гил."; }
        }
        if (statisticsDirty && statisticsValid && DateTimeOffset.UtcNow >= nextStatisticsSave)
        { SaveStatistics(); statisticsStatus = "История покупок сохранена."; }
    }
    private void ImportPurchaseStatistics(BuyLedger ledger)
    {
        if (!statisticsValid || statistics.Character != ledger.Character) return;
        try
        {
            bool changed = false;
            var known = statistics.Purchases.Select(p => p.Listing).ToHashSet();
            foreach (var entry in ledger.Entries.Where(e => e.Status == "Подтверждено" && e.Profit > 0))
            {
                if (known.Contains(entry.Listing)) continue;
                LoadCatalog();
                string name = catalog!.TryGetValue(entry.Item, out var item) ? item.Name : "Предмет #" + entry.Item;
                if (TradeStatistics.Import(entry, name) is { } trade && TradeStatistics.Add(statistics, trade))
                { changed = true; known.Add(trade.Listing); }
            }
            if (changed) SaveStatistics();
        }
        catch (Exception e) { statisticsStatus = "Ошибка сохранения статистики: " + e.Message; Log.Warning(e, "VendorProfit statistics import failed"); }
    }
}
