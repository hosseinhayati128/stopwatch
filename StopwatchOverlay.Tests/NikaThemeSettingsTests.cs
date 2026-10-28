using System.Text.Json;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class NikaThemeSettingsTests
{
    [Fact]
    public void RetainedChoicesAreUniqueAndAtlasCropsStayInBounds()
    {
        Assert.Equal(12, NikaThemeCatalog.Backgrounds.Count);
        Assert.Equal(12, NikaThemeCatalog.Backgrounds.Select(x => x.Id).Distinct().Count());
        Assert.Equal(4, NikaThemeCatalog.Typography.Count);
        Assert.Equal(17, NikaThemeCatalog.Artworks.Count);
        Assert.Equal(17, NikaThemeCatalog.Artworks.Select(x => x.Id).Distinct().Count());
        Assert.Null(NikaThemeCatalog.GetArtworkRegion("sun-warrior"));
        foreach (var art in NikaThemeCatalog.Artworks.Skip(1))
        {
            var rect = NikaThemeCatalog.GetArtworkRegion(art.Id)!.Value;
            Assert.InRange(rect.X, 0, 1);
            Assert.InRange(rect.Y, 0, 1);
            Assert.InRange(rect.Width, .01, 1 - rect.X);
            Assert.InRange(rect.Height, .01, 1 - rect.Y);
        }
    }

    [Theory]
    [InlineData("amber-atlas")]
    [InlineData("grand-line-vellum")]
    [InlineData("amber-voyage")]
    [InlineData("sky-island-chart")]
    [InlineData("lagoon-cartography")]
    [InlineData("wano-ink-atlas")]
    [InlineData("desert-kingdom")]
    [InlineData("lost-golden-city")]
    [InlineData("winter-island-log")]
    [InlineData("dawn-sea-chart")]
    [InlineData("navigator-masterwork")]
    public void ChoicesRoundTripAndSurviveSwitchingToExistingTheme(string background)
    {
        string folder = Path.Combine(Path.GetTempPath(), "nika-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "settings.json");
            var settings = new AppSettings
            {
                ThemeMode = AppThemeCatalog.Nika,
                NikaBackground = " " + background.ToUpperInvariant() + " ",
                NikaTypography = "chartmakers-script",
                NikaSidebarArtwork = "poneglyph",
                OverlayTheme = OverlayThemeCatalog.Daylight
            };
            Assert.True(SettingsStore.Save(settings, path));
            var loaded = SettingsStore.Load(path);
            Assert.Equal(AppThemeCatalog.Nika, loaded.ThemeMode);
            loaded.ThemeMode = AppThemeCatalog.Pirate;
            Assert.True(SettingsStore.Save(loaded, path));
            loaded = SettingsStore.Load(path);
            Assert.Equal(background, loaded.NikaBackground);
            Assert.Equal("chartmakers-script", loaded.NikaTypography);
            Assert.Equal("poneglyph", loaded.NikaSidebarArtwork);
            Assert.Equal(OverlayThemeCatalog.Daylight, loaded.OverlayTheme);
            Assert.Equal(OverlayThemeCatalog.Pirate,
                OverlayThemeCatalog.Resolve(OverlayThemeCatalog.FollowApplicationTheme, AppThemeCatalog.Nika));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void NikaIsADistinctPersistedApplicationTheme()
    {
        Assert.Equal("Nika's Chronicle", AppThemeCatalog.Normalize(" NIKA'S CHRONICLE "));
        Assert.Contains("Nika's Chronicle", AppThemeCatalog.All);
        Assert.Equal("one piece", AppThemeCatalog.Normalize("Pirate"));
        Assert.True(AppThemeCatalog.IsOnePiece(AppThemeCatalog.Nika));
        Assert.True(AppThemeCatalog.IsOnePiece(AppThemeCatalog.Pirate));
        Assert.False(AppThemeCatalog.IsOnePiece(AppThemeCatalog.Midnight));
    }

    [Fact]
    public void MissingOrInvalidOptionsReceiveSafeDefaults()
    {
        Assert.Equal("navigator-masterwork", new AppSettings().NikaBackground);
        Assert.Equal("navigator-masterwork", JsonSerializer.Deserialize<AppSettings>("{}")!.NikaBackground);
        Assert.Equal("navigator-masterwork", NikaThemeCatalog.NormalizeBackground("unknown"));
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"NikaBackground\":null,\"NikaTypography\":\"unknown\",\"NikaSidebarArtwork\":\"../file\"}")!;
        settings.NormalizeForRuntime();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        Assert.Equal("navigator-masterwork", json.RootElement.GetProperty("NikaBackground").GetString());
        Assert.Equal("original-classic", json.RootElement.GetProperty("NikaTypography").GetString());
        Assert.Equal("sun-warrior", json.RootElement.GetProperty("NikaSidebarArtwork").GetString());
    }
}
