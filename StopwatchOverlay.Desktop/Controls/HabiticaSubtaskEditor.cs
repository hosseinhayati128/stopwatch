using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Habitica;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class HabiticaSubtaskEditor : StackPanel
{
    private readonly StackPanel rows = new() { Spacing = 6 };
    private readonly TextBlock heading = new() { Text = "Checklist", FontWeight = FontWeight.SemiBold };
    internal Button AddButton { get; } = new() { Name = "AddSubtask", Content = "+ Add subtask", HorizontalAlignment = HorizontalAlignment.Left };
    private TodoAppearanceProfile? _todoLook;
    private bool _pixelActions;

    public int Count => rows.Children.Count;
    internal int CompletedCount => rows.Children.Cast<SubtaskRow>().Count(row => row.Completed.IsChecked == true);
    public event EventHandler? CountChanged;
    internal event EventHandler? CompletionChanged;

    public HabiticaSubtaskEditor()
    {
        Spacing = 6;
        AddButton.Classes.Add("modern");
        AddButton.Click += (_, _) =>
        {
            var row = CreateRow(new(Guid.NewGuid(), "", false));
            rows.Children.Add(row);
            RefreshOrder();
            CountChanged?.Invoke(this, EventArgs.Empty);
            row.Input.Focus();
        };
        Children.Add(heading); Children.Add(rows); Children.Add(AddButton);
    }

    public void ApplyTodoAppearance(TodoAppearanceProfile look, bool pixelActions)
    {
        _todoLook = look; _pixelActions = pixelActions;
        rows.Spacing = 5;
        heading.Text = "No subtasks yet."; heading.FontWeight = FontWeight.Normal;
        heading.FontSize = 14; heading.Foreground = TodoVisuals.Muted(look); heading.Margin = new(0, 3, 0, 3);
        heading.IsVisible = Count == 0;
        TodoVisuals.StyleButton(AddButton, look, pixel:pixelActions);
        AddButton.MinHeight = 31; AddButton.FontSize = 13; AddButton.Padding = new(9, 3);
        foreach (SubtaskRow row in rows.Children) row.ApplyAppearance(look, pixelActions);
    }

    public void SetSubtasks(IReadOnlyList<HabiticaSubtask> children)
    {
        rows.Children.Clear();
        rows.Children.AddRange(children.Select(CreateRow));
        RefreshOrder();
        CountChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<HabiticaSubtask> GetSubtasks() =>
        rows.Children.Cast<SubtaskRow>().Select(row => new HabiticaSubtask(row.Id, row.Input.Text?.Trim() ?? "", row.Completed.IsChecked == true)).ToArray();

    internal void ShowBlankRowErrors()
    {
        SubtaskRow? first = null;
        foreach (SubtaskRow row in rows.Children)
        {
            if (!string.IsNullOrWhiteSpace(row.Input.Text)) continue;
            row.SetError(true);
            first ??= row;
        }
        if (first != null)
        {
            // A previously collapsed host attaches its text boxes on the next layout.
            Dispatcher.UIThread.Post(() =>
            {
                if (TopLevel.GetTopLevel(first) == null || !first.IsEffectivelyVisible) return;
                first.UpdateLayout();
                first.Input.Focus();
                first.BringIntoView();
            }, DispatcherPriority.Background);
        }
    }

    private SubtaskRow CreateRow(HabiticaSubtask child)
    {
        var row = new SubtaskRow(child);
        row.Completed.IsCheckedChanged += (_, _) =>
        {
            if (_todoLook != null) row.UpdateInk(_todoLook);
            CompletionChanged?.Invoke(this, EventArgs.Empty);
        };
        void Move(int delta)
        {
            int index = rows.Children.IndexOf(row);
            int next = index + delta;
            if (index < 0 || next < 0 || next >= Count) return;
            rows.Children.Move(index, next);
            RefreshOrder();
        }
        row.Up.Click += (_, _) => Move(-1);
        row.Down.Click += (_, _) => Move(1);
        row.Remove.Click += (_, _) =>
        {
            int index = rows.Children.IndexOf(row);
            if (index < 0) return;
            rows.Children.RemoveAt(index);
            RefreshOrder();
            CountChanged?.Invoke(this, EventArgs.Empty);
            if (Count == 0) AddButton.Focus();
            else ((SubtaskRow)rows.Children[Math.Min(index, Count - 1)]).Input.Focus();
        };
        if (_todoLook != null) row.ApplyAppearance(_todoLook, _pixelActions);
        return row;
    }

    private void RefreshOrder()
    {
        for (int i = 0; i < Count; i++)
        {
            var row = (SubtaskRow)rows.Children[i];
            row.Up.IsEnabled = i > 0;
            row.Down.IsEnabled = i < Count - 1;
        }
        if (_todoLook != null) heading.IsVisible = Count == 0;
    }

    private sealed class SubtaskRow : Grid
    {
        private static readonly Geometry UpIcon = Geometry.Parse("M2,9 L7,4 L12,9");
        private static readonly Geometry DownIcon = Geometry.Parse("M2,4 L7,9 L12,4");
        private static readonly Geometry RemoveIcon = Geometry.Parse("M1,3 H13 M5,3 V1 H9 V3 M3,5 L4,13 H10 L11,5 M6,6 V10 M8,6 V10");
        private IBrush _rule = TodoVisuals.Brass;
        private readonly TextBlock error = new() { Name = "SubtaskError", Text = "Enter a subtask or remove this row.", IsVisible = false,
            FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new(0, 4, 0, 1), TextWrapping = TextWrapping.Wrap };
        internal Guid Id { get; }
        internal TextBox Input { get; } = new() { Watermark = "Subtask", Margin = new(5, 0) };
        internal CheckBox Completed { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
        internal Button Up { get; } = ActionButton("↑", "Move subtask up");
        internal Button Down { get; } = ActionButton("↓", "Move subtask down");
        internal Button Remove { get; } = ActionButton("×", "Remove subtask");

        internal SubtaskRow(HabiticaSubtask child)
        {
            Id = child.Id;
            ColumnDefinitions = new("Auto,*,Auto,Auto,Auto"); RowDefinitions = new("Auto,Auto");
            Completed.IsChecked = child.Completed; Input.Text = child.Text;
            AutoTextDirection.Attach(Input);
            AutomationProperties.SetName(Completed, "Subtask completed"); AutomationProperties.SetName(Input, "Subtask text");
            Children.Add(Completed); SetColumn(Input, 1); Children.Add(Input);
            SetColumn(Up, 2); Children.Add(Up); SetColumn(Down, 3); Children.Add(Down); SetColumn(Remove, 4); Children.Add(Remove);
            SetColumn(error, 1); SetColumnSpan(error, 4); SetRow(error, 1); Children.Add(error);
            Input.TextChanged += (_, _) => { if (error.IsVisible && !string.IsNullOrWhiteSpace(Input.Text)) SetError(false); };
        }

        private static Button ActionButton(string text, string label)
        {
            var button = new Button { Content = text, Padding = new(7, 3), VerticalAlignment = VerticalAlignment.Center };
            button.Classes.Add("modern"); AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label);
            return button;
        }

        internal void SetError(bool invalid)
        {
            error.IsVisible = invalid;
            Input.Watermark = invalid ? null : "Subtask text";
            var border = invalid ? error.Foreground : _rule;
            if (Input.Parent is TodoFrame frame) frame.BorderBrush = border;
            else Input.BorderBrush = border;
            AutomationProperties.SetHelpText(Input, invalid ? error.Text : null);
        }

        internal void UpdateInk(TodoAppearanceProfile look) =>
            Input.Foreground = Completed.IsChecked == true ? TodoVisuals.Muted(look) : TodoVisuals.Ink(look);

        internal void ApplyAppearance(TodoAppearanceProfile look, bool pixel)
        {
            bool dark = look.Background == "midnight-chart";
            ColumnDefinitions = new("26,*,32,32,38");
            _rule = Brush.Parse(dark ? "#688391" : "#BFA26E");
            error.Foreground = Brush.Parse(dark ? "#FFAAA0" : "#932D26");
            DesktopTypography.SetPreserveColor(error, true);
            TodoVisuals.Checkbox(Completed, look, child:true);
            Completed.MinWidth = 0; Completed.Width = 24; Completed.HorizontalAlignment = HorizontalAlignment.Left;
            Input.TextWrapping = TextWrapping.Wrap; Input.FontFamily = TodoVisuals.BodyFont; Input.FontSize = 14;
            Input.VerticalContentAlignment = VerticalAlignment.Center;
            Input.Margin = new(0, 0, 5, 0); Input.Padding = new(6, 4); Input.MinHeight = 35;
            var fieldBackground = dark ? TodoVisuals.Card(look) : Brush.Parse("#FFFAEB");
            Input.Background = fieldBackground;
            Input.BorderBrush = _rule; Input.CornerRadius = new(3); Input.Watermark = "Subtask text"; UpdateInk(look);
            if (pixel)
            {
                TodoVisuals.FramePixelInput(this, Input, look);
                if (Input.Parent is TodoFrame frame) { frame.Background = fieldBackground; frame.BorderBrush = _rule; }
            }
            void StyleAction(Button button, Geometry icon, IBrush stroke, int iconHeight)
            {
                TodoVisuals.StyleButton(button, look, pixel:pixel);
                button.Width = button.Height = button.MinHeight = 30; button.MinWidth = 0;
                button.Margin = button.Padding = new(0); button.HorizontalAlignment = HorizontalAlignment.Right;
                button.Background = Brushes.Transparent; button.BorderBrush = _rule;
                button.Content = new IconPath { Data = icon, Width = 13, Height = iconHeight, Stroke = stroke,
                    StrokeThickness = 1.6, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            StyleAction(Up, UpIcon, TodoVisuals.Ink(look), 8);
            StyleAction(Down, DownIcon, TodoVisuals.Ink(look), 8);
            StyleAction(Remove, RemoveIcon, error.Foreground!, 14);
        }
    }
}
