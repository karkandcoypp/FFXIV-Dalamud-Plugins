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
            UpdateStatisticsSafely();
            UpdateNpcSalesSafely();
            try
            {
                bool boardOpen = Visible((AtkUnitBase*)Gui.GetAddonByName("ItemSearch").Address) || Visible((AtkUnitBase*)Gui.GetAddonByName("ItemSearchResult").Address);
                if (config.UseExternalScanner && world != 0 && (boardOpen || config.BackgroundAlerts))
                    RefreshExternalFiles(DateTimeOffset.UtcNow);
                UpdateBackgroundAlerts(DateTimeOffset.UtcNow);
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
        ImportPurchaseStatistics(ledger);
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
                ledger.CheckedExternal == null || ledger.CheckedExternal.Count > 10000 ||
                ledger.Entries.Any(e => e.Cost <= 0) || ledger.Entries.Sum(e => e.Cost) != ledger.Charged)
                throw new InvalidDataException("Журнал расходов повреждён; автоматический сброс запрещён.");
            autoBuyer.Attach(ledger); ledgerValid = true;
            ImportPurchaseStatistics(ledger);
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
        if (!authorized || autoBuyer.Halted || autoBuyer.Busy || autoBuyer.HasPending) return;
        if (!boardOpen) { autoBuyer.Wait("Откройте Market Board: покупки работают только при открытом аукционе."); return; }
        if (state.Blocked) { autoBuyer.Wait("Закройте контекстное меню или диалог подтверждения в игре."); return; }
        if (scan != null) { autoBuyer.Wait("Ожидаю завершения текущего поиска лотов."); return; }
        if (config.UseExternalScanner)
        {
            RunExternalInbox(state, now);
            return; // No internal discovery timer or game query when the external inbox has no new target.
        }
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
        bool changed = ImGui.Checkbox("Включить автопокупку", ref config.AutoBuyEnabled);
        if (changed)
        {
            if (config.AutoBuyEnabled && ledgerValid && Player.IsLoaded)
            { config.AutoBuyCharacter = Player.ContentId; SaveBuyLedger(autoBuyer.Ledger); autoBuyer.Resume(); nextAutoPoll = DateTimeOffset.MinValue; considered = result; }
            else { config.AutoBuyEnabled = false; autoBuyer.Stop("Автопокупка выключена."); }
        }
        changed |= ImGui.Checkbox("Пробный режим — не тратить гили", ref config.AutoBuyDryRun);
        ImGui.TextWrapped(config.AutoBuyDryRun ? "Покажет предполагаемые покупки без расходов." : "Подходящие стаки покупаются без отдельного подтверждения. Продажа NPC — вручную.");
        SectionLabel("ЛИМИТЫ ПОКУПОК");
        if(ImGui.BeginTable("purchase-limits",2,ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();changed |= StackedNumber("Цена стака до, гил", ref config.AutoBuyLimits.MaximumLot);
            ImGui.TableNextColumn();changed |= StackedNumber("Общий бюджет, гил", ref config.AutoBuyLimits.Budget);
            ImGui.TableNextColumn();changed |= StackedNumber("Прибыль за стак от, гил", ref config.AutoBuyLimits.MinimumProfit);
            ImGui.TableNextColumn();changed |= StackedNumber("Прибыль от, %", ref config.AutoBuyLimits.MinimumRoi);
            ImGui.EndTable();
        }
        if(ImGui.CollapsingHeader("Дополнительные лимиты"))
        {
            if(BeginFields("extra-purchase-limits"))
            {
                changed |= NumberField("Интервал поиска, сек", ref config.AutoBuyLimits.IntervalSeconds);
                changed |= NumberField("Максимум попыток", ref config.AutoBuyLimits.MaximumPurchases);
                changed |= NumberField("Пауза перед покупкой, сек", ref config.AutoBuyLimits.PurchaseDelaySeconds);
                Help("От 1 до 15 секунд после запроса выдачи. По умолчанию 5; затем цена и лимиты проверяются заново.");
                ImGui.EndTable();
            }
        }
        if (changed) { config.AutoBuyLimits.Normalize(); Pi.SavePluginConfig(config); }
        ImGui.Spacing();ImGui.TextUnformatted($"Учтено расходов: {autoBuyer.Ledger.Charged:N0} / {config.AutoBuyLimits.Budget:N0} гил");
        ImGui.ProgressBar(Math.Clamp((float)autoBuyer.Ledger.Charged/config.AutoBuyLimits.Budget,0,1),new System.Numerics.Vector2(-1,8*UiScale),"");
        ImGui.TextWrapped(autoBuyer.Status);
        if (ImGui.Button("Остановить режим")) { config.AutoBuyEnabled = false; autoBuyer.Stop("Остановлено вручную."); Pi.SavePluginConfig(config); }
        ImGui.SameLine();
        ImGui.BeginDisabled(!ledgerValid || autoBuyer.HasPending);
        if (ImGui.Button("Продолжить")) { autoBuyer.Resume(); nextAutoPoll = DateTimeOffset.MinValue; }
        ImGui.EndDisabled();
        if(ImGui.CollapsingHeader("Журнал и сброс"))
        {
        ImGui.TextWrapped("Расходы сохраняются после перезапуска. Неподтверждённая покупка остаётся в лимите до ручной проверки.");
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
}
