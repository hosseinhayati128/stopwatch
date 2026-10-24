using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting.Unicode;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Choose the base direction from the first strong Unicode character, never from digits or punctuation.</summary>
public static class AutoTextDirection
{
    public static FlowDirection Detect(string? text)
    {
        if (string.IsNullOrEmpty(text)) return FlowDirection.LeftToRight;
        int isolateDepth = 0;
        for (int index = 0; index < text.Length;)
        {
            var codepoint = Codepoint.ReadAt(text.AsSpan(), index, out int count);
            index += Math.Max(1, count);
            switch (codepoint.BiDiClass)
            {
                case BidiClass.LeftToRightIsolate:
                case BidiClass.RightToLeftIsolate:
                case BidiClass.FirstStrongIsolate: isolateDepth++; continue;
                case BidiClass.PopDirectionalIsolate: isolateDepth = Math.Max(0, isolateDepth - 1); continue;
            }
            if (isolateDepth > 0) continue;
            switch (codepoint.BiDiClass)
            {
                case BidiClass.LeftToRight: return FlowDirection.LeftToRight;
                case BidiClass.RightToLeft:
                case BidiClass.ArabicLetter: return FlowDirection.RightToLeft;
            }
        }
        return FlowDirection.LeftToRight;
    }

    public static void Apply(TextBlock control, string? text)
    {
        control.FlowDirection = Detect(text);
        control.TextAlignment = TextAlignment.DetectFromContent;
    }

    public static void Attach(TextBox control)
    {
        void Update()
        {
            control.FlowDirection = Detect(control.Text);
            control.TextAlignment = TextAlignment.DetectFromContent;
        }
        control.TextChanged += (_, _) => Update();
        Update();
    }
}
