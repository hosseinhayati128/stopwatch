using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Platform;

namespace StopwatchOverlay.Desktop.Views;

public partial class FocusInterruptionDialogWindow
{
    private bool _focusAppearance;
    private static readonly IBrush FocusRule=Brush.Parse("#CDB784"),FocusCream=Brush.Parse("#F8EACA"),FocusTeal=Brush.Parse("#147A83");
    private static TextBlock FocusText(string value,double size=13,IBrush? brush=null)=>new(){Text=value,FontSize=size,FontFamily=ClockPickerPresentation.Body,
        Foreground=brush??ClockPickerPresentation.Navy,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
    private string FocusDefaultTitle=>DefaultReason switch{FocusPauseReason.TaskSwitch=>"Task switch",FocusPauseReason.ValidBreak=>"Break",_=>"Distraction"};

    private void ApplyFocusAppearance(string project,AppSettings settings)
    {
        if(AppThemeCatalog.Normalize(settings.ThemeMode)!=AppThemeCatalog.Nika)return;
        _focusAppearance=true;FontFamily=ClockPickerPresentation.Body;Resources["AppFontFamily"]=ClockPickerPresentation.Body;
        var buttons=new[]{ValidBreakButton,DistractionButton,IdeaButton,TaskSwitchButton};
        foreach(var button in buttons)((Panel)button.Parent!).Children.Remove(button);
        ((Panel)CountdownText.Parent!).Children.Remove(CountdownText);((Panel)FooterHintText.Parent!).Children.Remove(FooterHintText);Content=null;
        var ink=ClockPickerPresentation.Navy;var muted=ClockPickerPresentation.Muted;var gold=ClockPickerPresentation.Gold;
        var title=FocusText("Focus Check-in",28);title.Name="FocusHeading";title.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;title.FontWeight=FontWeight.Bold;
        var close=new Button {Name="FocusCloseButton",Width=30,Height=30,MinHeight=0,Padding=new(0),Background=Brushes.Transparent,BorderThickness=new(0),VerticalAlignment=VerticalAlignment.Top,
            Content=new Avalonia.Controls.Shapes.Path {Width=15,Height=15,Stretch=Stretch.Uniform,Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=ink,StrokeThickness=1.7}};
        AutomationProperties.SetName(close,"Skip check-in");ToolTip.SetTip(close,"Skip (Esc)");close.Click+=(_,_)=>SelectReason(FocusPauseReason.Unspecified);
        var titleRow=new Grid {ColumnDefinitions=new("*,30"),Children={title,close}};Grid.SetColumn(close,1);
        var header=new StackPanel {Spacing=3,Children={titleRow,FocusText("What was this pause for?",12,muted)}};
        var projectText=FocusText(project,18);projectText.FontWeight=FontWeight.SemiBold;AutoTextDirection.Apply(projectText,project);
        var projectInfo=new StackPanel {Spacing=3,Children={FocusText("PAUSED PROJECT",10,muted),projectText}};
        var duration=FocusText(FormatDuration(_pauseDuration),24);duration.FontWeight=FontWeight.SemiBold;duration.HorizontalAlignment=HorizontalAlignment.Right;
        var pauseLabel=FocusText("PAUSED FOR",10,muted);pauseLabel.HorizontalAlignment=HorizontalAlignment.Right;
        var durationInfo=new StackPanel {Spacing=1,Children={pauseLabel,duration}};
        var summaryRow=new Grid {ColumnDefinitions=new("*,16,Auto"),Children={projectInfo,durationInfo}};Grid.SetColumn(durationInfo,2);
        var summaryBody=new StackPanel {Children={summaryRow}};
        if(_isTaskSwitch)
        {
            string other=string.IsNullOrWhiteSpace(_otherProjectName)?"Another project":_otherProjectName.Trim();
            var detail=FocusText("During pause: "+other+(_otherProjectDuration.HasValue?" · "+FormatDuration(_otherProjectDuration.Value):""),12,FocusTeal);
            summaryBody.Children.Add(new Border {BorderBrush=FocusRule,BorderThickness=new(0,1,0,0),Margin=new(0,9,0,0),Padding=new(0,8,0,0),Child=detail});
        }
        var summary=new Border {Padding=new(12,10),Margin=new(0,0,0,12),CornerRadius=new(5),BorderBrush=FocusRule,BorderThickness=new(1),Background=Brush.Parse("#26FFF3D8"),Child=summaryBody};
        string[] titles=["Break / stepped away","Distraction","Idea / brainstorming","Task switch"];
        string[] descriptions=["Planned break, call or errand","Phone, social media or lost focus","A thought to capture before returning","Worked on another project or timer"];
        string[] icons=["M3,7 L15,7 L15,13 Q15,18 9,18 Q3,18 3,13 Z M15,8 L17,8 Q21,8 21,11 Q21,14 17,14 L15,14 M1,21 L18,21 M6,1 L6,4 M11,1 L11,4",
            "M13,1 L3,13 L10,13 L8,23 L21,9 L13,9 Z","M7,15 C1,10 4,2 11,2 C18,2 21,10 15,15 L15,18 L7,18 Z M8,21 L14,21 M9,24 L13,24",
            "M2,6 L21,6 M16,1 L21,6 L16,11 M22,18 L3,18 M8,13 L3,18 L8,23"];
        FocusPauseReason[] reasons=[FocusPauseReason.ValidBreak,FocusPauseReason.Distraction,FocusPauseReason.IdeaBrainstorming,FocusPauseReason.TaskSwitch];
        var choices=new StackPanel {Spacing=6};
        for(int index=0;index<buttons.Length;index++)
        {
            var button=buttons[index];bool active=reasons[index]==DefaultReason;IBrush foreground=active?FocusCream:ink;
            var key=FocusText((index+1).ToString(),13,foreground);key.FontWeight=FontWeight.SemiBold;key.HorizontalAlignment=HorizontalAlignment.Center;
            var badge=new Border {MinWidth=26,MinHeight=26,Padding=new(4,2),CornerRadius=new(4),BorderBrush=active?gold:FocusRule,BorderThickness=new(1),
                Background=active?Brush.Parse("#284654"):Brush.Parse("#F1DFB7"),VerticalAlignment=VerticalAlignment.Center,Child=key};
            var icon=new Avalonia.Controls.Shapes.Path {Width=20,Height=22,Stretch=Stretch.Uniform,Data=Geometry.Parse(icons[index]),Stroke=active?FocusCream:FocusTeal,
                StrokeThickness=1.5,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
            var label=FocusText(titles[index],14,foreground);label.FontWeight=FontWeight.SemiBold;
            var autoText=FocusText("AUTO",10,ink);autoText.FontWeight=FontWeight.SemiBold;autoText.HorizontalAlignment=HorizontalAlignment.Center;
            var auto=new Border {Padding=new(6,2),CornerRadius=new(3),Background=gold,Margin=new(8,0,0,0),IsVisible=active,Child=autoText};
            var line=new Grid {ColumnDefinitions=new("*,Auto"),Children={label,auto}};Grid.SetColumn(auto,1);
            var words=new StackPanel {Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children={line,FocusText(descriptions[index],11,active?Brush.Parse("#DACDAF"):muted)}};
            var row=new Grid {ColumnDefinitions=new("Auto,10,20,10,*"),Children={badge,icon,words}};Grid.SetColumn(icon,2);Grid.SetColumn(words,4);
            button.Classes.Remove("choice");button.MinHeight=62;button.Padding=new(12,9);button.Margin=new(0);button.CornerRadius=new(5);
            button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Center;
            button.Background=active?ink:FocusCream;button.Foreground=foreground;button.BorderBrush=active?gold:FocusRule;button.BorderThickness=new(1);button.Content=row;
            if(active)
            {
                button.Resources["ButtonBackgroundPointerOver"]=Brush.Parse("#214655");button.Resources["ButtonBackgroundPressed"]=Brush.Parse("#081E2A");
                button.Resources["ButtonForegroundPointerOver"]=button.Resources["ButtonForegroundPressed"]=FocusCream;
                button.Resources["ButtonBorderBrushPointerOver"]=button.Resources["ButtonBorderBrushPressed"]=gold;
            }
            DesktopTypography.SetPreserveColor(button,true);AutomationProperties.SetName(button,$"{index+1}. {titles[index]}"+(active?", automatic choice":""));choices.Children.Add(button);
        }
        var scroll=new ScrollViewer {Name="FocusBodyScroll",Margin=new(0,12,0,0),HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,Content=new StackPanel {Children={summary,choices}}};
        string why=DefaultReason==FocusPauseReason.TaskSwitch?"Activity on another project":DefaultReason==FocusPauseReason.ValidBreak?$"Pause of {_thresholdMinutes} min or longer":$"Pause under {_thresholdMinutes} min";
        CountdownText.FontSize=12;CountdownText.FontWeight=FontWeight.SemiBold;CountdownText.FontFamily=ClockPickerPresentation.Body;CountdownText.Foreground=ink;
        CountdownText.HorizontalAlignment=HorizontalAlignment.Stretch;CountdownText.TextWrapping=TextWrapping.Wrap;UpdateFocusCountdown();
        var reason=FocusText(why,10.5,muted);reason.MaxWidth=180;reason.Margin=new(12,0,0,0);reason.HorizontalAlignment=HorizontalAlignment.Right;
        var countdownRow=new Grid {ColumnDefinitions=new("*,Auto"),Children={CountdownText,reason}};Grid.SetColumn(reason,1);
        FooterHintText.Text="1–4 choose · Enter / Space default · Esc skip";FooterHintText.FontFamily=ClockPickerPresentation.Body;FooterHintText.FontSize=10.5;
        FooterHintText.Foreground=muted;FooterHintText.TextWrapping=TextWrapping.Wrap;
        var footer=new StackPanel {Spacing=8,Margin=new(0,12,0,0),Children={countdownRow,FooterHintText}};
        var layout=new Grid {RowDefinitions=new("Auto,*,Auto"),Children={header,scroll,footer}};Grid.SetRow(scroll,1);Grid.SetRow(footer,2);
        Content=new StopwatchFrameSurface {ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=AppUiScale.Normalize(settings.UiScalePercent)/100;SizeToContent=SizeToContent.Manual;Width=520*scale;Height=(_isTaskSwitch?570:538)*scale;
        Opened+=(_,_)=>{if(Screens.ScreenFromWindow(this) is {} screen)FitFocusToScreen(screen);else FitFocusContent();};
        SizeChanged+=(_,_)=>FitFocusContent();
    }
    private void UpdateFocusCountdown()=>CountdownText.Text=_focusAppearance?$"Auto-select {FocusDefaultTitle} in {_remainingSeconds}s":$"Closing in {_remainingSeconds}s";
    internal void FitFocusToScreen(Screen screen)
    {
        if(!_focusAppearance)return;
        MaxWidth=Math.Floor(screen.WorkingArea.Width/screen.Scaling);MaxHeight=Math.Floor(screen.WorkingArea.Height/screen.Scaling);
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        Position=OverlayScreenConstraint.PlaceOnScreen("Center",Position,new Size(Width,Height),screen);
        FitFocusContent();
    }
    internal void FitFocusContent()
    {
        if(DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame)return;
        double scale=DesktopPageAppearance.Scale(this);frame.Width=(ClientSize.Width>0?ClientSize.Width:Width)/scale;frame.Height=(ClientSize.Height>0?ClientSize.Height:Height)/scale;
        if(Content is LayoutTransformControl transform)transform.InvalidateArrange();
    }
}
