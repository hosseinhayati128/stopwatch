using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace StopwatchOverlay
{
    // Modal editor for the command mode leader shortcut (default Win+F2)
    // and dedicated global shortcuts like ShowActiveOverlay (default Win+Shift+F7).
    public partial class ShortcutsWindow : Window
    {
        private Shortcut _pendingLeader;
        private Shortcut _pendingShowActiveOverlay;
        private Shortcut _pendingOpenController;

        public Shortcut ResultLeader => _pendingLeader;
        public Shortcut ResultShowActiveOverlay => _pendingShowActiveOverlay;
        public Shortcut ResultOpenController => _pendingOpenController;

        // Retained for backward-compatibility with callers expecting a Dictionary.
        public Dictionary<ShortcutAction, Shortcut> Result => new()
        {
            [ShortcutAction.CommandLeader] = _pendingLeader,
            [ShortcutAction.ShowActiveOverlay] = _pendingShowActiveOverlay,
            [ShortcutAction.OpenController] = _pendingOpenController
        };

        public ShortcutsWindow(Shortcut currentLeader, Shortcut? currentShowActiveOverlay = null, Shortcut? currentOpenController = null)
        {
            InitializeComponent();
            _pendingLeader = currentLeader ?? AppSettings.DefaultLeaderShortcut();
            _pendingShowActiveOverlay = currentShowActiveOverlay ?? AppSettings.DefaultShowActiveOverlayShortcut();
            _pendingOpenController = currentOpenController ?? AppSettings.DefaultOpenControllerShortcut();
            RenderAllBoxes();
        }

        public ShortcutsWindow(Dictionary<ShortcutAction, Shortcut> current)
            : this(ExtractLeader(current), ExtractShowActiveOverlay(current), ExtractOpenController(current))
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

        private void RenderAllBoxes()
        {
            RenderLeaderBox();
            RenderShowActiveOverlayBox();
            RenderOpenControllerBox();
        }

        private void RenderLeaderBox()
        {
            if (LeaderShortcutBox == null) return;
            var combo = _pendingLeader != null ? _pendingLeader.Format() : "";
            LeaderShortcutBox.Text = combo.Length > 0 ? combo : "(none)";
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

        private void ShortcutBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox box)
                box.Text = "Press a key combo...";
        }

        private void ShortcutBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox box)
            {
                if (box.Tag as string == "ShowActiveOverlay")
                    RenderShowActiveOverlayBox();
                else if (box.Tag as string == "OpenController")
                    RenderOpenControllerBox();
                else
                    RenderLeaderBox();
            }
        }

        private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox box)
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
            else
            {
                _pendingLeader = shortcut;
                box.Text = _pendingLeader.Format();
            }

            Keyboard.ClearFocus(); // commit visually; user clicks Save to persist
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
                else
                {
                    _pendingLeader = new Shortcut(0, 0); // unbound
                    RenderLeaderBox();
                }
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            _pendingLeader = AppSettings.DefaultLeaderShortcut();
            _pendingShowActiveOverlay = AppSettings.DefaultShowActiveOverlayShortcut();
            _pendingOpenController = AppSettings.DefaultOpenControllerShortcut();
            RenderAllBoxes();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
