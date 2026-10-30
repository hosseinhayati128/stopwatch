using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StopwatchOverlay;

public sealed class TodoAppearanceSettings
{
    public int Version { get; set; } = 1;
    public string Profile { get; set; } = "classic";
    public TodoAppearanceProfile Classic { get; set; } = new();
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public TodoAppearanceProfile Pixel { get; set; } = TodoAppearanceProfile.PixelDefaults();

    [JsonIgnore] public TodoAppearanceProfile Current => Profile == "pixel" ? Pixel : Classic;
    [JsonIgnore] public bool IsPixel => Profile == "pixel";

    public void Normalize()
    {
        Version = 1;
        Profile = string.Equals(Profile?.Trim(), "pixel", StringComparison.OrdinalIgnoreCase) ? "pixel" : "classic";
        Classic ??= new();
        Pixel ??= TodoAppearanceProfile.PixelDefaults();
        // Deserialization can leave both fields referring to one mutable object in callers.
        if (ReferenceEquals(Classic, Pixel)) Pixel = Pixel.Copy();
        Classic.Normalize(false);
        Pixel.Normalize(true);
    }
}

public sealed class TodoAppearanceProfile
{
    public string Background { get; set; } = "navigator-parchment";
    public string SidebarArtwork { get; set; } = "none";
    public double SidebarArtworkOpacity { get; set; } = .18;
    public string HeaderEmblem { get; set; } = "luffy";
    public string LauncherEmblem { get; set; } = "luffy";
    // Keep the separate choice so unlinking restores it. Missing settings preserve existing behavior.
    public bool LauncherMatchesHeader { get; set; }
    [JsonIgnore] public string EffectiveLauncherEmblem => LauncherMatchesHeader ? HeaderEmblem : LauncherEmblem;
    public bool EngravedHeader { get; set; } = true;
    public string Rows { get; set; } = "ships-ledger";
    public string Priorities { get; set; } = "original-circles";
    public string Details { get; set; } = "margin-notes";
    public string Toolbar { get; set; } = "classic-brass";

    public static TodoAppearanceProfile PixelDefaults() => new()
    {
        Rows = "pixel-voyage", Priorities = "pixel-ranks", Details = "foldout-card", Toolbar = "pixel-controls"
    };
    public TodoAppearanceProfile Copy() => (TodoAppearanceProfile)MemberwiseClone();
    public void Normalize(bool pixel)
    {
        Background = TodoAppearanceCatalog.Normalize(Background, TodoAppearanceCatalog.Backgrounds, "navigator-parchment");
        SidebarArtwork = TodoAppearanceCatalog.Normalize(SidebarArtwork, TodoAppearanceCatalog.SidebarArtworks, "none");
        SidebarArtworkOpacity = double.IsFinite(SidebarArtworkOpacity) ? Math.Clamp(SidebarArtworkOpacity, 0, 1) : .18;
        HeaderEmblem = TodoAppearanceCatalog.Normalize(HeaderEmblem, TodoAppearanceCatalog.Emblems, "luffy");
        LauncherEmblem = TodoAppearanceCatalog.Normalize(LauncherEmblem, TodoAppearanceCatalog.Emblems, "luffy");
        Rows = TodoAppearanceCatalog.Normalize(Rows, TodoAppearanceCatalog.Rows, pixel ? "pixel-voyage" : "ships-ledger");
        Priorities = TodoAppearanceCatalog.Normalize(Priorities, TodoAppearanceCatalog.Priorities, pixel ? "pixel-ranks" : "original-circles");
        Details = TodoAppearanceCatalog.Normalize(Details, TodoAppearanceCatalog.Details, pixel ? "foldout-card" : "margin-notes");
        Toolbar = TodoAppearanceCatalog.Normalize(Toolbar, TodoAppearanceCatalog.Toolbars, pixel ? "pixel-controls" : "classic-brass");
    }
}

public sealed record TodoAppearanceOption(string Id, string Label)
{
    public override string ToString() => Label;
}

public static class TodoAppearanceCatalog
{
    public static IReadOnlyList<TodoAppearanceOption> Profiles { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("classic", "Classic"), new TodoAppearanceOption("pixel", "Pixel Voyage") });
    public static IReadOnlyList<TodoAppearanceOption> Backgrounds { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("navigator-parchment", "Navigator Parchment"), new("amber-vellum", "Amber Vellum"),
        new("midnight-chart", "Midnight Chart"), new("quiet-atlas", "Quiet Atlas") });
    public static IReadOnlyList<TodoAppearanceOption> SidebarArtworks { get; } = Array.AsReadOnly(
        new[] { new TodoAppearanceOption("none", "None") }.Concat(
            NikaThemeCatalog.Artworks.Select(option => new TodoAppearanceOption(option.Id, option.Name))).ToArray());
    public static IReadOnlyList<TodoAppearanceOption> Rows { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("ships-ledger", "Ship's Ledger"), new("parchment-cards", "Parchment Cards"),
        new("cartographers-notes", "Cartographer's Notes"), new("pixel-voyage", "Pixel Voyage") });
    public static IReadOnlyList<TodoAppearanceOption> Priorities { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("original-circles", "Original Circles"), new("ledger-stamps", "Ledger Stamps"), new("pixel-ranks", "Pixel Ranks") });
    public static IReadOnlyList<TodoAppearanceOption> Details { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("margin-notes", "Margin Notes"), new("foldout-card", "Foldout Card") });
    public static IReadOnlyList<TodoAppearanceOption> Toolbars { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("classic-brass", "Classic Brass"), new("pixel-controls", "Pixel Controls") });
    public static IReadOnlyList<TodoAppearanceOption> Emblems { get; } = Array.AsReadOnly(new[] {
        new TodoAppearanceOption("luffy", "Luffy"), new("zoro", "Zoro"), new("nami", "Nami"), new("usopp", "Usopp"),
        new("sanji", "Sanji"), new("chopper", "Chopper"), new("robin", "Robin"), new("franky", "Franky"),
        new("brook", "Brook"), new("jinbe", "Jinbe"), new("law", "Law"), new("whitebeard", "Whitebeard"),
        new("ace", "Ace"), new("buggy", "Buggy"), new("shanks", "Shanks"), new("roger", "Roger") });

    public static string Normalize(string? value, IReadOnlyList<TodoAppearanceOption> options, string fallback)
    {
        foreach (var option in options)
            if (string.Equals(value?.Trim(), option.Id, StringComparison.OrdinalIgnoreCase)) return option.Id;
        return fallback;
    }
}
