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

public partial class TimerNameWindow
{
    private TextBlock? _navigatorHint;
    private TextBlock? _detailsTitle;
    private TextBlock? _detailsText;

    private void ApplyNavigatorLayout()
    {
        if(_theme!=AppThemeCatalog.Nika)return;
        var rule=Brush.Parse("#509A7B48");var paper=Brush.Parse("#F7E8C7");
        Resources["PrimaryTextBrush"]=ClockPickerPresentation.Navy;Resources["SecondaryTextBrush"]=ClockPickerPresentation.Muted;
        Resources["DangerTextBrush"]=Brush.Parse("#8F3328");Resources["SurfaceRaisedBrush"]=paper;Resources["BorderBrush"]=rule;
        FontFamily=ClockPickerPresentation.Body;Foreground=ClockPickerPresentation.Navy;FontSize=12;
        Width=560;MinWidth=560;Height=490;MinHeight=380;MaxHeight=double.PositiveInfinity;SizeToContent=SizeToContent.Manual;
        SystemDecorations=SystemDecorations.None;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
        Content=null;ChooserScroll.Content=null;ChooserSurface.Child=null;ChooserBody.Children.Clear();ChooserActions.Children.Clear();
        ChooserBody.Margin=new(0,0,8,0);NavigatorFormStyles.Apply(this);

        HeadingText.Text=_isCreatingTimer?"New timer":"Choose project";HeadingText.Classes.Clear();HeadingText.FontSize=30;
        HeadingText.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;HeadingText.FontWeight=FontWeight.Bold;
        HeadingText.HorizontalAlignment=HorizontalAlignment.Left;HeadingText.Foreground=ClockPickerPresentation.Navy;
        DescriptionText.Text=_isCreatingTimer?"Choose a project for your new timer.":$"Current timer: {(_currentName.Length==0?"Unnamed":_currentName)}";
        DescriptionText.Margin=new(0,4,0,0);DescriptionText.FontSize=12;DescriptionText.TextWrapping=TextWrapping.NoWrap;DescriptionText.TextTrimming=TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(DescriptionText,DescriptionText.Text);
        var title=new StackPanel{Background=Brushes.Transparent,Children={HeadingText,DescriptionText}};
        title.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){BeginMoveDrag(e);e.Handled=true;}};
        var close=new Button{Name="ChooserClose",Width=28,Height=28,Padding=new(0),VerticalAlignment=VerticalAlignment.Top,Margin=new(0,7,0,0),
            Content=new Avalonia.Controls.Shapes.Path{Width=16,Height=16,Data=Geometry.Parse("M2,2 L14,14 M14,2 L2,14"),Stroke=ClockPickerPresentation.Navy,StrokeThickness=2,StrokeLineCap=PenLineCap.Round}};
        NavigatorFormStyles.StyleButton(close);close.Classes.Add("chooserClose");close.Click+=(_,_)=>Close(false);
        Styles.Add(new Style(s=>s.OfType<Button>().Class("chooserClose")){Setters={new Setter(TemplatedControl.BorderBrushProperty,Brushes.Transparent)}});
        AutomationProperties.SetName(close,"Close project chooser");ToolTip.SetTip(close,"Cancel and close");
        var header=new Grid{ColumnDefinitions=new("*,32"),Children={title,close}};Grid.SetColumn(close,1);

        if(ProjectSelector.Items[0] is ComboBoxItem empty)empty.Content="No project";
        foreach(var label in new[]{ProjectLabel,CategoryLabel,ScoreLabel,ProjectDraftLabel,CategoryDraftLabel})label.FontSize=10;
        foreach(var field in new TemplatedControl[]{ProjectSelector,CategorySelector,NewProjectBox,NewCategoryBox,ScorePerHourBox})
        {field.Classes.Add("editorField");field.Height=38;field.MinHeight=0;field.FontSize=13;field.FontFamily=ClockPickerPresentation.Body;field.Padding=new(10,5);}
        AutoTextDirection.Attach(NewProjectBox);AutoTextDirection.Attach(NewCategoryBox);
        foreach(var button in new[]{AddProjectButton,AddCategoryButton,CancelButton,SaveButton})
        {NavigatorFormStyles.StyleButton(button,button==SaveButton);button.Height=38;button.FontSize=12;button.Padding=new(10,0);}
        AddProjectButton.Width=70;AddCategoryButton.Width=62;CancelButton.Width=82;SaveButton.Width=132;
        AutomationProperties.SetName(AddProjectButton,"New project or back to project list");AutomationProperties.SetName(AddCategoryButton,"New category or back to category list");
        AutomationProperties.SetName(ProjectSelector,"Project");AutomationProperties.SetName(CategorySelector,"Category");
        AutomationProperties.SetName(NewProjectBox,"New project name");AutomationProperties.SetName(NewCategoryBox,"New category name");AutomationProperties.SetName(ScorePerHourBox,"Score per hour");
        ProjectDraftLabel.Text="NEW PROJECT NAME";CategoryDraftLabel.Text="NEW CATEGORY NAME";
        foreach(var panel in new[]{NewProjectPanel,NewCategoryPanel})
        {panel.Background=Brush.Parse("#46FFF3D8");panel.BorderBrush=rule;panel.Padding=new(12);panel.Margin=new(0,12,0,0);}
        ((StackPanel)NewProjectPanel.Child!).Children.Add(ChooserLabel("This name will also appear on the timer.",11));
        ValidationText.TextWrapping=CategoryValidationText.TextWrapping=TextWrapping.Wrap;
        NewProjectBox.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)ValidationText.IsVisible=false;};
        NewCategoryBox.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)CategoryValidationText.IsVisible=false;};
        ValidationText.PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty)NewProjectBox.Classes.Set("invalidTime",ValidationText.IsVisible);};
        CategoryValidationText.PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty)NewCategoryBox.Classes.Set("invalidTime",CategoryValidationText.IsVisible);};
        CategoryRow.ColumnDefinitions=new("*,160");CategoryRow.Margin=new(0,18,0,0);
        ((Control)CategoryRow.Children[0]).Margin=new(0,0,16,0);ScoreSuffix.IsVisible=false;
        ChooserBody.Children.Add(ProjectLabel);ChooserBody.Children.Add(ProjectRow);ChooserBody.Children.Add(NewProjectPanel);
        ChooserBody.Children.Add(CategoryRow);ChooserBody.Children.Add(NewCategoryPanel);
        ChooserBody.Children.Add(ChooserLabel("Points earned per hour of tracked work.",10,new Thickness(0,7,0,0)));
        _detailsTitle=ChooserLabel("Project details",12);_detailsTitle.Foreground=ClockPickerPresentation.Navy;
        _detailsText=ChooserLabel("",11);
        ChooserBody.Children.Add(new Border{Margin=new(0,18,0,0),Padding=new(12),Background=Brush.Parse("#26FFFFFF"),BorderBrush=rule,
            BorderThickness=new(0,1,0,0),Child=new StackPanel{Spacing=5,Children={_detailsTitle,_detailsText}}});
        NoProjectHintText.FontSize=11;NoProjectHintText.Margin=new(0,14,0,0);ChooserBody.Children.Add(NoProjectHintText);
        ChooserScroll.Content=ChooserBody;

        _navigatorHint=ChooserLabel("",10,new Thickness(0,4,0,0));
        var actions=new Grid{ColumnDefinitions=new("82,*,132"),Children={CancelButton,SaveButton}};Grid.SetColumn(SaveButton,2);
        var footer=new Grid{RowDefinitions=new("24,38"),Margin=new(0,12,0,0),Children={
            new Border{Height=1,Background=rule,VerticalAlignment=VerticalAlignment.Top},_navigatorHint,actions}};Grid.SetRow(actions,1);
        var layout=new Grid{RowDefinitions=new("76,*,74"),Children={header,ChooserScroll,footer}};Grid.SetRow(ChooserScroll,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface{ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        UpdateNavigatorDrafts();Opened+=(_,_)=>UpdateNavigatorDrafts();
        PointerPressed+=(_,e)=>
        {
            var p=e.GetPosition(this);
            if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&p.X>=Bounds.Width-18&&p.Y>=Bounds.Height-18){BeginResizeDrag(WindowEdge.SouthEast,e);e.Handled=true;}
        };
    }

    private void UpdateNavigatorDrafts()
    {
        if(_navigatorHint==null)return;
        AddProjectButton.Content=_isAddingProject?"Back":"New…";AddCategoryButton.Content=_isAddingCategory?"Back":"New…";
        _detailsTitle!.Text=_isAddingProject?(_isCreatingTimer?"Ready when you create":"Ready when you apply"):"Project details";
        _detailsText!.Text=_isAddingProject?"Your new project uses the category and score above.":"Selecting a project fills its saved category and score. You can adjust them here.";
        NoProjectHintText.Text=_isAddingProject?"Use Back to choose an existing project or leave the timer unnamed.":_isCreatingTimer?
            "No project? Leave this timer unnamed and assign one later.":"Choose “No project” to make this timer unnamed.";
        _navigatorHint.Text=$"Enter {(_isCreatingTimer?"create":"apply")}  ·  Esc {(_isAddingProject||_isAddingCategory?"back":"close")}";
        double desired=490+(_isAddingProject?112:0)+(_isAddingCategory?112:0);
        var screen=IsVisible?Screens.ScreenFromWindow(this):null;
        if(screen!=null)
        {double available=screen.WorkingArea.Height/RenderScaling-16;MinHeight=Math.Min(380,available);desired=Math.Min(desired,available);}
        Height=desired;
        if(screen!=null)
        {
            var work=screen.WorkingArea;
            Position=new PixelPoint(Position.X,Math.Clamp(Position.Y,work.Y,work.Bottom-(int)Math.Ceiling(desired*RenderScaling)));
        }
    }

    private static TextBlock ChooserLabel(string text,double size,Thickness margin=default)=>new(){Text=text,FontFamily=ClockPickerPresentation.Body,
        FontSize=size,Foreground=ClockPickerPresentation.Muted,TextWrapping=TextWrapping.Wrap,Margin=margin};
}
