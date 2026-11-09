using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Cached ink colors coordinated with each stopwatch background.</summary>
internal sealed record StopwatchTextPalette(IBrush Time,IBrush Name,IBrush Running,IBrush Paused,IBrush Recording,IBrush? Edge)
{
    private static StopwatchTextPalette Ink(string time,string name,string running,string paused,string recording,string? edge=null)
        =>new(Brush.Parse(time),Brush.Parse(name),Brush.Parse(running),Brush.Parse(paused),Brush.Parse(recording),edge==null?null:Brush.Parse(edge));
    private static readonly IReadOnlyDictionary<string,StopwatchTextPalette> Palettes=new Dictionary<string,StopwatchTextPalette>
    {
        ["navigator-parchment"]=Ink("#071C2A","#123F43","#124A36","#71300D","#8F241E"),
        ["amber-vellum"]=Ink("#27180D","#542911","#163E2C","#6A2B0C","#84201B"),
        ["midnight-chart"]=Ink("#FFF2D3","#F5D797","#94E4C5","#FFD58A","#FFA49A"),
        ["quiet-atlas"]=Ink("#17252C","#25453B","#17452E","#6B3517","#862B24"),
        ["ships-timber"]=Ink("#FFF5DC","#FFE1AC","#A7F0D0","#FFDB98","#FFB2A5"),
        ["sailcloth"]=Ink("#222A36","#4D352B","#194834","#753B1C","#8F2925"),
        ["captains-leather"]=Ink("#FFF1CC","#F9D59E","#A5EED1","#FFDB91","#FFB4A0"),
        ["aged-bronze"]=Ink("#121B18","#322414","#133C27","#552408","#741F16","#EBD39B"),
        ["tideglass"]=Ink("#031820","#092F38","#07352B","#452715","#681E21","#C2F0E8"),
        ["wano-waves"]=Ink("#10253A","#57312C","#194536","#743217","#8B2821"),
        ["celestial-navigation"]=Ink("#F5F3FF","#BFE6FF","#9AE8DC","#FFDB9A","#FFAAA9"),
        ["sky-island"]=Ink("#0A1D2C","#281E31","#163F35","#60331A","#79272B","#FFF0D1")
    };

    internal static StopwatchTextPalette For(string background)=>Palettes.TryGetValue(background,out var palette)
        ?palette:Palettes["navigator-parchment"];
}
