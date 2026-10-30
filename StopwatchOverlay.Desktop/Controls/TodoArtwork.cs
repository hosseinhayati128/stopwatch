using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Only artwork is rasterized. Text and hit targets remain native controls.</summary>
public sealed class TodoArtwork : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<TodoArtwork, string>(nameof(Kind), "Emblem");
    public static readonly StyledProperty<string> ChoiceProperty = AvaloniaProperty.Register<TodoArtwork, string>(nameof(Choice), "luffy");
    private static readonly Dictionary<string, Bitmap> Images = new();
    public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Choice { get => GetValue(ChoiceProperty); set => SetValue(ChoiceProperty, value); }
    static TodoArtwork() => AffectsRender<TodoArtwork>(KindProperty, ChoiceProperty);
    public TodoArtwork() { IsHitTestVisible = false; Focusable = false; }
    // Measured silhouette bounds in the source atlas, not equal grid cells.
    private static readonly Rect[] Emblems = {
        new(39,69,266,231),new(335,68,293,248),new(643,63,288,254),new(952,65,263,245),
        new(44,350,264,247),new(334,353,284,240),new(620,352,323,245),new(949,342,282,261),
        new(34,633,264,274),new(329,638,313,242),new(659,643,254,243),new(938,646,304,252),
        new(44,921,252,259),new(326,931,303,236),new(626,932,314,226),new(941,940,296,224)
    };
    public static Rect EmblemSource(string id)
    {
        int index=TodoAppearanceCatalog.Emblems.ToList().FindIndex(option=>option.Id==id);
        return Emblems[Math.Max(0,index)];
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        bool emblem = Kind == "Emblem";
        string id = TodoAppearanceCatalog.Normalize(Choice, emblem ? TodoAppearanceCatalog.Emblems : TodoAppearanceCatalog.Backgrounds,
            emblem ? "luffy" : "navigator-parchment");
        if (emblem && id == "luffy")
        {
            PirateArtwork.RenderPart(context, "Skull", Bounds.Size);
            return;
        }
        var bitmap = Load(Kind == "Header" ? "header-map.png" : emblem ? "emblems.png" : id + ".png");
        Rect source = new(bitmap.Size), target = new(Bounds.Size);
        if (emblem)
        {
            source=EmblemSource(id);
            double w=source.Width,h=source.Height;
            double scale = Math.Min(Bounds.Width / w, Bounds.Height / h)*.9;
            target = new Rect((Bounds.Width - w * scale) / 2, (Bounds.Height - h * scale) / 2, w * scale, h * scale);
            // These two silhouettes overlap in their bounding boxes by three pixels,
            // at different heights. Notch only the empty corner containing the neighbor.
            if(id is "buggy" or "shanks")
            {
                var clip=new StreamGeometry();using(var c=clip.Open())
                {
                    double x=3*scale,y=(1100-source.Y)*scale;
                    var points=id=="buggy"?new[]{target.TopLeft,target.TopRight,new Point(target.Right,target.Top+y),new Point(target.Right-x,target.Top+y),new Point(target.Right-x,target.Bottom),target.BottomLeft}
                        :new[]{new Point(target.Left+x,target.Top),target.TopRight,target.BottomRight,target.BottomLeft,new Point(target.Left,target.Top+y),new Point(target.Left+x,target.Top+y)};
                    c.BeginFigure(points[0],true);foreach(var point in points.Skip(1))c.LineTo(point);c.EndFigure(true);
                }
                using(context.PushGeometryClip(clip))context.DrawImage(bitmap,source,target);
                return;
            }
        }
        else source = NikaArtwork.CoverSource(source, Bounds.Size, 1);
        context.DrawImage(bitmap, source, target);
        if(Kind=="Background")
        {
            // Keep the selected map at the edges while giving native text a quiet reading area.
            var veil=new LinearGradientBrush {StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative)};
            var paper=Color.Parse(id=="midnight-chart"?"#082D43":"#F4D79A");
            veil.GradientStops.Add(new GradientStop(Color.FromArgb(130,paper.R,paper.G,paper.B),0));
            veil.GradientStops.Add(new GradientStop(Color.FromArgb(110,paper.R,paper.G,paper.B),.65));
            veil.GradientStops.Add(new GradientStop(Color.FromArgb(90,paper.R,paper.G,paper.B),.88));
            veil.GradientStops.Add(new GradientStop(Color.FromArgb(0,paper.R,paper.G,paper.B),1));
            context.FillRectangle(veil,target);
        }
    }

    private static Bitmap Load(string file)
    {
        if (!Images.TryGetValue(file, out var bitmap))
            Images[file] = bitmap = new Bitmap(AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Todo/" + file)));
        return bitmap;
    }
}
