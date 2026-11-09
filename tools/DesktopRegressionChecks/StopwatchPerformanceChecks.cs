using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class StopwatchPerformanceChecks
{
    private static readonly string Output=Path.GetFullPath(".superpowers/sdd/2026-11-05-stopwatch-redraw-cost");
    private static AppSettings Settings()=>new()
    {
        ThemeMode=AppThemeCatalog.Nika,TextSize=27,TextColor="Theme default",TimeFormat=1,
        StopwatchAppearance=new(){Frame="vine-filigree",Background="midnight-chart",Typography="instrument-digital",
            Artwork="edge-engraving",BackgroundOpacity=79,UnselectedOpacity=38,OpaqueLayers=(StopwatchOpaqueLayers)23}
    };
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Arrange(ThemedClockFace face)
    {face.Measure(Size.Infinity);face.Arrange(new Rect(face.DesiredSize));}
    internal static void UnchangedProperties()
    {
        var face=new ThemedClockFace();face.ApplySettings(Settings());
        face.Text="00:44:27";face.ProjectName="Navigation";face.ShowRec=true;Arrange(face);
        face.Text="00:44:27";
        Require(face.IsMeasureValid,"Repeating identical timer text invalidated layout.");
        face.ProjectName="  Navigation  ";
        Require(face.IsMeasureValid,"Repeating the normalized name invalidated layout.");
        face.ShowRec=true;
        Require(face.IsMeasureValid,"Repeating REC state invalidated layout.");
        face.ProjectName="New name";Require(!face.IsMeasureValid,"A changed name did not request layout.");
        Arrange(face);face.ShowRec=false;Require(!face.IsMeasureValid,"A changed REC setting did not request layout.");
    }
    internal static void StableDigitLayout()
    {
        var face=new ThemedClockFace();face.ApplySettings(Settings());face.Text="00:44:27";Arrange(face);
        var original=face.Bounds.Size;
        using var before=SharedFrameChecks.Capture(original,face.Render);var pixels=SharedFrameChecks.Pixels(before);
        face.Text="88:44:28";
        Require(face.IsMeasureValid,"Changing fixed-width Nika digits requested another layout pass.");
        using var after=SharedFrameChecks.Capture(original,face.Render);
        Require(!pixels.SequenceEqual(SharedFrameChecks.Pixels(after)),"Changing digits did not update rendered pixels.");
        face.Text="1234:44:28";Require(!face.IsMeasureValid,"Long-hour growth did not request more width.");
        Arrange(face);Require(face.DesiredSize.Width>original.Width,"Long hours were clipped to the old width.");
        face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Midnight,TextSize=27});Arrange(face);
        face.Text="12345:44:28";Require(!face.IsMeasureValid,"Legacy typography lost its required measurement.");
    }
    internal static void RepeatedPresentation()
    {
        var window=new OverlayWindow{ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.ApplyAppearance(Settings());window.SetActive(false);window.SetRunning(false);
            window.SetPauseResumeEnabled(true);window.SetInactiveSeparated(true,.38);window.Show();ClockOverlayChecks.Pump(100);
            var face=window.FindControl<ThemedClockFace>("ClockFace")!;
            var surface=window.FindControl<Border>("ActionSurface")!;
            var faceMask=face.OpacityMask;var toolbarMask=surface.OpacityMask;
            for(int i=0;i<10;i++)Apply(window,"00:44:27",false,false,.38);
            Require(ReferenceEquals(faceMask,face.OpacityMask),"Unchanged controller updates rebuilt the face opacity mask.");
            Require(ReferenceEquals(toolbarMask,surface.OpacityMask),"Unchanged controller updates restyled the toolbar.");
            window.SetInactiveSeparated(true,.5);
            Require(!ReferenceEquals(faceMask,face.OpacityMask),"Changed inactive opacity did not update the mask.");
            var settings=Settings();settings.StopwatchAppearance.HoverControls="pixel-controls";
            window.ApplyAppearance(settings);window.SetRunning(true);window.SetPauseResumeEnabled(false);
            Require(!window.FindControl<Button>("PauseResumeActionButton")!.IsEnabled,"Clock-mode controls did not refresh.");
            window.SetPauseResumeEnabled(true);
            Require(window.FindControl<Button>("PauseResumeActionButton")!.IsEnabled,"Leaving clock mode did not restore controls.");
        }
        finally{window.Close();}
    }
    internal static void DirectSettingsPreserveOpacity()
    {
        var window=new OverlayWindow{ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.ApplyAppearance(Settings());window.SetInactiveSeparated(true,.38);window.Show();ClockOverlayChecks.Pump(100);
            var toolbar=window.FindControl<Border>("ActionSurface")!;
            double before=toolbar.OpacityMask?.Opacity??1;
            window.ApplySettings(Colors.White,Colors.Black,27,2,"Consolas",.79,true);
            Require(Math.Abs((toolbar.OpacityMask?.Opacity??1)-before)<.0001,
                "Direct appearance settings reset the inactive toolbar's composite opacity.");
        }
        finally{window.Close();}
    }
    internal static void VisualParity()
    {
        foreach(string scenario in new[]{"paused","seconds","tenths"})
        {
            using var baseline=new Bitmap(Path.Combine(Output,$"baseline-{scenario}.png"));
            using var final=new Bitmap(Path.Combine(Output,$"final-{scenario}.png"));
            Require(baseline.PixelSize==final.PixelSize,"Performance repair changed overlay dimensions.");
            var before=SharedFrameChecks.Pixels(baseline);var after=SharedFrameChecks.Pixels(final);
            int maximum=0,changed=0;
            for(int i=0;i<before.Length;i++)
            {int delta=Math.Abs(before[i]-after[i]);maximum=Math.Max(maximum,delta);if(delta>0)changed++;}
            Console.WriteLine($"{scenario} parity: {changed} channels differ, maximum delta {maximum}/255.");
            Require(maximum<=2,"Performance repair changed the rendered appearance beyond subpixel rounding.");
        }
    }
    private static void Apply(OverlayWindow window,string text,bool running,bool active=true,double opacity=1)
    {
        window.SetTimerName("Navigation");window.UpdateTime(text);window.SetRunning(running);
        window.SetPauseResumeEnabled(true);window.SetActive(active);window.SetInactiveSeparated(!active,opacity);window.SetSeparateMergeState(false);
    }
    internal static void Benchmark(string label,string? output=null,AppSettings? appearance=null)
    {
        string directory=output??Output;var settings=appearance??Settings();
        Directory.CreateDirectory(directory);
        var results=new List<object>();
        using var process=Process.GetCurrentProcess();
        foreach(string scenario in new[]{"paused","seconds","tenths"})
        {
            var window=new OverlayWindow{ShowActivated=false,ShowInTaskbar=false};
            try
            {
                window.ApplyAppearance(settings);Apply(window,"00:44:27",scenario!="paused");window.Show();ClockOverlayChecks.Pump(250);
                void Run(int updates,HashSet<string>? texts=null)
                {
                    using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(20));int count=0;
                    var timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
                    timer.Tick+=(_,_)=>
                    {
                        int tick=count++;int milliseconds=scenario=="paused"?0:tick*33;
                        var time=TimeSpan.FromSeconds(2667)+TimeSpan.FromMilliseconds(milliseconds);
                        string text=$"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"+(scenario=="tenths"?$".{time.Milliseconds/100}":"");
                        texts?.Add(text);Apply(window,text,scenario!="paused");
                        if(count==updates){timer.Stop();DispatcherTimer.RunOnce(cancel.Cancel,TimeSpan.FromMilliseconds(80));}
                    };
                    timer.Start();Dispatcher.UIThread.MainLoop(cancel.Token);timer.Stop();
                    Require(count==updates,"Benchmark did not execute its fixed update count.");
                }
                Run(45);GC.Collect();GC.WaitForPendingFinalizers();ClockOverlayChecks.Pump(100);
                var texts=new HashSet<string>();double cpu=process.TotalProcessorTime.TotalSeconds;
                long allocations=GC.GetTotalAllocatedBytes();var wall=Stopwatch.StartNew();
                Run(180,texts);wall.Stop();
                double seconds=process.TotalProcessorTime.TotalSeconds-cpu;
                var result=new{scenario,updates=180,distinctTimes=texts.Count,wallSeconds=wall.Elapsed.TotalSeconds,cpuSeconds=seconds,
                    totalCpuPercent=seconds/wall.Elapsed.TotalSeconds/Environment.ProcessorCount*100,
                    allocatedBytes=GC.GetTotalAllocatedBytes()-allocations,workingSetBytes=process.WorkingSet64};
                results.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
                var face=window.FindControl<ThemedClockFace>("ClockFace")!;
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(face.Bounds.Width),(int)Math.Ceiling(face.Bounds.Height)),new Vector(96,96));
                bitmap.Render(face);bitmap.Save(Path.Combine(directory,$"{label}-{scenario}.png"));
            }
            finally{window.Close();ClockOverlayChecks.Pump(80);}
        }
        File.WriteAllText(Path.Combine(directory,label+".json"),JsonSerializer.Serialize(new{label,logicalProcessors=Environment.ProcessorCount,
            backend="Win32 AngleEgl with software fallback, private desktop",profile=settings.StopwatchAppearance,textSize=settings.TextSize,
            note="Same simulated controller updates in each run; no exports/readback inside the timed interval. Does not measure the user's whole workload or prove the sole cause of machine lag.",results},new JsonSerializerOptions{WriteIndented=true}));
    }
}
