using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace StopwatchOverlay.Desktop.Controls;

public partial class OverlayPositionPicker : UserControl
{
    internal static readonly string[] Positions = ["Top Left", "Top Center", "Top Right", "Bottom Left", "Bottom Center", "Bottom Right", "Custom"];
    private readonly RadioButton[] _choices = new RadioButton[Positions.Length];
    private bool _synchronizing;
    internal event Action<string>? PositionChosen;

    public OverlayPositionPicker()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        string group = Guid.NewGuid().ToString();
        var theme = (ControlTheme)Resources["positionChoice"]!;
        for (int i = 0; i < Positions.Length; i++)
        {
            string position = Positions[i];
            var choice = CustomChoice;
            if (position != "Custom")
            {
                choice = new RadioButton { Theme = theme, Content = new TextBlock { Text = position,
                    TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                Grid.SetColumn(choice, 2 * (i % 3));
                Grid.SetRow(choice, i < 3 ? 0 : 2);
                PresetGrid.Children.Add(choice);
                AutomationProperties.SetName(choice, position);
            }
            choice.GroupName = group;
            choice.IsCheckedChanged += (_, _) =>
            {
                if (!_synchronizing && choice.IsChecked == true) PositionChosen?.Invoke(position);
            };
            _choices[i] = choice;
        }
        AddHandler(KeyDownEvent, MoveSelection, RoutingStrategies.Tunnel);
    }

    internal void SetPosition(string value)
    {
        int index = Array.IndexOf(Positions, value);
        if (index < 0 || _choices[index].IsChecked == true) return;
        _synchronizing = true;
        try { _choices[index].IsChecked = true; }
        finally { _synchronizing = false; }
    }

    private void MoveSelection(object? sender, KeyEventArgs e)
    {
        if (e.Source is not RadioButton current || e.KeyModifiers != KeyModifiers.None) return;
        int index = Array.IndexOf(_choices, current);
        if (index < 0) return;
        int target = e.Key switch
        {
            Key.Left when index < 6 && index % 3 > 0 => index - 1,
            Key.Right when index < 6 && index % 3 < 2 => index + 1,
            Key.Up when index == 6 => 4,
            Key.Up when index >= 3 => index - 3,
            Key.Down when index < 3 => index + 3,
            Key.Down when index < 6 => 6,
            Key.Left or Key.Right or Key.Up or Key.Down => index,
            _ => -1
        };
        if (target < 0) return;
        _choices[target].Focus(NavigationMethod.Directional);
        _choices[target].IsChecked = true;
        e.Handled = true;
    }
}
