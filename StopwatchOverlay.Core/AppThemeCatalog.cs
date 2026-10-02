using System;
using System.Collections.Generic;

namespace StopwatchOverlay;

public static class AppThemeCatalog
{
    public const string Midnight = "Midnight";
    public const string Daylight = "Daylight";
    public const string PixelDeckNight = "Pixel Deck Night";
    public const string PixelDeckDay = "Pixel Deck Day";
    public const string Acanthus = "Acanthus";
    public const string Pirate = "one piece";
    public const string PixelDeck = PixelDeckNight;

    public static IReadOnlyList<string> All { get; } =
        [Midnight, Daylight, PixelDeckNight, PixelDeckDay, Acanthus, Pirate];

    public static string Normalize(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Equals(Pirate, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Pirate", StringComparison.OrdinalIgnoreCase))
            return Pirate;
        if (candidate.Equals(Acanthus, StringComparison.OrdinalIgnoreCase))
        {
            return Acanthus;
        }

        if (candidate.Equals(PixelDeckDay, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("PixelDeckDay", StringComparison.OrdinalIgnoreCase))
        {
            return PixelDeckDay;
        }

        if (candidate.Equals(PixelDeckNight, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Pixel Deck", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("PixelDeck", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("PixelDeckNight", StringComparison.OrdinalIgnoreCase))
        {
            return PixelDeckNight;
        }

        if (candidate.Equals(Daylight, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Light", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("Light Mode", StringComparison.OrdinalIgnoreCase))
        {
            return Daylight;
        }

        // "Dark" was the only value written by older releases.
        return Midnight;
    }
}
