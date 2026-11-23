using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private void NikaMinimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void NikaMaximize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void NikaClose_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
    private void NikaHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Classes.Contains("nika") || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is Avalonia.Visual source && (source is Button || source.GetVisualAncestors().Any(parent => parent is Button))) return;
        if (e.ClickCount == 2) NikaMaximize_Click(sender, new Avalonia.Interactivity.RoutedEventArgs());
        else BeginMoveDrag(e);
        e.Handled = true;
    }

    private ResourceInclude? _nikaControllerResources;
    private void ApplyActiveTimerStyle()
    {
        bool pixel = Classes.Contains("nika") && _settings.NikaControllerPixel;
        Classes.Set("pixelController", pixel);
        Resources["ControllerPixel"] = pixel;
    }

    private void ApplyNikaControllerResources(bool enabled)
    {
        SystemDecorations = enabled ? SystemDecorations.None : SystemDecorations.Full;
        if (_nikaControllerResources is not null) Resources.MergedDictionaries.Remove(_nikaControllerResources);
        _nikaControllerResources = enabled ? ThemeResources.Load("Nika") : null;
        if (_nikaControllerResources is not null) Resources.MergedDictionaries.Add(_nikaControllerResources);
        if (this.FindControl<PirateActionPanel>("ControllerActions") is { } actions) actions.Nika = enabled;
        if (!enabled)
        {
            LapButton.Content = "Add lap";
            ResetButton.Content = "Reset";
            ToggleOverlayButton.Content = _overlaysVisible ? "Hide overlay" : "Show overlay";
            EditButton.Content = "Edit";
        }
    }

    private void UpdateNikaDetails()
    {
        if (!Classes.Contains("nika")) return;
        UpdateNavyLaps(CurrentTimer.LapTimes);
        NikaTimerCount.Text = _timerManager.Sessions.Count.ToString("00");
        LapButton.Content = "Add lap";
        ResetButton.Content = "Reset";
        ToggleOverlayButton.Content = _overlaysVisible ? "Hide overlay" : "Show overlay";
        EditButton.Content = "Edit";
    }

    private void ApplyNikaChoices()
    {
        bool nika = Classes.Contains("nika");
        Resources["ControllerProjectAction"] = nika ? "Set project" : "Set Project";
        Resources["ControllerMenuRow"] = nika ? 0 : 1;
        Resources["ControllerRailHeadingMargin"] = nika ? new Avalonia.Thickness(10,0,10,8) : new Avalonia.Thickness(0,0,0,10);
        Resources["ControllerLapHeadingFontSize"] = nika ? 30d : 13d;
        if (this.FindControl<NikaArtwork>("NikaMap") is { } map)
            map.Choice = NikaThemeCatalog.NormalizeBackground(_settings.NikaBackground);
        if (this.FindControl<NikaArtwork>("NikaMural") is { } mural)
        {
            mural.Choice = NikaThemeCatalog.NormalizeArtwork(_settings.NikaSidebarArtwork);
            mural.Opacity = mural.Choice == "sun-warrior" ? .56 : .50;
        }
        string choice = NikaThemeCatalog.NormalizeTypography(_settings.NikaTypography);
        Resources["NikaHeadingFont"] = new FontFamily(choice switch
        {
            "typewriter-log" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Acanthus/#Cascadia Mono",
            "chartmakers-script" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Almendra",
            "navigator-precision" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Playfair Display ExtraBold",
            _ => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Playfair Display ExtraBold"
        });
        Resources["NikaDisplayFont"] = choice == "navigator-precision"
            ? new FontFamily("avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Inter ExtraBold")
            : Resources["NikaHeadingFont"];
        Resources["NikaDigitsFont"] = new FontFamily(choice switch
        {
            "typewriter-log" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Acanthus/#Cascadia Mono",
            "chartmakers-script" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Almendra",
            "navigator-precision" => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Rajdhani SemiBold",
            _ => "avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Roboto Condensed Black"
        });
        Resources["NikaTimerWeight"] = choice == "navigator-precision" ? FontWeight.SemiBold : FontWeight.Black;
    }
}
