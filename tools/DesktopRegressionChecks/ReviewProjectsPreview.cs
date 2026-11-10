using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design spike: synthetic values, shared renderer, no production services or save handlers.
internal static class ReviewProjectsPreview
{
    private const string Output="design/one-piece-v2/activity-review/projects-native-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),Cream=Brush.Parse("#F6E8C9"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2E756D");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        Application.Current!.Styles.Add(new FluentTheme());
        var measures=new List<object>();
        foreach(var state in new[]{"selected","expanded-activities","activity-offline","empty-selection"})
        {
            bool empty=state=="empty-selection",offline=state=="activity-offline",expanded=state=="expanded-activities";
            var heading=new StackPanel{Spacing=5,Children={
                new TextBlock{Text="Activity & Time Review",FontFamily=Type.NameFont,FontWeight=Type.NameWeight,FontSize=30,Foreground=Navy},
                Label("Choose projects in the order you worked on them.",12),
                new StackPanel{Orientation=Orientation.Horizontal,Spacing=14,Margin=new(0,6,0,0),Children={
                    Pill("01  Projects & activity",Navy,Cream),Label("02  Allocate time",11)}}}};
            var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),HorizontalAlignment=HorizontalAlignment.Right,
                Children={Label("TODAY · 14:00–14:30",10),Text("30 minutes",18)}};
            var close=Action("",28);close.Height=28;close.Padding=new(6);close.Content=Icon("M2,2 L14,14 M14,2 L2,14",Navy,16);
            close.BorderBrush=Brushes.Transparent;close.Background=Brushes.Transparent;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
            var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

            var projects=new Grid{ColumnDefinitions=new("*,8,*"),RowDefinitions=new("48,8,48,8,48,8,48")};
            string[] names=["Programming","Illustration","Reading","Learning","Writing","Art","Research","Break / Empty"];
            for(int i=0;i<names.Length;i++)
            {
                var positions=empty?"":i==0?"1, 4":i==1?"3":i==7?"2":"";
                var card=Project(names[i],positions);projects.Children.Add(card);Grid.SetColumn(card,(i%2)*2);Grid.SetRow(card,(i/2)*2);
            }
            var projectScroll=Scroll(projects);projectScroll.Margin=new(0,0,5,0);
            var projectHead=new StackPanel{Spacing=4,Children={Text("Projects",20),Label("Click to add a segment. Repeat a project to add it again.",11)}};
            var draft=new TextBox{Watermark="New project name",Height=34,MinHeight=0,Padding=new(9,5),FontSize=12,FontFamily=Body};draft.Classes.Add("editorField");
            var add=Action("Add",56);add.Height=34;
            var create=new Grid{ColumnDefinitions=new("*,8,56"),Margin=new(0,12,0,0),Children={draft,add}};Grid.SetColumn(add,2);
            var projectLayout=new Grid{RowDefinitions=new("58,*,46"),Children={projectHead,projectScroll,create}};Grid.SetRow(projectScroll,1);Grid.SetRow(create,2);

            var activityTitle=new StackPanel{Spacing=4,Children={Text("Activity breakdown",20),Label("Apps, websites & time away",11)}};
            var filter=new StackPanel{Spacing=5,HorizontalAlignment=HorizontalAlignment.Right,Children={Label("GROUP BELOW",9)}};
            var minus=Action("−",25);minus.Height=25;minus.Padding=new(0);var plus=Action("+",25);plus.Height=25;plus.Padding=new(0);
            filter.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={minus,Text("3%",12),plus}});
            var activityHead=new Grid{ColumnDefinitions=new("*,Auto"),Children={activityTitle,filter}};Grid.SetColumn(filter,1);
            var activityList=new StackPanel{Spacing=8};
            if(offline)
            {
                var retry=Action("Retry connection",142);
                activityList.Children.Add(new Border{Padding=new(18),Background=Cream,BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(6),Child=
                    new StackPanel{Spacing=12,Children={Text("ActivityWatch is unavailable",17),
                        Label("Activity details could not be loaded for this period. You can still choose projects and continue.",12),retry,
                        Label("Your project sequence stays here while you retry.",11)}}});
                filter.IsEnabled=false;
            }
            else
            {
                activityList.Children.Add(Activity("VS Code","Application","15m",50,Navy));
                activityList.Children.Add(Activity("Chrome","Web","9m",30,Teal));
                activityList.Children.Add(Activity("Away from keyboard","Idle","4m",13.333,Brush.Parse("#A46621")));
                var other=new StackPanel{Children={Activity("Other activities","3 activities · each below 3%","2m",6.667,Brush.Parse("#7D6A48"),true,expanded)}};
                if(expanded)
                {
                    var details=new StackPanel{Spacing=9};
                    foreach(var (name,time) in new[]{("Terminal","45s"),("Files","45s"),("Music","30s")})
                    {var row=new Grid{ColumnDefinitions=new("*,Auto"),Children={Label(name,11),Text(time,11)}};Grid.SetColumn(row.Children[1],1);details.Children.Add(row);}
                    other.Children.Add(new Border{Padding=new(11,0,11,10),Background=Cream,CornerRadius=new(0,0,5,5),Child=details});
                }
                activityList.Children.Add(other);
            }
            var activityScroll=Scroll(activityList);activityScroll.Margin=new(0,0,5,0);
            var activityNote=Label(offline?"Manual review is still available.":"Percentages use the full 30-minute review period.",10,new(0,7,0,0));
            var activityLayout=new Grid{RowDefinitions=new("62,*,24"),Children={activityHead,activityScroll,activityNote}};Grid.SetRow(activityScroll,1);Grid.SetRow(activityNote,2);
            var left=Pane(projectLayout);var right=Pane(activityLayout);
            var panes=new Grid{ColumnDefinitions=new("*,14,1.08*"),Margin=new(0,8,0,12),Children={left,right}};Grid.SetColumn(right,2);

            var sequence=new Grid{ColumnDefinitions=new("*,8,*,8,*,8,*")};
            string[] selectedNames=["Programming","Break / Empty","Illustration","Programming"];
            string[] ranges=["14:00–14:12","14:12–14:15","14:15–14:24","14:24–14:30"];
            if(!empty)
            {
                for(int i=0;i<selectedNames.Length;i++)
                {var chip=Segment(i+1,selectedNames[i],ranges[i]);sequence.Children.Add(chip);Grid.SetColumn(chip,i*2);}
            }
            else
            {
                var hint=new Border{Padding=new(12,0),Background=Brush.Parse("#38FFF3D8"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),
                    Child=Label("Add your first project above. Include Break / Empty for time away.",12)};
                sequence.Children.Add(hint);Grid.SetColumnSpan(hint,7);
            }
            var next=Action("Continue to time",156,true);next.IsEnabled=!empty;
            var skip=Action("Skip",66);
            var footerActions=new Grid{ColumnDefinitions=new("66,12,*,156"),Children={skip,Label("Esc skip · × remove segment",10),next}};Grid.SetColumn(footerActions.Children[1],2);Grid.SetColumn(next,3);
            var footer=new Grid{RowDefinitions=new("24,50,12,38"),Margin=new(0,5,0,0),Children={
                new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},
                Label(empty?"YOUR SEQUENCE · NO SEGMENTS":"YOUR SEQUENCE · 4 SEGMENTS",10),sequence,footerActions}};
            Grid.SetRow(sequence,1);Grid.SetRow(footerActions,3);
            var layout=new Grid{RowDefinitions=new("92,*,134"),Children={header,panes,footer}};Grid.SetRow(panes,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=1020,Height=700,Content=frame,ShowActivated=false,ShowInTaskbar=false,
                SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,FontFamily=Body,Foreground=Navy,
                RequestedThemeVariant=ThemeVariant.Light,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!
                .GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            try
            {
                window.Show();ClockOverlayChecks.Pump(55);
                Require(Math.Abs(frame.Bounds.Width-1020)<1&&Math.Abs(frame.Bounds.Height-700)<1,"Native host clipped the frame.");
                Require(left.Bounds.Width>400&&right.Bounds.Width>400,"Review panes collapsed.");
                Require(projectScroll.Extent.Height<=projectScroll.Viewport.Height+1,"Default project grid clipped.");
                if(!expanded)Require(activityScroll.Extent.Height<=activityScroll.Viewport.Height+1,"Default activity list clipped.");
                Require(next.IsEnabled==!empty,"Continue availability disagrees with the selected sequence.");
                Require(next.TranslatePoint(default,frame)!.Value.Y+next.Bounds.Height<frame.Bounds.Height-15,"Footer clipped.");
                if(expanded){activityScroll.Offset=new(0,1000);ClockOverlayChecks.Pump(25);}
                Save(frame,state);
                measures.Add(new{state,width=frame.Bounds.Width,height=frame.Bounds.Height,projectPane=left.Bounds.Width,
                    activityPane=right.Bounds.Width,projectContent=projectScroll.Extent.Height,projectViewport=projectScroll.Viewport.Height,
                    activityContent=activityScroll.Extent.Height,activityViewport=activityScroll.Viewport.Height,continueEnabled=next.IsEnabled});
                if(expanded)
                {
                    window.Height=520;ClockOverlayChecks.Pump(30);
                    Require(projectScroll.Extent.Height>projectScroll.Viewport.Height&&activityScroll.Extent.Height>activityScroll.Viewport.Height,"Short window needs independent scrolling.");
                    var y=next.TranslatePoint(default,frame)!.Value.Y;
                    projectScroll.Offset=new(0,1000);activityScroll.Offset=new(0,1000);ClockOverlayChecks.Pump(25);
                    Require(Math.Abs(next.TranslatePoint(default,frame)!.Value.Y-y)<1&&y+next.Bounds.Height<frame.Bounds.Height-15,"Scrolling moved or clipped the footer.");
                    Save(frame,"short-window");
                }
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,
            shortHeight=520,independentScrolling=true,fixedSequenceAndActions=true,frame="engraved-filigree",artworkScale=.16,newRuntimeArtwork=0,
            execution="Private desktop; native Avalonia controls and shared renderer/styles; synthetic values, no production services or save handlers."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured project sequence, activity grouping, offline, empty and short-window states; native frame, independent scrolling and fixed actions verified.");
    }

    private static TextBlock Label(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=Muted,
        TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock Text(string text,double size)=>new(){Text=text,FontFamily=Body,FontSize=size,FontWeight=FontWeight.SemiBold,Foreground=Navy,VerticalAlignment=VerticalAlignment.Center};
    private static Border Pill(string text,IBrush background,IBrush foreground)=>new(){Background=background,CornerRadius=new(4),Padding=new(9,5),Child=new TextBlock{Text=text,FontFamily=Body,FontSize=10,Foreground=foreground}};
    private static Border Pane(Control content)=>new(){Padding=new(14),Background=Brush.Parse("#30FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static ScrollViewer Scroll(Control content)=>new(){Content=content,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private static Button Action(string text,double width,bool primary=false)
    {var b=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};b.Classes.Add("editorAction");if(primary)b.Classes.Add("editorPrimary");return b;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,IBrush brush,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,
        Data=Geometry.Parse(path),Stroke=brush,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static Button Project(string name,string positions)
    {
        bool selected=positions.Length>0;var label=Text(name,12);label.Foreground=selected?Cream:Navy;label.TextTrimming=TextTrimming.CharacterEllipsis;
        Control mark=selected?Pill(positions,Gold,Navy):Icon("M8,2 L8,14 M2,8 L14,8",Muted,12);
        var content=new Grid{ColumnDefinitions=new("*,8,Auto"),Children={label,mark}};Grid.SetColumn(mark,2);
        var button=Action("",double.NaN);button.Height=48;button.Padding=new(10,0);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        button.Content=content;button.Background=selected?Navy:Cream;button.BorderBrush=selected?Gold:Rule;button.BorderThickness=new(1);return button;
    }
    private static Border Activity(string name,string category,string duration,double percentage,IBrush bar,bool grouped=false,bool expanded=false)
    {
        var amount=Text($"{duration}   ·   {percentage:0}%",12);var label=Text(name,13);label.TextTrimming=TextTrimming.CharacterEllipsis;
        var title=new Grid{ColumnDefinitions=new(grouped?"*,8,Auto,20":"*,8,Auto"),Children={label,amount}};Grid.SetColumn(amount,2);
        if(grouped){var chevron=Icon(expanded?"M3,5 L8,10 L13,5":"M5,3 L10,8 L5,13",Muted,9);title.Children.Add(chevron);Grid.SetColumn(chevron,3);}
        var progress=new Grid{ColumnDefinitions=new($"{percentage.ToString(System.Globalization.CultureInfo.InvariantCulture)}*,{(100-percentage).ToString(System.Globalization.CultureInfo.InvariantCulture)}*"),Height=4,
            Background=Brush.Parse("#249A7B48"),Children={new Border{Background=bar,CornerRadius=new(2)}}};
        return new Border{Padding=new(11,8),Background=Cream,CornerRadius=new(5),Child=new StackPanel{Spacing=5,Children={title,Label(category,10),progress}}};
    }
    private static Border Segment(int order,string name,string range)
    {
        var number=Pill(order.ToString(),Navy,Cream);number.VerticalAlignment=VerticalAlignment.Center;
        var label=Text(name,11);label.TextTrimming=TextTrimming.CharacterEllipsis;
        var words=new StackPanel{Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children={label,Label(range,10)}};
        var remove=Action("",22);remove.Height=24;remove.Padding=new(6);remove.Background=Brushes.Transparent;remove.BorderBrush=Brushes.Transparent;remove.Content=Icon("M2,2 L14,14 M14,2 L2,14",Muted,10);
        var row=new Grid{ColumnDefinitions=new("Auto,7,*,22"),Children={number,words,remove}};Grid.SetColumn(words,2);Grid.SetColumn(remove,3);
        return new Border{Padding=new(7),Background=Cream,BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),Child=row};
    }
    private static void Save(Control control,string name)
    {using var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));image.Render(control);image.Save(System.IO.Path.Combine(Output,name+".png"));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
