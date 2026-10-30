using System.Text.Json;
using StopwatchOverlay;
using Avalonia;
using Avalonia.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoAppearanceChecks
{
    internal static void Settings()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        settings.NormalizeForRuntime();
        using var initial = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        Require(initial.RootElement.TryGetProperty("TodoAppearance", out var appearance), "Old settings must receive a Todo appearance configuration.");
        Require(appearance.GetProperty("Profile").GetString() == "classic", "Classic must be the migration default.");
        Require(appearance.GetProperty("Classic").TryGetProperty("SidebarArtwork",out var artwork)&&artwork.GetString()=="none", "Old settings must keep the optional sidebar artwork layer disabled.");
        Require(appearance.GetProperty("Classic").GetProperty("Rows").GetString() == "ships-ledger", "Classic row default changed.");
        Require(appearance.GetProperty("Pixel").GetProperty("Rows").GetString() == "pixel-voyage", "Pixel defaults must be separate.");
        const string json = """
        {"NikaBackground":"amber-atlas","TodoAppearance":{"Profile":" PIXEL ","Classic":{"Background":"midnight-chart","HeaderEmblem":"zoro","LauncherEmblem":"nami","Rows":"parchment-cards","Details":"foldout-card"},"Pixel":{"Background":"quiet-atlas","HeaderEmblem":"law","LauncherEmblem":"brook","Rows":"cartographers-notes","Priorities":"ledger-stamps","Details":"margin-notes","Toolbar":"classic-brass","EngravedHeader":false}}}
        """;
        settings = JsonSerializer.Deserialize<AppSettings>(json)!;
        settings.NormalizeForRuntime();
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        roundTrip.NormalizeForRuntime();
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(roundTrip));
        var a = saved.RootElement.GetProperty("TodoAppearance");
        Require(a.GetProperty("Profile").GetString() == "pixel", "Profile ID must normalize.");
        Require(a.GetProperty("Classic").GetProperty("LauncherEmblem").GetString() == "nami", "Inactive profile was lost.");
        Require(a.GetProperty("Classic").GetProperty("HeaderEmblem").GetString() == "zoro", "Header and launcher must stay independent.");
        Require(a.GetProperty("Pixel").GetProperty("Rows").GetString() == "cartographers-notes", "Mixed profile choices must survive.");
        Require(!a.GetProperty("Pixel").GetProperty("EngravedHeader").GetBoolean(), "Engraving preference lost.");
        Require(roundTrip.NikaBackground == "amber-atlas", "Todo choices changed controller settings.");
        settings = JsonSerializer.Deserialize<AppSettings>("{\"TodoAppearance\":{\"Profile\":null,\"Classic\":null,\"Pixel\":{\"Background\":\"../file\",\"Rows\":null,\"HeaderEmblem\":\"unknown\"}}}")!;
        settings.NormalizeForRuntime();
        using var repaired = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        var p = repaired.RootElement.GetProperty("TodoAppearance").GetProperty("Pixel");
        Require(p.GetProperty("Background").GetString() == "navigator-parchment", "Invalid background must be safe.");
        Require(p.GetProperty("Rows").GetString() == "pixel-voyage", "Invalid Pixel rows must use Pixel defaults.");
        Require(p.GetProperty("HeaderEmblem").GetString() == "luffy", "Invalid emblem must fall back.");
        var partial=JsonSerializer.Deserialize<AppSettings>("{\"TodoAppearance\":{\"Pixel\":{\"Background\":\"quiet-atlas\"}}}")!;
        partial.NormalizeForRuntime();
        Require(partial.TodoAppearance.Pixel.Rows=="pixel-voyage"&&partial.TodoAppearance.Pixel.Priorities=="pixel-ranks"&&partial.TodoAppearance.Pixel.Details=="foldout-card"&&partial.TodoAppearance.Pixel.Toolbar=="pixel-controls","Partial Pixel settings lost profile defaults.");
        var linked=JsonSerializer.Deserialize<AppSettings>("""
        {"TodoAppearance":{"Classic":{"HeaderEmblem":"zoro","LauncherEmblem":"law","LauncherMatchesHeader":true},"Pixel":{"HeaderEmblem":"nami","LauncherEmblem":"brook"}}}
        """)!;
        linked.NormalizeForRuntime();
        using var linkedSaved=JsonDocument.Parse(JsonSerializer.Serialize(linked));
        var classic=linkedSaved.RootElement.GetProperty("TodoAppearance").GetProperty("Classic");
        Require(classic.TryGetProperty("LauncherMatchesHeader",out var matches)&&matches.GetBoolean(),"Same-as-header preference was not persisted.");
        Require(classic.GetProperty("LauncherEmblem").GetString()=="law","Linking erased the separate launcher selection.");
        var restored=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(linked))!;
        restored.NormalizeForRuntime();
        Require(restored.TodoAppearance.Classic.EffectiveLauncherEmblem=="zoro","Restored link did not follow the header.");
        restored.TodoAppearance.Classic.HeaderEmblem="nami";
        Require(restored.TodoAppearance.Classic.EffectiveLauncherEmblem=="nami","Linked launcher ignored a changed header.");
        restored.TodoAppearance.Classic.LauncherMatchesHeader=false;
        Require(restored.TodoAppearance.Classic.EffectiveLauncherEmblem=="law","Unlinking did not restore the independent emblem.");
        Require(!restored.TodoAppearance.Pixel.LauncherMatchesHeader&&restored.TodoAppearance.Pixel.EffectiveLauncherEmblem=="brook","Link preference leaked between profiles or changed old settings.");
        Require(!classic.TryGetProperty("EffectiveLauncherEmblem",out _),"Derived launcher choice should not be persisted.");
        var murals=JsonSerializer.Deserialize<AppSettings>("""
        {"NikaSidebarArtwork":"cloud-dragon","TodoAppearance":{"Classic":{"SidebarArtwork":" PHOENIX ","SidebarArtworkOpacity":0.32},"Pixel":{"SidebarArtwork":"laboon","SidebarArtworkOpacity":0.51}}}
        """)!;
        murals.NormalizeForRuntime();
        murals=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(murals))!;murals.NormalizeForRuntime();
        Require(murals.TodoAppearance.Classic.SidebarArtwork=="phoenix"&&murals.TodoAppearance.Classic.SidebarArtworkOpacity==.32,"Artwork selection and opacity did not survive a settings round trip.");
        Require(murals.TodoAppearance.Pixel.SidebarArtwork=="laboon"&&murals.TodoAppearance.Pixel.SidebarArtworkOpacity==.51,"Independent Pixel artwork preference was lost.");
        Require(murals.NikaSidebarArtwork=="cloud-dragon","Todo artwork changed the controller selection.");
        foreach(var (value,want) in new[]{(-.2,0d),(1.5,1d),(double.NaN,.18),(double.PositiveInfinity,.18)})
        {
            murals.TodoAppearance.Classic.SidebarArtwork="../image.png";murals.TodoAppearance.Classic.SidebarArtworkOpacity=value;murals.NormalizeForRuntime();
            Require(murals.TodoAppearance.Classic.SidebarArtwork=="none"&&murals.TodoAppearance.Classic.SidebarArtworkOpacity==want,"Invalid artwork or opacity did not normalize safely.");
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Overlay()
    {
        var child=new Border();
        var frame=new StopwatchOverlay.Desktop.Controls.TodoFrame {Child=child,BorderThickness=new Thickness(2),Padding=new Thickness(3)};
        frame.Measure(new Size(100,40));frame.Arrange(new Rect(0,0,100,40));
        Require(child.Bounds==new Rect(5,5,90,30),"Native frame lets its child paint over the reference border.");
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika };
        var window = new TodoOverlayWindow(settings);
        try
        {
            Require(window.Classes.Contains("nikaTodo"), "Nika Todo must use its approved scoped appearance.");
            Require(window.FindControl<Control>("TodoMap") != null, "Todo map artwork is missing.");
            Require(window.FindControl<Control>("TodoEmblem") != null, "Transparent crew emblem is missing.");
            settings.ThemeMode = AppThemeCatalog.Daylight;
            window.ApplySettings();
            Require(!window.Classes.Contains("nikaTodo"), "Nika styling leaked into another theme.");
        }
        finally { window.Close(); }
    }

    internal static void InspectorAndDialogs()
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika };
        var editor = new NoteEntryWindow(NoteType.Todo,settings);
        try { Require(editor.Classes.Contains("nikaTodoDialog"), "Todo editor must use the approved appearance."); }
        finally { editor.Close(); }
        var inspector = new SettingsWindow(settings);
        try
        {
            var nav=inspector.FindControl<ListBox>("NavigationList")!;
            Require(nav.Items.OfType<ListBoxItem>().Any(item => Equals(item.Tag,"TodoAppearance")), "Todo appearance category is missing.");
        }
        finally { inspector.Close(); }
    }
}
