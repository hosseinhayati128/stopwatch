using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace StopwatchOverlay;

public sealed class TypographyStyle
{
    public bool Enabled { get; set; }
    public double SizePercent { get; set; } = 100;
    public string Color { get; set; } = TypographySettings.ThemeDefault;

    public void Normalize()
    {
        SizePercent = double.IsFinite(SizePercent) ? Math.Clamp(SizePercent, 75, 175) : 100;
        Color = TypographySettings.NormalizeColor(Color);
    }
}

public sealed class TypographySettings
{
    public const string ThemeDefault = "Theme default";
    public const string ThemeText = "Theme text";
    public const string ThemeAccent = "Theme accent";
    public const string ThemeMuted = "Theme muted";
    public TypographyStyle Global { get; set; } = new();
    public Dictionary<string, TypographyStyle> Sections { get; set; } = new();

    public static readonly (string Key, string Label)[] Scopes =
    [
        ("Controller", "Controller"), ("Dashboard", "Dashboard & records"),
        ("Settings", "Settings"), ("Notes", "Notes, todos & reminders"),
        ("Dialogs", "Shortcuts & dialogs"), ("Overlay", "Floating overlay")
    ];

    public static readonly string[] ColorChoices =
    [ThemeDefault, ThemeText, ThemeAccent, ThemeMuted, "#F3DEAC", "#5A341D", "#FFFFFF", "#202124", "#207F78", "#C47437"];

    public void Normalize()
    {
        Global ??= new();
        Global.Normalize();
        Sections ??= new();
        foreach (var (key, _) in Scopes)
        {
            if (!Sections.TryGetValue(key, out var style) || style == null)
                Sections[key] = style = new();
            style.Normalize();
        }
    }

    public TypographyStyle Resolve(string scope)
        => Sections.TryGetValue(scope, out var style) && style is { Enabled: true } ? style : Global;

    public static string NormalizeColor(string? value)
    {
        string text = value?.Trim() ?? "";
        string? theme = ColorChoices.Take(4).FirstOrDefault(choice => choice.Equals(text, StringComparison.OrdinalIgnoreCase));
        return theme ?? (TryHexColor(text, out var color) ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : ThemeDefault);
    }

    public static bool TryHexColor(string? value, out Color color)
    {
        color = default;
        string hex = value?.Trim().TrimStart('#') ?? "";
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return false;
        color = System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
