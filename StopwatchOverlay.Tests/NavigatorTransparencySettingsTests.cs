using System;
using System.IO;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class NavigatorTransparencySettingsTests
{
    [Fact]
    public void LegacySettings_KeepClockFrameAndButtonDialsVisible()
    {
        using var files = new SettingsFiles();
        File.WriteAllText(files.Path, """{"BackgroundOpacity":0,"TextSize":63}""");

        AppSettings restored = SettingsStore.Load(files.Path);

        var expected = NavigatorOpaqueParts.ClockFrame | NavigatorOpaqueParts.MetalBorder
            | NavigatorOpaqueParts.TimerText | NavigatorOpaqueParts.ProjectName | NavigatorOpaqueParts.ControlDials;
        Assert.Equal(expected, new AppSettings().OpaqueOverlayParts);
        Assert.Equal(expected, restored.OpaqueOverlayParts);
        Assert.Equal(0, restored.BackgroundOpacity);
        Assert.Equal(63, restored.TextSize);
    }

    [Fact]
    public void SaveReload_PreservesEveryCombinationIncludingAllTransparent()
    {
        using var files = new SettingsFiles();
        for (int combination = 0; combination <= (int)NavigatorOpaqueParts.All; combination++)
        {
            var settings = new AppSettings
            {
                OpaqueOverlayParts = (NavigatorOpaqueParts)combination,
                BackgroundOpacity = 23,
                TextSize = 63,
                ThemeMode = AppThemeCatalog.Midnight,
                OverlayTheme = OverlayThemeCatalog.Pirate
            };

            Assert.True(SettingsStore.Save(settings, files.Path));
            AppSettings restored = SettingsStore.Load(files.Path);

            Assert.Equal((NavigatorOpaqueParts)combination, restored.OpaqueOverlayParts);
            Assert.Equal(23, restored.BackgroundOpacity);
            Assert.Equal(63, restored.TextSize);
            Assert.Equal(AppThemeCatalog.Midnight, restored.ThemeMode);
            Assert.Equal(OverlayThemeCatalog.Pirate, restored.OverlayTheme);
        }
    }

    [Theory]
    [InlineData(512, 0)]
    [InlineData(513, 1)]
    [InlineData(-1, 511)]
    [InlineData(int.MaxValue, 511)]
    public void Load_DropsUnknownBitsWithoutChangingKnownParts(int saved, int expected)
    {
        using var files = new SettingsFiles();
        File.WriteAllText(files.Path, "{\"OpaqueOverlayParts\":" + saved + ",\"BackgroundOpacity\":37}");

        AppSettings restored = SettingsStore.Load(files.Path);

        Assert.Equal((NavigatorOpaqueParts)expected, restored.OpaqueOverlayParts);
        Assert.Equal(37, restored.BackgroundOpacity);
    }

    private sealed class SettingsFiles : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "StopwatchOverlay-TransparencyTests-" + Guid.NewGuid().ToString("N"));

        internal string Path => System.IO.Path.Combine(_directory, "settings.json");

        internal SettingsFiles() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
