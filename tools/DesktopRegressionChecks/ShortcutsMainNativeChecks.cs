using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ShortcutsMainNativeChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (int scale in new[] {70,90,125})
        {
            var settings = new AppSettings { UiScalePercent=scale,ThemeMode=AppThemeCatalog.Nika };
            Desktop("DesktopPageAppearance","Initialize",settings);
            var w=new ShortcutsWindow {ShowActivated=false,ShowInTaskbar=false};
            try
            {
                var apply=typeof(ShortcutsWindow).GetMethod("ApplyNavigatorLayout",Hidden);
                Require(apply!=null,"Native Main Shortcuts layout is missing."); apply!.Invoke(w,[AppThemeCatalog.Nika]);
                w.Show(); Pump();
                var scroll=Visual<ScrollViewer>(w,"MainShortcutsScroll");
                var save=Visual<Button>(w,"ShortcutsSave"); var footer=save.TranslatePoint(default,w);
                Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Horizontal overflow.");
                Require(Field<TextBox>(w,"LeaderShortcutBox").Bounds.Width>200,"Reparented shortcut field is squeezed into its old grid column.");
                Capture(w,$"main-{scale}"); scroll.ScrollToEnd(); Pump();
                Require(footer==save.TranslatePoint(default,w),"Footer moved."); Capture(w,$"timing-{scale}");
                var leader=Field<TextBox>(w,"LeaderShortcutBox"); leader.BringIntoView(); leader.Focus(); Pump();
                if(scale==90) Capture(w,"recording");
                var original=w.ResultLeader; Key(leader,Avalonia.Input.Key.Escape);
                Require(w.ResultLeader==original,"Escape changed the saved draft.");
                leader.Focus(); Key(leader,Avalonia.Input.Key.LeftCtrl,KeyModifiers.Control); Require(w.ResultLeader==original,"Modifier-only capture changed draft.");
                Key(leader,Avalonia.Input.Key.F8,KeyModifiers.Control); Require(w.ResultLeader.Format()=="Ctrl+F8","Capture did not update draft.");
                var note=Field<TextBox>(w,"NoteLeaderShortcutBox"); note.Focus(); Key(note,Avalonia.Input.Key.F8,KeyModifiers.Control);
                Click(save); Require(w.DialogResult!=true && Field<TextBlock>(w,"PortalRegistrationText").IsVisible,"Duplicate globals were saved.");
                if(scale==90) Capture(w,"duplicate");
                Click(Visual<Button>(w,"ShortcutsReset")); Require(w.ResultLeader==AppSettings.DefaultLeaderShortcut(),"Reset failed.");
                Require(!Field<TextBlock>(w,"PortalRegistrationText").IsVisible,"Reset left stale duplicate validation visible.");
                var map=Field<Dictionary<ShortcutAction,uint>>(w,"_pendingCommandModeKeys");
                map[ShortcutAction.Reset]=map[ShortcutAction.StartStop];
                Click(save); Pump(); Require(Visual<Control>(w,"TimerCommandPage").IsEffectivelyVisible,"Save did not reveal invalid command mappings.");
                Click(Visual<Button>(w,"ShortcutsReset"));
                Click(Visual<Button>(w,"ShortcutsMappingsTab")); Pump(); Require(Visual<Control>(w,"CommandMappingsPage").IsEffectivelyVisible,"Mappings inaccessible.");
                Click(Visual<Button>(w,"ShortcutsMainTab")); Pump();
                var tab=new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Tab};leader.RaiseEvent(tab);
                Require(w.ResultLeader==AppSettings.DefaultLeaderShortcut(),"Tab was captured as a shortcut.");
                Click(Visual<Button>(w,"ShortcutsClear2")); Require(w.ResultShowActiveOverlay.VirtualKey==0,"Clear changed the wrong shortcut.");
                if(scale==125)
                {
                    Field<Slider>(w,"ChainingTimeoutSlider").Value=.8;
                    Click(save);Require(w.DialogResult==true && w.ResultShowActiveOverlay.VirtualKey==0 && w.ResultChainingTimeoutSeconds==.8,"Native Save lost drafts.");
                }
                else { Click(Visual<Button>(w,"ShortcutsCancel")); Require(w.DialogResult==false,"Cancel accepted drafts."); }
                Console.WriteLine($"PASS Main Shortcuts {scale}% capture, defaults, validation, navigation and layout");
            }
            finally {w.Close();Desktop("DesktopPageAppearance","Shutdown");}
        }
        usingOutcome();
        static void usingOutcome()
        {
            var input=AppSettings.DefaultCommandModeKeys();var original=input[ShortcutAction.Reset];
            var w=new ShortcutsWindow(AppSettings.DefaultLeaderShortcut(),commandModeKeys:input){ShowActivated=false,ShowInTaskbar=false};
            try {
                typeof(ShortcutsWindow).GetMethod("ApplyNavigatorLayout",Hidden)!.Invoke(w,[AppThemeCatalog.Daylight]);
                w.Show();Pump();Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Legacy theme replaced.");
                Key(Field<TextBox>(w,"ShowActiveOverlayShortcutBox"),Avalonia.Input.Key.F9,KeyModifiers.Control);
                Field<Slider>(w,"ChainingTimeoutSlider").Value=.9;
                typeof(ShortcutsWindow).GetMethod("Save_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
                Require(w.DialogResult==true && w.ResultChainingTimeoutSeconds==.9 && w.ResultShowActiveOverlay.Format()=="Ctrl+F9","Save did not retain edited values.");
                Require(input[ShortcutAction.Reset]==original && w.ResultCommandModeKeys[ShortcutAction.Reset]==original,"Main edits changed command mappings.");
            } finally {w.Close();}
        }
    }
    internal static void EscapeCapture()
    {
        var w=new ShortcutsWindow {ShowActivated=false,ShowInTaskbar=false};
        try {
            w.Show();Pump();var field=Field<TextBox>(w,"LeaderShortcutBox");field.Focus();var before=w.ResultLeader;Key(field,Avalonia.Input.Key.Escape);Require(w.ResultLeader==before,"Escape must cancel capture without binding Escape.");
            Key(field,Avalonia.Input.Key.F3,KeyModifiers.Meta);
            typeof(ShortcutsWindow).GetMethod("Save_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
            Require(Field<TextBlock>(w,"PortalRegistrationText").IsVisible,"Duplicate failure missing.");
            typeof(ShortcutsWindow).GetMethod("ResetDefaults_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
            Require(!Field<TextBlock>(w,"PortalRegistrationText").IsVisible,"Reset left a resolved duplicate error visible.");
            var status=typeof(ShortcutsWindow).GetMethod("SetRegistrationStatus",Hidden)!;
            status.Invoke(w,["Registration ready"]);
            Key(field,Avalonia.Input.Key.F3,KeyModifiers.Meta);
            typeof(ShortcutsWindow).GetMethod("Save_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
            status.Invoke(w,["New registration error"]);
            typeof(ShortcutsWindow).GetMethod("ResetDefaults_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
            Require(Field<TextBlock>(w,"PortalRegistrationText").Text=="New registration error","Resolving duplicate warning restored stale platform status.");
        }
        finally {w.Close();}
    }
    static T Field<T>(ShortcutsWindow w,string name)=>(T)typeof(ShortcutsWindow).GetField(name,Hidden)!.GetValue(w)!;
    static T Visual<T>(Control w,string name) where T:Control=>w.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    static void Key(Control control,Key key,KeyModifiers mods=KeyModifiers.None)=>control.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=mods});
    static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Pump()=>ClockOverlayChecks.Pump(80);
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Desktop(string type,string method,params object[] args)=>typeof(ShortcutsWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Capture(ShortcutsWindow w,string name)
    {
        const string folder="design/one-piece-v2/shortcuts/main-implemented";Directory.CreateDirectory(folder);
        using var image=new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width,(int)w.ClientSize.Height),new Vector(96,96));
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        if(((LayoutTransformControl)w.Content!).LayoutTransform!.Value.M11>=1)image.Render(w);
        else {using var context=image.CreateDrawingContext();context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        image.Save(Path.Combine(folder,name+".png"));
    }
}
