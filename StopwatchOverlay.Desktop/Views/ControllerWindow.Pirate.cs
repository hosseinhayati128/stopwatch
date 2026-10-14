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
        if (this.FindControl<Avalonia.Controls.Grid>("ControllerBody") is { } body)
            body.ColumnDefinitions = new ColumnDefinitions(pirate ? "300,*" : "260,*");
    }
}
