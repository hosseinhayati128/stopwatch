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

public partial class StopwatchFrameGallery : UserControl
{
    private readonly UniformGrid _tiles = new() { Columns = 4, Margin = new Thickness(0, 0, 5, 0),
        VerticalAlignment = VerticalAlignment.Top };

    public StopwatchFrameGallery()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        FrameOptions.ItemsPanel = new FuncTemplate<Panel?>(() => _tiles);
        FrameOptions.SizeChanged += (_, _) => FitTiles();
        FrameOptions.AddHandler(KeyDownEvent, Navigate, RoutingStrategies.Tunnel);
    }

    internal StopwatchFrameGallery(AppSettings settings, Action<string> selectFrame, Action back, Action<bool> compact)
        : this()
    {
        foreach (var option in StopwatchAppearanceCatalog.Frames)
        {
            var tile = new ListBoxItem { Content = CreateTile(option, settings), Tag = option };
            tile.Classes.Add("frameTile");
            AutomationProperties.SetName(tile, option.Label);
            FrameOptions.Items.Add(tile);
            if (option.Id == settings.StopwatchAppearance.Frame) FrameOptions.SelectedItem = tile;
        }
        UpdateSummary();
        FrameOptions.SelectionChanged += (_, _) =>
        {
            if (FrameOptions.SelectedItem is ListBoxItem { Tag: StopwatchAppearanceOption frame })
            {
                UpdateSummary();
                selectFrame(frame.Id);
            }
        };
        FrameGalleryBack.Click += (_, _) => back();
        FrameOptions.SizeChanged += (_, _) => compact(_tiles.Columns <= 2);
    }

    private void UpdateSummary()
    {
        if (FrameOptions.SelectedItem is ListBoxItem { Tag: StopwatchAppearanceOption selected })
            FrameSummary.Text = $"{FrameOptions.ItemCount} frames  ·  {selected.Label} selected";
    }

    internal void FocusSelection()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null && FrameOptions.SelectedItem is ListBoxItem item)
            {
                item.Focus(NavigationMethod.Tab);
                item.BringIntoView();
            }
        }, DispatcherPriority.Loaded);
    }

    private void FitTiles()
    {
        var size = FrameOptions.Bounds;
        if (size.Width <= 0 || size.Height <= 0) return;
        _tiles.Columns = Math.Clamp((int)(size.Width / 168), 1, 4);
        double height = _tiles.Columns <= 2 ? Math.Clamp(Math.Floor(size.Height / 3), 86, 121)
            : size.Width > 800 ? 121 : 103;
        foreach (var tile in FrameOptions.Items.OfType<ListBoxItem>())
            tile.Height = height - tile.Margin.Top - tile.Margin.Bottom;
    }

    private void Navigate(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None) return;
        int index = FrameOptions.SelectedIndex;
        int last = FrameOptions.ItemCount - 1;
        int target = e.Key switch
        {
            Key.Left => index - 1, Key.Right => index + 1,
            Key.Up => index - _tiles.Columns, Key.Down => index + _tiles.Columns,
            Key.Home => 0, Key.End => last, _ => -1
        };
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End) || last < 0) return;
        FrameOptions.SelectedIndex = Math.Clamp(target, 0, last);
        if (FrameOptions.SelectedItem is ListBoxItem item)
        {
            item.Focus(NavigationMethod.Directional);
            item.BringIntoView();
        }
        e.Handled = true;
    }

    private static Grid CreateTile(StopwatchAppearanceOption frame, AppSettings settings)
    {
        var clock = StopwatchAppearancePreview.CreateFrameSample(frame, settings);
        var stage = new Border { Padding = new Thickness(8, 4), Background = Brush.Parse("#213847"), Child = clock };
        var label = new TextBlock { Text = frame.Label, FontSize = 13, FontWeight = FontWeight.SemiBold,
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
