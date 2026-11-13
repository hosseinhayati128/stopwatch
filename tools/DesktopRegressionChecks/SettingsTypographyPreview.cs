using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design spike: real Settings controls with synthetic settings and no persistence subscribers.
internal static class SettingsTypographyPreview
{
    const string Output="design/one-piece-v2/settings/typography-native-preview";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly IBrush Ink=Brush.Parse("#102B3B"),Muted=Brush.Parse("#655539"),Rule=Brush.Parse("#509A7B48"),Teal=Brush.Parse("#2D7169"),Paper=Brush.Parse("#F7E8C7");
    static readonly FontFamily Body=StopwatchTypography.Resolve("modern-deck").NameFont;

    internal static void Run()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App,"Live app initialized in preview.");
        Application.Current!.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")});
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")){Source=new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml")});
        Directory.CreateDirectory(Output);var evidence=new List<object>();
        foreach(bool smallWindow in new[]{false,true})foreach(int percent in new[]{70,90,125})
        {
            var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=percent};
            typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
            Page("Initialize",settings);
            var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false,Width=smallWindow?840:1120,Height=smallWindow?600:720};
            try
            {
                var nav=window.FindControl<ListBox>("NavigationList")!;
                nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Typography"));
                // The integrated page owns these editors; return them before composing the archived proposal.
                typeof(SettingsWindow).GetMethod("RestoreTypographyPage",Hidden)!.Invoke(window,null);
                var globalHost=window.FindControl<StackPanel>("GlobalTypographyEditor")!;
                var global=(StackPanel)globalHost.Children.Single();globalHost.Children.Clear();
                var sectionsHost=window.FindControl<StackPanel>("SectionTypographyEditors")!;
                var sections=sectionsHost.Children.Cast<StackPanel>().ToArray();sectionsHost.Children.Clear();
                foreach(var editor in sections.Prepend(global))StyleEditor(editor);
                var host=window.FindControl<Grid>("SettingsColumns")!;host.IsVisible=true;host.Children.Clear();host.Background=Brushes.Transparent;
                window.Show();ClockOverlayChecks.Pump(60);
                bool compact=host.Bounds.Width<700;
                var globalTab=Action("Global text");var panelsTab=Action("Panel overrides");
                var tabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={globalTab,panelsTab}};
                var heading=new StackPanel{Spacing=4,Children={Text("Fonts & colors",23,Ink),Text("Keep your text clear, from timer digits to everyday labels.",12)}};
                var fields=new StackPanel{Spacing=12};var example=new StackPanel{Spacing=10};
                var exampleCard=Card(example);
                var body=new Grid{ColumnDefinitions=new(compact?"*":"1.03*,16,1*"),RowDefinitions=new(compact?"Auto,12,Auto":"Auto"),Children={fields,exampleCard}};
                Grid.SetRow(exampleCard,compact?2:0);Grid.SetColumn(exampleCard,compact?0:2);
                var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,5,0)};
                var page=new Grid{RowDefinitions=new("Auto,12,34,14,*"),Children={heading,tabs,scroll}};
                Grid.SetRow(tabs,2);Grid.SetRow(scroll,4);Grid.SetColumnSpan(page,3);host.Children.Add(page);
                window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="SettingsSaveStatus").Text="Design preview · sample settings only";
                var panelChoice=new ComboBox{ItemsSource=TypographySettings.Scopes.Select(s=>s.Label).ToArray(),SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch,Height=36,MinWidth=0,FontSize=12,FontFamily=Body,Classes={"editorField"}};
                AutomationProperties.SetName(panelChoice,"Panel to customize");
                var scopeStatus=Text("",11,Teal);var sampleStatus=Text("",11);
                var sampleHeading=Text("Today's progress",23,Ink);sampleHeading.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
                var sampleProject=Text("Programming",16,Ink);var sampleTime=Text("00:44:27",32,Ink);sampleTime.FontFamily=StopwatchTypography.Resolve("navigator-bold").TimerFont;
                var sampleDetail=Text("2 sessions · Today",11);var sampleAccent=Text("View time records",12,Teal);
                var selectedText=Text("Programming  ·  Selected",12,Paper);
                var selected=new Border{Background=Ink,CornerRadius=new(4),Padding=new(10),Child=selectedText};
                var specimen=Card(new StackPanel{Spacing=11,Children={sampleHeading,new Border{Height=1,Background=Rule},sampleProject,sampleTime,sampleDetail,selected,sampleAccent}});specimen.Background=Paper;
                example.Children.Add(Text("Live text sample",18,Ink));example.Children.Add(sampleStatus);example.Children.Add(specimen);
                example.Children.Add(Text("Text keeps its proportions. Selection colors stay readable.",11));
                bool overrides=false;
                void Refresh()
                {
                    string key=TypographySettings.Scopes[panelChoice.SelectedIndex].Key;
                    var style=overrides?settings.Typography.Resolve(key):settings.Typography.Global;
                    IBrush? color=style.Color switch{TypographySettings.ThemeDefault=>null,TypographySettings.ThemeText=>Ink,TypographySettings.ThemeAccent=>Teal,TypographySettings.ThemeMuted=>Muted,_=>Brush.Parse(style.Color)};
                    foreach(var (label,size,original) in new[]{(sampleHeading,23d,Ink),(sampleProject,16d,Ink),(sampleTime,32d,Ink),(sampleDetail,11d,Muted),(sampleAccent,12d,Teal),(selectedText,12d,Paper)})
                    {label.FontSize=size*style.SizePercent/100;label.Foreground=label==selectedText?Paper:color??original;}
                    sampleStatus.Text=$"{(overrides?panelChoice.SelectedItem:"All panels")} · {style.SizePercent:0}%";
                    scopeStatus.Text=$"{TypographySettings.Scopes.Length-settings.Typography.Sections.Values.Count(s=>s.Enabled)} panels follow global text";
                }
                var customize=Action("Customize panels");customize.HorizontalAlignment=HorizontalAlignment.Stretch;
                void Draw()
                {
                    foreach(var editor in sections.Prepend(global))if(editor.Parent is Panel old)old.Children.Remove(editor);
                    if(panelChoice.Parent is Panel oldChoice)oldChoice.Children.Remove(panelChoice);
                    if(scopeStatus.Parent is Panel oldStatus)oldStatus.Children.Remove(scopeStatus);
                    if(customize.Parent is Panel oldButton)oldButton.Children.Remove(customize);
                    fields.Children.Clear();globalTab.Classes.Set("editorPrimary",!overrides);panelsTab.Classes.Set("editorPrimary",overrides);
                    if(overrides)
                    {
                        var editor=sections[panelChoice.SelectedIndex];
                        fields.Children.Add(Card(new StackPanel{Spacing=10,Children={Text("PANEL OVERRIDE",10),panelChoice,editor,Text("Turn off the override to follow global text. Your panel choices are kept.",11)}}));
                    }
                    else
                    {
                        fields.Children.Add(Card(new StackPanel{Spacing=10,Children={Text("GLOBAL TEXT",10),global,Text("Theme default keeps the theme's readable text and selection colors.",11,Teal)}}));
                        fields.Children.Add(Card(new StackPanel{Spacing=9,Children={Text("Make an exception",16,Ink),scopeStatus,customize}}));
                    }
                    fields.Children.Add(Text("Text size changes the lettering. Page scale is in Appearance; font styles stay with your theme.",11));
                    Refresh();scroll.Offset=default;
                }
                globalTab.Click+=(_,_)=>{overrides=false;Draw();};panelsTab.Click+=(_,_)=>{overrides=true;Draw();};customize.Click+=(_,_)=>{overrides=true;Draw();};
                panelChoice.SelectionChanged+=(_,_)=>{if(overrides)Draw();};
                foreach(var editor in sections.Prepend(global))editor.GetType().GetEvent("Changed",Hidden)!.GetAddMethod(true)!.Invoke(editor,[new Action(Refresh)]);
                Draw();ClockOverlayChecks.Pump(60);
                var done=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SettingsDone");
                double footerY=done.TranslatePoint(default,window)!.Value.Y;
                string name=$"{(smallWindow?"compact":"wide")}-{percent}";
                CheckLayout();Save(window,name);
                scroll.ScrollToEnd();ClockOverlayChecks.Pump(30);
                Require(done.TranslatePoint(default,window)!.Value.Y==footerY,"Scrolling moved Done.");
                if(compact)Save(window,name+"-more");
                scroll.Offset=default;
                if(!smallWindow&&percent==90)
                {
                    customize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(25);
                    var editor=sections[1];var check=editor.GetLogicalDescendants().OfType<CheckBox>().Single();check.IsChecked=true;
                    var slider=editor.GetLogicalDescendants().OfType<Slider>().Single();slider.Value=115;
                    var color=editor.GetLogicalDescendants().OfType<ComboBox>().Single();color.SelectedItem=TypographySettings.ThemeAccent;
                    ClockOverlayChecks.Pump(40);CheckLayout();Save(window,"panel-override");
                    Require(settings.Typography.Resolve("Dashboard").SizePercent==115&&sampleTime.FontSize==36.8,"Override did not update sample.");
                    Require(settings.Typography.Global.SizePercent==100,"Override changed global settings.");
                    check.IsChecked=false;Require(settings.Typography.Sections["Dashboard"].SizePercent==115&&sampleTime.FontSize==32,"Disabling override lost choices or inheritance.");
                    ClockOverlayChecks.Pump(30);Save(window,"panel-inherited");
                    globalTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var hex=global.GetLogicalDescendants().OfType<TextBox>().Single();hex.Text="#207F78";
                    hex.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Enter});
                    Require(settings.Typography.Global.Color=="#207F78","Custom color did not commit.");
                    ClockOverlayChecks.Pump(30);Save(window,"custom-color");
                    hex.Text="#12XYZ";hex.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Enter});
                    Require(settings.Typography.Global.Color=="#207F78","Invalid color replaced the valid value.");
                    Require(global.GetLogicalDescendants().OfType<TextBlock>().Single(t=>t.Text?.StartsWith("Enter a valid hex")==true).IsVisible,"Validation missing.");
                    ClockOverlayChecks.Pump(30);Save(window,"invalid-color");
                }
                evidence.Add(new{name,percent,windowWidth=window.ClientSize.Width,windowHeight=window.ClientSize.Height,layout=compact?"stacked":"side-by-side",nativeRenderer=true,panelChoices=panelChoice.Items.Count,colorChoices=TypographySettings.ColorChoices.Length});
                void CheckLayout()
                {
                    Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Content overflows horizontally.");
                    Require(window.GetVisualDescendants().OfType<Button>().Count(b=>b.Name?.StartsWith("SettingsNav_")==true)==13,"Shell navigation changed.");
                    var bottom=done.TranslatePoint(new Point(done.Bounds.Width,done.Bounds.Height),window)!.Value;
                    Require(bottom.Y<=window.ClientSize.Height+1&&bottom.X<=window.ClientSize.Width+1,"Done is outside window.");
                    Require(window.Content is LayoutTransformControl scale&&Math.Abs(scale.LayoutTransform!.Value.M11-percent/100d)<.001,"Actual page scale not applied.");
                }
            }
            finally{window.Close();Page("Shutdown");}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="design-preview-only",execution="Private Windows desktop; synthetic settings; no persistence subscribers or installation.",states=evidence,checks=new[]{"Six window and scale combinations","Shared frame and 13 navigation items","Fixed Done footer","No horizontal overflow","Panel inheritance retains choices","Native custom color validation","Preview responds to existing editor events"}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS native Fonts & colors preview: six scale/layout combinations, override/inheritance, custom hex validation, fixed footer; synthetic settings only.");
    }

    static void StyleEditor(StackPanel editor)
    {
        editor.Margin=new(0);
        foreach(var text in editor.GetLogicalDescendants().OfType<TextBlock>()){text.FontFamily=Body;text.Foreground=Ink;}
        foreach(var control in editor.GetLogicalDescendants().OfType<TemplatedControl>().Where(c=>c is ComboBox or TextBox))
        {control.Classes.Add("editorField");control.FontFamily=Body;control.FontSize=12;control.MinHeight=0;control.Height=36;control.MinWidth=0;}
        foreach(var check in editor.GetLogicalDescendants().OfType<CheckBox>()){check.Content="Use separate text settings";check.FontFamily=Body;check.FontSize=12;check.Foreground=Ink;}
        var fields=(StackPanel)editor.Children.Last();var custom=(Grid)fields.Children[^2];
        fields.Children.Insert(fields.Children.IndexOf(custom),Text("Custom color",11));
        var hex=editor.GetLogicalDescendants().OfType<TextBox>().Single();AutomationProperties.SetName(hex,"Custom text color");
        var slider=editor.GetLogicalDescendants().OfType<Slider>().Single();AutomationProperties.SetName(slider,"Text size");
        var colors=editor.GetLogicalDescendants().OfType<ComboBox>().Single();AutomationProperties.SetName(colors,"Text color preset");
        var validation=editor.GetLogicalDescendants().OfType<TextBlock>().Single(t=>t.Text?.StartsWith("Enter a valid hex")==true);validation.Foreground=Brush.Parse("#8F3328");
    }
    static TextBlock Text(string text,double size,IBrush? brush=null)=>new(){Text=text,FontSize=size,FontFamily=Body,Foreground=brush??Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    static Border Card(Control content)=>new(){Child=content,Padding=new(14),CornerRadius=new(7),Background=Brush.Parse("#40FFF6E4"),BorderBrush=Rule,BorderThickness=new(1),VerticalAlignment=VerticalAlignment.Top};
    static Button Action(string title)=>new(){Content=title,Height=34,MinHeight=0,MinWidth=0,Padding=new(14,0),FontFamily=Body,Classes={"editorAction"}};
    static object? Page(string method,params object[] args)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Save(Window window,string name)
    {
        var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
        // shortcut: immediate export culls downscaled children; use a compositor capture if this fixture needs more transform types.
        if(((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11>=1)bitmap.Render(window);
        else
        {
            // The brush includes the page transform, so its destination stays in logical units.
            using var context=bitmap.CreateDrawingContext();
            context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));
        }
        var done=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SettingsDone");
        var point=done.TranslatePoint(new Point(8,8),window)!.Value;
        var pixel=new byte[4];var buffer=Marshal.AllocHGlobal(4);
        try{bitmap.CopyPixels(new PixelRect((int)point.X,(int)point.Y,1,1),buffer,4,4);Marshal.Copy(buffer,pixel,0,4);}finally{Marshal.FreeHGlobal(buffer);}
        Require(pixel[3]>200&&pixel.Take(3).All(c=>c<100),$"{name}: Done is missing from capture at {point}; pixel {string.Join(',',pixel)}.");
        bitmap.Save(Path.Combine(Output,name+".png"));
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
