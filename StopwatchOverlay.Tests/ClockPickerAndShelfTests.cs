using System;
using System.Collections.Generic;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public class ClockPickerAndShelfTests
{
    [Fact]
    public void DefaultCommandModeKeys_ContainsShowAllClocksAndToggleActiveClocks()
    {
        var keys = AppSettings.DefaultCommandModeKeys();

        Assert.True(keys.ContainsKey(ShortcutAction.ShowAllClocks));
        Assert.Equal(ShortcutCommandMap.VK_KEY_K, keys[ShortcutAction.ShowAllClocks]);

        Assert.True(keys.ContainsKey(ShortcutAction.ToggleActiveClocks));
        Assert.Equal(ShortcutCommandMap.VK_KEY_H, keys[ShortcutAction.ToggleActiveClocks]);
    }

    [Fact]
    public void ShortcutCommandMap_ActionMap_MapsKAndHKeysCorrectly()
    {
        Assert.True(ShortcutCommandMap.TryGetAction(ShortcutCommandMap.VK_KEY_K, out var actionK));
        Assert.Equal(ShortcutAction.ShowAllClocks, actionK);

        Assert.True(ShortcutCommandMap.TryGetAction(ShortcutCommandMap.VK_KEY_H, out var actionH));
        Assert.Equal(ShortcutAction.ToggleActiveClocks, actionH);
    }

    [Fact]
    public void CustomCommandModeKeys_AllowsRebindingShowAllClocksAndToggleActiveClocks()
    {
        var custom = new Dictionary<ShortcutAction, uint>
        {
            [ShortcutAction.ShowAllClocks] = 0x51, // 'Q'
            [ShortcutAction.ToggleActiveClocks] = 0x59 // 'Y'
        };

        var map = ShortcutCommandMap.BuildActionMap(custom);

        Assert.Equal(ShortcutAction.ShowAllClocks, map[0x51]);
        Assert.Equal(ShortcutAction.ToggleActiveClocks, map[0x59]);
    }

    [Fact]
    public void AppSettings_BackgroundTimerReminderSettings_DefaultAndNormalizeCorrectly()
    {
        var settings = new AppSettings();

        Assert.True(settings.BackgroundTimerReminderEnabled);
        Assert.Equal(3.0, settings.BackgroundTimerReminderIntervalMinutes);
        Assert.Equal(30.0, settings.BackgroundTimerReminderDurationSeconds);
        Assert.Equal(3, settings.BackgroundTimerMaxRemindersBeforeStop);

        // Test clamping in NormalizeForRuntime
        settings.BackgroundTimerReminderIntervalMinutes = -5;
        settings.BackgroundTimerReminderDurationSeconds = 1;
        settings.BackgroundTimerMaxRemindersBeforeStop = 0;

        settings.NormalizeForRuntime();

        Assert.Equal(3.0, settings.BackgroundTimerReminderIntervalMinutes);
        Assert.Equal(30.0, settings.BackgroundTimerReminderDurationSeconds);
        Assert.Equal(3, settings.BackgroundTimerMaxRemindersBeforeStop);
    }
}
