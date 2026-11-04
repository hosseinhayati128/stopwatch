using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutCommandHintWindow : Window
{
    public ShortcutCommandHintWindow() : this(AppSettings.DefaultCommandModeKeys()) { }

    internal ShortcutCommandHintWindow(IReadOnlyDictionary<ShortcutAction, uint> keys)
    {
        InitializeComponent();
        string Format(params (ShortcutAction Action, string Label)[] commands) =>
            string.Join(" · ", commands
                .Where(command => keys.TryGetValue(command.Action, out uint key) && key != 0)
                .Select(command => $"{Shortcut.FormatKeyName(keys[command.Action])} {command.Label}"));

        TimerCommandsFirst.Text = Format(
            (ShortcutAction.StartStop, "Start/Stop"), (ShortcutAction.Reset, "Reset"),
            (ShortcutAction.ToggleOverlay, "Overlay"), (ShortcutAction.Lap, "Lap"), (ShortcutAction.OpenController, "Controller"));
        TimerCommandsSecond.Text = Format(
            (ShortcutAction.ToggleClock, "Clock"), (ShortcutAction.NewTimer, "New"), (ShortcutAction.NextTimer, "Next"),
            (ShortcutAction.CloseTimer, "Close"), (ShortcutAction.RenameTimer, "Project"), (ShortcutAction.OpenDashboard, "Dashboard"),
            (ShortcutAction.EditTimer, "Edit"), (ShortcutAction.UndoTimerEdit, "Undo"),
            (ShortcutAction.PeriodicReview, "Review"), (ShortcutAction.AddRecord, "Record"), (ShortcutAction.SyncActivityWatch, "Sync"));
        TimerCommandsThird.Text = Format(
            (ShortcutAction.SeparateOverlay, "Separate"), (ShortcutAction.MergeOverlay, "Merge"),
            (ShortcutAction.NextSeparatedOverlay, "Switch"), (ShortcutAction.ShowAllClocks, "Clocks"), (ShortcutAction.ToggleActiveClocks, "Active"));
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        PositionAtTopCenter();
    }

    private void PositionAtTopCenter()
    {
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (screen != null)
        {
            var workArea = screen.WorkingArea;
            double scaling = screen.Scaling;
            double width = Bounds.Width > 0 ? Bounds.Width : 480;
            int pixelWidth = (int)(width * scaling);
            int x = workArea.X + (workArea.Width - pixelWidth) / 2;
            int y = workArea.Y + (int)(24 * scaling);
            Position = new PixelPoint(x, y);
        }
    }
}
