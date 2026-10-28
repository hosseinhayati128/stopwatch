using System;
using System.Windows;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Automation.Peers;

namespace StopwatchOverlay.Themes;

/// <summary>Tight, scalable ink lettering keeps long live timer values within the available width.</summary>
public sealed class NikaTimerText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(NikaTimerText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(NikaTimerText),
        new FrameworkPropertyMetadata(new FontFamily("Arial"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TextScaleProperty = DependencyProperty.Register(nameof(TextScale), typeof(double), typeof(NikaTimerText),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((NikaTimerText)d).SetValue(MaximumDisplayHeightPropertyKey, 132d * (double)e.NewValue)));
    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(nameof(TextBrush), typeof(Brush), typeof(NikaTimerText),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyPropertyKey MaximumDisplayHeightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(MaximumDisplayHeight), typeof(double), typeof(NikaTimerText), new PropertyMetadata(132d));
    public static readonly DependencyProperty MaximumDisplayHeightProperty = MaximumDisplayHeightPropertyKey.DependencyProperty;
    public double MaximumDisplayHeight => (double)GetValue(MaximumDisplayHeightProperty);
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double TextScale { get => (double)GetValue(TextScaleProperty); set => SetValue(TextScaleProperty, value); }
    public Brush? TextBrush { get => (Brush?)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    private Geometry? _geometry;
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property == TextProperty || e.Property == FontFamilyProperty || e.Property == TextScaleProperty) _geometry = null;
        base.OnPropertyChanged(e);
    }
    private Geometry Lettering()
    {
        if (_geometry != null) return _geometry;
        var text = new FormattedText(Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.Black, FontStretches.Normal), 160 * TextScale,
            Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        int decimalPoint = Text.LastIndexOf('.');
        if (decimalPoint >= 0 && decimalPoint < Text.Length - 1)
            text.SetFontSize(85 * TextScale, decimalPoint + 1, Text.Length - decimalPoint - 1);
        var geometry = text.BuildGeometry(new Point());
        var bounds = geometry.Bounds;
        if (!bounds.IsEmpty)
            geometry.Transform = new ScaleTransform(704 * TextScale / bounds.Width, 126 * TextScale / bounds.Height);
        return _geometry = geometry;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var bounds = Lettering().Bounds;
        return new Size(Math.Max(1, bounds.Width + 6), Math.Max(1, bounds.Height + 6));
    }
    protected override void OnRender(DrawingContext context)
    {
        var geometry = Lettering();
        if (geometry.Bounds.IsEmpty) return;
        var ink = TextBrush ?? new SolidColorBrush(Color.FromRgb(0, 35, 56));
        context.PushTransform(new TranslateTransform(3 - geometry.Bounds.X, 3 - geometry.Bounds.Y));
        context.DrawGeometry(ink, null, geometry);
        context.Pop();
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new TimerPeer(this);
    private sealed class TimerPeer(NikaTimerText owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
