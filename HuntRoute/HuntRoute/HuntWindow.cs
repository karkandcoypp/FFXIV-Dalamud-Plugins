using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public sealed class HuntWindow : Window {
    private readonly Plugin plugin;
    private int expansion;
    private string selectedZone = "";
    public HuntWindow(Plugin plugin) : base("Hunt Route###HuntRoute") {
        this.plugin=plugin; Size=new(980,760); SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(780,600), MaximumSize=new(float.MaxValue,float.MaxValue) };
    }
    public override void PreDraw() => HuntTheme.Push();
    public override void PostDraw() => HuntTheme.Pop();
    public override void Draw() {
        float line=ImGui.GetTextLineHeight();
        ImGui.TextColored(HuntTheme.Accent,"Hunt Route");
        ImGui.SameLine(); ImGui.TextDisabled("/  Контракты на охоту");
        if(!plugin.Ready) { ImGui.Spacing(); ImGui.TextWrapped(plugin.Error ?? "Войдите в игру, чтобы увидеть ваши контракты."); return; }
        ImGui.Spacing();
        if(ImGui.BeginTabBar("expansions",ImGuiTabBarFlags.FittingPolicyScroll)) {
            for(int i=0;i<Plugin.Boards.Length;i++) {
                if(ImGui.BeginTabItem(Plugin.Boards[i].Name)) {
                    if(expansion!=i) { expansion=i; selectedZone=""; }
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        var board=Plugin.Boards[expansion];
        var now=DateTimeOffset.UtcNow;
        float cardHeight=line*3+64;
        if(ImGui.BeginTable("reset-cards",2,ImGuiTableFlags.SizingStretchSame)) {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ResetCard(board,false,now,cardHeight);
            ImGui.TableNextColumn(); ResetCard(board,true,now,cardHeight); ImGui.EndTable();
        }
        if(ImGui.Button("К доске охоты",new Vector2(180,0))) {
            plugin.Teleport(board.Aetheryte);
            var mapId=Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(board.Territory).Map.RowId;
            Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(board.Territory,Service.DataManager.GetExcelSheet<Map>().GetRow(mapId),board.Position));
        }
        HuntTheme.Hint("Телепорт в город и отметка доски на карте. Обычная стоимость телепорта в гилах.");
        ImGui.SameLine();
        ImGui.Checkbox("Скрыть выполненные",ref plugin.HideDone);
        ImGui.SameLine();
        if(ImGui.Button("Обновить")) plugin.Refresh();
        ImGui.Spacing();
        var zones=plugin.Routes.Select(z=>new ZoneRoute(z.Name,z.Mobs.Where(m=>board.Daily.Contains(m.BillNumber)||board.Weekly==m.BillNumber).ToList(),z.Start))
            .Where(z=>z.Mobs.Count>0 && (!plugin.HideDone || z.Mobs.Any(m=>!plugin.Done(m)))).ToList();
        if(zones.Count==0) {
            ImGui.BeginChild("empty",Vector2.Zero,true);
            ImGui.Spacing(); ImGui.TextColored(HuntTheme.Accent,"Здесь пока нет целей");
            ImGui.TextWrapped(plugin.HideDone ? "Возьмите контракты на доске охоты или отключите скрытие выполненных." : "Возьмите контракты на доске охоты. Цели появятся автоматически.");
            ImGui.EndChild(); return;
        }
        if(!zones.Any(z=>z.Name==selectedZone)) selectedZone=zones[0].Name;
        float sidebar=Math.Clamp(ImGui.GetContentRegionAvail().X*.25f,185,235);
        ImGui.BeginChild("zone-list",new Vector2(sidebar,0),true);
        ImGui.TextDisabled($"ЗОНЫ  /  {zones.Count}"); ImGui.Spacing();
        foreach(var zone in zones) {
            ImGui.PushID(zone.Name);
            int done=zone.Mobs.Count(plugin.Done);
            var origin=ImGui.GetCursorScreenPos();
            bool selected=selectedZone==zone.Name;
            if(ImGui.Selectable("##zone",selected,ImGuiSelectableFlags.None,new Vector2(0,line*3+12))) selectedZone=zone.Name;
            var draw=ImGui.GetWindowDrawList();
            string title=zone.Name;
            while(title.Length>3 && ImGui.CalcTextSize(title).X>ImGui.GetContentRegionAvail().X-12) title=title.EndsWith("...")?title[..^4]+"...":title[..^2]+"...";
            draw.AddText(origin+new Vector2(8,7),ImGui.GetColorU32(selected?HuntTheme.Accent:new Vector4(.9f,.93f,.96f,1)),title);
            draw.AddText(origin+new Vector2(8,line+15),ImGui.GetColorU32(HuntTheme.Muted),$"{done} / {zone.Mobs.Count} целей выполнено");
            HuntTheme.Hint(zone.Name);
            ImGui.PopID();
        }
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("zone-detail",Vector2.Zero,true);
        DrawZone(zones.First(z=>z.Name==selectedZone));
        ImGui.EndChild();
    }
    private void ResetCard(Board board,bool weekly,DateTimeOffset now,float height) {
        ImGui.BeginChild(weekly?"weekly":"daily",new Vector2(0,height),true);
        ImGui.TextDisabled(weekly?"ЕЖЕНЕДЕЛЬНЫЕ":"ЕЖЕДНЕВНЫЕ");
        var progress=plugin.CollectionProgress(board,weekly);
        bool all=progress.Total>0 && progress.Taken==progress.Total && progress.NewSets==0;
        ImGui.TextColored(progress.Total==0?HuntTheme.Muted:all?HuntTheme.Accent:HuntTheme.Gold,progress.Label);
        HuntTheme.Hint(progress.Total==0?"Сначала откройте охоту этого дополнения."
            : $"Взято контрактов: {progress.Taken} из {progress.Total}. Выполнено: {progress.Completed}."
              + (progress.NewSets>0?$" На доске есть {progress.NewSets} обновлённых наборов вместо прежних взятых.":"")
              + "\nСтатус взятых контрактов и прогресс определяются по данным игры. Таймер показывает расписание следующего сброса.");
        ImGui.TextDisabled("Следующий сброс через "+Countdown(ResetClock.Next(now,weekly)-now));
        ImGui.EndChild();
    }
    private void DrawZone(ZoneRoute zone) {
        int done=zone.Mobs.Count(plugin.Done);
        ImGui.TextColored(HuntTheme.Accent,zone.Name);
        ImGui.TextDisabled($"Выполнено {done} из {zone.Mobs.Count} целей");
        ImGui.ProgressBar((float)done/zone.Mobs.Count,new Vector2(-1,4),"");
        if(zone.Start!=null) {
            ImGui.TextDisabled("Начало: "+zone.Start.Name);
            if(ImGui.Button("Начать маршрут")) plugin.Teleport(zone.Start.Id);
            HuntTheme.Hint("Телепорт к начальному эфириту маршрута. Затем выполняйте цели по порядку.");
        } else ImGui.TextDisabled("В зоне нет открытого эфирита");
        ImGui.SameLine(); ImGui.TextDisabled("(?)");
        HuntTheme.Hint("Маршрут соединяет соседние цели по кратчайшему расстоянию. Рельеф и закрытые проходы могут потребовать обхода. Кнопка «Телепорт» ведёт к ближайшему открытому эфириту выбранной цели.");
        ImGui.Spacing();
        var daily=zone.Mobs.Where(m=>!m.IsEliteMark).ToList();
        if(daily.Any(m=>!plugin.HideDone||!plugin.Done(m))) {
            ImGui.TextDisabled("ЦЕЛИ ПО ПОРЯДКУ");
            if(ImGui.BeginTable("targets",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)) {
                ImGui.TableSetupColumn("#",ImGuiTableColumnFlags.WidthFixed,24);
                ImGui.TableSetupColumn("Цель",ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Счёт",ImGuiTableColumnFlags.WidthFixed,48);
                ImGui.TableSetupColumn("Действия",ImGuiTableColumnFlags.WidthFixed,ImGui.CalcTextSize("Телепорт").X+ImGui.CalcTextSize("Карта").X+ImGui.GetStyle().FramePadding.X*4+ImGui.GetStyle().ItemSpacing.X+4);
                for(int i=0;i<daily.Count;i++) {
                    var m=daily[i]; if(plugin.HideDone&&plugin.Done(m)) continue;
                    ImGui.PushID($"{m.BillNumber}:{m.MobIndex}"); ImGui.TableNextRow(); ImGui.TableNextColumn();
                    ImGui.TextDisabled($"{i+1:00}"); ImGui.TableNextColumn();
                    bool known=Location.Database.TryGetValue(m.MobHuntId,out var position);
                    ImGui.TextWrapped(m.Name);
                    if(known && ImGui.IsItemClicked()) plugin.Travel(m);
                    if(known) HuntTheme.Hint($"X {position!.X:F1}, Y {position.Y:F1}. Нажмите имя или кнопку «Телепорт», чтобы отправиться к цели.");
                    ImGui.TableNextColumn(); ImGui.TextColored(plugin.Done(m)?HuntTheme.Accent:new Vector4(.9f,.93f,.96f,1),$"{plugin.Kills(m)}/{m.NeededKills}");
                    ImGui.TableNextColumn(); ImGui.BeginDisabled(!known);
                    if(ImGui.Button("Телепорт")) plugin.Travel(m);
                    HuntTheme.Hint("Телепорт к ближайшему открытому эфириту и отметка цели на карте.");
                    ImGui.SameLine(); if(ImGui.Button("Карта")) Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
                    ImGui.EndDisabled(); if(!known) HuntTheme.Hint("Координаты этой цели пока неизвестны.");
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
        foreach(var m in zone.Mobs.Where(m=>m.IsEliteMark&&(!plugin.HideDone||!plugin.Done(m)))) {
            ImGui.PushID($"elite-{m.BillNumber}:{m.MobIndex}"); ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
            ImGui.TextColored(HuntTheme.Gold,"НЕДЕЛЬНАЯ ЦЕЛЬ  /  B-РАНГ");
            ImGui.TextWrapped(m.Name+"    "+plugin.Kills(m)+"/"+m.NeededKills);
            if(ImGui.Button("Показать места появления")) plugin.Elite.Show(m);
            HuntTheme.Hint("Предполагаемые точки на карте. Обнаруженная клиентом цель также отмечается в игровом мире.");
            ImGui.PopID();
        }
    }
    private static string Countdown(TimeSpan t) => t.TotalDays>=1?$"{(int)t.TotalDays} д {t.Hours} ч":$"{(int)t.TotalHours} ч {t.Minutes:00} мин";
}
