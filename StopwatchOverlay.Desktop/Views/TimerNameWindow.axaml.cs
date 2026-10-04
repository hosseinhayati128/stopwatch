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
    private readonly Func<string, string>? _getProjectCategory;
    private readonly Func<string, double>? _getProjectScore;
    private bool _isAddingProject;
    private bool _isAddingCategory;

    public string TimerName { get; private set; } = "";
    public string Category { get; private set; } = "Work";
    public double ScorePerHour { get; private set; } = 1.0;
    public bool WasAccepted { get; private set; }

    public TimerNameWindow() : this("", Array.Empty<string>())
    {
    }

    public TimerNameWindow(
        string currentName,
        IEnumerable<string> projectNames,
        bool isCreatingTimer = false,
        string renameShortcut = "",
        IEnumerable<string>? categories = null,
        string currentCategory = "Work",
        Func<string, string>? getProjectCategory = null,
        double currentScorePerHour = 1.0,
        Func<string, double>? getProjectScore = null)
    {
        InitializeComponent();

        _isCreatingTimer = isCreatingTimer;
        _currentName = (currentName ?? "").Trim();
        _getProjectCategory = getProjectCategory;
        _getProjectScore = getProjectScore;
        Category = string.IsNullOrWhiteSpace(currentCategory) ? "Work" : currentCategory.Trim();
        ScorePerHour = (double.IsNaN(currentScorePerHour) || double.IsInfinity(currentScorePerHour) || currentScorePerHour < 0) ? 1.0 : currentScorePerHour;
        ScorePerHourBox.Value = (decimal)ScorePerHour;

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

        var cats = (categories ?? AppSettings.DefaultProjectCategories)
            .Select(c => c?.Trim() ?? "")
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cats.Count == 0)
            cats.AddRange(AppSettings.DefaultProjectCategories);

        if (!string.IsNullOrWhiteSpace(Category) && !cats.Contains(Category, StringComparer.OrdinalIgnoreCase))
            cats.Insert(0, Category);

        foreach (string cat in cats)
        {
            CategorySelector.Items.Add(new ComboBoxItem
            {
                Content = cat,
                Tag = cat
            });
        }

        SelectInitialCategory();

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

    public static async Task<(bool Accepted, string ProjectName, string Category, double ScorePerHour)> ShowAsync(
        Window? owner,
        string currentName,
        IEnumerable<string> projectNames,
        bool isCreatingTimer = false,
        string renameShortcut = "",
        IEnumerable<string>? categories = null,
        string currentCategory = "Work",
        Func<string, string>? getProjectCategory = null,
        double currentScorePerHour = 1.0,
        Func<string, double>? getProjectScore = null)
    {
        var dialog = new TimerNameWindow(currentName, projectNames, isCreatingTimer, renameShortcut, categories, currentCategory, getProjectCategory, currentScorePerHour, getProjectScore);
        if (owner != null)
        {
            var res = await dialog.ShowDialog<bool>(owner);
            return (res, dialog.TimerName, dialog.Category, dialog.ScorePerHour);
        }

        var tcs = new TaskCompletionSource<(bool, string, string, double)>();
        dialog.Closed += (_, _) => tcs.TrySetResult((dialog.WasAccepted, dialog.TimerName, dialog.Category, dialog.ScorePerHour));
        dialog.Show();
        return await tcs.Task;
    }

    private void SelectInitialCategory()
    {
        if (!string.IsNullOrWhiteSpace(Category))
        {
            foreach (object? candidate in CategorySelector.Items)
            {
                if (candidate is ComboBoxItem item
                    && item.Tag is string cat
                    && string.Equals(cat, Category, StringComparison.OrdinalIgnoreCase))
                {
                    CategorySelector.SelectedItem = item;
                    return;
                }
            }
        }

        if (CategorySelector.Items.Count > 0)
            CategorySelector.SelectedIndex = 0;
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
        {
            ShowNewProjectEditor(false);
        }
        else
        {
            ValidationText.IsVisible = false;
            string selectedProject = (ProjectSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (!string.IsNullOrWhiteSpace(selectedProject))
            {
                if (_getProjectCategory != null)
                {
                    string knownCat = _getProjectCategory(selectedProject);
                    if (!string.IsNullOrWhiteSpace(knownCat))
                    {
                        SetSelectedCategory(knownCat);
                    }
                }
                if (_getProjectScore != null)
                {
                    double knownScore = _getProjectScore(selectedProject);
                    if (knownScore >= 0 && !double.IsNaN(knownScore) && !double.IsInfinity(knownScore))
                    {
                        ScorePerHourBox.Value = (decimal)knownScore;
                    }
                }
            }
        }
    }

    private void SetSelectedCategory(string category)
    {
        foreach (object? candidate in CategorySelector.Items)
        {
            if (candidate is ComboBoxItem item
                && item.Tag is string cat
                && string.Equals(cat, category, StringComparison.OrdinalIgnoreCase))
            {
                CategorySelector.SelectedItem = item;
                return;
            }
        }

        // Category wasn't in the list; add it and select it
        var newItem = new ComboBoxItem { Content = category, Tag = category };
        CategorySelector.Items.Add(newItem);
        CategorySelector.SelectedItem = newItem;
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

    private void CategorySelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NewCategoryPanel == null)
            return;

        if (_isAddingCategory)
            ShowNewCategoryEditor(false);
        else
            CategoryValidationText.IsVisible = false;
    }

    private void AddCategoryButton_Click(object? sender, RoutedEventArgs e)
        => ShowNewCategoryEditor(!_isAddingCategory);

    private void ShowNewCategoryEditor(bool show)
    {
        _isAddingCategory = show;
        NewCategoryPanel.IsVisible = show;
        AddCategoryButton.Content = show ? "×" : "+";
        ToolTip.SetTip(AddCategoryButton, show ? "Cancel adding category" : "Add custom category");
        CategoryValidationText.IsVisible = false;

        if (show)
        {
            Dispatcher.UIThread.Post(() =>
            {
                NewCategoryBox.Focus();
                NewCategoryBox.SelectAll();
            });
        }
        else if (IsLoaded)
        {
            CategorySelector.Focus();
        }
    }

    private void NewCategoryBox_KeyDown(object? sender, KeyEventArgs e)
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
            if (_isAddingCategory)
                ShowNewCategoryEditor(false);
            else if (_isAddingProject)
                ShowNewProjectEditor(false);
            else
                Close(false);
        }
        else if (e.Key == Key.Enter && !_isAddingProject && !_isAddingCategory)
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

        string selectedCategory;
        if (_isAddingCategory)
        {
            selectedCategory = NewCategoryBox.Text?.Trim() ?? "";
            if (selectedCategory.Length == 0)
            {
                CategoryValidationText.Text = "Enter a category name.";
                CategoryValidationText.IsVisible = true;
                NewCategoryBox.Focus();
                return;
            }
            if (selectedCategory.Length > 40)
            {
                selectedCategory = selectedCategory.Substring(0, 40).Trim();
            }
        }
        else
        {
            selectedCategory = (CategorySelector.SelectedItem as ComboBoxItem)?.Tag as string
                ?? (CategorySelector.SelectedItem as string)
                ?? "Work";
        }

        TimerName = selectedName;
        Category = string.IsNullOrWhiteSpace(selectedCategory) ? "Work" : selectedCategory;
        decimal boxVal = ScorePerHourBox.Value ?? 1.0m;
        ScorePerHour = (boxVal < 0) ? 1.0 : (double)boxVal;
        WasAccepted = true;
        Close(true);
    }
}
