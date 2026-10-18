using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Automation;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Controls;

internal static class TodoProgressBadge
{
    internal static Border Create(Action toggle)
    {
        var symbol = new ShapePath
        {
            Data = Geometry.Parse("M8,1 A7,7 0 1 1 7.999,1 M6,4.5 L11,8 L6,11.5 Z"),
            Width = 14, Height = 14, Stretch = Stretch.Uniform,
            StrokeThickness = 1.4, Fill = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var badge = new Border { Name = "InProgressBadge", Child = symbol, Width = 18, Height = 14,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(badge, "In progress");
        badge.Classes.Add("todoInProgress");
        ToolTip.SetTip(badge, "In progress · Double-click to toggle off");
        badge.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.Handled || e.ClickCount != 2 || !e.GetCurrentPoint(badge).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            toggle();
        }, RoutingStrategies.Bubble);
        return badge;
    }
}
