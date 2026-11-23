using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerTimerListChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Output = "design/one-piece-v2/controller-timer-list/implemented-native";
    const string LongName = "Autumn exhibition catalogue and research notes";

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode=AppThemeCatalog.Nika, UiScalePercent=scale, TimeFormat=1, BlinkColon=false };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance","Initialize",settings);
            var w = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden,null,[typeof(AppSettings)],null)!.Invoke([settings]);
            try
            {
                w.ShowActivated=false; w.ShowInTaskbar=false; w.Show();
                var manager=(TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager",Hidden)!.GetValue(w)!;
                var timers=manager.Sessions;
                timers[0].Mode=1;
                timers[1].IsRunning=true;
                timers[2].RestoreElapsed(TimeSpan.FromSeconds(2678),false);
                timers[3].Name=LongName; timers[3].RestoreElapsed(TimeSpan.FromSeconds(4046),false);
                var list=w.FindControl<ListBox>("TimerRailList")!;
                var localTime=DateTime.Today.AddHours(9).AddMinutes(41).AddSeconds(7).ToUniversalTime();
                foreach(bool pixel in new[]{false,true})
                {
                    settings.NikaControllerPixel=pixel; Call("ApplyActiveTimerStyle");
                    timers[1].RestoreElapsed(TimeSpan.FromSeconds(1092),false);
                    manager.Activate(timers[2]); Call("RefreshTimerRail"); Call("UpdateActiveTimerDisplay");
                    Call("UpdateLiveRail",localTime); ClockOverlayChecks.Pump(90);
                    var rows=list.Items.Cast<TimerRailItemViewModel>().ToArray();
                    Require(rows.Select(r=>r.State).SequenceEqual(["Clock","Running","Paused","Paused","Ready"]),"Unexpected state labels.");
                    Require(rows[0].DisplayTime=="09:41:07" && rows[1].DisplayTime=="00:18:12","Live row time is stale.");
                    for(int i=0;i<rows.Length;i++)
                    {
                        var row=(ListBoxItem)list.ContainerFromItem(rows[i])!;
                        var visual=row.GetVisualDescendants().OfType<Control>().ToArray();
                        var frame=visual.OfType<TodoFrame>().Single(c=>c.Name=="RailSurface");
                        var name=visual.OfType<RailProjectTitle>().Single();
                        var menu=visual.OfType<Button>().Single();
                        var time=visual.Single(c=>(c.Classes.Contains("railTime")||c.Classes.Contains("railPixels")) && c.IsEffectivelyVisible);
                        var state=visual.Single(c=>c.Classes.Contains("railState"));
                        Require(frame.Pixel==pixel && (time is ControllerPixelDigits)==pixel,"Incorrect row style.");
                        Require((frame.BorderBrush as ISolidColorBrush)?.Color==Color.Parse(i==2?"#EDC778":"#3D5864"),"Selection color does not follow the active row.");
                        Require(Bounds(name).Right<=Bounds(menu).Left+.1 && Bounds(time).Right<=Bounds(state).Left+.1 && new Rect(w.ClientSize).Contains(Bounds(menu)), "Row content overlaps or clips.");
                        if(i==3)Require(name.Text==LongName && Equals(ToolTip.GetTip(name),LongName) && Truncated(name),"Long title lacks its second line, ellipsis, or full tooltip.");
                    }
                    ControllerSizingChecks.Capture(w,$"{(pixel?"pixel":"classic")}-controller-{scale}",Output);
                    var runningRow=(ListBoxItem)list.ContainerFromItem(rows[1])!;
                    var button=runningRow.GetVisualDescendants().OfType<Button>().Single();
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ClockOverlayChecks.Pump(40);
                    Require(manager.Active==timers[1] && button.ContextMenu is {IsOpen:true},"Row menu targeted the wrong timer.");
                    int lapCount=timers[1].LapTimes.Count;
                    button.ContextMenu!.Items.OfType<MenuItem>().Single(m=>Equals(m.Header,"Add lap")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    button.ContextMenu?.Close();
                    Require(timers[1].LapTimes.Count==lapCount+1,"Row menu action missed its timer.");
                    list.SelectedIndex=2;
                    timers[1].RestoreElapsed(TimeSpan.FromSeconds(1094),false);
                    Call("UpdateLiveRail",localTime.AddSeconds(2)); ClockOverlayChecks.Pump(35);
                    Require(rows[0].DisplayTime=="09:41:09" && rows[1].DisplayTime=="00:18:14" && rows[1].IsRunning && manager.Active==timers[2],"Selecting a paused timer stopped updates to another row.");
                    Call("RefreshTimerRail");
                    Require(ReferenceEquals(runningRow,list.ContainerFromItem(rows[1])),"Refreshing times recreated existing rows.");
                }
                timers[3].Name=new string('A',95)+" پروژه آزمایشی"; timers[3].Category="Research";
                timers[1].Mode=2; timers[1].CountdownInitialized=true; timers[1].CountdownRemaining=TimeSpan.FromSeconds(1);
                Call("RefreshTimerRail"); timers[1].IsRunning=false; timers[1].CountdownRemaining=TimeSpan.Zero;
                Call("UpdateLiveRail",localTime.AddSeconds(3)); ClockOverlayChecks.Pump(45);
                var countdown=(TimerRailItemViewModel)list.Items[1]!;
                Require(countdown.State=="Finished" && countdown.DisplayTime=="00:00:00","A background countdown failed to show completion.");
                var wrapped=((ListBoxItem)list.ContainerFromIndex(3)!).GetVisualDescendants().OfType<RailProjectTitle>().Single();
                Require(Truncated(wrapped) && Equals(ToolTip.GetTip(wrapped),timers[3].Name+" · [Research]"),"Unbroken title or category tooltip was lost.");
                settings.ThemeMode=AppThemeCatalog.Midnight; Call("ApplyControllerTheme"); ClockOverlayChecks.Pump(55);
                Require(!list.GetVisualDescendants().OfType<RailProjectTitle>().Any() && list.GetVisualDescendants().OfType<TextBlock>().Any(c=>c.Classes.Contains("railSummary")),"Navy rail leaked into another theme.");
                Console.WriteLine($"PASS Classic and Pixel {scale}%: independent selection, time updates, row menu, title truncation, countdown and theme restoration.");
            }
            finally { w.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance","Shutdown"); }
            void Call(string method,params object[] args)=>typeof(ControllerWindow).GetMethod(method,Hidden)!.Invoke(w,args);
            Rect Bounds(Control c)=>new(c.TranslatePoint(default,w)!.Value,c.TranslatePoint(new Point(c.Bounds.Width,c.Bounds.Height),w)!.Value);
        }
    }
    static bool Truncated(RailProjectTitle title) => title.TextLayout.TextLines.Count==2 &&
        (title.TextLayout.TextLines[^1].HasCollapsed || title.TextLayout.TextLines[^1].TextRuns.Any(run=>run.Text.Span.Contains('…')));
    static void Require(bool ok,string error) { if(!ok)throw new InvalidOperationException(error); }
}
