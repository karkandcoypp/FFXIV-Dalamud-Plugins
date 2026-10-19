using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EorzeaCompass;
public static class CompassTheme
{
    public static void Push(float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7 * scale);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.085f, .095f, .11f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.16f, .21f, .24f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.20f, .29f, .32f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.15f, .23f, .25f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.19f, .28f, .31f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.13f, .15f, .18f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.18f, .21f, .25f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.20f, .25f, .28f, 1));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.45f, .85f, .75f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.19f, .28f, .31f, 1));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.22f, .25f, .29f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.91f, .93f, .95f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(.45f, .85f, .75f, 1));
    }
    public static void Pop() { ImGui.PopStyleColor(13); ImGui.PopStyleVar(3); }
}
