using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace StopwatchOverlay.Desktop;

internal static class ThemeResources
{
    internal static ResourceInclude Load(string file) => new(new Uri("avares://StopwatchOverlay.Desktop/"))
    {
        Source = new Uri($"avares://StopwatchOverlay.Desktop/Themes/{file}.axaml")
    };

    internal static void ApplyApplication(string? theme)
    {
        if (Application.Current is not { } app) return;
        string normalized = AppThemeCatalog.Normalize(theme);
        string file = normalized switch
        {
            AppThemeCatalog.PixelDeckNight => "PixelDeck",
            AppThemeCatalog.PixelDeckDay => "PixelDeckDay",
            AppThemeCatalog.Pirate => "Pirate",
            AppThemeCatalog.Nika => "Pirate",
            _ => normalized
        };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(Load(file));
        app.RequestedThemeVariant = normalized is AppThemeCatalog.Daylight or AppThemeCatalog.PixelDeckDay or AppThemeCatalog.Acanthus or AppThemeCatalog.Pirate or AppThemeCatalog.Nika
            ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    internal static string OverlayFile(string theme) => theme switch
    {
        OverlayThemeCatalog.PixelDeckNight => "PixelDeckNightOverlay",
        OverlayThemeCatalog.PixelDeckDay => "PixelDeckDayOverlay",
        OverlayThemeCatalog.AcanthusLight => "AcanthusLightOverlay",
        OverlayThemeCatalog.AcanthusDarkElegantOlive => "AcanthusDarkElegantOliveOverlay",
        OverlayThemeCatalog.AcanthusDarkGoldCrest => "AcanthusDarkGoldCrestOverlay",
        OverlayThemeCatalog.AcanthusDarkMinimalBotanical => "AcanthusDarkMinimalBotanicalOverlay",
        OverlayThemeCatalog.Pirate => "PirateOverlay",
        OverlayThemeCatalog.Daylight => "DaylightOverlay",
        _ => "MidnightOverlay"
    };
}
