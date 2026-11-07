using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

internal static class HuntTheme {
    internal static readonly Vector4 Accent = new(.39f,.83f,.73f,1);
    internal static readonly Vector4 Muted = new(.60f,.66f,.73f,1);
    internal static readonly Vector4 Gold = new(.93f,.75f,.42f,1);
    internal static void Push() {
        ImGui.SetNextWindowBgAlpha(1);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.065f,.078f,.10f,1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,new Vector4(.09f,.108f,.135f,1));
        ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(.90f,.93f,.96f,1));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,Muted);
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.18f,.22f,.27f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.15f,.22f,.27f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.21f,.34f,.38f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,new Vector4(.24f,.43f,.43f,1));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.15f,.28f,.29f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,new Vector4(.17f,.30f,.32f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,new Vector4(.20f,.37f,.37f,1));
        ImGui.PushStyleColor(ImGuiCol.Tab,new Vector4(.10f,.14f,.18f,1));
        ImGui.PushStyleColor(ImGuiCol.TabActive,new Vector4(.18f,.31f,.32f,1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered,new Vector4(.21f,.36f,.37f,1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,new Vector4(.13f,.17f,.21f,1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,Accent);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram,Accent);
        ImGui.PushStyleColor(ImGuiCol.Separator,new Vector4(.18f,.22f,.27f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,16));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(12,7));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(10,10));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,10);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,6);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,10);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding,new Vector2(9,7));
    }
    internal static void Pop() { ImGui.PopStyleVar(7); ImGui.PopStyleColor(18); }
    internal static void Hint(string text) { if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(text); }
}
