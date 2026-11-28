using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public partial class StopwatchValueStepper : UserControl
{
    public StopwatchValueStepper() => InitializeComponent();

    internal static void ApplyNikaPalette(Control control)
    {
        control.Resources["SurfaceBrush"] = Brush.Parse("#F7E8C6");
        control.Resources["BorderSoftBrush"] = Brush.Parse("#CBB084");
        control.Resources["SurfaceRaisedBrush"] = Brush.Parse("#F1DEB7");
        control.Resources["BorderBrush"] = Brush.Parse("#BDA174");
        control.Resources["DialogSurfaceBrush"] = Brush.Parse("#FFF7E5");
        control.Resources["PrimaryTextBrush"] = Brush.Parse("#142F3C");
        control.Resources["SecondaryTextBrush"] = Brush.Parse("#625139");
        control.Resources["DangerTextBrush"] = Brush.Parse("#8B3827");
    }

    internal void Connect(Slider slider, TextBlock error, string key, string title, bool percent)
    {
        Entry.Name = key + "Entry";
        Decrease.Name = "Decrease" + key;
        Increase.Name = "Increase" + key;
        Unit.Text = percent ? "%" : "px";
        Entry.Width = percent ? 70 : 72;
        string unit = percent ? "percentage point" : "pixel";
        string label = title.ToLowerInvariant();
        AutomationProperties.SetName(Entry, percent ? title + " percentage" : title + " in pixels");
        AutomationProperties.SetName(Decrease, $"Decrease {label} by one {unit}");
        AutomationProperties.SetName(Increase, $"Increase {label} by one {unit}");

        slider.ValueChanged += (_, _) => Sync();
        Decrease.Click += (_, _) => slider.Value = Math.Ceiling(slider.Value) - 1;
        Increase.Click += (_, _) => slider.Value = Math.Floor(slider.Value) + 1;
        Entry.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Commit(leaving: false);
            else if (e.Key == Key.Escape) Sync();
            else return;
            e.Handled = true;
        };
        Entry.LostFocus += (_, _) => Commit(leaving: true);
        Sync();

        void Commit(bool leaving)
        {
            if (Entry.Text == slider.Value.ToString(CultureInfo.CurrentCulture))
            {
                error.IsVisible = false;
                return;
            }
            if (int.TryParse(Entry.Text, out int value) && value >= slider.Minimum && value <= slider.Maximum)
            {
                slider.Value = value;
                Sync();
            }
            else if (leaving) Sync();
            else
            {
                error.IsVisible = true;
                Entry.SelectAll();
            }
        }

        void Sync()
        {
            Entry.Text = slider.Value.ToString(CultureInfo.CurrentCulture);
            error.IsVisible = false;
            Decrease.IsEnabled = slider.Value > slider.Minimum;
            Increase.IsEnabled = slider.Value < slider.Maximum;
        }
    }
}
