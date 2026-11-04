using System.Diagnostics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private DateTimeOffset nextExternalExport, nextExternalRead;
    private DateTimeOffset nextExternalDecision;
    private DateTime externalWriteTime;
    private ScanResult? externalSnapshot;
    private string externalStatus = "Внешний сканер ещё не запущен.";
    private string? exportedRequest;
    private string ExternalPath(string name) => Path.Combine(Pi.ConfigDirectory.FullName, name);
    private void ExportScannerRequest()
    {
        if (world == 0) return;
        LoadCatalog();
        var limits = config.AutoBuyLimits;
        var request = new ScannerRequest(1, world, Data.GetExcelSheet<World>().GetRowOrDefault(world)?.Name.ToString() ?? world.ToString(),
            Policy.QuickItems(catalog!, config.LearnedItems ?? []), limits.IntervalSeconds, config.MaximumAgeMinutes,
            config.BackgroundAlerts ? Math.Max(limits.MaximumLot,config.AlertMaximumLot) : limits.MaximumLot,
            config.BackgroundAlerts ? Math.Min(limits.MinimumProfit,config.AlertMinimumProfit) : limits.MinimumProfit,
            config.BackgroundAlerts ? Math.Min(limits.MinimumRoi,config.AlertMinimumRoi) : limits.MinimumRoi, config.BackgroundChaos);
        string json = JsonSerializer.Serialize(request);
        if (json == exportedRequest) return;
        Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
        string path = ExternalPath("external-request.json");
        File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
        exportedRequest = json;
    }
    private void RefreshExternalFiles(DateTimeOffset now)
    {
        if (now >= nextExternalExport) { ExportScannerRequest(); nextExternalExport = now.AddSeconds(15); }
        if (now >= nextExternalRead)
        {
            nextExternalRead = now.AddSeconds(2);
            try
            {
                var file = new FileInfo(ExternalPath("external-result.json"));
                if (file.Exists && file.Length <= 2_000_000 && file.LastWriteTimeUtc != externalWriteTime)
                {
                    var snapshot = JsonSerializer.Deserialize<ScanResult>(File.ReadAllText(file.FullName));
                    externalSnapshot = snapshot; externalWriteTime = file.LastWriteTimeUtc;
                    if (snapshot?.Scope == "External" && snapshot.World == world) result = snapshot;
                }
                string statusPath = ExternalPath("external-status.json");
                if (File.Exists(statusPath) && new FileInfo(statusPath).Length <= 64_000)
                {
                    using var status = JsonDocument.Parse(File.ReadAllText(statusPath));
                    externalStatus = status.RootElement.GetProperty("State").GetString() + ": " + status.RootElement.GetProperty("Message").GetString();
                }
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
            { externalStatus = "Не удалось прочитать внешний результат: " + e.Message; externalSnapshot = null; }
        }
    }
    private void RunExternalInbox(BuyContext context, DateTimeOffset now)
    {
        RefreshExternalFiles(now);
        if (now < nextExternalDecision || externalSnapshot == null) return;
        nextExternalDecision = now.AddSeconds(2);
        var seen = autoBuyer.Ledger.CheckedExternal.ToHashSet(StringComparer.Ordinal);
        var candidates = ExternalFeed.Select(externalSnapshot, context.World, now, seen);
        if (candidates.Length == 0) return;
        LoadCatalog(); autoBuyer.Begin(candidates, catalog!, context, config.AutoBuyLimits, exactExternal: true);
        if (!autoBuyer.Busy) return;
        var queued = autoBuyer.QueuedItems.ToHashSet();
        // Keep recent tickets, independent of upload timestamp; expenditure entries are never trimmed here.
        autoBuyer.Ledger.CheckedExternal = autoBuyer.Ledger.CheckedExternal
            .Concat(candidates.Where(c => queued.Contains(c.Item.Id)).Select(ExternalFeed.Ticket)).Distinct().TakeLast(10000).ToList();
        SaveBuyLedger(autoBuyer.Ledger); // Failure cancels the cycle before its first native query.
    }
    private void DrawExternalControls(ref bool changed)
    {
        bool switched = ImGui.Checkbox("Источник автопоиска: отдельный скрипт Universalis", ref config.UseExternalScanner);
        if (switched)
        {
            autoBuyer.Stop("Источник поиска изменён."); if (config.AutoBuyEnabled) autoBuyer.Resume();
            nextExternalExport = DateTimeOffset.MinValue; changed = true;
        }
        if (!config.UseExternalScanner) return;
        DrawWorkerButtons();
        ImGui.TextWrapped("Поиск выполняется через Universalis. Условия покупки проверяются отдельно в игре.");
        ImGui.BeginDisabled(!ledgerValid || autoBuyer.Busy || autoBuyer.HasPending);
        if (ImGui.Button("Разрешить повторную проверку старых предложений"))
        {
            autoBuyer.Ledger.CheckedExternal.Clear(); SaveBuyLedger(autoBuyer.Ledger);
            if (config.AutoBuyEnabled) autoBuyer.Resume();
        }
        ImGui.EndDisabled();
        Help("Сбрасывает историю проверенных предложений. Учтённые расходы сохраняются.");
    }
    private void DrawWorkerButtons()
    {
        ImGui.BeginDisabled(world == 0);
        if (ImGui.Button("Запустить поиск"))
        {
            try
            {
                ExportScannerRequest();
                scannerStoppedManually = false; StartExternalProcess();
            }
            catch (Exception e) { externalStatus = "Ошибка запуска: " + e.Message; }
        }
        ImGui.SameLine();
        if (ImGui.Button("Остановить поиск"))
        { scannerStoppedManually = true; Directory.CreateDirectory(Pi.ConfigDirectory.FullName); File.WriteAllText(ExternalPath("external-worker.stop"), "stop"); }
        ImGui.EndDisabled();
    }
    private void StartExternalProcess()
    {
        string script = Path.Combine(Pi.AssemblyLocation.DirectoryName!, "vendorprofit_worker.py");
        string launcher = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "pyw.exe");
        if (!File.Exists(launcher)) throw new FileNotFoundException("Установите Python 3 с Python Launcher (pyw.exe).");
        if (!File.Exists(script)) throw new FileNotFoundException("Скрипт сканера отсутствует; переустановите пакет VendorProfit.");
        var start = new ProcessStartInfo(launcher) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-3"); start.ArgumentList.Add(script); start.ArgumentList.Add("--directory"); start.ArgumentList.Add(Pi.ConfigDirectory.FullName);
        using var process = Process.Start(start);
        externalStatus = "Сканер запускается отдельно от игры…";
    }
}
