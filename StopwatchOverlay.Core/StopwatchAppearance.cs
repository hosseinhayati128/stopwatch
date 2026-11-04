using System.Text.Json.Serialization;

namespace StopwatchOverlay;

[Flags]
public enum StopwatchOpaqueLayers
{
    None=0, Frame=1, TimeText=2, ProjectName=4, StateMarkers=8, HoverControls=16, Artwork=32,
    Default=Frame|TimeText|ProjectName|StateMarkers|HoverControls,
    All=Default|Artwork
}

/// <summary>Independent visual layers for every Nika stopwatch window.</summary>
[JsonConverter(typeof(StopwatchAppearanceJsonConverter))]
public sealed class StopwatchAppearanceSettings
{
    public int Version { get; set; } = 1;
    public string Background { get; set; } = "navigator-parchment";
    public string Frame { get; set; } = "engraved-filigree";
    public string Typography { get; set; } = "navigator-bold";
    public string Artwork { get; set; } = "clean";
    public string HoverControls { get; set; } = "filigree-keys";
    public double BackgroundOpacity { get; set; } = 70;
    public double UnselectedOpacity { get; set; } = 40;
    public StopwatchOpaqueLayers OpaqueLayers { get; set; } = StopwatchOpaqueLayers.Default;

    public StopwatchAppearanceSettings Copy() => (StopwatchAppearanceSettings)MemberwiseClone();

    public void Normalize()
    {
        Version=1;
        Background=StopwatchAppearanceCatalog.NormalizeId(Background,StopwatchAppearanceCatalog.Backgrounds,"navigator-parchment");
        Frame=StopwatchAppearanceCatalog.NormalizeId(Frame,StopwatchAppearanceCatalog.Frames,"engraved-filigree");
        Typography=StopwatchAppearanceCatalog.NormalizeId(Typography,StopwatchAppearanceCatalog.Typography,"navigator-bold");
        Artwork=StopwatchAppearanceCatalog.NormalizeId(Artwork,StopwatchAppearanceCatalog.Artworks,"clean");
        HoverControls=StopwatchAppearanceCatalog.NormalizeId(HoverControls,StopwatchAppearanceCatalog.HoverControls,"filigree-keys");
        BackgroundOpacity=double.IsFinite(BackgroundOpacity)?Math.Clamp(BackgroundOpacity,0,100):70;
        UnselectedOpacity=double.IsFinite(UnselectedOpacity)?Math.Clamp(UnselectedOpacity,10,100):40;
        OpaqueLayers &= StopwatchOpaqueLayers.All;
    }
}

public sealed record StopwatchAppearanceOption(string Id, string Label)
{
    public override string ToString() => Label;
}

public static class StopwatchAppearanceCatalog
{
    public static IReadOnlyList<StopwatchAppearanceOption> Backgrounds { get; } = Array.AsReadOnly(new[] {
        new StopwatchAppearanceOption("navigator-parchment","Navigator Parchment"),new("amber-vellum","Amber Vellum"),
        new("midnight-chart","Midnight Chart"),new("quiet-atlas","Quiet Atlas"),new("ships-timber","Ship's Timber"),
        new("sailcloth","Sailcloth"),new("captains-leather","Captain's Leather"),new("aged-bronze","Aged Bronze"),
        new("tideglass","Tideglass"),new("wano-waves","Wano Waves"),new("celestial-navigation","Celestial Navigation"),new("sky-island","Sky Island") });
    public static IReadOnlyList<StopwatchAppearanceOption> Frames { get; } = Array.AsReadOnly(new[] {
        new StopwatchAppearanceOption("classic-brass","Classic Brass"),new("navy-bevel","Navy Bevel"),new("walnut-rim","Walnut Rim"),
        new("pixel-voyage","Pixel Voyage"),new("instrument-case","Instrument Case"),new("slim-capsule","Slim Capsule"),
        new("rope-binding","Rope Binding"),new("chart-scroll","Chart Scroll"),new("admiralty","Admiralty"),new("bound-logbook","Bound Logbook"),
        new("cloud-crest","Cloud Crest"),new("tide-curve","Tide Curve"),new("engraved-filigree","Engraved Filigree"),new("open-corners","Open Corners"),
        new("vine-filigree","Vine Filigree"),new("royal-cartouche","Royal Cartouche") });
    public static IReadOnlyList<StopwatchAppearanceOption> Typography { get; } = Array.AsReadOnly(new[] {
        new StopwatchAppearanceOption("navigator-bold","Navigator Bold"),new("captains-serif","Captain's Serif"),new("typewriter-log","Typewriter Log"),
        new("instrument-digital","Instrument Digital"),new("pixel-voyage","Pixel Voyage"),new("modern-deck","Modern Deck"),new("art-deco","Art Deco"),
        new("rounded-deck","Rounded Deck"),new("chart-plotter","Chart Plotter"),new("compass-outline","Compass Outline") });
    public static IReadOnlyList<StopwatchAppearanceOption> Artworks { get; } = Array.AsReadOnly(new[] {
        new StopwatchAppearanceOption("clean","Clean"),new("corner-stamp","Corner Stamp"),new("edge-engraving","Edge Engraving") });
    public static IReadOnlyList<StopwatchAppearanceOption> HoverControls { get; } = Array.AsReadOnly(new[] {
        new StopwatchAppearanceOption("classic-brass","Classic Brass"),new("compass-dials","Compass Dials"),new("floating-buttons","Floating Buttons"),
        new("pixel-controls","Pixel Controls"),new("bronze-keys","Bronze Keys"),new("quiet-glyphs","Quiet Glyphs"),new("wooden-deck","Wooden Deck"),
        new("porcelain-keys","Porcelain Keys"),new("glass-dock","Glass Dock"),new("filigree-keys","Filigree Keys"),
        new("vine-filigree","Vine Filigree"),new("royal-cartouche","Royal Cartouche") });

    public static string NormalizeId(string? value,IReadOnlyList<StopwatchAppearanceOption> choices,string fallback)
        => choices.FirstOrDefault(o=>string.Equals(o.Id,value?.Trim(),StringComparison.OrdinalIgnoreCase))?.Id ?? fallback;
}
