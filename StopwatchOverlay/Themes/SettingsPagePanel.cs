using System;
using System.Windows;
using System.Windows.Controls;

namespace StopwatchOverlay.Themes;

/// <summary>Keeps the inspector usable as page scale changes the available logical space.</summary>
public sealed class SettingsPagePanel : Panel
{
    private double _previewWidth;
    private double _navigationWidth;
    private double _previewHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 3) return new Size();
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 1040;
        double height = double.IsFinite(availableSize.Height) ? availableSize.Height : 660;
        bool pirate = PirateVisual.GetEnabled(this);
        _previewWidth = pirate ? Math.Clamp(width * .32, 210, 380) : width >= 1000 ? 420 : 0;
        _navigationWidth = pirate ? 0 : 156;
        _previewHeight = pirate ? width < 800 || height < 550 ? 0 : Math.Min(440, height * .56) : height;
        InternalChildren[0].Visibility = _previewWidth > 0 && _previewHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
        InternalChildren[0].Measure(new Size(_previewWidth, _previewHeight));
        InternalChildren[1].Measure(new Size(pirate ? _previewWidth : _navigationWidth,
            pirate ? Math.Max(0, height - _previewHeight) : height));
        InternalChildren[2].Measure(new Size(Math.Max(0, width - _previewWidth - _navigationWidth), height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count != 3) return finalSize;
        bool pirate = PirateVisual.GetEnabled(this);
        InternalChildren[0].Arrange(new Rect(0, 0, _previewWidth, _previewHeight));
        InternalChildren[1].Arrange(new Rect(pirate ? 0 : _previewWidth, pirate ? _previewHeight : 0,
            pirate ? _previewWidth : _navigationWidth, pirate ? Math.Max(0, finalSize.Height - _previewHeight) : finalSize.Height));
        InternalChildren[2].Arrange(new Rect(_previewWidth + _navigationWidth, 0,
            Math.Max(0, finalSize.Width - _previewWidth - _navigationWidth), finalSize.Height));
        return finalSize;
    }
}
