using System.Numerics;
using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Game.Command;
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
    public int Version { get; set; } = 2;
    public bool AutoOpen = true;
    public bool ScanOnOpen = true;
    public bool Highlight = true;
    public int MinimumProfit = 1000;
    public int MaximumAgeMinutes = 180;
    public uint[] LearnedItems = [];
}

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static IGameGui Gui { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    private readonly Configuration config;
    private readonly MarketClient market = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? scanCancellation;
    private Task<ScanResult>? scan;
    private ScanResult? result;
    private Dictionary<uint, VendorItem>? catalog;
    private string progress = "Откройте Market Board или запустите проверку.";
    private string? error;
    private uint world;
    private bool open, wasBoardOpen, manual;
    private bool sized;
    private DateTimeOffset nextScan;
    private readonly List<Quote> live = new();
    private int highlighted;
    private string liveStatus = "Выберите предмет на Market Board.";

    public Plugin()
    {
        config = Pi.GetPluginConfig() as Configuration ?? new();
        config.MinimumProfit = Math.Clamp(config.MinimumProfit, 1, 100_000_000);
        config.MaximumAgeMinutes = Math.Clamp(config.MaximumAgeMinutes, 5, 1440);
        Commands.AddHandler("/vendorprofit", new CommandInfo((_, _) => Open()) { HelpMessage = "NPC resale opportunities / перепродажа NPC" });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
    }
    private void Open() { open = true; manual = true; }
    private string CachePath => Path.Combine(Pi.ConfigDirectory.FullName, $"candidates-{world}.json");
    private void RestoreCache()
    {
        try
        {
            var file = new FileInfo(CachePath);
            if (!file.Exists || file.Length > 2_000_000) return;
            var cached = JsonSerializer.Deserialize<ScanResult>(File.ReadAllText(file.FullName));
            if (cached == null || cached.World != world || cached.Suggestions == null || !Policy.Fresh(cached.Finished, DateTimeOffset.UtcNow, 20)) return;
            result = cached; nextScan = cached.Finished.AddMinutes(20); progress = "Показан сохранённый результат; повторного сканирования нет.";
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Log.Warning(e, "VendorProfit cache ignored"); }
    }
    private void SaveCache(ScanResult snapshot)
    {
        // Keep the previous successful result if every requested item failed.
        if (snapshot.Failed >= snapshot.Checked || snapshot.Checked == 0) return;
        try
        {
            Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
            string path = CachePath;
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(snapshot));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warning(e, "VendorProfit cache write failed"); }
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
    private void StartScan(bool full = false)
    {
        if (world == 0 || scan is { IsCompleted: false }) return;
        LoadCatalog();
        scanCancellation?.Dispose();
        scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        scanCancellation.CancelAfter(TimeSpan.FromMinutes(full ? 10 : 2));
        var token = scanCancellation.Token;
        var capturedWorld = world;
        var items = full ? catalog!.Values.Where(i => !i.Equipment).ToArray() : Policy.QuickItems(catalog!, config.LearnedItems ?? []);
        int age = config.MaximumAgeMinutes;
        error = null;
        nextScan = DateTimeOffset.UtcNow.AddMinutes(20);
        progress = full ? "Полный поиск по каталогу (может занять несколько минут)…" : $"Быстрая проверка: {items.Length} предметов, до 5 запросов…";
        scan = Task.Run(() => market.Scan(capturedWorld, items, age, p => Volatile.Write(ref progress, p), token, !full), token);
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
            world = current; live.Clear(); result = null; scanCancellation?.Cancel();
            nextScan = DateTimeOffset.MinValue;
            if (world != 0) RestoreCache();
        }
        var search = (AtkUnitBase*)Gui.GetAddonByName("ItemSearch").Address;
        var results = (AddonItemSearchResult*)Gui.GetAddonByName("ItemSearchResult").Address;
        bool boardOpen = Visible(search) || Visible((AtkUnitBase*)results);
        if (boardOpen && !wasBoardOpen && config.AutoOpen) { open = true; manual = false; }
        if (!boardOpen && wasBoardOpen && !manual) open = false;
        wasBoardOpen = boardOpen;
        if (scan is { IsCompleted: true })
        {
            if (scan.IsCompletedSuccessfully && scan.Result.World == world)
            {
                result = scan.Result; progress = "Проверка завершена.";
                config.LearnedItems = result.Suggestions.Select(s => s.Item.Id).Concat(config.LearnedItems ?? []).Distinct().Take(100).ToArray();
                Pi.SavePluginConfig(config);
                SaveCache(result);
            }
            else if (scan.IsFaulted) { error = scan.Exception!.GetBaseException().Message; Log.Warning(scan.Exception, "VendorProfit market scan failed"); }
            else if (scan.IsCanceled) progress = "Проверка остановлена (смена мира, отмена или лимит времени).";
            scan = null;
        }
        if (boardOpen && config.ScanOnOpen && DateTimeOffset.UtcNow >= nextScan && world != 0) StartScan();
        if (Visible((AtkUnitBase*)results)) ReadLive(results);
        else { live.Clear(); highlighted = 0; liveStatus = "Выберите предмет на Market Board."; }
        if (!open) return;
        if (!sized) { ImGui.SetNextWindowSize(new Vector2(790, 440), ImGuiCond.Always); sized = true; }
        if (ImGui.Begin("VendorProfit — перепродажа NPC###VendorProfit", ref open))
        {
            string worldName = Objects.LocalPlayer?.CurrentWorld.Value.Name.ToString() ?? "—";
            ImGui.TextUnformatted($"Текущий мир: {worldName}. Прибыль за весь стак, с налогом покупателя.");
            if (ImGui.CollapsingHeader("Настройки"))
            {
            bool changed = ImGui.Checkbox("Открывать с Market Board", ref config.AutoOpen);
            ImGui.SameLine(); changed |= ImGui.Checkbox("Подсветка лотов", ref config.Highlight);
            changed |= ImGui.Checkbox("Быстрая проверка при открытии (до 100 предметов, кэш 20 мин)", ref config.ScanOnOpen);
            changed |= ImGui.InputInt("Минимум прибыли за лот", ref config.MinimumProfit);
            changed |= ImGui.InputInt("Возраст данных Universalis, минут", ref config.MaximumAgeMinutes);
            if (changed)
            {
                config.MinimumProfit = Math.Clamp(config.MinimumProfit, 1, 100_000_000);
                config.MaximumAgeMinutes = Math.Clamp(config.MaximumAgeMinutes, 5, 1440);
                Pi.SavePluginConfig(config);
            }
            }
            ImGui.Separator();
            ImGui.TextUnformatted($"Открытый предмет: {liveStatus} Подсвечено строк: {highlighted}");
            DrawLive();
            if (ImGui.CollapsingHeader("Диагностика подсветки")) ImGui.TextWrapped(diagnostics);
            ImGui.Separator();
            if (scan == null)
            {
                if (ImGui.Button("Быстро обновить") && world != 0) StartScan();
                ImGui.SameLine();
                if (ImGui.Button("Полный каталог (долго)") && world != 0) StartScan(true);
            }
            else if (ImGui.Button("Остановить проверку")) { scanCancellation?.Cancel(); nextScan = DateTimeOffset.UtcNow.AddMinutes(20); }
            ImGui.SameLine(); ImGui.TextUnformatted(Volatile.Read(ref progress));
            if (error != null) ImGui.TextWrapped("Ошибка: " + error);
            if (result != null)
            {
                ImGui.TextWrapped($"Последняя проверка: {result.Finished.LocalDateTime:g}; предметов: {result.Checked}; неизвестных: {result.Unknown}; ошибок: {result.Failed}. {result.Status}");
                ImGui.TextWrapped("Быстрый список ограничен кандидатами; полный каталог запускается отдельно. Живые строки проверяются без Universalis.");
                DrawSuggestions(result);
            }
        }
        ImGui.End();
    }
    private void DrawLive()
    {
        var qualified = live.Where(q => q.Profit >= config.MinimumProfit).OrderByDescending(q => q.Profit).ToArray();
        ImGui.TextUnformatted($"Видимые лоты от порога: {qualified.Length}; вложить {qualified.Sum(q => q.Cost):N0}, NPC {qualified.Sum(q => q.Revenue):N0}, прибыль {qualified.Sum(q => q.Profit):N0} гил.");
        if (qualified.Length == 0) return;
        if (!ImGui.BeginTable("live", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 130))) return;
        foreach (var name in new[] { "Ретейнер / качество", "Кол-во", "Покупка с налогом", "Выручка NPC", "Прибыль" }) ImGui.TableSetupColumn(name);
        ImGui.TableHeadersRow();
        foreach (var q in qualified)
        {
            ImGui.TableNextRow(); Cell(0, q.Lot.Retainer + (q.Lot.Conservative ? " (NQ минимум)" : q.Lot.Hq ? " HQ" : " NQ")); Cell(1, q.Lot.Quantity.ToString());
            Cell(2, q.Cost.ToString("N0")); Cell(3, q.Revenue.ToString("N0")); Cell(4, "+" + q.Profit.ToString("N0"));
        }
        ImGui.EndTable();
    }
    private void DrawSuggestions(ScanResult snapshot)
    {
        var suggestions = snapshot.Suggestions.Where(s => s.Quote.Profit >= config.MinimumProfit && Policy.Fresh(s.Updated, DateTimeOffset.UtcNow, config.MaximumAgeMinutes)).ToArray();
        if (suggestions.Length == 0) { ImGui.TextWrapped("Подходящих лотов в проверенной части кэша нет. Открытые предметы всё равно проверяются по живой выдаче."); return; }
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

    private string diagnostics = "";
    private void ReadLive(AddonItemSearchResult* addon)
    {
        live.Clear(); highlighted = 0;
        LoadCatalog();
        var agent = AgentItemSearch.Instance();
        var proxy = agent == null ? null : agent->InfoProxyItemSearch;
        var list = addon->Results;
        string title = addon->ItemName == null ? "" : ReadText(addon->ItemName);
        var item = catalog!.Values.FirstOrDefault(i => string.Equals(i.Name, title, StringComparison.Ordinal));
        if (item == null) { liveStatus = $"Не распознано название предмета: {title}"; return; }
        if (list == null || proxy == null) { liveStatus = "Нет списка строк / данных рынка."; return; }
        // SearchItemId describes the search request, not necessarily the selected result.
        // Update/wait flags may remain set while 100 capped offers are already rendered.
        // Validate the visible title and each rendered row against actual native prices instead.
        var lots = new List<Lot>();
        foreach (var l in proxy->Listings)
        {
            if (l.ListingId == 0 || l.ItemId != item.Id) continue;
            lots.Add(new(l.ListingId, l.ItemId, "", l.UnitPrice, l.Quantity, l.TotalTax, l.IsHqItem,
                l.IsMannequin || l.IsSellingAsSet, item.Equipment && l.Durability != 30000));
        }
        var used = new HashSet<nint>();
        int visible = 0, parsed = 0, matched = 0;
        int length = Math.Clamp(list->AllocatedItemRendererListLength, 0, 200);
        bool canHighlight = config.Highlight && addon->NumBlockingAddons == 0;
        for (int i = 0; i < length && list->ItemRendererList != null; i++)
        {
            var renderer = list->ItemRendererList[i].AtkComponentListItemRenderer;
            if (renderer == null || !used.Add((nint)renderer) || renderer->OwnerNode == null || !renderer->OwnerNode->IsVisible()) continue;
            var fields = new List<RowField>();
            CollectText(&renderer->UldManager, fields, 0);
            var ordered = fields.Where(f => !string.IsNullOrWhiteSpace(f.Text)).OrderBy(f => f.X).ToArray();
            if (ordered.Length == 0) continue;
            visible++;
            var row = Policy.ParseRow(ordered.Select(f => f.Text).ToArray());
            if (row == null) continue;
            parsed++;
            var quote = Policy.FromRow(item, row, lots);
            if (quote == null) continue;
            matched++;
            live.Add(quote);
            if (!canHighlight || quote.Profit < config.MinimumProfit) continue;
            float scale = addon->GetScale();
            var start = ImGui.GetMainViewport().Pos + new Vector2(ordered.Min(f => f.X) - 4, ordered.Min(f => f.Y) - 2);
            var end = ImGui.GetMainViewport().Pos + new Vector2(ordered.Max(f => f.X + f.Width * scale) + 4, ordered.Max(f => f.Y + f.Height * scale) + 2);
            var owner = list->OwnerNode;
            if (owner == null) continue;
            // Do not paint recycled or partially clipped rows outside the actual list viewport.
            float left = owner->ScreenX, top = owner->ScreenY;
            var clipStart = ImGui.GetMainViewport().Pos + new Vector2(left, top);
            var clipEnd = clipStart + new Vector2(owner->Width * scale, owner->Height * scale);
            start = Vector2.Max(start, clipStart); end = Vector2.Min(end, clipEnd);
            if (end.X - start.X < 20 || end.Y - start.Y < 4) continue;
            var draw = ImGui.GetBackgroundDrawList();
            draw.AddRectFilled(start, end, 0x2520FF60);
            draw.AddRect(start, end, 0xFF60FF80, 2, ImDrawFlags.None, 2);
            highlighted++;
        }
        liveStatus = $"{item.Name}: проверены видимые строки {matched}/{visible}.";
        if (visible > 0 && matched == 0) liveStatus += " Строки не совпали с ценой, количеством и итогом игры.";
        diagnostics = $"v0.1.1 item={item.Id}; native={lots.Count}; renderers={length}; visible={visible}; parsed={parsed}; matched={matched}; " +
            $"request={proxy->SearchItemId}; selected={agent->ResultItemId}; waiting={proxy->WaitingForListings}; update={list->IsUpdatePending}";
    }
    private sealed record RowField(string Text, float X, float Y, ushort Width, ushort Height);
    private static string ReadText(AtkTextNode* node) =>
        Dalamud.Game.Text.SeStringHandling.SeString.Parse(node->NodeText.AsSpan()).TextValue.Trim();
    private static void CollectText(AtkUldManager* uld, List<RowField> texts, int depth)
    {
        if (depth > 3 || uld->NodeList == null || uld->NodeListCount > 256) return;
        for (int n = 0; n < uld->NodeListCount; n++)
        {
            var node = uld->NodeList[n];
            if (node == null || !node->IsVisible()) continue;
            if (node->Type == NodeType.Text)
                texts.Add(new(ReadText((AtkTextNode*)node), node->ScreenX, node->ScreenY, node->Width, node->Height));
            else if ((int)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null) CollectText(&component->UldManager, texts, depth + 1);
            }
        }
    }
    public void Dispose()
    {
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
