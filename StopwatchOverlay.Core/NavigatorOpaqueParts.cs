using System;

namespace StopwatchOverlay;

[Flags]
public enum NavigatorOpaqueParts
{
    ClockFrame = 1,
    ClockMap = 2,
    MetalBorder = 4,
    MetalFill = 8,
    TimerText = 16,
    ProjectName = 32,
    ControlBoard = 64,
    ControlMap = 128,
    ControlDials = 256,
    All = 511,
    Default = ClockFrame | MetalBorder | TimerText | ProjectName | ControlDials
}
