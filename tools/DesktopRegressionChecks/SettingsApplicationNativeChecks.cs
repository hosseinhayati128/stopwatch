using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsApplicationNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/application-implemented";
    private static readonly string[] Actions = [CloseActionChoice.Ask, CloseActionChoice.Minimize, CloseActionChoice.Exit];

    internal static void Controls()
    {
        var model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, CloseAction = CloseActionChoice.Minimize, StartWithWindows = true };
        WithWindow(model, false, window =>
        {
            var changes = new List<SettingsChangeKind>();
            Action<SettingsChangeKind> changed = changes.Add;
            typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(window, [changed]);
            Require(Choice(window, 1).IsChecked == true && Find<CheckBox>(window, "StartWithWindowsCheck").IsChecked == true, "Saved Application choices did not load.");
            for (int index = 2; index >= 0; index--)
            {
                int count = changes.Count;
                Click(Choice(window, index));
                ClockOverlayChecks.Pump(25);
                Require(model.CloseAction == Actions[index] && changes.Count == count + 1 && changes[^1] == SettingsChangeKind.Behavior,
                    "Close action did not emit one update through the existing settings path.");
            }
            Click(Find<CheckBox>(window, "StartWithWindowsCheck"));
            Require(!model.StartWithWindows && changes[^1] == SettingsChangeKind.Startup, "Startup did not use its existing settings callback.");
            int beforeReload = changes.Count;
            model.StartWithWindows = true;
            model.CloseAction = CloseActionChoice.Exit;
            Call(window, "ReloadFromSettings");
            ClockOverlayChecks.Pump(60);
            Require(Choice(window, 2).IsChecked == true && Find<CheckBox>(window, "StartWithWindowsCheck").IsChecked == true && changes.Count == beforeReload,
                "Reloading preferences lost selection or saved during loading.");
            foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
            {
                model.ThemeMode = theme;
                Desktop("ThemeResources", "ApplyApplication", theme);
                Call(window, "ReloadFromSettings");
                Call(window, "UpdatePreviewSafely");
                ClockOverlayChecks.Pump(65);
                if (theme == AppThemeCatalog.Nika) Require(Choice(window, 2).IsChecked == true, "Theme restoration lost the close action.");
                else Require(Find<CheckBox>(window, "StartWithWindowsCheck").Content is string && Find<ComboBox>(window, "CloseActionCombo").IsEffectivelyVisible,
                    "Legacy Application controls were not restored.");
            }
            Require(changes.Count == beforeReload, "Theme restoration committed settings.");
            Require(Choice(window, 2).Focus(), "Close choices cannot receive keyboard focus.");
            Require(KeyboardNavigationHandler.GetNext(Choice(window, 0), NavigationDirection.Down) == Choice(window, 1), "Arrow navigation skips close choices.");
        });
    }

    internal static void Layout()
    {
        foreach (int percent in new[] { 70, 90, 125 })
        foreach (bool compact in new[] { false, true })
        {
            var model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent };
            WithWindow(model, compact, window =>
            {
                var scroll = Visual<ScrollViewer>(window, "ApplicationScroll");
                var done = Visual<Button>(window, "SettingsDone");
                for (int index = 0; index < Actions.Length; index++)
                {
                    Choice(window, index).IsChecked = true;
                    Find<CheckBox>(window, "StartWithWindowsCheck").IsChecked = index == 1;
                    ClockOverlayChecks.Pump(35);
                    CheckFit();
                    if (!compact && percent == 90) Save(window, new[] { "ask", "tray", "exit" }[index]);
                    var location = done.TranslatePoint(default, window);
                    scroll.ScrollToEnd();
                    ClockOverlayChecks.Pump(25);
                    Require(done.TranslatePoint(default, window) == location, "Done moved with the scroll content.");
                    if (compact && percent == 90 && index == 1) Save(window, "compact-tray");
                    scroll.Offset = default;
                }
                if (compact && percent == 125)
                {
                    model.Typography.Global.SizePercent = 175;
                    Desktop("DesktopTypography", "Apply", model.Typography);
                    ClockOverlayChecks.Pump(80);
                    CheckFit();
                    scroll.ScrollToEnd();
                    ClockOverlayChecks.Pump(25);
                    Save(window, "large-text");
                }
                void CheckFit()
                {
                    Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Application overflow at {percent}% / compact={compact}.");
                    var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                    Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the client area.");
                }
            });
        }
    }

    private static void WithWindow(AppSettings model, bool compact, Action<SettingsWindow> check)
    {
        Desktop("ThemeResources", "ApplyApplication", model.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", model);
        Desktop("DesktopTypography", "Initialize", model.Typography);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-application-{Guid.NewGuid():N}.json"));
        var window = new SettingsWindow(model) { ShowActivated = false, ShowInTaskbar = false, Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        try
        {
            window.Show();
            ClockOverlayChecks.Pump(40);
            Visual<Button>(window, "SettingsNav_Application").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(50);
            check(window);
        }
        finally
        {
            window.Close();
            TelegramOutboxStore.SetCustomFilePath(null);
            Desktop("DesktopTypography", "Shutdown");
            Desktop("DesktopPageAppearance", "Shutdown");
        }
    }

    private static void Save(SettingsWindow window, string name)
    {
        Directory.CreateDirectory(Output);
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else
        {
            using var context = bitmap.CreateDrawingContext();
            context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size));
        }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static RadioButton Choice(Window window, int index) => Visual<RadioButton>(window, "ApplicationClose" + index);
    private static T Find<T>(Window window, string name) where T : Control => window.FindControl<T>(name)!;
    private static T Visual<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing " + name);
    private static void Click(ToggleButton button) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(button, null);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Hidden)!.Invoke(target, null);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
