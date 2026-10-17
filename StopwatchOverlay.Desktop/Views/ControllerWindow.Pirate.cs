using Avalonia.Controls;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private void ApplyControllerTheme()
    {
        bool pirate = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Pirate;
        Classes.Set("pirate", pirate);
        if (this.FindControl<PirateActionPanel>("ControllerActions") is { } actions)
            actions.Pirate = pirate;
        UpdateControllerLayout();
    }

    private void UpdateControllerLayout()
    {
        bool compact = (Bounds.Width > 0 ? Bounds.Width : Width) < 900;
        Classes.Set("compact", compact);
        if (this.FindControl<Grid>("ControllerBody") is { } body)
            body.ColumnDefinitions[0].Width = new GridLength(compact ? 210 : Classes.Contains("pirate") ? 300 : 260);
    }
}
