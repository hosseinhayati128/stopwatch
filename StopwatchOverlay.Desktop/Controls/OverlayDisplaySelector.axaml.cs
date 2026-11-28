using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace StopwatchOverlay.Desktop.Controls;

internal readonly record struct OverlayDisplay(string? Name, int Width, int Height, bool IsPrimary);

public partial class OverlayDisplaySelector : UserControl
{
    private readonly string _group = Guid.NewGuid().ToString();
    private readonly List<RadioButton> _choices = new();
    private bool _synchronizing;
    internal event Action<int>? DisplayChosen;

    public OverlayDisplaySelector()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        AddChoice(AllDisplays);
        SizeChanged += (_, _) => FitColumns();
        AddHandler(KeyDownEvent, MoveSelection, RoutingStrategies.Tunnel);
    }

    internal void SetDisplays(IReadOnlyList<OverlayDisplay> displays, int selected)
    {
        Monitors.Children.Clear();
        _choices.RemoveRange(1, _choices.Count - 1);
        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            string role = display.IsPrimary ? "Primary" : "Secondary";
            string resolution = $"{display.Width} × {display.Height}";
            var number = new TextBlock { Text = (i + 1).ToString(), FontSize = 13, Foreground = Brushes.Cornsilk,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var icon = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                new Border { Classes = { "monitor" }, Height = 23, CornerRadius = new(3), BorderThickness = new(1),
                    Background = MonitorFill, Child = number },
                new Border { Classes = { "stand" }, Width = 13, Height = 2, CornerRadius = new(1), HorizontalAlignment = HorizontalAlignment.Center }
            } };
            var caption = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = role },
                new TextBlock { Text = resolution, Classes = { "resolution" }, FontWeight = FontWeight.Normal, FontSize = 12 }
            } };
            Grid.SetColumn(caption, 2);
            var tile = new RadioButton { Theme = (ControlTheme)Resources["displayChoice"]!, Margin = new(3,0,3,4),
                Content = new Grid { ColumnDefinitions = new("36,8,*"), Children = { icon, caption } } };
            AutomationProperties.SetName(tile, $"Display {i + 1}, {role}, {resolution}");
            if (!string.IsNullOrWhiteSpace(display.Name)) ToolTip.SetTip(tile, display.Name);
            AddChoice(tile);
            Monitors.Children.Add(tile);
        }
        Monitors.IsVisible = displays.Count > 0;
        FitColumns();
        SetSelection(selected);
    }

    private static readonly IBrush MonitorFill = Brush.Parse("#264B58");

    private void FitColumns() => Monitors.Columns = _choices.Count > 2 && Bounds.Width >= 340 ? 2 : 1;

    private void AddChoice(RadioButton choice)
    {
        int index = _choices.Count;
        choice.GroupName = _group;
        choice.IsCheckedChanged += (_, _) =>
        {
            if (!_synchronizing && choice.IsChecked == true) DisplayChosen?.Invoke(index);
        };
        _choices.Add(choice);
    }

    internal void SetSelection(int index)
    {
        if (index < 0 || index >= _choices.Count || _choices[index].IsChecked == true) return;
        _synchronizing = true;
        try { _choices[index].IsChecked = true; }
        finally { _synchronizing = false; }
    }

    private void MoveSelection(object? sender, KeyEventArgs e)
    {
        if (e.Source is not RadioButton current || e.KeyModifiers != KeyModifiers.None) return;
        int index = _choices.IndexOf(current);
        if (index < 0) return;
        int columns = Monitors.Columns;
        int last = _choices.Count - 1;
        int target = e.Key switch
        {
            Key.Down when index == 0 => Math.Min(1, last),
            Key.Down when (index - 1) / columns < (last - 1) / columns => Math.Min(index + columns, last),
            Key.Up => Math.Max(0, index - columns),
            Key.Left when index > 0 && (index - 1) % columns > 0 => index - 1,
            Key.Right when index > 0 && index < last && index % columns != 0 => index + 1,
            Key.Down or Key.Left or Key.Right => index,
            _ => -1
        };
        if (target < 0) return;
        _choices[target].Focus(NavigationMethod.Directional);
        _choices[target].IsChecked = true;
        e.Handled = true;
    }
}
