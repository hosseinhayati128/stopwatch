using System.Globalization;
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

// Design spike: native controls and shared artwork/styles, synthetic data, no save handlers.
internal static class ProjectRecordPreview
{
    private const string Output="design/one-piece-v2/time-record/native-layout-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),FieldPaper=Brush.Parse("#F7E8C7"),Rule=Brush.Parse("#509A7B48"),Danger=Brush.Parse("#8F3328");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
        Application.Current!.Styles.Add(new FluentTheme());
        var measures=new List<object>();
        foreach(var (file,editing,adding,invalid) in new[]{("add-record",false,false,false),("edit-record",true,false,false),
            ("new-project",false,true,false),("invalid-range",false,true,true)})
        {
            double height=adding?732:620;
            var title=new TextBlock{Text=editing?"Edit time record":"Add time record",FontFamily=Type.NameFont,FontWeight=Type.NameWeight,FontSize=30,Foreground=Navy};
            var heading=new StackPanel{Spacing=4,Children={title,Label(editing?"Adjust this completed record.":"Log completed work for a project.",12)}};
            var close=Action("",28);close.Height=28;close.Padding=new(0);close.BorderBrush=Brushes.Transparent;
            close.Content=new Avalonia.Controls.Shapes.Path{Width=16,Height=16,Data=Geometry.Parse("M2,2 L14,14 M14,2 L2,14"),Stroke=Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round};
            close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
            var header=new Grid{ColumnDefinitions=new("*,32"),Children={heading,close}};Grid.SetColumn(close,1);

            var body=new StackPanel{Margin=new(0,0,8,0)};
            var project=new ComboBox{ItemsSource=new[]{"Illustration","Learning","Programming","Reading"},SelectedIndex=adding?-1:0,
                PlaceholderText="Choose a project",Height=38,MinHeight=0,Padding=new(10,5),FontSize=13,FontFamily=Body,
                HorizontalAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};
            project.Classes.Add("editorField");
            var newProject=Action(adding?"Back":"New…",70);
            var projectRow=new Grid{ColumnDefinitions=new("*,8,70"),Children={project,newProject}};Grid.SetColumn(newProject,2);
            body.Children.Add(Labeled("PROJECT",projectRow));
            if(adding)
            {
                var draft=new StackPanel{Spacing=7,Children={Label("NEW PROJECT NAME",10),Input("Sketchbook studies"),Label("This record will be added to the new project's history.",11)}};
                body.Children.Add(new Border{Margin=new(0,12,0,0),Padding=new(12),Background=Brush.Parse("#46FFF3D8"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(6),Child=draft});
            }

            var startDate=Date(editing?8:9);var endDate=Date(9);
            var startTime=Input(editing?"23:15:00":"09:00:00");var endTime=Input(invalid?"08:45:00":editing?"01:30:00":"10:30:00");
            if(invalid)endTime.Classes.Add("invalidTime");
            var start=Endpoint("START",startDate,startTime);var end=Endpoint("END",endDate,endTime);
            var range=new Grid{ColumnDefinitions=new("*,20,*"),Children={start,end}};Grid.SetColumn(end,2);
            body.Children.Add(new Border{Margin=new(0,18,0,0),Padding=new(12),Background=Brush.Parse("#26FFFFFF"),
                BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(6),Child=range});
            body.Children.Add(Label("Local time · 24-hour format · HH:mm or HH:mm:ss",10,new Thickness(0,9,0,0)));

            var duration=new TextBlock{Text=invalid?"—":editing?"02:15:00":"01:30:00",FontFamily=Type.TimerFont,FontWeight=Type.TimerWeight,
                FontSize=32,Foreground=invalid?Danger:Navy,VerticalAlignment=VerticalAlignment.Center};
            var summary=new Grid{ColumnDefinitions=new("*,Auto"),Children={
                new StackPanel{Spacing=5,VerticalAlignment=VerticalAlignment.Center,Children={Label("DURATION",10),Label(invalid?"Check the time range":editing?"Updated record length":"Time to add",12)}},duration}};Grid.SetColumn(duration,1);
            body.Children.Add(new Border{Margin=new(0,16,0,0),Padding=new(12),MinHeight=76,BorderBrush=invalid?Danger:Rule,
                BorderThickness=new(1),CornerRadius=new(6),Background=Brush.Parse("#46FFF3D8"),Child=summary});
            var message=Label(invalid?"End time must be later than start time.":editing?"The record crosses midnight into the following day.":"Completed time will be added to this project's history.",11,new Thickness(0,10,0,0));
            if(invalid)message.Foreground=Danger;body.Children.Add(message);

            var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var primary=Action(editing?"Save changes":"Add record",132,true);var cancel=Action("Cancel",82);
            var actions=new Grid{ColumnDefinitions=new("82,*,132"),Children={cancel,primary}};Grid.SetColumn(primary,2);
            var footer=new Grid{RowDefinitions=new("24,38"),Margin=new(0,12,0,0),Children={
                new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},Label("Tab move · Esc close",10,new Thickness(0,4,0,0)),actions}};Grid.SetRow(actions,1);
            var layout=new Grid{RowDefinitions=new("76,*,74"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=560,Height=height,Content=frame,ShowActivated=false,ShowInTaskbar=false,
                SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,FontFamily=Body,Foreground=Navy,
                RequestedThemeVariant=ThemeVariant.Light,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!
                .GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            window.Styles.Add(new Style(s=>s.OfType<CalendarDatePicker>().Class("editorField").Template().OfType<TextBox>()){
                Setters={new Setter(TemplatedControl.BackgroundProperty,FieldPaper),new Setter(TemplatedControl.ForegroundProperty,Navy),new Setter(TemplatedControl.BorderBrushProperty,Rule)}});
            try
            {
                window.Show();ClockOverlayChecks.Pump(55);
                Require(Math.Abs(frame.Bounds.Width-560)<1&&Math.Abs(frame.Bounds.Height-height)<1,"Native host clipped the frame.");
                Require(scroll.Extent.Height<=scroll.Viewport.Height+1,$"{file}: default-height body is clipped ({scroll.Extent.Height:F1}/{scroll.Viewport.Height:F1}).");
                Require(startDate.Bounds.Width>=190&&endDate.Bounds.Width>=190,"Date fields collapsed.");
                Require(primary.TranslatePoint(default,frame)!.Value.Y+primary.Bounds.Height<frame.Bounds.Height-15,"Footer lies outside the frame.");
                Save(frame,file);
                measures.Add(new{file,width=frame.Bounds.Width,height=frame.Bounds.Height,body=scroll.Extent.Height,viewport=scroll.Viewport.Height,dateWidth=startDate.Bounds.Width,fixedFooter=true});
                if(invalid)
                {
                    window.Height=440;ClockOverlayChecks.Pump(30);
                    double footerY=primary.TranslatePoint(default,frame)!.Value.Y;
                    Require(scroll.Extent.Height>scroll.Viewport.Height,"Short form needs a scrollable body.");
                    scroll.Offset=new Vector(0,1000);ClockOverlayChecks.Pump(25);
                    Require(Math.Abs(primary.TranslatePoint(default,frame)!.Value.Y-footerY)<1,"Scrolling moved the footer.");
                    Require(footerY+primary.Bounds.Height<frame.Bounds.Height-15,"Short-height footer is clipped.");
                    Save(frame,"short-window");
                }
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,
            shortHeight=440,shortHeightScroll=true,frame="engraved-filigree",artworkScale=.16,newRuntimeArtwork=0,
            execution="Private desktop; real Avalonia controls and shared renderer/styles; synthetic values, no save handlers or application lifetime."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured Add/Edit Time Record, new project, validation and short-height states; native frame, date widths, scrolling and fixed actions verified.");
    }

    private static TextBlock Label(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=Muted,
        TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static StackPanel Labeled(string text,Control field)=>new(){Spacing=6,Children={Label(text,10),field}};
    private static TextBox Input(string text)
    {
        var input=new TextBox{Text=text,Height=38,MinHeight=0,Padding=new(10,5),FontFamily=Body,FontSize=13,VerticalContentAlignment=VerticalAlignment.Center};
        input.Classes.Add("editorField");return input;
    }
    private static CalendarDatePicker Date(int day)
    {
        var date=new CalendarDatePicker{SelectedDate=new DateTime(2026,11,day),SelectedDateFormat=CalendarDatePickerFormat.Custom,CustomDateFormatString="dd MMM yyyy",
            Height=38,MinHeight=0,FontFamily=Body,FontSize=13,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};
        date.Classes.Add("editorField");return date;
    }
    private static StackPanel Endpoint(string title,Control date,Control time)=>new(){Spacing=7,Children={Label(title,10),date,Label("TIME",10,new Thickness(0,5,0,0)),time}};
    private static Button Action(string text,double width,bool primary=false)
    {
        var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};
        button.Classes.Add("editorAction");if(primary)button.Classes.Add("editorPrimary");return button;
    }
    private static void Save(Control control,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        bitmap.Render(control);bitmap.Save(System.IO.Path.Combine(Output,name+".png"));
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
