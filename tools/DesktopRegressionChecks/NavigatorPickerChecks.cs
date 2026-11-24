using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class NavigatorPickerChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Type PickerType=typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.TimerPickerWindow")!;
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class Fixture:IDisposable
    {
        internal readonly TimerSession[] Timers;
        internal readonly Window Window;
        internal ListBox List=>Window.GetVisualDescendants().OfType<ListBox>().Single();
        internal TimerSession? Chosen=>(TimerSession?)PickerType.GetProperty("SelectedTimer",Hidden)!.GetValue(Window);
        private static TimerSession[] Samples(TimeSpan? elapsed)
        {
            var timers=Enumerable.Range(1,12).Select(i=>new TimerSession(i){Name="Clock "+i}).ToArray();
            timers[0].Name="Illustration — The Grand Voyage";timers[0].IsSeparated=true;
            timers[0].RestoreElapsed(elapsed??new TimeSpan(123,44,27),false);
            timers[1].Name="Office clock";timers[1].Mode=1;
            timers[2].Name="برنامه‌نویسی پروژه";
            timers[3].Name="Tea break";timers[3].Mode=2;timers[3].CountdownRemaining=new TimeSpan(0,8,15);timers[3].IsRunning=true;
            timers[4].Name="Editing / episode 12";timers[4].Mode=3;timers[4].RestoreElapsed(new TimeSpan(1,12,8),false);
            timers[5].Name="";
            return timers;
        }
        internal Fixture(string theme=AppThemeCatalog.Nika,TimeSpan? elapsed=null)
            : this(Samples(elapsed), theme, 0, false) { }

        internal Fixture(TimerSession[] timers, string theme, int activeIndex, bool pixel)
        {
            Timers=timers;
            Window=(Window)Activator.CreateInstance(PickerType,Hidden,null,new object[]{Timers,Timers[activeIndex],new AppSettings{ThemeMode=theme,NikaControllerPixel=pixel}},null)!;
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(80);
        }
        public void Dispose()=>Window.Close();
    }
    private static ulong inputTime=10000;
    private static void Pointer(Fixture f,Control control,RawPointerEventType type,RawInputModifiers modifiers=RawInputModifiers.None)
    {
        // Feed Avalonia's managed raw-input pipeline, never OS SendInput.
        var platform=typeof(TopLevel).GetProperty("PlatformImpl",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(f.Window)!;
        var device=(IMouseDevice)platform.GetType().GetProperty("MouseDevice",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(platform)!;
        var point=control.TranslatePoint(new Point(control.Bounds.Width/2,control.Bounds.Height/2),f.Window)!.Value;
        var ctor=typeof(RawPointerEventArgs).GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .Single(c=>c.GetParameters().Length==6&&c.GetParameters()[4].ParameterType==typeof(Point));
        var input=(RawPointerEventArgs)ctor.Invoke(new object[]{device,inputTime+=30,(IInputRoot)f.Window,type,point,modifiers});
        // TopLevel's callback performs compositor hit-testing before forwarding
        // to InputManager. Bypassing it leaves raw pointer hit results empty.
        var receive=(Action<RawInputEventArgs>)platform.GetType().GetProperty("Input",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(platform)!;
        receive(input);
    }
    internal static void PointerStates()
    {
        using(var f=new Fixture())
        {
            var second=(ListBoxItem)f.List.ContainerFromIndex(1)!;
            Pointer(f,second,RawPointerEventType.Move);
            Require(second.IsPointerOver&&f.List.SelectedIndex==0&&f.Chosen==null,
                $"Hover state: over={second.IsPointerOver}, selection={f.List.SelectedIndex}, confirmed={f.Chosen!=null}, row={second.Bounds}.");
            Require((second.Background as ISolidColorBrush)?.Color==Color.Parse("#28FFFAE8"),"Native hover wash did not apply.");
            Pointer(f,second,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
            Pointer(f,second,RawPointerEventType.LeftButtonUp);
            Require(f.List.SelectedIndex==1&&f.Window.IsVisible&&f.Chosen==null,"Single click did not select without confirming.");
            Require((second.Background as ISolidColorBrush)?.Color==Color.Parse("#72FFEFBD"),"Hover hid the selection wash.");
            f.List.ScrollIntoView(4);
            ClockOverlayChecks.Pump(50);
            var fifth=(ListBoxItem)f.List.ContainerFromIndex(4)!;
            Pointer(f,fifth,RawPointerEventType.Move);
            for(int i=0;i<2&&f.Window.IsVisible;i++)
            {Pointer(f,fifth,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);if(f.Window.IsVisible)Pointer(f,fifth,RawPointerEventType.LeftButtonUp);}
            Require(f.Chosen==f.Timers[4]&&!f.Window.IsVisible,"Double-click did not confirm the clicked timer.");
        }
        using(var f=new Fixture())
        {
            var close=f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerClose");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(f.Chosen==null&&!f.Window.IsVisible,"Close button did not cancel.");
        }
    }
    internal static void ButtonStates()
    {
        using var f=new Fixture();
        var choose=f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerSelect");
        var presenter=choose.GetVisualDescendants().OfType<ContentPresenter>().Single(c=>c.Name=="PART_ContentPresenter");
        var size=choose.Bounds.Size;
        Pointer(f,choose,RawPointerEventType.Move);
        Require(choose.IsPointerOver,"Button did not receive native pointer hover.");
        Require((presenter.Background as ISolidColorBrush)?.Color.A is null or 0,"Fluent hover overpainted the custom button surface.");
        Require((presenter.Foreground as ISolidColorBrush)?.Color==Color.Parse("#F6E8C9"),"Fluent hover replaced button text color.");
        Pointer(f,choose,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
        Require(choose.IsPressed,"Button did not enter native pressed state.");
        Require((presenter.Background as ISolidColorBrush)?.Color.A is null or 0,"Fluent pressed surface overpainted the custom button.");
        Require(presenter.RenderTransform==null||presenter.RenderTransform.Value==Matrix.Identity,"Pressed state scaled button content.");
        Require(choose.Bounds.Size==size,"Button changed size while pressed.");
        // Leave the hit target before release, so this state check does not confirm.
        Pointer(f,f.List,RawPointerEventType.Move,RawInputModifiers.LeftMouseButton);
        Pointer(f,f.List,RawPointerEventType.LeftButtonUp);
        Require(f.Window.IsVisible&&f.Chosen==null,"Releasing outside the button confirmed a clock.");
    }
    internal static void CloseButtonStates()
    {
        using var f=new Fixture();
        var close=f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerClose");
        close.Focus(NavigationMethod.Tab);
        Require(close.Classes.Contains(":focus-visible"),"Close button did not receive keyboard focus.");
        Require((close.BorderBrush as ISolidColorBrush)?.Color==Color.Parse("#AF7C29"),"Close-button base styles hid its keyboard focus border.");
        Pointer(f,close,RawPointerEventType.Move);var hover=(close.Background as ISolidColorBrush)?.Color;
        Pointer(f,close,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
        Require(close.IsPressed&&(close.Background as ISolidColorBrush)?.Color!=hover,"Close-button pressed feedback was hidden by its hover/base rule.");
        Pointer(f,f.List,RawPointerEventType.Move,RawInputModifiers.LeftMouseButton);Pointer(f,f.List,RawPointerEventType.LeftButtonUp);
    }
    internal static void ExtremeHoursAndRecycling()
    {
        using var f=new Fixture(elapsed:TimeSpan.MaxValue);f.Window.Width=360;f.Window.Height=300;ClockOverlayChecks.Pump(60);
        var first=Row(f.List,0);var time=Text(first,"ClockPickerTime");
        Require(time.Text=="256204778:48:05","Maximum elapsed value was truncated or wrapped.");
        var right=time.TranslatePoint(new Point(time.Bounds.Width,0),first)!.Value.X;
        Require(right<=first.Bounds.Width+1,"Complete maximum-hour time exceeded the minimum-width row.");
        f.List.SelectedIndex=11;f.List.ScrollIntoView(11);ClockOverlayChecks.Pump(40);
        f.List.SelectedIndex=0;f.List.ScrollIntoView(0);ClockOverlayChecks.Pump(40);
        first=Row(f.List,0);
        Require(Text(first,"ClockPickerName").Text==f.Timers[0].Name&&Text(first,"ClockPickerTime").Text=="256204778:48:05","Recycled row kept another clock's contents.");
        var peer=ControlAutomationPeer.CreatePeerForElement((Control)f.List.ContainerFromIndex(0)!);
        Require(peer!=null&&peer.GetName().Contains(f.Timers[0].Name)&&peer.GetName().Contains("Separated"),"List item automation peer omitted complete clock details.");
    }
    internal static void Evidence()
    {
        const string output = "design/one-piece-v2/clock-picker/unified-surface";
        Directory.CreateDirectory(output);
        foreach (bool pixel in new[] { false, true })
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var timers = new[] { "Illustration", "Coastal field recordings and interview edits", "Reading", "Tea break" }
                .Select((name, i) => new TimerSession(i + 1) { Name = name }).ToArray();
            timers[0].RestoreElapsed(new TimeSpan(0, 44, 38), false);
            timers[0].IsRunning = true;
            timers[1].RestoreElapsed(new TimeSpan(1, 12, 8), false);
            timers[2].RestoreElapsed(new TimeSpan(0, 18, 42), false);
            timers[3].Mode = 2;
            timers[3].CountdownRemaining = new TimeSpan(0, 8, 15);
            timers[3].IsRunning = true;
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            try
            {
                using var f = new Fixture(timers, AppThemeCatalog.Nika, 0, pixel);
                var frame = f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
                double zoom = scale / 100d;
                Require(Math.Abs(f.Window.Width - 475 * zoom) < 1 && Math.Abs(f.Window.Height - 465 * zoom) < 1,
                    "Picker window did not follow application scale.");
                var frameEnd = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), f.Window)!.Value;
                Require(Math.Abs(frameEnd.X - f.Window.ClientSize.Width) < 2 && Math.Abs(frameEnd.Y - f.Window.ClientSize.Height) < 2,
                    "The scaled picker frame did not fit its window.");
                var longName = Text(Row(f.List, 1), "ClockPickerName");
                Require(longName.TextLayout.TextLines.Count == 2 && !longName.TextLayout.TextLines.Any(l => l.HasCollapsed), "Long name should fit in two lines.");
                Require(Row(f.List, 0).GetVisualDescendants().OfType<ControllerPixelDigits>().Any() == pixel, "Wrong time face.");
                for (int index = 0; index <= 1; index++)
                {
                    if (index == 1)
                    {
                        Key((Control)f.List.ContainerFromIndex(0)!, Avalonia.Input.Key.Down);
                        ClockOverlayChecks.Pump(50);
                    }
                    var selected = (ListBoxItem)f.List.ContainerFromIndex(index)!;
                    var mark = f.List.GetVisualDescendants().OfType<Border>().Single(c => c.Name == "ClockPickerSelectionMark" && c.IsVisible);
                    Require(f.List.SelectedIndex == index && selected.IsKeyboardFocusWithin && mark.GetVisualAncestors().Contains(selected),
                        "The selection mark did not follow keyboard focus.");
                    var idle = (ListBoxItem)f.List.ContainerFromIndex(2)!;
                    Require((f.List.Background as ISolidColorBrush)?.Color.A == 0 && (idle.Background as ISolidColorBrush)?.Color.A == 0 &&
                        idle.BorderThickness == new Thickness(0), "Idle clock rows should share the picker's paper.");
                    var badge = f.List.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "ClockPickerActive");
                    Require(badge.GetVisualAncestors().Contains(Row(f.List, 0)), "Active badge moved before confirmation.");
                    var size = PixelSize.FromSize(f.Window.ClientSize, 1);
                    var choose = f.Window.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "ClockPickerSelect");
                    var buttonInk = choose.TranslatePoint(new Point(10, choose.Bounds.Height / 2), f.Window)!.Value;
                    // Source the full, arranged frame in logical coordinates. The window's
                    // scaled wrapper has a smaller culling viewport at zoom levels below 100%.
                    using var image = new RenderTargetBitmap(size, new Vector(96, 96));
                    var pageTransform = frame.RenderTransform;
                    frame.SetCurrentValue(Visual.RenderTransformProperty, null);
                    try
                    {
                        using var dc = image.CreateDrawingContext();
                        var brush = new VisualBrush(frame) { Stretch = Stretch.Fill,
                            SourceRect = new RelativeRect(new Rect(frame.Bounds.Size), RelativeUnit.Absolute) };
                        dc.DrawRectangle(brush, null, new Rect(0, 0, size.Width, size.Height));
                    }
                    finally { frame.SetCurrentValue(Visual.RenderTransformProperty, pageTransform); }
                    image.Save(Path.Combine(output, $"{(pixel ? "pixel" : "classic")}-{(index == 0 ? "active" : "browse")}-{scale}.png"));
                    var bytes = SharedFrameChecks.Pixels(image);
                    var offset = ((int)buttonInk.Y * size.Width + (int)buttonInk.X) * 4;
                    Require(bytes[offset + 1] < 90 && bytes[offset + 3] == 255,
                        $"Missing Select clock button in the {scale}% capture: sample {buttonInk} green {bytes[offset+1]} alpha {bytes[offset+3]}, frame {frame.Bounds}, client {f.Window.ClientSize}.");
                }
                Key((Control)f.List.ContainerFromIndex(1)!, Avalonia.Input.Key.Enter);
                Require(f.Chosen == timers[1] && !f.Window.IsVisible, "Enter failed to confirm the highlighted row.");
            }
            finally { DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
        }
    }
    private static TextBlock Text(Control root,string name)=>root.GetVisualDescendants().OfType<TextBlock>().Single(c=>c.Name==name);
    private static Control Row(ListBox list,int index)=>(list.ContainerFromIndex(index) as Control)?.GetVisualDescendants().OfType<Control>().SingleOrDefault(c=>c.Name=="ClockPickerRow")
        ??throw new Exception("Native Navigator row template is missing.");
    internal static void Presentation()
    {
        using var f=new Fixture();var row=Row(f.List,0);
        Require(Text(row,"ClockPickerTime").Text=="123:44:27","Elapsed hours wrapped at 24.");
        Require(Text(row,"ClockPickerName").Text==f.Timers[0].Name,"Full name was lost from the native text control.");
        Require(Text(row,"ClockPickerActive").IsVisible,"Current timer lost its Active label.");
        Require((AutomationProperties.GetName(row)??"").Contains("Separated"),"Accessible row details omitted separated status.");
        Require((ToolTip.GetTip(row)?.ToString()??"").Contains(f.Timers[0].Name),"Tooltip omitted the full project name.");
        f.List.SelectedIndex=2;ClockOverlayChecks.Pump(30);
        Require(Text(row,"ClockPickerActive").IsVisible,"Selection moved the Active label.");
        Require(Text(Row(f.List,1),"ClockPickerMode").Text=="Clock · Live","Live clock mode missing.");
        Require(Text(Row(f.List,3),"ClockPickerTime").Text=="00:08:15","Countdown did not use remaining time.");
        Require(Text(Row(f.List,4),"ClockPickerMode").Text=="Timecode · Paused","Timecode mode missing.");
        f.List.ScrollIntoView(5);ClockOverlayChecks.Pump(50);Require(Text(Row(f.List,5),"ClockPickerName").Text=="Timer 6","Unnamed clock fallback changed.");
        f.List.ScrollIntoView(2);ClockOverlayChecks.Pump(50);Require(Text(Row(f.List,2),"ClockPickerName").FlowDirection==FlowDirection.RightToLeft,"RTL name did not keep its direction.");
        f.Window.Width=360;f.Window.Height=300;f.List.SelectedIndex=0;f.List.ScrollIntoView(0);ClockOverlayChecks.Pump(70);
        row=Row(f.List,0);var name=Text(row,"ClockPickerName");var time=Text(row,"ClockPickerTime");
        var badge=Text(row,"ClockPickerActive");
        var nameAt=name.TranslatePoint(default,row)!.Value;var timeAt=time.TranslatePoint(default,row)!.Value;var badgeAt=badge.TranslatePoint(default,row)!.Value;
        Require(nameAt.X+name.Bounds.Width<=badgeAt.X+1&&timeAt.Y+time.Bounds.Height<=badgeAt.Y+1,"Name, ACTIVE and time overlap at minimum size.");
        Require(timeAt.X+time.Bounds.Width<=row.Bounds.Width+1,"Time was clipped outside the row at minimum size.");
        var root=(Control)f.Window.Content!;
        var close=f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerClose");
        var choose=f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerSelect");
        Console.WriteLine($"Minimum layout: client={f.Window.ClientSize}, frame={root.Bounds}, grid={(f.List.Parent as Control)?.Bounds}, list={f.List.Bounds}, listMin={f.List.MinWidth}x{f.List.MinHeight}.");
        var closeAt=close.TranslatePoint(default,root)!.Value;var chooseAt=choose.TranslatePoint(default,root)!.Value;
        Require(closeAt.X+close.Bounds.Width<=root.Bounds.Width&&chooseAt.Y+choose.Bounds.Height<=root.Bounds.Height,
            "Header or confirmation controls extend outside the minimum-size frame.");
    }
    private static void Key(Control control,Avalonia.Input.Key key)=>control.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});
    internal static void Navigation()
    {
        using(var f=new Fixture())
        {
            Require(f.List.IsKeyboardFocusWithin&&f.List.SelectedIndex==0,"Initial keyboard focus/selection changed.");
            var first=(Control)f.List.ContainerFromIndex(0)!;first.Focus();Key(first,Avalonia.Input.Key.Down);ClockOverlayChecks.Pump(30);
            Require(f.List.SelectedIndex==1&&f.Window.IsVisible,"Arrow navigation confirmed or lost selection.");
            Key((Control)f.List.ContainerFromIndex(1)!,Avalonia.Input.Key.End);ClockOverlayChecks.Pump(60);
            Require(f.List.SelectedIndex==11,"End did not reach clocks beyond nine.");
            var last=Row(f.List,11);
            Require(Text(last,"ClockPickerNumber").Text=="12" && Text(last,"ClockPickerNumber").FontWeight==FontWeight.Normal,
                "Clock 12 should have a plain index beyond the digit shortcuts.");
            Key(f.Window,Avalonia.Input.Key.NumPad2);ClockOverlayChecks.Pump(15);
            Require(f.Chosen==f.Timers[1]&&!f.Window.IsVisible,"Scrolled numpad shortcut did not use absolute position two.");
        }
        using(var f=new Fixture())
        {Key(f.Window,Avalonia.Input.Key.Enter);Require(f.Chosen==f.Timers[0],"Enter did not confirm the selected timer.");}
        using(var f=new Fixture())
        {f.List.SelectedIndex=4;f.Window.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ClockPickerSelect").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(f.Chosen==f.Timers[4],"Select clock did not confirm the highlighted timer.");}
        using(var f=new Fixture())
        {Key(f.Window,Avalonia.Input.Key.Escape);Require(f.Chosen==null&&!f.Window.IsVisible,"Escape did not cancel.");}
        using(var f=new Fixture(AppThemeCatalog.Midnight))
        {Require(!f.Window.GetVisualDescendants().Any(c=>c.Name=="ClockPickerRow")&&f.Window.Height==410,"Navigator altered the non-Nika picker.");}
    }
}
