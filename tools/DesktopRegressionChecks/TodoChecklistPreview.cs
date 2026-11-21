using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Habitica;


internal static class TodoChecklistPreview
{
    const string Output = "design/one-piece-v2/todo-editor/checklist-implemented";
    const string Task = "Prepare the exhibition poster for print.";
    const string LongSubtask = "Compare the lettering and margins on the final proof before sending it to the print studio.";
    const string InvalidMessage = "Enter a subtask or remove this row.";
    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-checklist-design-");
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = vault.FullName, HabiticaEnabled = false, TelegramEnabled = false };
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(vault.FullName, "outbox.json"));
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            DeleteTodoNativeChecks.Desktop("DesktopTypography", "Initialize", settings.Typography);
            var crops = new Dictionary<string, Rect>();
            foreach (string profile in new[] { "classic", "pixel" })
            foreach (string state in new[] { "empty", "mixed", "validation" })
            {
                settings.TodoAppearance.Profile = profile;
                string stem = Path.Combine(Output, profile + "-" + state);
                foreach (int scale in new[] { 70, 90, 125 })
                {
                    settings.ObsidianVaultFolder = vault.CreateSubdirectory($"{profile}-{state}-{scale}").FullName;
                    settings.UiScalePercent = scale;
                    DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Apply", settings, true);
                    var bounds = Render(settings, state, scale, stem);
                    if (scale == 90) crops[profile + "-" + state] = bounds;
                }
                DeleteTodoPreview.Comparison(stem);
            }
            Overview(crops);
            CheckLongList(settings);
        }
        finally
        {
            DeleteTodoNativeChecks.Desktop("DesktopTypography", "Shutdown");
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown");
            TelegramOutboxStore.SetCustomFilePath(null);
            vault.Delete(true);
        }
    }

    static Rect Render(AppSettings settings, string state, int scale, string stem)
    {
        string vault = settings.ObsidianVaultFolder;
        var sync = HabiticaIntegration.Current;
        NoteEntry? entry = null;
        var first = new HabiticaSubtask(Guid.NewGuid(), "Check the trim size", true);
        var last = new HabiticaSubtask(Guid.NewGuid(), "Export the print PDF", false);
        var longItem = new HabiticaSubtask(Guid.NewGuid(), LongSubtask, false);
        HabiticaSubtask[] initial = state == "mixed" ? [first, longItem, last] : [first, last];
        if (state != "empty")
        {
            Require(ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, Task).Success, "Seed Todo");
            entry = ObsidianNotesSync.LoadAllNotes(vault).Single();
            Require(sync.SetChildren(settings, entry, initial).SyncWarning == null, "Seed checklist");
        }
        var window = new NoteEntryWindow(NoteType.Todo, settings, entry) { Topmost = false, ShowActivated = false };
        try
        {
            window.Show(); ClockOverlayChecks.Pump(85);
            var editor = (HabiticaSubtaskEditor)window.FindControl<ContentControl>("SubtaskEditorHost")!.Content!;
            T Named<T>(string name) where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
            var toggle = Named<ToggleButton>("TodoChecklistToggle");
            var add = Named<Button>("AddSubtask");
            var card = Named<TodoFrame>("TodoChecklistCard");
            var progress = Named<TextBlock>("ChecklistProgress");
            var save = window.FindControl<Button>("SaveButton")!;
            var rows = editor.Children.OfType<StackPanel>().Single();
            Grid Row(int i) => (Grid)rows.Children[i];
            TextBox Field(int i) => Row(i).GetLogicalDescendants().OfType<TextBox>().Single();
            Button Action(int i, string name) => Row(i).Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
            void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ClockOverlayChecks.Pump(25); }
            bool InlineError(int i) => Row(i).Children.OfType<TextBlock>().Any(t => t.IsVisible && t.Text == InvalidMessage);
            var parent = window.FindControl<TextBox>("NoteInputBox")!;
            if (entry == null) parent.Text = Task;
            toggle.IsChecked = true; ClockOverlayChecks.Pump(35);
            if (state == "validation")
            {
                Click(add); Click(Action(2, "Move subtask up"));
                toggle.IsChecked = false;
                Click(save);
                Require(window.IsVisible && toggle.IsChecked == true && Field(1).IsFocused && InlineError(1) &&
                    !window.FindControl<TextBlock>("SaveError")!.IsVisible && sync.GetDetails(settings, entry!).Children.SequenceEqual(initial), "Blank draft did not reopen at the row or changed stored data");
            }
            else if (state == "empty") add.Focus();
            else toggle.Focus();
            card.BringIntoView(); ClockOverlayChecks.Pump(80);
            Require(state == "empty" ? !progress.IsVisible && editor.Children.OfType<TextBlock>().Single().IsVisible :
                progress.Text == "1 of 3 done" && !Action(0, "Move subtask up").IsEnabled && !Action(2, "Move subtask down").IsEnabled, "Incorrect progress or boundary buttons");
            Require(card.TranslatePoint(new Point(0, card.Bounds.Height), window)!.Value.Y <= save.TranslatePoint(default, window)!.Value.Y + 1, "Checklist overlaps footer");
            DeleteTodoNativeChecks.Capture(window, scale, $"{stem}-{scale}.png");
            var crop = new Rect(card.TranslatePoint(default, window)!.Value,
                card.TranslatePoint(new Point(card.Bounds.Width, card.Bounds.Height), window)!.Value).Inflate(1);

            if (state == "mixed")
            {
                Require(Field(1).Bounds.Height > 43, "Long subtask did not wrap");
                Click(Action(1, "Move subtask down"));
                Require(editor.GetSubtasks().SequenceEqual(new[] { first, last, longItem }), "Reorder changed subtask identity");
                Row(2).Children.OfType<CheckBox>().Single().IsChecked = true;
                Require(progress.Text == "2 of 3 done", "Completing a row did not update progress");
                Click(Action(0, "Remove subtask"));
                Require(progress.Text == "1 of 2 done" && Field(0).IsFocused, "Removal did not update progress and focus");
            }
            else if (state == "validation")
            {
                Field(1).Text = "   "; ClockOverlayChecks.Pump(20);
                Require(InlineError(1), "Whitespace cleared the error");
                Field(1).Text = "Check margins"; ClockOverlayChecks.Pump(20);
                Require(!InlineError(1) && editor.GetSubtasks()[1].Text == "Check margins", "Corrected row still invalid");
            }
            else
            {
                Click(add); Require(editor.Count == 1 && Field(0).IsFocused, "First subtask did not receive focus");
                Click(Action(0, "Remove subtask")); Require(editor.Count == 0 && add.IsFocused, "Removing last item lost focus");
                Click(add); Field(0).Text = "Send the proof";
            }
            var expected = editor.GetSubtasks();
            toggle.IsChecked = false;
            if (scale == 70)
            {
                Click(window.FindControl<Button>("CancelButton")!);
                Require(!window.IsVisible && (entry == null ? !ObsidianNotesSync.LoadAllNotes(vault).Any() :
                    sync.GetDetails(settings, entry).Children.SequenceEqual(initial)), "Cancel wrote checklist changes");
            }
            else
            {
                Click(save);
                var saved = ObsidianNotesSync.LoadAllNotes(vault).Single();
                Require(!window.IsVisible && saved.Text == Task && sync.GetDetails(settings, saved).Children.SequenceEqual(expected), "Saved checklist differs from draft");
                var reopened = new NoteEntryWindow(NoteType.Todo, settings, saved);
                try
                {
                    var restored = (HabiticaSubtaskEditor)reopened.FindControl<ContentControl>("SubtaskEditorHost")!.Content!;
                    Require(restored.GetSubtasks().SequenceEqual(expected), "Reopen lost checklist details");
                }
                finally { reopened.Close(); }
            }
            Console.WriteLine($"PASS {Path.GetFileName(stem)} at {scale}%");
            return crop;
        }
        finally { window.Close(); }
    }

    static void CheckLongList(AppSettings settings)
    {
        settings.TodoAppearance.Pixel.Background = "midnight-chart";
        var window = new NoteEntryWindow(NoteType.Todo, settings) { Topmost = false, ShowActivated = false };
        try
        {
            var editor = (HabiticaSubtaskEditor)window.FindControl<ContentControl>("SubtaskEditorHost")!.Content!;
            var draft = Enumerable.Range(1, 24).Select(n => new HabiticaSubtask(Guid.NewGuid(), n >= 22 ? "" : $"Proof {n}", false)).ToArray();
            editor.SetSubtasks(draft); window.FindControl<TextBox>("NoteInputBox")!.Text = Task;
            window.Show(); ClockOverlayChecks.Pump(80);
            window.SizeToContent = SizeToContent.Manual; window.Height = 425;
            window.FindControl<Button>("SaveButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(120);
            var scroll = window.FindControl<ScrollViewer>("EditorScroll")!;
            var errors = editor.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "SubtaskError" && t.IsVisible).ToArray();
            var inputs = editor.GetVisualDescendants().OfType<TextBox>().ToArray();
            Require(inputs[21].IsFocused && errors.Length == 3 && scroll.Offset.Y > 0 &&
                errors[0].TranslatePoint(new Point(0, errors[0].Bounds.Height), scroll)!.Value.Y <= scroll.Bounds.Height + 1,
                $"Blank-row focus {inputs[21].IsFocused}, {errors.Length} errors, offset {scroll.Offset.Y}, extent {scroll.Extent.Height}, first error {errors.FirstOrDefault()?.TranslatePoint(default, scroll)}, viewport {scroll.Bounds.Height}");
            inputs[21].Text = "اصلاح حاشیه"; ClockOverlayChecks.Pump(30);
            Require(inputs[21].FlowDirection == FlowDirection.RightToLeft && !errors[0].IsVisible && errors[1].IsVisible, "Correcting one row cleared other errors or lost RTL");
            Console.WriteLine("PASS Long checklist, dark Pixel profile, multiple blank rows, scroll and RTL correction");
        }
        finally { window.Close(); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static void Overview(Dictionary<string, Rect> crops)
    {
        const int margin = 18, titleHeight = 34, captionHeight = 24;
        var states = new[] { ("empty", "Empty checklist"), ("mixed", "Completed and open subtasks"), ("validation", "Blank subtask after Save") };
        int width = (int)Math.Ceiling(crops.Values.Max(r => r.Width));
        int Height(string state) => (int)Math.Ceiling(Math.Max(crops["classic-" + state].Height, crops["pixel-" + state].Height));
        int height = margin + titleHeight + states.Sum(s => captionHeight + Height(s.Item1) + margin);
        var size = new PixelSize(2 * width + 3 * margin, height);
        using var sheet = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, size.Width, size.Height));
            void Label(string value, double fontSize, int x, int y) => dc.DrawText(new FormattedText(value,
                System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(TodoVisuals.BodyFont), fontSize, TodoVisuals.Ivory), new Point(x, y));
            Label("Classic · 90%", 18, margin, margin); Label("Pixel · 90%", 18, width + 2 * margin, margin);
            int y = margin + titleHeight;
            foreach (var (state, caption) in states)
            {
                Label(caption, 14, margin, y); y += captionHeight;
                int x = margin;
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    string name = profile + "-" + state;
                    using var bitmap = new Bitmap(Path.Combine(Output, name + "-90.png"));
                    var bounds = crops[name];
                    dc.DrawImage(bitmap, bounds, new Rect(new Point(x, y), bounds.Size));
                    x += width + margin;
                }
                y += Height(state) + margin;
            }
        }
        sheet.Save(Path.Combine(Output, "checklist-overview.png"));
    }
}
