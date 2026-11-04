using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Local sample state only: no timer, controller, session or persistence dependencies.</summary>
public sealed class StopwatchAppearancePreview : UserControl
{
    private readonly ThemedClockFace selected=new(){Text="00:44:27.0",ProjectName="Illustration"};
    private readonly ThemedClockFace unselected=new(){Text="00:44:27.0",ProjectName="Illustration"};
    private readonly Border backdrop=new(){Padding=new Thickness(8),CornerRadius=new CornerRadius(8)};
    private readonly Border toolbar=new(){HorizontalAlignment=HorizontalAlignment.Center};
    private readonly Button[] buttons=Enumerable.Range(0,3).Select(_=>new Button{IsHitTestVisible=false,Focusable=false}).ToArray();
    private readonly TextBlock unselectedLabel=new(){FontSize=11};
    private AppSettings sample=new(){OverlayTheme=OverlayThemeCatalog.Nika,TextColor="Theme default"};
    private bool running=true,darkBackdrop=true;

    public StopwatchAppearancePreview()
    {
        var buttonPanel=new StackPanel{Orientation=Orientation.Horizontal};foreach(var button in buttons)buttonPanel.Children.Add(button);toolbar.Child=buttonPanel;
        var selectedGroup=new StackPanel{Spacing=0};selectedGroup.Children.Add(selected);selectedGroup.Children.Add(toolbar);
        var stack=new StackPanel{Spacing=18,VerticalAlignment=VerticalAlignment.Center};
        stack.Children.Add(new TextBlock{Text="Selected · 100%",FontSize=11,Foreground=Brushes.Gray});
        stack.Children.Add(new Viewbox{Child=selectedGroup,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxHeight=190});
        stack.Children.Add(unselectedLabel);
        stack.Children.Add(new Viewbox{Child=unselected,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxHeight=150});
        backdrop.Child=stack;Content=backdrop;
        Apply(sample);
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

    private void Refresh()
    {
        selected.SetStopwatchPresentationState(new(true,running,false));
        unselected.SetStopwatchPresentationState(new(false,running,false));
        StopwatchOpacity.Apply(unselected,sample.StopwatchAppearance.UnselectedOpacity/100);
        StopwatchToolbarStyler.Apply(toolbar,buttons,sample.StopwatchAppearance,new(true,running,false),1);
        backdrop.Background=Brush.Parse(darkBackdrop?"#152936":"#EDE9DE");
        foreach(var text in ((StackPanel)backdrop.Child!).Children.OfType<TextBlock>())text.Foreground=Brush.Parse(darkBackdrop?"#D5E0DF":"#43555A");
        unselectedLabel.Text=$"Unselected · {sample.StopwatchAppearance.UnselectedOpacity:0}%";
    }

    public static Control Thumbnail(string kind,string id)
    {
        if(kind=="HoverControls")return new StopwatchToolbarArtwork{Choice=id,Width=82,Height=29};
        var profile=new StopwatchAppearanceSettings();
        switch(kind){case "Background":profile.Background=id;break;case "Frame":profile.Frame=id;break;case "Typography":profile.Typography=id;break;case "Artwork":profile.Artwork=id;break;}
        var face=new ThemedClockFace{Text=kind=="Typography"?"12:34:56.0":"00:44:27.0",ProjectName="Illustration"};
        face.ApplySettings(new AppSettings{TextColor="Theme default",StopwatchAppearance=profile,OverlayTheme=OverlayThemeCatalog.Nika});
        face.SetStopwatchPresentationState(new(false,true,false));
        return new Viewbox{Child=face,Width=82,Height=29,Stretch=Stretch.Uniform};
    }
}
