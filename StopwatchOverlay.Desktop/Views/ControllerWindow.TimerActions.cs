using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private ContextMenu? _openTimerActions;

    private readonly record struct TimerActionsState(TimerSession Timer, string State, bool OverlaysVisible, bool Separated);

    private TimerActionsState GetTimerActionsState(TimerSession session) =>
        new(session, TimerRailItemViewModel.StateOf(session), _overlaysVisible, session.IsSeparated);

    private void DismissStaleTimerActions(TimerSession active)
    {
        if (_openTimerActions?.Tag is TimerActionsState snapshot && snapshot != GetTimerActionsState(active))
            _openTimerActions.Close();
    }

    private void NikaRailMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TimerRailItemViewModel timer } button ||
            button.FindAncestorOfType<ListBoxItem>() is not { } row || !_timerManager.Sessions.Contains(timer.Session)) return;

        e.Handled = true;
        if (button.ContextMenu is { IsOpen: true }) return;
        _openTimerActions?.Close();
        if (!ReferenceEquals(_timerManager.Active, timer.Session))
            TimerRailList.SelectedItem = timer;
        else
            UpdateActiveTimerDisplay();
        var session = timer.Session;
        var snapshot = GetTimerActionsState(session);
        double scale = DesktopPageAppearance.Scale(this);
        var menu = new ContextMenu
        {
            Theme = (ControlTheme)this.FindResource("ControllerTimerActionsMenu")!,
            PlacementTarget = button,
            Placement = PlacementMode.Custom,
            Tag = snapshot,
            CustomPopupPlacementCallback = placement =>
            {
                var buttonTop = button.TranslatePoint(default, this);
                var buttonBottom = button.TranslatePoint(new Point(0, button.Bounds.Height), this);
                var rowEnd = row.TranslatePoint(new Point(row.Bounds.Width, 0), this);
                if (buttonTop == null || buttonBottom == null || rowEnd == null) return;
                double gap = Math.Max(3, 4 * scale);
                placement.AnchorRectangle = new Rect(buttonTop.Value.X - gap, buttonTop.Value.Y,
                    rowEnd.Value.X - buttonTop.Value.X + 2 * gap, buttonBottom.Value.Y - buttonTop.Value.Y);
                placement.Anchor = PopupAnchor.TopRight;
                placement.Gravity = PopupGravity.BottomRight;
                placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY | PopupPositionerConstraintAdjustment.FlipX |
                    PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY |
                    PopupPositionerConstraintAdjustment.ResizeY;
            }
        };
        menu.Resources["ControllerPixel"] = _settings.NikaControllerPixel;
        menu.Resources["TimerActionsZoom"] = new ScaleTransform(scale, scale);
        menu.Resources["TimerActionsWidth"] = Math.Max(224, 172 / scale);
        menu.Resources["TimerActionsTextSize"] = Math.Max(16, 12.5 / scale);
        var actionTheme = (ControlTheme)this.FindResource("ControllerTimerAction")!;
        var dividerTheme = (ControlTheme)this.FindResource("ControllerTimerActionDivider")!;

        void Divider() => menu.Items.Add(new Separator { Theme = dividerTheme });
        MenuItem Command(string title, string icon, EventHandler<RoutedEventArgs> handler)
        {
            var item = new MenuItem { Header = title, Icon = icon, Theme = actionTheme };
            item.Classes.Add("timerAction");
            AutomationProperties.SetName(item, title);
            item.Click += (source, args) =>
            {
                menu.Close();
                if (_timerManager.Active == session && snapshot == GetTimerActionsState(session))
                    handler(source, args);
            };
            menu.Items.Add(item);
            return item;
        }

        if (session.Mode != 1)
        {
            string transport = (string)StartStopButton.Content!;
            Command(transport, transport, StartStopButton_Click);
            Command("Add lap", "Add lap", LapButton_Click);
            Command("Reset", "Reset", ResetButton_Click);
            Divider();
        }
        Command(_overlaysVisible ? "Hide timer overlays" : "Show timer overlays", "Hide overlay", ToggleOverlayButton_Click);
        Command(session.IsSeparated ? "Combine overlays" : "Separate overlay", session.IsSeparated ? "Combine" : "Separate", ToggleCombinedOverlayMenuItem_Click);
        Divider();
        Command("Edit timer…", "Edit", EditTimerMenuItem_Click);
        Command("Set project…", "Project", RenameTimerMenuItem_Click);
        Divider();
        Command("Close timer…", "Close", CloseTimerMenuItem_Click).Classes.Add("closeTimer");

        menu.Opened += (_, _) =>
        {
            menu.SelectedIndex = 0;
            ((MenuItem)menu.Items[0]!).Focus();
        };
        menu.Closed += (_, _) =>
        {
            button.Classes.Remove("actionsOpen");
            if (ReferenceEquals(button.ContextMenu, menu)) button.ContextMenu = null;
            if (ReferenceEquals(_openTimerActions, menu)) _openTimerActions = null;
        };
        _openTimerActions = menu;
        button.Classes.Add("actionsOpen");
        button.ContextMenu = menu;
        menu.Open(button);
    }
}
