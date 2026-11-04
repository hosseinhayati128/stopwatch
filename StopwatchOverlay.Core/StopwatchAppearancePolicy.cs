namespace StopwatchOverlay;

public enum StopwatchMarker { Running, Paused }

public static class StopwatchAppearancePolicy
{
    public static double ResolveInactiveOpacityPercent(AppSettings settings)
        => OverlayThemeCatalog.Resolve(settings.OverlayTheme,settings.ApplicationTheme)==OverlayThemeCatalog.Nika
            ? FinitePercent(settings.StopwatchAppearance.UnselectedOpacity,10,40)
            : OverlayPresentationPolicy.ClampInactiveSeparatedOpacity(settings.InactiveSeparatedOverlayOpacity);
    public static StopwatchMarker MarkerFor(bool isRunning,bool isClockMode)
        => isRunning || isClockMode ? StopwatchMarker.Running : StopwatchMarker.Paused;

    public static double LayerOpacity(StopwatchAppearanceSettings profile,StopwatchOpaqueLayers layer)
        => (profile.OpaqueLayers & layer)!=0 ? 1 : FinitePercent(profile.BackgroundOpacity,0,70)/100;

    public static double SelectionOpacity(StopwatchAppearanceSettings profile,bool isSelected)
        => isSelected ? 1 : FinitePercent(profile.UnselectedOpacity,10,40)/100;

    private static double FinitePercent(double value,double minimum,double fallback)
        => double.IsFinite(value)?Math.Clamp(value,minimum,100):fallback;
}
