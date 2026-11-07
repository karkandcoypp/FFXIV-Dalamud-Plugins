using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

internal static class HuntTheme {
    internal static readonly Vector4 Accent=new(.37f,.85f,.70f,1);
    internal static readonly Vector4 Brand=new(.57f,.65f,1,1);
    internal static readonly Vector4 Muted=new(.56f,.62f,.73f,1);
    internal static readonly Vector4 Gold=new(.95f,.77f,.46f,1);
    private static readonly (ImGuiCol Slot,Vector4 Color)[] Colors=[
        (ImGuiCol.WindowBg,new(.055f,.065f,.095f,1)),(ImGuiCol.ChildBg,new(.083f,.099f,.14f,1)),
        (ImGuiCol.Text,new(.91f,.94f,.99f,1)),(ImGuiCol.TextDisabled,Muted),
        (ImGuiCol.Border,new(.16f,.19f,.27f,1)),(ImGuiCol.Separator,new(.16f,.19f,.27f,1)),
        (ImGuiCol.Button,new(.14f,.17f,.25f,1)),(ImGuiCol.ButtonHovered,new(.23f,.29f,.42f,1)),(ImGuiCol.ButtonActive,new(.28f,.35f,.53f,1)),
        (ImGuiCol.Header,new(.17f,.22f,.34f,1)),(ImGuiCol.HeaderHovered,new(.22f,.28f,.42f,1)),(ImGuiCol.HeaderActive,new(.25f,.32f,.48f,1)),
        (ImGuiCol.Tab,new(.09f,.11f,.17f,1)),(ImGuiCol.TabActive,new(.21f,.26f,.41f,1)),(ImGuiCol.TabHovered,new(.26f,.32f,.49f,1)),
        (ImGuiCol.FrameBg,new(.105f,.13f,.19f,1)),(ImGuiCol.CheckMark,Accent),(ImGuiCol.PlotHistogram,Brand),
        (ImGuiCol.TitleBg,new(.055f,.065f,.095f,1)),(ImGuiCol.TitleBgActive,new(.095f,.115f,.18f,1)),(ImGuiCol.TitleBgCollapsed,new(.055f,.065f,.095f,1)),
        (ImGuiCol.ScrollbarBg,new(.055f,.065f,.095f,1)),(ImGuiCol.ScrollbarGrab,new(.22f,.27f,.38f,1)),(ImGuiCol.ScrollbarGrabHovered,new(.30f,.37f,.51f,1)),
        (ImGuiCol.TableRowBg,new(.08f,.095f,.135f,1)),(ImGuiCol.TableRowBgAlt,new(.10f,.12f,.17f,1))
    ];
    internal static void Push() {
        ImGui.SetNextWindowBgAlpha(1);
        foreach(var (slot,color) in Colors) ImGui.PushStyleColor(slot,color);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(14,12));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(9,5));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(8,8));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,12);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,7);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,12);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,new Vector2(7,6));
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding,6);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding,8);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize,10);
    }
    internal static void Pop() { ImGui.PopStyleVar(10);ImGui.PopStyleColor(Colors.Length); }
    internal static void Hint(string text) { if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(text); }
}
