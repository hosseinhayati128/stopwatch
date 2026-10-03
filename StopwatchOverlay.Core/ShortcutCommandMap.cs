using System;
using System.Collections.Generic;

namespace StopwatchOverlay;

/// <summary>
/// Cross-platform virtual key code mappings and action resolutions for two-stage command mode.
/// </summary>
public static class ShortcutCommandMap
{
    public const string GuidanceLine1 = "Timer command: Space Start/Stop · R Reset · O Overlay · L Lap · W Controller";
    public const string GuidanceLine2 = "C Clock · N New · T Next · X Close · P Project · D Dashboard · E Edit · V Review · A Record · S Sync · B Separate · M Merge · Tab Switch";
    public const string GuidanceStatusText = "Timer command: Space Start/Stop · R Reset · O Overlay · L Lap · W Controller · C Clock · N New · T Next · X Close · P Project · D Dashboard · E Edit · V Review · U Undo · A Record · S Sync · B Separate · M Merge · Tab Switch";

    // Virtual key codes for fixed commands
    public const uint VK_SPACE = 0x20;
    public const uint VK_ESCAPE = 0x1B;
    public const uint VK_TAB = 0x09;
    public const uint VK_KEY_R = 0x52;
    public const uint VK_KEY_O = 0x4F;
    public const uint VK_KEY_L = 0x4C;
    public const uint VK_KEY_C = 0x43;
    public const uint VK_KEY_N = 0x4E;
    public const uint VK_KEY_T = 0x54;
    public const uint VK_KEY_X = 0x58;
    public const uint VK_KEY_P = 0x50;
    public const uint VK_KEY_D = 0x44;
    public const uint VK_KEY_W = 0x57;
    public const uint VK_KEY_E = 0x45;
    public const uint VK_KEY_U = 0x55;
    public const uint VK_KEY_A = 0x41;
    public const uint VK_KEY_S = 0x53;
    public const uint VK_KEY_V = 0x56;
    public const uint VK_KEY_B = 0x42;
    public const uint VK_KEY_M = 0x4D;
    public const uint VK_KEY_J = 0x4A;

    public static readonly IReadOnlyDictionary<uint, ShortcutAction> ActionMap = new Dictionary<uint, ShortcutAction>
    {
        [VK_SPACE] = ShortcutAction.StartStop,
        [VK_KEY_R] = ShortcutAction.Reset,
        [VK_KEY_O] = ShortcutAction.ToggleOverlay,
        [VK_KEY_L] = ShortcutAction.Lap,
        [VK_KEY_C] = ShortcutAction.ToggleClock,
        [VK_KEY_N] = ShortcutAction.NewTimer,
        [VK_KEY_T] = ShortcutAction.NextTimer,
        [VK_KEY_X] = ShortcutAction.CloseTimer,
        [VK_KEY_P] = ShortcutAction.RenameTimer,
        [VK_KEY_D] = ShortcutAction.OpenDashboard,
        [VK_KEY_W] = ShortcutAction.OpenController,
        [VK_KEY_E] = ShortcutAction.EditTimer,
        [VK_KEY_U] = ShortcutAction.UndoTimerEdit,
        [VK_KEY_A] = ShortcutAction.AddRecord,
        [VK_KEY_S] = ShortcutAction.SyncActivityWatch,
        [VK_KEY_V] = ShortcutAction.PeriodicReview,
        [VK_KEY_B] = ShortcutAction.SeparateOverlay,
        [VK_KEY_M] = ShortcutAction.MergeOverlay,
        [VK_TAB] = ShortcutAction.NextSeparatedOverlay,
        [VK_KEY_J] = ShortcutAction.NextSeparatedOverlay,
    };

    public static bool TryGetAction(uint virtualKey, out ShortcutAction action)
    {
        return ActionMap.TryGetValue(virtualKey, out action);
    }

    public static bool TryGetAction(char keyChar, out ShortcutAction action)
    {
        if (keyChar == '\t')
        {
            action = ShortcutAction.NextSeparatedOverlay;
            return true;
        }
        char upper = char.ToUpperInvariant(keyChar);
        if (upper == ' ')
        {
            action = ShortcutAction.StartStop;
            return true;
        }
        return ActionMap.TryGetValue((uint)upper, out action);
    }

    public static bool IsEscape(uint virtualKey) => virtualKey == VK_ESCAPE;
}
