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
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class FocusCheckInNativeChecks
{
    const string Output="design/one-piece-v2/focus-check-in/implemented";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Buttons=["ValidBreakButton","DistractionButton","IdeaButton","TaskSwitchButton"];
    static readonly FocusPauseReason[] Reasons=[FocusPauseReason.ValidBreak,FocusPauseReason.Distraction,FocusPauseReason.IdeaBrainstorming,FocusPauseReason.TaskSwitch];
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static T Named<T>(Window window,string name) where T:Control=>window.FindControl<T>(name)??window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Desktop(string type,string method,params object[] args)=>typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static DispatcherTimer Timer(FocusInterruptionDialogWindow window)=>(DispatcherTimer)typeof(FocusInterruptionDialogWindow).GetField("_timer",Private)!.GetValue(window)!;
    static void Tick(FocusInterruptionDialogWindow window)=>typeof(FocusInterruptionDialogWindow).GetMethod("Timer_Tick",Private)!.Invoke(window,[null,EventArgs.Empty]);
    sealed class Fixture:IDisposable
    {
        public readonly AppSettings Settings;
        public Fixture(int scale=90,string theme=AppThemeCatalog.Nika)
        {
            Settings=new(){ThemeMode=theme,UiScalePercent=scale,TelegramEnabled=false,HabiticaEnabled=false};
            Desktop("ThemeResources","ApplyApplication",theme);Desktop("DesktopPageAppearance","Initialize",Settings);Desktop("DesktopTypography","Initialize",Settings.Typography);
        }
        public FocusInterruptionDialogWindow Open(bool taskSwitch=false,double seconds=192,int threshold=5,int timeout=12,Action<FocusPauseReason>? callback=null,
            string project="Programming",string? other="Learning",TimeSpan? otherDuration=null)
        {
            object?[] args=[project,TimeSpan.FromSeconds(seconds),threshold,timeout,callback,taskSwitch,other,otherDuration??TimeSpan.FromSeconds(160)];
            var ctor=typeof(FocusInterruptionDialogWindow).GetConstructors().Single(c=>c.GetParameters().Length>=8);
            if(ctor.GetParameters().Length==9)args=[..args,Settings];
            var window=(FocusInterruptionDialogWindow)ctor.Invoke(args);window.ShowActivated=false;window.Topmost=false;
            window.Show();ClockOverlayChecks.Pump(65);return window;
        }
        public void Dispose(){Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");}
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})
        {
            using var fixture=new Fixture(scale);
            foreach(bool taskSwitch in new[]{false,true})
            {
                var window=fixture.Open(taskSwitch);Timer(window).Stop();
                try
                {
                    Require(window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Missing approved Focus Check-in frame.");
                    Require(Math.Abs(window.ClientSize.Width-Math.Min(520*scale/100d,window.MaxWidth))<1.1&&Math.Abs(window.ClientSize.Height-Math.Min((taskSwitch?570:538)*scale/100d,window.MaxHeight))<1.1,"Incorrect native size.");
                    Bounds(window);Require(Named<TextBlock>(window,"CountdownText").Text==$"Auto-select {(taskSwitch?"Task switch":"Distraction")} in 12s","Countdown does not name the default.");
                    var labels=window.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible).ToArray();
                    Require(labels.Count(t=>t.Text=="AUTO")==1,"Expected one automatic-choice marker.");
                    Require(labels.Any(t=>t.Text=="Programming")&&labels.Any(t=>t.Text=="3m 12s"),"Project/duration summary missing.");
                    Require(labels.Any(t=>t.Text?.Contains("During pause: Learning") ==true)==taskSwitch,"Task-switch context mismatch.");
                    Capture(window,scale,taskSwitch?"task-switch":"normal");
                    var automatic=Named<Button>(window,taskSwitch?"TaskSwitchButton":"DistractionButton");
                    var presenter=automatic.GetVisualDescendants().OfType<ContentPresenter>().Single(c=>c.Name=="PART_ContentPresenter");
                    foreach(string state in new[]{":pointerover",":pressed"})
                    {
                        ((IPseudoClasses)automatic.Classes).Set(state,true);ClockOverlayChecks.Pump(20);
                        Require(presenter.Background is ISolidColorBrush brush&&brush.Color.A==255&&brush.Color.R<60&&brush.Color.G<100,"Automatic choice loses its dark background on "+state);
                        ((IPseudoClasses)automatic.Classes).Set(state,false);
                    }
                }
                finally{window.Close();}
            }
        }
        using(var fixture=new Fixture())
        {
            var window=fixture.Open(seconds:520);Timer(window).Stop();try{Bounds(window);Capture(window,90,"long-pause");}finally{window.Close();}
            fixture.Settings.Typography.Global.SizePercent=125;Desktop("DesktopTypography","Apply",fixture.Settings.Typography);
            window=fixture.Open(true,project:"برنامه‌نویسی و بررسی گزارش‌های هفتگی پروژه با نام طولانی",other:"Learning and reviewing a long technical reference project");Timer(window).Stop();
            try
            {
                Bounds(window);var scroll=Named<ScrollViewer>(window,"FocusBodyScroll");Require(scroll.Extent.Height>scroll.Viewport.Height,"Long-text fixture does not exercise scrolling.");
                scroll.Offset=new(0,10000);ClockOverlayChecks.Pump(30);Bounds(window);Capture(window,90,"large-text-rtl");
                fixture.Settings.UiScalePercent=70;Desktop("DesktopPageAppearance","Apply",fixture.Settings,true);ClockOverlayChecks.Pump(50);Bounds(window);
            }
            finally{window.Close();}
        }
    }
    static void Bounds(Window window)
    {
        var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var end=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),window)!.Value;
        Require(Math.Abs(end.X-window.ClientSize.Width)<1.5&&Math.Abs(end.Y-window.ClientSize.Height)<1.5,"Frame does not fit native window.");
        foreach(string name in new[]{"FocusHeading","CountdownText","FooterHintText"})
        {
            var text=Named<TextBlock>(window,name);Require(text.TextLayout.Height<=text.Bounds.Height+1,name+" clips vertically.");
            var corner=text.TranslatePoint(new Point(text.Bounds.Width,text.Bounds.Height),frame)!.Value;
            Require(corner.X<frame.Bounds.Width-10&&corner.Y<frame.Bounds.Height-15,name+" exceeds frame.");
        }
        var scroll=Named<ScrollViewer>(window,"FocusBodyScroll");Require(scroll.Viewport.Height>=100,"Choice viewport collapsed.");
        var countdown=Named<TextBlock>(window,"CountdownText");Require(scroll.TranslatePoint(new Point(0,scroll.Bounds.Height),frame)!.Value.Y<countdown.TranslatePoint(default,frame)!.Value.Y,"Choices overlap footer.");
        foreach(var name in Buttons)Require(Named<Button>(window,name).IsEffectivelyVisible,"Choice is unavailable.");
    }
    static void Capture(Window window,int percent,string state)
    {
        var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
        if(percent>=100)bitmap.Render(window);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,$"focus-check-in-{state}-{percent}.png"));
    }
    internal static void SmallScreen()
    {
        using var fixture=new Fixture(125);var window=fixture.Open(true);Timer(window).Stop();
        try
        {
            var screen=new Screen(1.25,new PixelRect(0,0,1366,768),new PixelRect(0,32,1366,728),true);
            typeof(FocusInterruptionDialogWindow).GetMethod("FitFocusToScreen",Private)?.Invoke(window,[screen]);ClockOverlayChecks.Pump(70);
            var area=screen.WorkingArea;
            Require(window.ClientSize.Height*screen.Scaling<=area.Height+1&&window.ClientSize.Width*screen.Scaling<=area.Width+1,"Focus popup exceeds a small screen's working area.");
            Require(window.Position.X>=area.X&&window.Position.Y>=area.Y&&window.Position.X+window.ClientSize.Width*screen.Scaling<=area.Right+1&&window.Position.Y+window.ClientSize.Height*screen.Scaling<=area.Bottom+1,$"Focus popup is outside the monitor: position {window.Position}, client {window.ClientSize}, area {area}, scale {screen.Scaling}.");
            Bounds(window);var scroll=Named<ScrollViewer>(window,"FocusBodyScroll");Require(scroll.Extent.Height>scroll.Viewport.Height,"Small screen did not scroll the body.");
            scroll.Offset=new(0,10000);ClockOverlayChecks.Pump(20);Capture(window,125,"small-screen");
        }
        finally{window.Close();}
    }
    internal static void Behavior()
    {
        using var fixture=new Fixture();
        void Check(Action<FocusInterruptionDialogWindow> action,FocusPauseReason expected,bool taskSwitch=false,double seconds=192,int threshold=5,int timeout=12)
        {
            int calls=0;FocusPauseReason? result=null;
            var window=fixture.Open(taskSwitch,seconds,threshold,timeout,r=>{calls++;result=r;});
            try
            {
                Require(Timer(window).IsEnabled,"Countdown did not start.");action(window);ClockOverlayChecks.Pump(15);
                Require(!window.IsVisible&&calls==1&&result==expected&&window.SelectedReason==expected,"Wrong result, duplicate callback or window still open.");
                Require(!Timer(window).IsEnabled,"Countdown survived closing.");window.Close();Tick(window);Require(calls==1,"Closed popup repeated its callback.");
            }
            finally{window.Close();}
        }
        for(int i=0;i<Buttons.Length;i++){int index=i;Check(w=>Named<Button>(w,Buttons[index]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),Reasons[i]);}
        foreach(var (key,reason) in new[]{(Avalonia.Input.Key.D1,Reasons[0]),(Avalonia.Input.Key.NumPad1,Reasons[0]),(Avalonia.Input.Key.D2,Reasons[1]),(Avalonia.Input.Key.NumPad2,Reasons[1]),
            (Avalonia.Input.Key.D3,Reasons[2]),(Avalonia.Input.Key.NumPad3,Reasons[2]),(Avalonia.Input.Key.D4,Reasons[3]),(Avalonia.Input.Key.NumPad4,Reasons[3]),(Avalonia.Input.Key.S,Reasons[3]),
            (Avalonia.Input.Key.Enter,Reasons[1]),(Avalonia.Input.Key.Space,Reasons[1]),(Avalonia.Input.Key.Escape,FocusPauseReason.Unspecified)})
            Check(w=>w.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key}),reason);
        foreach(var key in new[]{Avalonia.Input.Key.Enter,Avalonia.Input.Key.Space})Check(w=>{var button=Named<Button>(w,"IdeaButton");button.Focus();button.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});},Reasons[1]);
        Check(w=>w.Close(),FocusPauseReason.Unspecified);
        Check(w=>Named<Button>(w,"FocusCloseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),FocusPauseReason.Unspecified);
        foreach(var (seconds,threshold,taskSwitch,expected) in new[]{(299d,5,false,Reasons[1]),(300d,5,false,Reasons[0]),(520d,5,false,Reasons[0]),(192d,2,false,Reasons[0]),(192d,5,true,Reasons[3])})
            Check(Tick,expected,taskSwitch,seconds,threshold,1);
        var countdown=fixture.Open(timeout:7);
        try{Tick(countdown);Require(Named<TextBlock>(countdown,"CountdownText").Text=="Auto-select Distraction in 6s","Live countdown did not update the default label.");}finally{countdown.Close();}
        var missing=fixture.Open(true,other:null);
        try{Require(Named<TextBlock>(missing,"CountdownText").Text!.Contains("Task switch"),"Missing project detail changed switch default.");}finally{missing.Close();}
        foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
        {
            fixture.Settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);var classic=fixture.Open();
            try{Require(!classic.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika appearance leaked into another theme.");Named<Button>(classic,"DistractionButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(classic.SelectedReason==Reasons[1],"Classic action regressed.");}finally{classic.Close();}
        }
    }
}
