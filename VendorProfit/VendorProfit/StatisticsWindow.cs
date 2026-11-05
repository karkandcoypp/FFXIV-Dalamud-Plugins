using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private int statisticsPeriod;
    private ulong selectedPurchase;
    private int saleQuantity = 1;
    private void DrawStatistics()
    {
        if (!statisticsValid)
        { ImGui.TextWrapped(statisticsStatus); return; }
        ImGui.SetNextItemWidth(195 * UiScale);
        ImGui.Combo("Покупки за период", ref statisticsPeriod, new[] { "Всё время", "Сегодня", "Последние 7 дней", "Последние 30 дней" }, 4);
        Help("Период относится к дате покупки. Продажи учитываются только после вашей ручной отметки.");
        var now = DateTimeOffset.Now;
        var since = statisticsPeriod switch { 1 => new DateTimeOffset(now.Date, now.Offset), 2 => now.AddDays(-7), 3 => now.AddDays(-30), _ => DateTimeOffset.MinValue };
        var rows = statistics.Purchases.Where(p => p.At >= since).OrderByDescending(p => p.At).ToArray();
        if (ImGui.BeginTable("trade-totals", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();Metric("spent", "ПОТРАЧЕНО", rows.Sum(p => p.Cost).ToString("N0"), MarketTheme.Text);
            ImGui.TableNextColumn();Metric("received", "ВЫРУЧКА NPC*", rows.Sum(p => p.SoldRevenue).ToString("N0"), MarketTheme.Text);
            ImGui.TableNextColumn();Metric("earned", "ПРИБЫЛЬ*", "+" + rows.Sum(p => p.RealizedProfit).ToString("N0"), MarketTheme.Gold);
            ImGui.EndTable();
        }
        ImGui.TextWrapped($"Не продано: {rows.Sum(p => (long)p.Quantity - p.SoldQuantity):N0} шт. · вложено {rows.Sum(p => p.RemainingCost):N0} гил · возможная прибыль +{rows.Sum(p => p.RemainingProfit):N0} гил");
        ImGui.TextDisabled("* Продажи подтверждаются вручную. Все суммы — в гилях.");
        Help("Прибыль считается по проданным единицам с их долей полной закупочной стоимости, включая налог. Остаток — потенциальная прибыль, не выручка.");
        long soldCost=rows.Sum(p=>p.SoldCost);
        if(soldCost>0)ImGui.TextDisabled($"Доходность проданного: {100m*rows.Sum(p=>p.RealizedProfit)/soldCost:F1}%");
        if (ImGui.SmallButton("Экспорт CSV"))
        {
            try
            {
                string path = ExternalPath($"statistics-{statistics.Character}-export.csv");
                Directory.CreateDirectory(Pi.ConfigDirectory.FullName);
                File.WriteAllText(path + ".tmp", TradeStatistics.Csv(statistics.Purchases.OrderBy(p => p.At)), new UTF8Encoding(true));
                File.Move(path + ".tmp", path, true);statisticsStatus = "CSV сохранён: " + path;
            }
            catch(Exception e){statisticsStatus = "Не удалось сохранить CSV: " + e.Message;}
        }
        ImGui.TextWrapped(statisticsStatus);
        if (statisticsDirty) ImGui.TextColored(MarketTheme.Gold, "Есть изменения, ожидающие сохранения на диск.");
        var selected = rows.FirstOrDefault(p => p.Listing == selectedPurchase);
        if (selected != null)
        {
            ImGui.Separator();ImGui.TextWrapped(selected.Name);
            int remaining = (int)Math.Min(int.MaxValue, selected.Quantity - selected.SoldQuantity);
            ImGui.SetNextItemWidth(110*UiScale);
            Number("шт. продано NPC",ref saleQuantity);saleQuantity=Math.Clamp(saleQuantity,1,Math.Max(1,remaining));
            ImGui.BeginDisabled(remaining==0);
            if(ImGui.Button("Подтвердить продажу NPC"))ChangeSold(selected,TradeStatistics.MarkSold(selected,(uint)saleQuantity));
            ImGui.EndDisabled();ImGui.SameLine();
            ImGui.BeginDisabled(selected.SoldQuantity==0);
            if(ImGui.Button("Снять отметки продажи"))ChangeSold(selected,selected with {SoldQuantity=0});
            ImGui.EndDisabled();
            Help("Только исправляет статистику. Не продаёт предмет и не возвращает гили в игре.");
            ImGui.TextDisabled("Отмечайте продажу только после получения гилей от NPC.");
        }
        if(rows.Length==0)
        {
            ImGui.Spacing();ImGui.TextWrapped("Покупок за этот период пока нет. Подтверждённые покупки с выгодой для перепродажи NPC появятся здесь автоматически, даже при закрытом окне.");
            return;
        }
        float height=Math.Max(100*UiScale,ImGui.GetContentRegionAvail().Y-8*UiScale);
        if(!ImGui.BeginTable("purchase-history",6,ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY|ImGuiTableFlags.Resizable,new Vector2(0,height)))return;
        ImGui.TableSetupColumn("Предмет",ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Шт.",ImGuiTableColumnFlags.WidthFixed,38*UiScale);
        ImGui.TableSetupColumn("Покупка",ImGuiTableColumnFlags.WidthFixed,80*UiScale);
        ImGui.TableSetupColumn("Прибыль NPC",ImGuiTableColumnFlags.WidthFixed,95*UiScale);
        ImGui.TableSetupColumn("Продано",ImGuiTableColumnFlags.WidthFixed,60*UiScale);
        ImGui.TableSetupColumn("Учтено*",ImGuiTableColumnFlags.WidthFixed,75*UiScale);
        ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
        foreach(var p in rows.Take(1000))
        {
            ImGui.PushID(p.Listing.ToString());ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
            if(ImGui.Selectable(p.Name+(p.Hq==true?" HQ":""),selectedPurchase==p.Listing))
            {selectedPurchase=p.Listing;saleQuantity=(int)Math.Min(int.MaxValue,Math.Max(1,p.Quantity-p.SoldQuantity));}
            Help($"{p.Name}\n{p.At.LocalDateTime:g} · сервер {(p.World==0?"неизвестен":p.World.ToString())}\nИсточник: {p.Source}\nПродажа всего стака NPC: {p.ExpectedRevenue:N0} гил\nНажмите, чтобы отметить фактическую продажу.");
            Cell(1,p.Quantity.ToString());Cell(2,p.Cost.ToString("N0"));
            ImGui.TableSetColumnIndex(3);ImGui.TextColored(MarketTheme.Gold,"+"+(p.ExpectedRevenue-p.Cost).ToString("N0"));
            Cell(4,$"{p.SoldQuantity}/{p.Quantity}");Cell(5,"+"+p.RealizedProfit.ToString("N0"));ImGui.PopID();
        }
        ImGui.EndTable();
        if(rows.Length>1000)ImGui.TextDisabled("Показаны последние 1000 покупок. CSV содержит всю историю.");
    }
    private void ChangeSold(TradePurchase old,TradePurchase updated)
    {
        int index=statistics.Purchases.FindIndex(p=>p.Listing==old.Listing);
        if(index<0)return;
        statistics.Purchases[index]=updated;
        try{SaveStatistics();statisticsStatus="Отметка продажи сохранена. Игровые действия не выполнялись.";}
        catch(Exception e){statisticsStatus="Отметка ожидает сохранения: "+e.Message;}
    }
}
