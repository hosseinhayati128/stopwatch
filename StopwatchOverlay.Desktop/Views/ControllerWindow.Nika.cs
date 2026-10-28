using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml.Styling;
using System.Globalization;
using System.Text.RegularExpressions;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public sealed record NikaLapRow(string Number, string Split, string Total, IBrush Background);

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

    internal static IReadOnlyList<NikaLapRow> BuildNikaLaps(IEnumerable<string> values)
    {
        var parsed = values.Select(raw => {
            var match = Regex.Match(raw, @"^Lap\s+(\d+):\s*(\d+):(\d{2}):(\d{2})(?:[.](\d+))?$", RegexOptions.CultureInvariant);
            TimeSpan? total = null;
            if (match.Success && long.TryParse(match.Groups[2].Value, out long hours) && hours <= 200000000
                && int.TryParse(match.Groups[3].Value, out int minutes) && minutes < 60
                && int.TryParse(match.Groups[4].Value, out int seconds) && seconds < 60
                && double.TryParse("0." + match.Groups[5].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double fraction))
            {
                try { total = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds + fraction); }
                catch (OverflowException) { }
            }
            return (Raw: raw, Number: match.Success ? match.Groups[1].Value : "—", Total: total);
        }).ToArray();
        string Format(TimeSpan value) => $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        return parsed.Select((row, index) => {
            TimeSpan? previous = index + 1 < parsed.Length ? parsed[index + 1].Total : TimeSpan.Zero;
            TimeSpan? split = row.Total.HasValue && previous.HasValue && row.Total >= previous ? row.Total - previous : null;
            return new NikaLapRow(row.Number == "—" ? "—" : row.Number.PadLeft(2, '0'), split.HasValue ? Format(split.Value) : "—",
                row.Total.HasValue ? Format(row.Total.Value) : row.Raw, index == 0 ? Brush.Parse("#55B58D4D") : Brushes.Transparent);
        }).ToArray();
    }

    private void NikaRailMenu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TimerRailItemViewModel timer } button) return;
        var session = _timerManager.Sessions.FirstOrDefault(item => item.Id == timer.Id);
        if (session is null) return;
        _timerManager.Activate(session);
        TimerRailList.SelectedItem = timer;
        UpdateActiveTimerDisplay();
        UpdateOverlayStates();
        MenuItem Command(string label, EventHandler<Avalonia.Interactivity.RoutedEventArgs> action)
        {
            var item = new MenuItem { Header = label, Background = Brush.Parse("#032437"), Foreground = Brush.Parse("#FFF1D9") };
            item.Click += action;
            return item;
        }
        var menu = new ContextMenu
        {
            Background = Brush.Parse("#032437"), Foreground = Brush.Parse("#FFF1D9"),
            ItemsSource = new object[] {
                Command("Start / Pause", StartStopButton_Click),
                Command("Add lap", LapButton_Click),
                Command("Reset", ResetButton_Click),
                Command("Show / Hide overlay", ToggleOverlayButton_Click),
                new Separator(),
                Command("Edit timer...", EditTimerMenuItem_Click),
                Command("Set project...", RenameTimerMenuItem_Click),
                Command(session.IsSeparated ? "Merge with herd overlay" : "Separate clock overlay", ToggleCombinedOverlayMenuItem_Click),
                Command("Close timer", CloseTimerMenuItem_Click)
            }
        };
        menu.Classes.Add("nikaRailMenu");
        button.ContextMenu = menu;
        menu.Open(button);
        e.Handled = true;
    }

    private string[] _nikaLapSnapshot = [];
    private void UpdateNikaDetails()
    {
        if (!Classes.Contains("nika")) return;
        if (!_nikaLapSnapshot.SequenceEqual(CurrentTimer.LapTimes))
        {
            _nikaLapSnapshot = CurrentTimer.LapTimes.ToArray();
            NikaLapTable.ItemsSource = BuildNikaLaps(_nikaLapSnapshot);
        }
        NikaTimerCount.Text = _timerManager.Sessions.Count.ToString("00");
        NikaLapCount.Text = $"{CurrentTimer.LapTimes.Count} laps";
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
        Resources["ControllerModeSpacing"] = nika ? 0d : 12d;
        Resources["ControllerModeMargin"] = nika ? new Avalonia.Thickness(0,0,84,0) : new Avalonia.Thickness(0,0,0,16);
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
        Resources["NikaLapDigitsFont"] = choice == "original-classic"
            ? new FontFamily("avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Barlow Condensed")
            : Resources["NikaDigitsFont"];
    }
}
