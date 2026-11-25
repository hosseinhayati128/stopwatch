using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ProjectRecordEditorWindow
{
    private TextBlock? _dayOffset;
    private static readonly IBrush EndpointRule = Brush.Parse("#C9AC7A");
    private static readonly IBrush EndpointPaper = Brush.Parse("#FAEDCF");

    private Border BuildEndpointFields()
    {
        bool pixel = DesktopPageAppearance.UsesPixelController;
        var fields = new Grid
        {
            ColumnDefinitions = new("72,8,1.32*,10,*"),
            RowDefinitions = new("Auto,5,Auto,Auto,8,1,8,Auto,Auto")
        };
        void Place(Control control, int row, int column)
        {
            Grid.SetRow(control, row);
            Grid.SetColumn(control, column);
            fields.Children.Add(control);
        }

        var dateLabel = RecordLabel("Date", 12);
        var timeLabel = RecordLabel("Local time · 24h", 12);
        timeLabel.FontWeight = dateLabel.FontWeight = FontWeight.SemiBold;
        Place(dateLabel, 0, 2);
        Place(timeLabel, 0, 4);
        var divider = new Border { Background = EndpointRule };
        Grid.SetColumnSpan(divider, 5);
        Place(divider, 5, 0);

        _dayOffset = RecordLabel("", 11);
        _dayOffset.FontWeight = FontWeight.SemiBold;
        _dayOffset.Foreground = Brush.Parse("#176458");
        _dayOffset.IsVisible = false;

        void AddRow(int row, string title, CalendarDatePicker date, TextBox time)
        {
            var label = RecordLabel(title, 17);
            label.Foreground = ClockPickerPresentation.Navy;
            label.FontWeight = FontWeight.SemiBold;
            var group = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1, Children = { label } };
            if (date == EndDatePicker) group.Children.Add(_dayOffset);
            Place(group, row, 0);

            time.Height = date.Height = double.NaN;
            time.MinHeight = date.MinHeight = 40;
            time.CornerRadius = date.CornerRadius = new CornerRadius(pixel ? 0 : 4);
            time.Background = date.Background = EndpointPaper;
            time.BorderBrush = date.BorderBrush = EndpointRule;
            date.Padding = new Thickness(8, 0);
            time.Padding = new Thickness(9, 0);
            time.VerticalContentAlignment = VerticalAlignment.Center;
            time.Foreground = ClockPickerPresentation.Navy;
            time.FontFamily = pixel ? StopwatchTypography.PixelNumbers : ClockPickerPresentation.Body;
            time.FontSize = pixel ? 17 : 16;
            time.FontWeight = pixel ? FontWeight.Normal : FontWeight.Medium;
            Place(date, row, 2);
            Place(time, row, 4);
        }

        AddRow(2, "Start", StartDatePicker, StartTimeBox);
        AddRow(7, "End", EndDatePicker, EndTimeBox);
        AutomationProperties.SetLiveSetting(ValidationText, AutomationLiveSetting.Polite);
        return new Border
        {
            Name = "RecordRange", Margin = new(0, 18, 0, 0), Padding = new(12, 9, 12, 11),
            Background = Brush.Parse("#F1DDB5"), BorderBrush = EndpointRule, BorderThickness = new(1),
            CornerRadius = new(pixel ? 0 : 6), Child = fields
        };
    }

    private bool PositionValidationMessage()
    {
        var fields = (Grid)StartTimeBox.Parent!;
        var field = _validationTarget;
        bool inline = field?.Parent == fields;
        var owner = inline ? fields : _recordFooterContent!;
        if (ValidationText.Parent != owner)
        {
            (ValidationText.Parent as Panel)?.Children.Remove(ValidationText);
            owner.Children.Add(ValidationText);
        }
        Grid.SetRow(ValidationText, inline ? Grid.GetRow(field!) + 1 : 1);
        Grid.SetColumn(ValidationText, inline ? Grid.GetColumn(field!) : 0);
        Grid.SetColumnSpan(ValidationText, inline ? 1 : owner.ColumnDefinitions.Count);
        ValidationText.Margin = new Thickness(0, inline ? 5 : 4, 0, 0);
        ValidationText.FontSize = 12;
        ValidationText.FontWeight = FontWeight.SemiBold;
        return inline;
    }
}
