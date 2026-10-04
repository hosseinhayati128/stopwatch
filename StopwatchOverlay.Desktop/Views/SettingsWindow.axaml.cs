using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using StopwatchOverlay.ActivityWatch;
using StopwatchOverlay.Internet;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<ProjectHistoryView>? _historyProvider;
    private readonly DispatcherTimer _previewTimer;
    private bool _loading;
    private bool _committing;
    private bool _sliderInteractionActive;
    private bool _closed;
    private bool _previewFailureReported;

    internal event Action<SettingsChangeKind>? SettingsChanged;
    internal event Action? SettingsInteractionStarted;
    internal event Action? SettingsInteractionCompleted;
    public event Action? ShowOverlayRequested;
    public event Action? PeriodicReviewRequested;

    internal string CurrentCategory { get; private set; } = "Overlay";

    public SettingsWindow() : this(new AppSettings())
    {
    }

    public SettingsWindow(AppSettings settings, Func<ProjectHistoryView>? historyProvider = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _historyProvider = historyProvider;
        InitializeComponent();

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            if (!_closed)
                UpdatePreviewSafely();
        };

        LoadControls();
        WireChanges();
        UpdatePreviewSafely();
        TelegramOutboxStore.OutboxChanged += OnOutboxChanged;
    }

    internal void ReloadFromSettings()
    {
        if (_closed)
            return;

        LoadControls();
        SchedulePreviewFromAppliedSettings();
    }

    internal void SchedulePreviewFromAppliedSettings()
    {
        if (_closed)
            return;

        if (!_previewTimer.IsEnabled)
            _previewTimer.Start();
    }

    private void LoadControls()
    {
        _loading = true;
        try
        {
            ThemeCombo.ItemsSource = AppThemeCatalog.All;
            ThemeCombo.SelectedItem = AppThemeCatalog.Normalize(_settings.ThemeMode);
            OverlayThemeCombo.ItemsSource = OverlayThemeCatalog.All;
            OverlayThemeCombo.SelectedItem = OverlayThemeCatalog.Normalize(_settings.OverlayTheme);

            ScreenCombo.Items.Clear();
            ScreenCombo.Items.Add("All displays");
            var screens = Screens.All;
            for (int i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                string name = !string.IsNullOrWhiteSpace(screen.DisplayName)
                    ? screen.DisplayName
                    : $"Display {i + 1} ({screen.Bounds.Width}x{screen.Bounds.Height})";
                ScreenCombo.Items.Add(screen.IsPrimary ? $"{name} (Primary)" : name);
            }
            ScreenCombo.SelectedIndex = SettingsChangePolicy.ResolveScreenComboIndex(
                _settings.ScreenIndex,
                screens.Count);

            SetItems(PositionCombo, ["Top Left", "Top Center", "Top Right", "Bottom Left", "Bottom Center", "Bottom Right", "Custom"], _settings.Position);
            SetItems(TextColorCombo, ["Theme default", "White", "Charcoal", "Yellow", "Cyan", "Lime", "Orange", "Red", "Magenta"], _settings.TextColor);
            SetItems(BorderColorCombo, ["Black", "White", "Dark Gray", "Red", "Blue"], _settings.BorderColor);
            SetItems(FontCombo, ["Consolas", "Cascadia Mono", "Segoe UI", "Arial", "Courier New", "Lucida Console"], _settings.FontFamily);
            SetItems(FormatCombo, ["HH:MM:SS.t", "HH:MM:SS", "MM:SS.t", "MM:SS", "HH:MM"], null);
            FormatCombo.SelectedIndex = Math.Clamp(_settings.TimeFormat, 0, FormatCombo.Items.Count - 1);

            TextSizeSlider.Value = _settings.TextSize;
            UiScaleSlider.Value = AppUiScale.Normalize(_settings.UiScalePercent);
            LoadTypographyEditors();
            BorderWidthSlider.Value = _settings.BorderWidth;
            BackgroundOpacitySlider.Value = _settings.BackgroundOpacity;
            InactiveSeparatedOpacitySlider.Value = _settings.InactiveSeparatedOverlayOpacity;
            foreach (var (control, part) in TransparencyPartControls)
                control.IsChecked = (_settings.OpaqueOverlayParts & part) != 0;
            BackgroundStrengthSlider.Value = _settings.PanelBackgroundStrength;
            ClickThroughCheck.IsChecked = _settings.ClickThrough;
            HideOverlayCaptureCheck.IsChecked = _settings.HideOverlayFromCapture;
            LightRingEnabledCheck.IsChecked = _settings.LightRingEnabled;
            LightRingBrightnessSlider.Value = _settings.LightRingBrightness;
            LightRingWidthSlider.Value = _settings.LightRingWidth;
            LightRingCaptureCheck.IsChecked = _settings.LightRingHideFromCapture;
            AutoStartCheck.IsChecked = _settings.AutoStart;
            RecCheck.IsChecked = _settings.ShowRecIndicator;
            BlinkCheck.IsChecked = _settings.BlinkColon;
            SmartInputCheck.IsChecked = _settings.UseSmartCountdownInput;
            CommandChainingTimeoutSlider.Value = _settings.CommandChainingTimeoutSeconds;
            StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
            CloseActionCombo.SelectedIndex = CloseActionChoice.Normalize(_settings.CloseAction) switch
            {
                CloseActionChoice.Minimize => 1,
                CloseActionChoice.Exit => 2,
                _ => 0
            };
            ObsidianAutoSyncCheck.IsChecked = _settings.ObsidianAutoSyncEnabled;
            ObsidianLogUnnamedCheck.IsChecked = _settings.ObsidianLogUnnamedTimers;
            ObsidianFolderTextBox.Text = _settings.ObsidianVaultFolder;
            ObsidianFileNameTextBox.Text = _settings.ObsidianExportFileName;
            TelegramEnabledCheck.IsChecked = _settings.TelegramEnabled;
            TelegramBotTokenTextBox.Text = _settings.TelegramBotToken;
            TelegramChatIdTextBox.Text = _settings.TelegramChatId;
            TelegramNotesTopicTextBox.Text = _settings.TelegramNotesTopicId;
            TelegramTodosTopicTextBox.Text = _settings.TelegramTodosTopicId;
            TelegramRemindersTopicTextBox.Text = _settings.TelegramRemindersTopicId;
            ActivityWatchEnabledCheck.IsChecked = _settings.ActivityWatchEnabled;
            ActivityWatchSyncOnStopwatchCheck.IsChecked = _settings.ActivityWatchSyncOnStopwatchSync;
            ActivityWatchPeriodicSyncCheck.IsChecked = _settings.ActivityWatchPeriodicSyncEnabled;
            ActivityWatchPeriodicSyncIntervalTextBox.Text = _settings.ActivityWatchPeriodicSyncIntervalMinutes.ToString();
            ActivityWatchIncludeTitlesCheck.IsChecked = _settings.ActivityWatchIncludeTitles;
            ActivityWatchIncludeWebCheck.IsChecked = _settings.ActivityWatchIncludeWeb;
            ActivityWatchServerUrlTextBox.Text = _settings.ActivityWatchServerUrl;
            ActivityWatchFileNameTextBox.Text = _settings.ActivityWatchExportFileName;
            ActivityWatchMinDurationTextBox.Text = _settings.ActivityWatchMinDurationSeconds.ToString();
            PeriodicReviewEnabledCheck.IsChecked = _settings.PeriodicReviewEnabled;
            PeriodicReviewIntervalTextBox.Text = _settings.PeriodicReviewIntervalMinutes.ToString();
            PeriodicReviewSnoozeTextBox.Text = _settings.PeriodicReviewSnoozeMinutes.ToString();
            PeriodicReviewAutoDismissTextBox.Text = _settings.PeriodicReviewAutoDismissSeconds.ToString();
            PeriodicReviewMinDurationTextBox.Text = _settings.PeriodicReviewMinDurationSeconds.ToString();
            PeriodicReviewMinPercentTextBox.Text = _settings.PeriodicReviewMinActivityPercent.ToString("0.#");
            InternetMonitorEnabledCheck.IsChecked = _settings.InternetMonitorEnabled;
            InternetMonitorOnlyDuringTimersCheck.IsChecked = _settings.InternetMonitorOnlyDuringTimers;
            InternetCheckIntervalTextBox.Text = _settings.InternetMonitorIntervalMinutes.ToString();
            InternetFileNameTextBox.Text = _settings.InternetLogFileName;
            int retDays = _settings.InternetLogRetentionDays;
            InternetRetentionCombo.SelectedIndex = retDays switch
            {
                30 => 1,
                14 => 2,
                7 => 3,
                _ => 0
            };
            UpdateObsidianPathPreview();
            RefreshBackgroundChoices(_settings.PanelBackgroundId);
            UpdateValueLabels();
            UpdateDependentControlStates();
            UpdateTelegramOutboxUi();
            LoadIdleStopControls();
            LoadCategoryControls();
        }
        finally
        {
            _loading = false;
        }
    }

    private static void SetItems(ComboBox comboBox, string[] items, string? selected)
    {
        comboBox.ItemsSource = items;
        comboBox.SelectedItem = items.FirstOrDefault(item =>
            item.Equals(selected, StringComparison.OrdinalIgnoreCase)) ?? items[0];
    }

    private void WireChanges()
    {
        ThemeCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.Theme);
        OverlayThemeCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayTheme);
        ScreenCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayScreen);
        PositionCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayPosition);
        TextColorCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayAppearance);
        BorderColorCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayAppearance);
        FontCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayGeometry);
        FormatCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayGeometry);
        BackgroundCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.BackgroundSelection);
        CloseActionCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.Behavior);

        WireSlider(TextSizeSlider, SettingsChangeKind.OverlayGeometry);
        WireSlider(UiScaleSlider, SettingsChangeKind.ApplicationScale);
        WireSlider(BorderWidthSlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(BackgroundOpacitySlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(InactiveSeparatedOpacitySlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(BackgroundStrengthSlider, SettingsChangeKind.BackgroundStrength);
        WireSlider(LightRingBrightnessSlider, SettingsChangeKind.LightRingAppearance);
        WireSlider(LightRingWidthSlider, SettingsChangeKind.LightRingAppearance);
        WireSlider(CommandChainingTimeoutSlider, SettingsChangeKind.Behavior);

        WireCheckBox(ClickThroughCheck, SettingsChangeKind.OverlayInteraction);
        WireCheckBox(HideOverlayCaptureCheck, SettingsChangeKind.OverlayInteraction);
        foreach (var (control, _) in TransparencyPartControls)
            WireCheckBox(control, SettingsChangeKind.OverlayAppearance);
        WireCheckBox(LightRingEnabledCheck, SettingsChangeKind.LightRingVisibility);
        WireCheckBox(LightRingCaptureCheck, SettingsChangeKind.LightRingAppearance);
        WireCheckBox(AutoStartCheck, SettingsChangeKind.Behavior);
        WireCheckBox(RecCheck, SettingsChangeKind.Behavior);
        WireCheckBox(BlinkCheck, SettingsChangeKind.Behavior);
        WireCheckBox(SmartInputCheck, SettingsChangeKind.Behavior);
        WireCheckBox(StartWithWindowsCheck, SettingsChangeKind.Startup);
        WireCheckBox(ObsidianAutoSyncCheck, SettingsChangeKind.ObsidianExport);
        WireCheckBox(ObsidianLogUnnamedCheck, SettingsChangeKind.ObsidianExport);

        ObsidianFolderTextBox.TextChanged += (_, _) =>
        {
            UpdateObsidianPathPreview();
            CommitControls(SettingsChangeKind.ObsidianExport);
        };
        ObsidianFileNameTextBox.TextChanged += (_, _) =>
        {
            UpdateObsidianPathPreview();
            CommitControls(SettingsChangeKind.ObsidianExport);
        };

        WireCheckBox(TelegramEnabledCheck, SettingsChangeKind.Telegram);
        TelegramBotTokenTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.Telegram);
        TelegramChatIdTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.Telegram);
        TelegramNotesTopicTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.Telegram);
        TelegramTodosTopicTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.Telegram);
        TelegramRemindersTopicTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.Telegram);

        WireCheckBox(ActivityWatchEnabledCheck, SettingsChangeKind.ActivityWatch);
        WireCheckBox(ActivityWatchSyncOnStopwatchCheck, SettingsChangeKind.ActivityWatch);
        WireCheckBox(ActivityWatchPeriodicSyncCheck, SettingsChangeKind.ActivityWatch);
        ActivityWatchPeriodicSyncIntervalTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.ActivityWatch);
        WireCheckBox(ActivityWatchIncludeTitlesCheck, SettingsChangeKind.ActivityWatch);
        WireCheckBox(ActivityWatchIncludeWebCheck, SettingsChangeKind.ActivityWatch);
        ActivityWatchServerUrlTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.ActivityWatch);
        ActivityWatchFileNameTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.ActivityWatch);
        ActivityWatchMinDurationTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.ActivityWatch);

        WireCheckBox(PeriodicReviewEnabledCheck, SettingsChangeKind.PeriodicReview);
        PeriodicReviewIntervalTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.PeriodicReview);
        PeriodicReviewSnoozeTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.PeriodicReview);
        PeriodicReviewAutoDismissTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.PeriodicReview);
        PeriodicReviewMinDurationTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.PeriodicReview);
        PeriodicReviewMinPercentTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.PeriodicReview);

        WireCheckBox(InternetMonitorEnabledCheck, SettingsChangeKind.InternetMonitor);
        WireCheckBox(InternetMonitorOnlyDuringTimersCheck, SettingsChangeKind.InternetMonitor);
        InternetCheckIntervalTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.InternetMonitor);
        InternetFileNameTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.InternetMonitor);

        WireCheckBox(IdleStopUnnamedCheck, SettingsChangeKind.IdleStop);
        WireCheckBox(IdleStopSubtractCheck, SettingsChangeKind.IdleStop);
        IdleStopDefaultTimeoutTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.IdleStop);

        WireCheckBox(FocusTrackingEnabledCheck, SettingsChangeKind.FocusTracking);
        FocusDistractionThresholdTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.FocusTracking);
        FocusMinimumPauseTextBox.TextChanged += (_, _) => CommitControls(SettingsChangeKind.FocusTracking);
    }

    private void WireSlider(Slider slider, SettingsChangeKind change)
    {
        slider.PropertyChanged += (_, se) =>
        {
            if (se.Property == Slider.ValueProperty)
                CommitControls(change);
        };
        slider.PointerPressed += (_, _) => BeginSliderInteraction();
        slider.PointerReleased += (_, _) => EndSliderInteraction();
        slider.PointerCaptureLost += (_, _) => EndSliderInteraction();
        slider.LostFocus += (_, _) => EndSliderInteraction();
        slider.KeyDown += (_, e) =>
        {
            if (IsSliderAdjustmentKey(e.Key))
                BeginSliderInteraction();
        };
        slider.KeyUp += (_, e) =>
        {
            if (IsSliderAdjustmentKey(e.Key))
                EndSliderInteraction();
        };
    }

    private void WireCheckBox(CheckBox checkBox, SettingsChangeKind change)
    {
        checkBox.IsCheckedChanged += (_, _) => CommitControls(change);
    }

    private void LoadTypographyEditors()
    {
        _settings.Typography.Normalize();
        GlobalTypographyEditor.Children.Clear();
        SectionTypographyEditors.Children.Clear();
        AddTypographyEditor(GlobalTypographyEditor, _settings.Typography.Global, "Global text", false);
        foreach (var (key, label) in TypographySettings.Scopes)
            AddTypographyEditor(SectionTypographyEditors, _settings.Typography.Sections[key], label, true);
    }

    private void AddTypographyEditor(Panel host, TypographyStyle style, string label, bool isOverride)
    {
        var editor = new TypographyEditor(style, label, isOverride);
        editor.Changed += () => CommitControls(SettingsChangeKind.Typography);
        editor.InteractionStarted += BeginSliderInteraction;
        editor.InteractionCompleted += EndSliderInteraction;
        host.Children.Add(editor);
    }

    private (CheckBox Control, NavigatorOpaqueParts Part)[] TransparencyPartControls =>
    [
        (ClockFrameOpaqueCheck, NavigatorOpaqueParts.ClockFrame),
        (ClockMapOpaqueCheck, NavigatorOpaqueParts.ClockMap),
        (MetalBorderOpaqueCheck, NavigatorOpaqueParts.MetalBorder),
        (MetalFillOpaqueCheck, NavigatorOpaqueParts.MetalFill),
        (TimerTextOpaqueCheck, NavigatorOpaqueParts.TimerText),
        (ProjectNameOpaqueCheck, NavigatorOpaqueParts.ProjectName),
        (ControlBoardOpaqueCheck, NavigatorOpaqueParts.ControlBoard),
        (ControlMapOpaqueCheck, NavigatorOpaqueParts.ControlMap),
        (ControlDialsOpaqueCheck, NavigatorOpaqueParts.ControlDials)
    ];

    private static bool IsSliderAdjustmentKey(Key key)
        => key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.PageUp or Key.PageDown or Key.Home or Key.End;

    private void BeginSliderInteraction()
    {
        if (_sliderInteractionActive || _closed)
            return;

        _sliderInteractionActive = true;
        SettingsInteractionStarted?.Invoke();
    }

    private void EndSliderInteraction()
    {
        if (!_sliderInteractionActive)
            return;

        _sliderInteractionActive = false;
        SettingsInteractionCompleted?.Invoke();
    }

    private void CommitControls(SettingsChangeKind change)
    {
        if (_loading || _committing || _closed)
            return;

        _committing = true;
        try
        {
            if ((change & SettingsChangeKind.ApplicationScale) != 0)
                _settings.UiScalePercent = AppUiScale.Normalize(UiScaleSlider.Value);
            if ((change & SettingsChangeKind.Theme) != 0)
                _settings.ThemeMode = AppThemeCatalog.Normalize(ThemeCombo.SelectedItem?.ToString());

            if ((change & SettingsChangeKind.OverlayTheme) != 0)
                _settings.OverlayTheme = OverlayThemeCatalog.Normalize(OverlayThemeCombo.SelectedItem?.ToString());

            if ((change & SettingsChangeKind.OverlayScreen) != 0)
                _settings.ScreenIndex = Math.Max(0, ScreenCombo.SelectedIndex);

            if ((change & SettingsChangeKind.OverlayPosition) != 0)
                _settings.Position = PositionCombo.SelectedItem?.ToString() ?? "Top Center";

            if ((change & (SettingsChangeKind.OverlayAppearance | SettingsChangeKind.OverlayGeometry)) != 0)
            {
                _settings.TextColor = TextColorCombo.SelectedItem?.ToString() ?? "White";
                _settings.BorderColor = BorderColorCombo.SelectedItem?.ToString() ?? "Black";
                _settings.FontFamily = FontCombo.SelectedItem?.ToString() ?? "Consolas";
                _settings.TimeFormat = Math.Max(0, FormatCombo.SelectedIndex);
                _settings.TextSize = TextSizeSlider.Value;
                _settings.BorderWidth = BorderWidthSlider.Value;
                _settings.BackgroundOpacity = BackgroundOpacitySlider.Value;
                _settings.InactiveSeparatedOverlayOpacity = InactiveSeparatedOpacitySlider.Value;
                _settings.OpaqueOverlayParts = TransparencyPartControls
                    .Where(item => item.Control.IsChecked == true)
                    .Aggregate((NavigatorOpaqueParts)0, (parts, item) => parts | item.Part);
            }

            if ((change & SettingsChangeKind.BackgroundSelection) != 0
                && BackgroundCombo.SelectedItem is AppBackgroundChoice { IsAvailable: true } background)
            {
                _settings.PanelBackgroundId = background.Id;
            }

            if ((change & SettingsChangeKind.BackgroundStrength) != 0)
            {
                _settings.PanelBackgroundStrength = Math.Clamp(
                    BackgroundStrengthSlider.Value,
                    AppBackgroundCatalog.MinimumPatternStrength,
                    AppBackgroundCatalog.MaximumPatternStrength);
            }

            if ((change & SettingsChangeKind.OverlayInteraction) != 0)
            {
                _settings.ClickThrough = ClickThroughCheck.IsChecked == true;
                _settings.HideOverlayFromCapture = HideOverlayCaptureCheck.IsChecked == true;
            }

            if ((change & (SettingsChangeKind.LightRingVisibility | SettingsChangeKind.LightRingAppearance)) != 0)
            {
                _settings.LightRingEnabled = LightRingEnabledCheck.IsChecked == true;
                _settings.LightRingBrightness = LightRingBrightnessSlider.Value;
                _settings.LightRingWidth = LightRingWidthSlider.Value;
                _settings.LightRingHideFromCapture = LightRingCaptureCheck.IsChecked == true;
            }

            if ((change & (SettingsChangeKind.Behavior | SettingsChangeKind.IdleStop)) != 0)
            {
                _settings.AutoStart = AutoStartCheck.IsChecked == true;
                _settings.ShowRecIndicator = RecCheck.IsChecked == true;
                _settings.BlinkColon = BlinkCheck.IsChecked == true;
                _settings.UseSmartCountdownInput = SmartInputCheck.IsChecked == true;
                _settings.CommandChainingTimeoutSeconds = Math.Round(CommandChainingTimeoutSlider.Value, 1);
                _settings.CloseAction = CloseActionCombo.SelectedIndex switch
                {
                    1 => CloseActionChoice.Minimize,
                    2 => CloseActionChoice.Exit,
                    _ => CloseActionChoice.Ask
                };
                _settings.IdleStopUnnamedTimers = IdleStopUnnamedCheck.IsChecked == true;
                _settings.IdleStopSubtractDuration = IdleStopSubtractCheck.IsChecked == true;
                if (int.TryParse(IdleStopDefaultTimeoutTextBox.Text?.Trim(), out int defaultIdleMin) && defaultIdleMin > 0)
                {
                    _settings.DefaultIdleStopTimeoutMinutes = defaultIdleMin;
                }
            }

            if ((change & SettingsChangeKind.FocusTracking) != 0)
            {
                _settings.FocusTrackingEnabled = FocusTrackingEnabledCheck.IsChecked == true;
                if (int.TryParse(FocusDistractionThresholdTextBox.Text?.Trim(), out int thresh) && thresh > 0)
                {
                    _settings.FocusDistractionThresholdMinutes = thresh;
                }
                if (int.TryParse(FocusMinimumPauseTextBox.Text?.Trim(), out int minPause) && minPause > 0)
                {
                    _settings.FocusMinimumPauseSeconds = minPause;
                }
            }

            if ((change & SettingsChangeKind.Startup) != 0)
                _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;

            if ((change & SettingsChangeKind.ObsidianExport) != 0)
            {
                _settings.ObsidianAutoSyncEnabled = ObsidianAutoSyncCheck.IsChecked == true;
                _settings.ObsidianLogUnnamedTimers = ObsidianLogUnnamedCheck.IsChecked == true;
                _settings.ObsidianVaultFolder = ObsidianFolderTextBox.Text?.Trim() ?? "";
                _settings.ObsidianExportFileName = ObsidianFileNameTextBox.Text?.Trim() ?? "";
            }

            if ((change & SettingsChangeKind.Telegram) != 0)
            {
                _settings.TelegramEnabled = TelegramEnabledCheck.IsChecked == true;
                _settings.TelegramBotToken = TelegramBotTokenTextBox.Text?.Trim() ?? "";
                _settings.TelegramChatId = TelegramChatIdTextBox.Text?.Trim() ?? "";
                _settings.TelegramNotesTopicId = TelegramNotesTopicTextBox.Text?.Trim() ?? "";
                _settings.TelegramTodosTopicId = TelegramTodosTopicTextBox.Text?.Trim() ?? "";
                _settings.TelegramRemindersTopicId = TelegramRemindersTopicTextBox.Text?.Trim() ?? "";
            }

            if ((change & SettingsChangeKind.ActivityWatch) != 0)
            {
                _settings.ActivityWatchEnabled = ActivityWatchEnabledCheck.IsChecked == true;
                _settings.ActivityWatchSyncOnStopwatchSync = ActivityWatchSyncOnStopwatchCheck.IsChecked == true;
                _settings.ActivityWatchPeriodicSyncEnabled = ActivityWatchPeriodicSyncCheck.IsChecked == true;
                if (int.TryParse(ActivityWatchPeriodicSyncIntervalTextBox.Text?.Trim(), out int syncInterval) && syncInterval > 0)
                    _settings.ActivityWatchPeriodicSyncIntervalMinutes = syncInterval;
                _settings.ActivityWatchIncludeTitles = ActivityWatchIncludeTitlesCheck.IsChecked == true;
                _settings.ActivityWatchIncludeWeb = ActivityWatchIncludeWebCheck.IsChecked == true;
                _settings.ActivityWatchServerUrl = ActivityWatchServerUrlTextBox.Text?.Trim() ?? "";
                _settings.ActivityWatchExportFileName = ActivityWatchFileNameTextBox.Text?.Trim() ?? "";
                if (int.TryParse(ActivityWatchMinDurationTextBox.Text?.Trim(), out int minSec) && minSec > 0)
                    _settings.ActivityWatchMinDurationSeconds = minSec;
            }

            if ((change & SettingsChangeKind.PeriodicReview) != 0)
            {
                _settings.PeriodicReviewEnabled = PeriodicReviewEnabledCheck.IsChecked == true;
                if (int.TryParse(PeriodicReviewIntervalTextBox.Text?.Trim(), out int reviewInterval) && reviewInterval > 0)
                    _settings.PeriodicReviewIntervalMinutes = reviewInterval;
                if (int.TryParse(PeriodicReviewSnoozeTextBox.Text?.Trim(), out int snooze) && snooze > 0)
                    _settings.PeriodicReviewSnoozeMinutes = snooze;
                if (int.TryParse(PeriodicReviewAutoDismissTextBox.Text?.Trim(), out int dismiss) && dismiss > 0)
                    _settings.PeriodicReviewAutoDismissSeconds = dismiss;
                if (int.TryParse(PeriodicReviewMinDurationTextBox.Text?.Trim(), out int minDur) && minDur >= 0)
                    _settings.PeriodicReviewMinDurationSeconds = minDur;
                if (double.TryParse(PeriodicReviewMinPercentTextBox.Text?.Trim(), out double minPct) && minPct >= 0)
                    _settings.PeriodicReviewMinActivityPercent = minPct;
            }

            if ((change & SettingsChangeKind.InternetMonitor) != 0)
            {
                _settings.InternetMonitorEnabled = InternetMonitorEnabledCheck.IsChecked == true;
                _settings.InternetMonitorOnlyDuringTimers = InternetMonitorOnlyDuringTimersCheck.IsChecked == true;
                if (int.TryParse(InternetCheckIntervalTextBox.Text?.Trim(), out int interval) && interval > 0)
                    _settings.InternetMonitorIntervalMinutes = interval;
                _settings.InternetLogFileName = InternetFileNameTextBox.Text?.Trim() ?? "";
                if (InternetRetentionCombo.SelectedItem is ComboBoxItem retItem && int.TryParse(retItem.Tag?.ToString(), out int rDays))
                    _settings.InternetLogRetentionDays = rDays;
            }

            UpdateValueLabels();
            UpdateDependentControlStates();
            if ((change & (SettingsChangeKind.Theme
                           | SettingsChangeKind.OverlayTheme
                           | SettingsChangeKind.OverlayAppearance
                           | SettingsChangeKind.OverlayGeometry
                           | SettingsChangeKind.BackgroundSelection
                           | SettingsChangeKind.BackgroundStrength)) != 0)
            {
                SchedulePreviewFromAppliedSettings();
            }

            CrashLogger.RecordUiAction($"Settings change: {change}", CurrentCategory);
            SettingsChanged?.Invoke(change);
        }
        finally
        {
            _committing = false;
        }
    }

    private void UpdateValueLabels()
    {
        TextSizeValueText.Text = $"{Math.Round(TextSizeSlider.Value):0} px";
        BorderWidthValueText.Text = $"{Math.Round(BorderWidthSlider.Value):0} px";
        BackgroundOpacityValueText.Text = $"{Math.Round(BackgroundOpacitySlider.Value):0}%";
        InactiveSeparatedOpacityValueText.Text = $"{Math.Round(InactiveSeparatedOpacitySlider.Value):0}%";
        BackgroundStrengthValueText.Text = $"{Math.Round(BackgroundStrengthSlider.Value):0}%";
        LightRingBrightnessValueText.Text = $"{Math.Round(LightRingBrightnessSlider.Value):0}%";
        LightRingWidthValueText.Text = $"{Math.Round(LightRingWidthSlider.Value):0} px";
        CommandChainingTimeoutValueText.Text = $"{CommandChainingTimeoutSlider.Value:0.0} s";
    }

    private void UpdateDependentControlStates()
    {
        TransparencyDetailsExpander.IsVisible =
            OverlayThemeCatalog.Resolve(_settings.OverlayTheme, _settings.ThemeMode) == OverlayThemeCatalog.Pirate;

        bool hasPattern = BackgroundCombo.SelectedItem is AppBackgroundChoice
            { IsThemeDefault: false, IsAvailable: true };
        BackgroundStrengthSlider.IsEnabled = hasPattern;
        RemoveBackgroundButton.IsEnabled = BackgroundCombo.SelectedItem is AppBackgroundChoice
            { IsCustom: true };

        bool ringEnabled = LightRingEnabledCheck.IsChecked == true;
        LightRingBrightnessSlider.IsEnabled = ringEnabled;
        LightRingWidthSlider.IsEnabled = ringEnabled;
        LightRingCaptureCheck.IsEnabled = ringEnabled;
    }

    private void UpdatePreviewSafely()
    {
        try
        {
            double opacity = OverlayPresentationPolicy.ClampBackgroundOpacity(_settings.BackgroundOpacity / 100.0);
            Color chromeColor = Color.FromArgb((byte)(opacity * 255), 20, 24, 28);
            PreviewSurface.Background = new SolidColorBrush(chromeColor);

            bool useThemeTextColor = _settings.TextColor == "Theme default";
            Color textColor = useThemeTextColor ? Colors.White : SelectedTextColor(_settings.TextColor);
            Color projectColor = useThemeTextColor ? textColor : SelectedTextColor(_settings.TextColor);

            PreviewTimeText.Foreground = new SolidColorBrush(textColor);
            PreviewProjectText.Foreground = new SolidColorBrush(projectColor);

            string fontName = string.IsNullOrWhiteSpace(_settings.FontFamily) ? "Cascadia Mono, Consolas, monospace" : _settings.FontFamily;
            PreviewTimeText.FontFamily = new FontFamily(fontName);
            PreviewTimeText.FontSize = Math.Clamp(_settings.TextSize, 16, 120);

            Color borderColor = SelectedBorderColor(_settings.BorderColor);
            PreviewSurface.BorderBrush = new SolidColorBrush(borderColor);
            PreviewSurface.BorderThickness = new Thickness(_settings.BorderWidth);

            _previewFailureReported = false;
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or NotSupportedException)
        {
            if (!_previewFailureReported)
            {
                _previewFailureReported = true;
                CrashLogger.LogRecoverable(exception, "SettingsPreview");
            }
        }
    }

    private static Color SelectedTextColor(string value) => value switch
    {
        "Yellow" => Colors.Yellow,
        "Cyan" => Colors.Cyan,
        "Lime" => Colors.Lime,
        "Orange" => Colors.Orange,
        "Red" => Colors.Red,
        "Magenta" => Colors.Magenta,
        "Charcoal" => Color.FromRgb(44, 41, 36),
        _ => Colors.White
    };

    private static Color SelectedBorderColor(string value) => value switch
    {
        "White" => Colors.White,
        "Dark Gray" => Colors.DarkGray,
        "Red" => Colors.Red,
        "Blue" => Colors.Blue,
        _ => Colors.Black
    };

    private void RefreshBackgroundChoices(string? preferredId)
    {
        var choices = AppBackgroundCatalog.GetAvailableChoices(_settings);
        BackgroundCombo.ItemsSource = choices;
        BackgroundCombo.SelectedItem = choices.FirstOrDefault(choice =>
            choice.Id.Equals(preferredId, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
    }

    private async void AddBackgroundButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = "Add application background",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"]
                }
            ]
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return;

        string? filePath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(filePath)) return;

        string previousSelection = _settings.PanelBackgroundId;
        if (!AppBackgroundCatalog.TryImport(
                filePath,
                _settings.CustomBackgrounds,
                out CustomAppBackground? imported,
                out string? error)
            || imported == null)
        {
            await ConfirmationDialogWindow.ShowAsync(
                this,
                "Background",
                "Background Import Failed",
                error ?? "The image could not be added.",
                "OK",
                destructive: false);
            return;
        }

        _settings.CustomBackgrounds.Add(imported);
        _settings.PanelBackgroundId = AppBackgroundCatalog.CustomSelectionId(imported.Id);
        if (!SettingsStore.Save(_settings))
        {
            _settings.CustomBackgrounds.RemoveAll(item =>
                item.Id.Equals(imported.Id, StringComparison.OrdinalIgnoreCase));
            _settings.PanelBackgroundId = previousSelection;
            AppBackgroundCatalog.DeleteManagedCopy(imported);
            await ConfirmationDialogWindow.ShowAsync(
                this,
                "Background",
                "Save Failed",
                "The background could not be saved. No changes were kept.",
                "OK",
                destructive: false);
            return;
        }

        _loading = true;
        try
        {
            RefreshBackgroundChoices(_settings.PanelBackgroundId);
            UpdateDependentControlStates();
        }
        finally
        {
            _loading = false;
        }
        CommitControls(SettingsChangeKind.BackgroundSelection);
    }

    private async void RemoveBackgroundButton_Click(object? sender, RoutedEventArgs e)
    {
        if (BackgroundCombo.SelectedItem is not AppBackgroundChoice { IsCustom: true } choice)
            return;

        CustomAppBackground? custom = _settings.CustomBackgrounds.FirstOrDefault(item =>
            AppBackgroundCatalog.CustomSelectionId(item.Id).Equals(choice.Id, StringComparison.OrdinalIgnoreCase));
        if (custom == null)
            return;

        string previousSelection = _settings.PanelBackgroundId;
        _settings.CustomBackgrounds.RemoveAll(item =>
            item.Id.Equals(custom.Id, StringComparison.OrdinalIgnoreCase));
        _settings.PanelBackgroundId = AppBackgroundCatalog.ThemeDefault;
        if (!SettingsStore.Save(_settings))
        {
            _settings.CustomBackgrounds.Add(custom);
            _settings.PanelBackgroundId = previousSelection;
            await ConfirmationDialogWindow.ShowAsync(
                this,
                "Background",
                "Removal Failed",
                "The background could not be removed because the updated library could not be saved.",
                "OK",
                destructive: false);
            return;
        }

        if (!AppBackgroundCatalog.DeleteManagedCopy(custom))
        {
            await ConfirmationDialogWindow.ShowAsync(
                this,
                "Background",
                "Notice",
                "The background was removed from the library, but its managed image could not be deleted.",
                "OK",
                destructive: false);
        }

        _loading = true;
        try
        {
            RefreshBackgroundChoices(_settings.PanelBackgroundId);
            UpdateDependentControlStates();
        }
        finally
        {
            _loading = false;
        }
        CommitControls(SettingsChangeKind.BackgroundSelection);
    }

    private void NavigationList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OverlayPanel == null)
            return;

        string tag = (NavigationList.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "Overlay";
        CurrentCategory = tag;
        OverlayPanel.IsVisible = tag == "Overlay";
        AppearancePanel.IsVisible = tag == "Appearance";
        TypographyPanel.IsVisible = tag == "Typography";
        LightRingPanel.IsVisible = tag == "LightRing";
        BehaviorPanel.IsVisible = tag == "Behavior";
        ApplicationPanel.IsVisible = tag == "Application";
        ObsidianPanel.IsVisible = tag == "Obsidian";
        TelegramPanel.IsVisible = tag == "Telegram";
        ActivityWatchPanel.IsVisible = tag == "ActivityWatch";
        InternetPanel.IsVisible = tag == "Internet";
        CrashLogger.RecordUiAction("Settings category changed", CurrentCategory);
        if (SettingsScrollViewer != null)
            SettingsScrollViewer.Offset = new Vector(0, 0);
    }

    private async void BrowseObsidianFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Obsidian Vault or Target Folder",
            AllowMultiple = false
        };

        if (!string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder) && Directory.Exists(_settings.ObsidianVaultFolder))
        {
            var startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(_settings.ObsidianVaultFolder);
            if (startFolder != null)
                options.SuggestedStartLocation = startFolder;
        }

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0 && result[0].TryGetLocalPath() is string localPath)
        {
            ObsidianFolderTextBox.Text = localPath;
        }
    }

    private void ExportNowButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_historyProvider == null)
        {
            ExportStatusText.Text = "History records are not available.";
            ExportStatusText.Foreground = Brushes.OrangeRed;
            return;
        }

        var history = _historyProvider();
        var result = ObsidianLogSync.SyncHistory(
            history,
            _settings,
            ObsidianFolderTextBox.Text ?? "",
            ObsidianFileNameTextBox.Text ?? "");

        if (result.Success)
        {
            ExportStatusText.Text = $"{result.Message} ({DateTime.Now:HH:mm:ss})";
            ExportStatusText.Foreground = Brushes.DeepSkyBlue;
        }
        else
        {
            ExportStatusText.Text = result.Message ?? "Export failed.";
            ExportStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    public async void TestTelegramButton_Click(object? sender, RoutedEventArgs e)
    {
        TestTelegramButton.IsEnabled = false;
        TelegramStatusText.Text = "Testing Telegram connection...";
        TelegramStatusText.Foreground = Brushes.DeepSkyBlue;

        try
        {
            var testSettings = new AppSettings
            {
                TelegramEnabled = TelegramEnabledCheck.IsChecked == true,
                TelegramBotToken = TelegramBotTokenTextBox.Text?.Trim() ?? "",
                TelegramChatId = TelegramChatIdTextBox.Text?.Trim() ?? "",
                TelegramNotesTopicId = TelegramNotesTopicTextBox.Text?.Trim() ?? "",
                TelegramTodosTopicId = TelegramTodosTopicTextBox.Text?.Trim() ?? "",
                TelegramRemindersTopicId = TelegramRemindersTopicTextBox.Text?.Trim() ?? ""
            };

            var result = await TelegramNotesSync.TestConnectionAsync(testSettings);
            if (result.Success)
            {
                TelegramStatusText.Text = "✓ " + result.Message;
                TelegramStatusText.Foreground = Brushes.DeepSkyBlue;
            }
            else
            {
                TelegramStatusText.Text = "✗ " + result.Message;
                TelegramStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            TelegramStatusText.Text = "✗ Connection error: " + ex.Message;
            TelegramStatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            TestTelegramButton.IsEnabled = true;
        }
    }

    private void UpdateTelegramOutboxUi()
    {
        if (_closed) return;
        int count = TelegramOutboxStore.PendingCount;
        if (count == 0)
        {
            TelegramOutboxStatusText.Text = "Outbox: 0 notes pending";
            TelegramOutboxDetailText.Text = "All notes are up to date.";
            SendPendingNotesButton.IsEnabled = false;
        }
        else
        {
            TelegramOutboxStatusText.Text = $"Outbox: {count} note(s) pending delivery";
            TelegramOutboxDetailText.Text = "Notes queued while offline. Will send automatically when online.";
            SendPendingNotesButton.IsEnabled = true;
        }
    }

    private void OnOutboxChanged()
    {
        if (_closed) return;
        Dispatcher.UIThread.Post(UpdateTelegramOutboxUi);
    }

    private async void SendPendingNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        SendPendingNotesButton.IsEnabled = false;
        TelegramOutboxDetailText.Text = "Sending pending notes to Telegram...";
        try
        {
            var (sent, remaining, lastError) = await TelegramNotesSync.FlushOutboxAsync(_settings);
            if (remaining == 0)
            {
                TelegramOutboxDetailText.Text = $"Successfully sent {sent} note(s)!";
                TelegramStatusText.Text = $"✓ Flushed outbox: {sent} note(s) sent.";
                TelegramStatusText.Foreground = Brushes.ForestGreen;
            }
            else
            {
                TelegramOutboxDetailText.Text = $"Sent {sent} note(s). {remaining} remaining. (Error: {lastError})";
                TelegramStatusText.Text = $"✗ Outbox flush paused: {lastError}";
                TelegramStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            TelegramOutboxDetailText.Text = "Error flushing outbox: " + ex.Message;
        }
        finally
        {
            UpdateTelegramOutboxUi();
        }
    }

    private async void ScanVaultNotesButton_Click(object? sender, RoutedEventArgs e)
    {
        ScanVaultNotesButton.IsEnabled = false;
        try
        {
            var unsentNotes = TelegramNotesSync.GetUnsentVaultNotes(_settings, lookbackDays: 3);
            if (unsentNotes.Count == 0)
            {
                await ConfirmationDialogWindow.ShowAsync(
                    this,
                    "Vault Scan Complete",
                    "Scan Result",
                    "No unsent notes found in your Obsidian vault from the last 3 days.",
                    "OK",
                    destructive: false);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {unsentNotes.Count} note(s) from the last 3 days that are not in your Telegram sent history:\n");
            foreach (var note in unsentNotes.Take(5))
            {
                string preview = note.Text.Length > 60 ? note.Text[..57] + "..." : note.Text;
                sb.AppendLine($"• [{note.Type}] {note.Timestamp:yyyy-MM-dd HH:mm} - {preview}");
            }
            if (unsentNotes.Count > 5)
            {
                sb.AppendLine($"... and {unsentNotes.Count - 5} more.");
            }
            sb.AppendLine("\nWould you like to add these notes to the Telegram outbox and send them now?");

            bool confirmed = await ConfirmationDialogWindow.ShowAsync(
                this,
                "Unsent Notes Found",
                "Sync Notes to Telegram",
                sb.ToString(),
                "Send Now",
                destructive: false);

            if (confirmed)
            {
                int queued = TelegramNotesSync.QueueVaultNotes(unsentNotes);
                UpdateTelegramOutboxUi();
                var (sent, remaining, lastError) = await TelegramNotesSync.FlushOutboxAsync(_settings);
                await ConfirmationDialogWindow.ShowAsync(
                    this,
                    "Notes Queued & Sent",
                    "Delivery Status",
                    $"Queued {queued} note(s).\nSuccessfully sent: {sent}\nRemaining in outbox: {remaining}" +
                    (lastError != null ? $"\n(Last note status: {lastError})" : ""),
                    "OK",
                    destructive: remaining > 0);
            }
        }
        catch (Exception ex)
        {
            await ConfirmationDialogWindow.ShowAsync(
                this,
                "Error",
                "Scan Failed",
                "Failed to scan vault: " + ex.Message,
                "OK",
                destructive: true);
        }
        finally
        {
            ScanVaultNotesButton.IsEnabled = true;
            UpdateTelegramOutboxUi();
        }
    }

    private void LoadIdleStopControls()
    {
        IdleStopUnnamedCheck.IsChecked = _settings.IdleStopUnnamedTimers;
        IdleStopDefaultTimeoutTextBox.Text = _settings.DefaultIdleStopTimeoutMinutes.ToString();
        IdleStopSubtractCheck.IsChecked = _settings.IdleStopSubtractDuration;

        PopulateIdleProjects();
    }

    private void PopulateIdleProjects()
    {
        var projectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_historyProvider != null)
        {
            var history = _historyProvider();
            foreach (var p in history.Projects)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                    projectNames.Add(p.Name.Trim());
            }
        }

        if (_settings.ProjectIdleRules != null)
        {
            foreach (var key in _settings.ProjectIdleRules.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    projectNames.Add(key.Trim());
            }
        }

        var sorted = projectNames.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

        string? prevSelected = IdleProjectComboBox.SelectedItem as string;
        IdleProjectComboBox.Items.Clear();
        foreach (var name in sorted)
        {
            IdleProjectComboBox.Items.Add(name);
        }

        if (sorted.Count > 0)
        {
            if (prevSelected != null && sorted.Contains(prevSelected, StringComparer.OrdinalIgnoreCase))
            {
                IdleProjectComboBox.SelectedItem = prevSelected;
            }
            else
            {
                IdleProjectComboBox.SelectedIndex = 0;
            }
        }
        else
        {
            UpdateSelectedProjectIdleUi(null);
        }

        RefreshIdleProjectsOverview();
    }

    private void IdleProjectComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IdleProjectComboBox.SelectedItem is string selectedProject && !string.IsNullOrWhiteSpace(selectedProject))
        {
            UpdateSelectedProjectIdleUi(selectedProject);
        }
        else
        {
            UpdateSelectedProjectIdleUi(null);
        }
    }

    private void UpdateSelectedProjectIdleUi(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            IdleProjectActiveCheck.IsEnabled = false;
            IdleProjectActiveCheck.IsChecked = false;
            IdleProjectTimeoutTextBox.IsEnabled = false;
            IdleProjectTimeoutTextBox.Text = _settings.DefaultIdleStopTimeoutMinutes.ToString();
            IdleProjectStatusSummaryText.Text = "No projects found. Create or name a timer to add projects.";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
                IdleProjectStatusSummaryText.Foreground = brush;
            return;
        }

        IdleProjectActiveCheck.IsEnabled = true;
        IdleProjectTimeoutTextBox.IsEnabled = true;

        bool isActive = _settings.IsIdleStopActiveForProject(projectName, out int timeoutMinutes, out _);
        IdleProjectActiveCheck.IsChecked = isActive;
        IdleProjectTimeoutTextBox.Text = timeoutMinutes.ToString();

        if (isActive)
        {
            IdleProjectStatusSummaryText.Text = $"Status: Active (stops timer when idle for {timeoutMinutes}m)";
            IdleProjectStatusSummaryText.Foreground = Brushes.LimeGreen;
        }
        else
        {
            IdleProjectStatusSummaryText.Text = "Status: Deactive (timer will not stop on idle)";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
                IdleProjectStatusSummaryText.Foreground = brush;
        }
    }

    private void IdleProjectActiveCheck_Click(object? sender, RoutedEventArgs e)
    {
        if (IdleProjectComboBox.SelectedItem is string selectedProject && !string.IsNullOrWhiteSpace(selectedProject))
        {
            bool enabled = IdleProjectActiveCheck.IsChecked == true;
            int timeout = int.TryParse(IdleProjectTimeoutTextBox.Text?.Trim(), out int parsed) && parsed > 0
                ? parsed
                : _settings.DefaultIdleStopTimeoutMinutes;

            _settings.SetProjectIdleRule(selectedProject, enabled, timeout);
            UpdateSelectedProjectIdleUi(selectedProject);
            RefreshIdleProjectsOverview();
            CommitControls(SettingsChangeKind.IdleStop);
        }
    }

    private void IdleProjectTimeoutTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (IdleProjectComboBox.SelectedItem is string selectedProject && !string.IsNullOrWhiteSpace(selectedProject))
        {
            if (int.TryParse(IdleProjectTimeoutTextBox.Text?.Trim(), out int timeout) && timeout > 0)
            {
                bool enabled = IdleProjectActiveCheck.IsChecked == true;
                _settings.SetProjectIdleRule(selectedProject, enabled, timeout);
                UpdateSelectedProjectIdleUi(selectedProject);
                RefreshIdleProjectsOverview();
                CommitControls(SettingsChangeKind.IdleStop);
            }
        }
    }

    private void IdleProjectQuickToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string projectName && !string.IsNullOrWhiteSpace(projectName))
        {
            bool isCurrentlyActive = _settings.IsIdleStopActiveForProject(projectName, out int timeoutMinutes, out _);
            _settings.SetProjectIdleRule(projectName, !isCurrentlyActive, timeoutMinutes);

            if (string.Equals(IdleProjectComboBox.SelectedItem as string, projectName, StringComparison.OrdinalIgnoreCase))
            {
                UpdateSelectedProjectIdleUi(projectName);
            }

            RefreshIdleProjectsOverview();
            CommitControls(SettingsChangeKind.IdleStop);
        }
    }

    private void RefreshIdleProjectsOverview()
    {
        var items = new List<ProjectIdleRuleViewModel>();
        var projectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_historyProvider != null)
        {
            foreach (var p in _historyProvider().Projects)
                if (!string.IsNullOrWhiteSpace(p.Name)) projectNames.Add(p.Name.Trim());
        }

        if (_settings.ProjectIdleRules != null)
        {
            foreach (var k in _settings.ProjectIdleRules.Keys)
                if (!string.IsNullOrWhiteSpace(k)) projectNames.Add(k.Trim());
        }

        IBrush secondaryBrush = Brushes.Gray;
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
            secondaryBrush = brush;

        foreach (var name in projectNames.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
        {
            bool isActive = _settings.IsIdleStopActiveForProject(name, out int timeout, out _);
            items.Add(new ProjectIdleRuleViewModel
            {
                ProjectName = name,
                StatusText = isActive ? $"Active ({timeout}m)" : "Deactive",
                StatusBrush = isActive ? Brushes.LimeGreen : secondaryBrush,
                ActionButtonText = isActive ? "Deactivate" : "Activate"
            });
        }

        IdleProjectsItemsControl.ItemsSource = items;
    }

    private void LoadCategoryControls()
    {
        PopulateCategories();
    }

    private void PopulateCategories()
    {
        var items = new List<ProjectCategoryItemViewModel>();
        var cats = _settings.GetNormalizedProjectCategories();
        var defaultSet = new HashSet<string>(AppSettings.DefaultProjectCategories, StringComparer.OrdinalIgnoreCase);

        foreach (var c in cats)
        {
            bool isDefault = defaultSet.Contains(c);
            items.Add(new ProjectCategoryItemViewModel
            {
                Name = c,
                Description = isDefault ? "Default preset" : "Custom category",
                CanDelete = !isDefault
            });
        }

        CategoriesItemsControl.ItemsSource = items;
    }

    private void AddCategorySettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        SubmitNewCategory();
    }

    private void NewCategoryTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SubmitNewCategory();
        }
    }

    private void SubmitNewCategory()
    {
        string name = NewCategoryTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryValidationStatusText.Text = "Please enter a category name.";
            CategoryValidationStatusText.IsVisible = true;
            NewCategoryTextBox.Focus();
            return;
        }

        if (name.Length > 40)
        {
            name = name.Substring(0, 40).Trim();
        }

        if (!_settings.AddProjectCategory(name))
        {
            CategoryValidationStatusText.Text = $"Category '{name}' already exists.";
            CategoryValidationStatusText.IsVisible = true;
            NewCategoryTextBox.Focus();
            return;
        }

        CategoryValidationStatusText.IsVisible = false;
        NewCategoryTextBox.Text = "";
        SettingsStore.Save(_settings);
        CommitControls(SettingsChangeKind.Behavior);
        PopulateCategories();
    }

    private void DeleteCategory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string catName })
        {
            _settings.ProjectCategories?.RemoveAll(c => string.Equals(c, catName, StringComparison.OrdinalIgnoreCase));
            _settings.NormalizeForRuntime();
            SettingsStore.Save(_settings);
            CommitControls(SettingsChangeKind.Behavior);
            PopulateCategories();
        }
    }

    private async void StartActivityWatchAppButton_Click(object? sender, RoutedEventArgs e)
    {
        StartActivityWatchAppButton.IsEnabled = false;
        ActivityWatchStatusText.Text = "Attempting to start ActivityWatch...";
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
            ActivityWatchStatusText.Foreground = brush;

        try
        {
            bool launched = ActivityWatchLauncher.TryLaunch();
            if (!launched)
            {
                ActivityWatchStatusText.Text = "✗ Could not locate ActivityWatch executable (aw-qt). Please start ActivityWatch manually.";
                ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
                return;
            }

            ActivityWatchStatusText.Text = "ActivityWatch starting, waiting for server to respond...";
            var client = new ActivityWatchClient(ActivityWatchServerUrlTextBox.Text?.Trim() ?? "");
            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(500);
                var (success, version, _) = await client.TestConnectionAsync();
                if (success)
                {
                    ActivityWatchStatusText.Text = $"✓ ActivityWatch is running and connected (Version: {version})";
                    ActivityWatchStatusText.Foreground = Brushes.ForestGreen;
                    return;
                }
            }

            ActivityWatchStatusText.Text = "ActivityWatch was started, but server took longer than expected to respond. Try 'Test Connection' shortly.";
            ActivityWatchStatusText.Foreground = Brushes.Goldenrod;
        }
        catch (Exception ex)
        {
            ActivityWatchStatusText.Text = "✗ Error: " + ex.Message;
            ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            StartActivityWatchAppButton.IsEnabled = true;
        }
    }

    private async void TestActivityWatchButton_Click(object? sender, RoutedEventArgs e)
    {
        TestActivityWatchButton.IsEnabled = false;
        ActivityWatchStatusText.Text = "Testing connection to ActivityWatch...";
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
            ActivityWatchStatusText.Foreground = brush;

        try
        {
            var client = new ActivityWatchClient(ActivityWatchServerUrlTextBox.Text?.Trim() ?? "");
            var (success, version, error) = await client.TestConnectionAsync();
            if (success)
            {
                ActivityWatchStatusText.Text = $"✓ Connected to ActivityWatch (Version: {version})";
                ActivityWatchStatusText.Foreground = Brushes.ForestGreen;
            }
            else
            {
                ActivityWatchStatusText.Text = "✗ " + (error ?? "Could not connect to ActivityWatch.");
                ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            ActivityWatchStatusText.Text = "✗ Error: " + ex.Message;
            ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            TestActivityWatchButton.IsEnabled = true;
        }
    }

    private async void SyncActivityWatchNowButton_Click(object? sender, RoutedEventArgs e)
    {
        SyncActivityWatchNowButton.IsEnabled = false;
        ActivityWatchStatusText.Text = "Syncing today's activity into Obsidian vault...";
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
            ActivityWatchStatusText.Foreground = brush;

        try
        {
            CommitControls(SettingsChangeKind.ActivityWatch);
            var result = await ActivityWatchSync.SyncAsync(_settings);
            if (result.Success)
            {
                ActivityWatchStatusText.Text = $"✓ Synced {result.AppCount} applications and {result.WebCount} web domains into '{Path.GetFileName(result.TargetFilePath)}'.";
                ActivityWatchStatusText.Foreground = Brushes.ForestGreen;
            }
            else
            {
                ActivityWatchStatusText.Text = "✗ " + (result.Message ?? "Failed to sync ActivityWatch log.");
                ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            ActivityWatchStatusText.Text = "✗ Sync error: " + ex.Message;
            ActivityWatchStatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            SyncActivityWatchNowButton.IsEnabled = true;
        }
    }

    private async void TestInternetButton_Click(object? sender, RoutedEventArgs e)
    {
        TestInternetButton.IsEnabled = false;
        InternetStatusText.Text = "Testing network latency, download speed, and Wi-Fi...";
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
            InternetStatusText.Foreground = brush;

        try
        {
            CommitControls(SettingsChangeKind.InternetMonitor);
            var result = await InternetSpeedProbe.CheckConnectionAsync(
                sampleBytes: _settings.InternetSampleSizeBytes,
                pingHost: "1.1.1.1");

            string speedStr = result.DownloadMbps.HasValue ? $"{result.DownloadMbps.Value:F1} Mbps" : "N/A";
            string pingStr = result.PingMs.HasValue ? $"{result.PingMs.Value} ms" : "N/A";
            string netStr = !string.IsNullOrWhiteSpace(result.NetworkInfo.DisplayText) ? result.NetworkInfo.DisplayText : "Unknown";

            if (result.Status == InternetStatus.Online)
            {
                InternetStatusText.Text = $"✓ {result.StatusBadge} | {netStr} | Ping: {pingStr} | Download: {speedStr} ({result.Notes})";
                InternetStatusText.Foreground = Brushes.ForestGreen;
            }
            else if (result.Status == InternetStatus.Slow)
            {
                InternetStatusText.Text = $"⚠ {result.StatusBadge} | {netStr} | Ping: {pingStr} | Download: {speedStr} ({result.Notes})";
                InternetStatusText.Foreground = Brushes.Goldenrod;
            }
            else
            {
                InternetStatusText.Text = $"✗ {result.StatusBadge} | {netStr} | {result.Notes}";
                InternetStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            InternetStatusText.Text = "✗ Test error: " + ex.Message;
            InternetStatusText.Foreground = Brushes.OrangeRed;
        }
        finally
        {
            TestInternetButton.IsEnabled = true;
        }
    }

    private void InternetRetentionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CommitControls(SettingsChangeKind.InternetMonitor);
    }

    private void SyncInternetLogButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var res = InternetLogSync.SyncLog(_settings);
            if (res.Success)
            {
                int count = InternetHistoryStore.GetAll().Count;
                InternetStatusText.Text = $"✓ Synced {count} check(s) to '{Path.GetFileName(res.TargetFilePath)}'.";
                InternetStatusText.Foreground = Brushes.ForestGreen;
            }
            else
            {
                InternetStatusText.Text = "✗ " + (res.Message ?? "Failed to sync internet log.");
                InternetStatusText.Foreground = Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            InternetStatusText.Text = "✗ Sync error: " + ex.Message;
            InternetStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    private async void ClearInternetLogButton_Click(object? sender, RoutedEventArgs e)
    {
        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            this,
            "Clear Internet Log",
            "Clear History",
            "Are you sure you want to clear your internet connection history?\n\nThis will clear the local internet history store and clear 'Internet Log.md'.",
            "Clear History",
            destructive: true);

        if (!confirmed)
            return;

        try
        {
            InternetHistoryStore.Clear();
            var res = InternetLogSync.SyncLog(_settings);
            InternetStatusText.Text = "✓ Internet log and local history have been cleared.";
            InternetStatusText.Foreground = Brushes.ForestGreen;
        }
        catch (Exception ex)
        {
            InternetStatusText.Text = "✗ Clear error: " + ex.Message;
            InternetStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    private void UpdateObsidianPathPreview()
    {
        string folder = ObsidianFolderTextBox.Text?.Trim() ?? "";
        string fileName = ObsidianFileNameTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = ObsidianLogSync.DefaultFileName;
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            fileName += ".md";

        if (string.IsNullOrWhiteSpace(folder))
        {
            ObsidianPathPreviewText.Text = "No folder selected. Click Browse to select your Obsidian vault.";
        }
        else
        {
            ObsidianPathPreviewText.Text = Path.Combine(folder, fileName);
        }
    }

    private void ShowOverlayButton_Click(object? sender, RoutedEventArgs e)
    {
        CrashLogger.RecordUiAction("Overlay visibility requested", CurrentCategory);
        ShowOverlayRequested?.Invoke();
    }

    private void ReviewNowButton_Click(object? sender, RoutedEventArgs e)
    {
        CrashLogger.RecordUiAction("Periodic review requested manually from settings", CurrentCategory);
        PeriodicReviewRequested?.Invoke();
    }

    private void DoneButton_Click(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _previewTimer.Stop();
        TelegramOutboxStore.OutboxChanged -= OnOutboxChanged;
        EndSliderInteraction();
        base.OnClosed(e);
    }
}

public sealed class ProjectIdleRuleViewModel
{
    public string ProjectName { get; init; } = "";
    public string StatusText { get; init; } = "";
    public IBrush StatusBrush { get; init; } = Brushes.Gray;
    public string ActionButtonText { get; init; } = "";
}

public sealed class ProjectCategoryItemViewModel
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool CanDelete { get; init; }
}

