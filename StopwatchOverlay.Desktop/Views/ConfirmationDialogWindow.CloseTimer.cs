using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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
    private bool _closeTimerConfigured, _closeTimerCompleted, _closeTimerFromEditor;
    private static readonly IBrush TimerRule = Brush.Parse("#CDB784"), TimerCream = Brush.Parse("#F8EACA"),
        TimerRed = Brush.Parse("#8D3F35"), TimerRedRule = Brush.Parse("#B67A60"), TimerTeal = Brush.Parse("#147A83");
    private static TextBlock TimerText(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = brush ?? ClockPickerPresentation.Navy,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    internal void ConfigureCloseTimer(TimerSession session, string displayTime, AppSettings settings, bool fromEditor = false)
    {
        _closeTimerConfigured = true;
        _closeTimerFromEditor = fromEditor;
        bool clock = session.Mode == 1;
        string mode = session.Mode switch { 2 => "countdown", 3 => "timecode", _ => "stopwatch" };
        string stateText = clock ? "Clock" : $"{(session.IsRunning ? "Running" : "Paused")} {mode}";
        string timeCaption = clock ? "Current time" : session.Mode == 2 ? "Remaining" : "Elapsed";
        string editorConsequence = session.Mode == 1 ? "Removes this clock from your list and closes the editor."
            : session.IsRunning ? "Stops this timer and closes the editor." : "Closes this timer and the editor.";
        if (fromEditor)
            MessageText.Text = $"{session.DisplayName}\n{stateText} · {timeCaption}: {displayTime}\n\n{editorConsequence}\nSaved project records stay in your history.\nUnsaved editor changes won’t be applied.";
        ConfirmButton.Content = "Close Timer";
        Closed += (_, _) => _closeTimerCompleted = true;
        Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (IsVisible) CancelButton.Focus(); });
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Complete(false); }
        }, RoutingStrategies.Tunnel);
        if (AppThemeCatalog.Normalize(settings.ThemeMode) != AppThemeCatalog.Nika) return;

        FontFamily = ClockPickerPresentation.Body; Resources["AppFontFamily"] = ClockPickerPresentation.Body;
        foreach (var control in new Control[] { HeadingText, MessageText, CancelButton, ConfirmButton }) ((Panel)control.Parent!).Children.Remove(control);
        Content = null;
        var ink = ClockPickerPresentation.Navy; var muted = ClockPickerPresentation.Muted; var gold = ClockPickerPresentation.Gold;
        HeadingText.Classes.Clear(); HeadingText.Text = "Close Timer"; HeadingText.FontSize = 28;
        HeadingText.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; HeadingText.FontWeight = FontWeight.Bold;
        HeadingText.Foreground = ink; HeadingText.HorizontalAlignment = HorizontalAlignment.Stretch; HeadingText.TextAlignment = TextAlignment.Left;
        var close = new Button
        {
            Name = "CloseTimerCancel", Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel closing this timer"); ToolTip.SetTip(close, "Cancel (Esc)"); close.Click += OnCancelClicked;
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { HeadingText, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, TimerText(fromEditor ? "Close this timer from the editor?" : "Close the selected timer?", 12, muted) } };
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 22, Height = 26, Stretch = Stretch.Uniform, Stroke = TimerTeal, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center,
            Data = Geometry.Parse(clock ? "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M12,6 L12,12 L16,14"
                : "M8,1 L16,1 M12,1 L12,5 M19,5 L22,2 M12,6 A10,10 0 1 0 12,26 A10,10 0 1 0 12,6 M12,10 L12,16 L16,18")
        };
        var name = TimerText(session.DisplayName, 18); name.Name = "CloseTimerName"; name.FontWeight = FontWeight.SemiBold; AutoTextDirection.Apply(name, name.Text);
        var state = TimerText(stateText, 11, clock || session.IsRunning ? TimerTeal : muted); state.Name = "CloseTimerState";
        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { name, state } };
        var time = TimerText(displayTime, 24); time.Name = "CloseTimerTime"; time.FontWeight = FontWeight.SemiBold; time.HorizontalAlignment = HorizontalAlignment.Right;
        var caption = TimerText(timeCaption, 10, muted); caption.Name = "CloseTimerTimeCaption"; caption.HorizontalAlignment = HorizontalAlignment.Right;
        var reading = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { time, caption } };
        var summaryRow = new Grid { ColumnDefinitions = new("22,12,*,14,Auto"), Children = { icon, details, reading } }; Grid.SetColumn(details, 2); Grid.SetColumn(reading, 4);
        var summary = new Border { MinHeight = 94, Padding = new(12), CornerRadius = new(5), Background = TimerCream, BorderBrush = TimerRule, BorderThickness = new(1), Child = summaryRow };
        MessageText.Text = fromEditor ? editorConsequence : clock ? "Removes this clock from your list." : "Stops this timer and removes it from your list.";
        MessageText.FontSize = 12; MessageText.FontFamily = ClockPickerPresentation.Body; MessageText.Foreground = ink; MessageText.Margin = new(0);
        var historyHint = TimerText("Saved project records stay in your history.", 11, muted); historyHint.Name = "CloseTimerHistoryHint";
        var explanation = new StackPanel { Spacing = 4, Margin = new(0, 14, 0, 0), Children = { MessageText, historyHint } };
        if (fromEditor) explanation.Children.Add(new TextBlock
        {
            Name = "EditorDraftHint", Text = "Unsaved editor changes won’t be applied.", FontFamily = ClockPickerPresentation.Body,
            FontSize = 11, Foreground = muted, TextWrapping = TextWrapping.Wrap
        });
        var body = new ScrollViewer
        {
            Name = "CloseTimerBody", Margin = new(0, 14, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new StackPanel { Children = { summary, explanation } }
        };
        foreach (var button in new[] { CancelButton, ConfirmButton })
        {
            bool cancel = button == CancelButton;
            var label = TimerText(cancel ? "Cancel" : "Close Timer", 13, cancel ? TimerCream : TimerRed); label.FontWeight = FontWeight.SemiBold;
            button.Classes.Clear(); button.MinHeight = 36; button.Padding = new(12, 7); button.CornerRadius = new(4);
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Background = cancel ? ink : TimerCream; button.Foreground = cancel ? TimerCream : TimerRed;
            button.BorderBrush = cancel ? gold : TimerRedRule; button.BorderThickness = new(1); button.Content = label;
            if (cancel)
            {
                button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655"); button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
                button.Resources["ButtonForegroundPointerOver"] = button.Resources["ButtonForegroundPressed"] = TimerCream;
                button.Resources["ButtonBorderBrushPointerOver"] = button.Resources["ButtonBorderBrushPressed"] = gold;
            }
            DesktopTypography.SetPreserveColor(button, true);
            AutomationProperties.SetName(button, cancel ? "Cancel closing this timer" : "Close this timer");
        }
        var hint = TimerText(fromEditor ? "Esc · back to editor" : "Esc · cancel", 11, muted); hint.Name = "CloseTimerFooterHint";
        var footerRow = new Grid { ColumnDefinitions = new("*,10,104,10,126"), Children = { hint, CancelButton, ConfirmButton } };
        Grid.SetColumn(CancelButton, 2); Grid.SetColumn(ConfirmButton, 4);
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = TimerRule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, body, footer } }; Grid.SetRow(body, 1); Grid.SetRow(footer, 2);
        Content = new StopwatchFrameSurface { ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        SystemDecorations = SystemDecorations.None; Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent]; CanResize = false;
        MinWidth = MinHeight = 0; SizeToContent = SizeToContent.Manual;
        double scale = AppUiScale.Normalize(settings.UiScalePercent) / 100; Width = 500 * scale; Height = (fromEditor ? 380 : 350) * scale;
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) FitCloseTimerToScreen(screen); else FitCloseTimerContent(); };
        SizeChanged += (_, _) => FitCloseTimerContent();
    }
    internal void FitCloseTimerToScreen(Screen screen)
    {
        MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling); MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        Width = Math.Min(Width, MaxWidth); Height = Math.Min(Height, MaxHeight);
        var area = screen.WorkingArea;
        double width = Math.Ceiling(Width * screen.Scaling), height = Math.Ceiling(Height * screen.Scaling);
        double centerX = area.X + area.Width / 2d, centerY = area.Y + area.Height / 2d;
        if (Owner is Window owner)
        {
            var size = owner.FrameSize ?? owner.ClientSize;
            centerX = owner.Position.X + size.Width * owner.RenderScaling / 2;
            centerY = owner.Position.Y + size.Height * owner.RenderScaling / 2;
        }
        Position = new PixelPoint((int)Math.Clamp(centerX - width / 2, area.X, area.Right - width), (int)Math.Clamp(centerY - height / 2, area.Y, area.Bottom - height));
        FitCloseTimerContent();
    }
    internal void FitCloseTimerContent(bool resizeWindow = false)
    {
        if (!_closeTimerConfigured || DesktopPageAppearance.Content(this) is not StopwatchFrameSurface frame) return;
        double scale = DesktopPageAppearance.Scale(this);
        if (resizeWindow)
        {
            Width = 500 * scale; Height = (_closeTimerFromEditor ? 380 : 350) * scale;
            if (Screens.ScreenFromWindow(this) is { } screen) { FitCloseTimerToScreen(screen); return; }
        }
        frame.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale; frame.Height = (ClientSize.Height > 0 ? ClientSize.Height : Height) / scale;
        if (Content is LayoutTransformControl transform) transform.InvalidateArrange();
    }
}
