using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using StopwatchOverlay.Desktop.Controls;

// Design spike: native layout and production frame, with static form values and no editor handlers.
internal static class TimerEditorPreview
{
    private const string Output="design/one-piece-v2/timer-editor/native-layout-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Cream=Brush.Parse("#F6E8C9"),Gold=Brush.Parse("#AF7C29"),
        Muted=Brush.Parse("#655539"),Paper=Brush.Parse("#EBD3A5"),FieldPaper=Brush.Parse("#F7E8C7"),Rule=Brush.Parse("#509A7B48");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run(bool states=false)
    {
        Directory.CreateDirectory(Output);
        Application.Current!.Styles.Add(new FluentTheme());
        var body=new StackPanel{Spacing=0,Margin=new(0,0,10,0)};
        var project=new Grid{ColumnDefinitions=new("*,8,70")};
        project.Children.Add(Field("Illustration",true));var newProject=Button("New…",70);Grid.SetColumn(newProject,2);project.Children.Add(newProject);
        body.Children.Add(Labeled("PROJECT",project));

        var categories=new Grid{ColumnDefinitions=new("*,16,156"),Margin=new(0,12,0,0)};
        var category=new Grid{ColumnDefinitions=new("*,8,62")};category.Children.Add(Field("Creative",true));
        var newCategory=Button("New…",62);Grid.SetColumn(newCategory,2);category.Children.Add(newCategory);
        categories.Children.Add(Labeled("CATEGORY",category));
        var score=Labeled("SCORE / HOUR",Field("1.0",suffix:"pts/hr"));Grid.SetColumn(score,2);categories.Children.Add(score);body.Children.Add(categories);
        var idle=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Margin=new(1,11,0,0)};
        idle.Children.Add(new Border{Width=15,Height=15,Background=Navy,BorderBrush=Gold,BorderThickness=new(1),CornerRadius=new(3),
            Child=new Path{Data=Geometry.Parse("M 3,7 L 6,10 L 11,4"),Stroke=Cream,StrokeThickness=1.5}});
        idle.Children.Add(Label("Idle stop for this project · 5m timeout",11,Muted));body.Children.Add(idle);

        var timeBody=new StackPanel{Spacing=0};
        var timeHeading=new Grid{ColumnDefinitions=new("*,Auto")};timeHeading.Children.Add(Heading("Elapsed time",21));
        var state=new Border{Background=Brush.Parse("#DAE3BD"),BorderBrush=Brush.Parse("#718C57"),BorderThickness=new(.6),CornerRadius=new(8),
            Padding=new(8,2),VerticalAlignment=VerticalAlignment.Center,Child=Label("LIVE 00:44:27",9,Brush.Parse("#244C35"))};
        Grid.SetColumn(state,1);timeHeading.Children.Add(state);timeBody.Children.Add(timeHeading);
        var timeRow=new Grid{ColumnDefinitions=new("*,10,116"),Margin=new(0,6,0,0)};
        var timeField=Field("00:44:27",height:46);var value=((Grid)timeField.Child!).Children.OfType<TextBlock>().First();
        value.FontFamily=Type.TimerFont;value.FontWeight=FontWeight.Black;value.FontSize=27;
        timeRow.Children.Add(timeField);var pause=Button("Pause timer",116);pause.Height=36;pause.VerticalAlignment=VerticalAlignment.Center;
        Grid.SetColumn(pause,2);timeRow.Children.Add(pause);timeBody.Children.Add(timeRow);
        var hint=Label("Total across 3 recorded segments",10,Muted);hint.Margin=new(1,7,0,0);timeBody.Children.Add(hint);
        var quick=new Grid{ColumnDefinitions=new("*,6,*,6,*,6,*,6,*,6,*"),Margin=new(0,12,0,0)};
        string[] adjustments=["−5m","−1m","+1m","+5m","+15m","Zero"];
        for(int i=0;i<adjustments.Length;i++){var button=Button(adjustments[i]);button.Height=30;Grid.SetColumn(button,i*2);quick.Children.Add(button);}
        timeBody.Children.Add(quick);
        body.Children.Add(new Border{Background=Brush.Parse("#46FFF3D8"),BorderBrush=Rule,BorderThickness=new(.8),CornerRadius=new(7),
            Padding=new(14),Margin=new(0,18,0,0),Child=timeBody});

        var latest=new Grid{ColumnDefinitions=new("160,14,*"),Margin=new(0,17,0,0)};
        latest.Children.Add(Labeled("LATEST SEGMENT START",Field("14:44:28")));
        var segmentDetail=new StackPanel{Spacing=4,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,0,4)};
        segmentDetail.Children.Add(Label("Today · 14m 59s",12,Navy));segmentDetail.Children.Add(Label("24-hour time",10,Muted));
        Grid.SetColumn(segmentDetail,2);latest.Children.Add(segmentDetail);body.Children.Add(latest);
        var startHint=Label("Editing the start adjusts elapsed time.",10,Muted);startHint.Margin=new(1,6,0,0);body.Children.Add(startHint);
        var segmentsHeader=Disclosure("Recorded segments","3 segments · 00:44:27",new Thickness(0,17,0,0));body.Children.Add(segmentsHeader);
        var recordsHeader=Disclosure("Project records","Included in project history",new Thickness(0,7,0,0));body.Children.Add(recordsHeader);

        var header=new Grid{ColumnDefinitions=new("*,32")};
        var title=new StackPanel{Spacing=2};title.Children.Add(Heading("Edit timer",30));
        title.Children.Add(Label("Illustration  ·  Stopwatch  ·  Running",12,Muted));header.Children.Add(title);
        var close=new Border{Width=28,Height=28,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0),
            Child=new Path{Data=Geometry.Parse("M 8,8 L 20,20 M 20,8 L 8,20"),Stroke=Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round}};
        Grid.SetColumn(close,1);header.Children.Add(close);
        var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};

        var footer=new Grid{RowDefinitions=new("18,12,38"),Margin=new(0,12,0,0)};
        footer.Children.Add(new Border{Background=Rule,Height=1,VerticalAlignment=VerticalAlignment.Top});
        var saveHint=Label("Ctrl+Enter save  ·  Esc cancel  ·  Ctrl+Z undo",10,Muted);saveHint.Margin=new(1,5,0,0);footer.Children.Add(saveHint);
        var actions=new Grid{ColumnDefinitions=new("82,*,146,10,132")};
        actions.Children.Add(Button("Cancel",82));var undo=Button("Undo previous edit",146);Grid.SetColumn(undo,2);actions.Children.Add(undo);
        var save=Button("Save & Apply",132,true);Grid.SetColumn(save,4);actions.Children.Add(save);Grid.SetRow(actions,2);footer.Children.Add(actions);
        var layout=new Grid{RowDefinitions=new("70,*,80")};layout.Children.Add(header);Grid.SetRow(scroll,1);layout.Children.Add(scroll);Grid.SetRow(footer,2);layout.Children.Add(footer);
        // Keep state exports at the approved size even when Windows constrains the private host window.
        var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout,Height=states?780:double.NaN,
            VerticalAlignment=VerticalAlignment.Top};
        var window=new Window{Content=frame,Width=620,Height=780,ShowActivated=false,ShowInTaskbar=false,
            SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent}};
        try
        {
            window.Show();ClockOverlayChecks.Pump(45);
            frame.Measure(new Size(620,780));frame.Arrange(new Rect(0,0,620,780));
            Require(layout.Bounds.Width>=520,"Editor content is too narrow.");
            Require(scroll.Viewport.Height>400,"Editor viewport has collapsed.");
            Require(save.TranslatePoint(default,frame)!.Value.Y+save.Bounds.Height<=frame.Bounds.Height-20,"Primary action exceeds the shared frame.");
            if(states)
            {
                var danger=Brush.Parse("#8F3328");
                var segments=new StackPanel{Spacing=0,Margin=new(4,0,4,8)};
                string[] ranges=["09:10:00 – 09:24:50","11:30:00 – 11:44:38","14:44:28 – Now"];
                string[] durations=["00:14:50","00:14:38","00:14:59"];
                for(int i=0;i<ranges.Length;i++)
                {
                    var row=new Grid{Height=34,ColumnDefinitions=new("22,*,Auto")};row.Children.Add(Label((i+1).ToString(),10,Muted));
                    var range=Label(ranges[i],12);Grid.SetColumn(range,1);row.Children.Add(range);
                    var duration=Label(durations[i],15);duration.FontFamily=Type.TimerFont;duration.FontWeight=FontWeight.Black;
                    Grid.SetColumn(duration,2);row.Children.Add(duration);segments.Children.Add(row);
                }
                body.Children.Insert(body.Children.IndexOf(segmentsHeader)+1,segments);
                var records=new StackPanel{Spacing=12,Margin=new(4,5,4,8)};
                var recordStatus=Label("This timer’s time is included in project history.",11,Muted);records.Children.Add(recordStatus);
                var discard=Button("Discard from records",170);Danger(discard,danger);
                records.Children.Add(RecordAction("Remove recorded time","Takes effect when you save",discard));
                var delete=Button("Delete timer",170);Danger(delete,danger);
                records.Children.Add(RecordAction("Close and remove this timer","Asks for confirmation",delete));body.Children.Add(records);
                foreach(var disclosure in new[]{segmentsHeader,recordsHeader})
                    ((Grid)disclosure.Child!).Children.OfType<Path>().Single().Data=Geometry.Parse("M 3,5 L 8,10 L 13,5");
                ClockOverlayChecks.Pump(40);scroll.Offset=new Vector(0,scroll.Extent.Height);ClockOverlayChecks.Pump(30);
                var footerAt=footer.TranslatePoint(default,frame)!.Value;
                Require(frame.Bounds.Width==620&&frame.Bounds.Height==780&&frame.Bounds.Y==0,"State capture changed or clipped the approved panel dimensions.");
                Require(delete.TranslatePoint(default,scroll)!.Value.Y+delete.Bounds.Height<=scroll.Bounds.Height+1,"Expanded records actions are clipped.");
                Require(segments.TranslatePoint(default,scroll)!.Value.Y>=0,"Expanded segment breakdown is not visible.");
                Save(frame,"records-expanded.png");

                recordStatus.Text="Records will be removed from project history when you save.";recordStatus.Foreground=danger;
                discard.Opacity=.42;((Grid)recordsHeader.Child!).Children.OfType<TextBlock>().Last().Text="Removal pending";
                ClockOverlayChecks.Pump(25);Save(frame,"records-removal-pending.png");

                body.Children.Remove(segments);body.Children.Remove(records);
                foreach(var disclosure in new[]{segmentsHeader,recordsHeader})
                    ((Grid)disclosure.Child!).Children.OfType<Path>().Single().Data=Geometry.Parse("M 6,2 L 11,7 L 6,12");
                ((Grid)recordsHeader.Child!).Children.OfType<TextBlock>().Last().Text="Included in project history";
                scroll.Offset=default;value.Text="00:49:27";timeField.BorderBrush=Gold;timeField.BorderThickness=new(2);
                hint.Text="+00:05:00 will be added backward from the latest segment.";hint.Foreground=Brush.Parse("#245B63");
                ClockOverlayChecks.Pump(25);Save(frame,"time-adjustment.png");
                value.Text="15x";timeField.BorderBrush=danger;hint.Foreground=danger;
                hint.Text="Enter a valid duration, such as 01:30:00, 15m or 45s.";save.Opacity=.42;
                ClockOverlayChecks.Pump(25);Save(frame,"invalid-duration.png");
                var finalFooter=footer.TranslatePoint(default,frame)!.Value;
                Require(Math.Abs(footerAt.Y-finalFooter.Y)<1&&Math.Abs(footerAt.X-finalFooter.X)<1,
                    $"Expanded content moved the action footer: expanded={footerAt}, collapsed={finalFooter}, frame={frame.Bounds}, layout={layout.Bounds}.");
                CaptureButtons(danger);
                CaptureStateBoard();
                File.WriteAllText(System.IO.Path.Combine(Output,"state-verification.json"),JsonSerializer.Serialize(new{
                    status="state-proposal-not-production",width=frame.Bounds.Width,height=frame.Bounds.Height,footerFixed=true,expandedActionsVisible=true,
                    scenarios=new[]{"Expanded records","Removal pending until Save","Valid time adjustment","Invalid duration / disabled Save"},
                    execution="Private desktop; simulated states, no edit/save/delete handlers or user data."},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("Captured records, pending removal, duration validation and button-state proposals; fixed-footer and expanded-content checks passed.");
                return;
            }
            Save(frame,"timer-editor-layout.png");
            var full=new{width=frame.Bounds.Width,height=frame.Bounds.Height,content=layout.Bounds.ToString(),viewport=scroll.Viewport.Height,extent=scroll.Extent.Height};
            frame.Measure(new Size(620,640));frame.Arrange(new Rect(0,0,620,640));ClockOverlayChecks.Pump(20);
            // Native window layout owns its client size; set it too for the smaller-height check.
            window.Height=640;ClockOverlayChecks.Pump(35);
            Require(scroll.Extent.Height>scroll.Viewport.Height,"Short-screen body did not become scrollable.");
            Require(save.TranslatePoint(default,frame)!.Value.Y+save.Bounds.Height<=frame.Bounds.Height-20,"Short-screen footer is clipped.");
            File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{
                status="layout-proposal-not-production",full,shortHeight=frame.Bounds.Height,shortViewport=scroll.Viewport.Height,
                frame="engraved-filigree",artworkScale=.16,newRuntimeArtwork=0,
                execution="Private desktop; static values, no editor handlers, user settings, timers or hotkeys."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("Native Timer Editor proposal captured; full/short-height viewport and fixed-footer checks passed.");
        }
        finally{window.Close();}
    }

    private static Grid RecordAction(string title,string detail,Border button)
    {
        var grid=new Grid{ColumnDefinitions=new("*,16,170")};var text=new StackPanel{Spacing=3,VerticalAlignment=VerticalAlignment.Center};
        text.Children.Add(Label(title,12));text.Children.Add(Label(detail,10,Muted));grid.Children.Add(text);
        Grid.SetColumn(button,2);grid.Children.Add(button);return grid;
    }
    private static void Danger(Border button,IBrush color)
    {button.BorderBrush=color;((TextBlock)button.Child!).Foreground=color;}
    private static void CaptureButtons(IBrush danger)
    {
        var board=new Canvas{Width=800,Height=264,Background=Paper};
        void At(Control control,double x,double y){Canvas.SetLeft(control,x);Canvas.SetTop(control,y);board.Children.Add(control);}
        string[] names=["Normal","Hover","Pressed","Keyboard focus","Disabled"];
        for(int i=0;i<names.Length;i++)
        {
            double x=24+i*152;At(Label(names[i],11,Muted),x,20);
            var primary=Button("Save & Apply",132,true);var secondary=Button("Pause timer",132);var destructive=Button("Delete timer",132);Danger(destructive,danger);
            Border[] buttons=[primary,secondary,destructive];
            for(int row=0;row<buttons.Length;row++)
            {
                var button=buttons[row];
                if(i is 1 or 2)button.Background=Brush.Parse(row==0?(i==1?"#214A5C":"#071E29"):row==1?(i==1?"#66FFFFFF":"#F1D796"):(i==1?"#EDCEB4":"#DCA990"));
                Control shown=button;
                if(i==3)shown=new Border{Padding=new(3),BorderBrush=row==2?danger:Gold,BorderThickness=new(1.5),CornerRadius=new(7),Child=button,Margin=new(-4.5)};
                if(i==4)button.Opacity=.42;
                At(shown,x,50+row*67);
            }
        }
        board.Measure(new Size(800,264));board.Arrange(new Rect(0,0,800,264));Save(board,"button-states.png");
    }
    private static void CaptureStateBoard()
    {
        var board=new Canvas{Width=1320,Height=904,Background=Brush.Parse("#10232F")};
        using var records=new Bitmap(System.IO.Path.Combine(Output,"records-expanded.png"));
        using var invalid=new Bitmap(System.IO.Path.Combine(Output,"invalid-duration.png"));
        void At(Control control,double x,double y){Canvas.SetLeft(control,x);Canvas.SetTop(control,y);board.Children.Add(control);}
        At(Label("Expanded records · scrolled down",21,Cream),26,24);At(Label("Invalid time entry",21,Cream),674,24);
        At(new Image{Source=records,Width=620,Height=780},26,66);At(new Image{Source=invalid,Width=620,Height=780},674,66);
        At(Label("Same approved layout · fixed footer · simulated native states",13,Brush.Parse("#B6CAD1")),28,871);
        board.Measure(new Size(1320,904));board.Arrange(new Rect(0,0,1320,904));Save(board,"timer-editor-states.png");
    }

    private static TextBlock Label(string text,double size,IBrush? color=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=color??Navy,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock Heading(string text,double size)=>new(){Text=text,FontFamily=Type.NameFont,FontWeight=FontWeight.Bold,FontSize=size,Foreground=Navy};
    private static StackPanel Labeled(string caption,Control field)
    {
        var stack=new StackPanel{Spacing=5};stack.Children.Add(Label(caption,10,Muted));stack.Children.Add(field);return stack;
    }
    private static Border Field(string value,bool dropdown=false,string? suffix=null,double height=36)
    {
        var grid=new Grid{ColumnDefinitions=new("*,Auto")};grid.Children.Add(Label(value,13));
        if(dropdown)
        {
            var arrow=new Path{Width=10,Height=6,Data=Geometry.Parse("M 1,1 L 5,5 L 9,1"),Stroke=Muted,StrokeThickness=1.3,VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(arrow,1);grid.Children.Add(arrow);
        }
        else if(suffix!=null){var trailing=Label(suffix,11,Muted);Grid.SetColumn(trailing,1);grid.Children.Add(trailing);}
        return new Border{Height=height,Padding=new(10,0),Background=FieldPaper,BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(4),Child=grid};
    }
    private static Border Button(string text,double width=double.NaN,bool primary=false)=>new(){Width=width,Height=36,CornerRadius=new(4),
        Background=primary?Navy:Brushes.Transparent,BorderBrush=Gold,BorderThickness=new(1),Child=new TextBlock{Text=text,FontFamily=Body,
            FontSize=12,FontWeight=primary?FontWeight.SemiBold:FontWeight.Normal,Foreground=primary?Cream:Navy,
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    private static Border Disclosure(string title,string detail,Thickness margin)
    {
        var grid=new Grid{ColumnDefinitions=new("*,Auto,18")};grid.Children.Add(Heading(title,18));
        var note=Label(detail,10,Muted);Grid.SetColumn(note,1);grid.Children.Add(note);
        var arrow=new Path{Data=Geometry.Parse("M 6,2 L 11,7 L 6,12"),Stroke=Muted,StrokeThickness=1.4,Height=14,VerticalAlignment=VerticalAlignment.Center};
        Grid.SetColumn(arrow,2);grid.Children.Add(arrow);
        return new Border{Height=38,Margin=margin,BorderBrush=Rule,BorderThickness=new(0,1,0,0),Child=grid};
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Save(Control control,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width,(int)control.Bounds.Height),new Vector(96,96));
        bitmap.Render(control);bitmap.Save(System.IO.Path.Combine(Output,name));
    }
}
