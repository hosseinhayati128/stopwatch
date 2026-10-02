using System;

namespace StopwatchOverlay;

public static class AppUiScale
{
    public const double DefaultPercent = 90;
    public static double Normalize(double percent)
        => double.IsFinite(percent) ? Math.Clamp(percent, 70, 125) : DefaultPercent;

    public static Action<double>? Applier { get; set; }

    public static void Apply(double percent)
        => Applier?.Invoke(Normalize(percent));
}
