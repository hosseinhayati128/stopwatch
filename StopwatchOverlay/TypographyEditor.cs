using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace StopwatchOverlay;

/// <summary>Shared global/panel editor. Disabling an override retains its saved choices.</summary>
internal sealed class TypographyEditor : StackPanel
{
    internal event Action? Changed;
    internal event Action? InteractionStarted;
    internal event Action? InteractionCompleted;
    private readonly TypographyStyle _style;
    private readonly ComboBox _colors;
    private readonly TextBox _hex;
    private readonly TextBlock _validation;
    private bool _syncing;

    internal TypographyEditor(TypographyStyle style, string label, bool isOverride)
    {
        _style = style;
        Margin = new Thickness(0, 0, 0, isOverride ? 22 : 0);
        var fields = new StackPanel { IsEnabled = !isOverride || style.Enabled };
        if (isOverride)
        {
            var enabled = new CheckBox { Content = label, IsChecked = style.Enabled, Margin = new Thickness(0, 0, 0, 10) };
            enabled.Checked += (_, _) => { style.Enabled = true; fields.IsEnabled = true; Changed?.Invoke(); };
            enabled.Unchecked += (_, _) => { style.Enabled = false; fields.IsEnabled = false; Changed?.Invoke(); };
            Children.Add(enabled);
        }
        var sizeHeader = new DockPanel();
        var value = new TextBlock { Text = $"{style.SizePercent:0}%", HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(value, Dock.Right);
        sizeHeader.Children.Add(value);
        sizeHeader.Children.Add(new TextBlock { Text = "Font size" });
        fields.Children.Add(sizeHeader);
        var slider = new Slider { Minimum = 75, Maximum = 175, TickFrequency = 5, IsSnapToTickEnabled = true,
            Value = style.SizePercent, Margin = new Thickness(0, 6, 0, 14) };
        slider.SetResourceReference(StyleProperty, "PirateSettingsSlider");
        System.Windows.Automation.AutomationProperties.SetName(slider, label + " font size");
        slider.ValueChanged += (_, _) => { style.SizePercent = slider.Value; value.Text = $"{slider.Value:0}%"; Changed?.Invoke(); };
        slider.PreviewMouseLeftButtonDown += (_, _) => InteractionStarted?.Invoke();
        slider.PreviewMouseLeftButtonUp += (_, _) => InteractionCompleted?.Invoke();
        slider.LostMouseCapture += (_, _) => InteractionCompleted?.Invoke();
        slider.LostKeyboardFocus += (_, _) => InteractionCompleted?.Invoke();
        slider.PreviewKeyDown += (_, e) => { if (IsAdjustment(e.Key)) InteractionStarted?.Invoke(); };
        slider.PreviewKeyUp += (_, e) => { if (IsAdjustment(e.Key)) InteractionCompleted?.Invoke(); };
        fields.Children.Add(slider);
        fields.Children.Add(new TextBlock { Text = "Font color", Margin = new Thickness(0, 0, 0, 6) });
        _colors = new ComboBox { ItemsSource = TypographySettings.ColorChoices, SelectedItem = style.Color, Margin = new Thickness(0, 0, 0, 8) };
        System.Windows.Automation.AutomationProperties.SetName(_colors, label + " font color preset");
        fields.Children.Add(_colors);
        var custom = new DockPanel();
        var picker = new Button { Content = "Choose…", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        picker.SetResourceReference(StyleProperty, "ModernButton");
        System.Windows.Automation.AutomationProperties.SetName(picker, label + " custom color picker");
        DockPanel.SetDock(picker, Dock.Right);
        custom.Children.Add(picker);
        _hex = new TextBox { Text = style.Color.StartsWith('#') ? style.Color : "", MinWidth = 85,
            ToolTip = "Custom color, e.g. #F3DEAC. Press Enter or leave the field to apply.", VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(_hex, label + " custom color hex");
        custom.Children.Add(_hex);
        fields.Children.Add(custom);
        _validation = new TextBlock { Text = "Enter a color such as #F3DEAC or #ABC.", Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
        fields.Children.Add(_validation);
        Children.Add(fields);
        _colors.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _colors.SelectedItem is string choice) SetColor(choice);
        };
        _hex.LostKeyboardFocus += (_, _) => CommitHex();
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; } };
        picker.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
            Brush? current = TypographyManager.ResolveBrush(this, style.Color) ?? TryFindResource("PrimaryTextBrush") as Brush;
            if (current is SolidColorBrush solid)
                dialog.Color = System.Drawing.Color.FromArgb(solid.Color.R, solid.Color.G, solid.Color.B);
            var owner = Window.GetWindow(this);
            var result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(owner).Handle));
            if (result == System.Windows.Forms.DialogResult.OK)
                SetColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        };
    }

    private static bool IsAdjustment(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End;

    private void CommitHex()
    {
        if (_syncing || string.IsNullOrWhiteSpace(_hex.Text)) return;
        if (!TypographySettings.TryHexColor(_hex.Text, out var color)) { _validation.Visibility = Visibility.Visible; return; }
        SetColor($"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private void SetColor(string color)
    {
        _syncing = true;
        _style.Color = color;
        _colors.SelectedItem = color;
        _hex.Text = color.StartsWith('#') ? color : "";
        _validation.Visibility = Visibility.Collapsed;
        _syncing = false;
        Changed?.Invoke();
    }

    private sealed class DialogOwner(IntPtr handle) : System.Windows.Forms.IWin32Window { public IntPtr Handle => handle; }
}
