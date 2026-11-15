using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ClockModeLayoutChecks
{
    private static readonly string Output=Path.Combine(Environment.CurrentDirectory,"design/one-piece-v2/clock-mode-centered");
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static byte[] Capture(ThemedClockFace face,string? file=null)
    {
        using var image=SharedFrameChecks.Capture(face.Bounds.Size,face.Render);
        if(file!=null){Directory.CreateDirectory(Output);image.Save(Path.Combine(Output,file+".png"));}
        return SharedFrameChecks.Pixels(image);
    }

    internal static void Modes()
    {
        foreach(string theme in OverlayThemeCatalog.All)
        {
            var window=new OverlayWindow();
            try
            {
                window.ApplyAppearance(new AppSettings{OverlayTheme=theme,TextSize=48,TextColor="Theme default"});
                window.UpdateTime("14:35:28");window.SetTimerName("Programming");window.SetActive(true);
                window.Show();ClockOverlayChecks.Pump(40);
                var face=window.FindControl<ThemedClockFace>("ClockFace")!;
                var timer=Capture(face,theme==OverlayThemeCatalog.Nika?"stopwatch":null);
                window.SetPauseResumeEnabled(false);ClockOverlayChecks.Pump(40);
                var clock=Capture(face,theme==OverlayThemeCatalog.Nika?"clock":null);
                window.SetTimerName("A different project name");ClockOverlayChecks.Pump(20);
                Require(clock.SequenceEqual(Capture(face)),theme+": clock still paints the project name.");
                window.SetTimerName("Programming");window.SetPauseResumeEnabled(true);ClockOverlayChecks.Pump(40);
                Require(timer.SequenceEqual(Capture(face)),theme+": returning to stopwatch changed its original layout.");
            }
            finally{window.Close();}
        }
    }

    internal static void Centering()
    {
        foreach(double scale in new[]{.7,.9,1.25})
        foreach(var type in StopwatchAppearanceCatalog.Typography)
        foreach(var frame in StopwatchAppearanceCatalog.Frames)
        {
            var face=new ThemedClockFace{Text="08:38:58",ProjectName="Hidden project"};
            face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,TextSize=48*scale,
                StopwatchAppearance=new(){Typography=type.Id,Frame=frame.Id}});
            face.SetStopwatchPresentationState(new(false,false,true));
            face.Measure(Size.Infinity);face.Arrange(new Rect(face.DesiredSize));
            var clock=Capture(face);int width=(int)Math.Ceiling(face.Bounds.Width);
            face.Text="";var empty=Capture(face);
            var changed=Enumerable.Range(0,clock.Length/4).Where(p=>Enumerable.Range(0,4).Any(c=>Math.Abs(clock[p*4+c]-empty[p*4+c])>8)).ToArray();
            Require(changed.Length>0,"Clock digits disappeared.");
            double centerX=(changed.Min(p=>p%width)+changed.Max(p=>p%width)+1)/2d;
            double centerY=(changed.Min(p=>p/width)+changed.Max(p=>p/width)+1)/2d;
            Require(Math.Abs(centerX-face.Bounds.Width/2)<2.5,$"{type.Id} {scale}: horizontal center {centerX}.");
            Require(Math.Abs(centerY-(face.Bounds.Height+12*scale)/2)<2.5,$"{type.Id} {frame.Id} {scale}: vertical center {centerY}, expected {(face.Bounds.Height+12*scale)/2}.");
        }
    }

    internal static void DefaultFormat()
    {
        Require(new AppSettings().TimeFormat==1,"New settings do not default to HH:MM:SS.");
        Require(JsonSerializer.Deserialize<AppSettings>("{}")!.TimeFormat==1,"Missing setting did not use HH:MM:SS.");
        for(int format=0;format<=4;format++)
            Require(JsonSerializer.Deserialize<AppSettings>($"{{\"TimeFormat\":{format}}}")!.TimeFormat==format,"Saved time format changed.");
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(hidden,null,[typeof(AppSettings)],null)!.Invoke([new AppSettings()]);
        try
        {
            var now=new DateTime(2026,11,15,10,22,33,DateTimeKind.Utc);
            var method=typeof(ControllerWindow).GetMethod("FormatDisplayTime",hidden,null,[typeof(TimerSession),typeof(DateTime)],null)!;
            Require((string)method.Invoke(controller,[new TimerSession(1){Mode=1},now])! == now.ToLocalTime().ToString("HH:mm:ss"),"Clock formatter did not use HH:MM:SS.");
        }
        finally{controller.Close();}
    }
}
