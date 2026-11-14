using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only proposal. No SettingsWindow, real screens, stores, timers or platform hooks.
internal static class SettingsShellPreview
{
    private const string Output="design/one-piece-v2/settings/first-layer-native-preview";
    private static readonly IBrush Navy=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Paper=Brush.Parse("#EBD3A5"),Gold=Brush.Parse("#AF7C29"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2D7169");
    private static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly string[] Positions=["Top Left","Top Center","Top Right","Bottom Left","Bottom Center","Bottom Right","Custom"];
    private static readonly string[][] Categories=[["OVERLAY","Overlay & position","Appearance","Stopwatch appearance","Todo appearance","Fonts & colors"],["APPLICATION","Behavior","Application"],["CONNECTIONS","Obsidian / Markdown","Telegram","Habitica","ActivityWatch","Internet & Network"]];

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Preview initialized the live application.");
        Application.Current!.Styles.Add(new FluentTheme());Directory.CreateDirectory(Output);
        var evidence=new List<object>();
        foreach(string state in new[]{"overview","interaction","all-displays","custom-position","long-name-rtl","compact"})
        {
            bool compact=state=="compact",rtl=state=="long-name-rtl",all=state=="all-displays";
            var displays=new[]{"All displays","Display 1 · Built-in (Primary)",rtl?"Display 2 · External ultrawide — 3440 × 1440":"Display 2 · External monitor"};
            var screen=Select(displays,all?0:rtl?2:1,"Display");
            var position=Select(Positions,all?4:state=="custom-position"?6:2,"Position");
            var clickThrough=new CheckBox{IsChecked=state=="interaction",VerticalAlignment=VerticalAlignment.Top};
            var hideCapture=new CheckBox{IsChecked=state=="interaction",VerticalAlignment=VerticalAlignment.Top};
            var heading=new StackPanel{Spacing=4,Children={Label("Overlay & position",23,Navy),Label("Choose where your clock appears.",12)}};
            var displayField=new StackPanel{Spacing=6,Children={Label("Display",12,Navy),screen}};
            var positionField=new StackPanel{Spacing=6,Children={Label("Position",12,Navy),position}};
            var placementFields=new Grid{ColumnDefinitions=new(compact?"*,12,*":"*"),RowDefinitions=new(compact?"Auto":"Auto,12,Auto"),Children={displayField,positionField}};
            Grid.SetColumn(positionField,compact?2:0);Grid.SetRow(positionField,compact?0:2);
            var placement=Pane(new StackPanel{Spacing=8,Children={Label("PLACEMENT",10),placementFields,Label("Choose a preset, or keep your custom placement.",11)}});
            var interaction=Pane(new StackPanel{Spacing=12,Children={Label("INTERACTION",10),Choice(clickThrough,"Click-through","Let clicks pass to the windows underneath."),new Border{Height=1,Background=Rule},Choice(hideCapture,"Hide from screen capture","The clock stays visible on your screen.")}});
            var controls=new StackPanel{Spacing=12};if(!compact)controls.Children.Add(heading);controls.Children.Add(placement);controls.Children.Add(interaction);

            var face=new ThemedClockFace{Text="00:05:10",ProjectName=rtl?"برنامه‌نویسی و پژوهش":"Programming"};
            face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,OverlayTheme=OverlayThemeCatalog.Nika,TextColor="Theme default",TextSize=48,ShowRecIndicator=false});
            face.SetStopwatchPresentationState(new(true,true,false));
            var clockView=new Viewbox{Child=face,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxWidth=286,Margin=new(12),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            var hidden=Label("Overlay preview hidden",12,Brush.Parse("#D1DEDE"));hidden.HorizontalAlignment=HorizontalAlignment.Center;hidden.IsVisible=false;
            var clockStage=new Border{Height=compact?84:118,CornerRadius=new(6),Background=Brush.Parse("#192F3E"),Child=new Grid{Children={clockView,hidden}}};
            var guide=new Canvas{Height=compact?62:96,ClipToBounds=true};
            var guideLabel=Label("",11,Navy);guideLabel.TextWrapping=TextWrapping.NoWrap;guideLabel.TextTrimming=TextTrimming.CharacterEllipsis;
            var guideCaption=Label("",10);guideCaption.TextWrapping=TextWrapping.NoWrap;guideCaption.TextTrimming=TextTrimming.CharacterEllipsis;
            var diagram=new StackPanel{Spacing=6};if(!compact)diagram.Children.Add(Label("PLACEMENT GUIDE",10));diagram.Children.Add(guide);diagram.Children.Add(guideLabel);if(!compact)diagram.Children.Add(guideCaption);
            var clickStatus=Label("",10,Teal);var captureStatus=Label("",10,Navy);
            var states=new StackPanel{Spacing=3,Children={clickStatus,captureStatus}};
            var show=Action("Show / hide active overlay",double.NaN);show.Height=34;show.HorizontalAlignment=HorizontalAlignment.Stretch;
            bool sampleVisible=true;
            show.Click+=(_,_)=>{sampleVisible=!sampleVisible;clockView.IsVisible=sampleVisible;hidden.IsVisible=!sampleVisible;};
            Control previewContent;
            if(compact)
            {
                var grid=new Grid{ColumnDefinitions=new("1.05*,14,*"),Children={clockStage,diagram}};Grid.SetColumn(diagram,2);
                show.Height=30;
                var actionRow=new Grid{ColumnDefinitions=new("*,12,220"),Children={Label("Live preview",17,Navy),show}};Grid.SetColumn(show,2);
                previewContent=new StackPanel{Spacing=8,Children={actionRow,grid}};
            }
            else previewContent=new StackPanel{Spacing=10,Children={Label("Live preview",18,Navy),Label("Sample clock · current appearance",11),clockStage,diagram,states,show,Label("The guide shows placement, not screen scale.",10)}};
            var preview=Pane(previewContent);preview.VerticalAlignment=VerticalAlignment.Top;
            var settings=new Grid{ColumnDefinitions=new(compact?"*":"1.13*,16,1*"),RowDefinitions=new(compact?"Auto,12,Auto,12,Auto":"Auto"),Children={controls,preview}};
            Grid.SetColumn(preview,compact?0:2);Grid.SetRow(preview,compact?2:0);Grid.SetRow(controls,compact?4:0);if(compact)settings.Children.Add(heading);
            var contentScroll=new ScrollViewer{Content=settings,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new(0,0,5,0)};

            var navigation=new StackPanel{Spacing=2};var navButtons=new List<Button>();
            foreach(var group in Categories)
            {
                var label=Label(group[0],9);label.Margin=new(10,navigation.Children.Count==0?0:12,0,5);navigation.Children.Add(label);
                foreach(var title in group.Skip(1))
                {
                    var button=Action(title,double.NaN);button.Height=28;button.FontSize=12;button.Padding=new(10,0);button.BorderThickness=new(0);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Left;
                    var text=Label(title,12,title=="Overlay & position"?Brush.Parse("#F6E8C9"):Navy);text.HorizontalAlignment=HorizontalAlignment.Left;text.TextWrapping=TextWrapping.NoWrap;
                    var item=new Grid{Children={text}};button.Content=item;button.SizeChanged+=(_,_)=>item.Width=Math.Max(0,button.Bounds.Width-20);if(title=="Overlay & position")button.Classes.Add("editorPrimary");
                    navButtons.Add(button);navigation.Children.Add(button);
                }
            }
            var navScroll=new ScrollViewer{Content=navigation,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var navPane=Pane(navScroll);navPane.Padding=new(8,12);navPane.Background=Brush.Parse("#26FFF6E4");
            var body=new Grid{ColumnDefinitions=new(compact?"184,14,*":"200,18,*"),Children={navPane,contentScroll}};Grid.SetColumn(contentScroll,2);
            var typography=StopwatchTypography.Resolve("navigator-bold");
            var titleBlock=new StackPanel{Spacing=5,Children={new TextBlock{Text="Settings & Overlay Inspector",FontFamily=typography.NameFont,FontWeight=typography.NameWeight,FontSize=compact?25:29,Foreground=Navy},Label("Your overlay, arranged for the way you work.",12)}};
            var close=Action("",28);close.Height=28;close.BorderThickness=new(0);close.Padding=new(5);close.Content=Icon("M2,2 L14,14 M14,2 L2,14",16);close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,8,0,0);AutomationProperties.SetName(close,"Close settings");
            var header=new Grid{ColumnDefinitions=new("*,20,28"),Children={titleBlock,close}};Grid.SetColumn(close,2);
            var done=Action("Done",124);done.Classes.Add("editorPrimary");
            var footerContent=new Grid{ColumnDefinitions=new("*,20,124"),Children={Label("Changes apply immediately",11),done}};Grid.SetColumn(done,2);
            var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=Rule,VerticalAlignment=VerticalAlignment.Top},footerContent}};Grid.SetRow(footerContent,1);
            var layout=new Grid{RowDefinitions=new("76,*,64"),Children={header,body,footer}};Grid.SetRow(body,1);Grid.SetRow(footer,2);
            var frame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Paper,Child=layout};
            var window=new Window{Width=compact?840:1120,Height=compact?600:720,Content=frame,ShowActivated=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=[WindowTransparencyLevel.Transparent],RequestedThemeVariant=ThemeVariant.Light,FontFamily=Body,Foreground=Navy};
            typeof(TimerNameWindow).Assembly.GetType("StopwatchOverlay.Desktop.Views.NavigatorFormStyles")!.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[window]);
            window.Resources["SystemAccentColor"]=Color.Parse("#2D7169");
            close.Click+=(_,_)=>window.Close();done.Click+=(_,_)=>window.Close();
            var markers=new List<Border>();
            void DrawGuide()
            {
                guide.Children.Clear();markers.Clear();double width=guide.Bounds.Width;if(width<=0)return;
                int count=screen.SelectedIndex==0?2:1;double gap=10,screenWidth=(width-gap*(count-1))/count,screenHeight=guide.Height-10;
                string placementName=position.SelectedItem?.ToString()??"Top Right";
                for(int i=0;i<count;i++)
                {
                    double left=i*(screenWidth+gap);
                    var monitor=new Border{Width=screenWidth,Height=screenHeight,CornerRadius=new(4),BorderThickness=new(1),BorderBrush=Brush.Parse("#69818A"),Background=Brush.Parse("#DBE1D9")};Canvas.SetLeft(monitor,left);guide.Children.Add(monitor);
                    var strip=new Border{Width=screenWidth-10,Height=4,CornerRadius=new(1),Background=Brush.Parse("#ADBDB8")};Canvas.SetLeft(strip,left+5);Canvas.SetTop(strip,screenHeight-8);guide.Children.Add(strip);
                    var name=Label(count==2?$"DISPLAY {i+1}":$"DISPLAY {screen.SelectedIndex}",8,Brush.Parse("#647D80"));Canvas.SetLeft(name,left+7);Canvas.SetTop(name,screenHeight/2-5);guide.Children.Add(name);
                    if(placementName=="Custom")continue;
                    var marker=new Border{Width=42,Height=17,CornerRadius=new(3),Background=Navy,BorderBrush=Gold,BorderThickness=new(1),Child=Label("Clock",8,Brush.Parse("#F6E8C9"))};
                    ((TextBlock)marker.Child).HorizontalAlignment=HorizontalAlignment.Center;
                    double x=placementName.EndsWith("Left")?7:placementName.EndsWith("Right")?screenWidth-49:(screenWidth-42)/2;
                    Canvas.SetLeft(marker,left+x);Canvas.SetTop(marker,placementName.StartsWith("Bottom")?screenHeight-31:7);guide.Children.Add(marker);markers.Add(marker);
                }
                guideLabel.Text=placementName=="Custom"?"Custom placement":placementName;
                guideCaption.Text=placementName=="Custom"?"Uses your saved overlay position.":screen.SelectedIndex==0?"Same preset on all connected displays.":screen.SelectedIndex==1?"1920 × 1080 · Primary display":"3440 × 1440 · External display";
            }
            void UpdateStates(){clickStatus.Text=clickThrough.IsChecked==true?"Click-through on · clicks pass through":"Click-through off · clock is interactive";captureStatus.Text=hideCapture.IsChecked==true?"Screen capture · clock hidden":"Screen capture · clock included";}
            screen.SelectionChanged+=(_,_)=>{ToolTip.SetTip(screen,screen.SelectedItem?.ToString());DrawGuide();};position.SelectionChanged+=(_,_)=>DrawGuide();guide.SizeChanged+=(_,_)=>DrawGuide();guide.Loaded+=(_,_)=>DrawGuide();
            clickThrough.IsCheckedChanged+=(_,_)=>UpdateStates();hideCapture.IsCheckedChanged+=(_,_)=>UpdateStates();UpdateStates();
            try
            {
                window.Show();ClockOverlayChecks.Pump(45);DrawGuide();ClockOverlayChecks.Pump(25);
                Require(navButtons.Count==12&&screen.Items.Count==3&&position.Items.Count==7,"Existing navigation or placement choices were lost.");
                Require(contentScroll.Extent.Width<=contentScroll.Viewport.Width+1&&navScroll.Extent.Width<=navScroll.Viewport.Width+1,"Preview has horizontal overflow.");
                Require(screen.Bounds.Width>=230&&position.Bounds.Width>=230,"Placement fields collapsed.");
                if(compact)Require(position.TranslatePoint(default,contentScroll)!.Value.Y+position.Bounds.Height<=contentScroll.Viewport.Height,"Compact preview pushes placement fields out of view.");
                Require(titleBlock.Bounds.Right<close.Bounds.Left,"Header text overlaps close.");
                Require(done.TranslatePoint(default,window)!.Value.Y+done.Bounds.Height<frame.Bounds.Height-15,"Done clips outside the frame.");
                Require(markers.All(m=>Canvas.GetLeft(m)>=0&&Canvas.GetLeft(m)+m.Width<=guide.Bounds.Width+.1&&Canvas.GetTop(m)+m.Height<=guide.Height),"Placement marker escapes its display.");
                Require(markers.Count==(state=="custom-position"?0:all?2:1),"Guide does not reflect selected display/position.");
                double footerY=done.TranslatePoint(default,window)!.Value.Y;
                Save(frame,state);
                if(compact){contentScroll.ScrollToEnd();ClockOverlayChecks.Pump(25);Save(frame,"compact-controls");Require(done.TranslatePoint(default,window)!.Value.Y==footerY,"Scrolling moves Done.");}
                if(state=="overview")
                {
                    clickThrough.IsChecked=true;hideCapture.IsChecked=true;Require(clickStatus.Text!.Contains("on")&&captureStatus.Text!.Contains("hidden"),"Sample interaction states do not update.");
                    clickThrough.IsChecked=false;hideCapture.IsChecked=false;show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(20);Require(hidden.IsVisible&&!clockView.IsVisible,"Sample show/hide button is disconnected.");Save(frame,"preview-hidden");
                }
                evidence.Add(new{state,width=frame.Bounds.Width,height=frame.Bounds.Height,categories=navButtons.Count,positionChoices=position.Items.Count,fieldWidth=screen.Bounds.Width,contentHeight=contentScroll.Extent.Height,viewport=contentScroll.Viewport.Height,clockRenderer=nameof(ThemedClockFace)});
            }
            finally{window.Close();}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="native-design-preview-not-production",checks=evidence,execution="Private desktop, synthetic displays and settings; no live settings, persistence or platform hooks.",newRuntimeArtwork=0},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS six native settings states; all 12 categories, seven positions, layout bounds, preview state and fixed Done checked.");
    }
    private static TextBlock Label(string text,double size,IBrush? ink=null)=>new(){Text=text,FontFamily=Body,FontSize=size,Foreground=ink??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static Border Pane(Control child)=>new(){Padding=new(14),CornerRadius=new(7),Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),Child=child};
    private static Button Action(string text,double width){var button=new Button{Content=text,Width=width,Height=38,MinWidth=0,MinHeight=0,Padding=new(10,0),FontFamily=Body,FontSize=12};button.Classes.Add("editorAction");return button;}
    private static ComboBox Select(string[] choices,int selected,string name)
    {
        var combo=new ComboBox{ItemsSource=choices,SelectedIndex=selected,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Height=36,MinHeight=0,MinWidth=0,Padding=new(10,4),FontSize=12,Classes={"editorField"}};
        combo.ItemTemplate=new FuncDataTemplate<string>((text,_)=>new TextBlock{Text=text,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,FontFamily=Body,FontSize=12});combo.SelectionBoxItemTemplate=combo.ItemTemplate;
        AutomationProperties.SetName(combo,name);ToolTip.SetTip(combo,choices[selected]);return combo;
    }
    private static Control Choice(CheckBox check,string title,string detail)
    {
        var titleText=Label(title,13,Navy);titleText.FontWeight=FontWeight.SemiBold;
        check.Content=new StackPanel{Spacing=3,Children={titleText,Label(detail,11)}};check.HorizontalAlignment=HorizontalAlignment.Stretch;check.HorizontalContentAlignment=HorizontalAlignment.Stretch;check.VerticalContentAlignment=VerticalAlignment.Top;check.FontFamily=Body;AutomationProperties.SetName(check,title);return check;
    }
    private static Avalonia.Controls.Shapes.Path Icon(string data,double size)=>new(){Width=size,Height=size,Data=Geometry.Parse(data),Stretch=Stretch.Uniform,Stroke=Navy,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private static void Save(Control root,string name){using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
