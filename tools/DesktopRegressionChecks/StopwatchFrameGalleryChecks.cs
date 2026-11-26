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
    internal static void Run()
    {
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale };
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
                Click("BrowseStopwatchFrames"); Wait();
                var list = Named<ListBox>("FrameOptions");
                var original = (ListBoxItem)list.SelectedItem!;
                Require(list.ItemCount == 16 && original.IsFocused && commits == 0, "Opening must focus the saved frame without committing.");
                KeyPress(original, Avalonia.Input.Key.End); Wait();
                Require(list.SelectedIndex == 15 && settings.StopwatchAppearance.Frame == "royal-cartouche" && commits == 1, "Keyboard selection failed.");
                Require(settings.StopwatchAppearance.Background == "navigator-parchment" && settings.StopwatchAppearance.Typography == "navigator-bold", "Frame selection changed another layer.");
                Require(Named<TextBlock>("FrameSummary").Text!.Contains("Royal Cartouche"), "Selection summary did not change.");
                var selected = (ListBoxItem)list.SelectedItem!;
                var top = selected.TranslatePoint(default,list)!.Value.Y;
                Require(top >= -1 && top + selected.Bounds.Height <= list.Bounds.Height + 1, "Selected frame scrolled out of view.");
                var check = selected.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p=>p.Classes.Contains("frameCheck"));
                Require(check.IsEffectivelyVisible, "Missing selected frame checkmark.");
                Capture(window,scale);
                KeyPress(selected, Avalonia.Input.Key.Escape); Wait();
                Require(Named<Button>("BrowseStopwatchFrames").IsFocused && commits == 1, "Escape must return focus and preserve selection.");
                Click("BrowseStopwatchFrames"); Wait();
                Require(Named<ListBox>("FrameOptions").SelectedIndex == 15, "Reopening lost chosen frame.");
                Click("FrameGalleryBack"); Wait();
                Click("BrowseStopwatchFrames"); Wait();
                Click("SettingsNav_TodoAppearance"); Wait();
                Require(window.FindControl<ScrollViewer>("SettingsScrollViewer")!.IsEffectivelyVisible && commits == 1, "Leaving the gallery hid another page or wrote settings.");
                Click("SettingsNav_StopwatchAppearance"); Wait();
                Require(Named<Button>("BrowseStopwatchFrames").IsVisible, "Frame row missing after changing category.");
                Console.WriteLine($"PASS {scale}%: all 16 frames, live choice, keyboard and category return.");
            }
            finally { window.Close(); Call("DesktopPageAppearance", "Shutdown"); }
        }
    }
    private static void Capture(Window window,int scale)
    {
        string directory = Path.Combine("design","one-piece-v2","settings","frame-gallery-implementation");
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
