using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using StopwatchOverlay.Themes;
using Xunit;

namespace StopwatchOverlay.Tests;

[Collection("Acanthus visual resources")]
public sealed class NavigatorClockTests
{
    [Fact]
    public void RenderedOpacity_FadesMapAndMetalInset_WhileKeepingWoodCompassRopeAndMetalRimOpaque()
    {
        RunSta(() =>
        {
            // Empty live text exposes the inset so this checks the actual composited artwork.
            var clock = new NavigatorClock { Text = "", ProjectName = "", TextSize = 48 };
            clock.SurfaceOpacity = 0;
            PixelFrame clear = Render(clock);
            Rect plate = clock.MetalBounds;
            Point rim = new(plate.X + plate.Width / 2, plate.Y + plate.Height * .07);
            Point map = new(clock.ActualWidth * .5, clock.ActualHeight * .25);
            Point wood = new(clock.ActualWidth * .5, clock.ActualHeight * .10);
            Point compass = new(clock.ActualWidth * .09, clock.ActualHeight * .145);
            Point rope = new(clock.ActualWidth * .815, clock.ActualHeight * .09);
            Point inset = new(clock.MetalInsetBounds.X + clock.MetalInsetBounds.Width / 2,
                clock.MetalInsetBounds.Y + clock.MetalInsetBounds.Height / 2);
            clock.SurfaceOpacity = .5;
            PixelFrame half = Render(clock);
            clock.SurfaceOpacity = 1;
            PixelFrame solid = Render(clock);

            foreach (Point retained in new[] { wood, compass, rope, rim })
            {
                Assert.InRange(clear.Alpha(retained), 250, 255);
                AssertPixelNear(clear.Pixel(retained), half.Pixel(retained));
                AssertPixelNear(clear.Pixel(retained), solid.Pixel(retained));
            }
            Assert.Equal(0, clear.Alpha(map));
            Assert.InRange(half.Alpha(map), 124, 132);
            Assert.InRange(solid.Alpha(map), 250, 255);
            Assert.Equal(0, clear.Alpha(inset));
            Assert.InRange(half.Alpha(inset), 100, 240);
            Assert.InRange(solid.Alpha(inset), 250, 255);
        });
    }

    [Fact]
    public void ToolbarRenderedOpacity_FadesBoardButRetainsAllThreeDialsAndGlyphs()
    {
        RunSta(() =>
        {
            var overlay = new OverlayWindow();
            try
            {
                overlay.ApplyTheme(OverlayThemeCatalog.Pirate, AppThemeCatalog.Midnight);
                overlay.UpdateTime("00:32:02");
                overlay.SetRunning(false);
                var timer = Node<Grid>(overlay, "TimerSurface");
                var popup = Node<Grid>(overlay, "ActionPopupRoot");
                var actionSurface = Node<Border>(overlay, "ActionSurface");
                var panel = Assert.IsType<NavigatorToolbarPanel>(actionSurface.Child);
                actionSurface.Opacity = 1;
                Node<TranslateTransform>(overlay, "ActionTranslate").Y = 0;
                var frames = new PixelFrame[3];
                double[] opacities = [0, .5, 1];
                for (int index = 0; index < opacities.Length; index++)
                {
                    overlay.ApplySettings(Colors.Cyan, Colors.Black, 48, 1, "Cascadia Mono", opacities[index]);
                    Layout(timer);
                    frames[index] = Render(popup);
                    Assert.Equal(opacities[index], NavigatorVisual.GetSurfaceOpacity(overlay));
                    Assert.Equal(opacities[index], NavigatorVisual.GetSurfaceOpacity(popup));
                    Assert.Equal(opacities[index], NavigatorVisual.GetSurfaceOpacity(panel));
                    Assert.Equal(1d, actionSurface.Opacity);
                }

                // The open gap between the first and second dial exposes both board materials.
                Point map = panel.TranslatePoint(new Point(panel.ActualWidth * .345, panel.ActualHeight * .5), popup);
                Point wood = panel.TranslatePoint(new Point(panel.ActualWidth * .345, panel.ActualHeight * .105), popup);
                foreach (Point fading in new[] { map, wood })
                {
                    Assert.Equal(0, frames[0].Alpha(fading));
                    Assert.InRange(frames[1].Alpha(fading), 124, 132);
                    Assert.InRange(frames[2].Alpha(fading), 250, 255);
                }
                foreach (string name in new[] { "CloseActionButton", "PauseResumeActionButton", "ResetActionButton" })
                {
                    var button = Node<Button>(overlay, name);
                    // Top brass rivet and the central live action sit well inside opaque art.
                    foreach (double y in new[] { .19, .5 })
                    {
                        Point retained = button.TranslatePoint(new Point(button.ActualWidth * .5, button.ActualHeight * y), popup);
                        Assert.InRange(frames[0].Alpha(retained), 250, 255);
                        AssertPixelNear(frames[0].Pixel(retained), frames[1].Pixel(retained));
                        AssertPixelNear(frames[0].Pixel(retained), frames[2].Pixel(retained));
                    }
                    Assert.Equal(1d, button.Opacity);
                }
            }
            finally { overlay.Close(); }
        });
    }

    [Fact]
    public void ClockPartFlags_IndependentlyKeepEachSelectedLayerOpaque()
    {
        RunSta(() =>
        {
            var clock = new NavigatorClock
            {
                Text = "00:32:02", ProjectName = "Navigation", TextSize = 48,
                TimerBrush = Brushes.Cyan, NameBrush = Brushes.Magenta, SurfaceOpacity = 0
            };
            NavigatorVisual.SetSurfaceOpacity(clock, 0);
            NavigatorVisual.SetOpaqueParts(clock, (NavigatorOpaqueParts)0);
            PixelFrame clear = Render(clock);
            Rect plate = clock.MetalBounds;
            var materialSamples = new[]
            {
                (Part: NavigatorOpaqueParts.ClockFrame, Point: new Point(clock.ActualWidth * .5, clock.ActualHeight * .10)),
                (Part: NavigatorOpaqueParts.ClockMap, Point: new Point(clock.ActualWidth * .5, clock.ActualHeight * .25)),
                (Part: NavigatorOpaqueParts.MetalBorder, Point: new Point(plate.X + plate.Width / 2, plate.Y + plate.Height * .07)),
                (Part: NavigatorOpaqueParts.MetalFill, Point: new Point(clock.MetalInsetBounds.X + clock.MetalInsetBounds.Width / 2,
                    clock.MetalInsetBounds.Y + clock.MetalInsetBounds.Height / 2))
            };
            foreach (var sample in materialSamples)
            {
                NavigatorVisual.SetOpaqueParts(clock, sample.Part);
                PixelFrame selected = Render(clock);
                Assert.Equal(0, clear.Alpha(sample.Point));
                Assert.InRange(selected.Alpha(sample.Point), 250, 255);
            }
            foreach (var text in new[]
            {
                (Part: NavigatorOpaqueParts.TimerText, Area: clock.MetalInsetBounds),
                (Part: NavigatorOpaqueParts.ProjectName, Area: new Rect(0, clock.ActualHeight * .65,
                    clock.ActualWidth, clock.ActualHeight * .2))
            })
            {
                NavigatorVisual.SetOpaqueParts(clock, text.Part);
                PixelFrame selected = Render(clock);
                Point opaqueGlyph = selected.FindOpaquePixel(text.Area);
                Assert.Equal(0, clear.Alpha(opaqueGlyph));
                NavigatorVisual.SetOpaqueParts(clock, (NavigatorOpaqueParts)0);
                Assert.Equal(0, Render(clock).Alpha(opaqueGlyph));
            }

            // Selecting every part overrides a zero slider exactly like a fully opaque clock.
            NavigatorVisual.SetOpaqueParts(clock, NavigatorOpaqueParts.All);
            PixelFrame allSelected = Render(clock);
            NavigatorVisual.SetOpaqueParts(clock, (NavigatorOpaqueParts)0);
            NavigatorVisual.SetSurfaceOpacity(clock, 1);
            clock.SurfaceOpacity = 1;
            Assert.Equal(allSelected.Bytes, Render(clock).Bytes);
        });
    }

    [Fact]
    public void ToolbarPartFlags_IndependentlyControlWoodMapAndCompassDials()
    {
        RunSta(() =>
        {
            var overlay = new OverlayWindow();
            try
            {
                overlay.ApplyTheme(OverlayThemeCatalog.Pirate, AppThemeCatalog.Midnight);
                overlay.UpdateTime("00:32:02");
                var timer = Node<Grid>(overlay, "TimerSurface");
                var popup = Node<Grid>(overlay, "ActionPopupRoot");
                var surface = Node<Border>(overlay, "ActionSurface");
                var panel = Assert.IsType<NavigatorToolbarPanel>(surface.Child);
                surface.Opacity = 1;
                Node<TranslateTransform>(overlay, "ActionTranslate").Y = 0;

                PixelFrame ApplyAndRender(NavigatorOpaqueParts parts, double opacity = 0)
                {
                    overlay.ApplySettings(Colors.Cyan, Colors.Black, 48, 1, "Cascadia Mono", opacity, opaqueParts: parts);
                    Layout(timer);
                    PixelFrame frame = Render(popup);
                    Assert.Equal(parts, NavigatorVisual.GetOpaqueParts(overlay));
                    Assert.Equal(parts, NavigatorVisual.GetOpaqueParts(popup));
                    Assert.Equal(parts, NavigatorVisual.GetOpaqueParts(panel));
                    return frame;
                }

                PixelFrame clear = ApplyAndRender((NavigatorOpaqueParts)0);
                Point map = panel.TranslatePoint(new Point(panel.ActualWidth * .345, panel.ActualHeight * .5), popup);
                Point wood = panel.TranslatePoint(new Point(panel.ActualWidth * .345, panel.ActualHeight * .105), popup);
                PixelFrame woodOnly = ApplyAndRender(NavigatorOpaqueParts.ControlBoard);
                PixelFrame mapOnly = ApplyAndRender(NavigatorOpaqueParts.ControlMap);
                PixelFrame dialsOnly = ApplyAndRender(NavigatorOpaqueParts.ControlDials);
                Assert.Equal(0, clear.Alpha(wood));
                Assert.Equal(0, clear.Alpha(map));
                Assert.InRange(woodOnly.Alpha(wood), 250, 255);
                Assert.Equal(0, woodOnly.Alpha(map));
                Assert.InRange(mapOnly.Alpha(map), 250, 255);
                Assert.Equal(0, mapOnly.Alpha(wood));
                Assert.Equal(0, dialsOnly.Alpha(wood));
                Assert.Equal(0, dialsOnly.Alpha(map));
                foreach (string name in new[] { "CloseActionButton", "PauseResumeActionButton", "ResetActionButton" })
                {
                    var button = Node<Button>(overlay, name);
                    Point dial = button.TranslatePoint(new Point(button.ActualWidth * .5, button.ActualHeight * .5), popup);
                    Assert.Equal(0, clear.Alpha(dial));
                    Assert.InRange(dialsOnly.Alpha(dial), 250, 255);
                }
                PixelFrame allSelected = ApplyAndRender(NavigatorOpaqueParts.All);
                Assert.Equal(allSelected.Bytes, ApplyAndRender((NavigatorOpaqueParts)0, 1).Bytes);
            }
            finally { overlay.Close(); }
        });
    }

    [Fact]
    public void DigitalDigitsRespectOutlineSettings()
    {
        RunSta(() =>
        {
            var clock = new NavigatorClock
            {
                Text = "08:32:08.8", TextSize = 48, TimerFont = new FontFamily("Consolas"),
                TimerBrush = Brushes.Cyan, SurfaceOpacity = 0,
                OutlineBrush = Brushes.Red, OutlineWidth = 1
            };
            NavigatorVisual.SetOpaqueParts(clock, NavigatorOpaqueParts.TimerText);
            NavigatorVisual.SetSurfaceOpacity(clock, 0);
            int thinRed = CountDominantPixels(Render(clock), 2);
            clock.OutlineWidth = 5;
            int thickRed = CountDominantPixels(Render(clock), 2);
            clock.OutlineBrush = Brushes.Blue;
            PixelFrame blueOutline = Render(clock);
            int thickBlue = CountDominantPixels(blueOutline, 0);

            Assert.True(thinRed > 10, "The digital timer's configured red outline was not rendered.");
            Assert.True(thickRed > thinRed + 100, "Increasing outline width must visibly increase its painted area.");
            Assert.True(thickBlue > 100, "Changing the configured outline color must render a blue outline.");
            Assert.InRange(CountDominantPixels(blueOutline, 2), 0, 2);

            // Pbgra channel dominance excludes both cyan digit fills and the warm gold bevel.
            static int CountDominantPixels(PixelFrame frame, int channel)
            {
                int count = 0;
                for (int offset = 0; offset < frame.Bytes.Length; offset += 4)
                {
                    int value = frame.Bytes[offset + channel];
                    if (frame.Bytes[offset + 3] >= 40 && value >= 80
                        && value > frame.Bytes[offset + 1] + 60
                        && value > frame.Bytes[offset + (channel == 2 ? 0 : 2)] + 60)
                        count++;
                }
                return count;
            }
        });
    }

    [Fact]
    public void LiveDigits_StayInsideMetalInset_ForLongTimersAndAllSupportedSizes()
    {
        RunSta(() =>
        {
            var clock = new NavigatorClock
            {
                SurfaceOpacity = 0, TimerBrush = Brushes.Cyan, OutlineBrush = Brushes.Black,
                ProjectName = "A project name long enough to require truncation inside the board"
            };
            double previousWidth = 0;
            foreach (int size in new[] { 16, 48, 120 })
            {
                clock.TextSize = size;
                clock.Text = "00:32:02";
                Render(clock);
                double shortWidth = clock.ActualWidth;
                clock.Text = "125:44:27.8";
                PixelFrame frame = Render(clock);
                Assert.True(clock.ActualWidth > shortWidth);
                Assert.True(clock.ActualWidth > previousWidth);
                previousWidth = clock.ActualWidth;
                Rect inset = clock.MetalInsetBounds;
                inset.Inflate(2, 2); // Allow the last antialiased pixel at fractional WPF coordinates.
                int digitPixels = 0;
                for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                {
                    int offset = (y * frame.Width + x) * 4;
                    if (frame.Bytes[offset] < 200 || frame.Bytes[offset + 1] < 200 || frame.Bytes[offset + 2] > 40)
                        continue;
                    digitPixels++;
                    Assert.True(inset.Contains(new Point(x, y)), $"{size} pt timer digit escaped its metal inset at {x},{y}");
                }
                Assert.True(digitPixels > size, $"{size} pt live timer was not rendered");
            }
        });
    }

    [Fact]
    public void ThemeRoundTrip_RetainsLiveValuesCustomFontAndExistingActionButtons()
    {
        RunSta(() =>
        {
            var overlay = new OverlayWindow();
            try
            {
                var root = Node<Grid>(overlay, "TimerSurface");
                var popup = Node<Grid>(overlay, "ActionPopupRoot");
                var clock = Node<NavigatorClock>(overlay, "NavigatorClockSurface");
                var pause = Node<Button>(overlay, "PauseResumeActionButton");
                overlay.UpdateTime("125:44:27.8");
                overlay.SetTimerName("  Navigation study  ");
                overlay.SetRecIndicatorVisible(true);
                NavigatorOpaqueParts customParts = NavigatorOpaqueParts.ClockMap | NavigatorOpaqueParts.TimerText
                    | NavigatorOpaqueParts.ControlMap | NavigatorOpaqueParts.ControlDials;
                overlay.ApplySettings(Colors.Cyan, Colors.Red, 61, 3, "Arial", .37, opaqueParts: customParts);

                foreach (string theme in new[] { OverlayThemeCatalog.Pirate, OverlayThemeCatalog.Midnight, OverlayThemeCatalog.Pirate })
                {
                    overlay.ApplyTheme(theme, AppThemeCatalog.Daylight);
                    Layout(root);
                    Layout(popup);
                    bool navigator = theme == OverlayThemeCatalog.Pirate;
                    Assert.Equal(navigator, NavigatorVisual.GetEnabled(overlay));
                    Assert.Equal(navigator, NavigatorVisual.GetEnabled(popup));
                    Assert.Equal(customParts, NavigatorVisual.GetOpaqueParts(overlay));
                    Assert.Equal(customParts, NavigatorVisual.GetOpaqueParts(popup));
                    Assert.Equal(customParts, NavigatorVisual.GetOpaqueParts(clock));
                    Assert.Equal(navigator ? Visibility.Visible : Visibility.Collapsed, clock.Visibility);
                    Assert.Same(pause, Node<Button>(overlay, "PauseResumeActionButton"));
                    Assert.Equal("125:44:27.8", clock.Text);
                    Assert.Equal("Navigation study", clock.ProjectName);
                    Assert.Equal("Arial", clock.TimerFont.Source);
                    Assert.Equal(61d, clock.TextSize);
                    Assert.Equal(.37, clock.SurfaceOpacity);
                    Assert.Equal(Colors.Cyan, Assert.IsType<SolidColorBrush>(clock.TimerBrush).Color);
                    Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(clock.OutlineBrush).Color);
                    Assert.True(clock.RecVisible);
                    Assert.Equal("Close", NavigatorVisual.GetAction(Node<Button>(overlay, "CloseActionButton")));
                    Assert.Equal("Reset", NavigatorVisual.GetAction(Node<Button>(overlay, "ResetActionButton")));

                    overlay.SetRunning(false);
                    Assert.Equal("Play", NavigatorVisual.GetAction(pause));
                    pause.ApplyTemplate();
                    string? playGeometry = navigator
                        ? Assert.IsType<Path>(pause.Template.FindName("ActionGlyph", pause)).Data.ToString() : null;
                    overlay.SetRunning(true);
                    Assert.Equal("Pause", NavigatorVisual.GetAction(pause));
                    if (navigator)
                    {
                        string pauseGeometry = Assert.IsType<Path>(pause.Template.FindName("ActionGlyph", pause)).Data.ToString();
                        Assert.NotEqual(playGeometry, pauseGeometry);
                        PixelFrame customFont = Render(clock);
                        Assert.True(customFont.Width > 0 && customFont.Height > 0);
                    }
                }
            }
            finally { overlay.Close(); }
        });
    }

    private static T Node<T>(OverlayWindow overlay, string name) where T : class
        => Assert.IsType<T>(overlay.FindName(name));

    private static void AssertPixelNear(uint expected, uint actual)
    {
        // Near-opaque asset pixels transmit only their remaining alpha fraction
        // of the changing backing map, plus at most two levels of rounding.
        int tolerance = Math.Max(2, 255 - (int)(expected >> 24));
        for (int shift = 0; shift < 32; shift += 8)
            Assert.InRange(Math.Abs((int)((expected >> shift) & 255) - (int)((actual >> shift) & 255)), 0, tolerance);
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }

    private static PixelFrame Render(FrameworkElement element)
    {
        Layout(element);
        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var bytes = new byte[width * height * 4];
        bitmap.CopyPixels(bytes, width * 4, 0);
        return new PixelFrame(width, height, bytes);
    }

    private sealed record PixelFrame(int Width, int Height, byte[] Bytes)
    {
        private int Offset(Point point) => ((int)point.Y * Width + (int)point.X) * 4;
        public int Alpha(Point point) => Bytes[Offset(point) + 3];
        public uint Pixel(Point point) => BitConverter.ToUInt32(Bytes, Offset(point));
        public Point FindOpaquePixel(Rect area)
        {
            for (int y = Math.Max(0, (int)Math.Ceiling(area.Top)); y < Math.Min(Height, (int)area.Bottom); y++)
            for (int x = Math.Max(0, (int)Math.Ceiling(area.Left)); x < Math.Min(Width, (int)area.Right); x++)
                if (Bytes[(y * Width + x) * 4 + 3] >= 250) return new Point(x, y);
            throw new InvalidOperationException($"No opaque live glyph was rendered inside {area}");
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA navigator test timed out");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
