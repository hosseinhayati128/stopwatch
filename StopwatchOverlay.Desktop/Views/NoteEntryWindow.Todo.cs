using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow
{
    private ToggleButton? _todoChecklistToggle;

    private void ConfigureParentTodoEditor(TodoAppearanceProfile profile, bool pixel)
    {
        bool dark = profile.Background == "midnight-chart";
        var rule = Brush.Parse(dark ? "#688391" : "#BF9F69");
        TextBlock Label(string text, double size) => new()
        {
            Text = text, FontSize = size, FontFamily = TodoVisuals.BodyFont, FontWeight = FontWeight.SemiBold,
            Foreground = TodoVisuals.Ink(profile), VerticalAlignment = VerticalAlignment.Center
        };
        var taskLabel = Label("Task", 14);
        taskLabel.Margin = new(0, 0, 0, 5);
        EditorBody.Children.Insert(0, taskLabel);

        TodoDetailsPanel.Children.Clear();
        var selectors = new Grid { ColumnDefinitions = new("*,16,*") };
        foreach (var row in new[] { TodoPriorityRow, TodoPlacementRow })
        {
            row.ColumnDefinitions = new("*"); row.RowDefinitions = new("Auto,4,Auto");
            var selector = row.Children.Last();
            Grid.SetColumn(selector, 0); Grid.SetRow(selector, 2);
            selector.HorizontalAlignment = HorizontalAlignment.Stretch; selector.Width = double.NaN;
            selectors.Children.Add(row);
        }
        Grid.SetColumn(TodoPlacementRow, 2);
        if (_isEditing) Grid.SetColumnSpan(TodoPriorityRow, 3);
        TodoPriorityCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
        TodoPriorityCombo.Width = double.NaN;

        DueEnabledToggle.Content = "Set a deadline"; DueEnabledToggle.FontSize = 14;
        ((Grid)TodoDueTime.Parent!).Children.Clear();
        DueFields.Children.Clear();
        var schedule = new Grid { ColumnDefinitions = new("Auto,12,*,10,Auto"), RowDefinitions = new("Auto,6,Auto") };
        var timeLabel = Label("Local time", 12);
        schedule.Children.Add(Label("Date", 12));
        Grid.SetRow(timeLabel, 2); schedule.Children.Add(timeLabel);
        Grid.SetColumn(TodoDueDate, 2); Grid.SetColumnSpan(TodoDueDate, 3); schedule.Children.Add(TodoDueDate);
        TodoDueTime.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(TodoDueTime, 2); Grid.SetRow(TodoDueTime, 2); schedule.Children.Add(TodoDueTime);
        Grid.SetColumn(ClearDueButton, 4); Grid.SetRow(ClearDueButton, 2); schedule.Children.Add(ClearDueButton);
        DueFields.Children.Add(schedule);
        var group = new StackPanel { Spacing = 10, Children = { selectors, new Border { Height = 1, Background = rule }, DueEnabledToggle, DueFields } };
        TodoDetailsPanel.Children.Add(new TodoFrame
        {
            Child = group, Pixel = pixel, CornerRadius = new(5), Padding = new(12, 10),
            Background = dark ? TodoVisuals.Card(profile) : Brush.Parse("#FBEDCE"), BorderBrush = rule, BorderThickness = new(1)
        });

        var toggle = new ToggleButton { Name = "TodoChecklistToggle", HorizontalAlignment = HorizontalAlignment.Left, FontSize = 14, Padding = new(9, 3) };
        _todoChecklistToggle = toggle;
        TodoVisuals.StyleButton(toggle, profile, pixel: pixel);
        void UpdateChecklist()
        {
            bool open = toggle.IsChecked == true;
            SubtaskEditorHost.IsVisible = open;
            string caption = _subtaskEditor.Count == 0 ? "Checklist" : $"Checklist  ({_subtaskEditor.Count})";
            toggle.Content = (open ? "▾  " : "▸  ") + caption;
            AutomationProperties.SetName(toggle, caption);
        }
        toggle.IsCheckedChanged += (_, _) => UpdateChecklist();
        _subtaskEditor.CountChanged += (_, _) => UpdateChecklist();
        UpdateChecklist();
        TodoDetailsPanel.Children.Add(toggle); TodoDetailsPanel.Children.Add(SubtaskEditorHost);
        ConfigureTodoWritingArea("Task", "What needs to be done?", _isEditing ? "Save changes" : "Save", 94);
    }
}
