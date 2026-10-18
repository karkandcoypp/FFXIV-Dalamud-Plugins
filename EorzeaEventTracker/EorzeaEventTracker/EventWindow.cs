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
    private EventInfo? localizedEvent;
    private static readonly Vector4 Teal = new(.40f, .88f, .77f, 1);
    private static readonly Vector4 Gold = new(1, .76f, .43f, 1);
    private static readonly Vector4 Muted = new(.59f, .65f, .73f, 1);
    private Localizer L => plugin.Locale;
    private string T(string text) => L.Text(text);
    private string E(string text) => L.Text(text, localizedEvent);
    private string F(string text, params object[] values) => L.Format(text, values);
    private float S => ImGui.GetFontSize() / 17f;

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
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.075f, .09f, .12f, .96f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.13f, .23f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.18f, .36f, .38f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.12f, .29f, .29f, 1));
        try { DrawContent(); }
        finally { ImGui.PopStyleColor(4); ImGui.PopStyleVar(3); }
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
        if (ImGui.BeginChild("Event rail", new Vector2(216 * S, -30 * S), true))
        {
            ImGui.TextColored(Muted, F("LIVE & UPCOMING  /  {0}", events.Count));
            foreach (var e in events)
            {
                ImGui.PushID(e.Id);
                if (ImGui.Selectable(L.Text(e.Title, e) + "###Event", selected == e.Id, ImGuiSelectableFlags.None, new Vector2(0, 30 * S))) selected = e.Id;
                ImGui.TextColored(e.Active(now) ? Teal : Gold, T(e.Active(now) ? "LIVE NOW" : "UPCOMING"));
                ImGui.TextDisabled(Countdown(e, now));
                if (plugin.Progress.Catalog.TryGetValue(e.Id, out var def))
                {
                    var done = def.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
                    ImGui.TextColored(Muted, F("{0} / {1} rewards confirmed", done, def.Rewards.Count));
                }
                ImGui.Separator();
                ImGui.PopID();
            }
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
            var known = plugin.Progress.Available && plugin.Progress.Quests.TryGetValue(quest.Id, out _);
            var done = known && plugin.Progress.Quests[quest.Id];
            ImGui.TextColored(done ? Teal : Muted, T(done ? "[DONE]" : known ? "[TODO]" : "[ ... ]"));
            ImGui.SameLine();
            ImGui.TextWrapped(E(quest.Name));
        }
        ImGui.TextWrapped(T("Quest completion does not mean all shop rewards are collected."));
        ImGui.Spacing();
        var count = definition.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
        ImGui.TextColored(Teal, F("REWARD COLLECTION   {0} / {1}", count, definition.Rewards.Count));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(definition.Rewards.Count == 0 ? 0 : (float)count / definition.Rewards.Count, new Vector2(-1, 9 * S), "");
        ImGui.PopStyleColor();
        ImGui.Checkbox(T("Show remaining rewards only") + "###Show remaining rewards only", ref remainingOnly);
        var columns = ImGui.GetContentRegionAvail().X >= 560 * S ? 2 : 1;
        if (ImGui.BeginTable("Rewards", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var reward in definition.Rewards)
            {
                var state = plugin.Progress.Rewards.GetValueOrDefault(reward.Id);
                if (remainingOnly && state.Confirmed) continue;
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
        if (ImGui.BeginChild("Reward tile", new Vector2(0, 146 * S), true))
        {
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(reward.Icon));
            if (reward.Icon != 0 && icon.TryGetWrap(out var wrap, out _)) ImGui.Image(wrap.Handle, new Vector2(38 * S));
            else ImGui.Dummy(new Vector2(38 * S));
            ImGui.SameLine();
            ImGui.BeginGroup();
            var textWidth = Math.Max(70 * S, ImGui.GetContentRegionAvail().X);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.TextUnformatted(E(reward.Name));
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.TextColored(progress.Confirmed ? Teal : Muted, L.RewardLabel(progress));
            if (reward.Price > 0) ImGui.TextColored(Gold, $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}");
            if (ImGui.IsWindowHovered()) ImGui.SetTooltip($"{reward.Name}\n{L.RewardLabel(progress)}\nItem #{reward.Id}");
        }
        ImGui.EndChild();
        ImGui.PopID();
    }

    private void DrawHero(EventInfo e)
    {
        var file = plugin.Images.GetPath(e.ImageUrl);
        var asset = plugin.Progress.BundledAsset(e.Id);
        if (file == null && !string.IsNullOrEmpty(asset))
            file = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "assets", asset);
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
        var notify = c.NotifyOnLogin; if (ImGui.Checkbox(T("Notify on login") + "###Notify on login", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
        ImGui.SameLine();
        var open = c.OpenOnLogin; if (ImGui.Checkbox(T("Open on login") + "###Open on login", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
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
