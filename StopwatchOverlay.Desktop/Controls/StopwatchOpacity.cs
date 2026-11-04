using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>OpacityMask isolates a composited group; Visual.Opacity blends each drawing separately.</summary>
internal static class StopwatchOpacity
{
    internal static IBrush Mask(double alpha) => new SolidColorBrush(Colors.White,Math.Clamp(alpha,0,1));
    internal static void Apply(Visual visual,double alpha)
    {
        visual.Opacity=1;
        visual.OpacityMask=alpha>=1?null:Mask(alpha);
    }
}
