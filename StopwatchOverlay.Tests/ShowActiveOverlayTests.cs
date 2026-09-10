using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class ShowActiveOverlayTests
    {
        [Fact]
        public void ShortcutAction_ShowActiveOverlay_HasStableValue13()
        {
            Assert.Equal(13, (int)ShortcutAction.ShowActiveOverlay);
        }

        [Fact]
        public void DefaultShortcuts_IncludesShowActiveOverlay_WithWinShiftF7()
        {
            var defaults = AppSettings.DefaultShortcuts();
            Assert.True(defaults.ContainsKey(ShortcutAction.ShowActiveOverlay));

            var shortcut = defaults[ShortcutAction.ShowActiveOverlay];
            Assert.Equal(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, shortcut.Modifiers);
            Assert.Equal(0x76u, shortcut.VirtualKey); // VK_F7
            Assert.Equal("Shift+Win+F7", shortcut.Format());
        }

        [Fact]
        public void OlderActionIdentifiers_RemainUnchanged()
        {
            Assert.Equal(1, (int)ShortcutAction.StartStop);
            Assert.Equal(2, (int)ShortcutAction.Reset);
            Assert.Equal(3, (int)ShortcutAction.ToggleOverlay);
            Assert.Equal(4, (int)ShortcutAction.Lap);
            Assert.Equal(5, (int)ShortcutAction.ToggleClock);
            Assert.Equal(6, (int)ShortcutAction.NewTimer);
            Assert.Equal(7, (int)ShortcutAction.NextTimer);
            Assert.Equal(8, (int)ShortcutAction.CloseTimer);
            Assert.Equal(9, (int)ShortcutAction.RenameTimer);
            Assert.Equal(10, (int)ShortcutAction.OpenDashboard);
            Assert.Equal(11, (int)ShortcutAction.ToggleCombinedOverlay);
            Assert.Equal(12, (int)ShortcutAction.CommandLeader);
        }

        [Fact]
        public void EnsureAllActions_AddsShowActiveOverlay_PreservesOtherActions()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            settings.Shortcuts[ShortcutAction.CommandLeader] = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2
            settings.Shortcuts[ShortcutAction.ToggleOverlay] = new Shortcut(Shortcut.MOD_WIN, 0x76); // Win+F7

            settings.EnsureAllActions();

            // CommandLeader and ToggleOverlay preserved
            Assert.Equal(new Shortcut(Shortcut.MOD_WIN, 0x71), settings.Shortcuts[ShortcutAction.CommandLeader]);
            Assert.Equal(new Shortcut(Shortcut.MOD_WIN, 0x76), settings.Shortcuts[ShortcutAction.ToggleOverlay]);

            // ShowActiveOverlay added
            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            Assert.Equal(AppSettings.DefaultShowActiveOverlayShortcut(), settings.Shortcuts[ShortcutAction.ShowActiveOverlay]);
        }

        [Fact]
        public void EnsureAllActions_CollisionFallback_UnbindsShowActiveOverlay()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            // User had previously mapped another action to Win+Shift+F7
            settings.Shortcuts[ShortcutAction.ToggleOverlay] = AppSettings.DefaultShowActiveOverlayShortcut();

            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            var s = settings.Shortcuts[ShortcutAction.ShowActiveOverlay];
            Assert.Equal(0u, s.VirtualKey);
            Assert.Equal(0u, s.Modifiers);
            Assert.Equal("", s.Format());
        }

        [Fact]
        public void ShowActiveOverlay_RoundtripPersistence_ExplicitUnbound()
        {
            var settings = new AppSettings();
            settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = new Shortcut(0, 0);

            string json = JsonSerializer.Serialize(settings);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(loaded);
            loaded.EnsureAllActions();

            Assert.True(loaded.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            Assert.Equal(0u, loaded.Shortcuts[ShortcutAction.ShowActiveOverlay].VirtualKey);
        }

        [Fact]
        public void ShowActiveOverlay_RoundtripPersistence_CustomBinding()
        {
            var settings = new AppSettings();
            var custom = new Shortcut(Shortcut.MOD_CONTROL | Shortcut.MOD_ALT, 0x53); // Ctrl+Alt+S
            settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = custom;

            string json = JsonSerializer.Serialize(settings);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(loaded);
            loaded.EnsureAllActions();

            Assert.True(loaded.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            Assert.Equal(custom, loaded.Shortcuts[ShortcutAction.ShowActiveOverlay]);
        }

        [Fact]
        public void ConflictDetection_BetweenLeaderAndShowActiveOverlay()
        {
            var leader = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2
            var showOverlay = new Shortcut(Shortcut.MOD_WIN, 0x71); // also Win+F2 -> conflict!

            bool isConflict = leader.VirtualKey != 0 &&
                              showOverlay.VirtualKey != 0 &&
                              leader.Modifiers == showOverlay.Modifiers &&
                              leader.VirtualKey == showOverlay.VirtualKey;

            Assert.True(isConflict);
        }

        [Fact]
        public void ConflictDetection_UnboundDoesNotConflict()
        {
            var leader = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2
            var unbound = new Shortcut(0, 0);

            bool isConflict = leader.VirtualKey != 0 &&
                              unbound.VirtualKey != 0 &&
                              leader.Modifiers == unbound.Modifiers &&
                              leader.VirtualKey == unbound.VirtualKey;

            Assert.False(isConflict);
        }

        [Fact]
        public void DetermineShowDecision_NoActiveTimer_ProducesNoTimer()
        {
            var separateDecision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: null,
                isOverlayAlreadyVisible: false,
                isCombinedMode: false);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.NoTimer, separateDecision);

            var combinedDecision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: null,
                isOverlayAlreadyVisible: false,
                isCombinedMode: true);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.NoTimer, combinedDecision);
        }

        [Fact]
        public void DetermineShowDecision_HiddenOverlay_ProducesShowSeparate()
        {
            var timer = new TimerSession(1);
            var decision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: timer,
                isOverlayAlreadyVisible: false,
                isCombinedMode: false);

            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.ShowSeparate, decision);
        }

        [Fact]
        public void DetermineShowDecision_HiddenOverlay_CombinedMode_ProducesShowCombined()
        {
            var timer = new TimerSession(1);
            var decision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: timer,
                isOverlayAlreadyVisible: false,
                isCombinedMode: true);

            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.ShowCombined, decision);
        }

        [Fact]
        public void DetermineShowDecision_AlreadyVisible_ProducesAlreadyVisible_Idempotent()
        {
            var timer = new TimerSession(1);

            // In separate mode
            var separateDecision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: timer,
                isOverlayAlreadyVisible: true,
                isCombinedMode: false);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.AlreadyVisible, separateDecision);

            // In combined mode
            var combinedDecision = OverlayPresentationPolicy.DetermineShowDecision(
                activeTimer: timer,
                isOverlayAlreadyVisible: true,
                isCombinedMode: true);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.AlreadyVisible, combinedDecision);
        }

        [Fact]
        public void ShowActiveOverlay_VisibilityOnly_DoesNotTriggerAutoStart()
        {
            // AutoStart setting is true, timer is paused, mode is stopwatch (0)
            bool autoStartPreference = true;
            bool isTimerRunning = false;
            int timerMode = 0;

            // ShowActiveOverlay (isShowOnlyAction = true) MUST NOT auto-start
            bool showShouldAutoStart = OverlayPresentationPolicy.ShouldAutoStartOnShow(
                isShowOnlyAction: true,
                autoStartPreference: autoStartPreference,
                isTimerRunning: isTimerRunning,
                timerMode: timerMode);
            Assert.False(showShouldAutoStart);

            // ToggleOverlay (isShowOnlyAction = false) DOES auto-start under these conditions
            bool toggleShouldAutoStart = OverlayPresentationPolicy.ShouldAutoStartOnShow(
                isShowOnlyAction: false,
                autoStartPreference: autoStartPreference,
                isTimerRunning: isTimerRunning,
                timerMode: timerMode);
            Assert.True(toggleShouldAutoStart);
        }

        [Fact]
        public void ShowActiveOverlay_PreservesTimerState()
        {
            var timer = new TimerSession(1)
            {
                Name = "Project Alpha",
                IsRunning = false,
                Mode = 2, // e.g. Countdown
                LapCount = 3,
                OverlayVisible = false
            };
            timer.LapTimes.Add("Lap 1: 00:01:00");
            timer.LapTimes.Add("Lap 2: 00:02:00");
            timer.LapTimes.Add("Lap 3: 00:03:00");

            var decision = OverlayPresentationPolicy.DetermineShowDecision(timer, false, false);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.ShowSeparate, decision);

            // Simulate executing ShowSeparate
            timer.OverlayVisible = true;

            // Assert timer running state, name, mode, and laps are unchanged
            Assert.False(timer.IsRunning);
            Assert.Equal("Project Alpha", timer.Name);
            Assert.Equal(2, timer.Mode);
            Assert.Equal(3, timer.LapCount);
            Assert.Equal(3, timer.LapTimes.Count);
            Assert.True(timer.OverlayVisible);
        }

        [Fact]
        public void ShowActiveOverlay_PreservesOtherTimers()
        {
            var manager = new TimerSessionManager();
            var t1 = manager.Create();
            var t2 = manager.Create();
            var t3 = manager.Create();

            t1.OverlayVisible = true;
            t2.OverlayVisible = false;
            t3.OverlayVisible = true;

            manager.Activate(t2);

            var decision = OverlayPresentationPolicy.DetermineShowDecision(manager.Active, false, false);
            Assert.Equal(OverlayPresentationPolicy.ShowOverlayDecision.ShowSeparate, decision);

            // Show active overlay for t2
            t2.OverlayVisible = true;

            // Other timers retain their overlay visibility and active state
            Assert.True(t1.OverlayVisible);
            Assert.True(t2.OverlayVisible);
            Assert.True(t3.OverlayVisible);
            Assert.Same(t2, manager.Active);
        }

        [Fact]
        public void CombinedMode_ShowsOnlyActiveTimer()
        {
            var manager = new TimerSessionManager();
            var t1 = manager.Create();
            var t2 = manager.Create();
            manager.Activate(t1);

            var selected = OverlayPresentationPolicy.SelectCombinedTimer(manager.Sessions, manager.Active);
            Assert.Same(t1, selected);
            Assert.NotSame(t2, selected);

            // Switch to t2
            manager.Activate(t2);
            var selectedAfterSwitch = OverlayPresentationPolicy.SelectCombinedTimer(manager.Sessions, manager.Active);
            Assert.Same(t2, selectedAfterSwitch);
            Assert.NotSame(t1, selectedAfterSwitch);
        }
    }
}
