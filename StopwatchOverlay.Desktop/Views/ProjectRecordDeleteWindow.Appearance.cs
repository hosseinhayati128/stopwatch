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

public partial class ProjectRecordDeleteWindow
{
    private static readonly IBrush DeleteRule = Brush.Parse("#CDB784"), DeleteCream = Brush.Parse("#F8EACA"),
        DeleteRed = Brush.Parse("#8D3F35"), DeleteRedRule = Brush.Parse("#B67A60");
    private static TextBlock DeleteText(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = brush ?? ClockPickerPresentation.Navy,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    private void ApplyDeleteAppearance(AppSettings settings, DateTime start, DateTime end, TimeSpan duration)
    {
        FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { HeadingText, ProjectText, DateText, TimeText, DurationText, CancelButton, DeleteButton })
            ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        Title = "Delete Time Record";
        HeadingText.Classes.Clear(); HeadingText.Text = Title; HeadingText.FontSize = 28; HeadingText.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont;
        HeadingText.FontWeight = FontWeight.Bold; HeadingText.Foreground = ink; HeadingText.HorizontalAlignment = HorizontalAlignment.Stretch;
        HeadingText.TextAlignment = TextAlignment.Left; HeadingText.TextWrapping = TextWrapping.Wrap;
        var close = new Button
        {
            Name = "DeleteRecordCancel", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel deleting this record"); ToolTip.SetTip(close, "Cancel (Esc)"); close.Click += Cancel_Click;
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { HeadingText, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, DeleteText("Review the saved session before deleting.", 12, muted) } };
        foreach (var text in new[] { ProjectText, DateText, TimeText, DurationText })
        {
            text.Classes.Clear(); text.Margin = new(0); text.FontFamily = ClockPickerPresentation.Body;
            text.Foreground = ink; text.VerticalAlignment = VerticalAlignment.Center; text.TextWrapping = TextWrapping.Wrap;
        }
        ProjectText.FontSize = 19; ProjectText.FontWeight = FontWeight.SemiBold; AutoTextDirection.Apply(ProjectText, ProjectText.Text);
        StackPanel Endpoint(string label, DateTime value, TextBlock date, TextBlock time)
        {
            date.Text = value.ToString("dd MMM yyyy", CultureInfo.CurrentCulture); date.FontSize = 13;
            time.Text = value.ToString("HH:mm", CultureInfo.CurrentCulture); time.FontSize = 20; time.FontWeight = FontWeight.SemiBold;
            return new StackPanel { Spacing = 2, Children = { DeleteText(label, 10, muted), date, time } };
        }
        var endDate = DeleteText(""); endDate.Name = "DeleteRecordEndDate";
        var endTime = DeleteText(""); endTime.Name = "DeleteRecordEndTime";
        var endpoints = new Grid { ColumnDefinitions = new("*,20,*"), Children = { Endpoint("START", start, DateText, TimeText), Endpoint("END", end, endDate, endTime) } }; Grid.SetColumn(endpoints.Children[1], 2);
        DurationText.Text = duration.TotalHours >= 1 ? $"{(long)duration.TotalHours} h {duration.Minutes} min" : duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes} min" : "<1 min";
        DurationText.FontSize = 13; DurationText.FontWeight = FontWeight.SemiBold;
        var durationRow = new Grid { ColumnDefinitions = new("*,Auto"), Children = { DeleteText("Duration", 12, muted), DurationText } }; Grid.SetColumn(DurationText, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = DeleteCream, BorderBrush = DeleteRule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                DeleteText("PROJECT", 10, muted), new Border { Margin = new(0, 4, 0, 0), Child = ProjectText },
                new Border { Margin = new(0, 10, 0, 10), Height = 1, Background = DeleteRule }, endpoints,
                new Border { Margin = new(0, 10, 0, 0), Padding = new(0, 8, 0, 0), BorderBrush = DeleteRule, BorderThickness = new(0, 1, 0, 0), Child = durationRow }
            } }
        };
        var warning = DeleteText("This cannot be undone.", 12, DeleteRed); warning.Name = "DeleteRecordWarning"; warning.FontWeight = FontWeight.SemiBold; DesktopTypography.SetPreserveColor(warning, true);
        var explanation = new StackPanel { Spacing = 4, Margin = new(0, 12, 0, 0), Children = { DeleteText("Permanently removes this session from your reports.", 12), warning } };
        var body = new ScrollViewer
        {
            Name = "DeleteRecordBody", Margin = new(0, 14, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, explanation } }
        };
        foreach (var button in new[] { CancelButton, DeleteButton })
        {
            bool cancel = button == CancelButton; var color = cancel ? DeleteCream : DeleteRed;
            var label = DeleteText(cancel ? "Cancel" : "Delete record", 13, color); label.FontWeight = FontWeight.SemiBold;
            button.Classes.Clear(); button.MinWidth = cancel ? 104 : 128; button.MinHeight = 36; button.Padding = new(12, 7); button.CornerRadius = new(4);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = cancel ? ink : DeleteCream; button.Foreground = color; button.BorderBrush = cancel ? gold : DeleteRedRule; button.BorderThickness = new(1); button.Content = label;
            button.Resources["ButtonBackgroundPointerOver"] = cancel ? Brush.Parse("#214655") : Brush.Parse("#F0DCC0");
            button.Resources["ButtonBackgroundPressed"] = cancel ? Brush.Parse("#081E2A") : Brush.Parse("#E7C7AB");
            button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = color;
            button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = button.BorderBrush;
            DesktopTypography.SetPreserveColor(button, true); AutomationProperties.SetName(button, cancel ? "Cancel deleting this record" : "Delete this time record");
        }
        var footerRow = new Grid { ColumnDefinitions = new("*,10,Auto,10,Auto"), Children = { DeleteText("Esc · cancel", 11, muted), CancelButton, DeleteButton } }; Grid.SetColumn(CancelButton, 2); Grid.SetColumn(DeleteButton, 4);
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = DeleteRule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, body, footer } }; Grid.SetRow(body, 1); Grid.SetRow(footer, 2);
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; MaxHeight = double.PositiveInfinity; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = 540 * scale; Height = 470 * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitDeleteRecordToScreen(screen); else FitDeleteRecordContent(); };
        SizeChanged += (_, _) => FitDeleteRecordContent();
    }
    internal void FitDeleteRecordToScreen(Screen screen)
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
        FitDeleteRecordContent();
    }
    internal void FitDeleteRecordContent(bool resizeWindow = false)
    {
        if (DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = 540 * scale; Height = 470 * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitDeleteRecordToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
