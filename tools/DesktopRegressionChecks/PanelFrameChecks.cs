using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class PanelFrameChecks
{
    internal const string Output="design/one-piece-v2/shared-panel-frame";
    const string Evidence=".superpowers/sdd/2026-11-14-panel-frame";
    static readonly IBrush Paper=Brush.Parse("#EBD3A5");
    internal static void Baseline()
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Evidence);
        string baseline=Path.Combine(Output,"clock-baseline.json");
        SharedFrameChecks.Require(!File.Exists(baseline),"Keep the recorded pre-change clock baseline; do not overwrite it.");
        File.WriteAllText(baseline,JsonSerializer.Serialize(ClockPixels(),new JsonSerializerOptions{WriteIndented=true}));
        CapturePanels("before");
        var asset=StopwatchAppearanceAssets.Shared.Get("frame","engraved-filigree");
        using var source=SharedFrameChecks.Capture(new Size(640,1448),dc=>
        {dc.DrawRectangle(Paper,null,new Rect(0,0,640,1448));dc.DrawImage(asset.Bitmap,new Rect(0,0,320,724),new Rect(0,0,640,1448));});
        source.Save(Path.Combine(Evidence,"source-left.png"));
    }
    internal static Dictionary<string,string> ClockPixels()
    {
        var result=new Dictionary<string,string>();
        foreach(var option in StopwatchAppearanceCatalog.Frames)
        foreach(int percent in new[]{70,90,125})
        {
            var face=new ThemedClockFace{Text="00:44:27.0",ProjectName="Illustration"};
            face.ApplySettings(new AppSettings{OverlayTheme=OverlayThemeCatalog.Nika,StopwatchAppearance=new(){Frame=option.Id}});
            face.ApplyAppearance(Colors.White,Colors.Black,48*percent/100d,2,"Consolas",.7,true,NavigatorOpaqueParts.All);
            face.SetStopwatchPresentationState(new(true,true,false));
            face.Measure(Size.Infinity);face.Arrange(new Rect(face.DesiredSize));
            using var bitmap=SharedFrameChecks.Capture(face.Bounds.Size,face.Render);
            result.Add(option.Id+"-"+percent,Convert.ToHexString(SHA256.HashData(SharedFrameChecks.Pixels(bitmap))));
        }
        return result;
    }
    internal static void CleanOrnaments()
    {
        var size=new Size(1120,1000);const double scale=.5;
        var asset=StopwatchAppearanceAssets.Shared.Get("frame","engraved-filigree");
        var panel=new StopwatchFrameSurface{Width=size.Width,Height=size.Height,ArtworkScale=scale};
        panel.Measure(size);panel.Arrange(new Rect(size));
        using var actual=SharedFrameChecks.Capture(size,panel.Render);
        var regions=new List<Rect>();
        using var expected=SharedFrameChecks.Capture(size,dc=>
        {
            foreach(bool right in new[]{false,true})
            foreach(var (sourceY,height,destinationY) in new[]{(0d,300d,0d),(300d,130d,(size.Height-65)/2),(430d,294d,size.Height-147)})
            {
                double width=sourceY==300?140:290,x=right?size.Width-width*scale:0;
                var destination=new Rect(x,destinationY,width*scale,height*scale);
                dc.DrawImage(asset.Bitmap,new Rect(right?2172-width:0,sourceY,width,height),destination);
                regions.Add(destination.Deflate(new Thickness(2,10,2,10)));
            }
        });
        var a=SharedFrameChecks.Pixels(actual);var e=SharedFrameChecks.Pixels(expected);int stray=0;
        foreach(var region in regions)
        for(int y=(int)Math.Ceiling(region.Top);y<region.Bottom;y++)
        for(int x=(int)Math.Ceiling(region.Left);x<region.Right;x++)
        {int i=(y*actual.PixelSize.Width+x)*4+3;if(e[i]<8&&a[i]>64)stray++;}
        SharedFrameChecks.Require(stray==0,$"Connecting rails paint {stray} pixels through the ornaments' open spaces.");
        var widths=Enumerable.Range(190,160).Select(y=>Enumerable.Range(0,60).Count(x=>a[(y*actual.PixelSize.Width+x)*4+3]>64)).ToArray();
        SharedFrameChecks.Require(widths.Max()-widths.Min()<=1,$"A straight connector changes silhouette width by {widths.Max()-widths.Min()} pixels between repeats.");
    }
    internal static void UnchangedClocks()
    {
        var baseline=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Output,"clock-baseline.json")))!;
        var current=ClockPixels();
        var changed=current.Where(p=>!baseline.TryGetValue(p.Key,out var hash)||hash!=p.Value).Select(p=>p.Key).ToArray();
        SharedFrameChecks.Require(baseline.Count==48&&current.Count==48&&changed.Length==0,"Floating clock pixels changed: "+string.Join(", ",changed));
        Console.WriteLine("All 48 floating clock renders match the pre-change pixels exactly.");
    }
    internal static void PanelLayouts()
    {
        var asset=StopwatchAppearanceAssets.Shared.Get("frame","engraved-filigree");
        foreach(var size in new[]{new Size(460,480),new Size(1120,720),new Size(1400,900)})
        foreach(int percent in new[]{70,90,125})
        {
            double scale=.16*percent/100d;
            var clock=StopwatchFrameRenderer.GetLayout(asset,size,scale);
            var panel=StopwatchFrameRenderer.GetLayout(asset,size,scale,panelFrame:true);
            SharedFrameChecks.Require(!ReferenceEquals(clock,panel),"Panel and clock recipes share a cache entry.");
            SharedFrameChecks.Require(ReferenceEquals(clock,StopwatchFrameRenderer.GetLayout(asset,size,scale)),"Panel rendering replaced cached clock geometry.");
            SharedFrameChecks.Require(ReferenceEquals(panel,StopwatchFrameRenderer.GetLayout(asset,size,scale,true)),"Panel geometry was rebuilt without a size change.");
            SharedFrameChecks.Require(panel.HasExactInteriorMask,"Panel border has an open join; background fell back to a rectangle.");
            var content=new Rect(size).Deflate(panel.ContentInsets);
            foreach(var point in new[]{content.TopLeft,content.TopRight,content.BottomLeft,content.BottomRight,content.Center})
                SharedFrameChecks.Require(panel.BackgroundClip.FillContains(point),"Panel content is clipped by the corrected frame.");
            SharedFrameChecks.Require(!panel.BackgroundClip.FillContains(default),"Panel fill leaked into its outer corner.");
        }
        var tiny=StopwatchFrameRenderer.GetLayout(asset,new Size(48,24),.18,true);
        SharedFrameChecks.Require(tiny.ArtworkScale<.18&&tiny.ArtworkScale>0,"Small panels did not uniformly fit their fixed ornaments.");
        SharedFrameChecks.Require(StopwatchFrameRenderer.CachedLayoutCount<=32,"Panel variants grew the cache without a bound.");
    }
    internal static void SettingsPreview()
    {
        Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        foreach(string uri in new[]{"avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml","avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"})
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri(uri)});
        var assembly=typeof(SettingsWindow).Assembly;
        assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=90};
        var pages=assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!;
        pages.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[settings]);
        var window=new SettingsWindow(settings){Width=1120,Height=720,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Typography"));
            window.Show();ClockOverlayChecks.Pump(100);
            var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
            using var bitmap=SharedFrameChecks.Capture(window.ClientSize,dc=>dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size)));
            bitmap.Save(Path.Combine(Output,"settings-native.png"));
        }
        finally{window.Close();pages.GetMethod("Shutdown",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);}
        SharedFrameUiChecks.Picker();
    }
    static RenderTargetBitmap Panel(Size size,int percent=100)
    {
        var panel=new StopwatchFrameSurface{Width=size.Width,Height=size.Height,ArtworkScale=.16*percent/100d,Background=Paper};
        panel.Measure(size);panel.Arrange(new Rect(size));
        return SharedFrameChecks.Capture(size,panel.Render);
    }
    internal static void CapturePanels(string stage)
    {
        Directory.CreateDirectory(Output);
        foreach(var (name,size) in new[]{("picker",new Size(460,480)),("settings",new Size(1120,720)),("large",new Size(1400,900))})
        foreach(int percent in new[]{70,90,125})
        {using var panel=Panel(size,percent);panel.Save(Path.Combine(Output,$"{stage}-{name}-{percent}.png"));}
        using var frame=Panel(new Size(1120,720),90);
        using var details=SharedFrameChecks.Capture(new Size(1140,500),dc=>
        {
            dc.DrawRectangle(Brush.Parse("#172B35"),null,new Rect(0,0,1140,500));
            var regions=new[]{new Rect(0,0,60,70),new Rect(0,330,40,60),new Rect(0,650,60,70)};
            string[] labels={"Top corner","Middle ornament","Bottom corner"};
            for(int i=0;i<regions.Length;i++)
            {
                dc.DrawText(new FormattedText(labels[i],System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,Typeface.Default,20,Brushes.White),new Point(30+i*380,16));
                dc.DrawImage(frame,regions[i],new Rect(30+i*380,60,regions[i].Width*5,regions[i].Height*5));
            }
        });
        details.Save(Path.Combine(Output,stage+"-details.png"));
    }
}
