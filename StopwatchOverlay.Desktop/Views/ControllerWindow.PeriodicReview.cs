using System;
using System.Threading;
using Avalonia.Threading;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private PeriodicReviewService? _periodicReviewService;
    private PeriodicReviewPromptWindow? _periodicReviewPrompt;
    private Action<DateTime, DateTime>? _periodicReviewHandler;
    private int _periodicReviewGeneration;

    private void StartPeriodicReview()
    {
        if (!_previewMode && !_isExiting && _periodicReviewService == null) AttachPeriodicReviewService(new(() => _settings));
    }

    private void AttachPeriodicReviewService(PeriodicReviewService service)
    {
        StopPeriodicReview();
        if (_isExiting) { service.Dispose(); return; }
        _periodicReviewService = service;
        _periodicReviewHandler = (start, end) =>
        {
            int generation = Volatile.Read(ref _periodicReviewGeneration);
            Dispatcher.UIThread.Post(() =>
            {
                if (_isExiting || generation != _periodicReviewGeneration || !ReferenceEquals(service, _periodicReviewService)
                    || !_settings.PeriodicReviewEnabled || !service.IsPromptShowing) return;
                ShowPeriodicReviewPrompt(start, end);
            });
        };
        service.PromptRequested += _periodicReviewHandler;
    }

    private void CancelPeriodicReviewPrompt()
    {
        var prompt = _periodicReviewPrompt; _periodicReviewPrompt = null;
        prompt?.Close();
    }

    private void RestartPeriodicReview()
    {
        // Invalidate old UI work before arming a timer that may already be due.
        Interlocked.Increment(ref _periodicReviewGeneration);
        CancelPeriodicReviewPrompt();
        _periodicReviewService?.RestartTimer(cancelPendingPrompt: _timerCommandDialog is not PeriodicReviewWindow);
    }

    private void StopPeriodicReview()
    {
        if (_periodicReviewService != null)
        {
            _periodicReviewService.PromptRequested -= _periodicReviewHandler;
            _periodicReviewService.Dispose(); _periodicReviewService = null;
        }
        _periodicReviewHandler = null; Interlocked.Increment(ref _periodicReviewGeneration);
        CancelPeriodicReviewPrompt();
    }

    private void ShowPeriodicReviewPrompt(DateTime start, DateTime end)
    {
        var service = _periodicReviewService!;
        if (_periodicReviewPrompt != null) return;
        if (_timerCommandDialog != null || _settingsWindow?.IsVisible == true || _behaviorFocusPrompt?.IsVisible == true)
        { service.OnPromptIgnored(); return; }
        PeriodicReviewPromptWindow? prompt = null;
        try
        {
            prompt = new(start, end, _settings.PeriodicReviewAutoDismissSeconds,
                _settings.PeriodicReviewSnoozeMinutes, _settings.PeriodicReviewIntervalMinutes, result =>
                {
                    if (!ReferenceEquals(prompt, _periodicReviewPrompt)) return;
                    _periodicReviewPrompt = null;
                    if (_isExiting || !ReferenceEquals(service, _periodicReviewService)) return;
                    if (result == PeriodicReviewPromptResult.Accepted) _ = OpenPeriodicReviewAsync(start, end);
                    else if (result == PeriodicReviewPromptResult.Skipped) service.OnPromptSkipped();
                    else service.OnPromptIgnored();
                }, settings: _settings);
            _periodicReviewPrompt = prompt;
            prompt.Show();
        }
        catch (Exception ex)
        {
            CancelPeriodicReviewPrompt(); service.OnPromptIgnored();
            CrashLogger.LogRecoverable(ex, "ControllerWindow.PeriodicReviewPrompt");
        }
    }
}
