using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

internal static class ToolbarGlyphChecks
{
    internal static void Capture(string label,string? output=null)
    {
        string directory=Path.GetFullPath(output??".superpowers/sdd/2026-11-05-hover-symbols");
        Directory.CreateDirectory(directory);
        var board=new Grid{Width=810,Height=960,Background=Brush.Parse("#142633"),
            ColumnDefinitions=new("*,*,*"),RowDefinitions=new("*,*,*,*")};
        int index=0;
        foreach(var choice in StopwatchAppearanceCatalog.HoverControls)
        {
            var card=new StackPanel{Margin=new(12),Spacing=6};
            card.Children.Add(new TextBlock{Text=choice.Label,Foreground=Brushes.White,FontSize=15});
            foreach(bool running in new[]{true,false})
            {
                card.Children.Add(new TextBlock{Text=running?"Running · pause":"Paused · resume",Foreground=Brush.Parse("#AFC5CD"),FontSize=11});
                var buttons=Enumerable.Range(0,3).Select(_=>new Button()).ToArray();
                var row=new StackPanel{Orientation=Orientation.Horizontal};foreach(var button in buttons)row.Children.Add(button);
                var surface=new Border{Child=row,Width=236,Height=56,HorizontalAlignment=HorizontalAlignment.Left};
                StopwatchToolbarStyler.Apply(surface,buttons,new StopwatchAppearanceSettings{HoverControls=choice.Id},new(true,running,false),1);
                card.Children.Add(surface);
            }
            Grid.SetColumn(card,index%3);Grid.SetRow(card,index/3);board.Children.Add(card);index++;
        }
        var window=new Window{Width=810,Height=960,Content=board,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(80);board.Measure(new Size(810,960));board.Arrange(new Rect(0,0,810,960));
            foreach(int scale in new[]{1,2})
            {
                using var bitmap=new RenderTargetBitmap(new PixelSize(810*scale,960*scale),new Vector(96*scale,96*scale));
                bitmap.Render(board);bitmap.Save(Path.Combine(directory,$"{label}-{scale}x.png"));
            }
        }
        finally{window.Close();}
    }
}
