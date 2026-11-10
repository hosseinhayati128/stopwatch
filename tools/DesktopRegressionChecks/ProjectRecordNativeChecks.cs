using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ProjectRecordNativeChecks
{
    private const string Output="design/one-piece-v2/time-record/native-implementation";
    private static readonly ProjectInfoView[] Projects=[new("illustration","Illustration"),new("reading","Reading")];
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void KeyDown(Control target,Key key)=>target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=key});
    internal static void Isolation()=>Require(Application.Current is not App,"Record fixtures initialized the production app and its live settings loader.");
    private sealed class Fixture:IDisposable
    {
        internal readonly ProjectRecordEditorWindow Window;
        internal int Writes;
        internal ProjectWorkIntervalView? Written;
        internal readonly ProjectWorkIntervalView? Original;
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)??Window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
        internal Button Save=>Named<Button>("SaveButton");
        internal Fixture(bool editing=false,string theme=AppThemeCatalog.Nika,ProjectRecordMutationStatus status=ProjectRecordMutationStatus.Success,bool empty=false)
        {
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            var start=DateTime.SpecifyKind(DateTime.Today.AddDays(-2).AddHours(23).AddMinutes(15),DateTimeKind.Local).ToUniversalTime().AddTicks(12345);
            Original=editing?new(Guid.NewGuid(),Guid.NewGuid(),"illustration","Illustration",start,start.AddMinutes(135)):null;
            Func<string,DateTime,DateTime,ProjectRecordMutationResult> commit=(name,s,e)=>
            {Writes++;if(status!=ProjectRecordMutationStatus.Success)return new(status);Written=new(Guid.NewGuid(),Guid.Empty,name.ToLowerInvariant(),name,s,e);return new(status,Written);};
            Window=new ProjectRecordEditorWindow(empty?Array.Empty<ProjectInfoView>():Projects,"illustration",Original,commit,theme:theme);
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(35);
            if(!editing)Range("09:00:00","10:30:00");
        }
        internal void Range(string start,string end)
        {
            Named<CalendarDatePicker>("StartDatePicker").SelectedDate=DateTime.Today.AddDays(-1);
            Named<CalendarDatePicker>("EndDatePicker").SelectedDate=DateTime.Today.AddDays(-1);
            Named<TextBox>("StartTimeBox").Text=start;Named<TextBox>("EndTimeBox").Text=end;ClockOverlayChecks.Pump(20);
        }
        public void Dispose()=>Window.Close();
    }
    internal static void Layout()
    {
        using var f=new Fixture();Require(f.Window.Content is StopwatchFrameSurface,"Record editor is missing the shared native frame.");
        Require(f.Window.SystemDecorations==SystemDecorations.None&&f.Window.Width==560&&f.Window.Height==620,"Record editor differs from the approved native dimensions/chrome.");
        var work=f.Window.Screens.ScreenFromWindow(f.Window)!.WorkingArea;
        f.Window.Position=new PixelPoint(work.X+30,work.Bottom-(int)(630*f.Window.RenderScaling));
        Click(f.Named<Button>("RecordNewProjectButton"));ClockOverlayChecks.Pump(25);
        Require(f.Window.Height==Math.Min(732,work.Height/f.Window.RenderScaling-16)&&f.Window.Position.Y+Math.Ceiling(f.Window.Bounds.Height*f.Window.RenderScaling)<=work.Bottom,
            $"Expansion bounds: height={f.Window.Height}, position={f.Window.Position}, bounds={f.Window.Bounds}, scale={f.Window.RenderScaling}, work={work}.");
        f.Window.Height=440;ClockOverlayChecks.Pump(25);var scroll=f.Named<ScrollViewer>("RecordScroll");
        double y=f.Save.TranslatePoint(default,f.Window)!.Value.Y;scroll.Offset=new Vector(0,1000);ClockOverlayChecks.Pump(25);
        Require(scroll.Extent.Height>scroll.Viewport.Height&&f.Save.TranslatePoint(default,f.Window)!.Value.Y==y&&y+f.Save.Bounds.Height<f.Window.Bounds.Height-15,"Short form footer scrolls/clips.");
    }
    internal static void Results()
    {
        using(var f=new Fixture()){Click(f.Save);Require(f.Writes==1&&f.Written?.ProjectName=="Illustration"&&f.Written.EndUtc-f.Written.StartUtc==TimeSpan.FromMinutes(90),"Add record changed the saved range/project.");}
        using(var f=new Fixture(true)){Click(f.Save);Require(f.Writes==1&&f.Written!.StartUtc==f.Original!.StartUtc&&f.Written.EndUtc==f.Original.EndUtc,"Unchanged UTC endpoints lost precision.");}
        using(var f=new Fixture()){Click(f.Named<Button>("RecordNewProjectButton"));f.Named<TextBox>("NewProjectBox").Text="Sketchbook studies";Click(f.Save);Require(f.Written?.ProjectName=="Sketchbook studies","New project draft not saved.");}
        using(var f=new Fixture())
        {
            Click(f.Named<Button>("RecordNewProjectButton"));f.Named<TextBox>("NewProjectBox").Text="Discarded";
            Click(f.Named<Button>("RecordNewProjectButton"));Click(f.Save);Require(f.Written?.ProjectName=="Illustration","Back did not restore previous project.");
        }
    }
    internal static void Validation()
    {
        using var f=new Fixture();
        foreach(var (start,end) in new[]{("09:00:00","08:45:00"),("25:00","10:30"),("09:00","bad"),("09:00","09:00")})
        {f.Range(start,end);Click(f.Save);Require(f.Writes==0&&f.Window.IsVisible&&f.Named<TextBlock>("ValidationText").IsVisible,"Invalid range reached persistence or lacks feedback.");}
        f.Range("09:00","10:30");f.Named<CalendarDatePicker>("EndDatePicker").SelectedDate=DateTime.Today.AddDays(1);Click(f.Save);Require(f.Writes==0,"Future range accepted.");
        f.Range("09:00","10:30");Click(f.Named<Button>("RecordNewProjectButton"));f.Named<TextBox>("NewProjectBox").Text=" ";Click(f.Save);Require(f.Writes==0,"Empty project accepted.");
        f.Named<TextBox>("NewProjectBox").Text="برنامه نویسی";ClockOverlayChecks.Pump(20);Require(f.Named<TextBox>("NewProjectBox").FlowDirection==FlowDirection.RightToLeft,"Persian draft direction missing.");
        KeyDown(f.Window,Avalonia.Input.Key.Escape);Require(f.Writes==0&&!f.Window.IsVisible,"Cancel wrote a record.");
    }
    internal static void FailuresAndCancellation()
    {
        foreach(var status in new[]{ProjectRecordMutationStatus.NotFound,ProjectRecordMutationStatus.OpenInterval,ProjectRecordMutationStatus.Overlap})
        {using var f=new Fixture(status:status);Click(f.Save);Require(f.Window.IsVisible&&f.Writes==1&&f.Written==null&&f.Named<TextBlock>("ValidationText").IsVisible,"Commit failure did not remain open with an error.");}
        ProjectRecordEditorWindow? dialog=null;
        using var opened=Window.WindowOpenedEvent.AddClassHandler<Window>((w,_)=>{if(w is ProjectRecordEditorWindow editor)dialog=editor;});
        var pending=ProjectRecordEditorWindow.ShowAsync(null,Projects,"illustration",commit:(_,_,_)=>new(ProjectRecordMutationStatus.Overlap));
        ClockOverlayChecks.Pump(30);Click(dialog!.FindControl<Button>("SaveButton")!);KeyDown(dialog!,Avalonia.Input.Key.Escape);ClockOverlayChecks.Pump(20);
        Require(pending.IsCompletedSuccessfully&&!pending.Result,"Ownerless cancellation after a failed commit falsely reports acceptance.");
    }
    internal static void InputAndThemes()
    {
        using(var f=new Fixture(theme:AppThemeCatalog.Midnight)){Require(f.Window.Content is not StopwatchFrameSurface,"Plain theme acquired the Nika frame.");}
        using(var f=new Fixture(empty:true)){Require(f.Named<Border>("NewProjectPanel").IsVisible,"Empty list does not offer project entry.");f.Named<TextBox>("NewProjectBox").Text="First project";Click(f.Save);Require(f.Written?.ProjectName=="First project","Empty project list cannot save a draft.");}
        using(var f=new Fixture())
        {
            var project=f.Named<ComboBox>("ProjectSelector");project.Focus();project.IsDropDownOpen=true;ClockOverlayChecks.Pump(20);
            KeyDown(project,Avalonia.Input.Key.Down);KeyDown(project,Avalonia.Input.Key.Enter);Require(f.Window.IsVisible&&f.Writes==0,"Project popup key saved the record.");
            var date=f.Named<CalendarDatePicker>("StartDatePicker");date.Focus();date.IsDropDownOpen=true;ClockOverlayChecks.Pump(25);
            Require(date.IsDropDownOpen,"Calendar does not open.");KeyDown(date,Avalonia.Input.Key.Escape);ClockOverlayChecks.Pump(20);
            Require(f.Window.IsVisible&&!date.IsDropDownOpen&&f.Writes==0,"Calendar Escape closes the whole editor or fails to close the popup.");
        }
    }
    internal static void TypedDatesAndLongRanges()
    {
        using(var f=new Fixture())
        {
            var date=f.Named<CalendarDatePicker>("StartDatePicker");var input=date.GetVisualDescendants().OfType<TextBox>().First();
            input.Focus();input.Text="not a date";f.Save.Focus();ClockOverlayChecks.Pump(20);Click(f.Save);
            Require(f.Writes==0&&f.Window.IsVisible,$"Invalid typed date saved previous date: input={input.Text}, text={date.Text}, selected={date.SelectedDate}.");
        }
        using(var f=new Fixture())
        {
            var date=f.Named<CalendarDatePicker>("StartDatePicker");var input=date.GetVisualDescendants().OfType<TextBox>().First();
            input.Focus();input.Text=DateTime.Today.AddDays(-2).ToString("dd MMM yyyy");f.Save.Focus();ClockOverlayChecks.Pump(20);Click(f.Save);
            Require(f.Written?.StartUtc.ToLocalTime().Date==DateTime.Today.AddDays(-2),"Typed date was not applied before saving.");
        }
        using(var f=new Fixture())
        {
            f.Named<CalendarDatePicker>("StartDatePicker").SelectedDate=DateTime.Today.AddYears(-100);ClockOverlayChecks.Pump(20);
            var duration=f.Named<TextBlock>("DurationPreviewText");var point=duration.TranslatePoint(default,f.Window)!.Value;
            Require(point.X>=0&&point.X+duration.Bounds.Width<f.Window.Bounds.Width-30,"Long duration overlaps or escapes the frame.");
        }
    }
    internal static void ErrorVisibility()
    {
        foreach(bool commitError in new[]{false,true})
        {
            using var f=new Fixture(status:commitError?ProjectRecordMutationStatus.Overlap:ProjectRecordMutationStatus.Success);
            f.Window.Height=440;if(!commitError)f.Range("09:00","08:45");ClockOverlayChecks.Pump(25);
            var scroll=f.Named<ScrollViewer>("RecordScroll");scroll.Offset=default;Click(f.Save);ClockOverlayChecks.Pump(45);
            var error=f.Named<TextBlock>("ValidationText");double y=error.TranslatePoint(default,scroll)!.Value.Y;
            Require(error.IsVisible&&y>=-1&&y+error.Bounds.Height<=scroll.Viewport.Height+1,
                $"Failed save feedback is outside the short form viewport (commit={commitError}, y={y}, height={error.Bounds.Height}, viewport={scroll.Viewport.Height}).");
        }
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        void Capture(Fixture f,string file){Require(f.Window.Content is StopwatchFrameSurface,"Cannot capture unimplemented native form.");ClockOverlayChecks.Pump(25);var root=(Control)f.Window.Content!;using var bmp=new RenderTargetBitmap(new PixelSize((int)root.Bounds.Width,(int)root.Bounds.Height),new Vector(96,96));bmp.Render(root);bmp.Save(Path.Combine(Output,file+".png"));}
        using(var f=new Fixture()){Capture(f,"add-record");Click(f.Named<Button>("RecordNewProjectButton"));f.Named<TextBox>("NewProjectBox").Text="Sketchbook studies";Capture(f,"new-project");f.Range("09:00:00","08:45:00");Click(f.Save);Capture(f,"invalid-range");f.Window.Height=440;ClockOverlayChecks.Pump(25);Click(f.Save);Capture(f,"short-window");}
        using(var f=new Fixture(true)){Capture(f,"edit-record");}
        using(var f=new Fixture())
        {
            var date=f.Named<CalendarDatePicker>("StartDatePicker");date.IsDropDownOpen=true;ClockOverlayChecks.Pump(30);
            var popup=date.GetVisualDescendants().OfType<Popup>().Concat(date.GetLogicalDescendants().OfType<Popup>()).First();
            var child=popup.Child!;Require(child.Bounds.Width>100&&child.Bounds.Height>100,"Calendar popup did not render.");
            using var bmp=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(child.Bounds.Width),(int)Math.Ceiling(child.Bounds.Height)),new Vector(96,96));
            bmp.Render(child);bmp.Save(Path.Combine(Output,"calendar-popup.png"));date.IsDropDownOpen=false;
        }
    }
}
