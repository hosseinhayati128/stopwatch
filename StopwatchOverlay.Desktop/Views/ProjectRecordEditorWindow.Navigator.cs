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
    private Border? _durationCard;
    private TextBlock? _durationHint;
    private TextBlock? _recordHint;
    private static readonly IBrush RecordRule=Brush.Parse("#509A7B48"),RecordDanger=Brush.Parse("#8F3328"),RecordField=Brush.Parse("#F7E8C7");
    private bool IsAddingProject=>_navigatorAdding||ProjectSelector.SelectedItem is ProjectChoice{IsNew:true};

    private void ApplyNavigatorLayout()
    {
        if(_theme!=AppThemeCatalog.Nika)return;
        Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        Resources["DangerTextBrush"]=RecordDanger;Resources["SurfaceRaisedBrush"]=RecordField;Resources["BorderBrush"]=RecordRule;
        FontFamily=ClockPickerPresentation.Body;Foreground=ClockPickerPresentation.Navy;FontSize=12;
        Width=MinWidth=560;Height=620;MinHeight=380;MaxHeight=double.PositiveInfinity;SizeToContent=SizeToContent.Manual;
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
        {date.SelectedDateFormat=CalendarDatePickerFormat.Custom;date.CustomDateFormatString="dd MMM yyyy";date.HorizontalAlignment=HorizontalAlignment.Stretch;date.ClearValue(PaddingProperty);}
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
        var start=Endpoint("START",StartDatePicker,StartTimeBox);var end=Endpoint("END",EndDatePicker,EndTimeBox);
        var range=new Grid{ColumnDefinitions=new("*,20,*"),Children={start,end}};Grid.SetColumn(end,2);
        RecordBody.Children.Add(new Border{Margin=new(0,18,0,0),Padding=new(12),Background=Brush.Parse("#26FFFFFF"),BorderBrush=RecordRule,BorderThickness=new(1),CornerRadius=new(6),Child=range});
        RecordBody.Children.Add(RecordLabel("Local time · 24-hour format · HH:mm or HH:mm:ss",10,new Thickness(0,9,0,0)));
        DurationPreviewText.Classes.Clear();DurationPreviewText.FontFamily=type.TimerFont;DurationPreviewText.FontWeight=type.TimerWeight;DurationPreviewText.FontSize=32;
        DurationPreviewText.Foreground=ClockPickerPresentation.Navy;DurationPreviewText.MaxWidth=240;DurationPreviewText.TextTrimming=TextTrimming.CharacterEllipsis;
        _durationHint=RecordLabel("",12);var summary=new Grid{ColumnDefinitions=new("*,Auto"),Children={new StackPanel{Spacing=5,VerticalAlignment=VerticalAlignment.Center,Children={RecordLabel("DURATION",10),_durationHint}},DurationPreviewText}};Grid.SetColumn(DurationPreviewText,1);
        _durationCard=new Border{Margin=new(0,16,0,0),Padding=new(12),MinHeight=76,BorderBrush=RecordRule,BorderThickness=new(1),CornerRadius=new(6),Background=Brush.Parse("#46FFF3D8"),Child=summary};RecordBody.Children.Add(_durationCard);
        _recordHint=RecordLabel("",11,new Thickness(0,10,0,0));RecordBody.Children.Add(_recordHint);
        ValidationText.FontSize=11;ValidationText.Margin=new(0,10,0,0);RecordBody.Children.Add(ValidationText);RecordScroll.Content=RecordBody;
        foreach(var button in new[]{CancelButton,SaveButton}){NavigatorFormStyles.StyleButton(button,button==SaveButton);button.Height=38;button.Padding=new(10,0);}
        CancelButton.Width=82;SaveButton.Width=132;
        var actions=new Grid{ColumnDefinitions=new("82,*,132"),Children={CancelButton,SaveButton}};Grid.SetColumn(SaveButton,2);
        var footer=new Grid{RowDefinitions=new("24,38"),Margin=new(0,12,0,0),Children={new Border{Height=1,Background=RecordRule,VerticalAlignment=VerticalAlignment.Top},RecordLabel("Tab move · Esc close",10,new Thickness(0,4,0,0)),actions}};Grid.SetRow(actions,1);
        var layout=new Grid{RowDefinitions=new("76,*,74"),Children={header,RecordScroll,footer}};Grid.SetRow(RecordScroll,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        PointerPressed+=(_,e)=>{var p=e.GetPosition(this);if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18){BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}};
        UpdateNavigatorProject();
    }

    private void UpdateNavigatorProject()
    {
        if(_newProjectButton==null)return;
        _newProjectButton.Content=IsAddingProject?"Back":"New…";
        double desired=IsAddingProject?732:620;var screen=IsVisible?Screens.ScreenFromWindow(this):null;
        if(screen!=null){double available=screen.WorkingArea.Height/RenderScaling-16;MinHeight=Math.Min(380,available);desired=Math.Min(desired,available);}
        Height=desired;
        if(screen!=null)Position=new PixelPoint(Position.X,Math.Clamp(Position.Y,screen.WorkingArea.Y,screen.WorkingArea.Bottom-(int)Math.Ceiling(desired*RenderScaling)));
    }
    private void UpdateNavigatorSummary(TimeSpan? duration)
    {
        if(_durationHint==null)return;
        DurationPreviewText.Text=duration is{} value?(value.Days>0?$"{value.Days}d ":"")+value.ToString(@"hh\:mm\:ss"):"—";
        DurationPreviewText.FontSize=DurationPreviewText.Text.Length>10?22:32;ToolTip.SetTip(DurationPreviewText,DurationPreviewText.Text);
        _durationHint.Text=duration==null?"Check the time range":_record?.EndUtc!=null?"Updated record length":"Time to add";
        _recordHint!.Text=duration!=null&&StartDatePicker.SelectedDate?.Date<EndDatePicker.SelectedDate?.Date?"The record crosses midnight into a later day.":_record?.EndUtc!=null?"Changes will update this completed record.":"Completed time will be added to this project's history.";
    }
    private void UpdateNavigatorValidation(bool show)
    {
        if(_durationCard==null)return;
        foreach(var field in new Control[]{ProjectSelector,NewProjectBox,StartDatePicker,EndDatePicker,StartTimeBox,EndTimeBox})field.Classes.Set("invalidTime",show&&field==_validationTarget);
        _durationCard.BorderBrush=show?RecordDanger:RecordRule;DurationPreviewText.Foreground=show?RecordDanger:ClockPickerPresentation.Navy;_recordHint!.IsVisible=!show;
    }
    private static StackPanel Endpoint(string title,Control date,Control time)=>new(){Spacing=7,Children={RecordLabel(title,10),date,RecordLabel("TIME",10,new Thickness(0,5,0,0)),time}};
    private static TextBlock RecordLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=ClockPickerPresentation.Body,FontSize=size,Foreground=ClockPickerPresentation.Muted,TextWrapping=TextWrapping.Wrap,Margin=margin,VerticalAlignment=VerticalAlignment.Center};
}
