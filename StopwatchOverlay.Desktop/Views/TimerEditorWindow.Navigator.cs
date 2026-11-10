using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow
{
    private TextBlock? _recordsSummary;
    private static readonly IBrush EditorPaper=Brush.Parse("#EBD3A5"),FieldPaper=Brush.Parse("#F7E8C7"),
        EditorRule=Brush.Parse("#509A7B48"),EditorDanger=Brush.Parse("#8F3328");

    private void ApplyNavigatorLayout()
    {
        if(_settings?.ThemeMode!=AppThemeCatalog.Nika)return;
        Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;
        Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        Resources["AccentBrush"]=Brush.Parse("#245B63");
        Resources["DangerTextBrush"]=EditorDanger;
        Resources["SurfaceRaisedBrush"]=FieldPaper;
        Resources["BorderBrush"]=EditorRule;
        FontFamily=ClockPickerPresentation.Body;Foreground=ClockPickerPresentation.Navy;FontSize=12;
        Width=620;MinWidth=560;Height=780;MinHeight=520;MaxHeight=double.PositiveInfinity;SizeToContent=SizeToContent.Manual;
        SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;
        TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        Content=null;EditorScroll.Content=null;EditorSurface.Child=null;EditorBody.Children.Clear();
        EditorBody.Margin=new(0,0,10,0);
        NavigatorFormStyles.Apply(this);

        HeadingText.Text="Edit timer";HeadingText.Classes.Clear();HeadingText.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;
        HeadingText.FontSize=30;HeadingText.FontWeight=FontWeight.Bold;HeadingText.HorizontalAlignment=HorizontalAlignment.Left;
        HeadingText.Foreground=ClockPickerPresentation.Navy;
        SubheadingText.Text=$"{_timer.DisplayName}  ·  {(_timer.Mode switch{1=>"Clock",2=>"Countdown",3=>"Timecode",_=>"Stopwatch"})}  ·  {(_isRunning?"Running":"Paused")}";
        SubheadingText.FontSize=12;SubheadingText.TextAlignment=TextAlignment.Left;SubheadingText.Margin=new(0,2,0,0);
        SubheadingText.TextWrapping=TextWrapping.NoWrap;SubheadingText.TextTrimming=TextTrimming.CharacterEllipsis;
        var title=new StackPanel{Background=Brushes.Transparent,Children={HeadingText,SubheadingText}};
        title.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        var close=new Button{Content=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M 2,2 L 14,14 M 14,2 L 2,14"),Width=16,Height=16,
            Stroke=ClockPickerPresentation.Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round},Padding=new(0),Width=28,Height=28,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0)};
        NavigatorFormStyles.StyleButton(close);close.Click+=(_,_)=>Close(false);AutomationProperties.SetName(close,"Close timer editor");ToolTip.SetTip(close,"Close (Esc)");
        var header=new Grid{ColumnDefinitions=new("*,32"),Children={title,close}};Grid.SetColumn(close,1);

        ProjectLabel.FontSize=10;
        ProjectRow.Margin=new(0,0,0,12);CategoryRow.Margin=new(0,0,0,10);CategoryRow.ColumnDefinitions=new("*,156");
        ((Control)CategoryRow.Children[0]).Margin=new(0,0,16,0);
        foreach(var field in new TemplatedControl[]{ProjectSelector,CustomProjectBox,CategorySelector,CustomCategoryBox,ScorePerHourBox,TimeValueBox,StartTimeBox})
        {field.Classes.Add("editorField");field.MinHeight=36;field.FontSize=13;field.Padding=new(10,5);}
        AutoTextDirection.Attach(CustomProjectBox);AutoTextDirection.Attach(CustomCategoryBox);
        foreach(var button in new[]{ToggleProjectInputButton,ToggleCategoryInputButton,PauseResumeButton,CancelButton,UndoPreviousEditButton,SaveButton,DiscardRecordsButton,DeleteTimerButton})
            NavigatorFormStyles.StyleButton(button,button==SaveButton,button==DiscardRecordsButton||button==DeleteTimerButton);
        ToggleProjectInputButton.Width=70;ToggleCategoryInputButton.Width=62;
        ToggleProjectInputButton.Height=ToggleCategoryInputButton.Height=36;
        IdleStopForProjectCheck.FontSize=11;IdleStopForProjectCheck.Margin=new(0);
        // The full project name remains available without widening a long/RTL form.
        IdleStopForProjectCheck.ContentTemplate=new FuncDataTemplate<object>((value,_)=>new TextBlock{Text=value?.ToString(),TextWrapping=TextWrapping.Wrap,FontSize=11});
        EditorBody.Children.Add(ProjectLabel);EditorBody.Children.Add(ProjectRow);EditorBody.Children.Add(CategoryRow);EditorBody.Children.Add(IdleStopForProjectCheck);

        foreach(var control in new Control[]{TimeValueLabel,LiveTimePreview,TimeValueBox,AdjustmentSummaryText,QuickAdjustButtons,PauseResumeButton,SegmentHeaderLabel,StartTimeBox,SegmentDetailText,RecordsStatusText,DiscardRecordsButton,DeleteTimerButton})
            if(control.Parent is Panel parent)parent.Children.Remove(control);
        TimeValueLabel.Text=_timer.Mode==2?"Remaining time":"Elapsed time";
        TimeValueLabel.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;TimeValueLabel.FontSize=21;TimeValueLabel.Foreground=ClockPickerPresentation.Navy;
        LiveTimePreview.FontSize=11;
        var timeHeader=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(0,0,0,10),Children={TimeValueLabel,LiveTimePreview}};Grid.SetColumn(LiveTimePreview,1);
        TimeValueBox.Height=46;TimeValueBox.FontSize=27;TimeValueBox.FontFamily=StopwatchTypography.Resolve("navigator-bold").TimerFont;
        PauseResumeButton.Width=116;PauseResumeButton.Height=36;PauseResumeButton.Margin=new(10,0,0,0);
        var timeRow=new Grid{ColumnDefinitions=new("*,Auto"),Children={TimeValueBox,PauseResumeButton}};Grid.SetColumn(PauseResumeButton,1);
        AdjustmentSummaryText.FontSize=11;AdjustmentSummaryText.Margin=new(0,8,0,0);
        QuickAdjustButtons.HorizontalAlignment=HorizontalAlignment.Left;QuickAdjustButtons.Margin=new(0,12,0,0);
        foreach(var button in QuickAdjustButtons.Children.OfType<Button>()){NavigatorFormStyles.StyleButton(button);button.Height=30;button.Padding=new(9,2);}
        TimeCard.Child=new StackPanel{Children={timeHeader,timeRow,AdjustmentSummaryText,QuickAdjustButtons}};
        TimeCard.Background=Brush.Parse("#46FFF3D8");TimeCard.BorderBrush=EditorRule;TimeCard.CornerRadius=new(7);TimeCard.Margin=new(0,18,0,0);
        EditorBody.Children.Add(TimeCard);

        SegmentHeaderLabel.Text="LATEST SEGMENT START";SegmentHeaderLabel.FontSize=10;
        var latest=new Grid{ColumnDefinitions=new("184,16,*"),Margin=new(0,17,0,0)};
        latest.Children.Add(new StackPanel{Children={SegmentHeaderLabel,StartTimeBox}});
        SegmentDetailText.TextWrapping=TextWrapping.Wrap;SegmentDetailText.VerticalAlignment=VerticalAlignment.Bottom;
        Grid.SetColumn(SegmentDetailText,2);latest.Children.Add(SegmentDetailText);EditorBody.Children.Add(latest);
        EditorBody.Children.Add(EditorLabel("Editing the start adjusts elapsed time.",10,new Thickness(1,6,0,0)));
        SegmentsExpander.Margin=new(0,17,0,0);StyleEditorExpander(SegmentsExpander);
        SegmentsExpander.Header=EditorLabel($"Recorded segments · {_intervals.Count} segments",13);
        EditorBody.Children.Add(SegmentsExpander);
        RecordsStatusText.Margin=new(0,0,0,10);
        var recordButtons=new WrapPanel{Orientation=Orientation.Horizontal,Children={DiscardRecordsButton,DeleteTimerButton}};
        DiscardRecordsButton.Width=170;DiscardRecordsButton.Margin=new(0,0,8,8);DeleteTimerButton.Margin=new(0,0,0,8);
        RecordsCard.Child=new StackPanel{Children={RecordsStatusText,recordButtons}};
        RecordsCard.Margin=new(0,6,0,0);RecordsCard.Background=Brush.Parse("#30FFFFFF");RecordsCard.BorderBrush=EditorRule;
        _recordsSummary=EditorLabel("",10);
        var recordsHeader=new StackPanel{Spacing=2,Children={EditorLabel("Project records",13),_recordsSummary}};
        var records=new Expander{Name="EditorRecordsExpander",Header=recordsHeader,Content=RecordsCard,Margin=new(0,7,0,0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
        StyleEditorExpander(records);
        AutomationProperties.SetName(records,"Project records");
        EditorBody.Children.Add(records);UpdateNavigatorRecordsSummary();
        EditorScroll.Content=EditorBody;

        CancelButton.Width=82;CancelButton.MinWidth=0;UndoPreviousEditButton.Width=146;UndoPreviousEditButton.MinWidth=0;SaveButton.Width=132;
        EditorActions.ColumnDefinitions=new("82,*,146,10,132");Grid.SetColumn(UndoPreviousEditButton,2);Grid.SetColumn(SaveButton,4);UndoPreviousEditButton.Margin=new(0);
        var footer=new Grid{RowDefinitions=new("18,12,38"),Margin=new(0,12,0,0)};
        footer.Children.Add(new Border{Background=EditorRule,Height=1,VerticalAlignment=VerticalAlignment.Top});
        footer.Children.Add(EditorLabel("Ctrl+Enter save  ·  Esc cancel"+(UndoPreviousEditButton.IsVisible?"  ·  Ctrl+Z undo":""),10,new Thickness(1,5,0,0)));
        Grid.SetRow(EditorActions,2);footer.Children.Add(EditorActions);
        var layout=new Grid{RowDefinitions=new("70,*,80"),Children={header,EditorScroll,footer}};Grid.SetRow(EditorScroll,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=EditorPaper,Child=layout};
        Opened+=(_,_)=>
        {
            var area=Screens.ScreenFromWindow(this)?.WorkingArea;
            if(area.HasValue){double available=area.Value.Height/RenderScaling-16;MinHeight=Math.Min(520,available);Height=Math.Min(780,available);}
        };
        PointerPressed+=(_,e)=>
        {
            var p=e.GetPosition(this);
            if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18)
            {BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}
        };
    }

    private static TextBlock EditorLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontSize=size,
        FontFamily=ClockPickerPresentation.Body,Foreground=ClockPickerPresentation.Muted,Margin=margin,TextWrapping=TextWrapping.Wrap};

    private void UpdateNavigatorRecordsSummary()
    {
        if(_recordsSummary==null)return;
        _recordsSummary.Text=_discardRecords?"Removal pending until Save":"Included in project history";
        _recordsSummary.Foreground=_discardRecords?EditorDanger:ClockPickerPresentation.Muted;
        RecordsStatusText.Foreground=_discardRecords?EditorDanger:ClockPickerPresentation.Muted;
    }

    private static void StyleEditorExpander(Expander expander)
    {
        expander.HorizontalAlignment=HorizontalAlignment.Stretch;expander.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        expander.Template=new FuncControlTemplate<Expander>((owner,scope)=>
        {
            var header=new ContentPresenter{HorizontalContentAlignment=HorizontalAlignment.Stretch};
            header.Bind(ContentPresenter.ContentProperty,owner.GetObservable(HeaderedContentControl.HeaderProperty));
            var arrow=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M 2,2 L 7,7 L 2,12"),Stroke=ClockPickerPresentation.Muted,StrokeThickness=1.5,
                Width=14,Height=14,VerticalAlignment=VerticalAlignment.Center,RenderTransformOrigin=RelativePoint.Center};
            arrow.Bind(Visual.RenderTransformProperty,new Binding("IsExpanded"){Source=owner,Converter=new Avalonia.Data.Converters.FuncValueConverter<bool,ITransform>(open=>new RotateTransform(open?90:0))});
            var heading=new Grid{ColumnDefinitions=new("*,20"),Children={header,arrow}};Grid.SetColumn(arrow,1);
            var toggle=new ToggleButton{Content=heading,Padding=new(0),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            AutomationProperties.SetName(toggle,owner.Name=="SegmentsExpander"?"Recorded segments":"Project records");
            toggle.Bind(ToggleButton.IsCheckedProperty,new Binding("IsExpanded"){Source=owner,Mode=BindingMode.TwoWay});
            toggle.Template=new FuncControlTemplate<ToggleButton>((button,_)=>
            {
                var content=new ContentPresenter{Content=heading,HorizontalContentAlignment=HorizontalAlignment.Stretch};
                var border=new Border{Padding=new(4,8),BorderBrush=EditorRule,BorderThickness=new(0,0,0,1),Child=content};
                border.Bind(Border.BackgroundProperty,button.GetObservable(TemplatedControl.BackgroundProperty));return border;
            });
            toggle.Styles.Add(new Style(s=>s.OfType<ToggleButton>()){Setters={new Setter(TemplatedControl.BackgroundProperty,Brushes.Transparent)}});
            toggle.Styles.Add(new Style(s=>s.OfType<ToggleButton>().Class(":pointerover")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#30FFFFFF"))}});
            toggle.Styles.Add(new Style(s=>s.OfType<ToggleButton>().Class(":focus-visible")){Setters={new Setter(TemplatedControl.BackgroundProperty,Brush.Parse("#55FFFFFF"))}});
            var body=new ContentPresenter{HorizontalContentAlignment=HorizontalAlignment.Stretch};
            body.Bind(ContentPresenter.ContentProperty,new TemplateBinding(ContentControl.ContentProperty),BindingPriority.Template);
            body.Bind(Visual.IsVisibleProperty,owner.GetObservable(Expander.IsExpandedProperty));
            return new StackPanel{Children={toggle,body}};
        });
    }

}
