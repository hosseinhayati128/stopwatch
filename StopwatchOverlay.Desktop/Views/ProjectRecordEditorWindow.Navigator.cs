using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordEditorWindow
{
    private bool _navigatorAdding;
    private ProjectChoice? _previousProject;
    private Button? _newProjectButton;
    private static readonly IBrush RecordRule=Brush.Parse("#509A7B48"),RecordDanger=Brush.Parse("#8F3328"),RecordField=Brush.Parse("#F7E8C7");
    private bool IsAddingProject=>_navigatorAdding||ProjectSelector.SelectedItem is ProjectChoice{IsNew:true};

    private void ApplyNavigatorLayout()
    {
        if(_theme!=AppThemeCatalog.Nika)return;
        Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        Resources["DangerTextBrush"]=RecordDanger;Resources["SurfaceRaisedBrush"]=RecordField;Resources["BorderBrush"]=RecordRule;
        FontFamily=ClockPickerPresentation.Body;Foreground=ClockPickerPresentation.Navy;FontSize=12;
        Width=MinWidth=560;Height=480;MinHeight=380;MaxHeight=double.PositiveInfinity;SizeToContent=SizeToContent.Manual;
        SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        Content=null;RecordScroll.Content=null;RecordSurface.Child=null;RecordBody.Children.Clear();RecordActions.Children.Clear();
        foreach(var field in new Control[]{StartDatePicker,EndDatePicker,StartTimeBox,EndTimeBox,DurationPreviewText})
            if(field.Parent is Panel parent)parent.Children.Remove(field);
        RecordBody.Margin=new(0,0,8,0);NavigatorFormStyles.Apply(this);
        var editing=_record?.EndUtc!=null;var type=StopwatchTypography.Resolve("navigator-bold");
        HeadingText.Text=editing?"Edit time record":"Add time record";HeadingText.Classes.Clear();HeadingText.FontFamily=type.NameFont;
        HeadingText.FontWeight=type.NameWeight;HeadingText.FontSize=30;HeadingText.HorizontalAlignment=HorizontalAlignment.Left;
        IntroText.Text=editing?"Adjust this completed record.":"Log completed work for a project.";IntroText.Margin=new(0,4,0,0);IntroText.FontSize=12;
        var heading=new StackPanel{Background=Brushes.Transparent,Children={HeadingText,IntroText}};
        heading.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        var close=new Button{Width=28,Height=28,Padding=new(0),VerticalAlignment=VerticalAlignment.Top,Margin=new(0,7,0,0),
            Content=new Avalonia.Controls.Shapes.Path{Width=16,Height=16,Data=Geometry.Parse("M2,2 L14,14 M14,2 L2,14"),Stroke=ClockPickerPresentation.Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round}};
        NavigatorFormStyles.StyleButton(close);close.BorderBrush=Brushes.Transparent;close.Click+=Cancel_Click;
        AutomationProperties.SetName(close,"Close time record editor");ToolTip.SetTip(close,"Cancel and close (Esc)");
        var header=new Grid{ColumnDefinitions=new("*,32"),Children={heading,close}};Grid.SetColumn(close,1);

        _navigatorAdding=IsAddingProject;_previousProject=ProjectSelector.SelectedItem as ProjectChoice;
        var choices=((IEnumerable<ProjectChoice>)ProjectSelector.ItemsSource!).Where(p=>!p.IsNew).ToArray();
        ProjectSelector.ItemsSource=choices;ProjectSelector.SelectedItem=_navigatorAdding?null:_previousProject;
        ProjectSelector.PlaceholderText="Choose a project";
        _newProjectButton=new Button{Name="RecordNewProjectButton",Width=70,Height=38,Padding=new(10,0)};NavigatorFormStyles.StyleButton(_newProjectButton);
        _newProjectButton.Click+=(_,_)=>
        {
            if(_navigatorAdding){_navigatorAdding=false;ProjectSelector.SelectedItem=_previousProject is{IsNew:false}?_previousProject:null;}
            else{_previousProject=ProjectSelector.SelectedItem as ProjectChoice;_navigatorAdding=true;ProjectSelector.SelectedItem=null;}
            UpdateNewProjectPanel();UpdateDurationPreview();
            if(!_navigatorAdding)ProjectSelector.Focus();
        };
        AutomationProperties.SetName(_newProjectButton,"New project or back to project list");
        var projectRow=new Grid{ColumnDefinitions=new("*,8,70"),Children={ProjectSelector,_newProjectButton}};Grid.SetColumn(_newProjectButton,2);
        ProjectLabel.FontSize=10;
        foreach(var field in new TemplatedControl[]{ProjectSelector,NewProjectBox,StartDatePicker,EndDatePicker,StartTimeBox,EndTimeBox})
        {field.Classes.Add("editorField");field.Height=38;field.MinHeight=0;field.FontFamily=ClockPickerPresentation.Body;field.FontSize=13;field.Padding=new(10,5);}
        foreach(var date in new[]{StartDatePicker,EndDatePicker})
        {
            date.SelectedDateFormat=CalendarDatePickerFormat.Custom;date.CustomDateFormatString="dd MMM yyyy";
            date.HorizontalAlignment=HorizontalAlignment.Stretch;date.ClearValue(PaddingProperty);
            ConfigureRecordCalendar(date);
        }
        Styles.Add(new Style(s=>s.OfType<CalendarDatePicker>().Class("editorField").Template().OfType<TextBox>()){
            Setters={new Setter(TemplatedControl.BackgroundProperty,RecordField),new Setter(TemplatedControl.ForegroundProperty,ClockPickerPresentation.Navy),new Setter(TemplatedControl.BorderBrushProperty,RecordRule)}});
        Styles.Add(new Style(s=>s.OfType<CalendarDatePicker>().Class("invalidTime").Template().OfType<TextBox>()){
            Setters={new Setter(TemplatedControl.BorderBrushProperty,RecordDanger)}});
        foreach(var (field,name) in new (Control,string)[]{(ProjectSelector,"Project"),(NewProjectBox,"New project name"),(StartDatePicker,"Start date"),(EndDatePicker,"End date"),(StartTimeBox,"Start time"),(EndTimeBox,"End time")})AutomationProperties.SetName(field,name);
        AutoTextDirection.Attach(NewProjectBox);
        NewProjectPanel.Padding=new(12);NewProjectPanel.Margin=new(0,12,0,0);NewProjectPanel.Background=Brush.Parse("#46FFF3D8");NewProjectPanel.BorderBrush=RecordRule;
        var draft=(StackPanel)NewProjectPanel.Child!;draft.Spacing=7;((TextBlock)draft.Children[0]).FontSize=10;
        draft.Children.Add(RecordLabel("This record will be added to the new project's history.",11));
        RecordBody.Children.Add(ProjectLabel);RecordBody.Children.Add(projectRow);RecordBody.Children.Add(NewProjectPanel);
        RecordBody.Children.Add(BuildEndpointFields());
        RecordScroll.Content = RecordBody;
        var footer = BuildRecordFooter();
        var layout=new Grid{RowDefinitions=new("76,*,Auto"),Children={header,RecordScroll,footer}};Grid.SetRow(RecordScroll,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        SizeChanged += (_, _) => FitRecordFrame();
        PointerPressed+=(_,e)=>{var p=e.GetPosition(this);if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18){BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}};
        UpdateNavigatorProject();
    }

    private void UpdateNavigatorProject()
    {
        if(_newProjectButton==null)return;
        _newProjectButton.Content=IsAddingProject?"Back":"New…";
        RefitNavigatorRecord();
    }
    private static TextBlock RecordLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=ClockPickerPresentation.Body,FontSize=size,Foreground=ClockPickerPresentation.Muted,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
}
