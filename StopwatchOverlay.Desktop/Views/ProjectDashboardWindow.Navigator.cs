using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectDashboardWindow
{
    private TextBlock? _overviewHint;
    private TextBlock? _trackedProjectHint;
    private bool? _compactOverview;
    private static readonly IBrush DashboardRule=Brush.Parse("#509A7B48"),DashboardPaper=Brush.Parse("#EBD3A5"),DashboardTeal=Brush.Parse("#2D7169");

    private void ApplyNavigatorLayout()
    {
        if(AppThemeCatalog.Normalize(_settings?.ThemeMode)!=AppThemeCatalog.Nika)return;
        FontFamily=ClockPickerPresentation.Body;Foreground=ClockPickerPresentation.Navy;FontSize=12;
        Width=1080;Height=620;SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;
        TransparencyLevelHint=[WindowTransparencyLevel.Transparent];RequestedThemeVariant=ThemeVariant.Light;
        NavigatorFormStyles.Apply(this);

        var legacyBody=(Grid)DashboardScrollViewer.Content!;
        var lowerSections=legacyBody.Children.Where(c=>Grid.GetRow(c)>=2).ToArray();
        foreach(var section in lowerSections)legacyBody.Children.Remove(section);
        Content=null;DashboardScrollViewer.Content=null;
        var records=HeaderActionsPanel.Children.OfType<Button>().Single(b=>Equals(b.Content,"Records"));
        var export=HeaderActionsPanel.Children.OfType<Button>().Single(b=>Equals(b.Content,"Export"));
        var refresh=HeaderActionsPanel.Children.OfType<Button>().Single(b=>Equals(b.Content,"Refresh"));
        foreach(var control in new Control[]{DashboardScrollViewer,records,export,refresh,ProjectFilterSelector,ProjectScorePanel,DeleteProjectButton,
            PreviousDayButton,DayButton,NextDayButton,SevenDaysButton,ThirtyDaysButton,AllTimeButton,RangeHeadingText,UpdatedText,
            TotalTrackedText,TotalScoreText,SessionCountText,ProjectCountText,ActiveCountText,AddRecordButton})
            if(control.Parent is Panel parent)parent.Children.Remove(control);

        var typography=StopwatchTypography.Resolve("navigator-bold");
        var heading=new StackPanel{Spacing=5,Background=Brushes.Transparent,Children={new TextBlock{Text="Project Time",FontFamily=typography.NameFont,FontWeight=typography.NameWeight,FontSize=30,Foreground=ClockPickerPresentation.Navy},DashboardLabel("Your time, projects and progress.",12)}};
        heading.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        Action(records,82,"DashboardRecordsButton");Action(export,76,"DashboardExportButton");Action(refresh,34,"DashboardRefreshButton");
        refresh.Content=DashboardIcon("M13,5 A6,6 0 1 0 14,10 M13,1 L13,5 L9,5",16);AutomationProperties.SetName(refresh,"Refresh dashboard");ToolTip.SetTip(refresh,"Refresh dashboard");
        var close=new Button{Content=DashboardIcon("M2,2 L14,14 M14,2 L2,14",16)};Action(close,28,"DashboardCloseButton");close.Height=28;close.Padding=new(6);close.BorderBrush=Brushes.Transparent;
        close.Click+=(_,_)=>Close();AutomationProperties.SetName(close,"Close dashboard");ToolTip.SetTip(close,"Close dashboard (Esc)");
        var headerActions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0),Children={records,export,refresh,new Border{Width=10},close}};
        var header=new Grid{ColumnDefinitions=new("*,16,Auto"),Children={heading,headerActions}};Grid.SetColumn(headerActions,2);

        ProjectFilterSelector.Width=double.NaN;ProjectFilterSelector.Height=36;ProjectFilterSelector.MinWidth=0;ProjectFilterSelector.MinHeight=0;ProjectFilterSelector.Margin=default;
        ProjectFilterSelector.HorizontalAlignment=HorizontalAlignment.Stretch;ProjectFilterSelector.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        ProjectFilterSelector.VerticalContentAlignment=VerticalAlignment.Center;ProjectFilterSelector.Padding=new(10,5);ProjectFilterSelector.FontSize=13;ProjectFilterSelector.Classes.Add("editorField");
        AutomationProperties.SetName(ProjectFilterSelector,"Project filter");
        ProjectFilterSelector.ItemTemplate=new FuncDataTemplate<ProjectFilterOption>((option,_)=>
        {
            var text=DashboardLabel(option?.Name??"",13,ClockPickerPresentation.Navy);text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;AutoTextDirection.Apply(text,option?.Name);return text;
        });
        var projectField=new StackPanel{Spacing=6,Children={DashboardLabel("PROJECT",10),ProjectFilterSelector}};
        var ranges=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
        foreach(var button in new[]{DayButton,SevenDaysButton,ThirtyDaysButton,AllTimeButton}){Action(button,button==DayButton?62:80);button.Height=36;ranges.Children.Add(button);}
        var rangeField=new StackPanel{Spacing=6,Children={DashboardLabel("PERIOD",10),ranges}};
        var filterFields=new Grid{ColumnDefinitions=new("*,24,338"),Children={projectField,rangeField}};Grid.SetColumn(rangeField,2);
        foreach(var button in new[]{PreviousDayButton,NextDayButton}){Action(button,28);button.Height=28;button.Padding=new(6);button.Margin=default;AutomationProperties.SetName(button,button==PreviousDayButton?"Previous day":"Next day");}
        PreviousDayButton.Content=DashboardIcon("M10,3 L5,8 L10,13",12);NextDayButton.Content=DashboardIcon("M5,3 L10,8 L5,13",12);
        var navigation=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={PreviousDayButton,NextDayButton}};
        RangeHeadingText.FontSize=12;RangeHeadingText.Foreground=ClockPickerPresentation.Navy;RangeHeadingText.VerticalAlignment=VerticalAlignment.Center;RangeHeadingText.TextWrapping=TextWrapping.Wrap;
        var dates=new Grid{ColumnDefinitions=new("*,12,Auto"),Children={RangeHeadingText,navigation}};Grid.SetColumn(navigation,2);
        var filters=DashboardPane(new StackPanel{Spacing=10,Children={filterFields,dates}},new(12));

        ProjectScorePanel.Children.Clear();ProjectScorePanel.Spacing=8;ProjectScorePanel.Children.Add(DashboardLabel("Project rate",11));ProjectScorePanel.Children.Add(ProjectScoreBox);ProjectScorePanel.Children.Add(DashboardLabel("pts / hour",11));
        ProjectScoreBox.Width=136;ProjectScoreBox.Height=32;ProjectScoreBox.MinHeight=0;ProjectScoreBox.FontSize=12;ProjectScoreBox.Classes.Add("editorField");AutomationProperties.SetName(ProjectScoreBox,"Project points per hour");
        Action(DeleteProjectButton,116);DeleteProjectButton.Content="Delete project";DeleteProjectButton.Height=32;DeleteProjectButton.Classes.Add("editorDanger");
        _overviewHint=DashboardLabel("",11);
        var context=new Grid{ColumnDefinitions=new("*,Auto"),Children={_overviewHint,ProjectScorePanel,DeleteProjectButton}};Grid.SetColumn(DeleteProjectButton,1);
        var contextRow=new Border{Height=40,Padding=new(2),Child=context};

        TotalTrackedText.FontFamily=ClockPickerPresentation.Body;TotalTrackedText.FontSize=48;TotalTrackedText.Foreground=ClockPickerPresentation.Cream;TotalTrackedText.Margin=default;
        var timeLabel=DashboardLabel("TRACKED TIME",11,ClockPickerPresentation.Cream);timeLabel.Opacity=.8;
        var clock=DashboardIcon("M8,1 A7,7 0 1 1 7.99,1 M8,4 L8,8 L11,10",26);clock.Stroke=ClockPickerPresentation.Cream;clock.Opacity=.75;
        var totalHeading=new Grid{ColumnDefinitions=new("*,26"),Children={timeLabel,clock}};Grid.SetColumn(clock,1);
        var totalValue=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Child=TotalTrackedText};
        _trackedProjectHint=DashboardLabel("",12,ClockPickerPresentation.Cream);_trackedProjectHint.Opacity=.85;
        var timeNote=DashboardLabel("Simultaneous timers are added.",10,ClockPickerPresentation.Cream);timeNote.Opacity=.75;
        var totalLayout=new Grid{RowDefinitions=new("30,*,Auto,26"),Children={totalHeading,totalValue,_trackedProjectHint,timeNote}};Grid.SetRow(totalValue,1);Grid.SetRow(_trackedProjectHint,2);Grid.SetRow(timeNote,3);
        var totalCard=new Border{Padding=new(20),CornerRadius=new(7),Background=ClockPickerPresentation.Navy,BorderBrush=ClockPickerPresentation.Gold,BorderThickness=new(1),Child=totalLayout};
        totalCard.Resources["PrimaryTextBrush"]=ClockPickerPresentation.Cream;
        var stats=new Grid{Name="DashboardStats",ColumnDefinitions=new("*,12,*"),RowDefinitions=new("*,12,*")};
        var scoreCard=Metric("VALUE SCORE",TotalScoreText,"Weighted by project rates",Brush.Parse("#8B5C24"));
        var sessionCard=Metric("SESSIONS",SessionCountText,"Within the selected period",ClockPickerPresentation.Navy);
        var projectCard=Metric("PROJECTS",ProjectCountText,"With tracked time",ClockPickerPresentation.Navy);
        var activeCard=Metric("ACTIVE NOW",ActiveCountText,"Across all projects, now",DashboardTeal);
        stats.Children.Add(scoreCard);stats.Children.Add(sessionCard);stats.Children.Add(projectCard);stats.Children.Add(activeCard);
        Grid.SetColumn(sessionCard,2);Grid.SetRow(projectCard,2);Grid.SetRow(activeCard,2);Grid.SetColumn(activeCard,2);
        var summary=new Grid{Name="DashboardSummary",Height=224,ColumnDefinitions=new(".85*,14,1.15*"),Children={totalCard,stats}};Grid.SetColumn(stats,2);
        var overview=new StackPanel{Spacing=12,Children={filters,contextRow,summary}};
        // Reparented XAML labels retain dynamic resource bindings; scope their ink to this overview.
        overview.Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;overview.Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        var body=new StackPanel{Spacing=18,Margin=new(0,0,6,0),Children={overview}};
        foreach(var section in lowerSections)body.Children.Add(section);
        DashboardScrollViewer.Content=body;

        Action(AddRecordButton,140);AddRecordButton.Classes.Add("editorPrimary");AddRecordButton.Content="+ Add record";AddRecordButton.Margin=default;AddRecordButton.VerticalAlignment=VerticalAlignment.Center;
        UpdatedText.FontSize=10;UpdatedText.Foreground=ClockPickerPresentation.Muted;UpdatedText.Margin=default;UpdatedText.VerticalAlignment=VerticalAlignment.Center;UpdatedText.TextWrapping=TextWrapping.Wrap;
        var footerActions=new Grid{ColumnDefinitions=new("*,16,140"),Children={UpdatedText,AddRecordButton}};Grid.SetColumn(UpdatedText,0);Grid.SetColumn(AddRecordButton,2);
        footerActions.Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        var footer=new Grid{RowDefinitions=new("16,38"),Margin=new(0,10,0,0),Children={new Border{Height=1,Background=DashboardRule,VerticalAlignment=VerticalAlignment.Top},footerActions}};Grid.SetRow(footerActions,1);
        var layout=new Grid{RowDefinitions=new("76,*,64"),Children={header,DashboardScrollViewer,footer}};Grid.SetRow(DashboardScrollViewer,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=DashboardPaper,Child=layout};
        void ResizeOverview()
        {
            bool compact=ClientSize.Width<960;if(_compactOverview==compact)return;_compactOverview=compact;
            filterFields.ColumnDefinitions=new(compact?"*":"*,24,338");filterFields.RowDefinitions=new(compact?"Auto,12,Auto":"Auto");Grid.SetColumn(rangeField,compact?0:2);Grid.SetRow(rangeField,compact?2:0);
            summary.ColumnDefinitions=new(compact?"*":".85*,14,1.15*");summary.RowDefinitions=new(compact?"180,12,224":"*");summary.Height=compact?416:224;Grid.SetColumn(stats,compact?0:2);Grid.SetRow(stats,compact?2:0);
        }
        SizeChanged+=(_,_)=>ResizeOverview();ResizeOverview();
        Opened+=(_,_)=>
        {
            var screen=Screens.ScreenFromWindow(this);if(screen==null)return;
            Height=Math.Min(Height,screen.WorkingArea.Height/RenderScaling-16);
        };
        PointerPressed+=(_,e)=>{var p=e.GetPosition(this);if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18){BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}};
    }

    private void UpdateNavigatorSummary(int projectCount,bool empty)
    {
        if(_overviewHint==null)return;
        _overviewHint.IsVisible=!ProjectScorePanel.IsVisible;
        _overviewHint.Text=empty?"No tracked time in this period. Add a record or choose another date.":"Each project's score per hour weights the value score below.";
        _trackedProjectHint!.Text=empty?"No tracked time in this period.":$"Across {projectCount} {(projectCount==1?"project":"projects")} in this period";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(_overviewHint!=null&&!e.Handled&&e.Key==Key.Escape&&!ProjectFilterSelector.IsDropDownOpen){Close();e.Handled=true;}
    }

    private static void Action(Button button,double width,string? name=null)
    {
        NavigatorFormStyles.StyleButton(button);button.Width=width;button.Height=38;button.Padding=new(10,0);button.FontSize=12;
        if(name!=null)button.Name=name;
    }
    private static TextBlock DashboardLabel(string text,double size,IBrush? ink=null)=>new(){Text=text,FontFamily=ClockPickerPresentation.Body,FontSize=size,Foreground=ink??ClockPickerPresentation.Muted,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    private static Border DashboardPane(Control content,Thickness padding)=>new(){Padding=padding,Background=Brush.Parse("#40FFF6E4"),BorderBrush=DashboardRule,BorderThickness=new(1),CornerRadius=new(7),Child=content};
    private static Border Metric(string title,TextBlock number,string detail,IBrush ink)
    {
        number.FontFamily=ClockPickerPresentation.Body;number.FontSize=27;number.Foreground=ink;number.Margin=default;
        var value=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,Child=number};
        var card=DashboardPane(new StackPanel{Spacing=3,Children={DashboardLabel(title,10),value,DashboardLabel(detail,10)}},new(14,10));
        card.Resources["PrimaryTextBrush"]=ink;card.Resources["AccentBrush"]=ink;return card;
    }
    private static Avalonia.Controls.Shapes.Path DashboardIcon(string path,double size)=>new(){Width=size,Height=size,Stretch=Stretch.Uniform,Data=Geometry.Parse(path),Stroke=ClockPickerPresentation.Navy,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
}
