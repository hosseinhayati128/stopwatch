using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;
using PathShape = Avalonia.Controls.Shapes.Path;

// Design spike only: real native controls with fixture values, no application/save handlers.
internal static class ProjectChooserPreview
{
    private const string Output="design/one-piece-v2/project-chooser/native-layout-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),FieldPaper=Brush.Parse("#F7E8C7"),Cream=Brush.Parse("#F6E8C9"),Rule=Brush.Parse("#509A7B48");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        Application.Current!.Styles.Add(new FluentTheme());
        var measures=new List<object>();
        foreach(var (file,creating,adding) in new[]{("new-timer-existing",true,false),("new-timer-new-project",true,true),("choose-project",false,false)})
        {
            double height=adding?602:490;
            var heading=new StackPanel{Spacing=4};heading.Children.Add(Title(creating?"New timer":"Choose project",30));
            heading.Children.Add(Label(creating?"Choose a project for your new timer.":"Current timer: Illustration",12,Muted));
            var close=Action("",28);close.Height=28;close.Padding=new(0);close.BorderBrush=Brushes.Transparent;
            close.Content=new PathShape{Width=16,Height=16,Data=Geometry.Parse("M2,2 L14,14 M14,2 L2,14"),Stroke=Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round};
            close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
            var header=new Grid{ColumnDefinitions=new("*,32"),Children={heading,close}};Grid.SetColumn(close,1);

            var body=new StackPanel{Margin=new(0,0,8,0)};
            var project=Choice(["No project","Illustration","Learning","Programming","Reading"],adding?0:1);
            var newProject=Action(adding?"Back":"New…",70);
            var projectRow=new Grid{ColumnDefinitions=new("*,8,70"),Children={project,newProject}};Grid.SetColumn(newProject,2);
            body.Children.Add(Labeled("PROJECT",projectRow));
            if(adding)
            {
                var input=new TextBox{Text="Pencil studies",MaxLength=60,Height=38,Background=FieldPaper,Foreground=Navy,BorderBrush=Gold,
                    Padding=new(10,6),FontFamily=Body,FontSize=13,VerticalContentAlignment=VerticalAlignment.Center};
                var draft=new StackPanel{Spacing=7,Children={Label("NEW PROJECT NAME",10,Muted),input,
                    Label("This name will also appear on the timer.",11,Muted)}};
                body.Children.Add(new Border{Padding=new(12),Margin=new(0,12,0,0),Background=Brush.Parse("#46FFF3D8"),BorderBrush=Rule,
                    BorderThickness=new(1),CornerRadius=new(6),Child=draft});
            }

            var category=Choice(["Creative","Work","Learning","Health","Hobbies"],0);
            var newCategory=Action("New…",62);
            var categoryRow=new Grid{ColumnDefinitions=new("*,8,62"),Children={category,newCategory}};Grid.SetColumn(newCategory,2);
            var score=new NumericUpDown{Value=1,Minimum=0,Maximum=1000,Increment=.5m,FormatString="0.0",Height=38,MinWidth=0,
                FontFamily=Body,FontSize=13,Background=FieldPaper,Foreground=Navy,BorderBrush=Rule,
                HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            var columns=new Grid{ColumnDefinitions=new("*,16,160"),Margin=new(0,18,0,0)};
            columns.Children.Add(Labeled("CATEGORY",categoryRow));var scoreField=Labeled("SCORE / HOUR",score);Grid.SetColumn(scoreField,2);columns.Children.Add(scoreField);
            body.Children.Add(columns);
            body.Children.Add(Label("Points earned per hour of tracked work.",10,Muted,new Thickness(0,7,0,0)));
            var note=new StackPanel{Spacing=5,Children={Label(adding?"Ready when you create":"Project details",12,Navy),
                Label(adding?"Your new project uses the category and score above.":"Selecting a project fills its saved category and score. You can adjust them here.",11,Muted)}};
            body.Children.Add(new Border{Padding=new(12),Margin=new(0,18,0,0),Background=Brush.Parse("#26FFFFFF"),BorderBrush=Rule,
                BorderThickness=new(0,1,0,0),Child=note});
            body.Children.Add(Label(adding?"Use Back to choose an existing project or leave the timer unnamed.":creating?"No project? Leave this timer unnamed and assign one later.":"Choose “No project” to make this timer unnamed.",11,Muted,new Thickness(0,14,0,0)));

            var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
            var primary=Action(creating?"Create timer":"Apply project",132,true);var cancel=Action("Cancel",82);
            var actions=new Grid{ColumnDefinitions=new("82,*,132"),Children={cancel,primary}};Grid.SetColumn(primary,2);
            var footer=new Grid{RowDefinitions=new("24,38"),Margin=new(0,12,0,0)};
            footer.Children.Add(new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top});
            footer.Children.Add(Label(adding?"Enter create  ·  Esc back":creating?"Enter create  ·  Esc close":"Enter apply  ·  Esc close",10,Muted,new Thickness(0,4,0,0)));
            Grid.SetRow(actions,1);footer.Children.Add(actions);
            var layout=new Grid{RowDefinitions=new("76,*,74"),Children={header,scroll,footer}};
            Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=560,Height=height,Content=frame,ShowActivated=false,ShowInTaskbar=false,
                SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,FontFamily=Body,Foreground=Navy,
                RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            try
            {
                window.Show();ClockOverlayChecks.Pump(55);
                Require(Math.Abs(frame.Bounds.Width-560)<1&&Math.Abs(frame.Bounds.Height-height)<1,"Native panel was clipped by the host window.");
                Require(scroll.Extent.Height<=scroll.Viewport.Height+1,$"{file}: default-height content needs scrolling ({scroll.Extent.Height:F1}/{scroll.Viewport.Height:F1}).");
                Require(primary.TranslatePoint(default,frame)!.Value.Y+primary.Bounds.Height<frame.Bounds.Height-15,"Footer lies outside the frame.");
                Require(category.Bounds.Width>=140&&score.Bounds.Width>=150,"Category or score field collapsed.");
                Save(frame,file);
                double footerY=primary.TranslatePoint(default,frame)!.Value.Y;
                scroll.Offset=new Vector(0,1000);ClockOverlayChecks.Pump(20);
                Require(Math.Abs(primary.TranslatePoint(default,frame)!.Value.Y-footerY)<1,"Scrolling moved the footer.");
                measures.Add(new{file,width=frame.Bounds.Width,height=frame.Bounds.Height,body=scroll.Extent.Height,viewport=scroll.Viewport.Height,fixedFooter=true});
                if(adding)
                {
                    window.Height=420;ClockOverlayChecks.Pump(35);
                    Require(scroll.Extent.Height>scroll.Viewport.Height,"Short-height form has no scrollable body.");
                    Require(primary.TranslatePoint(default,frame)!.Value.Y+primary.Bounds.Height<frame.Bounds.Height-15,"Short-height footer is clipped.");
                }
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,
            shortHeight=420,shortHeightScroll=true,frame="engraved-filigree",artworkScale=.16,newRuntimeArtwork=0,
            execution="Private desktop; real Avalonia controls and shared renderer; synthetic values, no application handlers or persistence."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured New Timer existing/new-project states and Choose Project; native layout, field widths, fixed footer and short-height scroll checks passed.");
    }

    private static TextBlock Label(string text,double size,IBrush? ink=null,Thickness margin=default)=>new(){Text=text,FontFamily=Body,FontSize=size,
        Foreground=ink??Navy,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock Title(string text,double size)=>new(){Text=text,FontFamily=Type.NameFont,FontWeight=FontWeight.Bold,FontSize=size,Foreground=Navy};
    private static StackPanel Labeled(string text,Control input)=>new(){Spacing=6,Children={Label(text,10,Muted),input}};
    private static ComboBox Choice(string[] items,int selected)=>new(){ItemsSource=items,SelectedIndex=selected,Height=38,MinHeight=0,
        HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,
        Padding=new(10,5),FontFamily=Body,FontSize=13,Foreground=Navy,Background=FieldPaper,BorderBrush=Rule,BorderThickness=new(1)};
    private static Button Action(string text,double width,bool primary=false)
    {
        var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),
            FontFamily=Body,FontSize=12,FontWeight=FontWeight.SemiBold,Foreground=primary?Cream:Navy,
            Background=primary?Navy:Brushes.Transparent,BorderBrush=Gold,BorderThickness=new(1)};
        button.Template=new FuncControlTemplate<Button>((owner,_)=>
        {
            var content=new ContentPresenter{Content=owner.Content,Foreground=owner.Foreground,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            return new Border{Background=owner.Background,BorderBrush=owner.BorderBrush,BorderThickness=owner.BorderThickness,CornerRadius=new(4),Padding=owner.Padding,Child=content};
        });return button;
    }
    private static void Save(Control control,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));
        bitmap.Render(control);bitmap.Save(System.IO.Path.Combine(Output,name+".png"));
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
