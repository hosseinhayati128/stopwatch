using System;
using Avalonia;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ShortcutCommandHintWindow : Window
{
    public ShortcutCommandHintWindow()
    {
        InitializeComponent();
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
