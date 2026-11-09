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
        internal Fixture(string theme=AppThemeCatalog.Nika,int count=12,bool mixed=true,int activeIndex=0,TimeSpan? elapsed=null)
        {
            Timers=Enumerable.Range(1,count).Select(i=>new TimerSession(i){Name="Clock "+i}).ToArray();
            Timers[0].Name="Illustration — The Grand Voyage";Timers[0].IsSeparated=true;
            Timers[0].RestoreElapsed(elapsed??new TimeSpan(123,44,27),false);
            Timers[1].Name="Office clock";Timers[1].Mode=1;
            Timers[2].Name="برنامه‌نویسی پروژه";
            Timers[3].Name="Tea break";Timers[3].Mode=2;Timers[3].CountdownRemaining=new TimeSpan(0,8,15);Timers[3].IsRunning=true;
            Timers[4].Name="Editing / episode 12";Timers[4].Mode=3;Timers[4].RestoreElapsed(new TimeSpan(1,12,8),false);
            Timers[5].Name="";
            if(!mixed)
            {
                string[] names={"Illustration","Coding","Reading","Study","Writing","Planning"};
                string[] times={"00:44:27","01:12:08","00:18:42","00:36:10","00:08:15","00:05:32"};
                for(int i=0;i<6;i++){Timers[i].Name=names[i];Timers[i].Mode=0;Timers[i].IsSeparated=false;
                    Timers[i].RestoreElapsed(TimeSpan.Parse(times[i]),false);Timers[i].IsRunning=i is 0 or 1 or 4;}
            }
            Window=(Window)Activator.CreateInstance(PickerType,Hidden,null,new object[]{Timers,Timers[activeIndex],new AppSettings{ThemeMode=theme}},null)!;
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
            Require((second.Background as ISolidColorBrush)?.Color==Color.Parse("#55FFFFFF"),"Native hover wash did not apply.");
            Pointer(f,second,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
            Pointer(f,second,RawPointerEventType.LeftButtonUp);
            Require(f.List.SelectedIndex==1&&f.Window.IsVisible&&f.Chosen==null,"Single click did not select without confirming.");
            Require((second.Background as ISolidColorBrush)?.Color==Color.Parse("#F1D796"),"Selected hover lost the gold selection treatment.");
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
        var at=time.TranslatePoint(default,first)!.Value;
        Require(at.X+time.Bounds.Width<=first.Bounds.Width+1,"Complete maximum-hour time exceeded the minimum-width row.");
        f.List.SelectedIndex=11;f.List.ScrollIntoView(11);ClockOverlayChecks.Pump(40);
        f.List.SelectedIndex=0;f.List.ScrollIntoView(0);ClockOverlayChecks.Pump(40);
        first=Row(f.List,0);
        Require(Text(first,"ClockPickerName").Text==f.Timers[0].Name&&Text(first,"ClockPickerTime").Text=="256204778:48:05","Recycled row kept another clock's contents.");
        var peer=ControlAutomationPeer.CreatePeerForElement((Control)f.List.ContainerFromIndex(0)!);
        Require(peer!=null&&peer.GetName().Contains(f.Timers[0].Name)&&peer.GetName().Contains("Separated"),"List item automation peer omitted complete clock details.");
    }
    internal static void Evidence()
    {
        string output=Path.GetFullPath("design/one-piece-v2/clock-picker/native-implementation");Directory.CreateDirectory(output);
        void Save(Control control,string name)
        {
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
            bitmap.Render(control);bitmap.Save(Path.Combine(output,name+".png"));
        }
        using(var f=new Fixture(count:6,mixed:false))
        {f.List.SelectedIndex=2;Pointer(f,(Control)f.List.ContainerFromIndex(1)!,RawPointerEventType.Move);Save((Control)f.Window.Content!,"row-states");}
        using(var f=new Fixture(count:6))
        {
            Save((Control)f.Window.Content!,"names-and-modes");
            f.Window.Width=360;f.Window.Height=300;ClockOverlayChecks.Pump(60);Save((Control)f.Window.Content!,"minimum-size");
            f.Window.Width=460;f.Window.Height=480;ClockOverlayChecks.Pump(50);
            var content=(Control)f.Window.Content!;f.Window.Content=null;content.Width=460;content.Height=480;
            foreach(double scale in new[]{1.5,2})
            {
                var zoom=new Viewbox{Width=460*scale,Height=480*scale,Child=content};f.Window.Content=zoom;
                f.Window.Width=460*scale;f.Window.Height=480*scale;ClockOverlayChecks.Pump(50);
                // The OS can constrain the hidden host to the monitor and center
                // this oversized child at a negative origin. Export the complete
                // native subtree at zero origin, independent of that host limit.
                zoom.Arrange(new Rect(0,0,460*scale,480*scale));
                Save(zoom,$"names-and-modes-{scale:0.0}x");f.Window.Content=null;zoom.Child=null;
            }
        }
        using(var f=new Fixture(activeIndex:10))
        {f.List.SelectedIndex=10;f.List.ScrollIntoView(11);ClockOverlayChecks.Pump(60);Save((Control)f.Window.Content!,"twelve-clocks");}
        var board=new Canvas{Width=1520,Height=824,Background=Brush.Parse("#10232F")};
        void At(Control control,double x,double y){Canvas.SetLeft(control,x);Canvas.SetTop(control,y);board.Children.Add(control);}
        TextBlock Label(string text,int size=18)=>new(){Text=text,FontFamily=StopwatchOverlay.Desktop.Controls.StopwatchTypography.Resolve("modern-deck").NameFont,FontSize=size,Foreground=Brush.Parse("#F6E8C9")};
        At(Label("NAVIGATOR / ACTUAL NATIVE PICKER",32),42,32);At(Label("Native list items, buttons and selection · shared frame · no added timer loop",17),44,83);
        string[] files={"row-states","names-and-modes","twelve-clocks"};string[] titles={"01  Row states","02  Names & timing modes","03  Twelve clocks"};
        var images=new List<Bitmap>();
        try
        {
            for(int i=0;i<3;i++){var bitmap=new Bitmap(Path.Combine(output,files[i]+".png"));images.Add(bitmap);
                At(Label(titles[i],25),48+i*488,146);At(new Image{Source=bitmap,Width=460,Height=480},42+i*488,202);}
            At(Label("ACTIVE stays with the current clock. Gold outline follows selection. Pale wash follows hover.",16),48,727);
            At(Label("Native controls rendered off-screen; operating-system window captions are outside these content captures.",12),48,778);
            board.Measure(new Size(1520,824));board.Arrange(new Rect(0,0,1520,824));Save(board,"navigator-native");
        }
        finally{foreach(var bitmap in images)bitmap.Dispose();}
    }
    private static TextBlock Text(Control root,string name)=>root.GetVisualDescendants().OfType<TextBlock>().Single(c=>c.Name==name);
    private static Control Row(ListBox list,int index)=>(list.ContainerFromIndex(index) as Control)?.GetVisualDescendants().OfType<Control>().SingleOrDefault(c=>c.Name=="ClockPickerRow")
        ??throw new Exception("Native Navigator row template is missing.");
    internal static void Presentation()
    {
        using var f=new Fixture();var row=Row(f.List,0);
        Require(Text(row,"ClockPickerTime").Text=="123:44:27","Elapsed hours wrapped at 24.");
        Require(Text(row,"ClockPickerName").Text==f.Timers[0].Name,"Full name was lost from the native text control.");
        Require(row.GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="ClockPickerActive").IsVisible,"Current timer lost ACTIVE badge.");
        Require((AutomationProperties.GetName(row)??"").Contains("Separated"),"Accessible row details omitted separated status.");
        Require((ToolTip.GetTip(row)?.ToString()??"").Contains(f.Timers[0].Name),"Tooltip omitted the full project name.");
        f.List.SelectedIndex=2;ClockOverlayChecks.Pump(30);
        Require(row.GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="ClockPickerActive").IsVisible,"Selection moved the ACTIVE badge.");
        Require(Text(Row(f.List,1),"ClockPickerMode").Text=="Clock","Live clock mode missing.");
        Require(Text(Row(f.List,3),"ClockPickerTime").Text=="00:08:15","Countdown did not use remaining time.");
        Require(Text(Row(f.List,4),"ClockPickerMode").Text=="Timecode","Timecode mode missing.");
        Require(Text(Row(f.List,5),"ClockPickerName").Text=="Timer 6","Unnamed clock fallback changed.");
        Require(Text(Row(f.List,2),"ClockPickerName").FlowDirection==FlowDirection.RightToLeft,"RTL name did not keep its direction.");
        f.Window.Width=360;f.Window.Height=300;f.List.SelectedIndex=0;f.List.ScrollIntoView(0);ClockOverlayChecks.Pump(70);
        row=Row(f.List,0);var name=Text(row,"ClockPickerName");var time=Text(row,"ClockPickerTime");
        var badge=row.GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="ClockPickerActive");
        var nameAt=name.TranslatePoint(default,row)!.Value;var timeAt=time.TranslatePoint(default,row)!.Value;var badgeAt=badge.TranslatePoint(default,row)!.Value;
        Require(nameAt.X+name.Bounds.Width<=badgeAt.X+1&&badgeAt.X+badge.Bounds.Width<=timeAt.X+1,"Name, ACTIVE and time overlap at minimum size.");
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
            Require(!last.GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="ClockPickerKeycap").IsVisible,"Clock 12 incorrectly advertises a digit shortcut.");
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
