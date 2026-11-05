using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static partial class StopwatchAppearanceChecks
{
    internal static string? OutputOverride {get;set;}
    internal static string OutputDirectory => Path.GetFullPath(OutputOverride??"design/one-piece-v2/stopwatch-overlay/implementation-review");
    internal static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }

    internal static void Renderer()
    {
        var face=new ThemedClockFace();
        face.ApplySettings(new AppSettings {TextColor="Theme default"});
        var profile=new StopwatchAppearanceSettings();
        Apply(face,profile);
        Size Measure(string time,string name)
        {
            face.Text=time; face.ProjectName=name;
            face.Measure(Size.Infinity); face.Arrange(new Rect(face.DesiredSize));
            return face.DesiredSize;
        }
        var baseline=Measure("00:00:00.0","Illustration");
        Require(Measure("11:11:11.1","Illustration")==baseline,"Ticks changed clock width.");
        Require(Measure("88:88:88.8","Illustration")==baseline,"Wide digits changed clock width.");
        SetState(face,true,true,false);
        Require(Measure("00:00:00.0","Illustration")==baseline,"Active tab changed clock geometry.");
        var running=Render(face,"default-running");
        int sample=(70*(int)Math.Ceiling(face.Bounds.Width)+60)*4+3;
        Require(running[sample]>=176&&running[sample]<=180,"Background layer is not 70%: "+running[sample]);
        SetState(face,true,false,false);
        Require(Measure("00:00:00.0","Illustration")==baseline,"Pause changed geometry.");
        Require(!running.SequenceEqual(Render(face,"default-paused")),"Running/paused marker did not redraw.");
        foreach(var name in new[]{"",new string('W',180),"تصویرسازی و برنامه ریزی برای پروژه"})
        {
            Require(Measure("00:00:00.0",name)==baseline,"Project name changed clock anchor/width.");
            Require(ControlAutomationPeer.CreatePeerForElement(face)!.GetName().Contains(name),"Accessible name lost full project text.");
            Render(face,null);
        }
        Require(Measure("123:44:27.0","Illustration").Width>baseline.Width,"Long-hour time did not gain digit space.");
        foreach(double size in new[]{16d,48d,120d})
        {
            face.ApplySettings(new AppSettings {TextSize=size,TextColor="Theme default"});Apply(face,profile);
            SetState(face,true,true,false);Measure("00:44:27.0","Illustration");
            Require(Render(face,"default-size-"+size).Any(v=>v>0),"Empty default renderer.");
        }
        face.ApplySettings(new AppSettings {TextColor="Theme default",ShowRecIndicator=true});Apply(face,profile);
        SetState(face,false,true,false);Measure("00:44:27.0","Illustration");Render(face,"default-rec");
        profile.BackgroundOpacity=0;profile.OpaqueLayers=StopwatchOpaqueLayers.None;
        Apply(face,profile);face.ShowRec=false;Measure("00:44:27.0","Illustration");
        var transparent=Render(face,null);
        Require(Enumerable.Range(0,transparent.Length/4).All(i=>transparent[i*4+3]==0),"Unchecked layers did not follow zero background opacity.");
        profile.OpaqueLayers=StopwatchOpaqueLayers.TimeText;Apply(face,profile);Measure("00:44:27.0","Illustration");
        var text=Render(face,null);
        Require(Enumerable.Range(0,text.Length/4).Any(i=>text[i*4+3]>240),"Keep-opaque time disappeared at zero background opacity.");
    }

    internal static void LiveOverlay()
    {
        var method=typeof(OverlayWindow).GetMethod("ApplyAppearance");
        Require(method!=null,"Native window does not yet apply the new appearance profile.");
        var settings=new AppSettings {ThemeMode=AppThemeCatalog.Nika,TextColor="Theme default"};
        var window=new OverlayWindow {ShowActivated=false,ShowInTaskbar=false};
        try
        {
            method!.Invoke(window,new[]{settings});window.UpdateTime("00:44:27.0");window.SetTimerName("Illustration");
            window.SetActive(true);window.SetRunning(false);window.Show();ClockOverlayChecks.Pump(100);
            var face=window.FindControl<ThemedClockFace>("ClockFace")!;
            Require(face.HasStopwatchAppearance,"Nika live window used the legacy clock renderer.");
            var dimensions=window.ClientSize;var position=window.Position;
            var selected=Render(face,"live-selected-paused");
            window.SetRunning(true);ClockOverlayChecks.Pump(30);
            Require(!selected.SequenceEqual(Render(face,"live-selected-running")),"Window did not forward running state.");
            window.SetInactiveSeparated(true,.4);ClockOverlayChecks.Pump(30);
            var dimmed=Render(window,"live-unselected-window");
            Require(MaxAlpha(dimmed)>=100&&MaxAlpha(dimmed)<=103,"Unselected clock was not composited at 40%: "+MaxAlpha(dimmed));
            Require(window.ClientSize==dimensions&&window.Position==position,"State transition moved the native clock.");
            var popup=window.FindControl<Popup>("ActionPopup")!;
            var surface=window.FindControl<Border>("ActionSurface")!;
            var pause=window.FindControl<Button>("PauseResumeActionButton")!;
            int actions=0;window.PauseResumeRequested+=()=>actions++;
            popup.IsOpen=true;ClockOverlayChecks.Pump(75);
            var pixels=Render(surface,"live-unselected-toolbar");
            int maximum=Enumerable.Range(0,pixels.Length/4).Max(i=>(int)pixels[i*4+3]);
            Require(maximum>=100&&maximum<=103,"Native toolbar alpha is not 40%: "+maximum);
            pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(actions==1,"Pause dispatch duplicated.");
            window.SetRunning(false);window.SetPauseResumeEnabled(true);var paused=Render(face,null);
            window.SetPauseResumeEnabled(false);Require(!pause.IsEnabled,"Clock-mode pause was not disabled.");
            Require(!paused.SequenceEqual(Render(face,null)),"Clock-mode window still displays paused marker.");
            window.SetPauseResumeEnabled(true);
            settings.StopwatchAppearance.OpaqueLayers &= ~StopwatchOpaqueLayers.HoverControls;
            method.Invoke(window,new[]{settings});window.SetInactiveSeparated(true,.4);popup.IsOpen=true;ClockOverlayChecks.Pump(30);
            var uncheckedPixels=Render(surface,"live-unchecked-toolbar");
            Require(MaxAlpha(uncheckedPixels)>=70&&MaxAlpha(uncheckedPixels)<=73,"Unchecked toolbar did not compose 70% then 40%: "+MaxAlpha(uncheckedPixels));
            InvokePointerBoundary(window,"OnTimerSurfacePointerExited");
            ClockOverlayChecks.Pump(60);InvokePointerBoundary(window,"ActionSurface_PointerEntered");ClockOverlayChecks.Pump(220);
            Require(popup.IsOpen,"Crossing into controls closed the popup.");
            InvokePointerBoundary(window,"ActionSurface_PointerExited");ClockOverlayChecks.Pump(240);
            Require(!popup.IsOpen,"Popup did not close after departure delay.");
            popup.IsOpen=true;window.SetClickThrough(true);
            InvokePointerBoundary(window,"OnTimerSurfacePointerEntered");Require(!popup.IsOpen,"Click-through opened controls.");
            window.SetClickThrough(false);
            settings.OverlayTheme=OverlayThemeCatalog.Pirate;method.Invoke(window,new[]{settings});
            Require(!face.HasStopwatchAppearance,"Legacy theme retained the new renderer.");
            settings.OverlayTheme=AppThemeCatalog.Nika;method.Invoke(window,new[]{settings});
            pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(actions==2,"Theme round trip duplicated handlers.");
            window.SetActive(true);window.SetInactiveSeparated(false,.4);window.SetRunning(false);ClockOverlayChecks.Pump(30);
            Require(window.Opacity==1,"Selected paused timer was dimmed.");
            Render(window,"live-default-window");
            window.ApplyTheme(OverlayThemeCatalog.Pirate,AppThemeCatalog.Nika);
            Require(!face.HasStopwatchAppearance,"Direct legacy theme change retained the Nika renderer.");
            window.ApplyTheme(AppThemeCatalog.Nika,AppThemeCatalog.Midnight);
            Require(face.HasStopwatchAppearance,"Direct Nika theme change did not install default appearance.");
        }
        finally{window.Close();}
    }

    internal static void CatalogAssets()
    {
        var failures=new List<string>();
        using var resources=JsonDocument.Parse(File.ReadAllText("StopwatchOverlay.Desktop/Assets/Stopwatch/asset-manifest.json"));
        using var manifest=JsonDocument.Parse(File.ReadAllText("design/one-piece-v2/stopwatch-overlay/design-exploration.json"));
        foreach(var (key,kind,options) in new[]{
            ("", "background",StopwatchAppearanceCatalog.Backgrounds),
            ("frameSelection","frame",StopwatchAppearanceCatalog.Frames),
            ("hoverControlsSelection","control",StopwatchAppearanceCatalog.HoverControls)})
        {
            var selected=(key==""?manifest.RootElement:manifest.RootElement.GetProperty(key)).GetProperty("selectedOptions").EnumerateArray().Select(x=>x.GetString()).OrderBy(x=>x);
            Require(selected.SequenceEqual(options.Select(x=>x.Id).OrderBy(x=>x)),"Catalog differs from retained "+kind+" choices.");
            foreach(var option in options)
                try
                {
                    var asset=StopwatchAppearanceAssets.Shared.Get(kind,option.Id);
                    Require(asset.Bitmap.PixelSize.Width>0&&asset.Bitmap.PixelSize.Height>0,"Empty image "+option.Id);
                    if(kind!="background")
                    {
                        var buffer=Marshal.AllocHGlobal(4);
                        try{asset.Bitmap.CopyPixels(new PixelRect(0,0,1,1),buffer,4,4);Require(Marshal.ReadByte(buffer,3)==0,"Opaque exterior corner on "+option.Id);}
                        finally{Marshal.FreeHGlobal(buffer);}
                    }
                }
                catch(Exception ex){failures.Add(option.Id+": "+ex.Message);}
        }
        foreach(var option in StopwatchAppearanceCatalog.Typography)
            try
            {
                var p=StopwatchTypography.Resolve(option.Id);
                var typeface=new Typeface(p.TimerFont,FontStyle.Normal,p.TimerWeight);
                Require(FontManager.Current.TryGetGlyphTypeface(typeface,out var glyphs)&&glyphs.GetGlyph('8')!=0,"Font did not load for "+option.Id);
                var declared=resources.RootElement.GetProperty("typography").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==option.Id);
                string expected=declared.GetProperty("timerFamily").GetString()!;
                Require(glyphs!.FamilyName.StartsWith(expected,StringComparison.Ordinal),"Unexpected font fallback for "+option.Id+": "+glyphs.FamilyName);
                foreach(char c in "0123456789:.- ")Require(glyphs.GetGlyph(c)!=0,"Missing timer glyph "+c+" in "+option.Id);
                foreach(string key in new[]{"timerFile","timerLicense","nameFile","nameLicense"})Require(File.Exists("StopwatchOverlay.Desktop/"+declared.GetProperty(key).GetString()),"Missing font provenance "+key+" in "+option.Id);
                Console.WriteLine("Font "+option.Id+": "+glyphs!.FamilyName);
                foreach(double size in new[]{16d,48d,120d})
                {
                    var face=new ThemedClockFace();face.ApplySettings(new AppSettings{TextSize=size,TextColor="Theme default"});
                    Apply(face,new StopwatchAppearanceSettings{Typography=option.Id});
                    face.ProjectName="تصویرسازی Project 123";face.Text="00:00:00.0";face.Measure(Size.Infinity);var zero=face.DesiredSize;
                    face.Text="88:88:88.8";face.Measure(Size.Infinity);Require(face.DesiredSize==zero,"Unstable digits in "+option.Id);
                    face.Text="123:45:67.8";face.Measure(Size.Infinity);face.Arrange(new Rect(face.DesiredSize));Render(face,null);
                }
            }
            catch(Exception ex){failures.Add(option.Id+": "+ex.Message);}
        Require(failures.Count==0,string.Join(" | ",failures));
        foreach(var background in StopwatchAppearanceCatalog.Backgrounds)
        {
            var grid=Gallery(4,4,350,170);
            for(int i=0;i<StopwatchAppearanceCatalog.Frames.Count;i++)
            {
                var frame=StopwatchAppearanceCatalog.Frames[i];
                AddCell(grid,Sample(new StopwatchAppearanceSettings{Background=background.Id,Frame=frame.Id},frame.Label),i,4);
            }
            RenderGallery(grid,"matrix-"+background.Id,1400,680);
        }
        var fonts=Gallery(2,10,440,160);
        for(int i=0;i<10;i++)for(int j=0;j<2;j++)
            AddCell(fonts,Sample(new StopwatchAppearanceSettings{Typography=StopwatchAppearanceCatalog.Typography[i].Id,
                Background=j==0?"navigator-parchment":"midnight-chart"},StopwatchAppearanceCatalog.Typography[i].Label),i*2+j,2);
        RenderGallery(fonts,"typography-all",880,1600);
        var art=Gallery(3,1,410,160);
        for(int i=0;i<3;i++)AddCell(art,Sample(new StopwatchAppearanceSettings{Artwork=StopwatchAppearanceCatalog.Artworks[i].Id},StopwatchAppearanceCatalog.Artworks[i].Label),i,3);
        RenderGallery(art,"artwork-all",1230,160);
        var artworkFace=new ThemedClockFace {Text="00:44:27.0",ProjectName="Illustration"};artworkFace.ApplySettings(new AppSettings{TextColor="Theme default"});
        byte[]? previous=null;
        foreach(var choice in StopwatchAppearanceCatalog.Artworks)
        {
            Apply(artworkFace,new StopwatchAppearanceSettings{Artwork=choice.Id});artworkFace.Measure(Size.Infinity);artworkFace.Arrange(new Rect(artworkFace.DesiredSize));
            var pixels=Render(artworkFace,null);Require(previous==null||!previous.SequenceEqual(pixels),"Artwork choice did not change the live rendering: "+choice.Id);previous=pixels;
        }
        string[] matched={"classic-brass","instrument-case","slim-capsule","pixel-voyage","navy-bevel","open-corners","walnut-rim","cloud-crest","tide-curve","engraved-filigree","vine-filigree","royal-cartouche"};
        var controls=Gallery(4,6,420,240);
        for(int i=0;i<12;i++)for(int j=0;j<2;j++)
        {
            var profile=new StopwatchAppearanceSettings{HoverControls=StopwatchAppearanceCatalog.HoverControls[i].Id,Frame=j==0?matched[i]:"engraved-filigree"};
            var stack=(StackPanel)Sample(profile,StopwatchAppearanceCatalog.HoverControls[i].Label+(j==0?" · matched":" · default / paused"),j==0);
            var panel=new StackPanel {Orientation=Orientation.Horizontal};var buttons=Enumerable.Range(0,3).Select(_=>new Button()).ToArray();
            foreach(var button in buttons)panel.Children.Add(button);
            var surface=new Border{Child=panel,HorizontalAlignment=HorizontalAlignment.Center};
            StopwatchToolbarStyler.Apply(surface,buttons,profile,new StopwatchPresentationState(true,j==0,false),1);stack.Children.Add(surface);
            AddCell(controls,stack,i*2+j,4);
        }
        RenderGallery(controls,"controls-all",1680,1440);
    }

    private static Grid Gallery(int columns,int rows,int width,int height)
        =>new(){Width=columns*width,Height=rows*height,ColumnDefinitions=new ColumnDefinitions(string.Join(",",Enumerable.Repeat("*",columns))),
            RowDefinitions=new RowDefinitions(string.Join(",",Enumerable.Repeat("*",rows))),Background=Brush.Parse("#142633")};

    private static Control Sample(StopwatchAppearanceSettings profile,string label,bool running=true)
    {
        var face=new ThemedClockFace {Text="00:44:27.0",ProjectName="Illustration"};
        face.ApplySettings(new AppSettings{TextColor="Theme default"});Apply(face,profile);SetState(face,true,running,false);
        var stack=new StackPanel {Margin=new Thickness(6),Spacing=3};
        stack.Children.Add(new TextBlock {Text=label,Foreground=Brushes.White,FontSize=12,HorizontalAlignment=HorizontalAlignment.Center});
        stack.Children.Add(new Viewbox {Child=face,Height=125,Stretch=Stretch.Uniform});
        return stack;
    }
    private static void AddCell(Grid grid,Control control,int index,int columns)
    {Grid.SetColumn(control,index%columns);Grid.SetRow(control,index/columns);grid.Children.Add(control);}
    private static void RenderGallery(Control control,string name,int width,int height)
    {
        var host=new Window{Content=control,Width=width,Height=height,ShowInTaskbar=false,ShowActivated=false};
        try{host.Show();ClockOverlayChecks.Pump(40);control.Measure(new Size(width,height));control.Arrange(new Rect(0,0,width,height));Render(control,name);}
        finally{host.Close();}
    }

    private static void InvokePointerBoundary(OverlayWindow window,string method)
        => typeof(OverlayWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,new object?[]{window,null});

    private static int MaxAlpha(byte[] pixels)=>Enumerable.Range(0,pixels.Length/4).Max(i=>(int)pixels[i*4+3]);

    private static void Apply(ThemedClockFace face,StopwatchAppearanceSettings profile)
    {
        var method=typeof(ThemedClockFace).GetMethod("ApplyStopwatchAppearance");
        Require(method!=null,"Shared clock does not yet support the independent stopwatch profile.");
        method!.Invoke(face,new object?[]{profile});
    }

    private static void SetState(ThemedClockFace face,bool active,bool running,bool clock)
    {
        var type=typeof(ThemedClockFace).Assembly.GetType("StopwatchOverlay.Desktop.Controls.StopwatchPresentationState")!;
        Require(type!=null,"Clock presentation state is missing.");
        typeof(ThemedClockFace).GetMethod("SetStopwatchPresentationState")!.Invoke(face,new[]{Activator.CreateInstance(type!,active,running,clock)});
    }

    internal static byte[] Render(Avalonia.Controls.Control control,string? name)
    {
        int width=(int)Math.Ceiling(control.Bounds.Width),height=(int)Math.Ceiling(control.Bounds.Height);
        Require(width>0&&height>0,"Control has empty bounds.");
        using var target=new RenderTargetBitmap(new PixelSize(width,height),new Vector(96,96));
        target.Render(control);
        var pixels=new byte[width*height*4];var buffer=Marshal.AllocHGlobal(pixels.Length);
        try{target.CopyPixels(new PixelRect(0,0,width,height),buffer,pixels.Length,width*4);Marshal.Copy(buffer,pixels,0,pixels.Length);}
        finally{Marshal.FreeHGlobal(buffer);}
        if(name!=null){Directory.CreateDirectory(OutputDirectory);target.Save(Path.Combine(OutputDirectory,name+".png"));}
        return pixels;
    }
}
