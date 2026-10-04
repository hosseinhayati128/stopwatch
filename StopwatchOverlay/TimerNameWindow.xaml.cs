using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace StopwatchOverlay
{
    public partial class TimerNameWindow : Window
    {
        private readonly string _currentName;
        private readonly bool _isCreatingTimer;
        private readonly Func<string, string>? _getProjectCategory;
        private bool _isAddingProject;
        private bool _isAddingCategory;

        public TimerNameWindow(
            string currentName,
            IEnumerable<string> projectNames,
            bool isCreatingTimer = false,
            string renameShortcut = "",
            IEnumerable<string>? categories = null,
            string currentCategory = "Work",
            Func<string, string>? getProjectCategory = null)
        {
            InitializeComponent();

            _isCreatingTimer = isCreatingTimer;
            _currentName = (currentName ?? "").Trim();
            _getProjectCategory = getProjectCategory;
            Category = string.IsNullOrWhiteSpace(currentCategory) ? "Work" : currentCategory.Trim();

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
                DescriptionText.Text =
                    "Select an existing project or use + to add a new one.";
                string assignmentHint = string.IsNullOrWhiteSpace(renameShortcut)
                    ? "You can assign it later from Timers > Set project."
                    : $"You can assign it later with {renameShortcut}.";
                NoProjectHintText.Text =
                    $"Leave ‘Select a project’ selected to create an unnamed timer. {assignmentHint}";
                SaveButton.Content = "Create timer";
            }
            else
            {
                NoProjectHintText.Text =
                    "Choose ‘Select a project’ to make this an unnamed timer.";
                SaveButton.Content = "Apply project";
            }

            SelectInitialProject();
        }

        public string TimerName { get; private set; } = "";
        public string Category { get; private set; } = "Work";
        public bool WasAccepted { get; private set; }

        private void SelectInitialProject()
        {
            if (_currentName.Length > 0)
            {
                foreach (object candidate in ProjectSelector.Items)
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            ProjectSelector.Focus();
            Keyboard.Focus(ProjectSelector);
        }

        private void ProjectSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewProjectPanel == null)
                return;

            if (_isAddingProject)
            {
                ShowNewProjectEditor(false);
            }
            else
            {
                ValidationText.Visibility = Visibility.Collapsed;
                string selectedProject = (ProjectSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                if (!string.IsNullOrWhiteSpace(selectedProject) && _getProjectCategory != null)
                {
                    string knownCat = _getProjectCategory(selectedProject);
                    if (!string.IsNullOrWhiteSpace(knownCat))
                    {
                        SetSelectedCategory(knownCat);
                    }
                }
            }
        }

        private void ProjectSelector_DropDownClosed(object? sender, EventArgs e)
        {
            // SelectionChanged does not fire when the user reselects the current
            // row. Closing the dropdown still makes that choice authoritative.
            if (_isAddingProject && ProjectSelector.SelectedIndex >= 0)
                ShowNewProjectEditor(false);
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
            => ShowNewProjectEditor(!_isAddingProject);

        private void ShowNewProjectEditor(bool show)
        {
            _isAddingProject = show;
            NewProjectPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            AddProjectButton.Content = show ? "×" : "+";
            AddProjectButton.ToolTip = show ? "Cancel adding project" : "Add new project";
            AutomationProperties.SetName(
                AddProjectButton,
                show ? "Cancel adding project" : "Add new project");
            AutomationProperties.SetHelpText(
                AddProjectButton,
                show
                    ? "Close the new project name field"
                    : "Open a field for entering a new project name");
            ValidationText.Visibility = Visibility.Collapsed;

            if (show)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NewProjectBox.Focus();
                    NewProjectBox.SelectAll();
                }), DispatcherPriority.Input);
            }
            else if (IsLoaded)
            {
                ProjectSelector.Focus();
            }
        }

        private void NewProjectBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            AcceptSelection();
        }

        private void CategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewCategoryPanel == null)
                return;

            if (_isAddingCategory)
                ShowNewCategoryEditor(false);
            else
                CategoryValidationText.Visibility = Visibility.Collapsed;
        }

        private void CategorySelector_DropDownClosed(object? sender, EventArgs e)
        {
            if (_isAddingCategory && CategorySelector.SelectedIndex >= 0)
                ShowNewCategoryEditor(false);
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
            => ShowNewCategoryEditor(!_isAddingCategory);

        private void ShowNewCategoryEditor(bool show)
        {
            _isAddingCategory = show;
            NewCategoryPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            AddCategoryButton.Content = show ? "×" : "+";
            AddCategoryButton.ToolTip = show ? "Cancel adding category" : "Add custom category";
            AutomationProperties.SetName(
                AddCategoryButton,
                show ? "Cancel adding category" : "Add custom category");
            AutomationProperties.SetHelpText(
                AddCategoryButton,
                show
                    ? "Close the custom category name field"
                    : "Open a field for entering a custom category name");
            CategoryValidationText.Visibility = Visibility.Collapsed;

            if (show)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NewCategoryBox.Focus();
                    NewCategoryBox.SelectAll();
                }), DispatcherPriority.Input);
            }
            else if (IsLoaded)
            {
                CategorySelector.Focus();
            }
        }

        private void NewCategoryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            AcceptSelection();
        }

        private void ProjectSelector_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || ProjectSelector.IsDropDownOpen)
                return;

            e.Handled = true;
            AcceptSelection();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !ProjectSelector.IsDropDownOpen && !CategorySelector.IsDropDownOpen)
            {
                e.Handled = true;
                if (_isAddingCategory)
                    ShowNewCategoryEditor(false);
                else if (_isAddingProject)
                    ShowNewProjectEditor(false);
                else
                    Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();

        private void Save_Click(object sender, RoutedEventArgs e)
            => AcceptSelection();

        private void AcceptSelection()
        {
            string selectedName;
            if (_isAddingProject)
            {
                selectedName = NewProjectBox.Text.Trim();
                if (selectedName.Length == 0)
                {
                    ValidationText.Text = "Enter a project name.";
                    ValidationText.Visibility = Visibility.Visible;
                    NewProjectBox.Focus();
                    return;
                }

                if (!ProjectTimeHistory.TryNormalizeProjectName(
                        selectedName,
                        out string? normalizedName))
                {
                    ValidationText.Text = "Enter a valid project name.";
                    ValidationText.Visibility = Visibility.Visible;
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
                selectedCategory = NewCategoryBox.Text.Trim();
                if (selectedCategory.Length == 0)
                {
                    CategoryValidationText.Text = "Enter a category name.";
                    CategoryValidationText.Visibility = Visibility.Visible;
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
            WasAccepted = true;
            Close();
        }
    }
}
