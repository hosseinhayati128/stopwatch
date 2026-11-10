using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TimerEditorNativeChecks
{
    private const string Output="design/one-piece-v2/timer-editor/native-implementation";
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void PressKey(Control target,Key key,KeyModifiers modifiers=KeyModifiers.None)=>target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=modifiers});
    private sealed class Fixture:IDisposable
    {
        internal readonly TimerSession Timer=new(1){Name="Illustration",Category="Creative",ScorePerHour=1};
        internal readonly TimerEditorWindow Window;
        internal TextBox Time=>Window.FindControl<TextBox>("TimeValueBox")!;
        internal TextBox Start=>Window.FindControl<TextBox>("StartTimeBox")!;
        internal Button Save=>Window.FindControl<Button>("SaveButton")!;
        internal Fixture(string theme=AppThemeCatalog.Nika,int mode=0,bool intervals=true,TimeSpan? elapsed=null)
        {
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            var value=elapsed??TimeSpan.FromSeconds(2667);Timer.RestoreElapsed(value,false);Timer.IsRunning=true;Timer.Mode=mode;Timer.CountdownRemaining=value;
            var now=DateTime.UtcNow;now=new DateTime(now.Ticks-now.Ticks%TimeSpan.TicksPerSecond,DateTimeKind.Utc);
            ProjectWorkIntervalView[] records=intervals?[
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddHours(-3).AddSeconds(-890),now.AddHours(-3)),
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddHours(-2).AddSeconds(-878),now.AddHours(-2)),
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddSeconds(-899),null)]:[];
            Window=new TimerEditorWindow(Timer,["Illustration","Reading"],records,true,new AppSettings{ThemeMode=theme,ActivityWatchEnabled=false});
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(55);
        }
        public void Dispose()=>Window.Close();
    }
    internal static void Validation()
    {
        using(var both=new Fixture())
        {
            both.Time.Text="15x";both.Start.Text="bad";ClockOverlayChecks.Pump(20);
            both.Start.Text=DateTime.Now.AddMinutes(-10).ToString("HH:mm:ss");ClockOverlayChecks.Pump(20);
            Require(both.Time.Text=="15x"&&!both.Save.IsEnabled,"Correcting start silently erased an invalid duration and enabled Save.");
            both.Time.Text="15m";ClockOverlayChecks.Pump(20);Require(both.Save.IsEnabled,"Correcting duration after start did not recover.");
        }
        foreach(bool keyboard in new[]{false,true})
        {
            using var f=new Fixture();f.Start.Text="not-time";f.Time.Text="15m";ClockOverlayChecks.Pump(20);
            if(keyboard)PressKey(f.Window,Avalonia.Input.Key.Enter,KeyModifiers.Control);else Click(f.Save);
            Require(f.Window.IsVisible&&!f.Window.WasSaved,"Save bypassed the invalid segment start via "+(keyboard?"Ctrl+Enter":"button handler")+".");
            Require(!f.Save.IsEnabled,"A valid duration re-enabled Save while segment start remained invalid.");
            Require(!string.IsNullOrWhiteSpace(f.Window.FindControl<TextBlock>("SegmentDetailText")!.Text)&&f.Start.Classes.Contains("invalidTime"),"Invalid start has no field error feedback.");
            Click(f.Window.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"300")));
            Require(!f.Save.IsEnabled,"Quick adjust cleared another field's validation failure.");
            f.Start.Text=DateTime.Now.AddMinutes(-10).ToString("HH:mm:ss");f.Time.Text="15x";ClockOverlayChecks.Pump(20);
            Require(!f.Save.IsEnabled&&f.Time.Classes.Contains("invalidTime"),"Invalid duration has no field error or still permits Save.");
            f.Time.Text="15m";ClockOverlayChecks.Pump(20);Require(f.Save.IsEnabled,"Correcting both fields did not restore Save.");
            PressKey(f.Window,Avalonia.Input.Key.Enter,KeyModifiers.Control);
            Require(f.Window.WasSaved&&f.Window.NewTimeValue==TimeSpan.FromMinutes(15),"Valid keyboard save returned the wrong duration.");
        }
    }
    internal static void DurationBounds()
    {
        foreach(string input in new[]{"1:Infinity","1:NaN","2147483647:59:59","999999999999999h","9999999999999999999999999999","-5m"})
            Require(!TimerEditorWindow.TryParseDuration(input,out _),"Invalid/overflow duration was accepted: "+input);
        foreach(var (input,seconds) in new[]{("15m",900),("1h 30m",5400),("01:30:00",5400),("05:30",330),("0",0)})
            Require(TimerEditorWindow.TryParseDuration(input,out var value)&&value==TimeSpan.FromSeconds(seconds),"Supported duration changed: "+input);
        using var f=new Fixture(intervals:false,elapsed:TimeSpan.MaxValue);
        Require(f.Window.IsVisible,"Maximum elapsed time cannot open the editor.");
        f.Time.Text="10675199.02:48:05.4775807";ClockOverlayChecks.Pump(20);
        Click(f.Window.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"300")));
        Require(f.Window.IsVisible&&f.Timer.Elapsed==TimeSpan.MaxValue,"Quick adjust overflow closed the editor or mutated the timer.");
        foreach(bool records in new[]{true,false})
        {
            using var ordinary=new Fixture(intervals:records);ordinary.Time.Text="10675199.02:48:05.4775807";ClockOverlayChecks.Pump(20);
            PressKey(ordinary.Window,Avalonia.Input.Key.Enter,KeyModifiers.Control);
            Require(ordinary.Window.IsVisible&&!ordinary.Window.WasSaved&&!ordinary.Save.IsEnabled,"A parseable duration can overflow project-history dates on Save.");
            ordinary.Time.Text="15m";ClockOverlayChecks.Pump(20);Require(ordinary.Save.IsEnabled,"History range validation did not recover after correction.");
        }
    }
    internal static void Layout()
    {
        using var f=new Fixture();
        Require(f.Window.Content is StopwatchFrameSurface,"Nika editor still uses the standard rectangular window.");
        Require(f.Window.SystemDecorations==SystemDecorations.None&&((ISolidColorBrush)f.Window.Background!).Color.A==0,"Nika editor retains native chrome or opaque exterior.");
        var scroll=f.Window.FindControl<ScrollViewer>("EditorScroll")!;
        var initial=f.Save.TranslatePoint(default,f.Window)!.Value;
        f.Window.FindControl<Expander>("SegmentsExpander")!.IsExpanded=true;
        f.Window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="EditorRecordsExpander").IsExpanded=true;ClockOverlayChecks.Pump(30);
        scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(25);
        var after=f.Save.TranslatePoint(default,f.Window)!.Value;
        Require(Math.Abs(initial.Y-after.Y)<1,"Expanding/scrolling records moved the fixed footer.");
        f.Window.Width=560;f.Window.Height=520;ClockOverlayChecks.Pump(35);
        Require(scroll.Extent.Height>scroll.Viewport.Height&&f.Save.TranslatePoint(default,f.Window)!.Value.Y+f.Save.Bounds.Height<=f.Window.Bounds.Height-15,"Short editor lost scrolling or clipped Save.");
        var undo=f.Window.FindControl<Button>("UndoPreviousEditButton")!;
        Require(undo.TranslatePoint(default,f.Window)!.Value.X+undo.Bounds.Width<=f.Save.TranslatePoint(default,f.Window)!.Value.X,"Narrow footer buttons overlap.");
    }
    internal static void FieldsAndResults()
    {
        using var f=new Fixture();
        Click(f.Window.FindControl<Button>("ToggleProjectInputButton")!);var project=f.Window.FindControl<TextBox>("CustomProjectBox")!;
        Require(project.IsVisible,"New project entry was lost.");project.Text="Pencil study";
        Click(f.Window.FindControl<Button>("ToggleCategoryInputButton")!);var category=f.Window.FindControl<TextBox>("CustomCategoryBox")!;
        Require(category.IsVisible,"New category entry was lost.");category.Text="Craft";
        f.Window.FindControl<NumericUpDown>("ScorePerHourBox")!.Value=2.5m;
        Click(f.Window.FindControl<Button>("PauseResumeButton")!);f.Time.Text="1h 30m";ClockOverlayChecks.Pump(20);Click(f.Save);
        Require(f.Window.WasSaved&&f.Window.NewProjectName=="Pencil study"&&f.Window.NewCategory=="Craft"&&f.Window.NewScorePerHour==2.5&&
            !f.Window.NewIsRunning&&f.Window.NewTimeValue==TimeSpan.FromMinutes(90),"Reparented fields changed the save result contract.");
        Require(f.Timer.Name=="Illustration"&&f.Timer.IsRunning&&f.Timer.Elapsed==TimeSpan.FromSeconds(2667),"Editor mutated the timer before the controller applied results.");
    }
    private static ulong inputTime=20000;
    private static void Pointer(Window window,Control control,RawPointerEventType type,RawInputModifiers modifiers=RawInputModifiers.None)
    {
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var platform=typeof(TopLevel).GetProperty("PlatformImpl",flags)!.GetValue(window)!;
        var device=(IMouseDevice)platform.GetType().GetProperty("MouseDevice",flags)!.GetValue(platform)!;
        var point=control.TranslatePoint(new Point(control.Bounds.Width/2,control.Bounds.Height/2),window)!.Value;
        var ctor=typeof(RawPointerEventArgs).GetConstructors(flags).Single(c=>c.GetParameters().Length==6&&c.GetParameters()[4].ParameterType==typeof(Point));
        var input=(RawPointerEventArgs)ctor.Invoke([device,inputTime+=30,(IInputRoot)window,type,point,modifiers]);
        ((Action<RawInputEventArgs>)platform.GetType().GetProperty("Input",flags)!.GetValue(platform)!)(input);
    }
    internal static void InputStates()
    {
        using var f=new Fixture();
        var normal=(f.Save.Background as ISolidColorBrush)?.Color;
        Pointer(f.Window,f.Save,RawPointerEventType.Move);var hover=(f.Save.Background as ISolidColorBrush)?.Color;
        Pointer(f.Window,f.Save,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
        var pressed=(f.Save.Background as ISolidColorBrush)?.Color;
        Require(f.Save.IsPressed&&normal!=hover&&hover!=pressed,"Primary button lost hover/pressed feedback.");
        var heading=f.Window.FindControl<TextBlock>("HeadingText")!;
        Pointer(f.Window,heading,RawPointerEventType.Move,RawInputModifiers.LeftMouseButton);Pointer(f.Window,heading,RawPointerEventType.LeftButtonUp);
        Require(!f.Window.WasSaved,"Dragging off Save still saved.");
        f.Save.Focus(NavigationMethod.Tab);Require(f.Save.IsFocused,"Keyboard cannot focus Save.");
        var project=f.Window.FindControl<ComboBox>("ProjectSelector")!;
        project.Focus();project.IsDropDownOpen=true;ClockOverlayChecks.Pump(25);
        Require(project.IsDropDownOpen,"Native project dropdown cannot open.");project.IsDropDownOpen=false;
        var category=f.Window.FindControl<ComboBox>("CategorySelector")!;
        category.Focus();category.IsDropDownOpen=true;ClockOverlayChecks.Pump(25);
        Require(category.IsDropDownOpen,"Native category dropdown cannot open.");category.IsDropDownOpen=false;
        var segments=f.Window.FindControl<Expander>("SegmentsExpander")!;segments.BringIntoView();ClockOverlayChecks.Pump(20);
        var toggle=segments.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().First();
        toggle.Focus(NavigationMethod.Tab);PressKey(toggle,Avalonia.Input.Key.Space);
        toggle.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyUpEvent,Key=Avalonia.Input.Key.Space});ClockOverlayChecks.Pump(20);
        Require(segments.IsExpanded,"Keyboard activation cannot expand recorded segments.");
    }
    internal static void RecordActions()
    {
        using(var f=new Fixture())
        {
            Click(f.Window.FindControl<Button>("DiscardRecordsButton")!);
            Require(f.Window.IsVisible&&!f.Window.WasSaved&&!f.Window.DiscardRecordsRequested,"Discard applied before Save.");
            Click(f.Save);Require(f.Window.WasSaved&&f.Window.DiscardRecordsRequested,"Save lost the pending discard.");
        }
        using(var f=new Fixture())
        {
            Click(f.Window.FindControl<Button>("DiscardRecordsButton")!);PressKey(f.Window,Avalonia.Input.Key.Escape);
            Require(!f.Window.WasSaved&&!f.Window.DiscardRecordsRequested,"Cancel applied pending removal.");
        }
        using(var f=new Fixture())
        {
            PressKey(f.Window,Avalonia.Input.Key.Z,KeyModifiers.Control);Require(f.Window.UndoRequested&&f.Window.WasSaved,"Ctrl+Z lost the existing undo action.");
        }
        using(var f=new Fixture())
        {
            Window? confirmation=null;
            using var opened=Window.WindowOpenedEvent.AddClassHandler<Window>((window,_)=>{if(window.GetType().Name=="ConfirmationDialogWindow")confirmation=window;});
            Click(f.Window.FindControl<Button>("DeleteTimerButton")!);ClockOverlayChecks.Pump(30);
            Require(confirmation?.IsVisible==true&&!f.Window.DeleteTimerRequested,"Delete bypassed confirmation.");
            confirmation!.Close();ClockOverlayChecks.Pump(20);Require(f.Window.IsVisible&&!f.Window.DeleteTimerRequested,"Canceling confirmation deleted the timer.");
            Click(f.Window.FindControl<Button>("DeleteTimerButton")!);ClockOverlayChecks.Pump(30);
            Click(confirmation!.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Delete")));ClockOverlayChecks.Pump(20);
            Require(f.Window.DeleteTimerRequested&&f.Window.WasSaved&&!f.Window.IsVisible,"Confirmed deletion did not return the existing delete request.");
        }
    }
    internal static void ThemesModesAndLifecycle()
    {
        foreach(int mode in new[]{0,1,2,3})
        {
            using var f=new Fixture(mode:mode);Require(f.Window.Content is StopwatchFrameSurface,"Timer mode lost Nika presentation.");
            var timer=(DispatcherTimer)typeof(TimerEditorWindow).GetField("_liveRefreshTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Window)!;
            Require(timer.IsEnabled&&timer.Interval==TimeSpan.FromMilliseconds(500),"Live refresh cadence changed.");
            f.Window.Close();Require(!timer.IsEnabled,"Closed editor retained a refresh timer.");
        }
        using(var f=new Fixture(AppThemeCatalog.Midnight)){Require(f.Window.Content is not StopwatchFrameSurface,"Legacy editor acquired Nika chrome.");}
        using(var f=new Fixture())
        {
            Click(f.Window.FindControl<Button>("ToggleProjectInputButton")!);var name=f.Window.FindControl<TextBox>("CustomProjectBox")!;
            name.Text="برنامه‌نویسی پروژه — a very long project name";name.Focus(NavigationMethod.Tab);ClockOverlayChecks.Pump(20);
            Require(name.IsFocused&&name.Bounds.Width>150&&name.Text.Contains("برنامه"),"Long/RTL project entry lost focus or editable space.");
            Require(name.FlowDirection==FlowDirection.RightToLeft,"Persian project text does not use right-to-left direction.");
            name.Text="Pencil study";ClockOverlayChecks.Pump(20);
            Require(name.FlowDirection==FlowDirection.LeftToRight,"Latin project text did not restore left-to-right direction.");
        }
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        using var f=new Fixture();Require(f.Window.Content is StopwatchFrameSurface,"Cannot capture an unimplemented editor.");
        void Save(string name)
        {
            var frame=(Control)f.Window.Content!;using var bitmap=new RenderTargetBitmap(new PixelSize((int)frame.Bounds.Width,(int)frame.Bounds.Height),new Vector(96,96));
            bitmap.Render(frame);bitmap.Save(Path.Combine(Output,name+".png"));
        }
        Save("main");f.Window.FindControl<Expander>("SegmentsExpander")!.IsExpanded=true;f.Window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="EditorRecordsExpander").IsExpanded=true;
        ClockOverlayChecks.Pump(30);var scroll=f.Window.FindControl<ScrollViewer>("EditorScroll")!;scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(20);Save("records");
        f.Window.FindControl<Expander>("SegmentsExpander")!.IsExpanded=false;f.Window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="EditorRecordsExpander").IsExpanded=false;
        f.Time.Text="15x";scroll.Offset=default;ClockOverlayChecks.Pump(25);Save("invalid-duration");
        f.Time.Text="15m";f.Start.Text="bad";ClockOverlayChecks.Pump(20);Save("invalid-start");
        f.Start.Text=DateTime.Now.AddMinutes(-10).ToString("HH:mm:ss");
        Click(f.Window.FindControl<Button>("ToggleProjectInputButton")!);
        var name=f.Window.FindControl<TextBox>("CustomProjectBox")!;name.Text="برنامه‌نویسی پروژه — a very long project name";
        ClockOverlayChecks.Pump(20);Save("long-project");
        Click(f.Window.FindControl<Button>("ToggleProjectInputButton")!);
        foreach(string selector in new[]{"ProjectSelector","CategorySelector"})
        {
            var box=f.Window.FindControl<ComboBox>(selector)!;box.IsDropDownOpen=true;ClockOverlayChecks.Pump(30);
            var popup=box.GetVisualDescendants().OfType<Popup>().Concat(box.GetLogicalDescendants().OfType<Popup>()).First();
            var child=popup.Child!;Require(child.Bounds.Width>50&&child.Bounds.Height>20,"Dropdown content did not render.");
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(child.Bounds.Width),(int)Math.Ceiling(child.Bounds.Height)),new Vector(96,96));
            bitmap.Render(child);bitmap.Save(Path.Combine(Output,selector+"-dropdown.png"));box.IsDropDownOpen=false;
        }
    }
}
