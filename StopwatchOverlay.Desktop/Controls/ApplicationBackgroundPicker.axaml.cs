using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace StopwatchOverlay.Desktop.Controls;

public partial class ApplicationBackgroundPicker : UserControl, IDisposable
{
    private readonly Dictionary<string, RadioButton> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Image Image, Bitmap Bitmap)> _images = new();
    private readonly string _group = Guid.NewGuid().ToString();
    private CancellationTokenSource? _load;
    private bool _syncing;
    internal event Action<AppBackgroundChoice>? Chosen;
    internal event Action? BackRequested;

    public ApplicationBackgroundPicker()
    {
        InitializeComponent();
        FontFamily = ClockPickerPresentation.Body;
        Back.FontFamily = Add.FontFamily = Remove.FontFamily = FontFamily;
        Back.Click += (_, _) => BackRequested?.Invoke();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            BackRequested?.Invoke();
        };
        SizeChanged += (_, e) =>
        {
            GalleryCard.Classes.Set("short", e.NewSize.Height < 414);
            bool stacked = e.NewSize.Width < 590;
            Columns.ColumnDefinitions = new(stacked ? "*" : "3.25*,14,1*");
            Columns.RowDefinitions = new(stacked ? "Auto,12,Auto" : "Auto");
            Grid.SetColumn(ImportedSection, stacked ? 0 : 2);
            Grid.SetRow(ImportedSection, stacked ? 2 : 0);
        };
    }

    internal void SetChoices(IEnumerable<AppBackgroundChoice> choices, AppBackgroundChoice? selected)
    {
        ReleaseImages();
        _tiles.Clear();
        DefaultHost.Content = null;
        Patterns.Children.Clear();
        Patterns.RowDefinitions.Clear();
        Imports.Children.Clear();
        var thumbnails = new List<(AppBackgroundChoice Choice, Image Image, TextBlock Label)>();
        int index = 0;
        foreach (var choice in choices)
        {
            var tile = new RadioButton { GroupName = _group, Tag = choice, Theme = (ControlTheme)Resources["backgroundChoice"]! };
            AutomationProperties.SetName(tile, choice.DisplayLabel);
            ToolTip.SetTip(tile, choice.DisplayLabel);
            var title = new TextBlock { Text = choice.DisplayLabel, FontSize = 13, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6,0) };
            if (choice.IsThemeDefault)
            {
                tile.Classes.Add("default");
                title.Margin = new Thickness(12,0,32,0);
                tile.Content = new Border { MinHeight = 33, Child = title };
                DefaultHost.Content = tile;
            }
            else
            {
                var image = new Image { Stretch = Stretch.UniformToFill };
                image.Classes.Add("thumbnail");
                var label = new Border { Child = title };
                label.Classes.Add("tileLabel");
                var content = new StackPanel { Children = { image, label } };
                tile.Content = content;
                if (choice.IsCustom) Imports.Children.Add(tile);
                else
                {
                    if (index % 3 == 0)
                    {
                        if (index != 0) Patterns.RowDefinitions.Add(new RowDefinition(6, GridUnitType.Pixel));
                        Patterns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    }
                    Grid.SetColumn(tile, index % 3 * 2);
                    Grid.SetRow(tile, index / 3 * 2);
                    Patterns.Children.Add(tile);
                    index++;
                }
                if (choice.IsAvailable) thumbnails.Add((choice, image, title));
            }
            _tiles.Add(choice.Id, tile);
            tile.IsCheckedChanged += (_, _) =>
            {
                if (!_syncing && tile.IsChecked == true) Chosen?.Invoke(choice);
            };
        }
        SetSelected(selected);
        _load = new CancellationTokenSource();
        _ = LoadThumbnails(thumbnails, _load.Token);
    }

    internal void SetSelected(AppBackgroundChoice? choice)
    {
        Remove.IsEnabled = choice?.IsCustom == true;
        if (choice == null || !_tiles.TryGetValue(choice.Id, out var tile)) return;
        _syncing = true;
        try { tile.IsChecked = true; }
        finally { _syncing = false; }
    }

    private async Task LoadThumbnails(List<(AppBackgroundChoice Choice, Image Image, TextBlock Label)> items, CancellationToken token)
    {
        foreach (var (choice, image, label) in items)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                var thumbnail = await Task.Run(() => ReadThumbnail(choice));
                if (token.IsCancellationRequested) { thumbnail.Dispose(); return; }
                _images.Add((image, thumbnail));
                image.Source = thumbnail;
            }
            catch (Exception ex) when (AppBackgroundCatalog.IsExpectedImageBoundaryFailure(ex))
            {
                if (token.IsCancellationRequested) return;
                label.Text = $"{choice.DisplayName} (unavailable)";
            }
        }
    }

    private static Bitmap ReadThumbnail(AppBackgroundChoice choice)
    {
        string path = choice.FilePath ?? choice.ResourceUri!;
        using var stream = choice.IsCustom ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete) :
            AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Backgrounds/" + Path.GetFileName(path)));
        using var decoded = DesktopPageAppearance.DecodeBackground(stream, Path.GetExtension(path));
        var size = decoded.PixelSize;
        double ratio = Math.Min(1, Math.Min(360d / size.Width, 768d / size.Height));
        return decoded.CreateScaledBitmap(new PixelSize(Math.Max(1,(int)(size.Width*ratio)), Math.Max(1,(int)(size.Height*ratio))));
    }

    private void ReleaseImages()
    {
        _load?.Cancel();
        _load?.Dispose();
        _load = null;
        foreach (var (image, bitmap) in _images) { image.Source = null; bitmap.Dispose(); }
        _images.Clear();
    }

    public void Dispose() => ReleaseImages();
}
