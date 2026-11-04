using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class StopwatchAppearancePolicyTests
{
    [Fact]
    public void IndependentNikaRouteDoesNotReplaceExplicitLegacyTheme()
    {
        Assert.Equal(AppThemeCatalog.Nika,OverlayThemeCatalog.Resolve(OverlayThemeCatalog.FollowApplicationTheme,AppThemeCatalog.Nika));
        Assert.Equal(AppThemeCatalog.Nika,OverlayThemeCatalog.Resolve(AppThemeCatalog.Nika,AppThemeCatalog.Midnight));
        Assert.Equal(OverlayThemeCatalog.Pirate,OverlayThemeCatalog.Resolve(OverlayThemeCatalog.Pirate,AppThemeCatalog.Nika));
    }
    [Fact]
    public void LayerTransparencyIsAppliedBeforeSelectionDimming()
    {
        var profile = new StopwatchAppearanceSettings();
        double selection = StopwatchAppearancePolicy.SelectionOpacity(profile, false);
        Assert.Equal(.4, selection);
        Assert.Equal(1, StopwatchAppearancePolicy.SelectionOpacity(profile, true));
        Assert.Equal(.28, profile.BackgroundOpacity / 100 * selection, 6);
        Assert.Equal(.4, StopwatchAppearancePolicy.LayerOpacity(profile, StopwatchOpaqueLayers.Frame) * selection, 6);
        Assert.Equal(.28, StopwatchAppearancePolicy.LayerOpacity(profile, StopwatchOpaqueLayers.Artwork) * selection, 6);
        profile.BackgroundOpacity=0;
        Assert.Equal(1, StopwatchAppearancePolicy.LayerOpacity(profile, StopwatchOpaqueLayers.TimeText));
        Assert.Equal(0, StopwatchAppearancePolicy.LayerOpacity(profile, StopwatchOpaqueLayers.Artwork));
    }

    [Theory]
    [InlineData(false, false, StopwatchMarker.Paused)]
    [InlineData(true, false, StopwatchMarker.Running)]
    [InlineData(false, true, StopwatchMarker.Running)]
    public void ClockModeCannotClaimToBePaused(bool running, bool clock, StopwatchMarker expected)
        => Assert.Equal(expected, StopwatchAppearancePolicy.MarkerFor(running, clock));
}
