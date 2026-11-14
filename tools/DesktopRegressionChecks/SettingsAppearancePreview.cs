using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design spike: only local preview settings; never subscribes the controller's persistence callbacks.
internal static class SettingsAppearancePreview
{
    const string Output="design/one-piece-v2/settings/appearance-native-preview";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2D7169");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;
    static readonly MethodInfo ApplyTheme=typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!;

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Live app initialized in preview.");
        Application.Current!.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml")});
        Directory.CreateDirectory(Output);var evidence=new List<object>();
        foreach(string state in new[]{"application","alternate-style","floating-clock","classic-clock","compact"})
        {
            bool compact=state=="compact",clock=state.Contains("clock"),classic=state=="classic-clock",alternate=state=="alternate-style";
            ApplyTheme.Invoke(null,[AppThemeCatalog.Nika]);
            var sample=new AppSettings{ThemeMode=AppThemeCatalog.Nika,OverlayTheme=classic?OverlayThemeCatalog.Midnight:OverlayThemeCatalog.Nika,TextColor="Theme default",TimeFormat=1};
            if(alternate){sample.NikaBackground="sky-island-chart";sample.NikaTypography="chartmakers-script";sample.NikaSidebarArtwork="cloud-dragon";}
            var window=new SettingsWindow(new AppSettings{ThemeMode=AppThemeCatalog.Nika}){ShowActivated=false,ShowInTaskbar=false,Width=compact?840:1120,Height=compact?600:720};
            var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().First(i=>Equals(i.Tag,"Appearance"));
            var host=window.FindControl<Grid>("SettingsColumns")!;host.Children.Clear();host.Background=Brushes.Transparent;
            var title=new StackPanel{Spacing=3,Children={Text("Appearance",23,Ink),Text("Choose how your app and floating clock look.",12)}};
            var appTab=ActionButton("App & panels",146);var clockTab=ActionButton("Floating clock",146);appTab.Height=clockTab.Height=32;
            var tabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={appTab,clockTab}};
            var fields=new StackPanel{Spacing=12};
            var formScroll=new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,5,0)};
            var previewContent=new StackPanel{Spacing=10};var preview=Card(previewContent);preview.VerticalAlignment=VerticalAlignment.Top;
            var previewScroll=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var body=new Grid{ColumnDefinitions=new(compact?"*":"1.08*,16,1*"),RowDefinitions=new(compact?"Auto,12,Auto":"*"),Children={formScroll,previewScroll}};
            Grid.SetColumn(previewScroll,compact?0:2);Grid.SetRow(formScroll,compact?2:0);
            var outerScroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=compact?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            if(compact){formScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;previewScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;}
            var page=new Grid{RowDefinitions=new("48,40,12,*"),Children={title,tabs,outerScroll}};Grid.SetRow(tabs,1);Grid.SetRow(outerScroll,3);Grid.SetColumnSpan(page,3);host.Children.Add(page);
            var snapshots=new List<RenderTargetBitmap>();var face=new ThemedClockFace{Text="00:05:10",ProjectName="Programming"};
            var appImage=new Image{Stretch=Stretch.Uniform};
            var previewTitle=Text("",18,Ink);var previewCaption=Text("",11);var previewNote=Text("",11,Teal);
            var appTheme=Select(AppThemeCatalog.All,AppThemeCatalog.Nika,"Application theme");
            var background=Select(NikaThemeCatalog.Backgrounds,sample.NikaBackground,"Nika background");
            var typography=Select(NikaThemeCatalog.Typography,sample.NikaTypography,"Nika typography");
            var artwork=Select(NikaThemeCatalog.Artworks,sample.NikaSidebarArtwork,"Sidebar artwork");
            var overlay=Select(OverlayThemeCatalog.All,sample.OverlayTheme,"Floating clock theme");
            var format=Select(new[]{"HH:MM:SS.t","HH:MM:SS","MM:SS.t","MM:SS","HH:MM"},"HH:MM:SS","Time format");
            var clockStage=new Border{Background=Brush.Parse("#192F3E"),CornerRadius=new(6),Height=compact?90:154,Child=new Viewbox{Child=face,Margin=new(16),MaxWidth=300,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly}};
            var appStage=new Border{Background=Brush.Parse("#D5BA89"),BorderBrush=Rule,BorderThickness=new(1),CornerRadius=new(5),Height=compact?114:210,ClipToBounds=true,Child=appImage};
            var typeHeading=Text("A page from your journal",compact?18:22,Ink);var typeDigits=Text("00:44:27",compact?24:26,Ink);
            var specimen=Card(new StackPanel{Spacing=4,Children={typeHeading,typeDigits,Text("Typography sample",10)}});specimen.Padding=new(compact?8:12);
            void RefreshClock()
            {
                face.ApplySettings(sample);face.SetStopwatchPresentationState(new(true,true,false));
                face.Text=sample.TimeFormat switch{0=>"00:05:10.5",2=>"05:10.5",3=>"05:10",4=>"00:05",_=>"00:05:10"};
                previewCaption.Text=OverlayThemeCatalog.Resolve(sample.OverlayTheme,sample.ThemeMode)+" · active clock";
            }
            void RefreshApplication()
            {
                // The synthetic constructor creates sample timers and never loads the workspace or starts services.
                var controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([sample]);
                controller.ShowActivated=false;controller.ShowInTaskbar=false;controller.Width=1120;controller.Height=736;
                try
                {
                    controller.Show();ClockOverlayChecks.Pump(35);var root=(Control)controller.Content!;
                    var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);snapshots.Add(bitmap);appImage.Source=bitmap;
                    if(controller.TryFindResource("NikaHeadingFont",out var headingFont)&&headingFont is FontFamily heading)typeHeading.FontFamily=heading;
                    if(controller.TryFindResource("NikaDigitsFont",out var digitFont)&&digitFont is FontFamily digits)typeDigits.FontFamily=digits;
                }
                finally{controller.Close();}
                previewCaption.Text=NikaThemeCatalog.Backgrounds.First(b=>b.Id==sample.NikaBackground).Name;
            }
            bool showingClock=clock;
            void Draw()
            {
                fields.Children.Clear();previewContent.Children.Clear();
                foreach(var field in new Control[]{appTheme,background,typography,artwork,overlay,format})
                    if(field.Parent is Panel parent)parent.Children.Remove(field);
                if(appStage.Parent is Panel appParent)appParent.Children.Remove(appStage);
                if(specimen.Parent is Panel typeParent)typeParent.Children.Remove(specimen);
                appTab.Classes.Set("editorPrimary",!showingClock);clockTab.Classes.Set("editorPrimary",showingClock);
                previewTitle.Text=showingClock?"Clock preview":"Application preview";
                previewContent.Children.Add(previewTitle);
                if(!compact)previewContent.Children.Add(previewCaption);
                if(compact&&!showingClock)
                {
                    var compactPreview=new Grid{ColumnDefinitions=new("1.05*,12,*"),Children={appStage,specimen}};Grid.SetColumn(specimen,2);previewContent.Children.Add(compactPreview);
                }
                else previewContent.Children.Add(showingClock?clockStage:appStage);
                if(showingClock)
                {
                    fields.Children.Add(Card(new StackPanel{Spacing=9,Children={Text("FLOATING CLOCK",10),Field("Theme",overlay),Field("Time format",format),SliderRow("Text size",16,120,sample.TextSize,"px",v=>{sample.TextSize=v;RefreshClock();})}}));
                    bool nika=OverlayThemeCatalog.Resolve(sample.OverlayTheme,sample.ThemeMode)==OverlayThemeCatalog.Nika;
                    if(nika)
                    {
                        var open=ActionButton("Open Stopwatch appearance",double.NaN);open.HorizontalAlignment=HorizontalAlignment.Stretch;
                        fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("Nika clock style",16,Ink),Text("Frame, background, typography, artwork and hover controls.",12),open,Text("Text colors follow your clock background automatically.",11,Teal)}}));
                        previewNote.Text="Your app theme stays the same.";
                        if(!compact)previewContent.Children.Add(Card(new StackPanel{Spacing=6,Children={Text("Current Nika style",13,Ink),Text("Engraved filigree · Navigator parchment",11),Text("Background 70% · Unselected 40%",11)}}));
                    }
                    else
                    {
                        fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("TEXT & OUTLINE",10),Field("Text color",Select(new[]{"Theme default","White","Charcoal","Yellow","Cyan","Lime","Orange","Red","Magenta"},"Theme default","Text color")),Field("Timer font",Select(new[]{"Consolas","Cascadia Mono","Segoe UI","Arial","Courier New","Lucida Console"},"Consolas","Timer font")),Field("Outline color",Select(new[]{"Black","White","Dark Gray","Red","Blue"},"Black","Outline color")),SliderRow("Outline thickness",1,5,2,"px")}}));
                        fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("VISIBILITY",10),SliderRow("Timer surface opacity",0,100,50,"%"),SliderRow("Inactive separated clock",10,100,40,"%")}}));
                        if(OverlayThemeCatalog.Resolve(sample.OverlayTheme,sample.ThemeMode)==OverlayThemeCatalog.Pirate)
                        {
                            var parts=new StackPanel{Spacing=6};
                            foreach(string part in new[]{"Clock wooden frame and rope","Clock parchment map","Metal border","Metal background","Timer digits","Project name","Button wooden frame and chain","Button blue map","Button dials and icons"})parts.Children.Add(new CheckBox{Content=part});
                            fields.Children.Add(Card(new Expander{Header="Transparency details",Content=parts}));
                        }
                        previewNote.Text="Independent clock theme · app remains Nika.";
                    }
                    RefreshClock();
                }
                else
                {
                    fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("APPLICATION",10),Field("Theme",appTheme),Text("Sets the look of the controller and app pages.",11)}}));
                    fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("NIKA’S CHRONICLE",10),Field("Background",background),Field("Typography",typography),Field("Sidebar artwork",artwork)}}));
                    fields.Children.Add(Card(new StackPanel{Spacing=8,Children={Text("SIZE & BACKGROUND",10),SliderRow("Page scale",70,125,sample.UiScalePercent,"%"),Text("Floating clock text has its own size.",11),Field("Application background",Select(AppBackgroundCatalog.GetAvailableChoices(sample,Output).Select(c=>c.DisplayLabel).ToArray(),"Theme default","Application background")),new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={ActionButton("Add image…",112),new Button{Content="Remove",Height=32,Classes={"editorAction"},IsEnabled=false}}},SliderRow("Background strength",10,45,30,"%",enabled:false)}}));
                    if(!compact)previewContent.Children.Add(specimen);
                    previewNote.Text="App appearance and floating clock style are independent.";
                    RefreshApplication();
                }
                if(!compact&&showingClock){previewContent.Children.Add(previewNote);previewContent.Children.Add(Text("Sample content · preview fitted to this panel",10));}
                formScroll.Offset=default;outerScroll.Offset=default;
            }
            appTab.Click+=(_,_)=>{showingClock=false;Draw();};clockTab.Click+=(_,_)=>{showingClock=true;Draw();};
            background.SelectionChanged+=(_,_)=>{sample.NikaBackground=((NikaThemeOption)background.SelectedItem!).Id;RefreshApplication();};
            typography.SelectionChanged+=(_,_)=>{sample.NikaTypography=((NikaThemeOption)typography.SelectedItem!).Id;RefreshApplication();};
            artwork.SelectionChanged+=(_,_)=>{sample.NikaSidebarArtwork=((NikaThemeOption)artwork.SelectedItem!).Id;RefreshApplication();};
            overlay.SelectionChanged+=(_,_)=>{sample.OverlayTheme=overlay.SelectedItem!.ToString()!;Draw();};
            format.SelectionChanged+=(_,_)=>{sample.TimeFormat=format.SelectedIndex;RefreshClock();};
            try
            {
                Draw();window.Show();ClockOverlayChecks.Pump(65);
                Require(formScroll.Extent.Width<=formScroll.Viewport.Width+1,"Appearance form overflows horizontally.");
                Require(window.GetVisualDescendants().OfType<Button>().Count(b=>b.Name?.StartsWith("SettingsNav_")==true)==12,"Shell navigation changed.");
                var done=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SettingsDone");double footerY=done.TranslatePoint(default,window)!.Value.Y;
                Save(window,state);
                if(state is "application" or "classic-clock" or "compact")
                {
                    (compact?outerScroll:formScroll).ScrollToEnd();ClockOverlayChecks.Pump(25);Save(window,state+"-more");
                    Require(done.TranslatePoint(default,window)!.Value.Y==footerY,"Scrolling moved the footer.");
                }
                if(state=="floating-clock")
                {
                    appTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(25);Require(!showingClock&&appImage.Source!=null,"Application tab did not restore its preview.");
                    clockTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(showingClock&&sample.OverlayTheme==OverlayThemeCatalog.Nika,"Tabs changed the independent clock theme.");
                }
                evidence.Add(new{state,width=window.Bounds.Width,height=window.Bounds.Height,applicationThemes=appTheme.Items.Count,nikaBackgrounds=background.Items.Count,typography=typography.Items.Count,artworks=artwork.Items.Count,clockThemes=overlay.Items.Count,nativeRenderer=true});
            }
            finally{window.Close();foreach(var bitmap in snapshots)bitmap.Dispose();}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="design-preview-only",execution="Private desktop; synthetic settings; no persistence or application installation.",states=evidence,newRuntimeArtwork=0},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS five native Appearance proposal states; shared shell, native controller/clock previews, option catalog counts, tab routing, compact scrolling and fixed footer.");
    }

    static TextBlock Text(string text,double size,IBrush? ink=null)=>new(){Text=text,FontSize=size,FontFamily=Body,Foreground=ink??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    static Border Card(Control child)=>new(){Child=child,Padding=new(14),CornerRadius=new(7),Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1)};
    static Button ActionButton(string title,double width)=>new(){Content=title,Width=width,Height=34,MinWidth=0,MinHeight=0,Padding=new(12,0),FontFamily=Body,Classes={"editorAction"}};
    static Control Field(string name,Control field)=>new StackPanel{Spacing=5,Children={Text(name,12,Ink),field}};
    static ComboBox Select<T>(IReadOnlyList<T> values,string selected,string name)
    {
        var box=new ComboBox{ItemsSource=values,SelectedItem=values.First(v=>v is NikaThemeOption option?option.Id==selected:v?.ToString()==selected),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Height=34,MinWidth=0,MinHeight=0,FontFamily=Body,FontSize=12,Padding=new(10,4),Classes={"editorField"}};
        box.ItemTemplate=new FuncDataTemplate<T>((value,_)=>new TextBlock{Text=value?.ToString(),FontFamily=Body,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis});box.SelectionBoxItemTemplate=box.ItemTemplate;
        AutomationProperties.SetName(box,name);ToolTip.SetTip(box,box.SelectedItem?.ToString());return box;
    }
    static Control SliderRow(string title,double min,double max,double value,string unit,Action<double>? changed=null,bool enabled=true)
    {
        var text=Text($"{value:0}{(unit=="%"?"":" ")}{unit}",12,Ink);var header=new Grid{ColumnDefinitions=new("*,Auto"),Children={Text(title,12,Ink),text}};Grid.SetColumn(text,1);
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,IsEnabled=enabled,MinHeight=24};AutomationProperties.SetName(slider,title);
        slider.PropertyChanged+=(_,e)=>{if(e.Property==Slider.ValueProperty){text.Text=$"{slider.Value:0}{(unit=="%"?"":" ")}{unit}";changed?.Invoke(slider.Value);}};
        return new StackPanel{Spacing=3,Children={header,slider}};
    }
    static void Save(Window window,string name){var root=(Control)window.Content!;using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
