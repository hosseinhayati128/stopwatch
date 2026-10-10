using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StopwatchOverlay
{
    // Modal editor for command mode leader shortcuts (Win+F2, Win+F3),
    // dedicated global shortcuts, and customizable command mode keys for Win+F2 and Win+F3.
    public partial class ShortcutsWindow : Window
    {
        private static readonly (ShortcutAction Action, string Description)[] WinF2Actions =
        [
            (ShortcutAction.StartStop, "Start or stop the active timer"),
            (ShortcutAction.Reset, "Reset the active timer"),
            (ShortcutAction.ToggleOverlay, "Show or hide the active timer overlay"),
            (ShortcutAction.Lap, "Record a lap"),
            (ShortcutAction.ToggleClock, "Toggle between current mode and Clock"),
            (ShortcutAction.NewTimer, "Create a new timer and make it active"),
            (ShortcutAction.NextTimer, "Select the next active timer"),
            (ShortcutAction.CloseTimer, "Close the active timer"),
            (ShortcutAction.RenameTimer, "Select, create, change, or clear project"),
            (ShortcutAction.OpenDashboard, "Open project time dashboard"),
            (ShortcutAction.OpenController, "Open stopwatch controller"),
            (ShortcutAction.EditTimer, "Edit active timer (adjust value, start time, discard records)"),
            (ShortcutAction.UndoTimerEdit, "Undo timer edit"),
            (ShortcutAction.AddRecord, "Add a new completed project record"),
            (ShortcutAction.SyncActivityWatch, "Sync ActivityWatch log to Obsidian now"),
            (ShortcutAction.PeriodicReview, "Open periodic review window"),
            (ShortcutAction.SeparateOverlay, "Separate active timer to dedicated overlay"),
            (ShortcutAction.MergeOverlay, "Merge active separated overlay back into herd"),
            (ShortcutAction.NextSeparatedOverlay, "Switch focus between separated overlays and herd"),
            (ShortcutAction.ShowAllClocks, "Show all clocks to choose directly"),
            (ShortcutAction.ToggleActiveClocks, "Show running clocks bar under overlay"),
        ];

        private static readonly (NoteCommandAction Action, string Description)[] WinF3Actions =
        [
            (NoteCommandAction.AddTodo, "Add a new Todo item"),
            (NoteCommandAction.AddNote, "Add a quick note"),
            (NoteCommandAction.AddReminder, "Add a reminder"),
            (NoteCommandAction.ViewNotes, "Open notes viewer"),
        ];

        private Shortcut _pendingLeader;
        private Shortcut _pendingNoteLeader;
        private Shortcut _pendingShowActiveOverlay;
        private Shortcut _pendingOpenController;
        private double _pendingChainingTimeoutSeconds;
        private Dictionary<ShortcutAction, uint> _pendingCommandModeKeys;
        private Dictionary<NoteCommandAction, uint> _pendingNoteCommandModeKeys;

        private readonly Dictionary<ShortcutAction, TextBox> _winF2TextBoxes = new();
        private readonly Dictionary<NoteCommandAction, TextBox> _winF3TextBoxes = new();

        public Shortcut ResultLeader => _pendingLeader;
        public Shortcut ResultNoteLeader => _pendingNoteLeader;
        public Shortcut ResultShowActiveOverlay => _pendingShowActiveOverlay;
        public Shortcut ResultOpenController => _pendingOpenController;
        public double ResultChainingTimeoutSeconds => _pendingChainingTimeoutSeconds;
        public Dictionary<ShortcutAction, uint> ResultCommandModeKeys => new(_pendingCommandModeKeys);
        public Dictionary<NoteCommandAction, uint> ResultNoteCommandModeKeys => new(_pendingNoteCommandModeKeys);

        // Retained for backward-compatibility with callers expecting a Dictionary.
        public Dictionary<ShortcutAction, Shortcut> Result => new()
        {
            [ShortcutAction.CommandLeader] = _pendingLeader,
            [ShortcutAction.NoteCommandLeader] = _pendingNoteLeader,
            [ShortcutAction.ShowActiveOverlay] = _pendingShowActiveOverlay,
            [ShortcutAction.OpenController] = _pendingOpenController
        };

        public ShortcutsWindow(
            Shortcut currentLeader,
            Shortcut? currentNoteLeader = null,
            Shortcut? currentShowActiveOverlay = null,
            Shortcut? currentOpenController = null,
            double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds,
            Dictionary<ShortcutAction, uint>? commandModeKeys = null,
            Dictionary<NoteCommandAction, uint>? noteCommandModeKeys = null)
        {
            InitializeComponent();
            _pendingLeader = currentLeader ?? AppSettings.DefaultLeaderShortcut();
            _pendingNoteLeader = currentNoteLeader ?? AppSettings.DefaultNoteLeaderShortcut();
            _pendingShowActiveOverlay = currentShowActiveOverlay ?? AppSettings.DefaultShowActiveOverlayShortcut();
            _pendingOpenController = currentOpenController ?? AppSettings.DefaultOpenControllerShortcut();
            _pendingChainingTimeoutSeconds = Math.Clamp(
                currentChainingTimeout,
                AppSettings.MinimumCommandChainingTimeoutSeconds,
                AppSettings.MaximumCommandChainingTimeoutSeconds);

            _pendingCommandModeKeys = commandModeKeys != null
                ? new Dictionary<ShortcutAction, uint>(commandModeKeys)
                : AppSettings.DefaultCommandModeKeys();

            // Sanitize: eliminate Tab for NextSeparatedOverlay
            if (_pendingCommandModeKeys.TryGetValue(ShortcutAction.NextSeparatedOverlay, out var switchVk) && switchVk == ShortcutCommandMap.VK_TAB)
            {
                _pendingCommandModeKeys[ShortcutAction.NextSeparatedOverlay] = ShortcutCommandMap.VK_KEY_J;
            }

            _pendingNoteCommandModeKeys = noteCommandModeKeys != null
                ? new Dictionary<NoteCommandAction, uint>(noteCommandModeKeys)
                : AppSettings.DefaultNoteCommandModeKeys();

            ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
            UpdateChainingTimeoutLabel();

            BuildCommandPanels();
            RenderAllBoxes();
        }

        public ShortcutsWindow(Dictionary<ShortcutAction, Shortcut> current, double currentChainingTimeout = AppSettings.DefaultCommandChainingTimeoutSeconds)
            : this(ExtractLeader(current), ExtractNoteLeader(current), ExtractShowActiveOverlay(current), ExtractOpenController(current), currentChainingTimeout)
        {
        }

        public ShortcutsWindow()
            : this(AppSettings.DefaultLeaderShortcut(), AppSettings.DefaultNoteLeaderShortcut(), AppSettings.DefaultShowActiveOverlayShortcut(), AppSettings.DefaultOpenControllerShortcut())
        {
        }

        private static Shortcut ExtractLeader(Dictionary<ShortcutAction, Shortcut> shortcuts)
        {
            if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.CommandLeader, out var leader))
            {
                return leader;
            }
            return AppSettings.DefaultLeaderShortcut();
        }

        private static Shortcut ExtractNoteLeader(Dictionary<ShortcutAction, Shortcut> shortcuts)
        {
            if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.NoteCommandLeader, out var leader))
            {
                return leader;
            }
            return AppSettings.DefaultNoteLeaderShortcut();
        }

        private static Shortcut ExtractShowActiveOverlay(Dictionary<ShortcutAction, Shortcut> shortcuts)
        {
            if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.ShowActiveOverlay, out var s))
            {
                return s;
            }
            return AppSettings.DefaultShowActiveOverlayShortcut();
        }

        private static Shortcut ExtractOpenController(Dictionary<ShortcutAction, Shortcut> shortcuts)
        {
            if (shortcuts != null && shortcuts.TryGetValue(ShortcutAction.OpenController, out var s))
            {
                return s;
            }
            return AppSettings.DefaultOpenControllerShortcut();
        }

        private void BuildCommandPanels()
        {
            BuildWinF2Panel();
            BuildWinF3Panel();
        }

        private void BuildWinF2Panel()
        {
            WinF2CommandsPanel.Children.Clear();
            _winF2TextBoxes.Clear();

            foreach (var (action, description) in WinF2Actions)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBlock = new TextBlock
                {
                    Text = description,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(textBlock, 0);

                var textBox = new TextBox
                {
                    IsReadOnly = true,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Tag = action,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                textBox.GotFocus += CommandKeyBox_GotFocus;
                textBox.LostFocus += CommandKeyBox_LostFocus;
                textBox.PreviewKeyDown += CommandKeyBox_PreviewKeyDown;
                Grid.SetColumn(textBox, 1);
                _winF2TextBoxes[action] = textBox;

                var clearButton = new Button
                {
                    Content = "Clear",
                    Tag = action,
                    Style = (Style)FindResource("ModernButton"),
                    Padding = new Thickness(8, 2, 8, 2)
                };
                clearButton.Click += ClearWinF2CommandKey_Click;
                Grid.SetColumn(clearButton, 2);

                grid.Children.Add(textBlock);
                grid.Children.Add(textBox);
                grid.Children.Add(clearButton);

                WinF2CommandsPanel.Children.Add(grid);
            }
        }

        private void BuildWinF3Panel()
        {
            WinF3CommandsPanel.Children.Clear();
            _winF3TextBoxes.Clear();

            foreach (var (action, description) in WinF3Actions)
            {
                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBlock = new TextBlock
                {
                    Text = description,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(textBlock, 0);

                var textBox = new TextBox
                {
                    IsReadOnly = true,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Tag = action,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                textBox.GotFocus += CommandKeyBox_GotFocus;
                textBox.LostFocus += CommandKeyBox_LostFocus;
                textBox.PreviewKeyDown += CommandKeyBox_PreviewKeyDown;
                Grid.SetColumn(textBox, 1);
                _winF3TextBoxes[action] = textBox;

                var clearButton = new Button
                {
                    Content = "Clear",
                    Tag = action,
                    Style = (Style)FindResource("ModernButton"),
                    Padding = new Thickness(8, 2, 8, 2)
                };
                clearButton.Click += ClearWinF3CommandKey_Click;
                Grid.SetColumn(clearButton, 2);

                grid.Children.Add(textBlock);
                grid.Children.Add(textBox);
                grid.Children.Add(clearButton);

                WinF3CommandsPanel.Children.Add(grid);
            }
        }

        private void RenderAllBoxes()
        {
            RenderLeaderBox();
            RenderNoteLeaderBox();
            RenderShowActiveOverlayBox();
            RenderOpenControllerBox();
            RenderCommandModeBoxes();
        }

        private void RenderLeaderBox()
        {
            if (LeaderShortcutBox == null) return;
            var combo = _pendingLeader != null ? _pendingLeader.Format() : "";
            LeaderShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
        }

        private void RenderNoteLeaderBox()
        {
            if (NoteLeaderShortcutBox == null) return;
            var combo = _pendingNoteLeader != null ? _pendingNoteLeader.Format() : "";
            NoteLeaderShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
        }

        private void RenderShowActiveOverlayBox()
        {
            if (ShowActiveOverlayShortcutBox == null) return;
            var combo = _pendingShowActiveOverlay != null ? _pendingShowActiveOverlay.Format() : "";
            ShowActiveOverlayShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
        }

        private void RenderOpenControllerBox()
        {
            if (OpenControllerShortcutBox == null) return;
            var combo = _pendingOpenController != null ? _pendingOpenController.Format() : "";
            OpenControllerShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
        }

        private void RenderCommandModeBoxes()
        {
            foreach (var (action, box) in _winF2TextBoxes)
            {
                if (_pendingCommandModeKeys.TryGetValue(action, out var vk) && vk != 0)
                {
                    box.Text = Shortcut.FormatKeyName(vk);
                }
                else
                {
                    box.Text = "(none)";
                }
            }

            foreach (var (action, box) in _winF3TextBoxes)
            {
                if (_pendingNoteCommandModeKeys.TryGetValue(action, out var vk) && vk != 0)
                {
                    box.Text = Shortcut.FormatKeyName(vk);
                }
                else
                {
                    box.Text = "(none)";
                }
            }
        }

        private void ShortcutBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
                box.Text = "Press a key combo...";
        }

        private void ShortcutBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                if (box.Tag as string == "ShowActiveOverlay")
                    RenderShowActiveOverlayBox();
                else if (box.Tag as string == "OpenController")
                    RenderOpenControllerBox();
                else if (box.Tag as string == "NoteLeader")
                    RenderNoteLeaderBox();
                else
                    RenderLeaderBox();
            }
        }

        private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            e.Handled = true;

            // Alt combos arrive as Key.System; the real key is in SystemKey.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore presses that are only a modifier — wait for a real key.
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
                return;

            uint mods = 0;
            var m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Control) != 0) mods |= Shortcut.MOD_CONTROL;
            if ((m & ModifierKeys.Alt) != 0) mods |= Shortcut.MOD_ALT;
            if ((m & ModifierKeys.Shift) != 0) mods |= Shortcut.MOD_SHIFT;
            if ((m & ModifierKeys.Windows) != 0) mods |= Shortcut.MOD_WIN;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            var shortcut = new Shortcut(mods, vk);

            if (box.Tag as string == "ShowActiveOverlay")
            {
                _pendingShowActiveOverlay = shortcut;
                box.Text = _pendingShowActiveOverlay.Format();
            }
            else if (box.Tag as string == "OpenController")
            {
                _pendingOpenController = shortcut;
                box.Text = _pendingOpenController.Format();
            }
            else if (box.Tag as string == "NoteLeader")
            {
                _pendingNoteLeader = shortcut;
                box.Text = _pendingNoteLeader.Format();
            }
            else
            {
                _pendingLeader = shortcut;
                box.Text = _pendingLeader.Format();
            }

            Keyboard.ClearFocus(); // commit visually; user clicks Save to persist
        }

        private void CommandKeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                box.Text = "Press key...";
            }
        }

        private void CommandKeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                if (box.Tag is ShortcutAction action)
                {
                    uint vk = _pendingCommandModeKeys.TryGetValue(action, out var val) ? val : 0;
                    box.Text = vk != 0 ? Shortcut.FormatKeyName(vk) : "(none)";
                }
                else if (box.Tag is NoteCommandAction noteAction)
                {
                    uint vk = _pendingNoteCommandModeKeys.TryGetValue(noteAction, out var val) ? val : 0;
                    box.Text = vk != 0 ? Shortcut.FormatKeyName(vk) : "(none)";
                }
            }
        }

        private void CommandKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier-only key presses
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
                return;

            if (key == Key.Escape)
            {
                SetWarning(box.Tag, "Escape is reserved for canceling command mode.");
                return;
            }

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            // Normalize numpad digits (0x60 - 0x69) to regular digits (0x30 - 0x39)
            if (vk >= 0x60 && vk <= 0x69)
            {
                vk = vk - 0x60 + 0x30;
            }

            if (box.Tag is ShortcutAction action)
            {
                // Check if key is already assigned to another Win+F2 action
                foreach (var (otherAction, otherVk) in _pendingCommandModeKeys)
                {
                    if (otherAction != action && otherVk != 0)
                    {
                        uint normalizedOther = (otherVk >= 0x60 && otherVk <= 0x69) ? (otherVk - 0x60 + 0x30) : otherVk;
                        if (normalizedOther == vk)
                        {
                            SetWarning(action, $"Key '{Shortcut.FormatKeyName(vk)}' is already assigned to '{AppSettings.FormatActionName(otherAction)}'. Duplicate keys are not allowed.");
                            return;
                        }
                    }
                }

                ClearWarning(action);
                _pendingCommandModeKeys[action] = vk;
                box.Text = Shortcut.FormatKeyName(vk);
                Keyboard.ClearFocus();
            }
            else if (box.Tag is NoteCommandAction noteAction)
            {
                // Check if key is already assigned to another Win+F3 action
                foreach (var (otherAction, otherVk) in _pendingNoteCommandModeKeys)
                {
                    if (otherAction != noteAction && otherVk != 0)
                    {
                        uint normalizedOther = (otherVk >= 0x60 && otherVk <= 0x69) ? (otherVk - 0x60 + 0x30) : otherVk;
                        if (normalizedOther == vk)
                        {
                            SetWarning(noteAction, $"Key '{Shortcut.FormatKeyName(vk)}' is already assigned to '{AppSettings.FormatNoteActionName(otherAction)}'. Duplicate keys are not allowed.");
                            return;
                        }
                    }
                }

                ClearWarning(noteAction);
                _pendingNoteCommandModeKeys[noteAction] = vk;
                box.Text = Shortcut.FormatKeyName(vk);
                Keyboard.ClearFocus();
            }
        }

        private void SetWarning(object? tag, string message)
        {
            if (tag is ShortcutAction)
            {
                WinF2ValidationWarning.Text = message;
                WinF2ValidationWarningBorder.Visibility = Visibility.Visible;
            }
            else
            {
                WinF3ValidationWarning.Text = message;
                WinF3ValidationWarningBorder.Visibility = Visibility.Visible;
            }
        }

        private void ClearWarning(object? tag)
        {
            if (tag is ShortcutAction)
            {
                WinF2ValidationWarningBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                WinF3ValidationWarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearWinF2CommandKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ShortcutAction action)
            {
                _pendingCommandModeKeys[action] = 0;
                if (_winF2TextBoxes.TryGetValue(action, out var box))
                {
                    box.Text = "(none)";
                }
                ClearWarning(action);
            }
        }

        private void ClearWinF3CommandKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is NoteCommandAction action)
            {
                _pendingNoteCommandModeKeys[action] = 0;
                if (_winF3TextBoxes.TryGetValue(action, out var box))
                {
                    box.Text = "(none)";
                }
                ClearWarning(action);
            }
        }

        private void ClearShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag as string == "ShowActiveOverlay")
                {
                    _pendingShowActiveOverlay = new Shortcut(0, 0); // unbound
                    RenderShowActiveOverlayBox();
                }
                else if (fe.Tag as string == "OpenController")
                {
                    _pendingOpenController = new Shortcut(0, 0); // unbound
                    RenderOpenControllerBox();
                }
                else if (fe.Tag as string == "NoteLeader")
                {
                    _pendingNoteLeader = new Shortcut(0, 0); // unbound
                    RenderNoteLeaderBox();
                }
                else
                {
                    _pendingLeader = new Shortcut(0, 0); // unbound
                    RenderLeaderBox();
                }
            }
        }

        private void ChainingTimeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _pendingChainingTimeoutSeconds = Math.Round(ChainingTimeoutSlider.Value, 1);
            UpdateChainingTimeoutLabel();
        }

        private void UpdateChainingTimeoutLabel()
        {
            if (ChainingTimeoutText != null)
                ChainingTimeoutText.Text = $"{_pendingChainingTimeoutSeconds:0.0} s";
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            _pendingLeader = AppSettings.DefaultLeaderShortcut();
            _pendingNoteLeader = AppSettings.DefaultNoteLeaderShortcut();
            _pendingShowActiveOverlay = AppSettings.DefaultShowActiveOverlayShortcut();
            _pendingOpenController = AppSettings.DefaultOpenControllerShortcut();
            _pendingChainingTimeoutSeconds = AppSettings.DefaultCommandChainingTimeoutSeconds;
            _pendingCommandModeKeys = AppSettings.DefaultCommandModeKeys();
            _pendingNoteCommandModeKeys = AppSettings.DefaultNoteCommandModeKeys();

            ChainingTimeoutSlider.Value = _pendingChainingTimeoutSeconds;
            UpdateChainingTimeoutLabel();

            WinF2ValidationWarningBorder.Visibility = Visibility.Collapsed;
            WinF3ValidationWarningBorder.Visibility = Visibility.Collapsed;

            RenderAllBoxes();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSettings.ValidateCommandModeKeys(_pendingCommandModeKeys, out var f2Error))
            {
                WinF2ValidationWarning.Text = f2Error;
                WinF2ValidationWarningBorder.Visibility = Visibility.Visible;
                TabWinF2.IsSelected = true;
                return;
            }

            if (!AppSettings.ValidateNoteCommandModeKeys(_pendingNoteCommandModeKeys, out var f3Error))
            {
                WinF3ValidationWarning.Text = f3Error;
                WinF3ValidationWarningBorder.Visibility = Visibility.Visible;
                TabWinF3.IsSelected = true;
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
