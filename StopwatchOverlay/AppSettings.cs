using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StopwatchOverlay
{
    public enum ShortcutAction
    {
        StartStop = 1,
        Reset = 2,
        ToggleOverlay = 3,
        Lap = 4,
        ToggleClock = 5,
        NewTimer = 6,
        NextTimer = 7,
        CloseTimer = 8,
        RenameTimer = 9,
        OpenDashboard = 10,
        ToggleCombinedOverlay = 11,
        CommandLeader = 12,
        ShowActiveOverlay = 13,
        OpenController = 14,
        NoteCommandLeader = 15,
        EditTimer = 16,
        UndoTimerEdit = 17,
        AddRecord = 18,
        SyncActivityWatch = 19
    }

    // VirtualKey == 0 means the action is unbound (no global hotkey).
    public record Shortcut(uint Modifiers, uint VirtualKey)
    {
        // Win32 modifier flags (used for both registration and capture).
        public const uint MOD_ALT     = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT   = 0x0004;
        public const uint MOD_WIN     = 0x0008;

        // Renders as "Ctrl+Shift+S", "Win+F5", etc. Empty string if unbound.
        public string Format()
        {
            if (VirtualKey == 0) return "";
            var parts = new List<string>();
            if ((Modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((Modifiers & MOD_WIN) != 0) parts.Add("Win");
            parts.Add(System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)VirtualKey).ToString());
            return string.Join("+", parts);
        }
    }

    public static class CloseActionChoice
    {
        public const string Ask = "Ask";
        public const string Minimize = "Minimize";
        public const string Exit = "Exit";

        public static readonly IReadOnlyList<string> All = [Ask, Minimize, Exit];

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Ask;
            string trimmed = value.Trim();
            if (trimmed.Equals(Minimize, StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Minimize to tray", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Minimize to system tray", StringComparison.OrdinalIgnoreCase))
                return Minimize;
            if (trimmed.Equals(Exit, StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Exit application", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Close completely", StringComparison.OrdinalIgnoreCase))
                return Exit;
            return Ask;
        }
    }

    public sealed class ProjectIdleRule
    {
        public bool Enabled { get; set; } = false;
        public int IdleMinutes { get; set; } = 5;
    }

    public class AppSettings
    {
        private const uint VK_F2 = 0x71;
        private const uint VK_F3 = 0x72;
        private const uint VK_F4 = 0x73;
        private const uint VK_F5 = 0x74;
        private const uint VK_F6 = 0x75;
        private const uint VK_F7 = 0x76;
        private const uint VK_F8 = 0x77;
        private const uint VK_F9 = 0x78;
        private const uint VK_F10 = 0x79;
        private const uint VK_F11 = 0x7A;
        private const uint VK_F12 = 0x7B;

        public const double DefaultCommandChainingTimeoutSeconds = 0.5;
        public const double MinimumCommandChainingTimeoutSeconds = 0.2;
        public const double MaximumCommandChainingTimeoutSeconds = 2.0;

        public int ShortcutSchemaVersion { get; set; } = 2;
        public Shortcut LeaderShortcut { get; set; } = DefaultLeaderShortcut();
        public Shortcut NoteLeaderShortcut { get; set; } = DefaultNoteLeaderShortcut();
        public Dictionary<ShortcutAction, Shortcut> Shortcuts { get; set; } = new();
        public double CommandChainingTimeoutSeconds { get; set; } = DefaultCommandChainingTimeoutSeconds;

        // Application chrome theme. Stable display names are kept in JSON for
        // backwards compatibility with the legacy "Dark" setting.
        public string ThemeMode { get; set; } = AppThemeCatalog.Midnight;
        public double UiScalePercent { get; set; } = 90;
        public TypographySettings Typography { get; set; } = new();
        public string CloseAction { get; set; } = CloseActionChoice.Ask;

        // ThemeMode remains the JSON contract so older releases can still read
        // the panel preference. The independent overlay choice is never folded
        // back into it, even when it resolves to the same palette.
        [JsonIgnore]
        public string ApplicationTheme
        {
            get => ThemeMode;
            set => ThemeMode = value;
        }

        public string OverlayTheme { get; set; } = OverlayThemeCatalog.FollowApplicationTheme;

        // Tiled application background. Custom images are copied into the app's
        // managed data folder; settings persist only their safe leaf filenames.
        public string PanelBackgroundId { get; set; } = AppBackgroundCatalog.ThemeDefault;
        public double PanelBackgroundStrength { get; set; } =
            AppBackgroundCatalog.DefaultPatternStrength;
        public List<CustomAppBackground> CustomBackgrounds { get; set; } = new();

        // Floating-overlay appearance (global, shared across timer modes).
        public string TextColor { get; set; } = "White";
        public string BorderColor { get; set; } = "Black";
        public string FontFamily { get; set; } = "Consolas";
        public int TimeFormat { get; set; } = 0;
        public double TextSize { get; set; } = 48;
        public double BorderWidth { get; set; } = 2;
        public double BackgroundOpacity { get; set; } = 50;
        public NavigatorOpaqueParts OpaqueOverlayParts { get; set; } = NavigatorOpaqueParts.Default;
        public bool HideOverlayFromCapture { get; set; } = false;

        // Layout
        public string Position { get; set; } = "Top Center";
        public int ScreenIndex { get; set; } = -1; // -1 = use default selection
        // Absolute overlay coordinates when Position == "Custom" (set by dragging the overlay).
        public bool HasCustomPosition { get; set; } = false;
        public double CustomLeft { get; set; } = 0;
        public double CustomTop { get; set; } = 0;

        // Light ring
        public bool LightRingEnabled { get; set; } = false;
        public double LightRingBrightness { get; set; } = 100;
        public double LightRingWidth { get; set; } = 20;
        public bool LightRingHideFromCapture { get; set; } = false;

        // Options
        public bool AutoStart { get; set; } = false;
        public bool ShowRecIndicator { get; set; } = false;
        public bool ClickThrough { get; set; } = false;
        public bool BlinkColon { get; set; } = false;
        public bool UseSmartCountdownInput { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;

        // Obsidian / Markdown Export
        public bool ObsidianAutoSyncEnabled { get; set; } = true;
        public string ObsidianVaultFolder { get; set; } = "";
        public string ObsidianExportFileName { get; set; } = "Stopwatch Log.md";
        public bool ObsidianLogUnnamedTimers { get; set; } = true;
        public string NotesSubfolder { get; set; } = "Notes";

        // Telegram Notes Forwarding
        public bool TelegramEnabled { get; set; } = false;
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";
        public string TelegramNotesTopicId { get; set; } = "";
        public string TelegramTodosTopicId { get; set; } = "";
        public string TelegramRemindersTopicId { get; set; } = "";

        // ActivityWatch Integration
        public bool ActivityWatchEnabled { get; set; } = false;
        public string ActivityWatchServerUrl { get; set; } = "http://localhost:5600";
        public string ActivityWatchExportFileName { get; set; } = "ActivityWatch Log.md";
        public int ActivityWatchMinDurationSeconds { get; set; } = 15;
        public bool ActivityWatchIncludeWeb { get; set; } = true;
        public bool ActivityWatchIncludeTitles { get; set; } = true;
        public bool ActivityWatchSyncOnStopwatchSync { get; set; } = true;
        public bool ActivityWatchPeriodicSyncEnabled { get; set; } = true;
        public int ActivityWatchPeriodicSyncIntervalMinutes { get; set; } = 60;

        // Internet & Network Monitoring
        public bool InternetMonitorEnabled { get; set; } = true;
        public int InternetMonitorIntervalMinutes { get; set; } = 10;
        public bool InternetMonitorOnlyDuringTimers { get; set; } = true;
        public string InternetLogFileName { get; set; } = "Internet Log.md";
        public int InternetSampleSizeBytes { get; set; } = 1_000_000;

        // Idle Stopwatch Inactivity Stopping
        public bool IdleStopUnnamedTimers { get; set; } = false;
        public int DefaultIdleStopTimeoutMinutes { get; set; } = 5;
        public bool IdleStopSubtractDuration { get; set; } = true;
        public Dictionary<string, ProjectIdleRule> ProjectIdleRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsIdleStopActiveForProject(string? projectName, out int timeoutMinutes, out bool subtractDuration)
        {
            subtractDuration = IdleStopSubtractDuration;
            timeoutMinutes = DefaultIdleStopTimeoutMinutes > 0 ? DefaultIdleStopTimeoutMinutes : 5;

            if (string.IsNullOrWhiteSpace(projectName))
            {
                return IdleStopUnnamedTimers;
            }

            string name = projectName.Trim();
            if (ProjectIdleRules != null && ProjectIdleRules.TryGetValue(name, out var rule))
            {
                if (rule.IdleMinutes > 0)
                {
                    timeoutMinutes = rule.IdleMinutes;
                }
                return rule.Enabled;
            }

            // Default for any project without an explicit rule is deactive (false)
            return false;
        }

        public void SetProjectIdleRule(string projectName, bool enabled, int idleMinutes)
        {
            int clamped = Math.Clamp(idleMinutes, 1, 1440);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                IdleStopUnnamedTimers = enabled;
                DefaultIdleStopTimeoutMinutes = clamped;
                return;
            }

            ProjectIdleRules ??= new Dictionary<string, ProjectIdleRule>(StringComparer.OrdinalIgnoreCase);
            string key = projectName.Trim();
            if (!ProjectIdleRules.TryGetValue(key, out var existing))
            {
                existing = new ProjectIdleRule();
                ProjectIdleRules[key] = existing;
            }

            existing.Enabled = enabled;
            existing.IdleMinutes = clamped;
        }

        // Last-used mode (0=Stopwatch, 1=Clock, 2=Countdown, 3=Timecode)
        public int Mode { get; set; } = 0;

        public static Shortcut DefaultLeaderShortcut() => new(Shortcut.MOD_WIN, VK_F2);
        public static Shortcut DefaultNoteLeaderShortcut() => new(Shortcut.MOD_WIN, VK_F3);
        public static Shortcut DefaultShowActiveOverlayShortcut() => new(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, VK_F7);
        public static Shortcut DefaultOpenControllerShortcut() => new(Shortcut.MOD_WIN | Shortcut.MOD_SHIFT, VK_F2);

        public static Dictionary<ShortcutAction, Shortcut> DefaultShortcuts() => new()
        {
            [ShortcutAction.CommandLeader] = DefaultLeaderShortcut(),
            [ShortcutAction.NoteCommandLeader] = DefaultNoteLeaderShortcut(),
            [ShortcutAction.ShowActiveOverlay] = DefaultShowActiveOverlayShortcut(),
            [ShortcutAction.OpenController] = DefaultOpenControllerShortcut(),
        };

        // Ensure shortcuts dictionary, leader shortcut, ShowActiveOverlay, and OpenController are initialized.
        public void EnsureAllActions()
        {
            Shortcuts ??= new Dictionary<ShortcutAction, Shortcut>();
            if (!Shortcuts.ContainsKey(ShortcutAction.CommandLeader))
            {
                Shortcuts[ShortcutAction.CommandLeader] = LeaderShortcut ?? DefaultLeaderShortcut();
            }
            LeaderShortcut ??= Shortcuts[ShortcutAction.CommandLeader];

            if (!Shortcuts.ContainsKey(ShortcutAction.NoteCommandLeader))
            {
                Shortcuts[ShortcutAction.NoteCommandLeader] = NoteLeaderShortcut ?? DefaultNoteLeaderShortcut();
            }
            NoteLeaderShortcut ??= Shortcuts[ShortcutAction.NoteCommandLeader];

            if (!Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay))
            {
                var def = DefaultShowActiveOverlayShortcut();
                bool collides = Shortcuts.Values.Any(s => s != null && s.VirtualKey != 0 && s.VirtualKey == def.VirtualKey && s.Modifiers == def.Modifiers);
                Shortcuts[ShortcutAction.ShowActiveOverlay] = collides ? new Shortcut(0, 0) : def;
            }

            if (!Shortcuts.ContainsKey(ShortcutAction.OpenController))
            {
                var def = DefaultOpenControllerShortcut();
                bool collides = Shortcuts.Values.Any(s => s != null && s.VirtualKey != 0 && s.VirtualKey == def.VirtualKey && s.Modifiers == def.Modifiers);
                Shortcuts[ShortcutAction.OpenController] = collides ? new Shortcut(0, 0) : def;
            }
        }

        public void NormalizeForRuntime()
        {
            bool isLegacy = ShortcutSchemaVersion < 2 || !Shortcuts.ContainsKey(ShortcutAction.CommandLeader);
            if (isLegacy)
            {
                LeaderShortcut ??= DefaultLeaderShortcut();
                if (LeaderShortcut.VirtualKey == 0 && LeaderShortcut.Modifiers == 0)
                {
                    LeaderShortcut = DefaultLeaderShortcut();
                }

                Shortcuts.Remove(ShortcutAction.NewTimer);
                Shortcuts.Remove(ShortcutAction.NextTimer);
                Shortcuts.Remove(ShortcutAction.CloseTimer);
                Shortcuts.Remove(ShortcutAction.StartStop);
                Shortcuts.Remove(ShortcutAction.Reset);
                Shortcuts.Remove(ShortcutAction.ToggleOverlay);
                Shortcuts.Remove(ShortcutAction.Lap);
                Shortcuts.Remove(ShortcutAction.ToggleClock);
                Shortcuts.Remove(ShortcutAction.RenameTimer);
                Shortcuts.Remove(ShortcutAction.OpenDashboard);
                Shortcuts.Remove(ShortcutAction.ToggleCombinedOverlay);

                Shortcuts[ShortcutAction.CommandLeader] = LeaderShortcut;
                ShortcutSchemaVersion = 2;
            }
            LeaderShortcut ??= Shortcuts.TryGetValue(ShortcutAction.CommandLeader, out var s) ? s : DefaultLeaderShortcut();
            Shortcuts[ShortcutAction.CommandLeader] = LeaderShortcut;

            NoteLeaderShortcut ??= Shortcuts.TryGetValue(ShortcutAction.NoteCommandLeader, out var ns) ? ns : DefaultNoteLeaderShortcut();
            Shortcuts[ShortcutAction.NoteCommandLeader] = NoteLeaderShortcut;

            if (!Shortcuts.ContainsKey(ShortcutAction.ShowActiveOverlay))
            {
                var def = DefaultShowActiveOverlayShortcut();
                bool collides = Shortcuts.Values.Any(s => s != null && s.VirtualKey != 0 && s.VirtualKey == def.VirtualKey && s.Modifiers == def.Modifiers);
                Shortcuts[ShortcutAction.ShowActiveOverlay] = collides ? new Shortcut(0, 0) : def;
            }

            if (!Shortcuts.ContainsKey(ShortcutAction.OpenController))
            {
                var def = DefaultOpenControllerShortcut();
                bool collides = Shortcuts.Values.Any(s => s != null && s.VirtualKey != 0 && s.VirtualKey == def.VirtualKey && s.Modifiers == def.Modifiers);
                Shortcuts[ShortcutAction.OpenController] = collides ? new Shortcut(0, 0) : def;
            }

            NotesSubfolder = string.IsNullOrWhiteSpace(NotesSubfolder) ? "Notes" : NotesSubfolder.Trim();

            ThemeMode = AppThemeCatalog.Normalize(ThemeMode);
            UiScalePercent = AppUiScale.Normalize(UiScalePercent);
            Typography ??= new();
            Typography.Normalize();
            OverlayTheme = OverlayThemeCatalog.Normalize(OverlayTheme);
            TextColor = NormalizeChoice(
                TextColor,
                "White",
                "Theme default", "White", "Charcoal", "Yellow", "Cyan", "Lime", "Orange", "Red", "Magenta");
            BorderColor = NormalizeChoice(
                BorderColor,
                "Black",
                "Black", "White", "Dark Gray", "Red", "Blue");
            FontFamily = NormalizeChoice(
                FontFamily,
                "Consolas",
                "Consolas", "Cascadia Mono", "Segoe UI", "Arial", "Courier New", "Lucida Console");
            Position = NormalizeChoice(
                Position,
                "Top Center",
                "Top Left", "Top Center", "Top Right", "Bottom Left", "Bottom Center", "Bottom Right", "Custom");
            TimeFormat = Math.Clamp(TimeFormat, 0, 4);
            TextSize = NormalizeRange(TextSize, 16, 120, 48);
            BorderWidth = NormalizeRange(BorderWidth, 1, 5, 2);
            BackgroundOpacity = NormalizeRange(BackgroundOpacity, 0, 100, 50);
            OpaqueOverlayParts &= NavigatorOpaqueParts.All;
            LightRingBrightness = NormalizeRange(LightRingBrightness, 10, 100, 100);
            LightRingWidth = NormalizeRange(LightRingWidth, 5, 100, 20);
            ScreenIndex = Math.Clamp(ScreenIndex, -1, 64);
            if (!double.IsFinite(CustomLeft) || !double.IsFinite(CustomTop))
            {
                HasCustomPosition = false;
                CustomLeft = 0;
                CustomTop = 0;
            }
            Mode = Math.Clamp(Mode, 0, 3);
            CommandChainingTimeoutSeconds = NormalizeRange(
                CommandChainingTimeoutSeconds,
                MinimumCommandChainingTimeoutSeconds,
                MaximumCommandChainingTimeoutSeconds,
                DefaultCommandChainingTimeoutSeconds);
            ObsidianExportFileName = string.IsNullOrWhiteSpace(ObsidianExportFileName)
                ? "Stopwatch Log.md"
                : ObsidianExportFileName.Trim();
            ObsidianVaultFolder = (ObsidianVaultFolder ?? "").Trim();
            TelegramBotToken = (TelegramBotToken ?? "").Trim();
            TelegramChatId = (TelegramChatId ?? "").Trim();
            TelegramNotesTopicId = (TelegramNotesTopicId ?? "").Trim();
            TelegramTodosTopicId = (TelegramTodosTopicId ?? "").Trim();
            TelegramRemindersTopicId = (TelegramRemindersTopicId ?? "").Trim();
            ActivityWatchServerUrl = ActivityWatch.ActivityWatchClient.NormalizeBaseUrl(ActivityWatchServerUrl);
            ActivityWatchExportFileName = string.IsNullOrWhiteSpace(ActivityWatchExportFileName)
                ? "ActivityWatch Log.md"
                : ActivityWatchExportFileName.Trim();
            ActivityWatchMinDurationSeconds = (int)NormalizeRange(ActivityWatchMinDurationSeconds, 1, 3600, 15);
            CloseAction = CloseActionChoice.Normalize(CloseAction);

            if (DefaultIdleStopTimeoutMinutes < 1)
                DefaultIdleStopTimeoutMinutes = 5;
            else if (DefaultIdleStopTimeoutMinutes > 1440)
                DefaultIdleStopTimeoutMinutes = 1440;

            ProjectIdleRules ??= new Dictionary<string, ProjectIdleRule>(StringComparer.OrdinalIgnoreCase);
            if (ProjectIdleRules.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                ProjectIdleRules = new Dictionary<string, ProjectIdleRule>(ProjectIdleRules, StringComparer.OrdinalIgnoreCase);
            }
            foreach (var rule in ProjectIdleRules.Values)
            {
                if (rule.IdleMinutes < 1) rule.IdleMinutes = 5;
                else if (rule.IdleMinutes > 1440) rule.IdleMinutes = 1440;
            }
        }

        private static string NormalizeChoice(
            string? value,
            string fallback,
            params string[] choices)
            => choices.FirstOrDefault(choice =>
                   choice.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? fallback;

        private static double NormalizeRange(
            double value,
            double minimum,
            double maximum,
            double fallback)
            => double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }

    public static class SettingsStore
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
        private static readonly object UnavailablePathsGate = new();
        private static readonly HashSet<string> UnavailablePrimaryPaths =
            new(StringComparer.OrdinalIgnoreCase);

        private enum FileReadResult
        {
            Missing,
            Success,
            Corrupt,
            Unavailable
        }

        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StopwatchOverlay",
            "settings.json");

        public static AppSettings Load() => Load(SettingsPath);

        public static AppSettings Load(string path)
        {
            FileReadResult primaryResult = TryLoadFile(
                path,
                out AppSettings? primary,
                out Exception? primaryError);
            if (primaryResult == FileReadResult.Success)
            {
                ClearUnavailable(path);
                return primary!;
            }

            string backupPath = path + ".bak";
            if (primaryError != null)
                CrashLogger.LogRecoverable(primaryError, "SettingsLoadPrimary");

            // A sharing violation, denied access, or other transient read problem
            // is not evidence of corrupt data. A backup may keep this run usable,
            // but never replace or later overwrite the potentially newer primary.
            if (primaryResult == FileReadResult.Unavailable)
            {
                ProtectUnavailable(path);
                if (TryLoadFile(backupPath, out AppSettings? unavailableBackup, out Exception? backupReadError)
                    == FileReadResult.Success)
                {
                    return unavailableBackup!;
                }

                if (backupReadError != null)
                    CrashLogger.LogRecoverable(backupReadError, "SettingsLoadBackup");
                return CreateDefaults();
            }

            if (primaryResult == FileReadResult.Corrupt)
                PreserveUnreadablePrimary(path);

            FileReadResult backupResult = TryLoadFile(
                backupPath,
                out AppSettings? backup,
                out Exception? backupError);
            if (backupResult == FileReadResult.Success)
            {
                try
                {
                    File.Copy(backupPath, path, overwrite: true);
                }
                catch (Exception exception) when (exception is
                    IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    CrashLogger.LogRecoverable(exception, "SettingsPrimaryRepair");
                }
                ClearUnavailable(path);
                return backup!;
            }

            if (backupError != null)
                CrashLogger.LogRecoverable(backupError, "SettingsLoadBackup");
            if (backupResult == FileReadResult.Unavailable)
                ProtectUnavailable(path);

            return CreateDefaults();
        }

        private static AppSettings CreateDefaults()
        {
            var fresh = new AppSettings
            {
                ShortcutSchemaVersion = 2,
                Shortcuts = AppSettings.DefaultShortcuts()
            };
            fresh.NormalizeForRuntime();
            AppBackgroundCatalog.NormalizeSettings(fresh);
            return fresh;
        }

        private static FileReadResult TryLoadFile(
            string path,
            out AppSettings? settings,
            out Exception? error)
        {
            settings = null;
            error = null;
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is
                FileNotFoundException or DirectoryNotFoundException)
            {
                return FileReadResult.Missing;
            }
            catch (Exception exception) when (exception is
                IOException or UnauthorizedAccessException or NotSupportedException)
            {
                error = exception;
                return FileReadResult.Unavailable;
            }

            try
            {
                settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (settings == null)
                    throw new JsonException("The settings document did not contain an object.");
                settings.EnsureAllActions();
                settings.NormalizeForRuntime();
                AppBackgroundCatalog.NormalizeSettings(settings);
                return FileReadResult.Success;
            }
            catch (Exception exception) when (exception is
                JsonException or NotSupportedException or ArgumentException)
            {
                error = exception;
                settings = null;
                return FileReadResult.Corrupt;
            }
        }

        private static void PreserveUnreadablePrimary(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                string preservedPath = path + ".corrupt-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
                File.Copy(path, preservedPath, overwrite: false);
                RetainPreservedCorruptFiles(path, retainedCount: 3);
            }
            catch (Exception exception) when (exception is
                IOException or UnauthorizedAccessException or NotSupportedException)
            {
                CrashLogger.LogRecoverable(exception, "SettingsCorruptPreservation");
            }
        }

        private static void RetainPreservedCorruptFiles(string path, int retainedCount)
        {
            string? directory = Path.GetDirectoryName(path);
            string leafName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(leafName))
                return;

            foreach (FileInfo obsolete in new DirectoryInfo(directory)
                         .GetFiles(leafName + ".corrupt-*", SearchOption.TopDirectoryOnly)
                         .OrderByDescending(file => file.LastWriteTimeUtc)
                         .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                         .Skip(Math.Max(1, retainedCount)))
            {
                try
                {
                    obsolete.Delete();
                }
                catch (Exception exception) when (exception is
                    IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    CrashLogger.LogRecoverable(exception, "SettingsCorruptRetention");
                }
            }
        }

        private static bool TryNormalizeStorePath(string path, out string normalizedPath)
        {
            try
            {
                normalizedPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception exception) when (exception is
                ArgumentException or NotSupportedException or PathTooLongException)
            {
                normalizedPath = string.Empty;
                return false;
            }
        }

        private static void ProtectUnavailable(string path)
        {
            if (!TryNormalizeStorePath(path, out string normalizedPath))
                return;
            lock (UnavailablePathsGate)
                UnavailablePrimaryPaths.Add(normalizedPath);
        }

        private static void ClearUnavailable(string path)
        {
            if (!TryNormalizeStorePath(path, out string normalizedPath))
                return;
            lock (UnavailablePathsGate)
                UnavailablePrimaryPaths.Remove(normalizedPath);
        }

        internal static bool IsWriteProtected(string path)
        {
            if (!TryNormalizeStorePath(path, out string normalizedPath))
                return false;
            lock (UnavailablePathsGate)
                return UnavailablePrimaryPaths.Contains(normalizedPath);
        }

        public static bool Save(AppSettings settings) => Save(settings, SettingsPath);

        public static bool Save(AppSettings settings, string path)
        {
            if (IsWriteProtected(path))
                return false;

            string? temporaryPath = null;
            bool saved = false;
            try
            {
                settings.EnsureAllActions();
                settings.NormalizeForRuntime();
                AppBackgroundCatalog.NormalizeSettings(settings);

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(json);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(
                            temporaryPath,
                            path,
                            path + ".bak",
                            ignoreMetadataErrors: true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Move(temporaryPath, path, overwrite: true);
                    }
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                temporaryPath = null;
                saved = true;
                ClearUnavailable(path);
            }
            catch (Exception exception) when (exception is
                IOException or UnauthorizedAccessException or NotSupportedException
                or ArgumentException or JsonException)
            {
                CrashLogger.LogRecoverable(exception, "SettingsSave");
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch { }
                }
            }

            return saved;
        }
    }
}
