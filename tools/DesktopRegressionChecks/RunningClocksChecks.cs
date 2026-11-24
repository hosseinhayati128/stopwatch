using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class RunningClocksChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string Output="design/one-piece-v2/running-clocks/navy-implementation";
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private sealed class Fixture:IDisposable
    {
        private readonly string data=Path.Combine(Path.GetTempPath(),"stopwatch-shelf-"+Guid.NewGuid().ToString("N"));
        internal readonly AppSettings Settings=new(){ThemeMode=AppThemeCatalog.Nika,TimeFormat=1,ActivityWatchEnabled=false};
        internal readonly ControllerWindow Controller;
        internal readonly TimerSessionManager Timers;
        internal Window Window=> (Window)typeof(ControllerWindow).GetField("_activeClocksShelf",Hidden)!.GetValue(Controller)!;
        internal Fixture(int count=3, bool pixel=false, int scale=100)
        {
            Settings.UiScalePercent=scale;
            Settings.NikaControllerPixel=pixel;
            Controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([Settings]);
            typeof(ControllerWindow).GetField("_projectTimeStore",Hidden)!.SetValue(Controller,new ProjectTimeStore(Path.Combine(data,"history.json")));
            Timers=(TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager",Hidden)!.GetValue(Controller)!;
            while(Timers.Count<count)Timers.Create();
            string[] names=["Illustration","Coding","Reading","Study","Writing — chapter two"];
            for(int i=0;i<Timers.Count;i++)
            {
                var timer=Timers.Sessions[i];timer.Name=i<names.Length?names[i]:"Clock "+(i+1);
                timer.RestoreElapsed(TimeSpan.FromSeconds(i==0?2667:i==1?4328:1122),false);timer.IsRunning=i<count;
            }
            Timers.Activate(Timers.Sessions[0]);Call("ToggleActiveClocksShelf");ClockOverlayChecks.Pump(50);
        }
        internal void Call(string name)=>typeof(ControllerWindow).GetMethod(name,Hidden)!.Invoke(Controller,null);
        internal void Update(){Call("UpdateActiveClocksShelf");ClockOverlayChecks.Pump(30);}
        internal Button[] Buttons(string name)=>Window.GetVisualDescendants().OfType<Button>().Where(b=>b.Name==name).ToArray();
        internal TextBlock[] Texts(string name)=>Window.GetVisualDescendants().OfType<TextBlock>().Where(b=>b.Name==name).ToArray();
        public void Dispose(){Controller.Close();if(Directory.Exists(data))Directory.Delete(data,true);}
    }
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Key(Control target,Avalonia.Input.Key key)
    {
        target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});
        target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyUpEvent,Key=key});
    }
    private static ulong inputTime=10000;
    internal static void Pointer(Window window,Control control,RawPointerEventType type,RawInputModifiers modifiers=RawInputModifiers.None)
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
        var select=f.Buttons("RunningClockSelect")[1];var pause=f.Buttons("RunningClockPause")[1];
        Pointer(f.Window,select,RawPointerEventType.Move);
        Require(select.IsPointerOver&&f.Timers.Active==f.Timers.Sessions[0],"Hover activated a timer or missed its name.");
        Require((select.Background as ISolidColorBrush)?.Color==Color.Parse("#66FFFFFF"),"Name hover wash is missing.");
        f.Window.Activate();select.Focus(NavigationMethod.Tab);Key(select,Avalonia.Input.Key.Enter);ClockOverlayChecks.Pump(30);
        Require(f.Timers.Active==f.Timers.Sessions[1],"Enter did not select the focused timer.");
        Key(select,Avalonia.Input.Key.Tab);
        Require(pause.IsFocused,"Tab did not move from the name to its Pause action.");
        Require((pause.BorderBrush as ISolidColorBrush)?.Color==Color.Parse("#F6E8C9"),"Pause has no visible keyboard focus.");
        Pointer(f.Window,pause,RawPointerEventType.Move);var hover=(pause.Background as ISolidColorBrush)?.Color;
        Pointer(f.Window,pause,RawPointerEventType.LeftButtonDown,RawInputModifiers.LeftMouseButton);
        Require(pause.IsPressed&&(pause.Background as ISolidColorBrush)?.Color!=hover,"Pause has no distinct pressed state.");
        Pointer(f.Window,select,RawPointerEventType.Move,RawInputModifiers.LeftMouseButton);Pointer(f.Window,select,RawPointerEventType.LeftButtonUp);
        Require(f.Timers.Sessions[1].IsRunning,"Releasing outside Pause still paused the timer.");
        var hide=f.Buttons("RunningClocksHide").Single();Pointer(f.Window,hide,RawPointerEventType.Move);
        Require((hide.Background as ISolidColorBrush)?.Color==Color.Parse("#66FFFFFF"),"Hide hover is hidden by its base style.");
    }
    internal static void Layout()
    {
        using var f=new Fixture();
        Require(f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Running Clocks still uses the basic rectangular layout.");
        Require(f.Window.SystemDecorations==SystemDecorations.None&&((ISolidColorBrush)f.Window.Background!).Color.A==0,"Shelf has native chrome or an opaque exterior.");
        Require(Math.Abs(f.Window.Bounds.Width-410)<1&&Math.Abs(f.Window.Bounds.Height-272)<1,"Three-row shelf lost the approved spacing.");
        Require(f.Buttons("RunningClockSelect").Length==3&&f.Buttons("RunningClockPause").All(b=>b.Bounds.Width>=34&&b.Bounds.Height>=34),"Rows or accessible pause hit areas are missing.");
        var positions=f.Buttons("RunningClockSelect").Select(b=>b.TranslatePoint(default,f.Window)!.Value.Y).ToArray();
        Require(Math.Abs(positions[1]-positions[0]-51)<1,"Incorrect shelf row spacing.");
        var clock=f.Timers.Sessions[3];clock.Mode=1;clock.IsRunning=true;f.Update();
        Require(f.Buttons("RunningClockSelect").Length==3,"Wall clocks entered the running-timer list.");
    }
    internal static void ActionsAndFocus()
    {
        using var f=new Fixture();
        Require(f.Buttons("RunningClockSelect").Length==3,"Native shelf selection controls are missing.");
        Click(f.Buttons("RunningClockSelect")[1]);
        Require(f.Timers.Active==f.Timers.Sessions[1]&&f.Timers.Sessions.Take(3).All(t=>t.IsRunning),"Selecting a clock stopped another timer or did not activate it.");
        f.Window.Activate();var pause=f.Buttons("RunningClockPause")[1];pause.Focus(NavigationMethod.Tab);
        var elapsed=f.Timers.Active!.Elapsed;Key(pause,Avalonia.Input.Key.Space);ClockOverlayChecks.Pump(40);
        Require(!f.Timers.Sessions[1].IsRunning&&f.Timers.Sessions[1].Elapsed==elapsed&&f.Timers.Sessions[0].IsRunning&&f.Timers.Sessions[2].IsRunning,"Pause reset time or affected another timer.");
        Require(f.Timers.Active==f.Timers.Sessions[1]&&f.Buttons("RunningClockSelect").Length==2,"Pausing changed active session or retained its shelf row.");
        Require(f.Buttons("RunningClockSelect")[1].IsFocused,"Removed Pause row did not move focus safely to the next name.");
        Require(AutomationProperties.GetName(f.Buttons("RunningClockPause")[1])!.Contains("Reading"),"Pause accessible name omits its timer.");
        Click(f.Buttons("RunningClocksHide").Single());Require(!f.Window.IsVisible&&f.Timers.Sessions[0].IsRunning,"Hide stopped a timer or left the shelf visible.");
        f.Call("ToggleActiveClocksShelf");ClockOverlayChecks.Pump(30);
        foreach(var timer in f.Timers.Sessions)timer.IsRunning=false;
        f.Buttons("RunningClockPause")[0].Focus(NavigationMethod.Tab);f.Update();
        Require(f.Buttons("RunningClocksHide").Single().IsFocused&&Math.Abs(f.Window.Bounds.Height-159)<1,"Empty shelf failed to shrink or retain usable focus.");
    }
    internal static void ReadabilityAndScrolling()
    {
        using var f=new Fixture(8);f.Settings.TimeFormat=0;
        f.Timers.Sessions[0].Name="Illustration — The Grand Voyage with a very long name";
        f.Timers.Sessions[0].RestoreElapsed(TimeSpan.MaxValue,false);f.Timers.Sessions[0].IsRunning=true;
        f.Timers.Sessions[1].Name="برنامه‌نویسی پروژه";f.Update();
        Require(f.Texts("RunningClockTime").Length==8,"Native time controls are missing.");
        var time=f.Texts("RunningClockTime")[0];var name=f.Texts("RunningClockName")[0];
        Require(time.Text=="256204778:48:05.4"&&time.FontSize>=14&&time.FlowDirection==FlowDirection.LeftToRight,"Complete long-hour time was lost or made too small.");
        Require(time.DesiredSize.Width<=time.Bounds.Width+.5&&name.Bounds.Width>=80,"Time clipped or consumed the entire name area.");
        Require(name.TextTrimming==TextTrimming.CharacterEllipsis&&f.Texts("RunningClockName")[1].FlowDirection==FlowDirection.RightToLeft,"Long/RTL names lost their presentation.");
        var scroll=f.Window.GetVisualDescendants().OfType<ScrollViewer>().Single();
        var hide=f.Buttons("RunningClocksHide").Single();var at=hide.TranslatePoint(default,f.Window);
        Require(scroll.Extent.Height>scroll.Viewport.Height&&Math.Abs(f.Window.Bounds.Height-374)<1,"Long lists do not scroll within five rows.");
        f.Buttons("RunningClockSelect")[7].BringIntoView();ClockOverlayChecks.Pump(30);
        Require(scroll.Offset.Y>0&&at==hide.TranslatePoint(default,f.Window),"Scrolling moved the footer or failed to reveal the last row.");
        f.Window.Activate();var lastPause=f.Buttons("RunningClockPause")[7];lastPause.Focus(NavigationMethod.Tab);
        Key(lastPause,Avalonia.Input.Key.Space);ClockOverlayChecks.Pump(40);
        var next=f.Buttons("RunningClockSelect")[6];var location=next.TranslatePoint(default,scroll)!.Value;
        Require(next.IsFocused&&location.Y>=0&&location.Y+next.Bounds.Height<=scroll.Bounds.Height+1,
            $"Pausing the final scrolled row lost visible name focus: focused={next.IsFocused}, y={location.Y}, row={next.Bounds.Height}, viewport={scroll.Bounds.Height}, offset={scroll.Offset.Y}.");
    }
    internal static void StableUpdatesAndThemes()
    {
        using var f=new Fixture();Require(f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika shelf is not framed.");
        var buttons=f.Buttons("RunningClockSelect");var frame=f.Window.Content;
        for(int i=0;i<60;i++)f.Call("UpdateActiveClocksShelf");
        Require(ReferenceEquals(frame,f.Window.Content)&&buttons.SequenceEqual(f.Buttons("RunningClockSelect")),"Unchanged ticks rebuild the shelf controls.");
        f.Settings.ThemeMode=AppThemeCatalog.Midnight;f.Update();
        Require(!f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any()&&f.Window.GetVisualDescendants().OfType<Button>().Count(b=>Equals(b.Content,"Pause"))==3,"Legacy theme lost its plain shelf.");
        f.Settings.ThemeMode=AppThemeCatalog.Nika;f.Update();Require(f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any()&&f.Buttons("RunningClockSelect").Length==3,"Switching back did not restore the shelf.");
        f.Settings.NikaControllerPixel=true;f.Update();
        Require(f.Window.GetVisualDescendants().OfType<ControllerPixelDigits>().Count()==3,"Open shelf did not adopt the controller's Pixel profile.");
        var rows=f.Buttons("RunningClockSelect"); f.Settings.UiScalePercent=70;f.Update();
        Require(rows.SequenceEqual(f.Buttons("RunningClockSelect"))&&Math.Abs(f.Window.Width-287)<1,"Scale change rebuilt rows or lost the requested width.");
        Click(f.Buttons("RunningClocksHide").Single());f.Timers.Sessions[3].IsRunning=true;f.Call("ToggleActiveClocksShelf");ClockOverlayChecks.Pump(30);
        Require(f.Buttons("RunningClockSelect").Length==4,"Reopening retained stale running clocks.");
    }
    internal static void Placement()
    {
        using var f=new Fixture(8);var overlay=new OverlayWindow();
        try
        {
        overlay.ShowActivated=false;overlay.Show();ClockOverlayChecks.Pump(30);
        var area=overlay.Screens.ScreenFromVisual(overlay)!.WorkingArea;
        overlay.Position=new PixelPoint(area.Right-(int)(overlay.Bounds.Width*overlay.RenderScaling)-2,area.Bottom-(int)(overlay.Bounds.Height*overlay.RenderScaling)-2);
        f.Window.Hide();f.Window.GetType().GetMethod("RepositionUnder",Hidden)!.Invoke(f.Window,[overlay]);f.Window.Show();ClockOverlayChecks.Pump(40);
        void Inside()=>Require(f.Window.Position.X>=area.X&&f.Window.Position.Y>=area.Y&&
            f.Window.Position.X+f.Window.Bounds.Width*f.Window.RenderScaling<=area.Right+2&&f.Window.Position.Y+f.Window.Bounds.Height*f.Window.RenderScaling<=area.Bottom+2,"Shelf was placed beyond its anchor monitor.");
        Inside();foreach(var t in f.Timers.Sessions.Skip(1))t.IsRunning=false;f.Update();
        // The controller has no visible overlay anchor in this fixture; retain the explicit one.
        f.Window.GetType().GetMethod("RepositionUnder",Hidden)!.Invoke(f.Window,[overlay]);Inside();
        }
        finally{overlay.Close();}
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        foreach(bool pixel in new[]{false,true})
        foreach(int scale in new[]{70,90,125})
        foreach(int count in new[]{4,0})
        {
            using var f=new Fixture(count,pixel,scale);
            string[] names=["Illustration","Coastal recordings and field notes","Reading","Piano practice"];
            int[] seconds=[2678,4328,1122,555];
            for(int i=0;i<count;i++)
            {
                var timer=f.Timers.Sessions[i];timer.Name=names[i];timer.RestoreElapsed(TimeSpan.FromSeconds(seconds[i]),false);timer.IsRunning=true;
            }
            f.Update();
            var content=f.Window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
            var size=PixelSize.FromSize(f.Window.ClientSize,1);
            Require(Math.Abs(size.Width-410*scale/100d)<2 && Math.Abs(size.Height-(count==0?159:323)*scale/100d)<2,"Incorrect scaled shelf size.");
            if(count>0)
            {
                var name=f.Texts("RunningClockName")[1];
                Require(name.TextLayout.TextLines.Count==2&&!name.TextLayout.TextLines.Any(l=>l.HasCollapsed),"Long project should wrap completely.");
                var rows=content.GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="RunningClockRow").ToArray();
                bool Active(int i)=>rows[i].GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="RunningClockActive").IsVisible
                    && (rows[i].Background as ISolidColorBrush)?.Color.A>0;
                Require(Active(0)&&!Active(1),"Active tint and label do not identify the current timer.");
                Click(f.Buttons("RunningClockSelect")[1]);f.Update();Require(!Active(0)&&Active(1),"Active treatment did not move to the selected row.");
                Click(f.Buttons("RunningClockSelect")[0]);f.Update();
                var scroll=content.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(scroll.Extent.Height<=scroll.Viewport.Height+1,"Four rows should fit without scrolling.");
                Require(content.GetVisualDescendants().OfType<ControllerPixelDigits>().Count()==(pixel?count:0),"Wrong clock face.");
            }
            using var image=new RenderTargetBitmap(size,new Vector(96,96));
            var transform=content.RenderTransform;
            content.SetCurrentValue(Visual.RenderTransformProperty,null);
            try
            {
                using var dc=image.CreateDrawingContext();
                dc.DrawRectangle(new VisualBrush(content){Stretch=Stretch.Fill,SourceRect=new RelativeRect(new Rect(content.Bounds.Size),RelativeUnit.Absolute)},
                    null,new Rect(0,0,size.Width,size.Height));
            }
            finally {content.SetCurrentValue(Visual.RenderTransformProperty,transform);}
            image.Save(Path.Combine(Output,$"{(pixel?"pixel":"classic")}-{(count==0?"empty":"running")}-{scale}.png"));
        }
    }
}
