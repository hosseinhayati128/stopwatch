using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private Grid? _activityWatchReviewCards;
    private TextBlock? _reviewAutoHint, _reviewDurationHelp, _reviewPercentHelp, _reviewPeriod,
        _reviewClosing, _reviewOutcome, _reviewAvailability, _reviewShortcut;
    private TextBlock[]? _reviewErrors;
    private Button? _reviewExampleAccept, _reviewExampleSkip;
    private bool? _reviewExampleAccepted;

    private void BuildActivityWatchReview()
    {
        _reviewExampleAccepted = null;
        ObsidianChoice(PeriodicReviewEnabledCheck, "Show review prompts", "");
        var choice = (StackPanel)PeriodicReviewEnabledCheck.Content!; choice.Spacing = 3;
        ((TextBlock)choice.Children[0]).FontSize = 13;
        _reviewAutoHint = (TextBlock)choice.Children[1]; _reviewAutoHint.FontSize = 11;
        _reviewErrors = new TextBlock[5];
        for (int i = 0; i < _reviewErrors.Length; i++) _reviewErrors[i] = SettingsLabel("", 11, Brush.Parse("#9B3C24"));
        _reviewDurationHelp = SettingsLabel("", 11); _reviewPercentHelp = SettingsLabel("", 11);
        var timing = TelegramCard("Prompt timing", PeriodicReviewEnabledCheck,
            ReviewNumberRow("Review every", SettingsLabel("", 11), PeriodicReviewIntervalTextBox, "min", _reviewErrors[0]),
            ReviewNumberRow("If ignored, remind after", SettingsLabel("When the prompt closes untouched.", 11), PeriodicReviewSnoozeTextBox, "min", _reviewErrors[1]),
            ReviewNumberRow("Keep prompt visible for", SettingsLabel("Then snooze if there is no response.", 11), PeriodicReviewAutoDismissTextBox, "sec", _reviewErrors[2]));
        var detail = TelegramCard("Activity detail",
            ReviewNumberRow("Short activity threshold", _reviewDurationHelp, PeriodicReviewMinDurationTextBox, "sec", _reviewErrors[3]),
            ReviewNumberRow("Group activities below", _reviewPercentHelp, PeriodicReviewMinPercentTextBox, "%", _reviewErrors[4]));
        _reviewPeriod = SettingsLabel("", 12, SettingsInk); _reviewClosing = SettingsLabel("", 11);
        _reviewExampleAccept = SettingsAction("Y  Review", "ReviewExampleAccept");
        _reviewExampleSkip = SettingsAction("Esc  Skip", "ReviewExampleSkip");
        _reviewExampleAccept.Click += (_, _) => { _reviewExampleAccepted = true; RefreshActivityWatchReview(); };
        _reviewExampleSkip.Click += (_, _) => { _reviewExampleAccepted = false; RefreshActivityWatchReview(); };
        AutomationProperties.SetName(_reviewExampleAccept, "Example: press Y to review");
        AutomationProperties.SetName(_reviewExampleSkip, "Example: press Escape to skip");
        foreach (var button in new[] { _reviewExampleAccept, _reviewExampleSkip, ReviewNowButton })
        {
            NavigatorFormStyles.StyleButton(button, button != _reviewExampleSkip);
            button.Height = double.NaN; button.MinHeight = 36; button.Padding = new(12, 6);
            button.HorizontalAlignment = HorizontalAlignment.Left;
        }
        _reviewExampleAccept.Margin = _reviewExampleSkip.Margin = new(0, 0, 8, 0);
        ReviewNowButton.Content = "Review now";
        var prompt = new Border { Background = Brush.Parse("#80FFF8E8"), BorderBrush = Brush.Parse("#509A7B48"),
            BorderThickness = new(1), CornerRadius = new(5), Padding = new(12), Child = new StackPanel { Spacing = 8,
            Children = { SettingsLabel("Time for a review", 17, SettingsInk), _reviewPeriod,
                new WrapPanel { Children = { _reviewExampleAccept, _reviewExampleSkip } }, _reviewClosing } } };
        _reviewOutcome = SettingsLabel("", 12, Brush.Parse("#2D7169")); _reviewOutcome.Name = "ReviewExampleOutcome";
        var example = TelegramCard("Prompt example", SettingsLabel("EXAMPLE ONLY", 9), prompt, _reviewOutcome,
            SettingsLabel("Skipping or ignoring a prompt does not delete recorded time.", 11));
        _reviewAvailability = SettingsLabel("", 11);
        _reviewShortcut = SettingsLabel("", 11); _reviewShortcut.Name = "ReviewShortcutHint";
        var onDemand = TelegramCard("Review on demand", SettingsLabel("Review now also works when automatic prompts are off.", 12),
            ReviewNowButton, _reviewShortcut, _reviewAvailability);
        foreach (var card in new[] { timing, detail, example, onDemand }) ((StackPanel)card.Child!).Spacing = 9;
        _activityWatchReviewCards = new Grid { Name = "ActivityWatchReviewCards", Children = {
            new StackPanel { Spacing = 14, Children = { timing, detail } }, new StackPanel { Spacing = 14, Children = { example, onDemand } } } };
    }

    private static StackPanel ReviewNumberRow(string title, TextBlock detail, TextBox field, string unit, TextBlock error)
    {
        detail.IsVisible = !string.IsNullOrEmpty(detail.Text);
        field.Classes.Add("editorField"); field.Height = double.NaN; field.Padding = new(10, 6);
        AutomationProperties.SetName(field, title + " (" + unit + ")");
        var label = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center,
            Children = { SettingsLabel(title, 13, SettingsInk), detail } };
        var row = new Grid { ColumnDefinitions = new("*,10,72,6,Auto"), Children = { label, field, SettingsLabel(unit, 12, SettingsInk) } };
        Grid.SetColumn(field, 2); Grid.SetColumn(row.Children[2], 4);
        field.VerticalAlignment = row.Children[2].VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel { Spacing = 4, Children = { row, error } };
    }

    private static bool ReviewInteger(TextBox field, int low, int high, out int value)
        => int.TryParse(field.Text?.Trim(), out value) && value >= low && value <= high;

    private bool ReviewPercent(out double value) => double.TryParse(PeriodicReviewMinPercentTextBox.Text?.Trim(),
        NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value) && value is >= 0 and <= 50;

    private bool ReviewDetailValid() => ReviewInteger(PeriodicReviewMinDurationTextBox, 0, 3600, out _) && ReviewPercent(out _);

    private void RefreshActivityWatchReview()
    {
        bool automatic = PeriodicReviewEnabledCheck.IsChecked == true;
        PeriodicReviewIntervalTextBox.IsEnabled = PeriodicReviewSnoozeTextBox.IsEnabled = PeriodicReviewAutoDismissTextBox.IsEnabled = automatic;
        ReviewNowButton.IsEnabled = ReviewDetailValid();
        if (_activityWatchReviewCards == null) return;
        _reviewAutoHint!.Text = automatic ? "While Stopwatch is open." : "Off by default · manual review still works.";
        _reviewAutoHint.Foreground = automatic ? Brush.Parse("#2D7169") : SettingsMuted;
        bool[] valid = { ReviewInteger(PeriodicReviewIntervalTextBox, 1, 1440, out int every),
            ReviewInteger(PeriodicReviewSnoozeTextBox, 1, 1440, out int snooze), ReviewInteger(PeriodicReviewAutoDismissTextBox, 1, 120, out int visible),
            ReviewInteger(PeriodicReviewMinDurationTextBox, 0, 3600, out int duration), ReviewPercent(out double percent) };
        string[] messages = { "Enter a whole number from 1 to 1,440 minutes.", "Enter a whole number from 1 to 1,440 minutes.",
            "Enter a whole number from 1 to 120 seconds.", "Enter a whole number from 0 to 3,600 seconds.", "Enter a number from 0 to 50%." };
        for (int i = 0; i < valid.Length; i++) { _reviewErrors![i].Text = messages[i]; _reviewErrors[i].IsVisible = !valid[i] && (i > 2 || automatic); }
        _reviewDurationHelp!.IsVisible = _reviewPercentHelp!.IsVisible = true;
        _reviewDurationHelp.Text = valid[3] && duration == 0 ? "0 uses the smallest filter: 1 second." : "Rejoin matching work across quick switches and hide short activity.";
        _reviewPercentHelp.Text = valid[4] && percent == 0 ? "Show each activity separately." : "Combine smaller entries into Others.";
        bool timing = valid[0] && valid[1] && valid[2];
        _reviewExampleAccept!.IsEnabled = _reviewExampleSkip!.IsEnabled = timing;
        _reviewPeriod!.Text = timing ? $"14:00 – {new TimeOnly(14, 0).AddMinutes(every):HH:mm} · {every} minutes" : "Correct the timing values to update this example.";
        _reviewClosing!.Text = timing ? $"Visible for {visible} sec · no response snoozes {snooze} min." : "Enter valid timing values to preview the prompt.";
        _reviewOutcome!.Text = _reviewExampleAccepted switch { true => "Y → open Activity & Time Review", false => $"Esc → prompt again in {every} min",
            _ => automatic ? "Y reviews · Esc waits for the next interval" : "Automatic prompts are off" };
        _reviewAvailability!.Text = _settings.ActivityWatchEnabled ? "ActivityWatch detail is included when available." : "ActivityWatch is off. Reviews can still use stopwatch time.";
        _reviewShortcut!.Text = _settings.CommandModeKeys.TryGetValue(ShortcutAction.PeriodicReview, out uint key) && key != 0
            ? $"Shortcut: {(_settings.LeaderShortcut ?? AppSettings.DefaultLeaderShortcut()).Format().Replace("+", " + ")}, then {Shortcut.FormatKeyName(key)}"
            : "Review shortcut is not assigned.";
    }
}
