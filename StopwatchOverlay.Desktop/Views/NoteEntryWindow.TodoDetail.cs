using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow
{
    private Grid? _todoEditorRoot;
    private double TodoEditorWidth => _noteType == NoteType.Todo ? 530 : 520;

    internal static NoteEntryWindow ForTodoDetail(AppSettings settings, TodoDetailKind kind, string parent,
        string? existingText, Func<string, NoteSyncResult> save)
    {
        bool note = kind == TodoDetailKind.Note;
        string title = (existingText == null ? "Add " : "Edit ") + (note ? "note" : "subtask");
        var editor = ForTodoDetail(settings, title, existingText ?? "", save);
        editor.ConfigureTodoDetailEditor(note, parent, existingText == null ? title : "Save changes");
        return editor;
    }

    private void ConfigureTodoDetailEditor(bool note, string parent, string action)
    {
        var parentName = new TextBlock
        {
            Text = parent, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap
        };
        AutoTextDirection.Apply(parentName, parent);
        var contextLabel = new TextBlock { Text = "Parent task", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var parentScroll = new ScrollViewer
        {
            Content = parentName, MaxHeight = 84,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AutomationProperties.SetName(parentScroll, "Parent task");
        var context = new Grid { ColumnDefinitions = new("Auto,12,*"), Children = { contextLabel, parentScroll } };
        Grid.SetColumn(parentScroll, 2);
        var strip = new Border { Child = context, Padding = new(12, 9), BorderThickness = new(2, 0, 0, 0), Margin = new(0, 0, 0, 14) };
        string field = note ? "Attached note" : "Subtask";
        var label = new TextBlock { Text = field, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new(0, 0, 0, 5) };
        EditorBody.Children.Insert(0, strip);
        EditorBody.Children.Insert(1, label);
        string prompt = note ? "Write a note for this task…" : "What needs to be done?";
        if (Classes.Contains("nikaTodoDialog"))
        {
            var profile = _settings.TodoAppearance.Current;
            bool dark = profile.Background == "midnight-chart";
            strip.Background = dark ? TodoVisuals.Card(profile) : Brush.Parse("#F0F8E8C7");
            strip.BorderBrush = Brush.Parse(dark ? "#688391" : "#B99C67");
            contextLabel.Foreground = dark ? TodoVisuals.Muted(profile) : Brush.Parse("#495953");
            parentName.Foreground = label.Foreground = TodoVisuals.Ink(profile);
        }
        ConfigureTodoWritingArea(field, prompt, action, note ? 138 : 83);
    }

    private void ConfigureTodoWritingArea(string field, string prompt, string action, double height)
    {
        var frame = (Decorator)NoteInputBox.Parent!;
        AutomationProperties.SetName(NoteInputBox, field);
        AutomationProperties.SetHelpText(NoteInputBox, prompt);
        AutomationProperties.SetName(SaveButton, action);
        AutomationProperties.SetName(CloseButton, "Close editor");
        NoteInputBox.Watermark = prompt;
        NoteInputBox.VerticalContentAlignment = VerticalAlignment.Top;
        ScrollViewer.SetHorizontalScrollBarVisibility(NoteInputBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(NoteInputBox, ScrollBarVisibility.Auto);
        frame.Height = height;
        SaveButton.Content = action;

        if (Classes.Contains("nikaTodoDialog"))
        {
            var profile = _settings.TodoAppearance.Current;
            bool dark = profile.Background == "midnight-chart";
            var ink = TodoVisuals.Ink(profile);
            var muted = dark ? TodoVisuals.Muted(profile) : Brush.Parse("#495953");
            var rule = Brush.Parse(dark ? "#688391" : "#B99C67");
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            NoteInputBox.Foreground = NoteInputBox.CaretBrush = ink;
            var placeholder = new TextBlock { Text = prompt, FontSize = 16, Foreground = muted, Margin = new(8, 5, 8, 0),
                VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, TextWrapping = TextWrapping.Wrap };
            NoteInputBox.Watermark = null;
            NoteInputBox.Padding = new(7, 5);
            frame.Child = null;
            frame.Child = new Grid { Children = { NoteInputBox, placeholder } };
            frame.Padding = new(5);
            frame.SetValue(Border.BackgroundProperty, dark ? TodoVisuals.Card(profile) : Brush.Parse("#FFF5DF"));
            frame.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            var focus = Brush.Parse(dark ? "#81C9D2" : "#287386");
            frame.SetValue(Border.BorderBrushProperty, rule);
            NoteInputBox.GotFocus += (_, _) => frame.SetValue(Border.BorderBrushProperty, focus);
            NoteInputBox.LostFocus += (_, _) => frame.SetValue(Border.BorderBrushProperty, rule);
            EditorScroll.Margin = new(22, 16, 22, 0);
            CloseButton.Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),
                Width = 15, Height = 15, Stroke = TodoVisuals.Ivory, StrokeThickness = 1.7, Stretch = Stretch.Uniform };
            CloseButton.Width = CloseButton.Height = 32;
            CloseButton.VerticalAlignment = VerticalAlignment.Center;
            CloseButton.Margin = new(0, 0, 12, 0);
            DesktopTypography.SetPreserveColor((Control)CloseButton.Parent!, true);

            EditorFooter.Margin = new(22, 13, 22, 17);
            EditorFooter.Children.Insert(0, new Border { Height = 1, Background = rule });
            EditorFooter.Children.Remove(EditorShortcutHint);
            EditorActions.Children.Insert(0, EditorShortcutHint);
            EditorActions.ColumnDefinitions = new("*,Auto,8,Auto");
            Grid.SetColumn(CancelButton, 1); Grid.SetColumn(SaveButton, 3);
            EditorShortcutHint.Text = "Ctrl+Enter save  ·  Esc cancel";
            EditorShortcutHint.Foreground = muted; EditorShortcutHint.FontSize = 11;
            EditorShortcutHint.HorizontalAlignment = HorizontalAlignment.Left;
            EditorShortcutHint.VerticalAlignment = VerticalAlignment.Center;
            EditorShortcutHint.TextWrapping = TextWrapping.Wrap;
            SaveButton.MinWidth = 110; CancelButton.MinWidth = 78;
            SaveButton.MinHeight = CancelButton.MinHeight = 36;
            SaveButton.Padding = CancelButton.Padding = new(12, 6);
            SaveButton.FontSize = CancelButton.FontSize = 14;
            var actionLabel = new TextBlock { Text = action, FontSize = 14, FontWeight = FontWeight.SemiBold, FontFamily = TodoVisuals.BodyFont };
            SaveButton.Content = actionLabel;
            DesktopTypography.SetPreserveColor(SaveButton, true);
            SaveButton.Opacity = 1;
            SaveButton.TemplateApplied += (_, e) =>
            {
                if (e.NameScope.Find<ContentPresenter>("PART_ContentPresenter") is { } presenter) presenter.Background = Brushes.Transparent;
            };
            var disabled = dark ? TodoVisuals.Card(profile) : Brush.Parse("#E3D3B3");
            void UpdateColors()
            {
                placeholder.IsVisible = string.IsNullOrEmpty(NoteInputBox.Text);
                bool ready = !string.IsNullOrWhiteSpace(NoteInputBox.Text);
                SaveButton.Background = ready ? TodoVisuals.Navy : disabled;
                SaveButton.BorderBrush = ready ? TodoVisuals.Gold : rule;
                actionLabel.Foreground = ready ? TodoVisuals.Ivory : muted;
            }
            NoteInputBox.TextChanged += (_, _) => UpdateColors();
            UpdateColors();
        }
        void UpdateSave() => SaveButton.IsEnabled = !_saving && !string.IsNullOrWhiteSpace(NoteInputBox.Text);
        NoteInputBox.TextChanged += (_, _) => UpdateSave();
        UpdateSave();

        var shell = (Control)Content!;
        Content = null;
        _todoEditorRoot = new Grid { VerticalAlignment = VerticalAlignment.Top, Children = { shell } };
        Content = _todoEditorRoot;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = TodoEditorWidth * AppUiScale.Normalize(_settings.UiScalePercent) / 100;
        MinWidth = MinHeight = 0;
        Height = double.NaN;
        SizeToContent = SizeToContent.Height;
        Opened += (_, _) => FitTodoEditor(false);
        SizeChanged += (_, _) => FitTodoEditor(false);
    }

    private void FitTodoEditor(bool scaleChanged)
    {
        double scale = DesktopPageAppearance.Scale(this);
        if (Screens.ScreenFromWindow(this) is { } screen)
        {
            MaxWidth = Math.Floor(screen.WorkingArea.Width / screen.Scaling);
            MaxHeight = Math.Floor(screen.WorkingArea.Height / screen.Scaling);
        }
        MinWidth = Math.Min((_noteType == NoteType.Todo ? 475 : 320) * scale, MaxWidth);
        if (_noteType == NoteType.Todo)
        {
            MinHeight = Math.Min(250 * scale, MaxHeight);
            if (Content is LayoutTransformControl transform) transform.VerticalAlignment = VerticalAlignment.Top;
            _todoEditorRoot!.MaxHeight = MaxHeight / scale;
            _todoEditorRoot.Height = SizeToContent.HasFlag(SizeToContent.Height) ? double.NaN : ClientSize.Height / scale;
        }
        if (scaleChanged) Width = TodoEditorWidth * scale;
        Width = Math.Min(Width, MaxWidth);
        _todoEditorRoot!.Width = (ClientSize.Width > 0 ? ClientSize.Width : Width) / scale;
    }
}
