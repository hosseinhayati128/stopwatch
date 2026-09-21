using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace StopwatchOverlay.Themes;

/// <summary>Inherited artwork opt-in; it never changes timer or overlay state.</summary>
public static class PirateVisual
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PirateVisual), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    public static readonly DependencyProperty SelectedProperty = DependencyProperty.RegisterAttached(
        "Selected", typeof(bool), typeof(PirateVisual), new PropertyMetadata(false));
    public static bool GetSelected(DependencyObject target) => (bool)target.GetValue(SelectedProperty);
    public static void SetSelected(DependencyObject target, bool value) => target.SetValue(SelectedProperty, value);

    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Brush), typeof(PirateVisual));
    public static Brush? GetIcon(DependencyObject target) => (Brush?)target.GetValue(IconProperty);
    public static void SetIcon(DependencyObject target, Brush value) => target.SetValue(IconProperty, value);

    public static readonly DependencyProperty EndIconProperty = DependencyProperty.RegisterAttached(
        "EndIcon", typeof(Brush), typeof(PirateVisual));
    public static Brush? GetEndIcon(DependencyObject target) => (Brush?)target.GetValue(EndIconProperty);
    public static void SetEndIcon(DependencyObject target, Brush value) => target.SetValue(EndIconProperty, value);
}

/// <summary>Preserves the original wrapping layout outside Pirate.</summary>
public sealed class ControllerActionPanel : WrapPanel
{
    protected override Size MeasureOverride(Size constraint)
    {
        if (!PirateVisual.GetEnabled(this)) return base.MeasureOverride(constraint);
        double width = double.IsFinite(constraint.Width) ? constraint.Width : 680;
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(Math.Max(0, (width - 14) / 2), 76));
        return new Size(width, 154);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!PirateVisual.GetEnabled(this)) return base.ArrangeOverride(finalSize);
        double width = Math.Max(0, (finalSize.Width - 14) / 2);
        // Existing child and keyboard order: Start, Lap, Reset, Overlay.
        // Reference placement: Start/Reset above Lap/Overlay.
        int[] slots = [0, 2, 1, 3];
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            int slot = i < slots.Length ? slots[i] : i;
            InternalChildren[i].Arrange(new Rect(slot % 2 * (width + 14), slot / 2 * 80, width, 74));
        }
        return finalSize;
    }
}

/// <summary>Keeps navigation readable when the controller's rail is collapsed.</summary>
public sealed class ControllerHeaderPanel : Panel
{
    private bool _stacked;
    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 2) return new Size();
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1100;
        InternalChildren[1].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double actions = InternalChildren[1].DesiredSize.Width;
        _stacked = PirateVisual.GetEnabled(this) && width < 900;
        if (_stacked)
            InternalChildren[1].Measure(new Size(width, 52));
        InternalChildren[0].Measure(new Size(Math.Max(0, _stacked ? width : width - actions - 16), 56));
        return new Size(width, _stacked ? 118 : PirateVisual.GetEnabled(this) ? 86 : 52);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count != 2) return finalSize;
        double actions = Math.Min(finalSize.Width, InternalChildren[1].DesiredSize.Width);
        InternalChildren[0].Arrange(new Rect(0, 0, Math.Max(0, _stacked ? finalSize.Width : finalSize.Width - actions - 16), _stacked ? 60 : finalSize.Height));
        InternalChildren[1].Arrange(new Rect(_stacked ? 0 : finalSize.Width - actions, _stacked ? 62 : 0,
            _stacked ? finalSize.Width : actions, _stacked ? 52 : finalSize.Height));
        return finalSize;
    }
}

/// <summary>Live, scalable engraved numerals; no timer values are baked into artwork.</summary>
public sealed class PirateTimerText : FrameworkElement
{
    public static readonly DependencyProperty TextScaleProperty = DependencyProperty.Register(
        nameof(TextScale), typeof(double), typeof(PirateTimerText),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) =>
            {
                var text = (PirateTimerText)d;
                text._geometry = null;
                text.SetValue(MaximumDisplayHeightPropertyKey, 118d * (double)e.NewValue);
            }));
    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(PirateTimerText),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyPropertyKey MaximumDisplayHeightPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MaximumDisplayHeight), typeof(double), typeof(PirateTimerText), new PropertyMetadata(118d));
    public static readonly DependencyProperty MaximumDisplayHeightProperty = MaximumDisplayHeightPropertyKey.DependencyProperty;
    public double TextScale { get => (double)GetValue(TextScaleProperty); set => SetValue(TextScaleProperty, value); }
    public Brush? TextBrush { get => (Brush?)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public double MaximumDisplayHeight => (double)GetValue(MaximumDisplayHeightProperty);
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PirateTimerText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((PirateTimerText)d)._geometry = null));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    private Geometry? _geometry;
    private Geometry Lettering => _geometry ??= CreateLettering();

    private Geometry CreateLettering()
    {
        var font = new FontFamily(new Uri("pack://application:,,,/StopwatchOverlay;component/"),
            "./Assets/Fonts/Pirate/#Almendra");
        var text = new FormattedText(Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            132 * TextScale, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        Geometry geometry = text.BuildGeometry(new Point());
        geometry.Freeze();
        return geometry;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(Math.Max(1, Lettering.Bounds.Width + 12), Math.Max(1, Lettering.Bounds.Height + 12));

    protected override void OnRender(DrawingContext drawing)
    {
        Geometry geometry = Lettering;
        if (geometry.Bounds.IsEmpty) return;
        drawing.PushTransform(new TranslateTransform(6 - geometry.Bounds.X, 6 - geometry.Bounds.Y + 2));
        drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(86, 53, 25)),
            new Pen(new SolidColorBrush(Color.FromRgb(60, 35, 19)), 5), geometry);
        drawing.Pop();
        drawing.PushTransform(new TranslateTransform(6 - geometry.Bounds.X, 6 - geometry.Bounds.Y));
        var gold = new LinearGradientBrush();
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(249, 225, 166), 0));
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(206, 169, 105), .45));
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(131, 92, 44), .7));
        gold.GradientStops.Add(new GradientStop(Color.FromRgb(232, 199, 134), 1));
        drawing.DrawGeometry(TextBrush ?? gold, new Pen(new SolidColorBrush(Color.FromRgb(47, 30, 19)), 3), geometry);
        if (TextBrush == null)
            drawing.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(180, 247, 221, 158)), .65), geometry);
        drawing.Pop();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimerPeer(this);
    private sealed class TimerPeer(PirateTimerText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
