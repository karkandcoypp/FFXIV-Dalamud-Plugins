using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EorzeaEventTracker;

public sealed class CompactWidget : Window
{
    private readonly Plugin plugin;
    private static readonly Vector4 Teal = JournalTheme.Green;
    private static readonly Vector4 Gold = JournalTheme.Gold;
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
        ImGui.PushStyleColor(ImGuiCol.WindowBg, JournalTheme.WithOpacity(JournalTheme.Background, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, JournalTheme.WithOpacity(JournalTheme.Panel, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, JournalTheme.WithOpacity(JournalTheme.Active, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, JournalTheme.FrameEdge);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, JournalTheme.Shadow);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8 * UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
        ImGui.PushStyleColor(ImGuiCol.Button, JournalTheme.Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.Separator, JournalTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, JournalTheme.Button);
        ImGui.PushStyleColor(ImGuiCol.Text, JournalTheme.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, JournalTheme.Muted);
    }

    public override void PostDraw() { ImGui.PopStyleVar(2); ImGui.PopStyleColor(13); }

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
            if (EventDiscoveryPolicy.IsNew(plugin.Preferences, e)) title += "  · " + L.Text("NEW");
            var cursor = ImGui.GetCursorPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = ImGui.CalcTextSize(title, false, width - 8 * S).Y + 8 * S;
            if (ImGui.Selectable("###OpenEvent", false, ImGuiSelectableFlags.None, new Vector2(0, height))) plugin.OpenEvent(e.Id);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                JournalChrome.Tooltip(UiScale, L.Text("Event journal"), L.Text("Click an event to open its journal."));
            }
            ImGui.SetCursorPos(cursor + new Vector2(4 * S, 4 * S));
            ImGui.PushTextWrapPos(cursor.X + width - 4 * S);
            ImGui.TextColored(new Vector4(.92f, .90f, .84f, 1), title);
            ImGui.PopTextWrapPos();
            ImGui.SetCursorPos(cursor + new Vector2(0, height + 4 * S));
            var deadline = DeadlinePolicy.Status(e, now);
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(JournalTheme.DeadlineColor(deadline), DeadlinePolicy.Countdown(e, now, L));
            ImGui.PopTextWrapPos();
            if (ImGui.IsItemHovered()) JournalChrome.Tooltip(UiScale, L.Text("Event deadline"), L.Text(DeadlinePolicy.Label(deadline)));
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
