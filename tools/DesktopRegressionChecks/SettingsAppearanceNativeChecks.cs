using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsAppearanceNativeChecks
{
    const string Output="design/one-piece-v2/settings/appearance-implemented";
    static void Save(Window window,string name)
    {
        Directory.CreateDirectory(Output);var root=(Control)window.Content!;
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));
    }
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static T Find<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static SettingsWindow Open(AppSettings? settings=null)
    {
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
        var window=new SettingsWindow(settings??new AppSettings{ThemeMode=AppThemeCatalog.Nika}){ShowActivated=false,ShowInTaskbar=false};
        var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Appearance"));
        window.Show();ClockOverlayChecks.Pump(60);return window;
    }
    internal static void LayoutAndActions()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,OverlayTheme=OverlayThemeCatalog.Nika,TimeFormat=1};var window=Open(settings);
        try
        {
            Require(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Name=="AppearanceAppTab"),"Appearance sections have not been integrated.");
            int changes=0;Action<SettingsChangeKind> changed=_=>changes++;
            typeof(SettingsWindow).GetEvent("SettingsChanged",Hidden)!.GetAddMethod(true)!.Invoke(window,[changed]);
            var clock=Find<Button>(window,"AppearanceClockTab");clock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(30);
            Save(window,"floating-clock");
            Require(window.FindControl<ComboBox>("OverlayThemeCombo")!.IsEffectivelyVisible,"Clock section does not show its existing theme control.");
            window.FindControl<ComboBox>("OverlayThemeCombo")!.SelectedItem=OverlayThemeCatalog.Midnight;
            ClockOverlayChecks.Pump(200);Save(window,"classic-clock");Require(settings.ThemeMode==AppThemeCatalog.Nika&&settings.OverlayTheme==OverlayThemeCatalog.Midnight,"Clock theme changed the application theme.");
            Require(window.FindControl<ComboBox>("FontCombo")!.IsEffectivelyVisible,"Legacy clock controls were lost.");
            window.FindControl<ComboBox>("OverlayThemeCombo")!.SelectedItem=OverlayThemeCatalog.Pirate;ClockOverlayChecks.Pump(200);
            window.FindControl<Expander>("TransparencyDetailsExpander")!.IsExpanded=true;Find<ScrollViewer>(window,"AppearanceFormScroll").ScrollToEnd();ClockOverlayChecks.Pump(40);
            Require(window.FindControl<Expander>("TransparencyDetailsExpander")!.GetVisualDescendants().OfType<CheckBox>().Count()==9,"Pirate transparency options were lost.");Save(window,"pirate-clock-more");
            window.FindControl<ComboBox>("OverlayThemeCombo")!.SelectedItem=OverlayThemeCatalog.Nika;ClockOverlayChecks.Pump(40);
            Find<Button>(window,"AppearanceOpenStopwatch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require((string)typeof(SettingsWindow).GetProperty("CurrentCategory",Hidden)!.GetValue(window)! == "StopwatchAppearance","Detailed clock style link did not navigate.");
            Require(changes==3,"Section navigation persisted settings.");
        }
        finally{window.Close();}
    }
    internal static void Presentation()
    {
        var type=typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance");
        Require(type!=null,"Application scale and background consumers are missing.");
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=90};
        Page("Initialize",settings);var window=Open(settings);var overlay=new OverlayWindow{ShowActivated=false,ShowInTaskbar=false};
        ControllerWindow? controller=null;
        try
        {
            overlay.Show();var position=overlay.Position;var size=overlay.ClientSize;
            foreach(double percent in new[]{70d,90d,125d})
            {
                settings.UiScalePercent=percent;Page("Apply",settings,true);ClockOverlayChecks.Pump(60);
                Require(window.Content is LayoutTransformControl transform&&Math.Abs(transform.LayoutTransform!.Value.M11-percent/100)<.001,"Page scale was not applied.");
                Require(overlay.Content is not LayoutTransformControl&&overlay.Position==position&&overlay.ClientSize==size,"Page scale changed the floating overlay.");
                var done=Find<Button>(window,"SettingsDone");var point=done.TranslatePoint(default,window)!.Value;
                Require(point.X>=0&&point.Y>=0&&point.Y+done.Bounds.Height*percent/100<=window.ClientSize.Height+1,"Scale moved Done outside the window.");
                Save(window,"scale-"+percent);
            }
            var second=Open(settings);try{Require(second.Content is LayoutTransformControl transform&&transform.LayoutTransform!.Value.M11==1.25,"New pages do not inherit page scale.");}finally{second.Close();}
            controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
            controller.ShowActivated=false;controller.ShowInTaskbar=false;controller.Show();ClockOverlayChecks.Pump(60);
            var frame=controller.FindControl<Grid>("ControllerFrame")!;
            var end=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),controller)!.Value;
            Require(Math.Abs(end.X-controller.ClientSize.Width)<=1&&Math.Abs(end.Y-controller.ClientSize.Height)<=1,"Zoomed controller does not fit its native window.");
            // The existing interaction-end callback must apply the pending value without persisting sample settings.
            settings.UiScalePercent=70;Page("Apply",settings,false);
            Require(((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11==1.25,"Scale changed during slider adjustment.");
            typeof(ControllerWindow).GetField("_pendingPageScale",Hidden)!.SetValue(controller,true);
            typeof(ControllerWindow).GetMethod("CompleteSettingsInteraction",Hidden)!.Invoke(controller,null);
            Require(((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11==.7,"Ending slider adjustment did not apply scale.");
        }
        finally{controller?.Close();overlay.Close();window.Close();Page("Shutdown");}
    }
    static object? Page(string method,params object[] args)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static object? PageField(string field)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetField(field,BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null);
    static void Theme(string theme)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
    internal static void BackgroundsAndRestoration()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,PanelBackgroundId=AppBackgroundCatalog.FestiveChalk};
        Page("Initialize",settings);var window=Open(settings);
        try
        {
            var decoded=PageField("_image");Require(decoded!=null,"Built-in background was not decoded.");
            var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();Require(frame.Pattern is ImageBrush,"Shared panel does not consume the background pattern.");
            settings.PanelBackgroundStrength=45;Page("Apply",settings,true);Require(ReferenceEquals(decoded,PageField("_image")),"Strength adjustment decoded the same image again.");
            Require(((ImageBrush)frame.Pattern!).Opacity==.45,"Background strength did not reach the panel.");
            Find<ScrollViewer>(window,"AppearanceFormScroll").ScrollToEnd();ClockOverlayChecks.Pump(60);Save(window,"background-pattern");
            settings.PanelBackgroundId=AppBackgroundCatalog.ThemeDefault;Page("Apply",settings,true);Require(frame.Pattern==null&&PageField("_image")==null,"Theme default retained the old pattern.");
            var custom=new CustomAppBackground{Id=Guid.NewGuid().ToString("N"),DisplayName="Unavailable sample"};custom.FileName="custom-"+custom.Id+".png";
            settings.CustomBackgrounds.Add(custom);settings.PanelBackgroundId="custom:"+custom.Id;Page("Apply",settings,true);
            Require(settings.PanelBackgroundId=="custom:"+custom.Id&&PageField("_image")==null,"Unavailable custom background changed the saved choice.");
            Require(typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetProperty("BackgroundWarning",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!=null,"Unavailable background has no user feedback.");
            settings.PanelBackgroundId=AppBackgroundCatalog.ThemeDefault;Page("Apply",settings,true);
            foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Nika,AppThemeCatalog.Pirate,AppThemeCatalog.Nika})
            {
                window.FindControl<ComboBox>("ThemeCombo")!.SelectedItem=theme;Theme(theme);ClockOverlayChecks.Pump(200);
                typeof(SettingsWindow).GetMethod("UpdatePreviewSafely",Hidden)!.Invoke(window,null);ClockOverlayChecks.Pump(40);
                Require(window.Content is LayoutTransformControl,"Theme replacement lost page scale.");
                Require((Page("Content",window) is StopwatchFrameSurface)==(theme==AppThemeCatalog.Nika),$"Theme {theme} retained {Page("Content",window)?.GetType().Name} instead of its shell.");
                Require(window.FindControl<ComboBox>("ThemeCombo")!.IsEffectivelyVisible,"Theme switch detached the Appearance controls.");
                if(theme!=AppThemeCatalog.Nika)
                {
                    Require(window.FindControl<Button>("AddBackgroundButton")!.Margin==new Thickness(8,0,0,0),"Theme switch lost the original button layout.");
                    Require(window.FindControl<ComboBox>("NikaTypographyCombo")!.Items.Count==4,"Theme switching lost Nika choices.");
                }
            }
        }
        finally{window.Close();Page("Shutdown");}
    }
    internal static void BoundedImages()
    {
        var decode=typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetMethod("DecodeBackground",BindingFlags.Static|BindingFlags.NonPublic);
        Require(decode!=null,"Background decoding does not bound both dimensions or avoid upscaling.");
        foreach(var size in new[]{new PixelSize(2,128),new PixelSize(128,2),new PixelSize(1,8192),new PixelSize(8192,1),new PixelSize(1600,1200)})
        {
            using var source=new WriteableBitmap(size,new Vector(96,96),Avalonia.Platform.PixelFormat.Bgra8888,Avalonia.Platform.AlphaFormat.Premul);
            using var stream=new MemoryStream();source.Save(stream);stream.Position=0;
            using var result=(Bitmap)decode!.Invoke(null,[stream,".png"])!;
            Require(result.PixelSize.Width<=Math.Min(1024,size.Width)&&result.PixelSize.Height<=Math.Min(1024,size.Height),"Background decoder enlarged a narrow image or exceeded its cache bound.");
        }
        using var invalid=new MemoryStream(new byte[]{1,2,3});
        try{decode!.Invoke(null,[invalid,".png"]);throw new Exception("Invalid image was decoded.");}
        catch(TargetInvocationException error) when(error.InnerException is InvalidDataException){}
    }
    internal static void ProductionBackgroundLookup()
    {
        var configure=typeof(StopwatchOverlay.Desktop.App).GetMethod("ConfigureBackgroundValidation",BindingFlags.NonPublic|BindingFlags.Static);
        Require(configure!=null,"Production background validation still decodes full images to read dimensions.");
        var originalReader=AppBackgroundCatalog.ImageDimensionReader;var originalValidator=AppBackgroundCatalog.ImageValidator;
        string scratch=Path.Combine(Path.GetTempPath(),"stopwatch-background-lookup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        try
        {
            configure!.Invoke(null,null);
            byte[] header=new byte[32];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(header,0);header[19]=2;header[23]=128;
            using(var metadata=new MemoryStream(header))Require(AppBackgroundCatalog.ImageDimensionReader!(metadata,".png")== (2,128),"Production dimension reader tried to decode pixel data.");
            bool rejected=false;
            try{using var invalid=new MemoryStream(header);AppBackgroundCatalog.ImageValidator!(invalid,".png");}
            catch(Exception error) when(AppBackgroundCatalog.IsExpectedImageBoundaryFailure(error)){rejected=true;}
            Require(rejected,"Import validation accepted header-only corrupt image data.");
            var reader=AppBackgroundCatalog.ImageDimensionReader!;int probes=0;
            AppBackgroundCatalog.ImageDimensionReader=(stream,extension)=>{probes++;return reader(stream,extension);};
            var settings=new AppSettings();
            for(int i=0;i<2;i++)
            {
                var item=new CustomAppBackground{Id=Guid.NewGuid().ToString("N"),DisplayName="Sample "+i};item.FileName="custom-"+item.Id+".png";
                using var source=new WriteableBitmap(new PixelSize(2,128),new Vector(96,96),Avalonia.Platform.PixelFormat.Bgra8888,Avalonia.Platform.AlphaFormat.Premul);
                source.Save(Path.Combine(scratch,item.FileName));settings.CustomBackgrounds.Add(item);
            }
            var selected=typeof(AppBackgroundCatalog).GetMethod("GetSelectedChoice",BindingFlags.Public|BindingFlags.Static);
            Require(selected!=null,"There is no selected-only background lookup.");
            settings.PanelBackgroundId=AppBackgroundCatalog.FestiveChalk;selected!.Invoke(null,[settings,scratch]);Require(probes==0,"Choosing a built-in inspected unrelated custom files.");
            settings.PanelBackgroundId="custom:"+settings.CustomBackgrounds[0].Id;
            var choice=(AppBackgroundChoice)selected.Invoke(null,[settings,scratch])!;
            Require(choice.IsAvailable&&probes==1,"Selecting one image inspected the whole custom library.");
            using var file=File.OpenRead(choice.FilePath!);using var decoded=(Bitmap)Page("DecodeBackground",file,".png")!;
            Require(decoded.PixelSize==new PixelSize(2,128),"Production lookup enlarged the selected image.");
        }
        finally
        {
            AppBackgroundCatalog.ImageDimensionReader=originalReader;AppBackgroundCatalog.ImageValidator=originalValidator;
            foreach(string file in Directory.EnumerateFiles(scratch))File.Delete(file);Directory.Delete(scratch);
        }
    }
    internal static void CloseDuringScaleAdjustment()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=90};Page("Initialize",settings);var window=Open(settings);
        try
        {
            Action completed=()=>Page("Apply",settings,true);
            typeof(SettingsWindow).GetEvent("SettingsInteractionCompleted",Hidden)!.GetAddMethod(true)!.Invoke(window,[completed]);
            typeof(SettingsWindow).GetMethod("BeginSliderInteraction",Hidden)!.Invoke(window,null);
            settings.UiScalePercent=125;window.Close();
            Require(typeof(SettingsWindow).GetField("_appearanceController",Hidden)!.GetValue(window)==null&&typeof(SettingsWindow).GetField("_appearanceSnapshot",Hidden)!.GetValue(window)==null,"Closing during a scale change recreated the cleaned-up preview.");
        }
        finally{window.Close();typeof(SettingsWindow).GetMethod("ReleaseAppearancePreview",Hidden)!.Invoke(window,null);Page("Shutdown");}
    }
    internal static void PreviewIsolation()
    {
        var hotkey=new PreviewHotKey();var originalHotkey=PlatformServices.HotKey;PlatformServices.HotKey=hotkey;
        var opened=new List<Window>();using var watch=Window.WindowOpenedEvent.AddClassHandler<Window>((w,_)=>opened.Add(w));
        var window=Open();
        try
        {
            Require(opened.Count==1,"Application preview opened another native window.");
            Require(window.GetVisualDescendants().OfType<Image>().Any(i=>i.Name=="AppearanceApplicationImage"&&i.Source!=null),"Native application preview is missing.");
            Save(window,"application");
            var sample=(ControllerWindow)typeof(SettingsWindow).GetField("_appearanceController",Hidden)!.GetValue(window)!;
            Require(sample.FindControl<TextBlock>("ActiveWorkspaceTitle")!.Bounds.Width>0,"Native application preview omitted its timer content.");
            Require(sample.FindControl<ListBox>("TimerRailList")!.GetRealizedContainers().Count()==5,"Native application preview omitted its sample timers.");
            window.Width=840;window.Height=600;ClockOverlayChecks.Pump(40);
            var done=Find<Button>(window,"SettingsDone");double y=done.TranslatePoint(default,window)!.Value.Y;
            Save(window,"compact");
            Find<ScrollViewer>(window,"AppearanceCompactScroll").ScrollToEnd();ClockOverlayChecks.Pump(30);
            Require(done.TranslatePoint(default,window)!.Value.Y==y,"Appearance footer scrolls away.");
        }
        finally{window.Close();PlatformServices.HotKey=originalHotkey;Require(!hotkey.Disposed&&!hotkey.Unregistered,"Closing the preview disrupted live global shortcuts.");}
    }
    private sealed class PreviewHotKey:IHotKeyService
    {
        internal bool Disposed,Unregistered;
        public bool IsInCommandMode{get;set;}
        public bool RegisterHotKey(ShortcutAction action,uint modifiers,uint virtualKey)=>true;
        public void UnregisterHotKey(ShortcutAction action)=>Unregistered=true;
        public void UnregisterAll()=>Unregistered=true;
        public event EventHandler<ShortcutAction>? HotKeyPressed{add{}remove{}}
        public event EventHandler<uint>? CommandKeyPressed{add{}remove{}}
        public void Dispose()=>Disposed=true;
    }
}
