using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only overview proposal: synthetic values, no dashboard refresh, record or settings handlers.
internal static class DashboardOverviewPreview
{
    private const string Output="design/one-piece-v2/project-dashboard/overview-native-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),Cream=Brush.Parse("#F6E8C9"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2D7169");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Preview initialized the live app.");
        Directory.CreateDirectory(Output);Application.Current!.Styles.Add(new FluentTheme());var measures=new List<object>();
        foreach(string state in new[]{"all-projects-today","project-seven-days","empty-day","project-rtl","all-time","compact"})
        {
            bool compact=state=="compact",rtl=state=="project-rtl",selected=state is "project-seven-days" or "project-rtl" or "compact",empty=state=="empty-day",allTime=state=="all-time";
            int rangeIndex=allTime?3:selected?1:0;string project=rtl?"برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت":"Programming";
            string date=allTime?"All recorded history":selected?"5–11 November 2026":empty?"Tuesday, 10 November 2026":"Today · Wednesday, 11 November 2026";
            string total=empty?"0m":allTime?"1,248h 36m":selected?"18h 24m":"6h 42m";
            string score=empty?"0.0":allTime?"2,497.2":selected?"36.8":"13.4";
            string sessions=empty?"0":allTime?"1,285":selected?"23":"12",projects=empty?"0":allTime?"12":selected?"1":"4";

            var heading=new StackPanel{Spacing=5,Children={new TextBlock{Text="Project Time",FontFamily=Type.NameFont,FontWeight=Type.NameWeight,FontSize=30,Foreground=Navy},Label("Your time, projects and progress.",12)}};
            var records=Action("Records",82);var export=Action("Export",76);
            ToolTip.SetTip(records,"Open project records");ToolTip.SetTip(export,"Export records to Obsidian Markdown");
            var refresh=Action("",34);refresh.Content=Icon("M13,5 A6,6 0 1 0 14,10 M13,1 L13,5 L9,5",Navy,16);ToolTip.SetTip(refresh,"Refresh dashboard");
            var close=Action("",28);close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.Content=Icon("M2,2 L14,14 M14,2 L2,14",Navy,16);ToolTip.SetTip(close,"Close dashboard");
            var headerActions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0),Children={records,export,refresh,new Border{Width=10},close}};
            var header=new Grid{ColumnDefinitions=new("*,16,Auto"),Children={heading,headerActions}};Grid.SetColumn(headerActions,2);

            var projectSelector=new ComboBox{ItemsSource=new[]{"All projects",project,"Art","Learning","Apply"},SelectedIndex=selected?1:0,Height=36,MinHeight=0,MinWidth=0,
                HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,
                FontFamily=Body,FontSize=13,Padding=new(10,5)};
            projectSelector.Classes.Add("editorField");
            projectSelector.ItemTemplate=new FuncDataTemplate<string>((value,_)=>
            {
                var text=Label(value??"",13,Navy);text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(text,value);return text;
            });
            var projectField=Labeled("PROJECT",projectSelector);
            var ranges=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
            string[] rangeLabels=["Day","7 days","30 days","All time"];
            for(int i=0;i<rangeLabels.Length;i++){var button=Action(rangeLabels[i],i==0?62:80,i==rangeIndex);button.Height=36;ranges.Children.Add(button);}
            var rangeField=Labeled("PERIOD",ranges);
            var filterFields=new Grid{ColumnDefinitions=new("*,24,338"),Children={projectField,rangeField}};Grid.SetColumn(rangeField,2);
            var previous=Action("",28);previous.Height=28;previous.Padding=new(6);previous.Content=Icon("M10,3 L5,8 L10,13",Navy,12);previous.IsEnabled=rangeIndex==0;ToolTip.SetTip(previous,"Previous day");
            var next=Action("",28);next.Height=28;next.Padding=new(6);next.Content=Icon("M5,3 L10,8 L5,13",Navy,12);next.IsEnabled=rangeIndex==0&&empty;ToolTip.SetTip(next,"Next day");
            var navigation=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={previous,next}};
            var dateText=Label(date,12,Navy);dateText.FontWeight=FontWeight.SemiBold;
            var dates=new Grid{ColumnDefinitions=new("*,12,Auto"),Children={dateText,navigation}};Grid.SetColumn(navigation,2);
            var filters=Pane(new StackPanel{Spacing=10,Children={filterFields,dates}},new Thickness(12));

            Control context;
            NumericUpDown? rate=null;Button? delete=null;
            if(selected)
            {
                rate=new NumericUpDown{Value=2,Minimum=0,Maximum=1000,Increment=.5m,FormatString="0.0",Width=136,Height=32,MinHeight=0,
                    FontFamily=Body,FontSize=12,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};rate.Classes.Add("editorField");
                var rateRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={Label("Project rate",11),rate,Label("pts / hour",11)}};
                delete=Action("Delete project",116);delete.Height=32;delete.Classes.Add("editorDanger");ToolTip.SetTip(delete,"Delete this project and its recorded sessions; confirmation required");
                var row=new Grid{ColumnDefinitions=new("*,Auto"),Children={rateRow,delete}};Grid.SetColumn(delete,1);context=row;
            }
            else context=Label(empty?"No tracked time in this period. Add a record or choose another date.":"Each project's score per hour weights the value score below.",11);
            var contextRow=new Border{Height=40,Padding=new(2,2),Child=context};

            var totalValue=Label(total,allTime?39:48,Cream);totalValue.FontWeight=FontWeight.SemiBold;totalValue.TextWrapping=TextWrapping.NoWrap;
            var timeLabel=Label("TRACKED TIME",11,Cream);timeLabel.Opacity=.8;
            var clock=Icon("M8,1 A7,7 0 1 1 7.99,1 M8,4 L8,8 L11,10",Cream,26);clock.Opacity=.75;
            var totalHeading=new Grid{ColumnDefinitions=new("*,26"),Children={timeLabel,clock}};Grid.SetColumn(clock,1);
            var totalDetail=Label(empty?"Start with your first record.":$"Across {projects} {(projects=="1"?"project":"projects")} in this period",12,Cream);totalDetail.Opacity=.85;
            var timeNote=Label("Simultaneous timers are added.",10,Cream);timeNote.Opacity=.75;
            var totalLayout=new Grid{RowDefinitions=new("30,*,Auto,26"),Children={totalHeading,totalValue,totalDetail,timeNote}};Grid.SetRow(totalValue,1);Grid.SetRow(totalDetail,2);Grid.SetRow(timeNote,3);
            var totalCard=new Border{Padding=new(20),CornerRadius=new(7),Background=Navy,BorderBrush=Gold,BorderThickness=new(1),Child=totalLayout};
            var stats=new Grid{ColumnDefinitions=new("*,12,*"),RowDefinitions=new("*,12,*")};
            var scoreCard=Metric("VALUE SCORE",score+" pts","Weighted by project rates",Brush.Parse("#8B5C24"));
            var sessionCard=Metric("SESSIONS",sessions,"Within the selected period",Navy);
            var projectCard=Metric("PROJECTS",projects,"With tracked time",Navy);
            var activeCard=Metric("ACTIVE NOW",empty?"0":"2","Across all projects, now",Teal);
            stats.Children.Add(scoreCard);stats.Children.Add(sessionCard);stats.Children.Add(projectCard);stats.Children.Add(activeCard);
            Grid.SetColumn(sessionCard,2);Grid.SetRow(projectCard,2);Grid.SetRow(activeCard,2);Grid.SetColumn(activeCard,2);
            var summary=new Grid{Height=224,ColumnDefinitions=new(".85*,14,1.15*"),Children={totalCard,stats}};Grid.SetColumn(stats,2);
            var body=new StackPanel{Spacing=12,Margin=new(0,0,6,0),Children={filters,contextRow,summary}};
            var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};

            var add=Action("+ Add record",140,true);ToolTip.SetTip(add,"Add a time record");
            var footerActions=new Grid{ColumnDefinitions=new("*,140"),Children={Label("Updated 16:48:00 · Times shown in your local time zone",10),add}};Grid.SetColumn(add,1);
            var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},footerActions}};Grid.SetRow(footerActions,1);
            var layout=new Grid{RowDefinitions=new("76,*,64"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=compact?760:1080,Height=compact?560:620,Content=frame,ShowActivated=false,ShowInTaskbar=false,
                SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,RequestedThemeVariant=ThemeVariant.Light,FontFamily=Body,Foreground=Navy,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            if(compact)
            {
                filterFields.ColumnDefinitions=new("*");filterFields.RowDefinitions=new("Auto,12,Auto");Grid.SetColumn(rangeField,0);Grid.SetRow(rangeField,2);
                summary.ColumnDefinitions=new("*");summary.RowDefinitions=new("180,12,224");summary.Height=416;Grid.SetColumn(stats,0);Grid.SetRow(stats,2);
            }
            try
            {
                window.Show();ClockOverlayChecks.Pump(55);
                Require(Math.Abs(frame.Bounds.Width-window.Width)<1&&Math.Abs(frame.Bounds.Height-window.Height)<1,"Native frame clipped by the host window.");
                Require(projectSelector.Bounds.Width>=250&&ranges.Bounds.Width>=320,"Project or range controls collapsed.");
                Require(heading.Bounds.Right<=headerActions.Bounds.Left-8,"Header title and actions overlap.");
                Require(totalValue.DesiredSize.Width<=totalLayout.Bounds.Width+1,"Tracked total is clipped.");
                if(rate!=null){var editor=rate.GetVisualDescendants().OfType<TextBox>().Single();Require(editor.Text=="2.0"&&editor.Bounds.Width>=48,"Project rate value is missing or squeezed by its spin buttons.");}
                if(!compact)Require(scroll.Extent.Height<=scroll.Viewport.Height+1,$"{state}: overview needs scrolling ({scroll.Extent.Height}/{scroll.Viewport.Height}).");
                else Require(scroll.Extent.Height>scroll.Viewport.Height,"Compact overview cannot scroll.");
                double footerY=add.TranslatePoint(default,frame)!.Value.Y;
                Require(footerY+add.Bounds.Height<frame.Bounds.Height-15,"Add record action lies outside the frame.");
                Save(frame,state);measures.Add(new{state,width=frame.Bounds.Width,height=frame.Bounds.Height,body=scroll.Extent.Height,viewport=scroll.Viewport.Height,projectWidth=projectSelector.Bounds.Width});
                if(compact){scroll.Offset=new(0,10000);ClockOverlayChecks.Pump(25);Require(Math.Abs(add.TranslatePoint(default,frame)!.Value.Y-footerY)<1,"Scrolling moves the footer.");Save(frame,"compact-summary");}
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,sharedFrame=true,newRuntimeArtwork=0,
            execution="Private desktop; drawing-only Avalonia fixture; synthetic data; no application hooks, timers, persistence or production actions."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured dashboard overview states; native bounds, long totals, compact scrolling and fixed actions verified.");
    }
    private static Border Metric(string title,string value,string detail,IBrush ink)
    {
        var number=Label(value,27,ink);number.FontWeight=FontWeight.SemiBold;
        return Pane(new StackPanel{Spacing=3,Children={Label(title,10),number,Label(detail,10)}},new Thickness(14,10));
    }
    private static TextBlock Label(string text,double size,IBrush? ink=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=ink??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static StackPanel Labeled(string label,Control child)=>new(){Spacing=6,Children={Label(label,10),child}};
    private static Border Pane(Control content,Thickness padding)=>new(){Padding=padding,Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static Button Action(string text,double width,bool primary=false){var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};button.Classes.Add("editorAction");if(primary)button.Classes.Add("editorPrimary");return button;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,IBrush ink,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=ink,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control control,string name){using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));bitmap.Render(control);bitmap.Save(System.IO.Path.Combine(Output,name+".png"));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
