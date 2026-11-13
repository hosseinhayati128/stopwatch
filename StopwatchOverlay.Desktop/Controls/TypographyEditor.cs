using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using System.Linq;

namespace StopwatchOverlay.Desktop.Controls;

internal sealed class TypographyEditor : StackPanel
{
    internal event Action? Changed;
    internal event Action? InteractionStarted;
    internal event Action? InteractionCompleted;
    private readonly TypographyStyle _style;
    private readonly ComboBox _colors;
    private readonly TextBox _hex;
    private readonly TextBlock _validation;
    private readonly ColorPicker _picker;
    private readonly TextBlock _customLabel=new(){Text="Custom color",FontSize=11,IsVisible=false};
    private readonly string _label;
    private readonly bool _isOverride;
    private bool _syncing;

    internal TypographyEditor(TypographyStyle style, string label, bool isOverride)
    {
        _style = style;
        _label=label;_isOverride=isOverride;
        Margin = new Thickness(0, 0, 0, isOverride ? 22 : 0);
        var fields = new StackPanel { IsEnabled = !isOverride || style.Enabled, Spacing = 6 };

        if (isOverride)
        {
            var enabled = new CheckBox { Content = label, IsChecked = style.Enabled, Margin = new Thickness(0, 0, 0, 10) };
            enabled.IsCheckedChanged += (_, _) =>
            {
                style.Enabled = enabled.IsChecked == true;
                fields.IsEnabled = style.Enabled;
                Changed?.Invoke();
            };
            Children.Add(enabled);
        }

        var sizeHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var sizeLabel = new TextBlock { Text = "Font size", FontSize = 12 };
        var value = new TextBlock { Text = $"{style.SizePercent:0}%", FontSize = 12 };
        Grid.SetColumn(sizeLabel, 0);
        Grid.SetColumn(value, 1);
        sizeHeader.Children.Add(sizeLabel);
        sizeHeader.Children.Add(value);
        fields.Children.Add(sizeHeader);

        var slider = new Slider
        {
            Minimum = 75,
            Maximum = 175,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Value = style.SizePercent,
            Margin = new Thickness(0, 2, 0, 10)
        };
        slider.PropertyChanged += (_, se) =>
        {
            if (se.Property == Slider.ValueProperty)
            {
                style.SizePercent = slider.Value;
                value.Text = $"{slider.Value:0}%";
                Changed?.Invoke();
            }
        };
        // Thumb/track handlers consume input; begin before they change Value and observe handled completion too.
        slider.AddHandler(PointerPressedEvent,(_,_)=>InteractionStarted?.Invoke(),RoutingStrategies.Tunnel,handledEventsToo:true);
        slider.AddHandler(PointerReleasedEvent,(_,_)=>InteractionCompleted?.Invoke(),RoutingStrategies.Bubble,handledEventsToo:true);
        slider.PointerCaptureLost += (_, _) => InteractionCompleted?.Invoke();
        slider.AddHandler(Thumb.DragCompletedEvent,(_,_)=>InteractionCompleted?.Invoke(),handledEventsToo:true);
        slider.LostFocus+=(_,_)=>InteractionCompleted?.Invoke();
        slider.AddHandler(KeyDownEvent,(_,e)=>{if(IsAdjustmentKey(e.Key))InteractionStarted?.Invoke();},RoutingStrategies.Tunnel,handledEventsToo:true);
        slider.AddHandler(KeyUpEvent,(_,e)=>{if(IsAdjustmentKey(e.Key))InteractionCompleted?.Invoke();},RoutingStrategies.Bubble,handledEventsToo:true);
        fields.Children.Add(slider);

        fields.Children.Add(new TextBlock { Text = "Font color", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });

        _colors = new ComboBox
        {
            ItemsSource = ColorChoices(style.Color),
            SelectedItem = style.Color,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };
        fields.Children.Add(_colors);
        fields.Children.Add(_customLabel);

        var custom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _hex = new TextBox
        {
            Text = style.Color.StartsWith('#') ? style.Color : "",
            Watermark = "#RRGGBB",
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(_hex, "Custom color, e.g. #F3DEAC. Press Enter or leave the field to apply.");
        Grid.SetColumn(_hex, 0);
        custom.Children.Add(_hex);

        var picker = _picker = new ColorPicker
        {
            Margin = new Thickness(8, 0, 0, 0),
            IsAlphaEnabled = false
        };
        if (TypographySettings.TryHexColor(style.Color, out var initialColor))
        {
            picker.Color = Color.FromRgb(initialColor.R, initialColor.G, initialColor.B);
        }
        picker.ColorChanged += (_, e) =>
        {
            if(!_syncing)SetColor($"#{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}");
        };
        Grid.SetColumn(picker, 1);
        custom.Children.Add(picker);
        fields.Children.Add(custom);

        _validation = new TextBlock
        {
            Text = "Enter a valid hex color like #F3DEAC or #ABC.",
            IsVisible = false,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        if (Application.Current?.TryFindResource("WarningBrush", out var wb) == true && wb is IBrush wBrush)
            _validation.Foreground = wBrush;
        fields.Children.Add(_validation);
        DesktopTypography.SetPreserveColor(_validation,true);
        AutomationProperties.SetName(slider,"Text size");AutomationProperties.SetName(_colors,"Text color preset");AutomationProperties.SetName(_hex,"Custom text color");

        Children.Add(fields);

        _colors.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _colors.SelectedItem is string choice)
            {
                SetColor(choice);
                if (TypographySettings.TryHexColor(choice, out var c))
                {
                    picker.Color = Color.FromRgb(c.R, c.G, c.B);
                }
            }
        };

        _hex.LostFocus += (_, _) => CommitHex();
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitHex();
                e.Handled = true;
            }
        };
    }

    private void CommitHex()
    {
        if (_syncing || string.IsNullOrWhiteSpace(_hex.Text)) return;
        if (!TypographySettings.TryHexColor(_hex.Text, out var color))
        {
            _validation.IsVisible = true;
            return;
        }
        SetColor($"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private void SetColor(string color)
    {
        _syncing = true;
        _style.Color = color;
        _colors.ItemsSource=ColorChoices(color);
        _colors.SelectedItem = color;
        _hex.Text = color.StartsWith('#') ? color : "";
        if(TypographySettings.TryHexColor(color,out var rgb))_picker.Color=Color.FromRgb(rgb.R,rgb.G,rgb.B);
        _validation.IsVisible = false;
        _syncing = false;
        Changed?.Invoke();
    }
    internal void UseNavigatorStyle(bool enabled)
    {
        Classes.Set("navigatorTypography",enabled);Margin=new(0,0,0,!enabled&&_isOverride?22:0);_customLabel.IsVisible=enabled;
        foreach(var field in this.GetLogicalDescendants().OfType<TemplatedControl>().Where(c=>c is TextBox or ComboBox))field.Classes.Set("editorField",enabled);
        if(_isOverride)((CheckBox)Children[0]).Content=enabled?"Use separate text settings":_label;
    }
    private static string[] ColorChoices(string color)=>TypographySettings.ColorChoices.Contains(color)?TypographySettings.ColorChoices:TypographySettings.ColorChoices.Append(color).ToArray();
    private static bool IsAdjustmentKey(Key key)=>key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
}
