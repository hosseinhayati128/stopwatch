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
        Resources["ComboBoxDropDownGlyphForeground"]=ClockPickerPresentation.Navy;
        Resources["ComboBoxDropDownGlyphForegroundFocused"]=ClockPickerPresentation.Navy;
        Resources["CheckBoxCheckBackgroundStrokeUnchecked"]=ClockPickerPresentation.Muted;
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
        var title=new StackPanel{Background=Brushes.Transparent,Children={HeadingText}};
        title.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        var close=new Button{Content=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M 2,2 L 14,14 M 14,2 L 2,14"),Width=16,Height=16,
            Stroke=ClockPickerPresentation.Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round},Padding=new(0),Width=28,Height=28,VerticalAlignment=VerticalAlignment.Top,Margin=new(0,6,0,0)};
        NavigatorFormStyles.StyleButton(close);close.Click+=(_,_)=>Close(false);AutomationProperties.SetName(close,"Close timer editor");ToolTip.SetTip(close,"Close (Esc)");
        var header=new Grid{ColumnDefinitions=new("*,32"),Children={title,close}};Grid.SetColumn(close,1);

        foreach(var field in new TemplatedControl[]{ProjectSelector,CustomProjectBox,CategorySelector,CustomCategoryBox,ScorePerHourBox,TimeValueBox,StartTimeBox})
        {field.Classes.Add("editorField");field.MinHeight=36;field.FontSize=13;field.Padding=new(10,5);}
        AutoTextDirection.Attach(CustomProjectBox);AutoTextDirection.Attach(CustomCategoryBox);
        foreach(var button in new[]{ToggleProjectInputButton,ToggleCategoryInputButton,PauseResumeButton,CancelButton,UndoPreviousEditButton,SaveButton,DiscardRecordsButton,DeleteTimerButton})
            NavigatorFormStyles.StyleButton(button,button==SaveButton,button==DiscardRecordsButton);
        IdleStopForProjectCheck.FontSize=11;IdleStopForProjectCheck.Margin=new(0);
        IdleStopForProjectCheck.ContentTemplate=new FuncDataTemplate<object>((value,_)=>new TextBlock{Text=value?.ToString(),TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=ClockPickerPresentation.Navy});

        foreach(var control in new Control[]{TimeValueLabel,LiveTimePreview,TimeValueBox,AdjustmentSummaryText,QuickAdjustButtons,PauseResumeButton,SegmentHeaderLabel,StartTimeBox,SegmentDetailText,RecordsStatusText,DiscardRecordsButton,DeleteTimerButton})
            if(control.Parent is Panel parent)parent.Children.Remove(control);
        bool pixel = _settings.NikaControllerPixel;
        EditorBody.Children.Add(BuildTimerIdentity(pixel));
        EditorBody.Children.Add(BuildTimeDraft(pixel));
        EditorBody.Children.Add(BuildProjectFields(pixel));

        StyleEditorExpander(SegmentsExpander);
        var segments = new StackPanel { Margin = new(0,16,0,0) };
        BuildRecordedSegments(segments, StartTimeBox, SegmentDetailText, SegmentsExpander, _intervals, pixel);
        EditorBody.Children.Add(segments);
        EditorBody.Children.Add(BuildProjectRecords(pixel));
        EditorScroll.Content=EditorBody;

        CancelButton.Width=82;CancelButton.MinWidth=0;UndoPreviousEditButton.Width=146;UndoPreviousEditButton.MinWidth=0;SaveButton.Width=132;
        EditorActions.ColumnDefinitions=new("82,*,146,10,132");Grid.SetColumn(UndoPreviousEditButton,2);Grid.SetColumn(SaveButton,4);UndoPreviousEditButton.Margin=new(0);
        var footer=new Grid{RowDefinitions=new("18,12,38"),Margin=new(0,12,0,0)};
        footer.Children.Add(new Border{Background=EditorRule,Height=1,VerticalAlignment=VerticalAlignment.Top});
        footer.Children.Add(EditorLabel("Ctrl+Enter save  ·  Esc cancel"+(UndoPreviousEditButton.IsVisible?"  ·  Ctrl+Z undo":""),10,new Thickness(1,5,0,0)));
        Grid.SetRow(EditorActions,2);footer.Children.Add(EditorActions);
        var layout=new Grid{RowDefinitions=new("50,*,80"),Children={header,EditorScroll,footer}};Grid.SetRow(EditorScroll,1);Grid.SetRow(footer,2);
        _navigatorFrame=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=EditorPaper,Child=layout};
        Content=_navigatorFrame;
        SizeChanged+=(_,_)=>FitEditorFrame();
        Opened+=(_,_)=>FitEditorWindow(1);
        PointerPressed+=(_,e)=>
        {
            var p=e.GetPosition(this);
            if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18)
            {BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}
        };
    }

    private Control BuildProjectFields(bool pixel)
    {
        CustomProjectBox.Watermark = "Project name";
        CustomCategoryBox.Watermark = "Category name";
        ProjectSelector.ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock
        {
            Text = name, TextWrapping = TextWrapping.Wrap, FontFamily = ClockPickerPresentation.Body, FontSize = 14
        });
        ProjectSelector.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var project = BuildSelectionField("Project", ProjectSelector, CustomProjectBox, ToggleProjectInputButton, pixel);
        var category = BuildSelectionField("Category", CategorySelector, CustomCategoryBox, ToggleCategoryInputButton, pixel);
        ((Panel)ScorePerHourBox.Parent!).Children.Remove(ScorePerHourBox);
        var scoreLabel = ProjectFieldLabel("Score per hour");
        AutomationProperties.SetName(ScorePerHourBox, "Score per hour");
        var score = new StackPanel { Spacing = 5, Children = { new Grid { Height = 26, Children = { scoreLabel } }, ScorePerHourBox } };
        var row = new Grid { ColumnDefinitions = new("*,18,140"), Children = { category, score } };
        Grid.SetColumn(score, 2);
        return new StackPanel { Spacing = 10, Children = { project, row, IdleStopForProjectCheck } };
    }

    private static TextBlock ProjectFieldLabel(string title) => new()
    {
        Text = title, FontFamily = ClockPickerPresentation.Body, FontSize = 14, FontWeight = FontWeight.SemiBold,
        Foreground = ClockPickerPresentation.Navy, VerticalAlignment = VerticalAlignment.Center
    };

    private static Control BuildSelectionField(string title, ComboBox list, TextBox entry, Button action, bool pixel)
    {
        var fields = (Grid)list.Parent!;
        if (fields.Parent is Panel parent) parent.Children.Remove(fields);
        fields.Children.Remove(action);
        fields.ColumnDefinitions = new("*");
        fields.Margin = default;
        AutomationProperties.SetName(list, title);
        AutomationProperties.SetName(entry, title);
        action.Content = "New " + title.ToLowerInvariant();
        action.Height = 26;
        action.Margin = default;
        action.Padding = new(9, 1);
        if (pixel) action.Classes.Add("pixelAction");
        var header = new Grid { ColumnDefinitions = new("*,Auto"), Children = { ProjectFieldLabel(title), action } };
        Grid.SetColumn(action, 1);
        return new StackPanel { Spacing = 5, Children = { header, fields } };
    }

    private static TextBlock EditorLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontSize=size,
        FontFamily=ClockPickerPresentation.Body,Foreground=ClockPickerPresentation.Muted,Margin=margin,TextWrapping=TextWrapping.Wrap};

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
