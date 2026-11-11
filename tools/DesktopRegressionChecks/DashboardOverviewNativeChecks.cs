using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class DashboardOverviewNativeChecks
{
    private const string Output="design/one-piece-v2/project-dashboard/overview-native-implementation";
    private const string Persian="برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Escape(Control target)=>target.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Escape});

    private sealed class Fixture:IDisposable
    {
        internal readonly ProjectDashboardWindow Window;
        internal DateTime Now=DateTime.Today.AddHours(12).ToUniversalTime();
        internal double Rate=2;
        internal int Reads,Writes,Deletes,Adds;
        internal bool CanEdit=true,Empty;
        internal ProjectInfoView[] Projects=>[new("programming","Programming",ScorePerHour:Rate),new("art",Persian)];
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)??Window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
        internal Fixture(string theme=AppThemeCatalog.Nika,bool readOnly=false)
        {
            Require(Application.Current is not App,"Fixture initialized live application state.");
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            CanEdit=!readOnly;
            Window=new ProjectDashboardWindow(History,(_,_,_)=>{Adds++;return new(ProjectRecordMutationStatus.NotFound);},(_,_,_,_)=>new(ProjectRecordMutationStatus.NotFound),_=>new(ProjectRecordMutationStatus.NotFound),()=>CanEdit,()=>CanEdit?null:"Read-only fixture",
                new AppSettings{ThemeMode=theme},name=>{Deletes++;return new(ProjectDeletionStatus.NotFound,name,0);},(_,rate)=>{Writes++;Rate=rate;return true;});
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();ClockOverlayChecks.Pump(45);
        }
        private ProjectHistoryView History()
        {
            Reads++;
            var day=Now.ToLocalTime().Date;
            ProjectWorkIntervalView Interval(string key,double start,double? end,int days=0)=>new(Guid.NewGuid(),Guid.NewGuid(),key,key=="art"?Persian:"Programming",day.AddDays(days).AddHours(start).ToUniversalTime(),end is{} h?day.AddDays(days).AddHours(h).ToUniversalTime():null);
            var rows=Empty?Array.Empty<ProjectWorkIntervalView>():new[]{Interval("programming",9,10),Interval("art",9,10),Interval("programming",11,null),Interval("art",11.5,null),Interval("programming",9,11,-1)};
            return (ProjectHistoryView)Activator.CreateInstance(typeof(ProjectHistoryView),BindingFlags.Instance|BindingFlags.NonPublic,null,[Now,Projects,rows,null],null)!;
        }
        internal void Select(string key)
        {
            var combo=Named<ComboBox>("ProjectFilterSelector");combo.SelectedItem=combo.Items.Cast<object>().Single(item=>(string?)item.GetType().GetProperty("Key")!.GetValue(item)==key);ClockOverlayChecks.Pump(20);
        }
        public void Dispose()=>Window.Close();
    }

    internal static void Layout()
    {
        using var f=new Fixture();
        Require(f.Window.Content is StopwatchFrameSurface&&f.Window.SystemDecorations==SystemDecorations.None,"Dashboard is missing the approved standalone shared frame.");
        f.Window.Width=1080;f.Window.Height=620;ClockOverlayChecks.Pump(35);
        var trackedInk=f.Named<TextBlock>("TotalTrackedText").Foreground as Avalonia.Media.ISolidColorBrush;
        Require(trackedInk?.Color==Avalonia.Media.Color.Parse("#F6E8C9"),$"Tracked time lost its cream contrast: {trackedInk?.Color}.");
        var add=f.Named<Button>("AddRecordButton");var scroll=f.Named<ScrollViewer>("DashboardScrollViewer");
        Require(f.Named<TextBlock>("UpdatedText").Bounds.Width>300,"Footer update label is squeezed into a spacer column.");
        double y=add.TranslatePoint(default,f.Window)!.Value.Y;
        Require(y>0&&y+add.Bounds.Height<f.Window.Bounds.Height-15,"Add record is outside the initial viewport.");
        Click(f.Named<Button>("DashboardRecordsButton"));ClockOverlayChecks.Pump(30);
        Require(f.Named<Expander>("ProjectRecordsExpander").IsExpanded&&scroll.Offset.Y>0,"Records action cannot reach existing records.");
        Require(add.TranslatePoint(default,f.Window)!.Value.Y==y,"Scrolling moves the footer action.");
        f.Window.Width=760;f.Window.Height=560;scroll.Offset=default;f.Select("programming");ClockOverlayChecks.Pump(35);
        Require(f.Named<ComboBox>("ProjectFilterSelector").Bounds.Width>=250,"Compact project selector collapsed.");
        var score=f.Named<NumericUpDown>("ProjectScoreBox").GetVisualDescendants().OfType<TextBox>().Single();
        Require(score.Text==2m.ToString("0.0")&&score.Bounds.Width>=48,"Score field hides its editable value.");
        Require(f.Named<Control>("DashboardStats").TranslatePoint(default,f.Named<Control>("DashboardSummary"))!.Value.Y>0,"Compact metrics do not stack.");
        scroll.Offset=new(0,10000);ClockOverlayChecks.Pump(20);
        Require(add.TranslatePoint(default,f.Window)!.Value.Y+add.Bounds.Height<f.Window.Bounds.Height-15,"Compact fixed footer clips.");
    }

    internal static void CalculationsAndFilters()
    {
        using var f=new Fixture();
        Require(f.Named<TextBlock>("TotalTrackedText").Text=="3h 30m"&&f.Named<TextBlock>("SessionCountText").Text=="4"&&f.Named<TextBlock>("ProjectCountText").Text=="2","Overlapping intervals or counts changed.");
        Require(f.Named<TextBlock>("TotalScoreText").Text==$"{5.5:0.0} pts","Weighted score changed.");
        f.Select("programming");Require(f.Named<TextBlock>("TotalTrackedText").Text=="2h"&&f.Named<TextBlock>("ActiveCountText").Text=="2","Project filter changed global active scope.");
        Click(f.Named<Button>("SevenDaysButton"));Require(f.Named<TextBlock>("TotalTrackedText").Text=="4h"&&f.Named<TextBlock>("SessionCountText").Text=="3","Seven-day range lost history.");
        Require(!f.Named<Button>("PreviousDayButton").IsEnabled&&!f.Named<Button>("NextDayButton").IsEnabled,"Day controls enabled outside Day mode.");
        Click(f.Named<Button>("DayButton"));Click(f.Named<Button>("PreviousDayButton"));Require(f.Named<TextBlock>("TotalTrackedText").Text=="2h"&&f.Named<Button>("NextDayButton").IsEnabled,"Historical day navigation failed.");
        Click(f.Named<Button>("NextDayButton"));Require(!f.Named<Button>("NextDayButton").IsEnabled,"Future day navigation is enabled.");
        f.Named<NumericUpDown>("ProjectScoreBox").Value=3;Require(f.Writes==1&&f.Named<TextBlock>("TotalScoreText").Text==$"{6.0:0.0} pts","Score edit lost its original callback/recalculation.");
        int reads=f.Reads;Click(f.Named<Button>("DashboardRefreshButton"));Require(f.Reads>reads,"Refresh is disconnected.");
    }

    internal static void LiveEditingAndLifecycle()
    {
        using var f=new Fixture();f.Select("programming");
        var selector=f.Named<ComboBox>("ProjectFilterSelector");var items=selector.ItemsSource;selector.IsDropDownOpen=true;ClockOverlayChecks.Pump(20);f.Window.RefreshData(true);
        Require(ReferenceEquals(items,selector.ItemsSource)&&selector.IsDropDownOpen,"Live refresh replaces an open project selector.");
        Escape(selector);ClockOverlayChecks.Pump(20);Require(f.Window.IsVisible,"Popup Escape closes the dashboard.");selector.IsDropDownOpen=false;
        var score=f.Named<NumericUpDown>("ProjectScoreBox");var editor=score.GetVisualDescendants().OfType<TextBox>().Single();editor.Focus();editor.Text="2.";f.Window.RefreshData(true);
        Require(editor.Text=="2.","Live refresh overwrites a rate being typed.");
        f.Named<Button>("DashboardRefreshButton").Focus();
        f.Now=f.Now.AddDays(1);f.Window.RefreshData(true);Require(!f.Named<Button>("NextDayButton").IsEnabled,"Following today retained a stale navigation state.");
        Click(f.Named<Button>("PreviousDayButton"));var heading=f.Named<TextBlock>("RangeHeadingText").Text;f.Now=f.Now.AddDays(1);f.Window.RefreshData(true);
        Require(f.Named<TextBlock>("RangeHeadingText").Text==heading,"Live refresh moved a selected historical date.");
        Click(f.Named<Button>("DashboardCloseButton"));
        var timer=(DispatcherTimer)typeof(ProjectDashboardWindow).GetField("_liveRefreshTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Window)!;
        Require(!f.Window.IsVisible&&!timer.IsEnabled,"Closing dashboard leaves the refresh timer running.");
    }

    internal static void DialogsAndThemes()
    {
        using(var f=new Fixture(theme:AppThemeCatalog.Midnight)){Require(f.Window.Content is not StopwatchFrameSurface,"Non-Nika dashboard acquired the engraved frame.");}
        using(var f=new Fixture(readOnly:true)){Require(!f.Named<Button>("AddRecordButton").IsEnabled,"Read-only dashboard offers an enabled write action.");}
        using var writable=new Fixture();writable.Select("programming");
        Window? dialog=null;using var opened=Window.WindowOpenedEvent.AddClassHandler<Window>((w,_)=>{if(w!=writable.Window)dialog=w;});
        Click(writable.Named<Button>("AddRecordButton"));ClockOverlayChecks.Pump(30);Require(dialog is ProjectRecordEditorWindow,"Footer Add record failed to open the existing editor.");
        Escape(dialog!);ClockOverlayChecks.Pump(20);Require(writable.Adds==0&&writable.Window.IsVisible,"Canceled editor wrote data or closed its owner.");
        dialog=null;Click(writable.Named<Button>("DeleteProjectButton"));ClockOverlayChecks.Pump(30);
        Require(dialog is ConfirmationDialogWindow&&writable.Deletes==0,"Project deletion bypassed confirmation.");Escape(dialog!);ClockOverlayChecks.Pump(20);
        Require(writable.Deletes==0&&writable.Window.IsVisible,"Canceled deletion mutated a project.");
    }

    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);
        using var f=new Fixture();
        void Capture(string name){ClockOverlayChecks.Pump(30);var root=(Control)f.Window.Content!;using var bmp=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bmp.Render(root);bmp.Save(Path.Combine(Output,name+".png"));}
        Capture("all-projects-today");f.Select("programming");Click(f.Named<Button>("SevenDaysButton"));Capture("project-seven-days");
        f.Select("art");Capture("project-rtl");Require(f.Named<ComboBox>("ProjectFilterSelector").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==Persian&&t.FlowDirection==Avalonia.Media.FlowDirection.RightToLeft),"Persian project name lacks RTL layout.");
        f.Window.Width=760;f.Window.Height=560;Capture("compact");f.Named<ScrollViewer>("DashboardScrollViewer").Offset=new(0,320);Capture("compact-summary");
        f.Window.Width=1080;f.Window.Height=620;f.Named<ScrollViewer>("DashboardScrollViewer").Offset=default;f.Empty=true;f.Window.RefreshData();Capture("empty-day");
    }
}
