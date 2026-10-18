using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace EorzeaEventTracker;

public sealed class CompactWidget : Window
{
    private readonly Plugin plugin;
    private static readonly Vector4 Teal = new(.40f, .88f, .77f, 1);
    private static readonly Vector4 Gold = new(1, .76f, .43f, 1);
    private Localizer L => plugin.Locale;
    private float S => ImGui.GetFontSize() / 17f;

    public CompactWidget(Plugin plugin) : base("Eorzea / Events###EETCompact", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.plugin = plugin;
        RespectCloseHotkey = false;
        Size = new Vector2(320, 290);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(260, 160), MaximumSize = new(520, 900) };
    }

    public override bool DrawConditions() => Plugin.Client.IsLoggedIn && plugin.AllowWidgetDisplay;

    public override void OnClose()
    {
        if (!plugin.Settings.ShowCompactWidget) return;
        plugin.Settings.ShowCompactWidget = false;
        plugin.Save();
    }

    public override void Draw()
    {
        ImGui.TextColored(Teal, L.Text("Active events"));
        ImGui.SameLine();
        if (ImGui.SmallButton(L.Text("Journal") + "###Journal")) plugin.OpenEvent("");
        ImGui.TextDisabled(plugin.Progress.Character);
        ImGui.Separator();
        var now = DateTimeOffset.UtcNow;
        var events = plugin.Feed.Feed.Events
            .Where(e => e.Active(now) && (plugin.Settings.ShowUnverified || e.Verified))
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
            ImGui.TextColored(new Vector4(.92f, .96f, 1, 1), title);
            ImGui.PopTextWrapPos();
            ImGui.SetCursorPos(cursor + new Vector2(0, height + 4 * S));
            var remaining = e.EndsAt!.Value - now;
            ImGui.TextColored(remaining <= TimeSpan.FromDays(1) ? Gold : Teal,
                L.Format("Ends in {0}d {1}h {2}m", (int)remaining.TotalDays, remaining.Hours, remaining.Minutes));
            if (!plugin.Progress.ReadyForReminders) ImGui.TextWrapped(L.Text("Waiting for character data"));
            else if (plugin.Progress.Catalog.TryGetValue(e.Id, out var definition))
            {
                var count = definition.Rewards.Count(r => plugin.Progress.Rewards.TryGetValue(r.Id, out var progress) && progress.Confirmed);
                ImGui.TextDisabled(L.Format("{0} / {1} rewards confirmed", count, definition.Rewards.Count));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
                ImGui.ProgressBar(definition.Rewards.Count == 0 ? 0 : (float)count / definition.Rewards.Count,
                    new Vector2(-1, 5 * S), "");
                ImGui.PopStyleColor();
            }
            else ImGui.TextWrapped(L.Text("Tracking is not available for this event yet."));
            ImGui.Separator();
            ImGui.PopID();
        }
    }
}
