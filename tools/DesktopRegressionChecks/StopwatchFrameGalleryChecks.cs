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
    internal static void Run() => Run(backgrounds: false);
    internal static void RunBackgrounds() => Run(backgrounds: true);

    private static void Run(bool backgrounds)
    {
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var trigger = backgrounds ? "BrowseStopwatchBackgrounds" : "BrowseStopwatchFrames";
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
                Require(list.ItemCount == (backgrounds ? 12 : 16) && original.IsFocused && commits == 0,
                    "Opening must focus the saved choice without committing.");
                if (backgrounds)
                {
                    var grid = list.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.UniformGrid>().Single();
                    Require(grid.Columns == (scale == 70 ? 4 : scale == 90 ? 3 : 2), "Incorrect background column count.");
                    var host = Named<ContentControl>("StopwatchPreviewHost");
                    var before = StopwatchAppearanceChecks.Render(host, null);
                    KeyPress(original, Avalonia.Input.Key.Right);
                    KeyPress((ListBoxItem)list.SelectedItem!, Avalonia.Input.Key.Right);
                    Wait();
                    Require(settings.StopwatchAppearance.Background == "midnight-chart" && commits == 2,
                        "Background selection did not commit once per change.");
                    Require(settings.StopwatchAppearance.Frame == "engraved-filigree" &&
                        settings.StopwatchAppearance.Typography == "navigator-bold", "Background selection changed another layer.");
                    Require(!before.SequenceEqual(StopwatchAppearanceChecks.Render(host, null)), "Live preview did not change.");
                }
                else
                {
                    KeyPress(original, Avalonia.Input.Key.End); Wait();
                    Require(settings.StopwatchAppearance.Frame == "royal-cartouche" && commits == 1, "Frame selection failed.");
                    Require(settings.StopwatchAppearance.Background == "navigator-parchment" &&
                        settings.StopwatchAppearance.Typography == "navigator-bold", "Frame selection changed another layer.");
                }
                int selectedIndex = backgrounds ? 2 : 15;
                int expectedCommits = backgrounds ? 2 : 1;
                var label = backgrounds ? "Midnight Chart" : "Royal Cartouche";
                Require(list.SelectedIndex == selectedIndex && Named<TextBlock>("GallerySummary").Text!.Contains(label),
                    "Gallery summary or selection did not update.");
                var selected = (ListBoxItem)list.SelectedItem!;
                var top = selected.TranslatePoint(default,list)!.Value.Y;
                Require(top >= -1 && top + selected.Bounds.Height <= list.Bounds.Height + 1, "Selected option scrolled out of view.");
                var check = selected.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p=>p.Classes.Contains("frameCheck"));
                Require(check.IsEffectivelyVisible, "Missing selected option checkmark.");
                Capture(window,scale,backgrounds);
                KeyPress(selected, Avalonia.Input.Key.Escape); Wait();
                Require(Named<Button>(trigger).IsFocused && commits == expectedCommits, "Escape must return focus and preserve selection.");
                Click(trigger); Wait();
                Require(Named<ListBox>("GalleryOptions").SelectedIndex == selectedIndex, "Reopening lost the selected option.");
                Click("GalleryBack"); Wait();
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
    private static void Capture(Window window,int scale,bool backgrounds)
    {
        string directory = Path.Combine("design","one-piece-v2","settings",backgrounds ? "background-gallery-implementation" : "frame-gallery-implementation");
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
        image.Save(Path.Combine(directory,$"selected-{scale}.png"));
    }
    private static void Call(string type,string method,params object[] args) =>
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!
            .GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args);
    private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
