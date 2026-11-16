using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Design-only native rendering; no hooks, live settings or command dispatch.
internal static class NoteCommandsPreview
{
    const string Output = "design/one-piece-v2/shortcuts/note-commands-native-preview";
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539");
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new() {
        Text=value, FontSize=size, Foreground=brush??Ink, FontFamily=StopwatchTypography.Resolve("modern-deck").NameFont,
        VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.NoWrap };
    static Border Badge(string value, bool dark=false)
    {
        var label=Text(value,11,dark?Brushes.White:Ink); label.FontWeight=FontWeight.SemiBold;
        label.HorizontalAlignment=HorizontalAlignment.Center;
        return new Border { Background=Brush.Parse(dark?"#102B3B":"#F8EACA"), BorderBrush=Brush.Parse("#B78329"),
            BorderThickness=new(1), CornerRadius=new(3), Padding=new(4,1), Child=label };
    }
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach(int percent in new[]{70,90,125})Render(percent);
    }
    static void Render(int percent)
    {
        var keys=AppSettings.DefaultNoteCommandModeKeys();
        var title=Text("Note Commands",18);title.FontWeight=FontWeight.Bold;
        title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
        var leader=Badge(AppSettings.DefaultNoteLeaderShortcut().Format().Replace("+"," + "),true);
        leader.VerticalAlignment=VerticalAlignment.Top;
        var header=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,0,0,7),Children={title,leader}};Grid.SetColumn(leader,1);
        var body=new Grid {ColumnDefinitions=new("*,12,*,12,*"),RowDefinitions=new("Auto,4,Auto")};
        var entries=new[]{(Shortcut.FormatKeyName(keys[NoteCommandAction.AddTodo]),"Add Todo"),
            (Shortcut.FormatKeyName(keys[NoteCommandAction.AddNote]),"Quick note"),
            (Shortcut.FormatKeyName(keys[NoteCommandAction.AddReminder]),"Add reminder"),
            (Shortcut.FormatKeyName(keys[NoteCommandAction.ViewNotes]),"View notes"),
            (Shortcut.FormatKeyName(keys[NoteCommandAction.ToggleTodoOverlay]),"Show / hide todos"),
            ("Space","Expand / collapse")};
        foreach(var (entry,index) in entries.Select((entry,index)=>(entry,index)))
        {
            var key=Badge(entry.Item1);key.MinWidth=44;var label=Text(entry.Item2);
            var row=new Grid {ColumnDefinitions=new("Auto,8,*"),MinHeight=21,Children={key,label}};
            Grid.SetColumn(label,2);Grid.SetColumn(row,index%3*2);Grid.SetRow(row,index/3*2);body.Children.Add(row);
        }
        var context=Text("Space expands / collapses Todos",11,Muted);var escape=Text("Esc cancels",11,Muted);
        var footer=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,7,0,0),Children={context,escape}};Grid.SetColumn(escape,1);
        var layout=new Grid {RowDefinitions=new("Auto,*,Auto"),Children={header,body,footer}};Grid.SetRow(body,1);Grid.SetRow(footer,2);
        var frame=new Border {Width=620,Height=132,Padding=new(12,10),CornerRadius=new(6),BorderThickness=new(1),
            BorderBrush=Brush.Parse("#B78329"),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=percent/100d;
        var w=new Window {Width=620*scale,Height=132*scale,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,
            Background=Brushes.Transparent,Content=new LayoutTransformControl {LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        try
        {
            w.Show();ClockOverlayChecks.Pump(120);
            if(Math.Abs(w.ClientSize.Width-620*scale)>1||Math.Abs(w.ClientSize.Height-132*scale)>1)throw new Exception("Native window size mismatch.");
            foreach(var row in body.Children.Cast<Grid>())
            {
                var label=(TextBlock)row.Children[1];
                var edge=row.TranslatePoint(new Point(row.Bounds.Width,row.Bounds.Height),frame)!.Value;
                if(label.DesiredSize.Width>label.Bounds.Width+1||edge.X>frame.Bounds.Width-12||edge.Y>footer.TranslatePoint(default,frame)!.Value.Y)
                    throw new Exception("Command text clipped or overlaps footer.");
            }
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
            if(scale>=1)bitmap.Render(w);
            else {using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            bitmap.Save(Path.Combine(Output,$"note-commands-{percent}.png"));
            Console.WriteLine($"PASS Note Commands {percent}%: six controls, native sizing, text bounds and footer separation");
        }
        finally {w.Close();}
    }
}
