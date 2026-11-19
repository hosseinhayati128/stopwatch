using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class DeleteTimeRecordNativeChecks
{
    const string Output = "design/one-piece-v2/delete-time-record/implemented";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static Button Button(Window w, bool delete) => w.GetVisualDescendants().OfType<Button>().Single(b => b.Name == (delete ? "DeleteButton" : "CancelButton") || Equals(b.Content, delete ? "Delete record" : "Cancel"));
    static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); ClockOverlayChecks.Pump(25); }
    static void Key(Control c, Avalonia.Input.Key key)
    {
        c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        c.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key }); ClockOverlayChecks.Pump(25);
    }
    static void Desktop(string type, string method, params object[] args) => typeof(ProjectRecordDeleteWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    static ProjectWorkIntervalView Record(string name = "Programming", DateTime? local = null, double minutes = 45)
    {
        var start = (local ?? new DateTime(2026, 11, 18, 14, 15, 0, DateTimeKind.Local)).ToUniversalTime();
        return new(Guid.NewGuid(), Guid.NewGuid(), "sample", name, start, start.AddMinutes(minutes));
    }
    sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Settings;
        readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        internal Fixture(int scale = 90, string theme = AppThemeCatalog.Nika)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, TelegramEnabled = false, HabiticaEnabled = false };
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
        }
        internal ProjectRecordDeleteWindow Create(ProjectWorkIntervalView? record = null)
        {
            var w = new ProjectRecordDeleteWindow(record ?? Record(), Settings);
            w.ShowActivated = false; w.ShowInTaskbar = false; return w;
        }
        internal ProjectRecordDeleteWindow Open(ProjectWorkIntervalView? record = null) { var w = Create(record); w.Show(); ClockOverlayChecks.Pump(65); return w; }
        public void Dispose() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); CultureInfo.CurrentCulture = _culture; }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            using var f = new Fixture(scale);
            foreach (string state in new[] { "standard", "long-name", "overnight" })
            {
                var record = state switch
                {
                    "long-name" => Record("Programming · Desktop interface improvements and accessibility review"),
                    "overnight" => Record("Reading · Research notes and planning for tomorrow", new DateTime(2026, 11, 18, 23, 30, 0, DateTimeKind.Local), 80),
                    _ => Record()
                };
                var w = f.Open(record);
                try
                {
                    Require(w.SystemDecorations == SystemDecorations.None && w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Delete Time Record is missing the approved frame.");
                    Require(Math.Abs(w.ClientSize.Width - 540 * scale / 100d) < 1.1 && Math.Abs(w.ClientSize.Height - 470 * scale / 100d) < 1.1, "Dialog does not follow the approved page scale.");
                    Bounds(w); Require(Button(w, false).IsFocused, "Cancel lacks initial focus.");
                    var scroll = Named<ScrollViewer>(w, "DeleteRecordBody"); Require(scroll.Extent.Height <= scroll.Viewport.Height + 1, "Approved standard/long/overnight samples should fit without scrolling.");
                    Capture(w, scale, state);
                }
                finally { w.Close(); }
            }
        }
        using (var f = new Fixture())
        {
            f.Settings.Typography.Global.SizePercent = 150; Desktop("DesktopTypography", "Apply", f.Settings.Typography);
            var w = f.Open(Record("برنامه‌نویسی و پژوهش درباره طراحی رابط کاربری و یادداشت‌های یک پروژه بلندمدت"));
            try
            {
                Require(Named<TextBlock>(w, "ProjectText").FlowDirection == FlowDirection.RightToLeft, "RTL name lost its direction.");
                Bounds(w); ScrollReachability(w); Capture(w, 90, "large-text");
                f.Settings.UiScalePercent = 125; Desktop("DesktopPageAppearance", "Apply", f.Settings, true); ClockOverlayChecks.Pump(45);
                Require(Math.Abs(w.ClientSize.Width - 675) < 1.1, "Live zoom did not resize the dialog.");
                var screen = new Screen(1.25, new PixelRect(-800, 0, 800, 600), new PixelRect(-800, 32, 800, 548), true);
                typeof(ProjectRecordDeleteWindow).GetMethod("FitDeleteRecordToScreen", Private)!.Invoke(w, [screen]); ClockOverlayChecks.Pump(40);
                Require(w.Position.X >= -800 && w.Position.Y >= 32 && w.Position.X + w.ClientSize.Width * 1.25 <= 1 && w.Position.Y + w.ClientSize.Height * 1.25 <= 581, "Dialog exceeds the working area.");
                Bounds(w); ScrollReachability(w); Capture(w, 125, "small-screen");
            }
            finally { w.Close(); }
        }
    }
    static void Bounds(Window w)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var end = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), w)!.Value;
        Require(Math.Abs(end.X - w.ClientSize.Width) < 1.5 && Math.Abs(end.Y - w.ClientSize.Height) < 1.5, "Frame does not fit native window.");
        foreach (string name in new[] { "HeadingText", "DeleteRecordCancel", "CancelButton", "DeleteButton" })
        {
            var c = Named<Control>(w, name); var corner = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), frame)!.Value;
            Require(corner.X <= frame.Bounds.Width - 10 && corner.Y <= frame.Bounds.Height - 15, name + " exceeds frame.");
        }
        foreach (var text in frame.GetVisualDescendants().OfType<TextBlock>()) Require(text.TextLayout.Height <= text.Bounds.Height + 1, "Text clipped: " + text.Text);
        var close = Named<Button>(w,"DeleteRecordCancel"); var glyph = (Avalonia.Controls.Shapes.Path)close.Content!;
        var closeStart = close.TranslatePoint(default, frame)!.Value; var title = Named<TextBlock>(w,"HeadingText");
        var titleEnd = title.TranslatePoint(new Point(title.Bounds.Width,0),frame)!.Value;
        Require(glyph.IsEffectivelyVisible && glyph.Bounds.Width > 0 && glyph.Bounds.Height > 0 && closeStart.X >= titleEnd.X - 1 && closeStart.Y >= 0,"Title overlaps or hides the X cancel control.");
        var body = Named<ScrollViewer>(w, "DeleteRecordBody"); Require(body.Viewport.Height > 70, "Record viewport is unusable.");
        Require(body.TranslatePoint(new Point(0, body.Bounds.Height), frame)!.Value.Y < Button(w, false).TranslatePoint(default, frame)!.Value.Y, "Record body overlaps the fixed actions.");
        foreach (var b in new[] { Button(w, false), Button(w, true) })
        {
            var text = b.GetVisualDescendants().OfType<TextBlock>().Single(); var center = text.TranslatePoint(new Point(text.Bounds.Width / 2, text.Bounds.Height / 2), b)!.Value;
            Require(Math.Abs(center.X - b.Bounds.Width / 2) < 1 && Math.Abs(center.Y - b.Bounds.Height / 2) < 1, "Action label is off center.");
        }
    }
    static void ScrollReachability(Window w)
    {
        var body = Named<ScrollViewer>(w, "DeleteRecordBody"); body.Offset = new(0, 10000); ClockOverlayChecks.Pump(25);
        var warning = Named<TextBlock>(w, "DeleteRecordWarning"); var bottom = warning.TranslatePoint(new Point(0, warning.Bounds.Height), body)!.Value;
        Require(bottom.Y <= body.Bounds.Height + 1 && bottom.Y >= warning.Bounds.Height, "Cannot scroll to deletion warning.");
        body.Offset = default; ClockOverlayChecks.Pump(25); var top = Named<TextBlock>(w, "ProjectText").TranslatePoint(default, body)!.Value;
        Require(top.Y >= 0 && top.Y < body.Bounds.Height, "Cannot scroll to project identity.");
    }
    static void Capture(Window w, int scale, string state)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (scale >= 100) bitmap.Render(w);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, $"delete-time-record-{state}-{scale}.png"));
    }
    internal static void Dates()
    {
        using var f = new Fixture();
        foreach (var (record, startDate, endDate, startTime, endTime, duration) in new[]
        {
            (Record(), "18 Nov 2026", "18 Nov 2026", "14:15", "15:00", "45 min"),
            (Record(local:new DateTime(2026,11,18,23,30,0,DateTimeKind.Local), minutes:80), "18 Nov 2026", "19 Nov 2026", "23:30", "00:50", "1 h 20 min"),
            (Record(local:new DateTime(2026,12,31,23,30,0,DateTimeKind.Local), minutes:90), "31 Dec 2026", "01 Jan 2027", "23:30", "01:00", "1 h 30 min"),
            (Record(minutes:1625), "18 Nov 2026", "19 Nov 2026", "14:15", "17:20", "27 h 5 min")
        })
        {
            var w = f.Open(record);
            try
            {
                Require(Named<TextBlock>(w,"DateText").Text == startDate && Named<TextBlock>(w,"DeleteRecordEndDate").Text == endDate, "Full endpoint dates are incorrect.");
                Require(Named<TextBlock>(w,"TimeText").Text == startTime && Named<TextBlock>(w,"DeleteRecordEndTime").Text == endTime, "Full endpoint times are incorrect.");
                Require(Named<TextBlock>(w,"DurationText").Text == duration, "Elapsed duration is incorrect.");
            }
            finally { w.Close(); }
        }
        var dst = Record() with { StartUtc = new DateTime(2021,3,21,20,0,0,DateTimeKind.Utc), EndUtc = new DateTime(2021,3,21,21,0,0,DateTimeKind.Utc) };
        var localDst = dst with { StartUtc = dst.StartUtc.ToLocalTime(), EndUtc = dst.EndUtc!.Value.ToLocalTime() };
        foreach (var input in new[] { dst, localDst, dst with { EndUtc = localDst.EndUtc }, dst with { StartUtc = DateTime.SpecifyKind(dst.StartUtc,DateTimeKind.Unspecified), EndUtc = DateTime.SpecifyKind(dst.EndUtc.Value,DateTimeKind.Unspecified) } })
        {
            var dstWindow = f.Open(input);
            try { Require(Named<TextBlock>(dstWindow,"DurationText").Text == "1 h 0 min", $"Duration did not normalize {input.StartUtc.Kind}/{input.EndUtc!.Value.Kind} endpoints across DST."); }
            finally { dstWindow.Close(); }
        }
        var shortWindow = f.Open(Record(minutes:.5)); try { Require(Named<TextBlock>(shortWindow,"DurationText").Text == "<1 min", "Sub-minute record misleadingly appears empty."); } finally { shortWindow.Close(); }
        bool rejected = false; try { f.Create(Record() with { EndUtc = null }); } catch (Exception e) { rejected = e.GetBaseException() is ArgumentException; }
        Require(rejected, "Open record was accepted for deletion.");
        f.Settings.ThemeMode = AppThemeCatalog.Midnight; Desktop("ThemeResources","ApplyApplication",f.Settings.ThemeMode);
        var legacy = f.Open(localDst); try { Require(Named<TextBlock>(legacy,"DurationText").Text == "1:00", "Legacy duration did not normalize local endpoints."); } finally { legacy.Close(); }
    }
    internal static void Actions()
    {
        using var f = new Fixture();
        void Check(Action<ProjectRecordDeleteWindow> action, bool expected)
        {
            var w = f.Open(); int closed = 0; w.Closed += (_, _) => closed++;
            try
            {
                action(w); Require(!w.IsVisible && w.WasDeleted == expected && closed == 1, "Incorrect confirmation result.");
                Click(Button(w,true)); Click(Button(w,false)); Require(w.WasDeleted == expected && closed == 1, "Repeated action changed a completed result.");
            }
            finally { w.Close(); }
        }
        var defaultWindow = f.Open(); try { Require(Button(defaultWindow,false).IsFocused, "Delete must not receive default focus."); Key(defaultWindow,Avalonia.Input.Key.Enter); Require(defaultWindow.IsVisible && !defaultWindow.WasDeleted, "Unfocused Enter deleted a record."); } finally { defaultWindow.Close(); }
        foreach (bool delete in new[] { false, true })
        {
            Check(w => Click(Button(w,delete)), delete);
            foreach (var key in new[] { Avalonia.Input.Key.Enter, Avalonia.Input.Key.Space }) Check(w => { var b=Button(w,delete); b.Focus(); Key(b,key); }, delete);
        }
        Check(w => { var b=Button(w,true); b.Focus(); Key(b,Avalonia.Input.Key.Escape); }, false);
        Check(w => Click(Named<Button>(w,"DeleteRecordCancel")), false); Check(w => w.Close(), false);
        var hover = f.Open();
        try
        {
            var b=Button(hover,false); var presenter=b.GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
            foreach (string state in new[]{":pointerover",":pressed"}) { ((IPseudoClasses)b.Classes).Set(state,true); ClockOverlayChecks.Pump(20); Require(presenter.Background is ISolidColorBrush color && color.Color.R<60 && color.Color.G<100,"Cancel loses readable contrast."); ((IPseudoClasses)b.Classes).Set(state,false); }
        }
        finally { hover.Close(); }
    }
    internal static void Lifecycle()
    {
        foreach (string theme in new[]{AppThemeCatalog.Nika,AppThemeCatalog.Midnight,AppThemeCatalog.Daylight,AppThemeCatalog.Pirate})
        {
            using var f=new Fixture(theme:theme); var w=f.Open();
            try { Require(w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any()==(theme==AppThemeCatalog.Nika),"Theme-specific frame selection is incorrect."); Click(Button(w,false)); Require(!w.WasDeleted,"Legacy Cancel confirmed deletion."); } finally { w.Close(); }
        }
        using var fixture=new Fixture();
        foreach (bool owned in new[]{false,true})
        {
            var owner=new Window { Width=700,Height=600,ShowActivated=false,ShowInTaskbar=false }; ProjectRecordDeleteWindow? dialog=null;
            using var listener=Window.WindowOpenedEvent.AddClassHandler<Window>((w,_)=>{if(w is ProjectRecordDeleteWindow d)dialog=d;});
            try
            {
                if(owned)owner.Show();
                var task=ProjectRecordDeleteWindow.ShowAsync(owned?owner:null,Record(),fixture.Settings);
                ClockOverlayChecks.Pump(50); Require(dialog is {IsVisible:true}&&!task.IsCompleted,"Dialog did not remain pending.");
                if(owned)owner.Close();else Click(Button(dialog!,true)); ClockOverlayChecks.Pump(40);
                Require(task.IsCompletedSuccessfully && task.Result==!owned && !dialog!.IsVisible,"Modal/ownerless completion or owner shutdown is incorrect.");
                Click(Button(dialog!,true)); Require(dialog!.WasDeleted==!owned,"Stale callback changed the result.");
            }
            finally { dialog?.Close(); owner.Close(); }
        }
    }
}
