using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

public sealed record StopwatchAsset(Bitmap Bitmap,Rect Source,Thickness SliceInsets,
    Thickness ContentInsets,Size MinimumSize,double IntrinsicOpacity,bool IsDark)
{
    public double[] ControlCenters {get;init;}={.3,.5,.7};
    public double ControlCenterY {get;init;}=.5;
}

/// <summary>Application-lifetime immutable artwork cache, shared by clocks and previews.</summary>
public sealed class StopwatchAppearanceAssets
{
    public static StopwatchAppearanceAssets Shared { get; } = new();
    private readonly Dictionary<string,StopwatchAsset> definitions=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Bitmap> bitmaps=new(StringComparer.Ordinal);
    private bool loaded;

    public StopwatchAsset Get(string kind,string id)
    {
        EnsureLoaded();
        return definitions.TryGetValue(kind+":"+id,out var value)?value:
            throw new InvalidOperationException($"Missing stopwatch asset: {kind}/{id}");
    }

    private void EnsureLoaded()
    {
        if(loaded)return;
        using var manifest=AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/Assets/Stopwatch/asset-manifest.json"));
        using var document=JsonDocument.Parse(manifest);
        foreach(var entry in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            string path=entry.GetProperty("path").GetString()!;
            if(!bitmaps.TryGetValue(path,out var bitmap))
            {
                using var stream=AssetLoader.Open(new Uri("avares://StopwatchOverlay.Desktop/"+path));
                bitmap=new Bitmap(stream);bitmaps.Add(path,bitmap);
            }
            var source=entry.TryGetProperty("sourceRect",out var crop)?crop.EnumerateArray().Select(x=>x.GetDouble()).ToArray():new[]{0d,0d,1d,1d};
            var rect=new Rect(source[0]*bitmap.Size.Width,source[1]*bitmap.Size.Height,source[2]*bitmap.Size.Width,source[3]*bitmap.Size.Height);
            if(rect.Width<=0||rect.Height<=0||rect.X<0||rect.Y<0||rect.Right>bitmap.Size.Width+.01||rect.Bottom>bitmap.Size.Height+.01)
                throw new InvalidOperationException("Invalid stopwatch source rectangle: "+path);
            Thickness Insets(string name)
            {
                if(!entry.TryGetProperty(name,out var property))return new Thickness(0);
                var values=property.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
                return new Thickness(values[0],values[1],values[2],values[3]);
            }
            var minimum=entry.TryGetProperty("minimumSize",out var min)?new Size(min[0].GetDouble(),min[1].GetDouble()):default;
            var asset=new StopwatchAsset(bitmap,rect,Insets("sliceInsets"),Insets("contentInsets"),minimum,
                entry.TryGetProperty("intrinsicOpacity",out var alpha)?alpha.GetDouble():1,
                entry.TryGetProperty("dark",out var dark)&&dark.GetBoolean())
            {
                ControlCenters=entry.TryGetProperty("controlCenters",out var centers)?centers.EnumerateArray().Select(x=>x.GetDouble()).ToArray():new[]{.3,.5,.7},
                ControlCenterY=entry.TryGetProperty("controlCenterY",out var cy)?cy.GetDouble():.5
            };
            if(asset.SliceInsets.Left+asset.SliceInsets.Right>rect.Width||asset.SliceInsets.Top+asset.SliceInsets.Bottom>rect.Height)
                throw new InvalidOperationException("Invalid stopwatch slice insets: "+path);
            definitions.Add(entry.GetProperty("kind").GetString()+":"+entry.GetProperty("id").GetString(),asset);
        }
        loaded=true;
    }

    public static void DrawFrame(DrawingContext context,StopwatchAsset asset,Rect destination,double scale)
    {
        var s=asset.Source;var i=asset.SliceInsets;
        if(i==default){context.DrawImage(asset.Bitmap,s,destination);return;}
        double factor=Math.Min(scale,Math.Min(destination.Width/(i.Left+i.Right),destination.Height/(i.Top+i.Bottom)));
        double[] sx={s.X,s.X+i.Left,s.Right-i.Right,s.Right},sy={s.Y,s.Y+i.Top,s.Bottom-i.Bottom,s.Bottom};
        double[] dx={destination.X,destination.X+i.Left*factor,destination.Right-i.Right*factor,destination.Right},
            dy={destination.Y,destination.Y+i.Top*factor,destination.Bottom-i.Bottom*factor,destination.Bottom};
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)
            if(dx[x+1]>dx[x]&&dy[y+1]>dy[y])
                context.DrawImage(asset.Bitmap,new Rect(sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y]),
                    new Rect(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]));
    }

    public static void DrawCover(DrawingContext context,StopwatchAsset asset,Rect destination)
    {
        var source=asset.Source;
        double ratio=Math.Max(destination.Width/source.Width,destination.Height/source.Height);
        double width=destination.Width/ratio,height=destination.Height/ratio;
        source=new Rect(source.X+(source.Width-width)/2,source.Y+(source.Height-height)/2,width,height);
        context.DrawImage(asset.Bitmap,source,destination);
    }

    public static Rect Fit(StopwatchAsset asset,Size available)
    {
        double factor=Math.Min(available.Width/asset.Source.Width,available.Height/asset.Source.Height);
        var size=new Size(asset.Source.Width*factor,asset.Source.Height*factor);
        return new Rect((available.Width-size.Width)/2,(available.Height-size.Height)/2,size.Width,size.Height);
    }
}
