using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private bool _updatingIdleProject;

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
        : this(settings, historyProvider, null) { }

    public SettingsWindow(AppSettings settings, Func<ProjectHistoryView>? historyProvider, System.Net.Http.HttpClient? telegramClient)
        : this(settings, historyProvider, telegramClient, null, null) { }

    internal SettingsWindow(AppSettings settings, Func<ProjectHistoryView>? historyProvider,
        System.Net.Http.HttpClient? telegramClient, System.Net.Http.HttpClient? activityWatchClient, Func<bool>? activityWatchLauncher)
        : this(settings, historyProvider, telegramClient, activityWatchClient, activityWatchLauncher, null, null) { }

    internal SettingsWindow(AppSettings settings, Func<ProjectHistoryView>? historyProvider,
        System.Net.Http.HttpClient? telegramClient, System.Net.Http.HttpClient? activityWatchClient, Func<bool>? activityWatchLauncher,
        Func<int, System.Threading.CancellationToken, Task<InternetCheckResult>>? internetProbe, Func<bool>? internetTimerRunning)
    {
        _internetProbe = internetProbe ?? ((bytes, token) => InternetSpeedProbe.CheckConnectionAsync(bytes, cancellationToken: token));
        _internetTimerRunning = internetTimerRunning;
        _telegramClient = telegramClient;
        _activityWatchHttpClient = activityWatchClient;
        _activityWatchLauncher = activityWatchLauncher ?? ActivityWatchLauncher.TryLaunch;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _historyProvider = historyProvider;
        InitializeComponent();
        SizeChanged+=(_,_)=>UpdateTodoResponsiveLayout();

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
        RefreshActivityWatchConnection();
        HabiticaPanel.Bind(_settings, () => SettingsChanged?.Invoke(SettingsChangeKind.None));
        UpdatePreviewSafely();
        TelegramOutboxStore.OutboxChanged += OnOutboxChanged;
        Screens.Changed += SettingsScreensChanged;
    }

    internal void ReloadFromSettings()
    {
        if (_closed)
            return;

        LoadControls();
        HabiticaPanel.Reload();
        SchedulePreviewFromAppliedSettings();
    }

    internal void SchedulePreviewFromAppliedSettings()
    {
        if (_closed)
            return;

        if (!_previewTimer.IsEnabled)
            _previewTimer.Start();
    }

    private static void LoadNikaChoice(ComboBox combo, IReadOnlyList<NikaThemeOption> options, string id)
    {
        combo.ItemsSource = options;
        combo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<NikaThemeOption>((option, _) =>
            new TextBlock { Text = option?.Name ?? "" });
        combo.SelectedItem = options.First(option => option.Id == id);
    }

    private void LoadControls()
    {
        _loading = true;
        try
        {
            ThemeCombo.ItemsSource = AppThemeCatalog.All;
            ThemeCombo.SelectedItem = AppThemeCatalog.Normalize(_settings.ThemeMode);
            LoadNikaChoice(NikaBackgroundCombo, NikaThemeCatalog.Backgrounds, NikaThemeCatalog.NormalizeBackground(_settings.NikaBackground));
            LoadNikaChoice(NikaTypographyCombo, NikaThemeCatalog.Typography, NikaThemeCatalog.NormalizeTypography(_settings.NikaTypography));
            ControllerStyleCombo.SelectedIndex = _settings.NikaControllerPixel ? 1 : 0;
            LoadNikaChoice(ControllerDigitsCombo, NikaThemeCatalog.LargeClockDigits, NikaThemeCatalog.NormalizeLargeClockDigits(_settings.ControllerLargeClockDigits));
            ControllerDigitsCombo.IsEnabled = _settings.NikaControllerPixel;
            LoadNikaChoice(NikaArtworkCombo, NikaThemeCatalog.Artworks, NikaThemeCatalog.NormalizeArtwork(_settings.NikaSidebarArtwork));
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
            PeriodicReviewMinPercentTextBox.Text = _settings.PeriodicReviewMinActivityPercent.ToString();
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
            RefreshTelegramConfiguration();
            LoadIdleStopControls();
            FocusTrackingEnabledCheck.IsChecked = _settings.FocusTrackingEnabled;
            FocusDistractionThresholdTextBox.Text = _settings.FocusDistractionThresholdMinutes.ToString();
            FocusMinimumPauseTextBox.Text = _settings.FocusMinimumPauseSeconds.ToString();
            UpdateBehaviorControlStates();
            LoadCategoryControls();
            LoadTodoAppearance();
            LoadStopwatchAppearance();
            RefreshApplicationChoices();
            RefreshActivityWatchExport();
            RefreshActivityWatchReview();
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
        NikaBackgroundCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.Theme);
        NikaTypographyCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.Theme);
        ControllerStyleCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.ControllerStyle);
        ControllerDigitsCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.ControllerStyle);
        NikaArtworkCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.Theme);
        OverlayThemeCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayTheme);
        ScreenCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayScreen);
        PositionCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayPosition);
        TextColorCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayAppearance);
        BorderColorCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayAppearance);
        FontCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayGeometry);
        FormatCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.OverlayGeometry);
        BackgroundCombo.SelectionChanged += (_, _) => CommitControls(SettingsChangeKind.BackgroundSelection);
        CloseActionCombo.SelectionChanged += (_, _) =>
        {
            CommitControls(SettingsChangeKind.Behavior);
            RefreshApplicationChoices();
        };

        WireSlider(TextSizeSlider, SettingsChangeKind.OverlayGeometry);
        WireSlider(UiScaleSlider, SettingsChangeKind.ApplicationScale);
        WireSlider(BorderWidthSlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(BackgroundOpacitySlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(InactiveSeparatedOpacitySlider, SettingsChangeKind.OverlayAppearance);
        WireSlider(BackgroundStrengthSlider, SettingsChangeKind.BackgroundStrength);
        WireSlider(CommandChainingTimeoutSlider, SettingsChangeKind.Behavior);

        WireCheckBox(ClickThroughCheck, SettingsChangeKind.OverlayInteraction);
        WireCheckBox(HideOverlayCaptureCheck, SettingsChangeKind.OverlayInteraction);
        foreach (var (control, _) in TransparencyPartControls)
            WireCheckBox(control, SettingsChangeKind.OverlayAppearance);
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
        InternetCheckIntervalTextBox.TextChanged += (_, _) =>
        {
            if (_loading) return;
            RefreshInternetConnection();
        };
        InternetCheckIntervalTextBox.LostFocus += (_, _) => CommitInternetInterval();
        InternetCheckIntervalTextBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; CommitInternetInterval(); }
        };
        InternetFileNameTextBox.TextChanged += (_, _) => { if (!_loading) RefreshInternetExport(); };
        InternetFileNameTextBox.LostFocus += (_, _) => CommitInternetFile();
        InternetFileNameTextBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; CommitInternetFile(); } };

        WireCheckBox(IdleStopUnnamedCheck, SettingsChangeKind.IdleStop);
        WireCheckBox(IdleStopSubtractCheck, SettingsChangeKind.IdleStop);
        IdleStopDefaultTimeoutTextBox.TextChanging += (_, _) =>
        {
            if (_loading || !TryBehaviorNumber(IdleStopDefaultTimeoutTextBox, 1440, out _)) return;
            CommitControls(SettingsChangeKind.IdleStop);
            UpdateSelectedProjectIdleUi(IdleProjectComboBox.SelectedItem as string);
        };

        WireCheckBox(FocusTrackingEnabledCheck, SettingsChangeKind.FocusTracking);
        FocusDistractionThresholdTextBox.TextChanging += (_, _) =>
        {
            if (!_loading && TryBehaviorNumber(FocusDistractionThresholdTextBox, 1440, out _)) CommitControls(SettingsChangeKind.FocusTracking);
        };
        FocusMinimumPauseTextBox.TextChanging += (_, _) =>
        {
            if (!_loading && TryBehaviorNumber(FocusMinimumPauseTextBox, 3600, out _)) CommitControls(SettingsChangeKind.FocusTracking);
        };
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
        RestoreTypographyPage();
        _settings.Typography.Normalize();
        GlobalTypographyEditor.Children.Clear();
        SectionTypographyEditors.Children.Clear();
        AddTypographyEditor(GlobalTypographyEditor, _settings.Typography.Global, "Global text", false);
        foreach (var (key, label) in TypographySettings.Scopes)
            AddTypographyEditor(SectionTypographyEditors, _settings.Typography.Sections[key], label, true);
        if(_navigatorSettingsBody!=null)UpdateNavigatorCategory();
    }

    private void AddTypographyEditor(Panel host, TypographyStyle style, string label, bool isOverride)
    {
        var editor = new TypographyEditor(style, label, isOverride);
        editor.Changed += () => {CommitControls(SettingsChangeKind.Typography);RefreshTypographyPreview();};
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
            {
                _settings.ThemeMode = AppThemeCatalog.Normalize(ThemeCombo.SelectedItem?.ToString());
                _settings.NikaBackground = NikaThemeCatalog.NormalizeBackground((NikaBackgroundCombo.SelectedItem as NikaThemeOption)?.Id);
                _settings.NikaTypography = NikaThemeCatalog.NormalizeTypography((NikaTypographyCombo.SelectedItem as NikaThemeOption)?.Id);
                _settings.NikaSidebarArtwork = NikaThemeCatalog.NormalizeArtwork((NikaArtworkCombo.SelectedItem as NikaThemeOption)?.Id);
            }

            if ((change & SettingsChangeKind.OverlayTheme) != 0)
                _settings.OverlayTheme = OverlayThemeCatalog.Normalize(OverlayThemeCombo.SelectedItem?.ToString());

            if ((change & SettingsChangeKind.ControllerStyle) != 0)
            {
                _settings.NikaControllerPixel = ControllerStyleCombo.SelectedIndex == 1;
                ControllerDigitsCombo.IsEnabled = _settings.NikaControllerPixel;
                _settings.ControllerLargeClockDigits = NikaThemeCatalog.NormalizeLargeClockDigits((ControllerDigitsCombo.SelectedItem as NikaThemeOption)?.Id);
            }

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

            if ((change & SettingsChangeKind.Behavior) != 0)
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
            }

            if ((change & SettingsChangeKind.IdleStop) != 0)
            {
                _settings.IdleStopUnnamedTimers = IdleStopUnnamedCheck.IsChecked == true;
                _settings.IdleStopSubtractDuration = IdleStopSubtractCheck.IsChecked == true;
                if (TryBehaviorNumber(IdleStopDefaultTimeoutTextBox, 1440, out int defaultIdleMin))
                {
                    _settings.DefaultIdleStopTimeoutMinutes = defaultIdleMin;
                }
            }

            if ((change & SettingsChangeKind.FocusTracking) != 0)
            {
                _settings.FocusTrackingEnabled = FocusTrackingEnabledCheck.IsChecked == true;
                if (TryBehaviorNumber(FocusDistractionThresholdTextBox, 1440, out int thresh))
                {
                    _settings.FocusDistractionThresholdMinutes = thresh;
                }
                if (TryBehaviorNumber(FocusMinimumPauseTextBox, 3600, out int minPause))
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
                if (int.TryParse(ActivityWatchPeriodicSyncIntervalTextBox.Text?.Trim(), out int syncInterval) && syncInterval is >= 1 and <= 1440)
                    _settings.ActivityWatchPeriodicSyncIntervalMinutes = syncInterval;
                _settings.ActivityWatchIncludeTitles = ActivityWatchIncludeTitlesCheck.IsChecked == true;
                _settings.ActivityWatchIncludeWeb = ActivityWatchIncludeWebCheck.IsChecked == true;
                _settings.ActivityWatchServerUrl = ActivityWatchServerUrlTextBox.Text?.Trim() ?? "";
                if (ActivityWatchSync.TryNormalizeExportFileName(ActivityWatchFileNameTextBox.Text, out _))
                    _settings.ActivityWatchExportFileName = ActivityWatchFileNameTextBox.Text?.Trim() ?? "";
                if (int.TryParse(ActivityWatchMinDurationTextBox.Text?.Trim(), out int minSec) && minSec is >= 1 and <= 3600)
                    _settings.ActivityWatchMinDurationSeconds = minSec;
            }

            if ((change & SettingsChangeKind.PeriodicReview) != 0)
            {
                _settings.PeriodicReviewEnabled = PeriodicReviewEnabledCheck.IsChecked == true;
                if (ReviewInteger(PeriodicReviewIntervalTextBox, 1, 1440, out int reviewInterval))
                    _settings.PeriodicReviewIntervalMinutes = reviewInterval;
                if (ReviewInteger(PeriodicReviewSnoozeTextBox, 1, 1440, out int snooze))
                    _settings.PeriodicReviewSnoozeMinutes = snooze;
                if (ReviewInteger(PeriodicReviewAutoDismissTextBox, 1, 120, out int dismiss))
                    _settings.PeriodicReviewAutoDismissSeconds = dismiss;
                if (ReviewInteger(PeriodicReviewMinDurationTextBox, 0, 3600, out int minDur))
                    _settings.PeriodicReviewMinDurationSeconds = minDur;
                if (ReviewPercent(out double minPct))
                    _settings.PeriodicReviewMinActivityPercent = minPct;
            }

            if ((change & SettingsChangeKind.InternetMonitor) != 0)
            {
                _settings.InternetMonitorEnabled = InternetMonitorEnabledCheck.IsChecked == true;
                _settings.InternetMonitorOnlyDuringTimers = InternetMonitorOnlyDuringTimersCheck.IsChecked == true;
                if (InternetRetentionCombo.SelectedItem is ComboBoxItem retItem && int.TryParse(retItem.Tag?.ToString(), out int rDays))
                    _settings.InternetLogRetentionDays = rDays;
            }

            UpdateValueLabels();
            UpdateDependentControlStates();
            if ((change & SettingsChangeKind.ControllerStyle) != 0) RefreshAppearancePreview();
            if ((change & (SettingsChangeKind.Behavior | SettingsChangeKind.FocusTracking)) != 0)
            {
                UpdateBehaviorControlStates();
                RefreshBehaviorPreview();
            }
            if ((change & (SettingsChangeKind.Theme
                           | SettingsChangeKind.OverlayTheme
                           | SettingsChangeKind.OverlayAppearance
                           | SettingsChangeKind.OverlayGeometry
                           | SettingsChangeKind.ApplicationScale
                           | SettingsChangeKind.BackgroundSelection
                           | SettingsChangeKind.BackgroundStrength)) != 0)
            {
                SchedulePreviewFromAppliedSettings();
            }

            if ((change & (SettingsChangeKind.Telegram | SettingsChangeKind.ObsidianExport)) != 0)
                RefreshTelegramConfiguration();

            CrashLogger.RecordUiAction($"Settings change: {change}", CurrentCategory);
            SettingsChanged?.Invoke(change);
            UpdateNavigatorInteraction();
            if ((change & SettingsChangeKind.InternetMonitor) != 0) RefreshInternetConnection();
            if ((change & (SettingsChangeKind.InternetMonitor | SettingsChangeKind.ObsidianExport)) != 0) RefreshInternetExport();
            if ((change & SettingsChangeKind.ActivityWatch) != 0) RefreshActivityWatchConnection();
            if ((change & (SettingsChangeKind.ActivityWatch | SettingsChangeKind.ObsidianExport)) != 0) RefreshActivityWatchExport();
            if ((change & (SettingsChangeKind.ActivityWatch | SettingsChangeKind.PeriodicReview)) != 0)
            { _reviewExampleAccepted = null; RefreshActivityWatchReview(); }
        }
        finally
        {
            _committing = false;
        }
    }

    private void UpdateValueLabels()
    {
        SyncStopwatchTextSizeControl();
        TextSizeValueText.Text = $"{Math.Round(TextSizeSlider.Value):0} px";
        BorderWidthValueText.Text = $"{Math.Round(BorderWidthSlider.Value):0} px";
        BackgroundOpacityValueText.Text = $"{Math.Round(BackgroundOpacitySlider.Value):0}%";
        InactiveSeparatedOpacityValueText.Text = $"{Math.Round(InactiveSeparatedOpacitySlider.Value):0}%";
        BackgroundStrengthValueText.Text = $"{Math.Round(BackgroundStrengthSlider.Value):0}%";
        CommandChainingTimeoutValueText.Text = $"{CommandChainingTimeoutSlider.Value:0.0} s";
    }

    private void UpdateDependentControlStates()
    {
        bool nikaOverlay=OverlayThemeCatalog.Resolve(_settings.OverlayTheme,_settings.ThemeMode)==OverlayThemeCatalog.Nika;
        foreach(var control in new Control[]{FontCombo,TextColorCombo,BorderColorCombo,BorderWidthSlider,BackgroundOpacitySlider,InactiveSeparatedOpacitySlider})control.IsEnabled=!nikaOverlay;
        NikaStopwatchAppearanceHint.IsVisible=nikaOverlay;
        if(_stopwatchActivateButton!=null)_stopwatchActivateButton.IsVisible=!nikaOverlay;
        NikaOptionsPanel.IsVisible = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Nika;
        if (TodoAppearancePanel != null && CurrentCategory == "TodoAppearance") LoadTodoAppearance();
        TransparencyDetailsExpander.IsVisible =
            OverlayThemeCatalog.Resolve(_settings.OverlayTheme, _settings.ThemeMode) == OverlayThemeCatalog.Pirate;

        bool hasPattern = BackgroundCombo.SelectedItem is AppBackgroundChoice
            { IsThemeDefault: false, IsAvailable: true };
        BackgroundStrengthSlider.IsEnabled = hasPattern;
        RemoveBackgroundButton.IsEnabled = BackgroundCombo.SelectedItem is AppBackgroundChoice
            { IsCustom: true };

    }

    private void UpdatePreviewSafely()
    {
        try
        {
            UpdateNavigatorSettingsTheme();
            if (AppThemeCatalog.Normalize(_settings.ThemeMode) is AppThemeCatalog.Pirate or AppThemeCatalog.Nika)
                SettingsHeaderSurface.Resources["PrimaryTextBrush"] = new SolidColorBrush(Color.Parse("#F6E6BF"));
            else
                SettingsHeaderSurface.Resources.Remove("PrimaryTextBrush");
            PreviewClock.ApplySettings(_settings);
            if(CurrentCategory=="StopwatchAppearance")RefreshStopwatchAppearancePreview();
            PreviewClock.Text = _settings.TimeFormat switch
            {
                1 => "00:05:10",
                2 => "05:10.5",
                3 => "05:10",
                4 => "00:05",
                _ => "00:05:10.5"
            };
            PreviewClock.ProjectName = "Programming";
            RefreshNavigatorSettingsPreview();
            RefreshAppearancePreview();
            PreviewClock.ApplyToolbar(PreviewToolbarSurface,
                new[] { PreviewCloseButton, PreviewPauseButton, PreviewResetButton, PreviewEditButton, PreviewSeparateMergeButton });

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
        if (tag != "StopwatchAppearance") CloseFrameGallery();
        if (tag != "Internet") { CancelInternetCheck(); DismissInternetExport(); }
        if (tag != "ActivityWatch") { CancelActivityWatchConnection(); CancelActivityWatchExport(); }
        else RefreshActivityWatchExport();
        if (tag != "Telegram") SetTelegramTokenVisible(false);
        if (tag != "Habitica") HabiticaPanel.HideConnectionDraft();
        UpdateNavigatorCategory();
        UpdateTodoPreviewVisibility(tag);
        OverlayPanel.IsVisible = tag == "Overlay";
        AppearancePanel.IsVisible = tag == "Appearance";
        TypographyPanel.IsVisible = tag == "Typography";
        BehaviorPanel.IsVisible = tag == "Behavior";
        ApplicationPanel.IsVisible = tag == "Application";
        ObsidianPanel.IsVisible = tag == "Obsidian";
        TelegramPanel.IsVisible = tag == "Telegram";
        HabiticaPanel.IsVisible = tag == "Habitica";
        ActivityWatchPanel.IsVisible = tag == "ActivityWatch";
        InternetPanel.IsVisible = tag == "Internet";
        if (tag == "Internet") _ = LoadInternetHistoryAsync();
        CrashLogger.RecordUiAction("Settings category changed", CurrentCategory);
        if (SettingsScrollViewer != null)
            SettingsScrollViewer.Offset = new Vector(0, 0);
    }

    private async void BrowseObsidianFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        try
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Obsidian Vault or Target Folder", AllowMultiple = false
            };
            if (!string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder) && Directory.Exists(_settings.ObsidianVaultFolder))
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(_settings.ObsidianVaultFolder);
            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (!_closed && result.Count > 0 && result[0].TryGetLocalPath() is string localPath)
                ObsidianFolderTextBox.Text = localPath;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "Settings.ObsidianFolder");
            if (!_closed) SetObsidianStatus("Could not choose a folder", ex.Message, true);
        }
    }

    private void ExportNowButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ObsidianFolderTextBox.Text))
        {
            RefreshObsidianDestination();
            return;
        }
        if (_historyProvider == null)
        {
            SetObsidianStatus("Export unavailable", "History records are not available.", true);
            return;
        }
        try
        {
            var result = ObsidianLogSync.SyncHistory(_historyProvider(), _settings,
                ObsidianFolderTextBox.Text ?? "", ObsidianFileNameTextBox.Text ?? "");
            SetObsidianStatus(result.Success ? "Export complete" : "Export failed",
                result.Success ? $"{result.Message} ({DateTime.Now:HH:mm:ss})" : result.Message ?? "Export failed.", !result.Success);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "Settings.ObsidianExport");
            SetObsidianStatus("Export failed", ex.Message, true);
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
        _updatingIdleProject = true;
        try
        {
            UpdateSelectedProjectIdleFields(projectName);
            IdleProjectTimeoutTextBox.Classes.Remove("invalidTime");
            ToolTip.SetTip(IdleProjectTimeoutTextBox, null);
        }
        finally { _updatingIdleProject = false; }
    }

    private void UpdateSelectedProjectIdleFields(string? projectName)
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
        UpdateIdleProjectStatus(isActive, timeoutMinutes);
    }

    private void UpdateIdleProjectStatus(bool isActive, int timeoutMinutes)
    {
        if (isActive)
        {
            IdleProjectStatusSummaryText.Text = $"Pauses after {timeoutMinutes} min idle";
            IdleProjectStatusSummaryText.Foreground = _behaviorPage != null ? BehaviorTeal : Brushes.LimeGreen;
        }
        else
        {
            IdleProjectStatusSummaryText.Text = "Idle stopping is off for this project.";
            if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stBrush) == true && stBrush is IBrush brush)
                IdleProjectStatusSummaryText.Foreground = brush;
        }
    }

    private void IdleProjectActiveCheck_Click(object? sender, RoutedEventArgs e)
    {
        if (IdleProjectComboBox.SelectedItem is string selectedProject && !string.IsNullOrWhiteSpace(selectedProject))
        {
            bool enabled = IdleProjectActiveCheck.IsChecked == true;
            _settings.IsIdleStopActiveForProject(selectedProject, out int savedTimeout, out _);
            int timeout = TryBehaviorNumber(IdleProjectTimeoutTextBox, 1440, out int parsed) ? parsed : savedTimeout;

            _settings.SetProjectIdleRule(selectedProject, enabled, timeout);
            UpdateSelectedProjectIdleUi(selectedProject);
            RefreshIdleProjectsOverview();
            CommitControls(SettingsChangeKind.IdleStop);
        }
    }

    private void IdleProjectTimeoutTextBox_TextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_loading || _updatingIdleProject || _closed) return;
        if (IdleProjectComboBox.SelectedItem is string selectedProject && !string.IsNullOrWhiteSpace(selectedProject))
        {
            if (TryBehaviorNumber(IdleProjectTimeoutTextBox, 1440, out int timeout))
            {
                bool enabled = IdleProjectActiveCheck.IsChecked == true;
                _settings.SetProjectIdleRule(selectedProject, enabled, timeout);
                UpdateIdleProjectStatus(enabled, timeout);
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
        if (_behaviorCategories != null)
        {
            _behaviorCategories.ItemsSource = items.Where(item => item.CanDelete).ToArray();
            _behaviorCategoryEmpty!.IsVisible = !items.Any(item => item.CanDelete);
        }
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
        CommitControls(SettingsChangeKind.Behavior);
        PopulateCategories();
    }

    private void DeleteCategory_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string catName } && !AppSettings.DefaultProjectCategories.Contains(catName, StringComparer.OrdinalIgnoreCase))
        {
            _settings.ProjectCategories?.RemoveAll(c => string.Equals(c, catName, StringComparison.OrdinalIgnoreCase));
            _settings.NormalizeForRuntime();
            CommitControls(SettingsChangeKind.Behavior);
            PopulateCategories();
        }
    }

    private async void StartActivityWatchAppButton_Click(object? sender, RoutedEventArgs e)
        => await RunActivityWatchConnectionAsync(launch: true);

    private async void TestActivityWatchButton_Click(object? sender, RoutedEventArgs e)
        => await RunActivityWatchConnectionAsync(launch: false);

    private async void SyncActivityWatchNowButton_Click(object? sender, RoutedEventArgs e)
        => await RunActivityWatchExportAsync();

    private async void TestInternetButton_Click(object? sender, RoutedEventArgs e)
        => await RunInternetCheckAsync();

    private void InternetRetentionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CommitControls(SettingsChangeKind.InternetMonitor);
    }

    private async void SyncInternetLogButton_Click(object? sender, RoutedEventArgs e) => await RunInternetExportAsync(false);

    private void ClearInternetLogButton_Click(object? sender, RoutedEventArgs e) => BeginInternetClear();

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
        RefreshObsidianDestination();
    }

    private void ShowOverlayButton_Click(object? sender, RoutedEventArgs e)
    {
        CrashLogger.RecordUiAction("Overlay visibility requested", CurrentCategory);
        ShowOverlayRequested?.Invoke();
    }

    private void ReviewNowButton_Click(object? sender, RoutedEventArgs e)
    {
        CrashLogger.RecordUiAction("Periodic review requested manually from settings", CurrentCategory);
        RefreshActivityWatchReview();
        if (ReviewDetailValid()) PeriodicReviewRequested?.Invoke();
    }

    private void DoneButton_Click(object? sender, RoutedEventArgs e) => Close();

    private void SettingsScreensChanged(object? sender, EventArgs e)
    {
        if(_closed)return;
        LoadControls();RefreshNavigatorSettingsPreview();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        DismissInternetExport();
        CancelInternetCheck();
        CancelActivityWatchConnection(); CancelActivityWatchExport();
        SetTelegramTokenVisible(false);
        _previewTimer.Stop();
        ReleaseAppearancePreview();
        TelegramOutboxStore.OutboxChanged -= OnOutboxChanged;
        Screens.Changed -= SettingsScreensChanged;
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
