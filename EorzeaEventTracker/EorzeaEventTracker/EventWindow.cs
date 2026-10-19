using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace EorzeaEventTracker;

public sealed class EventWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly ItemWiki wiki = new();
    private readonly CancellationTokenSource wikiCancellation = new();
    private Task<WikiResult>? wikiRequest;
    private string wikiItem = "";
    private string wikiMessage = "";
    private DateTime wikiMessageUntil;
    private bool settingsVisible;
    private string requestedTab = "";
    private DateTime nextStepRefresh;
    private NextStep? nextStep;
    private string nextStepEvent = "";
    private string urlBuffer;
    private string selected = "";
    private bool remainingOnly { get => plugin.Preferences.RemainingOnly; set => plugin.Preferences.RemainingOnly = value; }
    private bool favoritesOnly { get => plugin.Preferences.FavoritesOnly; set => plugin.Preferences.FavoritesOnly = value; }
    private bool showHidden;
    private string rewardSearch = "";
    private string rewardSearchEvent = "";
    private string viewingEventKey = "";
    private DateTime viewStarted;
    private DateTime lastViewFrame;
    private DateTime mapErrorUntil;
    private EventInfo? localizedEvent;
    private static readonly Vector4 Teal = JournalTheme.Green;
    private static readonly Vector4 QuestGreen = JournalTheme.Green;
    private static readonly Vector4 QuestAmber = JournalTheme.Amber;
    private static readonly Vector4 QuestRed = JournalTheme.Red;
    private static readonly Vector4 Gold = JournalTheme.Gold;
    private static readonly Vector4 Muted = JournalTheme.Muted;
    private Localizer L => plugin.Locale;
    private string T(string text) => L.Text(text);
    private string E(string text) => L.Text(text, localizedEvent);
    private string F(string text, params object[] values) => L.Format(text, values);
    private float UiScale => AppearancePolicy.Scale(plugin.Preferences);
    private float Opacity => AppearancePolicy.Opacity(plugin.Preferences);
    private float S => ImGui.GetFontSize() / 17f;
    internal void SwitchCharacter(ulong contentId)
    {
        WindowName = "Eorzea Event Tracker###EETMain-" + contentId;
        selected = "";
        nextStep = null;
        requestedTab = "";
        rewardSearch = "";
        rewardSearchEvent = "";
        viewingEventKey = "";
        showHidden = false;
        settingsVisible = false;
        mapErrorUntil = default;
        wikiRequest = null;
        wikiMessage = "";
        urlBuffer = plugin.Settings.FeedUrl;
        IsOpen = false;
    }
    internal void SelectEvent(string id) { selected = id; showHidden = false; }

    public EventWindow(Plugin plugin) : base("Eorzea Event Tracker###EETMain")
    {
        this.plugin = plugin;
        Size = new Vector2(1040, 790);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(800, 540), MaximumSize = new(1600, 1400) };
        urlBuffer = plugin.Settings.FeedUrl;
    }

    public override void PreDraw()
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(800, 540) * UiScale, MaximumSize = new Vector2(1600, 1400) * UiScale };
        ImGui.PushStyleColor(ImGuiCol.WindowBg, JournalTheme.WithOpacity(JournalTheme.Background, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, JournalTheme.WithOpacity(JournalTheme.Panel, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, JournalTheme.WithOpacity(JournalTheme.Active, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, JournalTheme.WithOpacity(JournalTheme.Panel, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, JournalTheme.FrameEdge);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, JournalTheme.Shadow);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8 * UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
    }

    public override void PostDraw() { ImGui.PopStyleVar(2); ImGui.PopStyleColor(6); }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, JournalTheme.WithOpacity(JournalTheme.Background, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, JournalTheme.FrameEdge);
        ImGui.PushStyleColor(ImGuiCol.Tab, JournalTheme.Panel);
        ImGui.PushStyleColor(ImGuiCol.TabActive, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, JournalTheme.WithOpacity(JournalTheme.Panel, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Button, JournalTheme.Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.Header, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, JournalTheme.Button);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, JournalTheme.Hover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, JournalTheme.Active);
        ImGui.PushStyleColor(ImGuiCol.Separator, JournalTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, JournalTheme.Panel);
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, JournalTheme.Button);
        ImGui.PushStyleColor(ImGuiCol.Text, JournalTheme.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        try { DrawContent(); }
        finally { ImGui.PopStyleColor(21); ImGui.PopStyleVar(4); ImGui.SetWindowFontScale(1); }
    }

    private bool BeginScaledChild(string id, Vector2 size, bool border, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        // Child windows inherit their parent's font scale. Normalize it so nested reward
        // panels get the same requested scale rather than multiplying or losing it.
        ImGui.SetWindowFontScale(1);
        var visible = ImGui.BeginChild(id, size, border, flags);
        ImGui.SetWindowFontScale(UiScale);
        if (visible && border)
        {
            var position = ImGui.GetWindowPos();
            var childBounds = ImGui.GetWindowSize();
            ImGui.GetWindowDrawList().AddRect(position + Vector2.One, position + childBounds - Vector2.One,
                ImGui.ColorConvertFloat4ToU32(JournalTheme.FrameLight), 5 * S);
        }
        return visible;
    }

    private void EndScaledChild()
    {
        ImGui.EndChild();
        ImGui.SetWindowFontScale(UiScale);
    }

    private void DrawContent()
    {
        var now = DateTimeOffset.UtcNow;
        var events = plugin.Feed.Feed.Events.Where(e => (plugin.Preferences.ShowUnverified || e.Verified) && (e.Active(now) || e.Upcoming(now)))
            .OrderBy(e => e.Upcoming(now)).ThenBy(e => e.EndsAt).ToList();
        var hiddenCount = events.Count(plugin.IsEventHidden);
        events = events.Where(e => plugin.IsEventHidden(e) == showHidden).ToList();
        if (!events.Any(e => e.Id == selected)) selected = events.FirstOrDefault()?.Id ?? "";
        ImGui.TextColored(Gold, T("E O R Z E A   /   E V E N T S"));
        ImGui.SameLine();
        ImGui.TextDisabled(T("Your seasonal adventure journal"));
        ImGui.TextColored(Muted, T(plugin.Progress.Character));
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Refresh") + "###Refresh"))
        {
            plugin.Images.RetryFailed();
            _ = plugin.Feed.RefreshAsync(plugin.Settings.FeedUrl);
        }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Settings") + "###Settings")) settingsVisible = !settingsVisible;
        if (settingsVisible) DrawSettings();
        ImGui.Separator();
        if (BeginScaledChild("Event rail", new Vector2(Math.Clamp(ImGui.GetContentRegionAvail().X * .27f, 224 * S, 290 * S), -30 * S), true))
        {
            ImGui.TextColored(Muted, showHidden ? F("Hidden events / {0}", events.Count) : F("LIVE & UPCOMING  /  {0}", events.Count));
            if (ImGui.SmallButton((showHidden ? T("Back to events") : F("Hidden ({0})", hiddenCount)) + "###HiddenEvents"))
                showHidden = !showHidden;
            if (showHidden) ImGui.TextWrapped(T("Hidden from the journal, widget and reminders. Restore any event below."));
            foreach (var e in events) DrawEventCard(e, now);
            if (events.Count == 0) ImGui.TextWrapped(T("No events to display. Check Hidden to restore an event."));
        }
        EndScaledChild();
        ImGui.SameLine();
        if (BeginScaledChild("Event detail", new Vector2(0, -30 * S), true))
        {
            var current = events.FirstOrDefault(e => e.Id == selected);
            if (current != null) DrawEvent(current, now);
            else if (ImGui.Button(T("Open Lodestone") + "###Open Lodestone")) OpenUrl("https://na.finalfantasyxiv.com/lodestone/topics/");
        }
        EndScaledChild();
        ImGui.TextColored(Muted, "Lodestone  /  " + L.FeedStatus(plugin.Feed.Status, plugin.Feed.UpdatedAt));
        if (ImGui.IsItemHovered()) HelpTooltip(T("Character progress"), T("Tracking stays on this computer. Collection unlocks and observed rewards are separate from quest completion."));
    }

    private void DrawEventCard(EventInfo e, DateTimeOffset now)
    {
        ImGui.PushID(e.Id);
        var active = selected == e.Id;
        var accent = active ? Gold : Muted;
        var width = ImGui.GetContentRegionAvail().X;
        var title = L.Text(e.Title, e);
        var padding = 12 * S;
        var titleHeight = ImGui.CalcTextSize(title, false, width - padding * 2).Y;
        var image = EventImagePath(e);
        var artwork = image == null ? null : Plugin.Textures.GetFromFile(image);
        var imageHeight = artwork == null ? 0 : 66 * S;
        var height = imageHeight + titleHeight + ImGui.GetTextLineHeightWithSpacing() * 3 + 76 * S;
        var start = ImGui.GetCursorScreenPos();
        var cursor = ImGui.GetCursorPos();
        if (ImGui.InvisibleButton("Select event", new Vector2(width, height - 32 * S))) selected = e.Id;
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var draw = ImGui.GetWindowDrawList();
        var background = active ? new Vector4(.19f, .19f, .175f, 1)
            : hovered ? JournalTheme.Button : JournalTheme.Card;
        background.W = Opacity;
        draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(background), 8 * S);
        draw.AddRect(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(active ? accent : hovered ? new Vector4(Gold.X, Gold.Y, Gold.Z, .60f) : JournalTheme.FrameEdge), 8 * S);
        draw.AddRectFilled(start + new Vector2(0, 10 * S), start + new Vector2(3 * S, height - 10 * S), ImGui.ColorConvertFloat4ToU32(accent), 2 * S);
        if (artwork != null && artwork.TryGetWrap(out var wrap, out _))
        {
            var size = new Vector2(width - padding * 2, imageHeight);
            var ratio = (float)wrap.Width / wrap.Height;
            var displayRatio = size.X / size.Y;
            var visible = ratio > displayRatio ? new Vector2(displayRatio / ratio, 1) : new Vector2(1, ratio / displayRatio);
            draw.AddImage(wrap.Handle, start + new Vector2(padding, 9 * S), start + new Vector2(padding, 9 * S) + size,
                (Vector2.One - visible) / 2, (Vector2.One + visible) / 2);
        }
        ImGui.SetCursorPos(cursor + new Vector2(padding, imageHeight + 16 * S));
        ImGui.PushTextWrapPos(cursor.X + width - padding);
        ImGui.TextColored(active ? new Vector4(.96f, .93f, .85f, 1) : new Vector4(.84f, .82f, .76f, 1), title);
        ImGui.PopTextWrapPos();
        ImGui.SetCursorPosX(cursor.X + padding);
        if (EventDiscoveryPolicy.IsNew(plugin.Preferences, e))
        {
            ImGui.TextColored(Gold, T("NEW"));
            if (ImGui.IsItemHovered()) HelpTooltip(T("New event"), T("Not yet viewed on this character. The mark clears after viewing event details."));
            ImGui.SameLine();
        }
        ImGui.TextColored(accent, T(e.Active(now) ? "LIVE NOW" : "UPCOMING"));
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.PushTextWrapPos(cursor.X + width - padding);
        ImGui.TextColored(JournalTheme.DeadlineColor(DeadlinePolicy.Status(e, now)), Countdown(e, now));
        ImGui.PopTextWrapPos();
        if (plugin.Progress.Catalog.TryGetValue(e.Id, out var def))
        {
            var (done, total) = RewardPreferences.Progress(def.Rewards, plugin.Preferences.IgnoredRewards, plugin.Progress.Rewards);
            ImGui.SetCursorPosX(cursor.X + padding);
            ImGui.TextColored(Muted, F("{0} / {1} rewards confirmed", done, total));
            var bar = start + new Vector2(padding, height - 44 * S);
            var barWidth = width - padding * 2;
            draw.AddRectFilled(bar, bar + new Vector2(barWidth, 3 * S), ImGui.ColorConvertFloat4ToU32(new Vector4(.22f, .21f, .18f, 1)), 2 * S);
            if (total > 0 && done > 0)
                draw.AddRectFilled(bar, bar + new Vector2(barWidth * done / total, 3 * S), ImGui.ColorConvertFloat4ToU32(Teal), 2 * S);
        }
        ImGui.SetCursorPos(cursor + new Vector2(padding, height - 30 * S));
        if (ImGui.SmallButton((showHidden ? T("Restore event") : T("Hide event")) + "###HideEvent"))
            plugin.SetEventHidden(e, !showHidden);
        if (ImGui.IsItemHovered()) HelpTooltip(T("Hidden events"), T("You can restore this event from Hidden. Progress is kept."));
        ImGui.SetCursorPos(cursor + new Vector2(0, height + 10 * S));
        ImGui.PopID();
    }

    private string? EventImagePath(EventInfo e)
    {
        var file = plugin.Images.GetPath(e.ImageUrl);
        var asset = plugin.Progress.BundledAsset(e.Id);
        return file ?? (string.IsNullOrEmpty(asset) ? null : Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "assets", asset));
    }

    private void DrawEvent(EventInfo e, DateTimeOffset now)
    {
        localizedEvent = e;
        try { DrawEventContent(e, now); }
        finally { localizedEvent = null; }
    }

    private void DrawEventContent(EventInfo e, DateTimeOffset now)
    {
        plugin.Progress.Catalog.TryGetValue(e.Id, out var definition);
        DrawHero(e, now);
        TrackViewedEvent(e);
        if (plugin.Feed.Feed.GeneratedAt > DateTimeOffset.MinValue)
        {
            ImGui.TextDisabled(F("Feed updated: {0}", plugin.Feed.Feed.GeneratedAt.ToLocalTime().ToString("g", L.Culture)));
            if (ImGui.IsItemHovered()) HelpTooltip(T("Event data"), T("Publication time of the event feed, not the last download or a change to this individual event."));
        }
        DrawNextStep(e, definition, now);
        if (e.EndsAt.HasValue) ImGui.TextDisabled(F("Ends {0}", e.EndsAt.Value.ToLocalTime().ToString("f", L.Culture)));
        if (!e.Verified) ImGui.TextColored(Gold, T("Partial details — check the official page"));
        if (plugin.Progress.ReadyForReminders && EventCompletionPolicy.IsComplete(definition, plugin.Progress.Quests))
            ImGui.TextColored(Teal, T("Main objective completed"));
        if (!string.IsNullOrWhiteSpace(e.Description)) ImGui.TextWrapped(E(e.Description));
        if (ImGui.Button(T("Official event guide") + "###Official event guide")) OpenUrl(e.Url);
        if (wikiRequest != null) ImGui.TextWrapped(F("Checking wiki: {0}", wikiItem));
        else if (DateTime.UtcNow < wikiMessageUntil) ImGui.TextWrapped(T(wikiMessage));
        if (DateTime.UtcNow < mapErrorUntil) ImGui.TextWrapped(T("Could not open the map. Log in and try again; this area may be unavailable."));
        ImGui.Spacing();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * S);
        var tabsVisible = ImGui.BeginTabBar("Event tabs", ImGuiTabBarFlags.FittingPolicyScroll);
        ImGui.PopStyleVar();
        if (tabsVisible)
        {
            if (BeginJournalTab("Your progress", "progress", 0))
            {
                if (definition == null) ImGui.TextWrapped(T("Automatic tracking has not been mapped for this event yet."));
                else DrawProgress(definition);
                ImGui.EndTabItem();
            }
            if (BeginJournalTab("How to join", "join", 1))
            {
                Section("REQUIREMENTS", e.Requirements);
                Section("YOUR NEXT STEPS", e.Steps);
                if (e.Locations.Count > 0)
                {
                    SectionHeading(T("EVENT NPCs"));
                    foreach (var location in e.Locations)
                    {
                        ImGui.PushID(location.Id);
                        LocationSummary(location);
                        DrawLocationButton(location.Id);
                        ImGui.PopID();
                    }
                }
                Section("SOURCE NOTES", e.Notes);
                ImGui.EndTabItem();
            }
            if (BeginJournalTab("Reward guide", "rewards", 2))
            {
                Section("REWARDS & PRICES", e.Rewards);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private bool BeginJournalTab(string title, string id, int icon)
    {
        var spaces = Math.Max(1, (int)Math.Ceiling(24 * S / Math.Max(1, ImGui.CalcTextSize(" ").X)));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * S);
        var active = ImGui.BeginTabItem(new string(' ', spaces) + T(title) + "###" + title, SelectRequestedTab(id));
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var scale = S;
        var center = new Vector2(min.X + 18 * scale, (min.Y + max.Y) * .5f);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(active ? Gold : Muted);
        draw.PushClipRect(min, max, true);
        if (icon == 0)
        {
            draw.AddRect(center + new Vector2(-7, -8) * scale, center + new Vector2(7, 8) * scale, color, 2 * scale);
            draw.AddLine(center + new Vector2(-3, -8) * scale, center + new Vector2(-3, 8) * scale, color);
            draw.AddLine(center + new Vector2(0, -3) * scale, center + new Vector2(4, -3) * scale, color);
            draw.AddLine(center + new Vector2(0, 2) * scale, center + new Vector2(4, 2) * scale, color);
        }
        else if (icon == 1)
        {
            draw.AddCircle(center, 8 * scale, color, 20);
            draw.AddLine(center + new Vector2(-3, 4) * scale, center + new Vector2(3, -4) * scale, color, 2 * scale);
            draw.AddLine(center + new Vector2(-3, 4) * scale, center + new Vector2(-2, -1) * scale, color);
            draw.AddLine(center + new Vector2(3, -4) * scale, center + new Vector2(2, 1) * scale, color);
        }
        else
        {
            draw.AddRect(center + new Vector2(-7, -4) * scale, center + new Vector2(7, 7) * scale, color, scale);
            draw.AddRect(center + new Vector2(-8, -7) * scale, center + new Vector2(8, -3) * scale, color, scale);
            draw.AddLine(center + new Vector2(0, -7) * scale, center + new Vector2(0, 7) * scale, color, 2 * scale);
            draw.AddCircle(center + new Vector2(-3, -9) * scale, 3 * scale, color, 12);
            draw.AddCircle(center + new Vector2(3, -9) * scale, 3 * scale, color, 12);
        }
        if (active) draw.AddLine(new Vector2(min.X + 8 * scale, max.Y - 2 * scale), new Vector2(max.X - 8 * scale, max.Y - 2 * scale),
            ImGui.ColorConvertFloat4ToU32(Gold), 2 * scale);
        draw.PopClipRect();
        return active;
    }

    private void HelpTooltip(string title, string body, string? footer = null, Vector4? accent = null) =>
        JournalChrome.Tooltip(UiScale, title, body, footer, accent);

    private void QuestHeading(uint id, string name, Vector4 color, float factor = 1.16f, bool blocked = false)
    {
        ImGui.BeginGroup();
        var icon = GameUiAssets.QuestIcon(id, blocked);
        if (icon != 0 && Plugin.Textures.GetFromGameIcon(new GameIconLookup(icon)).TryGetWrap(out var wrap, out _))
        {
            ImGui.Image(wrap.Handle, new Vector2(28 * S));
            if (ImGui.IsItemHovered()) HelpTooltip(name, T("Check quest progress below for completion and access requirements."));
            ImGui.SameLine();
        }
        Emphasis(name, color, factor);
        ImGui.EndGroup();
    }

    private void QuestKeyValue(string label, uint id, string name, float factor = 1.24f)
    {
        ImGui.TextColored(Muted, label);
        QuestHeading(id, name, JournalTheme.QuestTitle, factor);
    }

    private void Emphasis(string text, Vector4 color, float factor = 1.14f)
    {
        var wrap = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 6 * S;
        ImGui.SetWindowFontScale(UiScale * factor);
        ImGui.PushTextWrapPos(wrap);
        try { ImGui.TextColored(color, text); }
        finally { ImGui.PopTextWrapPos(); ImGui.SetWindowFontScale(UiScale); }
    }

    private void KeyValue(string label, string value, Vector4 color, float factor = 1.18f)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(Muted, label);
        ImGui.PopTextWrapPos();
        Emphasis(value, color, factor);
    }

    private void SectionHeading(string title)
    {
        ImGui.TextColored(Gold, title);
        var right = ImGui.GetWindowPos().X + ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        var end = ImGui.GetItemRectMax();
        var left = end.X + 12 * S;
        if (right - left > 20 * S)
            ImGui.GetWindowDrawList().AddLine(new Vector2(left, (ImGui.GetItemRectMin().Y + end.Y) * .5f),
                new Vector2(right, (ImGui.GetItemRectMin().Y + end.Y) * .5f), ImGui.ColorConvertFloat4ToU32(JournalTheme.Border));
    }

    private void StatusBadge(string text, Vector4 color, string? title = null, string? detail = null)
    {
        text = text.Trim('[', ']', ' ');
        var start = ImGui.GetCursorScreenPos();
        var padding = new Vector2(8, 3) * S;
        var size = ImGui.CalcTextSize(text) + padding * 2;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + size, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, .13f * Opacity)), 4 * S);
        draw.AddRect(start, start + size, ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, .35f)), 4 * S);
        draw.AddText(start + padding, ImGui.ColorConvertFloat4ToU32(color), text);
        ImGui.Dummy(size);
        if (ImGui.IsItemHovered() && title != null) HelpTooltip(title, detail ?? text, accent: color);
    }

    private void LocationSummary(EventLocation location)
    {
        Emphasis(E(location.Npc), JournalTheme.NpcTitle, 1.08f);
        ImGui.PushTextWrapPos(0);
        ImGui.TextColored(Muted, $"{E(location.Zone)}  ·  X:{location.X:F1} Y:{location.Y:F1}");
        ImGui.PopTextWrapPos();
    }

    private ImGuiTabItemFlags SelectRequestedTab(string tab)
    {
        if (requestedTab != tab) return ImGuiTabItemFlags.None;
        requestedTab = "";
        return ImGuiTabItemFlags.SetSelected;
    }

    private void DrawNextStep(EventInfo e, TrackingDefinition? definition, DateTimeOffset now)
    {
        if (nextStep == null || nextStepEvent != e.Id || DateTime.UtcNow >= nextStepRefresh)
        {
            nextStep = NextStepPolicy.Decide(e, definition, now, plugin.Progress.ReadyForReminders,
                plugin.Preferences, plugin.Progress.Quests, plugin.Progress.QuestAccess, plugin.Progress.Rewards,
                plugin.Progress.CurrencyBalances, id => plugin.Progress.QuestAccess.GetValueOrDefault(id) ?? GameQuestAccess.Read(id));
            nextStepEvent = e.Id;
            nextStepRefresh = DateTime.UtcNow.AddSeconds(1);
        }
        var step = nextStep;
        ImGui.PushID("NextStep");
        var panelStart = ImGui.GetCursorScreenPos();
        var panelWidth = ImGui.GetContentRegionAvail().X;
        var panelScale = S;
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(0, 6 * panelScale));
        ImGui.Indent(14 * panelScale);
        SectionHeading(T("NEXT STEP"));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 12 * panelScale);
        switch (step.Kind)
        {
            case NextStepKind.Waiting: ImGui.TextColored(Muted, T("Waiting for character data")); break;
            case NextStepKind.Upcoming: ImGui.TextColored(Muted, T("This event has not started yet. Check the participation guide before it begins.")); break;
            case NextStepKind.Ended: ImGui.TextColored(Muted, T("This event has ended. Check the official page for future dates.")); break;
            case NextStepKind.Unmapped: ImGui.TextColored(Muted, T("There is not enough tracking data to determine the next step. Check the official guide.")); break;
            case NextStepKind.Accepted:
                QuestKeyValue(T("Continue quest"), step.QuestId, step.Name);
                ImGui.TextColored(Muted, T("Open your quest journal and follow the current objective. The starting NPC is not necessarily your destination."));
                break;
            case NextStepKind.Class:
                KeyValue(T("Required class or job"), step.ClassName, QuestAmber);
                QuestKeyValue(T("To unlock"), step.QuestId, step.Name, 1.18f);
                break;
            case NextStepKind.Level:
                KeyValue(T("Required level"), step.Level.ToString(L.Culture), QuestAmber, 1.3f);
                QuestKeyValue(T("To unlock"), step.QuestId, step.Name, 1.18f);
                break;
            case NextStepKind.Choices:
                ImGui.TextColored(Muted, T("Complete ONE of these prerequisite quests:"));
                foreach (var name in step.Choices ?? Array.Empty<string>()) Emphasis(name, JournalTheme.QuestTitle, 1.1f);
                ImGui.TextColored(Muted, T("Choose the route appropriate for your character. Requirements are shown in quest progress."));
                break;
            case NextStepKind.Requirements:
                QuestKeyValue(T("Check quest requirements"), step.QuestId, step.Name, 1.18f);
                ImGui.TextColored(Muted, T("Availability cannot be confirmed automatically. Check quest progress or the official guide."));
                break;
            case NextStepKind.Quest:
                QuestKeyValue(T("Start quest"), step.QuestId, step.Name);
                if (step.Partial) ImGui.TextColored(Muted, T("Special conditions may apply. Check the event dates and quest requirements if the NPC is missing."));
                var tracked = definition?.Quests.FirstOrDefault(q => q.Id == step.QuestId);
                var location = e.Locations.FirstOrDefault(l => l.Id == tracked?.LocationId) ?? GameQuestAccess.Location(step.QuestId);
                if (location != null)
                {
                    LocationSummary(location);
                    if (ImGui.SmallButton(T("Where to go") + "###NextQuestMap"))
                        if (!EventNavigation.Open(location)) mapErrorUntil = DateTime.UtcNow.AddSeconds(10);
                }
                else ImGui.TextColored(Muted, T("No confirmed map destination is available. Check the participation guide."));
                break;
            case NextStepKind.Farm:
                foreach (var budget in (step.Budgets ?? Array.Empty<CurrencyBudget>()).Where(b => b.Remaining > 0))
                    KeyValue(T("Still to earn for favorites"), $"{budget.Remaining!.Value.ToString("N0", L.Culture)} {E(budget.Currency)}", Gold, 1.25f);
                if (step.Partial) ImGui.TextColored(Muted, T("Some favorite costs or currency balances are unknown. The amount above may not cover every selected reward."));
                ImGui.TextColored(Muted, T("See How to join for the event activities. The merchant is where you spend currency, not necessarily where you earn it."));
                if (ImGui.SmallButton(T("How to join") + "###NextJoin")) requestedTab = "join";
                break;
            case NextStepKind.Buy:
                KeyValue(T("Ready to buy"), step.Name, JournalTheme.QuestTitle);
                ImGui.TextColored(QuestGreen, T("Enough currency for your favorite rewards."));
                var merchant = e.Locations.FirstOrDefault(l => l.Id == step.LocationId);
                if (merchant != null) LocationSummary(merchant);
                DrawLocationButton(step.LocationId);
                break;
            case NextStepKind.ReviewRewards:
                ImGui.TextColored(Muted, T("Check your favorite rewards: some prices, balances or acquisition states are unknown. Use Already obtained for items collected earlier."));
                break;
            case NextStepKind.FavoritesComplete:
                ImGui.TextColored(QuestGreen, T("All favorite rewards obtained."));
                ImGui.TextColored(Muted, T("Choose more rewards or hide the event when you are finished."));
                break;
            case NextStepKind.CollectionComplete:
                ImGui.TextColored(Muted, T("All tracked, non-ignored rewards are collected. You can hide the event when you are finished."));
                break;
            case NextStepKind.TrackedQuestsComplete:
                ImGui.TextColored(Muted, T("Tracked quests are complete. Check the official guide for rewards not listed in the feed."));
                break;
            case NextStepKind.ChooseRewards:
                ImGui.TextColored(Muted, T("Choose the rewards you want and mark them as favorites. Your next step will include their remaining currency cost."));
                break;
        }
        ImGui.PopTextWrapPos();
        if (step.Kind is NextStepKind.Choices or NextStepKind.Requirements or NextStepKind.Class or NextStepKind.Level or NextStepKind.ReviewRewards or NextStepKind.ChooseRewards)
            if (ImGui.SmallButton(T("Your progress") + "###NextProgress")) requestedTab = "progress";
        if (step.Kind is NextStepKind.Unmapped or NextStepKind.Upcoming or NextStepKind.Ended or NextStepKind.Requirements or NextStepKind.TrackedQuestsComplete)
            if (ImGui.SmallButton(T("Official event guide") + "###NextOfficial")) OpenUrl(e.Url);
        if (step.Kind is NextStepKind.FavoritesComplete or NextStepKind.CollectionComplete)
            if (ImGui.SmallButton(T("Hide event") + "###NextHide")) plugin.SetEventHidden(e, true);
        ImGui.Unindent(14 * panelScale);
        ImGui.Dummy(new Vector2(0, 6 * panelScale));
        ImGui.EndGroup();
        var panelEnd = new Vector2(panelStart.X + panelWidth, ImGui.GetItemRectMax().Y);
        var panelDraw = ImGui.GetWindowDrawList();
        panelDraw.AddRect(panelStart, panelEnd, ImGui.ColorConvertFloat4ToU32(JournalTheme.Border), 6 * panelScale);
        panelDraw.AddRectFilled(panelStart + new Vector2(0, 8 * panelScale), new Vector2(panelStart.X + 3 * panelScale, panelEnd.Y - 8 * panelScale),
            ImGui.ColorConvertFloat4ToU32(Gold), 2 * panelScale);
        ImGui.Spacing();
        ImGui.PopID();
    }

    private void DrawProgress(TrackingDefinition definition)
    {
        ImGui.Spacing();
        SectionHeading(T("QUEST JOURNAL"));
        uint previousId = 0;
        foreach (var quest in definition.Quests)
        {
            ImGui.PushID((int)quest.Id);
            var known = plugin.Progress.Available && plugin.Progress.Quests.TryGetValue(quest.Id, out _);
            var done = known && plugin.Progress.Quests[quest.Id];
            var access = known ? plugin.Progress.QuestAccess.GetValueOrDefault(quest.Id) : null;
            var blocked = access?.Blocked == true;
            var color = done ? QuestGreen : !known ? Muted : blocked ? QuestRed : QuestAmber;
            var start = ImGui.GetCursorScreenPos();
            var marker = start + new Vector2(10 * S, ImGui.GetTextLineHeight() * .5f);
            var draw = ImGui.GetWindowDrawList();
            // Only draw a connector where the installed quest data confirms the dependency.
            if (previousId != 0 && access?.Previous.Any(q => q.Id == previousId) == true)
                draw.AddLine(new Vector2(marker.X, start.Y - 13 * S), marker, ImGui.ColorConvertFloat4ToU32(JournalTheme.Border), 2 * S);
            draw.AddCircleFilled(marker, 8 * S, ImGui.ColorConvertFloat4ToU32(JournalTheme.Panel));
            draw.AddCircle(marker, 7 * S, ImGui.ColorConvertFloat4ToU32(color), 16, 1.5f * S);
            if (done)
            {
                draw.AddLine(marker + new Vector2(-3, 0) * S, marker + new Vector2(-.5f, 2.5f) * S,
                    ImGui.ColorConvertFloat4ToU32(color), 2 * S);
                draw.AddLine(marker + new Vector2(-.5f, 2.5f) * S, marker + new Vector2(4, -3) * S,
                    ImGui.ColorConvertFloat4ToU32(color), 2 * S);
            }
            else draw.AddCircleFilled(marker, 2.5f * S, ImGui.ColorConvertFloat4ToU32(color));
            ImGui.Indent(28 * S);
            QuestHeading(quest.Id, E(quest.Name), done ? JournalTheme.CompletedTitle : JournalTheme.QuestTitle, done ? 1.08f : 1.16f, blocked);
            StatusBadge(T(done ? "[DONE]" : !known ? "[ ... ]" : blocked ? "[LOCKED]"
                : access?.Accepted == true ? "[IN PROGRESS]" : "[TODO]"), color, quest.Name,
                T(done ? "Completed by this character." : !known ? "Waiting for character data" : blocked ? "Requirements are not met. Review the conditions below."
                    : access?.Accepted == true ? "Accepted. Follow the current objective in your quest journal." : "Not completed. Check the requirements and starting NPC below."));
            if (!done)
            {
                if (access?.Accepted != true) DrawLocationButton(quest.LocationId);
                if (known && (blocked || access?.Accepted == true)) DrawQuestAccess(access, 0, new HashSet<uint> { quest.Id });
                else if (known && ImGui.TreeNode(T("Requirements") + "###QuestRequirements"))
                {
                    DrawQuestAccess(access, 0, new HashSet<uint> { quest.Id });
                    ImGui.TreePop();
                }
            }
            ImGui.Unindent(28 * S);
            var end = ImGui.GetCursorScreenPos();
            var next = definition.Quests.SkipWhile(q => q.Id != quest.Id).Skip(1).FirstOrDefault();
            if (next != null && plugin.Progress.QuestAccess.GetValueOrDefault(next.Id)?.Previous.Any(q => q.Id == quest.Id) == true)
                draw.AddLine(marker + new Vector2(0, 9 * S), new Vector2(marker.X, end.Y + 5 * S),
                    ImGui.ColorConvertFloat4ToU32(JournalTheme.Border), 2 * S);
            ImGui.Dummy(new Vector2(0, 10 * S));
            previousId = quest.Id;
            ImGui.PopID();
        }
        ImGui.TextWrapped(T("Quest completion does not mean all shop rewards are collected."));
        ImGui.Spacing();
        var (count, total) = RewardPreferences.Progress(definition.Rewards, plugin.Preferences.IgnoredRewards, plugin.Progress.Rewards);
        ImGui.TextColored(Gold, F("REWARD COLLECTION   {0} / {1}", count, total));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(total == 0 ? 0 : (float)count / total, new Vector2(-1, 9 * S), "");
        ImGui.PopStyleColor();
        if (rewardSearchEvent != localizedEvent?.Id)
        {
            rewardSearch = "";
            rewardSearchEvent = localizedEvent?.Id ?? "";
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("###RewardSearch", T("Search rewards by English name"), ref rewardSearch, 256);
        if (rewardSearch.Length > 0)
        {
            if (ImGui.SmallButton(T("Clear search") + "###ClearRewardSearch")) rewardSearch = "";
        }
        var sort = Enum.IsDefined(plugin.Preferences.RewardSort) ? (int)plugin.Preferences.RewardSort : 0;
        ImGui.SetNextItemWidth(Math.Min(280 * S, ImGui.GetContentRegionAvail().X * .65f));
        if (ImGui.Combo(T("Sort") + "###RewardSort", ref sort,
            T("Recommended") + "\0" + T("Name (A-Z)") + "\0" + T("Price (low to high)") + "\0"))
        {
            plugin.Preferences.RewardSort = (RewardSortMode)sort;
            plugin.Save();
        }
        if (ImGui.IsItemHovered()) HelpTooltip(T("Reward sorting"), T("Sorts within groups. Missing favorites come first; obtained rewards stay at the end. Prices are compared within each currency; unknown prices follow known prices."));
        var category = Enum.IsDefined(plugin.Preferences.RewardTypeFilter) ? (int)plugin.Preferences.RewardTypeFilter : 0;
        ImGui.SetNextItemWidth(Math.Min(280 * S, ImGui.GetContentRegionAvail().X * .65f));
        if (ImGui.Combo(T("Reward type") + "###RewardCategory", ref category,
            T("All types") + "\0" + T("Gear") + "\0" + T("Mounts") + "\0" + T("Minions") + "\0" + T("Housing") + "\0" + T("Other") + "\0"))
        {
            plugin.Preferences.RewardTypeFilter = (RewardCategory)category;
            plugin.Save();
        }
        if (ImGui.IsItemHovered()) HelpTooltip(T("Reward types"), T("Types use installed item data. Gear includes equipment; Housing includes outdoor items. Unclassified items appear under Other."));
        var remaining = remainingOnly;
        if (ImGui.Checkbox(T("Show remaining rewards only") + "###Show remaining rewards only", ref remaining)) { remainingOnly = remaining; plugin.Save(); }
        var onlyFavorites = favoritesOnly;
        if (ImGui.Checkbox(T("Favorites only") + "###FavoritesOnly", ref onlyFavorites)) { favoritesOnly = onlyFavorites; plugin.Save(); }
        var favorites = definition.Rewards.Where(r => plugin.Preferences.FavoriteRewards.Contains(r.Id) && !plugin.Preferences.IgnoredRewards.Contains(r.Id)).ToList();
        if (favorites.Count > 0)
        {
            var confirmed = favorites.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
            ImGui.TextColored(Gold, F("FAVORITES   {0} / {1} obtained", confirmed, favorites.Count));
            DrawFavoriteBudget(definition);
        }
        var visibleRewards = RewardListPolicy.Select(definition.Rewards, rewardSearch, plugin.Preferences.RewardSort,
            plugin.Preferences, plugin.Progress.Rewards, remainingOnly, favoritesOnly,
            (RewardCategory)category, plugin.Progress.Categories);
        ImGui.TextDisabled(F("Showing {0} of {1} rewards", visibleRewards.Count, definition.Rewards.Count));
        if (visibleRewards.Count == 0)
            ImGui.TextWrapped(T(!plugin.Progress.Available ? "Waiting for character data" : "No rewards match. Change the search text or filters."));
        var pendingRewards = visibleRewards.Where(r => !plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed).ToList();
        var obtainedRewards = visibleRewards.Where(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed).ToList();
        DrawRewardGrid("PendingRewards", pendingRewards);
        if (obtainedRewards.Count > 0)
        {
            var key = EventDiscoveryPolicy.Key(localizedEvent!);
            var expanded = plugin.Preferences.ExpandedObtainedEvents.Contains(key);
            ImGui.SetNextItemOpen(expanded, ImGuiCond.Always);
            var open = ImGui.CollapsingHeader(F("Obtained — {0}", obtainedRewards.Count) + "###ObtainedRewards");
            if (open != expanded)
            {
                if (open) plugin.Preferences.ExpandedObtainedEvents.Add(key);
                else plugin.Preferences.ExpandedObtainedEvents.Remove(key);
                plugin.Save();
            }
            if (open) DrawRewardGrid("ObtainedRewards", obtainedRewards);
            else if (rewardSearch.Length > 0 && pendingRewards.Count == 0)
                ImGui.TextWrapped(T("Search matches are in the obtained section above. Expand it to view them."));
        }
        ImGui.Spacing();
        if (ImGui.CollapsingHeader(T("HOW TRACKING WORKS") + "###TrackingHelp"))
        {
            ImGui.TextWrapped(T("Unlocked = registered collection. In bags / equipped = currently found. Previously obtained = seen before on this character. Earned from quest = guaranteed quest reward."));
            ImGui.TextWrapped(T("Not detected does not mean never owned: retainers, housing, glamour dresser and armoire are not scanned. Items seen by this plugin are remembered per character."));
        }
    }

    private void TrackViewedEvent(EventInfo e)
    {
        if (!plugin.HasCharacterPreferences) return;
        var key = EventDiscoveryPolicy.Key(e);
        var now = DateTime.UtcNow;
        if (viewingEventKey != key || now - lastViewFrame > TimeSpan.FromMilliseconds(500))
        {
            viewingEventKey = key;
            viewStarted = now;
        }
        lastViewFrame = now;
        if (now - viewStarted >= TimeSpan.FromSeconds(2) && EventDiscoveryPolicy.MarkViewed(plugin.Preferences, e)) plugin.Save();
    }

    private void DrawRewardGrid(string id, IReadOnlyList<TrackedReward> rewards)
    {
        if (rewards.Count == 0) return;
        var columns = ImGui.GetContentRegionAvail().X >= 560 * S ? 2 : 1;
        if (!ImGui.BeginTable(id, columns, ImGuiTableFlags.SizingStretchSame)) return;
        foreach (var reward in rewards)
        {
            ImGui.TableNextColumn();
            DrawReward(reward, plugin.Progress.Rewards.GetValueOrDefault(reward.Id));
        }
        ImGui.EndTable();
    }

    private void DrawFavoriteBudget(TrackingDefinition definition)
    {
        if (!plugin.Progress.ReadyForReminders)
        {
            ImGui.TextWrapped(T("Waiting for character data"));
            return;
        }
        var pending = FavoriteBudget.Pending(definition.Rewards, plugin.Preferences.FavoriteRewards,
            plugin.Preferences.IgnoredRewards, plugin.Progress.Rewards);
        if (pending.Count == 0)
        {
            ImGui.TextColored(Teal, T("All favorite rewards obtained."));
            return;
        }
        ImGui.Separator();
        SectionHeading(T("CURRENCY FOR FAVORITES"));
        foreach (var budget in FavoriteBudget.Calculate(pending, plugin.Progress.CurrencyBalances))
        {
            ImGui.TextWrapped(F("Remaining rewards cost: {0} {1}", budget.Required.ToString("N0", L.Culture), E(budget.Currency)));
            if (budget.Balance is { } balance)
            {
                ImGui.TextWrapped(F("You have: {0} {1}", balance.ToString("N0", L.Culture), E(budget.Currency)));
                ImGui.TextColored(budget.Remaining == 0 ? Teal : Gold,
                    F("Still to earn: {0} {1}", budget.Remaining!.Value.ToString("N0", L.Culture), E(budget.Currency)));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
                ImGui.ProgressBar((float)Math.Min(1d, (double)balance / budget.Required), new Vector2(-1, 6 * S), "");
                ImGui.PopStyleColor();
            }
            else ImGui.TextWrapped(T("Balance unavailable; the cost above is not the amount still to earn."));
        }
        var unpriced = pending.Count(r => r.Price == 0 && r.EarnedByQuest == 0);
        if (unpriced > 0) ImGui.TextWrapped(F("Favorites with unknown prices: {0}. Not included in the total.", unpriced));
        if (pending.Any(r => r.Price == 0 && r.EarnedByQuest != 0))
            ImGui.TextWrapped(T("Quest rewards require completing their quests, not currency."));
        ImGui.Separator();
    }

    private void DrawReward(TrackedReward reward, RewardProgress progress)
    {
        ImGui.PushID((int)reward.Id);
        var scale = S;
        var padding = 10 * scale;
        var gap = 5 * scale;
        var ignored = plugin.Preferences.IgnoredRewards.Contains(reward.Id);
        var favorite = plugin.Preferences.FavoriteRewards.Contains(reward.Id);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - padding * 2 - 2 * scale);
        var textWidth = Math.Max(50 * scale, width - 54 * scale);
        var currencyIcon = GameUiAssets.Item(FavoriteBudget.CurrencyId(reward)).Icon;
        var currencyWidth = currencyIcon != 0 ? 23 * scale : 0;
        var itemUi = GameUiAssets.Item(reward.Id);
        var title = E(reward.Name);
        var price = reward.Price > 0 ? $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}" : "";
        var headerHeight = Math.Max(44 * scale, ImGui.CalcTextSize(title, false, textWidth).Y
            + (price.Length > 0 ? Math.Max(18 * scale, ImGui.CalcTextSize(price, false, Math.Max(30 * scale, textWidth - currencyWidth)).Y) + gap : 0));
        var status = ignored ? T("Not interested") : L.RewardLabel(progress);
        var statusHeight = ImGui.CalcTextSize(status, false, width).Y;
        var footerHeight = ImGui.GetFrameHeight();
        var hasLocation = localizedEvent?.Locations.Any(l => l.Id == reward.LocationId) == true;
        var footerWidth = footerHeight + ImGui.CalcTextSize(T("Actions")).X + ImGui.GetStyle().FramePadding.X * 2 + 8 * scale;
        var mapWidth = hasLocation ? ImGui.CalcTextSize(T("Where to go")).X + ImGui.GetStyle().FramePadding.X * 2 + 8 * scale : 0;
        var mapOnNewLine = hasLocation && footerWidth + mapWidth > width;
        var height = padding * 2 + headerHeight + statusHeight + footerHeight + gap * 3
            + (mapOnNewLine ? footerHeight + gap : 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8 * scale, gap));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, JournalTheme.WithOpacity(ignored ? JournalTheme.Panel : JournalTheme.Card, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, favorite && !ignored ? new Vector4(Gold.X, Gold.Y, Gold.Z, .65f) : JournalTheme.Border);
        if (BeginScaledChild("Reward tile", new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            var tilePosition = ImGui.GetWindowPos();
            var tileSize = ImGui.GetWindowSize();
            if (ImGui.IsWindowHovered())
            {
                var tileDraw = ImGui.GetWindowDrawList();
                tileDraw.AddRectFilled(tilePosition + Vector2.One, tilePosition + tileSize - Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(.75f, .74f, .65f, .035f)), 5 * scale);
                tileDraw.AddRect(tilePosition + Vector2.One, tilePosition + tileSize - Vector2.One,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(Gold.X, Gold.Y, Gold.Z, .65f)), 5 * scale);
            }
            var iconStart = ImGui.GetCursorScreenPos();
            var displayIcon = itemUi.Icon != 0 ? itemUi.Icon : reward.Icon;
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(displayIcon));
            if (displayIcon != 0 && icon.TryGetWrap(out var wrap, out _))
                ImGui.Image(wrap.Handle, new Vector2(44 * scale), Vector2.Zero, Vector2.One, ignored ? new Vector4(.5f, .5f, .5f, 1) : Vector4.One);
            else ImGui.Dummy(new Vector2(44 * scale));
            ImGui.GetWindowDrawList().AddRect(iconStart, iconStart + new Vector2(44 * scale),
                ImGui.ColorConvertFloat4ToU32(ignored ? Muted : GameUiAssets.RarityColor(itemUi.Rarity)), 3 * scale);
            if (progress.Confirmed)
            {
                var stamp = iconStart + new Vector2(37, 37) * scale;
                var iconDraw = ImGui.GetWindowDrawList();
                iconDraw.AddCircleFilled(stamp, 8 * scale, ImGui.ColorConvertFloat4ToU32(JournalTheme.Background));
                iconDraw.AddCircle(stamp, 7 * scale, ImGui.ColorConvertFloat4ToU32(QuestGreen), 16);
                iconDraw.AddLine(stamp + new Vector2(-3, 0) * scale, stamp + new Vector2(-.5f, 3) * scale,
                    ImGui.ColorConvertFloat4ToU32(QuestGreen), 2 * scale);
                iconDraw.AddLine(stamp + new Vector2(-.5f, 3) * scale, stamp + new Vector2(4, -3) * scale,
                    ImGui.ColorConvertFloat4ToU32(QuestGreen), 2 * scale);
            }
            DrawWikiLink(reward);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextColored(ignored ? Muted : favorite ? Gold : JournalTheme.Text, title);
            DrawWikiLink(reward, true);
            if (price.Length > 0)
            {
                if (currencyIcon != 0 && Plugin.Textures.GetFromGameIcon(new GameIconLookup(currencyIcon)).TryGetWrap(out var currencyWrap, out _))
                {
                    ImGui.Image(currencyWrap.Handle, new Vector2(18 * scale));
                    if (ImGui.IsItemHovered()) HelpTooltip(E(reward.Currency), T("Currency required for this reward."));
                    ImGui.SameLine(0, 5 * scale);
                }
                ImGui.TextColored(ignored ? Muted : Gold, price);
            }
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.SetCursorPos(new Vector2(padding, padding + headerHeight + gap));
            ImGui.PushTextWrapPos(padding + width);
            ImGui.TextColored(!ignored && progress.Confirmed ? QuestGreen : Muted, status);
            if (ImGui.IsItemHovered()) HelpTooltip(reward.Name, status,
                T("Automatic checks use this character's unlocks and observed items. Manual marks apply only to this character."));
            ImGui.PopTextWrapPos();
            if (ImGui.Button("###FavoriteToggle", new Vector2(footerHeight)))
            {
                RewardPreferences.Favorite(plugin.Preferences, reward.Id, !favorite);
                plugin.Save();
            }
            var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) * .5f;
            var draw = ImGui.GetWindowDrawList();
            var starColor = ImGui.ColorConvertFloat4ToU32(favorite && !ignored ? Gold : Muted);
            var radius = footerHeight * .30f;
            for (var i = 0; i < 10; i++)
            {
                Vector2 Point(int n) { var angle = -MathF.PI / 2 + n * MathF.PI / 5; return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * (n % 2 == 0 ? 1 : .45f); }
                draw.AddLine(Point(i), Point((i + 1) % 10), starColor, favorite ? 2 * scale : 1.2f * scale);
            }
            if (ImGui.IsItemHovered()) HelpTooltip(reward.Name, T(favorite ? "Remove from favorites" : "Add to favorites"));
            ImGui.SameLine();
            if (ImGui.Button(T("Actions") + "###RewardActions")) ImGui.OpenPopup("Reward actions");
            if (hasLocation)
            {
                if (!mapOnNewLine) ImGui.SameLine();
                DrawLocationButton(reward.LocationId);
            }
            if (ImGui.BeginPopup("Reward actions"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 320 * scale);
                ImGui.TextColored(Gold, title);
                ImGui.PopTextWrapPos();
                ImGui.Separator();
                if (ImGui.Checkbox(T("Favorite") + "###Favorite", ref favorite)) { RewardPreferences.Favorite(plugin.Preferences, reward.Id, favorite); plugin.Save(); }
                if (ImGui.Checkbox(T("Not interested") + "###NotInterested", ref ignored)) { RewardPreferences.Ignore(plugin.Preferences, reward.Id, ignored); plugin.Save(); }
                if (ImGui.IsItemHovered()) HelpTooltip(reward.Name, T("Excluded from progress and reminders. Uncheck to count this reward again."));
                var manual = plugin.Progress.IsManuallyObtained(reward.Id);
                ImGui.BeginDisabled(!plugin.Progress.ReadyForReminders);
                if (ImGui.Checkbox(T("Already obtained (manual)") + "###ManuallyObtained", ref manual)) plugin.Progress.SetManuallyObtained(reward.Id, manual);
                if (ImGui.IsItemHovered()) HelpTooltip(reward.Name, T("Saved for this character. Clearing this mark preserves any automatic confirmation."));
                ImGui.EndDisabled();
                ImGui.EndPopup();
            }
        }
        EndScaledChild();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
        ImGui.PopID();
    }

    private void DrawQuestAccess(QuestAccess? access, int depth, HashSet<uint> path)
    {
        if (access == null || !access.Known) { ImGui.TextWrapped(T("Quest requirements could not be checked.")); return; }
        if (access.Accepted) { ImGui.TextWrapped(T("Quest already accepted. Open your quest journal and follow the current objective; the starting NPC may have moved.")); return; }
        if (!access.LevelMet) ImGui.TextColored(QuestAmber, F("Required level: {0} (current class)", access.Level));
        if (!access.ClassMet) KeyValue(T("Required class or job"), access.ClassName, QuestAmber, 1.1f);
        var previousMet = QuestAccess.MeetsPrevious(access.Join, access.Previous);
        if (previousMet != true && access.Previous.Count > 0)
        {
            ImGui.TextWrapped(T(previousMet == null ? "Check prerequisite quests (special conditions):"
                : access.Join == 2 ? "Complete ONE of these quests:" : "Complete these quests first:"));
            foreach (var requirement in access.Previous)
            {
                if (access.Join == 1 && requirement.Complete) continue;
                ImGui.PushID((int)requirement.Id);
                ImGui.Indent(12 * S);
                ImGui.TextColored(requirement.Complete ? QuestGreen : QuestAmber, requirement.Complete ? "+" : "-");
                ImGui.SameLine();
                QuestHeading(requirement.Id, requirement.Name, requirement.Complete ? JournalTheme.CompletedTitle : JournalTheme.QuestTitle, 1.08f);
                if (!requirement.Complete && depth < 4 && !path.Contains(requirement.Id)
                    && ImGui.TreeNode(T("How to unlock") + "###Prerequisite"))
                {
                    var location = GameQuestAccess.Location(requirement.Id);
                    if (location != null)
                    {
                        LocationSummary(location);
                        if (ImGui.SmallButton(T("Where to go") + "###PrerequisiteMap"))
                            if (!EventNavigation.Open(location)) mapErrorUntil = DateTime.UtcNow.AddSeconds(10);
                    }
                    path.Add(requirement.Id);
                    DrawQuestAccess(GameQuestAccess.Read(requirement.Id), depth + 1, path);
                    path.Remove(requirement.Id);
                    ImGui.TreePop();
                }
                ImGui.Unindent(12 * S);
                ImGui.PopID();
            }
        }
        if (access.Seasonal) ImGui.TextWrapped(T("Seasonal quest: its NPC is available only during the relevant event."));
        if (access.Partial) ImGui.TextWrapped(T("Some conditions cannot be checked automatically. Check the quest requirements in the game or official guide."));
        else if (!access.Blocked) ImGui.TextWrapped(T("Checked level and quest prerequisites are met. If the NPC is missing, check the event dates and whether the quest is already in your journal."));
        if (access.Blocked) ImGui.TextWrapped(T("The quest NPC may be unavailable until these requirements are met."));
    }

    private void DrawLocationButton(string locationId)
    {
        var location = localizedEvent?.Locations.FirstOrDefault(l => l.Id == locationId);
        if (location == null) return;
        ImGui.BeginDisabled(!Plugin.Client.IsLoggedIn);
        if (ImGui.SmallButton(T("Where to go") + "###Map"))
            if (!EventNavigation.Open(location)) mapErrorUntil = DateTime.UtcNow.AddSeconds(10);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            HelpTooltip(E(location.Npc), $"{E(location.Zone)}\nX:{location.X:F1} Y:{location.Y:F1}",
                T(Plugin.Client.IsLoggedIn ? "Opens the map at this confirmed destination. Access conditions may still apply." : "Log in to open the map."), JournalTheme.NpcTitle);
        ImGui.EndDisabled();
    }

    private void DrawHero(EventInfo e, DateTimeOffset now)
    {
        var scale = S;
        var width = ImGui.GetContentRegionAvail().X;
        var height = Math.Clamp(width * .25f, 125 * scale, 185 * scale);
        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var size = new Vector2(width, height);
        draw.AddRectFilled(start, start + size, ImGui.ColorConvertFloat4ToU32(JournalTheme.Panel), 5 * scale);
        var file = EventImagePath(e);
        if (file != null && Plugin.Textures.GetFromFile(file).TryGetWrap(out var wrap, out _))
        {
            var displayRatio = width / height;
            var imageRatio = (float)wrap.Width / wrap.Height;
            var visible = imageRatio > displayRatio ? new Vector2(displayRatio / imageRatio, 1) : new Vector2(1, imageRatio / displayRatio);
            draw.AddImage(wrap.Handle, start, start + size, (Vector2.One - visible) / 2, (Vector2.One + visible) / 2);
        }
        draw.AddRect(start, start + size, ImGui.ColorConvertFloat4ToU32(JournalTheme.Border), 4 * scale);
        ImGui.Dummy(size);
        ImGui.Dummy(new Vector2(0, 3 * scale));
        ImGui.SetWindowFontScale(UiScale * 1.65f);
        try
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(JournalTheme.Text, E(e.Title));
            ImGui.PopTextWrapPos();
        }
        finally { ImGui.SetWindowFontScale(UiScale); }
        var accent = ImGui.GetCursorScreenPos();
        draw.AddLine(accent, accent + new Vector2(46 * scale, 0), ImGui.ColorConvertFloat4ToU32(Gold), 2 * scale);
        ImGui.Dummy(new Vector2(0, 2 * scale));
        DrawDeadline(e, now);
        ImGui.Spacing();
    }

    private void DrawWikiLink(TrackedReward reward, bool title = false)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var hint = wikiItem == reward.Name && wikiRequest != null ? F("Checking wiki: {0}", wikiItem)
            : wikiItem == reward.Name && DateTime.UtcNow < wikiMessageUntil && wikiMessage.Length > 0 ? T(wikiMessage)
            : T("Click to open this item on the FFXIV wiki.");
        if (title)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y - S), new Vector2(max.X, max.Y - S),
                ImGui.ColorConvertFloat4ToU32(Gold), S);
        }
        var description = GameUiAssets.Item(reward.Id).Description;
        var details = string.IsNullOrWhiteSpace(description) ? hint : description;
        if (reward.Price > 0) details += "\n\n" + F("Price: {0} {1}", reward.Price.ToString("N0", L.Culture), E(reward.Currency));
        HelpTooltip(reward.Name, details, string.IsNullOrWhiteSpace(description) ? null : hint);
        if (!clicked) return;
        if (wikiItem == reward.Name && wikiRequest != null) return;
        wikiItem = reward.Name;
        wikiMessage = "";
        wikiRequest = wiki.FindAsync(reward.Name, wikiCancellation.Token);
    }

    internal void UpdateExternalLinks()
    {
        if (wikiRequest is not { IsCompleted: true } request) return;
        wikiRequest = null;
        var result = request.IsCompletedSuccessfully ? request.Result : new WikiResult(WikiState.Unavailable);
        if (request.IsFaulted) Plugin.Log.Warning(request.Exception!, "Item wiki lookup failed");
        if (result.State == WikiState.Found) OpenUrl(result.Url);
        else
        {
            wikiMessage = result.State == WikiState.Missing ? "No unambiguous wiki page found for this item."
                : "Could not reach the wiki. Please try again.";
            wikiMessageUntil = DateTime.UtcNow.AddSeconds(10);
        }
    }

    private void DrawSettings()
    {
        var c = plugin.Preferences;
        ImGui.TextWrapped(T("Display and reward preferences belong to this character. The event feed URL is shared."));
        if (!plugin.HasCharacterPreferences) ImGui.TextWrapped(T("Log in to edit this character's preferences."));
        ImGui.BeginDisabled(!plugin.HasCharacterPreferences);
        var language = L.IsRussian ? 1 : 0;
        if (ImGui.Combo(T("Language") + "###Language", ref language, "English\0Русский\0"))
        {
            c.Language = language == 1 ? "ru" : "en";
            plugin.Save();
        }
        ImGui.TextWrapped(T("Item, NPC and quest names always stay in English. Interface and event descriptions use your selected language."));
        var uiScale = UiScale;
        if (ImGui.SliderFloat(T("Interface scale") + "###UiScale", ref uiScale, .75f, 1.75f, "%.2fx")) c.UiScale = uiScale;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        var opacity = Opacity;
        if (ImGui.SliderFloat(T("Background opacity") + "###BackgroundOpacity", ref opacity, .4f, 1, "%.2f")) c.BackgroundOpacity = opacity;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        ImGui.TextWrapped(T("Applies to the journal and widget. Text and icons remain opaque."));
        if (ImGui.SmallButton(T("Reset appearance") + "###ResetAppearance"))
        { c.UiScale = 1; c.BackgroundOpacity = 1; plugin.Save(); }
        var widget = c.ShowCompactWidget;
        if (ImGui.Checkbox(T("Show compact event widget") + "###CompactWidget", ref widget)) plugin.SetWidgetVisible(widget);
        var notify = c.NotifyOnLogin; if (ImGui.Checkbox(T("Notify on login") + "###Notify on login", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
        ImGui.SameLine();
        var open = c.OpenOnLogin; if (ImGui.Checkbox(T("Open on login") + "###Open on login", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
        ImGui.TextWrapped(T("Automatic opening skips inactive events and completed main quests. You can always open the journal or widget manually."));
        var deadlines = c.NotifyFavoriteDeadlines;
        if (ImGui.Checkbox(T("Remind me about favorite rewards before events end") + "###FavoriteDeadlines", ref deadlines))
        { c.NotifyFavoriteDeadlines = deadlines; plugin.Save(); }
        ImGui.TextWrapped(T("Reminders at 3 days and 24 hours, once per character and event deadline. If you were offline, the latest reminder appears after login. Unknown rewards are skipped."));
        var unverified = c.ShowUnverified; if (ImGui.Checkbox(T("Show partial event entries") + "###Show partial event entries", ref unverified)) { c.ShowUnverified = unverified; plugin.Save(); }
        ImGui.EndDisabled();
        ImGui.InputText(T("HTTPS feed URL") + "###HTTPS feed URL", ref urlBuffer, 512);
        if (ImGui.Button(T("Save feed URL") + "###Save feed URL")) { plugin.Settings.FeedUrl = urlBuffer.Trim(); plugin.Save(); _ = plugin.Feed.RefreshAsync(plugin.Settings.FeedUrl); }
        ImGui.TextWrapped(T("Events, quest IDs, rewards, artwork and translations update through the feed. New tracking mechanics may still require a plugin update."));
        ImGui.TextWrapped(T("Leave URL empty for bundled / cached data. Character tracking works locally and refreshes every second."));
    }

    private string Countdown(EventInfo e, DateTimeOffset now) => DeadlinePolicy.Countdown(e, now, L);

    private void DrawDeadline(EventInfo e, DateTimeOffset now)
    {
        var status = DeadlinePolicy.Status(e, now);
        var color = JournalTheme.DeadlineColor(status);
        var text = Countdown(e, now);
        var scale = S;
        var width = ImGui.GetContentRegionAvail().X;
        var padding = 10 * scale;
        var labelHeight = ImGui.CalcTextSize(T(DeadlinePolicy.Label(status)), false, width - padding * 2).Y;
        float textHeight;
        ImGui.SetWindowFontScale(UiScale * 1.3f);
        try { textHeight = ImGui.CalcTextSize(text, false, width - padding * 2).Y; }
        finally { ImGui.SetWindowFontScale(UiScale); }
        var height = padding * 2 + labelHeight + 5 * scale + textHeight;
        var start = ImGui.GetCursorScreenPos();
        var cursor = ImGui.GetCursorPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, height),
            ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, .10f * Opacity)), 5 * scale);
        draw.AddRectFilled(start, start + new Vector2(3 * scale, height), ImGui.ColorConvertFloat4ToU32(color), 2 * scale);
        ImGui.SetCursorPos(cursor + new Vector2(padding));
        ImGui.PushTextWrapPos(cursor.X + width - padding);
        ImGui.TextColored(color, T(DeadlinePolicy.Label(status)));
        ImGui.SetCursorPos(cursor + new Vector2(padding, padding + labelHeight + 5 * scale));
        ImGui.SetWindowFontScale(UiScale * 1.3f);
        try { ImGui.TextColored(JournalTheme.Text, text); }
        finally { ImGui.SetWindowFontScale(UiScale); ImGui.PopTextWrapPos(); }
        ImGui.SetCursorPos(cursor + new Vector2(0, height + 8 * scale));
    }

    private void Section(string title, System.Collections.Generic.IEnumerable<string> items)
    {
        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count == 0) return;
        ImGui.Spacing();
        SectionHeading(T(title));
        ImGui.PushTextWrapPos(0);
        foreach (var item in list) ImGui.BulletText(E(item));
        ImGui.PopTextWrapPos();
    }

    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !(uri.Host.Equals("finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase) || ItemWiki.IsPageUrl(url))) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Plugin.Log.Error(ex, "Could not open external page"); }
    }
    public void Dispose() { wikiRequest = null; wikiCancellation.Cancel(); wiki.Dispose(); wikiCancellation.Dispose(); }
}
