using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PickerKey=Avalonia.Input.Key;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SharedFrameUiChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Picker()
    {
        var type=typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.TimerPickerWindow")!;
        var ctor=type.GetConstructor(Hidden,null,new[]{typeof(IReadOnlyList<TimerSession>),typeof(TimerSession),typeof(AppSettings)},null);
        SharedFrameChecks.Require(ctor!=null,"Picker does not yet accept theme context for the shared frame.");
        var timers=Enumerable.Range(1,12).Select(i=>new TimerSession(i){Name=i==3?new string('W',160):"Clock "+i}).ToArray();
        Window Open(string theme)
        {
            var window=(Window)ctor!.Invoke(new object[]{timers,timers[2],new AppSettings{ThemeMode=theme}});
            window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();ClockOverlayChecks.Pump(50);return window;
        }
        TimerSession? Selected(Window window)=>(TimerSession?)type.GetProperty("SelectedTimer",Hidden)!.GetValue(window);
        foreach(var key in new[]{PickerKey.D1,PickerKey.NumPad2,PickerKey.Escape,PickerKey.Enter})
        {
            var window=Open(AppThemeCatalog.Nika);
            try
            {
                var surface=window.GetVisualDescendants().SingleOrDefault(c=>c.GetType().Name=="StopwatchFrameSurface");
                SharedFrameChecks.Require(surface!=null,"Nika picker does not use the shared frame surface.");
                var list=window.GetVisualDescendants().OfType<ListBox>().Single();
                SharedFrameChecks.Require(list.ItemCount==12&&list.SelectedIndex==2,"Frame wrapper changed picker contents or initial selection.");
                if(key==PickerKey.Enter)
                    SharedFrameChecks.Require(list.IsKeyboardFocusWithin,$"Initial picker focus is outside the list (list focusable: {list.Focusable}).");
                window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});ClockOverlayChecks.Pump(15);
                var expected=key==PickerKey.D1?timers[0]:key==PickerKey.NumPad2?timers[1]:key==PickerKey.Enter?timers[2]:null;
                SharedFrameChecks.Require(Selected(window)==expected&&!window.IsVisible,"Picker key behavior changed: "+key);
            }
            finally{window.Close();}
        }
        using var legacy=new WindowScope(Open(AppThemeCatalog.Midnight));
        SharedFrameChecks.Require(!legacy.Window.GetVisualDescendants().Any(c=>c.GetType().Name=="StopwatchFrameSurface"),"Non-Nika picker gained a decorative frame.");
    }
    private sealed class WindowScope(Window window):IDisposable
    {internal Window Window=>window;public void Dispose()=>window.Close();}

    internal static void Evidence()
    {
        string output=Path.GetFullPath("design/one-piece-v2/clock-picker/shared-renderer-integration");Directory.CreateDirectory(output);
        var app=Application.Current!;var resources=app.Resources.MergedDictionaries.ToArray();var variant=app.RequestedThemeVariant;
        try
        {
            typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{AppThemeCatalog.Nika});
            ThemedClockFace Clock(string frame,double fontSize=48)
            {
                var face=new ThemedClockFace{Text="00:44:27.0",ProjectName="Illustration"};
                face.ApplySettings(new AppSettings{OverlayTheme=OverlayThemeCatalog.Nika,TextColor="Theme default",TextSize=fontSize,StopwatchAppearance=new(){Frame=frame}});
                face.SetStopwatchPresentationState(new(true,true,false));
                face.Measure(Size.Infinity);face.Width=face.DesiredSize.Width;face.Height=face.DesiredSize.Height;face.Arrange(new Rect(face.DesiredSize));return face;
            }
            void Save(Control control,string name)
            {
                var size=control.Bounds.Size;
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height)),new Vector(96,96));
                bitmap.Render(control);bitmap.Save(Path.Combine(output,name+".png"));
            }
            void At(Canvas board,Control control,double x,double y){Canvas.SetLeft(control,x);Canvas.SetTop(control,y);board.Children.Add(control);}
            TextBlock Label(string text,int size=20)=>new(){Text=text,FontSize=size,Foreground=Brush.Parse("#F5E7C8")};
            var clock=Clock("engraved-filigree");Save(clock,"stopwatch");
            string[] names={"Illustration","Coding","Reading","Study","Writing","Planning"};
            var timers=names.Select((name,i)=>new TimerSession(i+1){Name=name}).ToArray();
            var type=typeof(OverlayWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.TimerPickerWindow")!;
            using var picker=new WindowScope((Window)Activator.CreateInstance(type,Hidden,null,new object[]{timers,timers[0],new AppSettings{ThemeMode=AppThemeCatalog.Nika}},null)!);
            picker.Window.ShowActivated=false;picker.Window.ShowInTaskbar=false;picker.Window.Show();ClockOverlayChecks.Pump(70);
            Save((Control)picker.Window.Content!,"clock-picker");
            var text=new StackPanel{Spacing=18};
            foreach(string value in new[]{"Larger shared panel","The same frame pieces and interior clip.","Background, padding and contents are independent.","No page-specific frame PNGs."})
                text.Children.Add(new TextBlock{Text=value,FontSize=22,Foreground=Brush.Parse("#052536")});
            var panel=new StopwatchFrameSurface{Width=820,Height=650,Child=text,Padding=new Thickness(8),Background=Brush.Parse("#EFDCB5")};
            panel.Measure(new Size(820,650));panel.Arrange(new Rect(0,0,820,650));Save(panel,"large-panel");
            var board=new Canvas{Width=1480,Height=1030,Background=Brush.Parse("#10232F")};
            At(board,Label("One shared production renderer",36),48,30);
            At(board,Label("Existing artwork · modular frame · matching background clip",18),50,84);
            At(board,Label("STOPWATCH OVERLAY"),64,150);At(board,Label("CLOCK PICKER"),64,415);At(board,Label("REUSABLE PANEL"),592,238);
            using var clockImage=new Bitmap(Path.Combine(output,"stopwatch.png"));
            using var pickerImage=new Bitmap(Path.Combine(output,"clock-picker.png"));
            using var panelImage=new Bitmap(Path.Combine(output,"large-panel.png"));
            At(board,new Image{Source=clockImage,Width=clockImage.Size.Width,Height=clockImage.Size.Height},64,190);
            At(board,new Image{Source=pickerImage,Width=pickerImage.Size.Width,Height=pickerImage.Size.Height},64,450);
            At(board,new Image{Source=panelImage,Width=820,Height=650},592,280);
            board.Measure(new Size(1480,1030));board.Arrange(new Rect(0,0,1480,1030));Save(board,"shared-renderer");

            var catalog=new Canvas{Width=1800,Height=1040,Background=Brush.Parse("#10232F")};
            At(catalog,Label("All 16 frames / actual stopwatch controls",28),28,20);
            for(int i=0;i<StopwatchAppearanceCatalog.Frames.Count;i++)
            {
                var option=StopwatchAppearanceCatalog.Frames[i];var face=Clock(option.Id);
                At(catalog,Label($"{i+1:00}  {option.Label}",17),25+i%4*445,75+i/4*235);
                At(catalog,face,25+i%4*445,105+i/4*235);
                foreach(double size in new[]{16d,120d})
                {
                    var scaled=Clock(option.Id,size);
                    SharedFrameChecks.Require(double.IsFinite(scaled.Width)&&scaled.Width>0&&scaled.Height>0,"Invalid scaled overlay: "+option.Id);
                }
            }
            catalog.Measure(new Size(1800,1040));catalog.Arrange(new Rect(0,0,1800,1040));Save(catalog,"all-frame-overlays");
        }
        finally
        {app.Resources.MergedDictionaries.Clear();foreach(var resource in resources)app.Resources.MergedDictionaries.Add(resource);app.RequestedThemeVariant=variant;}
    }
}
