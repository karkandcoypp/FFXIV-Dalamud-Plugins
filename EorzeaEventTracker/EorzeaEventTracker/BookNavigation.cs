using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EorzeaEventTracker;

public sealed partial class EventWindow
{
    private int contentsPage;
    private int sheetPage;
    private int sheetSection;
    private int npcIndex;
    private string previousEvent = "";
    private bool revealContentsSelection;
    private DateTime flipStarted;
    private const ImGuiWindowFlags PageFlags = ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    private float FooterHeight => ImGui.GetFrameHeightWithSpacing() + 10 * S;

    private void DrawBookContent()
    {
        var now = DateTimeOffset.UtcNow;
        var events = plugin.Feed.Feed.Events.Where(e => (plugin.Settings.ShowUnverified || e.Verified) && (e.Active(now) || e.Upcoming(now)))
            .OrderBy(e => e.Upcoming(now)).ThenBy(e => e.EndsAt).ToList();
        if (!events.Any(e => e.Id == selected)) selected = events.FirstOrDefault()?.Id ?? "";
        if (selected != previousEvent)
        {
            previousEvent = selected; revealContentsSelection = true; sheetPage = 0; sheetSection = 0; npcIndex = 0; flipStarted = DateTime.UtcNow;
        }
        ImGui.TextColored(Gold, T("E O R Z E A   /   E V E N T S"));
        ImGui.SameLine(); ImGui.TextDisabled(T(plugin.Progress.Character));
        if (ImGui.SmallButton(T("Refresh") + "###Refresh")) { plugin.Images.RetryFailed(); _ = plugin.Feed.RefreshAsync(plugin.Settings.FeedUrl); }
        ImGui.SameLine();
        if (ImGui.SmallButton(T("Settings") + "###Settings")) { settingsVisible = !settingsVisible; sheetPage = 0; flipStarted = DateTime.UtcNow; }
        ImGui.Separator();
        var railWidth = Math.Clamp(ImGui.GetContentRegionAvail().X * .32f, 260 * S, 360 * S);
        var gutter = 18 * S;
        DrawBookBackground(railWidth, gutter);
        // Margins are part of the physical pages; content and footers stay inside them.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(42 * S, 44 * S));
        if (BeginScaledChild("Contents page", new Vector2(railWidth, -28 * S), false, PageFlags)) DrawContents(events, now);
        EndScaledChild();
        ImGui.SameLine(0, gutter);
        var rightWidth = ImGui.GetContentRegionAvail().X;
        var outerMargin = Math.Max(52 * S, rightWidth * .14f - 42 * S);
        if (BeginScaledChild("Book sheet", new Vector2(Math.Max(100 * S, rightWidth - outerMargin), -28 * S), false, PageFlags))
        {
            var current = events.FirstOrDefault(e => e.Id == selected);
            localizedEvent = current;
            try
            {
                if (settingsVisible) DrawSettingsSheet();
                else if (current != null) DrawSheet(current, now);
                else DrawTextSheet(new() { (T("No current events in this feed. Check Lodestone for announcements."), false) });
            }
            finally { localizedEvent = null; }
        }
        EndScaledChild();
        ImGui.PopStyleVar();
        ImGui.TextColored(Muted, "Lodestone / " + L.FeedStatus(plugin.Feed.Status, plugin.Feed.UpdatedAt));
    }

    private void DrawContents(List<EventInfo> events, DateTimeOffset now)
    {
        ImGui.TextColored(Gold, F("LIVE & UPCOMING  /  {0}", events.Count));
        var start = ImGui.GetCursorPos();
        var available = ImGui.GetContentRegionAvail();
        var heights = events.Select(e =>
        {
            var image = EventImagePath(e);
            return (image == null ? 0 : 66 * S) + ImGui.CalcTextSize(L.Text(e.Title, e), false, available.X - 24 * S).Y
                + ImGui.GetTextLineHeightWithSpacing() * 3 + 54 * S;
        }).ToList();
        var pages = BookPagination.Pack(heights, available.Y - FooterHeight);
        if (revealContentsSelection)
        {
            var index = events.FindIndex(e => e.Id == selected);
            var found = pages.FindIndex(p => index >= p.Start && index < p.Start + p.Count);
            if (found >= 0) contentsPage = found;
            revealContentsSelection = false;
        }
        contentsPage = Math.Clamp(contentsPage, 0, pages.Count - 1);
        var page = pages[contentsPage];
        for (var i = page.Start; i < page.Start + page.Count; i++) DrawEventCard(events[i], now);
        if (events.Count == 0) ImGui.TextWrapped(T("No active events right now."));
        DrawFlipShadow(start, new Vector2(available.X, Math.Max(0, available.Y - FooterHeight)));
        DrawPageFooter(ref contentsPage, pages.Count, start + new Vector2(0, available.Y - FooterHeight), "Contents", true);
    }

    private void DrawBookmarks()
    {
        var labels = new[] { T("Overview"), T("How to join"), T("QUEST JOURNAL"), T("Reward guide") };
        for (var i = 0; i < labels.Length; i++)
        {
            var width = ImGui.CalcTextSize(labels[i]).X + ImGui.GetStyle().FramePadding.X * 2;
            if (i > 0 && ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - ImGui.GetItemRectMax().X > width + 8 * S) ImGui.SameLine();
            var active = i == sheetSection;
            if (active)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.46f, .18f, .13f, 1));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.98f, .94f, .83f, 1));
            }
            if (ImGui.SmallButton(labels[i] + "###Bookmark" + i)) { sheetSection = i; sheetPage = 0; flipStarted = DateTime.UtcNow; }
            if (active) ImGui.PopStyleColor(2);
        }
        ImGui.Separator();
    }

    private void DrawSheet(EventInfo e, DateTimeOffset now)
    {
        DrawBookmarks();
        if (sheetSection == 0)
        {
            var paragraphs = new List<(string, bool)>();
            if (e.EndsAt.HasValue) paragraphs.Add((F("Ends {0}", e.EndsAt.Value.ToLocalTime().ToString("f", L.Culture)), false));
            plugin.Progress.Catalog.TryGetValue(e.Id, out var definition);
            if (plugin.Progress.ReadyForReminders && EventCompletionPolicy.IsComplete(definition, plugin.Progress.Quests)) paragraphs.Add((T("Main objective completed"), true));
            if (!e.Verified) paragraphs.Add((T("Partial details — check the official page"), true));
            paragraphs.Add((E(e.Description), false));
            AddSection(paragraphs, "SOURCE NOTES", e.Notes);
            DrawTextSheet(paragraphs, e, now);
        }
        else if (sheetSection == 1)
        {
            DrawDestinationPicker(e);
            var paragraphs = new List<(string, bool)>();
            AddSection(paragraphs, "REQUIREMENTS", e.Requirements);
            AddSection(paragraphs, "YOUR NEXT STEPS", e.Steps);
            DrawTextSheet(paragraphs);
        }
        else if (sheetSection == 2)
        {
            DrawDestinationPicker(e);
            var paragraphs = new List<(string, bool)>();
            if (plugin.Progress.Catalog.TryGetValue(e.Id, out var definition))
                foreach (var quest in definition.Quests)
                {
                    var known = plugin.Progress.ReadyForReminders && plugin.Progress.Quests.TryGetValue(quest.Id, out _);
                    var done = known && plugin.Progress.Quests[quest.Id];
                    paragraphs.Add((T(done ? "[DONE]" : known ? "[TODO]" : "[ ... ]") + "  " + E(quest.Name), true));
                }
            else paragraphs.Add((T("Automatic tracking has not been mapped for this event yet."), false));
            paragraphs.Add((T("Quest completion does not mean all shop rewards are collected."), false));
            DrawTextSheet(paragraphs);
        }
        else DrawRewardSheet(e);
    }

    private void DrawDestinationPicker(EventInfo e)
    {
        if (e.Locations.Count == 0) return;
        npcIndex = Math.Clamp(npcIndex, 0, e.Locations.Count - 1);
        var labels = string.Join('\0', e.Locations.Select(l => E(l.Npc))) + '\0';
        ImGui.SetNextItemWidth(Math.Max(70 * S, ImGui.GetContentRegionAvail().X - 100 * S));
        ImGui.Combo("###Destination", ref npcIndex, labels);
        ImGui.SameLine(); DrawLocationButton(e.Locations[npcIndex].Id);
    }

    private void AddSection(List<(string, bool)> paragraphs, string title, IEnumerable<string> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (list.Count == 0) return;
        paragraphs.Add((T(title), true));
        foreach (var value in list) paragraphs.Add(("- " + E(value), false));
    }

    private void DrawTextSheet(List<(string Text, bool Heading)> paragraphs, EventInfo? cover = null, DateTimeOffset? coverTime = null)
    {
        if (DateTime.UtcNow < mapErrorUntil) paragraphs.Insert(0, (T("Could not open the map. Log in and try again; this area may be unavailable."), false));
        var start = ImGui.GetCursorPos();
        var available = ImGui.GetContentRegionAvail();
        var lines = new List<(string Text, bool Heading)>();
        foreach (var paragraph in paragraphs)
        {
            foreach (var line in BookPagination.Wrap(paragraph.Text, Math.Max(20, available.X - 3 * S), text => ImGui.CalcTextSize(text).X)) lines.Add((line, paragraph.Heading));
            lines.Add(("", false));
        }
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var capacity = Math.Max(1, (int)((available.Y - FooterHeight - 4 * S) / lineHeight));
        var prefix = cover == null ? 0 : 1;
        var pageCount = Math.Max(1, (lines.Count + capacity - 1) / capacity) + prefix;
        sheetPage = Math.Clamp(sheetPage, 0, pageCount - 1);
        if (cover != null && sheetPage == 0)
        {
            var now = coverTime ?? DateTimeOffset.UtcNow;
            if (available.Y > 320 * S) DrawHero(cover, now);
            else { ImGui.TextWrapped(E(cover.Title)); ImGui.TextColored(Gold, Countdown(cover, now)); }
            if (ImGui.SmallButton(T("Official event guide") + "###OfficialGuide")) OpenUrl(cover.Url);
            if (ImGui.GetContentRegionAvail().Y - FooterHeight > 65 * S) ImGui.TextWrapped(T("Turn the page for event details."));
        }
        else foreach (var line in lines.Skip((sheetPage - prefix) * capacity).Take(capacity))
        {
            if (line.Text.Length == 0) ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight()));
            else ImGui.TextColored(line.Heading ? Gold : new Vector4(.20f, .15f, .105f, 1), line.Text);
        }
        DrawFlipShadow(start, new Vector2(available.X, Math.Max(0, available.Y - FooterHeight)));
        DrawPageFooter(ref sheetPage, pageCount, start + new Vector2(0, available.Y - FooterHeight), "Text", true);
    }

    private void DrawRewardSheet(EventInfo e)
    {
        if (!plugin.Progress.Catalog.TryGetValue(e.Id, out var definition))
        {
            var paragraphs = new List<(string, bool)>(); AddSection(paragraphs, "REWARDS & PRICES", e.Rewards); DrawTextSheet(paragraphs); return;
        }
        var (done, total) = RewardPreferences.Progress(definition.Rewards, plugin.Settings.IgnoredRewards, plugin.Progress.Rewards);
        ImGui.TextColored(Teal, F("REWARD COLLECTION   {0} / {1}", done, total));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(total == 0 ? 0 : (float)done / total, new Vector2(-1, 5 * S), "");
        ImGui.PopStyleColor();
        WrappedCheckbox(T("Show remaining rewards only"), "Remaining", ref remainingOnly);
        WrappedCheckbox(T("Favorites only"), "Favorites", ref favoritesOnly);
        var rewards = definition.Rewards.Where(r => (!favoritesOnly || plugin.Settings.FavoriteRewards.Contains(r.Id))
            && (!remainingOnly || (!plugin.Settings.IgnoredRewards.Contains(r.Id) && !plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed))).ToList();
        var start = ImGui.GetCursorPos(); var available = ImGui.GetContentRegionAvail();
        var budget = Math.Max(1, available.Y - FooterHeight - 8 * S);
        var columns = available.X >= 520 * S ? 2 : 1;
        var cellWidth = available.X / columns - ImGui.GetStyle().CellPadding.X * 2 - 4 * S;
        var maxCardHeight = Math.Max(1, budget - ImGui.GetStyle().CellPadding.Y * 2 - 5 * S);
        var compact = rewards.Any(r => MeasureReward(r, plugin.Progress.Rewards.GetValueOrDefault(r.Id), cellWidth, false).Height > maxCardHeight);
        var heights = new List<float>();
        for (var i = 0; i < rewards.Count; i += columns)
            heights.Add(rewards.Skip(i).Take(columns).Max(r => Math.Min(maxCardHeight, MeasureReward(r, plugin.Progress.Rewards.GetValueOrDefault(r.Id), cellWidth, compact).Height))
                + ImGui.GetStyle().CellPadding.Y * 2 + 5 * S);
        var pages = BookPagination.Pack(heights, budget);
        sheetPage = Math.Clamp(sheetPage, 0, pages.Count - 1);
        var page = pages[sheetPage];
        if (ImGui.BeginTable("Reward leaves", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var reward in rewards.Skip(page.Start * columns).Take(page.Count * columns))
            {
                ImGui.TableNextColumn();
                var state = plugin.Progress.Rewards.GetValueOrDefault(reward.Id);
                if (MeasureReward(reward, state, cellWidth, compact).Height <= maxCardHeight) DrawReward(reward, state, compact);
                else DrawSmallRewardLeaf(reward, state, maxCardHeight);
            }
            ImGui.EndTable();
        }
        if (rewards.Count == 0) ImGui.TextWrapped(T("No rewards match these filters. Mark rewards as favorites or change the filters."));
        DrawFlipShadow(start, new Vector2(available.X, budget));
        DrawPageFooter(ref sheetPage, pages.Count, start + new Vector2(0, available.Y - FooterHeight), "Rewards", true);
    }

    private void DrawSmallRewardLeaf(TrackedReward reward, RewardProgress state, float height)
    {
        ImGui.PushID((int)reward.Id);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8 * S));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.98f, .95f, .86f, .86f * Opacity));
        if (BeginScaledChild("Small reward leaf", new Vector2(0, height), false, PageFlags))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var lines = BookPagination.Wrap(E(reward.Name), width, text => ImGui.CalcTextSize(text).X);
            var capacity = Math.Max(1, (int)((height - 24 * S - ImGui.GetFrameHeightWithSpacing()) / ImGui.GetTextLineHeightWithSpacing()));
            for (var i = 0; i < Math.Min(capacity, lines.Count); i++)
            {
                ImGui.TextUnformatted(lines[i] + (i == capacity - 1 && lines.Count > capacity ? "..." : ""));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(E(reward.Name));
            }
            ImGui.SetCursorPosY(height - ImGui.GetFrameHeight() - 8 * S);
            if (ImGui.SmallButton(T("Reward actions") + "###LeafActions")) ImGui.OpenPopup("LeafActions");
            if (ImGui.IsPopupOpen("LeafActions"))
                ImGui.SetNextWindowSizeConstraints(new Vector2(200 * S, 0), new Vector2(Math.Min(450 * S, ImGui.GetMainViewport().WorkSize.X - 24), ImGui.GetMainViewport().WorkSize.Y * .8f));
            if (ImGui.BeginPopup("LeafActions"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Min(380 * S, ImGui.GetMainViewport().WorkSize.X * .6f));
                ImGui.TextUnformatted(E(reward.Name));
                ImGui.TextUnformatted(L.RewardLabel(state));
                if (reward.Price > 0) ImGui.TextUnformatted($"{reward.Price} {E(reward.Currency)}");
                ImGui.PopTextWrapPos();
                DrawRewardControls(reward, false);
                ImGui.EndPopup();
            }
        }
        EndScaledChild(); ImGui.PopStyleColor(); ImGui.PopStyleVar(); ImGui.PopID();
    }

    private void DrawPageFooter(ref int page, int count, Vector2 position, string id, bool animate)
    {
        ImGui.PushID(id); ImGui.SetCursorPos(position); ImGui.Separator();
        var previous = page;
        ImGui.BeginDisabled(page <= 0); if (ImGui.SmallButton("<###Previous")) page--; ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.TextColored(Muted, F("Page {0} / {1}", page + 1, count));
        ImGui.SameLine(); ImGui.BeginDisabled(page >= count - 1); if (ImGui.SmallButton(">###Next")) page++; ImGui.EndDisabled();
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0) page = Math.Clamp(page + (wheel < 0 ? 1 : -1), 0, count - 1);
        }
        if (previous != page && animate) flipStarted = DateTime.UtcNow;
        ImGui.PopID();
    }

    private void DrawFlipShadow(Vector2 localStart, Vector2 size)
    {
        var elapsed = (DateTime.UtcNow - flipStarted).TotalSeconds;
        if (elapsed < 0 || elapsed > .24) return;
        var start = ImGui.GetWindowPos() + localStart;
        var x = start.X + size.X * (1 - (float)(elapsed / .24));
        var draw = ImGui.GetWindowDrawList();
        var clear = ImGui.ColorConvertFloat4ToU32(new Vector4(.25f, .15f, .07f, 0));
        var shade = ImGui.ColorConvertFloat4ToU32(new Vector4(.25f, .15f, .07f, .12f));
        draw.PushClipRect(start, start + size, true);
        draw.AddRectFilledMultiColor(new Vector2(x - 30 * S, start.Y), new Vector2(x + 3 * S, start.Y + size.Y), clear, shade, shade, clear);
        draw.PopClipRect();
    }

    private bool WrappedCheckbox(string label, string id, ref bool value)
    {
        var changed = ImGui.Checkbox("###" + id, ref value);
        ImGui.SameLine();
        ImGui.TextWrapped(label);
        return changed;
    }

    private void DrawSettingsSheet()
    {
        ImGui.TextColored(Gold, T("Settings"));
        var start = ImGui.GetCursorPos(); var available = ImGui.GetContentRegionAvail();
        sheetPage = Math.Clamp(sheetPage, 0, 4);
        var c = plugin.Settings;
        if (sheetPage == 0)
        {
            var language = L.IsRussian ? 1 : 0;
            ImGui.TextUnformatted(T("Language")); ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("###Language", ref language, "English\0Русский\0")) { c.Language = language == 1 ? "ru" : "en"; plugin.Save(); }
            ImGui.TextWrapped(T("Names use the event's translations when available. Original item names remain in tooltips."));
        }
        else if (sheetPage == 1)
        {
            var scale = AppearancePolicy.Scale(c);
            ImGui.TextUnformatted(T("Interface scale")); ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("###Scale", ref scale, .75f, 1.75f, "%.2fx")) c.UiScale = scale;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
            var opacity = Opacity;
            ImGui.TextUnformatted(T("Background opacity")); ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("###Opacity", ref opacity, .4f, 1, "%.2f")) c.BackgroundOpacity = opacity;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
            if (UiScale + .01f < scale && ImGui.IsItemHovered()) ImGui.SetTooltip(T("Scale is limited to keep the book on screen."));
            if (ImGui.SmallButton(T("Reset appearance") + "###Reset")) { c.UiScale = 1; c.BackgroundOpacity = 1; plugin.Save(); }
        }
        else if (sheetPage == 2)
        {
            var widget = c.ShowCompactWidget;
            if (WrappedCheckbox(T("Show compact event widget"), "Widget", ref widget)) plugin.SetWidgetVisible(widget);
            var notify = c.NotifyOnLogin; if (WrappedCheckbox(T("Notify on login"), "Notify", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
            var open = c.OpenOnLogin; if (WrappedCheckbox(T("Open on login"), "Open", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
            var partial = c.ShowUnverified; if (WrappedCheckbox(T("Show partial event entries"), "Partial", ref partial)) { c.ShowUnverified = partial; plugin.Save(); }
        }
        else if (sheetPage == 3)
        {
            var notify = c.NotifyFavoriteDeadlines;
            if (WrappedCheckbox(T("Remind me about favorite rewards before events end"), "Deadline", ref notify)) { c.NotifyFavoriteDeadlines = notify; plugin.Save(); }
            ImGui.TextWrapped(T("Reminders are sent once at 3 days and 24 hours for unfinished favorites."));
        }
        else
        {
            ImGui.TextColored(Gold, T("HTTPS feed URL")); ImGui.SetNextItemWidth(-1);
            ImGui.InputText("###FeedUrl", ref urlBuffer, 512);
            if (ImGui.Button(T("Save feed URL") + "###SaveFeed")) { c.FeedUrl = urlBuffer.Trim(); plugin.Save(); _ = plugin.Feed.RefreshAsync(c.FeedUrl); }
            ImGui.TextWrapped(T("Use HTTPS; leave empty for cached event data."));
        }
        DrawFlipShadow(start, new Vector2(available.X, Math.Max(0, available.Y - FooterHeight)));
        DrawPageFooter(ref sheetPage, 5, start + new Vector2(0, available.Y - FooterHeight), "Settings", true);
    }
}
