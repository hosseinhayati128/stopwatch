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

internal static class QuickNoteNativeChecks
{
    const string Output="design/one-piece-v2/notes/quick-note-implemented";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Named<T>(Window w,string name) where T:Control=>w.FindControl<T>(name)!;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Desktop(string type,string method,params object[] args)=>typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Setup(AppSettings settings)
    {
        Desktop("ThemeResources","ApplyApplication",settings.ThemeMode);
        Desktop("DesktopPageAppearance","Initialize",settings);
        Desktop("DesktopTypography","Initialize",settings.Typography);
    }
    static void Cleanup(){Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");}
    static NoteEntryWindow Open(AppSettings settings,NoteType type=NoteType.Note)
    {
        var w=new NoteEntryWindow(type,settings){ShowActivated=false,Topmost=false};w.Show();ClockOverlayChecks.Pump(100);return w;
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})
        {
            var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=scale};Setup(settings);
            using var done=new CleanupAction(Cleanup);
            var w=Open(settings);
            try
            {
                var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().SingleOrDefault();
                Require(frame!=null,"Quick Note is missing its approved shared panel frame.");
                Require(Math.Abs(w.ClientSize.Width-620*scale/100d)<1.1&&Math.Abs(w.ClientSize.Height-360*scale/100d)<1.1,"Quick Note window sizing differs from approved scale.");
                Bounds(w,frame!);
                Capture(w,frame!,scale,$"quick-note-{scale}");
                Named<TextBox>(w,"NoteInputBox").Text="Remember to sketch the dashboard idea.\nKeep the daily summary simple.";
                ClockOverlayChecks.Pump(30);
                if(scale==90)Capture(w,frame!,scale,"quick-note-filled-90");
                var input=Named<TextBox>(w,"NoteInputBox");
                input.Text=string.Join("\n",Enumerable.Repeat("یادداشت آزمایشی برای بررسی متن طولانی",40));ClockOverlayChecks.Pump(30);
                Require(input.FlowDirection==Avalonia.Media.FlowDirection.RightToLeft,"Persian input direction lost.");
                var scroll=input.GetVisualDescendants().OfType<ScrollViewer>().First();
                Require(scroll.Extent.Height>scroll.Viewport.Height,"Long note cannot scroll.");
                if(scale==90){settings.Typography.Global.SizePercent=125;Desktop("DesktopTypography","Apply",settings.Typography);}
                typeof(NoteEntryWindow).GetMethod("ShowSaveError",Private)!.Invoke(w,["Could not save your changes. Check the notes folder and try again."]);
                w.Width=w.MinWidth;w.Height=w.MinHeight;ClockOverlayChecks.Pump(60);Bounds(w,frame!);
                Capture(w,frame!,scale,$"quick-note-error-{scale}");
                var error=Named<TextBlock>(w,"SaveError");
                Require(error.TextLayout.Height<=error.Bounds.Height+1&&error.TextLayout.TextLines.Sum(line=>line.Length)>=error.Text!.Length,"Error text clipped at minimum size.");
                Console.WriteLine($"PASS Quick Note {scale}%: empty/filled, frame sizing, minimum-size error and fixed actions");
            }
            finally{w.Close();}
        }
    }
    static void Bounds(Window w,Control frame)
    {
        var edgeOfFrame=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),w)!.Value;
        Require(Math.Abs(edgeOfFrame.X-w.ClientSize.Width)<1.5&&Math.Abs(edgeOfFrame.Y-w.ClientSize.Height)<1.5,$"Frame exceeds window: {edgeOfFrame}, client={w.ClientSize}, frame={frame.Bounds}.");
        var input=Named<TextBox>(w,"NoteInputBox");var save=Named<Button>(w,"SaveButton");
        Require(input.Bounds.Height>=60,"Editor collapsed.");
        Require(input.TranslatePoint(new Point(0,input.Bounds.Height),frame)!.Value.Y<save.TranslatePoint(default,frame)!.Value.Y,"Editor overlaps actions.");
        foreach(var name in new[]{"SaveButton","CancelButton","CloseButton","EditorShortcutHint"})
        {
            var control=Named<Control>(w,name);var edge=control.TranslatePoint(new Point(control.Bounds.Width,control.Bounds.Height),frame)!.Value;
            Require(edge.X<=frame.Bounds.Width-8&&edge.Y<=frame.Bounds.Height-8&&control.Bounds.Width>=control.DesiredSize.Width-control.Margin.Left-control.Margin.Right-1,name+" clipped.");
        }
    }
    static void Capture(Window w,Control frame,int scale,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
        if(scale>=100)bitmap.Render(w);
        else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,name+".png"));
    }
    static void Key(Window w,Key key,KeyModifiers modifiers=KeyModifiers.None)=>Named<TextBox>(w,"NoteInputBox").RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=modifiers});
    internal static void Behavior()
    {
        string vault=Path.Combine(Path.GetTempPath(),"quick-note-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(vault);
        var settings=new AppSettings{ThemeMode=AppThemeCatalog.Nika,UiScalePercent=90,ObsidianVaultFolder=vault,NotesSubfolder="Notes",TelegramEnabled=false,HabiticaEnabled=false};
        Setup(settings);
        try
        {
            var w=Open(settings);
            try
            {
                Key(w,Avalonia.Input.Key.Enter,KeyModifiers.Control);ClockOverlayChecks.Pump(25);
                Require(w.IsVisible&&!Directory.EnumerateFiles(vault,"*",SearchOption.AllDirectories).Any(),"Empty note was saved or closed.");
                Named<TextBox>(w,"NoteInputBox").Text="Quick note fixture\nSecond line";
                Key(w,Avalonia.Input.Key.Enter);ClockOverlayChecks.Pump(25);Require(w.IsVisible,"Plain Enter saved the note.");
                Key(w,Avalonia.Input.Key.Enter,KeyModifiers.Control);ClockOverlayChecks.Pump(80);
                Require(!w.IsVisible,"Ctrl+Enter did not close after saving.");
                string saved=string.Join("\n",Directory.EnumerateFiles(vault,"*.md",SearchOption.AllDirectories).Select(File.ReadAllText));
                Require(saved.Contains("Quick note fixture")&&saved.Contains("Second line"),"Multiline note was not persisted.");
            }
            finally{w.Close();}
            foreach(string action in new[]{"escape","CancelButton","CloseButton"})
            {
                w=Open(settings);Named<TextBox>(w,"NoteInputBox").Text="Must not save this draft";
                try{if(action=="escape")Key(w,Avalonia.Input.Key.Escape);else Named<Button>(w,action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(!w.IsVisible,"Cancel/close failed.");}
                finally{w.Close();}
            }
            Require(!Directory.EnumerateFiles(vault,"*.md",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("Must not save")),"Canceled draft was saved.");
            File.WriteAllText(Path.Combine(vault,"blocked"),"fixture");settings.NotesSubfolder="blocked";
            w=Open(settings);
            try
            {
                Named<TextBox>(w,"NoteInputBox").Text="Retry this draft";Named<Button>(w,"SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(80);
                Require(w.IsVisible&&Named<TextBlock>(w,"SaveError").IsVisible&&Named<Button>(w,"SaveButton").IsEnabled&&Named<TextBox>(w,"NoteInputBox").Text=="Retry this draft","Failed save lost draft or prevented retry.");
                settings.NotesSubfolder="Notes";Named<Button>(w,"SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(80);Require(!w.IsVisible,"Save retry did not succeed.");
            }
            finally{w.Close();}
            foreach(var type in new[]{NoteType.Todo,NoteType.Reminder})
            {
                w=Open(settings,type);try{Require(!w.Classes.Contains("nikaQuickNote"),"Quick Note shell leaked into "+type);}finally{w.Close();}
            }
            w=NoteEntryWindow.ForTodoDetail(settings,"Edit detail","Existing detail",_=>new(true,""));
            try{Require(w.Classes.Contains("nikaTodoDialog"),"Todo detail editor lost its appearance.");}finally{w.Close();}
            foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
            {
                settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);w=Open(settings);
                try{Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika shell leaked into another theme.");}finally{w.Close();}
            }
            Console.WriteLine("PASS Quick Note keyboard, local save, retry, cancellation, shared editors and classic themes");
        }
        finally{Cleanup();Directory.Delete(vault,true);}
    }
    sealed class CleanupAction(Action action):IDisposable{public void Dispose()=>action();}
}
