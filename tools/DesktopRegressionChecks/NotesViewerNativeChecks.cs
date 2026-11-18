using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class NotesViewerNativeChecks
{
    const string Output="design/one-piece-v2/notes/viewer-implemented";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static T Named<T>(Window w,string name) where T:Control=>w.FindControl<T>(name)!;
    static void Invoke(object target,string name,params object?[] args)=>target.GetType().GetMethod(name,Private)!.Invoke(target,args);
    static void Desktop(string type,string method,params object[] args)=>typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop."+type)!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static DispatcherTimer Timer(NotesViewerWindow w)=>(DispatcherTimer)typeof(NotesViewerWindow).GetField("_autoCloseTimer",Private)!.GetValue(w)!;
    static Border[] Rows(Window w)=>Named<StackPanel>(w,"ItemsContainer").Children.OfType<Border>().Where(c=>c.Child is Grid).ToArray();
    static void Filter(Window w,string name)
    {
        var button=w.GetVisualDescendants().OfType<Button>().SingleOrDefault(b=>b.Name=="ViewerTab"+name);
        if(button!=null)button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else Named<RadioButton>(w,"Filter"+name+"Radio").IsChecked=true;
        ClockOverlayChecks.Pump(30);
    }
    sealed class Fixture:IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"viewer-check-"+Guid.NewGuid().ToString("N"));
        public readonly AppSettings Settings;
        public Fixture(int scale=90)
        {
            string vault=Path.Combine(Root,"Sample vault");Directory.CreateDirectory(vault);
            Settings=new(){ThemeMode=AppThemeCatalog.Nika,UiScalePercent=scale,ObsidianVaultFolder=vault,NotesSubfolder="Capture",HabiticaEnabled=false,TelegramEnabled=false};
            var today=DateTime.Today;
            var entries=new[]{new NoteEntry(NoteType.Todo,today.AddHours(10).AddMinutes(40),"Finish the dashboard sketches"),
                new NoteEntry(NoteType.Note,today.AddHours(10).AddMinutes(10),"Keep the weekly summary focused on finished work."),
                new NoteEntry(NoteType.Reminder,today.AddHours(9).AddMinutes(15),"Review the dashboard sketches"),
                new NoteEntry(NoteType.Todo,today.AddDays(-1).AddHours(16).AddMinutes(20),"Back up the project files"),
                new NoteEntry(NoteType.Note,today.AddDays(-1).AddHours(12).AddMinutes(10),"ایده‌های جلسه بعد را یادداشت کنم."),
                new NoteEntry(NoteType.Todo,today.AddDays(-1).AddHours(9),"Prepare next week’s reading list")};
            foreach(var entry in entries)
            {
                var result=HabiticaIntegration.Current.Apply(Settings,null,entry,entry.Type==NoteType.Reminder?today.AddDays(1).AddHours(18).AddMinutes(30):null,null,false,
                    ()=>ObsidianNotesSync.AppendEntry(vault,entry.Type,entry.Text,Settings.NotesSubfolder,entry.Timestamp));
                Require(result.LocalResult.Success,"Fixture note creation failed.");
            }
            Require(TodoOrganizationStore.SetInProgress(vault,entries[0],true,Settings.NotesSubfolder).Success,"Fixture progress failed.");
            Require(ObsidianNotesSync.SetEntryCompleted(vault,entries[3],true,Settings.NotesSubfolder).Success,"Fixture completion failed.");
            Desktop("ThemeResources","ApplyApplication",Settings.ThemeMode);Desktop("DesktopPageAppearance","Initialize",Settings);Desktop("DesktopTypography","Initialize",Settings.Typography);
        }
        public NotesViewerWindow Open(){var w=new NotesViewerWindow(Settings){ShowActivated=false,Topmost=false};w.Show();ClockOverlayChecks.Pump(90);return w;}
        public NoteEntry[] Entries()=>ObsidianNotesSync.LoadAllNotes(Settings.ObsidianVaultFolder,Settings.NotesSubfolder).ToArray();
        public void Dispose(){Desktop("DesktopTypography","Shutdown");Desktop("DesktopPageAppearance","Shutdown");Directory.Delete(Root,true);}
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach(int scale in new[]{70,90,125})
        {
            using var fixture=new Fixture(scale);var w=fixture.Open();Timer(w).Stop();
            try
            {
                var frame=w.GetVisualDescendants().OfType<StopwatchFrameSurface>().SingleOrDefault();Require(frame!=null,"Viewer is missing approved shared frame.");
                Require(Math.Abs(w.ClientSize.Width-760*scale/100d)<1.1&&Math.Abs(w.ClientSize.Height-580*scale/100d)<1.1,"Viewer initial size differs from proposal.");
                Bounds(w,frame!);Require(Rows(w).Length==6,"All entries missing.");
                Require(Named<TextBlock>(w,"VaultLocationText").Text!.Contains("Capture"),"Footer ignores configured notes subfolder.");
                Capture(w,frame!,scale,"all");
                foreach(var (filter,count) in new[]{("Todos",3),("Notes",2),("Reminders",1)})
                {
                    Filter(w,filter);Require(Rows(w).Length==count,"Incorrect filtered entry count.");
                    Require(w.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==$"{count} of 6 entries · Newest first"),"Filtered count missing.");
                    if(scale==90)Capture(w,frame!,scale,filter.ToLowerInvariant());
                }
                Filter(w,"All");var scroll=Named<ScrollViewer>(w,"NotesScrollViewer");double footerY=Named<TextBlock>(w,"VaultLocationText").TranslatePoint(default,frame!)!.Value.Y;
                Require(scroll.Extent.Height>scroll.Viewport.Height,"Fixture does not exercise scrolling.");scroll.Offset=new Vector(0,1000);ClockOverlayChecks.Pump(30);
                Require(Math.Abs(Named<TextBlock>(w,"VaultLocationText").TranslatePoint(default,frame!)!.Value.Y-footerY)<1,"Scrolling moved footer.");
                if(scale==90)
                {
                    fixture.Settings.Typography.Global.SizePercent=125;Desktop("DesktopTypography","Apply",fixture.Settings.Typography);
                    Invoke(w,"ShowMutationStatus",new HabiticaMutationResult(new(false,"","Could not save this change. Check the notes folder and try again.")));
                    w.Width=w.MinWidth;w.Height=w.MinHeight;ClockOverlayChecks.Pump(70);Bounds(w,frame!);
                    Require(!Named<TextBlock>(w,"CountdownText").Text!.Contains("Could not"),"Error replaced compact countdown.");
                    Capture(w,frame!,scale,"large-text-error");
                }
                Console.WriteLine($"PASS viewer native {scale}%: tabs, counts, frame fit and fixed footer");
            }
            finally{w.Close();}
        }
        using var emptyFixture=new Fixture();
        foreach(var entry in emptyFixture.Entries().Where(e=>e.Type==NoteType.Reminder))Require(ObsidianNotesSync.DeleteEntry(emptyFixture.Settings.ObsidianVaultFolder,entry,emptyFixture.Settings.NotesSubfolder).Success,"Fixture delete failed.");
        var empty=emptyFixture.Open();Timer(empty).Stop();
        try
        {
            Filter(empty,"Reminders");Require(Rows(empty).Length==0&&empty.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="No reminders yet"),"Filtered empty state missing.");
            Capture(empty,empty.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single(),90,"empty");
            emptyFixture.Settings.ObsidianVaultFolder="";Invoke(empty,"LoadNotes");Filter(empty,"All");
            Require(Rows(empty).Length==0&&empty.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Choose a notes folder"),"Missing vault left stale entries.");
            Capture(empty,empty.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single(),90,"unconfigured");
        }
        finally{empty.Close();}
    }
    static void Bounds(Window w,Control frame)
    {
        var edge=frame.TranslatePoint(new Point(frame.Bounds.Width,frame.Bounds.Height),w)!.Value;
        Require(Math.Abs(edge.X-w.ClientSize.Width)<1.5&&Math.Abs(edge.Y-w.ClientSize.Height)<1.5,"Frame exceeds native viewer window.");
        foreach(var name in new[]{"ViewerHeading","CountdownText","VaultLocationText"})
        {
            var text=Named<TextBlock>(w,name);Require(text.TextLayout.Height<=text.Bounds.Height+1,name+" clipped vertically.");
        }
        foreach(var b in w.GetVisualDescendants().OfType<Button>().Where(b=>b.Name?.StartsWith("ViewerTab")==true))
        {
            var end=b.TranslatePoint(new Point(b.Bounds.Width,b.Bounds.Height),frame)!.Value;
            Require(end.X<frame.Bounds.Width-10&&end.Y<Named<ScrollViewer>(w,"NotesScrollViewer").TranslatePoint(default,frame!)!.Value.Y,"Tab clipped or overlaps list.");
        }
        Require(Named<ScrollViewer>(w,"NotesScrollViewer").Bounds.Height>=100,"List viewport collapsed.");
    }
    static void Capture(Window w,Control frame,int scale,string state)
    {
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width),(int)Math.Ceiling(w.ClientSize.Height)),new Vector(96,96));
        if(scale>=100)bitmap.Render(w);else{using var dc=bitmap.CreateDrawingContext();dc.DrawRectangle(new VisualBrush(frame){Stretch=Stretch.Fill,SourceRect=new RelativeRect(frame.Bounds,RelativeUnit.Absolute)},null,new Rect(frame.Bounds.Size));}
        bitmap.Save(Path.Combine(Output,$"notes-viewer-{state}-{scale}.png"));
    }
    internal static void Behavior()
    {
        using var fixture=new Fixture();var w=fixture.Open();
        try
        {
            Require(Timer(w).IsEnabled,"Countdown did not start.");Invoke(w,"AutoCloseTimer_Tick",null,EventArgs.Empty);Require(Named<TextBlock>(w,"CountdownText").Text!.Contains("29s"),"Countdown did not advance.");
            Filter(w,"Todos");var row=Rows(w).First();var check=row.GetVisualDescendants().OfType<CheckBox>().Single();check.IsChecked=true;ClockOverlayChecks.Pump(60);
            Require(fixture.Entries().Single(e=>e.Text=="Finish the dashboard sketches").IsCompleted&&Rows(w).Length==3,"Completion failed or lost filter.");
            Filter(w,"Reminders");row=Rows(w).Single();var before=fixture.Entries().Single(e=>e.Type==NoteType.Reminder);var due=HabiticaIntegration.Current.GetDetails(fixture.Settings,before).DueAt;
            row.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true;ClockOverlayChecks.Pump(40);
            var after=fixture.Entries().Single(e=>e.Type==NoteType.Reminder);Require(after.IsCompleted&&HabiticaIntegration.Current.GetDetails(fixture.Settings,after).DueAt==due,"Completion changed reminder schedule.");
            row=Rows(w).Single();row.ContextMenu!.Open(row);ClockOverlayChecks.Pump(30);Require(!Timer(w).IsEnabled,"Context menu did not pause countdown.");
            Require(Named<TextBlock>(w,"CountdownText").Text=="Auto-close paused","Paused countdown label is stale.");
            var edit=row.ContextMenu.Items.OfType<MenuItem>().First();edit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));ClockOverlayChecks.Pump(60);
            var editor=w.OwnedWindows.OfType<NoteEntryWindow>().Single();Named<Button>(editor,"CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(40);
            Require(Rows(w).Length==1&&!Timer(w).IsEnabled,"Returning from editor lost filter or restarted countdown.");
            row=Rows(w).Single();var delete=row.ContextMenu!.Items.OfType<MenuItem>().Last();delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));ClockOverlayChecks.Pump(40);
            var confirmation=w.OwnedWindows.Single();confirmation.Close(false);ClockOverlayChecks.Pump(40);
            Require(fixture.Entries().Count(e=>e.Type==NoteType.Reminder)==1,"Canceled delete removed reminder.");
        }
        finally{w.Close();Require(!Timer(w).IsEnabled,"Closed viewer left timer running.");}
        foreach(var key in new[]{Avalonia.Input.Key.Enter,Avalonia.Input.Key.Escape}){w=fixture.Open();try{w.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});Require(!w.IsVisible&&!Timer(w).IsEnabled,"Close shortcut failed.");}finally{w.Close();}}
        w=fixture.Open();try{typeof(NotesViewerWindow).GetField("_countdownSeconds",Private)!.SetValue(w,1);Invoke(w,"AutoCloseTimer_Tick",null,EventArgs.Empty);Require(!w.IsVisible&&!Timer(w).IsEnabled,"Auto-close expiry failed.");}finally{w.Close();}
        foreach(string theme in new[]{AppThemeCatalog.Midnight,AppThemeCatalog.Daylight})
        {
            fixture.Settings.ThemeMode=theme;Desktop("ThemeResources","ApplyApplication",theme);w=fixture.Open();
            try{Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(),"Nika viewer leaked into another theme.");Filter(w,"Notes");Require(Rows(w).Length==2,"Classic filter regressed.");}finally{w.Close();}
        }
        Console.WriteLine("PASS viewer completion, reminder due retention, edit/delete cancellation, countdown and classic themes");
    }
}
