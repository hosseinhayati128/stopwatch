using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectDashboardWindow
{
    private StackPanel? _navigatorCharts;
    private Grid _chartCards = null!, _dailyChart = null!;
    private Border _dailyCard = null!, _calendarCard = null!;
    private Canvas _dailyPlot = null!, _dailyAxis = null!;
    private ScrollViewer _dailyScroll = null!, _projectScroll = null!;
    private TextBlock _chartScope = null!, _chartPeriod = null!, _projectCount = null!, _projectScrollHint = null!;
    private TextBlock _dailyTotal = null!, _dailyCaption = null!, _dailyEmpty = null!, _calendarHint = null!;
    private readonly Dictionary<string, SolidColorBrush> _chartProjectBrushes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ChartProjectRow> _chartProjectRows = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<DateTime, TimeSpan> _chartDays = [];
    private DateTime _chartFirstDay;
    private int _chartDayCount;
    private string? _chartDayScope;
    private DashboardRange _chartRange;
    private double? _dailyPendingOffset;
    private double _chartCeiling;
    private bool _drawDays, _dailyShowLatest;
    private static readonly SolidColorBrush[] ChartProjectInks = [..new[]{"#2D7169","#3B6394","#7A527F","#96543E","#596F43","#8A4764","#456E83","#8B5C24","#505989","#596D58","#834C48","#6F6331"}.Select(c=>new SolidColorBrush(Color.Parse(c)))];
    private static readonly IBrush[] ChartHeatInks = [..new[]{"#E1D0AD","#C8D8BF","#8FBAA3","#4E9381","#246758"}.Select(Brush.Parse)];
    private static readonly FuncControlTemplate<Button> ChartCellTemplate = new((button,_)=>
    {
        var tile = new Border { Margin=new(2,1), CornerRadius=new(2) };
        tile.Bind(Border.BackgroundProperty,button.GetObservable(Button.BackgroundProperty));
        tile.Bind(Border.BorderBrushProperty,button.GetObservable(Button.BorderBrushProperty));
        tile.Bind(Border.BorderThicknessProperty,button.GetObservable(Button.BorderThicknessProperty));
        button.GotFocus+=(_,_)=>tile.BoxShadow=BoxShadows.Parse("0 0 0 1 #102B3B");
        button.LostFocus+=(_,_)=>tile.BoxShadow=default;
        return tile;
    });
    private sealed record ChartProjectRow(Grid Root, TextBlock Name, TextBlock Time, TextBlock Points, Grid Track);

    private Control CreateNavigatorCharts()
    {
        _chartScope=DashboardLabel("",13,ClockPickerPresentation.Navy);_chartScope.FontWeight=FontWeight.SemiBold;
        _chartScope.TextWrapping=TextWrapping.NoWrap;_chartScope.TextTrimming=TextTrimming.CharacterEllipsis;
        _chartPeriod=DashboardLabel("",11);
        var context=new Grid{Height=20,ColumnDefinitions=new("*,20,Auto"),Children={_chartScope,_chartPeriod}};Grid.SetColumn(_chartPeriod,2);
        if(ProjectBarsPanel.Parent is Panel oldProjects)oldProjects.Children.Remove(ProjectBarsPanel);
        ProjectBarsPanel.Spacing=7;ProjectBarsPanel.Margin=default;
        _projectScroll=new(){Name="DashboardProjectScroll",Content=ProjectBarsPanel,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        _projectCount=DashboardLabel("",11);_projectScrollHint=DashboardLabel("Bar lengths compare tracked time",10);
        var projectBody=new Grid{RowDefinitions=new("26,20,*,16"),Children={ChartHeading("Time by project",_projectCount),DashboardLabel("Tracked time · points weighted by project rate",10),_projectScroll,_projectScrollHint}};
        for(int i=1;i<projectBody.Children.Count;i++)Grid.SetRow(projectBody.Children[i],i);
        var projectCard=DashboardPane(projectBody,new(14));

        _dailyPlot=new(){Name="DashboardDailyPlot",Height=144,HorizontalAlignment=HorizontalAlignment.Left,ClipToBounds=true};
        _dailyAxis=new(){Width=32,Height=144};
        _dailyScroll=new(){Name="DashboardDailyScroll",Content=_dailyPlot,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};
        _dailyChart=new(){Height=162,ColumnDefinitions=new("32,*"),Children={_dailyAxis,_dailyScroll}};Grid.SetColumn(_dailyScroll,1);
        _dailyTotal=DashboardLabel("",11);_dailyCaption=DashboardLabel("",10);
        _dailyEmpty=DashboardLabel("No daily totals yet. Track time or add a record to see this chart.",13,ClockPickerPresentation.Navy);_dailyEmpty.Name="DashboardDailyEmpty";_dailyEmpty.Margin=new(16,24);
        var dailyBody=new Grid{RowDefinitions=new("26,20,*"),Children={ChartHeading("Daily totals",_dailyTotal),_dailyCaption,_dailyChart,_dailyEmpty}};
        Grid.SetRow(_dailyCaption,1);Grid.SetRow(_dailyChart,2);Grid.SetRow(_dailyEmpty,2);
        _dailyCard=DashboardPane(dailyBody,new(14));
        _chartCards=new(){Height=248,ColumnDefinitions=new("1.15*,14,1*"),Children={projectCard,_dailyCard}};Grid.SetColumn(_dailyCard,2);
        _dailyScroll.ScrollChanged+=(_,_)=>DrawNavigatorDays();
        _dailyScroll.SizeChanged+=(_,_)=>DrawNavigatorDays();

        if(HeatmapScrollViewer.Parent is Panel oldCalendar)oldCalendar.Children.Remove(HeatmapScrollViewer);
        HeatmapScrollViewer.Height=123;HeatmapScrollViewer.Margin=default;
        HeatmapScrollViewer.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto;
        HeatmapScrollViewer.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;
        var weekdays=new Grid{Name="DashboardHeatmapWeekdays",Width=32,Height=123,VerticalAlignment=VerticalAlignment.Top,RowDefinitions=new("18,15,15,15,15,15,15,15")};
        foreach(int d in new[]{0,2,4}){var label=DashboardLabel(new[]{"Mon","Tue","Wed","Thu","Fri"}[d],9);weekdays.Children.Add(label);Grid.SetRow(label,d+1);}
        var calendar=new Grid{Name="DashboardHeatmapCalendar",ColumnDefinitions=new("32,*"),Children={weekdays,HeatmapScrollViewer}};Grid.SetColumn(HeatmapScrollViewer,1);
        var legend=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,VerticalAlignment=VerticalAlignment.Center,Children={DashboardLabel("Less",10)}};
        foreach(var ink in ChartHeatInks)legend.Children.Add(new Border{Width=12,Height=12,CornerRadius=new(2),Background=ink,BorderBrush=DashboardRule,BorderThickness=new(.5)});
        legend.Children.Add(DashboardLabel("More",10));ToolTip.SetTip(legend,"Intensity is relative to the busiest day in the displayed year and project filter.");
        _calendarHint=DashboardLabel("",11);_calendarHint.Name="DashboardHeatmapHint";_calendarHint.TextWrapping=TextWrapping.NoWrap;_calendarHint.TextTrimming=TextTrimming.CharacterEllipsis;
        var heatFooter=new Grid{ColumnDefinitions=new("*,12,Auto"),Children={_calendarHint,legend}};Grid.SetColumn(legend,2);
        _calendarCard=DashboardPane(new StackPanel{Spacing=10,Children={ChartHeading("Activity heatmap",DashboardLabel("Last 12 months",11)),calendar,heatFooter}},new(14));
        _calendarCard.Name="DashboardHeatmapCard";_calendarCard.Height=220;
        _navigatorCharts=new(){Name="DashboardChartsLayer",Spacing=12,Children={context,_chartCards,_calendarCard}};
        return _navigatorCharts;
    }

    private static Grid ChartHeading(string text,TextBlock detail)
    {
        detail.HorizontalAlignment=HorizontalAlignment.Right;
        var grid=new Grid{Height=26,ColumnDefinitions=new("*,Auto"),Children={DashboardLabel(text,20,ClockPickerPresentation.Navy),detail}};
        Grid.SetColumn(detail,1);return grid;
    }

    private void ResizeNavigatorCharts(bool compact)
    {
        _chartCards.ColumnDefinitions=new(compact?"*":"1.15*,14,1*");_chartCards.RowDefinitions=new(compact?"248,12,248":"*");
        _chartCards.Height=compact?508:248;Grid.SetColumn(_dailyCard,compact?0:2);Grid.SetRow(_dailyCard,compact?2:0);
        HeatmapScrollViewer.Height=compact?142:123;_calendarCard.Height=compact?239:220;
    }

    private SolidColorBrush NavigatorProjectBrush(string key)
    {
        if(!_chartProjectBrushes.TryGetValue(key,out var brush))
        {
            // shortcut: colors repeat beyond twelve projects; add distinct patterns if that becomes ambiguous.
            brush=ChartProjectInks[_chartProjectBrushes.Count%ChartProjectInks.Length];_chartProjectBrushes.Add(key,brush);
        }
        return brush;
    }

    private void RenderNavigatorProjects(IReadOnlyList<DisplayInterval> intervals)
    {
        // Seed from the complete project list so filtering and ranking cannot reassign identity colors.
        foreach(var project in _history!.Projects)NavigatorProjectBrush(project.Key);
        var totals=intervals.GroupBy(i=>i.ProjectKey,StringComparer.OrdinalIgnoreCase)
            .Select(g=>new ProjectTotal(g.Key,g.First().ProjectName,TimeSpan.FromTicks(g.Sum(i=>i.Duration.Ticks))))
            .OrderByDescending(p=>p.Duration).ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
        _projectCount.Text=totals.Length==0?"No tracked projects":$"{totals.Length} {(totals.Length==1?"project":"projects")}";
        _projectScrollHint.Text=totals.Length>4?"Scroll for more projects":"Bar lengths compare tracked time";
        if(totals.Length==0)
        {
            if(ProjectBarsPanel.Children.Count!=1||ProjectBarsPanel.Children[0] is not TextBlock)
            {ProjectBarsPanel.Children.Clear();ProjectBarsPanel.Children.Add(DashboardLabel("No project time yet. Track time or add a record to see this comparison.",13,ClockPickerPresentation.Navy));}
            return;
        }
        var rows=new List<Control>();double maximum=Math.Max(1,totals[0].Duration.Ticks);
        foreach(var project in totals)
        {
            var ink=NavigatorProjectBrush(project.Key);
            if(!_chartProjectRows.TryGetValue(project.Key,out var row))
            {
                var name=DashboardLabel("",13,ClockPickerPresentation.Navy);name.Name="DashboardProjectName";name.FontWeight=FontWeight.SemiBold;name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;
                var time=DashboardLabel("",13,ClockPickerPresentation.Navy);time.Name="DashboardProjectTime";time.FontWeight=FontWeight.SemiBold;time.HorizontalAlignment=HorizontalAlignment.Right;time.TextWrapping=TextWrapping.NoWrap;
                var points=DashboardLabel("",10);points.Name="DashboardProjectPoints";points.HorizontalAlignment=HorizontalAlignment.Right;points.TextWrapping=TextWrapping.NoWrap;
                var track=new Grid{Height=6,ColumnDefinitions=new("*,0*"),Background=Brush.Parse("#DDC599"),Margin=new(0,0,12,0),Children={new Border{Name="DashboardProjectFill",Background=ink,CornerRadius=new(3)}}};
                var marker=new Border{Width=8,Height=8,CornerRadius=new(4),Background=ink,VerticalAlignment=VerticalAlignment.Center};
                var root=new Grid{Tag=project.Key,Height=32,ColumnDefinitions=new("8,10,*,90"),RowDefinitions=new("19,13"),Children={marker,name,time,track,points}};
                Grid.SetColumn(name,2);Grid.SetColumn(time,3);Grid.SetColumn(track,2);Grid.SetRow(track,1);Grid.SetColumn(points,3);Grid.SetRow(points,1);
                row=new(root,name,time,points,track);_chartProjectRows.Add(project.Key,row);
            }
            var info=_history.Projects.FirstOrDefault(p=>string.Equals(p.Key,project.Key,StringComparison.OrdinalIgnoreCase));
            row.Name.Text=project.Name;AutoTextDirection.Apply(row.Name,project.Name);
            row.Time.Text=FormatCompactDuration(project.Duration);row.Points.Text=$"{project.Duration.TotalHours*(info?.ScorePerHour??1):0.0} pts";
            ToolTip.SetTip(row.Root,$"{project.Name}{(string.IsNullOrWhiteSpace(info?.Category)?"":$" [{info.Category}]")} · {row.Time.Text} · {row.Points.Text}");
            double fraction=project.Duration.Ticks/maximum;row.Track.ColumnDefinitions[0].Width=new(fraction,GridUnitType.Star);row.Track.ColumnDefinitions[1].Width=new(1-fraction,GridUnitType.Star);
            rows.Add(row.Root);
        }
        if(!ProjectBarsPanel.Children.SequenceEqual(rows))
        {var offset=_projectScroll.Offset;ProjectBarsPanel.Children.Clear();ProjectBarsPanel.Children.AddRange(rows);_projectScroll.Offset=offset;}
    }

    private void RenderNavigatorDays(IReadOnlyList<DayTotal> totals)
    {
        var last=_selectedRange==DashboardRange.Day?(_selectedDayLocal??_latestLocalDate):_latestLocalDate;
        if(last>_latestLocalDate)last=_latestLocalDate;
        var first=_selectedRange switch{DashboardRange.Day=>last,DashboardRange.SevenDays=>last.AddDays(-6),DashboardRange.ThirtyDays=>last.AddDays(-29),_=>totals.Count==0?last:totals.Min(t=>t.Date)};
        int count=(last-first).Days+1;
        if(_chartDayCount==0||_chartRange!=_selectedRange||_chartDayScope!=_selectedProjectKey)
        {_dailyShowLatest=true;_dailyPendingOffset=null;}
        else if(_chartDayCount>7&&(first!=_chartFirstDay||count!=_chartDayCount))
        {
            // A rolling range shifts its origin; keep the same date under the reader's viewport.
            _dailyShowLatest=_dailyScroll.Offset.X>=_dailyScroll.Extent.Width-_dailyScroll.Viewport.Width-1;
            if(!_dailyShowLatest)_dailyPendingOffset=Math.Max(0,_dailyScroll.Offset.X+(_chartFirstDay-first).TotalDays*46);
        }
        _chartFirstDay=first;_chartDayCount=count;_chartDayScope=_selectedProjectKey;_chartRange=_selectedRange;
        _chartDays=totals.ToDictionary(d=>d.Date,d=>d.Duration);
        _chartCeiling=Math.Max(120,Math.Ceiling(totals.Select(d=>d.Duration.TotalMinutes).DefaultIfEmpty(0).Max()/120)*120);
        var total=TimeSpan.FromTicks(totals.Sum(d=>d.Duration.Ticks));
        _dailyTotal.Text=FormatCompactDuration(total);
        _dailyCaption.Text=count==1?"Selected day · local time":count<=7?"Selected week · local dates":$"{count:N0} days · scroll across dates";
        _dailyEmpty.IsVisible=totals.Count==0;_dailyChart.IsVisible=totals.Count>0;
        string scope=(ProjectFilterSelector.SelectedItem as ProjectFilterOption)?.Name??"All projects";
        _chartScope.Text=scope;AutoTextDirection.Apply(_chartScope,scope);ToolTip.SetTip(_chartScope,scope);
        _chartPeriod.Text=$"{(count==1?last.ToString("d MMMM yyyy",CultureInfo.CurrentCulture):$"{first:d MMM}–{last:d MMM yyyy}")}  ·  {_dailyTotal.Text} tracked";
        DrawNavigatorDays();
        Dispatcher.UIThread.Post(DrawNavigatorDays,DispatcherPriority.Loaded);
    }

    private void DrawNavigatorDays()
    {
        if(_drawDays||_chartDayCount==0||_dailyScroll.Viewport.Width<=0)return;
        _drawDays=true;
        try
        {
            double viewport=_dailyScroll.Viewport.Width,step=_chartDayCount<=7?viewport/_chartDayCount:46;
            _dailyPlot.Width=Math.Max(viewport,_chartDayCount*step);
            if(Math.Abs(_dailyScroll.Extent.Width-_dailyPlot.Width)<1)
            {
                if(_dailyShowLatest)_dailyScroll.Offset=new(Math.Max(0,_dailyPlot.Width-viewport),0);
                else if(_dailyPendingOffset is double offset)_dailyScroll.Offset=new(offset,0);
                _dailyShowLatest=false;_dailyPendingOffset=null;
            }
            _dailyPlot.Children.Clear();_dailyAxis.Children.Clear();
            foreach(double fraction in new[]{0d,.5,1})
            {
                var guide=new Border{Width=_dailyPlot.Width,Height=1,Background=DashboardRule};Canvas.SetTop(guide,108*(1-fraction)+16);_dailyPlot.Children.Add(guide);
                var label=DashboardLabel(fraction==0?"0":FormatCompactDuration(TimeSpan.FromMinutes(_chartCeiling*fraction)),10);Canvas.SetTop(label,108*(1-fraction)+10);_dailyAxis.Children.Add(label);
            }
            // Realize only visible dates; even decades of history keep a small native visual tree.
            int from=Math.Max(0,(int)Math.Floor(_dailyScroll.Offset.X/step)-1),to=Math.Min(_chartDayCount,(int)Math.Ceiling((_dailyScroll.Offset.X+viewport)/step)+1);
            for(int i=from;i<to;i++)
            {
                var date=_chartFirstDay.AddDays(i);var duration=_chartDays.GetValueOrDefault(date);double height=duration.TotalMinutes/_chartCeiling*108;
                var bar=new Border{Name="DashboardDailyBar",Tag=date,Width=_chartDayCount==1?64:Math.Min(36,step*.5),Height=height,Background=_selectedProjectKey==null?ClockPickerPresentation.Navy:NavigatorProjectBrush(_selectedProjectKey),CornerRadius=new(3,3,0,0)};
                Canvas.SetLeft(bar,i*step+(step-bar.Width)/2);Canvas.SetTop(bar,124-height);_dailyPlot.Children.Add(bar);
                string detail=$"{date:dddd, d MMMM yyyy} · {FormatCompactDuration(duration)} · {_chartScope.Text}";ToolTip.SetTip(bar,detail);AutomationProperties.SetName(bar,detail);
                var value=DashboardLabel(FormatCompactDuration(duration),10,ClockPickerPresentation.Navy);value.Name="DashboardDayValue";value.Tag=date;value.Width=step;value.TextAlignment=TextAlignment.Center;value.TextWrapping=TextWrapping.NoWrap;
                Canvas.SetLeft(value,i*step);Canvas.SetTop(value,108-height);_dailyPlot.Children.Add(value);ToolTip.SetTip(value,detail);
                var label=DashboardLabel(date.ToString(_chartDayCount==1?"ddd d":_chartDayCount<=7?"ddd":"d MMM",CultureInfo.CurrentCulture),10);label.Width=step;label.TextAlignment=TextAlignment.Center;label.TextWrapping=TextWrapping.NoWrap;
                Canvas.SetLeft(label,i*step);Canvas.SetTop(label,128);_dailyPlot.Children.Add(label);
            }
        }
        finally{_drawDays=false;}
    }

    private void RenderNavigatorHeatmap(IReadOnlyList<HeatmapDayValue> values,DateTime firstDate,DateTime today,string scope,long maximumTicks)
    {
        if(_heatmapFirstDate!=firstDate||_heatmapCells.Count!=HeatmapWeekCount*7)
        {
            bool followLatest=_heatmapFirstDate==null||HeatmapScrollViewer.Offset.X>=HeatmapScrollViewer.Extent.Width-HeatmapScrollViewer.Viewport.Width-1;
            double offset=Math.Max(0,HeatmapScrollViewer.Offset.X-(firstDate-(_heatmapFirstDate??firstDate)).TotalDays/7*17);
            _heatmapFirstDate=firstDate;_heatmapCells.Clear();HeatmapHost.Children.Clear();
            HeatmapHost.Width=901;HeatmapHost.Height=123;HeatmapHost.VerticalAlignment=VerticalAlignment.Top;
            HeatmapHost.ColumnDefinitions=new(string.Join(',',Enumerable.Repeat("17",HeatmapWeekCount)));HeatmapHost.RowDefinitions=new("18,15,15,15,15,15,15,15");
            for(int week=0;week<HeatmapWeekCount;week++)
            {
                var monday=firstDate.AddDays(week*7);
                if(week==0||monday.Day<=7){var label=DashboardLabel(monday.ToString("MMM",CultureInfo.CurrentCulture),9);HeatmapHost.Children.Add(label);Grid.SetColumn(label,week);Grid.SetColumnSpan(label,Math.Min(3,HeatmapWeekCount-week));}
                for(int day=0;day<7;day++)
                {
                    var date=monday.AddDays(day);var cell=new Button{Width=17,Height=15,MinWidth=0,MinHeight=0,Padding=default,Tag=date,Template=ChartCellTemplate,Cursor=new Cursor(StandardCursorType.Hand)};
                    cell.Click+=(_,_)=>{if(date>_latestLocalDate)return;_selectedDayLocal=date;_followToday=date==_latestLocalDate;SelectRange(DashboardRange.Day);};
                    HeatmapHost.Children.Add(cell);_heatmapCells.Add(date,cell);Grid.SetColumn(cell,week);Grid.SetRow(cell,day+1);
                }
            }
            Dispatcher.UIThread.Post(()=>HeatmapScrollViewer.Offset=new(followLatest?HeatmapScrollViewer.Extent.Width:offset,0),DispatcherPriority.Loaded);
        }
        foreach(var value in values)
        {
            var cell=_heatmapCells[value.Date];bool future=value.Date>today,selected=_selectedRange==DashboardRange.Day&&_selectedDayLocal==value.Date;
            cell.Background=ChartHeatInks[future?0:GetHeatmapLevel(value.Duration.Ticks,maximumTicks)];
            cell.BorderBrush=selected?ClockPickerPresentation.Navy:value.Date==today?ClockPickerPresentation.Gold:DashboardRule;
            cell.BorderThickness=new(selected?2:value.Date==today?1.5:.5);cell.Opacity=future?.25:1;cell.IsEnabled=!future;
            string tip=CreateHeatmapToolTip(value,scope,future);ToolTip.SetTip(cell,tip);AutomationProperties.SetName(cell,tip);
        }
        var selectedValue=values.FirstOrDefault(v=>v.Date==_selectedDayLocal);
        _calendarHint.Text=maximumTicks==0?"No tracked time in the last 12 months.":_selectedRange!=DashboardRange.Day?"Select a day to view its totals.":
            selectedValue.Date==default?"Selected day is outside this calendar's last 12 months.":$"Selected: {selectedValue.Date:ddd, d MMM} · {FormatCompactDuration(selectedValue.Duration)} · {selectedValue.RecordCount} {(selectedValue.RecordCount==1?"session":"sessions")}";
        _calendarHint.Foreground=_selectedRange==DashboardRange.Day?ClockPickerPresentation.Navy:ClockPickerPresentation.Muted;
        ToolTip.SetTip(_calendarHint,_calendarHint.Text);_heatmapRenderedToday=today;
    }
}
