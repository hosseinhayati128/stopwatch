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
        internal ComboBox State => Window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "StateAfterSave");
        internal Fixture(string theme=AppThemeCatalog.Nika,int mode=0,bool intervals=true,TimeSpan? elapsed=null,
            bool pixel=false,bool running=true,string name="Illustration",AppSettings? settings=null,bool canUndo=true)
        {
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            var value=elapsed??TimeSpan.FromSeconds(2667);Timer.RestoreElapsed(value,false);Timer.IsRunning=running;Timer.Mode=mode;Timer.CountdownRemaining=value;Timer.Name=name;
            var now=DateTime.UtcNow;now=new DateTime(now.Ticks-now.Ticks%TimeSpan.TicksPerSecond,DateTimeKind.Utc);
            ProjectWorkIntervalView[] records=intervals?[
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddHours(-3).AddSeconds(-890),now.AddHours(-3)),
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddHours(-2).AddSeconds(-878),now.AddHours(-2)),
                new(Guid.NewGuid(),Timer.Id,"Illustration","Illustration",now.AddSeconds(-899),null)]:[];
            Window=new TimerEditorWindow(Timer,[name,"Reading"],records,canUndo,settings??new AppSettings{ThemeMode=theme,ActivityWatchEnabled=false,NikaControllerPixel=pixel});
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
        f.State.SelectedIndex=1;f.Time.Text="1h 30m";ClockOverlayChecks.Pump(20);Click(f.Save);
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
    internal static void ProjectRecords()
    {
        const string folder = "design/one-piece-v2/timer-editor/project-records-implementation";
        Directory.CreateDirectory(folder);
        foreach (bool pixel in new[] { false, true })
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel, UiScalePercent = scale };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            try
            {
                foreach (string state in new[] { "included", "pending", "empty" })
                {
                    bool hasRecords = state != "empty";
                    using var f = new Fixture(intervals: hasRecords, settings: settings);
                    var section = f.Window.GetVisualDescendants().OfType<Expander>().Single(e => e.Name == "EditorRecordsExpander");
                    var action = f.Window.FindControl<Button>("DiscardRecordsButton")!;
                    var score = f.Window.FindControl<NumericUpDown>("ScorePerHourBox")!;
                    var summary = ((StackPanel)section.Header!).Children.OfType<TextBlock>().Last();
                    score.Value = 2.5m;
                    Require(!section.IsExpanded && summary.IsVisible && summary.Text == (hasRecords ? "Included in project history" : "No saved records"),
                        "Collapsed records summary does not match the timer's history.");
                    section.IsExpanded = true;
                    ClockOverlayChecks.Pump(25);
                    Require(!summary.IsVisible && action.IsVisible == hasRecords, "Incorrect expanded summary or history action.");
                    if (hasRecords)
                    {
                        action.Focus(NavigationMethod.Tab);
                        Click(action);
                        Require(Equals(action.Content, "Keep records") && action.IsFocused && !f.Window.DiscardRecordsRequested,
                            "Marking records lost keyboard focus or immediately applied removal.");
                        section.IsExpanded = false;
                        Require(summary.IsVisible && summary.Text == "Removal pending", "Collapsed header lost pending status.");
                        section.IsExpanded = true;
                        if (state == "included")
                        {
                            Click(action);
                            Require(Equals(action.Content, "Remove on save") && score.Value == 2.5m, "Keeping records lost an unrelated draft edit.");
                        }
                    }
                    f.Save.Focus();
                    ClockOverlayChecks.Pump(25);
                    var scroll = f.Window.FindControl<ScrollViewer>("EditorScroll")!;
                    scroll.Offset = new Vector(0, scroll.Extent.Height);
                    ClockOverlayChecks.Pump(35);
                    var bottom = section.TranslatePoint(new Point(0, section.Bounds.Height), scroll)!.Value.Y;
                    Require(section.TranslatePoint(default, scroll)!.Value.Y >= -1 && bottom <= scroll.Bounds.Height + 1,
                        $"Project records is clipped at {scale}%.");
                    Capture(f.Window, Path.Combine(folder, $"{(pixel ? "pixel" : "classic")}-{state}-{scale}.png"));
                    Click(f.Save);
                    Require(f.Window.WasSaved && f.Window.NewScorePerHour == 2.5 && f.Window.DiscardRecordsRequested == (state == "pending"),
                        "Save lost the score or applied the wrong history action.");
                }
            }
            finally { DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
        }
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
            Click(confirmation!.FindControl<Button>("ConfirmButton")!);ClockOverlayChecks.Pump(20);
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

    internal static void IdentityAndTimeDraft()
    {
        const string output = "design/one-piece-v2/timer-editor/identity-implementation";
        Directory.CreateDirectory(output);
        foreach (bool pixel in new[] { false, true })
        foreach (int percent in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent, NikaControllerPixel = pixel };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            try
            {
                foreach (var (sample, name, running, mode, seconds, delta) in new[]
                {
                    ("running", "Illustration", true, 0, 2538, 300),
                    ("paused", "Coastal field recordings and interview edits for the archive", false, 0, 4592, -60),
                    ("countdown", "Sketch warm-up", true, 2, 762, 300)
                })
                {
                    var elapsed = TimeSpan.FromSeconds(seconds);
                    using var f = new Fixture(mode: mode, intervals: false, elapsed: elapsed, pixel: pixel, running: running, name: name);
                    string Stamp(TimeSpan value) => value.ToString(@"hh\:mm\:ss");
                    var texts = f.Window.GetVisualDescendants().OfType<TextBlock>().ToArray();
                    var project = texts.Single(t => t.Text == name && t.TextWrapping == TextWrapping.Wrap && t.FontSize == 21);
                    var liveState = texts.Single(t => t.Text == (running ? "Running" : "Paused") && t.FontSize == 12);
                    var digits = f.Window.GetVisualDescendants().OfType<ControllerPixelDigits>().SingleOrDefault();
                    var classicDigits = texts.SingleOrDefault(t => t.Name == "ClassicTimerReading");
                    Require(pixel || classicDigits?.Foreground is ISolidColorBrush { Color: var ink } && ink == Color.Parse("#FFF2D5"),
                        "Classic digits lost contrast against the navy band.");
                    string? Reading() => pixel ? digits!.Text : classicDigits!.Text;
                    Require((digits != null) == pixel && Reading() == Stamp(elapsed) && f.Time.Text == Stamp(elapsed), "Wrong live or initial draft reading for " + sample);
                    Require(f.State.SelectedIndex == (running ? 0 : 1), "Incorrect initial state selection.");
                    var draft = TimeSpan.FromSeconds(seconds + delta);
                    Click(f.Window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Tag, delta.ToString())));
                    f.Save.Focus();
                    f.Window.FindControl<ScrollViewer>("EditorScroll")!.Offset = default;
                    ClockOverlayChecks.Pump(30);
                    Require(f.Time.Text == Stamp(draft) && f.Save.IsEnabled && Reading() == Stamp(elapsed), "Quick adjustment changed live time or lost draft.");
                    double zoom = percent / 100d;
                    var size = PixelSize.FromSize(f.Window.ClientSize, 1);
                    var screen = f.Window.Screens.ScreenFromWindow(f.Window) ?? f.Window.Screens.Primary!;
                    var footer = f.Save.TranslatePoint(new Point(f.Save.Bounds.Width, f.Save.Bounds.Height), f.Window)!.Value;
                    Require(Math.Abs(size.Width - 620 * zoom) <= 2 && Math.Abs(size.Height - Math.Min(780 * zoom, screen.WorkingArea.Height / f.Window.RenderScaling - 16)) <= 2,
                        $"Incorrect native client size at {percent}%: {size}.");
                    Require(footer.Y <= size.Height && footer.X <= size.Width && project.TranslatePoint(new Point(0, project.Bounds.Height), f.Window)!.Value.Y < f.Time.TranslatePoint(default, f.Window)!.Value.Y,
                        $"Clipped identity or footer at {percent}%.");
                    Require(sample != "paused" || project.Bounds.Height > 40, "Long project name did not wrap.");
                    Capture(f.Window, Path.Combine(output, $"{(pixel ? "pixel" : "classic")}-{sample}-{percent}.png"));
                    f.State.SelectedIndex = running ? 1 : 0;
                    var next = elapsed + TimeSpan.FromSeconds(mode == 2 ? -1 : 1);
                    if (mode == 2) f.Timer.CountdownRemaining = next;
                    else f.Timer.RestoreElapsed(next, false);
                    ClockOverlayChecks.Pump(550);
                    Require(Reading() == Stamp(next) && f.Time.Text == Stamp(draft) && liveState.Text == (running ? "Running" : "Paused") && f.Timer.IsRunning == running,
                        "Live refresh and pending edits interfered with each other.");
                    Click(f.Save);
                    Require(f.Window.WasSaved && f.Window.NewTimeValue == draft && f.Window.NewIsRunning == !running && f.Window.NewProjectName == name, "Incorrect saved draft.");
                }
            }
            finally { DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
        }
        using var clock = new Fixture(mode: 1, intervals: false);
        var reading = clock.Window.FindControl<TextBlock>("LiveTimePreview")!;
        Require(TimeSpan.TryParse(reading.Text, out var time) && (DateTime.Now.TimeOfDay - time).Duration() < TimeSpan.FromSeconds(3) &&
            !clock.Window.GetVisualDescendants().Any(v => v is ComboBox { Name: "StateAfterSave" }), "Clock mode has a stale wall time or an inapplicable state selector.");
        using var canceled = new Fixture(intervals: false);
        canceled.Time.Text = "20m";
        canceled.State.SelectedIndex = 1;
        PressKey(canceled.Window, Avalonia.Input.Key.Escape);
        Require(!canceled.Window.WasSaved && canceled.Timer.IsRunning && canceled.Timer.Elapsed == TimeSpan.FromSeconds(2667), "Cancel applied pending time or state.");
    }

    private static void Capture(Window window, string file, PixelRect? crop = null)
    {
        var size = PixelSize.FromSize(window.ClientSize, 1);
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var transform = frame.RenderTransform;
        using var image = new RenderTargetBitmap(crop?.Size ?? size, new Vector(96, 96));
        frame.SetCurrentValue(Visual.RenderTransformProperty, null);
        try
        {
            using var dc = image.CreateDrawingContext();
            var brush = new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) };
            dc.DrawRectangle(brush, null, new Rect(-(crop?.X ?? 0), -(crop?.Y ?? 0), size.Width, size.Height));
        }
        finally { frame.SetCurrentValue(Visual.RenderTransformProperty, transform); }
        image.Save(file);
    }

    internal static void Footer()
    {
        const string folder = "design/one-piece-v2/timer-editor/footer-implementation";
        Directory.CreateDirectory(folder);
        foreach (int scale in new[] { 70, 90, 125 })
        foreach (bool pixel in new[] { false, true })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel, UiScalePercent = scale };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            try
            {
                foreach (string state in new[] { "available", "unavailable", "invalid" })
                {
                    using var f = new Fixture(intervals: false, running: false, settings: settings, canUndo: state != "unavailable");
                    var footer = f.Window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "EditorFooter");
                    var error = footer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "FooterError");
                    var undo = f.Window.FindControl<Button>("UndoPreviousEditButton")!;
                    var cancel = f.Window.FindControl<Button>("CancelButton")!;
                    var scroll = f.Window.FindControl<ScrollViewer>("EditorScroll")!;
                    var initial = f.Save.TranslatePoint(default, f.Window)!.Value;
                    if (state == "invalid") f.Time.Text = "15x";
                    ClockOverlayChecks.Pump(30);
                    Require(undo.IsVisible == (state != "unavailable") && f.Save.IsEnabled == (state != "invalid"), "Wrong footer action availability.");
                    Require(error.IsEffectivelyVisible == (state == "invalid") && (state != "invalid" || error.Text == "Enter a valid elapsed time to save."),
                        "Footer validation does not match the invalid field.");
                    scroll.Offset = new Vector(0, scroll.Extent.Height);
                    ClockOverlayChecks.Pump(30);
                    var after = f.Save.TranslatePoint(default, f.Window)!.Value;
                    Require(Math.Abs(initial.X - after.X) <= 1 && Math.Abs(initial.Y - after.Y) <= 1, "Scrolling or validation moved Save.");
                    Require(undo.Classes.Contains("pixelAction") == pixel && f.Save.Classes.Contains("pixelAction") == pixel, "Wrong footer button style.");
                    foreach (var (button, key) in new[] { (undo, "Ctrl+Z"), (cancel, "Esc"), (f.Save, "Ctrl+Enter") })
                    {
                        if (!button.IsVisible) continue;
                        var hint = button.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == key);
                        var right = hint.TranslatePoint(new Point(hint.Bounds.Width, hint.Bounds.Height), button)!.Value;
                        Require(right.X <= button.Bounds.Width && right.Y <= button.Bounds.Height, $"Clipped {key} at {scale}%.");
                    }
                    var a = footer.TranslatePoint(default, f.Window)!.Value;
                    var b = footer.TranslatePoint(new Point(footer.Bounds.Width, footer.Bounds.Height), f.Window)!.Value;
                    int x = (int)Math.Floor(a.X) - 5, y = (int)Math.Floor(a.Y) - 4;
                    var rect = new PixelRect(x, y, (int)Math.Ceiling(b.X) + 5 - x, (int)Math.Ceiling(b.Y) + 4 - y);
                    Require(x >= 0 && y >= 0 && rect.Right <= f.Window.ClientSize.Width + 1 && rect.Bottom <= f.Window.ClientSize.Height + 1,
                        "Footer outside client area.");
                    Capture(f.Window, Path.Combine(folder, $"{(pixel ? "pixel" : "classic")}-{state}-{scale}.png"), rect);

                    switch (state)
                    {
                        case "available":
                            PressKey(cancel, Avalonia.Input.Key.Z, KeyModifiers.Control);
                            Require(f.Window.WasSaved && f.Window.UndoRequested, "Ctrl+Z failed to undo.");
                            break;
                        case "unavailable":
                            PressKey(cancel, Avalonia.Input.Key.Z, KeyModifiers.Control);
                            Require(f.Window.IsVisible && !f.Window.UndoRequested, "Unavailable undo ran.");
                            PressKey(cancel, Avalonia.Input.Key.Escape);
                            Require(!f.Window.IsVisible && !f.Window.WasSaved, "Escape failed to cancel.");
                            break;
                        case "invalid":
                            PressKey(cancel, Avalonia.Input.Key.Enter, KeyModifiers.Control);
                            Require(f.Window.IsVisible && !f.Window.WasSaved, "Ctrl+Enter accepted an invalid time.");
                            f.Start.Text = "bad";
                            f.Time.Text = "15m";
                            ClockOverlayChecks.Pump(20);
                            Require(!f.Save.IsEnabled && error.Text == "Enter a valid start time to save.", "Footer did not switch validation fields.");
                            f.Start.Text = DateTime.Now.AddMinutes(-10).ToString("HH:mm:ss");
                            f.Time.Text = "15m";
                            ClockOverlayChecks.Pump(20);
                            Require(f.Save.IsEnabled && !error.IsEffectivelyVisible, "Correcting time left Save disabled.");
                            PressKey(cancel, Avalonia.Input.Key.Enter, KeyModifiers.Control);
                            Require(f.Window.WasSaved && f.Window.NewTimeValue == TimeSpan.FromMinutes(15), "Ctrl+Enter failed after correction.");
                            break;
                    }
                }
            }
            finally { DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
        }
        var large = new TypographySettings();
        large.Global.SizePercent = 175;
        DeleteTodoNativeChecks.Desktop("DesktopTypography", "Initialize", large);
        try
        {
            using var f = new Fixture();
            f.Window.Width = f.Window.MinWidth;
            ClockOverlayChecks.Pump(50);
            var undo = f.Window.FindControl<Button>("UndoPreviousEditButton")!;
            var cancel = f.Window.FindControl<Button>("CancelButton")!;
            var undoEnd = undo.TranslatePoint(new Point(undo.Bounds.Width, undo.Bounds.Height), f.Window)!.Value;
            var cancelStart = cancel.TranslatePoint(default, f.Window)!.Value;
            var saveEnd = f.Save.TranslatePoint(new Point(f.Save.Bounds.Width, f.Save.Bounds.Height), f.Window)!.Value;
            Require(undoEnd.Y <= cancelStart.Y && cancelStart.X >= 0 && saveEnd.X <= f.Window.ClientSize.Width && saveEnd.Y <= f.Window.ClientSize.Height,
                "Large dialog fonts clipped or overlapped footer actions.");
        }
        finally { DeleteTodoNativeChecks.Desktop("DesktopTypography", "Shutdown"); }
    }

    internal static void ProjectFields()
    {
        const string output = "design/one-piece-v2/timer-editor/project-fields-implementation";
        Directory.CreateDirectory(output);
        foreach (bool pixel in new[] { false, true })
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel, UiScalePercent = scale };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            try
            {
                foreach (string state in new[] { "existing", "long", "new" })
                {
                    string name = state == "long" ? "Field recordings and interview edits for the coastal history archive · Spring collection" : "Illustration";
                    int minutes = state == "long" ? 12 : 5;
                    settings.ProjectIdleRules[name] = new() { Enabled = true, IdleMinutes = minutes };
                    using var f = new Fixture(name: name, intervals: false, settings: settings, elapsed: TimeSpan.FromSeconds(2538));
                    T Get<T>(string id) where T : Control => f.Window.FindControl<T>(id)!;
                    var project = Get<ComboBox>("ProjectSelector");
                    var toggleProject = Get<Button>("ToggleProjectInputButton");
                    var toggleCategory = Get<Button>("ToggleCategoryInputButton");
                    var idle = Get<CheckBox>("IdleStopForProjectCheck");
                    var score = Get<NumericUpDown>("ScorePerHourBox");
                    score.Value = 2.5m;
                    Require(Equals(toggleProject.Content, "New project") && Equals(toggleCategory.Content, "New category") &&
                        Equals(idle.Content, $"Stop timers for this project after {minutes} idle minutes") && idle.IsChecked == true,
                        "Incorrect initial project actions or idle rule.");
                    Require(project.Bounds.Width > 470 && (state != "long" || project.Bounds.Height > 40), "Project selection failed to use full width or wrap.");
                    string expectedCategory = "Creative", expectedProject = name;
                    if (state == "new")
                    {
                        project.SelectedIndex = 0;
                        Require(Equals(idle.Content, "Stop unassigned timers after 5 idle minutes"), "Unassigned idle rule has the wrong scope.");
                        project.SelectedItem = name;
                        var projectEntry = Get<TextBox>("CustomProjectBox");
                        var categoryEntry = Get<TextBox>("CustomCategoryBox");
                        Click(toggleProject); projectEntry.Text = expectedProject = "Oral history interviews";
                        Click(toggleCategory); categoryEntry.Text = expectedCategory = "Fieldwork";
                        Click(toggleProject); Click(toggleCategory);
                        Require(project.IsVisible && Equals(project.SelectedItem, name) && Get<ComboBox>("CategorySelector").IsFocused, "Existing selection or focus lost.");
                        Click(toggleCategory); Click(toggleProject);
                        ClockOverlayChecks.Pump(25);
                        Require(projectEntry.IsFocused && projectEntry.Text == expectedProject && categoryEntry.Text == expectedCategory &&
                            Equals(toggleProject.Content, "Choose existing") && Equals(toggleCategory.Content, "Choose existing") &&
                            idle.IsChecked == false && Equals(idle.Content, "Stop timers for this project after 5 idle minutes"), "New-entry mode lost its draft or idle rule.");
                        projectEntry.SelectionStart = projectEntry.SelectionEnd = projectEntry.CaretIndex = expectedProject.Length;
                    }
                    else f.Save.Focus();
                    var scroll = Get<ScrollViewer>("EditorScroll");
                    scroll.Offset = default;
                    ClockOverlayChecks.Pump(45);
                    var bottom = idle.TranslatePoint(new Point(0, idle.Bounds.Height), scroll)!.Value.Y;
                    if (bottom > scroll.Bounds.Height - 4) { scroll.Offset = new Vector(0, bottom - scroll.Bounds.Height + 8); ClockOverlayChecks.Pump(30); }
                    Require(idle.TranslatePoint(new Point(0, idle.Bounds.Height), scroll)!.Value.Y <= scroll.Bounds.Height + 1, "Idle rule clipped.");
                    Capture(f.Window, Path.Combine(output, $"{(pixel ? "pixel" : "classic")}-{state}-{scale}.png"));
                    Click(f.Save);
                    Require(f.Window.WasSaved && f.Window.NewProjectName == expectedProject && f.Window.NewCategory == expectedCategory &&
                        f.Window.NewScorePerHour == 2.5 && f.Timer.Name == name, "Project editor returned the wrong saved values.");
                }
            }
            finally { DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
        }
    }
}
