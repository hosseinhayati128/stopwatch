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

    public static void DrawTime(DrawingContext context,string text,Point origin,StopwatchTypographyProfile profile,double size,IBrush brush)
    {
        double x=origin.X;
        foreach(char c in text)
        {
            double advance=Advance(c,profile,size);
            // The corrected Typewriter reference uses an open zero, not a slashed/dotted zero.
            string character=profile==Profiles["typewriter-log"]&&c=='0'?"O":c.ToString();
            double glyphSize=profile.DigitStyle==StopwatchDigitStyle.Pixel?size*.64:size;
            var glyph=Format(character,profile.TimerFont,profile.TimerWeight,glyphSize,brush);
            glyph.SetFontFeatures(TimerFeatures);
            double fit=Math.Min(1,(advance-size*.035)/Math.Max(1,glyph.WidthIncludingTrailingWhitespace));
            var point=new Point(x+(advance-glyph.WidthIncludingTrailingWhitespace*fit)/2,origin.Y+(size-glyphSize)*.55);
            if(profile.DigitStyle==StopwatchDigitStyle.SevenSegment&&char.IsAsciiDigit(c))
                DrawSegments(context,c,new Rect(x+size*.045,origin.Y+size*.19,advance-size*.09,size*.70),brush);
            else if(profile.DigitStyle is StopwatchDigitStyle.Outline or StopwatchDigitStyle.Inline)
            {
                using(context.PushTransform(Matrix.CreateScale(fit,1)*Matrix.CreateTranslation(point.X,point.Y)))
                {
                    var geometry=glyph.BuildGeometry(default);
                    if(geometry!=null)context.DrawGeometry(null,new Pen(brush,size*.025),geometry);
                }
            }
            else using(context.PushTransform(Matrix.CreateScale(fit,1)*Matrix.CreateTranslation(point.X,point.Y)))context.DrawText(glyph,default);
            x+=advance;
        }
    }

    private static readonly int[] Segments={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
    private static void DrawSegments(DrawingContext context,char digit,Rect r,IBrush brush)
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
            context.DrawGeometry(brush,null,geometry);
        }
    }

    internal static FormattedText Format(string text,FontFamily font,FontWeight weight,double size,IBrush brush)
        => new(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(font,FontStyle.Normal,weight),size,brush);
}
