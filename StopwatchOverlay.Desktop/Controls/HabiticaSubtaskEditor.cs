using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class HabiticaSubtaskEditor : StackPanel
{
    private TodoAppearanceProfile? _todoLook;
    private bool _pixelActions;
    public void ApplyTodoAppearance(TodoAppearanceProfile look, bool pixelActions)
    {
        _todoLook=look;_pixelActions=pixelActions;
        foreach(var row in rows.Children.OfType<Grid>())StyleRow(row);
        foreach(var button in Children.OfType<Button>())TodoVisuals.StyleButton(button,look,pixel:pixelActions);
    }
    private void StyleRow(Grid row)
    {
        if(_todoLook==null)return;
        foreach(var button in row.Children.OfType<Button>())
        {
            TodoVisuals.StyleButton(button,_todoLook,pixel:_pixelActions);
            button.Width=button.Height=34;button.Padding=new Thickness(0);button.Margin=new Thickness(2,0,0,0);button.FontSize=18;
        }
        foreach(var check in row.Children.OfType<CheckBox>())TodoVisuals.Checkbox(check,_todoLook,child:true);
        foreach(var input in row.GetLogicalDescendants().OfType<TextBox>().ToArray())
        {
            input.Background=TodoVisuals.Card(_todoLook);input.Foreground=TodoVisuals.Ink(_todoLook);input.BorderBrush=TodoVisuals.Brass;input.FontFamily=TodoVisuals.BodyFont;
            input.FontSize=15;input.MinHeight=34;input.VerticalContentAlignment=VerticalAlignment.Center;
            if(_pixelActions&&input.Parent==row)TodoVisuals.FramePixelInput(row,input,_todoLook);
        }
    }
    private readonly StackPanel rows = new() { Spacing = 6 };
    public HabiticaSubtaskEditor()
    {
        Spacing = 6;
        Children.Add(new TextBlock { Text = "Checklist", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        Children.Add(rows);
        var add = new Button { Content = "+ Add subtask", HorizontalAlignment = HorizontalAlignment.Left };
        add.Classes.Add("modern");
        add.Click += (_, _) => { var row = AddRow(new(Guid.NewGuid(), "", false)); RowInput(row).Focus(); };
        Children.Add(add);
    }

    public void SetSubtasks(IReadOnlyList<HabiticaSubtask> children)
    {
        rows.Children.Clear();
        foreach (var child in children) AddRow(child);
    }

    public IReadOnlyList<HabiticaSubtask> GetSubtasks() => rows.Children.Cast<Grid>()
        .Select(row => new HabiticaSubtask((Guid)row.Tag!, RowInput(row).Text?.Trim() ?? "",
            row.Children.OfType<CheckBox>().Single().IsChecked == true)).ToArray();

    private static TextBox RowInput(Grid row)=>row.GetLogicalDescendants().OfType<TextBox>().Single();

    private Grid AddRow(HabiticaSubtask child)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), Tag = child.Id };
        var check = new CheckBox { IsChecked = child.Completed, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(check, "Subtask completed");
        row.Children.Add(check);
        var input = new TextBox { Text = child.Text, Watermark = "Subtask", Margin = new Thickness(5, 0) };
        AutoTextDirection.Attach(input);
        AutomationProperties.SetName(input, "Subtask text");
        Grid.SetColumn(input, 1); row.Children.Add(input);
        void AddButton(string title, int column, string name, Action action)
        {
            var button = new Button { Content = title, Padding = new Thickness(7, 3), VerticalAlignment = VerticalAlignment.Center };
            button.Classes.Add("modern"); AutomationProperties.SetName(button, name); ToolTip.SetTip(button, name);
            button.Click += (_, _) => action(); Grid.SetColumn(button, column); row.Children.Add(button);
        }
        void Move(int delta)
        {
            int index = rows.Children.IndexOf(row), next = index + delta;
            if (next < 0 || next >= rows.Children.Count) return;
            rows.Children.RemoveAt(index); rows.Children.Insert(next, row);
        }
        AddButton("↑", 2, "Move subtask up", () => Move(-1));
        AddButton("↓", 3, "Move subtask down", () => Move(1));
        AddButton("×", 4, "Remove subtask", () => rows.Children.Remove(row));
        rows.Children.Add(row);
        StyleRow(row);
        return row;
    }
}
