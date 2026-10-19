using System.Numerics;

namespace EorzeaEventTracker;

// Shared journal/widget palette. Status colors carry meaning, never the entire background.
internal static class JournalTheme
{
    public static readonly Vector4 Background = new(.075f, .082f, .090f, 1);
    public static readonly Vector4 Panel = new(.095f, .103f, .112f, 1);
    public static readonly Vector4 Card = new(.125f, .135f, .145f, 1);
    public static readonly Vector4 Gold = new(.79f, .71f, .53f, 1);
    public static readonly Vector4 Text = new(.91f, .89f, .83f, 1);
    public static readonly Vector4 Muted = new(.64f, .66f, .65f, 1);
    public static readonly Vector4 QuestTitle = new(.91f, .81f, .60f, 1);
    public static readonly Vector4 NpcTitle = new(.64f, .80f, .87f, 1);
    public static readonly Vector4 CompletedTitle = new(.64f, .76f, .66f, 1);
    public static readonly Vector4 FrameEdge = new(.26f, .28f, .30f, .90f);
    public static readonly Vector4 FrameLight = new(.76f, .74f, .65f, .18f);
    public static readonly Vector4 Shadow = new(.015f, .018f, .022f, .65f);
    public static readonly Vector4 Border = new(.48f, .45f, .36f, .45f);
    public static readonly Vector4 Button = new(.19f, .205f, .215f, 1);
    public static readonly Vector4 Hover = new(.26f, .275f, .28f, 1);
    public static readonly Vector4 Active = new(.31f, .29f, .23f, 1);
    public static readonly Vector4 Green = new(.49f, .77f, .61f, 1);
    public static readonly Vector4 Amber = new(.88f, .73f, .44f, 1);
    public static readonly Vector4 Red = new(.85f, .54f, .49f, 1);
    public static Vector4 DeadlineColor(DeadlineStatus status) => status switch
    {
        DeadlineStatus.Comfortable => Green,
        DeadlineStatus.Soon => Amber,
        DeadlineStatus.Urgent => Red,
        DeadlineStatus.Upcoming => Gold,
        _ => Muted,
    };
    public static Vector4 WithOpacity(Vector4 color, float opacity) => new(color.X, color.Y, color.Z, color.W * opacity);
}
