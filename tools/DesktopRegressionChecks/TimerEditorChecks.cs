using Avalonia.Controls;
using Avalonia.Interactivity;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class TimerEditorChecks
{
    internal static void HiddenOwner()
    {
        foreach (bool ownerVisible in new[] { false, true })
        foreach (bool save in new[] { false, true })
        {
            var owner = new Window { ShowActivated = false };
            var timer = new TimerSession(1);
            timer.RestoreElapsed(TimeSpan.FromMinutes(5), false);
            var editor = new TimerEditorWindow(timer, Array.Empty<string>(), Array.Empty<ProjectWorkIntervalView>());
            try
            {
                owner.Show(); if (!ownerVisible) owner.Hide();
                var finished = editor.ShowForOwnerAsync(owner);
                ClockOverlayChecks.Pump(150);
                if (!editor.IsVisible || owner.IsVisible != ownerVisible || finished.IsCompleted)
                    throw new Exception("Editing a tray timer did not open independently of the hidden controller.");
                if (ownerVisible && editor.Owner != owner)
                    throw new Exception("Visible controller lost modal ownership of its timer editor.");
                if (save)
                {
                    editor.FindControl<TextBox>("TimeValueBox")!.Text = "07:30";
                    ClockOverlayChecks.Pump(50);
                    editor.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else editor.Close();
                ClockOverlayChecks.Pump(100);
                if (!finished.IsCompletedSuccessfully || editor.WasSaved != save || timer.Elapsed != TimeSpan.FromMinutes(5))
                    throw new Exception("Closing the editor changed the timer directly or left the edit operation pending.");
                if (save && editor.NewTimeValue != TimeSpan.FromMinutes(7.5))
                    throw new Exception("Timer edit did not return the saved duration.");
            }
            finally { editor.Close(); owner.Close(); }
        }
    }
}
