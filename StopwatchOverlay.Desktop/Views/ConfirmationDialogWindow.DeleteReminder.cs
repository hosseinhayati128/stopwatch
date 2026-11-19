using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    private bool _deleteReminderConfigured, _deleteReminderCompleted;
    private double _deleteReminderHeight = 450;
    internal void ConfigureDeleteReminder(string reminder, DateTime? dueAt, AppSettings settings)
    {
        _deleteReminderConfigured = true;
        var localDue = dueAt?.Kind == DateTimeKind.Utc ? dueAt.Value.ToLocalTime() : dueAt;
        string schedule = localDue?.ToString("dd MMM yyyy · HH:mm", CultureInfo.InvariantCulture) ?? "No date or time set";
        Title = "Delete Reminder"; HeadingText.Text = "Delete this reminder?";
        MessageText.Text = reminder + "\n\n" + schedule + (localDue.HasValue ? " · Local time" : "")
            + "\n\nDeletes this reminder from your local notes. If linked, its Habitica task is deleted when sync runs. Existing Telegram reminders are kept. This cannot be undone.";
        ConfirmButton.Content = "Delete reminder"; CancelButton.Content = "Cancel";
        Closed += (_, _) => _deleteReminderCompleted = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Complete(false); } }, RoutingStrategies.Tunnel);
        if (AppThemeCatalog.Normalize(settings.ThemeMode) != AppThemeCatalog.Nika) return;

        FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { HeadingText, MessageText, CancelButton, ConfirmButton }) ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        TextBlock Text(string value, double size = 13, IBrush? color = null) => new()
        {
            Text = value, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = color ?? ink,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
        };
        var rule = Brush.Parse("#CDB784"); var cream = Brush.Parse("#F8EACA"); var red = Brush.Parse("#8D3F35"); var redRule = Brush.Parse("#B67A60");
        HeadingText.Classes.Clear(); HeadingText.Text = Title; HeadingText.FontSize = 28;
        HeadingText.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; HeadingText.FontWeight = FontWeight.Bold;
        HeadingText.Foreground = ink; HeadingText.HorizontalAlignment = HorizontalAlignment.Stretch; HeadingText.TextAlignment = TextAlignment.Left;
        var close = new Button
        {
            Name = "DeleteReminderCancel", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel deleting this reminder"); ToolTip.SetTip(close, "Cancel (Esc)"); close.Click += OnCancelClicked;
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { HeadingText, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, Text("Review the reminder before deleting.", 12, muted) } };
        var name = Text(reminder, 17); name.Name = "DeleteReminderText"; name.FontWeight = FontWeight.SemiBold; AutoTextDirection.Apply(name, reminder);
        name.Measure(new Size(424, double.PositiveInfinity));
        _deleteReminderHeight = name.DesiredSize.Height > 42 ? 520 : 450;
        var reading = new ScrollViewer
        {
            Name = "DeleteReminderTextScroll", Content = name, MaxHeight = 116, Margin = new(0, 5, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetName(reading, "Reminder text");
        var due = Text(schedule, 13); due.Name = "DeleteReminderSchedule"; due.FontWeight = FontWeight.SemiBold;
        var dueInfo = new StackPanel { Spacing = 2, Children = { due } };
        if (localDue.HasValue) dueInfo.Children.Add(Text("Local time", 11, muted));
        var scheduleRow = new Grid { ColumnDefinitions = new("100,*"), Children = { Text("Schedule", 12, muted), dueInfo } }; Grid.SetColumn(dueInfo, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = cream, BorderBrush = rule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                Text("REMINDER", 10, muted), reading,
                new Border { Margin = new(0, 12, 0, 0), Padding = new(0, 10, 0, 0), BorderBrush = rule, BorderThickness = new(0, 1, 0, 0), Child = scheduleRow }
            } }
        };
        MessageText.Text = "Deletes this reminder from your local notes.";
        MessageText.FontSize = 12; MessageText.FontFamily = ClockPickerPresentation.Body; MessageText.Foreground = ink; MessageText.Margin = new(0);
        var warning = Text("This cannot be undone.", 12, red); warning.Name = "DeleteReminderWarning"; warning.FontWeight = FontWeight.SemiBold;
        DesktopTypography.SetPreserveColor(warning, true);
        var explanation = new StackPanel { Spacing = 5, Margin = new(0, 14, 0, 0), Children =
        {
            MessageText, Text("If linked, its Habitica task is deleted when sync runs.", 12, muted),
            Text("Existing Telegram reminders are kept.", 12, muted), new Border { Margin = new(0, 4, 0, 0), Child = warning }
        } };
        var body = new ScrollViewer
        {
            Name = "DeleteReminderBody", Margin = new(0, 16, 0, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, explanation } }
        };
        foreach (var button in new[] { CancelButton, ConfirmButton })
        {
            bool safe = button == CancelButton; var color = safe ? cream : red;
            var label = Text(safe ? "Cancel" : "Delete reminder", 13, color); label.FontWeight = FontWeight.SemiBold;
            button.Classes.Clear(); button.MinWidth = safe ? 94 : 142; button.MinHeight = 36; button.Padding = new(12, 7); button.CornerRadius = new(4);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = safe ? ink : cream; button.Foreground = color; button.BorderBrush = safe ? gold : redRule; button.BorderThickness = new(1); button.Content = label;
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse(safe ? "#214655" : "#F0DCC0"); button.Resources["ButtonBackgroundPressed"] = Brush.Parse(safe ? "#081E2A" : "#E7C7AB");
            button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = color;
            button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = button.BorderBrush;
            DesktopTypography.SetPreserveColor(button, true); AutomationProperties.SetName(button, safe ? "Cancel deleting this reminder" : "Delete this reminder");
        }
        var hint = Text("Esc · cancel", 11, muted);
        var footerRow = new Grid { ColumnDefinitions = new("*,10,Auto,10,Auto"), Children = { hint, CancelButton, ConfirmButton } }; Grid.SetColumn(CancelButton, 2); Grid.SetColumn(ConfirmButton, 4);
        bool stacked = false;
        footerRow.SizeChanged += (_, _) =>
        {
            double required = hint.DesiredSize.Width + 20 + Math.Max(CancelButton.MinWidth, ((Control)CancelButton.Content!).DesiredSize.Width + 26)
                + Math.Max(ConfirmButton.MinWidth, ((Control)ConfirmButton.Content!).DesiredSize.Width + 26);
            bool narrow = footerRow.Bounds.Width < required; if (narrow == stacked) return; stacked = narrow;
            footerRow.ColumnDefinitions = new(narrow ? "*" : "*,10,Auto,10,Auto"); footerRow.RowDefinitions = new(narrow ? "Auto,Auto,Auto" : "*");
            Grid.SetColumn(CancelButton, narrow ? 0 : 2); Grid.SetColumn(ConfirmButton, narrow ? 0 : 4);
            Grid.SetRow(CancelButton, narrow ? 1 : 0); Grid.SetRow(ConfirmButton, narrow ? 2 : 0);
            CancelButton.Margin = ConfirmButton.Margin = narrow ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        };
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, body, footer } }; Grid.SetRow(body, 1); Grid.SetRow(footer, 2);
        // Keep the decorative heading from consuming the reading area on narrow screens.
        layout.SizeChanged += (_, _) => HeadingText.FontSize = layout.Bounds.Width < 300 ? 22 : 28;
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; MaxHeight = double.PositiveInfinity; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = 540 * scale; Height = _deleteReminderHeight * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitDeleteReminderToScreen(screen); else FitDeleteReminderContent(); };
        SizeChanged += (_, _) => FitDeleteReminderContent();
    }
    internal void FitDeleteReminderToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea; double width = Math.Ceiling(Width * screen.Scaling), height = Math.Ceiling(Height * screen.Scaling);
        double centerX = area.X + area.Width / 2d, centerY = area.Y + area.Height / 2d;
        if (Owner is Window owner)
        {
            var size = owner.FrameSize ?? owner.ClientSize;
            centerX = owner.Position.X + size.Width * owner.RenderScaling / 2; centerY = owner.Position.Y + size.Height * owner.RenderScaling / 2;
        }
        Position = new PixelPoint((int)Math.Clamp(centerX - width / 2, area.X, area.Right - width), (int)Math.Clamp(centerY - height / 2, area.Y, area.Bottom - height));
        FitDeleteReminderContent();
    }
    internal void FitDeleteReminderContent(bool resizeWindow = false)
    {
        if (!_deleteReminderConfigured || DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = 540 * scale; Height = _deleteReminderHeight * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitDeleteReminderToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
