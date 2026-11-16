using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ShortcutsMappingsNativeChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(ShortcutsWindow w,string name)=>(T)typeof(ShortcutsWindow).GetField(name,Hidden)!.GetValue(w)!;
    static T Find<T>(Control root,string name) where T:Control
    {
        var result=root.GetVisualDescendants().OfType<T>().SingleOrDefault(c=>c.Name==name);
        Require(result!=null,$"Missing production control: {name}");return result!;
    }
    static TextBox Timer(ShortcutsWindow w,ShortcutAction action)=>Field<Dictionary<ShortcutAction,TextBox>>(w,"_winF2TextBoxes")[action];
    static TextBox Note(ShortcutsWindow w,NoteCommandAction action)=>Field<Dictionary<NoteCommandAction,TextBox>>(w,"_winF3TextBoxes")[action];
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Pump()=>ClockOverlayChecks.Pump(80);
    static void Click(Button b){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    static void Click(ShortcutsWindow w,string name)=>Click(Find<Button>(w,name));
    static void Key(Control c,Avalonia.Input.Key key,KeyModifiers mods=KeyModifiers.None)=>c.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=mods});
    static void Desktop(string method,params object[] args)=>typeof(ShortcutsWindow).Assembly.GetType("StopwatchOverlay.Desktop.DesktopPageAppearance")!
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void WithWindow(Action<ShortcutsWindow> check,int scale=90,string? theme=null,Dictionary<ShortcutAction,uint>? timers=null)
    {
        theme??=AppThemeCatalog.Nika;Desktop("Initialize",new AppSettings{ThemeMode=theme,UiScalePercent=scale});
        var w=new ShortcutsWindow(AppSettings.DefaultLeaderShortcut(),commandModeKeys:timers){ShowActivated=false,ShowInTaskbar=false};
        try{typeof(ShortcutsWindow).GetMethod("ApplyNavigatorLayout",Hidden)!.Invoke(w,[theme]);w.Show();Pump();check(w);}
        finally{w.Close();Desktop("Shutdown");}
    }
    internal static void Layout()
    {
        foreach(int scale in new[]{70,90,125})WithWindow(w=>
        {
            var original=w.ResultCommandModeKeys;
            Click(w,"ShortcutsMappingsTab");
            Require(Find<Control>(w,"TimerCommandPage").IsEffectivelyVisible,"Timer family is not reachable.");
            var visible=w.GetVisualDescendants().OfType<TextBox>().Where(b=>b.IsEffectivelyVisible).ToArray();
            Require(visible.Count(b=>b.Tag is ShortcutAction)==21,"Not all timer mappings reached the new page.");
            Require(visible.All(b=>b.Bounds.Width>=90),"Mapping key clipped after reparenting.");
            var scroll=Find<ScrollViewer>(w,"MainShortcutsScroll");var save=Find<Button>(w,"ShortcutsSave");var footer=save.TranslatePoint(default,w);
            Require(scroll.Extent.Width<=scroll.Viewport.Width+1&&scroll.Viewport.Height>100,"Mappings overflow the viewport.");
            Capture(w,$"timers-{scale}");scroll.ScrollToEnd();Pump();Capture(w,$"overlays-{scale}");
            Require(footer==save.TranslatePoint(default,w),"Footer moved during scrolling.");
            Click(w,"NoteCommandsTab");Require(!Timer(w,ShortcutAction.StartStop).IsEffectivelyVisible&&Note(w,NoteCommandAction.AddTodo).IsEffectivelyVisible,"Family switch retained timer controls.");
            Require(w.GetVisualDescendants().OfType<TextBox>().Count(b=>b.IsEffectivelyVisible&&b.Tag is NoteCommandAction)==5,"Note mapping missing.");
            Capture(w,$"notes-{scale}");
            Require(original.All(pair=>w.ResultCommandModeKeys[pair.Key]==pair.Value),"Navigation changed bindings.");
            w.Width=700;w.Height=500;ClockOverlayChecks.Pump(250);scroll.ScrollToEnd();Pump();
            Require(scroll.Viewport.Height>30&&scroll.Extent.Width<=scroll.Viewport.Width+1,"Minimum window loses scroll access.");
            Capture(w,$"minimum-{scale}");
            var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
            Require(Math.Abs(frame.Bounds.X)<2&&Math.Abs(frame.Bounds.Y)<2,$"Frame offset after resize at {scale}%: {frame.Bounds}.");
            var bottomRight=save.TranslatePoint(new Point(save.Bounds.Width,save.Bounds.Height),frame);
            Require(bottomRight is {} point&&point.Y<=frame.Bounds.Height&&point.X<=frame.Bounds.Width,"Save outside frame.");
            Require(Math.Abs(frame.Bounds.Width*scale/100d-w.ClientSize.Width)<2&&Math.Abs(frame.Bounds.Height*scale/100d-w.ClientSize.Height)<2,"Frame exceeds minimum window.");
            w.Width=1120;w.Height=720;Pump();
            Desktop("Apply",new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=125},true);Pump();
            Require(Math.Abs(frame.Bounds.Width*1.25-w.ClientSize.Width)<2&&Math.Abs(frame.Bounds.Height*1.25-w.ClientSize.Height)<2,"Frame failed to follow resize or live page scaling.");
        },scale);
    }
    internal static void CaptureAndValidation()=>WithWindow(w=>
    {
        Click(w,"ShortcutsMappingsTab");var start=Timer(w,ShortcutAction.StartStop);start.Focus();Key(start,Avalonia.Input.Key.F9);
        Require(w.ResultCommandModeKeys[ShortcutAction.StartStop]==0x78,"Timer capture did not update draft.");
        var reset=Timer(w,ShortcutAction.Reset);reset.Focus();Key(reset,Avalonia.Input.Key.F9);Pump();
        Require(w.ResultCommandModeKeys[ShortcutAction.Reset]==0x52&&Field<Border>(w,"WinF2ValidationWarningBorder").IsEffectivelyVisible,"Duplicate timer key was accepted or warning hidden.");
        Click(w,"NoteCommandsTab");Require(!Field<Border>(w,"WinF2ValidationWarningBorder").IsEffectivelyVisible,"Timer warning leaked into note family.");
        var todo=Note(w,NoteCommandAction.AddTodo);todo.Focus();Pump();Capture(w,"recording");Key(todo,Avalonia.Input.Key.N);Pump();
        Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0x54&&Field<Border>(w,"WinF3ValidationWarningBorder").IsEffectivelyVisible,"Duplicate note key was accepted.");Capture(w,"duplicate");
        Key(todo,Avalonia.Input.Key.F9);Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0x78,"Cross-family key reuse was blocked.");
        Key(todo,Avalonia.Input.Key.NumPad5);var note=Note(w,NoteCommandAction.AddNote);note.Focus();Key(note,Avalonia.Input.Key.D5);
        Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0x35&&w.ResultNoteCommandModeKeys[NoteCommandAction.AddNote]==0x4E,"Numpad normalization or duplicate rejection failed.");
        Key(note,Avalonia.Input.Key.Escape);Key(note,Avalonia.Input.Key.LeftCtrl,KeyModifiers.Control);
        Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddNote]==0x4E,"Reserved/modifier-only capture changed binding.");
        Click(w,"ShortcutsReset");Click(w,"ShortcutsMainTab");
        Field<Dictionary<NoteCommandAction,uint>>(w,"_pendingNoteCommandModeKeys")[NoteCommandAction.AddTodo]=0x4E;
        Click(w,"ShortcutsSave");Require(w.DialogResult!=true&&Find<Control>(w,"NoteCommandPage").IsEffectivelyVisible&&Field<Border>(w,"WinF3ValidationWarningBorder").IsEffectivelyVisible,"Save did not reveal invalid note family.");
        Click(w,"ShortcutsReset");Click(w,"ShortcutsMainTab");
        Field<Dictionary<ShortcutAction,uint>>(w,"_pendingCommandModeKeys")[ShortcutAction.Reset]=0x20;
        Click(w,"ShortcutsSave");Require(w.DialogResult!=true&&Find<Control>(w,"TimerCommandPage").IsEffectivelyVisible&&Field<Border>(w,"WinF2ValidationWarningBorder").IsEffectivelyVisible,"Save did not reveal invalid timer family.");
        Click(w,"ShortcutsReset");Require(!Field<Border>(w,"WinF2ValidationWarningBorder").IsVisible&&!Field<Border>(w,"WinF3ValidationWarningBorder").IsVisible,"Reset kept stale warnings.");
    });
    internal static void DraftsAndOutcomes()
    {
        var input=AppSettings.DefaultCommandModeKeys();
        WithWindow(w=>
        {
            var leader=Field<TextBox>(w,"NoteLeaderShortcutBox");leader.Focus();Key(leader,Avalonia.Input.Key.F10,KeyModifiers.Control);
            Click(w,"ShortcutsMappingsTab");Click(w,"NoteCommandsTab");
            Require(Find<TextBlock>(w,"CommandMappingSequence").Text!.Contains("F10"),"Custom leader missing from guidance.");
            var note=Note(w,NoteCommandAction.AddNote);note.Focus();Key(note,Avalonia.Input.Key.F12);
            Require(Find<TextBlock>(w,"NoteCommandExample").Text!.Contains("F12"),"Quick-note example kept old mapping.");
            Click(w,"ClearNoteCommandAddNote");Require(w.ResultNoteCommandModeKeys[NoteCommandAction.AddNote]==0&&!Find<TextBlock>(w,"NoteCommandExample").Text!.Contains("F12"),"Clear left a false command example.");Capture(w,"unassigned");
            Click(w,"ShortcutsMainTab");Click(w,"ShortcutsClear1");Click(w,"ShortcutsMappingsTab");
            Require(Find<TextBlock>(w,"CommandMappingSequence").Text!.Contains("Assign")&&!Find<TextBlock>(w,"CommandMappingSequence").Text!.Contains("F3"),"Unassigned leader displayed a usable shortcut.");
            Click(w,"ShortcutsReset");Require(w.ResultNoteLeader.Format()=="Win+F3"&&w.ResultNoteCommandModeKeys[NoteCommandAction.AddNote]==0x4E,"Reset did not restore note defaults.");
            Click(w,"TimerCommandsTab");Click(w,"ClearTimerCommandStartStop");Click(w,"ShortcutsCancel");
            Require(w.DialogResult==false&&input[ShortcutAction.StartStop]==0x20,"Cancel changed caller's bindings.");
        },timers:input);
        WithWindow(w=>
        {
            Click(w,"ShortcutsMappingsTab");var start=Timer(w,ShortcutAction.StartStop);start.Focus();Key(start,Avalonia.Input.Key.F9);
            Click(w,"NoteCommandsTab");Click(w,"ClearNoteCommandAddTodo");Click(w,"ShortcutsSave");
            Require(w.DialogResult==true&&w.ResultCommandModeKeys[ShortcutAction.StartStop]==0x78&&w.ResultNoteCommandModeKeys[NoteCommandAction.AddTodo]==0,"Save lost mapping edits.");
            Require(input[ShortcutAction.StartStop]==0x20,"Save mutated caller's input before acceptance.");
        },timers:input);
    }
    internal static void KeyboardAndLegacy()
    {
        foreach(var theme in new[]{AppThemeCatalog.Nika,AppThemeCatalog.Daylight})WithWindow(w=>
        {
            if(theme==AppThemeCatalog.Nika)Click(w,"ShortcutsMappingsTab");
            else Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Non-Nika layout replaced.");
            var field=Timer(w,ShortcutAction.StartStop);Require(field.Focus(),"Mapping editor cannot focus.");
            Key(field,Avalonia.Input.Key.Tab);Pump();
            var next=w.FocusManager?.GetFocusedElement() as Control;
            Require(next!=null&&next!=field,"Tab did not move focus out of the command editor.");
            Key(next!,Avalonia.Input.Key.Tab,KeyModifiers.Shift);Pump();
            Require(field.IsFocused,"Shift+Tab did not return to the command editor.");
            Require(w.ResultCommandModeKeys[ShortcutAction.StartStop]==0x20,"Tab traversal became an accidental command binding.");
            Key(field,Avalonia.Input.Key.F11);
            typeof(ShortcutsWindow).GetMethod("Save_Click",Hidden)!.Invoke(w,[null,new RoutedEventArgs()]);
            Require(w.DialogResult==true&&w.ResultCommandModeKeys[ShortcutAction.StartStop]==0x7A,"Legacy/native Save failed.");
        },theme:theme);
    }
    static void Capture(ShortcutsWindow w,string name)
    {
        const string path="design/one-piece-v2/shortcuts/mappings-implemented";Directory.CreateDirectory(path);
        using var image=new RenderTargetBitmap(new PixelSize((int)w.ClientSize.Width,(int)w.ClientSize.Height),new Vector(96,96));
        var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        if(((LayoutTransformControl)w.Content!).LayoutTransform!.Value.M11>=1)image.Render(w);
        else{using var context=image.CreateDrawingContext();context.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        image.Save(Path.Combine(path,name+".png"));
    }
}
