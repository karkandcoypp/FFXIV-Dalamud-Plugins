using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private readonly NativeBuyGateway buyGateway;
    private readonly AutoBuyer autoBuyer;
    private ulong ledgerCharacter;
    private DateTimeOffset nextAutoPoll;
    private ScanResult? considered;
    private bool ledgerValid;
    private static readonly JsonSerializerOptions LedgerJson = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private void OnAutoFramework(Dalamud.Plugin.Services.IFramework framework)
    {
        lock (automationGate)
        {
            if (disposed) return;
            try
            {
                bool boardOpen = Visible((AtkUnitBase*)Gui.GetAddonByName("ItemSearch").Address) || Visible((AtkUnitBase*)Gui.GetAddonByName("ItemSearchResult").Address);
                UpdateAutoBuy(boardOpen);
            }
            catch (Exception e)
            {
                autoBuyer.Stop("Ошибка автопокупки: " + e.Message); config.AutoBuyEnabled = false;
                if (error != e.Message) Log.Warning(e, "VendorProfit automatic purchasing stopped");
                error = e.Message;
            }
        }
    }
    private string LedgerPath(ulong character) => Path.Combine(Pi.ConfigDirectory.FullName, $"purchases-{character}.json");
    private void SaveBuyLedger(BuyLedger ledger)
    {
        if (ledger.Character == 0 || ledger.Character != ledgerCharacter) throw new InvalidOperationException("Персонаж журнала покупок не совпадает.");
        string path = LedgerPath(ledger.Character);
        Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(ledger, LedgerJson));
        File.Move(path + ".tmp", path, true);
    }
    private void LoadBuyLedger(ulong character)
    {
        autoBuyer.Stop("Загружаю журнал расходов…");
        ledgerCharacter = character; ledgerValid = false; considered = null;
        if (character == 0 || autoBuyer.HasPending) return;
        try
        {
            string path = LedgerPath(character);
            if (!File.Exists(path) && config.AutoBuyEnabled && config.AutoBuyCharacter == character)
                throw new InvalidDataException("Журнал активного режима отсутствует. Выключите режим и проверьте расходы; автоматический сброс запрещён.");
            var ledger = File.Exists(path) ? JsonSerializer.Deserialize<BuyLedger>(File.ReadAllText(path), LedgerJson) : new BuyLedger { Character = character };
            if (ledger == null || ledger.Character != character || ledger.Charged < 0 || ledger.Entries == null || ledger.Entries.Count > 1000 ||
                ledger.Entries.Any(e => e.Cost <= 0) || ledger.Entries.Sum(e => e.Cost) != ledger.Charged)
                throw new InvalidDataException("Журнал расходов повреждён; автоматический сброс запрещён.");
            autoBuyer.Attach(ledger); ledgerValid = true;
            if (ledger.Entries.Any(e => e.Status != "Подтверждено"))
                autoBuyer.Stop("В журнале есть неподтверждённая покупка. Проверьте инвентарь и расходы перед ручным сбросом.");
            else if (config.AutoBuyEnabled && config.AutoBuyCharacter == character) autoBuyer.Resume();
        }
        catch (Exception e) { Log.Warning(e, "VendorProfit purchase ledger blocked"); autoBuyer.Stop("Не удалось прочитать журнал расходов: " + e.Message); }
    }
    private BuyContext BuyState(bool boardOpen)
    {
        var inv = InventoryManager.Instance();
        bool modal = Visible((AtkUnitBase*)Gui.GetAddonByName("SelectYesno").Address) || Visible((AtkUnitBase*)Gui.GetAddonByName("ContextMenu").Address);
        uint actualWorld = Client.IsLoggedIn ? Objects.LocalPlayer?.CurrentWorld.RowId ?? 0 : 0;
        return new(Player.IsLoaded ? Player.ContentId : 0, actualWorld, boardOpen, modal, inv == null ? 0 : inv->GetGil(), inv == null ? 0 : inv->GetEmptySlotsInBag());
    }
    private void UpdateAutoBuy(bool boardOpen)
    {
        ulong character = Client.IsLoggedIn && Player.IsLoaded ? Player.ContentId : 0;
        if (character != ledgerCharacter)
        {
            if (autoBuyer.HasPending)
            {
                autoBuyer.AbandonPending("Персонаж изменился до подтверждения покупки. Сумма оставлена в журнале до ручной проверки.");
            }
            LoadBuyLedger(character);
        }
        if (world == 0 && !autoBuyer.HasPending) return;
        bool authorized = config.AutoBuyEnabled && ledgerValid && character != 0 && config.AutoBuyCharacter == character;
        if (!authorized && !autoBuyer.HasPending) return;
        var state = BuyState(boardOpen);
        var now = DateTimeOffset.UtcNow;
        bool wasBusy = autoBuyer.Busy;
        if (autoBuyer.HasPending || authorized) autoBuyer.Tick(now, state, config.AutoBuyLimits, authorized, config.AutoBuyDryRun);
        if (wasBusy && !autoBuyer.Busy) nextAutoPoll = now.AddSeconds(config.AutoBuyLimits.IntervalSeconds);
        if (!authorized || !boardOpen || autoBuyer.Halted || autoBuyer.Busy || autoBuyer.HasPending || scan != null || state.Blocked) return;
        if (result != null && !ReferenceEquals(result, considered))
        {
            considered = result;
            // A Chaos result may inform the table, but never authorizes buying on another world.
            if (Policy.Fresh(result.Finished, now, 2))
            {
                LoadCatalog(); autoBuyer.Begin(result.Suggestions, catalog!, state, config.AutoBuyLimits);
                nextAutoPoll = now.AddSeconds(config.AutoBuyLimits.IntervalSeconds);
                if (autoBuyer.Busy) return;
            }
        }
        if (now >= nextAutoPoll)
        {
            nextAutoPoll = now.AddSeconds(config.AutoBuyLimits.IntervalSeconds);
            StartScan();
        }
    }
    private void DrawAutoBuy()
    {
        if (config.AutoBuyEnabled) ImGui.TextWrapped("Авторежим: " + autoBuyer.Status);
        if (!ImGui.CollapsingHeader("Периодический поиск и автопокупка")) return;
        bool changed = ImGui.Checkbox("Включить периодический поиск и автопокупку", ref config.AutoBuyEnabled);
        if (changed)
        {
            if (config.AutoBuyEnabled && ledgerValid && Player.IsLoaded)
            { config.AutoBuyCharacter = Player.ContentId; SaveBuyLedger(autoBuyer.Ledger); autoBuyer.Resume(); nextAutoPoll = DateTimeOffset.MinValue; considered = result; }
            else { config.AutoBuyEnabled = false; autoBuyer.Stop("Автопокупка выключена."); }
        }
        changed |= ImGui.Checkbox("Пробный режим — проверять без покупок", ref config.AutoBuyDryRun);
        changed |= ImGui.InputInt("Авто: максимум за стак", ref config.AutoBuyLimits.MaximumLot);
        changed |= ImGui.InputInt("Авто: общий бюджет до сброса", ref config.AutoBuyLimits.Budget);
        changed |= ImGui.InputInt("Авто: минимум прибыли за стак", ref config.AutoBuyLimits.MinimumProfit);
        changed |= ImGui.InputInt("Авто: минимум прибыли, % от покупки", ref config.AutoBuyLimits.MinimumRoi);
        changed |= ImGui.InputInt("Авто: интервал проверок, секунд", ref config.AutoBuyLimits.IntervalSeconds);
        changed |= ImGui.InputInt("Авто: максимум покупок до сброса", ref config.AutoBuyLimits.MaximumPurchases);
        if (changed) { config.AutoBuyLimits.Normalize(); Pi.SavePluginConfig(config); }
        ImGui.TextWrapped($"Учтено в бюджете: {autoBuyer.Ledger.Charged:N0} / {config.AutoBuyLimits.Budget:N0} гил; попыток {autoBuyer.Ledger.Entries.Count}. {autoBuyer.Status}");
        ImGui.TextWrapped("Работает при открытом Market Board на текущем сервере и только для включившего режим персонажа. Свежая игровая выдача проверяется перед каждой покупкой. Автопоиск может закрывать список лотов. Пробный режим не тратит гили.");
        ImGui.TextWrapped("Неоднозначный результат останавливает покупки; сумма остаётся учтённой до ручной проверки. Перезагрузка плагина расходы не сбрасывает. Продажа NPC выполняется вручную.");
        if (ImGui.Button("Остановить режим")) { config.AutoBuyEnabled = false; autoBuyer.Stop("Остановлено вручную."); Pi.SavePluginConfig(config); }
        ImGui.SameLine();
        ImGui.BeginDisabled(!ledgerValid || autoBuyer.HasPending);
        if (ImGui.Button("Возобновить после проверки")) { autoBuyer.Resume(); nextAutoPoll = DateTimeOffset.MinValue; }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(config.AutoBuyEnabled || autoBuyer.HasPending || !ledgerValid);
        if (ImGui.Button("Сбросить учтённые расходы и журнал"))
        {
            var ledger = new BuyLedger { Character = ledgerCharacter };
            SaveBuyLedger(ledger); autoBuyer.Attach(ledger);
        }
        ImGui.EndDisabled();
        foreach (var entry in autoBuyer.Ledger.Entries.TakeLast(8).Reverse())
            ImGui.TextWrapped($"{entry.At.LocalDateTime:t}: #{entry.Item} ×{entry.Quantity}, {entry.Cost:N0} гил, расчётная прибыль {entry.Profit:N0}; {entry.Status}");
        foreach (string observation in autoBuyer.Observations.TakeLast(3)) ImGui.TextWrapped(observation);
    }
}
