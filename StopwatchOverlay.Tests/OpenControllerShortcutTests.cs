using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class OpenControllerShortcutTests
    {
        [Fact]
        public void ShortcutAction_OpenController_HasStableValue14()
        {
            Assert.Equal(14, (int)ShortcutAction.OpenController);
        }

        [Fact]
        public void DefaultShortcuts_IncludesOpenController_WithWinShiftF2()
        {
            var defaults = AppSettings.DefaultShortcuts();
            Assert.True(defaults.ContainsKey(ShortcutAction.OpenController));

            var shortcut = defaults[ShortcutAction.OpenController];
            Assert.Equal(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, shortcut.Modifiers);
            Assert.Equal(0x71u, shortcut.VirtualKey); // VK_F2
            Assert.Equal("Shift+Win+F2", shortcut.Format());
        }

        [Fact]
        public void EnsureAllActions_AddsOpenController_PreservesOtherActions()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            settings.Shortcuts[ShortcutAction.CommandLeader] = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2
            settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = new Shortcut(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, 0x76); // Win+Shift+F7

            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            Assert.Equal(AppSettings.DefaultOpenControllerShortcut(), settings.Shortcuts[ShortcutAction.OpenController]);
        }

        [Fact]
        public void EnsureAllActions_CollisionFallback_UnbindsOpenController()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            // User had previously assigned Win+Shift+F2 to ShowActiveOverlay
            settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = AppSettings.DefaultOpenControllerShortcut();

            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            var s = settings.Shortcuts[ShortcutAction.OpenController];
            Assert.Equal(0u, s.VirtualKey);
            Assert.Equal(0u, s.Modifiers);
            Assert.Equal("", s.Format());
        }

        [Fact]
        public void OpenController_RoundtripPersistence_ExplicitUnbound()
        {
            var settings = new AppSettings();
            settings.Shortcuts[ShortcutAction.OpenController] = new Shortcut(0, 0);

            string json = JsonSerializer.Serialize(settings);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(loaded);
            loaded.EnsureAllActions();

            Assert.True(loaded.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            Assert.Equal(0u, loaded.Shortcuts[ShortcutAction.OpenController].VirtualKey);
        }

        [Fact]
        public void OpenController_RoundtripPersistence_CustomBinding()
        {
            var settings = new AppSettings();
            var custom = new Shortcut(Shortcut.MOD_CONTROL | Shortcut.MOD_ALT, 0x43); // Ctrl+Alt+C
            settings.Shortcuts[ShortcutAction.OpenController] = custom;

            string json = JsonSerializer.Serialize(settings);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(loaded);
            loaded.EnsureAllActions();

            Assert.True(loaded.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            Assert.Equal(custom, loaded.Shortcuts[ShortcutAction.OpenController]);
        }

        [Fact]
        public void ConflictDetection_BetweenOpenControllerAndLeader()
        {
            var leader = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2
            var controller = new Shortcut(Shortcut.MOD_WIN, 0x71); // Win+F2 -> conflict!

            bool isConflict = leader.VirtualKey != 0 &&
                              controller.VirtualKey != 0 &&
                              leader.Modifiers == controller.Modifiers &&
                              leader.VirtualKey == controller.VirtualKey;

            Assert.True(isConflict);
        }

        [Fact]
        public void DefaultOpenController_DoesNotConflictWithDefaultLeader()
        {
            var leader = AppSettings.DefaultLeaderShortcut(); // Win+F2 (MOD_WIN, 0x71)
            var controller = AppSettings.DefaultOpenControllerShortcut(); // Win+Shift+F2 (MOD_WIN | MOD_SHIFT, 0x71)

            bool isConflict = leader.VirtualKey != 0 &&
                              controller.VirtualKey != 0 &&
                              leader.Modifiers == controller.Modifiers &&
                              leader.VirtualKey == controller.VirtualKey;

            Assert.False(isConflict);
        }

        [Fact]
        public void DefaultOpenController_DoesNotConflictWithDefaultShowOverlay()
        {
            var showOverlay = AppSettings.DefaultShowActiveOverlayShortcut(); // Win+Shift+F7
            var controller = AppSettings.DefaultOpenControllerShortcut(); // Win+Shift+F2

            bool isConflict = showOverlay.VirtualKey != 0 &&
                              controller.VirtualKey != 0 &&
                              showOverlay.Modifiers == controller.Modifiers &&
                              showOverlay.VirtualKey == controller.VirtualKey;

            Assert.False(isConflict);
        }
    }
}
