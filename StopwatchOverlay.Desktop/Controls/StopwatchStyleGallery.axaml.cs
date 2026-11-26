using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Controls;

internal sealed record StopwatchGalleryLayer(string Name, IReadOnlyList<StopwatchAppearanceOption> Options,
    Func<StopwatchAppearanceSettings, string> Selected, Action<StopwatchAppearanceSettings, string> Select)
{
    internal static readonly StopwatchGalleryLayer Frame = new("Frame", StopwatchAppearanceCatalog.Frames,
        p => p.Frame, (p, id) => p.Frame = id);
    internal static readonly StopwatchGalleryLayer Background = new("Background", StopwatchAppearanceCatalog.Backgrounds,
        p => p.Background, (p, id) => p.Background = id);

    internal static readonly StopwatchGalleryLayer Typography = new("Typography", StopwatchAppearanceCatalog.Typography,
        p => p.Typography, (p, id) => p.Typography = id);
    internal static readonly StopwatchGalleryLayer Artwork = new("Artwork", StopwatchAppearanceCatalog.Artworks,
        p => p.Artwork, (p, id) => p.Artwork = id);

    internal string Label => this == Typography ? "typeface" : Name.ToLowerInvariant();
    internal string BrowseLabel => this == Typography ? "Browse typography" : $"Browse {Label}s";
    internal string ButtonName => this == Typography ? "BrowseStopwatchTypography" : $"BrowseStopwatch{Name}s";
}

public partial class StopwatchStyleGallery : UserControl
{
    private readonly StopwatchGalleryLayer _layer = StopwatchGalleryLayer.Frame;
    private readonly UniformGrid _tiles = new() { Columns = 4, Margin = new Thickness(0, 0, 5, 0),
        VerticalAlignment = VerticalAlignment.Top };

    public StopwatchStyleGallery()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        GalleryOptions.ItemsPanel = new FuncTemplate<Panel?>(() => _tiles);
        GalleryOptions.SizeChanged += (_, _) => FitTiles();
        GalleryOptions.AddHandler(KeyDownEvent, Navigate, RoutingStrategies.Tunnel);
    }

    internal StopwatchStyleGallery(AppSettings settings, StopwatchGalleryLayer layer, Action<string> selectOption, Action back, Action<bool> compact)
        : this()
    {
        _layer = layer;
        GalleryHeading.Text = $"Choose a {layer.Label}";
        AutomationProperties.SetName(GalleryOptions, $"Clock {layer.Label}s");
        foreach (var option in layer.Options)
        {
            var tile = new ListBoxItem { Content = CreateTile(option, settings), Tag = option };
            tile.Classes.Add("frameTile");
            AutomationProperties.SetName(tile, option.Label);
            GalleryOptions.Items.Add(tile);
            if (option.Id == layer.Selected(settings.StopwatchAppearance)) GalleryOptions.SelectedItem = tile;
        }
        UpdateSummary();
        GalleryOptions.SelectionChanged += (_, _) =>
        {
            if (GalleryOptions.SelectedItem is ListBoxItem { Tag: StopwatchAppearanceOption option })
            {
                UpdateSummary();
                selectOption(option.Id);
            }
        };
        GalleryBack.Click += (_, _) => back();
        GalleryOptions.SizeChanged += (_, _) => compact(layer == StopwatchGalleryLayer.Typography
            ? GalleryOptions.Bounds.Width < 480 : _tiles.Columns <= 2);
    }

    private void UpdateSummary()
    {
        if (GalleryOptions.SelectedItem is ListBoxItem { Tag: StopwatchAppearanceOption selected })
            GallerySummary.Text = $"{GalleryOptions.ItemCount} {_layer.Label}s  ·  {selected.Label} selected";
    }

    internal void FocusSelection()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null && GalleryOptions.SelectedItem is ListBoxItem item)
            {
                item.Focus(NavigationMethod.Tab);
                item.BringIntoView();
            }
        }, DispatcherPriority.Loaded);
    }

    private void FitTiles()
    {
        var size = GalleryOptions.Bounds;
        if (size.Width <= 0 || size.Height <= 0) return;
        if (_layer == StopwatchGalleryLayer.Typography)
        {
            _tiles.Columns = size.Width >= 320 ? 2 : 1;
            bool narrow = size.Width < 480;
            double rowHeight = Math.Clamp(Math.Floor(size.Height / (narrow ? 3 : 5)), 86, 148);
            foreach (var tile in GalleryOptions.Items.OfType<ListBoxItem>())
            {
                tile.Height = rowHeight - tile.Margin.Top - tile.Margin.Bottom;
                if (tile.Content is Grid card)
                    card.RowDefinitions[1].Height = new GridLength(narrow ? 30 : 24);
            }
            return;
        }
        bool backgrounds = _layer == StopwatchGalleryLayer.Background;
        _tiles.Columns = backgrounds && size.Width >= 330
            ? Math.Clamp((int)(size.Width / 218), 2, 4)
            : Math.Clamp((int)(size.Width / 168), 1, 4);
        double height = _tiles.Columns <= 2 ? Math.Clamp(Math.Floor(size.Height / 3), 86, 121)
            : size.Width > 800 ? (backgrounds ? 134 : 121) : (backgrounds ? 110 : 103);
        foreach (var tile in GalleryOptions.Items.OfType<ListBoxItem>())
            tile.Height = height - tile.Margin.Top - tile.Margin.Bottom;
    }

    private void Navigate(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None) return;
        int index = GalleryOptions.SelectedIndex;
        int last = GalleryOptions.ItemCount - 1;
        int target = e.Key switch
        {
            Key.Left => index - 1, Key.Right => index + 1,
            Key.Up => index - _tiles.Columns, Key.Down => index + _tiles.Columns,
            Key.Home => 0, Key.End => last, _ => -1
        };
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End) || last < 0) return;
        GalleryOptions.SelectedIndex = Math.Clamp(target, 0, last);
        if (GalleryOptions.SelectedItem is ListBoxItem item)
        {
            item.Focus(NavigationMethod.Directional);
            item.BringIntoView();
        }
        e.Handled = true;
    }

    private Grid CreateTile(StopwatchAppearanceOption option, AppSettings settings)
    {
        var clock = StopwatchAppearancePreview.CreateGallerySample(_layer, option, settings);
        var stage = new Border { Padding = new Thickness(8, 4), Background = Brush.Parse("#213847"), Child = clock };
        if (_layer == StopwatchGalleryLayer.Typography)
        {
            clock.RenderTransform = new ScaleTransform(1.25, 1.25);
            stage.ClipToBounds = true;
        }
        var label = new TextBlock { Text = option.Label, FontSize = 13, FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("frameName");
        var check = new Avalonia.Controls.Shapes.Path { Width = 14, Height = 14, StrokeThickness = 2,
            Stroke = Brush.Parse("#FFF2D4"), Data = Geometry.Parse("M2,8 L6,12 L14,3"), VerticalAlignment = VerticalAlignment.Center };
        check.Classes.Add("frameCheck");
        var caption = new Grid { ColumnDefinitions = new("*,16"), Children = { label, check } };
        Grid.SetColumn(check, 1);
        var strip = new Border { Padding = new Thickness(8,2), Child = caption };
        strip.Classes.Add("frameCaption");
        Grid.SetRow(strip, 1);
        return new Grid { RowDefinitions = new("*,30"), Children = { stage, strip } };
    }
}
