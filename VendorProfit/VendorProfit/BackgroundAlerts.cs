using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Utility;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private ulong alertCharacter;
    private Dictionary<string,DateTimeOffset> alerted = new();
    private DateTimeOffset nextAlert, nextWorkerCheck;
    private bool scannerStoppedManually;
    private DateTimeOffset nextBackgroundHttp;
    private string alertStatus = "Ожидание фоновых результатов.";
    private void NormalizeAlerts()
    {
        config.AlertMinimumProfit=Math.Clamp(config.AlertMinimumProfit,1,10_000_000);
        config.AlertMaximumLot=Math.Clamp(config.AlertMaximumLot,1,10_000_000);
        config.AlertMinimumRoi=Math.Clamp(config.AlertMinimumRoi,1,10000);
        config.AlertCooldownSeconds=Math.Clamp(config.AlertCooldownSeconds,30,1800);
    }
    private void UpdateBackgroundAlerts(DateTimeOffset now)
    {
        if (!config.BackgroundAlerts || !Client.IsLoggedIn || !Player.IsLoaded || world==0) return;
        try
        {
            if (config.UseExternalScanner && config.AutoStartScanner && !scannerStoppedManually && now>=nextWorkerCheck)
            {
                nextWorkerCheck=now.AddMinutes(5);
                // A ready/error heartbeat indicates a live worker (network errors are retried by that worker).
                bool recent=false;
                string statusPath=ExternalPath("external-status.json");
                if (File.Exists(statusPath))
                {
                    using var doc=JsonDocument.Parse(File.ReadAllText(statusPath));
                    var root=doc.RootElement;
                    string? workerState=root.GetProperty("State").GetString();
                    if (workerState is "Ready" or "Error" &&
                        (!root.TryGetProperty("Version",out var version) || version.GetString()!="0.5.4"))
                    {
                        File.WriteAllText(ExternalPath("external-worker.stop"),"update");
                        nextWorkerCheck=now.AddSeconds(5);externalStatus="Перезапускаю старую версию фонового сканера…";return;
                    }
                    recent=root.TryGetProperty("At",out var at) && at.TryGetDateTimeOffset(out var when) &&
                        when>now.AddSeconds(-Math.Max(300,config.AutoBuyLimits.IntervalSeconds*2+60)) &&
                        root.GetProperty("State").GetString() is "Ready" or "Error";
                }
                if (!recent) { ExportScannerRequest(); StartExternalProcess(); }
            }
            if (!config.UseExternalScanner && scan==null && now>=nextBackgroundHttp)
            {
                nextBackgroundHttp=now.AddSeconds(config.AutoBuyLimits.IntervalSeconds);
                StartScan(chaos:config.BackgroundChaos);
            }
            if (alertCharacter!=Player.ContentId)
            {
                alertCharacter=Player.ContentId;
                string path=ExternalPath($"notifications-{alertCharacter}.json");
                alerted=File.Exists(path) && new FileInfo(path).Length<=2_000_000
                    ? JsonSerializer.Deserialize<Dictionary<string,DateTimeOffset>>(File.ReadAllText(path)) ?? new() : new();
                alerted=alerted.OrderByDescending(p=>p.Value).Take(5000).ToDictionary(p=>p.Key,p=>p.Value);
                nextAlert=DateTimeOffset.MinValue;
            }
            if (now<nextAlert) return;
            var snapshot=config.UseExternalScanner ? externalSnapshot : result;
            if (snapshot==null) return;
            var found=AlertPolicy.NewLots(snapshot,world,config.BackgroundChaos,now,config.MaximumAgeMinutes,
                config.AlertMinimumProfit,config.AlertMaximumLot,config.AlertMinimumRoi,alerted.Keys.ToHashSet());
            if (found.Length==0) return;
            var message=AlertMessage().AddUiForeground($"Потенциальных лотов: {found.Length}. ",31);
            foreach (var lot in found.Take(3))
            {
                string server=string.IsNullOrEmpty(lot.WorldName) ? Data.GetExcelSheet<World>().GetRowOrDefault(lot.World)?.Name.ToString() ?? lot.World.ToString() : lot.WorldName;
                message.AddText(server+": ").AddItemLink(lot.Item.Id,lot.Quote.Lot.Hq)
                    .AddText($" ×{lot.Quote.Lot.Quantity}, купить за ")
                    .AddUiForeground($"{lot.Quote.Cost:N0}",31).AddText(", ")
                    .AddUiForeground($"прибыль +{lot.Quote.Profit:N0} гил",45)
                    .AddText($", кэш {(now-lot.Updated).TotalMinutes:F0} мин; ");
            }
            message.AddText("Подробнее: /vendorprofit. Наличие не подтверждено в игре.");
            Chat.Print(message.Build()); // Local echo only: no /tell, chat network messages or game search requests.
            foreach (var lot in found) alerted[AlertPolicy.Key(lot)]=now;
            alerted=alerted.OrderByDescending(p=>p.Value).Take(5000).ToDictionary(p=>p.Key,p=>p.Value);
            string file=ExternalPath($"notifications-{alertCharacter}.json");
            Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
            File.WriteAllText(file+".tmp",JsonSerializer.Serialize(alerted));File.Move(file+".tmp",file,true);
            nextAlert=now.AddSeconds(config.AlertCooldownSeconds);alertStatus=$"Сообщено о {found.Length} новых лотах ({now.LocalDateTime:t}).";
            PlayAlertEffects();
        }
        catch (Exception e)
        {
            nextAlert=now.AddMinutes(1);alertStatus="Ошибка фоновых уведомлений: "+e.Message;
            Log.Warning(e,"VendorProfit background alerts failed");
        }
    }
    // Each colored fragment restores the default, preserving item links and subsequent chat lines.
    private static SeStringBuilder AlertMessage()=>new SeStringBuilder().AddUiForeground("[VendorProfit] ",31);
    private void PlayAlertEffects()
    {
        if(config.AlertSound)
        {
            try { UIGlobals.PlayChatSoundEffect(3); }
            catch(Exception e) { Log.Warning(e,"VendorProfit notification sound failed"); }
        }
        if(config.AlertFlashTaskbar)
        {
            // Never flash an active game, including from the test button. Unknown focus fails closed.
            try { WindowAttention.FlashIfInactive((uint)Environment.ProcessId, WindowAttention.ForegroundProcess,
                () => Util.FlashWindow(flashIfOpen: false)); }
            catch(Exception e) { Log.Warning(e,"VendorProfit taskbar notification failed"); }
        }
    }
    private void DrawBackgroundAlerts()
    {
        ImGui.TextUnformatted("Сообщения только для вас");
        bool changed=ImGui.Checkbox("Сообщать о новых лотах в чат",ref config.BackgroundAlerts);
        Help("Локальные сообщения в игре. Отправки другим игрокам нет.");
        changed|=ImGui.Checkbox("Звук при новом уведомлении",ref config.AlertSound);
        Help("Один звук на сообщение о новых лотах. Громкость зависит от настроек звука игры.");
        changed|=ImGui.Checkbox("Мигать значком игры на панели задач",ref config.AlertFlashTaskbar);
        Help("Если игра свёрнута или вы в другом окне. Мигание прекращается при возвращении в игру.");
        changed|=ImGui.Checkbox("Запускать поиск при входе в игру",ref config.AutoStartScanner);
        changed|=ImGui.Checkbox("Искать по всему Chaos",ref config.BackgroundChaos);
        ImGui.Spacing();ImGui.TextUnformatted("Когда сообщать");
        if(BeginFields("alert-settings"))
        {
            changed|=NumberField("Прибыль за стак от, гил",ref config.AlertMinimumProfit);
            changed|=NumberField("Стоимость стака до, гил",ref config.AlertMaximumLot);
            changed|=NumberField("Прибыль от, %",ref config.AlertMinimumRoi);
            changed|=NumberField("Пауза между сообщениями, сек",ref config.AlertCooldownSeconds);
            changed|=NumberField("Интервал поиска, сек",ref config.AutoBuyLimits.IntervalSeconds);
            ImGui.EndTable();
        }
        if(changed)
        {
            NormalizeAlerts();config.AutoBuyLimits.Normalize();nextExternalExport=DateTimeOffset.MinValue;nextWorkerCheck=DateTimeOffset.MinValue;
            Pi.SavePluginConfig(config);
        }
        ImGui.Spacing();ImGui.TextDisabled("Аукцион можно закрыть. Повторные сообщения о том же лоте подавляются.");
        if (ImGui.Button("Тестовое сообщение"))
        {
            Chat.Print(AlertMessage().AddUiForeground("Тестовое уведомление",31)
                .AddText(": ").AddUiForeground("сообщение видно только вам.",45).Build());
            PlayAlertEffects();
        }
        Help("Проверяет сообщение и звук. Мигание срабатывает только при неактивной игре, в том числе для теста.");
        if(config.UseExternalScanner){ImGui.SameLine();DrawWorkerButtons();}
        ImGui.Spacing();ImGui.TextWrapped(alertStatus);
    }
}
