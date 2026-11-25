using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow
{
    private TextBlock? _footerError;

    private Control BuildEditorFooter(bool pixel)
    {
        EditorActions.Children.Clear();
        SetFooterAction(UndoPreviousEditButton, "Undo saved edit", "Ctrl+Z", pixel);
        SetFooterAction(CancelButton, "Cancel", "Esc", pixel);
        SetFooterAction(SaveButton, "Save & Apply", "Ctrl+Enter", pixel, primary: true);

        _footerError = Text("", 12, EditorDanger, strong: true);
        _footerError.Name = "FooterError";
        AutomationProperties.SetLiveSetting(_footerError, AutomationLiveSetting.Polite);
        var notice = new Border
        {
            BorderBrush = EditorDanger, BorderThickness = new(2, 0, 0, 0),
            Margin = new(0, 8, 0, 0), Padding = new(7, 0, 0, 0), Child = _footerError
        };
        notice.Bind(IsVisibleProperty, _footerError.GetObservable(IsVisibleProperty));

        var closingActions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { CancelButton, SaveButton }
        };
        UndoPreviousEditButton.HorizontalAlignment = HorizontalAlignment.Left;
        var actions = new FooterActionPanel(UndoPreviousEditButton, closingActions)
        {
            Margin = new(0, 9, 0, 0),
            Children = { UndoPreviousEditButton, closingActions }
        };
        return new StackPanel
        {
            Name = "EditorFooter", Margin = new(0, 9, 0, 0),
            Children = { new Border { Height = 1, Background = EditorRule }, notice, actions }
        };
    }

    private static void SetFooterAction(Button button, string caption, string shortcut, bool pixel, bool primary = false)
    {
        var ink = primary ? FieldPaper : Navy;
        var shortcutText = Text(shortcut, 11.5, ink);
        shortcutText.FontWeight = FontWeight.Medium;
        var keycap = new Border
        {
            Background = Brush.Parse(primary ? "#294B59" : "#F4E2B9"),
            BorderBrush = Brush.Parse(primary ? "#73929B" : "#C5A66F"),
            CornerRadius = new(pixel ? 0 : 3), BorderThickness = new(1), Padding = new(4, 1),
            VerticalAlignment = VerticalAlignment.Center, Child = shortcutText
        };
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
            Children = { Text(caption, 13, ink, strong: true), keycap }
        };
        AutomationProperties.SetName(button, caption);
        AutomationProperties.SetAcceleratorKey(button, shortcut);
        button.Width = button.Height = double.NaN;
        button.MinWidth = 0;
        button.MinHeight = 39;
        button.Padding = new(10, 1);
        button.Margin = default;
        if (pixel) button.Classes.Add("pixelAction");
    }

    private void UpdateFooterValidation(bool durationValid, bool startValid)
    {
        if (_footerError == null) return;
        _footerError.IsVisible = !durationValid || !startValid;
        if (!_footerError.IsVisible) return;
        _footerError.Text = (durationValid, startValid) switch
        {
            (false, false) => "Check the duration and start time before saving.",
            (true, false) => "Enter a valid start time to save.",
            _ when _timer.Mode == 1 => "Enter a valid recorded time to save.",
            _ when _timer.Mode == 2 => "Enter a valid remaining time to save.",
            _ => "Enter a valid elapsed time to save."
        };
    }

    // Make room for enlarged dialog fonts without separating Cancel from Save.
    private sealed class FooterActionPanel(Button undo, StackPanel closingActions) : Panel
    {
        private bool NeedsSecondRow(double width) =>
            undo.IsVisible && undo.DesiredSize.Width + 8 + closingActions.DesiredSize.Width > width;

        protected override Size MeasureOverride(Size availableSize)
        {
            undo.Measure(Size.Infinity);
            closingActions.Measure(Size.Infinity);
            var left = undo.DesiredSize;
            var right = closingActions.DesiredSize;
            if (NeedsSecondRow(availableSize.Width))
                return new Size(Math.Max(left.Width, right.Width), left.Height + 6 + right.Height);
            return new Size(left.Width + (undo.IsVisible ? 8 : 0) + right.Width, Math.Max(left.Height, right.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var right = closingActions.DesiredSize;
            undo.Arrange(new Rect(undo.DesiredSize));
            double y = NeedsSecondRow(finalSize.Width) ? undo.DesiredSize.Height + 6 : 0;
            closingActions.Arrange(new Rect(finalSize.Width - right.Width, y, right.Width, right.Height));
            return finalSize;
        }
    }
}
