using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class StopwatchFrameGalleryChecks
{
    internal static void Run() => Run("Frames", 16, "Royal Cartouche", p => p.Frame = "royal-cartouche");
    internal static void RunBackgrounds() => Run("Backgrounds", 12, "Midnight Chart", p => p.Background = "midnight-chart");
    internal static void RunTypography() => Run("Typography", 10, "Typewriter Log", p => p.Typography = "typewriter-log");

    private static void Run(string layer, int count, string label, Action<StopwatchAppearanceSettings> expectedChoice)
    {
        foreach (int scale in new[] { 70, 90, 125 })
        {
            string trigger = "BrowseStopwatch" + layer;
            var settings = new AppSettings { TextColor = "Theme default", ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale };
            Call("ThemeResources", "ApplyApplication", settings.ThemeMode);
            Call("DesktopPageAppearance", "Initialize", settings);
            var window = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false };
            int commits = 0;
            Action<SettingsChangeKind> changed = kind => { if (kind == SettingsChangeKind.OverlayAppearance) commits++; };
            typeof(SettingsWindow).GetEvent("SettingsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetAddMethod(true)!.Invoke(window, [changed]);
            T Named<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name && c.IsEffectivelyVisible);
            void Click(string name) => Named<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            void Wait() => ClockOverlayChecks.Pump(130);
            void KeyPress(Control control, Key key) => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
            try
            {
                window.Show(); Wait();
                Click("SettingsNav_StopwatchAppearance"); Wait();
                Click(trigger); Wait();
                var list = Named<ListBox>("GalleryOptions");
                var original = (ListBoxItem)list.SelectedItem!;
                Require(list.ItemCount == count && original.IsFocused && commits == 0,
                    "Opening must focus the saved choice without committing.");
                var grid = list.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.UniformGrid>().Single();
                int expectedColumns = layer switch
                {
                    "Typography" => 2,
                    "Backgrounds" when scale == 90 => 3,
                    _ => scale == 125 ? 2 : 4
                };
                Require(grid.Columns == expectedColumns, $"Incorrect {layer} column count.");
                var host = Named<ContentControl>("StopwatchPreviewHost");
                var expected = settings.StopwatchAppearance.Copy();
                expectedChoice(expected);
                var before = StopwatchAppearanceChecks.Render(host, null);
                int expectedCommits = 1;
                int selectedIndex = count - 1;
                if (layer == "Frames")
                {
                    KeyPress(original, Avalonia.Input.Key.End);
                }
                else
                {
                    KeyPress(original, Avalonia.Input.Key.Right);
                    KeyPress((ListBoxItem)list.SelectedItem!, Avalonia.Input.Key.Right);
                    selectedIndex = 2;
                    expectedCommits = 2;
                }
                Wait();
                Require(commits == expectedCommits &&
                    System.Text.Json.JsonSerializer.Serialize(settings.StopwatchAppearance) ==
                    System.Text.Json.JsonSerializer.Serialize(expected), "Selection must commit only the chosen layer.");
                Require(!before.SequenceEqual(StopwatchAppearanceChecks.Render(host, null)), "Live preview did not change.");
                if (layer == "Typography")
                {
                    var samples = list.GetVisualDescendants().OfType<ThemedClockFace>().ToArray();
                    Require(samples.Length == 10 && samples.Concat(host.GetVisualDescendants().OfType<ThemedClockFace>())
                        .All(face => face.Text == "12:34:56.0" && face.ProjectName == "Illustration"), "Inconsistent type specimens.");
                    var distinctSamples = samples.Select(face => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        StopwatchAppearanceChecks.Render(face, null)))).Distinct().Count();
                    Require(distinctSamples == count, "Some typefaces rendered the same sample.");
                }
                Require(list.SelectedIndex == selectedIndex && Named<TextBlock>("GallerySummary").Text!.Contains(label),
                    "Gallery summary or selection did not update.");
                var selected = (ListBoxItem)list.SelectedItem!;
                var top = selected.TranslatePoint(default,list)!.Value.Y;
                Require(top >= -1 && top + selected.Bounds.Height <= list.Bounds.Height + 1, "Selected option scrolled out of view.");
                var check = selected.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p=>p.Classes.Contains("frameCheck"));
                Require(check.IsEffectivelyVisible, "Missing selected option checkmark.");
                Capture(window, scale, layer);
                KeyPress(selected, Avalonia.Input.Key.Escape); Wait();
                Require(Named<Button>(trigger).IsFocused && commits == expectedCommits, "Escape must return focus and preserve selection.");
                Click(trigger); Wait();
                Require(Named<ListBox>("GalleryOptions").SelectedIndex == selectedIndex, "Reopening lost the selected option.");
                if (layer == "Typography" && scale == 125)
                {
                    var reopened = Named<ListBox>("GalleryOptions");
                    KeyPress((ListBoxItem)reopened.SelectedItem!, Avalonia.Input.Key.End); Wait();
                    var last = (ListBoxItem)reopened.SelectedItem!;
                    var y = last.TranslatePoint(default, reopened)!.Value.Y;
                    Require(reopened.SelectedIndex == 9 && y >= -1 && y + last.Bounds.Height <= reopened.Bounds.Height + 1,
                        "The final typeface did not scroll into view.");
                    Capture(window, scale, layer, "lower");
                    expectedCommits++;
                }
                Click("GalleryBack"); Wait();
                Require(host.GetVisualDescendants().OfType<ThemedClockFace>().All(face => face.Text == "00:44:27.0"),
                    "Back must restore the standard preview time.");
                Click(trigger); Wait();
                Click("SettingsNav_TodoAppearance"); Wait();
                Require(window.FindControl<ScrollViewer>("SettingsScrollViewer")!.IsEffectivelyVisible && commits == expectedCommits, "Leaving the gallery hid another page or wrote settings.");
                Click("SettingsNav_StopwatchAppearance"); Wait();
                Require(Named<Button>(trigger).IsVisible, "Browse row missing after changing category.");
                Console.WriteLine($"PASS {scale}%: {list.ItemCount} choices, live choice, keyboard and category return.");
            }
            finally { window.Close(); Call("DesktopPageAppearance", "Shutdown"); }
        }
    }
    private static void Capture(Window window, int scale, string layer, string state = "selected")
    {
        string directory = Path.Combine("design","one-piece-v2","settings",layer.ToLowerInvariant().TrimEnd('s') + "-gallery-implementation");
        Directory.CreateDirectory(directory);
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var image = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize,1),new Vector(96,96));
        var transform = frame.RenderTransform;
        frame.SetCurrentValue(Visual.RenderTransformProperty,null);
        try
        {
            using var context = image.CreateDrawingContext();
            context.DrawRectangle(new VisualBrush(frame) { Stretch=Stretch.Fill, SourceRect=new RelativeRect(new Rect(frame.Bounds.Size),RelativeUnit.Absolute) },null,new Rect(window.ClientSize));
        }
        finally { frame.SetCurrentValue(Visual.RenderTransformProperty,transform); }
        image.Save(Path.Combine(directory,$"{state}-{scale}.png"));
    }
    private static void Call(string type,string method,params object[] args) =>
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!
            .GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args);
    private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
