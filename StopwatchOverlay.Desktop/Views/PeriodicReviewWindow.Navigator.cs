using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class PeriodicReviewWindow
{
    private StopwatchFrameSurface? _navigatorReviewFrame;
    private Control? _legacyReviewContent;
    private Grid? _reviewProjectsGrid;
    private ScrollViewer? _reviewSequenceScroll;
    private TextBlock? _reviewSequenceCount;
    private TextBlock? _reviewActivityNote;
    private Grid? _reviewProjectsLayout;
    private Grid? _reviewAllocationLayout;
    private Grid? _reviewMoodLayout;
    private static readonly IBrush ReviewNavy=Brush.Parse("#102B3B"),ReviewMuted=Brush.Parse("#655539"),ReviewGold=Brush.Parse("#AF7C29"),
        ReviewCream=Brush.Parse("#F6E8C9"),ReviewRule=Brush.Parse("#509A7B48");
    private static readonly FontFamily ReviewBody=StopwatchTypography.Resolve("modern-deck").NameFont;

    private void ApplyNavigatorReviewLayout()
    {
        if (_settings.ThemeMode != AppThemeCatalog.Nika) return;
        _legacyReviewContent=(Control)Content!;Content=null;
        foreach(var control in new Control[]{Step1Container,NewProjectTextBox,AddNewProjectButton,ActivitySummariesContainer,
            ActivityWatchStatusCard,ActivityWatchStatusHeading,ActivityWatchStatusMessage,RetryActivityWatchButton,
            DecreaseThresholdButton,IncreaseThresholdButton,ThresholdPercentText,SelectedSequenceChipsContainer,SelectedSequenceText,
            ContinueToStep2Button,Step1SkipButton}) DetachReviewControl(control);
        Step1Container.Children.Clear();Step1Container.ColumnDefinitions=new("*,14,1.08*");
        Step1Container.Margin=new(0,8,0,12);
        NavigatorFormStyles.Apply(this);
        Width=1020;Height=700;MinHeight=520;
        var type=StopwatchTypography.Resolve("navigator-bold");
        var heading=new StackPanel{Spacing=5,Children={new TextBlock{Text="Activity & Time Review",FontFamily=type.NameFont,FontWeight=type.NameWeight,FontSize=30,Foreground=ReviewNavy},
            ReviewLabel("Choose projects in the order you worked on them.",12),
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=14,Margin=new(0,6,0,0),Children={
                ReviewPill("01  Projects & activity",ReviewNavy,ReviewCream),ReviewLabel("02  Allocate time",11)}}}};
        heading.PointerPressed+=OnHeaderPointerPressed;
        var start=_startUtc.ToLocalTime();var end=_endUtc.ToLocalTime();
        string dates=start.Date==DateTime.Today&&end.Date==start.Date?"TODAY":start.Date==end.Date?start.ToString("dd MMM"): $"{start:dd MMM} – {end:dd MMM}";
        var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),HorizontalAlignment=HorizontalAlignment.Right,
            Children={ReviewLabel($"{dates} · {start:HH:mm}–{end:HH:mm}",10),ReviewText($"{TotalPeriodMinutes} minutes",18)}};
        var close=ReviewAction("",28);close.Name="ReviewCloseButton";close.Height=28;close.Padding=new(6);close.Content=ReviewIcon("M2,2 L14,14 M14,2 L2,14",ReviewNavy,16);
        close.BorderBrush=Brushes.Transparent;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
        AutomationProperties.SetName(close,"Close review without saving");close.Click+=SkipReportButton_Click;
        var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

        _reviewProjectsGrid=new Grid{Name="ReviewProjectsGrid",ColumnDefinitions=new("*,8,*")};
        var projectScroll=ReviewScroll(_reviewProjectsGrid,"ReviewProjectsScroll");
        var projectHead=new StackPanel{Spacing=4,Children={ReviewText("Projects",20),ReviewLabel("Click to add a segment. Repeat a project to add it again.",11)}};
        NewProjectTextBox.Classes.Add("editorField");NewProjectTextBox.Height=34;NewProjectTextBox.MinHeight=0;NewProjectTextBox.Padding=new(9,5);
        NewProjectTextBox.Watermark="New project name";NewProjectTextBox.FontFamily=ReviewBody;NewProjectTextBox.FontSize=12;
        AutomationProperties.SetName(NewProjectTextBox,"New project name");AutoTextDirection.Attach(NewProjectTextBox);
        StyleReviewAction(AddNewProjectButton,56,34);AddNewProjectButton.Content="Add";AddNewProjectButton.Margin=new(0);
        var create=new Grid{ColumnDefinitions=new("*,8,56"),Margin=new(0,12,0,0),Children={NewProjectTextBox,AddNewProjectButton}};Grid.SetColumn(AddNewProjectButton,2);
        var projectLayout=new Grid{RowDefinitions=new("58,*,46"),Children={projectHead,projectScroll,create}};Grid.SetRow(projectScroll,1);Grid.SetRow(create,2);

        var activityTitle=new StackPanel{Spacing=4,Children={ReviewText("Activity breakdown",20),ReviewLabel("Apps, websites & time away",11)}};
        foreach(var b in new[]{DecreaseThresholdButton,IncreaseThresholdButton}){StyleReviewAction(b,25,25);b.Padding=new(0);}
        IncreaseThresholdButton.Content="+";
        AutomationProperties.SetName(DecreaseThresholdButton,"Decrease activity grouping threshold");AutomationProperties.SetName(IncreaseThresholdButton,"Increase activity grouping threshold");
        ThresholdPercentText.FontFamily=ReviewBody;ThresholdPercentText.FontSize=12;ThresholdPercentText.Foreground=ReviewNavy;ThresholdPercentText.VerticalAlignment=VerticalAlignment.Center;
        var filter=new StackPanel{Spacing=5,HorizontalAlignment=HorizontalAlignment.Right,Children={ReviewLabel("GROUP BELOW",9),
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={DecreaseThresholdButton,ThresholdPercentText,IncreaseThresholdButton}}}};
        var activityHead=new Grid{ColumnDefinitions=new("*,Auto"),Children={activityTitle,filter}};Grid.SetColumn(filter,1);
        ActivitySummariesContainer.Spacing=8;
        ActivityWatchStatusCard.Background=ReviewCream;ActivityWatchStatusCard.BorderBrush=ReviewRule;ActivityWatchStatusCard.Padding=new(18);ActivityWatchStatusCard.Margin=new(0);
        ActivityWatchStatusCard.HorizontalAlignment=HorizontalAlignment.Stretch;ActivityWatchStatusCard.VerticalAlignment=VerticalAlignment.Top;
        ActivityWatchStatusCard.CornerRadius=new(6);
        ActivityWatchStatusHeading.FontFamily=ReviewBody;ActivityWatchStatusHeading.FontSize=17;ActivityWatchStatusHeading.Foreground=ReviewNavy;ActivityWatchStatusHeading.TextWrapping=TextWrapping.Wrap;
        ActivityWatchStatusMessage.FontFamily=ReviewBody;ActivityWatchStatusMessage.FontSize=12;ActivityWatchStatusMessage.Foreground=ReviewMuted;
        foreach(var text in new[]{ActivityWatchStatusHeading,ActivityWatchStatusMessage}){text.HorizontalAlignment=HorizontalAlignment.Stretch;text.TextAlignment=TextAlignment.Left;text.Margin=new(0);}
        StyleReviewAction(RetryActivityWatchButton,142,38);RetryActivityWatchButton.Content="Retry connection";RetryActivityWatchButton.HorizontalAlignment=HorizontalAlignment.Left;
        ActivityWatchStatusCard.Child=new StackPanel{Spacing=12,Children={ActivityWatchStatusHeading,ActivityWatchStatusMessage,RetryActivityWatchButton}};
        var activityBody=new StackPanel{Children={ActivitySummariesContainer,ActivityWatchStatusCard}};
        var activityScroll=ReviewScroll(activityBody,"ReviewActivityScroll");
        _reviewActivityNote=ReviewLabel($"Percentages use the full {TotalPeriodMinutes}-minute review period.",10,new(0,7,0,0));
        var activityLayout=new Grid{RowDefinitions=new("62,*,24"),Children={activityHead,activityScroll,_reviewActivityNote}};Grid.SetRow(activityScroll,1);Grid.SetRow(_reviewActivityNote,2);
        Step1Container.Children.Add(ReviewPane(projectLayout));var right=ReviewPane(activityLayout);Step1Container.Children.Add(right);Grid.SetColumn(right,2);

        _reviewSequenceCount=ReviewLabel("YOUR SEQUENCE · NO SEGMENTS",10);
        SelectedSequenceChipsContainer.Margin=new(0);
        SelectedSequenceText.FontFamily=ReviewBody;SelectedSequenceText.Foreground=ReviewMuted;SelectedSequenceText.FontSize=12;SelectedSequenceText.Margin=new(12,0);
        var sequenceBody=new Grid{Children={SelectedSequenceChipsContainer,SelectedSequenceText}};
        _reviewSequenceScroll=new ScrollViewer{Name="ReviewSequenceScroll",Content=sequenceBody,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};
        _reviewSequenceScroll.SizeChanged+=(_,_)=>SizeNavigatorSequence();
        StyleReviewAction(ContinueToStep2Button,156,38,true);ContinueToStep2Button.Content="Continue to time";
        StyleReviewAction(Step1SkipButton,66,38);Step1SkipButton.Content="Skip";
        var actions=new Grid{ColumnDefinitions=new("66,12,*,156"),Children={Step1SkipButton,ReviewLabel("Esc skip · × remove segment",10),ContinueToStep2Button}};Grid.SetColumn(actions.Children[1],2);Grid.SetColumn(ContinueToStep2Button,3);
        var footer=new Grid{RowDefinitions=new("24,56,6,38"),Margin=new(0,5,0,0),Children={new Border{Height=1,Background=ReviewRule,VerticalAlignment=VerticalAlignment.Top},_reviewSequenceCount,_reviewSequenceScroll,actions}};
        Grid.SetRow(_reviewSequenceScroll,1);Grid.SetRow(actions,3);
        var layout=new Grid{RowDefinitions=new("92,*,134"),Children={header,Step1Container,footer}};Grid.SetRow(Step1Container,1);Grid.SetRow(footer,2);
        _navigatorReviewFrame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        // Reparented XAML controls retain dynamic resource bindings; scope their palette to this step.
        _navigatorReviewFrame.Resources["PrimaryTextBrush"]=ReviewNavy;_navigatorReviewFrame.Resources["AccentBrush"]=ReviewNavy;
        _navigatorReviewFrame.Resources["SecondaryTextBrush"]=ReviewMuted;_navigatorReviewFrame.Resources["BorderBrush"]=ReviewRule;
        _navigatorReviewFrame.Resources["SurfaceRaisedBrush"]=ReviewCream;
        _reviewProjectsLayout=layout;
        _reviewAllocationLayout=CreateNavigatorAllocationLayout();
        _reviewMoodLayout=CreateNavigatorMoodLayout();
        Content=new Grid{Children={_legacyReviewContent,new ThemeVariantScope{RequestedThemeVariant=ThemeVariant.Light,Child=_navigatorReviewFrame}}};
        UpdateNavigatorReviewStep();
        PointerPressed+=(_,e)=>{var p=e.GetPosition(this);if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18){BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}};
    }

    private void UpdateNavigatorReviewStep()
    {
        if(_navigatorReviewFrame==null)return;
        _navigatorReviewFrame.IsVisible=true;_legacyReviewContent!.IsVisible=false;
        _navigatorReviewFrame.Child=_currentStep switch {2=>_reviewAllocationLayout,3=>_reviewMoodLayout,_=>_reviewProjectsLayout};
    }

    private void RenderNavigatorProjects()
    {
        _reviewProjectsGrid!.Children.Clear();_reviewProjectsGrid.RowDefinitions.Clear();
        int i=0;
        foreach(var item in _availableProjects.OrderBy(p=>p.IsBreak))
        {
            if(i%2==0){if(i>0)_reviewProjectsGrid.RowDefinitions.Add(new RowDefinition(8,GridUnitType.Pixel));_reviewProjectsGrid.RowDefinitions.Add(new RowDefinition(48,GridUnitType.Pixel));}
            var label=ReviewText(item.IsBreak?"Break / Empty":item.ProjectName,12);label.Foreground=item.IsSelected?ReviewCream:ReviewNavy;label.TextTrimming=TextTrimming.CharacterEllipsis;
            AutoTextDirection.Apply(label,label.Text);
            Control mark=item.IsSelected?ReviewPill(item.SelectionBadgeText??item.SelectionOrder.ToString(),ReviewGold,ReviewNavy):ReviewIcon("M8,2 L8,14 M2,8 L14,8",ReviewMuted,12);
            mark.MaxWidth=72;var row=new Grid{ColumnDefinitions=new("*,8,Auto"),Children={label,mark}};Grid.SetColumn(mark,2);
            var button=ReviewAction("",double.NaN);button.Tag=item;button.Height=48;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Content=row;
            button.Background=item.IsSelected?ReviewNavy:ReviewCream;button.BorderBrush=item.IsSelected?ReviewGold:ReviewRule;
            AutomationProperties.SetName(button,$"Add {item.ProjectName} segment");ToolTip.SetTip(button,item.IsSelected?$"{item.ProjectName} · positions {item.SelectionBadgeText}. Click to add another segment.":item.ProjectName);
            button.Click+=(_,_)=>AddProjectInstance(item.ProjectName,item.IsBreak);
            _reviewProjectsGrid.Children.Add(button);Grid.SetColumn(button,i%2*2);Grid.SetRow(button,i/2*2);i++;
        }
    }

    private void UpdateNavigatorSequenceCount(int count)
    {
        if(_reviewSequenceCount==null)return;
        _reviewSequenceCount.Text=count==0?"YOUR SEQUENCE · NO SEGMENTS":$"YOUR SEQUENCE · {count} SEGMENT{(count==1?"":"S")}";
        if(count==0){SelectedSequenceText.Text="Add your first project above. Include Break / Empty for time away.";SelectedSequenceText.Foreground=ReviewMuted;}
    }

    private ReviewProjectSelectionItem? FindSequenceSource(IReadOnlyList<ReviewProjectSelectionItem> chronological,int index)
    {
        var part=chronological[index];
        bool Same(ReviewProjectSelectionItem item)=>string.Equals(item.ProjectName,part.ProjectName,StringComparison.OrdinalIgnoreCase);
        var selected=_selectedItems.Where(Same).ToList();
        int offset=part.IsFixedTracked?0:chronological.Count(item=>Same(item)&&item.IsFixedTracked);
        int occurrence=chronological.Take(index).Count(item=>Same(item)&&item.IsFixedTracked==part.IsFixedTracked);
        // A single selected project can represent multiple recorded intervals.
        return selected.ElementAtOrDefault(offset+occurrence)??selected.FirstOrDefault();
    }

    private Border CreateNavigatorSequenceChip(ReviewProjectSelectionItem item,ReviewProjectSelectionItem? source)
    {
        var number=ReviewPill(item.SelectionOrder.ToString(),ReviewNavy,ReviewCream);number.VerticalAlignment=VerticalAlignment.Center;
        var label=ReviewText(item.IsBreak?"Break / Empty":item.ProjectName,11);label.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(label,label.Text);
        var words=new StackPanel{Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children={label,ReviewLabel($"{item.CalculatedStartUtc.ToLocalTime():HH:mm}–{item.CalculatedEndUtc.ToLocalTime():HH:mm}",10)}};
        var remove=ReviewAction("",22);remove.Height=24;remove.Padding=new(6);remove.BorderBrush=Brushes.Transparent;remove.Content=ReviewIcon("M2,2 L14,14 M14,2 L2,14",ReviewMuted,10);
        remove.Tag=source;AutomationProperties.SetName(remove,$"Remove {item.ProjectName} segment {item.SelectionOrder}");ToolTip.SetTip(remove,"Remove this project selection");
        remove.Click+=(_,_)=>{if(source!=null)_selectedItems.Remove(source);UpdateStep1SequenceSummary();RenderProjectCards();};
        var row=new Grid{ColumnDefinitions=new("Auto,7,*,22"),Children={number,words,remove}};Grid.SetColumn(words,2);Grid.SetColumn(remove,3);
        var chip=new Border{Width=SequenceChipWidth(),Height=50,Margin=new(0,0,8,0),Padding=new(7),Background=ReviewCream,BorderBrush=ReviewRule,BorderThickness=new(1),CornerRadius=new(5),Child=row};
        ToolTip.SetTip(chip,$"{item.PartDisplayName} · {item.CalculatedStartUtc.ToLocalTime():g}–{item.CalculatedEndUtc.ToLocalTime():g}");return chip;
    }
    private double SequenceChipWidth()=>Math.Max(180,Math.Floor(((_reviewSequenceScroll?.Bounds.Width??944)-36)/4));
    private void SizeNavigatorSequence(){foreach(var chip in SelectedSequenceChipsContainer.Children.OfType<Border>())chip.Width=SequenceChipWidth();}

    private void ShowNavigatorActivityStatus(string heading,string message,bool retry)
    {
        ActivitySummariesContainer.Children.Clear();ActivityWatchStatusCard.IsVisible=true;ActivityWatchStatusHeading.Text=heading;ActivityWatchStatusMessage.Text=message;
        RetryActivityWatchButton.IsVisible=retry;DecreaseThresholdButton.IsEnabled=false;IncreaseThresholdButton.IsEnabled=false;
        _reviewActivityNote!.Text="Manual review is still available.";
    }
    private void RenderNavigatorActivities()
    {
        if(_model==null){RenderActivityLoadingState();return;}
        var raw=_model.RawActivitySummaries.Count>0?_model.RawActivitySummaries:_model.ActivitySummaries;
        var groups=PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(raw,TotalPeriodDuration,_thresholdPercent);
        if(groups.Count==0)
        {
            ShowNavigatorActivityStatus(_model.ActivityWatchAvailable?"No activity recorded":"ActivityWatch is unavailable",
                _model.ActivityWatchAvailable?"There are no activity details for this period. You can still choose projects and continue.":"Activity details could not be loaded for this period. You can still choose projects and continue.",!_model.ActivityWatchAvailable);return;
        }
        ActivityWatchStatusCard.IsVisible=false;ActivitySummariesContainer.Children.Clear();DecreaseThresholdButton.IsEnabled=true;IncreaseThresholdButton.IsEnabled=true;
        _reviewActivityNote!.Text=$"Percentages use the full {TotalPeriodMinutes}-minute review period.";
        foreach(var group in groups)ActivitySummariesContainer.Children.Add(CreateNavigatorActivity(group));
    }
    private Control CreateNavigatorActivity(ActivitySummaryGroup group)
    {
        bool expandable=group.IsOther&&group.SubItems.Count>0;
        var title=ReviewText(group.IsOther?"Other activities":group.Title,13);title.TextTrimming=TextTrimming.CharacterEllipsis;
        var amount=ReviewText($"{ReviewDuration(group.TotalDuration)}   ·   {group.PercentageDisplay}",12);
        var top=new Grid{ColumnDefinitions=new(expandable?"*,8,Auto,20":"*,8,Auto"),Children={title,amount}};Grid.SetColumn(amount,2);
        if(expandable){var chevron=ReviewIcon(_isOthersExpanded?"M3,5 L8,10 L13,5":"M5,3 L10,8 L5,13",ReviewMuted,9);top.Children.Add(chevron);Grid.SetColumn(chevron,3);}
        string category=group.IsOther?$"{group.SubItems.Count} activities · each below {_thresholdPercent:0.#}%":group.IsIdle?"Idle":group.Category=="Web"?"Web":"Application";
        IBrush ink=group.IsOther?Brush.Parse("#7D6A48"):group.IsIdle?Brush.Parse("#A46621"):group.Category=="Web"?Brush.Parse("#2E756D"):ReviewNavy;
        var bar=new Grid{Height=4,Background=Brush.Parse("#249A7B48")};bar.ColumnDefinitions.Add(new ColumnDefinition(Math.Clamp(group.Percentage,0,100),GridUnitType.Star));bar.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(0,100-group.Percentage),GridUnitType.Star));bar.Children.Add(new Border{Background=ink,CornerRadius=new(2)});
        var summary=new StackPanel{Spacing=5,Children={top,ReviewLabel(category,10),bar}};
        Control header;
        if(expandable)
        {
            var toggle=new Button{Content=summary,Padding=new(11,8),MinHeight=0,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent,BorderThickness=new(0),Foreground=ReviewNavy};
            AutomationProperties.SetName(toggle,_isOthersExpanded?"Collapse other activities":"Expand other activities");
            toggle.Click+=(_,_)=>{_isOthersExpanded=!_isOthersExpanded;RenderActivitySummaries();};header=toggle;
        }
        else header=new Border{Padding=new(11,8),Child=summary};
        var stack=new StackPanel{Children={header}};
        if(expandable&&_isOthersExpanded)
        {
            var details=new StackPanel{Spacing=9,Margin=new(11,0,11,10)};
            foreach(var sub in group.SubItems)
            {
                var name=ReviewLabel(sub.Title,11);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;
                var time=ReviewLabel($"{ReviewDuration(sub.TotalDuration)} · {sub.PercentageDisplay}",10);
                var row=new Grid{ColumnDefinitions=new("*,10,Auto"),Children={name,time}};Grid.SetColumn(time,2);ToolTip.SetTip(row,$"{sub.App} — {sub.Title}");details.Children.Add(row);
            }
            stack.Children.Add(details);
        }
        var card=new Border{Background=ReviewCream,CornerRadius=new(5),Child=stack};ToolTip.SetTip(card,$"{group.App} — {group.Title}");return card;
    }

    private static void DetachReviewControl(Control control)
    {switch(control.Parent){case Panel panel:panel.Children.Remove(control);break;case Decorator decorator:decorator.Child=null;break;case ContentControl content:content.Content=null;break;}}
    private static string ReviewDuration(TimeSpan duration)
    {
        long seconds=Math.Max(0,(long)Math.Round(duration.TotalSeconds));
        string text=seconds>=3600?$"{seconds/3600}h {seconds%3600/60}m":seconds>=60?$"{seconds/60}m":"";
        return seconds%60>0||text.Length==0?$"{text}{(text.Length==0?"":" ")}{seconds%60}s":text;
    }
    private static TextBlock ReviewLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=ReviewBody,FontSize=size,Foreground=ReviewMuted,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
    private static TextBlock ReviewText(string text,double size)=>new(){Text=text,FontFamily=ReviewBody,FontSize=size,FontWeight=FontWeight.SemiBold,Foreground=ReviewNavy,VerticalAlignment=VerticalAlignment.Center};
    private static Border ReviewPill(string text,IBrush background,IBrush foreground)=>new(){Background=background,CornerRadius=new(4),Padding=new(9,5),Child=new TextBlock{Text=text,FontFamily=ReviewBody,FontSize=10,Foreground=foreground,TextTrimming=TextTrimming.CharacterEllipsis}};
    private static Border ReviewPane(Control content)=>new(){Padding=new(14),Background=Brush.Parse("#30FFF6E4"),BorderBrush=ReviewRule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static ScrollViewer ReviewScroll(Control content,string name)=>new(){Name=name,Content=content,Margin=new(0,0,5,0),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private static Button ReviewAction(string text,double width){var button=new Button{Content=text};StyleReviewAction(button,width,38);return button;}
    private static void StyleReviewAction(Button button,double width,double height,bool primary=false){NavigatorFormStyles.StyleButton(button,primary);button.Width=width;button.Height=height;button.Padding=new(10,0);button.FontFamily=ReviewBody;button.FontSize=12;}
    private static Avalonia.Controls.Shapes.Path ReviewIcon(string data,IBrush ink,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(data),Stroke=ink,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
}
