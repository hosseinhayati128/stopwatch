using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StopwatchOverlay.Themes;

/// <summary>Stretches painted panels while preserving their brass corners and compass caps.</summary>
public sealed class NoteArtworkBorder : Border
{
    public static readonly DependencyProperty ArtworkProperty = DependencyProperty.Register(nameof(Artwork), typeof(string),
        typeof(NoteArtworkBorder), new FrameworkPropertyMetadata("Frame", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Artwork { get => (string)GetValue(ArtworkProperty); set => SetValue(ArtworkProperty, value); }
    private static readonly Lazy<BitmapSource> Frame = new(() => Load("frame"));
    private static readonly Lazy<BitmapSource> Paper = new(() => Load("paper"));
    private static readonly Lazy<BitmapSource> Button = new(() => Load("button"));
    private static BitmapSource Load(string name)
    {
        var source = new BitmapImage(new Uri($"pack://application:,,,/StopwatchOverlay;component/Assets/Pirate/notes-{name}.png"));
        // Exclude the transparent canvas margin from component measurements.
        var pixels = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = pixels.PixelWidth, height = pixels.PixelHeight;
        var bytes = new byte[width * height * 4];
        pixels.CopyPixels(bytes, width * 4, 0);
        int left = width - 1, top = height - 1, right = 0, bottom = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (bytes[(y * width + x) * 4 + 3] > 20)
            { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        var result = new CroppedBitmap(source, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        result.Freeze();
        return result;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!PirateVisual.GetEnabled(this) || ActualWidth <= 0 || ActualHeight <= 0) return;
        bool button = Artwork == "Button", paper = Artwork == "Paper";
        BitmapSource source = button ? Button.Value : paper ? Paper.Value : Frame.Value;
        double sourceCapX = button ? .19 : paper ? .055 : .115;
        double sourceCapY = button ? .24 : paper ? .075 : .115;
        double capX = button ? Math.Min(ActualHeight * .88, ActualWidth * .24) : paper ? 23 : 52;
        double capY = button ? ActualHeight * .24 : paper ? 23 : 52;
        capX = Math.Min(capX, ActualWidth / 2); capY = Math.Min(capY, ActualHeight / 2);
        double[] sx = [0, sourceCapX, 1 - sourceCapX, 1], sy = [0, sourceCapY, 1 - sourceCapY, 1];
        double[] dx = [0, capX, ActualWidth - capX, ActualWidth], dy = [0, capY, ActualHeight - capY, ActualHeight];
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
        {
            var brush = new ImageBrush(source)
            { Viewbox = new Rect(sx[x], sy[y], sx[x + 1] - sx[x], sy[y + 1] - sy[y]), Stretch = Stretch.Fill };
            dc.DrawRectangle(brush, null, new Rect(dx[x], dy[y], dx[x + 1] - dx[x], dy[y + 1] - dy[y]));
        }
    }
}
