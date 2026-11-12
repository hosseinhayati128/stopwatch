using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectDashboardWindow
{
    private StackPanel? _navigatorTimeline;
    private Canvas _timelineDays = null!;
    private TextBlock _timelineScope = null!, _timelinePeriod = null!, _timelineTotal = null!, _timelineEmpty = null!, _recordsCount = null!;
    private Border _navigatorRecordsCard = null!;
    private StackPanel _navigatorRecordsContent = null!;
    private Grid _recordColumns = null!;
    private Button _toggleRecords = null!;
    private bool _compactRecords, _timelineDrawPending;
    private List<NavigatorTimelineDay> _timelineDayModels = [];
    private readonly Dictionary<DateTime, (Canvas View, NavigatorTimelineDay Model, double Width)> _timelineDayViews = [];
    private readonly Dictionary<Guid, NavigatorRecordRow> _navigatorRecordRows = [];
    private static readonly IBrush RecordPaper = Brush.Parse("#66FFF6E4"), TimelineTrack = Brush.Parse("#1A9A7B48");

    private sealed record NavigatorSegment(TimelineItem Item, int Number, Guid Id, bool EndsNow);
    private sealed record NavigatorTimelineDay(DateTime Date, List<NavigatorSegment> Segments, double Top, double Height, bool ShowDate);
    private sealed class NavigatorRecordRow
    {
        internal ProjectWorkIntervalView Source = null!;
        internal Border Card = null!, Marker = null!;
        internal Grid Layout = null!, Project = null!;
        internal StackPanel Names = null!, Clock = null!, Actions = null!;
        internal TextBlock Number = null!, Name = null!, Detail = null!, Time = null!, Date = null!, Duration = null!, Live = null!;
        internal Button Edit = null!, Delete = null!;
    }

    private Control CreateNavigatorTimelineRecords()
    {
        _timelineScope=DashboardLabel("",13,ClockPickerPresentation.Navy);_timelineScope.FontWeight=FontWeight.SemiBold;
        _timelineScope.TextWrapping=TextWrapping.NoWrap;_timelineScope.TextTrimming=TextTrimming.CharacterEllipsis;
        _timelinePeriod=DashboardLabel("",11);
        var context=new Grid{Height=20,ColumnDefinitions=new("*,20,Auto"),Children={_timelineScope,_timelinePeriod}};Grid.SetColumn(_timelinePeriod,2);
        _timelineTotal=DashboardLabel("",11);_timelineTotal.Name="DashboardTimelineTotal";
        var timelineHead=new Grid{Height=26,ColumnDefinitions=new("*,Auto"),Children={DashboardLabel("Daily timeline",20,ClockPickerPresentation.Navy),_timelineTotal}};Grid.SetColumn(_timelineTotal,1);
        _timelineDays=new(){Name="DashboardTimelineDays",ClipToBounds=true};
        _timelineEmpty=DashboardLabel("No sessions in this period. Track time or add a record to build your timeline.",12);_timelineEmpty.Name="DashboardTimelineEmpty";_timelineEmpty.Margin=new(0,22);
        var timeline=DashboardPane(new StackPanel{Spacing=6,Children={timelineHead,DashboardLabel("Full local day · overlapping sessions use separate lanes",10),_timelineDays,_timelineEmpty}},new(12));

        foreach(var control in new Control[]{ProjectRecordsExpander,RecordsPanel,RecordsEmptyState,RecordsPersistenceWarningPanel,PreviousRecordsPageButton,RecordsPageStatusText,NextRecordsPageButton})
            if(control.Parent is Panel parent)parent.Children.Remove(control);
        // Keep the existing expander state and named controls for the Records shortcut and legacy callers.
        ProjectRecordsExpander.Content=null;ProjectRecordsExpander.Header=null;ProjectRecordsExpander.IsVisible=false;
        _recordsCount=DashboardLabel("",11);
        _toggleRecords=new(){Name="DashboardToggleRecords",Content="Show records"};Action(_toggleRecords,108);_toggleRecords.Height=30;
        _toggleRecords.Click+=(_,_)=>{ProjectRecordsExpander.IsExpanded=!ProjectRecordsExpander.IsExpanded;RenderNavigatorRecords();};
        var recordsHead=new Grid{Height=30,ColumnDefinitions=new("*,Auto,14,108"),Children={DashboardLabel("Project records",20,ClockPickerPresentation.Navy),_recordsCount,_toggleRecords}};
        Grid.SetColumn(_recordsCount,1);Grid.SetColumn(_toggleRecords,3);
        _recordColumns=new(){Height=18,Margin=new(11,0),ColumnDefinitions=new("*,210,92,138")};
        foreach(string title in new[]{"PROJECT","TIME IN PERIOD","DURATION","ACTIONS"}){var label=DashboardLabel(title,9);Grid.SetColumn(label,_recordColumns.Children.Count);_recordColumns.Children.Add(label);}
        RecordsPanel.Spacing=6;RecordsPanel.Margin=default;
        RecordsPersistenceWarningPanel.Margin=default;RecordsPersistenceWarningPanel.Padding=new(10,8);
        RecordsEmptyHeadingText.FontSize=14;RecordsEmptyHeadingText.TextWrapping=TextWrapping.Wrap;RecordsEmptyHeadingText.TextAlignment=TextAlignment.Center;
        RecordsPageStatusText.FontSize=11;RecordsPageStatusText.MinWidth=0;
        Action(PreviousRecordsPageButton,80);PreviousRecordsPageButton.Content="Previous";PreviousRecordsPageButton.Height=30;
        Action(NextRecordsPageButton,64);NextRecordsPageButton.Content="Next";NextRecordsPageButton.Height=30;
        var pages=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,Children={PreviousRecordsPageButton,RecordsPageStatusText,NextRecordsPageButton}};
        var paging=new Grid{Height=30,ColumnDefinitions=new("*,Auto"),Children={DashboardLabel("Edit opens the full saved record",10),pages}};Grid.SetColumn(pages,1);
        _navigatorRecordsContent=new(){Name="DashboardRecordsContent",Spacing=8,Children={RecordsPersistenceWarningPanel,_recordColumns,RecordsPanel,RecordsEmptyState,paging}};
        _navigatorRecordsCard=DashboardPane(new StackPanel{Spacing=8,Children={recordsHead,_navigatorRecordsContent}},new(12));
        _navigatorRecordsCard.Name="DashboardRecordsCard";
        _navigatorRecordsCard.Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;
        _navigatorRecordsCard.Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        _navigatorRecordsCard.Resources["WarningBrush"]=Brush.Parse("#8B5C24");
        _navigatorRecordsCard.Resources["SurfaceRaisedBrush"]=RecordPaper;
        _navigatorTimeline=new(){Name="DashboardTimelineLayer",Spacing=10,Children={context,timeline,_navigatorRecordsCard,ProjectRecordsExpander}};
        _timelineDays.SizeChanged+=(_,_)=>QueueNavigatorTimelineDraw();
        _timelineDays.Loaded+=(_,_)=>QueueNavigatorTimelineDraw();
        DashboardScrollViewer.ScrollChanged+=(_,_)=>QueueNavigatorTimelineDraw();
        return _navigatorTimeline;
    }

    private void RenderNavigatorTimeline()
    {
        _timelineScope.Text=(ProjectFilterSelector.SelectedItem as ProjectFilterOption)?.Name??"All projects";
        AutoTextDirection.Apply(_timelineScope,_timelineScope.Text);ToolTip.SetTip(_timelineScope,_timelineScope.Text);
        _timelinePeriod.Text=RangeHeadingText.Text;
        _timelineTotal.Text=$"{FormatCompactDuration(TimeSpan.FromTicks(_records.Sum(r=>r.Duration.Ticks)))} · {_records.Count} {(_records.Count==1?"session":"sessions")}";
        _timelineEmpty.IsVisible=_records.Count==0;
        var numbered=new Dictionary<DayFragment,(int Number,Guid Id,bool EndsNow)>(ReferenceEqualityComparer.Instance);
        for(int i=0;i<_records.Count;i++)
            foreach(var fragment in SplitByLocalDay(_records[i]))numbered.Add(fragment,(i+1,_records[i].Source.Id,fragment.IsLive&&fragment.EndsAtIntervalEnd&&fragment.EndUtc==EnsureUtc(_history!.AsOfUtc)));
        var models=new List<NavigatorTimelineDay>();double top=0;bool showDate=_selectedRange!=DashboardRange.Day;
        foreach(var group in numbered.Keys.GroupBy(f=>f.Date).OrderByDescending(g=>g.Key))
        {
            var items=AssignTimelineLanes(group.ToList()).Select(item=>new NavigatorSegment(item,numbered[item.Fragment].Number,numbered[item.Fragment].Id,numbered[item.Fragment].EndsNow)).ToList();
            int lanes=items.Max(s=>s.Item.Lane)+1;
            double height=24+lanes*27+(showDate?24:0);
            models.Add(new(group.Key,items,top,height,showDate));top+=height+10;
        }
        _timelineDayModels=models;_timelineDays.Height=Math.Max(0,top-10);
        QueueNavigatorTimelineDraw();
    }

    private void QueueNavigatorTimelineDraw()
    {
        if(_timelineDrawPending)return;_timelineDrawPending=true;
        Dispatcher.UIThread.Post(()=>{_timelineDrawPending=false;if(IsVisible)DrawNavigatorTimeline();},DispatcherPriority.Loaded);
    }

    private void DrawNavigatorTimeline()
    {
        double width=_timelineDays.Bounds.Width;
        if(width<3||_timelineDays.TranslatePoint(default,DashboardScrollViewer) is not{} origin)return;
        double first=-origin.Y-100,last=first+DashboardScrollViewer.Viewport.Height+200;
        var visible=_timelineDayModels.Where(d=>d.Top+d.Height>=first&&d.Top<=last).ToArray();
        var dates=visible.Select(d=>d.Date).ToHashSet();
        foreach(var date in _timelineDayViews.Keys.Where(d=>!dates.Contains(d)).ToArray()){_timelineDays.Children.Remove(_timelineDayViews[date].View);_timelineDayViews.Remove(date);}
        foreach(var day in visible)
        {
            if(!_timelineDayViews.TryGetValue(day.Date,out var existing))
            {existing=(new Canvas{ClipToBounds=true},day,0);_timelineDays.Children.Add(existing.View);}
            var view=existing.View;view.Width=width;view.Height=day.Height;Canvas.SetTop(view,day.Top);
            if(existing.Width!=width||existing.Model.ShowDate!=day.ShowDate||!existing.Model.Segments.SequenceEqual(day.Segments))
                DrawNavigatorDay(view,day,width);
            _timelineDayViews[day.Date]=(view,day,width);
        }
    }

    private void DrawNavigatorDay(Canvas view,NavigatorTimelineDay day,double width)
    {
        view.Children.Clear();double heading=day.ShowDate?24:0;
        if(day.ShowDate)
        {
            string total=$"{FormatCompactDuration(TimeSpan.FromTicks(day.Segments.Sum(s=>s.Item.Fragment.Duration.Ticks)))} · {day.Segments.Count} {(day.Segments.Count==1?"session":"sessions")}";
            var count=DashboardLabel(total,10);var head=new Grid{Width=width,Height=20,ColumnDefinitions=new("*,Auto"),Children={DashboardLabel(day.Date.ToString("ddd, d MMM yyyy",CultureInfo.CurrentCulture),12,ClockPickerPresentation.Navy),count}};Grid.SetColumn(count,1);view.Children.Add(head);
        }
        var bounds=day.Segments[0].Item.Fragment;double seconds=(bounds.DayEndUtc-bounds.DayStartUtc).TotalSeconds;
        for(int hour=0;hour<=24;hour+=6)
        {
            double x=width*Math.Clamp((LocalBoundaryToUtc(day.Date.AddHours(hour))-bounds.DayStartUtc).TotalSeconds/seconds,0,1);
            var tick=DashboardLabel($"{hour:00}:00",9);tick.Width=34;tick.TextAlignment=hour==0?TextAlignment.Left:hour==24?TextAlignment.Right:TextAlignment.Center;
            Canvas.SetLeft(tick,Math.Clamp(x-17,0,Math.Max(0,width-34)));Canvas.SetTop(tick,heading);view.Children.Add(tick);
            if(hour is >0 and <24){var guide=new Border{Width=1,Height=day.Height-heading-20,Background=DashboardRule};Canvas.SetLeft(guide,x);Canvas.SetTop(guide,heading+20);view.Children.Add(guide);}
        }
        for(int lane=0;lane<=day.Segments.Max(s=>s.Item.Lane);lane++)
        {var track=new Border{Width=width,Height=21,Background=TimelineTrack,CornerRadius=new(3)};Canvas.SetTop(track,heading+22+lane*27);view.Children.Add(track);}
        foreach(var segment in day.Segments)
        {
            var fragment=segment.Item.Fragment;
            double start=width*Math.Clamp((fragment.StartUtc-bounds.DayStartUtc).TotalSeconds/seconds,0,1);
            double end=width*Math.Clamp((fragment.EndUtc-bounds.DayStartUtc).TotalSeconds/seconds,0,1);
            double left=Math.Min(start,width-3),barWidth=Math.Min(width-left,Math.Max(3,end-start));
            var bar=new Border{Name="DashboardTimelineSegment",Tag=segment.Id,Width=barWidth,Height=21,Background=GetProjectBrush(fragment.ProjectKey),CornerRadius=new(3),ClipToBounds=true};
            if(fragment.IsLive){bar.BorderBrush=ClockPickerPresentation.Navy;bar.BorderThickness=new(1.5);}
            if(barWidth>=18)
            {
                var text=DashboardLabel(barWidth>=92?$"{segment.Number} · {fragment.ProjectName}":segment.Number.ToString(CultureInfo.CurrentCulture),10,Brushes.White);
                text.FontWeight=FontWeight.SemiBold;text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.Margin=new(5,0);AutoTextDirection.Apply(text,text.Text);bar.Child=text;
            }
            string tip=$"{segment.Number}. {fragment.ProjectName}\n{fragment.StartLocal:d MMM yyyy HH:mm:ss zzz} – {(segment.EndsNow?"Now":fragment.EndLocal.ToString("d MMM yyyy HH:mm:ss zzz",CultureInfo.CurrentCulture))}\n{FormatDetailedDuration(fragment.Duration)}";
            ToolTip.SetTip(bar,tip);AutomationProperties.SetName(bar,tip);Canvas.SetLeft(bar,left);Canvas.SetTop(bar,heading+22+segment.Item.Lane*27);view.Children.Add(bar);
        }
    }

    private void RenderNavigatorRecords()
    {
        bool expanded=ProjectRecordsExpander.IsExpanded;
        _navigatorRecordsContent.IsVisible=expanded;_toggleRecords.Content=expanded?"Hide records":"Show records";
        _recordsCount.Text=$"{_records.Count} {(_records.Count==1?"record":"records")} · Newest first";
        int pageCount=Math.Max(1,(_records.Count+RecordsPerPage-1)/RecordsPerPage);
        _recordsPageIndex=Math.Clamp(_recordsPageIndex,0,pageCount-1);
        int offset=_recordsPageIndex*RecordsPerPage;
        RecordsPageStatusText.Text=_records.Count==0?"0 records":$"{offset+1}–{Math.Min(_records.Count,offset+RecordsPerPage)} of {_records.Count}";
        PreviousRecordsPageButton.IsEnabled=_recordsPageIndex>0;NextRecordsPageButton.IsEnabled=_recordsPageIndex+1<pageCount;
        RecordsEmptyState.IsVisible=_records.Count==0;RecordsPanel.IsVisible=_records.Count>0;_recordColumns.IsVisible=_records.Count>0&&!_compactRecords;
        var selected=ProjectFilterSelector.SelectedItem as ProjectFilterOption;
        RecordsEmptyHeadingText.Text=selected?.Key==null?"No project records in this period":$"No {selected.Name} records in this period";
        if(!expanded)return;
        var visible=_records.Skip(offset).Take(RecordsPerPage).ToArray();var ids=visible.Select(r=>r.Source.Id).ToHashSet();
        foreach(var id in _navigatorRecordRows.Keys.Where(id=>!ids.Contains(id)).ToArray()){RecordsPanel.Children.Remove(_navigatorRecordRows[id].Card);_navigatorRecordRows.Remove(id);}
        bool canEdit=SafeCanMutateRecords();
        for(int i=0;i<visible.Length;i++)
        {
            var interval=visible[i];
            if(!_navigatorRecordRows.TryGetValue(interval.Source.Id,out var row)){row=CreateNavigatorRecordRow();_navigatorRecordRows.Add(interval.Source.Id,row);}
            UpdateNavigatorRecordRow(row,interval,offset+i+1,canEdit);
            int old=RecordsPanel.Children.IndexOf(row.Card);
            if(old!=i){if(old>=0)RecordsPanel.Children.RemoveAt(old);RecordsPanel.Children.Insert(i,row.Card);}
        }
    }

    private NavigatorRecordRow CreateNavigatorRecordRow()
    {
        TextBlock Text(string name,double size,IBrush? brush=null){var t=DashboardLabel("",size,brush);t.Name=name;return t;}
        var row=new NavigatorRecordRow
        {
            Number=Text("DashboardRecordNumber",11,Brushes.White),Name=Text("DashboardRecordName",13,ClockPickerPresentation.Navy),Detail=Text("DashboardRecordDetail",10),
            Time=Text("DashboardRecordTime",12,ClockPickerPresentation.Navy),Date=Text("DashboardRecordDate",10),Duration=Text("DashboardRecordDuration",13,ClockPickerPresentation.Navy),Live=DashboardLabel("Live",11,DashboardTeal),Edit=new(){Content="Edit"},Delete=new(){Content="Delete"}
        };
        row.Number.HorizontalAlignment=HorizontalAlignment.Center;row.Number.VerticalAlignment=VerticalAlignment.Center;
        row.Name.FontWeight=FontWeight.SemiBold;row.Name.TextWrapping=TextWrapping.NoWrap;row.Name.TextTrimming=TextTrimming.CharacterEllipsis;row.Name.Margin=new(0,0,12,0);
        row.Detail.TextWrapping=TextWrapping.NoWrap;row.Detail.TextTrimming=TextTrimming.CharacterEllipsis;row.Detail.Margin=new(0,0,12,0);
        row.Duration.FontWeight=FontWeight.SemiBold;row.Duration.TextWrapping=TextWrapping.NoWrap;row.Duration.TextTrimming=TextTrimming.CharacterEllipsis;
        row.Time.TextWrapping=TextWrapping.NoWrap;row.Time.TextTrimming=TextTrimming.CharacterEllipsis;
        row.Date.TextWrapping=TextWrapping.NoWrap;row.Date.TextTrimming=TextTrimming.CharacterEllipsis;
        row.Marker=new(){Name="DashboardRecordMarker",Width=24,Height=24,CornerRadius=new(4),VerticalAlignment=VerticalAlignment.Center,Child=row.Number};
        row.Names=new(){Spacing=2,VerticalAlignment=VerticalAlignment.Center,Children={row.Name,row.Detail}};
        row.Project=new(){ColumnDefinitions=new("24,10,*"),Children={row.Marker,row.Names}};Grid.SetColumn(row.Names,2);
        row.Clock=new(){Spacing=2,VerticalAlignment=VerticalAlignment.Center,Children={row.Time,row.Date}};
        Action(row.Edit,56);row.Edit.Height=30;Action(row.Delete,64);row.Delete.Height=30;row.Delete.Classes.Add("editorDanger");
        row.Edit.Click+=async(_,_)=>{if(SafeCanMutateRecords()&&!row.Source.IsOpen)await EditRecordAsync(row.Source);};
        row.Delete.Click+=async(_,_)=>{if(SafeCanMutateRecords()&&!row.Source.IsOpen)await DeleteRecordAsync(row.Source);};
        row.Actions=new(){Orientation=Orientation.Horizontal,Spacing=6,VerticalAlignment=VerticalAlignment.Center,Children={row.Edit,row.Delete,row.Live}};
        row.Layout=new(){Children={row.Project,row.Clock,row.Duration,row.Actions}};
        row.Card=new(){Name="DashboardRecordRow",Padding=new(10,4),MinHeight=42,CornerRadius=new(5),Background=RecordPaper,BorderBrush=DashboardRule,BorderThickness=new(1),Child=row.Layout};
        LayoutNavigatorRecordRow(row);return row;
    }

    private void UpdateNavigatorRecordRow(NavigatorRecordRow row,DisplayInterval interval,int number,bool canEdit)
    {
        var source=interval.Source;row.Source=source;row.Card.Tag=source.Id;
        string category=_history?.Projects.FirstOrDefault(p=>string.Equals(p.Key,source.ProjectKey,StringComparison.OrdinalIgnoreCase))?.Category??"";
        bool partial=interval.StartUtc!=EnsureUtc(source.StartUtc)||interval.EndUtc!=EnsureUtc(source.EndUtc??_history!.AsOfUtc);
        bool endsNow=source.IsOpen&&interval.EndUtc==EnsureUtc(_history!.AsOfUtc);
        row.Number.Text=number.ToString(CultureInfo.CurrentCulture);row.Marker.Background=GetProjectBrush(source.ProjectKey);
        row.Name.Text=source.ProjectName;AutoTextDirection.Apply(row.Name,source.ProjectName);ToolTip.SetTip(row.Name,source.ProjectName);
        row.Detail.Text=source.IsOpen?"RUNNING · pause to edit":category;
        if(partial)row.Detail.Text=source.IsOpen?row.Detail.Text+" · Part of saved record":"Part of saved record";
        string end=endsNow?"Now":interval.EndLocal.ToString(interval.EndLocal.Date==interval.StartLocal.Date?"HH:mm":"d MMM HH:mm",CultureInfo.CurrentCulture);
        row.Time.Text=$"{interval.StartLocal:HH:mm} – {end}";
        row.Date.Text=interval.StartLocal.ToString(_compactRecords?"d MMM":"d MMM yyyy",CultureInfo.CurrentCulture)+(_compactRecords&&partial?" · Partial record":"");
        row.Duration.Text=FormatCompactDuration(interval.Duration);ToolTip.SetTip(row.Duration,FormatDetailedDuration(interval.Duration));
        row.Edit.IsVisible=row.Delete.IsVisible=canEdit&&!source.IsOpen;row.Live.IsVisible=source.IsOpen;
        AutomationProperties.SetName(row.Edit,$"Edit {source.ProjectName}, {source.StartUtc.ToLocalTime():d MMM HH:mm}");
        AutomationProperties.SetName(row.Delete,$"Delete {source.ProjectName}, {source.StartUtc.ToLocalTime():d MMM HH:mm}");
        string tip=$"{number}. {source.ProjectName}{(category.Length>0?" · "+category:"")}\nTime in period: {interval.StartLocal:d MMM yyyy HH:mm:ss zzz} – {(endsNow?"Now":interval.EndLocal.ToString("d MMM yyyy HH:mm:ss zzz",CultureInfo.CurrentCulture))}\n{FormatDetailedDuration(interval.Duration)}";
        if(partial)tip+=$"\nFull saved record: {source.StartUtc.ToLocalTime():d MMM yyyy HH:mm:ss zzz} – {(source.EndUtc.HasValue?source.EndUtc.Value.ToLocalTime().ToString("d MMM yyyy HH:mm:ss zzz",CultureInfo.CurrentCulture):"Now")}";
        tip+=source.IsOpen?"\nRunning · pause to edit":canEdit?"\nEdit opens the full saved record":"\nRead-only";
        ToolTip.SetTip(row.Card,tip);AutomationProperties.SetName(row.Card,tip);
    }

    private void ResizeNavigatorRecords(bool compact)
    {
        _compactRecords=compact;
        foreach(var row in _navigatorRecordRows.Values)LayoutNavigatorRecordRow(row);
        if(_navigatorTimeline!=null)RenderNavigatorRecords();
    }

    private void LayoutNavigatorRecordRow(NavigatorRecordRow row)
    {
        row.Layout.ColumnDefinitions=new(_compactRecords?"*,92,138":"*,210,92,138");row.Layout.RowDefinitions=new(_compactRecords?"24,3,18":"Auto");
        row.Detail.IsVisible=!_compactRecords;
        row.Clock.Orientation=_compactRecords?Orientation.Horizontal:Orientation.Vertical;row.Clock.Spacing=_compactRecords?8:2;row.Clock.Margin=_compactRecords?new(34,0,12,0):default;
        Grid.SetColumn(row.Clock,_compactRecords?0:1);Grid.SetRow(row.Clock,_compactRecords?2:0);
        Grid.SetColumn(row.Duration,_compactRecords?1:2);Grid.SetRowSpan(row.Duration,_compactRecords?3:1);
        Grid.SetColumn(row.Actions,_compactRecords?2:3);Grid.SetRowSpan(row.Actions,_compactRecords?3:1);
    }
}
