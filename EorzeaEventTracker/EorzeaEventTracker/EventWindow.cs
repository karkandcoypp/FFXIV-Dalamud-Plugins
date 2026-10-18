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

public sealed partial class EventWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool settingsVisible;
    private string urlBuffer;
    private string selected = "";
    private bool remainingOnly;
    private bool favoritesOnly;
    private DateTime mapErrorUntil;
    private EventInfo? localizedEvent;
    private static readonly Vector4 Teal = new(.18f, .36f, .26f, 1);
    private static readonly Vector4 Gold = new(.49f, .29f, .12f, 1);
    private static readonly Vector4 Muted = new(.40f, .33f, .25f, 1);
    private Localizer L => plugin.Locale;
    private string T(string text) => L.Text(text);
    private string E(string text) => L.Text(text, localizedEvent);
    private string F(string text, params object[] values) => L.Format(text, values);
    private float renderScale = 1;
    private float UiScale => renderScale;
    private float Opacity => AppearancePolicy.Opacity(plugin.Settings);
    private float S => ImGui.GetFontSize() / 17f;
    internal void SelectEvent(string id) { selected = id; sheetPage = 0; sheetSection = 0; settingsVisible = false; }

    public EventWindow(Plugin plugin) : base("Eorzea Event Tracker###EETMain")
    {
        this.plugin = plugin;
        Size = new Vector2(1180, 820);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(800, 540), MaximumSize = new(1600, 1400) };
        urlBuffer = plugin.Settings.FeedUrl;
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public override void PreDraw()
    {
        var hostScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        var maximum = ImGui.GetMainViewport().WorkSize / hostScale - new Vector2(12);
        renderScale = Math.Min(AppearancePolicy.Scale(plugin.Settings), Math.Max(.65f, maximum.Y / 620f));
        SizeConstraints = new WindowSizeConstraints { MinimumSize = Vector2.Min(new Vector2(900, 600) * UiScale, maximum), MaximumSize = maximum };
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.89f, .83f, .70f, .98f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.20f, .12f, .075f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.30f, .18f, .10f, Opacity));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.96f, .91f, .79f, 1));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(.20f, .12f, .075f, Opacity));
    }

    public override void PostDraw() => ImGui.PopStyleColor(5);

    public override void Draw()
    {
        ImGui.SetWindowFontScale(UiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * S);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * S);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.89f, .83f, .70f, .98f * Opacity));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.48f, .34f, .19f, .35f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.80f, .71f, .56f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.91f, .84f, .68f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.95f, .89f, .75f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.79f, .68f, .49f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.88f, .77f, .57f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.91f, .84f, .68f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.72f, .58f, .38f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.85f, .78f, .64f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(.92f, .86f, .73f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(.78f, .66f, .47f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.90f, .81f, .64f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.82f, .70f, .51f, 1));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.49f, .34f, .18f, .35f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, new Vector4(.80f, .71f, .56f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(.88f, .79f, .61f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.20f, .15f, .105f, 1));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        try { DrawBookContent(); }
        finally { ImGui.PopStyleColor(21); ImGui.PopStyleVar(3); ImGui.SetWindowFontScale(1); }
    }

    private bool BeginScaledChild(string id, Vector2 size, bool border, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        // Child windows inherit their parent's font scale. Normalize it so nested reward
        // panels get the same requested scale rather than multiplying or losing it.
        ImGui.SetWindowFontScale(1);
        var visible = ImGui.BeginChild(id, size, border, flags | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetWindowFontScale(UiScale);
        return visible;
    }

    private void EndScaledChild()
    {
        ImGui.EndChild();
        ImGui.SetWindowFontScale(UiScale);
    }

    private void DrawBookBackground(float leftPageWidth, float gutter)
    {
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(100 * S, ImGui.GetContentRegionAvail().Y - 30 * S));
        var draw = ImGui.GetWindowDrawList();
        var file = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "assets", "book.png");
        if (Plugin.Textures.GetFromFile(file).TryGetWrap(out var wrap, out _))
        {
            // Preserve the central binding while independently sizing the two content pages.
            var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, Opacity));
            draw.AddImage(wrap.Handle, start, start + new Vector2(leftPageWidth, size.Y), Vector2.Zero, new Vector2(.49f, 1), tint);
            draw.AddImage(wrap.Handle, start + new Vector2(leftPageWidth, 0), start + new Vector2(leftPageWidth + gutter, size.Y), new Vector2(.49f, 0), new Vector2(.51f, 1), tint);
            draw.AddImage(wrap.Handle, start + new Vector2(leftPageWidth + gutter, 0), start + size, new Vector2(.51f, 0), Vector2.One, tint);
        }
        else
        {
            draw.AddRectFilled(start, start + size, ImGui.ColorConvertFloat4ToU32(new Vector4(.95f, .88f, .73f, Opacity)), 10 * S);
            draw.AddRectFilled(start + new Vector2(leftPageWidth, 0), start + new Vector2(leftPageWidth + gutter, size.Y), ImGui.ColorConvertFloat4ToU32(new Vector4(.58f, .41f, .23f, Opacity)));
        }
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
        var height = imageHeight + titleHeight + ImGui.GetTextLineHeightWithSpacing() * 3 + 44 * S;
        var start = ImGui.GetCursorScreenPos();
        var cursor = ImGui.GetCursorPos();
        if (ImGui.InvisibleButton("Select event", new Vector2(width, height))) SelectEvent(e.Id);
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var draw = ImGui.GetWindowDrawList();
        var background = active ? new Vector4(.91f, .82f, .64f, 1)
            : hovered ? new Vector4(.97f, .91f, .79f, 1) : new Vector4(.96f, .90f, .77f, 1);
        background.W = Opacity;
        draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(background), 8 * S);
        draw.AddRect(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(active ? accent : new Vector4(.53f, .37f, .20f, .35f)), 8 * S);
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
        if (active)
        {
            var ribbonLeft = start + new Vector2(width - 28 * S, 0);
            var ribbonRight = start + new Vector2(width - 14 * S, 28 * S);
            draw.AddRectFilled(ribbonLeft, ribbonRight, ImGui.ColorConvertFloat4ToU32(new Vector4(.46f, .16f, .13f, 1)));
            draw.AddTriangleFilled(new Vector2(ribbonLeft.X, ribbonRight.Y), ribbonRight,
                start + new Vector2(width - 21 * S, 21 * S), ImGui.ColorConvertFloat4ToU32(background));
        }
        ImGui.SetCursorPos(cursor + new Vector2(padding, imageHeight + 16 * S));
        ImGui.PushTextWrapPos(cursor.X + width - padding);
        ImGui.TextColored(active ? new Vector4(.27f, .13f, .09f, 1) : new Vector4(.28f, .22f, .16f, 1), title);
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
            var bar = start + new Vector2(padding, height - 12 * S);
            var barWidth = width - padding * 2;
            draw.AddRectFilled(bar, bar + new Vector2(barWidth, 3 * S), ImGui.ColorConvertFloat4ToU32(new Vector4(.67f, .57f, .40f, .45f)), 2 * S);
            if (total > 0 && done > 0)
                draw.AddRectFilled(bar, bar + new Vector2(barWidth * done / total, 3 * S), ImGui.ColorConvertFloat4ToU32(Teal), 2 * S);
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

    private void DrawReward(TrackedReward reward, RewardProgress progress, bool compact = false)
    {
        ImGui.PushID((int)reward.Id);
        var scale = S;
        var padding = 10 * scale;
        var gap = 5 * scale;
        var ignored = plugin.Settings.IgnoredRewards.Contains(reward.Id);
        var favorite = plugin.Settings.FavoriteRewards.Contains(reward.Id);
        var layout = MeasureReward(reward, progress, ImGui.GetContentRegionAvail().X, compact);
        var width = ImGui.GetContentRegionAvail().X - padding * 2;
        var textWidth = Math.Max(50 * scale, width - 52 * scale);
        var headerHeight = layout.Header;
        var status = L.RewardLabel(progress);
        var height = layout.Height;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8 * scale, gap));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (ignored ? .82f : 1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ignored ? new Vector4(.83f, .81f, .75f, .85f * Opacity) : new Vector4(.98f, .95f, .86f, .86f * Opacity));
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
            ImGui.TextColored(ignored ? Muted : favorite ? Gold : new Vector4(.23f, .17f, .11f, 1), E(reward.Name));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{reward.Name}\nItem #{reward.Id}");
            if (reward.Price > 0) ImGui.TextColored(ignored ? Muted : Gold, $"{reward.Price.ToString("N0", L.Culture)} {E(reward.Currency)}");
            ImGui.PopTextWrapPos();
            ImGui.EndGroup();
            ImGui.SetCursorPos(new Vector2(padding, padding + headerHeight + gap));
            ImGui.PushTextWrapPos(padding + width);
            ImGui.TextColored(!ignored && progress.Confirmed ? Teal : Muted, status);
            ImGui.PopTextWrapPos();
            if (compact)
            {
                if (ImGui.SmallButton(T("Reward actions") + "###Actions")) ImGui.OpenPopup("RewardActions");
                if (ImGui.BeginPopup("RewardActions"))
                {
                    DrawRewardControls(reward, false);
                    ImGui.EndPopup();
                }
            }
            else DrawRewardControls(reward, layout.OneLine);
        }
        EndScaledChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
        ImGui.PopID();
    }

    private (float Height, float Header, bool OneLine) MeasureReward(TrackedReward reward, RewardProgress progress, float cellWidth, bool compact)
    {
        var width = Math.Max(40 * S, cellWidth - 20 * S);
        var textWidth = Math.Max(50 * S, width - 52 * S);
        var title = ImGui.CalcTextSize(E(reward.Name), false, textWidth).Y;
        var header = Math.Max(42 * S, title + (reward.Price > 0 ? ImGui.GetTextLineHeight() + 5 * S : 0));
        var oneLine = ImGui.CalcTextSize(T("Favorite")).X + ImGui.CalcTextSize(T("Not interested")).X + ImGui.GetFrameHeight() * 2 + 24 * S <= width;
        var controls = compact ? ImGui.GetFrameHeight() : ImGui.GetFrameHeight() * (oneLine ? 2 : 3) + 5 * S * (oneLine ? 1 : 2);
        var map = !compact && localizedEvent?.Locations.Any(l => l.Id == reward.LocationId) == true ? ImGui.GetFrameHeight() + 5 * S : 0;
        return (20 * S + header + ImGui.CalcTextSize(L.RewardLabel(progress), false, width).Y + controls + 15 * S + map, header, oneLine);
    }

    private void DrawRewardControls(TrackedReward reward, bool oneLine)
    {
        var favorite = plugin.Settings.FavoriteRewards.Contains(reward.Id);
        var ignored = plugin.Settings.IgnoredRewards.Contains(reward.Id);
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
        var height = Math.Max(Math.Clamp(width * .30f, 130 * scale, 175 * scale),
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
        ImGui.TextColored(new Vector4(.91f, .77f, .48f, 1), T(e.Active(now) ? "SEASONAL JOURNAL  /  LIVE" : "SEASONAL JOURNAL  /  UPCOMING"));
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

    private string Countdown(EventInfo e, DateTimeOffset now)
    {
        var future = e.Upcoming(now);
        var duration = (future ? e.StartsAt : e.EndsAt) - now;
        if (duration is not { } remaining || remaining <= TimeSpan.Zero) return T("Ended");
        return F(future ? "Starts in {0}d {1}h {2}m" : "Ends in {0}d {1}h {2}m", (int)remaining.TotalDays, remaining.Hours, remaining.Minutes);
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
