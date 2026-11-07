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
    public HuntWindow(Plugin plugin) : base("Hunt Route###HuntRoute") {
        this.plugin=plugin; Size=new(930,740); SizeCondition=ImGuiCond.FirstUseEver;
    }
    private int expansion = 2;
    private readonly Vector4 accent = new(.35f,.85f,.78f,1);
    public override void Draw() {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.12f,.22f,.25f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.17f,.35f,.38f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.22f,.45f,.46f,1));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.11f,.16f,.20f,1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.16f,.32f,.34f,1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.18f,.4f,.42f,1));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, accent);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10,8));
        try {
            ImGui.TextColored(accent,"HUNT ROUTE  /  ОХОТНИЧЬИ КОНТРАКТЫ");
            var now = DateTimeOffset.UtcNow;
            ImGui.TextDisabled($"Ежедневный сброс через {Countdown(ResetClock.Next(now,false)-now)}  ·  Еженедельный через {Countdown(ResetClock.Next(now,true)-now)}");
            if (!plugin.Ready) { ImGui.TextWrapped(plugin.Error ?? "Войдите в игру — данные появятся автоматически."); return; }
            if (ImGui.Button("Обновить")) plugin.Refresh();
            ImGui.SameLine(); ImGui.Checkbox("Скрыть выполненные",ref plugin.HideDone);
            ImGui.Separator();
            if(ImGui.BeginTabBar("expansions")) {
                for (int i=0;i<Plugin.Boards.Length;i++) {
                    if(ImGui.BeginTabItem(Plugin.Boards[i].Name)) { expansion=i; ImGui.EndTabItem(); }
                }
                ImGui.EndTabBar();
            }
            var board = Plugin.Boards[expansion];
            ImGui.Spacing();
            ImGui.TextColored(accent, "ДОСКА ОХОТЫ  /  " + board.Name);
            ImGui.TextWrapped("Ежедневные: " + plugin.Status(board,false));
            ImGui.TextWrapped("Еженедельный: " + plugin.Status(board,true));
            ImGui.TextDisabled("Сброс: по расписанию UTC; факт получения: по наблюдению в игре.");
            if (ImGui.Button("Телепорт к доске")) {
                plugin.Teleport(board.Aetheryte);
                var mapId = Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(board.Territory).Map.RowId;
                Service.GameGui.OpenMapWithMapLink(new MapLinkPayload(board.Territory,mapId,(int)(board.Position.X*1000),(int)(board.Position.Y*1000)));
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Телепорт в город доски, затем отметка её расположения на карте. Расходует гилы по правилам игры.");
            ImGui.Separator();
            ImGui.TextDisabled("Нажмите цель для телепорта к ближайшему открытому эфириту. «Карта» ставит отметку.");
            ImGui.TextDisabled("Маршрут по прямым расстояниям; рельеф и проходы могут потребовать обхода.");
            bool found=false;
            foreach (var zone in plugin.Routes.Where(z=>z.Mobs.Any(m=>board.Daily.Contains(m.BillNumber)||board.Weekly==m.BillNumber))) {
                var mobs=zone.Mobs.Where(m=>board.Daily.Contains(m.BillNumber)||board.Weekly==m.BillNumber).ToList();
                int done=mobs.Count(plugin.Done);
                if (plugin.HideDone && done==mobs.Count) continue;
                found=true;
                ImGui.PushID(zone.Name);
                ImGui.Spacing(); ImGui.TextColored(accent,zone.Name);
                ImGui.ProgressBar((float)done/mobs.Count,new Vector2(-1,5),"");
                ImGui.TextDisabled(zone.Start==null ? "Нет открытого эфирита в зоне" : "Начало маршрута: " + zone.Start.Name);
                if (zone.Start!=null && ImGui.Button("К началу маршрута")) plugin.Teleport(zone.Start.Id);
                int step=0;
                foreach(var m in mobs) {
                    step++;
                    if(plugin.HideDone && plugin.Done(m)) continue;
                    ImGui.PushID($"{m.BillNumber}:{m.MobIndex}");
                    bool known=!m.IsEliteMark && Location.Database.ContainsKey(m.MobHuntId);
                    ImGui.BeginDisabled(!known);
                    if(ImGui.Selectable($"{step:00}  {m.Name}    {plugin.Kills(m)}/{m.NeededKills}",false,ImGuiSelectableFlags.None,new Vector2(Math.Max(250,ImGui.GetContentRegionAvail().X-90),30))) plugin.Travel(m);
                    ImGui.SameLine();
                    if(ImGui.Button("Карта")) Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
                    ImGui.EndDisabled();
                    if(m.IsEliteMark && ImGui.Button("Места появления / B-rank")) plugin.Elite.Show(m);
                    else if(!known && !m.IsEliteMark) ImGui.TextDisabled("Для этой цели нет координат");
                    ImGui.PopID();
                }
                ImGui.PopID();
            }
            if(!found) ImGui.TextWrapped("Нет незавершённых контрактов. Проверьте доску или покажите выполненные.");
        } finally { ImGui.PopStyleVar(2); ImGui.PopStyleColor(7); }
    }
    private static string Countdown(TimeSpan t) => t.TotalDays>=1 ? $"{(int)t.TotalDays} д {t.Hours} ч" : $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
}
