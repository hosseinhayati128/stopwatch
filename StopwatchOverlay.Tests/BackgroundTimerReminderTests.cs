using System;
using Xunit;

namespace StopwatchOverlay.Tests;

public class BackgroundTimerReminderTests
{
    [Fact]
    public void DisabledService_DoesNotShowReminders()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = false,
            Interval = TimeSpan.FromMinutes(3)
        };

        var timer = new TimerSession(1) { IsRunning = true };
        DateTime now = DateTime.UtcNow;

        bool show = service.ShouldShowReminder(timer, Guid.NewGuid(), now, out var alert);
        Assert.False(show);
        Assert.Null(alert);

        // Advance 10 minutes
        show = service.ShouldShowReminder(timer, Guid.NewGuid(), now.AddMinutes(10), out alert);
        Assert.False(show);
        Assert.Null(alert);
    }

    [Fact]
    public void ForegroundActiveTimer_DoesNotShowReminder()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(3)
        };

        var timer = new TimerSession(1) { IsRunning = true };
        DateTime now = DateTime.UtcNow;

        // activeTimerId == timer.Id
        bool show = service.ShouldShowReminder(timer, timer.Id, now, out var alert);
        Assert.False(show);
        Assert.Null(alert);

        show = service.ShouldShowReminder(timer, timer.Id, now.AddMinutes(10), out alert);
        Assert.False(show);
        Assert.Null(alert);
    }

    [Fact]
    public void BackgroundTimer_TriggersReminderAfterInterval()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(3),
            PopupDuration = TimeSpan.FromSeconds(30),
            MaxRemindersBeforeStop = 3
        };

        var timer = new TimerSession(2) { Name = "Deep Work", IsRunning = true };
        Guid activeTimerId = Guid.NewGuid();
        DateTime start = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

        // First registration
        bool show1 = service.ShouldShowReminder(timer, activeTimerId, start, out var alert1);
        Assert.False(show1);
        Assert.Null(alert1);

        // At 2 minutes - not yet
        bool show2 = service.ShouldShowReminder(timer, activeTimerId, start.AddMinutes(2), out var alert2);
        Assert.False(show2);
        Assert.Null(alert2);

        // At 3 minutes - triggers reminder!
        bool show3 = service.ShouldShowReminder(timer, activeTimerId, start.AddMinutes(3), out var alert3);
        Assert.True(show3);
        Assert.NotNull(alert3);
        Assert.Equal(timer.Id, alert3!.TimerSessionId);
        Assert.Equal("Deep Work", alert3.TimerName);
        Assert.Equal(1, alert3.ReminderIndex);
        Assert.Equal(3, alert3.MaxRemindersBeforeStop);
        Assert.True(service.IsPopupOpen(timer.Id));
    }

    [Fact]
    public void UnacknowledgedPopupTimeout_IncrementsCount_AndAutoStopsOnMaxLimit()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(3),
            PopupDuration = TimeSpan.FromSeconds(30),
            MaxRemindersBeforeStop = 3
        };

        var timer = new TimerSession(3) { Name = "Background Task", IsRunning = true };
        Guid activeTimerId = Guid.NewGuid();
        DateTime t = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        // Register
        service.ShouldShowReminder(timer, activeTimerId, t, out _);

        // 1st reminder at 3 min
        t = t.AddMinutes(3);
        Assert.True(service.ShouldShowReminder(timer, activeTimerId, t, out var a1));
        Assert.Equal(1, a1!.ReminderIndex);

        // 1st timeout
        bool timedOut1 = service.RecordPopupTimedOut(timer.Id, out int unackCount1, out bool autoStop1);
        Assert.True(timedOut1);
        Assert.Equal(1, unackCount1);
        Assert.False(autoStop1);
        Assert.False(service.IsPopupOpen(timer.Id));

        // 2nd reminder at 6 min
        t = t.AddMinutes(3);
        Assert.True(service.ShouldShowReminder(timer, activeTimerId, t, out var a2));
        Assert.Equal(2, a2!.ReminderIndex);

        // 2nd timeout
        bool timedOut2 = service.RecordPopupTimedOut(timer.Id, out int unackCount2, out bool autoStop2);
        Assert.True(timedOut2);
        Assert.Equal(2, unackCount2);
        Assert.False(autoStop2);

        // 3rd reminder at 9 min
        t = t.AddMinutes(3);
        Assert.True(service.ShouldShowReminder(timer, activeTimerId, t, out var a3));
        Assert.Equal(3, a3!.ReminderIndex);

        // 3rd timeout -> should auto stop!
        bool timedOut3 = service.RecordPopupTimedOut(timer.Id, out int unackCount3, out bool autoStop3);
        Assert.True(timedOut3);
        Assert.Equal(3, unackCount3);
        Assert.True(autoStop3); // Auto-stop triggered!
    }

    [Fact]
    public void UserAcknowledgment_ResetsUnacknowledgedCount()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(3),
            MaxRemindersBeforeStop = 3
        };

        var timer = new TimerSession(4) { IsRunning = true };
        Guid activeTimerId = Guid.NewGuid();
        DateTime t = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);

        service.ShouldShowReminder(timer, activeTimerId, t, out _);

        // 1st reminder
        t = t.AddMinutes(3);
        service.ShouldShowReminder(timer, activeTimerId, t, out _);
        service.RecordPopupTimedOut(timer.Id, out int count1, out _);
        Assert.Equal(1, count1);

        // 2nd reminder
        t = t.AddMinutes(3);
        service.ShouldShowReminder(timer, activeTimerId, t, out _);

        // User clicks "Keep Running"
        service.RecordUserAcknowledged(timer.Id, t);
        Assert.Equal(0, service.GetUnacknowledgedCount(timer.Id));
        Assert.False(service.IsPopupOpen(timer.Id));

        // Next reminder will come after another 3 minutes
        t = t.AddMinutes(2);
        Assert.False(service.ShouldShowReminder(timer, activeTimerId, t, out _));

        t = t.AddMinutes(1);
        Assert.True(service.ShouldShowReminder(timer, activeTimerId, t, out var aNew));
        Assert.Equal(1, aNew!.ReminderIndex); // Starts again at 1, not 3!
    }

    [Fact]
    public void ActivatingTimer_ClearsBackgroundState()
    {
        var service = new BackgroundTimerReminderService
        {
            IsEnabled = true,
            Interval = TimeSpan.FromMinutes(3)
        };

        var timer = new TimerSession(5) { IsRunning = true };
        DateTime t = new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc);

        service.ShouldShowReminder(timer, Guid.NewGuid(), t, out _);
        t = t.AddMinutes(3);
        service.ShouldShowReminder(timer, Guid.NewGuid(), t, out _);
        service.RecordPopupTimedOut(timer.Id, out int count, out _);
        Assert.Equal(1, count);

        // User switches overlay to this timer (timer.Id == activeTimerId)
        bool show = service.ShouldShowReminder(timer, timer.Id, t, out _);
        Assert.False(show);
        Assert.Equal(0, service.GetUnacknowledgedCount(timer.Id));
    }
}
