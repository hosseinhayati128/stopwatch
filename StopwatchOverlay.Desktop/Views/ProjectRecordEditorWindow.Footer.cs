using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordEditorWindow
{
    private static readonly IBrush RecordFooterPaper = Brush.Parse("#F4E1BC");
    private static readonly IBrush RecordFooterRule = Brush.Parse("#B3935D");
    private static readonly IBrush RecordFooterInk = Brush.Parse("#584B3B");
    private Grid? _recordFooterContent;
    private TextBlock? _recordFooterStatus;
    private TextBlock? _durationCaption;
    private double _fittedRecordHeight;
    private bool _recordHeightFitQueued;

    private Border BuildRecordFooter()
    {
        bool pixel = DesktopPageAppearance.UsesPixelController;
        _durationCaption = RecordLabel("Duration", 12);
        _durationCaption.Name = "RecordDurationCaption";
        _durationCaption.Foreground = RecordFooterInk;
        _durationCaption.FontWeight = FontWeight.SemiBold;
        DurationPreviewText.Classes.Clear();
        DurationPreviewText.LineHeight = 37;
        DurationPreviewText.TextTrimming = TextTrimming.CharacterEllipsis;
        UpdateDurationTypeface();
        var amount = new StackPanel { Children = { _durationCaption, DurationPreviewText } };

        foreach (var action in new[] { CancelButton, SaveButton })
        {
            NavigatorFormStyles.StyleButton(action, action == SaveButton);
            if (pixel) action.Classes.Add("pixelAction");
            action.Height = 39;
            action.Padding = new Thickness(10, 0);
        }
        CancelButton.Width = 70;
        SaveButton.Width = 130;
        SaveButton.Classes.Add("recordFooterSave");
        Styles.Add(new Style(s => s.OfType<Button>().Class("recordFooterSave").Class(":disabled"))
        {
            Setters =
            {
                new Setter(Visual.OpacityProperty, 1d),
                new Setter(Button.BackgroundProperty, Brush.Parse("#E2D1AD")),
                new Setter(Button.BorderBrushProperty, Brush.Parse("#AC9976")),
                new Setter(Button.ForegroundProperty, Brush.Parse("#695941")),
            },
        });
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center, Children = { CancelButton, SaveButton },
        };

        _recordFooterStatus = RecordLabel("", 12, new Thickness(0, 4, 0, 0));
        _recordFooterStatus.Name = "RecordFooterStatus";
        _recordFooterStatus.TextWrapping = TextWrapping.NoWrap;
        _recordFooterStatus.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetLiveSetting(_recordFooterStatus, AutomationLiveSetting.Polite);
        _recordFooterContent = new Grid
        {
            ColumnDefinitions = new("*,12,Auto"), RowDefinitions = new("Auto,Auto"),
            Children = { amount, actions, _recordFooterStatus },
        };
        Grid.SetColumn(actions, 2);
        Grid.SetRow(_recordFooterStatus, 1);
        Grid.SetColumnSpan(_recordFooterStatus, 3);
        _recordFooterContent.Children.Add(ValidationText);
        PositionValidationMessage();
        return new Border
        {
            Name = "RecordFooter", Background = RecordFooterPaper,
            BorderBrush = RecordFooterRule, BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(pixel ? 0 : 5),
            Padding = new Thickness(12, 8, 10, 9), Margin = new Thickness(0, 12, 8, 0),
            Child = _recordFooterContent,
        };
    }

    private void UpdateRecordFooter(TimeSpan? duration, string projectName)
    {
        bool valid = duration.HasValue;
        SaveButton.IsEnabled = valid;
        var elapsed = duration.GetValueOrDefault();
        string clock = elapsed.ToString(@"hh\:mm\:ss");
        // The day count sits above the digits so very long records fit beside the buttons.
        _durationCaption!.Text = elapsed.Days > 0 ? $"Duration · {elapsed.Days:N0}d" : "Duration";
        DurationPreviewText.Text = valid ? clock : "—";
        DurationPreviewText.Foreground = valid ? ClockPickerPresentation.Navy : RecordDanger;
        ToolTip.SetTip(DurationPreviewText, elapsed.Days > 0 ? $"{elapsed.Days:N0}d {clock}" : null);
        UpdateDurationTypeface();
        string instruction;
        if (_validationTarget == StartTimeBox) instruction = "Correct the start time to save.";
        else if (_validationTarget == EndTimeBox) instruction = "Correct the end time to save.";
        else if (_validationTarget == StartDatePicker) instruction = "Correct the start date to save.";
        else if (_validationTarget == EndDatePicker) instruction = "Correct the end date to save.";
        else instruction = "Check the record details.";
        _recordFooterStatus!.Text = valid ? $"Project · {projectName}" : instruction;
        _recordFooterStatus.Foreground = valid ? RecordFooterInk : RecordDanger;
        _recordFooterStatus.FontWeight = valid ? FontWeight.Normal : FontWeight.SemiBold;
        ToolTip.SetTip(_recordFooterStatus, valid ? projectName : null);

        int days = valid ? ((ReadRecordDate(EndDatePicker)?.Date - ReadRecordDate(StartDatePicker)?.Date)?.Days ?? 0) : 0;
        _dayOffset!.IsVisible = days > 0;
        _dayOffset.Text = days == 1 ? "Next day" : $"+{days} days";
    }

    private void UpdateDurationTypeface()
    {
        if (DesktopPageAppearance.UsesPixelController && SaveButton.IsEnabled)
        {
            // Keep each block of the bitmap face on a whole device pixel.
            double deviceScale = DesktopPageAppearance.Scale(this) * RenderScaling;
            DurationPreviewText.FontFamily = StopwatchTypography.PixelNumbers;
            DurationPreviewText.FontWeight = FontWeight.Normal;
            DurationPreviewText.FontSize = 8 * Math.Max(1, Math.Round(3.2 * deviceScale)) / deviceScale;
        }
        else
        {
            var face = StopwatchTypography.Resolve("navigator-bold");
            DurationPreviewText.FontFamily = face.TimerFont;
            DurationPreviewText.FontWeight = face.TimerWeight;
            DurationPreviewText.FontSize = 30;
        }
    }

    private void UpdateNavigatorValidation(bool show)
    {
        if (_recordFooterStatus == null) return;
        bool inline = show && PositionValidationMessage();
        _recordFooterStatus.IsVisible = !show || inline;
        foreach (var field in new Control[] { ProjectSelector, NewProjectBox, StartDatePicker, EndDatePicker, StartTimeBox, EndTimeBox })
            field.Classes.Set("invalidTime", show && field == _validationTarget);
        QueueRecordHeightFit();
    }

    private double PreferredRecordHeight => (IsAddingProject ? 592 : 480) * DesktopPageAppearance.Scale(this);

    internal void RefitNavigatorRecord()
    {
        if (_recordFooterContent == null) return;
        SetFittedRecordHeight(PreferredRecordHeight);
        FitRecordFrame();
        UpdateDurationTypeface();
        QueueRecordHeightFit();
    }

    private void FitRecordFrame()
    {
        if (Content is not LayoutTransformControl { Child: StopwatchFrameSurface frame } transform ||
            ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;
        double scale = DesktopPageAppearance.Scale(this);
        frame.Width = ClientSize.Width / scale;
        frame.Height = ClientSize.Height / scale;
        transform.InvalidateArrange();
    }

    private void SetFittedRecordHeight(double height)
    {
        var screen = IsVisible ? Screens.ScreenFromWindow(this) : null;
        double available = screen == null ? double.PositiveInfinity : Math.Max(1, screen.WorkingArea.Height / RenderScaling - 16);
        MinHeight = Math.Min(available, Math.Min(380, 475 * DesktopPageAppearance.Scale(this)));
        _fittedRecordHeight = Math.Clamp(height, MinHeight, available);
        Height = _fittedRecordHeight;
        if (screen != null)
        {
            int top = screen.WorkingArea.Y;
            int bottom = screen.WorkingArea.Bottom - (int)Math.Ceiling(_fittedRecordHeight * RenderScaling);
            Position = new PixelPoint(Position.X, Math.Clamp(Position.Y, top, Math.Max(top, bottom)));
        }
    }

    private void QueueRecordHeightFit()
    {
        if (_recordHeightFitQueued || !IsVisible) return;
        _recordHeightFitQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _recordHeightFitQueued = false;
            // A user-resized short window scrolls naturally and keeps its chosen height.
            if (!IsVisible || Math.Abs(ClientSize.Height - _fittedRecordHeight) > 1 ||
                Math.Abs(Height - _fittedRecordHeight) > 1 || RecordScroll.Viewport.Height <= 0)
                return;
            double needed = ClientSize.Height + (RecordBody.DesiredSize.Height + 6 - RecordScroll.Viewport.Height)
                * DesktopPageAppearance.Scale(this);
            double height = Math.Max(PreferredRecordHeight, Math.Ceiling(needed));
            if (Math.Abs(Height - height) > 1) SetFittedRecordHeight(height);
        }, DispatcherPriority.Background);
    }
}
