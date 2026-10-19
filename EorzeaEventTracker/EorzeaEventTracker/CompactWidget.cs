using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EorzeaEventTracker;

public sealed class CompactWidget : Window
{
    private readonly Plugin plugin;
    private static readonly Vector4 Teal = new(.49f, .69f, .62f, 1);
    private static readonly Vector4 Gold = new(.84f, .72f, .48f, 1);
    private Localizer L => plugin.Locale;
    private float UiScale => AppearancePolicy.Scale(plugin.Preferences);
    private float Opacity => AppearancePolicy.Opacity(plugin.Preferences);
    private float S => ImGui.GetFontSize() / 17f;

    public CompactWidget(Plugin plugin) : base("Eorzea / Events###EETCompact", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.plugin = plugin;
        RespectCloseHotkey = false;
        Size = new Vector2(320, 290);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(260, 160), MaximumSize = new(520, 900) };
    }

    internal void SwitchCharacter(ulong contentId)
    {
        WindowName = "Eorzea / Events###EETCompact-" + contentId;
        IsOpen = contentId != 0 && plugin.Preferences.ShowCompactWidget;
    }

    public override void PreDraw()
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(260, 160) * UiScale, MaximumSize = new Vector2(520, 900) * UiScale };
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.075f, .072f, .065f, .96f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.10f, .095f, .080f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.19f, .17f, .12f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.39f, .35f, .26f, .40f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.21f, .195f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.30f, .27f, .21f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.35f, .29f, .19f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.28f, .25f, .18f, 1));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.35f, .31f, .23f, .45f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.17f, .16f, .14f, 1));
    }

    public override void PostDraw() => ImGui.PopStyleColor(10);

    public override bool DrawConditions() => Plugin.Client.IsLoggedIn && plugin.AllowWidgetDisplay;

    public override void OnClose()
    {
        if (!plugin.Preferences.ShowCompactWidget) return;
        plugin.Preferences.ShowCompactWidget = false;
        plugin.Save();
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(UiScale);
        try { DrawContent(); }
        finally { ImGui.SetWindowFontScale(1); }
    }

    private void DrawContent()
    {
        ImGui.TextColored(Gold, L.Text("Active events"));
        ImGui.SameLine();
        if (ImGui.SmallButton(L.Text("Journal") + "###Journal")) plugin.OpenEvent("");
        ImGui.TextDisabled(plugin.Progress.Character);
        ImGui.Separator();
        var now = DateTimeOffset.UtcNow;
        var events = plugin.Feed.Feed.Events
            .Where(e => !plugin.IsEventHidden(e) && e.Active(now) && (plugin.Preferences.ShowUnverified || e.Verified))
            .OrderBy(e => e.EndsAt).ToList();
        if (events.Count == 0) ImGui.TextWrapped(L.Text("No active events right now."));
        foreach (var e in events)
        {
            ImGui.PushID(e.Id);
            var title = L.Text(e.Title, e);
            var cursor = ImGui.GetCursorPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = ImGui.CalcTextSize(title, false, width - 8 * S).Y + 8 * S;
            if (ImGui.Selectable("###OpenEvent", false, ImGuiSelectableFlags.None, new Vector2(0, height))) plugin.OpenEvent(e.Id);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip(L.Text("Click an event to open its journal."));
            }
            ImGui.SetCursorPos(cursor + new Vector2(4 * S, 4 * S));
            ImGui.PushTextWrapPos(cursor.X + width - 4 * S);
            ImGui.TextColored(new Vector4(.92f, .90f, .84f, 1), title);
            ImGui.PopTextWrapPos();
            ImGui.SetCursorPos(cursor + new Vector2(0, height + 4 * S));
            var remaining = e.EndsAt!.Value - now;
            ImGui.TextColored(remaining <= TimeSpan.FromDays(1) ? Gold : Teal,
                L.Format("Ends in {0}d {1}h {2}m", (int)remaining.TotalDays, remaining.Hours, remaining.Minutes));
            if (!plugin.Progress.ReadyForReminders) ImGui.TextWrapped(L.Text("Waiting for character data"));
            else if (plugin.Progress.Catalog.TryGetValue(e.Id, out var definition))
            {
                var (count, total) = RewardPreferences.Progress(definition.Rewards, plugin.Preferences.IgnoredRewards, plugin.Progress.Rewards);
                ImGui.TextDisabled(L.Format("{0} / {1} rewards confirmed", count, total));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
                ImGui.ProgressBar(total == 0 ? 0 : (float)count / total,
                    new Vector2(-1, 5 * S), "");
                ImGui.PopStyleColor();
            }
            else ImGui.TextWrapped(L.Text("Tracking is not available for this event yet."));
            ImGui.Separator();
            ImGui.PopID();
        }
    }
}
