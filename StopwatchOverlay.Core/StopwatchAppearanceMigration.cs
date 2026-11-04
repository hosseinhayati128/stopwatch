using System.Text.Json;

namespace StopwatchOverlay;

internal static class StopwatchAppearanceMigration
{
    internal static void Apply(AppSettings settings,JsonElement root)
    {
        if(root.TryGetProperty(nameof(AppSettings.StopwatchAppearance),out var profile)
            && profile.ValueKind!=JsonValueKind.Null) return;
        settings.StopwatchAppearance ??= new();
        if(ReadFinite(root,nameof(AppSettings.BackgroundOpacity)) is double background)
            settings.StopwatchAppearance.BackgroundOpacity=background;
        if(ReadFinite(root,nameof(AppSettings.InactiveSeparatedOverlayOpacity)) is double unselected)
            settings.StopwatchAppearance.UnselectedOpacity=unselected;
    }

    private static double? ReadFinite(JsonElement root,string name)
        => root.TryGetProperty(name,out var element) && element.ValueKind==JsonValueKind.Number
            && element.TryGetDouble(out double value) && double.IsFinite(value) ? value : null;
}
