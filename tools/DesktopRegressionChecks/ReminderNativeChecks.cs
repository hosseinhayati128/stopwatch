using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ReminderNativeChecks
{
    const string Output="design/one-piece-v2/notes/reminder-implemented";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly DateTime Due=new(2026,11,19,18,30,0,DateTimeKind.Unspecified);
    static T Named<T>(Window w,string name) where T:Control=>w.FindControl<T>(name)!;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Desktop(string type,string method,params object[] args)=>typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static void Setup(AppSettings settings)
    {
        Desktop("ThemeResources","ApplyApplication",settings.ThemeMode);Desktop("DesktopPageAppearance","Initialize",settings);Desktop("DesktopTypography","Initialize",settings.Typography);
    }
    static void Cleanup(){Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");}
    static AppSettings Settings(string vault,int scale=90)=>new(){ThemeMode=AppThemeCatalog.Nika,UiScalePercent=scale,ObsidianVaultFolder=vault,NotesSubfolder="Notes",HabiticaEnabled=false,TelegramEnabled=false};
    static NoteEntryWindow Open(AppSettings s,NoteEntry? entry=null)
    {
        var w=new NoteEntryWindow(NoteType.Reminder,s,entry){ShowActivated=false,Topmost=false};w.Show();ClockOverlayChecks.Pump(70);return w;
    }
    static void Schedule(Window w,bool on)
    {
        Named<DatePicker>(w,"TodoDueDate").SelectedDate=new DateTimeOffset(Due.Date);
        Named<TimePicker>(w,"TodoDueTime").SelectedTime=Due.TimeOfDay;
        Named<CheckBox>(w,"DueEnabledToggle").IsChecked=on;ClockOverlayChecks.Pump(40);
    }
    static void Click(Window w,string name)=>Named<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Key(Window w,Key key,KeyModifiers modifiers=KeyModifiers.None)=>Named<TextBox>(w,"NoteInputBox").RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key,KeyModifiers=modifiers});
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        string vault=Path.Combine(Path.GetTempPath(),"reminder-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(vault);
        try
        {
            foreach(int scale in new[]{70,90,125})foreach(bool editing in new[]{false,true})foreach(bool scheduled in new[]{false,true})
            {
                var settings=Settings(vault,scale);Setup(settings);
                var entry=editing?new NoteEntry(NoteType.Reminder,new DateTime(2026,11,18,10,0,0),"Review the dashboard sketches before the next session."):null;
                var w=Open(settings,entry);
                try
                {
                    Schedule(w,scheduled);
                    var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().SingleOrDefault();
                    Require(frame!=null,"Reminder is missing the approved shared frame.");
                    Require(Math.Abs(w.ClientSize.Width-620*scale/100d)<1.1&&Math.Abs(w.ClientSize.Height-(scheduled?510:420)*scale/100d)<1.1,"Reminder sizing differs from approved design.");
                    Bounds(w,frame!);
                    Require(Named<Button>(w,"SaveButton").Content?.ToString()==(editing?"Save changes":"Save reminder"),"Wrong primary action.");
                    double editorHeight=Named<TextBox>(w,"NoteInputBox").Bounds.Height;
                    Named<CheckBox>(w,"DueEnabledToggle").IsChecked=!scheduled;ClockOverlayChecks.Pump(50);
                    Require(Math.Abs(Named<TextBox>(w,"NoteInputBox").Bounds.Height-editorHeight)<2,"Scheduling squeezed the writing area.");
                    Named<CheckBox>(w,"DueEnabledToggle").IsChecked=scheduled;ClockOverlayChecks.Pump(50);
                    Require(Named<DatePicker>(w,"TodoDueDate").SelectedDate?.Date==Due.Date&&Named<TimePicker>(w,"TodoDueTime").SelectedTime==Due.TimeOfDay,"Toggle lost draft date/time.");
                    Require(Named<StackPanel>(w,"DueFields").IsVisible==scheduled,"Schedule visibility is wrong.");
                    Capture(w,frame!,scale,$"reminder-{(editing?"edit":"add")}-{(scheduled?"scheduled":"unscheduled")}-{scale}");
                    if(scale==90&&editing&&scheduled)
                    {
                        settings.Typography.Global.SizePercent=125;Desktop("DesktopTypography","Apply",settings.Typography);
                        var input=Named<TextBox>(w,"NoteInputBox");input.Text=string.Join("\n",Enumerable.Repeat("یادآوری برای بررسی طرح‌های جدید",30));
                        typeof(NoteEntryWindow).GetMethod("ShowSaveError",Private)!.Invoke(w,["Could not save your changes. Check the notes folder and try again."]);
                        w.Width=w.MinWidth;w.Height=w.MinHeight;ClockOverlayChecks.Pump(80);Bounds(w,frame!);
                        Require(input.FlowDirection==FlowDirection.RightToLeft,"Persian direction lost.");
                        var scroll=input.GetVisualDescendants().OfType<ScrollViewer>().First();Require(scroll.Extent.Height>scroll.Viewport.Height,"Long reminder cannot scroll.");
                        var error=Named<TextBlock>(w,"SaveError");Require(error.TextLayout.Height<=error.Bounds.Height+1,"Save error clipped.");
                        Capture(w,frame!,scale,"reminder-error-rtl-90");
                    }
                    Console.WriteLine($"PASS Reminder {(editing?"edit":"add")} {(scheduled?"scheduled":"unscheduled")} {scale}%");
                }
                finally{w.Close();Cleanup();}
            }
        }
        finally{Directory.Delete(vault,true);}
    }
    static void Bounds(Window w,Control frame)
    {
        var edge=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),w)!.Value;
        Require(Math.Abs(edge.X-w.ClientSize.Width)<1.5&&Math.Abs(edge.Y-w.ClientSize.Height)<1.5,"Reminder frame exceeds window.");
        var input=Named<TextBox>(w,"NoteInputBox");Require(input.Bounds.Height>=60,"Writing area collapsed.");
        foreach(string name in new[]{"CloseButton","SaveButton","CancelButton","EditorShortcutHint"})
        {
            var c=Named<Control>(w,name);var end=c.TranslatePoint(new Point(c.Bounds.Width,c.Bounds.Height),frame)!.Value;
            Require(end.X<=frame.Bounds.Width-8&&end.Y<=frame.Bounds.Height-8,"Action or hint clipped: "+name);
        }
        if(Named<CheckBox>(w,"DueEnabledToggle").IsChecked==true)
        {
            Require(Named<DatePicker>(w,"TodoDueDate").Bounds.Width>=240&&Named<TimePicker>(w,"TodoDueTime").Bounds.Width>=180,"Date/time fields collapsed.");
            foreach(var picker in new Control[]{Named<DatePicker>(w,"TodoDueDate"),Named<TimePicker>(w,"TodoDueTime")})
                foreach(var text in picker.GetVisualDescendants().OfType<TextBlock>())
                    Require(text.FontFamily.Name==StopwatchTypography.Resolve("modern-deck").NameFont.Name,"Picker text differs from approved body font.");
            var fields=Named<StackPanel>(w,"DueFields");var end=fields.TranslatePoint(new Point(0,fields.Bounds.Height),frame)!.Value;
            Require(end.Y<Named<Button>(w,"SaveButton").TranslatePoint(default,frame)!.Value.Y,"Schedule overlaps footer.");
        }
    }
    static void Capture(Window w,Control frame,int scale,string name)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
        if(scale>=100)bitmap.Render(w);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,name+".png"));
    }
    internal static void Behavior()
    {
        string vault=Path.Combine(Path.GetTempPath(),"reminder-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(vault);
        var settings=Settings(vault);Setup(settings);
        NoteEntry[] Entries()=>ObsidianNotesSync.LoadAllNotes(vault,"Notes").Where(e=>e.Type==NoteType.Reminder).ToArray();
        try
        {
            var w=Open(settings);
            try
            {
                Key(w,Avalonia.Input.Key.Enter,KeyModifiers.Control);Require(w.IsVisible&&Entries().Length==0,"Blank reminder saved.");
                Named<TextBox>(w,"NoteInputBox").Text="Review sketches\nBring notes";
                Key(w,Avalonia.Input.Key.Enter);Require(w.IsVisible,"Plain Enter saved reminder.");Schedule(w,true);
                Named<DatePicker>(w,"TodoDueDate").SelectedDate=null;Click(w,"SaveButton");ClockOverlayChecks.Pump(30);
                Require(w.IsVisible&&Named<TextBlock>(w,"SaveError").IsVisible&&Entries().Length==0,"Missing date was accepted.");
                Schedule(w,true);Key(w,Avalonia.Input.Key.Enter,KeyModifiers.Control);ClockOverlayChecks.Pump(70);
                Require(!w.IsVisible&&Entries().Length==1,"Scheduled reminder not saved exactly once.");
            }
            finally{w.Close();}
            var entry=Entries().Single();Require(HabiticaIntegration.Current.GetDetails(settings,entry).DueAt==Due,"Saved due time differs.");
            w=Open(settings,entry);
            try
            {
                Require(Named<CheckBox>(w,"DueEnabledToggle").IsChecked==true&&Named<TimePicker>(w,"TodoDueTime").SelectedTime==Due.TimeOfDay,"Edit did not restore scheduling.");
                Named<TextBox>(w,"NoteInputBox").Text="Updated reminder";Named<CheckBox>(w,"DueEnabledToggle").IsChecked=false;
                Click(w,"SaveButton");ClockOverlayChecks.Pump(70);Require(!w.IsVisible,"Edit did not save.");
            }
            finally{w.Close();}
            entry=Entries().Single();Require(entry.Text=="Updated reminder"&&HabiticaIntegration.Current.GetDetails(settings,entry).DueAt==null,"Edit duplicated reminder or failed to remove schedule.");
            w=Open(settings,entry);
            try{Schedule(w,true);Click(w,"SaveButton");ClockOverlayChecks.Pump(60);Require(!w.IsVisible&&HabiticaIntegration.Current.GetDetails(settings,Entries().Single()).DueAt==Due,"Edit failed to add schedule.");}finally{w.Close();}
            foreach(string cancel in new[]{"escape","CancelButton","CloseButton"})
            {
                w=Open(settings,Entries().Single());try{Named<TextBox>(w,"NoteInputBox").Text="Do not save";Schedule(w,false);if(cancel=="escape")Key(w,Avalonia.Input.Key.Escape);else Click(w,cancel);Require(!w.IsVisible&&Entries().Single().Text=="Updated reminder","Cancellation saved draft.");}finally{w.Close();}
            }
            settings.NotesSubfolder="blocked";File.WriteAllText(Path.Combine(vault,"blocked"),"fixture");w=Open(settings);
            try
            {
                Named<TextBox>(w,"NoteInputBox").Text="Retry reminder";Click(w,"SaveButton");ClockOverlayChecks.Pump(60);
                Require(w.IsVisible&&Named<TextBlock>(w,"SaveError").IsVisible&&Named<TextBox>(w,"NoteInputBox").Text=="Retry reminder"&&Named<Button>(w,"SaveButton").IsEnabled,"Save failure lost draft or disabled retry.");
                settings.NotesSubfolder="Notes";Click(w,"SaveButton");ClockOverlayChecks.Pump(60);Require(!w.IsVisible&&Entries().Length==2,"Retry did not save exactly once.");
            }
            finally{w.Close();}
            w=Open(settings);
            try
            {
                typeof(NoteEntryWindow).GetMethod("ShowSavedSyncWarning",Private)!.Invoke(w,["Saved locally. Sync needs attention."]);
                Require(!Named<CheckBox>(w,"DueEnabledToggle").IsEffectivelyEnabled&&!Named<TextBox>(w,"NoteInputBox").IsEnabled,"Saved-warning state allows changes.");
                Require(Named<Button>(w,"SaveButton").Content?.ToString()=="Close"&&!Named<Button>(w,"CancelButton").IsVisible,"Saved-warning actions are misleading.");
                Click(w,"SaveButton");Require(!w.IsVisible&&Entries().Length==2,"Warning close saved another copy.");
            }
            finally{w.Close();}
            foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
            {
                settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);w=Open(settings);
                try{Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Reminder styling leaked into other theme.");}finally{w.Close();}
            }
            Console.WriteLine("PASS Reminder create/edit schedules, validation, cancel, retry, warning and theme isolation");
        }
        finally{Cleanup();Directory.Delete(vault,true);}
    }
}
