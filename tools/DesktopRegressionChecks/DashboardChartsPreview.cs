using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only design spike. Synthetic totals; no live dashboard, settings, timers or record handlers.
internal static class DashboardChartsPreview
{
    private const string Output="design/one-piece-v2/project-dashboard/charts-native-preview";
    private static readonly DateTime Today=new(2026,11,11);
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),Paper=Brush.Parse("#EBD3A5"),Rule=Brush.Parse("#509A7B48");
    private static readonly IBrush[] ProjectInks=[..new[]{"#2D7169","#3B6394","#7A527F","#96543E","#596F43","#8A4764","#456E83","#8B5C24","#505989","#596D58"}.Select(Brush.Parse)];
    private static readonly IBrush[] HeatInks=[..new[]{"#E1D0AD","#C8D8BF","#8FBAA3","#4E9381","#246758"}.Select(Brush.Parse)];
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly StopwatchTypographyProfile Heading=StopwatchTypography.Resolve("navigator-bold");
    private sealed record Project(string Name,int Minutes,double Rate,int Color);
    private static readonly FuncControlTemplate<Button> CellTemplate=new((button,_)=>
    {
        var tile=new Border{Margin=new(2,1),CornerRadius=new(2)};
        tile.Bind(Border.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));tile.Bind(Border.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));tile.Bind(Border.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));return tile;
    });

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Preview initialized the live app.");
        Application.Current!.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);var checks=new List<object>();
        foreach(string state in new[]{"all-projects","selected-project","selected-day","empty","many-projects","thirty-days","project-rtl","compact"})
        {
            bool compact=state=="compact",selected=state is "selected-project" or "project-rtl",day=state=="selected-day",empty=state=="empty",many=state=="many-projects",month=state=="thirty-days";
            string projectName=state=="project-rtl"?"برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت":"Programming";
            int[] week=selected?[60,114,30,144,0,132,84]:[180,240,126,270,60,228,198];
            int[] days=empty?new int[7]:day?[270]:month?Enumerable.Range(0,30).Select(i=>i%8==0?0:week[i%7]).ToArray():week;
            DateTime first=day?Today.AddDays(-3):Today.AddDays(1-days.Length);
            int total=days.Sum();
            Project[] projects=empty?[]:selected?[new(projectName,total,2,0)]:day?[new("Programming",144,2,0),new("Learning",66,1,1),new("Art",36,1.5,2),new("Apply",24,1,3)]:
                many?[new("Programming · Deep work",330,2,0),new("Learning",240,1,1),new("Art",180,1.5,2),new("Apply",132,1,3),new("Research and long-form reading",120,1,4),new("برنامه‌نویسی و پژوهش — پروژه بلندمدت",96,1,5),new("Writing",72,1,6),new("Planning",60,1,7),new("Practice",42,1,8),new("Language",30,1,9)]:
                month?[new("Programming",(int)(total*.43),2,0),new("Learning",(int)(total*.29),1,1),new("Art",(int)(total*.17),1.5,2),new("Apply",total-(int)(total*.43)-(int)(total*.29)-(int)(total*.17),1,3)]:
                [new("Programming",564,2,0),new("Learning",378,1,1),new("Art",216,1.5,2),new("Apply",144,1,3)];
            Require(projects.Sum(p=>p.Minutes)==total,"Fixture project and daily totals disagree.");

            var title=new StackPanel{Spacing=5,Children={new TextBlock{Text="Project Time",FontFamily=Heading.NameFont,FontWeight=Heading.NameWeight,FontSize=30,Foreground=Navy},Label("Your time, projects and progress.",12)}};
            var refresh=Action("",34);refresh.Content=Icon("M13,5 A6,6 0 1 0 14,10 M13,1 L13,5 L9,5",16);ToolTip.SetTip(refresh,"Refresh dashboard");
            var close=Action("",28);close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.Content=Icon("M2,2 L14,14 M14,2 L2,14",16);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0),Children={Action("Records",82),Action("Export",76),refresh,new Border{Width=10},close}};
            var header=new Grid{ColumnDefinitions=new("*,16,Auto"),Children={title,actions}};Grid.SetColumn(actions,2);
            string scope=selected?projectName:"All projects";
            var scopeText=Label(scope,13,Navy);scopeText.FontWeight=FontWeight.SemiBold;scopeText.TextWrapping=TextWrapping.NoWrap;scopeText.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(scopeText,scope);ToolTip.SetTip(scopeText,scope);
            string period=day?"8 November 2026":month?"13 October–11 November 2026":"5–11 November 2026";
            var contextRight=Label($"{period}  ·  {Time(total)} tracked",11);
            var context=new Grid{Height=20,ColumnDefinitions=new("*,20,Auto"),Children={scopeText,contextRight}};Grid.SetColumn(contextRight,2);

            var projectList=new StackPanel{Spacing=7};var valueLabels=new List<TextBlock>();
            foreach(var project in projects)
            {
                var name=Label(project.Name,13,Navy);name.FontWeight=FontWeight.SemiBold;name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(name,project.Name);
                ToolTip.SetTip(name,$"{project.Name} · {Time(project.Minutes)} · {project.Minutes/60d*project.Rate:0.0} pts");
                var amount=Label(Time(project.Minutes),13,Navy);amount.FontWeight=FontWeight.SemiBold;amount.HorizontalAlignment=HorizontalAlignment.Right;amount.TextWrapping=TextWrapping.NoWrap;valueLabels.Add(amount);
                var score=Label($"{project.Minutes/60d*project.Rate:0.0} pts",10);score.HorizontalAlignment=HorizontalAlignment.Right;
                double fraction=(double)project.Minutes/Math.Max(1,projects.Max(p=>p.Minutes));
                var track=new Grid{Height=6,ColumnDefinitions=new($"{fraction.ToString(CultureInfo.InvariantCulture)}*,{(1-fraction).ToString(CultureInfo.InvariantCulture)}*"),Background=Brush.Parse("#DDC599"),Margin=new(0,0,12,0)};
                track.Children.Add(new Border{Background=ProjectInks[project.Color],CornerRadius=new(3)});
                var marker=new Border{Width=8,Height=8,CornerRadius=new(4),Background=ProjectInks[project.Color],VerticalAlignment=VerticalAlignment.Center};
                var row=new Grid{Height=32,ColumnDefinitions=new("8,10,*,90"),RowDefinitions=new("19,13"),Children={marker,name,amount,track,score}};
                Grid.SetColumn(name,2);Grid.SetColumn(amount,3);Grid.SetColumn(track,2);Grid.SetRow(track,1);Grid.SetColumn(score,3);Grid.SetRow(score,1);projectList.Children.Add(row);
            }
            if(empty)projectList.Children.Add(Empty("No project time yet","Track time or add a record to see this comparison."));
            var projectScroll=new ScrollViewer{Content=projectList,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var projectHead=Title("Time by project",empty?"No tracked projects":$"{projects.Length} {(projects.Length==1?"project":"projects")}");
            var projectBody=new Grid{RowDefinitions=new("26,20,*,16"),Children={projectHead,Label("Tracked time · points weighted by project rate",10),projectScroll,Label(many?"Scroll for more projects":"Bar lengths compare tracked time",10)}};
            Grid.SetRow(projectBody.Children[1],1);Grid.SetRow(projectScroll,2);Grid.SetRow(projectBody.Children[3],3);
            var projectCard=Pane(projectBody);

            var dailyHead=Title("Daily totals",Time(total));
            var dailySub=Label(day?"Selected day · local time":month?"30 days · scroll across dates":"Selected week · local dates",10);
            const double plotHeight=108,dayWidth=46;
            int ceiling=Math.Max(120,(int)Math.Ceiling(days.Max()/120d)*120);
            var axis=new Canvas{Width=32,Height=144};
            foreach(int minutes in new[]{0,ceiling/2,ceiling}){var label=Label(minutes==0?"0":Time(minutes),10);Canvas.SetTop(label,plotHeight-minutes/(double)ceiling*plotHeight+10);axis.Children.Add(label);}
            var plot=new Canvas{Width=Math.Max(dayWidth*7,days.Length*dayWidth),Height=144,ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left};
            var guides=new List<Border>();foreach(double fraction in new[]{0d,.5,1}){var guide=new Border{Width=plot.Width,Height=1,Background=Rule,Margin=new(0,plotHeight*(1-fraction)+16,0,0)};plot.Children.Add(guide);guides.Add(guide);}
            var bars=new List<Border>();var dailyValues=new List<TextBlock>();var dailyDates=new List<TextBlock>();
            for(int i=0;i<days.Length;i++)
            {
                double h=days[i]/(double)ceiling*plotHeight;var ink=selected?ProjectInks[0]:Navy;
                var bar=new Border{Width=day?64:24,Height=h,Background=ink,CornerRadius=new(3,3,0,0)};double x=day?plot.Width/2-32:i*dayWidth+11;
                Canvas.SetLeft(bar,x);Canvas.SetTop(bar,plotHeight-h+16);plot.Children.Add(bar);bars.Add(bar);
                var value=Label(Time(days[i],true),10,Navy);value.Width=day?96:dayWidth;value.TextAlignment=TextAlignment.Center;value.TextWrapping=TextWrapping.NoWrap;
                Canvas.SetLeft(value,day?plot.Width/2-48:i*dayWidth);Canvas.SetTop(value,plotHeight-h);plot.Children.Add(value);dailyValues.Add(value);
                var date=Label(day?first.ToString("ddd d",CultureInfo.InvariantCulture):month?first.AddDays(i).ToString("d MMM",CultureInfo.InvariantCulture):first.AddDays(i).ToString("ddd",CultureInfo.InvariantCulture),10);date.Width=day?96:dayWidth;date.TextAlignment=TextAlignment.Center;
                Canvas.SetLeft(date,day?plot.Width/2-48:i*dayWidth);Canvas.SetTop(date,plotHeight+20);plot.Children.Add(date);dailyDates.Add(date);
                ToolTip.SetTip(bar,$"{first.AddDays(i):dddd, d MMMM yyyy} · {Time(days[i])} · {scope}");
            }
            var dailyScroll=new ScrollViewer{Content=plot,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var dailyChart=new Grid{ColumnDefinitions=new("32,*"),Height=162,Children={axis,dailyScroll}};Grid.SetColumn(dailyScroll,1);
            var dailyBody=new Grid{RowDefinitions=new("26,20,*"),Children={dailyHead,dailySub,empty?Empty("No daily totals yet","Days with tracked time will appear here."):dailyChart}};
            Grid.SetRow(dailySub,1);Grid.SetRow(dailyBody.Children[2],2);
            var dailyCard=Pane(dailyBody);
            var charts=new Grid{Height=248,ColumnDefinitions=new("1.15*,14,1*"),Children={projectCard,dailyCard}};Grid.SetColumn(dailyCard,2);

            DateTime monday=Today.AddDays(-2),start=monday.AddDays(-52*7),selectedDate=Today.AddDays(-3);
            int Value(DateTime date)
            {
                if(empty||date>Today)return 0;
                int periodOffset=(date-first).Days;if(periodOffset>=0&&periodOffset<days.Length)return days[periodOffset];
                int offset=(date-Today.AddDays(-6)).Days;
                if(offset is >=0 and <7)return week[offset];
                int n=(date-start).Days;if(n%11 is 0 or 1||date.DayOfWeek==DayOfWeek.Saturday)return 0;
                return (selected?24:42)+((n*13+n/7)%7)*(selected?24:42);
            }
            int maximum=Enumerable.Range(0,371).Max(i=>Value(start.AddDays(i)));
            var heatmap=new Grid{Width=901,Height=123,VerticalAlignment=VerticalAlignment.Top,ColumnDefinitions=new(string.Join(',',Enumerable.Repeat("17",53))),RowDefinitions=new("18,"+string.Join(',',Enumerable.Repeat("15",7)))};
            var weekdays=new Grid{Width=32,Height=123,RowDefinitions=new("18,"+string.Join(',',Enumerable.Repeat("15",7))),VerticalAlignment=VerticalAlignment.Top};
            for(int d=0;d<7;d+=2){if(d==6)continue;var label=Label(new[]{"Mon","Tue","Wed","Thu","Fri","Sat","Sun"}[d],9);weekdays.Children.Add(label);Grid.SetRow(label,d+1);}
            string previousMonth="";var cells=new List<Button>();
            for(int w=0;w<53;w++)
            {
                var date=start.AddDays(w*7);string monthName=date.ToString("MMM",CultureInfo.InvariantCulture);
                if(w==0||date.Day<=7&&monthName!=previousMonth){var label=Label(monthName,9);heatmap.Children.Add(label);Grid.SetColumn(label,w);Grid.SetColumnSpan(label,Math.Min(3,53-w));previousMonth=monthName;}
                for(int d=0;d<7;d++)
                {
                    date=start.AddDays(w*7+d);int minutes=Value(date);int level=minutes==0?0:Math.Min(4,1+(int)(minutes/(double)Math.Max(1,maximum)*4));bool future=date>Today,isSelected=day&&date==selectedDate;
                    var cell=new Button{Width=17,Height=15,MinWidth=0,MinHeight=0,Padding=new(0),Background=HeatInks[level],BorderBrush=isSelected?Navy:date==Today?Gold:Brush.Parse("#309A7B48"),BorderThickness=new(isSelected?2:date==Today?1.5:.5),Opacity=future?.25:1,IsEnabled=!future,Tag=date};
                    cell.Template=CellTemplate;
                    string detail=future?$"{date:d MMM yyyy} · Future date":$"{date:dddd, d MMMM yyyy} · {Time(minutes)} · {scope}";ToolTip.SetTip(cell,detail);AutomationProperties.SetName(cell,detail);
                    heatmap.Children.Add(cell);cells.Add(cell);Grid.SetColumn(cell,w);Grid.SetRow(cell,d+1);
                }
            }
            var heatScroll=new ScrollViewer{Content=heatmap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Height=compact?142:123};
            var calendar=new Grid{ColumnDefinitions=new("32,*"),Children={weekdays,heatScroll}};Grid.SetColumn(heatScroll,1);
            var heatHead=Title("Activity heatmap","Last 12 months");
            var legend=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,VerticalAlignment=VerticalAlignment.Center,Children={Label("Less",10)}};
            foreach(var ink in HeatInks)legend.Children.Add(new Border{Width=12,Height=12,CornerRadius=new(2),Background=ink,BorderBrush=Rule,BorderThickness=new(.5)});legend.Children.Add(Label("More",10));
            var heatHint=Label(day?$"Selected: Sun, 8 Nov · {Time(Value(selectedDate))} · 5 sessions":empty?"No tracked time in the last 12 months.":"Select a day to view its totals.",11,day?Navy:Muted);
            var heatFooter=new Grid{ColumnDefinitions=new("*,12,Auto"),Children={heatHint,legend}};Grid.SetColumn(legend,2);
            var heatBody=new StackPanel{Spacing=10,Children={heatHead,calendar,heatFooter}};var heatCard=Pane(heatBody);heatCard.Height=compact?239:220;
            ToolTip.SetTip(legend,"Intensity is relative to the busiest day in this project's displayed year.");
            var body=new StackPanel{Spacing=12,Margin=new(0,0,6,0),Children={context,charts,heatCard}};
            var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var add=Action("+ Add record",140,true);
            var footerActions=new Grid{ColumnDefinitions=new("*,16,140"),Children={Label("Updated 16:48:00 · Times shown in your local time zone",10),add}};Grid.SetColumn(add,2);
            var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},footerActions}};Grid.SetRow(footerActions,1);
            var layout=new Grid{RowDefinitions=new("76,*,64"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=compact?760:1080,Height=compact?560:720,Content=frame,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,RequestedThemeVariant=ThemeVariant.Light,FontFamily=Body,Foreground=Navy,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            if(compact){charts.ColumnDefinitions=new("*");charts.RowDefinitions=new("248,12,248");charts.Height=508;Grid.SetColumn(dailyCard,0);Grid.SetRow(dailyCard,2);}
            try
            {
                window.Show();ClockOverlayChecks.Pump(50);
                if(!month&&!empty)
                {
                    plot.Width=dailyScroll.Viewport.Width;double step=plot.Width/(day?1:days.Length);foreach(var guide in guides)guide.Width=plot.Width;
                    for(int i=0;i<days.Length;i++){bars[i].Width=day?64:Math.Min(36,step*.5);Canvas.SetLeft(bars[i],i*step+(step-bars[i].Width)/2);dailyValues[i].Width=step;dailyDates[i].Width=step;Canvas.SetLeft(dailyValues[i],i*step);Canvas.SetLeft(dailyDates[i],i*step);}
                }
                if(month)dailyScroll.Offset=new(dailyScroll.Extent.Width,0);if(compact)heatScroll.Offset=new(heatScroll.Extent.Width,0);ClockOverlayChecks.Pump(25);
                Require(Math.Abs(frame.Bounds.Width-window.Width)<1&&Math.Abs(frame.Bounds.Height-window.Height)<1,"Native frame dimensions changed.");
                Require(title.Bounds.Right<actions.Bounds.Left,"Header actions overlap the title.");
                Require(cells.Count==371&&cells.Where(c=>(DateTime)c.Tag!>Today).All(c=>!c.IsEnabled),"Year map or future-day state is invalid.");
                Require(days.Select((minutes,i)=>minutes==Value(first.AddDays(i))).All(equal=>equal),"Daily chart and calendar disagree for the selected period.");
                Require(weekdays.TranslatePoint(default,heatCard)!.Value.X<20,"Calendar weekday labels scroll out of view.");
                var mondayLabel=weekdays.Children[0];Require(Math.Abs(mondayLabel.TranslatePoint(default,calendar)!.Value.Y+mondayLabel.Bounds.Height/2-cells[0].TranslatePoint(default,calendar)!.Value.Y-cells[0].Bounds.Height/2)<1,"Fixed weekday labels no longer align with calendar rows.");
                Require(bars.All(b=>b.Height>=0&&b.Height<=plotHeight),"Daily bar escapes the scale.");
                Require(valueLabels.All(v=>v.DesiredSize.Width<=90),"Project duration label is clipped.");
                if(!compact)Require(scroll.Extent.Height<=scroll.Viewport.Height+1,$"Full chart layer requires scrolling: {scroll.Extent.Height}/{scroll.Viewport.Height}.");
                else Require(scroll.Extent.Height>scroll.Viewport.Height&&heatScroll.Extent.Width>heatScroll.Viewport.Width&&heatScroll.Offset.X>0,"Narrow view lost fixed-size calendar scrolling.");
                if(many)Require(projectScroll.Extent.Height>projectScroll.Viewport.Height,"Long project list cannot scroll.");
                if(month)Require(dailyScroll.Extent.Width>dailyScroll.Viewport.Width&&dailyScroll.Offset.X>0,$"Long date range lost scrolling: {dailyScroll.Extent.Width}/{dailyScroll.Viewport.Width}, offset {dailyScroll.Offset.X}.");
                double footerY=add.TranslatePoint(default,frame)!.Value.Y;Require(footerY+add.Bounds.Height<frame.Bounds.Height-15,"Footer action escaped frame.");
                Save(frame,state);checks.Add(new{state,width=frame.Bounds.Width,height=frame.Bounds.Height,totalMinutes=total,projectCount=projects.Length,yearCells=cells.Count,body=scroll.Extent.Height,viewport=scroll.Viewport.Height});
                if(compact){scroll.ScrollToEnd();ClockOverlayChecks.Pump(25);Require(Math.Abs(add.TranslatePoint(default,frame)!.Value.Y-footerY)<1,"Body scrolling moves Add record.");Save(frame,"compact-heatmap");}
                if(many){projectScroll.ScrollToEnd();ClockOverlayChecks.Pump(20);Save(frame,"many-projects-scrolled");}
            }
            finally{window.Close();}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",checks,sharedFrame=true,newRuntimeArtwork=0,execution="Private desktop; drawing-only app; synthetic data; no live app settings, hooks, timer or persistence handlers."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured eight native chart/heatmap states; layout, coherent totals, fixed footer, long-list/date scrolling and future-day states verified.");
    }
    private static string Time(int minutes,bool tight=false)=>minutes<60?$"{minutes}m":minutes%60==0?$"{minutes/60}h":$"{minutes/60}h{(tight?"":" ")}{minutes%60}m";
    private static Grid Title(string text,string detail){var right=Label(detail,11);right.HorizontalAlignment=HorizontalAlignment.Right;var grid=new Grid{ColumnDefinitions=new("*,Auto"),Children={Label(text,20,Navy),right}};Grid.SetColumn(right,1);return grid;}
    private static Border Empty(string heading,string detail)=>new(){Padding=new(16,24),Child=new StackPanel{Spacing=7,Children={Label(heading,15,Navy),Label(detail,11)}}};
    private static TextBlock Label(string text,double size,IBrush? ink=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=ink??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static Border Pane(Control child)=>new(){Padding=new(14),Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Child=child};
    private static Button Action(string text,double width,bool primary=false){var b=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};b.Classes.Add("editorAction");if(primary)b.Classes.Add("editorPrimary");return b;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=Navy,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control root,string name){using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
