using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Controls;

public partial class StopwatchArtworkPicker : UserControl
{
    private readonly List<(StopwatchAppearanceOption Option, RadioButton Button, ThemedClockFace Face)> _cards = new();
    private readonly record struct SampleStyle(string Background, string Frame, string Typography, double Opacity,
        StopwatchOpaqueLayers OpaqueLayers, double TextSize, string TextColor)
    {
        internal static SampleStyle From(AppSettings settings)
        {
            var p = settings.StopwatchAppearance;
            return new(p.Background, p.Frame, p.Typography, p.BackgroundOpacity, p.OpaqueLayers, settings.TextSize, settings.TextColor);
        }
    }
    private SampleStyle _sampleStyle;

    public StopwatchArtworkPicker()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        SizeChanged += (_, e) => FitCards(e.NewSize.Width);
        AddHandler(KeyDownEvent, Navigate, RoutingStrategies.Tunnel);
    }

    internal StopwatchArtworkPicker(AppSettings settings, Action<string> choose) : this()
    {
        _sampleStyle = SampleStyle.From(settings);
        foreach (var option in StopwatchAppearanceCatalog.Artworks)
        {
            var sample = StopwatchAppearancePreview.CreateGallerySample(StopwatchGalleryLayer.Artwork, option, settings);
            var button = new RadioButton { Tag = option.Label, Content = sample, IsChecked = option.Id == settings.StopwatchAppearance.Artwork };
            button.Classes.Add("artworkCard");
            AutomationProperties.SetName(button, option.Label);
            Grid.SetColumn(button, _cards.Count * 2);
            Cards.Children.Add(button);
            _cards.Add((option, button, (ThemedClockFace)sample.Child!));
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true) choose(option.Id);
            };
        }
    }

    internal void Apply(AppSettings settings)
    {
        var style = SampleStyle.From(settings);
        foreach (var (option, button, face) in _cards)
        {
            if (style != _sampleStyle)
                StopwatchAppearancePreview.ApplyGallerySample(face, StopwatchGalleryLayer.Artwork, option, settings);
            if (option.Id == settings.StopwatchAppearance.Artwork) button.IsChecked = true;
        }
        _sampleStyle = style;
    }

    private void FitCards(double width)
    {
        if (width <= 0) return;
        bool compact = width / 3 < 145;
        Height = Math.Clamp(width / 10 + 43, 81, 139) + (compact ? 12 : 0);
        foreach (var (option, button, _) in _cards)
        {
            button.Classes.Set("compact", compact);
            button.Tag = compact ? option.Label.Replace(" ", "\n") : option.Label;
        }
    }

    private void Navigate(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right)) return;
        int index = _cards.FindIndex(card => card.Button.IsFocused);
        if (index < 0) return;
        var target = _cards[Math.Clamp(index + (e.Key == Key.Left ? -1 : 1), 0, _cards.Count - 1)].Button;
        target.Focus(NavigationMethod.Directional);
        target.IsChecked = true;
        e.Handled = true;
    }
}
