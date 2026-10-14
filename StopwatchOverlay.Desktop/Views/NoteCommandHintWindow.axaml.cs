using System;
using System.Collections.Generic;
using System.Linq;
using StopwatchOverlay;
using Avalonia;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteCommandHintWindow : Window
{
    public NoteCommandHintWindow() : this(null) { }

    public NoteCommandHintWindow(IReadOnlyDictionary<NoteCommandAction, uint>? keys)
    {
        InitializeComponent();
        keys ??= AppSettings.DefaultNoteCommandModeKeys();
        var options = new[] {
            (NoteCommandAction.AddTodo, "Todo"), (NoteCommandAction.AddNote, "Quick Note"),
            (NoteCommandAction.ViewNotes, "View Notes"), (NoteCommandAction.AddReminder, "Reminder")
        };
        NoteOptions.Text = string.Join(" · ", options.Select(option =>
            $"{(keys.TryGetValue(option.Item1, out var key) && key != 0 ? Shortcut.FormatKeyName(key) : "—")} {option.Item2}"));
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
            double width = Bounds.Width > 0 ? Bounds.Width : 340;
            int pixelWidth = (int)(width * scaling);
            int x = workArea.X + (workArea.Width - pixelWidth) / 2;
            int y = workArea.Y + (int)(24 * scaling);
            Position = new PixelPoint(x, y);
        }
    }
}
