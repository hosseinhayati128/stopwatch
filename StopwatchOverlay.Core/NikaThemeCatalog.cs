using System;
using System.Collections.Generic;

namespace StopwatchOverlay;

public sealed record NikaThemeOption(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>A normalized source rectangle in the approved mural atlas, without labels.</summary>
public readonly record struct NikaArtworkRegion(double X, double Y, double Width, double Height);

/// <summary>Stable persisted choices for Nika's Chronicle. Independent of overlay and global typography.</summary>
public static class NikaThemeCatalog
{
    public const string DefaultBackground = "navigator-masterwork";

    public static IReadOnlyList<NikaThemeOption> Backgrounds { get; } = Array.AsReadOnly(new[]
    {
        new NikaThemeOption("original-map", "Original Map"),
        new NikaThemeOption("amber-atlas", "Amber Atlas"),
        new NikaThemeOption("grand-line-vellum", "01 · Grand Line Vellum"),
        new NikaThemeOption("amber-voyage", "02 · Amber Voyage"),
        new NikaThemeOption("sky-island-chart", "03 · Sky Island Chart"),
        new NikaThemeOption("lagoon-cartography", "04 · Lagoon Cartography"),
        new NikaThemeOption("wano-ink-atlas", "05 · Wano Ink Atlas"),
        new NikaThemeOption("desert-kingdom", "06 · Desert Kingdom"),
        new NikaThemeOption("lost-golden-city", "07 · Lost Golden City"),
        new NikaThemeOption("winter-island-log", "08 · Winter Island Log"),
        new NikaThemeOption("dawn-sea-chart", "09 · Dawn Sea Chart"),
        new NikaThemeOption("navigator-masterwork", "10 · Navigator's Masterwork")
    });

    public static IReadOnlyList<NikaThemeOption> Typography { get; } = Array.AsReadOnly(new[]
    {
        new NikaThemeOption("original-classic", "Original Classic"),
        new NikaThemeOption("typewriter-log", "Typewriter Log"),
        new NikaThemeOption("chartmakers-script", "Chartmaker's Script"),
        new NikaThemeOption("navigator-precision", "Navigator Precision")
    });

    public static IReadOnlyList<NikaThemeOption> Artworks { get; } = Array.AsReadOnly(new[]
    {
        new NikaThemeOption("sun-warrior", "Sun Warrior"),
        new NikaThemeOption("dawn-dancer", "01 · Dawn Dancer"),
        new NikaThemeOption("sun-pirates", "02 · Sun Pirates"),
        new NikaThemeOption("kozuki-crane", "03 · Kozuki Crane"),
        new NikaThemeOption("shandora-bell", "04 · Shandora Bell"),
        new NikaThemeOption("cloud-dragon", "05 · Cloud Dragon"),
        new NikaThemeOption("phoenix", "06 · Phoenix"),
        new NikaThemeOption("zunesha", "07 · Zunesha"),
        new NikaThemeOption("laboon", "08 · Laboon"),
        new NikaThemeOption("sea-king", "09 · Sea King"),
        new NikaThemeOption("straw-hat-crest", "10 · Straw Hat Crest"),
        new NikaThemeOption("heart-pirates", "11 · Heart Pirates"),
        new NikaThemeOption("whitebeard", "12 · Whitebeard"),
        new NikaThemeOption("kuja-serpents", "13 · Kuja Serpents"),
        new NikaThemeOption("three-sword-spirit", "14 · Three-Sword Spirit"),
        new NikaThemeOption("aces-flame", "15 · Ace's Flame"),
        new NikaThemeOption("poneglyph", "16 · Poneglyph")
    });

    public static string NormalizeBackground(string? value) => Normalize(value, Backgrounds, DefaultBackground);

    /// <summary>Embedded filename shared by both apps; only catalog IDs can select an asset.</summary>
    public static string GetBackgroundAsset(string? background)
    {
        string id = NormalizeBackground(background);
        return id is "original-map" or "amber-atlas" ? "map.png" : $"background-{id}.png";
    }

    public static string NormalizeTypography(string? value) => Normalize(value, Typography);
    public static string NormalizeArtwork(string? value) => Normalize(value, Artworks);

    private static string Normalize(string? value, IReadOnlyList<NikaThemeOption> options, string? fallback = null)
    {
        string candidate = value?.Trim() ?? string.Empty;
        foreach (var option in options)
            if (candidate.Equals(option.Id, StringComparison.OrdinalIgnoreCase))
                return option.Id;
        return fallback ?? options[0].Id;
    }

    /// <summary>Null selects warrior.png. Other choices crop mural-atlas.png at runtime.</summary>
    public static NikaArtworkRegion? GetArtworkRegion(string? artwork)
    {
        string id = NormalizeArtwork(artwork);
        int index = 0;
        for (int i = 1; i < Artworks.Count; i++)
            if (Artworks[i].Id == id) { index = i; break; }
        if (index == 0) return null;
        // Original atlas is 1254 square. Rows differ slightly; exclude all captions and rules.
        double[] top = [44, 352, 652, 926];
        double[] bottom = [311, 615, 884, 1197];
        int row = (index - 1) / 4;
        int col = (index - 1) % 4;
        double[] left = [20, 323, 632, 941];
        double[] right = [310, 619, 928, 1234];
        return new(left[col] / 1254, top[row] / 1254,
            (right[col] - left[col]) / 1254, (bottom[row] - top[row]) / 1254);
    }
}
