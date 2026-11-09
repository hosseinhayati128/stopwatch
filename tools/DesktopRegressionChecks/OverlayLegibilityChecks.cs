using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

internal static class OverlayLegibilityChecks
{
    internal const string Output=".superpowers/sdd/2026-11-06-overlay-legibility";
    // Centers measured from the inner button faces of the shipped source PNGs.
    private static readonly Dictionary<string,Point[]> Centers=new()
    {
        ["filigree-keys"]=[new(698.5,359),new(1085.5,359),new(1472,359)],
        ["classic-brass"]=[new(451.5,381),new(988,381),new(1523.5,381)],
        ["compass-dials"]=[new(482.5,367.5),new(1021.5,367.5),new(1560,368)],
        ["floating-buttons"]=[new(420,338.5),new(1086,338.5),new(1751.5,339)],
        ["pixel-controls"]=[new(484,422.5),new(956.5,422),new(1431,422.5)],
        ["bronze-keys"]=[new(534,350),new(1083,350),new(1633,350)],
        ["wooden-deck"]=[new(532.5,390),new(1000,390),new(1460,389)],
        ["porcelain-keys"]=[new(407.5,365.5),new(990,366),new(1570.5,364)],
        ["vine-filigree"]=[new(651,368.5),new(1002,368.5),new(1354,368.5)],
        ["royal-cartouche"]=[new(536,364.5),new(989.5,364),new(1445,364)]
    };
    internal static void ButtonCenters()
    {
        foreach(var (id,centers) in Centers)
        {
            var row=new StackPanel{Orientation=Orientation.Horizontal};
            var buttons=Enumerable.Range(0,3).Select(_=>new Button()).ToArray();foreach(var b in buttons)row.Children.Add(b);
            var surface=new Border{Child=row,Width=236,Height=56};
            StopwatchToolbarStyler.Apply(surface,buttons,new(){HoverControls=id},new(true,true,false),1);
            var window=new Window{Content=surface,Width=236,Height=56,ShowActivated=false,ShowInTaskbar=false};
            try
            {
                window.Show();ClockOverlayChecks.Pump(20);surface.Measure(new Size(236,56));surface.Arrange(new Rect(0,0,236,56));
                var asset=StopwatchAppearanceAssets.Shared.Get("control",id);var fitted=StopwatchAppearanceAssets.Fit(asset,new Size(236,56));
                for(int i=0;i<3;i++)
                {
                    var actual=buttons[i].TranslatePoint(new Point(17,17),surface)!.Value;
                    var expected=new Point(fitted.X+(centers[i].X-asset.Source.X)/asset.Source.Width*fitted.Width,
                        fitted.Y+(centers[i].Y-asset.Source.Y)/asset.Source.Height*fitted.Height);
                    double distance=Math.Sqrt(Math.Pow(actual.X-expected.X,2)+Math.Pow(actual.Y-expected.Y,2));
                    if(distance>.8)throw new Exception($"{id} button {i+1} is {distance:F2} DIP away from its artwork center.");
                }
            }
            finally{window.Close();}
        }
    }
    private static ThemedClockFace Face(string background,string color)=>Create(background,color);
    private static ThemedClockFace Create(string background,string color)
    {
        var face=new ThemedClockFace{Text="00:44:27",ProjectName="Navigation"};
        face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,TextColor=color,TextSize=27,
            StopwatchAppearance=new(){Background=background,BackgroundOpacity=70}});
        face.SetStopwatchPresentationState(new(true,true,false));return face;
    }
    internal static void BackgroundColors()
    {
        foreach(var background in StopwatchAppearanceCatalog.Backgrounds)
        {
            var light=Face(background.Id,"White");light.Measure(Size.Infinity);light.Arrange(new Rect(light.DesiredSize));
            var themed=Face(background.Id,"Theme default");themed.Measure(Size.Infinity);themed.Arrange(new Rect(themed.DesiredSize));
            using var a=SharedFrameChecks.Capture(light.Bounds.Size,light.Render);
            using var b=SharedFrameChecks.Capture(themed.Bounds.Size,themed.Render);
            if(!SharedFrameChecks.Pixels(a).SequenceEqual(SharedFrameChecks.Pixels(b)))
                throw new Exception($"The legacy White text setting overrides {background.Id}'s Nika palette.");
        }
    }
    internal static void Capture(string label)
    {
        Directory.CreateDirectory(Output);
        var board=new Grid{Width=1200,Height=1040,Background=Brush.Parse("#182B38"),ColumnDefinitions=new("*,*,*"),RowDefinitions=new("*,*,*,*")};
        int index=0;
        foreach(var choice in StopwatchAppearanceCatalog.Backgrounds)
        {
            var card=new StackPanel{Spacing=8,Margin=new(12)};
            card.Children.Add(new TextBlock{Text=choice.Label,FontSize=16,Foreground=Brushes.White});
            foreach(var desktop in new[]{"#182B38","#E8E6E1"})
            {
                var face=Face(choice.Id,"White");
                card.Children.Add(new Border{Background=Brush.Parse(desktop),Padding=new(8),Child=face,HorizontalAlignment=HorizontalAlignment.Center});
            }
            Grid.SetColumn(card,index%3);Grid.SetRow(card,index/3);board.Children.Add(card);index++;
        }
        var window=new Window{Content=board,Width=1200,Height=1040,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(60);board.Measure(new Size(1200,1040));board.Arrange(new Rect(0,0,1200,1040));
            using var bitmap=new RenderTargetBitmap(new PixelSize(1200,1040),new Vector(96,96));bitmap.Render(board);bitmap.Save(Path.Combine(Output,label+"-backgrounds.png"));
        }
        finally{window.Close();}
        ToolbarGlyphChecks.Capture(label,Output);
        if(label=="after")CaptureTypography();
    }

    private static void CaptureTypography()
    {
        var board=new Grid{Width=1080,Height=1500,Background=Brush.Parse("#182B38"),ColumnDefinitions=new("*,*,*"),
            RowDefinitions=new("*,*,*,*,*,*,*,*,*,*")};
        int row=0;
        foreach(var typography in StopwatchAppearanceCatalog.Typography)
        {
            int column=0;
            foreach(string background in new[]{"aged-bronze","tideglass","sky-island"})
            {
                var face=new ThemedClockFace{Text="00:44:27",ProjectName="Navigation"};
                face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,TextColor="White",TextSize=27,
                    StopwatchAppearance=new(){Background=background,Typography=typography.Id,BackgroundOpacity=70}});
                face.SetStopwatchPresentationState(new(false,false,false));
                var card=new StackPanel{Margin=new(8),Spacing=4};
                card.Children.Add(new TextBlock{Text=typography.Label+" · "+background,Foreground=Brushes.White,FontSize=12});
                card.Children.Add(face);Grid.SetColumn(card,column++);Grid.SetRow(card,row);board.Children.Add(card);
            }
            row++;
        }
        var window=new Window{Content=board,Width=1080,Height=1500,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(60);board.Measure(new Size(1080,1500));board.Arrange(new Rect(0,0,1080,1500));
            using var bitmap=new RenderTargetBitmap(new PixelSize(1080,1500),new Vector(96,96));bitmap.Render(board);
            bitmap.Save(Path.Combine(Output,"after-textured-typography.png"));
        }
        finally{window.Close();}
    }
}
