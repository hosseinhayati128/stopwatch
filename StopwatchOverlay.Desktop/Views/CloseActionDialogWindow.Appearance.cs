using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class CloseActionDialogWindow
{
    private static readonly IBrush CloseRule = Brush.Parse("#CDB784"), CloseCream = Brush.Parse("#F8EACA"),
        CloseExitInk = Brush.Parse("#8D3F35"), CloseExitRule = Brush.Parse("#B67A60");
    private static TextBlock CloseText(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = brush ?? ClockPickerPresentation.Navy,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    private void ApplyCloseAppearance(AppSettings settings)
    {
        FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { HeadingText, MessageText, RememberCheck, MinimizeButton, ExitButton, CancelButton })
            ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        HeadingText.Classes.Clear(); HeadingText.FontSize = 28; HeadingText.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont;
        HeadingText.FontWeight = FontWeight.Bold; HeadingText.Foreground = ink; HeadingText.HorizontalAlignment = HorizontalAlignment.Stretch;
        HeadingText.TextAlignment = TextAlignment.Left; HeadingText.TextWrapping = TextWrapping.Wrap;
        var close = new Button
        {
            Name = "CloseDialogCancel", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel closing Stopwatch"); ToolTip.SetTip(close, "Cancel (Esc)"); close.Click += OnCancelClicked;
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { HeadingText, close } }; Grid.SetColumn(close, 1);
        MessageText.Text = "What would you like to do?"; MessageText.Margin = new(0); MessageText.FontSize = 12;
        MessageText.FontFamily = ClockPickerPresentation.Body; MessageText.Foreground = muted; MessageText.TextAlignment = TextAlignment.Left;
        var header = new StackPanel { Spacing = 3, Children = { titleRow, MessageText } };
        var choices = new StackPanel { Spacing = 8 };
        foreach (var button in new[] { MinimizeButton, ExitButton })
        {
            bool minimize = button == MinimizeButton; var foreground = minimize ? CloseCream : CloseExitInk;
            var icon = new Avalonia.Controls.Shapes.Path
            {
                Width = 22, Height = 22, Stretch = Stretch.Uniform, Stroke = foreground, StrokeThickness = 1.5,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Data = Geometry.Parse(minimize ? "M2,15 L2,22 L22,22 L22,15 M7,12 L12,17 L17,12 M12,1 L12,17" : "M12,1 L12,12 M6,5 A10,10 0 1 0 18,5")
            };
            var title = CloseText(minimize ? "Minimize to tray" : "Exit Stopwatch", 15, foreground); title.FontWeight = FontWeight.SemiBold;
            var description = CloseText(minimize ? "Timers, overlays and shortcuts stay active." : "Close the app, overlays and shortcuts.", 11,
                minimize ? Brush.Parse("#DACDAF") : muted);
            var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { title, description } };
            var row = new Grid { ColumnDefinitions = new(minimize ? "22,12,*,10,48" : "22,12,*"), Children = { icon, words } }; Grid.SetColumn(words, 2);
            if (minimize)
            {
                var key = CloseText("Enter", 10.5, CloseCream); key.FontWeight = FontWeight.SemiBold; key.HorizontalAlignment = HorizontalAlignment.Center;
                var badge = new Border { Name = "CloseEnterBadge", Width = 48, Height = 26, Background = Brush.Parse("#284654"), BorderBrush = gold,
                    BorderThickness = new(1), CornerRadius = new(4), VerticalAlignment = VerticalAlignment.Center, Child = key };
                Grid.SetColumn(badge, 4); row.Children.Add(badge);
            }
            button.Classes.Clear(); button.MinHeight = 72; button.Padding = new(12, 10); button.Margin = new(0); button.CornerRadius = new(5);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = minimize ? ink : CloseCream; button.Foreground = foreground; button.BorderBrush = minimize ? gold : CloseExitRule; button.BorderThickness = new(1); button.Content = row;
            if (minimize)
            {
                button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655"); button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
                button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = CloseCream;
                button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = gold;
            }
            DesktopTypography.SetPreserveColor(button, true);
            AutomationProperties.SetName(button, minimize ? "Minimize to tray. Timers, overlays and shortcuts stay active." : "Exit Stopwatch. Close the app, overlays and shortcuts.");
            choices.Children.Add(button);
        }
        var rememberLabel = CloseText("Remember my choice"); rememberLabel.Margin = new(8, 0, 0, 0);
        RememberCheck.Content = rememberLabel; RememberCheck.MinHeight = 28; RememberCheck.Padding = new(0); RememberCheck.Margin = new(0);
        RememberCheck.HorizontalAlignment = HorizontalAlignment.Left; RememberCheck.Foreground = ink;
        RememberCheck.Resources["CheckBoxCheckBackgroundFillChecked"] = ink; RememberCheck.Resources["CheckBoxCheckBorderBrushChecked"] = gold;
        RememberCheck.Resources["CheckBoxCheckGlyphForegroundChecked"] = CloseCream;
        AutomationProperties.SetName(RememberCheck, "Remember my choice for closing the controller");
        var rememberHint = CloseText("Ask each time I close the controller.", 11, muted); rememberHint.Name = "CloseRememberHint"; rememberHint.Margin = new(28, 0, 0, 0);
        RememberCheck.IsCheckedChanged += (_, _) => rememberHint.Text = RememberCheck.IsChecked == true ? "Next time, use the action I choose here." : "Ask each time I close the controller.";
        var settingsHint = CloseText("Change later in Settings → Application.", 11, muted); settingsHint.Margin = new(28, 0, 0, 0);
        var preference = new StackPanel { Spacing = 3, Margin = new(0, 14, 0, 0), Children = { RememberCheck, rememberHint, settingsHint } };
        var scroll = new ScrollViewer
        {
            Name = "CloseDialogBody", Margin = new(0, 14, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new StackPanel { Children = { choices, preference } }
        };
        var cancelLabel = CloseText("Cancel"); cancelLabel.FontWeight = FontWeight.SemiBold; cancelLabel.HorizontalAlignment = HorizontalAlignment.Center;
        CancelButton.Classes.Clear(); CancelButton.Width = 92; CancelButton.MinHeight = 34; CancelButton.Padding = new(12, 5); CancelButton.CornerRadius = new(4);
        CancelButton.Background = CloseCream; CancelButton.Foreground = ink; CancelButton.BorderBrush = gold; CancelButton.BorderThickness = new(1);
        CancelButton.HorizontalContentAlignment = HorizontalAlignment.Center; CancelButton.VerticalContentAlignment = VerticalAlignment.Center; CancelButton.Content = cancelLabel;
        AutomationProperties.SetName(CancelButton, "Cancel closing Stopwatch");
        var hint = CloseText("Esc · keep Stopwatch open", 11, muted); hint.Name = "CloseFooterHint";
        var footerRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = { hint, CancelButton } }; Grid.SetColumn(CancelButton, 2);
        var footer = new Border { Margin = new(0, 10, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = CloseRule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, scroll, footer } }; Grid.SetRow(scroll, 1); Grid.SetRow(footer, 2);
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = 0; MinHeight = 0; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = 500 * scale; Height = 428 * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitCloseDialogToScreen(screen); else FitCloseDialogContent(); };
        SizeChanged += (_, _) => FitCloseDialogContent();
    }
    internal void FitCloseDialogToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea;
        double width = Math.Ceiling(Width * screen.Scaling), height = Math.Ceiling(Height * screen.Scaling);
        double centerX = area.X + area.Width / 2d, centerY = area.Y + area.Height / 2d;
        if (Owner is Window owner)
        {
            var ownerSize = owner.FrameSize ?? owner.ClientSize;
            centerX = owner.Position.X + ownerSize.Width * owner.RenderScaling / 2;
            centerY = owner.Position.Y + ownerSize.Height * owner.RenderScaling / 2;
        }
        Position = new PixelPoint((int)Math.Clamp(centerX - width / 2, area.X, area.Right - width), (int)Math.Clamp(centerY - height / 2, area.Y, area.Bottom - height));
        FitCloseDialogContent();
    }
    internal void FitCloseDialogContent(bool resizeWindow = false)
    {
        if (DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = 500 * scale; Height = 428 * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitCloseDialogToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
