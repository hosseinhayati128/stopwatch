using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public partial class StopwatchTextSizeEditor : UserControl
{
    internal event Action<double>? ValueChanged;

    internal double Value
    {
        get => StopwatchTextSize.Value;
        set => StopwatchTextSize.Value = value;
    }

    public StopwatchTextSizeEditor()
    {
        InitializeComponent();
        StopwatchTextSize.ValueChanged += (_, _) =>
        {
            UpdateEntry();
            ValueChanged?.Invoke(Value);
        };
        DecreaseTextSize.Click += (_, _) => Value = Math.Ceiling(Value) - 1;
        IncreaseTextSize.Click += (_, _) => Value = Math.Floor(Value) + 1;
        TextSizeEntry.KeyDown += EntryKeyDown;
        TextSizeEntry.LostFocus += (_, _) => CommitEntry(leaving: true);
        UpdateEntry();
    }

    internal StopwatchTextSizeEditor(double value, bool nika) : this()
    {
        if (nika)
        {
            FontFamily = ClockPickerPresentation.Body;
            Resources["SurfaceBrush"] = Brush.Parse("#F7E8C6");
            Resources["BorderSoftBrush"] = Brush.Parse("#CBB084");
            Resources["SurfaceRaisedBrush"] = Brush.Parse("#F1DEB7");
            Resources["BorderBrush"] = Brush.Parse("#BDA174");
            Resources["DialogSurfaceBrush"] = Brush.Parse("#FFF7E5");
            Resources["PrimaryTextBrush"] = Brush.Parse("#142F3C");
            Resources["SecondaryTextBrush"] = Brush.Parse("#625139");
            Resources["DangerTextBrush"] = Brush.Parse("#8B3827");
        }
        Value = value;
    }

    private void EntryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CommitEntry(leaving: false);
        else if (e.Key == Key.Escape)
            UpdateEntry();
        else
            return;
        e.Handled = true;
    }

    private void CommitEntry(bool leaving)
    {
        if (TextSizeEntry.Text == Value.ToString(CultureInfo.CurrentCulture))
        {
            TextSizeError.IsVisible = false;
            return;
        }
        if (int.TryParse(TextSizeEntry.Text, out var size) && size >= StopwatchTextSize.Minimum && size <= StopwatchTextSize.Maximum)
        {
            Value = size;
            UpdateEntry();
        }
        else if (leaving)
            UpdateEntry();
        else
        {
            TextSizeError.IsVisible = true;
            TextSizeEntry.SelectAll();
        }
    }

    private void UpdateEntry()
    {
        TextSizeEntry.Text = Value.ToString(CultureInfo.CurrentCulture);
        TextSizeError.IsVisible = false;
        DecreaseTextSize.IsEnabled = Value > StopwatchTextSize.Minimum;
        IncreaseTextSize.IsEnabled = Value < StopwatchTextSize.Maximum;
    }
}
