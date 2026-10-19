using System;

namespace EorzeaEventTracker;

public static class AppearancePolicy
{
    public static float Scale(CharacterSettings settings) => float.IsFinite(settings.UiScale) ? Math.Clamp(settings.UiScale, .75f, 1.75f) : 1;
    public static float Opacity(CharacterSettings settings) => float.IsFinite(settings.BackgroundOpacity) ? Math.Clamp(settings.BackgroundOpacity, .4f, 1) : 1;
}
