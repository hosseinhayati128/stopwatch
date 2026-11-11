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
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only proposal: sample values and native controls, without production save or settings handlers.
internal static class ReviewMoodPreview
{
    private const string Output="design/one-piece-v2/activity-review/mood-native-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Gold=Brush.Parse("#AF7C29"),
        Paper=Brush.Parse("#EBD3A5"),Cream=Brush.Parse("#F6E8C9"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2E756D"),
        Ochre=Brush.Parse("#8B5C24"),Low=Brush.Parse("#934B3F");
    private static readonly StopwatchTypographyProfile Type=StopwatchTypography.Resolve("navigator-bold");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Directory.CreateDirectory(Output);Application.Current!.Styles.Add(new FluentTheme());var measures=new List<object>();
        foreach(var state in new[]{"neutral","selected-feelings","low-mood","custom-feeling-rtl"})
        {
            bool selected=state=="selected-feelings",low=state=="low-mood",rtl=state=="custom-feeling-rtl";
            double score=low?2.4:selected?7.6:rtl?8.5:5.0;
            var ink=low?Low:selected||rtl?Teal:Ochre;
            string descriptor=low?"Distressed / Low":selected?"Good / Content":rtl?"Thrilled / Great":"Neutral / Okay";
            string subtext=low?"Very low mood, struggling or overwhelmed":selected?"Positive, clear, content & engaged":rtl?"Energized, thrilled, peak satisfaction":"Steady, moderate, routine headspace";
            string[] chosen=low?["Tired","Overwhelmed"]:selected?["Calm","Focused","Motivated"]:rtl?["Focused","آرام"]:[];
            var heading=new StackPanel{Spacing=5,Children={new TextBlock{Text="Activity & Time Review",FontFamily=Type.NameFont,FontWeight=Type.NameWeight,FontSize=30,Foreground=Navy},
                Label("How did you feel during this review?",12),
                new StackPanel{Orientation=Orientation.Horizontal,Spacing=16,Margin=new(0,6,0,0),Children={Label("01  Projects & activity",11),Label("02  Allocate time",11),Pill("03  Feelings & mood",Navy,Cream)}}}};
            var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),Children={Label("TODAY · 14:00–14:30",10),Text("30 minutes",18)}};
            var close=Action("",28);close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
            close.Content=Icon("M2,2 L14,14 M14,2 L2,14",Navy,16);
            var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

            var summaryWords=new StackPanel{Spacing=4,Children={Text("22 min of project time · 2 projects",13),Label("Programming · Illustration",11)}};
            var summaryRest=new StackPanel{Spacing=4,HorizontalAlignment=HorizontalAlignment.Right,Children={Label("3 min break · 5 min unallocated",11),Label("Breaks and gaps stay out of project history.",10)}};
            var summaryRow=new Grid{ColumnDefinitions=new("*,Auto"),Children={summaryWords,summaryRest}};Grid.SetColumn(summaryRest,1);
            var summary=new Border{Padding=new(12,8),Background=Brush.Parse("#42FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Margin=new(0,6,0,4),Child=summaryRow};

            var moodTitle=new StackPanel{Spacing=4,Children={Text("How was your mood?",20),Label("Choose a score from 1.0 to 10.0.",11)}};
            var figure=new TextBlock{Text=score.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture),FontFamily=Body,FontSize=52,FontWeight=FontWeight.SemiBold,Foreground=ink,VerticalAlignment=VerticalAlignment.Center};
            var scoreValue=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,Children={figure,new TextBlock{Text="/10",FontFamily=Body,FontSize=14,Foreground=Muted,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,0,12)}}};
            var face=new Grid{Width=48,Height=48,Children={new Avalonia.Controls.Shapes.Ellipse{Stroke=ink,StrokeThickness=1.6,Margin=new(2)},
                new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M16,18 L16,21 M32,18 L32,21 "+(low?"M15,34 Q24,25 33,34":score>=7?"M15,28 Q24,39 33,28":"M16,31 L32,31")),Stroke=ink,StrokeThickness=1.8,StrokeLineCap=PenLineCap.Round}}};
            var scoreRow=new Grid{ColumnDefinitions=new("*,48"),Children={scoreValue,face}};Grid.SetColumn(face,1);face.VerticalAlignment=VerticalAlignment.Center;
            var description=Text(descriptor,16);description.Foreground=ink;
            var scoreCard=new Border{Padding=new(16,10),Background=Cream,BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(6),Child=new StackPanel{Spacing=4,Children={scoreRow,description,Label(subtext,11)}}};
            var down=Action("−0.1",48);var up=Action("+0.1",48);down.Height=up.Height=30;down.Padding=up.Padding=new(0);
            var slider=new Slider{Minimum=1,Maximum=10,Value=score,SmallChange=.1,LargeChange=.5,Height=40,MinHeight=0,VerticalAlignment=VerticalAlignment.Center,Margin=new(8,0)};
            var controls=new Grid{ColumnDefinitions=new("48,*,48"),Children={down,slider,up}};Grid.SetColumn(slider,1);Grid.SetColumn(up,2);
            var ticks=new Grid{Margin=new(56,0),Children={Label("1.0 · Low",9),Label("5.0",9),Label("10.0 · Thrilled",9)}};ticks.Children[1].HorizontalAlignment=HorizontalAlignment.Center;ticks.Children[2].HorizontalAlignment=HorizontalAlignment.Right;
            var presets=new Grid{ColumnDefinitions=new("*,6,*,6,*,6,*,6,*,6,*")};int i=0;
            foreach(double value in new[]{1d,3,5,7,8.5,10})
            {
                var button=Action(value.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture),double.NaN,Math.Abs(value-score)<.01);button.Height=30;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new(0);
                presets.Children.Add(button);Grid.SetColumn(button,i++*2);
            }
            var moodContent=new StackPanel{Spacing=9,Children={scoreCard,controls,ticks,Label("QUICK SCORES",9),presets,
                Label("The score describes this review period. Feelings and a note are optional.",11,new(0,5,0,0))}};
            var moodScroll=Scroll(moodContent);var moodLayout=new Grid{RowDefinitions=new("54,*"),Children={moodTitle,moodScroll}};Grid.SetRow(moodScroll,1);

            var feelingTitle=new Grid{ColumnDefinitions=new("*,Auto"),Children={new StackPanel{Spacing=4,Children={Text("Feelings & context",20),Label("Choose any that fit. You can select more than one.",11)}},Pill($"{chosen.Length} selected",Brush.Parse("#24AF7C29"),Muted)}};
            Grid.SetColumn(feelingTitle.Children[1],1);feelingTitle.Children[1].VerticalAlignment=VerticalAlignment.Top;
            var keywords=new AppSettings().MoodPresetKeywords.ToList();if(rtl)keywords.Add("آرام");var chips=new WrapPanel{Orientation=Orientation.Horizontal};
            foreach(var word in keywords)
            {
                bool active=chosen.Contains(word);var label=Label(word,12);label.Foreground=active?Cream:Navy;label.TextWrapping=TextWrapping.NoWrap;AutoTextDirection.Apply(label,word);
                var content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={label}};
                if(active)content.Children.Insert(0,Icon("M2,7 L6,11 L13,3",Cream,11));
                var chip=Action("",double.NaN,active);chip.Height=31;chip.Padding=new(10,0);chip.Margin=new(0,0,7,7);chip.Content=content;
                if(!active){chip.Background=Cream;chip.BorderBrush=Rule;}chips.Children.Add(chip);
            }
            var custom=Input(rtl?"خلاق":"",34);custom.Watermark="Add your own feeling";AutoTextDirection.Attach(custom);
            var add=Action("Add",56);add.Height=34;var addRow=new Grid{ColumnDefinitions=new("*,8,56"),Margin=new(0,2,0,0),Children={custom,add}};Grid.SetColumn(add,2);
            var note=Input(selected?"A short break helped. I came back focused and made steady progress.":low?"Felt drained after switching between tasks. Needed a longer break.":rtl?"بعد از یک استراحت کوتاه، با آرامش و تمرکز به کار برگشتم.":"",64);
            note.Watermark="What influenced how you felt?";note.AcceptsReturn=true;note.TextWrapping=TextWrapping.Wrap;note.VerticalContentAlignment=VerticalAlignment.Top;AutoTextDirection.Attach(note);
            var noteHeading=new Grid{ColumnDefinitions=new("*,Auto"),Children={Text("Reflection",13),Label("Optional",10)}};Grid.SetColumn(noteHeading.Children[1],1);
            var feelingsContent=new StackPanel{Spacing=6,Children={chips,addRow,Label("Enter adds a feeling.",10),new Border{Height=1,Background=Rule,Margin=new(0,5,0,4)},noteHeading,note,Label("Enter adds a new line in your note.",10)}};
            var feelingsScroll=Scroll(feelingsContent);var feelingLayout=new Grid{RowDefinitions=new("54,*"),Children={feelingTitle,feelingsScroll}};Grid.SetRow(feelingsScroll,1);
            var left=Pane(moodLayout);var right=Pane(feelingLayout);var panes=new Grid{ColumnDefinitions=new(".85*,14,1.15*"),Margin=new(0,8,0,8),Children={left,right}};Grid.SetColumn(right,2);

            var back=Action("Back",82);var timeOnly=Action("Save time only",128);var save=Action("Save & complete",176,true);
            var actions=new Grid{ColumnDefinitions=new("82,12,*,128,8,176"),Children={back,Label("Esc back · Enter save",10),timeOnly,save}};Grid.SetColumn(actions.Children[1],2);Grid.SetColumn(timeOnly,3);Grid.SetColumn(save,5);
            var footer=new Grid{RowDefinitions=new("18,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},actions}};Grid.SetRow(actions,1);
            var layout=new Grid{RowDefinitions=new("84,64,*,66"),Children={header,summary,panes,footer}};Grid.SetRow(summary,1);Grid.SetRow(panes,2);Grid.SetRow(footer,3);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=1020,Height=700,Content=frame,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,
                Background=Brushes.Transparent,FontFamily=Body,Foreground=Navy,RequestedThemeVariant=ThemeVariant.Light,TransparencyLevelHint=[WindowTransparencyLevel.Transparent]};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            try
            {
                window.Show();ClockOverlayChecks.Pump(50);
                Save(frame,state);Console.WriteLine($"{state}: mood {moodScroll.Extent.Height:0.#}/{moodScroll.Viewport.Height:0.#}; feelings {feelingsScroll.Extent.Height:0.#}/{feelingsScroll.Viewport.Height:0.#}; tags {chips.Bounds.Height:0.#}.");
                Require(Math.Abs(frame.Bounds.Width-1020)<1&&Math.Abs(frame.Bounds.Height-700)<1,"Native host clipped the frame.");
                Require(moodScroll.Extent.Height<=moodScroll.Viewport.Height+1,"Default mood controls overflow.");
                Require(feelingsScroll.Extent.Height<=feelingsScroll.Viewport.Height+1,"Default feeling controls overflow.");
                Require(save.TranslatePoint(default,frame)!.Value.Y+save.Bounds.Height<frame.Bounds.Height-15,"Save actions clipped.");
                Require(summaryRest.TranslatePoint(default,summary)!.Value.Y+summaryRest.Bounds.Height<=summary.Bounds.Height-5,"Time summary clipped.");
                measures.Add(new{state,score,selectedFeelings=chosen.Length,width=frame.Bounds.Width,height=frame.Bounds.Height,moodViewport=moodScroll.Viewport.Height,feelingsViewport=feelingsScroll.Viewport.Height});
                if(selected)
                {
                    window.Height=520;ClockOverlayChecks.Pump(25);double y=save.TranslatePoint(default,frame)!.Value.Y;
                    Require(moodScroll.Extent.Height>moodScroll.Viewport.Height&&feelingsScroll.Extent.Height>feelingsScroll.Viewport.Height,"Short panes do not scroll independently.");
                    moodScroll.Offset=new(0,10000);feelingsScroll.Offset=new(0,10000);ClockOverlayChecks.Pump(20);
                    Require(Math.Abs(save.TranslatePoint(default,frame)!.Value.Y-y)<1&&y+save.Bounds.Height<frame.Bounds.Height-15,"Scrolling moved fixed save actions.");Save(frame,"short-window");
                }
            }
            finally{window.Close();}
        }
        File.WriteAllText(System.IO.Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",measures,shortHeight=520,fixedActions=true,independentScrolling=true,
            newRuntimeArtwork=0,execution="Private desktop; drawing-only Avalonia app; synthetic values; no production settings or save handlers."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Captured neutral, selected feelings, low mood, custom RTL and short-height states; native bounds and fixed actions verified.");
    }
    private static TextBlock Label(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=Muted,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock Text(string text,double size)=>new(){Text=text,FontFamily=Body,FontSize=size,FontWeight=FontWeight.SemiBold,Foreground=Navy,VerticalAlignment=VerticalAlignment.Center};
    private static Border Pill(string text,IBrush background,IBrush foreground)=>new(){Background=background,CornerRadius=new(4),Padding=new(8,4),Child=new TextBlock{Text=text,FontFamily=Body,FontSize=10,Foreground=foreground}};
    private static Border Pane(Control content)=>new(){Padding=new(13),Background=Brush.Parse("#30FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static ScrollViewer Scroll(Control content)=>new(){Content=content,Margin=new(0,0,5,0),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private static TextBox Input(string value,double height){var input=new TextBox{Text=value,Height=height,MinHeight=0,MinWidth=0,Padding=new(9,6),FontFamily=Body,FontSize=12};input.Classes.Add("editorField");return input;}
    private static Button Action(string text,double width,bool primary=false){var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};button.Classes.Add("editorAction");if(primary)button.Classes.Add("editorPrimary");return button;}
    private static Avalonia.Controls.Shapes.Path Icon(string path,IBrush brush,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=brush,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control control,string name){using var image=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)),new Vector(96,96));image.Render(control);image.Save(System.IO.Path.Combine(Output,name+".png"));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
