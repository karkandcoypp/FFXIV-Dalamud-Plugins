using System.Numerics;
using Dalamud.Bindings.ImGui;
using World = Lumina.Excel.Sheets.World;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private int requestedTab = -1;
    private int selectedPage;
    private string itemFilter = "";
    // Standalone visual checks cannot use Dalamud's hosted native text-input extension.
    private bool staticPreview = false;
    private bool Number(string label,ref int value)
    {
        if(!staticPreview)return ImGui.InputInt(label,ref value);
        ImGui.BeginDisabled();ImGui.Button(value+"###preview"+label,new(ImGui.CalcItemWidth(),0));ImGui.EndDisabled();
        if(!label.StartsWith("##")){ImGui.SameLine();ImGui.TextUnformatted(label);}
        return false;
    }
    private static float UiScale => ImGui.GetFontSize()/16f;
    private void OpenSettings() { Open(); requestedTab=3; }
    private static void Help(string text) { if(ImGui.IsItemHovered()) ImGui.SetTooltip(text); }
    private static bool BeginFields(string id)
    {
        if(!ImGui.BeginTable(id,2,ImGuiTableFlags.SizingStretchProp))return false;
        ImGui.TableSetupColumn("Параметр",ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Значение",ImGuiTableColumnFlags.WidthFixed,145*UiScale);
        return true;
    }
    private bool NumberField(string label,ref int value,string hint="")
    {
        ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);ImGui.AlignTextToFramePadding();ImGui.TextUnformatted(label);Help(hint);
        ImGui.TableSetColumnIndex(1);ImGui.SetNextItemWidth(-1);
        bool changed=Number("##"+label,ref value);Help(hint);return changed;
    }
    private bool StackedNumber(string label,ref int value)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,4)*UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,5)*UiScale);
        ImGui.TextDisabled(label);ImGui.SetNextItemWidth(-1);
        bool changed=Number("##"+label,ref value);ImGui.PopStyleVar(2);return changed;
    }
    private void DrawWindow(string worldName,bool resultsOpen)
    {
        ImGui.SetNextWindowSizeConstraints(new Vector2(800,480)*UiScale,new Vector2(float.MaxValue));
        if(!sized){ImGui.SetNextWindowSize(new Vector2(980,650)*UiScale,ImGuiCond.Always);sized=true;}
        MarketTheme.Push(UiScale);
        try
        {
            if(ImGui.Begin("VendorProfit###VendorProfit",ref open))
            {
                if(requestedTab>=0){selectedPage=requestedTab;requestedTab=-1;}
                ImGui.BeginChild("navigation",new Vector2(154*UiScale,0),false);
                LargeText("VendorProfit",MarketTheme.Accent,1.12f);
                ImGui.TextDisabled("РЫНОК  /  NPC");
                ImGui.Dummy(new Vector2(0,18*UiScale));
                string[] pages=["Лоты","Уведомления","Автопокупка","Настройки"];
                for(int i=0;i<pages.Length;i++)
                {
                    bool active=selectedPage==i;
                    ImGui.PushStyleColor(ImGuiCol.Button,active?new Vector4(.11f,.25f,.26f,1):new Vector4(0,0,0,0));
                    ImGui.PushStyleColor(ImGuiCol.Text,active?MarketTheme.Accent:MarketTheme.Muted);
                    if(ImGui.Button(pages[i],new Vector2(-1,40*UiScale)))selectedPage=i;
                    ImGui.PopStyleColor(2);
                }
                if(config.AutoBuyEnabled)
                {
                    ImGui.Spacing();ImGui.TextColored(MarketTheme.Gold,config.AutoBuyDryRun?"Пробные покупки":"Покупки включены");
                    if(ImGui.Button("Стоп покупки",new Vector2(-1,0))){config.AutoBuyEnabled=false;autoBuyer.Stop("Остановлено вручную.");Pi.SavePluginConfig(config);}
                }
                ImGui.SetCursorPosY(Math.Max(ImGui.GetCursorPosY(),ImGui.GetWindowHeight()-92*UiScale));
                ImGui.Separator();SectionLabel("ТЕКУЩИЙ СЕРВЕР");
                ImGui.TextUnformatted(worldName);ImGui.TextDisabled("Налог учтён");
                ImGui.EndChild();ImGui.SameLine();
                ImGui.BeginChild("workspace",Vector2.Zero,true);
                string[] subtitles=["Выгодные покупки для продажи NPC","Новые возможности — даже с закрытым окном","Покупки под контролем бюджета","Окно, подсветка и параметры поиска"];
                LargeText(pages[selectedPage],MarketTheme.Text,1.4f);
                ImGui.TextWrapped(subtitles[selectedPage]);ImGui.Spacing();
                switch(selectedPage)
                {
                    case 0:DrawOpportunities(worldName,resultsOpen);break;
                    case 1:DrawBackgroundAlerts();break;
                    case 2:DrawAutoBuy();break;
                    case 3:DrawGeneralSettings();break;
                }
                ImGui.EndChild();
            }
            ImGui.End();
        }
        finally{MarketTheme.Pop();}
    }
    private static void LargeText(string text,Vector4 color,float multiplier=1.4f)
    {
        float fit=Math.Min(multiplier,ImGui.GetContentRegionAvail().X/Math.Max(1,ImGui.CalcTextSize(text).X));
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(),ImGui.GetFontSize()*fit,ImGui.GetCursorScreenPos(),ImGui.ColorConvertFloat4ToU32(color),text);
        ImGui.Dummy(new Vector2(1,ImGui.GetFontSize()*fit));
    }
    private static void SectionLabel(string text){ImGui.Spacing();ImGui.TextColored(MarketTheme.Muted,text);}
    private static void Metric(string id,string label,string value,Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg,MarketTheme.Raised);
        ImGui.BeginChild(id,new Vector2(0,76*UiScale),true,ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.TextDisabled(label);LargeText(value,color,1.35f);
        ImGui.EndChild();ImGui.PopStyleColor();
    }
    private string SearchStatus()
    {
        if(scan is { IsCompleted:false })return "Идёт поиск…";
        if(error!=null)return "Ошибка поиска";
        if(scannerStoppedManually)return "Сканер остановлен";
        if(config.UseExternalScanner)
        {
            if(externalStatus.StartsWith("Error",StringComparison.OrdinalIgnoreCase) || externalStatus.StartsWith("Ошибка"))return "Ошибка фонового сканера";
            if(externalStatus.StartsWith("Stopped"))return "Сканер остановлен";
            if(externalStatus.StartsWith("Ready"))return "Фоновый поиск работает";
            if(externalStatus.Contains("запуска",StringComparison.OrdinalIgnoreCase))return "Запускаем сканер…";
        }
        return result==null ? "Ожидание первого результата" : "Поиск завершён";
    }
    private void DrawOpportunities(string worldName,bool resultsOpen)
    {
        ImGui.TextColored(error==null ? MarketTheme.Accent : new Vector4(1,.55f,.45f,1),SearchStatus());
        Help(error ?? externalStatus);
        ImGui.SameLine();ImGui.TextDisabled(result==null ? "" : $" · {result.Finished.LocalDateTime:t}");
        ImGui.BeginDisabled(world==0 || scan!=null);
        if(ImGui.Button("Обновить"))StartScan(chaos:config.BackgroundChaos);
        ImGui.EndDisabled();
        ImGui.SameLine();
        bool chaos=config.BackgroundChaos;
        if(ImGui.Checkbox("Весь Chaos",ref chaos))
        {config.BackgroundChaos=chaos;nextExternalExport=DateTimeOffset.MinValue;Pi.SavePluginConfig(config);}
        Help("Выключено — текущий сервер. Включено — все серверы Chaos.");
        if(scan!=null){ImGui.SameLine();if(ImGui.Button("Отменить"))scanCancellation?.Cancel();}
        ImGui.Spacing();
        ImGui.SetNextItemWidth(Math.Max(170*UiScale,ImGui.GetContentRegionAvail().X-255*UiScale));
        if(staticPreview){ImGui.BeginDisabled();ImGui.Button("Предмет или сервер…##find",new(ImGui.CalcItemWidth(),0));ImGui.EndDisabled();}
        else ImGui.InputTextWithHint("##find","Предмет или сервер…",ref itemFilter,128);
        ImGui.SameLine();ImGui.SetNextItemWidth(120*UiScale);
        if(Number("Прибыль от",ref config.MinimumProfit))
        {config.MinimumProfit=Math.Clamp(config.MinimumProfit,1,100_000_000);Pi.SavePluginConfig(config);}
        Help("Минимальная прибыль за целый стак. Фильтр таблицы; лимиты уведомлений и покупок задаются отдельно.");
        if(result==null)
        {
            ImGui.Spacing();ImGui.TextWrapped("Результатов пока нет. Нажмите «Обновить» или включите фоновый поиск на вкладке «Уведомления».");
        }
        else DrawSuggestions(result,worldName);
        if(resultsOpen && ImGui.CollapsingHeader("Открытый предмет в игре"))DrawLive();
    }
    private void DrawSuggestions(ScanResult snapshot,string currentWorld)
    {
        string Server(Suggestion s)=>!string.IsNullOrEmpty(s.WorldName)?s.WorldName:(s.World==world||s.World==0?currentWorld:Data.GetExcelSheet<World>().GetRowOrDefault(s.World)?.Name.ToString()??s.World.ToString());
        var suggestions=snapshot.Suggestions.Where(s=>s.Quote.Profit>=config.MinimumProfit && Policy.Fresh(s.Updated,DateTimeOffset.UtcNow,config.MaximumAgeMinutes))
            .Where(s=>string.IsNullOrWhiteSpace(itemFilter)||s.Item.Name.Contains(itemFilter,StringComparison.OrdinalIgnoreCase)||Server(s).Contains(itemFilter,StringComparison.OrdinalIgnoreCase)).ToArray();
        ImGui.Spacing();
        if(suggestions.Length==0)
        {ImGui.TextUnformatted("Подходящих лотов пока нет.");ImGui.TextWrapped("Можно снизить порог прибыли или дождаться следующего обновления.");return;}
        if(ImGui.BeginTable("summary",3,ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();Metric("count","ЛОТОВ",suggestions.Length.ToString(),MarketTheme.Text);
            ImGui.TableNextColumn();Metric("cash","ВЛОЖЕНИЯ",suggestions.Sum(s=>s.Quote.Cost).ToString("N0"),MarketTheme.Text);
            ImGui.TableNextColumn();Metric("profit","ПРИБЫЛЬ NPC","+"+suggestions.Sum(s=>s.Quote.Profit).ToString("N0"),MarketTheme.Gold);
            ImGui.EndTable();
        }
        ImGui.TextDisabled("Кэш рынка · наличие проверьте в игре");
        Help("Суммы в гилях, за целые стаки, с налогом. Нажмите предмет, чтобы скопировать название; наведите для подробностей.");
        float height=Math.Max(70*UiScale,ImGui.GetContentRegionAvail().Y-(snapshot.Servers==null?8:36)*UiScale);
        if(!ImGui.BeginTable("opportunities",5,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.ScrollY|ImGuiTableFlags.Resizable,new Vector2(0,height)))return;
        ImGui.TableSetupColumn("Предмет",ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Сервер",ImGuiTableColumnFlags.WidthFixed,95*UiScale);
        ImGui.TableSetupColumn("Шт.",ImGuiTableColumnFlags.WidthFixed,40*UiScale);
        ImGui.TableSetupColumn("Покупка",ImGuiTableColumnFlags.WidthFixed,95*UiScale);
        ImGui.TableSetupColumn("Прибыль",ImGuiTableColumnFlags.WidthFixed,95*UiScale);
        ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
        int index=0;
        foreach(var s in suggestions.Take(200))
        {
            ImGui.PushID(index++);ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
            if(ImGui.Selectable(s.Item.Name+(s.Quote.Lot.Hq?" HQ":"")))ImGui.SetClipboardText(s.Item.Name);
            Help($"{s.Item.Name}\nРетейнер: {s.Quote.Lot.Retainer}\nЦена: {s.Quote.Lot.Price:N0} × {s.Quote.Lot.Quantity}\nНалог: {s.Quote.Lot.Tax:N0}{(s.EstimatedTax?" (оценка)":"")}\nПродажа NPC: {s.Quote.Revenue:N0}\nКэш: {(DateTimeOffset.UtcNow-s.Updated).TotalMinutes:F0} мин. Наличие требует проверки.");
            Cell(1,Server(s));Cell(2,s.Quote.Lot.Quantity.ToString());Cell(3,s.Quote.Cost.ToString("N0"));
            ImGui.TableSetColumnIndex(4);ImGui.TextColored(MarketTheme.Gold,"+"+s.Quote.Profit.ToString("N0"));
            ImGui.PopID();
        }
        ImGui.EndTable();
        if(suggestions.Length>200)ImGui.TextDisabled("Показаны 200 самых прибыльных лотов.");
        if(snapshot.Servers is {Length:>0} servers && ImGui.CollapsingHeader("Сводка по серверам"))
            foreach(var server in servers)
            {var lots=suggestions.Where(s=>s.World==server.World).ToArray();ImGui.TextUnformatted($"{server.Name}: {lots.Length} лотов, прибыль +{lots.Sum(s=>s.Quote.Profit):N0} гил");}
    }
    private void DrawLive()
    {
        var qualified=live.Where(q=>q.Profit>=config.MinimumProfit).OrderByDescending(q=>q.Profit).ToArray();
        if(qualified.Length==0){ImGui.TextWrapped($"В видимых строках нет лотов с прибылью от {config.MinimumProfit:N0} гил.");return;}
        if(!ImGui.BeginTable("live",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH))return;
        foreach(string label in new[]{"Ретейнер","Шт.","Покупка","Прибыль"})ImGui.TableSetupColumn(label);
        ImGui.TableHeadersRow();
        foreach(var q in qualified){ImGui.TableNextRow();Cell(0,q.Lot.Retainer);Cell(1,q.Lot.Quantity.ToString());Cell(2,q.Cost.ToString("N0"));Cell(3,"+"+q.Profit.ToString("N0"));}
        ImGui.EndTable();
    }
    private static void Cell(int column,string text){ImGui.TableSetColumnIndex(column);ImGui.TextUnformatted(text);}
    private void DrawGeneralSettings()
    {
        ImGui.TextUnformatted("Окно и подсветка");
        bool changed=ImGui.Checkbox("Открывать окно вместе с аукционом",ref config.AutoOpen);
        changed|=ImGui.Checkbox("Подсвечивать выгодные лоты в игре",ref config.Highlight);
        if(BeginFields("general"))
        {changed|=NumberField("Допустимый возраст кэша, мин",ref config.MaximumAgeMinutes);ImGui.EndTable();}
        if(changed){config.MaximumAgeMinutes=Math.Clamp(config.MaximumAgeMinutes,5,1440);Pi.SavePluginConfig(config);}
        ImGui.Spacing();
        if(ImGui.CollapsingHeader("Дополнительно"))
        {
            bool sourceChanged=false;DrawExternalControls(ref sourceChanged);
            ImGui.BeginDisabled(config.UseExternalScanner);
            sourceChanged|=ImGui.Checkbox("Встроенный поиск при открытии",ref config.ScanOnOpen);ImGui.EndDisabled();
            if(sourceChanged)Pi.SavePluginConfig(config);
            ImGui.BeginDisabled(scan!=null||world==0);
            if(ImGui.Button("Проверить полный каталог"))StartScan(true);
            ImGui.EndDisabled();Help("Только текущий сервер. Может занять несколько минут.");
        }
        if(ImGui.CollapsingHeader("Диагностика"))
        {
            ImGui.TextWrapped("Сканер: "+externalStatus);ImGui.TextWrapped("Проход: "+progress);
            ImGui.TextWrapped(diagnostics);ImGui.TextWrapped(liveStatus);
            if(result!=null)ImGui.TextWrapped($"Проверено: {result.Checked}; неизвестных: {result.Unknown}; ошибок: {result.Failed}. {result.Status}");
            if(error!=null)ImGui.TextWrapped(error);
        }
    }
}
