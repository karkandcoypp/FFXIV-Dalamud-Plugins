using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BagSense;

public static class BagTheme
{
    public static readonly Vector4 Background = new(.042f, .055f, .079f, 1);
    public static readonly Vector4 Panel = new(.065f, .084f, .115f, 1);
    public static readonly Vector4 Accent = new(.36f, .79f, .92f, 1);
    public static readonly Vector4 Muted = new(.59f, .66f, .75f, 1);
    public static void Push(float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 8) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * scale);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.89f, .93f, .98f, 1));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.12f, .18f, .25f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.17f, .29f, .39f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.18f, .36f, .46f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.10f, .25f, .34f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.12f, .29f, .39f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.14f, .33f, .44f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.10f, .14f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.13f, .21f, .29f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.14f, .25f, .34f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.16f, .21f, .28f, 1));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.14f, .19f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.075f, .11f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.12f, .26f, .35f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.14f, .31f, .41f, 1));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Accent);
    }
    public static void Pop() { ImGui.PopStyleColor(19); ImGui.PopStyleVar(5); }
}
