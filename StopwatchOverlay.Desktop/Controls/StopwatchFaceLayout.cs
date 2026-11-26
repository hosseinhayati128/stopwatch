using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

internal readonly record struct StopwatchFaceLayout(Rect Frame, Rect Content, double Scale)
{
    internal double NameMaxWidth => Math.Max(1, Math.Min(Frame.Width - 100 * Scale, Content.Width - 23 * Scale));
    internal Rect ActiveBadge => new(Frame.Center.X - 42 * Scale, 8 * Scale, 84 * Scale, 22 * Scale);
    internal Rect ArtworkClip => new(0, 0, 82 * Scale, Frame.Bottom);

    internal Rect Artwork(bool corner) => corner
        ? new(28 * Scale, 33 * Scale, 48 * Scale, 48 * Scale)
        : new(-30 * Scale, 30 * Scale, 116 * Scale, 116 * Scale);

    internal Rect Time(double width) => new(Frame.Center.X - width / 2, Content.Top, width, 48 * Scale);

    internal FormattedText FormatName(string text, StopwatchTypographyProfile type, IBrush brush)
    {
        var size = (type.DigitStyle == StopwatchDigitStyle.Pixel ? 14 : 20) * Scale;
        var name = StopwatchTypography.Format(text, type.NameFont, type.NameWeight, size, brush);
        name.MaxTextWidth = NameMaxWidth;
        name.MaxTextHeight = 32 * Scale;
        name.Trimming = TextTrimming.CharacterEllipsis;
        return name;
    }

    internal Rect ProjectName(FormattedText name)
    {
        double width = Math.Min(name.WidthIncludingTrailingWhitespace, name.MaxTextWidth);
        return new((Frame.Width - width + 23 * Scale) / 2, Content.Top + 48 * Scale, width, name.Height);
    }

    internal Rect Marker(Rect name) => new(name.X - 23 * Scale, name.Y + 7 * Scale, 13 * Scale, 14 * Scale);

    internal static StopwatchFaceLayout Create(Size size, StopwatchAsset asset, double scale, out StopwatchFrameLayout frameLayout)
    {
        var frame = new Rect(0, 12 * scale, size.Width, size.Height - 12 * scale);
        frameLayout = StopwatchFrameRenderer.GetLayout(asset, frame.Size, .18 * scale);
        return new(frame, frame.Deflate(frameLayout.ContentInsets), scale);
    }
}
