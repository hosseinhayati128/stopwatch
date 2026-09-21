using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StopwatchOverlay.Themes;

/// <summary>Arranges the existing actions over the navigator chart without changing their behavior.</summary>
public sealed class NavigatorToolbarPanel : StackPanel
{
    private const double AspectHeight = 340d / 900d;
    private static readonly Lazy<BitmapImage> Artwork = new(() =>
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/StopwatchOverlay;component/Assets/Pirate/navigator-toolbar.png");
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    });

    public static readonly DependencyProperty ReferenceWidthProperty = DependencyProperty.Register(
        nameof(ReferenceWidth), typeof(double), typeof(NavigatorToolbarPanel),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ReferenceWidth
    {
        get => (double)GetValue(ReferenceWidthProperty);
        set => SetValue(ReferenceWidthProperty, value);
    }

    public NavigatorToolbarPanel() => Orientation = Orientation.Horizontal;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == NavigatorVisual.EnabledProperty || e.Property == NavigatorVisual.ScaleProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (!NavigatorVisual.GetEnabled(this)) return base.MeasureOverride(constraint);
        double width = double.IsFinite(ReferenceWidth) && ReferenceWidth > 0
            ? ReferenceWidth : 520 * NavigatorVisual.GetScale(this);
        if (double.IsFinite(constraint.Width)) width = Math.Min(width, constraint.Width);
        width = Math.Max(0, width);
        double diameter = width * .24;
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(diameter, diameter));
        return new Size(width, width * AspectHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!NavigatorVisual.GetEnabled(this)) return base.ArrangeOverride(finalSize);
        double diameter = finalSize.Width * .24;
        double top = Math.Max(0, (finalSize.Height - diameter) / 2);
        for (int index = 0; index < InternalChildren.Count; index++)
        {
            // Close, pause/resume and reset retain their original logical order.
            double left = finalSize.Width * (.07 + index * .31);
            InternalChildren[index].Arrange(new Rect(left, top, diameter, diameter));
        }
        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (NavigatorVisual.GetEnabled(this) && ActualWidth > 0 && ActualHeight > 0)
        {
            Rect bounds = new(0, 0, ActualWidth, ActualHeight);
            Geometry map = Geometry.Parse("M218,107 L1100,107 Q1180,202 1310,154 L1430,93 L1664,147 L1845,109 L1936,223 L1936,535 L1844,650 L1295,651 L1165,630 L935,651 L779,628 L641,623 L467,651 L211,651 L124,552 L118,220Z").Clone();
            map.Transform = new ScaleTransform(ActualWidth / 2060, ActualHeight / 763);
            Geometry frame = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), map);
            var parts = NavigatorVisual.GetOpaqueParts(this);
            double opacity = NavigatorVisual.GetSurfaceOpacity(this);
            foreach (var layer in new[] { (map, NavigatorOpaqueParts.ControlMap), (frame, NavigatorOpaqueParts.ControlBoard) })
            {
                drawingContext.PushOpacity(NavigatorVisual.LayerOpacity(parts, layer.Item2, opacity));
                drawingContext.PushClip(layer.Item1);
                drawingContext.DrawImage(Artwork.Value, bounds);
                drawingContext.Pop();
                drawingContext.Pop();
            }
        }
    }
}
