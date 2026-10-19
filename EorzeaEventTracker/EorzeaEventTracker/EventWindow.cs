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
    }

    public override void PostDraw() => ImGui.PopStyleColor(4);

    public override void Draw()
    {
        ImGui.SetWindowFontScale(UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, JournalTheme.WithOpacity(JournalTheme.Background, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, JournalTheme.Border);
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
        finally { ImGui.PopStyleColor(21); ImGui.PopStyleVar(3); ImGui.SetWindowFontScale(1); }
    }

    private bool BeginScaledChild(string id, Vector2 size, bool border, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        // Child windows inherit their parent's font scale. Normalize it so nested reward
        // panels get the same requested scale rather than multiplying or losing it.
        ImGui.SetWindowFontScale(1);
        var visible = ImGui.BeginChild(id, size, border, flags);
        ImGui.SetWindowFontScale(UiScale);
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
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Tracking stays on this computer. Collection unlocks and observed rewards are separate from quest completion."));
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
        draw.AddRect(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(active ? accent : JournalTheme.Border), 8 * S);
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
        ImGui.TextColored(accent, T(e.Active(now) ? "LIVE NOW" : "UPCOMING"));
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.TextColored(Muted, Countdown(e, now));
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
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("You can restore this event from Hidden. Progress is kept."));
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
        if (ImGui.BeginTabBar("Event tabs"))
        {
            if (ImGui.BeginTabItem(T("Your progress") + "###Your progress", SelectRequestedTab("progress")))
            {
                if (definition == null) ImGui.TextWrapped(T("Automatic tracking has not been mapped for this event yet."));
                else DrawProgress(definition);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(T("How to join") + "###How to join", SelectRequestedTab("join")))
            {
                Section("REQUIREMENTS", e.Requirements);
                Section("YOUR NEXT STEPS", e.Steps);
                if (e.Locations.Count > 0)
                {
                    ImGui.TextColored(Gold, T("EVENT NPCs"));
                    foreach (var location in e.Locations)
                    {
                        ImGui.PushID(location.Id);
                        ImGui.TextWrapped($"{E(location.Npc)} — {E(location.Zone)} (X:{location.X:F1} Y:{location.Y:F1})");
                        DrawLocationButton(location.Id);
                        ImGui.PopID();
                    }
                }
                Section("SOURCE NOTES", e.Notes);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(T("Reward guide") + "###Reward guide"))
            {
                Section("REWARDS & PRICES", e.Rewards);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
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
        ImGui.Separator();
        ImGui.TextColored(Gold, T("NEXT STEP"));
        ImGui.PushTextWrapPos(0);
        switch (step.Kind)
        {
            case NextStepKind.Waiting: ImGui.TextWrapped(T("Waiting for character data")); break;
            case NextStepKind.Upcoming: ImGui.TextWrapped(T("This event has not started yet. Check the participation guide before it begins.")); break;
            case NextStepKind.Ended: ImGui.TextWrapped(T("This event has ended. Check the official page for future dates.")); break;
            case NextStepKind.Unmapped: ImGui.TextWrapped(T("There is not enough tracking data to determine the next step. Check the official guide.")); break;
            case NextStepKind.Accepted:
                ImGui.TextColored(QuestAmber, F("Continue quest: {0}", step.Name));
                ImGui.TextWrapped(T("Open your quest journal and follow the current objective. The starting NPC is not necessarily your destination."));
                break;
            case NextStepKind.Class:
                ImGui.TextWrapped(F("Switch to the required class or job: {0}", step.ClassName));
                ImGui.TextWrapped(F("To unlock: {0}", step.Name));
                break;
            case NextStepKind.Level:
                ImGui.TextWrapped(F("Reach level {0} on the required class to unlock {1}.", step.Level, step.Name));
                break;
            case NextStepKind.Choices:
                ImGui.TextWrapped(T("Complete ONE of these prerequisite quests:"));
                foreach (var name in step.Choices ?? Array.Empty<string>()) ImGui.BulletText(name);
                ImGui.TextWrapped(T("Choose the route appropriate for your character. Requirements are shown in quest progress."));
                break;
            case NextStepKind.Requirements:
                ImGui.TextWrapped(F("Check the requirements for: {0}", step.Name));
                ImGui.TextWrapped(T("Availability cannot be confirmed automatically. Check quest progress or the official guide."));
                break;
            case NextStepKind.Quest:
                ImGui.TextWrapped(F("Start quest: {0}", step.Name));
                if (step.Partial) ImGui.TextWrapped(T("Special conditions may apply. Check the event dates and quest requirements if the NPC is missing."));
                var tracked = definition?.Quests.FirstOrDefault(q => q.Id == step.QuestId);
                var location = e.Locations.FirstOrDefault(l => l.Id == tracked?.LocationId) ?? GameQuestAccess.Location(step.QuestId);
                if (location != null)
                {
                    ImGui.TextWrapped($"{E(location.Npc)} — {E(location.Zone)} (X:{location.X:F1} Y:{location.Y:F1})");
                    if (ImGui.SmallButton(T("Where to go") + "###NextQuestMap"))
                        if (!EventNavigation.Open(location)) mapErrorUntil = DateTime.UtcNow.AddSeconds(10);
                }
                else ImGui.TextWrapped(T("No confirmed map destination is available. Check the participation guide."));
                break;
            case NextStepKind.Farm:
                foreach (var budget in (step.Budgets ?? Array.Empty<CurrencyBudget>()).Where(b => b.Remaining > 0))
                    ImGui.TextWrapped(F("Earn {0} more {1} for your favorite rewards.", budget.Remaining!.Value.ToString("N0", L.Culture), E(budget.Currency)));
                if (step.Partial) ImGui.TextWrapped(T("Some favorite costs or currency balances are unknown. The amount above may not cover every selected reward."));
                ImGui.TextWrapped(T("See How to join for the event activities. The merchant is where you spend currency, not necessarily where you earn it."));
                if (ImGui.SmallButton(T("How to join") + "###NextJoin")) requestedTab = "join";
                break;
            case NextStepKind.Buy:
                ImGui.TextWrapped(F("You have enough currency for your favorite rewards. Buy: {0}", step.Name));
                var merchant = e.Locations.FirstOrDefault(l => l.Id == step.LocationId);
                if (merchant != null) ImGui.TextWrapped($"{E(merchant.Npc)} — {E(merchant.Zone)}");
                DrawLocationButton(step.LocationId);
                break;
            case NextStepKind.ReviewRewards:
                ImGui.TextWrapped(T("Check your favorite rewards: some prices, balances or acquisition states are unknown. Use Already obtained for items collected earlier."));
                break;
            case NextStepKind.FavoritesComplete:
                ImGui.TextColored(QuestGreen, T("All favorite rewards obtained."));
                ImGui.TextWrapped(T("Choose more rewards or hide the event when you are finished."));
                break;
            case NextStepKind.CollectionComplete:
                ImGui.TextWrapped(T("All tracked, non-ignored rewards are collected. You can hide the event when you are finished."));
                break;
            case NextStepKind.TrackedQuestsComplete:
                ImGui.TextWrapped(T("Tracked quests are complete. Check the official guide for rewards not listed in the feed."));
                break;
            case NextStepKind.ChooseRewards:
                ImGui.TextWrapped(T("Choose the rewards you want and mark them as favorites. Your next step will include their remaining currency cost."));
                break;
        }
        ImGui.PopTextWrapPos();
        if (step.Kind is NextStepKind.Choices or NextStepKind.Requirements or NextStepKind.Class or NextStepKind.Level or NextStepKind.ReviewRewards or NextStepKind.ChooseRewards)
            if (ImGui.SmallButton(T("Your progress") + "###NextProgress")) requestedTab = "progress";
        if (step.Kind is NextStepKind.Unmapped or NextStepKind.Upcoming or NextStepKind.Ended or NextStepKind.Requirements or NextStepKind.TrackedQuestsComplete)
            if (ImGui.SmallButton(T("Official event guide") + "###NextOfficial")) OpenUrl(e.Url);
        if (step.Kind is NextStepKind.FavoritesComplete or NextStepKind.CollectionComplete)
            if (ImGui.SmallButton(T("Hide event") + "###NextHide")) plugin.SetEventHidden(e, true);
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PopID();
    }

    private void DrawProgress(TrackingDefinition definition)
    {
        ImGui.Spacing();
        ImGui.TextColored(Gold, T("QUEST JOURNAL"));
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
            ImGui.TextWrapped(E(quest.Name));
            ImGui.TextColored(color, T(done ? "[DONE]" : !known ? "[ ... ]" : blocked ? "[LOCKED]"
                : access?.Accepted == true ? "[IN PROGRESS]" : "[TODO]"));
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
        if (!definition.Rewards.Any(r => (!favoritesOnly || plugin.Preferences.FavoriteRewards.Contains(r.Id))
            && (!remainingOnly || (!plugin.Preferences.IgnoredRewards.Contains(r.Id) && !plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed))))
            ImGui.TextWrapped(T("No rewards match these filters. Mark rewards as favorites or change the filters."));
        var columns = ImGui.GetContentRegionAvail().X >= 560 * S ? 2 : 1;
        if (ImGui.BeginTable("Rewards", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var reward in definition.Rewards)
            {
                var state = plugin.Progress.Rewards.GetValueOrDefault(reward.Id);
                if (remainingOnly && (state.Confirmed || plugin.Preferences.IgnoredRewards.Contains(reward.Id))) continue;
                if (favoritesOnly && !plugin.Preferences.FavoriteRewards.Contains(reward.Id)) continue;
                ImGui.TableNextColumn();
                DrawReward(reward, state);
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
        if (ImGui.CollapsingHeader(T("HOW TRACKING WORKS") + "###TrackingHelp"))
        {
            ImGui.TextWrapped(T("Unlocked = registered collection. In bags / equipped = currently found. Previously obtained = seen before on this character. Earned from quest = guaranteed quest reward."));
            ImGui.TextWrapped(T("Not detected does not mean never owned: retainers, housing, glamour dresser and armoire are not scanned. Items seen by this plugin are remembered per character."));
        }
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
        ImGui.TextColored(Gold, T("CURRENCY FOR FAVORITES"));
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
        var title = E(reward.Name);
        var price = reward.Price > 0 ? $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}" : "";
        var headerHeight = Math.Max(44 * scale, ImGui.CalcTextSize(title, false, textWidth).Y
            + (price.Length > 0 ? ImGui.CalcTextSize(price, false, textWidth).Y + gap : 0));
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
            var iconStart = ImGui.GetCursorScreenPos();
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(reward.Icon));
            if (reward.Icon != 0 && icon.TryGetWrap(out var wrap, out _))
                ImGui.Image(wrap.Handle, new Vector2(44 * scale), Vector2.Zero, Vector2.One, ignored ? new Vector4(.5f, .5f, .5f, 1) : Vector4.One);
            else ImGui.Dummy(new Vector2(44 * scale));
            ImGui.GetWindowDrawList().AddRect(iconStart, iconStart + new Vector2(44 * scale),
                ImGui.ColorConvertFloat4ToU32(progress.Confirmed && !ignored ? QuestGreen : JournalTheme.Border), 3 * scale);
            DrawWikiLink(reward);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextColored(ignored ? Muted : favorite ? Gold : JournalTheme.Text, title);
            DrawWikiLink(reward);
            if (price.Length > 0) ImGui.TextColored(ignored ? Muted : Gold, price);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.SetCursorPos(new Vector2(padding, padding + headerHeight + gap));
            ImGui.PushTextWrapPos(padding + width);
            ImGui.TextColored(!ignored && progress.Confirmed ? QuestGreen : Muted, status);
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
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T(favorite ? "Remove from favorites" : "Add to favorites"));
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
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Excluded from progress and reminders. Uncheck to count this reward again."));
                var manual = plugin.Progress.IsManuallyObtained(reward.Id);
                ImGui.BeginDisabled(!plugin.Progress.ReadyForReminders);
                if (ImGui.Checkbox(T("Already obtained (manual)") + "###ManuallyObtained", ref manual)) plugin.Progress.SetManuallyObtained(reward.Id, manual);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Saved for this character. Clearing this mark preserves any automatic confirmation."));
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
        if (!access.ClassMet) ImGui.TextWrapped(F("Required class or job: {0}", access.ClassName));
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
                ImGui.TextWrapped(requirement.Name);
                if (!requirement.Complete && depth < 4 && !path.Contains(requirement.Id)
                    && ImGui.TreeNode(T("How to unlock") + "###Prerequisite"))
                {
                    var location = GameQuestAccess.Location(requirement.Id);
                    if (location != null)
                    {
                        ImGui.TextWrapped($"{location.Npc} — {location.Zone} (X:{location.X:F1} Y:{location.Y:F1})");
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
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"{E(location.Npc)} — {E(location.Zone)}\nX:{location.X:F1} Y:{location.Y:F1}");
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
        ImGui.TextColored(Gold, Countdown(e, now));
        ImGui.Spacing();
    }

    private void DrawWikiLink(TrackedReward reward)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var hint = wikiItem == reward.Name && wikiRequest != null ? F("Checking wiki: {0}", wikiItem)
            : wikiItem == reward.Name && DateTime.UtcNow < wikiMessageUntil && wikiMessage.Length > 0 ? T(wikiMessage)
            : T("Click to open this item on the FFXIV wiki.");
        ImGui.SetTooltip(reward.Name + "\n" + hint);
        if (!ImGui.IsItemClicked(ImGuiMouseButton.Left)) return;
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

    private string Countdown(EventInfo e, DateTimeOffset now)
    {
        var future = e.Upcoming(now);
        var duration = (future ? e.StartsAt : e.EndsAt) - now;
        if (duration is not { } remaining || remaining <= TimeSpan.Zero) return T("Ended");
        return F(future ? "Starts in {0}d {1}h {2}m" : "Ends in {0}d {1}h {2}m", (int)remaining.TotalDays, remaining.Hours, remaining.Minutes);
    }

    private void Section(string title, System.Collections.Generic.IEnumerable<string> items)
    {
        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count == 0) return;
        ImGui.Spacing();
        ImGui.TextColored(Gold, T(title));
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
