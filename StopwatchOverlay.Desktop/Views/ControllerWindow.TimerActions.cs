using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private ContextMenu? _openTimerActions;

    private readonly record struct TimerActionsState(TimerSession? Timer, string? Name, string State, bool OverlaysVisible,
        bool Separated, int Mode, int PreviousMode, bool CanStart, bool HasNext);

    private TimerActionsState GetTimerActionsState()
    {
        var session = _timerManager.Active;
        return new(session, session?.Name, session == null ? "" : TimerRailItemViewModel.StateOf(session), _overlaysVisible,
            session?.IsSeparated == true, session?.Mode ?? -1, session?.LastNonClockMode ?? 0, StartStopButton.IsEnabled, _timerManager.Count > 1);
    }

    private void DismissStaleTimerActions()
    {
        if (_openTimerActions?.Tag is TimerActionsState snapshot && snapshot != GetTimerActionsState())
            _openTimerActions.Close();
    }

    private void NavyTimers_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (NavyTimersButton.ContextMenu is { IsOpen: true } open) open.Close();
        else OpenTimerActions(NavyTimersButton);
    }

    private void NavyTimers_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && NavyTimersButton.ContextMenu?.IsOpen != true) NavyTimers_Click(sender, e);
    }

    private Separator TimerMenuHeading(string name, string state)
    {
        var heading = new Separator { Theme = (ControlTheme)this.FindResource("TimerMenuHeading")!, Tag = name, DataContext = state };
        heading.Classes.Set("running", state is "Running" or "Finished");
        heading.Classes.Set("clock", state == "Clock");
        return heading;
    }

    private Separator TimerManagementHeading() =>
        new() { Theme = (ControlTheme)this.FindResource("TimerManagementHeading")! };

    private void NikaRailMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TimerRailItemViewModel timer } button ||
            button.FindAncestorOfType<ListBoxItem>() is not { } row || !_timerManager.Sessions.Contains(timer.Session)) return;

        e.Handled = true;
        if (button.ContextMenu is { IsOpen: true }) return;
        if (!ReferenceEquals(_timerManager.Active, timer.Session))
            TimerRailList.SelectedItem = timer;
        else
            UpdateActiveTimerDisplay();
        OpenTimerActions(button, row);
    }

    private void OpenTimerActions(Button button, ListBoxItem? row = null)
    {
        _openTimerActions?.Close();
        var snapshot = GetTimerActionsState();
        bool header = row == null;
        double scale = DesktopPageAppearance.Scale(this);
        var menu = new ContextMenu
        {
            Theme = (ControlTheme)this.FindResource("ControllerTimerActionsMenu")!,
            PlacementTarget = button,
            Placement = header ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.Custom,
            VerticalOffset = header ? 3 : 0,
            Tag = snapshot
        };
        if (row != null)
        {
            menu.CustomPopupPlacementCallback = placement =>
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
            };
        }
        menu.Resources["ControllerPixel"] = _settings.NikaControllerPixel;
        menu.Resources["TimerActionsZoom"] = new ScaleTransform(scale, scale);
        menu.Resources["TimerActionsWidth"] = header ? 220d : Math.Max(224, 172 / scale);
        menu.Resources["TimerActionsTextSize"] = header ? Math.Max(15, 13 / scale) : Math.Max(16, 12.5 / scale);
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
                if (snapshot == GetTimerActionsState())
                    handler(source, args);
            };
            menu.Items.Add(item);
            return item;
        }

        if (snapshot.Timer is { } session)
        {
            if (header) menu.Items.Add(TimerMenuHeading(session.DisplayName, snapshot.State));
            if (session.Mode != 1)
            {
                string transport = (string)StartStopButton.Content!;
                Command(transport, transport, StartStopButton_Click).IsEnabled = snapshot.CanStart;
                Command("Add lap", "Add lap", LapButton_Click);
                Command("Reset", "Reset", ResetButton_Click);
                if (!header) Divider();
            }
            else if (header)
                Command(ClockReturnLabel(session), "Stopwatch", (_, _) => ToggleClockMode());
            Command(_overlaysVisible ? "Hide timer overlays" : "Show timer overlays", "Hide overlay", ToggleOverlayButton_Click);
            Command(session.IsSeparated ? "Combine overlays" : "Separate overlay", session.IsSeparated ? "Combine" : "Separate", ToggleCombinedOverlayMenuItem_Click);
            if (!header) Divider();
            Command("Edit timer…", "Edit", EditTimerMenuItem_Click);
            Command("Set project…", "Project", RenameTimerMenuItem_Click);
            if (!header) Divider();
            Command("Close timer…", "Close", CloseTimerMenuItem_Click).Classes.Add("closeTimer");
        }
        if (header)
        {
            if (snapshot.Timer != null) menu.Items.Add(TimerManagementHeading());
            Command("New timer…", "New timer", NewTimerMenuItem_Click);
            if (snapshot.Timer != null) Command("Next active timer", "Next", NextTimerMenuItem_Click).IsEnabled = snapshot.HasNext;
        }

        menu.Opened += (_, _) =>
        {
            var first = menu.Items.OfType<MenuItem>().First(item => item.IsEnabled);
            menu.SelectedIndex = menu.Items.IndexOf(first);
            first.Focus();
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
