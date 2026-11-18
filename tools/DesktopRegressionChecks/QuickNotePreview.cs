using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;

// Isolated design fixture: no persistence, hooks or live settings.
internal static class QuickNotePreview
{
    const string Output = "design/one-piece-v2/notes/quick-note-native-preview";
    static readonly IBrush Ink=Brush.Parse("#102B3B"), Muted=Brush.Parse("#655539"), Gold=Brush.Parse("#B78329");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string text,double size,IBrush? brush=null)=>new() {
        Text=text,FontSize=size,FontFamily=Body,Foreground=brush??Ink,VerticalAlignment=VerticalAlignment.Center };
    static Button Action(string text,bool primary=false)=>new() {
        Content=Text(text,13,primary?Brushes.White:Ink),Height=38,MinWidth=88,Padding=new(16,6),
        HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,
        Background=primary?Ink:Brushes.Transparent,BorderBrush=Gold,BorderThickness=new(1),CornerRadius=new(4) };
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int percent in new[]{70,90,125})Render(percent);
    }
    static void Render(int percent)
    {
        var title=Text("Quick Note",28);title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var heading=new StackPanel {Spacing=4,Children={title,Text("Capture a thought. Keep your flow.",12,Muted)}};
        var close=Action("");close.MinWidth=32;close.Width=32;close.Height=32;close.Padding=new(0);close.BorderThickness=new(0);
        close.VerticalAlignment=VerticalAlignment.Top;
        close.Content=new Avalonia.Controls.Shapes.Path {Width=16,Height=16,Stretch=Stretch.Uniform,
            Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=Ink,StrokeThickness=1.7};
        var header=new Grid {ColumnDefinitions=new("*,32"),Children={heading,close}};Grid.SetColumn(close,1);
        var input=new TextBox {AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Watermark="Write a quick note…",
            FontFamily=Body,FontSize=14,Foreground=Ink,Background=Brush.Parse("#F8EACA"),BorderBrush=Brush.Parse("#CDB784"),
            BorderThickness=new(1),CornerRadius=new(5),Padding=new(14,12),VerticalContentAlignment=VerticalAlignment.Top};
        var cancel=Action("Cancel");var save=Action("Save note",true);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8,Children={cancel,save}};
        var hint=Text("Ctrl+Enter save  ·  Esc cancel",11,Muted);
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),Children={hint,actions}};Grid.SetColumn(actions,1);
        var layout=new Grid {RowDefinitions=new("66,*,16,38"),Children={header,input,footer}};Grid.SetRow(input,1);Grid.SetRow(footer,3);
        var frame=new StopwatchFrameSurface {Width=620,Height=360,ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=percent/100d;
        var window=new Window {Width=620*scale,Height=360*scale,ShowActivated=false,ShowInTaskbar=false,
            SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,
            Content=new LayoutTransformControl {LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        try
        {
            window.Show();ClockOverlayChecks.Pump(120);
            if(Math.Abs(window.ClientSize.Width-620*scale)>1||Math.Abs(window.ClientSize.Height-360*scale)>1)
                throw new Exception("Native window size mismatch.");
            if(input.Bounds.Height<150||hint.DesiredSize.Width>hint.Bounds.Width+1||
                input.TranslatePoint(new Point(0,input.Bounds.Height),frame)!.Value.Y>=footer.TranslatePoint(default,frame)!.Value.Y)
                throw new Exception("Editor or footer clipped.");
            foreach(var control in new Control[]{close,cancel,save})
            {
                var corner=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),frame)!.Value;
                if(corner.X>frame.Bounds.Width-10||corner.Y>frame.Bounds.Height-10)throw new Exception("Action outside frame.");
            }
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
            if(scale>=1)bitmap.Render(window);
            else {using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            bitmap.Save(Path.Combine(Output,$"quick-note-{percent}.png"));
            Console.WriteLine($"PASS Quick Note {percent}%: native sizing, editor space, footer separation and action bounds.");
        }
        finally {window.Close();}
    }
}
