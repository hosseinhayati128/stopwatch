using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static partial class StopwatchAppearanceChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int WindowStyle(IntPtr handle,int index);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr handle,out uint affinity);

    internal static void ControllerAndResources()
    {
        string settingsPath=SettingsStore.SettingsPath;
        byte[]? settingsBefore=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        var settings=new AppSettings{OverlayTheme=OverlayThemeCatalog.Nika,TextColor="Theme default"};
        var constructor=typeof(ControllerWindow).GetConstructor(Hidden,null,new[]{typeof(AppSettings)},null)!;
        var controller=(ControllerWindow)constructor.Invoke(new object[]{settings});
        var manager=(TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager",Hidden)!.GetValue(controller)!;
        var overlays=(Dictionary<Guid,OverlayWindow>)typeof(ControllerWindow).GetField("_overlays",Hidden)!.GetValue(controller)!;
        OverlayWindow? herd=null;
        void Invoke(string name,params object[] args)=>typeof(ControllerWindow).GetMethod(name,Hidden)!.Invoke(controller,args);
        try
        {
            var active=manager.Active!;var other=manager.Sessions[0];
            manager.Separate(other);Invoke("UpdateOverlayStates");
            herd=(OverlayWindow)typeof(ControllerWindow).GetField("_herdOverlay",Hidden)!.GetValue(controller)!;
            var separate=overlays[other.Id];
            foreach(int mode in new[]{0,1,2,3})
            foreach(bool selected in new[]{false,true})
            foreach(bool running in new[]{false,true})
            {
                manager.Activate(selected?other:active);other.Mode=mode;other.IsRunning=running;
                Invoke("UpdateOverlayStates");ClockOverlayChecks.Pump(15);
                Require(separate.FindControl<Button>("PauseResumeActionButton")!.IsEnabled==(mode!=1),"Clock pause state changed.");
                int alpha=MaxAlpha(Render(separate,null));
                Require(selected?alpha==255:alpha is >=100 and <=103,"Separate selection opacity depends on mode or run state.");
                int herdAlpha=MaxAlpha(Render(herd,null));
                Require(selected?herdAlpha is >=100 and <=103:herdAlpha==255,"Herd selection opacity is inconsistent.");
            }
            other.IsRunning=false;manager.Activate(active);Invoke("UpdateOverlayStates");
            var before=manager.Sessions.Select(s=>(s.Id,s.Elapsed,s.Mode,s.IsRunning)).ToArray();
            settings.StopwatchAppearance.Frame="royal-cartouche";settings.StopwatchAppearance.Background="midnight-chart";
            Invoke("ApplyOverlayStyle",herd);Invoke("ApplyOverlayStyle",separate);Invoke("UpdateOverlayStates");
            Require(manager.Active==active&&before.SequenceEqual(manager.Sessions.Select(s=>(s.Id,s.Elapsed,s.Mode,s.IsRunning))),"Appearance edits changed timer identity or elapsed time.");
            Require(herd.FindControl<ThemedClockFace>("ClockFace")!.HasStopwatchAppearance&&separate.FindControl<ThemedClockFace>("ClockFace")!.HasStopwatchAppearance,"Shared style did not reach both overlays.");
            Invoke("ToggleOverlayVisibility");Require(!herd.IsVisible&&!separate.IsVisible,"Hide missed an overlay.");
            Invoke("ToggleOverlayVisibility");Require(herd.IsVisible&&separate.IsVisible,"Show missed an overlay.");

            // Probe only timer updates and painting, with the same cached resource objects.
            var cache=StopwatchAppearanceAssets.Shared;
            var assets=new[]{cache.Get("background","midnight-chart"),cache.Get("frame","royal-cartouche"),cache.Get("control","filigree-keys")};
            var face=separate.FindControl<ThemedClockFace>("ClockFace")!;
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int tick=0;tick<120;tick++){separate.UpdateTime($"00:44:{tick%60:00}.{tick%10}");Render(face,null);}
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Require(ReferenceEquals(assets[0],cache.Get("background","midnight-chart"))&&ReferenceEquals(assets[1],cache.Get("frame","royal-cartouche"))&&ReferenceEquals(assets[2],cache.Get("control","filigree-keys")),"Timer updates replaced cached assets.");
            Console.WriteLine($"120 update/render iterations reused bitmap definitions; current-thread allocations including readback: {allocated:N0} bytes.");
            if(OperatingSystem.IsWindows())
            {
                IntPtr handle=separate.TryGetPlatformHandle()!.Handle;
                Require((WindowStyle(handle,-20)&0x08000008)==0x08000008,"Nika lost native TOPMOST/NOACTIVATE flags.");
                separate.SetClickThrough(true);Require((WindowStyle(handle,-20)&0x20)!=0,"Native click-through was not set.");
                separate.SetClickThrough(false);Require((WindowStyle(handle,-20)&0x20)==0,"Native click-through was not cleared.");
                separate.SetHideFromCapture(true);Require(GetWindowDisplayAffinity(handle,out uint affinity)&&affinity==0x11,"Capture exclusion was not set.");
                separate.SetHideFromCapture(false);Require(GetWindowDisplayAffinity(handle,out affinity)&&affinity==0,"Capture exclusion was not cleared.");
            }
        }
        finally
        {
            foreach(var overlay in overlays.Values)overlay.Close();herd?.Close();controller.Close();
            Require(settingsBefore==null?!File.Exists(settingsPath):File.ReadAllBytes(settingsPath).SequenceEqual(settingsBefore),"Behavior fixture changed real settings.");
        }
        using var pointer=new Avalonia.Input.Pointer(950,PointerType.Mouse,true);
        var window=new OverlayWindow{ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.ApplyAppearance(settings);window.Show();ClockOverlayChecks.Pump(40);
            int close=0,reset=0,activate=0,toggle=0;
            window.CloseRequested+=()=>close++;window.ResetRequested+=()=>reset++;window.ActivationRequested+=()=>activate++;window.ClockToggleRequested+=()=>toggle++;
            window.FindControl<Button>("ResetActionButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.FindControl<Button>("CloseActionButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var right=new PointerPressedEventArgs(window,pointer,window,new Point(20,30),0,new PointerPointProperties(RawInputModifiers.RightMouseButton,PointerUpdateKind.RightButtonPressed),KeyModifiers.None,1);
            typeof(OverlayWindow).GetMethod("OnTimerSurfacePointerPressed",Hidden)!.Invoke(window,new object[]{window,right});
            Require(close==1&&reset==1&&activate==3&&toggle==1&&right.Handled,"Native action/right-click dispatch changed.");
        }
        finally{window.Close();}
    }
}
