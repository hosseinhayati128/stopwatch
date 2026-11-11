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
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design spike: native controls with synthetic values; no app lifetime, activity queries or save handlers.
internal static class ReviewAllocationPreview
{
    private const string Output="design/one-piece-v2/activity-review/allocation-native-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),Cream=Brush.Parse("#F6E8C9"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2E756D"),
        Ochre=Brush.Parse("#8B5C24"),Gap=Brush.Parse("#DDC599");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private sealed record Segment(string Name,int Start,int Minutes,bool Recorded=false,bool Break=false,bool Gap=false,string Part="")
    {
        internal IBrush Ink=>Gap?ReviewAllocationPreview.Gap:Recorded?Teal:Break?Ochre:Navy;
        internal string Range=>$"{Time(Start)}–{Time(Start+Minutes)}";
    }

    internal static void Run()
    {
        Directory.CreateDirectory(Output);Application.Current!.Styles.Add(new FluentTheme());
        var measures=new List<object>();
        foreach(var state in new[]{"allocation-overview","exact-time-expanded","split-around-recorded","stop-at-recorded"})
        {
            bool expanded=state=="exact-time-expanded",split=state=="split-around-recorded",stop=state=="stop-at-recorded",feelings=!expanded;
            Segment[] segments=split?[new("Programming",0,10,Part:"Part 1 of 2"),new("Illustration",10,10,true),new("Programming",20,8,Part:"Part 2 of 2"),new("Unallocated",28,2,Gap:true)]:
                stop?[new("Programming",0,10),new("Illustration",10,10,true),new("Unallocated",20,10,Gap:true)]:
                [new("Programming",0,12),new("Illustration",12,10,true),new("Break / Empty",22,expanded?8:3,Break:true),..expanded?Array.Empty<Segment>():[new("Unallocated",25,5,Gap:true)]];
            Require(segments.Sum(s=>s.Minutes)==30&&segments.Select((s,i)=>i==0?s.Start==0:s.Start==segments[i-1].Start+segments[i-1].Minutes).All(x=>x),"Fixture must cover the 30-minute period without overlaps.");
            int unallocated=segments.Where(s=>s.Gap).Sum(s=>s.Minutes);
            var heading=new StackPanel{Spacing=5,Children={
                new TextBlock{Text="Activity & Time Review",FontFamily=Type.NameFont,FontWeight=Type.NameWeight,FontSize=30,Foreground=Navy},
                Label("Allocate time and check the order of your sessions.",12),
                new StackPanel{Orientation=Orientation.Horizontal,Spacing=14,Margin=new(0,6,0,0),Children={Label("01  Projects & activity",11),Pill("02  Allocate time",Navy,Cream)}}}};
            var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),HorizontalAlignment=HorizontalAlignment.Right,Children={Label("TODAY · 14:00–14:30",10),Text("30 minutes",18)}};
            var close=IconButton("M2,2 L14,14 M14,2 L2,14",28,16);close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
            var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

            var policy=new ComboBox{ItemsSource=new[]{"Split & continue","Stop at recorded time"},SelectedIndex=stop?1:0,Height=28,MinHeight=0,FontFamily=Body,FontSize=11,Padding=new(8,3),HorizontalAlignment=HorizontalAlignment.Stretch};policy.Classes.Add("editorField");
            var top=new Grid{ColumnDefinitions=new("*,114,174"),Children={Text($"{30-unallocated} of 30 min allocated",15),Label("At recorded time",10),policy}};Grid.SetColumn(top.Children[1],1);Grid.SetColumn(policy,2);
            var bar=new Grid{Height=25};
            for(int i=0;i<segments.Length;i++)
            {
                var s=segments[i];bar.ColumnDefinitions.Add(new ColumnDefinition(s.Minutes,GridUnitType.Star));
                var segment=new Border{Background=s.Ink,BorderBrush=Paper,BorderThickness=new(0,0,i==segments.Length-1?0:2,0),Child=
                    new TextBlock{Text=s.Gap?$"{s.Minutes}m gap":$"{i+1} · {s.Minutes}m",FontFamily=Body,FontSize=11,FontWeight=FontWeight.SemiBold,Foreground=s.Gap?Navy:Cream,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
                ToolTip.SetTip(segment,$"{s.Name} · {s.Range}");bar.Children.Add(segment);Grid.SetColumn(segment,i);
            }
            var ticks=new Grid{Children={Label("14:00",9),Label("14:15",9),Label("14:30",9)}};ticks.Children[1].HorizontalAlignment=HorizontalAlignment.Center;ticks.Children[2].HorizontalAlignment=HorizontalAlignment.Right;
            string note=split?"Programming keeps 18m: 10m before the recorded session, then 8m after.":stop?"Programming stops at 14:10. Its remaining 8 requested minutes are not allocated.":
                unallocated>0?$"{unallocated}m unallocated · Gaps and breaks are left out of project history.":"All 30 minutes allocated · Review the timeline before saving.";
            var status=Label(note,11);status.Foreground=unallocated>0?Ochre:Teal;
            var summary=new Border{Padding=new(12,9),Background=Brush.Parse("#42FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Margin=new(0,6,0,6),Child=
                new StackPanel{Spacing=4,Children={top,bar,ticks,status}}};

            var editors=new StackPanel{Spacing=8};int order=0;
            foreach(var s in segments.Where(s=>!s.Gap))editors.Children.Add(Editor(s,++order,expanded&&order==1,split&&order==1,stop&&order==1));
            var editorScroll=Scroll(editors);
            var add=Action("+ Add session",100);add.Height=30;
            var editorHead=new Grid{ColumnDefinitions=new("*,100"),Children={Text("Sessions",20),add}};Grid.SetColumn(add,1);
            var editorLayout=new Grid{RowDefinitions=new("42,*"),Children={editorHead,editorScroll}};Grid.SetRow(editorScroll,1);
            var timeline=new StackPanel{Spacing=7};order=0;
            foreach(var s in segments)timeline.Children.Add(TimelineRow(s,s.Gap?0:++order));
            var timelineScroll=Scroll(timeline);
            var timelineHead=new StackPanel{Spacing=3,Children={Text("Timeline",20),Label("Sessions, breaks and remaining time",11)}};
            var timelineLayout=new Grid{RowDefinitions=new("48,*,25"),Children={timelineHead,timelineScroll,Label("Drag manual blocks above to reposition them.",10)}};Grid.SetRow(timelineScroll,1);Grid.SetRow(timelineLayout.Children[2],2);
            var left=Pane(editorLayout);var right=Pane(timelineLayout);
            var panes=new Grid{ColumnDefinitions=new("1.15*,14,*"),Margin=new(0,7,0,8),Children={left,right}};Grid.SetColumn(right,2);

            var back=Action("Back",82);var saveOnly=Action("Save time only",128);saveOnly.IsVisible=feelings;
            var primary=Action(feelings?"Continue to feelings":"Verify & save",176,true);
            var footerActions=new Grid{ColumnDefinitions=new("82,12,*,128,8,176"),Children={back,Label(feelings?"Esc back · Enter continue":"Esc back · Enter save",10),saveOnly,primary}};
            Grid.SetColumn(footerActions.Children[1],2);Grid.SetColumn(saveOnly,3);Grid.SetColumn(primary,5);
            var footer=new Grid{RowDefinitions=new("18,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},footerActions}};Grid.SetRow(footerActions,1);
            var layout=new Grid{RowDefinitions=new("84,120,*,66"),Children={header,summary,panes,footer}};Grid.SetRow(summary,1);Grid.SetRow(panes,2);Grid.SetRow(footer,3);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=1020,Height=700,Content=frame,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,
                Background=Brushes.Transparent,FontFamily=Body,Foreground=Navy,RequestedThemeVariant=ThemeVariant.Light,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            try
            {
                window.Show();ClockOverlayChecks.Pump(55);
                Require(Math.Abs(frame.Bounds.Width-1020)<1&&Math.Abs(frame.Bounds.Height-700)<1,"Native host clipped the frame.");
                Require(left.Bounds.Width>=480&&right.Bounds.Width>=410,"Editor and timeline panes collapsed.");
                Require(primary.TranslatePoint(default,frame)!.Value.Y+primary.Bounds.Height<frame.Bounds.Height-15,"Footer clipped.");
                Require(status.TranslatePoint(default,summary)!.Value.Y+status.Bounds.Height<=summary.Bounds.Height-5,"Summary feedback clipped.");
                foreach(var row in frame.GetVisualDescendants().OfType<Grid>().Where(g=>g.Name=="DurationControls"))
                    for(int i=1;i<row.Children.Count;i++)Require(row.Children[i-1].Bounds.Right<=row.Children[i].Bounds.Left+1,"Duration controls overlap.");
                Require(timelineScroll.Extent.Height<=timelineScroll.Viewport.Height+1,"Default timeline clipped.");
                if(!expanded&&!split&&!stop)Require(editorScroll.Extent.Height<=editorScroll.Viewport.Height+1,"Default session cards clipped.");
                Save(frame,state);measures.Add(new{state,width=frame.Bounds.Width,height=frame.Bounds.Height,leftWidth=left.Bounds.Width,rightWidth=right.Bounds.Width,
                    editorContent=editorScroll.Extent.Height,editorViewport=editorScroll.Viewport.Height,timelineContent=timelineScroll.Extent.Height,timelineViewport=timelineScroll.Viewport.Height,
                    allocated=30-unallocated,unallocated,feelingsEnabled=feelings});
                if(expanded)
                {
                    window.Height=520;ClockOverlayChecks.Pump(25);var y=primary.TranslatePoint(default,frame)!.Value.Y;
                    Require(editorScroll.Extent.Height>editorScroll.Viewport.Height&&timelineScroll.Extent.Height>timelineScroll.Viewport.Height,"Short window needs independent scrolling.");
                    editorScroll.Offset=new(0,1000);timelineScroll.Offset=new(0,1000);ClockOverlayChecks.Pump(25);
                    Require(Math.Abs(primary.TranslatePoint(default,frame)!.Value.Y-y)<1&&y+primary.Bounds.Height<frame.Bounds.Height-15,"Scrolling moved or clipped fixed actions.");Save(frame,"short-window");
                }
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,
            shortHeight=520,fixedActions=true,independentScrolling=true,newRuntimeArtwork=0,frame="engraved-filigree",artworkScale=.16,
            execution="Private desktop; drawing-only Avalonia application; synthetic values; no production activity queries or save/navigation handlers."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured allocation, expanded time controls, split/stop policy and short-height states; fixture totals, native bounds, scrolling and fixed actions verified.");
    }

    private static Border Editor(Segment s,int order,bool expanded,bool split,bool stopped)
    {
        var number=Pill(order.ToString(),s.Ink,Cream);number.VerticalAlignment=VerticalAlignment.Center;
        var name=Text(s.Name,14);name.TextTrimming=TextTrimming.CharacterEllipsis;
        var title=new Grid{ColumnDefinitions=new("Auto,8,*,Auto,28"),Children={number,name,Pill(s.Recorded?"RECORDED":s.Part.Length>0?s.Part:"MANUAL",s.Recorded?Brush.Parse("#242E756D"):Brushes.Transparent,s.Recorded?Teal:Muted),
            IconButton(expanded?"M3,10 L8,5 L13,10":"M3,5 L8,10 L13,5",26,12)}};
        Grid.SetColumn(name,2);Grid.SetColumn(title.Children[2],3);Grid.SetColumn(title.Children[3],4);
        var range=new Grid{ColumnDefinitions=new("*,Auto"),Children={Label(s.Range,11),Text(s.Recorded?$"{s.Minutes} min":"",12)}};Grid.SetColumn(range.Children[1],1);
        var body=new StackPanel{Spacing=4,Children={title,range}};
        if(s.Recorded)body.Children.Add(Label("Time and duration are fixed by your stopwatch.",10));
        else
        {
            var slider=new Slider{Minimum=0,Maximum=30,Value=s.Minutes,Height=26,MinHeight=0,VerticalAlignment=VerticalAlignment.Center,Foreground=s.Ink};
            var minus5=Action("−5",26);var minus=Action("−1",26);var plus=Action("+1",26);var plus5=Action("+5",26);
            foreach(var button in new[]{minus5,minus,plus,plus5}){button.Height=26;button.Padding=new(0);button.FontSize=10;}
            var minutes=Input(s.Minutes.ToString(),42,26);minutes.HorizontalContentAlignment=HorizontalAlignment.Center;
            var duration=new Grid{Name="DurationControls",ColumnDefinitions=new("*,6,26,4,26,4,42,24,26,4,26"),Children={slider,minus5,minus,minutes,Label("min",10),plus,plus5}};
            int[] columns=[0,2,4,6,7,8,10];for(int i=0;i<duration.Children.Count;i++)Grid.SetColumn(duration.Children[i],columns[i]);
            body.Children.Add(duration);
        }
        if(split||stopped)
        {
            body.Children.Add(new Border{Padding=new(8,5),Margin=new(0,4,0,0),Background=Brush.Parse("#36AF7C29"),CornerRadius=new(4),Child=
                Label(split?"Continues at 14:20 after the recorded session.":"Stopped at 14:10 · 8 requested minutes not allocated.",10)});
        }
        if(expanded)
        {
            var start=new StackPanel{Spacing=4,Children={Label("START",9),Input(Time(s.Start),double.NaN,30)}};
            var end=new StackPanel{Spacing=4,Children={Label("END",9),Input(Time(s.Start+s.Minutes),double.NaN,30)}};
            var times=new Grid{ColumnDefinitions=new("*,12,*"),Margin=new(0,7,0,0),Children={start,end}};Grid.SetColumn(end,2);body.Children.Add(times);
            var earlier=Action("−1 min",65);var later=Action("+1 min",65);var up=IconButton("M3,10 L8,5 L13,10",28,10);up.IsEnabled=false;var down=IconButton("M3,5 L8,10 L13,5",28,10);
            earlier.Height=28;later.Height=28;
            var shift=new Grid{ColumnDefinitions=new("32,65,6,65,*,40,28,6,28"),Margin=new(0,6,0,0),Children={Label("Shift",10),earlier,later,Label("Order",10),up,down}};
            int[] columns=[0,1,3,5,6,8];for(int i=0;i<shift.Children.Count;i++)Grid.SetColumn(shift.Children[i],columns[i]);body.Children.Add(shift);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=7,Margin=new(0,6,0,0)};
            foreach(var (text,width) in new[]{("Split",60d),("Add part",82d),("Remove",78d)}){var action=Action(text,width);action.Height=28;if(text=="Remove")action.Foreground=Brush.Parse("#8F3328");actions.Children.Add(action);}
            body.Children.Add(actions);
        }
        var card=new Border{Padding=new(10,7),Background=Cream,BorderBrush=expanded?Gold:Rule,BorderThickness=new(expanded?1.5:1),CornerRadius=new(5),Child=body};
        return card;
    }
    private static Border TimelineRow(Segment s,int order)
    {
        var badge=s.Recorded?(Control)Icon("M4,7 L4,5 C4,0 12,0 12,5 L12,7 M2,7 L14,7 L14,15 L2,15 Z",Teal,14):Text(s.Gap?"—":order.ToString(),11);
        badge.HorizontalAlignment=HorizontalAlignment.Center;
        var marker=new Border{Width=24,Height=26,Background=s.Gap?Gap:Brush.Parse("#22FFFFFF"),CornerRadius=new(4),Child=badge,VerticalAlignment=VerticalAlignment.Center};
        var name=Text(s.Name,13);name.TextTrimming=TextTrimming.CharacterEllipsis;
        var detail=Label(s.Gap?"Not saved to project history":s.Recorded?"Recorded · fixed":s.Break?"Break · not saved":s.Part.Length>0?s.Part:"Manual session",10);
        var words=new StackPanel{Spacing=3,Children={name,detail}};
        var amount=new StackPanel{Spacing=3,HorizontalAlignment=HorizontalAlignment.Right,Children={Text($"{s.Minutes} min",13),Label(s.Range,10)}};
        var content=new Grid{ColumnDefinitions=new("24,8,*,8,Auto"),Children={marker,words,amount}};Grid.SetColumn(words,2);Grid.SetColumn(amount,4);
        return new Border{Padding=new(10,9),Background=s.Gap?Brush.Parse("#24AF7C29"):Cream,BorderBrush=s.Gap?Gold:Rule,BorderThickness=new(3,1,1,1),CornerRadius=new(5),Child=content};
    }
    private static string Time(int minutes)=>new TimeOnly(14,0).AddMinutes(minutes).ToString("HH:mm");
    private static TextBox Input(string value,double width,double height){var input=new TextBox{Text=value,Width=width,Height=height,MinWidth=0,MinHeight=0,Padding=new(7,3),FontFamily=Body,FontSize=12,VerticalContentAlignment=VerticalAlignment.Center};input.Classes.Add("editorField");return input;}
    private static TextBlock Label(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock Text(string text,double size)=>new(){Text=text,FontFamily=Body,FontSize=size,FontWeight=FontWeight.SemiBold,Foreground=Navy,VerticalAlignment=VerticalAlignment.Center};
    private static Border Pill(string text,IBrush background,IBrush foreground)=>new(){Background=background,CornerRadius=new(4),Padding=new(8,4),Child=new TextBlock{Text=text,FontFamily=Body,FontSize=10,Foreground=foreground}};
    private static Border Pane(Control content)=>new(){Padding=new(12),Background=Brush.Parse("#30FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static ScrollViewer Scroll(Control content)=>new(){Content=content,Margin=new(0,0,5,0),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private static Button Action(string text,double width,bool primary=false){var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};button.Classes.Add("editorAction");if(primary)button.Classes.Add("editorPrimary");return button;}
    private static Button IconButton(string path,double size,double glyph){var button=Action("",size);button.Height=size;button.Padding=new(4);button.BorderBrush=Brushes.Transparent;button.Content=Icon(path,Navy,glyph);return button;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,IBrush brush,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=brush,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control control,string name){using var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));image.Render(control);image.Save(System.IO.Path.Combine(Output,name+".png"));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
