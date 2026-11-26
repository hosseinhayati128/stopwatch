using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Local sample state only: no timer, controller, session or persistence dependencies.</summary>
public sealed class StopwatchAppearancePreview : UserControl
{
    private const string DefaultTime = "00:44:27.0";
    private const string TypographyTime = "12:34:56.0";
    private const string SampleProject = "Illustration";
    private readonly ThemedClockFace selected=new(){Text=DefaultTime,ProjectName=SampleProject};
    private readonly ThemedClockFace unselected=new(){Text=DefaultTime,ProjectName=SampleProject};
    private readonly Border backdrop=new(){Padding=new Thickness(8,7),CornerRadius=new CornerRadius(6)};
    private readonly Border toolbar;
    private readonly Button[] buttons;
    private readonly TextBlock inactivePercent = new() { FontSize = 12 };
    private readonly Border divider = new() { Height = 1, Margin = new(0,2,0,5) };
    private AppSettings sample=new(){OverlayTheme=OverlayThemeCatalog.Nika,TextColor="Theme default"};
    private bool running=true,darkBackdrop=true;

    public StopwatchAppearancePreview()
    {
        toolbar = CreateSampleToolbar(out buttons);
        var selectedGroup=new StackPanel{Spacing=0};selectedGroup.Children.Add(selected);selectedGroup.Children.Add(toolbar);
        VerticalAlignment = VerticalAlignment.Top;
        FontFamily = ClockPickerPresentation.Body;
        FontWeight = FontWeight.SemiBold;
        var grid = new Grid { RowDefinitions = new("Auto,3*,Auto,Auto,2*") };
        var active = new Viewbox { Child = selectedGroup, StretchDirection = StretchDirection.DownOnly };
        var inactive = new Viewbox { Child = unselected, StretchDirection = StretchDirection.DownOnly };
        var activeHeader = ClockHeader("Active clock", new TextBlock { FontSize = 12, Text = "100%" });
        var inactiveHeader = ClockHeader("Inactive clock", inactivePercent);
        Grid.SetRow(active, 1);
        Grid.SetRow(divider, 2);
        Grid.SetRow(inactiveHeader, 3);
        Grid.SetRow(inactive, 4);
        grid.Children.Add(activeHeader);
        grid.Children.Add(active);
        grid.Children.Add(divider);
        grid.Children.Add(inactiveHeader);
        grid.Children.Add(inactive);
        backdrop.Child=grid;Content=backdrop;
        Apply(sample);
    }

    private static Grid ClockHeader(string title, TextBlock percentage)
    {
        var label = new TextBlock { Text = title, FontSize = 13 };
        Grid.SetColumn(percentage, 1);
        percentage.VerticalAlignment = VerticalAlignment.Center;
        return new Grid { ColumnDefinitions = new("*,Auto"), Children = { label, percentage } };
    }

    public void Apply(AppSettings settings)
    {
        sample=new AppSettings{ThemeMode=settings.ThemeMode,OverlayTheme=OverlayThemeCatalog.Nika,TextSize=settings.TextSize,
            TextColor=settings.TextColor,StopwatchAppearance=settings.StopwatchAppearance.Copy(),ShowRecIndicator=settings.ShowRecIndicator};
        sample.StopwatchAppearance.Normalize();
        selected.ApplySettings(sample);unselected.ApplySettings(sample);
        Refresh();
    }

    public void SetPreviewState(bool isRunning,bool isDarkBackdrop)
    {running=isRunning;darkBackdrop=isDarkBackdrop;Refresh();}

    internal void SetTypographySample(bool active)
    {
        selected.Text = unselected.Text = active ? TypographyTime : DefaultTime;
    }

    private void Refresh()
    {
        selected.SetStopwatchPresentationState(new(true,running,false));
        unselected.SetStopwatchPresentationState(new(false,running,false));
        StopwatchOpacity.Apply(unselected,sample.StopwatchAppearance.UnselectedOpacity/100);
        StopwatchToolbarStyler.Apply(toolbar,buttons,sample.StopwatchAppearance,new(true,running,false),1);
        backdrop.Background=Brush.Parse(darkBackdrop?"#152936":"#EDE9DE");
        Foreground = Brush.Parse(darkBackdrop ? "#F0E9D6" : "#263B44");
        divider.Background = Brush.Parse(darkBackdrop ? "#49606A" : "#C7C6BB");
        inactivePercent.Text = $"{sample.StopwatchAppearance.UnselectedOpacity:0}%";
    }

    public static Control Thumbnail(string kind,string id)
    {
        if(kind=="HoverControls")return new StopwatchToolbarArtwork{Choice=id,Width=82,Height=29};
        var profile=new StopwatchAppearanceSettings();
        switch(kind){case "Background":profile.Background=id;break;case "Frame":profile.Frame=id;break;case "Typography":profile.Typography=id;break;case "Artwork":profile.Artwork=id;break;}
        var face=new ThemedClockFace{Text=kind=="Typography"?TypographyTime:DefaultTime,ProjectName=SampleProject};
        face.ApplySettings(new AppSettings{TextColor="Theme default",StopwatchAppearance=profile,OverlayTheme=OverlayThemeCatalog.Nika});
        face.SetStopwatchPresentationState(new(false,true,false));
        return new Viewbox{Child=face,Width=82,Height=29,Stretch=Stretch.Uniform};
    }

    private static Border CreateSampleToolbar(out Button[] actionButtons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        actionButtons = Enumerable.Range(0, 3).Select(_ => new Button { Focusable = false, IsHitTestVisible = false }).ToArray();
        foreach (var button in actionButtons) panel.Children.Add(button);
        return new Border { Child = panel, HorizontalAlignment = HorizontalAlignment.Center };
    }

    private static Border CreateStyledToolbar(StopwatchAppearanceSettings appearance)
    {
        var bar = CreateSampleToolbar(out var actionButtons);
        StopwatchToolbarStyler.Apply(bar, actionButtons, appearance, new(true, true, false), 1);
        return bar;
    }

    internal static Viewbox CreateToolbarThumbnail(StopwatchAppearanceSettings appearance) =>
        new() { Child = CreateStyledToolbar(appearance), Stretch = Stretch.Uniform, IsHitTestVisible = false };

    internal static Viewbox CreateGallerySample(StopwatchGalleryLayer layer, StopwatchAppearanceOption option, AppSettings settings)
    {
        var face = new ThemedClockFace { Text = layer == StopwatchGalleryLayer.Typography ? TypographyTime : DefaultTime,
            ProjectName = SampleProject };
        var appearance = ApplyGallerySample(face, layer, option, settings);
        Control content = face;
        if (layer == StopwatchGalleryLayer.Toolbar)
            content = new StackPanel { Children = { face, CreateStyledToolbar(appearance) } };
        return new Viewbox { Child = content, Stretch = Stretch.Uniform, IsHitTestVisible = false };
    }

    internal static StopwatchAppearanceSettings ApplyGallerySample(ThemedClockFace face, StopwatchGalleryLayer layer, StopwatchAppearanceOption option, AppSettings settings)
    {
        var appearance = settings.StopwatchAppearance.Copy();
        layer.Select(appearance, option.Id);
        face.ApplySettings(new AppSettings { ThemeMode = settings.ThemeMode, OverlayTheme = OverlayThemeCatalog.Nika,
            TextSize = settings.TextSize, TextColor = settings.TextColor, StopwatchAppearance = appearance });
        face.SetStopwatchPresentationState(new(layer == StopwatchGalleryLayer.Toolbar, true, false));
        return appearance;
    }
}
