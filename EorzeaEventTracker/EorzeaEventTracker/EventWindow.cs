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
    private static readonly Vector4 Teal = new(.40f, .88f, .77f, 1);
    private static readonly Vector4 Gold = new(1, .76f, .43f, 1);
    private static readonly Vector4 Muted = new(.59f, .65f, .73f, 1);
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
        ImGui.TextColored(Teal, "E O R Z E A   /   E V E N T S");
        ImGui.SameLine();
        ImGui.TextDisabled("Your seasonal adventure journal");
        ImGui.TextColored(Muted, plugin.Progress.Character);
        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh")) _ = plugin.Feed.RefreshAsync(plugin.Settings.FeedUrl);
        ImGui.SameLine();
        if (ImGui.SmallButton("Settings")) settingsVisible = !settingsVisible;
        if (settingsVisible) DrawSettings();
        ImGui.Separator();
        if (ImGui.BeginChild("Event rail", new Vector2(216 * S, -30 * S), true))
        {
            ImGui.TextColored(Muted, $"LIVE & UPCOMING  /  {events.Count}");
            foreach (var e in events)
            {
                ImGui.PushID(e.Id);
                if (ImGui.Selectable(e.Title, selected == e.Id, ImGuiSelectableFlags.None, new Vector2(0, 30 * S))) selected = e.Id;
                ImGui.TextColored(e.Active(now) ? Teal : Gold, e.Active(now) ? "LIVE NOW" : "UPCOMING");
                ImGui.TextDisabled(Countdown(e, now));
                if (plugin.Progress.Catalog.TryGetValue(e.Id, out var def))
                {
                    var done = def.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
                    ImGui.TextColored(Muted, $"{done} / {def.Rewards.Count} rewards confirmed");
                }
                ImGui.Separator();
                ImGui.PopID();
            }
            if (events.Count == 0) ImGui.TextWrapped("No current events in this feed. Check Lodestone for announcements.");
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("Event detail", new Vector2(0, -30 * S), true))
        {
            var current = events.FirstOrDefault(e => e.Id == selected);
            if (current != null) DrawEvent(current, now);
            else if (ImGui.Button("Open Lodestone")) OpenUrl("https://na.finalfantasyxiv.com/lodestone/topics/");
        }
        ImGui.EndChild();
        ImGui.TextColored(Muted, "Lodestone  /  " + plugin.Feed.Status);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tracking stays on this computer. Collection unlocks and observed rewards are separate from quest completion.");
    }

    private void DrawEvent(EventInfo e, DateTimeOffset now)
    {
        plugin.Progress.Catalog.TryGetValue(e.Id, out var definition);
        if (definition != null) DrawHero(definition);
        ImGui.TextColored(e.Active(now) ? Teal : Gold, e.Active(now) ? "SEASONAL JOURNAL  /  LIVE" : "SEASONAL JOURNAL  /  UPCOMING");
        ImGui.TextWrapped(e.Title);
        ImGui.TextColored(Gold, Countdown(e, now));
        if (e.EndsAt.HasValue) ImGui.TextDisabled("Ends " + e.EndsAt.Value.ToLocalTime().ToString("f"));
        if (!e.Verified) ImGui.TextColored(Gold, "Partial details — check the official page");
        if (!string.IsNullOrWhiteSpace(e.Description)) ImGui.TextWrapped(e.Description);
        if (ImGui.Button("Official event guide")) OpenUrl(e.Url);
        ImGui.Spacing();
        if (ImGui.BeginTabBar("Event tabs"))
        {
            if (ImGui.BeginTabItem("Your progress"))
            {
                if (definition == null) ImGui.TextWrapped("Automatic tracking has not been mapped for this event yet.");
                else DrawProgress(definition);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("How to join"))
            {
                Section("REQUIREMENTS", e.Requirements);
                Section("YOUR NEXT STEPS", e.Steps);
                Section("SOURCE NOTES", e.Notes);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Reward guide"))
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
        ImGui.TextColored(Teal, "QUEST JOURNAL");
        foreach (var quest in definition.Quests)
        {
            var known = plugin.Progress.Available && plugin.Progress.Quests.TryGetValue(quest.Id, out _);
            var done = known && plugin.Progress.Quests[quest.Id];
            ImGui.TextColored(done ? Teal : Muted, done ? "[DONE]" : known ? "[TODO]" : "[ ... ]");
            ImGui.SameLine();
            ImGui.TextWrapped(quest.Name);
        }
        ImGui.TextDisabled("Quest completion does not mean all shop rewards are collected.");
        ImGui.Spacing();
        var count = definition.Rewards.Count(r => plugin.Progress.Rewards.GetValueOrDefault(r.Id).Confirmed);
        ImGui.TextColored(Teal, $"REWARD COLLECTION   {count} / {definition.Rewards.Count}");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Teal);
        ImGui.ProgressBar(definition.Rewards.Count == 0 ? 0 : (float)count / definition.Rewards.Count, new Vector2(-1, 9 * S), "");
        ImGui.PopStyleColor();
        ImGui.Checkbox("Show remaining rewards only", ref remainingOnly);
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
        ImGui.TextColored(Muted, "HOW TRACKING WORKS");
        ImGui.TextWrapped("Unlocked = registered collection. In bags / equipped = currently found. Previously obtained = seen before on this character. Earned from quest = guaranteed quest reward.");
        ImGui.TextWrapped("Not detected does not mean never owned: retainers, housing, glamour dresser and armoire are not scanned. Items seen by this plugin are remembered per character.");
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
            ImGui.TextUnformatted(reward.Name);
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.TextColored(progress.Confirmed ? Teal : Muted, progress.Label);
            if (reward.Price > 0) ImGui.TextColored(Gold, $"{reward.Price:N0} MGF");
            if (ImGui.IsWindowHovered()) ImGui.SetTooltip($"{reward.Name}\n{progress.Label}\nItem #{reward.Id}");
        }
        ImGui.EndChild();
        ImGui.PopID();
    }

    private void DrawHero(TrackingDefinition definition)
    {
        var file = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "assets", definition.Asset);
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
        var notify = c.NotifyOnLogin; if (ImGui.Checkbox("Notify on login", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
        ImGui.SameLine();
        var open = c.OpenOnLogin; if (ImGui.Checkbox("Open on login", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
        var unverified = c.ShowUnverified; if (ImGui.Checkbox("Show partial event entries", ref unverified)) { c.ShowUnverified = unverified; plugin.Save(); }
        ImGui.InputText("HTTPS feed URL", ref urlBuffer, 512);
        if (ImGui.Button("Save feed URL")) { c.FeedUrl = urlBuffer.Trim(); plugin.Save(); _ = plugin.Feed.RefreshAsync(c.FeedUrl); }
        ImGui.TextWrapped("Leave URL empty for bundled / cached data. Character tracking works locally and refreshes every second.");
    }

    private static string Countdown(EventInfo e, DateTimeOffset now)
    {
        var future = e.Upcoming(now);
        var duration = (future ? e.StartsAt : e.EndsAt) - now;
        if (duration is not { } remaining || remaining <= TimeSpan.Zero) return "Ended";
        return $"{(future ? "Starts" : "Ends")} in {(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes}m";
    }

    private static void Section(string title, System.Collections.Generic.IEnumerable<string> items)
    {
        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count == 0) return;
        ImGui.Spacing();
        ImGui.TextColored(Teal, title);
        ImGui.PushTextWrapPos(0);
        foreach (var item in list) ImGui.BulletText(item);
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
