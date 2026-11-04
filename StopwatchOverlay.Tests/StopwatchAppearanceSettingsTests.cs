using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class StopwatchAppearanceSettingsTests
{
    [Theory]
    [InlineData("BackgroundOpacity","\"bad\"")]
    [InlineData("BackgroundOpacity","1e9999")]
    [InlineData("UnselectedOpacity","null")]
    [InlineData("OpaqueLayers","[]")]
    [InlineData("Background","23")]
    [InlineData("Frame","{}")]
    [InlineData("Typography","false")]
    [InlineData("Artwork","42")]
    [InlineData("HoverControls","[]")]
    [InlineData("Version","\"future\"")]
    public void MalformedAppearanceFieldPreservesValidSiblingsAndUnrelatedSettings(string field,string malformed)
    {
        using var files=new Fixture();
        var root=JsonNode.Parse("""
            {"TextSize":67,"BackgroundOpacity":85,"OverlayTheme":"Daylight",
             "StopwatchAppearance":{"Background":"midnight-chart","Frame":"classic-brass","BackgroundOpacity":25,"UnselectedOpacity":55}}
            """)!;
        root["StopwatchAppearance"]![field]=JsonNode.Parse(malformed);
        File.WriteAllText(files.Path,root.ToJsonString());
        var settings=SettingsStore.Load(files.Path);
        Assert.Equal(67,settings.TextSize);
        Assert.Equal(85,settings.BackgroundOpacity);
        Assert.Equal(OverlayThemeCatalog.Daylight,settings.OverlayTheme);
        Assert.Equal(field=="Background"?"navigator-parchment":"midnight-chart",settings.StopwatchAppearance.Background);
        Assert.Equal(field=="Frame"?"engraved-filigree":"classic-brass",settings.StopwatchAppearance.Frame);
        Assert.Equal(field=="BackgroundOpacity"?70:25,settings.StopwatchAppearance.BackgroundOpacity);
        Assert.Equal(field=="UnselectedOpacity"?40:55,settings.StopwatchAppearance.UnselectedOpacity);
        Assert.Equal(StopwatchOpaqueLayers.Default,settings.StopwatchAppearance.OpaqueLayers);
        Assert.True(SettingsStore.Save(settings,files.Path));
        Assert.Equal(JsonSerializer.Serialize(settings.StopwatchAppearance),JsonSerializer.Serialize(SettingsStore.Load(files.Path).StopwatchAppearance));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"bad\"")]
    public void MalformedProfileKeepsUnrelatedSettingsAndDoesNotRemigrateLegacyValues(string profile)
    {
        using var files=new Fixture();
        File.WriteAllText(files.Path,"{\"TextSize\":67,\"BackgroundOpacity\":85,\"StopwatchAppearance\":"+profile+"}");
        var settings=SettingsStore.Load(files.Path);
        Assert.Equal(67,settings.TextSize);
        Assert.Equal(85,settings.BackgroundOpacity);
        Assert.Equal(70,settings.StopwatchAppearance.BackgroundOpacity);
    }

    [Theory]
    [InlineData("{}", 70, 40)]
    [InlineData("{\"BackgroundOpacity\":0,\"InactiveSeparatedOverlayOpacity\":35}", 0, 35)]
    [InlineData("{\"BackgroundOpacity\":85,\"InactiveSeparatedOverlayOpacity\":55}", 85, 55)]
    [InlineData("{\"BackgroundOpacity\":20}", 20, 40)]
    [InlineData("{\"StopwatchAppearance\":null,\"BackgroundOpacity\":20}", 20, 40)]
    [InlineData("{\"BackgroundOpacity\":85,\"InactiveSeparatedOverlayOpacity\":55,\"StopwatchAppearance\":{\"BackgroundOpacity\":25,\"UnselectedOpacity\":60}}", 25, 60)]
    public void LoadingMigratesOnlyExplicitLegacyValuesOnce(string json, double background, double unselected)
    {
        using var files = new Fixture();
        File.WriteAllText(files.Path, json);
        var settings = SettingsStore.Load(files.Path);
        Assert.Equal(background, settings.StopwatchAppearance.BackgroundOpacity);
        Assert.Equal(unselected, settings.StopwatchAppearance.UnselectedOpacity);
        Assert.True(SettingsStore.Save(settings, files.Path));
        var restarted = SettingsStore.Load(files.Path);
        Assert.Equal(background, restarted.StopwatchAppearance.BackgroundOpacity);
        Assert.Equal(unselected, restarted.StopwatchAppearance.UnselectedOpacity);
    }

    [Fact]
    public void PartialProfileRepairsOneLayerWithoutOverwritingOtherPreferences()
    {
        using var files = new Fixture();
        File.WriteAllText(files.Path, """
        {"ThemeMode":"Nika's Chronicle","OverlayTheme":"Daylight","TextSize":67,"BackgroundOpacity":85,
         "TodoAppearance":{"Classic":{"Background":"quiet-atlas"}},
         "StopwatchAppearance":{"Background":"midnight-chart","Frame":"../unknown","Artwork":" CLEAN ","OpaqueLayers":63}}
        """);
        var settings = SettingsStore.Load(files.Path);
        var p = settings.StopwatchAppearance;
        Assert.Equal("engraved-filigree", p.Frame);
        Assert.Equal("midnight-chart", p.Background);
        Assert.Equal("navigator-bold", p.Typography);
        Assert.Equal("filigree-keys", p.HoverControls);
        Assert.Equal("clean", p.Artwork);
        Assert.Equal(70, p.BackgroundOpacity);
        p.Artwork = "corner-stamp"; p.Normalize();
        Assert.True(p.OpaqueLayers.HasFlag(StopwatchOpaqueLayers.Artwork));
        Assert.True(SettingsStore.Save(settings, files.Path));
        var after = SettingsStore.Load(files.Path);
        Assert.Equal(67, after.TextSize);
        Assert.Equal(85, after.BackgroundOpacity);
        Assert.Equal(OverlayThemeCatalog.Daylight, after.OverlayTheme);
        Assert.Equal("quiet-atlas", after.TodoAppearance.Classic.Background);
        Assert.Equal(JsonSerializer.Serialize(p), JsonSerializer.Serialize(after.StopwatchAppearance));
    }

    [Fact]
    public void EveryRetainedChoiceSurvivesNormalizationAndIndependentRoundTrip()
    {
        var groups = new[] { StopwatchAppearanceCatalog.Backgrounds, StopwatchAppearanceCatalog.Frames,
            StopwatchAppearanceCatalog.Typography, StopwatchAppearanceCatalog.Artworks, StopwatchAppearanceCatalog.HoverControls };
        Assert.Equal(new[] {12,16,10,3,12}, groups.Select(g => g.Count));
        foreach (var group in groups)
        {
            Assert.Equal(group.Count, group.Select(o => o.Id).Distinct().Count());
            foreach (var item in group)
                Assert.Equal(item.Id, StopwatchAppearanceCatalog.NormalizeId(" " + item.Id.ToUpperInvariant() + " ", group, "invalid"));
        }
        foreach (var frame in StopwatchAppearanceCatalog.Frames)
        {
            var p = new StopwatchAppearanceSettings {Frame=frame.Id, Background="sky-island", HoverControls="quiet-glyphs"};
            var after = JsonSerializer.Deserialize<StopwatchAppearanceSettings>(JsonSerializer.Serialize(p))!;
            after.Normalize();
            Assert.Equal(frame.Id, after.Frame);
            Assert.Equal("sky-island", after.Background);
            Assert.Equal("quiet-glyphs", after.HoverControls);
        }
    }

    [Fact]
    public void InvalidNumbersAndNullProfileCannotReachRendering()
    {
        var p = new StopwatchAppearanceSettings {BackgroundOpacity=double.NaN, UnselectedOpacity=double.PositiveInfinity, OpaqueLayers=(StopwatchOpaqueLayers)255};
        p.Normalize();
        Assert.Equal(70, p.BackgroundOpacity); Assert.Equal(40, p.UnselectedOpacity);
        Assert.Equal(StopwatchOpaqueLayers.All, p.OpaqueLayers);
        p.BackgroundOpacity=-1; p.UnselectedOpacity=150; p.Normalize();
        Assert.Equal(0, p.BackgroundOpacity); Assert.Equal(100, p.UnselectedOpacity);
        var settings = new AppSettings {StopwatchAppearance=null!}; settings.NormalizeForRuntime();
        Assert.Equal("navigator-parchment", settings.StopwatchAppearance.Background);
        var copy=p.Copy(); copy.Frame="classic-brass";
        Assert.Equal("engraved-filigree", p.Frame);
    }

    [Fact]
    public void CorruptPrimaryRetainsExistingBackupRecovery()
    {
        using var files = new Fixture();
        File.WriteAllText(files.Path, "{broken");
        File.WriteAllText(files.Path+".bak", "{\"StopwatchAppearance\":{\"Frame\":\"royal-cartouche\"}}");
        Assert.Equal("royal-cartouche", SettingsStore.Load(files.Path).StopwatchAppearance.Frame);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "stopwatch-appearance-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(directory, "settings.json");
        public Fixture() => Directory.CreateDirectory(directory);
        public void Dispose() => Directory.Delete(directory, true);
    }
}
