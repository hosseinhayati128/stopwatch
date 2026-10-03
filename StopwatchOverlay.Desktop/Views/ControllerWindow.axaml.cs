using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Views;

public sealed class TimerRailItemViewModel
{
    public Guid Id { get; init; }
    public string DisplayName { get; set; } = "";
    public string DisplaySummary { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool IsActive { get; set; }
    public bool IsSeparated { get; set; }
    public int Mode { get; set; }
}

public partial class ControllerWindow : Window
{
    private readonly TimerSessionManager _timerManager = new();
    private readonly TimerWorkspaceStore _workspaceStore = new();
    private readonly ProjectTimeStore _projectTimeStore = new();
    private ProjectTimeHistory _projectHistory = new();
    private AppSettings _settings = new();

    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _idleTimer;

    private OverlayWindow? _herdOverlay;
    private readonly Dictionary<Guid, OverlayWindow> _overlays = new();
    private LightRingWindow? _lightRingWindow;
    private ProjectDashboardWindow? _dashboardWindow;
    private SettingsWindow? _settingsWindow;
    private ShortcutsWindow? _shortcutsWindow;
    private ShortcutCommandHintWindow? _commandHintWindow;
    private DispatcherTimer? _commandModeTimer;
    private bool _isCommandModeActive;
    private bool _overlaysVisible = true;

    private bool _updatingUi;
    private bool _isExiting;

    private TimerSession CurrentTimer => _timerManager.Active ?? _timerManager.Sessions.FirstOrDefault() ?? _emptyTimer;
    private readonly TimerSession _emptyTimer = new(1);

    public ControllerWindow()
    {
        InitializeComponent();

        _settings = SettingsStore.Load();
        if (_projectTimeStore.TryLoad(out var loadedHistory) && loadedHistory != null)
        {
            _projectHistory = loadedHistory;
        }
        else
        {
            _projectHistory = new ProjectTimeHistory();
        }

        DateTime now = DateTime.UtcNow;
        bool loaded = _workspaceStore.TryLoad(_timerManager, now, now.ToLocalTime());
        if (!loaded || _timerManager.Sessions.Count == 0)
        {
            _timerManager.Create();
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTimerTick);
        _saveTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, OnSaveTimerTick);
        _idleTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnIdleTimerTick);

        Opened += OnOpened;
        Closing += OnClosing;

        RefreshTimerRail();
        UpdateActiveTimerDisplay();

        _timer.Start();
        _saveTimer.Start();
        _idleTimer.Start();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        InitializePlatformServices();
        UpdateOverlayStates();
        UpdateLightRing();
    }

    private void InitializePlatformServices()
    {
        try
        {
            PlatformServices.HotKey.UnregisterAll();
            PlatformServices.HotKey.HotKeyPressed -= OnHotKeyPressed;
            PlatformServices.HotKey.CommandKeyPressed -= OnCommandKeyPressed;
            PlatformServices.HotKey.HotKeyPressed += OnHotKeyPressed;
            PlatformServices.HotKey.CommandKeyPressed += OnCommandKeyPressed;

            var leader = _settings.LeaderShortcut ?? AppSettings.DefaultLeaderShortcut();
            PlatformServices.HotKey.RegisterHotKey(ShortcutAction.CommandLeader, leader.Modifiers, leader.VirtualKey);

            if (_settings.Shortcuts.TryGetValue(ShortcutAction.ShowActiveOverlay, out var overlaySc) && overlaySc != null)
            {
                PlatformServices.HotKey.RegisterHotKey(ShortcutAction.ShowActiveOverlay, overlaySc.Modifiers, overlaySc.VirtualKey);
            }
            if (_settings.Shortcuts.TryGetValue(ShortcutAction.OpenController, out var ctrlSc) && ctrlSc != null)
            {
                PlatformServices.HotKey.RegisterHotKey(ShortcutAction.OpenController, ctrlSc.Modifiers, ctrlSc.VirtualKey);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ControllerWindow.InitializePlatformServices");
        }
    }

    private void OnHotKeyPressed(object? sender, ShortcutAction action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (action)
            {
                case ShortcutAction.ShowActiveOverlay:
                    ToggleOverlayVisibility();
                    break;
                case ShortcutAction.OpenController:
                    Show();
                    Activate();
                    break;
                case ShortcutAction.CommandLeader:
                    if (_isCommandModeActive)
                    {
                        ExitShortcutCommandMode();
                    }
                    else
                    {
                        EnterShortcutCommandMode();
                    }
                    break;
            }
        });
    }

    private void EnterShortcutCommandMode()
    {
        _isCommandModeActive = true;
        PlatformServices.HotKey.IsInCommandMode = true;

        if (_commandHintWindow == null)
        {
            _commandHintWindow = new ShortcutCommandHintWindow();
            _commandHintWindow.Show();
        }

        double timeout = Math.Max(1.0, _settings.CommandChainingTimeoutSeconds);
        if (_commandModeTimer == null)
        {
            _commandModeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(timeout)
            };
            _commandModeTimer.Tick += (_, _) => ExitShortcutCommandMode();
        }
        else
        {
            _commandModeTimer.Stop();
            _commandModeTimer.Interval = TimeSpan.FromSeconds(timeout);
        }

        _commandModeTimer.Start();
    }

    private void ExitShortcutCommandMode()
    {
        _isCommandModeActive = false;
        PlatformServices.HotKey.IsInCommandMode = false;
        _commandModeTimer?.Stop();

        if (_commandHintWindow != null)
        {
            try { _commandHintWindow.Close(); } catch { }
            _commandHintWindow = null;
        }
    }

    private void OnCommandKeyPressed(object? sender, uint vk)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isCommandModeActive) return;

            if (ShortcutCommandMap.IsEscape(vk))
            {
                ExitShortcutCommandMode();
                return;
            }

            if (ShortcutCommandMap.TryGetAction(vk, out var action))
            {
                if (action is ShortcutAction.OpenController or ShortcutAction.OpenDashboard or ShortcutAction.NewTimer or ShortcutAction.RenameTimer or ShortcutAction.EditTimer or ShortcutAction.AddRecord)
                {
                    ExitShortcutCommandMode();
                }
                else
                {
                    _commandModeTimer?.Stop();
                    _commandModeTimer?.Start();
                }

                ExecuteShortcutAction(action);
            }
            else
            {
                ExitShortcutCommandMode();
            }
        });
    }

    private void ExecuteShortcutAction(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.StartStop:
                ToggleStartPause();
                break;
            case ShortcutAction.Reset:
                ResetActiveTimer();
                break;
            case ShortcutAction.ToggleOverlay:
                ToggleOverlayVisibility();
                break;
            case ShortcutAction.Lap:
                AddLap();
                break;
            case ShortcutAction.ToggleClock:
                ToggleClockMode();
                break;
            case ShortcutAction.NewTimer:
                CreateNewTimer();
                break;
            case ShortcutAction.NextTimer:
                SelectNextTimer();
                break;
            case ShortcutAction.CloseTimer:
                CloseSession(CurrentTimer);
                break;
            case ShortcutAction.RenameTimer:
                RenameTimerMenuItem_Click(this, new RoutedEventArgs());
                break;
            case ShortcutAction.EditTimer:
                OpenTimerEditor(CurrentTimer);
                break;
            case ShortcutAction.OpenDashboard:
                DashboardButton_Click(this, new RoutedEventArgs());
                break;
            case ShortcutAction.OpenController:
                Show();
                Activate();
                break;
            case ShortcutAction.SeparateOverlay:
                SeparateActiveSession();
                break;
            case ShortcutAction.MergeOverlay:
                MergeActiveSession();
                break;
            case ShortcutAction.NextSeparatedOverlay:
                SwitchSeparatedOverlay();
                break;
        }
    }

    private TimerSession? GetActiveHerdSession()
    {
        var herd = _timerManager.HerdSessions;
        if (herd.Count == 0) return null;
        if (_timerManager.Active != null && !_timerManager.Active.IsSeparated)
            return _timerManager.Active;
        return herd[0];
    }

    private OverlayWindow EnsureHerdOverlay()
    {
        if (_herdOverlay != null) return _herdOverlay;

        _herdOverlay = new OverlayWindow();
        _herdOverlay.SetSeparateMergeState(false);
        _herdOverlay.SetClickThrough(_settings.ClickThrough);
        _herdOverlay.SetHideFromCapture(_settings.HideOverlayFromCapture);

        _herdOverlay.CloseRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null) CloseSession(herdSession);
        };
        _herdOverlay.PauseResumeRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null) ToggleSessionStartPause(herdSession);
        };
        _herdOverlay.ResetRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null) ResetSession(herdSession);
        };
        _herdOverlay.EditRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null) OpenTimerEditor(herdSession);
        };
        _herdOverlay.ActivationRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null)
            {
                _timerManager.Activate(herdSession);
                RefreshTimerRail();
                UpdateActiveTimerDisplay();
                UpdateOverlayStates();
            }
        };
        _herdOverlay.SeparateMergeRequested += () =>
        {
            var herdSession = GetActiveHerdSession();
            if (herdSession != null)
            {
                _timerManager.Activate(herdSession);
                SeparateActiveSession();
            }
        };

        ApplyOverlayStyle(_herdOverlay);
        if (_overlaysVisible) _herdOverlay.Show();
        return _herdOverlay;
    }

    private OverlayWindow GetOrCreateSeparatedOverlay(TimerSession session)
    {
        if (_overlays.TryGetValue(session.Id, out var existing))
            return existing;

        var overlay = new OverlayWindow();
        overlay.SetTimerName(session.Name);
        overlay.UpdateTime(FormatDisplayTime(session));
        overlay.SetRunning(session.IsRunning);
        overlay.SetSeparateMergeState(true);
        overlay.SetClickThrough(_settings.ClickThrough);
        overlay.SetHideFromCapture(_settings.HideOverlayFromCapture);

        overlay.CloseRequested += () => CloseSession(session);
        overlay.PauseResumeRequested += () => ToggleSessionStartPause(session);
        overlay.ResetRequested += () => ResetSession(session);
        overlay.EditRequested += () => OpenTimerEditor(session);
        overlay.ActivationRequested += () =>
        {
            _timerManager.Activate(session);
            RefreshTimerRail();
            UpdateActiveTimerDisplay();
            UpdateOverlayStates();
        };
        overlay.SeparateMergeRequested += () =>
        {
            _timerManager.Activate(session);
            MergeActiveSession();
        };

        ApplyOverlayStyle(overlay);
        if (_herdOverlay != null)
        {
            var herdPos = _herdOverlay.Position;
            overlay.Position = new PixelPoint(herdPos.X + 40, herdPos.Y + 40);
        }
        if (_overlaysVisible) overlay.Show();

        _overlays[session.Id] = overlay;
        return overlay;
    }

    private void UpdateOverlayStates()
    {
        var herd = _timerManager.HerdSessions;
        var active = _timerManager.Active;

        if (herd.Count > 0)
        {
            var herdOverlay = EnsureHerdOverlay();
            var herdSession = GetActiveHerdSession();
            if (herdSession != null)
            {
                herdOverlay.SetTimerName(herdSession.Name);
                herdOverlay.UpdateTime(FormatDisplayTime(herdSession));
                herdOverlay.SetRunning(herdSession.IsRunning);
                herdOverlay.SetPauseResumeEnabled(herdSession.Mode != 1);

                bool isHerdActive = active != null && !active.IsSeparated && herdSession == active;
                herdOverlay.SetActive(isHerdActive);
                herdOverlay.SetInactiveSeparated(!isHerdActive, _settings.InactiveSeparatedOverlayOpacity / 100.0);
                herdOverlay.SetSeparateMergeState(false);
                if (_overlaysVisible && !herdOverlay.IsVisible) herdOverlay.Show();
            }
        }
        else if (_herdOverlay != null)
        {
            _herdOverlay.Hide();
        }

        var separatedIds = _timerManager.SeparatedSessions.Select(s => s.Id).ToHashSet();
        var toRemove = _overlays.Keys.Where(id => !separatedIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _overlays[id].Close();
            _overlays.Remove(id);
        }

        foreach (var session in _timerManager.SeparatedSessions)
        {
            var overlay = GetOrCreateSeparatedOverlay(session);
            overlay.SetTimerName(session.Name);
            overlay.UpdateTime(FormatDisplayTime(session));
            overlay.SetRunning(session.IsRunning);
            overlay.SetPauseResumeEnabled(session.Mode != 1);

            bool isSessionActive = session == active;
            overlay.SetActive(isSessionActive);
            overlay.SetInactiveSeparated(!isSessionActive, _settings.InactiveSeparatedOverlayOpacity / 100.0);
            overlay.SetSeparateMergeState(true);
            if (_overlaysVisible && !overlay.IsVisible) overlay.Show();
        }
    }

    private void SeparateActiveSession()
    {
        var session = CurrentTimer;
        if (session == null || session.IsSeparated) return;
        _timerManager.Separate(session);
        GetOrCreateSeparatedOverlay(session);
        UpdateOverlayStates();
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
    }

    private void MergeActiveSession()
    {
        var session = CurrentTimer;
        if (session == null || !session.IsSeparated) return;
        _timerManager.Merge(session);
        if (_overlays.TryGetValue(session.Id, out var overlay))
        {
            overlay.Close();
            _overlays.Remove(session.Id);
        }
        UpdateOverlayStates();
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
    }

    private void SwitchSeparatedOverlay()
    {
        var next = _timerManager.CycleNextSeparatedOrHerd();
        if (next != null)
        {
            RefreshTimerRail();
            UpdateActiveTimerDisplay();
            UpdateOverlayStates();
        }
    }

    private void ApplyOverlayStyle(OverlayWindow overlay)
    {
        Color text = _settings.TextColor == "Theme default" ? Colors.White : ParseColor(_settings.TextColor, Colors.White);
        Color border = ParseColor(_settings.BorderColor, Colors.Black);
        overlay.ApplySettings(
            text,
            border,
            (int)_settings.TextSize,
            (int)_settings.BorderWidth,
            _settings.FontFamily,
            _settings.BackgroundOpacity / 100.0,
            _settings.TextColor == "Theme default");
    }

    private static Color ParseColor(string name, Color fallback) => name switch
    {
        "White" => Colors.White,
        "Yellow" => Colors.Yellow,
        "Cyan" => Colors.Cyan,
        "Lime" => Colors.Lime,
        "Orange" => Colors.Orange,
        "Red" => Colors.Red,
        "Magenta" => Colors.Magenta,
        "Charcoal" => Color.FromRgb(44, 41, 36),
        "Dark Gray" => Colors.DarkGray,
        "Blue" => Colors.Blue,
        "Black" => Colors.Black,
        _ => fallback
    };

    private void UpdateLightRing()
    {
        if (_settings.LightRingEnabled)
        {
            if (_lightRingWindow == null)
            {
                _lightRingWindow = new LightRingWindow();
                _lightRingWindow.Show();
            }
            _lightRingWindow.ApplySettings(
                _settings.LightRingBrightness / 100.0,
                (int)_settings.LightRingWidth,
                _settings.LightRingHideFromCapture);
            _lightRingWindow.PositionOnScreen(Screens.Primary);
        }
        else if (_lightRingWindow != null)
        {
            _lightRingWindow.Close();
            _lightRingWindow = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;

        foreach (var session in _timerManager.Sessions)
        {
            if (session.Mode == 2 && session.IsRunning)
            {
                if (session.LastCountdownUpdateUtc == default)
                    session.LastCountdownUpdateUtc = now;

                var delta = now - session.LastCountdownUpdateUtc;
                session.LastCountdownUpdateUtc = now;

                if (session.CountdownRemaining > delta)
                {
                    session.CountdownRemaining -= delta;
                }
                else
                {
                    session.CountdownRemaining = TimeSpan.Zero;
                    session.IsRunning = false;
                    session.Stopwatch.Stop();
                }
            }
        }

        UpdateOverlayStates();
        UpdateActiveTimerDisplay();
    }

    private void UpdateActiveTimerDisplay()
    {
        var session = CurrentTimer;
        TimeDisplay.Text = FormatDisplayTime(session);
        ActiveWorkspaceTitle.Text = session.DisplayName;

        bool hasProject = !string.IsNullOrWhiteSpace(session.Name);
        ProjectSubtitle.IsVisible = hasProject;
        ProjectSubtitle.Text = hasProject ? $"Project: {session.Name}" : "";

        StartStopButton.Content = session.IsRunning ? "Pause" : "Start";
        RecIndicator.IsVisible = session.IsRunning && _settings.ShowRecIndicator;

        StatusDot.Fill = session.IsRunning ? Brushes.LimeGreen : Brushes.Gray;
        StatusText.Text = session.IsRunning ? "Running" : "Paused";

        LapListBox.ItemsSource = session.LapTimes;
        LapPlaceholder.IsVisible = session.LapTimes.Count == 0;

        UpdateModeRadios();
        ToggleCombinedMenuItem.Header = session.IsSeparated ? "Merge with herd overlay" : "Separate clock overlay";
        CombinedRailStatus.Text = session.IsSeparated ? "Separated overlay" : "Herd overlay";
    }

    private void ToggleClockMode()
    {
        var session = CurrentTimer;
        if (session.Mode == 1)
        {
            session.Mode = session.LastNonClockMode;
        }
        else
        {
            session.LastNonClockMode = session.Mode;
            session.Mode = 1;
        }
        UpdateModeRadios();
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
        UpdateOverlayStates();
    }

    private void UpdateModeRadios()
    {
        _updatingUi = true;
        try
        {
            StopwatchModeRadio.IsChecked = CurrentTimer.Mode == 0;
            ClockModeRadio.IsChecked = CurrentTimer.Mode == 1;
            CountdownModeRadio.IsChecked = CurrentTimer.Mode == 2;
            TimecodeModeRadio.IsChecked = CurrentTimer.Mode == 3;
            CountdownPanel.IsVisible = CurrentTimer.Mode == 2;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private static string FormatDisplayTime(TimerSession session)
    {
        TimeSpan time = session.Mode switch
        {
            1 => DateTime.Now.TimeOfDay,
            2 => session.CountdownRemaining,
            _ => session.Elapsed
        };

        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 100:0}";
    }

    private void RefreshTimerRail()
    {
        var active = _timerManager.Active;
        var items = _timerManager.Sessions.Select(s => new TimerRailItemViewModel
        {
            Id = s.Id,
            DisplayName = s.DisplayName,
            DisplaySummary = $"{FormatDisplayTime(s)} · {(s.IsRunning ? "Running" : "Paused")}{(s.IsSeparated ? " · Separated" : "")}",
            IsRunning = s.IsRunning,
            IsActive = s == active,
            IsSeparated = s.IsSeparated,
            Mode = s.Mode
        }).ToList();

        _updatingUi = true;
        try
        {
            TimerRailList.ItemsSource = items;
            var activeVm = items.FirstOrDefault(i => i.IsActive);
            if (activeVm != null)
                TimerRailList.SelectedItem = activeVm;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            _workspaceStore.Save(_timerManager, now);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ControllerWindow.SaveSnapshot");
        }
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        try
        {
            var idleTime = PlatformServices.IdleDetection.GetIdleTime();
            var session = CurrentTimer;
            if (session.IsRunning && idleTime.TotalMinutes >= _settings.DefaultIdleStopTimeoutMinutes)
            {
                session.IsRunning = false;
                session.Stopwatch.Stop();
                if (_settings.IdleStopSubtractDuration && session.Elapsed > idleTime)
                {
                    session.RestoreElapsed(session.Elapsed - idleTime, false);
                }
                UpdateActiveTimerDisplay();
                RefreshTimerRail();
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ControllerWindow.IdleCheck");
        }
    }

    private void ToggleStartPause()
    {
        ToggleSessionStartPause(CurrentTimer);
    }

    private void ToggleSessionStartPause(TimerSession session)
    {
        session.IsRunning = !session.IsRunning;
        if (session.IsRunning)
        {
            session.Stopwatch.Start();
            session.LastCountdownUpdateUtc = DateTime.UtcNow;
        }
        else
        {
            session.Stopwatch.Stop();
        }

        UpdateActiveTimerDisplay();
        RefreshTimerRail();

        if (_overlays.TryGetValue(session.Id, out var overlay))
        {
            overlay.SetRunning(session.IsRunning);
        }
    }

    private void ResetActiveTimer()
    {
        ResetSession(CurrentTimer);
    }

    private void ResetSession(TimerSession session)
    {
        session.IsRunning = false;
        session.ResetElapsed();
        session.LapTimes.Clear();
        session.CountdownRemaining = session.CountdownDuration;

        UpdateActiveTimerDisplay();
        RefreshTimerRail();

        if (_overlays.TryGetValue(session.Id, out var overlay))
        {
            overlay.UpdateTime(FormatDisplayTime(session));
            overlay.SetRunning(false);
        }
    }

    private void AddLap()
    {
        var session = CurrentTimer;
        string lapStr = $"Lap {session.LapTimes.Count + 1}: {FormatDisplayTime(session)}";
        session.LapTimes.Insert(0, lapStr);
        UpdateActiveTimerDisplay();
    }

    private void ToggleOverlayVisibility()
    {
        _overlaysVisible = !_overlaysVisible;
        ToggleOverlayButton.Content = _overlaysVisible ? "Hide overlay" : "Show overlay";
        if (_herdOverlay != null)
        {
            if (_overlaysVisible) _herdOverlay.Show();
            else _herdOverlay.Hide();
        }
        foreach (var overlay in _overlays.Values)
        {
            if (_overlaysVisible) overlay.Show();
            else overlay.Hide();
        }
    }

    private void CreateNewTimer()
    {
        var newSession = _timerManager.Create();
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
        UpdateOverlayStates();
    }

    private void SelectNextTimer()
    {
        var next = _timerManager.CycleNextHerd() ?? _timerManager.CycleNext();
        if (next != null)
        {
            RefreshTimerRail();
            UpdateActiveTimerDisplay();
            UpdateOverlayStates();
        }
    }

    private void ToggleCombinedOverlay()
    {
        if (CurrentTimer.IsSeparated)
        {
            MergeActiveSession();
        }
        else
        {
            SeparateActiveSession();
        }
    }

    private async void CloseSession(TimerSession session)
    {
        if (_timerManager.Sessions.Count <= 1)
        {
            ResetSession(session);
            return;
        }

        string targetName = session.DisplayName;
        bool confirmed = await ConfirmationDialogWindow.ShowAsync(
            this,
            "Close Timer",
            "Close Timer",
            $"Are you sure you want to close '{targetName}'?",
            "Close",
            destructive: true);

        if (!confirmed) return;

        if (_overlays.TryGetValue(session.Id, out var overlay))
        {
            overlay.Close();
            _overlays.Remove(session.Id);
        }

        _timerManager.Close(session);
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
        UpdateOverlayStates();
    }

    private async void OpenTimerEditor(TimerSession session)
    {
        var projectNames = _projectHistory.ProjectNames.ToList();
        var intervals = Array.Empty<ProjectWorkIntervalView>();
        var editor = new TimerEditorWindow(session, projectNames, intervals, canUndo: false, settings: _settings);
        await editor.ShowDialog(this);
        if (editor.WasSaved)
        {
            session.RestoreElapsed(editor.NewTimeValue, editor.NewIsRunning);
            if (!string.IsNullOrEmpty(editor.NewProjectName))
            {
                session.Name = editor.NewProjectName;
                if (_overlays.TryGetValue(session.Id, out var overlay))
                    overlay.SetTimerName(session.Name);
            }
            UpdateActiveTimerDisplay();
            RefreshTimerRail();
            UpdateOverlayStates();
        }
    }

    private void TimerRailList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi) return;
        if (TimerRailList.SelectedItem is TimerRailItemViewModel vm)
        {
            var session = _timerManager.Sessions.FirstOrDefault(s => s.Id == vm.Id);
            if (session != null)
            {
                _timerManager.Activate(session);
                UpdateActiveTimerDisplay();
                UpdateOverlayStates();
            }
        }
    }

    private void ModeRadio_Checked(object? sender, RoutedEventArgs e)
    {
        if (StopwatchModeRadio.IsChecked == true)
            CurrentTimer.Mode = 0;
        else if (ClockModeRadio.IsChecked == true)
            CurrentTimer.Mode = 1;
        else if (CountdownModeRadio.IsChecked == true)
            CurrentTimer.Mode = 2;
        else if (TimecodeModeRadio.IsChecked == true)
            CurrentTimer.Mode = 3;

        CountdownPanel.IsVisible = CurrentTimer.Mode == 2;
        RefreshTimerRail();
    }

    private void CountdownTypeRadio_Checked(object? sender, RoutedEventArgs e)
    {
        // Toggle countdown duration vs until-clock-time
    }

    private void PresetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int minutes))
        {
            CountdownMinutesBox.Value = minutes;
            CountdownSecondsBox.Value = 0;
            CurrentTimer.CountdownDuration = TimeSpan.FromMinutes(minutes);
            CurrentTimer.CountdownRemaining = CurrentTimer.CountdownDuration;
        }
    }

    private void StartStopButton_Click(object? sender, RoutedEventArgs e) => ToggleStartPause();
    private void LapButton_Click(object? sender, RoutedEventArgs e) => AddLap();
    private void ResetButton_Click(object? sender, RoutedEventArgs e) => ResetActiveTimer();
    private void ToggleOverlayButton_Click(object? sender, RoutedEventArgs e) => ToggleOverlayVisibility();
    private void NewTimerMenuItem_Click(object? sender, RoutedEventArgs e) => CreateNewTimer();
    private void NextTimerMenuItem_Click(object? sender, RoutedEventArgs e) => SelectNextTimer();
    private void ToggleCombinedOverlayMenuItem_Click(object? sender, RoutedEventArgs e) => ToggleCombinedOverlay();
    private void EditTimerMenuItem_Click(object? sender, RoutedEventArgs e) => OpenTimerEditor(CurrentTimer);
    private void CloseTimerMenuItem_Click(object? sender, RoutedEventArgs e) => CloseSession(CurrentTimer);

    private async void RenameTimerMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var session = CurrentTimer;
        var projectNames = _projectHistory.ProjectNames.ToList();
        var dialog = new TimerNameWindow(session.Name, projectNames);
        await dialog.ShowDialog(this);
        if (dialog.WasAccepted)
        {
            session.Name = dialog.TimerName;
            if (_overlays.TryGetValue(session.Id, out var overlay))
            {
                overlay.SetTimerName(session.Name);
            }
            UpdateActiveTimerDisplay();
            RefreshTimerRail();
        }
    }

    private void DashboardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_dashboardWindow == null || !_dashboardWindow.IsVisible)
        {
            _dashboardWindow = new ProjectDashboardWindow();
            _dashboardWindow.Closed += (_, _) => _dashboardWindow = null;
            _dashboardWindow.Show();
        }
        else
        {
            _dashboardWindow.Activate();
        }
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow == null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.SettingsChanged += OnSettingsChanged;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void OnSettingsChanged(SettingsChangeKind change)
    {
        _settings = SettingsStore.Load();
        if (_herdOverlay != null)
        {
            ApplyOverlayStyle(_herdOverlay);
            _herdOverlay.SetClickThrough(_settings.ClickThrough);
            _herdOverlay.SetHideFromCapture(_settings.HideOverlayFromCapture);
        }
        foreach (var overlay in _overlays.Values)
        {
            ApplyOverlayStyle(overlay);
            overlay.SetClickThrough(_settings.ClickThrough);
            overlay.SetHideFromCapture(_settings.HideOverlayFromCapture);
        }
        UpdateOverlayStates();
        UpdateLightRing();
    }

    private async void ShortcutsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_shortcutsWindow == null || !_shortcutsWindow.IsVisible)
        {
            _shortcutsWindow = new ShortcutsWindow(_settings.Shortcuts);
            await _shortcutsWindow.ShowDialog(this);
            if (_shortcutsWindow.DialogResult == true)
            {
                _settings.Shortcuts[ShortcutAction.CommandLeader] = _shortcutsWindow.ResultLeader;
                _settings.Shortcuts[ShortcutAction.NoteCommandLeader] = _shortcutsWindow.ResultNoteLeader;
                _settings.Shortcuts[ShortcutAction.ShowActiveOverlay] = _shortcutsWindow.ResultShowActiveOverlay;
                _settings.Shortcuts[ShortcutAction.OpenController] = _shortcutsWindow.ResultOpenController;
                _settings.CommandChainingTimeoutSeconds = _shortcutsWindow.ResultChainingTimeoutSeconds;
                SettingsStore.Save(_settings);
                InitializePlatformServices();
            }
            _shortcutsWindow = null;
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
            return;

        string action = CloseActionChoice.Normalize(_settings.CloseAction);
        if (action == CloseActionChoice.Ask)
        {
            e.Cancel = true;
            var (choice, remember) = await CloseActionDialogWindow.ShowAsync(this);
            if (choice == CloseDialogResult.Cancel)
                return;

            if (remember)
            {
                _settings.CloseAction = choice == CloseDialogResult.MinimizeToTray ? CloseActionChoice.Minimize : CloseActionChoice.Exit;
                SettingsStore.Save(_settings);
            }

            if (choice == CloseDialogResult.MinimizeToTray)
            {
                Hide();
            }
            else
            {
                _isExiting = true;
                Close();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }
        else if (action == CloseActionChoice.Minimize)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _isExiting = true;
            _herdOverlay?.Close();
            _herdOverlay = null;
            foreach (var overlay in _overlays.Values)
                overlay.Close();
            _overlays.Clear();
            _lightRingWindow?.Close();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _saveTimer.Stop();
        _idleTimer.Stop();
        _commandModeTimer?.Stop();
        PlatformServices.HotKey.HotKeyPressed -= OnHotKeyPressed;
        PlatformServices.HotKey.CommandKeyPressed -= OnCommandKeyPressed;
        ExitShortcutCommandMode();
        _herdOverlay?.Close();
        _herdOverlay = null;
        foreach (var overlay in _overlays.Values)
            overlay.Close();
        _overlays.Clear();
        base.OnClosed(e);
    }
}
