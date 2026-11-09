using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class PickerStabilityChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string Output=Path.GetFullPath(".superpowers/sdd/2026-11-05-picker-stability-and-memory");
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Call(ControllerWindow window,string method,params object[] arguments)=>
        typeof(ControllerWindow).GetMethod(method,Hidden)!.Invoke(window,arguments);
    private static T Field<T>(ControllerWindow window,string name)=>(T)typeof(ControllerWindow).GetField(name,Hidden)!.GetValue(window)!;
    private static ControllerWindow Controller()=> (ControllerWindow)Activator.CreateInstance(typeof(ControllerWindow),Hidden,null,
        new object[]{new AppSettings{ThemeMode=AppThemeCatalog.Nika,TimeFormat=1,ActivityWatchEnabled=false}},null)!;

    // Catches retaining a terminal Window in either overlay cache after Alt+F4/OS close.
    internal static void ReopenClosedOverlays()
    {
        using var fixture=new ControllerFixture();var controller=fixture.Controller;
        var timers=Field<TimerSessionManager>(controller,"_timerManager");
        Call(controller,"UpdateOverlayStates");ClockOverlayChecks.Pump(70);
        var original=Field<OverlayWindow>(controller,"_herdOverlay");
        var session=timers.Active!;var elapsed=session.Elapsed;int count=timers.Count;
        original.Close();
        Call(controller,"OnTimerTick",new object(),EventArgs.Empty);
        Require(!Field<OverlayWindow>(controller,"_herdOverlay").IsVisible,"A native-closed herd overlay reappeared on the next timer tick.");
        Call(controller,"ToggleActiveTimerOverlay");
        var replacement=Field<OverlayWindow>(controller,"_herdOverlay");
        Require(!ReferenceEquals(original,replacement)&&replacement.IsVisible,"The closed herd overlay was not recreated.");
        Require(ReferenceEquals(timers.Active,session)&&timers.Count==count&&session.Elapsed==elapsed,"Closing a native window changed its timer.");
        for(int i=0;i<3;i++)
        {
            replacement.Close();Call(controller,"ShowActiveTimerOverlay");
            replacement=Field<OverlayWindow>(controller,"_herdOverlay");
            Require(replacement.IsVisible,"Repeated herd close/show lost the visible overlay.");
        }
        Call(controller,"SeparateActiveSession");
        var separated=Field<Dictionary<Guid,OverlayWindow>>(controller,"_overlays");
        original=separated[session.Id];original.Close();
        Call(controller,"OnTimerTick",new object(),EventArgs.Empty);
        Require(!separated[session.Id].IsVisible,"A native-closed separated overlay reappeared on the next timer tick.");
        Call(controller,"ShowActiveTimerOverlay");
        Require(separated.TryGetValue(session.Id,out replacement)&&!ReferenceEquals(original,replacement)&&replacement.IsVisible,
            "The closed separated overlay was not recreated.");
        Call(controller,"MergeActiveSession");
        Require(!separated.ContainsKey(session.Id)&&!session.IsSeparated,"Closing callbacks broke merge cleanup.");
        Require(Field<OverlayWindow>(controller,"_herdOverlay").IsVisible,"Internal merge closure incorrectly hid the herd timer.");
        Call(controller,"SeparateActiveSession");
        timers.Activate(timers.Sessions.First(timer=>timer!=session));Call(controller,"SeparateActiveSession");
        Require(separated.Count==2,"The shutdown fixture requires two separated windows.");
        controller.Close();
        Require(separated.Count==0,"Controller shutdown retained separated windows.");
    }
    private sealed class ControllerFixture:IDisposable
    {
        internal readonly ControllerWindow Controller=PickerStabilityChecks.Controller();
        public void Dispose()=>Controller.Close();
    }

    // Measures the decoded working set, rather than inferring memory from compressed PNG sizes.
    internal static void ColdArtwork(string label,bool assertLazy)
    {
        Directory.CreateDirectory(Output);
        using var process=Process.GetCurrentProcess();
        GC.Collect();GC.WaitForPendingFinalizers();process.Refresh();
        long before=process.PrivateMemorySize64;var watch=Stopwatch.StartNew();
        var assets=new StopwatchAppearanceAssets();
        var frame=assets.Get("frame","engraved-filigree");watch.Stop();process.Refresh();
        var decoded=(Dictionary<string,Bitmap>)typeof(StopwatchAppearanceAssets).GetField("bitmaps",Hidden)!.GetValue(assets)!;
        long pixels=decoded.Values.Sum(bitmap=>(long)bitmap.PixelSize.Width*bitmap.PixelSize.Height*4);
        var result=new{label,firstGetMilliseconds=watch.Elapsed.TotalMilliseconds,decodedImages=decoded.Count,
            decodedRgbaBytes=pixels,privateBytesBefore=before,privateBytesAfter=process.PrivateMemorySize64};
        Console.WriteLine(JsonSerializer.Serialize(result));
        File.WriteAllText(Path.Combine(Output,label+"-artwork.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Require(ReferenceEquals(frame,assets.Get("frame","engraved-filigree")),"Repeated asset reads lost shared identity.");
        if(assertLazy)
        {
            Require(decoded.Count==1,"Requesting one frame decoded "+decoded.Count+" unrelated images.");
            var pixelsBefore=pixels;
            assets.Get("background","navigator-parchment");
            Require(decoded.Count==2&&pixelsBefore>0,"A second selected image was not loaded on demand.");
        }
    }

    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window,int index);
    internal static void StandalonePicker()
    {
        var type=typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.TimerPickerWindow")!;
        var timers=new[]{new TimerSession(1){Name="Navigation"}};
        var picker=(Window)Activator.CreateInstance(type,Hidden,null,new object[]{timers,timers[0],new AppSettings{ThemeMode=AppThemeCatalog.Nika}},null)!;
        try
        {
            picker.ShowActivated=false;picker.Show();ClockOverlayChecks.Pump(100);
            Require(picker.SystemDecorations==SystemDecorations.None,"The Navigator picker still has Windows decorations.");
            Require(picker.Background is ISolidColorBrush brush&&brush.Color.A==0,"The picker paints a rectangular native background.");
            Require(picker.ActualTransparencyLevel==WindowTransparencyLevel.Transparent,"Native transparent composition was not achieved.");
            if(OperatingSystem.IsWindows())
            {
                int style=GetWindowLong(picker.TryGetPlatformHandle()!.Handle,-16);
                Require((style&0x00C00000)==0,"The native HWND still has caption/border styles: "+style.ToString("X"));
            }
            // The screenshot must include the native client root, including transparent outer corners.
            using var bitmap=new RenderTargetBitmap(new PixelSize(460,480),new Vector(96,96));
            bitmap.Render(picker);
            var pixels=SharedFrameChecks.Pixels(bitmap);
            Require(pixels[3]==0&&pixels[(460-1)*4+3]==0,"The frame's outer corners are not transparent.");
            Directory.CreateDirectory(Output);bitmap.Save(Path.Combine(Output,"standalone-picker.png"));
        }
        finally{picker.Close();}
    }
}
