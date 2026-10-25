using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Network.Structures;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace VendorProfit;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool AutoOpen = true;
    public bool ScanOnOpen = true;
    public bool Highlight = true;
    public int MinimumProfit = 1000;
    public int MaximumAgeMinutes = 180;
}

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static IGameGui Gui { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IMarketBoard Board { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    private readonly Configuration config;
    private readonly MarketClient market = new();
    private readonly ConcurrentDictionary<ulong, (Lot Lot, DateTimeOffset Seen, uint World)> received = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? scanCancellation;
    private Task<ScanResult>? scan;
    private ScanResult? result;
    private Dictionary<uint, VendorItem>? catalog;
    private string progress = "Откройте Market Board или запустите проверку.";
    private string? error;
    private uint world;
    private bool open, wasBoardOpen, manual;
    private DateTimeOffset nextScan;
    private readonly List<Quote> live = new();
    private int highlighted;
    private string liveStatus = "Выберите предмет на Market Board.";

    public Plugin()
    {
        config = Pi.GetPluginConfig() as Configuration ?? new();
        config.MinimumProfit = Math.Clamp(config.MinimumProfit, 1, 100_000_000);
        config.MaximumAgeMinutes = Math.Clamp(config.MaximumAgeMinutes, 5, 1440);
        Board.OfferingsReceived += OnOfferings;
        Board.ItemPurchased += OnPurchased;
        Commands.AddHandler("/vendorprofit", new CommandInfo((_, _) => Open()) { HelpMessage = "NPC resale opportunities / перепродажа NPC" });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
    }
    private void Open() { open = true; manual = true; }
    private void OnPurchased(IMarketBoardPurchase purchase) => received.Clear();
    private void OnOfferings(IMarketBoardCurrentOfferings offerings)
    {
        uint capturedWorld = world;
        var now = DateTimeOffset.UtcNow;
        foreach (var l in offerings.ItemListings)
            received[l.ListingId] = (new(l.ListingId, l.ItemId, l.RetainerName, l.PricePerUnit, l.ItemQuantity,
                l.TotalTax, l.IsHq, l.OnMannequin), now, capturedWorld);
        if (received.Count > 1000)
            foreach (var entry in received.Where(e => e.Value.Seen < now.AddMinutes(-3)).ToArray()) received.TryRemove(entry.Key, out _);
    }
    private void LoadCatalog()
    {
        if (catalog != null) return;
        catalog = new();
        foreach (var item in Data.GetExcelSheet<Item>())
        {
            if (item.PriceLow == 0 || item.IsUntradable || item.IsIndisposable || item.ItemSearchCategory.RowId == 0 || item.StackSize == 0) continue;
            catalog[item.RowId] = new(item.RowId, item.Name.ToString(), item.PriceLow, item.CanBeHq, item.StackSize, item.EquipSlotCategory.RowId != 0);
        }
    }
    private void StartScan()
    {
        if (world == 0 || scan is { IsCompleted: false }) return;
        LoadCatalog();
        scanCancellation?.Dispose();
        scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        scanCancellation.CancelAfter(TimeSpan.FromMinutes(10));
        var token = scanCancellation.Token;
        var capturedWorld = world;
        var items = catalog!.Values.Where(i => !i.Equipment).ToArray();
        int age = config.MaximumAgeMinutes;
        error = null;
        nextScan = DateTimeOffset.UtcNow.AddMinutes(20);
        scan = Task.Run(() => market.Scan(capturedWorld, items, age, p => Volatile.Write(ref progress, p), token), token);
    }
    private static bool Visible(AtkUnitBase* addon) => addon != null && addon->IsVisible && addon->IsReady;
    private void Draw()
    {
        try { DrawCore(); }
        catch (Exception e)
        {
            live.Clear(); highlighted = 0;
            if (error != e.Message) Log.Warning(e, "VendorProfit: UI read failed; highlights suppressed");
            error = e.Message;
        }
    }
    private void DrawCore()
    {
        uint current = Client.IsLoggedIn ? Objects.LocalPlayer?.CurrentWorld.RowId ?? 0 : 0;
        if (current != world)
        {
            world = current; received.Clear(); live.Clear(); result = null; scanCancellation?.Cancel();
            nextScan = DateTimeOffset.MinValue;
        }
        var search = (AtkUnitBase*)Gui.GetAddonByName("ItemSearch").Address;
        var results = (AddonItemSearchResult*)Gui.GetAddonByName("ItemSearchResult").Address;
        bool boardOpen = Visible(search) || Visible((AtkUnitBase*)results);
        if (boardOpen && !wasBoardOpen && config.AutoOpen) { open = true; manual = false; }
        if (!boardOpen && wasBoardOpen && !manual) open = false;
        wasBoardOpen = boardOpen;
        if (scan is { IsCompleted: true })
        {
            if (scan.IsCompletedSuccessfully && scan.Result.World == world) { result = scan.Result; progress = "Проверка завершена."; }
            else if (scan.IsFaulted) { error = scan.Exception!.GetBaseException().Message; Log.Warning(scan.Exception, "VendorProfit market scan failed"); }
            else if (scan.IsCanceled) progress = "Проверка остановлена (смена мира, отмена или лимит 10 минут).";
            scan = null;
        }
        if (boardOpen && config.ScanOnOpen && DateTimeOffset.UtcNow >= nextScan && world != 0) StartScan();
        if (Visible((AtkUnitBase*)results)) ReadLive(results);
        else { live.Clear(); highlighted = 0; liveStatus = "Выберите предмет на Market Board."; }
        if (!open) return;
        ImGui.SetNextWindowSize(new Vector2(840, 530), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("VendorProfit — перепродажа NPC###VendorProfit", ref open))
        {
            string worldName = Objects.LocalPlayer?.CurrentWorld.Value.Name.ToString() ?? "—";
            ImGui.TextUnformatted($"Текущий мир: {worldName}. Прибыль за весь стак, с налогом покупателя.");
            bool changed = ImGui.Checkbox("Открывать с Market Board", ref config.AutoOpen);
            ImGui.SameLine(); changed |= ImGui.Checkbox("Подсветка лотов", ref config.Highlight);
            changed |= ImGui.Checkbox("Проверять Universalis при открытии (кэш 20 мин)", ref config.ScanOnOpen);
            changed |= ImGui.InputInt("Минимум прибыли за лот", ref config.MinimumProfit);
            changed |= ImGui.InputInt("Возраст данных Universalis, минут", ref config.MaximumAgeMinutes);
            if (changed)
            {
                config.MinimumProfit = Math.Clamp(config.MinimumProfit, 1, 100_000_000);
                config.MaximumAgeMinutes = Math.Clamp(config.MaximumAgeMinutes, 5, 1440);
                Pi.SavePluginConfig(config);
            }
            ImGui.Separator();
            ImGui.TextUnformatted($"Открытый предмет: {liveStatus} Подсвечено строк: {highlighted}");
            DrawLive();
            ImGui.Separator();
            if (scan == null)
            {
                if (ImGui.Button("Обновить кандидатов") && world != 0) StartScan();
            }
            else if (ImGui.Button("Остановить проверку")) { scanCancellation?.Cancel(); nextScan = DateTimeOffset.UtcNow.AddMinutes(20); }
            ImGui.SameLine(); ImGui.TextUnformatted(Volatile.Read(ref progress));
            if (error != null) ImGui.TextWrapped("Ошибка: " + error);
            if (result != null)
            {
                ImGui.TextWrapped($"Последняя проверка: {result.Finished.LocalDateTime:g}; предметов: {result.Checked}; неизвестных: {result.Unknown}; ошибок: {result.Failed}. {result.Status}");
                ImGui.TextWrapped("Экипировка проверяется только в открытой выдаче: Universalis не сообщает состояние. Нулевые и отсутствующие цены не считаются выгодой.");
                DrawSuggestions(result);
            }
        }
        ImGui.End();
    }
    private void DrawLive()
    {
        var qualified = live.Where(q => q.Profit >= config.MinimumProfit).OrderByDescending(q => q.Profit).ToArray();
        ImGui.TextUnformatted($"Под порог: {qualified.Length} лотов; вложить {qualified.Sum(q => q.Cost):N0}, получить {qualified.Sum(q => q.Revenue):N0}, прибыль {qualified.Sum(q => q.Profit):N0} гил.");
        if (!ImGui.BeginTable("live", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 130))) return;
        foreach (var name in new[] { "Ретейнер / качество", "Кол-во", "Покупка с налогом", "Выручка NPC", "Прибыль" }) ImGui.TableSetupColumn(name);
        ImGui.TableHeadersRow();
        foreach (var q in qualified)
        {
            ImGui.TableNextRow(); Cell(0, q.Lot.Retainer + (q.Lot.Hq ? " HQ" : " NQ")); Cell(1, q.Lot.Quantity.ToString());
            Cell(2, q.Cost.ToString("N0")); Cell(3, q.Revenue.ToString("N0")); Cell(4, "+" + q.Profit.ToString("N0"));
        }
        ImGui.EndTable();
    }
    private void DrawSuggestions(ScanResult snapshot)
    {
        var suggestions = snapshot.Suggestions.Where(s => s.Quote.Profit >= config.MinimumProfit && Policy.Fresh(s.Updated, DateTimeOffset.UtcNow, config.MaximumAgeMinutes)).ToArray();
        if (suggestions.Length == 0) ImGui.TextWrapped("Подходящих лотов в проверенной части кэша нет. Открытые предметы всё равно проверяются по живой выдаче.");
        if (!ImGui.BeginTable("suggestions", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 180))) return;
        foreach (var name in new[] { "Предмет (нажать: скопировать)", "Кол-во", "Покупка", "Прибыль", "Возраст / налог" }) ImGui.TableSetupColumn(name);
        ImGui.TableHeadersRow();
        int index = 0;
        foreach (var s in suggestions.Take(200))
        {
            ImGui.PushID(index++); ImGui.TableNextRow(); ImGui.TableSetColumnIndex(0);
            if (ImGui.Selectable(s.Item.Name + (s.Quote.Lot.Hq ? " HQ" : ""))) ImGui.SetClipboardText(s.Item.Name);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{s.Quote.Lot.Retainer}; NPC: {s.Quote.Revenue:N0}. Вставьте название в поиск Market Board.");
            Cell(1, s.Quote.Lot.Quantity.ToString()); Cell(2, s.Quote.Cost.ToString("N0")); Cell(3, "+" + s.Quote.Profit.ToString("N0"));
            Cell(4, $"{(DateTimeOffset.UtcNow - s.Updated).TotalMinutes:F0} мин / {(s.EstimatedTax ? "оценка 5%" : "API")}");
            ImGui.PopID();
        }
        ImGui.EndTable();
    }
    private static void Cell(int column, string text) { ImGui.TableSetColumnIndex(column); ImGui.TextUnformatted(text); }

    private void ReadLive(AddonItemSearchResult* addon)
    {
        live.Clear(); highlighted = 0;
        LoadCatalog();
        var agent = AgentItemSearch.Instance();
        var proxy = agent == null ? null : agent->InfoProxyItemSearch;
        var list = addon->Results;
        liveStatus = "Ожидание текущих лотов / подтверждения строк.";
        if (proxy == null || proxy->WaitingForListings || list == null || list->IsUpdatePending || list->IsScrollRefreshPending ||
            proxy->ListingCount > 100 || agent->ResultItemId != proxy->SearchItemId ||
            !catalog!.TryGetValue(proxy->SearchItemId, out var item)) return;
        var lots = new List<Lot>();
        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < proxy->ListingCount; i++)
        {
            var l = proxy->Listings[i];
            if (l.ItemId != item.Id || !received.TryGetValue(l.ListingId, out var entry) || entry.World != world || entry.Seen < now.AddMinutes(-2)) continue;
            var network = entry.Lot;
            if (network.Item != l.ItemId || network.Price != l.UnitPrice || network.Quantity != l.Quantity || network.Hq != l.IsHqItem || network.Tax != l.TotalTax) continue;
            var lot = network with { Mannequin = l.IsMannequin || l.IsSellingAsSet, Damaged = item.Equipment && l.Durability != 30000 };
            lots.Add(lot);
            if (Policy.Evaluate(item, lot) is { } q) live.Add(q);
        }
        liveStatus = $"{item.Name}; подтверждено {lots.Count}/{proxy->ListingCount} лотов (не старше 2 мин).";
        if (!config.Highlight || lots.Count == 0 || lots.Count != proxy->ListingCount || Visible((AtkUnitBase*)Gui.GetAddonByName("SelectYesno").Address)) return;
        // Resolve each currently rendered row by its displayed retainer, quantity and tax-inclusive total.
        // No writes to game nodes: outlines disappear naturally on close, scrolling or unloading.
        for (int i = 0; i < Math.Min(list->ListLength, 100); i++)
        {
            if (!list->IsItemVisible(i, false)) continue;
            var renderer = list->GetItemRenderer(i);
            if (renderer == null || renderer->ListItemIndex != i || renderer->OwnerNode == null || !renderer->OwnerNode->IsVisible()) continue;
            var texts = new HashSet<string>();
            CollectText(&renderer->UldManager, texts, 0);
            var matched = Policy.Match(lots, texts);
            if (matched == null || Policy.Evaluate(item, matched) is not { } quote || quote.Profit < config.MinimumProfit) continue;
            var node = renderer->OwnerNode;
            float x, y; renderer->GetScreenPosition(&x, &y);
            float scale = addon->GetScale();
            var start = ImGui.GetMainViewport().Pos + new Vector2(x, y);
            var end = start + new Vector2(node->Width * scale, node->Height * scale);
            if (end.X <= start.X || end.Y <= start.Y) continue;
            var draw = ImGui.GetForegroundDrawList();
            draw.AddRectFilled(start, end, 0x2520FF60);
            draw.AddRect(start, end, 0xFF60FF80, 2, ImDrawFlags.None, 2);
            highlighted++;
        }
    }
    private static void CollectText(AtkUldManager* uld, HashSet<string> texts, int depth)
    {
        if (depth > 2 || uld->NodeList == null || uld->NodeListCount > 256) return;
        for (int n = 0; n < uld->NodeListCount; n++)
        {
            var node = uld->NodeList[n];
            if (node == null || !node->IsVisible()) continue;
            if (node->Type == NodeType.Text)
            {
                string text = ((AtkTextNode*)node)->NodeText.ToString().Trim();
                texts.Add(text);
                if (!text.Any(char.IsLetter)) texts.Add(Policy.NormalizeNumber(text));
            }
            else if ((int)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null) CollectText(&component->UldManager, texts, depth + 1);
            }
        }
    }
    public void Dispose()
    {
        Board.OfferingsReceived -= OnOfferings;
        Board.ItemPurchased -= OnPurchased;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/vendorprofit");
        lifetime.Cancel(); scanCancellation?.Cancel();
        // The worker uses only immutable snapshots; cancellation never waits on the game thread.
        var pending = scan;
        if (pending != null) _ = pending.ContinueWith(_ => { market.Dispose(); scanCancellation?.Dispose(); lifetime.Dispose(); }, TaskScheduler.Default);
        else { market.Dispose(); scanCancellation?.Dispose(); lifetime.Dispose(); }
    }
}
