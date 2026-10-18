using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace EorzeaEventTracker;

public sealed class EventWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool settingsVisible;
    private string urlBuffer;
    public EventWindow(Plugin plugin) : base("Eorzea Event Tracker###EETMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(470, 340), MaximumSize = new Vector2(1100, 1000) };
        urlBuffer = plugin.Settings.FeedUrl;
    }

    public override void Draw()
    {
        var now = DateTimeOffset.UtcNow;
        ImGui.TextUnformatted("Official Lodestone events");
        ImGui.SameLine();
        if (ImGui.Button("Refresh")) _ = plugin.Feed.RefreshAsync(plugin.Settings.FeedUrl);
        ImGui.SameLine();
        if (ImGui.Button("Settings")) settingsVisible = !settingsVisible;
        ImGui.TextDisabled(plugin.Feed.Status);
        if (settingsVisible) DrawSettings();
        ImGui.Separator();

        var all = plugin.Feed.Feed.Events.Where(e => plugin.Settings.ShowUnverified || e.Verified).ToList();
        var active = all.Where(e => e.Active(now)).OrderBy(e => e.EndsAt).ToList();
        var upcoming = all.Where(e => e.Upcoming(now)).OrderBy(e => e.StartsAt).ToList();
        ImGui.Text($"Active: {active.Count} | Upcoming: {upcoming.Count}");
        if (active.Count == 0) ImGui.TextDisabled("No active events in the current feed. Check the official site for current events.");
        foreach (var item in active) DrawEvent(item, now);
        if (ImGui.CollapsingHeader($"Upcoming events ({upcoming.Count})"))
            foreach (var item in upcoming) DrawEvent(item, now);
    }

    private void DrawSettings()
    {
        var c = plugin.Settings;
        var notify = c.NotifyOnLogin; if (ImGui.Checkbox("Notify on login", ref notify)) { c.NotifyOnLogin = notify; plugin.Save(); }
        var open = c.OpenOnLogin; if (ImGui.Checkbox("Open window on login", ref open)) { c.OpenOnLogin = open; plugin.Save(); }
        var unverified = c.ShowUnverified; if (ImGui.Checkbox("Show unverified entries", ref unverified)) { c.ShowUnverified = unverified; plugin.Save(); }
        ImGui.InputText("HTTPS feed URL", ref urlBuffer, 512);
        if (ImGui.Button("Save feed URL"))
        {
            c.FeedUrl = urlBuffer.Trim();
            plugin.Save();
            _ = plugin.Feed.RefreshAsync(c.FeedUrl);
        }
        ImGui.TextWrapped("Leave URL empty to use bundled/cache data. Marked completion is manual in this MVP.");
        ImGui.Separator();
    }

    private void DrawEvent(EventInfo e, DateTimeOffset now)
    {
        if (!ImGui.CollapsingHeader($"{e.Title}##{e.Id}", ImGuiTreeNodeFlags.DefaultOpen)) return;
        if (!e.Verified) ImGui.TextDisabled("UNVERIFIED / PARTIAL DATA — open official page");
        if (e.EndsAt.HasValue)
        {
            var remaining = e.EndsAt.Value - now;
            ImGui.Text(remaining > TimeSpan.Zero ? $"Ends in: {(int)remaining.TotalDays}d {remaining.Hours}h" : "Ended");
        }
        if (e.StartsAt.HasValue) ImGui.Text("Starts (local): " + e.StartsAt.Value.ToLocalTime().ToString("g"));
        if (e.EndsAt.HasValue) ImGui.Text("Ends (local): " + e.EndsAt.Value.ToLocalTime().ToString("g"));
        if (!string.IsNullOrWhiteSpace(e.Description)) ImGui.TextWrapped(e.Description);
        Section("Requirements", e.Requirements);
        Section("How to participate", e.Steps);
        Section("Rewards", e.Rewards);
        Section("Notes", e.Notes);
        var done = plugin.Settings.ManualCompletions.GetValueOrDefault(e.Id);
        if (ImGui.Checkbox("Marked completed (manual)##" + e.Id, ref done))
        {
            plugin.Settings.ManualCompletions[e.Id] = done;
            plugin.Save();
        }
        if (ImGui.Button("Open official site##" + e.Id) &&
            Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && (uri.Host.Equals("finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".finalfantasyxiv.com", StringComparison.OrdinalIgnoreCase)))
        {
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { Plugin.Log.Error(ex, "Could not open event page"); }
        }
        ImGui.Separator();
    }

    private static void Section(string title, System.Collections.Generic.IEnumerable<string> items)
    {
        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (list.Count == 0) return;
        ImGui.TextUnformatted(title);
        ImGui.PushTextWrapPos(0);
        foreach (var item in list) ImGui.BulletText(item);
        ImGui.PopTextWrapPos();
    }
    public void Dispose() { }
}
