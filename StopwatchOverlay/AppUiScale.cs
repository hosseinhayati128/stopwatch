using System;
using System.Windows;
using System.Windows.Media;

namespace StopwatchOverlay;

public static class AppUiScale
{
    public const double DefaultPercent = 90;
    public static double Normalize(double percent)
        => double.IsFinite(percent) ? Math.Clamp(percent, 70, 125) : DefaultPercent;

    public static void Apply(double percent)
    {
        if (Application.Current is not { } app) return;
        double factor = Normalize(percent) / 100;
        if (app.Resources["ApplicationScaleTransform"] is ScaleTransform current
            && current.ScaleX == factor && current.ScaleY == factor) return;
        var transform = new ScaleTransform(factor, factor);
        transform.Freeze();
        app.Resources["ApplicationScaleTransform"] = transform;
    }
}
