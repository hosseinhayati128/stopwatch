using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NotesViewerWindow
{
    private TextBlock? _viewerSummary,_viewerEmptyTitle,_viewerStatus;
    private StackPanel? _viewerEmpty;
    private ScrollViewer? _viewerStatusScroll;
    private readonly List<(Button Button,string Label,NoteType? Type)> _viewerTabs=new();
    private static readonly IBrush ViewerRule=Brush.Parse("#CDB784"),ViewerCream=Brush.Parse("#F8EACA"),ViewerTeal=Brush.Parse("#147A83");
    private static TextBlock ViewerText(string text,double size=13,IBrush? color=null)=>new(){Text=text,FontSize=size,FontFamily=ClockPickerPresentation.Body,
        Foreground=color??ClockPickerPresentation.Navy,VerticalAlignment=VerticalAlignment.Center};

    private void ApplyViewerAppearance()
    {
        if(AppThemeCatalog.Normalize(_settings.ThemeMode)!=AppThemeCatalog.Nika)return;
        FontFamily=ClockPickerPresentation.Body;Resources["AppFontFamily"]=ClockPickerPresentation.Body;
        var header=(Panel)ViewerHeading.Parent!;header.Children.Remove(ViewerHeading);header.Children.Remove(CloseButton);header.Children.Remove(CountdownBadge);
        ((Border)NotesScrollViewer.Parent!).Child=null;ItemsContainer.Children.Remove(EmptyMessageText);
        ((Panel)VaultLocationText.Parent!).Children.Remove(VaultLocationText);Content=null;
        ViewerHeading.Text="Notes, Todos & Reminders";ViewerHeading.FontSize=28;ViewerHeading.FontWeight=FontWeight.Bold;
        ViewerHeading.FontFamily=StopwatchTypography.Resolve("navigator-bold").NameFont;ViewerHeading.Foreground=ClockPickerPresentation.Navy;ViewerHeading.TextWrapping=TextWrapping.Wrap;
        CloseButton.Width=CloseButton.Height=32;CloseButton.MinHeight=0;CloseButton.Background=Brushes.Transparent;CloseButton.BorderThickness=new(0);CloseButton.VerticalAlignment=VerticalAlignment.Top;
        CloseButton.Content=new Avalonia.Controls.Shapes.Path {Width=16,Height=16,Stretch=Stretch.Uniform,Data=Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),Stroke=ClockPickerPresentation.Navy,StrokeThickness=1.7};
        AutomationProperties.SetName(CloseButton,"Close notes viewer");
        var heading=new Grid {ColumnDefinitions=new("*,32"),Background=Brushes.Transparent,Children={ViewerHeading,CloseButton}};Grid.SetColumn(CloseButton,1);heading.PointerPressed+=OnHeaderPointerPressed;
        _viewerSummary=ViewerText("",12,ClockPickerPresentation.Muted);_viewerSummary.TextWrapping=TextWrapping.Wrap;
        CountdownText.Text="Auto-close · 30s";CountdownText.FontFamily=ClockPickerPresentation.Body;CountdownText.Foreground=ClockPickerPresentation.Muted;
        CountdownBadge.Margin=new(0);CountdownBadge.Padding=new(8,3);CountdownBadge.CornerRadius=new(3);CountdownBadge.Background=ViewerCream;CountdownBadge.BorderBrush=ViewerRule;
        var subrow=new Grid {ColumnDefinitions=new("*,Auto"),Margin=new(0,4,0,0),Children={_viewerSummary,CountdownBadge}};Grid.SetColumn(CountdownBadge,1);
        var top=new StackPanel {Margin=new(0,0,0,14),Children={heading,subrow}};
        var tabs=new WrapPanel {Orientation=Orientation.Horizontal};
        foreach(var (label,type) in new (string,NoteType?)[]{("All",null),("Todos",NoteType.Todo),("Notes",NoteType.Note),("Reminders",NoteType.Reminder)})
        {
            var button=new Button {Name="ViewerTab"+label,Content=label,MinHeight=34,Padding=new(14,6),Margin=new(0,0,6,6),CornerRadius=new(4),BorderBrush=ClockPickerPresentation.Gold,BorderThickness=new(1),
                FontFamily=ClockPickerPresentation.Body,FontSize=13,FontWeight=FontWeight.Normal,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            DesktopTypography.SetPreserveColor(button,true);
            button.Click+=(_,_)=>{_currentFilter=type;RenderEntries();NotesScrollViewer.Offset=default;};
            _viewerTabs.Add((button,label,type));tabs.Children.Add(button);
        }
        _viewerEmptyTitle=ViewerText("",20);_viewerEmptyTitle.FontWeight=FontWeight.SemiBold;_viewerEmptyTitle.HorizontalAlignment=HorizontalAlignment.Center;_viewerEmptyTitle.TextWrapping=TextWrapping.Wrap;
        EmptyMessageText.Margin=new(0);EmptyMessageText.FontSize=13;EmptyMessageText.Foreground=ClockPickerPresentation.Muted;EmptyMessageText.TextWrapping=TextWrapping.Wrap;
        _viewerEmpty=new StackPanel {Spacing=10,MaxWidth=390,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Children={_viewerEmptyTitle,EmptyMessageText}};
        var surface=new Border {Padding=new(12),BorderBrush=ViewerRule,BorderThickness=new(1),CornerRadius=new(6),Background=Brush.Parse("#26FFF3D8"),Child=new Grid {Children={NotesScrollViewer,_viewerEmpty}}};
        ((Panel)SearchBar.Parent!).Children.Remove(SearchBar);
        ((Panel)SearchHint.Parent!).Children.Remove(SearchHint);
        SearchHint.Foreground=ClockPickerPresentation.Muted;
        SearchBar.Background=Brush.Parse("#FBF0D7");
        SearchBar.BorderBrush=ViewerRule;
        SearchField.Foreground=ClearSearch.Foreground=ClockPickerPresentation.Navy;
        SearchField.FontSize=14;
        SearchShortcut.Foreground=ClockPickerPresentation.Muted;
        SearchField.GotFocus+=(_,_)=>SearchBar.BorderBrush=ViewerTeal;
        SearchField.LostFocus+=(_,_)=>SearchBar.BorderBrush=ViewerRule;
        var filters=new StackPanel {Spacing=2,Children={tabs,SearchBar}};
        VaultLocationText.Foreground=ClockPickerPresentation.Muted;VaultLocationText.TextTrimming=TextTrimming.CharacterEllipsis;
        var footer=new Grid {ColumnDefinitions=new("Auto,16,*"),Children={SearchHint,VaultLocationText}};Grid.SetColumn(VaultLocationText,2);
        _viewerStatus=ViewerText("",12,Brush.Parse("#9C3026"));_viewerStatus.TextWrapping=TextWrapping.Wrap;
        _viewerStatusScroll=new ScrollViewer {Content=_viewerStatus,MaxHeight=100,Margin=new(0,0,0,10),IsVisible=false,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        var bottom=new StackPanel {Margin=new(0,14,0,0),Children={_viewerStatusScroll,footer}};
        var layout=new Grid {RowDefinitions=new("Auto,Auto,*,Auto"),Children={top,filters,surface,bottom}};Grid.SetRow(filters,1);Grid.SetRow(surface,2);Grid.SetRow(bottom,3);
        Content=new StopwatchFrameSurface {ArtworkScale=.16,Padding=new(10),Background=Brush.Parse("#EBD3A5"),Child=layout};
        double scale=AppUiScale.Normalize(_settings.UiScalePercent)/100;
        MinWidth=620*scale;MinHeight=440*scale;Width=760*scale;Height=580*scale;
        Opened+=(_,_)=>FitViewerContent();SizeChanged+=(_,_)=>FitViewerContent();
    }

    private void UpdateViewerSummary(int count, IReadOnlyList<NoteEntry> matches, bool searching)
    {
        if(_viewerSummary==null)return;
        _viewerSummary.Text=searching ? (count == 1 ? "1 match" : $"{count} matches")
            : (_currentFilter==null?$"{_allEntries.Count} entries":$"{count} of {_allEntries.Count} entries")+" · Newest first";
        foreach(var (button,label,type) in _viewerTabs)
        {
            int total=matches.Count(e=>type==null||e.Type==type);button.Content=$"{label}  {total}";
            bool active=type==_currentFilter;button.Background=active?ClockPickerPresentation.Navy:Brushes.Transparent;button.Foreground=active?Brushes.White:ClockPickerPresentation.Navy;
            AutomationProperties.SetName(button,$"{label}, {total} entries"+(active?", selected":""));
        }
        _viewerEmpty!.IsVisible=false;NotesScrollViewer.IsVisible=true;
    }

    private void ShowViewerEmpty(string title,string message)
    {
        _viewerEmptyTitle!.Text=title;EmptyMessageText.Text=message;EmptyMessageText.IsVisible=true;
        _viewerEmpty!.IsVisible=true;NotesScrollViewer.IsVisible=false;
    }

    private void StyleViewerRow(Border border,NoteEntry entry,TextBlock text,TextBlock time,DateTime? due)
    {
        var grid=(Grid)border.Child!;var marker=grid.Children[0];var content=grid.Children[2];grid.Children.Clear();
        if(entry.Type==NoteType.Note)marker=new Avalonia.Controls.Shapes.Path {Width=18,Height=20,Stretch=Stretch.Uniform,Margin=new(1,2,0,0),
            Data=Geometry.Parse("M3,1 L12,1 L17,6 L17,20 L3,20 Z M12,1 L12,6 L17,6 M6,10 L14,10 M6,14 L14,14"),Stroke=ViewerTeal,StrokeThickness=1.5};
        else if(marker is CheckBox check)
        {
            check.MinWidth=check.MinHeight=0;check.Padding=new(0);check.Margin=new(0,1,0,0);
            check.Resources["CheckBoxCheckBackgroundFillChecked"]=ClockPickerPresentation.Navy;check.Resources["CheckBoxCheckBorderBrushChecked"]=ClockPickerPresentation.Gold;check.Resources["CheckBoxCheckGlyphForegroundChecked"]=ViewerCream;
        }
        marker.VerticalAlignment=VerticalAlignment.Top;
        text.FontFamily=ClockPickerPresentation.Body;text.FontSize=14;text.Foreground=entry.IsCompleted?ClockPickerPresentation.Muted:ClockPickerPresentation.Navy;
        time.FontFamily=ClockPickerPresentation.Body;time.Foreground=ClockPickerPresentation.Muted;time.Margin=new(12,2,0,0);
        bool progress=entry.Type==NoteType.Todo&&!entry.IsCompleted&&_organization?.IsInProgress(entry)==true;
        string detail=entry.Type.ToString()+(entry.IsCompleted?" · Completed":progress?" · In progress":due.HasValue?$" · Due {due:dd MMM · HH:mm}":"");
        var metadata=ViewerText(detail,11,progress?ViewerTeal:ClockPickerPresentation.Muted);metadata.TextWrapping=TextWrapping.Wrap;
        var body=new StackPanel {Spacing=5,Children={content,metadata}};
        grid.ColumnDefinitions=new("24,10,*,Auto");grid.Children.Add(marker);grid.Children.Add(body);grid.Children.Add(time);
        Grid.SetColumn(marker,0);Grid.SetColumn(body,2);Grid.SetColumn(time,3);
        border.MinHeight=64;border.Padding=new(12,10);border.Margin=new(0,0,0,8);border.CornerRadius=new(5);border.Background=ViewerCream;border.BorderBrush=ViewerRule;
    }

    private void PauseAutoClose()
    {
        _autoCloseTimer.Stop();
        CountdownText.Text="Auto-close paused";
    }

    internal void FitViewerContent()
    {
        if(DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame)return;
        double scale=DesktopPageAppearance.Scale(this);
        frame.Width=(ClientSize.Width>0?ClientSize.Width:Width)/scale;frame.Height=(ClientSize.Height>0?ClientSize.Height:Height)/scale;
        if(Content is LayoutTransformControl transform)transform.InvalidateArrange();
    }
}
