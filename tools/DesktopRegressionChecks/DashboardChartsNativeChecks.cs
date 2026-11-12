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
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class DashboardChartsNativeChecks
{
    private const string Output="design/one-piece-v2/project-dashboard/charts-native-implementation";
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private sealed class Fixture:IDisposable
    {
        internal readonly ProjectDashboardWindow Window;
        internal DateTime Now=DateTime.SpecifyKind(new DateTime(2026,11,11,12,0,0),DateTimeKind.Local).ToUniversalTime();
        internal bool Empty,LongHistory,Many,Persian,DenseYear;
        internal double Rate=2;
        internal int Writes;
        internal T Named<T>(string name) where T:Control=>Window.FindControl<T>(name)??Window.GetVisualDescendants().OfType<T>().First(c=>c.Name==name);
        internal Fixture(string theme=AppThemeCatalog.Nika)
        {
            Require(Application.Current is not App,"Native fixture loaded live settings.");
            typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!.GetMethod("ApplyApplication",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
            Window=new ProjectDashboardWindow(History,(_,_,_)=>new(ProjectRecordMutationStatus.NotFound),(_,_,_,_)=>new(ProjectRecordMutationStatus.NotFound),_=>new(ProjectRecordMutationStatus.NotFound),()=>true,()=>null,new AppSettings{ThemeMode=theme},setProjectScore:(_,rate)=>{Writes++;Rate=rate;return true;});
            Window.ShowActivated=false;Window.ShowInTaskbar=false;Window.Show();Window.Height=720;ClockOverlayChecks.Pump(35);
        }
        private ProjectHistoryView History()
        {
            var today=Now.ToLocalTime().Date;
            var projects=new List<ProjectInfoView>{new("programming",Persian?"برنامه‌نویسی و پژوهش — یادداشت‌های یک پروژه بلندمدت":"Programming",ScorePerHour:Rate),new("learning","Learning"),new("art","Art",ScorePerHour:1.5),new("apply","Apply")};
            if(Many)for(int n=0;n<8;n++)projects.Add(new("extra"+n,n==0?"Research and long-form reading — a project with a longer name":n==1?"برنامه‌نویسی و پژوهش — پروژه بلندمدت":"Project "+(n+5)));
            var rows=new List<ProjectWorkIntervalView>();
            void Add(int p,DateTime start,int minutes,bool live=false)=>rows.Add(new(Guid.NewGuid(),Guid.NewGuid(),projects[p].Key,projects[p].Name,start.ToUniversalTime(),live?null:start.AddMinutes(minutes).ToUniversalTime()));
            if(!Empty)
            {
                int[][] values=[[60,114,30,144,0,132,84],[60,60,60,66,30,48,54],[36,36,24,36,18,30,36],[24,30,12,24,12,18,24]];
                for(int p=0;p<4;p++)for(int d=0;d<7;d++)if(values[p][d]>0)Add(p,today.AddDays(d-6).AddHours(p==0&&d==6?10.6:9),values[p][d],p==0&&d==6);
                if(LongHistory)Add(0,new DateTime(2016,11,11,9,0,0),60);
                if(DenseYear)for(int d=8;d<365;d+=3)for(int p=0;p<4;p++)Add(p,today.AddDays(-d).AddHours(9),15+(d*7+p*13)%90);
                if(Many)for(int p=4;p<projects.Count;p++)Add(p,today.AddHours(8),15+p);
            }
            return (ProjectHistoryView)Activator.CreateInstance(typeof(ProjectHistoryView),BindingFlags.Instance|BindingFlags.NonPublic,null,[Now,projects,rows,null],null)!;
        }
        internal void Select(string? key){var combo=Named<ComboBox>("ProjectFilterSelector");combo.SelectedItem=combo.Items.Cast<object>().Single(x=>(string?)x.GetType().GetProperty("Key")!.GetValue(x)==key);ClockOverlayChecks.Pump(25);}
        internal void Week(){Click(Named<Button>("SevenDaysButton"));ClockOverlayChecks.Pump(30);}
        internal void Charts(){ClockOverlayChecks.Pump(25);var scroll=Named<ScrollViewer>("DashboardScrollViewer");scroll.Offset=new(0,scroll.Offset.Y+Named<Control>("DashboardChartsLayer").TranslatePoint(default,scroll)!.Value.Y);ClockOverlayChecks.Pump(30);}
        internal Grid Row(string key)=>Named<StackPanel>("ProjectBarsPanel").Children.OfType<Grid>().Single(c=>Equals(c.Tag,key));
        internal TextBlock RowText(string key,string name)=>Row(key).GetVisualDescendants().OfType<TextBlock>().Single(c=>c.Name==name);
        internal TextBlock Day(DateTime date)=>Named<Canvas>("DashboardDailyPlot").Children.OfType<TextBlock>().Single(c=>c.Name=="DashboardDayValue"&&Equals(c.Tag,date));
        internal Button Cell(DateTime date)=>Named<Grid>("HeatmapHost").Children.OfType<Button>().Single(c=>Equals(c.Tag,date));
        public void Dispose()=>Window.Close();
    }
    internal static void DataAndSelection()
    {
        using var f=new Fixture();f.Week();f.Charts();
        Require(f.RowText("programming","DashboardProjectTime").Text=="9h 24m","Project duration changed.");
        Require(f.RowText("programming","DashboardProjectPoints").Text==$"{18.8:0.0} pts","Weighted project value changed.");
        var today=f.Now.ToLocalTime().Date;Require(f.Day(today).Text=="3h 18m","Daily totals do not include overlapping projects.");
        var ink=f.Row("programming").GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="DashboardProjectFill").Background;
        f.Select("programming");Require(f.Day(today.AddDays(-2)).Text=="0m","Missing dates disappear from the selected week.");
        var bar=f.Named<Canvas>("DashboardDailyPlot").Children.OfType<Border>().First(c=>c.Name=="DashboardDailyBar");Require(Equals(ink,bar.Background),"Selected project changes its daily chart color.");
        f.Named<NumericUpDown>("ProjectScoreBox").Value=3;ClockOverlayChecks.Pump(25);Require(f.Writes==1&&f.RowText("programming","DashboardProjectPoints").Text==$"{28.2:0.0} pts","Rate edit does not update points.");
        f.Select(null);Require(Equals(ink,f.Row("programming").GetVisualDescendants().OfType<Border>().Single(c=>c.Name=="DashboardProjectFill").Background),"Filter changes project identity color.");
        var selected=today.AddDays(-3);var cell=f.Cell(selected);Click(cell);ClockOverlayChecks.Pump(25);
        Require(f.Named<TextBlock>("TotalTrackedText").Text=="4h 30m"&&f.Day(selected).Text=="4h 30m","Calendar selection does not update Day charts.");
        Require(cell.BorderThickness.Left==2&&f.Named<TextBlock>("DashboardHeatmapHint").Text!.Contains("4h 30m"),"Selected calendar day lacks its outline/summary.");
        var heading=f.Named<TextBlock>("RangeHeadingText").Text;var future=f.Cell(today.AddDays(1));Require(!future.IsEnabled,"Future calendar day is enabled.");Click(future);Require(f.Named<TextBlock>("RangeHeadingText").Text==heading,"Future date bypasses its guard.");
        var keyboardCell=f.Cell(today.AddDays(-1));keyboardCell.Focus();keyboardCell.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Enter});ClockOverlayChecks.Pump(25);
        Require(f.Named<TextBlock>("TotalTrackedText").Text=="3h 48m","Keyboard calendar activation stopped working.");
    }
    internal static void LongHistoryAndRefresh()
    {
        using var f=new Fixture();f.LongHistory=true;f.Window.RefreshData();Click(f.Named<Button>("AllTimeButton"));f.Charts();
        var plot=f.Named<Canvas>("DashboardDailyPlot");var scroll=f.Named<ScrollViewer>("DashboardDailyScroll");
        Require(plot.Width>100000&&plot.Children.Count<90,"Long history creates controls for every day or loses its time span.");
        Require(scroll.Offset.X>10000,"A new All time range does not show the most recent dates.");
        scroll.Offset=new(46*100,0);ClockOverlayChecks.Pump(25);double x=scroll.Offset.X;
        var cell=f.Cell(f.Now.ToLocalTime().Date);var row=f.Row("programming");f.Now=f.Now.AddSeconds(2);f.Window.RefreshData(true);ClockOverlayChecks.Pump(25);
        Require(Math.Abs(scroll.Offset.X-x)<1&&ReferenceEquals(cell,f.Cell(f.Now.ToLocalTime().Date))&&ReferenceEquals(row,f.Row("programming")),"Live refresh resets reading position or recreates native rows/calendar cells.");
        Require(plot.Children.Count<90,"Scrolling increases the realized chart without a bound.");
        var oldDate=f.Now.ToLocalTime().Date.AddYears(-10);typeof(ProjectDashboardWindow).GetField("_selectedDayLocal",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(f.Window,oldDate);Click(f.Named<Button>("DayButton"));ClockOverlayChecks.Pump(25);
        Require(f.Named<TextBlock>("DashboardHeatmapHint").Text!.Contains("outside"),"A selected day outside the year is misleading.");
        f.Many=true;f.Window.RefreshData();f.Week();var projectScroll=f.Named<ScrollViewer>("DashboardProjectScroll");projectScroll.Offset=new(0,1000);ClockOverlayChecks.Pump(25);double y=projectScroll.Offset.Y;f.Window.RefreshData(true);ClockOverlayChecks.Pump(25);
        Require(y>0&&projectScroll.Offset.Y==y,"Long project list loses its reading position.");
        f.Window.Width=760;f.Now=DateTime.SpecifyKind(new DateTime(2026,11,15,23,59,58),DateTimeKind.Local).ToUniversalTime();f.Window.RefreshData();Click(f.Named<Button>("AllTimeButton"));f.Charts();
        scroll.Offset=new(4600,0);var heat=f.Named<ScrollViewer>("HeatmapScrollViewer");heat.Offset=new(170,0);ClockOverlayChecks.Pump(25);x=scroll.Offset.X;double calendarX=heat.Offset.X;
        f.Now=f.Now.AddSeconds(4);f.Window.RefreshData(true);ClockOverlayChecks.Pump(25);
        Require(Math.Abs(scroll.Offset.X-x)<1,"Midnight live tick jumps the All time chart away from the date being read.");
        Require(Math.Abs(heat.Offset.X-(calendarX-17))<1,"Monday calendar rollover loses the week being read.");
        Click(f.Named<Button>("ThirtyDaysButton"));ClockOverlayChecks.Pump(25);scroll.Offset=new(460,0);ClockOverlayChecks.Pump(25);x=scroll.Offset.X;
        f.Now=f.Now.AddDays(1);f.Window.RefreshData(true);ClockOverlayChecks.Pump(25);
        Require(Math.Abs(scroll.Offset.X-(x-46))<1,"Rolling 30-day window loses the local date being read.");
    }
    internal static void LayoutAndStates()
    {
        using(var plain=new Fixture(AppThemeCatalog.Midnight)){Require(plain.Window.Content is not StopwatchFrameSurface,"Non-Nika dashboard gained Navigator charts.");}
        using var f=new Fixture();f.Week();f.Charts();
        var layer=f.Named<Control>("DashboardChartsLayer");Require(layer.Bounds.Height<=514,"Expanded charts do not fit the approved layer height.");
        var add=f.Named<Button>("AddRecordButton");double footerY=add.TranslatePoint(default,f.Window)!.Value.Y;f.Named<ScrollViewer>("DashboardScrollViewer").ScrollToEnd();ClockOverlayChecks.Pump(25);Require(add.TranslatePoint(default,f.Window)!.Value.Y==footerY,"Chart scrolling moves the footer.");
        f.Window.Width=760;f.Window.Height=560;ClockOverlayChecks.Pump(25);f.Charts();var heatScroll=f.Named<ScrollViewer>("HeatmapScrollViewer");heatScroll.Offset=new(heatScroll.Extent.Width,0);ClockOverlayChecks.Pump(25);
        var weekdays=f.Named<Grid>("DashboardHeatmapWeekdays");var calendar=f.Named<Grid>("DashboardHeatmapCalendar");var label=weekdays.Children[0];var first=f.Named<Grid>("HeatmapHost").Children.OfType<Button>().First();
        Require(weekdays.TranslatePoint(default,calendar)!.Value.X==0&&Math.Abs(label.TranslatePoint(default,calendar)!.Value.Y+label.Bounds.Height/2-first.TranslatePoint(default,calendar)!.Value.Y-first.Bounds.Height/2)<1,"Fixed calendar labels drift from their rows.");
        Require(heatScroll.Extent.Width>heatScroll.Viewport.Width&&heatScroll.Offset.X>0,"Narrow calendar stretches or loses horizontal scrolling.");
        f.Persian=true;f.Window.RefreshData();Require(f.RowText("programming","DashboardProjectName").FlowDirection==FlowDirection.RightToLeft,"Persian project direction is missing.");
        f.Empty=true;f.Window.RefreshData();Require(f.Named<TextBlock>("DashboardDailyEmpty").IsVisible&&f.Named<TextBlock>("DashboardHeatmapHint").Text!.Contains("No tracked time"),"Empty charts have no explanation.");
    }
    internal static void Evidence()
    {
        Directory.CreateDirectory(Output);using var f=new Fixture();f.DenseYear=true;f.Window.RefreshData();f.Week();f.Charts();
        void Capture(string name){ClockOverlayChecks.Pump(30);var root=(Control)f.Window.Content!;using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(root.Bounds.Width),(int)Math.Ceiling(root.Bounds.Height)),new Vector(96,96));bitmap.Render(root);bitmap.Save(Path.Combine(Output,name+".png"));}
        Capture("all-projects");f.Select("programming");f.Charts();Capture("selected-project");f.Select(null);Click(f.Cell(f.Now.ToLocalTime().Date.AddDays(-3)));f.Charts();Capture("selected-day");
        f.Week();f.Persian=true;f.Window.RefreshData();f.Charts();Capture("project-rtl");f.Persian=false;Click(f.Named<Button>("ThirtyDaysButton"));f.Charts();Capture("thirty-days");
        f.Window.Width=760;f.Window.Height=560;f.Week();f.Charts();Capture("compact");f.Named<Control>("DashboardHeatmapCard").BringIntoView();ClockOverlayChecks.Pump(25);f.Named<ScrollViewer>("HeatmapScrollViewer").Offset=new(10000,0);Capture("compact-heatmap");
        f.Window.Width=1080;f.Window.Height=720;f.Empty=true;f.Window.RefreshData();f.Charts();Capture("empty");
    }
}
