using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace HuntRoute;

public sealed class NextZoneWindow : Window {
    private readonly Plugin plugin;
    private readonly NextZoneNotice notice=new();
    public NextZoneWindow(Plugin plugin) : base("Продолжить охоту?###HuntRouteNextZone",ImGuiWindowFlags.NoCollapse) {
        this.plugin=plugin;
        Size=new(430,210); SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints { MinimumSize=new(340,190),MaximumSize=new(620,400) };
    }
    internal void Sync() {
        if(plugin.Session.Phase!=RoutePhase.NextZone || plugin.Session.Suggested==null) { IsOpen=false; return; }
        if(notice.ShouldOpen(plugin.Session)) IsOpen=true;
    }
    public override void PreDraw() {
        HuntTheme.Push();
        var viewport=ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos+viewport.WorkSize*.5f,ImGuiCond.Appearing,new Vector2(.5f,.5f));
    }
    public override void PostDraw() => HuntTheme.Pop();
    public override void Draw() {
        if(!plugin.Ready || plugin.Session.Phase!=RoutePhase.NextZone || plugin.Session.Suggested is not {} next) { IsOpen=false; return; }
        ImGui.TextColored(HuntTheme.Accent,"Цели маршрута в зоне выполнены");
        ImGui.TextWrapped(plugin.Session.Target?.TerritoryName ?? "Текущая зона");
        ImGui.Separator();
        ImGui.TextDisabled("Следующая зона"); ImGui.SameLine(); ImGui.TextWrapped(next.TerritoryName);
        ImGui.TextWrapped("Первая цель: "+next.Name);
        if(ImGui.Button("Телепортироваться")) {
            plugin.Travel(next);
            // A rejected request keeps the offer available for retry.
            if(plugin.Session.Phase!=RoutePhase.NextZone) IsOpen=false;
        }
        HuntTheme.Hint("К ближайшему открытому эфириту первой цели. После прибытия сопровождение продолжится автоматически. Обычная стоимость телепорта в гилах.");
        ImGui.SameLine(); if(ImGui.Button("Позже")) IsOpen=false;
        HuntTheme.Hint("Отложить переход. Кнопка телепорта останется в основном окне.");
    }
}
