using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EorzeaEventTracker;

internal static class JournalChrome
{
    public static void Tooltip(float uiScale, string title, string body, string? footer = null, Vector4? accent = null)
    {
        var scale = ImGui.GetFontSize() / 17f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1f);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(.075f, .082f, .09f, .99f));
        ImGui.PushStyleColor(ImGuiCol.Border, JournalTheme.FrameLight);
        ImGui.SetWindowFontScale(1);
        ImGui.BeginTooltip();
        ImGui.SetWindowFontScale(uiScale);
        var width = Math.Max(100 * scale, Math.Min(350 * scale, ImGui.GetMainViewport().WorkSize.X - 40 * scale));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.SetWindowFontScale(uiScale * 1.12f);
        ImGui.TextColored(accent ?? JournalTheme.Gold, title);
        ImGui.SetWindowFontScale(uiScale);
        ImGui.Separator();
        ImGui.TextColored(JournalTheme.Text, body);
        if (!string.IsNullOrWhiteSpace(footer))
        {
            ImGui.Spacing();
            ImGui.TextColored(JournalTheme.Muted, footer);
        }
        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1);
        ImGui.EndTooltip();
        ImGui.SetWindowFontScale(uiScale);
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

}
