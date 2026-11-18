using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

// Design spike only: synthetic pauses, static countdown, no timer history, hooks or settings.
internal static class FocusCheckInPreview
{
    const string Output="design/one-piece-v2/focus-check-in/native-preview";
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#B78329"),Rule=Brush.Parse("#CDB784"),Cream=Brush.Parse("#F8EACA"),Teal=Brush.Parse("#147A83");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    static readonly string[] Titles=["Break / stepped away","Distraction","Idea / brainstorming","Task switch"];
    static readonly string[] Descriptions=["Planned break, call or errand","Phone, social media or lost focus","A thought to capture before returning","Worked on another project or timer"];
    static readonly string[] Icons=[
        "M3,7 L15,7 L15,13 Q15,18 9,18 Q3,18 3,13 Z M15,8 L17,8 Q21,8 21,11 Q21,14 17,14 L15,14 M1,21 L18,21 M6,1 L6,4 M11,1 L11,4",
        "M13,1 L3,13 L10,13 L8,23 L21,9 L13,9 Z",
        "M7,15 C1,10 4,2 11,2 C18,2 21,10 15,15 L15,18 L7,18 Z M8,21 L14,21 M9,24 L13,24",
        "M2,6 L21,6 M16,1 L21,6 L16,11 M22,18 L3,18 M8,13 L3,18 L8,23"];
    static TextBlock Text(string value,double size=13,IBrush? brush=null)=>new(){Text=value,FontSize=size,FontFamily=Body,Foreground=brush??Ink,
        VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
    static Border Tag(string value,IBrush background,IBrush foreground)
    {
        var text=Text(value,10,foreground);text.FontWeight=FontWeight.SemiBold;text.HorizontalAlignment=HorizontalAlignment.Center;
        return new Border {Padding=new(6,2),CornerRadius=new(3),Background=background,Child=text};
    }
    static Button Choice(int index,int automatic)
    {
        bool active=index==automatic;IBrush ink=active?Cream:Ink;
        var key=Text((index+1).ToString(),13,ink);key.FontWeight=FontWeight.SemiBold;key.HorizontalAlignment=HorizontalAlignment.Center;
        var badge=new Border {Width=26,Height=26,CornerRadius=new(4),BorderBrush=active?Gold:Rule,BorderThickness=new(1),
            Background=active?Brush.Parse("#284654"):Brush.Parse("#F1DFB7"),VerticalAlignment=VerticalAlignment.Center,Child=key};
        var icon=new Avalonia.Controls.Shapes.Path {Width=20,Height=22,Stretch=Stretch.Uniform,Data=Geometry.Parse(Icons[index]),
            Stroke=active?Cream:Teal,StrokeThickness=1.5,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        var title=Text(Titles[index],14,ink);title.FontWeight=FontWeight.SemiBold;
        var auto=Tag("AUTO",Gold,Ink);auto.IsVisible=active;auto.Margin=new(8,0,0,0);
        var titleLine=new Grid {ColumnDefinitions=new("*,Auto"),Children={title,auto}};Grid.SetColumn(auto,1);
        var description=Text(Descriptions[index],11,active?Brush.Parse("#DACDAF"):Muted);
        var words=new StackPanel {Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children={titleLine,description}};
        var row=new Grid {ColumnDefinitions=new("26,10,20,10,*"),Children={badge,icon,words}};Grid.SetColumn(icon,2);Grid.SetColumn(words,4);
        var button=new Button {Name="FocusChoice"+(index+1),MinHeight=62,Padding=new(12,9),HorizontalAlignment=HorizontalAlignment.Stretch,
            HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,Content=row,
            Background=active?Ink:Cream,BorderBrush=active?Gold:Rule,BorderThickness=new(1),CornerRadius=new(5)};
        AutomationProperties.SetName(button,$"{index+1}. {Titles[index]}"+(active?", automatic choice":""));
        return button;
    }
    internal static void Run()
    {
        if(Application.Current is not FrameSizePreviewApp)throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        foreach(int percent in new[]{70,90,125})foreach(bool taskSwitch in new[]{false,true})Render(percent,taskSwitch);
        Render(90,false,longPause:true);
    }
    static void Render(int percent,bool taskSwitch,bool longPause=false)
    {
        int automatic=taskSwitch?3:longPause?0:1;
        string selected=taskSwitch?"Task switch":longPause?"Break":"Distraction";
        var title=Text("Focus Check-in",28);title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var close=new Button {Width=30,Height=30,MinHeight=0,Padding=new(0),Background=Brushes.Transparent,BorderThickness=new(0),VerticalAlignment=VerticalAlignment.Top,
            Content=new Avalonia.Controls.Shapes.Path {Width=15,Height=15,Stretch=Stretch.Uniform,Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=Ink,StrokeThickness=1.7}};
        AutomationProperties.SetName(close,"Skip check-in");ToolTip.SetTip(close,"Skip (Esc)");
        var titleRow=new Grid {ColumnDefinitions=new("*,30"),Children={title,close}};Grid.SetColumn(close,1);
        var header=new StackPanel {Spacing=3,Children={titleRow,Text("What was this pause for?",12,Muted)}};
        var project=Text("Programming",18);project.FontWeight=FontWeight.SemiBold;
        var projectInfo=new StackPanel {Spacing=3,Children={Text("PAUSED PROJECT",10,Muted),project}};
        var duration=Text(longPause?"8m 40s":"3m 12s",24);duration.FontWeight=FontWeight.SemiBold;duration.HorizontalAlignment=HorizontalAlignment.Right;
        var pauseLabel=Text("PAUSED FOR",10,Muted);pauseLabel.HorizontalAlignment=HorizontalAlignment.Right;
        var durationInfo=new StackPanel {Spacing=1,Children={pauseLabel,duration}};
        var summaryRow=new Grid {ColumnDefinitions=new("*,16,Auto"),Children={projectInfo,durationInfo}};Grid.SetColumn(durationInfo,2);
        var summaryBody=new StackPanel {Children={summaryRow}};
        if(taskSwitch)
        {
            var other=Text("During pause: Learning · 2m 40s",12,Teal);
            summaryBody.Children.Add(new Border {BorderBrush=Rule,BorderThickness=new(0,1,0,0),Margin=new(0,9,0,0),Padding=new(0,8,0,0),Child=other});
        }
        var summary=new Border {Padding=new(12,10),Margin=new(0,12,0,12),CornerRadius=new(5),BorderBrush=Rule,BorderThickness=new(1),Background=Brush.Parse("#26FFF3D8"),Child=summaryBody};
        var choices=new StackPanel {Spacing=6};for(int index=0;index<4;index++)choices.Children.Add(Choice(index,automatic));
        string why=taskSwitch?"Activity on another project":longPause?"Pause of 5 min or longer":"Pause under 5 min";
        var defaultText=Text($"Auto-select {selected} in 12s",12);defaultText.FontWeight=FontWeight.SemiBold;
        var reason=Text(why,10.5,Muted);reason.Margin=new(12,0,0,0);reason.HorizontalAlignment=HorizontalAlignment.Right;
        var countdownRow=new Grid {ColumnDefinitions=new("*,Auto"),Children={defaultText,reason}};Grid.SetColumn(reason,1);
        var footer=new StackPanel {Spacing=8,Margin=new(0,12,0,0),Children={countdownRow,Text("1–4 choose · Enter / Space default · Esc skip",10.5,Muted)}};
        var layout=new Grid {RowDefinitions=new("Auto,Auto,Auto,Auto"),Children={header,summary,choices,footer}};
        Grid.SetRow(summary,1);Grid.SetRow(choices,2);Grid.SetRow(footer,3);
        double width=520,height=taskSwitch?570:538,scale=percent/100d;
        var frame=new StopwatchFrameSurface {Width=width,Height=height,ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        var window=new Window {Title="Focus Check-in design preview",Width=width*scale,Height=height*scale,ShowActivated=false,ShowInTaskbar=false,
            SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,
            Content=new LayoutTransformControl {LayoutTransform=new ScaleTransform(scale,scale),Child=frame}};
        try
        {
            window.Show();ClockOverlayChecks.Pump(100);
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
            if(scale>=1)bitmap.Render(window);
            else {using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
            string state=taskSwitch?"task-switch":longPause?"long-pause":"normal";
            bitmap.Save(Path.Combine(Output,$"focus-check-in-{state}-{percent}.png"));
            Require(Math.Abs(window.ClientSize.Width-width*scale)<1.1&&Math.Abs(window.ClientSize.Height-height*scale)<1.1,"Native size mismatch.");
            foreach(var label in layout.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible))
            {
                Require(label.TextLayout.Height<=label.Bounds.Height+1,"Text clipped: "+label.Text);
                var end=label.TranslatePoint(new Point(label.Bounds.Width,label.Bounds.Height),layout)!.Value;
                Require(end.X<=layout.Bounds.Width+1&&end.Y<=layout.Bounds.Height+1,$"Text exceeds panel: {label.Text}; edge {end}, panel {layout.Bounds.Size}");
            }
            Require(choices.Children.Count==4,"Expected four choices.");
            Require(choices.Children.Cast<Button>().Count(b=>b.Background==Ink)==1,"Expected one automatic choice.");
            Require(footer.TranslatePoint(new Point(0,footer.Bounds.Height),frame)!.Value.Y<frame.Bounds.Height-20,"Footer overlaps ornament.");
            Require(choices.TranslatePoint(new Point(0,choices.Bounds.Height),frame)!.Value.Y<defaultText.TranslatePoint(default,frame)!.Value.Y,"Footer overlaps choices.");
            foreach(var choice in choices.Children.Cast<Button>())
            {
                var row=(Grid)choice.Content!;var badge=(Border)row.Children[0];var key=(TextBlock)badge.Child!;
                var center=key.TranslatePoint(new Point(key.Bounds.Width/2,key.Bounds.Height/2),badge)!.Value;
                Require(Math.Abs(center.X-badge.Bounds.Width/2)<1&&Math.Abs(center.Y-badge.Bounds.Height/2)<1,"Key is not centered.");
            }
            Console.WriteLine($"PASS Focus Check-in {state} {percent}%: native size, text bounds, four choices, key centering and footer separation");
        }
        finally{window.Close();}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
