using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerSizingChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Output="design/one-piece-v2/settings/controller-sizing";
    static readonly Type Appearance=typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!;
    static void Page(string method,params object[] args)=>Appearance.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var failures=new List<string>();
        var evidence=new List<object>();
        foreach(double percent in new[]{70d,90d,125d})
        {
            var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=percent};
            typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[settings.ThemeMode]);
            Page("Initialize",settings);
            var window=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
            window.ShowActivated=false;window.ShowInTaskbar=false;
            var initialSize=new Size(window.Width,window.Height);
            try
            {
                window.Show();ClockOverlayChecks.Pump(150);
                Inspect("launch-"+percent,true);
                var area=window.Screens.ScreenFromWindow(window)!.WorkingArea;
                var expected=new Size(Math.Min(initialSize.Width*percent/100,area.Width/window.RenderScaling),Math.Min(initialSize.Height*percent/100,area.Height/window.RenderScaling));
                if(Math.Abs(window.ClientSize.Width-expected.Width)>1||Math.Abs(window.ClientSize.Height-expected.Height)>1)
                    failures.Add($"{percent}% launch changed the requested panel size: expected {expected}, actual {window.ClientSize}.");
                var size=window.ClientSize;
                Page("Apply",settings,true);window.Hide();window.Show();ClockOverlayChecks.Pump(80);
                if(Math.Abs(window.ClientSize.Width-size.Width)>1||Math.Abs(window.ClientSize.Height-size.Height)>1)failures.Add(percent+"% repeated apply/show changed window size.");
                window.Width=900;window.Height=600;ClockOverlayChecks.Pump(80);Inspect("resized-"+percent,false);
                window.WindowState=WindowState.Maximized;ClockOverlayChecks.Pump(120);Inspect("maximized-"+percent,false);
                window.WindowState=WindowState.Normal;ClockOverlayChecks.Pump(80);Inspect("restored-"+percent,false);
                // Exercise the same application path used after releasing the Appearance slider.
                foreach(double next in new[]{125d,70d,90d})
                {
                    settings.UiScalePercent=next;Page("Apply",settings,true);ClockOverlayChecks.Pump(80);
                    Inspect("changed-"+percent+"-to-"+next,false);
                }
                foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Pirate,AppThemeCatalog.Nika})
                {
                    settings.ThemeMode=theme;
                    typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
                    typeof(ControllerWindow).GetMethod("ApplyControllerTheme",Hidden)!.Invoke(window,null);
                    ClockOverlayChecks.Pump(100);Inspect("theme-"+percent+"-"+theme,false);
                }
            }
            finally{window.Close();Page("Shutdown");}

            void Inspect(string state,bool capture)
            {
                var frame=window.FindControl<Grid>("ControllerFrame")!;
                var start=frame.TranslatePoint(default,window)!.Value;
                var end=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),window)!.Value;
                var host=window.FindControl<Viewbox>("ControllerLayoutHost")!;
                evidence.Add(new{state,scale=settings.UiScalePercent,dpi=window.RenderScaling,client=window.ClientSize.ToString(),nativeFrame=window.FrameSize.ToString(),hostSize=$"{host.Width}, {host.Height}",hostBounds=host.Bounds.ToString(),hostDesired=host.DesiredSize.ToString(),transformBounds=((Control)window.Content!).Bounds.ToString(),start=start.ToString(),end=end.ToString()});
                if(capture)Capture(window,"controller-"+percent);
                double scale=settings.UiScalePercent/100;
                if(Math.Abs(start.X-frame.Margin.Left*scale)>1||Math.Abs(start.Y-frame.Margin.Top*scale)>1||Math.Abs(end.X+frame.Margin.Right*scale-window.ClientSize.Width)>1||Math.Abs(end.Y+frame.Margin.Bottom*scale-window.ClientSize.Height)>1)
                    failures.Add($"{state}: panel {start} to {end} does not fill window {window.ClientSize}.");
                foreach(var control in new Control[]{window.FindControl<Button>("StartStopButton")!,window.GetVisualDescendants().OfType<StackPanel>().First(c=>c.Classes.Contains("nikaWindowControls"))}.Where(c=>c.IsEffectivelyVisible))
                {
                    var top=control.TranslatePoint(default,window)!.Value;
                    var bottom=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),window)!.Value;
                    if(top.X < -1||top.Y < -1||bottom.X>window.ClientSize.Width+1||bottom.Y>window.ClientSize.Height+1)
                        failures.Add(state+": controller actions are outside the client area.");
                }
            }
        }
        File.WriteAllText(Path.Combine(Output,"bounds.json"),System.Text.Json.JsonSerializer.Serialize(evidence,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        if(failures.Count!=0)throw new Exception(string.Join(Environment.NewLine,failures));
        Console.WriteLine($"Controller panel fills native window in {evidence.Count} launch/resize/maximize/restore/scale states at 70/90/125%; captures: {Output}.");
    }
    static void Capture(ControllerWindow window,string name)
    {
        var frame=window.FindControl<Grid>("ControllerFrame")!;
        var start=frame.TranslatePoint(default,window)!.Value;
        var end=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),window)!.Value;
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
        using(var context=bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(window.Background!,null,new Rect(window.ClientSize));
            // Capture the arranged frame at its native bounds, avoiding Avalonia's nested Viewbox capture culling.
            context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(start,end));
        }
        bitmap.Save(Path.Combine(Output,name+".png"));
    }
}
