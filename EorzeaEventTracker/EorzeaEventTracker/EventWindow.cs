using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace EorzeaEventTracker;

public sealed class EventWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool settingsVisible;
    private string urlBuffer;
    private string selected = "";
    private bool remainingOnly;
    private bool favoritesOnly;
    private bool showHidden;
    private DateTime mapErrorUntil;
    private EventInfo? localizedEvent;
    private static readonly Vector4 Teal = new(.49f, .69f, .62f, 1);
    private static readonly Vector4 Gold = new(.84f, .72f, .48f, 1);
    private static readonly Vector4 Muted = new(.61f, .61f, .59f, 1);
    private Localizer L => plugin.Locale;
    private string T(string text) => L.Text(text);
    private string E(string text) => L.Text(text, localizedEvent);
    private string F(string text, params object[] values) => L.Format(text, values);
    private float UiScale => AppearancePolicy.Scale(plugin.Settings);
    private float Opacity => AppearancePolicy.Opacity(plugin.Settings);
    private float S => ImGui.GetFontSize() / 17f;
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
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.065f, .063f, .060f, .98f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.10f, .095f, .080f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.19f, .17f, .12f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(.10f, .095f, .080f, Opacity));
    }

    public override void PostDraw() => ImGui.PopStyleColor(4);

    public override void Draw()
    {
        ImGui.SetWindowFontScale(UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.065f, .063f, .060f, .98f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.39f, .35f, .26f, .40f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.115f, .11f, .10f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.25f, .22f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.31f, .27f, .19f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.085f, .082f, .077f, .98f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.21f, .195f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.30f, .27f, .21f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.25f, .22f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.35f, .29f, .19f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.17f, .16f, .14f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.24f, .22f, .17f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.29f, .25f, .18f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.28f, .25f, .18f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.32f, .28f, .19f, 1));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.35f, .31f, .23f, .45f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, new Vector4(.115f, .11f, .10f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(.21f, .19f, .15f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.89f, .87f, .81f, 1));
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
        var events = plugin.Feed.Feed.Events.Where(e => (plugin.Settings.ShowUnverified || e.Verified) && (e.Active(now) || e.Upcoming(now)))
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
        var background = active ? new Vector4(.19f, .17f, .125f, 1)
            : hovered ? new Vector4(.15f, .14f, .12f, 1) : new Vector4(.105f, .10f, .09f, 1);
        background.W = Opacity;
        draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(background), 8 * S);
        draw.AddRect(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(active ? accent : new Vector4(.25f, .23f, .19f, .4f)), 8 * S);
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
            var (done, total) = RewardPreferences.Progress(def.Rewards, plugin.Settings.IgnoredRewards, plugin.Progress.Rewards);
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
        if (e.EndsAt.HasValue) ImGui.TextDisabled(F("Ends {0}", e.EndsAt.Value.ToLocalTime().ToString("f", L.Culture)));
        if (!e.Verified) ImGui.TextColored(Gold, T("Partial details — check the official page"));
        if (plugin.Progress.ReadyForReminders && EventCompletionPolicy.IsComplete(definition, plugin.Progress.Quests))
            ImGui.TextColored(Teal, T("Main objective completed"));
        if (!string.IsNullOrWhiteSpace(e.Description)) ImGui.TextWrapped(E(e.Description));
        if (ImGui.Button(T("Official event guide") + "###Official event guide")) OpenUrl(e.Url);
        if (DateTime.UtcNow < mapErrorUntil) ImGui.TextWrapped(T("Could not open the map. Log in and try again; this area may be unavailable."));
        ImGui.Spacing();
        if (ImGui.BeginTabBar("Event tabs"))
        {
            if (ImGui.BeginTabItem(T("Your progress") + "###Your progress"))
            {
                if (definition == null) ImGui.TextWrapped(T("Automatic tracking has not been mapped for this event yet."));
                else DrawProgress(definition);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(T("How to join") + "###How to join"))
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

    private void DrawProgress(TrackingDefinition definition)
    {
        ImGui.Spacing();
        ImGui.TextColored(Gold, T("QUEST JOURNAL"));
        foreach (var quest in definition.Quests)
        {
            ImGui.PushID((int)quest.Id);
            var known = plugin.Progress.Available && plugin.Progress.Quests.TryGetValue(quest.Id, out _);
            var done = known && plugin.Progress.Quests[quest.Id];
            ImGui.TextColored(done ? Teal : Muted, T(done ? "[DONE]" : known ? "[TODO]" : "[ ... ]"));
            ImGui.SameLine();
            ImGui.TextWrapped(E(quest.Name));
            DrawLocationButton(quest.LocationId);
            ImGui.PopID();
        }
        ImGui.TextWrapped(T("Quest completion does not mean all shop rewards are collected."));
        ImGui.Spacing();
        var (count, total) = RewardPreferences.Progress(definition.Rewards, plugin.Settings.IgnoredRewards, plugin.Progress.Rewards);
        ImGui.TextColored(Gold, F("REWARD COLLECTION   {0} / {1}", count, total));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(total == 0 ? 0 : (float)count / total, new Vector2(-1, 9 * S), "");
        ImGui.PopStyleColor();
        ImGui.Checkbox(T("Show remaining rewards only") + "###Show remaining rewards only", ref remainingOnly);
        ImGui.Checkbox(T("Favorites only") + "###FavoritesOnly", ref favoritesOnly);
        var favorites = definition.Rewards.Where(r => plugin.Settings.FavoriteRewards.Contains(r.Id) && !plugin.Settings.IgnoredRewards.Contains(r.Id)).ToList();
        if (favorites.Count > 0)
        {
            var confirmed = favorites.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
            ImGui.TextColored(Gold, F("FAVORITES   {0} / {1} obtained", confirmed, favorites.Count));
            DrawFavoriteBudget(definition);
        }
        if (!definition.Rewards.Any(r => (!favoritesOnly || plugin.Settings.FavoriteRewards.Contains(r.Id))
            && (!remainingOnly || (!plugin.Settings.IgnoredRewards.Contains(r.Id) && !plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed))))
            ImGui.TextWrapped(T("No rewards match these filters. Mark rewards as favorites or change the filters."));
        var columns = ImGui.GetContentRegionAvail().X >= 560 * S ? 2 : 1;
        if (ImGui.BeginTable("Rewards", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var reward in definition.Rewards)
            {
                var state = plugin.Progress.Rewards.GetValueOrDefault(reward.Id);
                if (remainingOnly && (state.Confirmed || plugin.Settings.IgnoredRewards.Contains(reward.Id))) continue;
                if (favoritesOnly && !plugin.Settings.FavoriteRewards.Contains(reward.Id)) continue;
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
        var pending = FavoriteBudget.Pending(definition.Rewards, plugin.Settings.FavoriteRewards,
            plugin.Settings.IgnoredRewards, plugin.Progress.Rewards);
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
        var ignored = plugin.Settings.IgnoredRewards.Contains(reward.Id);
        var favorite = plugin.Settings.FavoriteRewards.Contains(reward.Id);
        var width = ImGui.GetContentRegionAvail().X - padding * 2;
        var textWidth = Math.Max(50 * scale, width - 52 * scale);
        var titleHeight = ImGui.CalcTextSize(E(reward.Name), false, textWidth).Y;
        var headerHeight = Math.Max(42 * scale, titleHeight + (reward.Price > 0 ? ImGui.GetTextLineHeight() + gap : 0));
        var status = L.RewardLabel(progress);
        var statusHeight = ImGui.CalcTextSize(status, false, width).Y;
        var oneLine = ImGui.CalcTextSize(T("Favorite")).X + ImGui.CalcTextSize(T("Not interested")).X
            + ImGui.GetFrameHeight() * 2 + 24 * scale <= width;
        var controlsHeight = ImGui.GetFrameHeight() * (oneLine ? 1 : 2) + (oneLine ? 0 : gap) + ImGui.GetFrameHeight() + gap;
        var hasLocation = localizedEvent?.Locations.Any(l => l.Id == reward.LocationId) == true;
        var height = padding * 2 + headerHeight + statusHeight + controlsHeight + gap * 3
            + (hasLocation ? ImGui.GetTextLineHeight() + gap + ImGui.GetStyle().FramePadding.Y * 2 : 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8 * scale, gap));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (ignored ? .60f : 1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ignored ? new Vector4(.115f, .115f, .115f, Opacity) : new Vector4(.12f, .115f, .103f, Opacity));
        if (BeginScaledChild("Reward tile", new Vector2(0, height), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (favorite && !ignored)
            {
                var top = ImGui.GetWindowPos();
                ImGui.GetWindowDrawList().AddRectFilled(top + new Vector2(10 * scale, 0),
                    top + new Vector2(ImGui.GetWindowWidth() - 10 * scale, 2 * scale), ImGui.ColorConvertFloat4ToU32(Gold));
            }
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(reward.Icon));
            if (reward.Icon != 0 && icon.TryGetWrap(out var wrap, out _)) ImGui.Image(wrap.Handle, new Vector2(42 * scale));
            else ImGui.Dummy(new Vector2(42 * scale));
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextColored(ignored ? Muted : favorite ? Gold : new Vector4(.92f, .90f, .84f, 1), E(reward.Name));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{reward.Name}\nItem #{reward.Id}");
            if (reward.Price > 0) ImGui.TextColored(ignored ? Muted : Gold, $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}");
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.SetCursorPos(new Vector2(padding, padding + headerHeight + gap));
            ImGui.PushTextWrapPos(padding + width);
            ImGui.TextColored(!ignored && progress.Confirmed ? Teal : Muted, status);
            ImGui.PopTextWrapPos();
            if (ImGui.Checkbox(T("Favorite") + "###Favorite", ref favorite))
            {
                RewardPreferences.Favorite(plugin.Settings, reward.Id, favorite);
                plugin.Save();
            }
            if (oneLine) ImGui.SameLine();
            if (ImGui.Checkbox(T("Not interested") + "###NotInterested", ref ignored))
            {
                RewardPreferences.Ignore(plugin.Settings, reward.Id, ignored);
                plugin.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Excluded from progress and reminders. Uncheck to count this reward again."));
            var manuallyObtained = plugin.Progress.IsManuallyObtained(reward.Id);
            ImGui.BeginDisabled(!plugin.Progress.ReadyForReminders);
            if (ImGui.Checkbox(T("Already obtained (manual)") + "###ManuallyObtained", ref manuallyObtained))
                plugin.Progress.SetManuallyObtained(reward.Id, manuallyObtained);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Saved for this character. Clearing this mark preserves any automatic confirmation."));
            ImGui.EndDisabled();
            DrawLocationButton(reward.LocationId);
        }
        EndScaledChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
        ImGui.PopID();
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
        var padding = 18 * scale;
        var title = E(e.Title);
        float titleHeight;
        ImGui.SetWindowFontScale(UiScale * 1.2f);
        try { titleHeight = ImGui.CalcTextSize(title, false, width - padding * 2).Y; }
        finally { ImGui.SetWindowFontScale(UiScale); }
        var height = Math.Max(Math.Clamp(width * .31f, 150 * scale, 205 * scale),
            titleHeight + ImGui.GetTextLineHeightWithSpacing() * 2 + padding * 2);
        var start = ImGui.GetCursorScreenPos();
        var cursor = ImGui.GetCursorPos();
        var size = new Vector2(width, height);
        ImGui.Dummy(size);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + size, ImGui.ColorConvertFloat4ToU32(new Vector4(.13f, .12f, .095f, 1)), 6 * scale);
        var file = EventImagePath(e);
        if (file != null && Plugin.Textures.GetFromFile(file).TryGetWrap(out var wrap, out _))
        {
            var displayRatio = width / height;
            var imageRatio = (float)wrap.Width / wrap.Height;
            var visible = imageRatio > displayRatio ? new Vector2(displayRatio / imageRatio, 1) : new Vector2(1, imageRatio / displayRatio);
            draw.AddImage(wrap.Handle, start, start + size, (Vector2.One - visible) / 2, (Vector2.One + visible) / 2);
        }
        var shadeTop = ImGui.ColorConvertFloat4ToU32(new Vector4(.025f, .023f, .02f, .15f));
        var shadeBottom = ImGui.ColorConvertFloat4ToU32(new Vector4(.025f, .023f, .02f, .97f));
        draw.AddRectFilledMultiColor(start, start + size, shadeTop, shadeTop, shadeBottom, shadeBottom);
        draw.AddRect(start, start + size, ImGui.ColorConvertFloat4ToU32(new Vector4(Gold.X, Gold.Y, Gold.Z, .45f)), 4 * scale);
        var textY = height - padding - titleHeight - ImGui.GetTextLineHeightWithSpacing() * 2;
        ImGui.SetCursorPos(cursor + new Vector2(padding, textY));
        ImGui.TextColored(Gold, T(e.Active(now) ? "SEASONAL JOURNAL  /  LIVE" : "SEASONAL JOURNAL  /  UPCOMING"));
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.SetWindowFontScale(UiScale * 1.2f);
        try
        {
            ImGui.PushTextWrapPos(cursor.X + width - padding);
            ImGui.TextColored(new Vector4(.98f, .95f, .88f, 1), title);
            ImGui.PopTextWrapPos();
        }
        finally { ImGui.SetWindowFontScale(UiScale); }
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.TextColored(new Vector4(.88f, .86f, .80f, 1), Countdown(e, now));
        ImGui.SetCursorPos(cursor + new Vector2(0, height + 12 * scale));
    }

    private void DrawSettings()
    {
        var c = plugin.Settings;
        var language = L.IsRussian ? 1 : 0;
        if (ImGui.Combo(T("Language") + "###Language", ref language, "English\0Русский\0"))
        {
            c.Language = language == 1 ? "ru" : "en";
            plugin.Save();
        }
        ImGui.TextWrapped(T("Item and NPC names always stay in English. Interface and event descriptions use your selected language."));
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
        ImGui.InputText(T("HTTPS feed URL") + "###HTTPS feed URL", ref urlBuffer, 512);
        if (ImGui.Button(T("Save feed URL") + "###Save feed URL")) { c.FeedUrl = urlBuffer.Trim(); plugin.Save(); _ = plugin.Feed.RefreshAsync(c.FeedUrl); }
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
            !(uri.Host.Equals("finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase))) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Plugin.Log.Error(ex, "Could not open official page"); }
    }
    public void Dispose() { }
}
