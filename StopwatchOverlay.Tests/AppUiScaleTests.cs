using System;
using System.IO;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class AppUiScaleTests
{
    [Theory]
    [InlineData(double.NaN, 90)]
    [InlineData(double.PositiveInfinity, 90)]
    [InlineData(double.NegativeInfinity, 90)]
    [InlineData(-1, 70)]
    [InlineData(0, 70)]
    [InlineData(70, 70)]
    [InlineData(82.5, 82.5)]
    [InlineData(100, 100)]
    [InlineData(125, 125)]
    [InlineData(1000, 125)]
    public void Normalize_RejectsNonfiniteValuesAndBoundsFiniteValues(double value, double expected)
    {
        Assert.Equal(expected, AppUiScale.Normalize(value));
        var settings = new AppSettings { UiScalePercent = value };
        settings.NormalizeForRuntime();
        Assert.Equal(expected, settings.UiScalePercent);
    }

    [Theory]
    [InlineData(70, 70)]
    [InlineData(82.5, 82.5)]
    [InlineData(125, 125)]
    [InlineData(-10, 70)]
    [InlineData(150, 125)]
    [InlineData(double.NaN, 90)]
    public void SaveReload_PreservesScaleIndependentlyOfOverlayAppearanceAndPosition(double value, double expected)
    {
        using var files = new SettingsFiles();
        var settings = new AppSettings
        {
            UiScalePercent = value,
            TextSize = 63,
            BorderWidth = 4,
            Position = "Custom",
            HasCustomPosition = true,
            CustomLeft = -830.25,
            CustomTop = 127.5,
            LightRingWidth = 36,
            ThemeMode = AppThemeCatalog.PixelDeckNight,
            OverlayTheme = OverlayThemeCatalog.Daylight
        };

        Assert.True(SettingsStore.Save(settings, files.Path));
        AppSettings restored = SettingsStore.Load(files.Path);

        Assert.Equal(expected, restored.UiScalePercent);
        Assert.Equal(63, restored.TextSize);
        Assert.Equal(4, restored.BorderWidth);
        Assert.Equal("Custom", restored.Position);
        Assert.True(restored.HasCustomPosition);
        Assert.Equal(-830.25, restored.CustomLeft);
        Assert.Equal(127.5, restored.CustomTop);
        Assert.Equal(36, restored.LightRingWidth);
        Assert.Equal(AppThemeCatalog.PixelDeckNight, restored.ThemeMode);
        Assert.Equal(OverlayThemeCatalog.Daylight, restored.OverlayTheme);
    }

    [Fact]
    public void LegacySettings_WithoutScaleUseCompactDefaultAndKeepOverlaySize()
    {
        using var files = new SettingsFiles();
        File.WriteAllText(files.Path, """{"TextSize": 72, "CustomLeft": -100, "CustomTop": 55}""");

        AppSettings restored = SettingsStore.Load(files.Path);

        Assert.Equal(AppUiScale.DefaultPercent, new AppSettings().UiScalePercent);
        Assert.Equal(AppUiScale.DefaultPercent, restored.UiScalePercent);
        Assert.Equal(72, restored.TextSize);
        Assert.Equal(-100, restored.CustomLeft);
        Assert.Equal(55, restored.CustomTop);
        Assert.True(SettingsStore.Save(restored, files.Path));
        Assert.Equal(AppUiScale.DefaultPercent, SettingsStore.Load(files.Path).UiScalePercent);
    }

    [Theory]
    [InlineData("-25", 70)]
    [InlineData("500", 125)]
    [InlineData("87.5", 87.5)]
    public void Load_NormalizesStoredScaleWithoutDiscardingOtherPreferences(string persistedValue, double expected)
    {
        using var files = new SettingsFiles();
        File.WriteAllText(files.Path, "{\"UiScalePercent\":" + persistedValue + ",\"TextSize\":61}");

        AppSettings restored = SettingsStore.Load(files.Path);

        Assert.Equal(expected, restored.UiScalePercent);
        Assert.Equal(61, restored.TextSize);
    }

    [Fact]
    public void ApplicationScale_IsContinuousWithoutRequestingThemeBackgroundOrLightRingRebuild()
    {
        Assert.True(SettingsChangePolicy.IsContinuous(SettingsChangeKind.ApplicationScale));
        Assert.False(SettingsChangePolicy.RequiresThemeApply(SettingsChangeKind.ApplicationScale));
        Assert.False(SettingsChangePolicy.RequiresBackgroundApply(SettingsChangeKind.ApplicationScale));
        Assert.False(SettingsChangePolicy.RequiresLightRingRebuild(SettingsChangeKind.ApplicationScale));
    }

    private sealed class SettingsFiles : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "StopwatchOverlay-UiScaleTests-" + Guid.NewGuid().ToString("N"));

        internal string Path => System.IO.Path.Combine(_directory, "settings.json");

        internal SettingsFiles() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
