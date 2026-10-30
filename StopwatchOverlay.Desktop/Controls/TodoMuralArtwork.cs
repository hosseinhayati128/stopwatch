using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Transparent sidebar engraving over the Todo map; never owns input or task state.</summary>
public sealed class TodoMuralArtwork : Control
{
    public static readonly StyledProperty<string> ChoiceProperty = AvaloniaProperty.Register<TodoMuralArtwork, string>(nameof(Choice), "none");
    public static readonly StyledProperty<bool> DarkPaperProperty = AvaloniaProperty.Register<TodoMuralArtwork, bool>(nameof(DarkPaper));
    private static readonly Dictionary<(string File, bool Dark), Bitmap> Images = new();
    public string Choice { get => GetValue(ChoiceProperty); set => SetValue(ChoiceProperty, value); }
    public bool DarkPaper { get => GetValue(DarkPaperProperty); set => SetValue(DarkPaperProperty, value); }
    static TodoMuralArtwork() => AffectsRender<TodoMuralArtwork>(ChoiceProperty, DarkPaperProperty);
    public TodoMuralArtwork() { IsHitTestVisible = false; Focusable = false; }

    public void Apply(TodoAppearanceProfile look)
    {
        Choice = look.SidebarArtwork;
        DarkPaper = look.Background == "midnight-chart";
        Opacity = look.SidebarArtworkOpacity;
        IsVisible = Choice != "none";
    }

    public override void Render(DrawingContext context)
    {
        string id = TodoAppearanceCatalog.Normalize(Choice, TodoAppearanceCatalog.SidebarArtworks, "none");
        if (id == "none" || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var bitmap = LoadArtwork(id == "sun-warrior" ? "warrior-reference-flat.png" : "mural-atlas.png", DarkPaper);
        var source = new Rect(bitmap.Size);
        if (NikaThemeCatalog.GetArtworkRegion(id) is { } crop)
            source = new Rect(crop.X * bitmap.Size.Width, crop.Y * bitmap.Size.Height, crop.Width * bitmap.Size.Width, crop.Height * bitmap.Size.Height);
        var center = SubjectCenter(id, bitmap.Size);
        // Reserve equal space on either side of the subject. Its asymmetric sword,
        // tail or clouds remain complete, with a safe gutter at every panel size.
        double horizontalSpan = 2 * Math.Max(center.X - source.Left, source.Right - center.X);
        double verticalSpan = 2 * Math.Max(center.Y - source.Top, source.Bottom - center.Y);
        double scale = Math.Min(Bounds.Width * .92 / horizontalSpan, Bounds.Height * .88 / verticalSpan);
        double width = source.Width * scale, height = source.Height * scale;
        var target = new Rect(Bounds.Width / 2 - (center.X - source.X) * scale,
            Bounds.Height / 2 - (center.Y - source.Y) * scale, width, height);
        // One image draw keeps the control's opacity linear. Nested drawing masks
        // compound it in Avalonia's software renderer (50% became roughly 12%).
        context.DrawImage(bitmap, source, target);
    }

    private static Point SubjectCenter(string id, Size bitmapSize)
    {
        // Reviewed against the approved source art and native overlay captures.
        // Automatic alpha bounds/mass center decorations, not necessarily subjects.
        // Coordinates refer to the 1206x1305 warrior and 1254x1254 mural atlas.
        // See design/one-piece-v2/todo-overlay/artwork-alignment.md.
        var point = id switch
        {
            "sun-warrior" => new Point(685, 647),
            "dawn-dancer" => new Point(155, 180),
            "sun-pirates" => new Point(468, 183),
            "kozuki-crane" => new Point(780.5, 182),
            "shandora-bell" => new Point(1086, 182.5),
            "cloud-dragon" => new Point(179, 484),
            "phoenix" => new Point(469.5, 484.5),
            "zunesha" => new Point(759, 486.5),
            "laboon" => new Point(1047, 484.5),
            "sea-king" => new Point(146, 769.5),
            "straw-hat-crest" => new Point(476, 772.5),
            "heart-pirates" => new Point(782, 768.5),
            "whitebeard" => new Point(1094, 774.5),
            "kuja-serpents" => new Point(163, 1064),
            "three-sword-spirit" => new Point(469.5, 1062.5),
            "aces-flame" => new Point(792.5, 1062.5),
            "poneglyph" => new Point(1079, 1064),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        return id == "sun-warrior"
            ? new Point(point.X / 1206 * bitmapSize.Width, point.Y / 1305 * bitmapSize.Height)
            : new Point(point.X / 1254 * bitmapSize.Width, point.Y / 1254 * bitmapSize.Height);
    }

    private static Bitmap LoadArtwork(string file, bool dark)
    {
        if (Images.TryGetValue((file, dark), out var cached)) return cached;
        using var source = new Bitmap(AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Nika/" + file)));
        var regions = file == "warrior-reference-flat.png" ? new[] { new Rect(source.PixelSize.ToSize(1)) }
            : NikaThemeCatalog.Artworks.Skip(1).Select(option =>
            {
                var r = NikaThemeCatalog.GetArtworkRegion(option.Id)!.Value;
                return new Rect(r.X * source.PixelSize.Width, r.Y * source.PixelSize.Height, r.Width * source.PixelSize.Width, r.Height * source.PixelSize.Height);
            }).ToArray();
        var tint = Color.Parse(dark ? "#D5AC62" : "#876126");
        var mask = new WriteableBitmap(source.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var buffer = mask.Lock())
        {
            source.CopyPixels(buffer, AlphaFormat.Unpremul);
            var row = new byte[buffer.RowBytes];
            for (int y = 0; y < buffer.Size.Height; y++)
            {
                var rowRegions = regions.Where(r => y >= r.Top && y < r.Bottom).ToArray();
                var address = IntPtr.Add(buffer.Address, y * buffer.RowBytes);
                Marshal.Copy(address, row, 0, row.Length);
                for (int x = 0; x < buffer.Size.Width; x++)
                {
                    int i = x * 4;
                    // Gold is warm; the atlas backing is blue. Keep the engraved texture
                    // and existing alpha, discarding the backing rather than drawing it.
                    double gold = Math.Clamp((row[i + 2] - row[i] - 12) / 100.0, 0, 1);
                    Rect region = default;
                    foreach (var candidate in rowRegions)
                        if (x >= candidate.Left && x < candidate.Right) { region = candidate; break; }
                    double fade = region.Width == 0 ? 0 : Math.Clamp(Math.Min(
                        Math.Min(x - region.Left, region.Right - 1 - x) / (region.Width * .04),
                        Math.Min(y - region.Top, region.Bottom - 1 - y) / (region.Height * .04)), 0, 1);
                    row[i + 3] = (byte)Math.Round(row[i + 3] * gold * fade);
                    row[i] = tint.B; row[i + 1] = tint.G; row[i + 2] = tint.R;
                }
                Marshal.Copy(row, 0, address, row.Length);
            }
        }
        Images[(file, dark)] = mask;
        return mask;
    }
}
