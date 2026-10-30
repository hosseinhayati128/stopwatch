using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow
{
    private readonly HashSet<string> _expandedDetails = new();

    private bool DetailsExpanded(NoteEntry entry) => _expandedDetails.Any(key =>
        (_organization?.ResolveKey(key) ?? key) == TodoOrganizationStore.GetKey(entry));

    private void AddDetailControls(Grid container, NoteEntry entry, TodoDetailDocument document, IReadOnlyList<HabiticaSubtask> children)
    {
        var items = document.Items.Concat(children.Select(child => new TodoDetail(TodoDetailKind.Subtask,
            child.Text, child.Completed, -1, 0, child.Id.ToString()))).ToArray();
        if (items.Length == 0) return;
        string renderedVault = _settings.ObsidianVaultFolder;
        string? renderedSubfolder = _settings.NotesSubfolder;
        string renderedAccount = DetailAccount();
        bool ContextCurrent() => !_closed && renderedVault == _settings.ObsidianVaultFolder &&
            renderedSubfolder == _settings.NotesSubfolder && renderedAccount == DetailAccount();
        bool expanded = DetailsExpanded(entry);
        int tasks = items.Count(item => item.Kind == TodoDetailKind.Subtask);
        int done = items.Count(item => item.Kind == TodoDetailKind.Subtask && item.IsCompleted);
        int notes = items.Length - tasks;
        string summary = string.Join(" · ", new[] { tasks > 0 ? $"{done}/{tasks} subtask{(tasks == 1 ? "" : "s")}" : null,
            notes > 0 ? $"{notes} note{(notes == 1 ? "" : "s")}" : null }.Where(value => value != null));
        var toggle = new Button { Name = "ToggleTodoDetails", Content = (expanded ? "▾ " : "▸ ") + summary,
            Padding = new Thickness(5, 2), Margin = new Thickness(7, 0, 0, 3),
            HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 11 };
        AutomationProperties.SetName(toggle, (expanded ? "Collapse" : "Expand") + " task details");
        toggle.Click += (_, e) =>
        {
            e.Handled = true;
            if (expanded) _expandedDetails.RemoveWhere(key => (_organization?.ResolveKey(key) ?? key) == TodoOrganizationStore.GetKey(entry));
            else _expandedDetails.Add(TodoOrganizationStore.GetKey(entry));
            _snapshot = null; ReloadTodos();
        };
        Grid.SetColumn(toggle, 2); Grid.SetRow(toggle, 3); container.Children.Add(toggle);
        if (!expanded) return;
        var details = new StackPanel { Name = "TodoDetailsPanel", Spacing = 5, Margin = new Thickness(12, 0, 0, 8) };
        foreach (var detail in items)
        {
            var text = TodoRichText.Create(detail.Text, entry.IsCompleted || detail.IsCompleted);
            text.MaxWidth = 205;
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = detail.Kind == TodoDetailKind.Note ? "Edit note" : "Edit subtask" };
            edit.Click += async (_, e) => { e.Handled = true; menu.Close(); if (ContextCurrent()) await EditDetailAsync(entry, detail.Kind, detail); else DetailConflict(); };
            var delete = new MenuItem { Header = "Delete" };
            delete.Click += async (_, e) =>
            {
                e.Handled = true; menu.Close();
                if (_closed || _deleteInProgress) return;
                if (!ContextCurrent()) { DetailConflict(); return; }
                string vault = _settings.ObsidianVaultFolder;
                string? subfolder = _settings.NotesSubfolder;
                string account = DetailAccount();
                _deleteInProgress = true;
                try
                {
                    _deleteDialog = new ConfirmationDialogWindow("Delete detail", "Delete this " + (detail.Kind == TodoDetailKind.Note ? "note?" : "subtask?"), detail.Text, "Delete", destructive: true) { Topmost = Topmost };
                    _deleteDialog.ApplyTodoAppearance(_settings);
                    bool confirmed = await _deleteDialog.ShowDialog<bool>(this);
                    if (confirmed && !_closed)
                    {
                        if (vault != _settings.ObsidianVaultFolder || subfolder != _settings.NotesSubfolder || account != DetailAccount())
                            ShowError("Your sync account or notes folder changed. Reopen the task and choose Delete again.");
                        else ChangeDetail(entry, detail, null);
                    }
                }
                catch (Exception ex) { CrashLogger.LogRecoverable(ex, "TodoOverlayWindow.DeleteDetail"); if (!_closed) ShowError("Could not delete this detail. Refresh and try again."); }
                finally { _deleteDialog = null; _deleteInProgress = false; if (!_closed) { _snapshot = null; ReloadTodos(); } }
            };
            menu.Items.Add(edit); menu.Items.Add(delete); TrackTaskMenu(menu);
            Control control;
            if (detail.Kind == TodoDetailKind.Subtask)
            {
                text.Margin = new Thickness(7, 0, 0, 0);
                var checkbox = new HoldToCompleteCheckBox { Name = "SubtaskCheckBox", Content = text, IsChecked = detail.IsCompleted, IsEnabled = !_deleteInProgress };
                checkbox.Classes.Add("todoRow");
                ToolTip.SetTip(checkbox, "Hold for 1 second to " + (detail.IsCompleted ? "mark incomplete" : "complete"));
                checkbox.IsCheckedChanged += (_, _) =>
                {
                    if (ContextCurrent()) ChangeDetail(entry, detail, detail.Text, checkbox.IsChecked == true);
                    else DetailConflict();
                };
                Control progress;
                if (IsNikaTodo && _settings.TodoAppearance.IsPixel)
                {
                    var meter = new TodoHoldMeter { Pixel = true, Ink = TodoVisuals.Ink(TodoLook), Height = 6,
                        Margin = new Thickness(26, 2, 4, 0), IsHitTestVisible = false };
                    meter.Bind(TodoHoldMeter.ValueProperty, checkbox.GetObservable(HoldToCompleteCheckBox.HoldProgressProperty));
                    meter.Bind(IsVisibleProperty, new Binding(nameof(HoldToCompleteCheckBox.HoldProgress)) {
                        Source = checkbox, Converter = new FuncValueConverter<double, bool>(value => value > 0) });
                    progress = meter;
                }
                else
                {
                    var bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 2, IsHitTestVisible = false, Background = Brushes.Transparent };
                    bar.Bind(ProgressBar.ValueProperty, checkbox.GetObservable(HoldToCompleteCheckBox.HoldProgressProperty));
                    progress = bar;
                }
                var group = new StackPanel(); group.Children.Add(checkbox); group.Children.Add(progress);
                checkbox.ContextMenu = menu;
                control = group;
            }
            else
            {
                control = new Border { Child = text, Padding = new Thickness(8, 3), BorderThickness = new Thickness(2, 0, 0, 0), BorderBrush = PriorityBrush(4) };
            }
            // Preserve the rich text's link actions alongside detail actions.
            if (text.ContextMenu != null)
            {
                foreach (var item in text.ContextMenu.Items.Cast<object>().ToArray()) { text.ContextMenu.Items.Remove(item); menu.Items.Add(item); }
            }
            text.ContextMenu = menu; control.ContextMenu = menu;
            details.Children.Add(control);
        }
        Grid.SetColumn(details, 2); Grid.SetRow(details, 4); Grid.SetColumnSpan(details, 2); container.Children.Add(details);
    }

    private string DetailAccount() => $"{_settings.HabiticaEnabled}|{_settings.HabiticaUserId}|{_settings.HabiticaClientId}";

    private bool DetailParentCurrent(NoteEntry entry) => ObsidianNotesSync.LoadAllNotes(
        _settings.ObsidianVaultFolder, _settings.NotesSubfolder).Count(candidate => candidate == entry) == 1;

    private NoteSyncResult DetailConflict()
    {
        const string message = "This task or detail changed. Reopen the task and try again.";
        ShowError(message); _snapshot = null;
        Dispatcher.UIThread.Post(() => { if (!_closed) ReloadTodos(); });
        return new(false, "", message);
    }

    private NoteSyncResult SaveChildren(NoteEntry entry, IReadOnlyList<HabiticaSubtask> children)
    {
        var mutation = HabiticaIntegration.Current.SetChildren(_settings, entry, children);
        if (!mutation.LocalResult.Success) ShowError(mutation.LocalResult.Message ?? "Could not save the subtask.");
        else if (mutation.SyncWarning != null) ShowError(mutation.SyncWarning);
        _snapshot = null;
        Dispatcher.UIThread.Post(() => { if (!_closed) ReloadTodos(); });
        if (mutation.LocalResult.Success)
        {
            // SetChildren's fallback local write has no task-file change: verify the metadata itself.
            var persisted = HabiticaIntegration.Current.GetDetails(_settings, entry);
            if (persisted.Id == Guid.Empty || !persisted.Children.SequenceEqual(children))
            {
                string message = mutation.SyncWarning ?? "The subtask could not be saved. Try again.";
                ShowError(message);
                return new(false, "", message);
            }
        }
        return mutation.LocalResult;
    }

    private NoteSyncResult ChangeDetail(NoteEntry entry, TodoDetail detail, string? content, bool? completed = null)
    {
        if (!DetailParentCurrent(entry)) return DetailConflict();
        if (detail.Start >= 0)
            return SaveDetail(entry, completed.HasValue ? TodoDetails.SetCompleted(entry.Text, detail, completed.Value)
                : TodoDetails.Replace(entry.Text, detail, content));
        var current = HabiticaIntegration.Current.GetDetails(_settings, entry);
        var child = current.Children.SingleOrDefault(item => item.Id.ToString() == detail.Source);
        if (child == null || child.Text != detail.Text || child.Completed != detail.IsCompleted) return DetailConflict();
        var changed = content == null ? current.Children.Where(item => item.Id != child.Id)
            : current.Children.Select(item => item.Id == child.Id ? item with { Text = content, Completed = completed ?? item.Completed } : item);
        return SaveChildren(entry, changed.ToArray());
    }

    private NoteSyncResult SaveDetail(NoteEntry entry, string text)
    {
        // Load current metadata rather than overwriting priority/due changes made while the editor was open.
        var organization = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder);
        if (!DetailParentCurrent(entry)) return DetailConflict();
        var details = HabiticaIntegration.Current.GetDetails(_settings, entry);
        var mutation = HabiticaIntegration.Current.Apply(_settings, entry, entry with { Text = text }, details.DueAt,
            null, false, () => TodoOrganizationStore.SaveTodo(_settings.ObsidianVaultFolder, entry, text,
                organization.GetPriority(entry), organization.GetDueAt(entry), _settings.NotesSubfolder));
        var result = mutation.LocalResult;
        if (mutation.SyncWarning != null) ShowError(mutation.SyncWarning);
        if (result.Success)
        {
            if (DetailsExpanded(entry)) _expandedDetails.Add(TodoOrganizationStore.GetKey(entry with { Text = text }));
            _nextSyncUtc = DateTime.MinValue;
        }
        else ShowError(result.Message ?? "Could not save task details.");
        _snapshot = null;
        Dispatcher.UIThread.Post(() => { if (!_closed) { ReloadTodos(); _ = SynchronizeTelegramAsync(); } });
        return result;
    }

    private async Task EditDetailAsync(NoteEntry entry, TodoDetailKind kind, TodoDetail? detail = null)
    {
        if (_closed || _deleteInProgress) return;
        if (_taskEditor?.IsVisible == true) { _taskEditor.Activate(); return; }
        CancelInteractions();
        Guid newChildId = Guid.NewGuid(); // A retry must address the same child, including after pending recovery.
        string title = (detail == null ? "Add " : "Edit ") + (kind == TodoDetailKind.Note ? "note" : "subtask");
        _taskEditor = NoteEntryWindow.ForTodoDetail(_settings, title, detail?.Text ?? "", content =>
        {
            if (detail != null) return ChangeDetail(entry, detail, content);
            if (!DetailParentCurrent(entry)) return DetailConflict();
            if (kind == TodoDetailKind.Note) return SaveDetail(entry, TodoDetails.Append(entry.Text, kind, content));
            var current = HabiticaIntegration.Current.GetDetails(_settings, entry);
            var child = new HabiticaSubtask(newChildId, content, false);
            var children = current.Children.Any(item => item.Id == newChildId)
                ? current.Children.Select(item => item.Id == newChildId ? child : item)
                : current.Children.Append(child);
            return SaveChildren(entry, children.ToArray());
        });
        try { await _taskEditor.ShowDialog(this); }
        finally
        {
            _taskEditor = null;
            if (!_closed) { _snapshot = null; ReloadTodos(); _ = SynchronizeTelegramAsync(); }
        }
    }
}
