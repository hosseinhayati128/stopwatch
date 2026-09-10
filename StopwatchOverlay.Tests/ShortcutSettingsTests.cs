using System.Text.Json;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class ShortcutSettingsTests
    {
        [Fact]
        public void DefaultLeaderShortcut_IsWinF2()
        {
            var leader = AppSettings.DefaultLeaderShortcut();
            Assert.Equal(Shortcut.MOD_WIN, leader.Modifiers);
            Assert.Equal(0x71u, leader.VirtualKey); // VK_F2
        }

        [Fact]
        public void LeaderShortcut_Format_ProducesWinF2()
        {
            var leader = AppSettings.DefaultLeaderShortcut();
            Assert.Equal("Win+F2", leader.Format());
        }

        [Fact]
        public void DefaultShortcuts_OldDirectGlobalBindingsAreNoLongerDefaults()
        {
            var shortcuts = AppSettings.DefaultShortcuts();
            Assert.DoesNotContain(ShortcutAction.StartStop, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.Reset, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.ToggleOverlay, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.Lap, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.ToggleClock, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.NewTimer, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.NextTimer, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.CloseTimer, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.RenameTimer, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.OpenDashboard, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.DoesNotContain(ShortcutAction.ToggleCombinedOverlay, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)shortcuts);
            Assert.True(shortcuts.ContainsKey(ShortcutAction.CommandLeader));
            Assert.True(shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            Assert.True(shortcuts.ContainsKey(ShortcutAction.OpenController));
        }

        [Fact]
        public void ShortcutAction_ExistingIdsRemainStableAndNewIdsAreAppended()
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
            Assert.Equal(13, (int)ShortcutAction.ShowActiveOverlay);
            Assert.Equal(14, (int)ShortcutAction.OpenController);
        }

        [Fact]
        public void EnsureAllActions_InitializesLeaderShortcutAndShortcuts()
        {
            var settings = new AppSettings { LeaderShortcut = null! };
            settings.EnsureAllActions();

            Assert.NotNull(settings.LeaderShortcut);
            Assert.Equal("Win+F2", settings.LeaderShortcut.Format());
            Assert.NotNull(settings.Shortcuts);
        }

        [Fact]
        public void LegacyShortcutSettings_MigratesWithoutChangingUnrelatedSettings()
        {
            // Simulate older settings JSON containing direct Win+F2..Win+F11 bindings and ShortcutSchemaVersion 1
            string legacyJson = """
            {
              "ShortcutSchemaVersion": 1,
              "ThemeMode": "Acanthus",
              "OverlayTheme": "Acanthus Dark Elegant Olive",
              "TextColor": "Yellow",
              "BorderColor": "White",
              "FontFamily": "Consolas",
              "TextSize": 64.0,
              "BorderWidth": 3.0,
              "BackgroundOpacity": 75.0,
              "Position": "Top Right",
              "Shortcuts": {
                "NewTimer": { "Modifiers": 8, "VirtualKey": 113 },
                "NextTimer": { "Modifiers": 8, "VirtualKey": 114 },
                "CloseTimer": { "Modifiers": 8, "VirtualKey": 115 },
                "StartStop": { "Modifiers": 8, "VirtualKey": 116 },
                "Reset": { "Modifiers": 8, "VirtualKey": 117 },
                "ToggleOverlay": { "Modifiers": 8, "VirtualKey": 118 },
                "Lap": { "Modifiers": 8, "VirtualKey": 119 },
                "ToggleClock": { "Modifiers": 8, "VirtualKey": 120 },
                "RenameTimer": { "Modifiers": 8, "VirtualKey": 121 },
                "OpenDashboard": { "Modifiers": 8, "VirtualKey": 122 },
                "ToggleCombinedOverlay": { "Modifiers": 8, "VirtualKey": 123 }
              }
            }
            """;

            var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.NotNull(settings);

            // Prior to normalization, ShortcutSchemaVersion is 1 (< 2)
            Assert.Equal(1, settings.ShortcutSchemaVersion);

            settings.NormalizeForRuntime();

            // After normalization:
            // 1. Schema version updated to 2
            Assert.Equal(2, settings.ShortcutSchemaVersion);

            // 2. Leader is Win+F2
            Assert.NotNull(settings.LeaderShortcut);
            Assert.Equal(Shortcut.MOD_WIN, settings.LeaderShortcut.Modifiers);
            Assert.Equal(0x71u, settings.LeaderShortcut.VirtualKey);
            Assert.Equal("Win+F2", settings.LeaderShortcut.Format());

            // 3. Old direct action shortcuts are no longer active
            Assert.DoesNotContain(ShortcutAction.StartStop, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)settings.Shortcuts);
            Assert.DoesNotContain(ShortcutAction.NewTimer, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)settings.Shortcuts);
            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.CommandLeader));

            // 4. Unrelated settings are completely preserved
            Assert.Equal("Acanthus", settings.ThemeMode);
            Assert.Equal("Acanthus Dark Elegant Olive", settings.OverlayTheme);
            Assert.Equal("Yellow", settings.TextColor);
            Assert.Equal("White", settings.BorderColor);
            Assert.Equal("Consolas", settings.FontFamily);
            Assert.Equal(64.0, settings.TextSize);
            Assert.Equal(3.0, settings.BorderWidth);
            Assert.Equal(75.0, settings.BackgroundOpacity);
            Assert.Equal("Top Right", settings.Position);
        }

        [Fact]
        public void UnversionedLegacySettings_MigratesToSchemaVersion2AndLeader()
        {
            // Simulate older settings JSON without any ShortcutSchemaVersion property
            string legacyJson = """
            {
              "ThemeMode": "Midnight",
              "Shortcuts": {
                "StartStop": { "Modifiers": 8, "VirtualKey": 116 },
                "Reset": { "Modifiers": 8, "VirtualKey": 117 }
              }
            }
            """;

            var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.NotNull(settings);

            // Because CommandLeader is missing, NormalizeForRuntime migrates legacy shortcuts
            settings.NormalizeForRuntime();

            Assert.Equal(2, settings.ShortcutSchemaVersion);
            Assert.NotNull(settings.LeaderShortcut);
            Assert.Equal("Win+F2", settings.LeaderShortcut.Format());
            Assert.DoesNotContain(ShortcutAction.StartStop, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)settings.Shortcuts);
            Assert.DoesNotContain(ShortcutAction.Reset, (System.Collections.Generic.IDictionary<ShortcutAction, Shortcut>)settings.Shortcuts);
            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.CommandLeader));
        }

        [Fact]
        public void DefaultShowActiveOverlayShortcut_IsWinShiftF7()
        {
            var shortcut = AppSettings.DefaultShowActiveOverlayShortcut();
            Assert.Equal(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, shortcut.Modifiers);
            Assert.Equal(0x76u, shortcut.VirtualKey); // VK_F7
            Assert.Equal("Shift+Win+F7", shortcut.Format());
        }

        [Fact]
        public void EnsureAllActions_InitializesShowActiveOverlay_WithoutCollision()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            var s = settings.Shortcuts[ShortcutAction.ShowActiveOverlay];
            Assert.Equal(AppSettings.DefaultShowActiveOverlayShortcut(), s);
        }

        [Fact]
        public void EnsureAllActions_InitializesShowActiveOverlay_FallsBackToUnboundOnCollision()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            // Simulate a user who assigned Win+Shift+F7 to another action
            var customCombo = AppSettings.DefaultShowActiveOverlayShortcut();
            settings.Shortcuts[ShortcutAction.ToggleOverlay] = customCombo;

            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            var s = settings.Shortcuts[ShortcutAction.ShowActiveOverlay];
            // Must be unbound (0, 0) because of collision
            Assert.Equal(0u, s.VirtualKey);
            Assert.Equal(0u, s.Modifiers);
        }

        [Fact]
        public void ShowActiveOverlay_RoundtripsSerialization_IncludingUnbound()
        {
            var settings = new AppSettings();
            settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = new Shortcut(0, 0);

            string json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(restored);
            restored.EnsureAllActions();

            Assert.True(restored.Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay));
            Assert.Equal(0u, restored.Shortcuts[ShortcutAction.ShowActiveOverlay].VirtualKey);
        }

        [Fact]
        public void DefaultOpenControllerShortcut_IsWinShiftF2()
        {
            var shortcut = AppSettings.DefaultOpenControllerShortcut();
            Assert.Equal(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, shortcut.Modifiers);
            Assert.Equal(0x71u, shortcut.VirtualKey); // VK_F2
            Assert.Equal("Shift+Win+F2", shortcut.Format());
        }

        [Fact]
        public void EnsureAllActions_InitializesOpenController_WithoutCollision()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            var s = settings.Shortcuts[ShortcutAction.OpenController];
            Assert.Equal(AppSettings.DefaultOpenControllerShortcut(), s);
        }

        [Fact]
        public void EnsureAllActions_InitializesOpenController_FallsBackToUnboundOnCollision()
        {
            var settings = new AppSettings();
            settings.Shortcuts.Clear();
            // Simulate a user who assigned Win+Shift+F2 to another action
            var customCombo = AppSettings.DefaultOpenControllerShortcut();
            settings.Shortcuts[ShortcutAction.ToggleOverlay] = customCombo;

            settings.EnsureAllActions();

            Assert.True(settings.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            var s = settings.Shortcuts[ShortcutAction.OpenController];
            Assert.Equal(0u, s.VirtualKey);
            Assert.Equal(0u, s.Modifiers);
        }

        [Fact]
        public void OpenController_RoundtripsSerialization_IncludingUnbound()
        {
            var settings = new AppSettings();
            settings.Shortcuts[ShortcutAction.OpenController] = new Shortcut(0, 0);

            string json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(restored);
            restored.EnsureAllActions();

            Assert.True(restored.Shortcuts.ContainsKey(ShortcutAction.OpenController));
            Assert.Equal(0u, restored.Shortcuts[ShortcutAction.OpenController].VirtualKey);
        }
    }
}
