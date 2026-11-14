using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Desktop.Platform;

namespace StopwatchOverlay.Desktop;

// Application pages share these resources; floating overlays own their scale and surfaces.
internal static class DesktopPageAppearance
{
    private static readonly Dictionary<Window,LayoutTransformControl> Pages=new();
    private static IDisposable? _opened;
    private static AppSettings? _settings;
    private static Bitmap? _image;
    private static string? _imageKey;
    private static string? _publishedTheme;
    private static double _publishedStrength=double.NaN;
    private static double _scale=1;
    internal static string? BackgroundWarning { get; private set; }
    internal static IBrush? Pattern { get; private set; }

    internal static void Initialize(AppSettings settings)
    {
        _settings=settings;
        _opened??=Window.WindowOpenedEvent.AddClassHandler<Window>((window,_)=>Attach(window));
        Apply(settings,true);
    }
    internal static void Apply(AppSettings settings,bool applyScale)
    {
        _settings=settings;
        ApplyBackground(settings);
        if(!applyScale)return;
        double previousScale=_scale;
        _scale=AppUiScale.Normalize(settings.UiScalePercent)/100;
        foreach(var (window,transform) in Pages.ToArray())
        {
            transform.LayoutTransform=new ScaleTransform(_scale,_scale);
            if(window is ControllerWindow controller)ResizeController(controller,_scale/previousScale,window.ClientSize);
            if(window is SettingsWindow inspector)inspector.RefreshPageScale();
        }
    }
    internal static double Scale(Window window)=>Pages.ContainsKey(window)?_scale:1;
    internal static object? Content(Window window)=>Pages.TryGetValue(window,out var transform)?transform.Child:window.Content;
    private static bool IsPage(Window window)=>window.GetType().Namespace==typeof(ControllerWindow).Namespace&&window is not
        (OverlayWindow or TodoOverlayWindow or ActiveClocksShelfWindow or ShortcutCommandHintWindow or NoteCommandHintWindow);
    private static void Attach(Window window)
    {
        if(!IsPage(window)||Pages.ContainsKey(window)||window.Content is not Control content)return;
        // WindowOpened can precede the first native resize to the requested Width/Height.
        var clientSize=new Size(double.IsFinite(window.Width)?window.Width:window.ClientSize.Width,
                                double.IsFinite(window.Height)?window.Height:window.ClientSize.Height);
        window.Content=null;
        var transform=new LayoutTransformControl{Child=content,LayoutTransform=new ScaleTransform(_scale,_scale)};
        Pages.Add(window,transform);
        window.Content=transform;
        if(window is ControllerWindow controller)
        {
            window.SizeChanged+=ControllerSizeChanged;
            ResizeController(controller,_scale,clientSize);
        }
        window.PropertyChanged+=ContentChanged;window.Closed+=Closed;
        if(window is SettingsWindow inspector)inspector.RefreshPageScale();
    }
    private static void ControllerSizeChanged(object? sender,SizeChangedEventArgs e)
    {
        if(sender is ControllerWindow controller)FitControllerContent(controller);
    }
    private static void ResizeController(ControllerWindow window,double ratio,Size size)
    {
        window.MinWidth=Math.Round(window.MinWidth*ratio,6);window.MinHeight=Math.Round(window.MinHeight*ratio,6);
        if(window.WindowState==WindowState.Normal&&Math.Abs(ratio-1)>.0001)
        {
            double width=size.Width*ratio,height=size.Height*ratio;
            if(window.Screens.ScreenFromWindow(window) is {} screen)
            {
                var chrome=window.SystemDecorations==SystemDecorations.None?default(Size):(window.FrameSize??window.ClientSize)-window.ClientSize;
                width=Math.Min(width,screen.WorkingArea.Width/window.RenderScaling-chrome.Width);
                height=Math.Min(height,screen.WorkingArea.Height/window.RenderScaling-chrome.Height);
            }
            window.Width=width;window.Height=height;
            window.Position=OverlayScreenConstraint.ClampToScreens(window.Position,window.FrameSize??window.ClientSize,window.Screens.All);
        }
        FitControllerContent(window);
    }
    private static void FitControllerContent(ControllerWindow window)
    {
        // The native client area is already zoomed; layout must use its unscaled size.
        if(Content(window) is Control content){content.Width=window.ClientSize.Width/_scale;content.Height=window.ClientSize.Height/_scale;}
        window.UpdateControllerLayout();
        // SizeChanged runs after arrangement; its updated canvas must also be re-centered.
        Pages[window].InvalidateArrange();
    }
    private static void ContentChanged(object? sender,AvaloniaPropertyChangedEventArgs e)
    {
        if(e.Property!=ContentControl.ContentProperty||sender is not Window window||!Pages.Remove(window,out var transform))return;
        // Settings replaces its shell on theme changes. Detach synchronously so it can reuse that root.
        window.PropertyChanged-=ContentChanged;window.Closed-=Closed;window.SizeChanged-=ControllerSizeChanged;
        transform.Child=null;
        Dispatcher.UIThread.Post(()=>{if(window.IsVisible)Attach(window);});
    }
    private static void Closed(object? sender,EventArgs e)
    {
        if(sender is not Window window)return;
        Pages.Remove(window);window.PropertyChanged-=ContentChanged;window.Closed-=Closed;window.SizeChanged-=ControllerSizeChanged;
    }
    internal static void RefreshTheme()
    {
        _publishedTheme=null;
        if(_settings!=null)ApplyBackground(_settings);
    }
    private static void ApplyBackground(AppSettings settings)
    {
        if(Application.Current is not {} app)return;
        string key=settings.PanelBackgroundId;
        if(key!=_imageKey)
        {
            Bitmap? next=null;BackgroundWarning=null;
            try
            {
                var choice=AppBackgroundCatalog.GetSelectedChoice(settings);
                if(choice is {IsThemeDefault:false})
                {
                    if(!choice.IsAvailable)throw new IOException("The selected background is unavailable.");
                    using var stream=choice.IsCustom?File.OpenRead(choice.FilePath!):AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Backgrounds/"+Path.GetFileName(choice.ResourceUri!)));
                    next=DecodeBackground(stream,Path.GetExtension(choice.FilePath??choice.ResourceUri!));
                }
            }
            catch(Exception ex) when(AppBackgroundCatalog.IsExpectedImageBoundaryFailure(ex))
            {
                BackgroundWarning="Background unavailable · using the theme surface. Add the image again to restore it.";
                CrashLogger.LogRecoverable(ex,"DesktopPageAppearance.Background");
            }
            var previous=_image;_image=next;_imageKey=key;
            PublishBackground(settings,app);
            previous?.Dispose();
            return;
        }
        if(_publishedTheme!=settings.ThemeMode||_publishedStrength!=settings.PanelBackgroundStrength)PublishBackground(settings,app);
    }
    private static void PublishBackground(AppSettings settings,Application app)
    {
        _publishedTheme=settings.ThemeMode;_publishedStrength=settings.PanelBackgroundStrength;
        Pattern=_image==null?null:new ImageBrush(_image){TileMode=TileMode.Tile,Stretch=Stretch.UniformToFill,DestinationRect=new RelativeRect(0,0,512,512,RelativeUnit.Absolute),Opacity=Math.Clamp(double.IsFinite(settings.PanelBackgroundStrength)?settings.PanelBackgroundStrength:30,10,45)/100};
        app.Resources["DesktopPanelPattern"]=Pattern;
        app.Resources.Remove("AppBackgroundBrush");
        if(Pattern==null||!app.TryFindResource("AppBackgroundBrush",out var value)||value is not IBrush surface)return;
        var rectangle=new RectangleGeometry(new Rect(0,0,512,512));
        var drawing=new DrawingGroup();drawing.Children.Add(new GeometryDrawing{Brush=surface,Geometry=rectangle});drawing.Children.Add(new GeometryDrawing{Brush=Pattern,Geometry=rectangle});
        app.Resources["AppBackgroundBrush"]=new DrawingBrush(drawing){TileMode=TileMode.Tile,DestinationRect=new RelativeRect(0,0,512,512,RelativeUnit.Absolute)};
    }
    internal static Bitmap DecodeBackground(Stream stream,string extension)
    {
        if(stream.Length>AppBackgroundCatalog.MaximumImportBytes||!AppBackgroundCatalog.TryReadImageDimensions(stream,extension.ToLowerInvariant(),out int width,out int height)
            ||width<=0||height<=0||width>AppBackgroundCatalog.MaximumImageDimension||height>AppBackgroundCatalog.MaximumImageDimension||(long)width*height>AppBackgroundCatalog.MaximumPixelCount)
            throw new InvalidDataException("The background image exceeds the supported image limits or has an invalid header.");
        stream.Position=0;
        try
        {
            double ratio=Math.Min(1,1024d/Math.Max(width,height));
            if(Math.Min(width,height)*ratio<1)
            {
                // Skia rounds the narrow dimension to zero; these thin sources contain at most 65536 pixels.
                using var original=new Bitmap(stream);
                return original.CreateScaledBitmap(new PixelSize(Math.Max(1,(int)(width*ratio)),Math.Max(1,(int)(height*ratio))));
            }
            return width>=height?Bitmap.DecodeToWidth(stream,Math.Min(1024,width)):Bitmap.DecodeToHeight(stream,Math.Min(1024,height));
        }
        catch(NullReferenceException error)
        {
            throw new InvalidDataException("The image decoder could not read this background.",error);
        }
    }
    internal static void Shutdown()
    {
        _opened?.Dispose();_opened=null;_settings=null;
        foreach(var window in Pages.Keys.ToArray())Closed(window,EventArgs.Empty);
        if(Application.Current is {} app){app.Resources.Remove("DesktopPanelPattern");app.Resources.Remove("AppBackgroundBrush");}
        _image?.Dispose();_image=null;_imageKey=null;_publishedTheme=null;_publishedStrength=double.NaN;Pattern=null;BackgroundWarning=null;_scale=1;
    }
}
