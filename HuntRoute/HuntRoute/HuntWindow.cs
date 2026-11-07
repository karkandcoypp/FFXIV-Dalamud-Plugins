using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
namespace HuntRoute;

public sealed class HuntWindow : Window {
    private readonly Plugin plugin;
    private int expansion;
    private string selectedZone="";
    public HuntWindow(Plugin plugin) : base("Hunt Route###HuntRoute") {
        this.plugin=plugin; Size=new(800,620); SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(520,430),MaximumSize=new(float.MaxValue,float.MaxValue) };
    }
    public override void PreDraw() => HuntTheme.Push();
    public override void PostDraw() => HuntTheme.Pop();
    public override void Draw() {
        float line=ImGui.GetTextLineHeight();
        ImGui.TextColored(HuntTheme.Accent,"Hunt Route"); ImGui.SameLine(); ImGui.TextDisabled("/  Охота по контрактам");
        if(!plugin.Ready) { ImGui.TextWrapped(plugin.Error ?? (plugin.Session.Phase!=RoutePhase.Idle?"Загрузка зоны. Сопровождение продолжится автоматически.":"Войдите в игру, чтобы увидеть контракты.")); return; }
        if(ImGui.BeginTabBar("expansions",ImGuiTabBarFlags.FittingPolicyScroll)) {
            for(int i=0;i<Plugin.Boards.Length;i++) if(ImGui.BeginTabItem(Plugin.Boards[i].Name)) {
                if(expansion!=i) { expansion=i; selectedZone=""; } ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        var board=Plugin.Boards[expansion]; var now=DateTimeOffset.UtcNow;
        if(ImGui.BeginTable("reset-cards",2,ImGuiTableFlags.SizingStretchSame)) {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); ResetCard(board,false,now,line*3+32);
            ImGui.TableNextColumn(); ResetCard(board,true,now,line*3+32); ImGui.EndTable();
        }
        if(ImGui.Button("К доске охоты") && plugin.Teleport(board.Aetheryte)) {
            plugin.Session.Stop();
            var map=Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(board.Territory).Map.Value;
            Service.GameGui.OpenMapWithMapLink(MapCoordinates.CreateLink(board.Territory,map,board.Position));
        }
        HuntTheme.Hint("Телепорт в город и отметка доски на карте. Если вы уже в этом городе, откроется только карта.");
        ImGui.SameLine(); ImGui.Checkbox("Скрыть готовые",ref plugin.HideDone);
        ImGui.SameLine(); if(ImGui.Button("Обновить")) plugin.Refresh();
        DrawSession();
        var zones=plugin.Routes.Select(z=>new ZoneRoute(z.Name,z.Mobs.Where(m=>board.Daily.Contains(m.BillNumber)||board.Weekly==m.BillNumber).ToList(),z.Start,z.StartsHere))
            .Where(z=>z.Mobs.Count>0 && (!plugin.HideDone||z.Mobs.Any(m=>!plugin.Done(m)))).ToList();
        if(zones.Count==0) {
            ImGui.BeginChild("empty",Vector2.Zero,true);
            bool dailyDone=plugin.CollectionProgress(board,false).AllCompleted;
            ImGui.TextColored(HuntTheme.Accent,dailyDone?"Все ежедневные контракты на сегодня выполнены":"Нет незавершённых целей");
            ImGui.TextWrapped(dailyDone?"Можно вернуться после ежедневного сброса. Недельный контракт отмечается отдельно.":"Возьмите контракты на доске или отключите скрытие выполненных."); ImGui.EndChild(); return;
        }
        if(!zones.Any(z=>z.Name==selectedZone)) selectedZone=zones[0].Name;
        bool narrow=ImGui.GetContentRegionAvail().X<740;
        if(narrow) {
            ImGui.SetNextItemWidth(-1);
            if(ImGui.BeginCombo("##zone-choice",selectedZone)) {
                foreach(var zone in zones) if(ImGui.Selectable($"{zone.Name}  ({zone.Mobs.Count(plugin.Done)}/{zone.Mobs.Count})",zone.Name==selectedZone)) selectedZone=zone.Name;
                ImGui.EndCombo();
            }
        } else {
            ImGui.BeginChild("zone-list",new Vector2(Math.Clamp(ImGui.GetContentRegionAvail().X*.23f,170,215),0),true);
            ImGui.TextDisabled($"ЗОНЫ / {zones.Count}");
            foreach(var zone in zones) {
                ImGui.PushID(zone.Name); var top=ImGui.GetCursorScreenPos();
                bool selected=selectedZone==zone.Name;
                if(ImGui.Selectable("##zone",selected,ImGuiSelectableFlags.None,new Vector2(0,line*2+14))) selectedZone=zone.Name;
                var draw=ImGui.GetWindowDrawList();
                draw.AddText(top+new Vector2(6,4),ImGui.GetColorU32(selected?HuntTheme.Accent:new Vector4(.9f,.93f,.96f,1)),Fit(zone.Name,ImGui.GetContentRegionAvail().X-12));
                draw.AddText(top+new Vector2(6,line+9),ImGui.GetColorU32(HuntTheme.Muted),$"{zone.Mobs.Count(plugin.Done)}/{zone.Mobs.Count} целей выполнено");
                HuntTheme.Hint(zone.Name); ImGui.PopID();
            }
            ImGui.EndChild(); ImGui.SameLine();
        }
        ImGui.BeginChild("zone-detail",Vector2.Zero,true); DrawZone(zones.First(z=>z.Name==selectedZone)); ImGui.EndChild();
    }
    private void DrawSession() {
        var session=plugin.Session;
        if(session.Phase==RoutePhase.Idle) return;
        ImGui.PushStyleColor(ImGuiCol.ChildBg,new Vector4(.10f,.18f,.19f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(8,6)); ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(6,4));
        ImGui.BeginChild("route-follow",new Vector2(0,ImGui.GetTextLineHeight()*2+30),true);
        string text=session.Phase switch {
            RoutePhase.WaitingForZone=>"В пути: "+session.Target?.TerritoryName,
            RoutePhase.Following=>$"Цель: {session.Target?.Name}  {plugin.Kills(session.Target!)}/{session.Target?.NeededKills}",
            RoutePhase.NextZone=>"Цели маршрута в зоне выполнены",
            RoutePhase.Finished=>"Маршрут по известным точкам завершён",
            _=>"Контракт изменился. Начните маршрут заново."
        };
        float stopWidth=ImGui.CalcTextSize("Стоп").X+ImGui.GetStyle().FramePadding.X*2;
        ImGui.TextColored(HuntTheme.Accent,Fit(text,ImGui.GetContentRegionAvail().X-stopWidth-12));
        HuntTheme.Hint(text);
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X-stopWidth);
        if(ImGui.SmallButton("Стоп")) session.Stop();
        if(session.Phase==RoutePhase.NextZone && session.Suggested is {} next) {
            if(ImGui.Button("Телепорт: "+Fit(next.TerritoryName ?? "Следующая зона",Math.Max(120,ImGui.GetContentRegionAvail().X-150)))) plugin.Travel(next);
            HuntTheme.Hint("Перейти к следующей зоне. Телепорт запускается только по вашему нажатию; после прибытия метки переключаются автоматически.");
        } else if(session.Phase==RoutePhase.Following) ImGui.TextDisabled("Метка сменится после выполнения цели.");
        else if(session.Phase==RoutePhase.WaitingForZone) ImGui.TextDisabled("Метка появится после прибытия в зону.");
        else if(session.Phase==RoutePhase.Finished) ImGui.TextDisabled("Цели без координат и B-ранги ищутся отдельно.");
        ImGui.EndChild(); ImGui.PopStyleVar(2); ImGui.PopStyleColor();
    }
    private void ResetCard(Board board,bool weekly,DateTimeOffset now,float height) {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(8,6)); ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(6,4));
        ImGui.BeginChild(weekly?"weekly":"daily",new Vector2(0,height),true);
        ImGui.TextDisabled(weekly?"Еженедельные":"Ежедневные");
        var progress=plugin.CollectionProgress(board,weekly);
        bool all=progress.Total>0 && progress.Taken==progress.Total && progress.NewSets==0;
        if(progress.AllCompleted) {
            var at=ImGui.GetCursorScreenPos();float h=ImGui.GetTextLineHeight();var draw=ImGui.GetWindowDrawList();uint color=ImGui.GetColorU32(HuntTheme.Accent);
            draw.AddLine(at+new Vector2(1,h*.5f),at+new Vector2(5,h*.75f),color,2);
            draw.AddLine(at+new Vector2(5,h*.75f),at+new Vector2(13,h*.2f),color,2);
            ImGui.Dummy(new Vector2(15,h));ImGui.SameLine();
        }
        string label=progress.AllCompleted?(weekly?"Выполнено за неделю":"Выполнено сегодня")+$" · {progress.Completed}/{progress.Total}":progress.Label;
        ImGui.TextColored(progress.Total==0?HuntTheme.Muted:all||progress.AllCompleted?HuntTheme.Accent:HuntTheme.Gold,Fit(label,ImGui.GetContentRegionAvail().X));
        HuntTheme.Hint(progress.Total==0?"Сначала откройте охоту этого дополнения.":$"Взято {progress.Taken}/{progress.Total}, выполнено {progress.Completed}. Обновлённых наборов на доске: {progress.NewSets}. Статус берётся из игры; таймер показывает расписание сброса.");
        ImGui.TextDisabled("Сброс через "+Countdown(ResetClock.Next(now,weekly)-now));
        ImGui.EndChild(); ImGui.PopStyleVar(2);
    }
    private void DrawZone(ZoneRoute zone) {
        int done=zone.Mobs.Count(plugin.Done);
        ImGui.TextColored(HuntTheme.Accent,zone.Name); ImGui.SameLine(); ImGui.TextDisabled($"{done}/{zone.Mobs.Count}");
        ImGui.ProgressBar((float)done/zone.Mobs.Count,new Vector2(-1,3),"");
        var first=zone.Mobs.FirstOrDefault(m=>!m.IsEliteMark && !plugin.Done(m) && Location.Database.ContainsKey(m.MobHuntId));
        if(first!=null) {
            if(ImGui.Button(zone.StartsHere?"Следующая цель":"Начать маршрут")) plugin.Travel(first);
            HuntTheme.Hint(zone.StartsHere?"Вы уже в зоне. Начать сопровождение и поставить метку без телепорта.":"Телепорт к первой цели и автоматическая смена меток после выполнения каждой цели.");
            ImGui.SameLine(); ImGui.TextDisabled(zone.StartsHere?"Вы уже в зоне":Fit(zone.Start?.Name ?? "Нет открытого эфирита",Math.Max(80,ImGui.GetContentRegionAvail().X-20)));
        }
        var daily=zone.Mobs.Where(m=>!m.IsEliteMark).ToList();
        if(daily.Any(m=>!plugin.HideDone||!plugin.Done(m)) && ImGui.BeginTable("targets",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)) {
            bool here=zone.StartsHere;
            string action=here?"К цели":"Телепорт";
            ImGui.TableSetupColumn("#",ImGuiTableColumnFlags.WidthFixed,20);
            ImGui.TableSetupColumn("Цель",ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Счёт",ImGuiTableColumnFlags.WidthFixed,ImGui.CalcTextSize("99/99").X);
            ImGui.TableSetupColumn("Действия",ImGuiTableColumnFlags.WidthFixed,ImGui.CalcTextSize(action+"Карта").X+ImGui.GetStyle().FramePadding.X*4+ImGui.GetStyle().ItemSpacing.X+4);
            int step=0;
            foreach(var m in daily) {
                if(plugin.HideDone&&plugin.Done(m)) continue;
                step++; ImGui.PushID($"{m.BillNumber}:{m.MobIndex}"); ImGui.TableNextRow();
                if(plugin.Session.Phase==RoutePhase.Following && plugin.Session.Target is {} tracked && tracked.BillNumber==m.BillNumber && tracked.MobIndex==m.MobIndex)
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,ImGui.GetColorU32(new Vector4(.12f,.29f,.27f,1)));
                ImGui.TableNextColumn(); ImGui.TextDisabled($"{step:00}"); ImGui.TableNextColumn();
                bool known=Location.Database.ContainsKey(m.MobHuntId);
                ImGui.TextWrapped(m.Name);
                if(known && ImGui.IsItemClicked()) plugin.Travel(m);
                MobPreview.OnHover(m,plugin.Kills(m));
                ImGui.TableNextColumn(); ImGui.TextColored(plugin.Done(m)?HuntTheme.Accent:new Vector4(.9f,.93f,.96f,1),$"{plugin.Kills(m)}/{m.NeededKills}");
                ImGui.TableNextColumn(); ImGui.BeginDisabled(!known);
                if(ImGui.Button(action)) plugin.Travel(m);
                HuntTheme.Hint(here?"Вы уже в зоне: только метка и сопровождение.":"Телепорт к ближайшему открытому эфириту цели и сопровождение маршрута.");
                ImGui.SameLine(); if(ImGui.Button("Карта")) Location.CreateMapMarker(m.TerritoryType,m.MapId,m.MobHuntId,m.Name);
                ImGui.EndDisabled(); if(!known) HuntTheme.Hint("Координаты этой цели пока неизвестны."); ImGui.PopID();
            }
            ImGui.EndTable();
        }
        foreach(var m in zone.Mobs.Where(m=>m.IsEliteMark&&(!plugin.HideDone||!plugin.Done(m)))) {
            ImGui.PushID($"elite-{m.BillNumber}:{m.MobIndex}"); ImGui.Separator();
            ImGui.TextColored(HuntTheme.Gold,"B-ранг · "+m.Name); MobPreview.OnHover(m,plugin.Kills(m));
            if(ImGui.Button("Места появления")) plugin.Elite.Show(m);
            HuntTheme.Hint("Предполагаемые точки на карте и метки обнаруженной клиентом цели в мире. Недельная охота не входит в автоматический дневной маршрут."); ImGui.PopID();
        }
    }
    private static string Fit(string text,float width) {
        if(ImGui.CalcTextSize(text).X<=width) return text;
        while(text.Length>1 && ImGui.CalcTextSize(text+"...").X>width) text=text[..^1];
        return text+"...";
    }
    private static string Countdown(TimeSpan t) => t.TotalDays>=1?$"{(int)t.TotalDays} д {t.Hours} ч":$"{(int)t.TotalHours} ч {t.Minutes:00} мин";
}
