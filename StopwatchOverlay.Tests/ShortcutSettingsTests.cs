using System.Collections.Generic;
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
            Assert.Equal(21, (int)ShortcutAction.SeparateOverlay);
            Assert.Equal(22, (int)ShortcutAction.MergeOverlay);
            Assert.Equal(23, (int)ShortcutAction.NextSeparatedOverlay);
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

        [Fact]
        public void DefaultCommandChainingTimeoutSeconds_IsHalfSecond()
        {
            Assert.Equal(0.5, AppSettings.DefaultCommandChainingTimeoutSeconds);
            var settings = new AppSettings();
            Assert.Equal(0.5, settings.CommandChainingTimeoutSeconds);
        }

        [Theory]
        [InlineData(0.0, 0.2)]
        [InlineData(0.1, 0.2)]
        [InlineData(0.2, 0.2)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.5, 1.5)]
        [InlineData(2.0, 2.0)]
        [InlineData(5.0, 2.0)]
        [InlineData(double.NaN, 0.5)]
        [InlineData(double.PositiveInfinity, 0.5)]
        public void NormalizeForRuntime_ClampsCommandChainingTimeoutSeconds(double input, double expected)
        {
            var settings = new AppSettings
            {
                CommandChainingTimeoutSeconds = input
            };
            settings.NormalizeForRuntime();
            Assert.Equal(expected, settings.CommandChainingTimeoutSeconds);
        }

        [Fact]
        public void CommandChainingTimeoutSeconds_RoundtripsSerialization()
        {
            var settings = new AppSettings
            {
                CommandChainingTimeoutSeconds = 1.4
            };

            string json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(restored);
            restored.NormalizeForRuntime();

            Assert.Equal(1.4, restored.CommandChainingTimeoutSeconds);
        }

        [Fact]
        public void LegacySettingsJson_WithoutCommandChainingTimeout_LoadsDefaultValue()
        {
            string json = """
            {
              "ThemeMode": "Acanthus",
              "OverlayTheme": "Acanthus Dark Elegant Olive"
            }
            """;
            var restored = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.NotNull(restored);
            restored.NormalizeForRuntime();

            Assert.Equal(0.5, restored.CommandChainingTimeoutSeconds);
        }

        [Fact]
        public void DefaultCommandModeKeys_HasNextSeparatedOverlayMappedToJ_NotTab()
        {
            var def = AppSettings.DefaultCommandModeKeys();
            Assert.True(def.ContainsKey(ShortcutAction.NextSeparatedOverlay));
            Assert.Equal(ShortcutCommandMap.VK_KEY_J, def[ShortcutAction.NextSeparatedOverlay]);
            Assert.DoesNotContain(ShortcutCommandMap.VK_TAB, def.Values);
        }

        [Fact]
        public void EnsureCommandModeKeys_MigratesLegacyTabToJ()
        {
            var settings = new AppSettings
            {
                CommandModeKeys = new Dictionary<ShortcutAction, uint>
                {
                    [ShortcutAction.NextSeparatedOverlay] = ShortcutCommandMap.VK_TAB
                }
            };
            settings.EnsureCommandModeKeys();
            Assert.Equal(ShortcutCommandMap.VK_KEY_J, settings.CommandModeKeys[ShortcutAction.NextSeparatedOverlay]);
        }

        [Fact]
        public void ValidateCommandModeKeys_UniqueKeys_ReturnsTrue()
        {
            var keys = AppSettings.DefaultCommandModeKeys();
            bool valid = AppSettings.ValidateCommandModeKeys(keys, out var error);
            Assert.True(valid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateCommandModeKeys_DuplicateKeys_ReturnsFalse()
        {
            var keys = new Dictionary<ShortcutAction, uint>(AppSettings.DefaultCommandModeKeys())
            {
                [ShortcutAction.Reset] = ShortcutCommandMap.VK_KEY_R,
                [ShortcutAction.RenameTimer] = ShortcutCommandMap.VK_KEY_R // Duplicate!
            };
            bool valid = AppSettings.ValidateCommandModeKeys(keys, out var error);
            Assert.False(valid);
            Assert.NotNull(error);
            Assert.Contains("Duplicate", error);
            Assert.Contains("R", error);
        }

        [Fact]
        public void ValidateCommandModeKeys_EscapeKey_ReturnsFalse()
        {
            var keys = new Dictionary<ShortcutAction, uint>(AppSettings.DefaultCommandModeKeys())
            {
                [ShortcutAction.Reset] = ShortcutCommandMap.VK_ESCAPE
            };
            bool valid = AppSettings.ValidateCommandModeKeys(keys, out var error);
            Assert.False(valid);
            Assert.NotNull(error);
            Assert.Contains("Escape", error);
        }

        [Fact]
        public void ValidateNoteCommandModeKeys_UniqueKeys_ReturnsTrue()
        {
            var keys = AppSettings.DefaultNoteCommandModeKeys();
            bool valid = AppSettings.ValidateNoteCommandModeKeys(keys, out var error);
            Assert.True(valid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateNoteCommandModeKeys_DuplicateKeys_ReturnsFalse()
        {
            var keys = new Dictionary<NoteCommandAction, uint>(AppSettings.DefaultNoteCommandModeKeys())
            {
                [NoteCommandAction.AddNote] = 0x31 // Duplicate '1' (same as AddTodo)
            };
            bool valid = AppSettings.ValidateNoteCommandModeKeys(keys, out var error);
            Assert.False(valid);
            Assert.NotNull(error);
            Assert.Contains("Duplicate", error);
        }

        [Fact]
        public void ShortcutCommandMap_GuidanceTexts_ContainJSwitch_NotTab()
        {
            Assert.Contains("J Switch", ShortcutCommandMap.GuidanceLine2);
            Assert.DoesNotContain("Tab Switch", ShortcutCommandMap.GuidanceLine2);
            Assert.Contains("J Switch", ShortcutCommandMap.GuidanceStatusText);
            Assert.DoesNotContain("Tab Switch", ShortcutCommandMap.GuidanceStatusText);
        }

        [Fact]
        public void ShortcutCommandMap_TryGetAction_WithCustomKeys_ResolvesCustomKey()
        {
            var customKeys = new Dictionary<ShortcutAction, uint>(AppSettings.DefaultCommandModeKeys())
            {
                [ShortcutAction.NextSeparatedOverlay] = 0x4B // 'K'
            };

            // Custom 'K' (0x4B) maps to NextSeparatedOverlay
            bool mapped = ShortcutCommandMap.TryGetAction(0x4B, customKeys, out var action);
            Assert.True(mapped);
            Assert.Equal(ShortcutAction.NextSeparatedOverlay, action);

            // Default 'J' (0x4A) does not map to NextSeparatedOverlay when overridden
            mapped = ShortcutCommandMap.TryGetAction(ShortcutCommandMap.VK_KEY_J, customKeys, out var oldAction);
            Assert.False(mapped);
        }
    }
}
