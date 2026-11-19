using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class PeriodicReviewPromptWindow
{
    private bool _reviewAppearance;
    private static readonly IBrush ReviewRule = Brush.Parse("#CDB784"), ReviewCream = Brush.Parse("#F8EACA"), ReviewTeal = Brush.Parse("#147A83");
    private static TextBlock ReviewText(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = brush ?? ClockPickerPresentation.Navy,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    private void ApplyReviewAppearance(DateTime startUtc, DateTime endUtc, int durationMinutes, AppSettings settings)
    {
        if (AppThemeCatalog.Normalize(settings.ThemeMode) != AppThemeCatalog.Nika) return;
        _reviewAppearance = true; FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { ReviewNowButton, SkipButton, PeriodSummaryText, SkipSubText, CountdownText, FooterHintText })
            ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        var title = ReviewText("Activity Review", 28); title.Name = "ReviewPromptHeading";
        title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; title.FontWeight = FontWeight.Bold;
        var close = new Button
        {
            Name = "ReviewPromptClose", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent,
            BorderThickness = new(0), VerticalAlignment = VerticalAlignment.Top,
            Content = new Avalonia.Controls.Shapes.Path
            {
                Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7
            }
        };
        AutomationProperties.SetName(close, $"Close; remind again in {_snoozeMinutes} minutes");
        ToolTip.SetTip(close, $"Close and remind again in {_snoozeMinutes} min"); close.Click += (_, _) => Close();
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { title, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, ReviewText("A quick look at your recent time.", 12, muted) } };
        DateTime start = startUtc.ToLocalTime(), end = endUtc.ToLocalTime(), today = DateTime.Today;
        string DateLabel(DateTime value) => value.Date == today ? "TODAY" : value.Date == today.AddDays(-1) ? "YESTERDAY" : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var date = ReviewText(start.Date == end.Date ? DateLabel(start) : $"{DateLabel(start)} → {DateLabel(end)}", 10, muted); date.Name = "ReviewPromptDate";
        PeriodSummaryText.Text = $"{start:HH:mm} – {end:HH:mm}"; PeriodSummaryText.FontSize = 21; PeriodSummaryText.FontWeight = FontWeight.SemiBold;
        PeriodSummaryText.FontFamily = ClockPickerPresentation.Body; PeriodSummaryText.Foreground = ink; PeriodSummaryText.Margin = new(0); PeriodSummaryText.TextWrapping = TextWrapping.Wrap;
        var period = new StackPanel { Spacing = 3, Children = { date, PeriodSummaryText } };
        var duration = ReviewText($"{durationMinutes} min", 23, ReviewTeal);
        duration.Name = "ReviewPromptDuration"; duration.FontWeight = FontWeight.SemiBold; duration.HorizontalAlignment = HorizontalAlignment.Right;
        var summaryRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = { period, duration } }; Grid.SetColumn(duration, 2);
        var summary = new Border
        {
            Padding = new(12, 10), Margin = new(0, 0, 0, 12), CornerRadius = new(5), BorderBrush = ReviewRule,
            BorderThickness = new(1), Background = Brush.Parse("#26FFF3D8"), Child = summaryRow
        };
        var choices = new StackPanel { Spacing = 6 };
        foreach (var button in new[] { ReviewNowButton, SkipButton })
        {
            bool review = button == ReviewNowButton; var foreground = review ? ReviewCream : ink;
            var key = ReviewText(review ? "Y" : "Esc", review ? 14 : 11, foreground); key.FontWeight = FontWeight.SemiBold; key.HorizontalAlignment = HorizontalAlignment.Center;
            var badge = new Border
            {
                MinWidth = 32, MinHeight = 28, Padding = new(4, 2), CornerRadius = new(4), BorderBrush = review ? gold : ReviewRule, BorderThickness = new(1),
                Background = review ? Brush.Parse("#284654") : Brush.Parse("#F1DFB7"), VerticalAlignment = VerticalAlignment.Center, Child = key
            };
            var icon = new Avalonia.Controls.Shapes.Path
            {
                Width = 20, Height = 22, Stretch = Stretch.Uniform, Stroke = review ? ReviewCream : ReviewTeal, StrokeThickness = 1.5,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Data = Geometry.Parse(review ? "M3,2 L16,2 L21,7 L21,23 L3,23 Z M16,2 L16,7 L21,7 M7,14 L10,17 L17,10" : "M3,3 L13,12 L3,21 Z M19,3 L19,21")
            };
            var label = ReviewText(review ? "Review now" : "Skip this review", 15, foreground); label.FontWeight = FontWeight.SemiBold;
            var description = review ? ReviewText("Open Activity & Time Review", 11, Brush.Parse("#DACDAF")) : SkipSubText;
            if (!review)
            {
                description.Text = $"Next reminder in {_intervalMinutes} min"; description.FontSize = 11;
                description.FontFamily = ClockPickerPresentation.Body; description.Foreground = muted; description.TextWrapping = TextWrapping.Wrap;
            }
            var words = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { label, description } };
            var row = new Grid { ColumnDefinitions = new("Auto,12,20,12,*"), Children = { badge, icon, words } }; Grid.SetColumn(icon, 2); Grid.SetColumn(words, 4);
            button.Classes.Remove("choice"); button.MinHeight = 62; button.Padding = new(12, 9); button.Margin = new(0); button.CornerRadius = new(5);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = review ? ink : ReviewCream; button.Foreground = foreground; button.BorderBrush = review ? gold : ReviewRule; button.BorderThickness = new(1); button.Content = row;
            if (review)
            {
                button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655"); button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
                button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = ReviewCream;
                button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = gold;
            }
            DesktopTypography.SetPreserveColor(button, true);
            AutomationProperties.SetName(button, review ? "Y. Review now" : $"Escape. Skip this review; next reminder in {_intervalMinutes} minutes"); choices.Children.Add(button);
        }
        var scroll = new ScrollViewer
        {
            Name = "ReviewPromptBody", Margin = new(0, 12, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, choices } }
        };
        CountdownText.FontSize = 12; CountdownText.FontWeight = FontWeight.SemiBold; CountdownText.FontFamily = ClockPickerPresentation.Body;
        CountdownText.Foreground = ink; CountdownText.HorizontalAlignment = HorizontalAlignment.Stretch; CountdownText.TextWrapping = TextWrapping.Wrap; UpdateReviewCountdown();
        FooterHintText.Text = $"If ignored or closed: remind again in {_snoozeMinutes} min"; FooterHintText.FontSize = 11;
        FooterHintText.FontFamily = ClockPickerPresentation.Body; FooterHintText.Foreground = muted; FooterHintText.HorizontalAlignment = HorizontalAlignment.Stretch; FooterHintText.TextWrapping = TextWrapping.Wrap;
        var footer = new StackPanel { Spacing = 4, Margin = new(0, 12, 0, 0), Children = { CountdownText, FooterHintText } };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, scroll, footer } }; Grid.SetRow(scroll, 1); Grid.SetRow(footer, 2);
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; SizeToContent = SizeToContent.Manual; Width = 480 * scale; Height = 396 * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitReviewPromptToScreen(screen); else FitReviewPromptContent(); };
        SizeChanged += (_, _) => FitReviewPromptContent();
    }
    private void UpdateReviewCountdown() => CountdownText.Text = _reviewAppearance ? $"Closes in {_remainingSeconds}s" : $"Closing in {_remainingSeconds}s (snoozes for {_snoozeMinutes}m)";
    internal void FitReviewPromptToScreen(Screen screen)
    {
        if (!_reviewAppearance) return;
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea;
        Position = new PixelPoint(area.X + (area.Width - (int)Math.Ceiling(Width * screen.Scaling)) / 2,
            area.Y + (area.Height - (int)Math.Ceiling(Height * screen.Scaling)) / 2);
        FitReviewPromptContent();
    }
    internal void FitReviewPromptContent(bool resizeWindow = false)
    {
        if (DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            // Keep the compact logical size when page zoom changes, then cap it to the screen.
            Width = 480 * scale; Height = 396 * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitReviewPromptToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
