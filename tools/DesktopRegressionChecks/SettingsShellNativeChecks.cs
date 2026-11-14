using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsShellNativeChecks
{
    internal static string? OutputOverride { get; set; }
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static T Find<T>(Window window,string name) where T:Control => window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static SettingsWindow Open(AppSettings? settings=null)
    {
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
        var window=new SettingsWindow(settings??new AppSettings{ThemeMode=AppThemeCatalog.Nika}){ShowActivated=false,ShowInTaskbar=false};
        window.Show();ClockOverlayChecks.Pump(50);return window;
    }
    internal static void Layout()
    {
        var window=Open();
        try
        {
            Require(window.SystemDecorations==SystemDecorations.None&&window.Content is StopwatchFrameSurface,"Approved standalone Settings frame is missing.");
            Require(window.GetVisualDescendants().OfType<Button>().Count(b=>b.Name?.StartsWith("SettingsNav_")==true)==12,"Expected 12 retained Settings categories.");
            Require(!window.FindControl<ListBox>("NavigationList")!.Items.OfType<ListBoxItem>().Any(i=>Equals(i.Tag,"LightRing")),"Removed Light ring category remains in navigation.");
            Save(window,"overview");
            int changes=0;Action<SettingsChangeKind> changed=_=>changes++;
            typeof(SettingsWindow).GetEvent("SettingsChanged",Hidden)!.GetAddMethod(true)!.Invoke(window,[changed]);
            typeof(SettingsWindow).GetMethod("ReloadFromSettings",Hidden)!.Invoke(window,null);
            var category=typeof(SettingsWindow).GetProperty("CurrentCategory",Hidden)!;
            foreach(var button in window.GetVisualDescendants().OfType<Button>().Where(b=>b.Name?.StartsWith("SettingsNav_")==true).ToArray())
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(15);
                string key=button.Name![12..];Require((string)category.GetValue(window)! == key,"Navigation did not select "+key);
                if(key is not ("Overlay" or "Appearance" or "Typography" or "Behavior" or "Application" or "Obsidian"))Require(window.FindControl<Control>(key+"Panel")!.IsEffectivelyVisible,"Navigation hid "+key+" fields.");
                if(key=="Obsidian")Require(window.FindControl<TextBox>("ObsidianFolderTextBox")!.IsEffectivelyVisible,"Navigation hid Obsidian fields.");
                if(key=="Application")Require(Find<ScrollViewer>(window,"ApplicationScroll").IsEffectivelyVisible,"Navigation hid Application fields.");
                if(key=="Typography")Require(Find<Grid>(window,"TypographyPage").IsEffectivelyVisible,"Navigation hid Typography fields.");
                if(key=="Behavior")Require(Find<Grid>(window,"BehaviorPage").IsEffectivelyVisible,"Navigation hid Behavior fields.");
                if(key=="Appearance")Require(window.FindControl<ComboBox>("ThemeCombo")!.IsEffectivelyVisible,"Navigation hid Appearance fields.");
                if(key is "StopwatchAppearance" or "TodoAppearance")Save(window,key+"-retained");
            }
            Find<Button>(window,"SettingsNav_Overlay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Width=840;window.Height=600;ClockOverlayChecks.Pump(40);
            var scroll=Find<ScrollViewer>(window,"NavigatorSettingsScroll");var position=window.FindControl<ComboBox>("PositionCombo")!;
            Require(position.TranslatePoint(default,scroll)!.Value.Y+position.Bounds.Height<=scroll.Viewport.Height,"Compact placement field is below the initial viewport.");
            var done=Find<Button>(window,"SettingsDone");double y=done.TranslatePoint(default,window)!.Value.Y;
            Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Compact page has horizontal overflow.");Save(window,"compact");
            scroll.ScrollToEnd();ClockOverlayChecks.Pump(20);Require(done.TranslatePoint(default,window)!.Value.Y==y,"Settings footer scrolls away.");
            Save(window,"compact-controls");
            window.Width=1120;window.Height=720;ClockOverlayChecks.Pump(30);
            Require(window.FindControl<ComboBox>("ScreenCombo")!.IsEffectivelyVisible,"Resize detached placement controls.");
            Require(window.GetVisualDescendants().OfType<ThemedClockFace>().Any(),"Resize detached the clock preview.");
            Require(changes==0,"Loading, navigation or resizing committed settings.");
            var first=Find<Button>(window,"SettingsNav_Overlay");
            var next=KeyboardNavigationHandler.GetNext(first,NavigationDirection.Next);
            Require(next==Find<Button>(window,"SettingsNav_Appearance"),"Keyboard traversal skips settings categories.");
            Require(done.Focus(),"Done cannot receive keyboard focus.");
        }
        finally{window.Close();}
    }
    internal static void Controls()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika};var window=Open(settings);
        try
        {
            window.FindControl<CheckBox>("ClickThroughCheck")!.IsChecked=true;
            window.FindControl<ComboBox>("PositionCombo")!.SelectedItem="Bottom Right";
            Require(settings.ClickThrough&&settings.Position=="Bottom Right","First-layer controls do not apply to settings.");
            typeof(SettingsWindow).GetMethod("ReportPersistenceResult",Hidden)!.Invoke(window,[false]);
            Require(Find<TextBlock>(window,"SettingsSaveStatus").Text!.Contains("could not be saved"),"Save failure is absent from the fixed footer.");
            Save(window,"save-failure");
            Find<Button>(window,"SettingsNav_Application").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Find<Button>(window,"SettingsNav_Overlay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Find<TextBlock>(window,"SettingsSaveStatus").Text!.Contains("could not be saved"),"Navigation erased persistence failure.");
            typeof(SettingsWindow).GetMethod("ReportPersistenceResult",Hidden)!.Invoke(window,[true]);
            window.FindControl<CheckBox>("HideOverlayCaptureCheck")!.IsChecked=true;ClockOverlayChecks.Pump(25);Save(window,"interaction");
            window.FindControl<ComboBox>("PositionCombo")!.SelectedItem="Custom";ClockOverlayChecks.Pump(20);Save(window,"custom-position");
            foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Nika,AppThemeCatalog.Pirate,AppThemeCatalog.Nika})
            {
                typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,[theme]);
                window.FindControl<ComboBox>("ThemeCombo")!.SelectedItem=theme;
                for(int attempt=0;attempt<20&&(window.Content is StopwatchFrameSurface)!=(theme==AppThemeCatalog.Nika);attempt++)ClockOverlayChecks.Pump(25);
                Require((window.Content is StopwatchFrameSurface)==(theme==AppThemeCatalog.Nika),$"Theme {theme}, stored {settings.ThemeMode}, root {window.Content?.GetType().Name}: incorrect shell.");
                Require(window.FindControl<ComboBox>("PositionCombo")!.SelectedItem?.ToString()=="Custom","Theme switch lost settings values.");
                if(theme!=AppThemeCatalog.Nika)
                {
                    Require(window.FindControl<CheckBox>("ClickThroughCheck")!.Content is string,"Legacy theme retained Nika checkbox labels.");
                    Require(!window.FindControl<ComboBox>("ScreenCombo")!.Classes.Contains("editorField"),"Legacy theme retained Nika field styling.");
                    if(theme==AppThemeCatalog.Midnight)Save(window,"legacy-midnight");
                }
            }
        }
        finally{window.Close();}
    }
    internal static void OverlayActions()
    {
        var controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([new AppSettings{ThemeMode=AppThemeCatalog.Nika}]);
        SettingsWindow? settings=null;
        try
        {
            // The synthetic constructor does not load stores, start ticks or register hotkeys.
            typeof(ControllerWindow).GetMethod("SettingsButton_Click",Hidden)!.Invoke(controller,[null,new RoutedEventArgs()]);
            settings=(SettingsWindow)typeof(ControllerWindow).GetField("_settingsWindow",Hidden)!.GetValue(controller)!;
            ClockOverlayChecks.Pump(30);
            var button=Find<Button>(settings,"SettingsShowOverlay");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var overlay=(OverlayWindow?)typeof(ControllerWindow).GetField("_herdOverlay",Hidden)!.GetValue(controller);
            Require(overlay!=null&&!overlay.IsVisible,"Show/hide did not hide the active overlay.");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(overlay!.IsVisible,"Show/hide did not restore the active overlay.");
        }
        finally{settings?.Close();controller.Close();}
    }
    internal static void Placement()
    {
        var place=typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.Platform.OverlayScreenConstraint")!.GetMethod("PlaceOnScreen",BindingFlags.NonPublic|BindingFlags.Static);
        Require(place!=null,"Preset placement is not implemented.");
        var screen=new Screen(1.5,new PixelRect(-1920,0,1920,1080),new PixelRect(-1920,32,1920,1000),false);
        var size=new Size(300,100);
        PixelPoint Point(string preset,PixelPoint point=default)=>(PixelPoint)place!.Invoke(null,[preset,point,size,screen])!;
        Require(Point("Top Left")==new PixelPoint(-1902,50),"Top-left ignores work area or DPI.");
        Require(Point("Bottom Right")==new PixelPoint(-468,864),"Bottom-right ignores work area or DPI.");
        Require(Point("Top Center")==new PixelPoint(-1185,50),"Center placement is wrong.");
        Require(Point("Custom",new(-1500,100))==new PixelPoint(-1500,100),"Valid custom position moved.");
        Require(Point("Custom",new(100,2000))==new PixelPoint(-450,882),"Offscreen custom position is not clamped.");
        var oversized=(PixelPoint)place!.Invoke(null,["Bottom Right",default(PixelPoint),new Size(2000,2000),screen])!;
        Require(oversized==screen.WorkingArea.Position,"Oversized overlay escapes work area.");
    }

    internal static void Mirrors()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,ScreenIndex=0,Position="Top Right"};
        var controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
        try
        {
            var manager=(TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager",Hidden)!.GetValue(controller)!;
            var session=manager.Active!;var time=session.Elapsed;int count=manager.Count;
            var primary=controller.Screens.Primary!;
            // Both synthetic monitors use real private-desktop bounds so native clamping does not fight the fixture.
            var screens=new[]{new Screen(primary.Scaling,primary.Bounds,primary.WorkingArea,true),new Screen(primary.Scaling,primary.Bounds,primary.WorkingArea,false)};
            typeof(ControllerWindow).GetField("_overlayScreens",Hidden)!.SetValue(controller,screens);
            void Update()=>typeof(ControllerWindow).GetMethod("UpdateOverlayStates",Hidden)!.Invoke(controller,null);
            OverlayWindow[] Windows()
            {
                var map=(System.Collections.IDictionary)typeof(ControllerWindow).GetField("_displayOverlays",Hidden)!.GetValue(controller)!;
                return map.Values.Cast<System.Collections.IEnumerable>().SelectMany(v=>v.Cast<object>()).Select(d=>(OverlayWindow)d.GetType().GetField("Window",Hidden)!.GetValue(d)!).ToArray();
            }
            Update();ClockOverlayChecks.Pump(30);Update();
            var windows=Windows();Require(windows.Length==2&&windows.All(w=>w.IsVisible),"All displays did not mirror the same timer.");
            var position=windows[0].Position;for(int i=0;i<20;i++)Update();Require(Windows().SequenceEqual(windows)&&windows[0].Position==position,"Stable ticks recreate or move windows.");
            typeof(ControllerWindow).GetMethod("ToggleActiveTimerOverlay",Hidden)!.Invoke(controller,null);Require(windows.All(w=>!w.IsVisible),"Hide left a display copy visible.");
            typeof(ControllerWindow).GetMethod("ShowActiveTimerOverlay",Hidden)!.Invoke(controller,null);Require(windows.All(w=>w.IsVisible),"Show did not restore display copies.");
            var moved=new PixelPoint(primary.WorkingArea.X+100,primary.WorkingArea.Y+100);windows[0].Position=moved;
            ((Action)typeof(OverlayWindow).GetField("PositionChangedByUser",Hidden)!.GetValue(windows[0])!)();Update();
            Require(windows[0].Position==moved&&session.HasCustomPosition&&session.CustomPositionsByScreen.Count>0,"User placement was not retained.");
            settings.ScreenIndex=1;typeof(ControllerWindow).GetMethod("InvalidateOverlayPlacement",Hidden)!.Invoke(controller,[false]);Update();
            Require(Windows().Length==1&&!windows[1].IsVisible,"Single display left a mirror behind.");
            typeof(ControllerWindow).GetMethod("InvalidateOverlayPlacement",Hidden)!.Invoke(controller,[true]);
            var other=manager.Sessions.First(s=>s!=session);manager.Separate(other);Update();ClockOverlayChecks.Pump(30);Update();
            Require(Windows().Length==2&&Windows().Select(w=>w.Position).Distinct().Count()==2,"Separated clocks overlap at the same preset.");
            Require(manager.Count==count&&session.Elapsed==time,"Placement or visibility changed timer data.");
        }
        finally{controller.Close();}
    }

    private static void Save(Window window,string name)
    {
        string folder=OutputOverride??"design/one-piece-v2/settings/implemented";Directory.CreateDirectory(folder);
        var root=(Control)window.Content!;using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(folder,name+".png"));
    }
}
