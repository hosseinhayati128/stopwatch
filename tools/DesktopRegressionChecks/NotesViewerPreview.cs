using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;

// Isolated design fixture: synthetic entries, no vault, completion writes, hooks or countdown timer.
internal static class NotesViewerPreview
{
    const string Output="design/one-piece-v2/notes/viewer-native-preview";
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#B78329"),Rule=Brush.Parse("#CDB784"),Cream=Brush.Parse("#F8EACA"),Teal=Brush.Parse("#147A83");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    sealed record Entry(string Type,string Day,string Time,string Text,bool Done=false,bool Progress=false,string? Due=null);
    static readonly Entry[] Samples=[
        new("Todo","TODAY · 18 NOV","10:40","Finish the dashboard sketches",Progress:true),
        new("Note","TODAY · 18 NOV","10:10","Keep the weekly summary focused on finished work."),
        new("Reminder","TODAY · 18 NOV","09:15","Review the dashboard sketches",Due:"19 Nov · 18:30"),
        new("Todo","YESTERDAY · 17 NOV","16:20","Back up the project files",Done:true),
        new("Note","YESTERDAY · 17 NOV","12:10","ایده‌های جلسه بعد را یادداشت کنم."),
        new("Todo","YESTERDAY · 17 NOV","09:00","Prepare next week’s reading list")];
    static TextBlock Text(string text,double size=13,IBrush? color=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=color??Ink,VerticalAlignment=VerticalAlignment.Center};
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})Render(scale,"all");
        foreach(string state in new[]{"todos","notes","reminders","empty","unconfigured"})Render(90,state);
    }
    static Border Row(Entry entry)
    {
        Control marker;
        if(entry.Type=="Note")marker=new Avalonia.Controls.Shapes.Path {Width=18,Height=20,Stretch=Stretch.Uniform,Margin=new(1,2,0,0),
            Data=Geometry.Parse("M3,1 L12,1 L17,6 L17,20 L3,20 Z M12,1 L12,6 L17,6 M6,10 L14,10 M6,14 L14,14"),Stroke=Teal,StrokeThickness=1.5};
        else
        {
            var check=new CheckBox {IsChecked=entry.Done,MinWidth=0,MinHeight=0,Padding=new(0),VerticalAlignment=VerticalAlignment.Top,Margin=new(0,1,0,0)};
            check.Resources["CheckBoxCheckBackgroundFillChecked"]=Ink;check.Resources["CheckBoxCheckBorderBrushChecked"]=Gold;check.Resources["CheckBoxCheckGlyphForegroundChecked"]=Cream;
            Avalonia.Automation.AutomationProperties.SetName(check,"Complete "+entry.Text);marker=check;
        }
        marker.VerticalAlignment=VerticalAlignment.Top;
        var content=Text(entry.Text,14,entry.Done?Muted:Ink);content.TextWrapping=TextWrapping.Wrap;AutoTextDirection.Apply(content,entry.Text);
        if(entry.Done)content.TextDecorations=TextDecorations.Strikethrough;
        string detail=entry.Type+(entry.Done?" · Completed":entry.Progress?" · In progress":entry.Due!=null?" · Due "+entry.Due:"");
        var metadata=Text(detail,11,entry.Progress?Teal:Muted);
        var body=new StackPanel {Spacing=5,Children={content,metadata}};
        var time=Text(entry.Time,11,Muted);time.VerticalAlignment=VerticalAlignment.Top;time.Margin=new(12,2,0,0);
        var row=new Grid {ColumnDefinitions=new("24,10,*,Auto"),Children={marker,body,time}};Grid.SetColumn(body,2);Grid.SetColumn(time,3);
        return new Border {Name="ViewerEntry",MinHeight=64,Padding=new(12,10),Margin=new(0,0,0,8),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),Background=Cream,Child=row};
    }
    static void Render(int percent,string state)
    {
        Entry[] entries=state=="unconfigured"?[]:state=="empty"?Samples.Where(e=>e.Type!="Reminder").ToArray():Samples;
        var title=Text("Notes, Todos & Reminders",28);title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var close=new Button {Width=32,Height=32,Padding=new(0),BorderThickness=new(0),Background=Brushes.Transparent,VerticalAlignment=VerticalAlignment.Top,
            Content=new Avalonia.Controls.Shapes.Path {Width=16,Height=16,Stretch=Stretch.Uniform,Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=Ink,StrokeThickness=1.7}};
        var heading=new Grid {ColumnDefinitions=new("*,32"),Children={title,close}};Grid.SetColumn(close,1);
        var subtitle=Text($"{entries.Length} entries · Newest first",12,Muted);
        var countdown=new Border {Padding=new(8,3),CornerRadius=new(3),BorderBrush=Rule,BorderThickness=new(1),Background=Cream,Child=Text("Auto-close · 30s",11,Muted)};
        var subrow=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,4,0,0),Children={subtitle,countdown}};Grid.SetColumn(countdown,1);
        var header=new StackPanel {Children={heading,subrow}};
        var tabs=new StackPanel {Orientation=Orientation.Horizontal,Spacing=6,Margin=new(0,0,0,14)};
        var list=new StackPanel();
        var emptyTitle=Text("No reminders yet",20);emptyTitle.FontWeight=FontWeight.SemiBold;emptyTitle.HorizontalAlignment=HorizontalAlignment.Center;
        var emptyHelp=Text("Your reminders will appear here once you add one.",13,Muted);emptyHelp.TextWrapping=TextWrapping.Wrap;emptyHelp.TextAlignment=TextAlignment.Center;
        var emptyPanel=new StackPanel {Spacing=10,MaxWidth=390,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Children={emptyTitle,emptyHelp}};
        var scroll=new ScrollViewer {Content=list,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
        var body=new Grid {Children={scroll,emptyPanel}};
        var listSurface=new Border {Padding=new(12),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(6),Background=Brush.Parse("#26FFF3D8"),Child=body};
        var footerHint=Text("Enter / Esc close",11,Muted);
        var vault=Text(state=="unconfigured"?"Vault not configured":"Vault: Sample vault / Notes",11,Muted);
        vault.TextTrimming=TextTrimming.CharacterEllipsis;vault.HorizontalAlignment=HorizontalAlignment.Right;
        var footer=new Grid {ColumnDefinitions=new("Auto,16,*"),Margin=new(0,14,0,0),Children={footerHint,vault}};Grid.SetColumn(vault,2);
        var layout=new Grid {RowDefinitions=new("76,Auto,*,Auto"),Children={header,tabs,listSurface,footer}};
        Grid.SetRow(tabs,1);Grid.SetRow(listSurface,2);Grid.SetRow(footer,3);
        var frame=new StopwatchFrameSurface {Width=760,Height=580,ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        var tabButtons=new List<(Button Button,string? Type)>();
        void Select(string? type)
        {
            list.Children.Clear();var visible=entries.Where(e=>type==null||e.Type==type).ToArray();
            subtitle.Text=(type==null?$"{entries.Length} entries":$"{visible.Length} of {entries.Length} entries")+" · Newest first";
            foreach(var (button,kind) in tabButtons)
            {
                bool active=kind==type;button.Background=active?Ink:Brushes.Transparent;((TextBlock)button.Content!).Foreground=active?Brushes.White:Ink;
            }
            foreach(var day in visible.GroupBy(e=>e.Day))
            {
                var date=Text(day.Key,11,Muted);date.Margin=new(2,3,0,8);date.FontWeight=FontWeight.SemiBold;list.Children.Add(date);
                foreach(var entry in day)list.Children.Add(Row(entry));
            }
            emptyPanel.IsVisible=visible.Length==0;scroll.IsVisible=visible.Length>0;scroll.Offset=default;
            emptyTitle.Text=state=="unconfigured"?"Choose a notes folder":type=="Reminder"?"No reminders yet":type=="Todo"?"No todos yet":type=="Note"?"No quick notes yet":"Nothing here yet";
            emptyHelp.Text=state=="unconfigured"?"Select an Obsidian vault in Settings to view your notes, todos, and reminders.":"Your "+(type=="Reminder"?"reminders":type=="Todo"?"todos":"notes")+" will appear here once you add one.";
        }
        foreach(var (label,type) in new (string,string?)[]{("All",null),("Todos","Todo"),("Notes","Note"),("Reminders","Reminder")})
        {
            int count=entries.Count(e=>type==null||e.Type==type);
            var button=new Button {Content=Text($"{label}  {count}",13),Height=34,Padding=new(14,6),CornerRadius=new(4),BorderBrush=Gold,BorderThickness=new(1),
                HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            button.Click+=(_,_)=>Select(type);tabButtons.Add((button,type));tabs.Children.Add(button);
        }
        string? selected=state is "reminders" or "empty"?"Reminder":state=="todos"?"Todo":state=="notes"?"Note":null;Select(selected);
        double scale=percent/100d;
        var window=new Window {Width=760*scale,Height=580*scale,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,
            Background=Brushes.Transparent,RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,Content=new LayoutTransformControl {LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        try
        {
            window.Show();ClockOverlayChecks.Pump(100);
            Require(Math.Abs(window.ClientSize.Width-760*scale)<1.1&&Math.Abs(window.ClientSize.Height-580*scale)<1.1,"Native size mismatch.");
            Require(title.DesiredSize.Width<=title.Bounds.Width+1,"Viewer title clipped.");
            foreach(var (button,_) in tabButtons)Require(((TextBlock)button.Content!).DesiredSize.Width<=button.Bounds.Width-button.Padding.Left-button.Padding.Right+1,"Filter tab clipped.");
            Require(countdown.TranslatePoint(new Point(countdown.Bounds.Width,0),frame)!.Value.X<frame.Bounds.Width-10,"Countdown exceeds frame.");
            Require(footer.TranslatePoint(new Point(0,footer.Bounds.Height),frame)!.Value.Y<frame.Bounds.Height-10,"Footer clipped.");
            double footerY=footer.TranslatePoint(default,frame)!.Value.Y;
            if(state=="all")
            {
                Require(scroll.Extent.Height>scroll.Viewport.Height,"Fixture does not exercise list scrolling.");
                scroll.Offset=new Vector(0,1000);ClockOverlayChecks.Pump(25);
                Require(Math.Abs(footer.TranslatePoint(default,frame)!.Value.Y-footerY)<1,"Scrolling moved footer.");scroll.Offset=default;ClockOverlayChecks.Pump(25);
            }
            foreach(var (button,type) in tabButtons)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(15);
                int count=entries.Count(e=>type==null||e.Type==type);
                Require(list.Children.OfType<Border>().Count()==count&&emptyPanel.IsVisible==(count==0),"Filter rows or empty state mismatch.");
            }
            Select(selected);ClockOverlayChecks.Pump(30);
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
            if(scale>=1)bitmap.Render(window);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            bitmap.Save(Path.Combine(Output,$"notes-viewer-{state}-{percent}.png"));
            Console.WriteLine($"PASS viewer {state} {percent}%: native size, tabs, filtering, empty state and fixed footer");
        }
        finally{window.Close();}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
