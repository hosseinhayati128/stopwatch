using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class NikaThemeChecks
{
    internal static void Run()
    {
        var apply = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication", BindingFlags.Static | BindingFlags.NonPublic)!;
        apply.Invoke(null, new object[] { "Nika's Chronicle" });
        Application.Current!.TryFindResource("PrimaryActionBrush", out var paletteValue);
        if (paletteValue is not ISolidColorBrush palette || palette.Color != Color.Parse("#167C70"))
            throw new Exception("Nika must reuse the existing One Piece application palette: " + paletteValue);
        CheckArtworkGeometryAndAlpha();
        var lapBuilder = typeof(ControllerWindow).GetMethod("BuildNikaLaps", BindingFlags.NonPublic | BindingFlags.Static)!;
        var lapRows = (IReadOnlyList<NikaLapRow>)lapBuilder.Invoke(null, new object[] { new[] { "Lap 3: 00:44:27.0", "Lap 2: 00:29:18.0", "Lap 1: 00:14:36.0" } })!;
        if (lapRows[0].Split != "00:15:09" || lapRows[1].Split != "00:14:42" || lapRows[2].Split != "00:14:36")
            throw new Exception("Nika lap splits do not derive from recorded totals.");
        var malformedLaps = (IReadOnlyList<NikaLapRow>)lapBuilder.Invoke(null, new object[] { new[] { "Lap 2: unknown", "Lap 1: 00:00:02.0" } })!;
        if (malformedLaps[0].Split != "—" || malformedLaps[0].Total != "Lap 2: unknown")
            throw new Exception("Malformed stored lap text must remain visible without an invented split.");
        var constructor = typeof(ControllerWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(AppSettings) }, null)
            ?? throw new Exception("A controller preview must avoid reading or saving user data.");
        var settings = new AppSettings { ThemeMode = "Nika's Chronicle" };
        var editor = new SettingsWindow(settings);
        try
        {
            if (!editor.TryFindResource("PrimaryActionBrush", out var editorAction) || editorAction is not ISolidColorBrush editorBrush || editorBrush.Color != Color.Parse("#167C70"))
                throw new Exception("Nika settings lost the original One Piece palette.");
            var background = editor.FindControl<ComboBox>("NikaBackgroundCombo")!;
            var typography = editor.FindControl<ComboBox>("NikaTypographyCombo")!;
            var artwork = editor.FindControl<ComboBox>("NikaArtworkCombo")!;
            if (background.ItemCount != 12 || typography.ItemCount != 4 || artwork.ItemCount != 17)
                throw new Exception("Nika settings do not expose all approved choices.");
            string overlay = settings.OverlayTheme;
            string font = settings.FontFamily;
            background.SelectedIndex = 1; typography.SelectedIndex = 3; artwork.SelectedIndex = 16;
            if (settings.NikaBackground != "amber-atlas" || settings.NikaTypography != "navigator-precision" || settings.NikaSidebarArtwork != "poneglyph")
                throw new Exception("Nika selectors did not commit their independent settings.");
            if (settings.OverlayTheme != overlay || settings.FontFamily != font)
                throw new Exception("Nika choices changed floating clock settings.");
            foreach (var option in NikaThemeCatalog.Backgrounds)
            {
                background.SelectedItem = option;
                if (settings.NikaBackground != option.Id || settings.NikaTypography != "navigator-precision"
                    || settings.NikaSidebarArtwork != "poneglyph" || settings.OverlayTheme != overlay || settings.FontFamily != font)
                    throw new Exception("Background selection did not remain independent: " + option.Id);
            }
            background.SelectedIndex = 0; typography.SelectedIndex = 0; artwork.SelectedIndex = 0;
        }
        finally { editor.Close(); }
        var controller = (ControllerWindow)constructor.Invoke(new object[] { settings });
        controller.ShowActivated = false;
        controller.ShowInTaskbar = false;
        controller.FindControl<ListBox>("LapListBox")!.ItemsPanel =
            new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel());
        try
        {
            controller.Show(); ClockOverlayChecks.Pump(150);
            AssertReferenceTypography(controller);
            AssertFrameFillsWindow(controller);
            CaptureActual(controller, "controller-native-launch");
            foreach (string requiredClass in new[] { "nikaHeaderNav" })
            {
                var required = controller.GetVisualDescendants().OfType<Control>().First(control => control.Classes.Contains(requiredClass));
                if (!required.IsEffectivelyVisible)
                    throw new Exception("The normal launch hides approved controller navigation: " + requiredClass);
            }
            if (!controller.Classes.Contains("nika")) throw new Exception("Controller did not select Nika layout.");
            var digits = controller.FindControl<TextBlock>("NikaTimeDisplay")!;
            if (!digits.IsEffectivelyVisible) throw new Exception("Nika live timer display is hidden.");
            var start = controller.FindControl<Button>("StartStopButton")!;
            if (start.Background is not ISolidColorBrush brush || brush.Color.R < 150 || brush.Color.B > 100)
                throw new Exception("Primary action lost its crimson palette.");
            var lap = controller.FindControl<Button>("LapButton")!;
            lap.ApplyTemplate();
            var presenter = lap.GetVisualDescendants().OfType<ContentPresenter>().First();
            foreach (string state in new[] { ":pointerover", ":pressed", ":focus-visible" })
            {
                ((IPseudoClasses)lap.Classes).Set(state, true);
                if (presenter.Foreground is not ISolidColorBrush actionText || actionText.Color != Color.Parse("#FFF1D9"))
                    throw new Exception("Nika secondary action loses cream text in " + state + ": " + presenter.Foreground);
                ((IPseudoClasses)lap.Classes).Set(state, false);
            }
            var timerMenu = controller.GetLogicalDescendants().OfType<MenuItem>().First(item => item.Header?.ToString() == "_Timers");
            if (!timerMenu.IsEffectivelyVisible || timerMenu.Items.Count < 7)
                throw new Exception("Nika hides existing timer menu commands.");
            timerMenu.IsSubMenuOpen = true; ClockOverlayChecks.Pump(50);
            var menuCommand = timerMenu.Items.OfType<MenuItem>().First();
            if (menuCommand.Foreground is not ISolidColorBrush menuText || menuText.Color != Color.Parse("#FFF1D9")
                || menuCommand.Background is not ISolidColorBrush menuBackground || menuBackground.Color != Color.Parse("#032437"))
                throw new Exception("Nika submenu lost its coordinated readable palette: " + menuCommand.Foreground + ", " + menuCommand.Background);
            if (!menuCommand.GetVisualDescendants().OfType<Border>().Any(border =>
                border.Background is ISolidColorBrush surface && surface.Color == Color.Parse("#032437")))
                throw new Exception("Nika submenu template does not render a navy surface.");
            timerMenu.IsSubMenuOpen = false;
            foreach (var (width, height) in new[] { (1400, 720), (1040, 720), (1040, 520), (600, 720), (600, 520) })
            {
                controller.Width = width; controller.Height = height; ClockOverlayChecks.Pump(150); controller.UpdateLayout();
                AssertFrameFillsWindow(controller);
                foreach (string name in new[] { "StartStopButton", "LapButton", "ResetButton", "ToggleOverlayButton", "EditButton", "WorkspaceNewTimerButton", "NewTimerRailButton" })
                {
                    var button = controller.FindControl<Button>(name)!;
                    var position = button.TranslatePoint(default, controller);
                    var end = button.TranslatePoint(new Point(button.Bounds.Width, button.Bounds.Height), controller);
                    if (!button.IsEffectivelyVisible || position == null || end == null || position.Value.X < -1 || position.Value.Y < -1
                        || end.Value.X > controller.ClientSize.Width + 1 || end.Value.Y > controller.ClientSize.Height + 1)
                        throw new Exception(name + " overflows native Nika controller at " + width + ": position=" + position + ", end=" + end + ", client=" + controller.ClientSize);
                    var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), controller)!.Value;
                    var hit = controller.InputHitTest(center) as Visual;
                    if (hit != button && (hit == null || !hit.GetVisualAncestors().Contains(button)))
                        throw new Exception(name + " is not clickable at its native scaled position at " + width);
                }
                AssertRecordedLapsVisible(controller);
                CaptureActual(controller, "controller-native-" + width + "x" + height);
            }
            controller.WindowState = WindowState.Maximized; ClockOverlayChecks.Pump(200);
            AssertReferenceTypography(controller);
            AssertRecordedLapsVisible(controller);
            AssertFrameFillsWindow(controller);
            CaptureActual(controller, "controller-maximized");
            var applyChoices = typeof(ControllerWindow).GetMethod("ApplyNikaChoices", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var background in NikaThemeCatalog.Backgrounds.Skip(2))
            {
                settings.NikaBackground = background.Id;
                applyChoices.Invoke(controller, null);
                if (controller.FindControl<NikaArtwork>("NikaMap")!.Choice != background.Id)
                    throw new Exception("The controller did not apply background " + background.Id);
                CaptureActual(controller, "background-" + background.Id);
            }
            settings.NikaBackground = "original-map";
            applyChoices.Invoke(controller, null);
            controller.WindowState = WindowState.Normal; ClockOverlayChecks.Pump(150);
            controller.Width = 1536; controller.Height = 1024; ClockOverlayChecks.Pump(150);
            AssertFrameFillsWindow(controller);
            CaptureActual(controller, "controller-paused");
            settings.ThemeMode = AppThemeCatalog.Midnight;
            typeof(ControllerWindow).GetMethod("ApplyControllerTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
            apply.Invoke(null, new object[] { AppThemeCatalog.Midnight }); ClockOverlayChecks.Pump(50);
            if (controller.TryFindResource("PrimaryActionBrush", out var restored) && restored is ISolidColorBrush restoredBrush && restoredBrush.Color == Color.Parse("#CD392C"))
                throw new Exception("The local Nika palette leaked after switching themes.");
            if (controller.Classes.Contains("nika") || digits.IsEffectivelyVisible || !controller.FindControl<TextBlock>("TimeDisplay")!.IsEffectivelyVisible)
                throw new Exception("Switching away did not restore the standard controller.");
            foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Pirate })
            {
                settings.ThemeMode = theme;
                typeof(ControllerWindow).GetMethod("ApplyControllerTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
                apply.Invoke(null, new object[] { theme });
                controller.Width = 1040; controller.Height = 720; ClockOverlayChecks.Pump(150);
                var frame = controller.FindControl<Grid>("ControllerFrame")!;
                var host = controller.FindControl<Viewbox>("ControllerLayoutHost")!;
                if (host.Stretch != Stretch.None || frame.Bounds.Width > controller.ClientSize.Width + 1
                    || frame.Bounds.Height > controller.ClientSize.Height + 1)
                    throw new Exception("The Nika canvas size leaked into " + theme);
            }
            settings.ThemeMode = AppThemeCatalog.Nika;
            settings.NikaTypography = "navigator-precision";
            typeof(ControllerWindow).GetMethod("ApplyControllerTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
            apply.Invoke(null, new object[] { AppThemeCatalog.Nika });
            controller.WindowState = WindowState.Maximized; ClockOverlayChecks.Pump(150);
            var navigatorHeading = controller.FindControl<TextBlock>("ActiveWorkspaceTitle")!;
            var navigatorFace = new Typeface(navigatorHeading.FontFamily, navigatorHeading.FontStyle, navigatorHeading.FontWeight).GlyphTypeface;
            var navigatorDigits = new Typeface(digits.FontFamily, digits.FontStyle, digits.FontWeight).GlyphTypeface;
            Console.WriteLine($"Navigator typography: heading={navigatorFace.FamilyName}/{navigatorFace.Weight}, digits={navigatorDigits.FamilyName}/{navigatorDigits.Weight}");
            if (!navigatorFace.FamilyName.StartsWith("Inter", StringComparison.Ordinal) || navigatorFace.Weight < FontWeight.ExtraBold
                || !navigatorDigits.FamilyName.StartsWith("Rajdhani", StringComparison.Ordinal) || navigatorDigits.Weight != FontWeight.SemiBold)
                throw new Exception("Navigator Precision fell back to unapproved font faces.");
            AssertFrameFillsWindow(controller);
            AssertRecordedLapsVisible(controller);
            CaptureActual(controller, "controller-navigator-precision");
        }
        finally { controller.Close(); }
    }

    private static void CheckArtworkGeometryAndAlpha()
    {
        var backgroundRenders = new HashSet<string>();
        foreach (var option in NikaThemeCatalog.Backgrounds)
        {
            var size = new Size(360, 240);
            var artwork = new NikaArtwork { Part = "Map", Choice = option.Id, Width = size.Width, Height = size.Height };
            artwork.Measure(size); artwork.Arrange(new Rect(size));
            using var capture = new RenderTargetBitmap(new PixelSize(360, 240), new Vector(96, 96));
            capture.Render(artwork);
            using var encoded = new MemoryStream();
            capture.Save(encoded);
            string hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(encoded.ToArray()));
            if (!backgroundRenders.Add(hash))
                throw new Exception("Background renders the same image as another choice: " + option.Id);
        }
        Console.WriteLine($"Nika backgrounds: {backgroundRenders.Count} distinct embedded images rendered.");
        var cover = typeof(NikaArtwork).GetMethod("CoverSource", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var size in new[] { new Size(1136, 888), new Size(1600, 96), new Size(360, 888) })
        {
            var crop = (Rect)cover.Invoke(null, new object[] { new Rect(0, 0, 1536, 1024), size, .5d })!;
            if (Math.Abs(size.Width / crop.Width - size.Height / crop.Height) > .00001
                || crop.X < 0 || crop.Y < 0 || crop.Right > 1536.001 || crop.Bottom > 1024.001)
                throw new Exception("Nika artwork distorts a circular feature at " + size);
        }
        using (var mural = new Bitmap(AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Nika/warrior-reference-flat.png"))))
        {
            if (HasInkNear(mural, new Point(0, 0), 0) || HasInkNear(mural, new Point(mural.PixelSize.Width - 1, mural.PixelSize.Height - 1), 0))
                throw new Exception("Nika mural has an opaque rectangular backdrop.");
        }
        var circles = typeof(NikaArtwork).GetMethod("CircularDials", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (double width in new[] { 1000d, 1380d })
        {
            var size = new Size(width, 316);
            var dial = new NikaArtwork { Part = "Dial", Width = width, Height = 316 };
            dial.Measure(size); dial.Arrange(new Rect(size));
            using var capture = new RenderTargetBitmap(new PixelSize((int)width, 316), new Vector(96, 96));
            capture.Render(dial);
            var segments = ((Point Center, double Radius, double Angle)[])circles.Invoke(null, new object[] { size })!;
            foreach (var circle in segments)
            foreach (double offset in new[] { -.6, 0d, .6 })
            {
                var point = new Point(circle.Center.X + circle.Radius * Math.Cos(circle.Angle + offset),
                    circle.Center.Y + circle.Radius * Math.Sin(circle.Angle + offset));
                if (!HasInkNear(capture, point, 2)) throw new Exception("The rendered dial no longer follows a circle at " + size);
            }
        }
    }

    private static bool HasInkNear(Bitmap bitmap, Point point, int radius)
    {
        int side = radius * 2 + 1, length = side * side * 4;
        var data = new byte[length];
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            bitmap.CopyPixels(new PixelRect((int)Math.Round(point.X) - radius, (int)Math.Round(point.Y) - radius, side, side), buffer, length, side * 4);
            Marshal.Copy(buffer, data, 0, length);
            return Enumerable.Range(0, side * side).Any(pixel => data[pixel * 4 + 3] > 32);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void AssertFrameFillsWindow(ControllerWindow controller)
    {
        var frame = controller.FindControl<Grid>("ControllerFrame")!;
        var start = frame.TranslatePoint(default, controller)!.Value;
        var end = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), controller)!.Value;
        if (Math.Abs(start.X) > 1 || Math.Abs(start.Y) > 1
            || Math.Abs(end.X - controller.ClientSize.Width) > 1 || Math.Abs(end.Y - controller.ClientSize.Height) > 1)
            throw new Exception("Nika leaves an outer gutter: start=" + start + ", end=" + end + ", window=" + controller.ClientSize);
    }

    private static void AssertRecordedLapsVisible(ControllerWindow controller)
    {
        var table = controller.FindControl<ItemsControl>("NikaLapTable")!;
        var rows = table.GetVisualDescendants().OfType<Border>().Where(row => row.DataContext is NikaLapRow).ToArray();
        double bottom = rows.Select(row => row.TranslatePoint(new Point(row.Bounds.Width, row.Bounds.Height), controller)!.Value.Y).DefaultIfEmpty(double.PositiveInfinity).Max();
        var footer = controller.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("nikaFooter"));
        var footerStart = footer.TranslatePoint(default, controller)!.Value;
        if (table.ItemCount != 3 || rows.Length != 3 || bottom > footerStart.Y - 3)
            throw new Exception($"Recorded laps overlap the footer: last row bottom={bottom}, footer top={footerStart.Y}.");
    }

    private static void AssertReferenceTypography(ControllerWindow controller)
    {
        var heading = controller.FindControl<TextBlock>("ActiveWorkspaceTitle")!;
        var digits = controller.FindControl<TextBlock>("NikaTimeDisplay")!;
        var headingFace = new Typeface(heading.FontFamily, heading.FontStyle, heading.FontWeight).GlyphTypeface;
        var digitsFace = new Typeface(digits.FontFamily, digits.FontStyle, digits.FontWeight).GlyphTypeface;
        var body = controller.FindControl<Grid>("ControllerBody")!;
        double workspaceWidth = body.Bounds.Width - body.ColumnDefinitions[0].ActualWidth;
        var timerStart = digits.TranslatePoint(default, body)!.Value;
        var timerEnd = digits.TranslatePoint(new Point(digits.Bounds.Width, digits.Bounds.Height), body)!.Value;
        Console.WriteLine($"Nika typography: heading={headingFace.FamilyName}/{headingFace.Weight}, digits={digitsFace.FamilyName}/{digitsFace.Weight}, timer/workspace={((timerEnd.X - timerStart.X) / workspaceWidth):F2}");
        if (!headingFace.FamilyName.StartsWith("Playfair Display", StringComparison.Ordinal) || headingFace.Weight < FontWeight.ExtraBold)
            throw new Exception("The approved heavy display heading resolves to an ordinary or fallback font.");
        if (!digitsFace.FamilyName.StartsWith("Roboto Condensed", StringComparison.Ordinal) || digitsFace.Weight < FontWeight.Black)
            throw new Exception("The approved timer does not resolve its embedded Black face.");
        if ((timerEnd.X - timerStart.X) / workspaceWidth < .59)
            throw new Exception("The timer is too small to carry the approved typographic hierarchy.");
    }

    private static void CaptureActual(ControllerWindow controller, string name)
    {
        controller.UpdateLayout();
        string output = Path.GetFullPath("design/one-piece-v2/implemented-desktop/" + name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(controller.ClientSize.Width), (int)Math.Ceiling(controller.ClientSize.Height)), new Vector(96,96));
        // Avalonia 11.2 ImmediateRenderer carries an unscaled culling rectangle
        // through Viewbox transforms. Render the already-arranged frame at its
        // actual native bounds to avoid that capture-only clipping bug.
        var frame = controller.FindControl<Grid>("ControllerFrame")!;
        var topLeft = frame.TranslatePoint(default, controller)!.Value;
        var bottomRight = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), controller)!.Value;
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(controller.Background!, null, new Rect(controller.ClientSize));
            context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill,
                SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(topLeft, bottomRight));
        }
        bitmap.Save(output);
        Console.WriteLine("Native layout capture: " + controller.ClientSize + " " + output);
    }
}
