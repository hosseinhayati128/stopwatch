using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace StopwatchOverlay.Desktop.Views;

internal sealed class ActiveClocksShelfWindow : Window
{
    private readonly StackPanel _rows = new() { Spacing = 4 };
    private readonly Action<TimerSession> _select;
    private readonly Action<TimerSession> _pause;
    private readonly Dictionary<Guid, (Button Select, TextBlock Time)> _controls = new();
    private Guid[]? _runningIds;

    internal ActiveClocksShelfWindow(Action<TimerSession> select, Action<TimerSession> pause)
    {
        _select = select;
        _pause = pause;
        Title = "Running clocks";
        Width = 350;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var close = new Button { Content = "Hide", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Hide();
        var layout = new StackPanel { Margin = new Thickness(10), Spacing = 6 };
        layout.Children.Add(new TextBlock { Text = "Running clocks" });
        layout.Children.Add(new ScrollViewer { Content = _rows, MaxHeight = 220 });
        layout.Children.Add(close);
        Content = layout;
    }

    internal void UpdateClocks(IReadOnlyList<TimerSession> timers, TimerSession? active, Func<TimerSession, string> format)
    {
        var running = timers.Where(timer => timer.IsRunning && timer.Mode != 1).ToArray();
        var ids = running.Select(timer => timer.Id).ToArray();
        if (_runningIds == null || !_runningIds.SequenceEqual(ids))
        {
            _runningIds = ids;
            _rows.Children.Clear();
            _controls.Clear();
            foreach (var timer in running)
            {
                var select = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
                select.Click += (_, _) => _select(timer);
                var time = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0) };
                var pause = new Button { Content = "Pause" };
                pause.Click += (_, _) => _pause(timer);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
                Grid.SetColumn(time, 1);
                Grid.SetColumn(pause, 2);
                row.Children.Add(select);
                row.Children.Add(time);
                row.Children.Add(pause);
                _rows.Children.Add(row);
                _controls.Add(timer.Id, (select, time));
            }
            if (running.Length == 0) _rows.Children.Add(new TextBlock { Text = "No running clocks" });
        }
        foreach (var timer in running)
        {
            var controls = _controls[timer.Id];
            controls.Select.Content = timer == active ? $"{timer.DisplayName} · Active" : timer.DisplayName;
            controls.Time.Text = format(timer);
        }
    }

    internal void RepositionUnder(OverlayWindow overlay)
    {
        double scale = overlay.RenderScaling;
        int x = overlay.Position.X;
        int y = overlay.Position.Y + (int)Math.Ceiling((overlay.Bounds.Height + 4) * scale);
        if (Screens.ScreenFromVisual(overlay) is { WorkingArea: var area })
        {
            int width = (int)Math.Ceiling(Width * scale);
            int height = (int)Math.Ceiling(Bounds.Height * scale);
            x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width));
            if (y + height > area.Bottom)
                y = Math.Max(area.Y, overlay.Position.Y - height - (int)(4 * scale));
        }
        var position = new PixelPoint(x, y);
        if (position != Position) Position = position;
    }
}
