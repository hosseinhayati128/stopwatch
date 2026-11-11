using System;
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
    private TextBlock? _reviewMoodProjectTime,_reviewMoodProjectNames,_reviewMoodRemainingTime,_reviewMoodSelectedCount;
    private Avalonia.Controls.Shapes.Ellipse? _reviewMoodFaceOutline;
    private Avalonia.Controls.Shapes.Path? _reviewMoodFaceExpression;
    private static readonly IBrush ReviewMoodLow=Brush.Parse("#934B3F");

    private Grid CreateNavigatorMoodLayout()
    {
        foreach(var control in new Control[]{Step3Container,MoodScoreBigText,MoodDescriptorText,MoodSubtext,MoodSlider,DecreaseMoodButton,IncreaseMoodButton,
            MoodPresetsContainer,FeelingsKeywordsContainer,NewKeywordTextBox,AddNewKeywordButton,MoodNoteTextBox,Step3BackButton,Step3SkipButton,Step3SaveButton})DetachReviewControl(control);
        var type=StopwatchTypography.Resolve("navigator-bold");
        var heading=new StackPanel{Spacing=5,Children={new TextBlock{Text="Activity & Time Review",FontFamily=type.NameFont,FontWeight=type.NameWeight,FontSize=30,Foreground=ReviewNavy},
            ReviewLabel("How did you feel during this review?",12),
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=16,Margin=new(0,6,0,0),Children={ReviewLabel("01  Projects & activity",11),ReviewLabel("02  Allocate time",11),ReviewPill("03  Feelings & mood",ReviewNavy,ReviewCream)}}}};
        heading.PointerPressed+=OnHeaderPointerPressed;
        var start=_startUtc.ToLocalTime();var end=_endUtc.ToLocalTime();
        string dates=start.Date==DateTime.Today&&end.Date==start.Date?"TODAY":start.Date==end.Date?start.ToString("dd MMM"):$"{start:dd MMM} – {end:dd MMM}";
        var period=new StackPanel{Spacing=4,Margin=new(0,8,16,0),Children={ReviewLabel($"{dates} · {start:HH:mm}–{end:HH:mm}",10),ReviewText($"{TotalPeriodMinutes} minutes",18)}};
        var close=ReviewAction("",28);close.Name="ReviewMoodCloseButton";close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new(0,7,0,0);
        close.Content=ReviewIcon("M2,2 L14,14 M14,2 L2,14",ReviewNavy,16);AutomationProperties.SetName(close,"Close review without saving");close.Click+=SkipReportButton_Click;
        var header=new Grid{ColumnDefinitions=new("*,Auto,28"),Children={heading,period,close}};Grid.SetColumn(period,1);Grid.SetColumn(close,2);

        _reviewMoodProjectTime=ReviewText("",13);_reviewMoodProjectTime.Name="ReviewMoodProjectTime";
        _reviewMoodProjectNames=ReviewLabel("",11);_reviewMoodProjectNames.Name="ReviewMoodProjectNames";_reviewMoodProjectNames.TextWrapping=TextWrapping.NoWrap;_reviewMoodProjectNames.TextTrimming=TextTrimming.CharacterEllipsis;
        _reviewMoodRemainingTime=ReviewLabel("",11);_reviewMoodRemainingTime.Name="ReviewMoodRemainingTime";
        var summaryWords=new StackPanel{Spacing=4,Children={_reviewMoodProjectTime,_reviewMoodProjectNames}};
        var summaryRest=new StackPanel{Spacing=4,Children={_reviewMoodRemainingTime,ReviewLabel("Breaks and gaps stay out of project history.",10)}};
        var summaryRow=new Grid{ColumnDefinitions=new("*,12,Auto"),Children={summaryWords,summaryRest}};Grid.SetColumn(summaryRest,2);
        var summary=new Border{Padding=new(12,8),Background=Brush.Parse("#42FFF6E4"),BorderBrush=ReviewRule,BorderThickness=new(1),CornerRadius=new(7),Margin=new(0,6,0,4),Child=summaryRow};

        var moodTitle=new StackPanel{Spacing=4,Children={ReviewText("How was your mood?",20),ReviewLabel("Choose a score from 1.0 to 10.0.",11)}};
        MoodScoreBigText.FontFamily=ReviewBody;MoodScoreBigText.FontSize=52;MoodScoreBigText.FontWeight=FontWeight.SemiBold;MoodScoreBigText.Margin=new(0);
        var scoreValue=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,Children={MoodScoreBigText,new TextBlock{Text="/10",FontFamily=ReviewBody,FontSize=14,Foreground=ReviewMuted,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,0,12)}}};
        _reviewMoodFaceOutline=new(){Stroke=ReviewOchre,StrokeThickness=1.6,Margin=new(2)};
        _reviewMoodFaceExpression=new(){Stroke=ReviewOchre,StrokeThickness=1.8,StrokeLineCap=PenLineCap.Round};
        var face=new Grid{Width=48,Height=48,VerticalAlignment=VerticalAlignment.Center,Children={_reviewMoodFaceOutline,_reviewMoodFaceExpression}};
        var scoreRow=new Grid{ColumnDefinitions=new("*,48"),Children={scoreValue,face}};Grid.SetColumn(face,1);
        MoodDescriptorText.FontFamily=ReviewBody;MoodDescriptorText.FontSize=16;MoodDescriptorText.Margin=new(0);MoodDescriptorText.TextWrapping=TextWrapping.Wrap;
        MoodSubtext.FontFamily=ReviewBody;MoodSubtext.FontSize=11;MoodSubtext.Foreground=ReviewMuted;MoodSubtext.Margin=new(0);MoodSubtext.TextWrapping=TextWrapping.Wrap;
        var scoreCard=new Border{Padding=new(16,10),Background=ReviewCream,BorderBrush=ReviewRule,BorderThickness=new(1),CornerRadius=new(6),Child=new StackPanel{Spacing=4,Children={scoreRow,MoodDescriptorText,MoodSubtext}}};
        foreach(var button in new[]{DecreaseMoodButton,IncreaseMoodButton}){StyleReviewAction(button,48,30);button.Padding=new(0);}
        DecreaseMoodButton.Content="−0.1";IncreaseMoodButton.Content="+0.1";
        AutomationProperties.SetName(DecreaseMoodButton,"Decrease mood score by 0.1");AutomationProperties.SetName(IncreaseMoodButton,"Increase mood score by 0.1");
        MoodSlider.Height=40;MoodSlider.MinHeight=0;MoodSlider.Margin=new(8,0);AutomationProperties.SetName(MoodSlider,"Mood score from 1.0 to 10.0");
        var controls=new Grid{ColumnDefinitions=new("48,*,48"),Children={DecreaseMoodButton,MoodSlider,IncreaseMoodButton}};Grid.SetColumn(MoodSlider,1);Grid.SetColumn(IncreaseMoodButton,2);
        var ticks=new Grid{Margin=new(56,0),Children={ReviewLabel("1.0 · Low",9),ReviewLabel("5.0",9),ReviewLabel("10.0 · Thrilled",9)}};ticks.Children[1].HorizontalAlignment=HorizontalAlignment.Center;ticks.Children[2].HorizontalAlignment=HorizontalAlignment.Right;
        MoodPresetsContainer.Margin=new(0);MoodPresetsContainer.SizeChanged+=(_,_)=>UpdateNavigatorMoodPresets();
        var moodContent=new StackPanel{Spacing=9,Children={scoreCard,controls,ticks,ReviewLabel("QUICK SCORES",9),MoodPresetsContainer,ReviewLabel("The score describes this review period. Feelings and a note are optional.",11,new(0,5,0,0))}};
        var moodScroll=ReviewScroll(moodContent,"ReviewMoodScroll");var moodLayout=new Grid{RowDefinitions=new("54,*"),Children={moodTitle,moodScroll}};Grid.SetRow(moodScroll,1);

        _reviewMoodSelectedCount=ReviewLabel("0 selected",10);_reviewMoodSelectedCount.Name="ReviewMoodSelectedCount";
        var count=new Border{Padding=new(8,4),CornerRadius=new(4),Background=Brush.Parse("#24AF7C29"),Child=_reviewMoodSelectedCount,VerticalAlignment=VerticalAlignment.Top};
        var feelingTitle=new Grid{ColumnDefinitions=new("*,8,Auto"),Children={new StackPanel{Spacing=4,Children={ReviewText("Feelings & context",20),ReviewLabel("Choose any that fit. You can select more than one.",11)}},count}};Grid.SetColumn(count,2);
        FeelingsKeywordsContainer.Margin=new(0);FeelingsKeywordsContainer.SizeChanged+=(_,_)=>SizeNavigatorMoodKeywords();
        StyleAllocationField(NewKeywordTextBox,"Add your own feeling",34);NewKeywordTextBox.Padding=new(9,6);NewKeywordTextBox.Watermark="Add your own feeling";AutoTextDirection.Attach(NewKeywordTextBox);
        StyleReviewAction(AddNewKeywordButton,56,34);AddNewKeywordButton.Content="Add";AddNewKeywordButton.Margin=new(0);
        var addRow=new Grid{ColumnDefinitions=new("*,8,56"),Margin=new(0,2,0,0),Children={NewKeywordTextBox,AddNewKeywordButton}};Grid.SetColumn(AddNewKeywordButton,2);
        StyleAllocationField(MoodNoteTextBox,"Optional reflection",64);MoodNoteTextBox.Padding=new(9,6);MoodNoteTextBox.Watermark="What influenced how you felt?";MoodNoteTextBox.AcceptsReturn=true;MoodNoteTextBox.TextWrapping=TextWrapping.Wrap;MoodNoteTextBox.VerticalContentAlignment=VerticalAlignment.Top;AutoTextDirection.Attach(MoodNoteTextBox);
        var noteHeading=new Grid{ColumnDefinitions=new("*,Auto"),Children={ReviewText("Reflection",13),ReviewLabel("Optional",10)}};Grid.SetColumn(noteHeading.Children[1],1);
        var feelingsContent=new StackPanel{Spacing=6,Children={FeelingsKeywordsContainer,addRow,ReviewLabel("Enter adds a feeling.",10),new Border{Height=1,Background=ReviewRule,Margin=new(0,5,0,4)},noteHeading,MoodNoteTextBox,ReviewLabel("Enter adds a new line in your note.",10)}};
        var feelingsScroll=ReviewScroll(feelingsContent,"ReviewFeelingsScroll");var feelingLayout=new Grid{RowDefinitions=new("54,*"),Children={feelingTitle,feelingsScroll}};Grid.SetRow(feelingsScroll,1);
        var left=ReviewPane(moodLayout);var right=ReviewPane(feelingLayout);left.Padding=right.Padding=new(13);
        Step3Container.Children.Clear();Step3Container.ColumnDefinitions=new(".85*,14,1.15*");Step3Container.Margin=new(0,8,0,8);
        Step3Container.Children.Add(left);Step3Container.Children.Add(right);Grid.SetColumn(right,2);

        StyleReviewAction(Step3BackButton,82,38);Step3BackButton.Content="Back";
        StyleReviewAction(Step3SkipButton,128,38);Step3SkipButton.Content="Save time only";
        StyleReviewAction(Step3SaveButton,176,38,true);Step3SaveButton.Content="Save & complete";
        var actions=new Grid{ColumnDefinitions=new("82,12,*,128,8,176"),Children={Step3BackButton,ReviewLabel("Esc back · Enter save",10),Step3SkipButton,Step3SaveButton}};Grid.SetColumn(actions.Children[1],2);Grid.SetColumn(Step3SkipButton,3);Grid.SetColumn(Step3SaveButton,5);
        var footer=new Grid{RowDefinitions=new("18,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=ReviewRule,VerticalAlignment=VerticalAlignment.Top},actions}};Grid.SetRow(actions,1);
        var layout=new Grid{RowDefinitions=new("84,64,*,66"),Children={header,summary,Step3Container,footer}};Grid.SetRow(summary,1);Grid.SetRow(Step3Container,2);Grid.SetRow(footer,3);return layout;
    }

    private void UpdateNavigatorMoodScore(string descriptor)
    {
        var ink=_currentMoodScore<3?ReviewMoodLow:_currentMoodScore<7?ReviewOchre:ReviewTeal;
        // Keep the original XAML accent bindings consistent when this page is reattached.
        MoodScoreBigText.Resources["AccentBrush"]=MoodDescriptorText.Resources["AccentBrush"]=ink;
        MoodScoreBigText.Foreground=MoodDescriptorText.Foreground=ink;MoodDescriptorText.Text=descriptor;
        _reviewMoodFaceOutline!.Stroke=_reviewMoodFaceExpression!.Stroke=ink;
        _reviewMoodFaceExpression.Data=Geometry.Parse("M16,18 L16,21 M32,18 L32,21 "+(_currentMoodScore<5?"M15,34 Q24,25 33,34":_currentMoodScore>=7?"M15,28 Q24,39 33,28":"M16,31 L32,31"));
        DecreaseMoodButton.IsEnabled=_currentMoodScore>1;IncreaseMoodButton.IsEnabled=_currentMoodScore<10;UpdateNavigatorMoodPresets();
    }

    private void UpdateNavigatorMoodPresets()
    {
        var buttons=MoodPresetsContainer.Children.OfType<Button>().ToArray();
        for(int i=0;i<buttons.Length;i++)
        {
            var button=buttons[i];button.Classes.Set("editorPrimary",button.Tag is double value&&Math.Abs(value-_currentMoodScore)<.01);
            button.Margin=new(0,0,i==buttons.Length-1?0:6,0);
            // Leave rounding room so the last preset never creates a second wrap row.
            if(MoodPresetsContainer.Bounds.Width>0)button.Width=Math.Max(26,Math.Floor((MoodPresetsContainer.Bounds.Width-30)/6));
            AutomationProperties.SetName(button,$"Set mood score to {button.Content}");
        }
    }

    private void RenderNavigatorMoodKeywords()
    {
        // Keep existing buttons attached when toggling so keyboard focus stays on the selected feeling.
        if(!FeelingsKeywordsContainer.Children.OfType<Button>().Select(b=>b.Tag as string).SequenceEqual(_allKeywords))
        {
            FeelingsKeywordsContainer.Children.Clear();
            foreach(var keyword in _allKeywords)
            {
                var button=ReviewAction("",double.NaN);button.Tag=keyword;button.Height=31;button.Padding=new(10,0);button.Margin=new(0,0,7,7);
                button.Click+=(_,_)=>ToggleKeyword(keyword);FeelingsKeywordsContainer.Children.Add(button);
            }
        }
        foreach(var button in FeelingsKeywordsContainer.Children.OfType<Button>())
        {
            var keyword=(string)button.Tag!;bool selected=_selectedKeywords.Contains(keyword);button.Classes.Set("editorPrimary",selected);
            var label=ReviewLabel(keyword,12);label.Foreground=selected?ReviewCream:ReviewNavy;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(label,keyword);
            var content=new Grid{ColumnDefinitions=new(selected?"11,6,*":"*"),Children={label}};
            if(selected){Grid.SetColumn(label,2);content.Children.Add(ReviewIcon("M2,7 L6,11 L13,3",ReviewCream,11));}
            button.Content=content;button.ClearValue(BackgroundProperty);button.BorderBrush=selected?ReviewGold:ReviewRule;
            if(!selected)button.Background=ReviewCream;
            ToolTip.SetTip(button,keyword);AutomationProperties.SetName(button,$"{keyword}, {(selected?"selected":"not selected")}");
        }
        _reviewMoodSelectedCount!.Text=$"{_selectedKeywords.Count} selected";SizeNavigatorMoodKeywords();
    }

    private void SizeNavigatorMoodKeywords()
    {
        if(FeelingsKeywordsContainer.Bounds.Width<=0)return;
        foreach(var button in FeelingsKeywordsContainer.Children.OfType<Button>())
        {
            button.MaxWidth=Math.Max(60,FeelingsKeywordsContainer.Bounds.Width-7);
            if(button.Content is Grid content)content.Children.OfType<TextBlock>().Single().MaxWidth=Math.Max(20,button.MaxWidth-40);
        }
    }

    private void UpdateNavigatorMoodSummary()
    {
        var timeline=PeriodicReviewDataAggregator.BuildFullTimeline(_startUtc,_endUtc,_selectedItems);
        var projects=timeline.Where(s=>!s.IsBreak&&!s.IsUnallocated&&s.Item!=null&&s.DurationMinutes>0).ToArray();
        var names=projects.Select(s=>s.Item!.ProjectName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _reviewMoodProjectTime!.Text=$"{projects.Sum(s=>s.DurationMinutes):0.#} min of project time · {names.Length} {(names.Length==1?"project":"projects")}";
        _reviewMoodProjectNames!.Text=names.Length==0?"No project time allocated":string.Join(" · ",names);ToolTip.SetTip(_reviewMoodProjectNames,_reviewMoodProjectNames.Text);AutoTextDirection.Apply(_reviewMoodProjectNames,_reviewMoodProjectNames.Text);
        _reviewMoodRemainingTime!.Text=$"{timeline.Where(s=>s.IsBreak).Sum(s=>s.DurationMinutes):0.#} min break · {timeline.Where(s=>s.IsUnallocated).Sum(s=>s.DurationMinutes):0.#} min unallocated";
    }
}
