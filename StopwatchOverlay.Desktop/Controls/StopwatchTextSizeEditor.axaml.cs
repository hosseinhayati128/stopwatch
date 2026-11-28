using Avalonia.Controls;

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
        Stepper.Connect(StopwatchTextSize, TextSizeError, "TextSize", "Text size", percent: false);
        StopwatchTextSize.ValueChanged += (_, _) => ValueChanged?.Invoke(Value);
    }

    internal StopwatchTextSizeEditor(double value, bool nika) : this()
    {
        if (nika)
        {
            FontFamily = ClockPickerPresentation.Body;
            StopwatchValueStepper.ApplyNikaPalette(this);
        }
        Value = value;
    }
}
