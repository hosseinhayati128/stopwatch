using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerNameWindow : Window
{
    private readonly string _currentName;
    private readonly bool _isCreatingTimer;
    private bool _isAddingProject;

    public string TimerName { get; private set; } = "";
    public bool WasAccepted { get; private set; }

    public TimerNameWindow() : this("", Array.Empty<string>())
    {
    }

    public TimerNameWindow(
        string currentName,
        IEnumerable<string> projectNames,
        bool isCreatingTimer = false,
        string renameShortcut = "")
    {
        InitializeComponent();

        _isCreatingTimer = isCreatingTimer;
        _currentName = (currentName ?? "").Trim();
        var projects = (projectNames ?? Enumerable.Empty<string>())
            .Select(name => name?.Trim() ?? "")
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (_currentName.Length > 0
            && !projects.Contains(_currentName, StringComparer.OrdinalIgnoreCase))
            projects.Insert(0, _currentName);

        ProjectSelector.Items.Add(new ComboBoxItem
        {
            Content = "Select a project",
            Tag = null
        });

        foreach (string project in projects)
        {
            ProjectSelector.Items.Add(new ComboBoxItem
            {
                Content = project,
                Tag = project
            });
        }

        if (_isCreatingTimer)
        {
            Title = "Create timer";
            HeadingText.Text = "Choose a project for this timer";
            DescriptionText.Text = "Select an existing project or use + to add a new one.";
            string assignmentHint = string.IsNullOrWhiteSpace(renameShortcut)
                ? "You can assign it later from Timers > Set project."
                : $"You can assign it later with {renameShortcut}.";
            NoProjectHintText.Text = $"Leave ‘Select a project’ selected to create an unnamed timer. {assignmentHint}";
            SaveButton.Content = "Create timer";
        }
        else
        {
            NoProjectHintText.Text = "Choose ‘Select a project’ to make this an unnamed timer.";
            SaveButton.Content = "Apply project";
        }

        SelectInitialProject();
        Opened += (_, _) => ProjectSelector.Focus();
    }

    public static async Task<(bool Accepted, string ProjectName)> ShowAsync(
        Window? owner,
        string currentName,
        IEnumerable<string> projectNames,
        bool isCreatingTimer = false,
        string renameShortcut = "")
    {
        var dialog = new TimerNameWindow(currentName, projectNames, isCreatingTimer, renameShortcut);
        if (owner != null)
        {
            var res = await dialog.ShowDialog<bool>(owner);
            return (res, dialog.TimerName);
        }

        var tcs = new TaskCompletionSource<(bool, string)>();
        dialog.Closed += (_, _) => tcs.TrySetResult((dialog.WasAccepted, dialog.TimerName));
        dialog.Show();
        return await tcs.Task;
    }

    private void SelectInitialProject()
    {
        if (_currentName.Length > 0)
        {
            foreach (object? candidate in ProjectSelector.Items)
            {
                if (candidate is ComboBoxItem item
                    && item.Tag is string project
                    && string.Equals(project, _currentName, StringComparison.OrdinalIgnoreCase))
                {
                    ProjectSelector.SelectedItem = item;
                    return;
                }
            }
        }

        ProjectSelector.SelectedIndex = 0;
    }

    private void ProjectSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NewProjectPanel == null)
            return;

        if (_isAddingProject)
            ShowNewProjectEditor(false);
        else
            ValidationText.IsVisible = false;
    }

    private void AddProjectButton_Click(object? sender, RoutedEventArgs e)
        => ShowNewProjectEditor(!_isAddingProject);

    private void ShowNewProjectEditor(bool show)
    {
        _isAddingProject = show;
        NewProjectPanel.IsVisible = show;
        AddProjectButton.Content = show ? "×" : "+";
        ToolTip.SetTip(AddProjectButton, show ? "Cancel adding project" : "Add new project");
        ValidationText.IsVisible = false;

        if (show)
        {
            Dispatcher.UIThread.Post(() =>
            {
                NewProjectBox.Focus();
                NewProjectBox.SelectAll();
            });
        }
        else if (IsLoaded)
        {
            ProjectSelector.Focus();
        }
    }

    private void NewProjectBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        AcceptSelection();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_isAddingProject)
                ShowNewProjectEditor(false);
            else
                Close(false);
        }
        else if (e.Key == Key.Enter && !_isAddingProject)
        {
            e.Handled = true;
            AcceptSelection();
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(false);

    private void Save_Click(object? sender, RoutedEventArgs e)
        => AcceptSelection();

    private void AcceptSelection()
    {
        string selectedName;
        if (_isAddingProject)
        {
            selectedName = NewProjectBox.Text?.Trim() ?? "";
            if (selectedName.Length == 0)
            {
                ValidationText.Text = "Enter a project name.";
                ValidationText.IsVisible = true;
                NewProjectBox.Focus();
                return;
            }

            if (!ProjectTimeHistory.TryNormalizeProjectName(selectedName, out string? normalizedName))
            {
                ValidationText.Text = "Enter a valid project name.";
                ValidationText.IsVisible = true;
                NewProjectBox.Focus();
                return;
            }

            selectedName = normalizedName!;
        }
        else
        {
            selectedName = (ProjectSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        }

        TimerName = selectedName;
        WasAccepted = true;
        Close(true);
    }
}
