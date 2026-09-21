using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StopwatchOverlay.Themes;

public static class NavigatorVisual
{
    public static readonly DependencyProperty SurfaceOpacityProperty = DependencyProperty.RegisterAttached(
        "SurfaceOpacity", typeof(double), typeof(NavigatorVisual), new FrameworkPropertyMetadata(1d,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static double GetSurfaceOpacity(DependencyObject target) => (double)target.GetValue(SurfaceOpacityProperty);
    public static void SetSurfaceOpacity(DependencyObject target, double value) => target.SetValue(SurfaceOpacityProperty, value);
    public static readonly DependencyProperty OpaquePartsProperty = DependencyProperty.RegisterAttached(
        "OpaqueParts", typeof(NavigatorOpaqueParts), typeof(NavigatorVisual), new FrameworkPropertyMetadata(NavigatorOpaqueParts.Default,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public static NavigatorOpaqueParts GetOpaqueParts(DependencyObject target) => (NavigatorOpaqueParts)target.GetValue(OpaquePartsProperty);
    public static void SetOpaqueParts(DependencyObject target, NavigatorOpaqueParts value) => target.SetValue(OpaquePartsProperty, value & NavigatorOpaqueParts.All);
    public static double LayerOpacity(NavigatorOpaqueParts parts, NavigatorOpaqueParts layer, double opacity)
        => (parts & layer) != 0 ? 1 : double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(NavigatorVisual), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.RegisterAttached(
        "Scale", typeof(double), typeof(NavigatorVisual), new FrameworkPropertyMetadata(1d,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static double GetScale(DependencyObject target) => (double)target.GetValue(ScaleProperty);
    public static void SetScale(DependencyObject target, double value) => target.SetValue(ScaleProperty, value);
    public static readonly DependencyProperty ActionProperty = DependencyProperty.RegisterAttached(
        "Action", typeof(string), typeof(NavigatorVisual), new PropertyMetadata("Play"));
    public static string GetAction(DependencyObject target) => (string)target.GetValue(ActionProperty);
    public static void SetAction(DependencyObject target, string value) => target.SetValue(ActionProperty, value);
}

public sealed class NavigatorDialOpacityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => NavigatorVisual.LayerOpacity(values[0] is NavigatorOpaqueParts parts ? parts : NavigatorOpaqueParts.Default,
            NavigatorOpaqueParts.ControlDials, values[1] is double opacity ? opacity : 1);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
