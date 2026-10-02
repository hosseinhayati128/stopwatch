using System;
using System.IO;
using System.Security;
using System.Threading;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Platform.Windows;

public sealed class WindowsSingleInstanceService : ISingleInstanceService
{
    private const string SingleInstanceMutexName = @"Local\StopwatchOverlay.SingleInstance";
    private const string ShowExistingEventName = @"Local\StopwatchOverlay.ShowExisting";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showExistingEvent;
    private RegisteredWaitHandle? _showExistingRegistration;
    private bool _ownsSingleInstanceMutex;
    private Action? _onShowExistingRequested;

    public bool TryAcquireSingleInstance(Action onShowExistingRequested)
    {
        _onShowExistingRequested = onShowExistingRequested;

        try
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out _ownsSingleInstanceMutex);
        }
        catch (Exception exception) when (IsExpectedSingleInstanceBoundaryFailure(exception))
        {
            CrashLogger.LogRecoverable(exception, "SingleInstanceMutexCreate");
            _singleInstanceMutex = null;
            return true;
        }

        if (_singleInstanceMutex != null && !_ownsSingleInstanceMutex)
        {
            return false;
        }

        StartExistingInstanceListener();
        return true;
    }

    public void SignalExistingInstance()
    {
        try
        {
            using var showExistingEvent = EventWaitHandle.OpenExisting(ShowExistingEventName);
            showExistingEvent.Set();
        }
        catch (Exception exception) when (IsExpectedSingleInstanceBoundaryFailure(exception))
        {
            CrashLogger.LogRecoverable(exception, "SingleInstanceSignalExisting");
        }
    }

    private void StartExistingInstanceListener()
    {
        try
        {
            _showExistingEvent = new EventWaitHandle(
                initialState: false,
                EventResetMode.AutoReset,
                ShowExistingEventName);

            _showExistingRegistration = ThreadPool.RegisterWaitForSingleObject(
                _showExistingEvent,
                (_, _) => _onShowExistingRequested?.Invoke(),
                null,
                Timeout.InfiniteTimeSpan,
                executeOnlyOnce: false);
        }
        catch (Exception exception) when (IsExpectedSingleInstanceBoundaryFailure(exception))
        {
            CrashLogger.LogRecoverable(exception, "SingleInstanceListenerStart");
            _showExistingRegistration?.Unregister(null);
            _showExistingRegistration = null;
            _showExistingEvent?.Dispose();
            _showExistingEvent = null;
        }
    }

    private static bool IsExpectedSingleInstanceBoundaryFailure(Exception exception)
        => exception is UnauthorizedAccessException
            or WaitHandleCannotBeOpenedException
            or IOException
            or SecurityException;

    public void Dispose()
    {
        try
        {
            _showExistingRegistration?.Unregister(null);
            _showExistingRegistration = null;

            _showExistingEvent?.Dispose();
            _showExistingEvent = null;

            if (_singleInstanceMutex != null)
            {
                if (_ownsSingleInstanceMutex)
                {
                    try
                    {
                        _singleInstanceMutex.ReleaseMutex();
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
