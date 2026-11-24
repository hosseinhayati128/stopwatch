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

internal static class ControllerTimersDropdownChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-timers-dropdown/implemented-native";
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    internal static void Run()
    {
        DeleteTodoNativeChecks.Desktop("ThemeResources","ApplyApplication",AppThemeCatalog.Nika);
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})
            foreach(bool pixel in new[]{false,true}) Render(scale,pixel);
    }

    static void Render(int scale, bool pixel)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel,
            UiScalePercent = scale, TimeFormat = 1, BlinkColon = false, FocusTrackingEnabled = false };
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance","Initialize",settings);
        var w = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
        try
        {
            w.ShowActivated = w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Show(); ClockOverlayChecks.Pump(65);
            var work = w.Screens.ScreenFromVisual(w)!.WorkingArea;
            w.Position = new PixelPoint(work.X,work.Y);
            var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager",Hidden)!.GetValue(w)!;
            var trigger = w.FindControl<Button>("NavyTimersButton") ?? throw new Exception("The Navy Timers dropdown trigger is missing.");
            ContextMenu Open(bool keyboard = false)
            {
                if (keyboard)
                    trigger.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Down });
                else
                    trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(65);
                return trigger.ContextMenu ?? throw new Exception("Timers dropdown did not open.");
            }
            void Refresh()
            {
                typeof(ControllerWindow).GetMethod("RefreshTimerRail", Hidden)!.Invoke(w, null);
                typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay", Hidden)!.Invoke(w, null);
                ClockOverlayChecks.Pump(65);
            }
            foreach (string state in new[] {"running","paused","clock","empty"})
            {
                if (state == "empty")
                    foreach(var t in manager.Sessions.ToArray()) manager.Close(t);
                else
                {
                    var t = manager.Active!;
                    t.Name = state switch { "running" => "Reading", "paused" => "Illustration", _ => "Navigation" };
                    t.Mode = state == "clock" ? 1 : 0;
                    t.LastNonClockMode = 0;
                    t.IsRunning = state == "running";
                    t.RestoreElapsed(TimeSpan.FromMinutes(26),t.IsRunning);
                    t.IsSeparated = state == "clock";
                }
                typeof(ControllerWindow).GetField("_overlaysVisible",Hidden)!.SetValue(w,state != "paused");
                Refresh();
                var menu = Open(keyboard: state == "paused");
                try
                {
                    ClockOverlayChecks.Pump(100);
                    var first = menu.Items.OfType<MenuItem>().First();
                    Require(menu.SelectedIndex == menu.Items.IndexOf(first), "First command was not selected.");
                    Require(first.IsFocused, "First command did not receive keyboard focus.");
                    var surface = menu.GetVisualDescendants().OfType<TodoFrame>().First(c => c.Name == "ActionSurface");
                    var dpi = w.RenderScaling;
                    var bounds = ScreenRect(surface,dpi);
                    var anchor = ScreenRect(trigger,dpi);
                    var expected = state switch {"clock"=>8, "empty"=>1, _=>10};
                    Require(menu.IsOpen && surface.Pixel == pixel && menu.Items.OfType<MenuItem>().Count()==expected,"Wrong surface or actions");
                    if (manager.Active is { } active)
                    {
                        var labels = surface.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                        Require(labels.Contains(active.DisplayName) && labels.Contains(state == "clock" ? "Clock" : active.IsRunning ? "Running" : "Paused"),
                            "Missing project name or status.");
                    }
                    Require(work.ToRect(dpi).Inflate(1).Contains(bounds) && Math.Abs(bounds.Right-anchor.Right) < 4 &&
                        bounds.Top >= anchor.Bottom-1 && bounds.Top < anchor.Bottom+10, $"Anchor mismatch: {anchor} -> {bounds}");
                    foreach (var t in surface.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible))
                        Require(bounds.Inflate(1).Contains(ScreenRect(t,dpi)) && t.Bounds.Width>0 &&
                            t.TextLayout.WidthIncludingTrailingWhitespace <= t.Bounds.Width+1 && !t.TextLayout.TextLines.Any(line=>line.HasCollapsed),
                            "Clipped label " + t.Text);
                    string stem = $"{(pixel?"pixel":"classic")}-{state}-{scale}";
                    Capture(w,surface,bounds.Union(anchor).Inflate(6).Intersect(work.ToRect(dpi)),stem);
                    if (state == "paused" && scale == 90) Capture(w,surface,ScreenRect(w,dpi),stem+"-controller");
                    string expectedFirst = state switch { "running"=>"Pause", "paused"=>"Resume", "clock"=>"Switch to stopwatch", _=>"New timer…" };
                    Require(Equals(first.Header, expectedFirst), "First command differs from timer state.");
                    if (state != "empty")
                    {
                        first.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Avalonia.Input.Key.Enter });
                        ClockOverlayChecks.Pump(45);
                        Require(!menu.IsOpen && (state=="clock" ? manager.Active!.Mode==0 : manager.Active!.IsRunning==(state=="paused")),
                            "Enter did not run the selected timer command.");
                    }
                    else
                    {
                        first.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Avalonia.Input.Key.Escape });
                        ClockOverlayChecks.Pump(45);
                        Require(!menu.IsOpen && manager.Count==0 && !w.OwnedWindows.Any(d=>d.IsVisible), "Escape ran an empty-state command.");
                        var create = Open().Items.OfType<MenuItem>().Single();
                        create.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Avalonia.Input.Key.Enter });
                        ClockOverlayChecks.Pump(45);
                        var dialog = w.OwnedWindows.OfType<TimerNameWindow>().Single(d => d.IsVisible);
                        dialog.Close(false);
                        ClockOverlayChecks.Pump(45);
                        Require(manager.Count == 0, "Cancel created a timer.");
                    }
                    Console.WriteLine($"PASS {stem}: {expected} actions, popup {bounds}");
                }
                finally { menu.Close(); trigger.ContextMenu=null; ClockOverlayChecks.Pump(25); }
            }
            var only = manager.Create();
            only.Name = new string('W', 160);
            only.Mode = 1;
            only.LastNonClockMode = 2;
            Refresh();
            var clockMenu = Open();
            var next = clockMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Next active timer"));
            Require(!next.IsEnabled, "Next active timer must be disabled for a single timer.");
            var returnToTimer = clockMenu.Items.OfType<MenuItem>().First();
            Require(Equals(returnToTimer.Header, "Switch to countdown"), "Wrong return mode.");
            var longTitle = clockMenu.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == only.Name);
            Require(longTitle.TextLayout.TextLines.Any(line => line.HasCollapsed) && Equals(ToolTip.GetTip(longTitle), only.Name),
                "A long project name was not bounded or accessible.");
            returnToTimer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            ClockOverlayChecks.Pump(55);
            Require(only.Mode == 2 && !clockMenu.IsOpen, "Clock did not return to countdown.");
            var neighbor = manager.Create();
            manager.Activate(only);
            Refresh();
            var management = Open();
            management.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Next active timer")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ClockOverlayChecks.Pump(45);
            Require(!management.IsOpen && manager.Active == neighbor, "Next active timer did not switch the selection.");
        }
        finally { w.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance","Shutdown"); }
    }

    static Rect ScreenRect(Control c, double dpi) => new(c.PointToScreen(default).ToPoint(dpi), c.PointToScreen(new Point(c.Bounds.Width,c.Bounds.Height)).ToPoint(dpi));

    static void Capture(ControllerWindow w, TodoFrame surface, Rect crop, string name)
    {
        double dpi=w.RenderScaling;
        var frame=w.FindControl<Grid>("ControllerFrame")!;
        var offset=new Vector(-crop.X,-crop.Y);
        using var bitmap=new RenderTargetBitmap(PixelSize.FromSize(crop.Size,dpi),new Vector(96*dpi,96*dpi));
        using(var dc=bitmap.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#102631"),null,new Rect(crop.Size));
            foreach(Control control in new Control[]{frame,surface})
                dc.DrawRectangle(new VisualBrush(control){ Stretch=Stretch.Fill, SourceRect=new RelativeRect(new Rect(control == surface ? ScreenRect(surface,dpi).Size : control.Bounds.Size),RelativeUnit.Absolute)},
                    null,ScreenRect(control,dpi).Translate(offset));
        }
        bitmap.Save(Path.Combine(Output,name+".png"));
    }
}
