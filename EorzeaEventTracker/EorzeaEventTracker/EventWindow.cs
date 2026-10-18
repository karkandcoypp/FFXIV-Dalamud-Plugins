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
    private DateTime mapErrorUntil;
    private EventInfo? localizedEvent;
    private static readonly Vector4 Teal = new(.40f, .88f, .77f, 1);
    private static readonly Vector4 Gold = new(1, .76f, .43f, 1);
    private static readonly Vector4 Muted = new(.59f, .65f, .73f, 1);
    private Localizer L => plugin.Locale;
    private string T(string text) => L.Text(text);
    private string E(string text) => L.Text(text, localizedEvent);
    private string F(string text, params object[] values) => L.Format(text, values);
    private float S => ImGui.GetFontSize() / 17f;
    internal void SelectEvent(string id) => selected = id;

    public EventWindow(Plugin plugin) : base("Eorzea Event Tracker###EETMain")
    {
        this.plugin = plugin;
        Size = new Vector2(1040, 790);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(800, 540), MaximumSize = new(1600, 1400) };
        urlBuffer = plugin.Settings.FeedUrl;
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.055f, .065f, .085f, .97f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.24f, .30f, .37f, .65f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.11f, .15f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.13f, .32f, .34f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.18f, .40f, .42f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Teal);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.075f, .09f, .12f, .96f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.13f, .23f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.18f, .36f, .38f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.12f, .29f, .29f, 1));
        try { DrawContent(); }
        finally { ImGui.PopStyleColor(10); ImGui.PopStyleVar(3); }
    }

    private void DrawContent()
    {
        var now = DateTimeOffset.UtcNow;
        var events = plugin.Feed.Feed.Events.Where(e => (plugin.Settings.ShowUnverified || e.Verified) && (e.Active(now) || e.Upcoming(now)))
            .OrderBy(e => e.Upcoming(now)).ThenBy(e => e.EndsAt).ToList();
        if (!events.Any(e => e.Id == selected)) selected = events.FirstOrDefault()?.Id ?? "";
        ImGui.TextColored(Teal, T("E O R Z E A   /   E V E N T S"));
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
        if (ImGui.BeginChild("Event rail", new Vector2(Math.Clamp(ImGui.GetContentRegionAvail().X * .27f, 224 * S, 290 * S), -30 * S), true))
        {
            ImGui.TextColored(Muted, F("LIVE & UPCOMING  /  {0}", events.Count));
            foreach (var e in events) DrawEventCard(e, now);
            if (events.Count == 0) ImGui.TextWrapped(T("No current events in this feed. Check Lodestone for announcements."));
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("Event detail", new Vector2(0, -30 * S), true))
        {
            var current = events.FirstOrDefault(e => e.Id == selected);
            if (current != null) DrawEvent(current, now);
            else if (ImGui.Button(T("Open Lodestone") + "###Open Lodestone")) OpenUrl("https://na.finalfantasyxiv.com/lodestone/topics/");
        }
        ImGui.EndChild();
        ImGui.TextColored(Muted, "Lodestone  /  " + L.FeedStatus(plugin.Feed.Status, plugin.Feed.UpdatedAt));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("Tracking stays on this computer. Collection unlocks and observed rewards are separate from quest completion."));
    }

    private void DrawEventCard(EventInfo e, DateTimeOffset now)
    {
        ImGui.PushID(e.Id);
        var active = selected == e.Id;
        var accent = e.Active(now) ? Teal : Gold;
        var width = ImGui.GetContentRegionAvail().X;
        var title = L.Text(e.Title, e);
        var padding = 12 * S;
        var titleHeight = ImGui.CalcTextSize(title, false, width - padding * 2).Y;
        var image = EventImagePath(e);
        var artwork = image == null ? null : Plugin.Textures.GetFromFile(image);
        var imageHeight = artwork == null ? 0 : 66 * S;
        var height = imageHeight + titleHeight + ImGui.GetTextLineHeightWithSpacing() * 3 + 44 * S;
        var start = ImGui.GetCursorScreenPos();
        var cursor = ImGui.GetCursorPos();
        if (ImGui.InvisibleButton("Select event", new Vector2(width, height))) selected = e.Id;
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var draw = ImGui.GetWindowDrawList();
        var background = active ? new Vector4(.10f, .22f, .25f, 1)
            : hovered ? new Vector4(.12f, .16f, .21f, 1) : new Vector4(.085f, .11f, .15f, 1);
        draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(background), 8 * S);
        draw.AddRect(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(active ? accent : new Vector4(.24f, .30f, .37f, .6f)), 8 * S);
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
        ImGui.TextColored(active ? new Vector4(.94f, 1, 1, 1) : new Vector4(.86f, .90f, .96f, 1), title);
        ImGui.PopTextWrapPos();
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.TextColored(accent, T(e.Active(now) ? "LIVE NOW" : "UPCOMING"));
        ImGui.SetCursorPosX(cursor.X + padding);
        ImGui.TextColored(Muted, Countdown(e, now));
        if (plugin.Progress.Catalog.TryGetValue(e.Id, out var def))
        {
            var done = def.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
            ImGui.SetCursorPosX(cursor.X + padding);
            ImGui.TextColored(Muted, F("{0} / {1} rewards confirmed", done, def.Rewards.Count));
            var bar = start + new Vector2(padding, height - 12 * S);
            var barWidth = width - padding * 2;
            draw.AddRectFilled(bar, bar + new Vector2(barWidth, 3 * S), ImGui.ColorConvertFloat4ToU32(new Vector4(.20f, .26f, .31f, 1)), 2 * S);
            if (def.Rewards.Count > 0 && done > 0)
                draw.AddRectFilled(bar, bar + new Vector2(barWidth * done / def.Rewards.Count, 3 * S), ImGui.ColorConvertFloat4ToU32(accent), 2 * S);
        }
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
        DrawHero(e);
        ImGui.TextColored(e.Active(now) ? Teal : Gold, T(e.Active(now) ? "SEASONAL JOURNAL  /  LIVE" : "SEASONAL JOURNAL  /  UPCOMING"));
        ImGui.TextWrapped(E(e.Title));
        ImGui.TextColored(Gold, Countdown(e, now));
        if (e.EndsAt.HasValue) ImGui.TextDisabled(F("Ends {0}", e.EndsAt.Value.ToLocalTime().ToString("f", L.Culture)));
        if (!e.Verified) ImGui.TextColored(Gold, T("Partial details — check the official page"));
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
                    ImGui.TextColored(Teal, T("EVENT NPCs"));
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
        ImGui.TextColored(Teal, T("QUEST JOURNAL"));
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
        var count = definition.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
        ImGui.TextColored(Teal, F("REWARD COLLECTION   {0} / {1}", count, definition.Rewards.Count));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(definition.Rewards.Count == 0 ? 0 : (float)count / definition.Rewards.Count, new Vector2(-1, 9 * S), "");
        ImGui.PopStyleColor();
        ImGui.Checkbox(T("Show remaining rewards only") + "###Show remaining rewards only", ref remainingOnly);
        ImGui.Checkbox(T("Favorites only") + "###FavoritesOnly", ref favoritesOnly);
        var favorites = definition.Rewards.Where(r => plugin.Settings.FavoriteRewards.Contains(r.Id)).ToList();
        if (favorites.Count > 0)
        {
            var confirmed = favorites.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
            ImGui.TextColored(Gold, F("FAVORITES   {0} / {1} obtained", confirmed, favorites.Count));
        }
        if (!definition.Rewards.Any(r => (!favoritesOnly || plugin.Settings.FavoriteRewards.Contains(r.Id))
            && (!remainingOnly || !plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed)))
            ImGui.TextWrapped(T("No rewards match these filters. Mark rewards as favorites or change the filters."));
        var columns = ImGui.GetContentRegionAvail().X >= 560 * S ? 2 : 1;
        if (ImGui.BeginTable("Rewards", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var reward in definition.Rewards)
            {
                var state = plugin.Progress.Rewards.GetValueOrDefault(reward.Id);
                if (remainingOnly && state.Confirmed) continue;
                if (favoritesOnly && !plugin.Settings.FavoriteRewards.Contains(reward.Id)) continue;
                ImGui.TableNextColumn();
                DrawReward(reward, state);
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.TextColored(Muted, T("HOW TRACKING WORKS"));
        ImGui.TextWrapped(T("Unlocked = registered collection. In bags / equipped = currently found. Previously obtained = seen before on this character. Earned from quest = guaranteed quest reward."));
        ImGui.TextWrapped(T("Not detected does not mean never owned: retainers, housing, glamour dresser and armoire are not scanned. Items seen by this plugin are remembered per character."));
    }

    private void DrawReward(TrackedReward reward, RewardProgress progress)
    {
        ImGui.PushID((int)reward.Id);
        if (ImGui.BeginChild("Reward tile", new Vector2(0, 210 * S), true))
        {
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(reward.Icon));
            if (reward.Icon != 0 && icon.TryGetWrap(out var wrap, out _)) ImGui.Image(wrap.Handle, new Vector2(38 * S));
            else ImGui.Dummy(new Vector2(38 * S));
            ImGui.SameLine();
            ImGui.BeginGroup();
            var textWidth = Math.Max(70 * S, ImGui.GetContentRegionAvail().X);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextUnformatted(E(reward.Name));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{reward.Name}\nItem #{reward.Id}");
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.TextColored(progress.Confirmed ? Teal : Muted, L.RewardLabel(progress));
            if (reward.Price > 0) ImGui.TextColored(Gold, $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}");
            var favorite = plugin.Settings.FavoriteRewards.Contains(reward.Id);
            if (ImGui.Checkbox(T("Favorite") + "###Favorite", ref favorite))
            {
                if (favorite) plugin.Settings.FavoriteRewards.Add(reward.Id);
                else plugin.Settings.FavoriteRewards.Remove(reward.Id);
                plugin.Save();
            }
            DrawLocationButton(reward.LocationId);
        }
        ImGui.EndChild();
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

    private void DrawHero(EventInfo e)
    {
        var file = EventImagePath(e);
        if (file == null) return;
        var shared = Plugin.Textures.GetFromFile(file);
        var width = ImGui.GetContentRegionAvail().X;
        var height = Math.Clamp(width * .34f, 120 * S, 200 * S);
        if (shared.TryGetWrap(out var wrap, out _))
        {
            var displayRatio = width / height;
            var imageRatio = (float)wrap.Width / wrap.Height;
            var visible = imageRatio > displayRatio ? new Vector2(displayRatio / imageRatio, 1) : new Vector2(1, imageRatio / displayRatio);
            ImGui.Image(wrap.Handle, new Vector2(width, height), (Vector2.One - visible) / 2, (Vector2.One + visible) / 2);
        }
        else ImGui.Dummy(new Vector2(width, height));
        ImGui.Spacing();
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
        ImGui.TextWrapped(T("Names use the event's translations when available. Original item names remain in tooltips."));
        var widget = c.ShowCompactWidget;
        if (ImGui.Checkbox(T("Show compact event widget") + "###CompactWidget", ref widget)) plugin.SetWidgetVisible(widget);
        var notify = c.NotifyOnLogin; if (ImGui.Checkbox(T("Notify on login") + "###Notify on login", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
        ImGui.SameLine();
        var open = c.OpenOnLogin; if (ImGui.Checkbox(T("Open on login") + "###Open on login", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
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
        ImGui.TextColored(Teal, T(title));
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
