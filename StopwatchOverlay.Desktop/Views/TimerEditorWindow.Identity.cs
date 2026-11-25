using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow
{
    private StopwatchFrameSurface? _navigatorFrame;
    private ComboBox? _stateAfterSave;
    private TextBlock? _liveState;
    private static readonly IBrush LiveInk = Brush.Parse("#A5E2C9"), PausedInk = Brush.Parse("#F0CB87"),
        ReadoutLabelInk = Brush.Parse("#CCDBD9"), ReadoutInk = Brush.Parse("#FFF2D5");

    private Control BuildTimerIdentity(bool pixel)
    {
        var timer = ClockPickerItem.Create(_timer, 0, null, DateTime.Now);
        var mode = IdentityText(timer.Mode, 12, ReadoutLabelInk);
        _liveState = IdentityText(timer.State, 12, LiveInk);
        _liveState.FontWeight = FontWeight.SemiBold;
        var dot = new Border { Width = 7, Height = 7, CornerRadius = new(pixel ? 0 : 4), VerticalAlignment = VerticalAlignment.Center };
        dot.Bind(Border.BackgroundProperty, _liveState.GetObservable(TextBlock.ForegroundProperty));
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,
            Children = { mode, IdentityText("·", 12, ReadoutLabelInk), dot, _liveState } };
        var project = IdentityText(timer.Name, 21, ReadoutInk);
        project.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont;
        project.FontWeight = FontWeight.SemiBold;
        project.FlowDirection = timer.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        project.TextWrapping = TextWrapping.Wrap;
        var identity = new StackPanel { Margin = new(0, 0, 16, 0), Spacing = 4, VerticalAlignment = VerticalAlignment.Center,
            Children = { status, project } };

        var label = IdentityText(_timer.Mode switch { 1 => "Local time", 2 => "Remaining now", _ => "Elapsed now" }, 12, ReadoutLabelInk);
        LiveTimePreview.Text = timer.Time;
        Control display;
        if (pixel)
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var digits = new ControllerPixelDigits { FontSize = 45, Height = 43, Foreground = ReadoutInk };
            digits.SelectLargeFace(_settings!.ControllerLargeClockDigits);
            digits.Bind(ControllerPixelDigits.TextProperty, LiveTimePreview.GetObservable(TextBlock.TextProperty));
            digits.Bind(AutomationProperties.NameProperty, LiveTimePreview.GetObservable(TextBlock.TextProperty));
            display = digits;
        }
        else
        {
            var digits = IdentityText(timer.Time, 32, ReadoutInk);
            digits.Name = "ClassicTimerReading";
            digits.FontFamily = StopwatchTypography.Resolve("navigator-bold").TimerFont;
            digits.FontWeight = FontWeight.Bold;
            digits.Bind(TextBlock.TextProperty, LiveTimePreview.GetObservable(TextBlock.TextProperty));
            display = digits;
        }
        var fit = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = display,
            HorizontalAlignment = pixel ? HorizontalAlignment.Center : HorizontalAlignment.Left };
        var readout = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { label, fit } };
        var band = new Grid { ColumnDefinitions = new("*,168"), Children = { identity, readout } };
        Grid.SetColumn(readout, 1);
        DesktopTypography.SetPreserveColor(band, true);
        return new TodoFrame { Pixel = pixel, Padding = new(16, 12), MinHeight = 85, CornerRadius = new(5),
            Background = ClockPickerPresentation.Navy, BorderBrush = ClockPickerPresentation.Navy, BorderThickness = new(1), Child = band };
    }

    private void UpdateNavigatorReading()
    {
        LiveTimePreview.Text = ClockPickerItem.ReadTime(_timer, DateTime.Now);
        bool live = _timer.Mode == 1 || _timer.IsRunning;
        _liveState!.Text = _timer.Mode == 1 ? "Live" : live ? "Running" : "Paused";
        _liveState.Foreground = live ? LiveInk : PausedInk;
    }

    private static TextBlock IdentityText(string text, double size, IBrush ink) => new()
    {
        Text = text, FontSize = size, FontFamily = ClockPickerPresentation.Body, Foreground = ink, VerticalAlignment = VerticalAlignment.Center
    };

    private Control BuildTimeDraft(bool pixel)
    {
        bool clock = _timer.Mode == 1;
        var form = new Grid { ColumnDefinitions = new(clock ? "*" : "*,14,135"), RowDefinitions = new("Auto,5,42") };
        TimeValueLabel.Text = _timer.Mode switch { 1 => "Recorded time to save", 2 => "Remaining time to save", _ => "Elapsed time to save" };
        TimeValueLabel.FontSize = 15;
        TimeValueLabel.Foreground = ClockPickerPresentation.Navy;
        AutomationProperties.SetName(TimeValueBox, TimeValueLabel.Text);
        TimeValueBox.FontFamily = StopwatchTypography.Resolve("navigator-bold").TimerFont;
        TimeValueBox.FontSize = 24;
        TimeValueBox.Height = 42;
        TimeValueBox.MinHeight = 0;
        form.Children.Add(TimeValueLabel);
        form.Children.Add(TimeValueBox);
        Grid.SetRow(TimeValueBox, 2);
        var fields = new List<TemplatedControl> { CategorySelector, ProjectSelector, ScorePerHourBox };
        if (!clock)
        {
            var stateLabel = IdentityText("State after save", 13, ClockPickerPresentation.Navy);
            _stateAfterSave = new ComboBox { Name = "StateAfterSave", ItemsSource = new[] { "Running", "Paused" },
                SelectedIndex = _isRunning ? 0 : 1, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, MinHeight = 0, FontSize = 13 };
            _stateAfterSave.Classes.Add("editorField");
            AutomationProperties.SetName(_stateAfterSave, "State after save");
            _stateAfterSave.SelectionChanged += (_, _) => _isRunning = _stateAfterSave.SelectedIndex == 0;
            fields.Add(_stateAfterSave);
            form.Children.Add(stateLabel);
            form.Children.Add(_stateAfterSave);
            Grid.SetColumn(stateLabel, 2);
            Grid.SetColumn(_stateAfterSave, 2);
            Grid.SetRow(_stateAfterSave, 2);
        }
        foreach (var field in fields)
            field.Styles.Add(new Style(s => s.OfType<PathIcon>()) { Setters = { new Setter(TemplatedControl.ForegroundProperty, ClockPickerPresentation.Navy) } });

        QuickAdjustButtons.HorizontalAlignment = HorizontalAlignment.Left;
        QuickAdjustButtons.Margin = new(0, 9, 0, 0);
        foreach (var button in QuickAdjustButtons.Children.OfType<Button>())
        {
            NavigatorFormStyles.StyleButton(button);
            button.Height = 30;
            button.Padding = new(9, 2);
            if (pixel) button.Classes.Add("pixelAction");
        }
        if (pixel)
            foreach (var button in new[] { SaveButton, CancelButton, UndoPreviousEditButton }) button.Classes.Add("pixelAction");
        AdjustmentSummaryText.FontSize = 11;
        AdjustmentSummaryText.Margin = new(0, 7, 0, 0);
        return new Border { Margin = new(0, 13, 0, 14), Padding = new(0, 0, 0, 13), BorderThickness = new(0, 0, 0, 1),
            BorderBrush = EditorRule, Child = new StackPanel { Children = { form, QuickAdjustButtons, AdjustmentSummaryText } } };
    }

    internal void FitEditorWindow(double ratio)
    {
        if (_navigatorFrame == null) return;
        double scale = DesktopPageAppearance.Scale(this);
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        double maxWidth = screen?.WorkingArea.Width / RenderScaling ?? double.PositiveInfinity;
        double maxHeight = screen == null ? double.PositiveInfinity : Math.Max(1, screen.WorkingArea.Height / RenderScaling - 16);
        MinWidth = Math.Min(560 * scale, maxWidth);
        MinHeight = Math.Min(520 * scale, maxHeight);
        Width = Math.Clamp(Width * ratio, MinWidth, maxWidth);
        Height = Math.Min(780 * scale, maxHeight);
        FitEditorFrame();
    }

    private void FitEditorFrame()
    {
        if (_navigatorFrame == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        double scale = DesktopPageAppearance.Scale(this);
        _navigatorFrame.Width = ClientSize.Width / scale;
        _navigatorFrame.Height = ClientSize.Height / scale;
    }
}
