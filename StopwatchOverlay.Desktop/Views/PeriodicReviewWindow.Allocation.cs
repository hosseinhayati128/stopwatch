using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class PeriodicReviewWindow
{
    private ComboBox? _reviewCollisionSelector;
    private TextBlock? _reviewAllocationNote;
    private static readonly IBrush ReviewTeal=Brush.Parse("#2E756D"),ReviewOchre=Brush.Parse("#8B5C24"),ReviewGap=Brush.Parse("#DDC599");
    private static readonly IBrush ReviewBreak=Brush.Parse("#716A5D");
    private static readonly IBrush[] ReviewProjectColors =
    [
        Brush.Parse("#2D7169"),Brush.Parse("#3B6394"),Brush.Parse("#7A527F"),Brush.Parse("#96543E"),
        Brush.Parse("#596F43"),Brush.Parse("#8A4764"),Brush.Parse("#456E83"),Brush.Parse("#8B5C24"),
        Brush.Parse("#505989"),Brush.Parse("#596D58"),Brush.Parse("#834C48"),Brush.Parse("#6F6331")
    ];
    private readonly Dictionary<string,IBrush> _reviewProjectColors=new(StringComparer.OrdinalIgnoreCase);
    private IBrush AllocationInk(ReviewProjectSelectionItem? item)
    {
        if(item==null)return ReviewGap;
        if(item.IsBreak)return ReviewBreak;
        string project=item.ProjectName.Trim();
        if(!_reviewProjectColors.TryGetValue(project,out var ink))
        {
            // shortcut: colors repeat beyond twelve projects per review; expand the palette if larger reviews need it.
            ink=ReviewProjectColors[_reviewProjectColors.Count%ReviewProjectColors.Length];
            _reviewProjectColors.Add(project,ink);
        }
        return ink;
    }

    private Grid CreateNavigatorAllocationLayout()
    {
        foreach(var control in new Control[]{Step2Container,Step2AllocationSummaryText,DistributionBarContainer,TimelineScaleStartText,TimelineScaleMidText,TimelineScaleEndText,
            Step2AddSessionButton,AllocatedProjectsContainer,TimelinePreviewContainer,Step2BackButton,Step2SkipFeelingsButton,Step2ContinueButton,Step2VerifyButton})DetachReviewControl(control);
        var type=StopwatchTypography.Resolve("navigator-bold");
        var heading=new StackPanel{Spacing=5,Children={new TextBlock{Text="Activity & Time Review",FontFamily=type.NameFont,FontWeight=type.NameWeight,FontSize=30,Foreground=ReviewNavy},
            ReviewLabel("Allocate time and check the order of your sessions.",12),
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=14,Margin=new(0,6,0,0),Children={ReviewLabel("01  Projects & activity",11),ReviewPill("02  Allocate time",ReviewNavy,ReviewCream)}}}};
        heading.PointerPressed+=OnHeaderPointerPressed;
        var start=_startUtc.ToLocalTime();var end=_endUtc.ToLocalTime();
        string dates=start.Date==DateTime.Today&&end.Date==start.Date?"TODAY":start.Date==end.Date?start.ToString("dd MMM"):$"{start:dd MMM} – {end:dd MMM}";
        var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),Children={ReviewLabel($"{dates} · {start:HH:mm}–{end:HH:mm}",10),ReviewText($"{TotalPeriodMinutes} minutes",18)}};
        var close=ReviewAction("",28);close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
        close.Content=ReviewIcon("M2,2 L14,14 M14,2 L2,14",ReviewNavy,16);AutomationProperties.SetName(close,"Close review without saving");close.Click+=SkipReportButton_Click;
        var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

        Step2AllocationSummaryText.FontFamily=ReviewBody;Step2AllocationSummaryText.FontSize=15;Step2AllocationSummaryText.Foreground=ReviewNavy;
        _reviewCollisionSelector=new ComboBox{Name="ReviewCollisionSelector",ItemsSource=new[]{"Split & continue","Stop at recorded time"},
            SelectedIndex=string.Equals(_settings.PeriodicReviewFixedCollisionBehavior,"StopAtFixed",StringComparison.OrdinalIgnoreCase)?1:0,
            Height=28,MinHeight=0,FontFamily=ReviewBody,FontSize=11,Padding=new(8,3),HorizontalAlignment=HorizontalAlignment.Stretch};
        _reviewCollisionSelector.Classes.Add("editorField");AutomationProperties.SetName(_reviewCollisionSelector,"When manual time reaches a recorded session");
        _reviewCollisionSelector.SelectionChanged+=(_,_)=>
        {
            bool stop=string.Equals(_settings.PeriodicReviewFixedCollisionBehavior,"StopAtFixed",StringComparison.OrdinalIgnoreCase);
            if(_reviewCollisionSelector.SelectedIndex>=0&&(_reviewCollisionSelector.SelectedIndex==1)!=stop)CollisionModeButton_Click(_reviewCollisionSelector,new());
        };
        var summaryHead=new Grid{ColumnDefinitions=new("*,114,174"),Children={Step2AllocationSummaryText,ReviewLabel("At recorded time",10),_reviewCollisionSelector}};
        Grid.SetColumn(summaryHead.Children[1],1);Grid.SetColumn(_reviewCollisionSelector,2);Grid.SetColumn(Step2AllocationSummaryText,0);
        DistributionBarContainer.Height=25;Grid.SetColumn(DistributionBarContainer,0);
        var ticks=new Grid{Children={TimelineScaleStartText,TimelineScaleMidText,TimelineScaleEndText}};
        foreach(var tick in new[]{TimelineScaleStartText,TimelineScaleMidText,TimelineScaleEndText}){tick.FontFamily=ReviewBody;tick.FontSize=9;tick.Foreground=ReviewMuted;}
        _reviewAllocationNote=ReviewLabel("",11);
        var summary=new Border{Padding=new(12,9),Background=Brush.Parse("#42FFF6E4"),BorderBrush=ReviewRule,BorderThickness=new(1),CornerRadius=new(7),Margin=new(0,6,0,6),
            Child=new StackPanel{Spacing=4,Children={summaryHead,DistributionBarContainer,ticks,_reviewAllocationNote}}};

        AllocatedProjectsContainer.Spacing=8;TimelinePreviewContainer.Spacing=7;
        StyleReviewAction(Step2AddSessionButton,100,30);Step2AddSessionButton.Content="+ Add session";Step2AddSessionButton.Margin=new(0);
        var sessions=ReviewScroll(AllocatedProjectsContainer,"ReviewSessionsScroll");var timeline=ReviewScroll(TimelinePreviewContainer,"ReviewTimelineScroll");
        var sessionHead=new Grid{ColumnDefinitions=new("*,100"),Children={ReviewText("Sessions",20),Step2AddSessionButton}};Grid.SetColumn(Step2AddSessionButton,1);
        var sessionLayout=new Grid{RowDefinitions=new("42,*"),Children={sessionHead,sessions}};Grid.SetRow(sessions,1);
        var timelineHead=new StackPanel{Spacing=3,Children={ReviewText("Timeline",20),ReviewLabel("Sessions, breaks and remaining time",11)}};
        var timelineLayout=new Grid{RowDefinitions=new("48,*,25"),Children={timelineHead,timeline,ReviewLabel("Drag manual blocks above to reposition them.",10)}};
        Grid.SetRow(timeline,1);Grid.SetRow(timelineLayout.Children[2],2);
        var left=ReviewPane(sessionLayout);var right=ReviewPane(timelineLayout);left.Padding=right.Padding=new(12);
        var panes=new Grid{ColumnDefinitions=new("1.15*,14,*"),Margin=new(0,7,0,8),Children={left,right}};Grid.SetColumn(right,2);
        Step2Container.Children.Clear();Step2Container.RowDefinitions=new("120,*");Step2Container.ColumnDefinitions.Clear();Step2Container.Margin=new(0);
        Step2Container.Children.Add(summary);Step2Container.Children.Add(panes);Grid.SetRow(panes,1);

        StyleReviewAction(Step2BackButton,82,38);Step2BackButton.Content="Back";
        StyleReviewAction(Step2SkipFeelingsButton,128,38);Step2SkipFeelingsButton.Content="Save time only";
        StyleReviewAction(Step2ContinueButton,176,38,true);Step2ContinueButton.Content="Continue to feelings";
        StyleReviewAction(Step2VerifyButton,176,38,true);Step2VerifyButton.Content="Verify & save";
        var actions=new Grid{ColumnDefinitions=new("82,12,*,128,8,176"),Children={Step2BackButton,ReviewLabel(_settings.PeriodicReviewFeelingsEnabled?"Esc back · Enter continue":"Esc back · Enter save",10),Step2SkipFeelingsButton,Step2ContinueButton,Step2VerifyButton}};
        Grid.SetColumn(actions.Children[1],2);Grid.SetColumn(Step2SkipFeelingsButton,3);Grid.SetColumn(Step2ContinueButton,5);Grid.SetColumn(Step2VerifyButton,5);
        var footer=new Grid{RowDefinitions=new("18,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=ReviewRule,VerticalAlignment=VerticalAlignment.Top},actions}};Grid.SetRow(actions,1);
        var layout=new Grid{RowDefinitions=new("84,*,66"),Children={header,Step2Container,footer}};Grid.SetRow(Step2Container,1);Grid.SetRow(footer,2);return layout;
    }

    private void StyleNavigatorAllocatedCard(AllocatedCardViewHolder holder,int index,TextBlock name,StackPanel oldActions,Button expand,StackPanel presets)
    {
        var item=holder.Item;var border=holder.Border;border.Tag=item;
        border.Padding=new(10,7);border.Margin=new(0);border.Background=ReviewCream;border.BorderBrush=item.IsExpanded?ReviewGold:ReviewRule;
        border.BorderThickness=new(item.IsExpanded?1.5:1);border.CornerRadius=new(5);
        var actions=oldActions.Children.OfType<Button>().Where(b=>b!=expand).ToArray();oldActions.Children.Clear();
        foreach(var c in new Control?[]{name,expand,holder.RangeBadgeText,holder.Slider,holder.MinInput,holder.StartBox,holder.EndBox})if(c!=null)DetachReviewControl(c);
        name.Text=item.IsBreak?"Break / Empty":item.ProjectName;name.FontFamily=ReviewBody;name.FontSize=14;name.Foreground=ReviewNavy;name.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(name,name.Text);
        StyleReviewAction(expand,26,26);expand.Padding=new(4);expand.BorderBrush=Brushes.Transparent;expand.Content=ReviewIcon(item.IsExpanded?"M3,10 L8,5 L13,10":"M3,5 L8,10 L13,5",ReviewNavy,12);
        AutomationProperties.SetName(expand,$"{(item.IsExpanded?"Collapse":"Expand")} {item.ProjectName} session");expand.Name="AllocationExpand";
        var badge=ReviewPill((index+1).ToString(),AllocationInk(item),ReviewCream);badge.Padding=new(7,3);badge.VerticalAlignment=VerticalAlignment.Center;
        var kind=ReviewLabel(item.IsFixedTracked?"RECORDED":item.TotalParts>1?$"Part {item.PartIndex} of {item.TotalParts}":"MANUAL",10);kind.Foreground=item.IsFixedTracked?ReviewTeal:ReviewMuted;
        kind.Margin=new(8,0,0,0);
        var title=new Grid{ColumnDefinitions=new("Auto,8,*,Auto,28"),Children={badge,name,kind,expand}};Grid.SetColumn(name,2);Grid.SetColumn(kind,3);Grid.SetColumn(expand,4);
        holder.RangeBadgeText!.Foreground=ReviewMuted;holder.RangeBadgeText.FontFamily=ReviewBody;holder.RangeBadgeText.FontWeight=FontWeight.Normal;holder.RangeBadgeText.FontSize=11;
        var body=new StackPanel{Spacing=4,Children={title,holder.RangeBadgeText}};
        if(item.IsFixedTracked)body.Children.Add(ReviewLabel("Time and duration are fixed by your stopwatch.",10));
        else
        {
            var buttons=presets.Children.OfType<Button>().ToArray();presets.Children.Clear();
            foreach(var b in buttons){StyleReviewAction(b,26,26);b.Padding=new(0);b.FontSize=10;AutomationProperties.SetName(b,$"{b.Content} minutes for {item.ProjectName}");}
            StyleAllocationField(holder.MinInput,"Minutes",26);holder.MinInput.Width=42;holder.MinInput.Name="AllocationMinutes";
            holder.Slider.Margin=new(0);holder.Slider.Height=26;holder.Slider.MinHeight=0;AutomationProperties.SetName(holder.Slider,$"Duration of {item.ProjectName}");
            var duration=new Grid{Name="AllocationDurationControls",ColumnDefinitions=new("*,6,26,4,26,4,42,24,26,4,26"),Children={holder.Slider,buttons[0],buttons[1],holder.MinInput,ReviewLabel("min",10),buttons[2],buttons[3]}};
            int[] columns=[0,2,4,6,7,8,10];for(int i=0;i<duration.Children.Count;i++)Grid.SetColumn(duration.Children[i],columns[i]);body.Children.Add(duration);
        }
        holder.WarningText=ReviewLabel(NavigatorCollisionWarning(item),10,new(0,3,0,0));holder.WarningText.Foreground=ReviewOchre;holder.WarningText.IsVisible=!string.IsNullOrWhiteSpace(holder.WarningText.Text);body.Children.Add(holder.WarningText);
        var shift=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={ReviewLabel("Shift",10)}};
        var order=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,Children={ReviewLabel("Order",10)}};
        var more=new StackPanel{Orientation=Orientation.Horizontal,Spacing=7};
        foreach(var b in actions)
        {
            string key=b.Content?.ToString()??"";
            string label=key switch{"◀"=>"−1 min","▶"=>"+1 min","✂️"=>"Split","＋"=>"Add part","🗑️"=>"Remove",_=>key};
            StyleReviewAction(b,key is "▲" or "▼"?28:key=="✂️"?60:key=="＋"?82:key=="🗑️"?78:65,28);b.Content=label;b.Padding=new(4,0);
            AutomationProperties.SetName(b,ToolTip.GetTip(b)?.ToString()??label);
            if(key is "▲" or "▼") {b.Content=ReviewIcon(key=="▲"?"M3,10 L8,5 L13,10":"M3,5 L8,10 L13,5",ReviewNavy,10);order.Children.Add(b);}
            else if(key is "◀" or "▶")shift.Children.Add(b);
            else {if(key=="🗑️")b.Classes.Add("editorDanger");if(key=="✂️"){holder.SplitButton=b;b.IsEnabled=item.AllocatedMinutes>=2;}more.Children.Add(b);}
        }
        if(item.IsExpanded)
        {
            StyleAllocationField(holder.StartBox!,"Start time (HH:mm)",30);StyleAllocationField(holder.EndBox!,"End time (HH:mm)",30);
            holder.StartBox!.Name="AllocationStart";holder.EndBox!.Name="AllocationEnd";
            holder.StartBox.IsEnabled=holder.EndBox.IsEnabled=!item.IsFixedTracked;
            var start=new StackPanel{Spacing=4,Children={ReviewLabel("START",9),holder.StartBox}};var end=new StackPanel{Spacing=4,Children={ReviewLabel("END",9),holder.EndBox}};
            var times=new Grid{ColumnDefinitions=new("*,12,*"),Margin=new(0,7,0,0),Children={start,end}};Grid.SetColumn(end,2);body.Children.Add(times);
            if(!item.IsFixedTracked){order.IsVisible=order.Children.Count>1;var row=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,6,0,0),Children={shift,order}};Grid.SetColumn(order,1);body.Children.Add(row);}
            more.Margin=new(0,6,0,0);body.Children.Add(more);
        }
        border.Child=body;
    }

    private static void StyleAllocationField(TextBox input,string label,double height)
    {
        input.Classes.Add("editorField");input.MinWidth=0;input.MinHeight=0;input.Height=height;input.Padding=new(7,3);input.FontFamily=ReviewBody;input.FontSize=12;
        input.VerticalContentAlignment=VerticalAlignment.Center;AutomationProperties.SetName(input,label);
    }
    private static string NavigatorCollisionWarning(ReviewProjectSelectionItem item)=>(item.CollisionWarning??"").Replace("⚠️","").Trim();

    private void StyleNavigatorDistributionSegment(Border border,TimelineSlot slot)
    {
        border.Background=AllocationInk(slot.Item);border.BorderBrush=Brush.Parse("#EBD3A5");border.BorderThickness=new(0,0,2,0);border.Margin=new(0);border.CornerRadius=new(0);
        var label=ReviewText(slot.IsUnallocated?$"{slot.DurationMinutes:0}m gap":$"{_selectedItems.IndexOf(slot.Item!)+1} · {slot.DurationMinutes:0}m",11);
        label.Foreground=slot.IsUnallocated?ReviewNavy:ReviewCream;label.HorizontalAlignment=HorizontalAlignment.Center;label.TextTrimming=TextTrimming.CharacterEllipsis;border.Child=label;
    }

    private Control CreateNavigatorTimelineCard(TimelineSlot slot)
    {
        var item=slot.Item;
        var ink=AllocationInk(item);
        Control mark=item?.IsFixedTracked==true?ReviewIcon("M4,7 L4,5 C4,0 12,0 12,5 L12,7 M2,7 L14,7 L14,15 L2,15 Z",ReviewCream,14):ReviewText(slot.IsUnallocated?"—":(_selectedItems.IndexOf(item!)+1).ToString(),11);
        if(mark is TextBlock number)number.Foreground=slot.IsUnallocated?ReviewNavy:ReviewCream;
        mark.HorizontalAlignment=HorizontalAlignment.Center;
        var badge=new Border{Width=24,Height=26,Background=ink,CornerRadius=new(4),Child=mark,VerticalAlignment=VerticalAlignment.Center};
        var name=ReviewText(slot.IsUnallocated?"Unallocated":slot.IsBreak?"Break / Empty":item!.ProjectName,13);name.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(name,name.Text);
        string detail=slot.IsUnallocated?"Not saved to project history":item!.IsFixedTracked?"Recorded · fixed":slot.IsBreak?"Break · not saved":item.TotalParts>1?$"Part {item.PartIndex} of {item.TotalParts}":"Manual session";
        var words=new StackPanel{Spacing=3,Children={name,ReviewLabel(detail,10)}};
        var amount=new StackPanel{Spacing=3,HorizontalAlignment=HorizontalAlignment.Right,Children={ReviewText($"{slot.DurationMinutes:0} min",13),ReviewLabel($"{slot.StartLocal:HH:mm}–{slot.EndLocal:HH:mm}",10)}};
        var row=new Grid{ColumnDefinitions=new("24,8,*,8,Auto"),Children={badge,words,amount}};Grid.SetColumn(words,2);Grid.SetColumn(amount,4);
        var card=new Border{Padding=new(10,9),Background=slot.IsUnallocated?Brush.Parse("#24AF7C29"):ReviewCream,BorderBrush=ink,BorderThickness=new(3,1,1,1),CornerRadius=new(5),Child=row};
        ToolTip.SetTip(card,CreateRichToolTip(slot));return card;
    }

    private void UpdateNavigatorAllocationSummary(List<TimelineSlot> timeline)
    {
        int allocated=(int)Math.Round(_selectedItems.Sum(x=>x.AllocatedMinutes));int gap=(int)Math.Round(timeline.Where(x=>x.IsUnallocated).Sum(x=>x.DurationMinutes));
        Step2AllocationSummaryText.Text=$"{allocated} of {TotalPeriodMinutes} min allocated";Step2AllocationSummaryText.Foreground=ReviewNavy;
        _reviewAllocationNote!.Text=allocated>TotalPeriodMinutes?$"Allocated time exceeds the review period by {allocated-TotalPeriodMinutes}m.":gap>0?$"{gap}m unallocated · Gaps and breaks are left out of project history.":"All time allocated · Review the timeline before saving.";
        _reviewAllocationNote.Foreground=gap>0||allocated>TotalPeriodMinutes?ReviewOchre:ReviewTeal;
    }
}
