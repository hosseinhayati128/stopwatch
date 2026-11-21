using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
        toggle.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<ContentPresenter>("PART_ContentPresenter") is { } presenter) presenter.Background = Brushes.Transparent;
        };
        var add = _subtaskEditor.AddButton;
        _subtaskEditor.Children.Remove(add);
        var title = Label("▾  Checklist", 16);
        var progress = new TextBlock { Name = "ChecklistProgress", FontSize = 12, Foreground = TodoVisuals.Muted(profile), VerticalAlignment = VerticalAlignment.Center };
        var header = new Grid { ColumnDefinitions = new("Auto,10,*,Auto"), Children = { toggle, progress, add } };
        Grid.SetColumn(progress, 2); Grid.SetColumn(add, 3);
        var separator = new Border { Height = 1, Background = rule };
        var section = new StackPanel { Spacing = 8, Children = { header, separator, SubtaskEditorHost } };
        var card = new TodoFrame { Name = "TodoChecklistCard", Pixel = pixel, CornerRadius = new(5), Child = section };
        var fill = dark ? TodoVisuals.Card(profile) : Brush.Parse("#FBEDCE");
        void UpdateChecklist()
        {
            bool open = toggle.IsChecked == true;
            SubtaskEditorHost.IsVisible = open;
            int count = _subtaskEditor.Count;
            string caption = count == 0 ? "Checklist" : $"Checklist  ({count})";
            toggle.Content = open ? title : "▸  " + caption;
            AutomationProperties.SetName(toggle, caption);
            progress.IsVisible = open && count > 0;
            progress.Text = progress.IsVisible ? $"{_subtaskEditor.CompletedCount} of {count} done" : "";
            add.IsVisible = separator.IsVisible = open;
            toggle.Background = open ? Brushes.Transparent : TodoVisuals.Card(profile);
            toggle.BorderThickness = new(open ? 0 : 1);
            toggle.Padding = open ? new(3, 0) : new(9, 3);
            card.Padding = open ? new(11, 9) : new(0);
            card.Background = open ? fill : null; card.BorderBrush = open ? rule : null;
            card.BorderThickness = new(open ? 1 : 0);
        }
        toggle.IsCheckedChanged += (_, _) => UpdateChecklist();
        _subtaskEditor.CountChanged += (_, _) => UpdateChecklist();
        _subtaskEditor.CompletionChanged += (_, _) => UpdateChecklist();
        UpdateChecklist();
        TodoDetailsPanel.Children.Add(card);
        ConfigureTodoWritingArea("Task", "What needs to be done?", _isEditing ? "Save changes" : "Save", 94);
    }
}
