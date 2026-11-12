using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Views;

internal static class DashboardTimelineNativeChecks
{
    private const string Output="design/one-piece-v2/project-dashboard/timeline-records-native-implementation";
    private const string Persian="برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ClockOverlayChecks.Pump(25);}
    private static void Escape(Window window){window.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Escape});ClockOverlayChecks.Pump(20);}
    private sealed class Fixture:IDisposable
    {
        internal readonly ProjectDashboardWindow Window;
        internal readonly List<ProjectWorkIntervalView> Rows=[];
        internal ProjectInfoView[] Projects=[new("programming","Programming","Development"),new("learning","Learning","Study"),new("art","Art","Creative"),new("apply","Apply")];
        internal DateTime Now=new DateTime(2026,11,12,14,20,0,DateTimeKind.Local).ToUniversalTime();
        internal bool CanEdit=true;
        internal int Writes,Deletes;
        internal Guid? LastWrite,LastDelete;
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)??Window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
        internal Fixture()
        {
            Require(Application.Current is not App,"Fixture loaded live settings.");
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[AppThemeCatalog.Nika]);
            var d=Now.ToLocalTime().Date;
            Add(0,d.AddHours(9),90);Add(1,d.AddHours(9.5),30);Add(2,d.AddHours(11),45);Add(0,d.AddHours(13),null);
            Window=new ProjectDashboardWindow(History,(_,_,_)=>new(ProjectRecordMutationStatus.NotFound),(id,key,start,end)=>
            {Writes++;LastWrite=id;int i=Rows.FindIndex(r=>r.Id==id);var p=Projects.Single(p=>p.Name==key||p.Key==key);Rows[i]=Rows[i] with{ProjectKey=p.Key,ProjectName=p.Name,StartUtc=start,EndUtc=end};return new(ProjectRecordMutationStatus.Success,Rows[i]);},
            id=>{Deletes++;LastDelete=id;Rows.RemoveAll(r=>r.Id==id);return new(ProjectRecordMutationStatus.Success);},()=>CanEdit,()=>CanEdit?null:"Record editing is unavailable while history is read-only.",new AppSettings{ThemeMode=AppThemeCatalog.Nika});
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();Window.Height=720;ClockOverlayChecks.Pump(40);
        }
        internal ProjectWorkIntervalView Add(int p,DateTime start,int? minutes)
        {var row=new ProjectWorkIntervalView(Guid.NewGuid(),Guid.NewGuid(),Projects[p].Key,Projects[p].Name,start.ToUniversalTime(),minutes.HasValue?start.AddMinutes(minutes.Value).ToUniversalTime():null);Rows.Add(row);return row;}
        private ProjectHistoryView History()=>(ProjectHistoryView)Activator.CreateInstance(typeof(ProjectHistoryView),BindingFlags.Instance|BindingFlags.NonPublic,null,[Now,Projects,Rows.ToArray(),null],null)!;
        internal void Refresh(){Window.RefreshData();ClockOverlayChecks.Pump(30);Require(!(bool)typeof(ProjectDashboardWindow).GetField("_refreshFailed",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Window)!,"Dashboard swallowed a rendering exception.");}
        internal void ShowRecords(){Click(Named<Button>("DashboardRecordsButton"));}
        internal void Layer(){ClockOverlayChecks.Pump(25);var s=Named<ScrollViewer>("DashboardScrollViewer");s.Offset=new(0,s.Offset.Y+Named<Control>("DashboardTimelineLayer").TranslatePoint(default,s)!.Value.Y);ClockOverlayChecks.Pump(35);}
        internal Border[] Cards()=>Named<StackPanel>("RecordsPanel").Children.OfType<Border>().ToArray();
        internal Border Card(Guid id)=>Cards().Single(c=>Equals(c.Tag,id));
        internal Border[] Bars()=>Named<Canvas>("DashboardTimelineDays").GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="DashboardTimelineSegment").ToArray();
        internal static TextBlock Text(Control c,string name)=>c.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name==name);
        internal static Button Button(Control c,string content)=>c.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,content));
        internal void Select(string? key){var combo=Named<ComboBox>("ProjectFilterSelector");combo.SelectedItem=combo.Items.Cast<object>().Single(i=>(string?)i.GetType().GetProperty("Key")!.GetValue(i)==key);ClockOverlayChecks.Pump(25);}
        internal void Capture(string name){Directory.CreateDirectory(Output);ClockOverlayChecks.Pump(35);var root=(Control)Window.Content!;using var bmp=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bmp.Render(root);bmp.Save(Path.Combine(Output,name+".png"));}
        public void Dispose()=>Window.Close();
    }

    internal static void DataAndLayout()
    {
        using var f=new Fixture();f.ShowRecords();f.Layer();
        Require(f.Named<TextBlock>("DashboardTimelineTotal").Text=="4h 5m · 4 sessions","Timeline total or session count is incorrect.");
        Require(f.Cards().Length==4&&Equals(f.Cards()[0].Tag,f.Rows[3].Id),"Records are not newest first before pagination.");
        foreach(var bar in f.Bars())
        {
            var card=f.Card((Guid)bar.Tag!);var marker=card.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="DashboardRecordMarker");
            Require(Equals(bar.Background,marker.Background),"Timeline and record project colors differ.");
            Require(((string)ToolTip.GetTip(bar)!).StartsWith(Fixture.Text(card,"DashboardRecordNumber").Text+"."),"Timeline and records have different numbers.");
            Require(Canvas.GetLeft(bar)>=0&&Canvas.GetLeft(bar)+bar.Width<=((Control)bar.Parent!).Bounds.Width+.1,"Segment escapes the local day.");
        }
        var first=f.Bars().Single(b=>Equals(b.Tag,f.Rows[0].Id));var overlap=f.Bars().Single(b=>Equals(b.Tag,f.Rows[1].Id));
        Require(Canvas.GetTop(first)!=Canvas.GetTop(overlap),"Simultaneous sessions share a lane.");
        Require(!f.Card(f.Rows[3].Id).GetVisualDescendants().OfType<Button>().Any(b=>b.IsVisible),"Running record offers edit/delete.");
        f.Capture("day");
        var d=f.Now.ToLocalTime().Date;var overnight=f.Add(3,d.AddMinutes(-30),60);f.Add(1,d.AddHours(12),2);f.Refresh();f.Layer();
        Require(Fixture.Text(f.Card(overnight.Id),"DashboardRecordDuration").Text=="30m"&&Fixture.Text(f.Card(overnight.Id),"DashboardRecordDetail").Text!.Contains("Part of saved record"),"Clipped overnight record loses duration or partial label.");
        Require(((string)ToolTip.GetTip(f.Card(overnight.Id))!).Contains("23:30"),"Partial record tooltip omits full source time.");f.ShowRecords();f.Capture("overnight");
        Click(f.Named<Button>("SevenDaysButton"));f.Layer();Require(f.Bars().Count(b=>Equals(b.Tag,overnight.Id))==2,"Cross-midnight source is not on both day tracks.");f.Capture("week");
        f.Select("programming");f.Layer();Require(f.Cards().Length==2&&f.Bars().All(b=>Equals(b.Background,first.Background)),"Project filtering changes identity color.");f.Capture("project");
        f.Select(null);f.Add(1,d.AddHours(9.5),30);f.Refresh();f.Layer();Require(f.Bars().Count(b=>((string)ToolTip.GetTip(b)!).Contains("Learning"))==3,"Identical intervals lose a distinct source.");
        var pastLive=f.Add(2,d.AddHours(-2),null);f.Refresh();Click(f.Named<Button>("DayButton"));Click(f.Named<Button>("PreviousDayButton"));f.ShowRecords();
        Require(!Fixture.Text(f.Card(pastLive.Id),"DashboardRecordTime").Text!.Contains("Now"),"Past clipped open record falsely ends Now.");
        var status=Fixture.Text(f.Card(pastLive.Id),"DashboardRecordDetail").Text!;
        Require(status.Contains("RUNNING")&&status.Contains("Part of saved record"),"Running status hides the partial-record label.");
    }

    internal static void Actions()
    {
        using var f=new Fixture();var saved=f.Add(3,f.Now.ToLocalTime().Date.AddMinutes(-30),60);f.Refresh();f.ShowRecords();Window? dialog=null;
        using var opened=Window.WindowOpenedEvent.AddClassHandler<Window>((w,_)=>{if(w!=f.Window)dialog=w;});
        Click(Fixture.Button(f.Card(saved.Id),"Edit"));Require(dialog is ProjectRecordEditorWindow,"Edit did not open the existing editor.");
        Require(dialog!.FindControl<CalendarDatePicker>("StartDatePicker")!.SelectedDate?.Date==saved.StartUtc.ToLocalTime().Date&&dialog!.FindControl<TextBox>("StartTimeBox")!.Text!.StartsWith("23:30"),"Edit opens clipped rather than full source record.");
        Escape(dialog!);Require(f.Writes==0,"Cancel wrote the record.");
        Click(Fixture.Button(f.Card(saved.Id),"Edit"));dialog!.FindControl<TextBox>("EndTimeBox")!.Text="00:45:00";Click(dialog!.FindControl<Button>("SaveButton")!);
        Require(f.Writes==1&&f.LastWrite==saved.Id&&!dialog!.IsVisible&&Fixture.Text(f.Card(saved.Id),"DashboardRecordDuration").Text=="45m","Save did not update the intended full source and refresh its row.");
        Click(Fixture.Button(f.Card(saved.Id),"Delete"));Require(dialog is ProjectRecordDeleteWindow&&f.Deletes==0,"Delete bypassed confirmation.");
        Click(dialog!.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Cancel")));Require(f.Deletes==0,"Canceled deletion wrote data.");
        Click(Fixture.Button(f.Card(saved.Id),"Delete"));Click(dialog!.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Delete record")));
        Require(f.Deletes==1&&f.LastDelete==saved.Id&&!f.Cards().Any(c=>Equals(c.Tag,saved.Id)),"Confirmed deletion did not remove the intended source.");
    }

    internal static void RefreshAndPaging()
    {
        using var f=new Fixture();f.ShowRecords();var card=f.Card(f.Rows[0].Id);var edit=Fixture.Button(card,"Edit");edit.Focus();var s=f.Named<ScrollViewer>("DashboardScrollViewer");double y=s.Offset.Y;
        f.Now=f.Now.AddSeconds(2);f.Refresh();Require(ReferenceEquals(card,f.Card(f.Rows[0].Id))&&edit.IsFocused&&Math.Abs(s.Offset.Y-y)<1,"Live refresh replaces record controls, focus or scroll.");
        var today=f.Now.ToLocalTime().Date;for(int i=0;i<43;i++)f.Add(i%4,today.AddDays(-1-i/8).AddMinutes(i*3),20);f.Refresh();Click(f.Named<Button>("SevenDaysButton"));f.ShowRecords();
        Require(f.Cards().Length==20&&f.Named<TextBlock>("RecordsPageStatusText").Text=="1–20 of 47","First page is not bounded to 20 records.");
        var firstIds=f.Cards().Select(c=>c.Tag).ToArray();Click(f.Named<Button>("NextRecordsPageButton"));Require(f.Cards().Length==20&&!f.Cards().Any(c=>firstIds.Contains(c.Tag))&&Fixture.Text(f.Cards()[0],"DashboardRecordNumber").Text=="21","Next page reuses wrong records/numbers.");
        Click(f.Named<Button>("NextRecordsPageButton"));Require(f.Cards().Length==7&&!f.Named<Button>("NextRecordsPageButton").IsEnabled,"Last page count or enabled state is incorrect.");
        Click(f.Named<Button>("PreviousRecordsPageButton"));f.Capture("paged-records");
        f.Rows.Clear();for(int i=0;i<1200;i++)f.Add(0,today.AddDays(-i).AddHours(9),30);f.Refresh();Click(f.Named<Button>("AllTimeButton"));f.Layer();
        var canvas=f.Named<Canvas>("DashboardTimelineDays");Require(canvas.Height>60000&&canvas.Children.Count<20,"Long timeline realizes every historical day.");
        s.Offset=new(0,s.Offset.Y+30000);ClockOverlayChecks.Pump(30);Require(canvas.Children.Count>0&&canvas.Children.Count<20,"Timeline scrolling loses days or grows unbounded.");
    }

    internal static void StatesAndEvidence()
    {
        using var f=new Fixture();f.Projects[0]=f.Projects[0] with{Name=Persian};for(int i=0;i<f.Rows.Count;i++)if(f.Rows[i].ProjectKey=="programming")f.Rows[i]=f.Rows[i] with{ProjectName=Persian};f.Refresh();f.ShowRecords();f.Layer();
        Require(Fixture.Text(f.Card(f.Rows[0].Id),"DashboardRecordName").FlowDirection==FlowDirection.RightToLeft,"Record name is not RTL.");
        f.Window.Width=760;f.Window.Height=560;ClockOverlayChecks.Pump(30);f.ShowRecords();var scroll=f.Named<ScrollViewer>("DashboardScrollViewer");scroll.ScrollToEnd();ClockOverlayChecks.Pump(30);
        Require(scroll.Extent.Width<=scroll.Viewport.Width+1,"Compact records require horizontal scrolling.");
        foreach(var card in f.Cards())Require(Fixture.Text(card,"DashboardRecordName").Bounds.Width>120&&Fixture.Text(card,"DashboardRecordDuration").Bounds.Width>30,"Compact name or duration collapsed.");
        double footer=f.Named<Button>("AddRecordButton").TranslatePoint(default,f.Window)!.Value.Y;f.Capture("compact-records");
        f.Layer();Require(f.Named<Button>("AddRecordButton").TranslatePoint(default,f.Window)!.Value.Y==footer,"Timeline scrolling moves fixed footer.");f.Capture("compact");
        Click(f.Named<Button>("DashboardToggleRecords"));Require(!f.Named<StackPanel>("DashboardRecordsContent").IsVisible,"Hide records failed.");f.ShowRecords();Require(f.Named<StackPanel>("DashboardRecordsContent").IsVisible,"Header Records shortcut failed.");
        f.Window.Width=1080;f.Window.Height=720;f.CanEdit=false;f.Refresh();f.Layer();Require(!f.Named<Button>("AddRecordButton").IsEnabled&&f.Cards().All(c=>!c.GetVisualDescendants().OfType<Button>().Any(b=>b.IsVisible)),"Read-only history offers mutation actions.");Require(f.Named<Border>("RecordsPersistenceWarningPanel").IsVisible,"Read-only warning is missing.");f.Capture("read-only");
        f.CanEdit=true;f.Rows.Clear();f.Refresh();f.Layer();Require(f.Named<StackPanel>("RecordsEmptyState").IsVisible&&f.Named<TextBlock>("DashboardTimelineEmpty").IsVisible,"Empty states are missing.");f.Capture("empty");
    }
}
