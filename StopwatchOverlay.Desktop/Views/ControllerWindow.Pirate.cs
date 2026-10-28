using Avalonia.Controls;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private void ApplyControllerTheme()
    {
        bool pirate = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Pirate;
        bool nika = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Nika;
        Classes.Set("pirate", pirate);
        Classes.Set("nika", nika);
        ApplyNikaControllerResources(nika);
        ApplyNikaChoices();
        if (this.FindControl<PirateActionPanel>("ControllerActions") is { } actions)
            actions.Pirate = pirate;
        UpdateControllerLayout();
    }

    private void UpdateControllerLayout()
    {
        bool nika = Classes.Contains("nika");
        double width = ClientSize.Width > 0 ? ClientSize.Width : Width;
        double height = ClientSize.Height > 0 ? ClientSize.Height : Height;
        bool compact = !nika && width < 900;
        if (nika) Resources["ControllerModeMargin"] = new Avalonia.Thickness(0,0,84,0);
        Classes.Set("compact", compact);
        Classes.Set("short", !nika && height < 780);
        double scale = nika ? Math.Max(.01, Math.Min(width / 1496d, height / 984d)) : 1;
        if (nika) Resources["NikaTimerWidth"] = Math.Max(1, (width / scale - 360) * .64);
        if (this.FindControl<Viewbox>("ControllerLayoutHost") is { } host)
        {
            // Scale the controls uniformly, but let the canvas follow the actual
            // window aspect ratio. A fixed canvas would leave outer empty bands.
            host.Stretch = nika ? Avalonia.Media.Stretch.Uniform : Avalonia.Media.Stretch.None;
        }
        if (this.FindControl<Grid>("ControllerFrame") is { } frame)
        {
            frame.ColumnDefinitions.Clear();
            frame.Width = nika ? width / scale : Math.Max(0, width - frame.Margin.Left - frame.Margin.Right);
            frame.Height = nika ? height / scale : Math.Max(0, height - frame.Margin.Top - frame.Margin.Bottom);
        }
        if (this.FindControl<Grid>("ControllerBody") is { } body)
        {
            if (this.FindControl<Border>("NikaHeader") is { } header) header.Width = double.NaN;
            body.Width = double.NaN;
            body.ColumnDefinitions[0].Width = new GridLength(nika ? 360 : compact ? 210 : Classes.Contains("pirate") ? 300 : 260);
        }
    }
}
