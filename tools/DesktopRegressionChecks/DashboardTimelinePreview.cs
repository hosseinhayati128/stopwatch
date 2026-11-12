using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only proposal: synthetic local dates, no app settings, refresh timer, or record mutations.
internal static class DashboardTimelinePreview
{
    private const string Output="design/one-piece-v2/project-dashboard/timeline-records-native-preview";
    private static readonly DateTime Today=new(2026,11,12),Now=Today.AddHours(14).AddMinutes(20);
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Paper=Brush.Parse("#EBD3A5"),Gold=Brush.Parse("#AF7C29"),Rule=Brush.Parse("#509A7B48");
    private static readonly IBrush[] Inks=[..new[]{"#2D7169","#3B6394","#7A527F","#96543E"}.Select(Brush.Parse)];
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly string[] Names=["Programming","Learning","Art","Apply"];
    private sealed record Session(int Project,DateTime Start,DateTime End,bool Running=false,string? Name=null);
    private sealed record Row(Session Source,DateTime Start,DateTime End,int Number)
    {
        internal string Name=>Source.Name??Names[Source.Project];
        internal TimeSpan Duration=>End-Start;
        internal bool Partial=>Start!=Source.Start||End!=Source.End;
    }

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Preview loaded live application settings.");
        Application.Current!.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);var evidence=new List<object>();
        foreach(string state in new[]{"day","week","project","overnight","many-records","read-only","empty","compact"})
        {
            bool compact=state=="compact",week=state=="week",empty=state=="empty",readOnly=state=="read-only",many=state=="many-records",overnight=state=="overnight",selected=state=="project";
            var sessions=new List<Session>{new(0,Today.AddHours(9),Today.AddHours(10.5)),new(1,Today.AddHours(9.5),Today.AddHours(10)),new(2,Today.AddHours(11),Today.AddHours(11.75)),new(0,Today.AddHours(13),Now,true)};
            if(week)sessions.AddRange([new(0,Today.AddDays(-1).AddHours(8),Today.AddDays(-1).AddHours(10.5)),new(1,Today.AddDays(-1).AddHours(14),Today.AddDays(-1).AddHours(15.5)),new(3,Today.AddDays(-1).AddHours(23.5),Today.AddHours(.5)),new(2,Today.AddDays(-2).AddHours(18),Today.AddDays(-2).AddHours(20))]);
            if(overnight)sessions.AddRange([new(3,Today.AddDays(-1).AddHours(23.5),Today.AddHours(.5)),new(1,Today.AddHours(12),Today.AddHours(12).AddMinutes(2))]);
            if(many){sessions.Clear();for(int i=0;i<43;i++){var start=Today.AddDays(-(i/8)).AddHours(7+(i%8)*.75);sessions.Add(new(i%4,start,start.AddMinutes(25+i%4*5),false,i==1?"برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت":i==2?"Research and long-form reading — a longer project name":null));}}
            if(empty)sessions.Clear();if(selected)sessions.RemoveAll(s=>s.Project!=0);
            DateTime rangeStart=week||many?Today.AddDays(-6):Today;
            var rows=sessions.Where(s=>s.End>rangeStart&&s.Start<Now).OrderByDescending(s=>s.Start).Select((s,i)=>new Row(s,s.Start<rangeStart?rangeStart:s.Start,s.End>Now?Now:s.End,i+1)).ToArray();
            TimeSpan total=TimeSpan.FromTicks(rows.Sum(r=>r.Duration.Ticks));
            var scope=Label(selected?"Programming":"All projects",13,Navy);scope.FontWeight=FontWeight.SemiBold;
            var period=Label($"{(week||many?"6–12 November 2026":"Today · 12 November 2026")}  ·  {Time(total)} tracked",11);
            var context=new Grid{Height=20,ColumnDefinitions=new("*,20,Auto"),Children={scope,period}};Grid.SetColumn(period,2);

            var dayBlocks=new StackPanel{Spacing=10};var segments=new List<(Border Bar,Canvas Plot,Row Row,int Lane,double Start,double End)>();
            foreach(var date in rows.SelectMany(r=>Enumerable.Range(0,(r.End.Date-r.Start.Date).Days+1).Select(n=>r.Start.Date.AddDays(n))).Distinct().OrderByDescending(d=>d))
            {
                var dayRows=rows.Where(r=>r.Start<date.AddDays(1)&&r.End>date).Select(r=>r with{Start=r.Start<date?date:r.Start,End=r.End>date.AddDays(1)?date.AddDays(1):r.End}).OrderBy(r=>r.Start).ToArray();
                if(dayRows.Length==0)continue;
                var lanes=new List<DateTime>();var placed=new List<(Row Row,int Lane)>();
                foreach(var row in dayRows){int lane=lanes.FindIndex(end=>end<=row.Start);if(lane<0){lane=lanes.Count;lanes.Add(row.End);}else lanes[lane]=row.End;placed.Add((row,lane));}
                var heading=Label(date==Today?"Today · Thu, 12 Nov":date.ToString("ddd, d MMM",CultureInfo.InvariantCulture),12,Navy);heading.FontWeight=FontWeight.SemiBold;
                var summary=Label($"{Time(TimeSpan.FromTicks(dayRows.Sum(r=>r.Duration.Ticks)))} · {dayRows.Length} {(dayRows.Length==1?"session":"sessions")}",11);
                var dayHead=new Grid{Height=20,ColumnDefinitions=new("*,Auto"),Children={heading,summary}};Grid.SetColumn(summary,1);
                var plot=new Canvas{Height=24+lanes.Count*27,ClipToBounds=true};
                void Draw()
                {
                    plot.Children.Clear();segments.RemoveAll(s=>ReferenceEquals(s.Plot,plot));double width=plot.Bounds.Width;if(width<=0)return;
                    for(int hour=0;hour<=24;hour+=6)
                    {
                        double x=width*hour/24d;var tick=Label($"{hour:00}:00",9);tick.Width=34;tick.TextAlignment=hour==0?TextAlignment.Left:hour==24?TextAlignment.Right:TextAlignment.Center;
                        Canvas.SetLeft(tick,Math.Clamp(x-17,0,width-34));Canvas.SetTop(tick,0);plot.Children.Add(tick);
                        if(hour is >0 and <24){var guide=new Border{Width=1,Height=plot.Height-20,Background=Rule};Canvas.SetLeft(guide,x);Canvas.SetTop(guide,20);plot.Children.Add(guide);}
                    }
                    for(int lane=0;lane<lanes.Count;lane++){var track=new Border{Width=width,Height=21,Background=Brush.Parse("#1A9A7B48"),CornerRadius=new(3)};Canvas.SetTop(track,22+lane*27);plot.Children.Add(track);}
                    foreach(var item in placed)
                    {
                        double x=width*(item.Row.Start-date).TotalHours/24,end=width*(item.Row.End-date).TotalHours/24;
                        double barWidth=Math.Min(width-x,Math.Max(3,end-x));var bar=new Border{Width=barWidth,Height=21,Background=Inks[item.Row.Source.Project],CornerRadius=new(3),ClipToBounds=true};
                        if(item.Row.Source.Running){bar.BorderBrush=Navy;bar.BorderThickness=new(1.5);}
                        if(barWidth>=18){var label=Label(barWidth>=92?$"{item.Row.Number} · {item.Row.Name}":item.Row.Number.ToString(CultureInfo.InvariantCulture),10,Brushes.White);label.FontWeight=FontWeight.SemiBold;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.Margin=new(5,0);AutoTextDirection.Apply(label,label.Text);bar.Child=label;}
                        string tip=$"{item.Row.Number}. {item.Row.Name}\n{item.Row.Start:dd MMM HH:mm} – {(item.Row.Source.Running?"Now":item.Row.End.ToString("dd MMM HH:mm",CultureInfo.InvariantCulture))}\n{Time(item.Row.Duration)}";
                        ToolTip.SetTip(bar,tip);AutomationProperties.SetName(bar,tip);Canvas.SetLeft(bar,x);Canvas.SetTop(bar,22+item.Lane*27);plot.Children.Add(bar);segments.Add((bar,plot,item.Row,item.Lane,x,end));
                    }
                }
                plot.SizeChanged+=(_,_)=>Draw();plot.Loaded+=(_,_)=>Draw();dayBlocks.Children.Add(week||many?new StackPanel{Spacing=4,Children={dayHead,plot}}:plot);
            }
            if(empty)dayBlocks.Children.Add(new Border{Padding=new(0,22),Child=Label("No sessions in this period. Track time or add a record to build your timeline.",13,Navy)});
            var timelineHead=Title("Daily timeline",Label(week||many?"24-hour view":$"24 hours · {Time(total)} · {rows.Length} sessions",11));
            var timeline=Pane(new StackPanel{Spacing=6,Children={timelineHead,Label("Local time · overlapping sessions use separate lanes · numbers match the records below",10),dayBlocks}});

            var expand=Action(week?"Show records":"Hide records",108);expand.Height=30;
            var recordsTitle=Label("Project records",20,Navy);var recordsCount=Label($"{rows.Length} {(rows.Length==1?"record":"records")} · Newest first",11);
            var recordHead=new Grid{Height=30,ColumnDefinitions=new("*,Auto,14,108"),Children={recordsTitle,recordsCount,expand}};Grid.SetColumn(recordsCount,1);Grid.SetColumn(expand,3);
            var recordsContent=new StackPanel{Spacing=8,IsVisible=!week};
            if(readOnly)recordsContent.Children.Add(new Border{Padding=new(10,8),Background=Brush.Parse("#F4DFB0"),BorderBrush=Gold,BorderThickness=new(1),CornerRadius=new(4),Child=Label("Saved history is unavailable. Editing is disabled to protect your records.",11,Navy)});
            var tableHeads=new Grid{ColumnDefinitions=new("*,210,92,138"),Height=18,Margin=new(11,0),Children={Label("PROJECT",9),Label("TIME IN PERIOD",9),Label("DURATION",9)}};Grid.SetColumn(tableHeads.Children[1],1);Grid.SetColumn(tableHeads.Children[2],2);
            if(!compact&&!empty)recordsContent.Children.Add(tableHeads);
            var recordRows=new StackPanel{Spacing=6};var namedLabels=new List<TextBlock>();var timeLabels=new List<TextBlock>();var actionButtons=new List<Button>();
            foreach(var row in rows.Take(20))
            {
                var number=Label(row.Number.ToString(CultureInfo.InvariantCulture),11,Brushes.White);number.HorizontalAlignment=HorizontalAlignment.Center;
                var marker=new Border{Width=24,Height=24,CornerRadius=new(4),Background=Inks[row.Source.Project],Child=number,VerticalAlignment=VerticalAlignment.Center};
                var name=Label(row.Name,13,Navy);name.FontWeight=FontWeight.SemiBold;name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new(0,0,12,0);AutoTextDirection.Apply(name,row.Name);namedLabels.Add(name);
                var sub=Label(row.Source.Running?"RUNNING · pause to edit":row.Partial?"Work · Part of saved record":"Work",10,row.Source.Running?Inks[row.Source.Project]:Muted);
                Control projectText=compact?name:new StackPanel{Spacing=2,Children={name,sub}};
                var project=new Grid{ColumnDefinitions=new("24,10,*"),Children={marker,projectText}};Grid.SetColumn(project.Children[1],2);
                string end=row.Source.Running?"Now":row.End.Date!=row.Start.Date?row.End.ToString("d MMM HH:mm",CultureInfo.InvariantCulture):row.End.ToString("HH:mm",CultureInfo.InvariantCulture);
                var times=Label($"{row.Start:HH:mm} – {end}",12,Navy);times.TextWrapping=TextWrapping.NoWrap;timeLabels.Add(times);
                var stamp=Label(row.Start.ToString(compact?"d MMM":"ddd, d MMM",CultureInfo.InvariantCulture)+(compact&&row.Partial?" · Partial record":""),10);
                var clock=new StackPanel{Spacing=compact?8:2,Orientation=compact?Orientation.Horizontal:Orientation.Vertical,Margin=compact?new(34,0,0,0):default,VerticalAlignment=VerticalAlignment.Center,Children={times,stamp}};
                var duration=Label(Time(row.Duration),13,Navy);duration.FontWeight=FontWeight.SemiBold;duration.TextWrapping=TextWrapping.NoWrap;
                var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
                if(!row.Source.Running&&!readOnly){var edit=Action("Edit",56);var delete=Action("Delete",64);delete.Classes.Add("editorDanger");edit.Height=30;delete.Height=30;actions.Children.Add(edit);actions.Children.Add(delete);actionButtons.Add(edit);actionButtons.Add(delete);}
                else actions.Children.Add(Label(row.Source.Running?"Live":"Read only",11,row.Source.Running?Inks[row.Source.Project]:Muted));
                var grid=new Grid{ColumnDefinitions=new(compact?"*,92,138":"*,210,92,138"),RowDefinitions=new(compact?"24,3,18":"Auto"),Children={project,clock,duration,actions}};
                Grid.SetColumn(clock,compact?0:1);Grid.SetRow(clock,compact?2:0);Grid.SetColumn(duration,compact?1:2);Grid.SetColumn(actions,compact?2:3);Grid.SetRowSpan(actions,compact?3:1);Grid.SetRowSpan(duration,compact?3:1);
                var card=new Border{Padding=new(10,4),MinHeight=42,Background=Brush.Parse("#66FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),Child=grid,Tag=row};
                string details=$"{row.Number}. {row.Name} · Work\nFull record: {row.Source.Start:d MMM HH:mm} – {(row.Source.Running?"Now":row.Source.End.ToString("d MMM HH:mm",CultureInfo.InvariantCulture))}\n{Time(row.Source.End-row.Source.Start)} total; {Time(row.Duration)} in this period{(row.Source.Running?"\nPause this timer before editing its record.":"")}";
                ToolTip.SetTip(card,details);AutomationProperties.SetName(card,details);recordRows.Children.Add(card);
            }
            if(empty)recordRows.Children.Add(new Border{Padding=new(0,22),Child=new StackPanel{Spacing=6,Children={Label("No project records in this period",15,Navy),Label("Add a record or choose another date in the overview.",12)}}});
            recordsContent.Children.Add(recordRows);
            var previous=Action("Previous",80);previous.Height=30;previous.IsEnabled=false;var next=Action("Next",64);next.Height=30;next.IsEnabled=rows.Length>20;
            var pages=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,Children={previous,Label(rows.Length==0?"0 records":$"1–{Math.Min(20,rows.Length)} of {rows.Length}",11),next}};
            var paging=new Grid{Height=30,ColumnDefinitions=new("*,Auto"),Children={Label("Edit opens the full saved record",10),pages}};Grid.SetColumn(pages,1);recordsContent.Children.Add(paging);
            var records=Pane(new StackPanel{Spacing=8,Children={recordHead,recordsContent}});
            expand.Click+=(_,_)=>{recordsContent.IsVisible=!recordsContent.IsVisible;expand.Content=recordsContent.IsVisible?"Hide records":"Show records";};
            var body=new StackPanel{Spacing=10,Margin=new(0,0,6,0),Children={context,timeline,records}};
            var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var typography=StopwatchTypography.Resolve("navigator-bold");
            var title=new StackPanel{Spacing=5,Children={new TextBlock{Text="Project Time",FontFamily=typography.NameFont,FontWeight=typography.NameWeight,FontSize=30,Foreground=Navy},Label("Your time, projects and progress.",12)}};
            var recordsButton=Action("Records",82);recordsButton.Click+=(_,_)=>{recordsContent.IsVisible=true;expand.Content="Hide records";records.BringIntoView();};
            var refresh=Action("",34);refresh.Content=Icon("M13,5 A6,6 0 1 0 14,10 M13,1 L13,5 L9,5",16);AutomationProperties.SetName(refresh,"Refresh dashboard");
            var close=Action("",28);close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.Content=Icon("M2,2 L14,14 M14,2 L2,14",16);AutomationProperties.SetName(close,"Close dashboard");
            var topActions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Margin=new(0,6,0,0),VerticalAlignment=VerticalAlignment.Top,Children={recordsButton,Action("Export",76),refresh,new Border{Width=10},close}};
            var header=new Grid{ColumnDefinitions=new("*,16,Auto"),Children={title,topActions}};Grid.SetColumn(topActions,2);
            var add=Action("+ Add record",140);add.Classes.Add("editorPrimary");add.IsEnabled=!readOnly;
            var footerActions=new Grid{ColumnDefinitions=new("*,16,140"),Children={Label("Updated 14:20:00 · Times shown in your local time zone",10),add}};Grid.SetColumn(add,2);
            var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},footerActions}};Grid.SetRow(footerActions,1);
            var layout=new Grid{RowDefinitions=new("76,*,64"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=compact?760:1080,Height=compact?560:720,Content=frame,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,RequestedThemeVariant=ThemeVariant.Light,FontFamily=Body,Foreground=Navy,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            try
            {
                window.Show();ClockOverlayChecks.Pump(50);
                Require(title.Bounds.Right<topActions.Bounds.Left,"Header actions overlap.");
                Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Records require horizontal scrolling.");
                Require(segments.All(s=>s.Bar.Width>=3&&Canvas.GetLeft(s.Bar)>=0&&Canvas.GetLeft(s.Bar)+s.Bar.Width<=s.Plot.Bounds.Width+1),"Timeline segment escapes the day.");
                foreach(var group in segments.GroupBy(s=>(s.Plot,s.Lane))){var sorted=group.OrderBy(s=>s.Start).ToArray();Require(sorted.Zip(sorted.Skip(1)).All(pair=>pair.First.End<=pair.Second.Start+.01),"Overlapping sessions share a lane.");}
                Require(segments.All(s=>Equals(s.Bar.Background,Inks[s.Row.Source.Project])&&rows.Any(r=>r.Number==s.Row.Number&&r.Source==s.Row.Source)),"Timeline and record identity differ.");
                Require(TimeSpan.FromTicks(segments.Sum(s=>s.Row.Duration.Ticks))==total,"Timeline and record totals disagree.");
                Require(actionButtons.Count==rows.Take(20).Count(r=>!r.Source.Running&&!readOnly)*2,"Running/read-only records expose mutation actions.");
                if(recordsContent.IsVisible)Require(namedLabels.All(n=>n.Bounds.Width>0),$"{state}: project names have no usable width.");
                if(recordsContent.IsVisible&&!compact)Require(namedLabels.Zip(timeLabels).All(pair=>pair.Second.TranslatePoint(default,window)!.Value.X-pair.First.TranslatePoint(new Point(pair.First.Bounds.Width,0),window)!.Value.X>=10),"Long project names crowd the time column.");
                if(overnight)Require(rows.Any(r=>r.Partial&&r.Duration==TimeSpan.FromMinutes(30))&&segments.Any(s=>s.Bar.Width==3),"Overnight clipping or tiny-session state missing.");
                if(many){Require(recordRows.Children.Count==20&&next.IsEnabled&&!previous.IsEnabled,"Record paging bounds differ.");Require(namedLabels.Any(n=>n.FlowDirection==FlowDirection.RightToLeft),"Persian record direction is missing.");}
                if(readOnly)Require(!add.IsEnabled,"Read-only state permits additions.");
                double footerY=add.TranslatePoint(default,frame)!.Value.Y;
                if(state=="day"){Save(frame,state);Require(scroll.Extent.Height<=scroll.Viewport.Height+1,$"Expanded layer does not fit: {scroll.Extent.Height}/{scroll.Viewport.Height}.");}
                if(overnight||many){scroll.Offset=new(0,records.TranslatePoint(default,scroll)!.Value.Y+scroll.Offset.Y);ClockOverlayChecks.Pump(25);}
                Save(frame,state);evidence.Add(new{state,records=rows.Length,visibleRecordRows=recordRows.Children.Count,totalMinutes=total.TotalMinutes,timelineSegments=segments.Count,body=scroll.Extent.Height,viewport=scroll.Viewport.Height,width=frame.Bounds.Width,height=frame.Bounds.Height});
                if(compact||many){scroll.ScrollToEnd();ClockOverlayChecks.Pump(25);Require(add.TranslatePoint(default,frame)!.Value.Y==footerY,"Scrolling moves the footer.");Save(frame,compact?"compact-records":"many-records-scrolled");}
            }
            finally{window.Close();}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",checks=evidence,execution="Private Windows desktop; drawing-only Avalonia app; synthetic data; no app settings, hooks, persistence or live timer handlers.",newRuntimeArtwork=0},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured eight native timeline/records states; lanes, identities, coherent totals, overnight/tiny sessions, read-only/live restrictions, page bounds, RTL, compact width and fixed footer verified.");
    }
    private static string Time(TimeSpan value)=>value.TotalHours>=1?$"{(int)value.TotalHours}h{(value.Minutes==0?"":$" {value.Minutes}m")}":$"{(int)value.TotalMinutes}m";
    private static TextBlock Label(string text,double size,IBrush? ink=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=ink??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static Grid Title(string title,TextBlock right){var g=new Grid{Height=26,ColumnDefinitions=new("*,Auto"),Children={Label(title,20,Navy),right}};Grid.SetColumn(right,1);return g;}
    private static Border Pane(Control child)=>new(){Padding=new(12),CornerRadius=new(7),Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),Child=child};
    private static Button Action(string text,double width){var b=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};b.Classes.Add("editorAction");return b;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=Navy,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control root,string name){using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
