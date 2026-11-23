using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoEmblemPickerChecks
{
    const string Output = "design/one-piece-v2/todo-emblem-picker/implemented";

    internal static void Run()
    {
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            foreach (string profile in new[] { "classic", "pixel" })
                CheckProfile(scale, profile);
            TodoTaskMenuPreview.Comparison(Output, scale, ["launcher", "header-narrow"],
                ["Launcher · regular width", "Header · minimum width"], "picker");
        }
    }

    static void CheckProfile(int scale, string profile)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale,
            ActivityWatchEnabled = false, InternetMonitorEnabled = false, TelegramEnabled = false, HabiticaEnabled = false };
        settings.TodoAppearance.Profile = profile;
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
        DeleteTodoNativeChecks.Desktop("DesktopTypography", "Initialize", settings.Typography);
        var look = settings.TodoAppearance.Current;
        var inspector = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false };
        TodoEmblemPickerWindow Open(string which)
        {
            Click(Named<Button>(inspector, "Choose" + which + "Emblem"));
            ClockOverlayChecks.Pump(50);
            return inspector.OwnedWindows.OfType<TodoEmblemPickerWindow>().Single();
        }
        try
        {
            inspector.Show();
            var nav = inspector.FindControl<ListBox>("NavigationList")!;
            nav.SelectedItem = nav.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "TodoAppearance"));
            ClockOverlayChecks.Pump(60);
            foreach (bool launcher in new[] { true, false })
            {
                var picker = Open(launcher ? "Launcher" : "Header");
                var grid = picker.GetVisualDescendants().OfType<UniformGrid>().Single();
                var scroll = picker.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(grid.Columns == 4 && grid.Children.Count == TodoAppearanceCatalog.Emblems.Count, "Regular emblem gallery");
                picker.Width = picker.MinWidth;
                ClockOverlayChecks.Pump(50);
                Require(grid.Columns == 3, "The picker must reflow to three columns at its minimum width.");
                double zoom = scale / 100d;
                Require(Math.Abs(picker.MinWidth - 420 * zoom) < 1, "Settings scale was not forwarded");
                if (launcher) picker.Width = 568 * zoom;
                else picker.Height = 575 * zoom;
                ClockOverlayChecks.Pump(50);
                Require(grid.Columns == (launcher ? 4 : 3), "Resize did not restore the expected columns");
                var last = Named<Button>(picker, "Emblem_" + TodoAppearanceCatalog.Emblems.Last().Id);
                last.BringIntoView();
                ClockOverlayChecks.Pump(50);
                Require(VisibleInside(last, scroll), "The final emblem cannot be reached");
                scroll.Offset = default;
                ClockOverlayChecks.Pump(35);
                string id = launcher ? "zoro" : "whitebeard";
                var button = Named<Button>(picker, "Emblem_" + id);
                button.BringIntoView();
                Click(button);
                ClockOverlayChecks.Pump(50);
                Require(VisibleInside(button, scroll), $"{profile} {scale}% {id} clipped: " +
                    $"{button.TranslatePoint(default, scroll)} + {button.Bounds.Size}, viewport {scroll.Bounds.Size}, offset {scroll.Offset}");
                Require(look.HeaderEmblem == "luffy" && look.LauncherEmblem == (launcher ? "luffy" : "zoro"), "Preview wrote through to settings");
                var art = Named<TodoArtwork>(picker, "PreviewEmblem");
                Require(art.Choice == id && picker.SelectedEmblem == id && picker.Result == null, "Draft preview mismatch");
                var start = art.TranslatePoint(default, picker)!.Value;
                var end = art.TranslatePoint(new Point(art.Bounds.Width, art.Bounds.Height), picker)!.Value;
                int expected = launcher ? 72 : 64;
                Require(Math.Abs(end.Y - start.Y - expected) <= 1.5 && Math.Abs(end.X - start.X - expected) <= 1.5, "Actual-size sample scaled with the picker");
                Require(grid.GetVisualDescendants().OfType<RadioButton>().Count(b => b.IsChecked == true) == 1 &&
                    button is RadioButton { IsChecked: true }, "Emblem selection is not exclusive");
                Require(grid.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "SelectionCheck" && b.IsVisible) == 1, "Missing or duplicate checkmark");
                Require(grid.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "EmblemLabel")
                    .All(t => t.Bounds.Width + 1 >= t.TextLayout.WidthIncludingTrailingWhitespace), "Emblem name clipped");
                var shell = Named<TodoFrame>(picker, "PickerShell");
                Require(shell.Pixel == (profile == "pixel") && VisibleInside(Named<Button>(picker, "UseEmblem"), picker), "Profile or footer layout");
                var origin = shell.TranslatePoint(default, picker)!.Value;
                Require(Math.Abs(shell.Bounds.Width * zoom - picker.ClientSize.Width) < 2 &&
                    Math.Abs(shell.Bounds.Height * zoom - picker.ClientSize.Height) < 2 &&
                    Math.Abs(origin.X) < 2 && Math.Abs(origin.Y) < 2,
                    $"{profile} {scale}% {id}: shell {shell.Bounds} at {origin} must fill client {picker.ClientSize}.");
                Capture(shell, zoom, $"{profile}-{(launcher ? "launcher" : "header-narrow")}-{scale}");
                Click(Named<Button>(picker, launcher ? "UseEmblem" : "CancelEmblem"));
                ClockOverlayChecks.Pump(40);
                Require(!picker.IsVisible && look.LauncherEmblem == "zoro" && look.HeaderEmblem == "luffy", "Use or Cancel changed the wrong preference");
            }

            Named<CheckBox>(inspector, "TodoLauncherMatchesHeader").IsChecked = true;
            Require(!Named<Button>(inspector, "ChooseLauncherEmblem").IsEnabled, "Linked launcher can be edited");
            var header = Open("Header");
            Click(Named<Button>(header, "Emblem_law"));
            Click(Named<Button>(header, "UseEmblem"));
            ClockOverlayChecks.Pump(40);
            Require(look.EffectiveLauncherEmblem == "law" && look.LauncherEmblem == "zoro", "Linked header lost the separate launcher choice");
            var reopened = Open("Header");
            Require(reopened.SelectedEmblem == "law", "Reopening lost the applied choice");
            var previous = Named<Button>(reopened, "Emblem_law");
            previous.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Down });
            Require(reopened.SelectedEmblem == "shanks" && look.HeaderEmblem == "law", "Keyboard navigation did not stage the next row");
            reopened.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
            ClockOverlayChecks.Pump(40);
            Require(!reopened.IsVisible && look.HeaderEmblem == "law", "Escape applied a draft");
            Console.WriteLine($"PASS {profile} at {scale}% · launcher, narrow header, linked launcher, apply, cancel, escape");
        }
        finally { inspector.Close(); }
    }

    static void Capture(TodoFrame shell, double zoom, string name)
    {
        Directory.CreateDirectory(Output);
        var size = new PixelSize((int)Math.Ceiling(shell.Bounds.Width * zoom), (int)Math.Ceiling(shell.Bounds.Height * zoom));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * zoom, 96 * zoom));
        // The capture DPI supplies the same scale as the window's layout transform.
        var transform = shell.RenderTransform;
        shell.RenderTransform = null;
        try { bitmap.Render(shell); }
        finally { shell.RenderTransform = transform; }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    static bool VisibleInside(Control child, Control parent)
    {
        var start = child.TranslatePoint(default, parent)!.Value;
        var end = child.TranslatePoint(new Point(child.Bounds.Width, child.Bounds.Height), parent)!.Value;
        return start.X >= -1 && start.Y >= -1 && end.X <= parent.Bounds.Width + 1 && end.Y <= parent.Bounds.Height + 1;
    }

    static T Named<T>(Control host, string name) where T : Control =>
        host.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
