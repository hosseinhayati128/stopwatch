using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

internal sealed class ControllerDigitAtlas(string name, int digitWidth, int separatorWidth, int capWidth = 0)
{
    // Two-times artwork. The last slots contain punctuation, the ticket ends and a minus sign.
    private const int SlotWidth = 96;
    private const int Height = 148;
    private Bitmap? _bitmap;
    private static readonly ControllerDigitAtlas Stencil = new("deck-stencil", 94, 32);
    private static readonly ControllerDigitAtlas Ticket = new("time-ticket", 84, 34, 18);
    private static readonly ControllerDigitAtlas Chartmaker = new("chartmaker", 88, 32);
    private static readonly ControllerDigitAtlas Cathode = new("cathode", 86, 32);

    internal static ControllerDigitAtlas? Resolve(string? id) => id switch
    {
        "deck-stencil" => Stencil,
        "time-ticket" => Ticket,
        "chartmaker" => Chartmaker,
        "cathode" => Cathode,
        _ => null
    };

    private int Advance(char c) => char.IsAsciiDigit(c) ? digitWidth : c == '-' ? 32 : separatorWidth;

    private int Width(string text)
    {
        int width = 2 * capWidth;
        foreach (char c in text) width += Advance(c);
        return width;
    }

    // Match the visible cap height of the original large Pixel Voyage digits.
    private static double Scale(double fontSize) => fontSize * .53 / 88;
    internal double Measure(string text, double fontSize) => Width(text) * Scale(fontSize);

    internal void Draw(DrawingContext context, string text, double fontSize, Size bounds)
    {
        if (_bitmap is null)
        {
            using var stream = AssetLoader.Open(new Uri($"avares://StopwatchOverlay.Desktop/Assets/Nika/ControllerDigits/{name}.png"));
            _bitmap = new Bitmap(stream);
        }
        double scale = Scale(fontSize);
        double x = (bounds.Width - Width(text) * scale) / 2;
        double y = (bounds.Height - Height * scale) / 2;
        if (capWidth > 0) DrawSlot(13, capWidth);
        foreach (char c in text)
        {
            int slot = c switch { >= '0' and <= '9' => c - '0', ':' => 10, '.' or ',' => 11, '-' => 15, _ => 12 };
            DrawSlot(slot, Advance(c));
        }
        if (capWidth > 0) DrawSlot(14, capWidth);

        void DrawSlot(int slot, int width)
        {
            context.DrawImage(_bitmap, new Rect(slot * SlotWidth, 0, width, Height), new Rect(x, y, width * scale, Height * scale));
            x += width * scale;
        }
    }
}
