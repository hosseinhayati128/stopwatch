using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public enum StopwatchDigitStyle { Font, SevenSegment, Pixel, Outline, Inline }
public sealed record StopwatchTypographyProfile(FontFamily TimerFont,FontWeight TimerWeight,
    FontFamily NameFont,FontWeight NameWeight,StopwatchDigitStyle DigitStyle,double DigitAdvance);

public static class StopwatchTypography
{
    private static readonly FontFeatureCollection TimerFeatures = new() { FontFeature.Parse("lnum"), FontFeature.Parse("tnum") };
    private static readonly FontFamily Digits=new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Nika/#Roboto Condensed Black");
    private static readonly FontFamily Serif=new("avares://StopwatchOverlay.Desktop/Assets/Fonts/Pirate/#Almendra");
    private static readonly StopwatchTypographyProfile Navigator=new(Digits,FontWeight.Black,Serif,FontWeight.Bold,StopwatchDigitStyle.Font,.52);
    private static FontFamily Font(string directory,string family)=>new($"avares://StopwatchOverlay.Desktop/Assets/Fonts/{directory}/#{family}");
    private static readonly IReadOnlyDictionary<string,StopwatchTypographyProfile> Profiles=new Dictionary<string,StopwatchTypographyProfile>
    {
        ["navigator-bold"]=Navigator,
        ["captains-serif"]=new(Font("Nika","Playfair Display ExtraBold"),FontWeight.ExtraBold,Serif,FontWeight.Bold,StopwatchDigitStyle.Font,.56),
        ["typewriter-log"]=new(Font("Acanthus","Cascadia Mono"),FontWeight.SemiBold,Font("Acanthus","Cascadia Mono"),FontWeight.SemiBold,StopwatchDigitStyle.Font,.60),
        ["instrument-digital"]=new(Font("Nika","Rajdhani SemiBold"),FontWeight.SemiBold,Font("Pirate","Barlow Condensed"),FontWeight.SemiBold,StopwatchDigitStyle.SevenSegment,.56),
        ["pixel-voyage"]=new(Font("Stopwatch","Press Start 2P"),FontWeight.Normal,Font("Stopwatch","Press Start 2P"),FontWeight.Normal,StopwatchDigitStyle.Pixel,.66),
        ["modern-deck"]=new(Font("Nika","Inter ExtraBold"),FontWeight.ExtraBold,Font("Acanthus","Inter"),FontWeight.SemiBold,StopwatchDigitStyle.Font,.64),
        ["art-deco"]=new(Font("Stopwatch","Poiret One"),FontWeight.Normal,Font("Stopwatch","Poiret One"),FontWeight.Normal,StopwatchDigitStyle.Inline,.48),
        ["rounded-deck"]=new(Font("Stopwatch","Nunito ExtraBold"),FontWeight.ExtraBold,Font("Stopwatch","Nunito"),FontWeight.Bold,StopwatchDigitStyle.Font,.60),
        ["chart-plotter"]=new(Font("Nika","Rajdhani SemiBold"),FontWeight.SemiBold,Font("Acanthus","Cascadia Mono"),FontWeight.Normal,StopwatchDigitStyle.Font,.47),
        ["compass-outline"]=new(Digits,FontWeight.Black,Font("Acanthus","Cascadia Mono"),FontWeight.Normal,StopwatchDigitStyle.Outline,.55)
    };
    public static StopwatchTypographyProfile Resolve(string id)=>Profiles.TryGetValue(id,out var profile)?profile:
        throw new InvalidOperationException("Missing stopwatch typography: "+id);

    public static double Advance(char value,StopwatchTypographyProfile profile,double size)
        => size*(value is ':' or ' '? .24:value is '.' or ','? .18:profile.DigitAdvance);
    public static double Width(string text,StopwatchTypographyProfile profile,double size)
        => text.Sum(c=>Advance(c,profile,size));

    private readonly record struct EdgedGlyph(FormattedText Text,Geometry? Shape);
    private static readonly Dictionary<(StopwatchTypographyProfile Profile,double Size,char Character,IBrush Brush),EdgedGlyph> EdgedGlyphs=new();
    private static EdgedGlyph Glyph(StopwatchTypographyProfile profile,double size,char c,IBrush brush,bool withEdge)
    {
        var key=(profile,size,c,brush);
        if(withEdge&&EdgedGlyphs.TryGetValue(key,out var cached))return cached;
        string character=profile==Profiles["typewriter-log"]&&c=='0'?"O":c.ToString();
        var text=Format(character,profile.TimerFont,profile.TimerWeight,size,brush);
        text.SetFontFeatures(TimerFeatures);
        var glyph=new EdgedGlyph(text,withEdge?text.BuildGeometry(default):null);
        if(withEdge)
        {
            // Font/size sliders can explore many combinations. Keep the shared cache bounded.
            if(EdgedGlyphs.Count>=256)EdgedGlyphs.Clear();
            EdgedGlyphs.Add(key,glyph);
        }
        return glyph;
    }

    public static void DrawTime(DrawingContext context,string text,Point origin,StopwatchTypographyProfile profile,double size,IBrush brush,IBrush? edge=null)
    {
        double x=origin.X;
        var edgePen=edge==null?null:new Pen(edge,size*.045,lineJoin:PenLineJoin.Round);
        foreach(char c in text)
        {
            double advance=Advance(c,profile,size);
            // The corrected Typewriter reference uses an open zero, not a slashed/dotted zero.
            double glyphSize=profile.DigitStyle==StopwatchDigitStyle.Pixel?size*.64:size;
            var cached=Glyph(profile,glyphSize,c,brush,edge!=null);
            var glyph=cached.Text;
            double fit=Math.Min(1,(advance-size*.035)/Math.Max(1,glyph.WidthIncludingTrailingWhitespace));
            var point=new Point(x+(advance-glyph.WidthIncludingTrailingWhitespace*fit)/2,origin.Y+(size-glyphSize)*.55);
            if(profile.DigitStyle==StopwatchDigitStyle.SevenSegment&&char.IsAsciiDigit(c))
                DrawSegments(context,c,new Rect(x+size*.045,origin.Y+size*.19,advance-size*.09,size*.70),brush,edgePen);
            else if(profile.DigitStyle is StopwatchDigitStyle.Outline or StopwatchDigitStyle.Inline)
            {
                using(context.PushTransform(Matrix.CreateScale(fit,1)*Matrix.CreateTranslation(point.X,point.Y)))
                {
                    var geometry=cached.Shape??glyph.BuildGeometry(default);
                    if(geometry!=null)
                    {
                        if(edge!=null)context.DrawGeometry(null,new Pen(edge,size*.07,lineJoin:PenLineJoin.Round),geometry);
                        context.DrawGeometry(null,new Pen(brush,size*.025),geometry);
                    }
                }
            }
            else using(context.PushTransform(Matrix.CreateScale(fit,1)*Matrix.CreateTranslation(point.X,point.Y)))
            {
                if(cached.Shape!=null)context.DrawGeometry(null,edgePen,cached.Shape);
                context.DrawText(glyph,default);
            }
            x+=advance;
        }
    }

    private static readonly int[] Segments={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
    private static void DrawSegments(DrawingContext context,char digit,Rect r,IBrush brush,IPen? edgePen)
    {
        double t=r.Height*.13,g=t*.18;
        Rect[] pieces={new(r.X+t,r.Y,r.Width-2*t,t),new(r.Right-t,r.Y+t,t,r.Height/2-t-g),
            new(r.Right-t,r.Center.Y+g,t,r.Height/2-t-g),new(r.X+t,r.Bottom-t,r.Width-2*t,t),
            new(r.X,r.Center.Y+g,t,r.Height/2-t-g),new(r.X,r.Y+t,t,r.Height/2-t-g),new(r.X+t,r.Center.Y-t/2,r.Width-2*t,t)};
        for(int i=0;i<7;i++)if((Segments[digit-'0']&(1<<i))!=0)
        {
            var p=pieces[i];double cut=t*.38;var geometry=new StreamGeometry();using(var path=geometry.Open())
            {
                path.BeginFigure(new Point(p.X+cut,p.Y),true);path.LineTo(new Point(p.Right-cut,p.Y));path.LineTo(new Point(p.Right,p.Y+cut));
                path.LineTo(new Point(p.Right,p.Bottom-cut));path.LineTo(new Point(p.Right-cut,p.Bottom));path.LineTo(new Point(p.X+cut,p.Bottom));
                path.LineTo(new Point(p.X,p.Bottom-cut));path.LineTo(new Point(p.X,p.Y+cut));path.EndFigure(true);
            }
            if(edgePen!=null)context.DrawGeometry(null,edgePen,geometry);
            context.DrawGeometry(brush,null,geometry);
        }
    }

    internal static FormattedText Format(string text,FontFamily font,FontWeight weight,double size,IBrush brush)
        => new(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(font,FontStyle.Normal,weight),size,brush);
}
