using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsTypographyNativeChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Output="design/one-piece-v2/settings/typography-implemented";
    static Type Service=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopTypography")??throw new Exception("Desktop typography application service is missing.");
    static object? Call(string name,params object[] args)=>Service.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static void Application()
    {
        var settings=new TypographySettings();settings.Normalize();Call("Initialize",settings);
        var source=new TextBlock{FontSize=20};var bound=new TextBlock{Text="Bound"};bound.Bind(TextBlock.FontSizeProperty,source.GetObservable(TextBlock.FontSizeProperty));
        var inherited=new TextBlock{Text="Inherited"};var box=new TextBox{Text="Input",FontSize=18};
        var root=new StackPanel{Children={bound,inherited,box}};var window=new Window{Content=root,Width=400,Height=300,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            Call("SetScope",root,"Dashboard");window.Show();ClockOverlayChecks.Pump(50);double original=inherited.FontSize;
            settings.Global.SizePercent=125;Call("Apply",settings);
            Require(bound.FontSize==25,"Bound text does not scale to 125%.");Require(Math.Abs(inherited.FontSize-original*1.25)<.001,"Inherited text did not scale.");Require(box.FontSize==22.5,"Input text did not scale once.");
            source.FontSize=24;ClockOverlayChecks.Pump(20);Require(bound.FontSize==30,"Changing source no longer updates bound text.");
            settings.Global.SizePercent=150;Call("Apply",settings);Call("Apply",settings);Require(bound.FontSize==36,"Repeated application compounded the size.");
            settings.Sections["Dashboard"].Enabled=true;settings.Sections["Dashboard"].SizePercent=175;Call("Apply",settings);Require(bound.FontSize==42,"Dashboard scope is not applied.");
            settings.Sections["Dashboard"].Enabled=false;Call("Apply",settings);Require(bound.FontSize==36,"Disabled override does not inherit global.");
            settings.Global.SizePercent=100;Call("Apply",settings);source.FontSize=26;Require(bound.FontSize==26,"Reset destroyed original binding.");
            var later=new TextBlock{Text="Added later",FontSize=20};root.Children.Add(later);settings.Global.SizePercent=125;Call("Apply",settings);ClockOverlayChecks.Pump(30);Require(later.FontSize==25,"New text is not styled.");
            settings.Global.Color="#207F78";Call("Apply",settings);Require(((ISolidColorBrush)bound.Foreground!).Color==Color.Parse("#207F78"),"Custom color is not applied.");
            bound.Resources["SampleInk"]=Brushes.Red;bound.Bind(TextBlock.ForegroundProperty,bound.GetResourceObservable("SampleInk"));
            settings.Global.Color=TypographySettings.ThemeDefault;Call("Apply",settings);Require(Equals(bound.Foreground,Brushes.Red),"Theme default did not restore underlying resource.");
            bound.Resources["SampleInk"]=Brushes.Blue;Require(Equals(bound.Foreground,Brushes.Blue),"Resource updates were lost.");
            var small=new Run("fraction"){FontSize=10};var rich=new TextBlock{FontSize=20,Inlines=new InlineCollection{new Run("digits"),small}};root.Children.Add(rich);ClockOverlayChecks.Pump(30);Call("Apply",settings);
            Require(small.FontSize==12.5,"Explicit inline size did not preserve proportions.");
            settings.Global.SizePercent=100;Call("Apply",settings);Require(small.FontSize==10,"Inline reset did not restore its source.");
            later.ClearValue(TextBlock.FontSizeProperty);settings.Global.SizePercent=125;Call("Apply",settings);
            Require(Math.Abs(later.FontSize-original*1.25)<.001,"Clearing a local size stopped inherited scaling.");
        }
        finally{window.Close();Call("Shutdown");}
    }
    internal static void Panel()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika};
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
        var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Typography"));window.Show();ClockOverlayChecks.Pump(40);
            Require(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Name=="TypographyGlobalTab"),"Approved typography panel has not been integrated.");
        }
        finally{window.Close();}
    }
    internal static void PopupsAndClocks()
    {
        var settings=new TypographySettings();settings.Normalize();Call("Initialize",settings);
        var combo=new ComboBox{ItemsSource=new[]{"First choice","Second choice"},SelectedIndex=0};
        var face=new ThemedClockFace{Text="00:05:10"};face.ApplySettings(new AppSettings{ThemeMode=AppThemeCatalog.Nika,OverlayTheme=OverlayThemeCatalog.Nika});Call("SetScope",face,"Overlay");
        var actionText=new TextBlock{Text="Selected action",Foreground=Brushes.White,FontSize=12};var action=new Button{Content=actionText,Background=Brushes.Navy};
        var content=new StackPanel{Children={combo,face,action}};Call("SetScope",content,"Dashboard");
        var window=new Window{Content=content,Width=520,Height=420,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(40);combo.IsDropDownOpen=true;ClockOverlayChecks.Pump(40);
            var popup=combo.GetVisualDescendants().OfType<Popup>().First();var label=popup.Child!.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text=="Second choice");
            double size=label.FontSize;Require((string)Call("ResolveScope",label)! == "Dashboard","Popup lost its owner's scope.");
            settings.Sections["Dashboard"].Enabled=true;settings.Sections["Dashboard"].SizePercent=150;settings.Global.Color="#207F78";Call("Apply",settings);ClockOverlayChecks.Pump(30);
            Require(Math.Abs(label.FontSize-size*1.5)<.001,"Open popup did not update its font.");Require(Equals(actionText.Foreground,Brushes.White),"Custom text color reduced button readability.");
            settings.Sections["Overlay"].Enabled=true;settings.Sections["Overlay"].SizePercent=150;settings.Sections["Overlay"].Color="#207F78";Call("Apply",settings);
            Require(face.TextSize==72&&((ISolidColorBrush)face.TimerBrush).Color==Color.Parse("#207F78"),"Painted overlay ignored its scope.");
            settings.Global.Color=TypographySettings.ThemeDefault;settings.Sections["Overlay"].Enabled=false;Call("Apply",settings);Require(face.TextSize==48,"Painted overlay did not reset.");
            combo.IsDropDownOpen=false;
        }
        finally{window.Close();Call("Shutdown");}
    }
    internal static void ProductionEvidence()
    {
        Directory.CreateDirectory(Output);var evidence=new List<object>();
        foreach(bool compact in new[]{false,true})foreach(int scale in new[]{70,90,125})
        {
            var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=scale};Call("Initialize",settings.Typography);Page("Initialize",settings);Theme(AppThemeCatalog.Nika);
            var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false,Width=compact?840:1120,Height=compact?600:720};
            typeof(SettingsWindow).GetEvent("SettingsChanged",Hidden)!.GetAddMethod(true)!.Invoke(window,[new Action<SettingsChangeKind>(_=>Call("Apply",settings.Typography))]);
            try
            {
                var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Typography"));window.Show();ClockOverlayChecks.Pump(60);
                string name=$"{(compact?"compact":"wide")}-{scale}";CheckBounds(window);Save(window,name);
                var scroll=Find<ScrollViewer>(window,"TypographyScroll");var done=Find<Button>(window,"SettingsDone");var before=done.TranslatePoint(default,window);
                scroll.ScrollToEnd();ClockOverlayChecks.Pump(20);Require(done.TranslatePoint(default,window)==before,"Content scrolling moved Done.");scroll.Offset=default;
                if(!compact&&scale==90)
                {
                    Find<Button>(window,"TypographyPanelsTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(30);
                    var choice=Find<ComboBox>(window,"TypographyPanelChoice");Require(choice.Items.Count==6,"Panel scope options changed.");
                    var editor=(Control)((Array)typeof(SettingsWindow).GetField("_typographySections",Hidden)!.GetValue(window)!).GetValue(1)!;
                    editor.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked=true;
                    editor.GetLogicalDescendants().OfType<Slider>().Single().Value=115;
                    editor.GetLogicalDescendants().OfType<ComboBox>().Single().SelectedItem=TypographySettings.ThemeAccent;
                    ClockOverlayChecks.Pump(40);Require(Math.Abs(Find<TextBlock>(window,"TypographySampleTime").FontSize-36.8)<.001,"Live panel specimen does not use the real service.");Save(window,"panel-override");
                    editor.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked=false;Require(settings.Typography.Sections["Dashboard"].SizePercent==115&&Find<TextBlock>(window,"TypographySampleTime").FontSize==32,"Turning off an override lost its choices or inheritance.");
                    Find<Button>(window,"TypographyGlobalTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var global=(Control)typeof(SettingsWindow).GetField("_typographyGlobal",Hidden)!.GetValue(window)!;
                    var hex=global.GetLogicalDescendants().OfType<TextBox>().Single();hex.Text="#17394A";hex.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Enter});
                    Require(settings.Typography.Global.Color=="#17394A","Custom hex did not apply.");
                    Require(global.GetLogicalDescendants().OfType<ColorPicker>().Single().Color==Color.Parse("#17394A"),"Picker swatch did not follow hex input.");
                    hex.Text="#XX";hex.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Enter});Require(settings.Typography.Global.Color=="#17394A","Invalid hex replaced the last valid color.");
                    ClockOverlayChecks.Pump(30);Save(window,"invalid-color");
                    foreach(int textSize in new[]{75,175,100})
                    {global.GetLogicalDescendants().OfType<Slider>().Single().Value=textSize;ClockOverlayChecks.Pump(40);CheckBounds(window);Save(window,"text-"+textSize);}
                    settings.ThemeMode=AppThemeCatalog.Midnight;Theme(settings.ThemeMode);typeof(SettingsWindow).GetMethod("UpdatePreviewSafely",Hidden)!.Invoke(window,null);ClockOverlayChecks.Pump(30);
                    Require(window.FindControl<StackPanel>("GlobalTypographyEditor")!.Children.Count==1&&window.FindControl<StackPanel>("SectionTypographyEditors")!.Children.Count==6,"Theme restoration lost the original editors.");
                    settings.ThemeMode=AppThemeCatalog.Nika;Theme(settings.ThemeMode);typeof(SettingsWindow).GetMethod("UpdatePreviewSafely",Hidden)!.Invoke(window,null);ClockOverlayChecks.Pump(30);
                    Require(Find<Button>(window,"TypographyGlobalTab").IsEffectivelyVisible,"Returning to Nika did not restore typography page.");
                }
                evidence.Add(new{name,scale,nativeWidth=window.ClientSize.Width,nativeHeight=window.ClientSize.Height});
                var globalEditor=(Control)typeof(SettingsWindow).GetField("_typographyGlobal",Hidden)!.GetValue(window)!;
                globalEditor.GetLogicalDescendants().OfType<Slider>().Single().Value=175;
                ClockOverlayChecks.Pump(40);CheckBounds(window);Save(window,name+"-large-text");
                if(compact&&scale==125)
                {
                    var body=(Grid)typeof(SettingsWindow).GetField("_typographyBody",Hidden)!.GetValue(window)!;
                    scroll.Offset=new Vector(0,body.Bounds.Y);ClockOverlayChecks.Pump(20);CheckBounds(window);Save(window,name+"-large-text-controls");
                }
            }
            finally{window.Close();Page("Shutdown");Call("Shutdown");}
        }
        File.WriteAllText(Path.Combine(Output,"verification.json"),JsonSerializer.Serialize(new{status="production-native",states=evidence,execution="Private desktop; synthetic settings; no persistence, installation or restart"},new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void SelectionAndLifecycle()
    {
        var settings=new AppSettings();settings.Typography.Global.Color="#207F78";Call("Initialize",settings.Typography);
        var label=new TextBlock{Text="Selectable",Foreground=Brushes.White,FontSize=20};
        var item=new ListBoxItem{Content=label};var list=new ListBox{ItemsSource=new[]{item}};
        var window=new Window{Content=list,Width=350,Height=200,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(30);Require(((ISolidColorBrush)label.Foreground!).Color==Color.Parse("#207F78"),"Unselected item did not use custom color.");
            list.SelectedIndex=0;ClockOverlayChecks.Pump(30);Require(Equals(label.Foreground,Brushes.White),"Selection did not restore readable original text.");
            list.SelectedIndex=-1;ClockOverlayChecks.Pump(30);Require(((ISolidColorBrush)label.Foreground!).Color==Color.Parse("#207F78"),"Deselection did not restore custom color.");
            var controller=(ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
            try
            {
                settings.Typography.Global.SizePercent=150;
                typeof(ControllerWindow).GetField("_pendingTypography",Hidden)!.SetValue(controller,true);
                Require(label.FontSize==20,"Uncommitted slider adjustment resized unrelated text.");
                typeof(ControllerWindow).GetMethod("CompleteSettingsInteraction",Hidden)!.Invoke(controller,null);
                Require(label.FontSize==30,"Ending adjustment did not apply typography.");
            }
            finally{controller.Close();}
        }
        finally
        {
            window.Close();ClockOverlayChecks.Pump(30);
            int remaining=((System.Collections.IDictionary)Service.GetField("Anchors",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Count;
            Call("Shutdown");Require(remaining==0,"Closing controls did not release typography anchors before shutdown.");
        }
    }
    internal static void PreviewScopes()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika};settings.Typography.Normalize();
        settings.Typography.Sections["Overlay"]=new TypographyStyle{Enabled=true,SizePercent=175,Color="#C00000"};
        settings.Typography.Sections["Settings"]=new TypographyStyle{Enabled=true,SizePercent=100,Color="#0000C0"};
        Call("Initialize",settings.Typography);Theme(settings.ThemeMode);
        var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show();ClockOverlayChecks.Pump(40);var nav=window.FindControl<ListBox>("NavigationList")!;
            foreach(string category in new[]{"Overlay","Appearance","StopwatchAppearance"})
            {
                nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,category));
                if(category=="Appearance")Find<Button>(window,"AppearanceClockTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(40);Call("Apply",settings.Typography);
                var faces=window.GetVisualDescendants().OfType<ThemedClockFace>().Where(c=>c.IsEffectivelyVisible).ToArray();Require(faces.Length>0,"No visible clock preview for "+category);
                foreach(var face in faces)Require((string)Call("ResolveScope",face)! == "Overlay"&&((ISolidColorBrush)face.TimerBrush).Color==Color.Parse("#C00000"),category+" clock preview does not use Overlay typography.");
            }
        }
        finally{window.Close();Call("Shutdown");}
    }
    internal static void ReopenedCustomColor()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika};settings.Typography.Global.Color="#17394A";Theme(settings.ThemeMode);
        var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            var global=window.FindControl<StackPanel>("GlobalTypographyEditor")!;
            var colors=global.GetLogicalDescendants().OfType<ComboBox>().Single();
            Require(Equals(colors.SelectedItem,"#17394A"),"Reopened custom color is missing from preset selection.");
        }
        finally{window.Close();}
    }
    internal static void GestureRouting()
    {
        Theme(AppThemeCatalog.Nika);var window=new SettingsWindow(new AppSettings{ThemeMode=AppThemeCatalog.Nika}){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Typography"));window.Show();ClockOverlayChecks.Pump(30);
            var global=(Control)typeof(SettingsWindow).GetField("_typographyGlobal",Hidden)!.GetValue(window)!;var slider=global.GetLogicalDescendants().OfType<Slider>().Single();
            int started=0,completed=0;
            typeof(SettingsWindow).GetEvent("SettingsInteractionStarted",Hidden)!.GetAddMethod(true)!.Invoke(window,[new Action(()=>started++)]);
            typeof(SettingsWindow).GetEvent("SettingsInteractionCompleted",Hidden)!.GetAddMethod(true)!.Invoke(window,[new Action(()=>completed++)]);
            var thumb=slider.GetVisualDescendants().OfType<Thumb>().First();var pointer=new Avalonia.Input.Pointer(1242,PointerType.Mouse,true);var point=thumb.TranslatePoint(new Point(3,3),window)!.Value;
            thumb.RaiseEvent(new PointerPressedEventArgs(thumb,pointer,window,point,0,new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed),KeyModifiers.None,1){Handled=true});
            Require(started==1,"Handled thumb press did not begin deferred adjustment.");
            thumb.RaiseEvent(new PointerReleasedEventArgs(thumb,pointer,window,point,1,new PointerPointProperties(RawInputModifiers.None,PointerUpdateKind.LeftButtonReleased),KeyModifiers.None,MouseButton.Left){Handled=true});
            Require(completed==1,"Handled pointer release did not complete adjustment.");
            slider.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Right,Handled=true});Require(started==2,"Keyboard adjustment did not begin deferral.");
            slider.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyUpEvent,Key=Avalonia.Input.Key.Right,Handled=true});Require(completed==2,"Keyboard release did not complete adjustment.");
        }
        finally{window.Close();}
    }
    internal static void ApplicationPreview()
    {
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika};settings.Typography.Normalize();settings.Typography.Sections["Controller"]=new TypographyStyle{Enabled=true,SizePercent=125};
        Theme(settings.ThemeMode);Call("Initialize",settings.Typography);var window=new SettingsWindow(settings){ShowActivated=false,ShowInTaskbar=false};
        try
        {
            var nav=window.FindControl<ListBox>("NavigationList")!;nav.SelectedItem=nav.Items.OfType<ListBoxItem>().Single(i=>Equals(i.Tag,"Appearance"));window.Show();ClockOverlayChecks.Pump(40);
            var controller=(ControllerWindow)typeof(SettingsWindow).GetField("_appearanceController",Hidden)!.GetValue(window)!;
            var timerText=controller.FindControl<TextBlock>("NikaTimeDisplay")!;
            Require(timerText.FontSize==270,$"Synthetic preview font {timerText.FontSize}; root {timerText.GetVisualRoot()?.GetType().Name??"none"}; scope {Call("ResolveScope",timerText)}.");
            var before=typeof(SettingsWindow).GetField("_appearanceSnapshot",Hidden)!.GetValue(window);
            settings.Typography.Sections["Controller"].SizePercent=150;Call("Apply",settings.Typography);
            typeof(SettingsWindow).GetMethod("RefreshAppearancePreview",Hidden)!.Invoke(window,null);
            Require(!ReferenceEquals(before,typeof(SettingsWindow).GetField("_appearanceSnapshot",Hidden)!.GetValue(window)),"Application snapshot cache ignored typography changes.");
            Require(controller.FindControl<TextBlock>("NikaTimeDisplay")!.FontSize==324,"Refreshed application preview kept the old font size.");
            var anchors=(System.Collections.IDictionary)Service.GetField("Anchors",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            Require(!anchors.Keys.Cast<Control>().Any(c=>ReferenceEquals(c.GetVisualRoot(),controller)),"Synthetic snapshot retained typography anchors.");
        }
        finally{window.Close();Call("Shutdown");}
    }
    static T Find<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Theme(string theme)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
    static object? Page(string method,params object[] args)=>typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void CheckBounds(Window window)
    {
        var scroll=Find<ScrollViewer>(window,"TypographyScroll");Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Typography content overflows horizontally.");
        Require(scroll.Viewport.Height>=80,"Large text leaves too little room to reach the editing controls.");
        var done=Find<Button>(window,"SettingsDone");var bottom=done.TranslatePoint(new Point(done.Bounds.Width,done.Bounds.Height),window)!.Value;
        Require(bottom.Y<=window.ClientSize.Height+1&&bottom.X<=window.ClientSize.Width+1,"Done is outside the native window.");
    }
    static void Save(Window window,string name)
    {
        var frame=window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width),(int)Math.Ceiling(window.ClientSize.Height)),new Vector(96,96));
        if(((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11>=1)bitmap.Render(window);
        else{using var context=bitmap.CreateDrawingContext();context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        var done=Find<Button>(window,"SettingsDone");var point=done.TranslatePoint(new Point(8,8),window)!.Value;var pixel=new byte[4];var buffer=Marshal.AllocHGlobal(4);
        try{bitmap.CopyPixels(new PixelRect((int)point.X,(int)point.Y,1,1),buffer,4,4);Marshal.Copy(buffer,pixel,0,4);}finally{Marshal.FreeHGlobal(buffer);}
        Require(pixel[3]>200&&pixel.Take(3).All(c=>c<100),name+": native capture lost the footer.");bitmap.Save(Path.Combine(Output,name+".png"));
    }
}
