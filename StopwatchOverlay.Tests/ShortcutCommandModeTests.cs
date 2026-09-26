using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class ShortcutCommandModeTests
    {
        [Theory]
        [InlineData(' ', ShortcutAction.StartStop)]
        [InlineData('R', ShortcutAction.Reset)]
        [InlineData('r', ShortcutAction.Reset)]
        [InlineData('O', ShortcutAction.ToggleOverlay)]
        [InlineData('o', ShortcutAction.ToggleOverlay)]
        [InlineData('L', ShortcutAction.Lap)]
        [InlineData('l', ShortcutAction.Lap)]
        [InlineData('C', ShortcutAction.ToggleClock)]
        [InlineData('c', ShortcutAction.ToggleClock)]
        [InlineData('N', ShortcutAction.NewTimer)]
        [InlineData('n', ShortcutAction.NewTimer)]
        [InlineData('T', ShortcutAction.NextTimer)]
        [InlineData('t', ShortcutAction.NextTimer)]
        [InlineData('X', ShortcutAction.CloseTimer)]
        [InlineData('x', ShortcutAction.CloseTimer)]
        [InlineData('P', ShortcutAction.RenameTimer)]
        [InlineData('p', ShortcutAction.RenameTimer)]
        [InlineData('D', ShortcutAction.OpenDashboard)]
        [InlineData('d', ShortcutAction.OpenDashboard)]
        [InlineData('W', ShortcutAction.OpenController)]
        [InlineData('w', ShortcutAction.OpenController)]
        [InlineData('E', ShortcutAction.EditTimer)]
        [InlineData('e', ShortcutAction.EditTimer)]
        [InlineData('U', ShortcutAction.UndoTimerEdit)]
        [InlineData('u', ShortcutAction.UndoTimerEdit)]
        [InlineData('A', ShortcutAction.AddRecord)]
        [InlineData('a', ShortcutAction.AddRecord)]
        [InlineData('S', ShortcutAction.SyncActivityWatch)]
        [InlineData('s', ShortcutAction.SyncActivityWatch)]
        public void TryGetAction_FromChar_MapsToExpectedAction(char keyChar, ShortcutAction expectedAction)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(keyChar, out ShortcutAction action);
            Assert.True(mapped);
            Assert.Equal(expectedAction, action);
        }

        [Theory]
        [InlineData(ShortcutCommandMode.VK_SPACE, ShortcutAction.StartStop)]
        [InlineData(ShortcutCommandMode.VK_KEY_R, ShortcutAction.Reset)]
        [InlineData(ShortcutCommandMode.VK_KEY_O, ShortcutAction.ToggleOverlay)]
        [InlineData(ShortcutCommandMode.VK_KEY_L, ShortcutAction.Lap)]
        [InlineData(ShortcutCommandMode.VK_KEY_C, ShortcutAction.ToggleClock)]
        [InlineData(ShortcutCommandMode.VK_KEY_N, ShortcutAction.NewTimer)]
        [InlineData(ShortcutCommandMode.VK_KEY_T, ShortcutAction.NextTimer)]
        [InlineData(ShortcutCommandMode.VK_KEY_X, ShortcutAction.CloseTimer)]
        [InlineData(ShortcutCommandMode.VK_KEY_P, ShortcutAction.RenameTimer)]
        [InlineData(ShortcutCommandMode.VK_KEY_D, ShortcutAction.OpenDashboard)]
        [InlineData(ShortcutCommandMode.VK_KEY_W, ShortcutAction.OpenController)]
        [InlineData(ShortcutCommandMode.VK_KEY_E, ShortcutAction.EditTimer)]
        [InlineData(ShortcutCommandMode.VK_KEY_U, ShortcutAction.UndoTimerEdit)]
        [InlineData(ShortcutCommandMode.VK_KEY_A, ShortcutAction.AddRecord)]
        [InlineData(ShortcutCommandMode.VK_KEY_S, ShortcutAction.SyncActivityWatch)]
        public void TryGetAction_FromVirtualKey_MapsToExpectedAction(uint vk, ShortcutAction expectedAction)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(vk, out ShortcutAction action);
            Assert.True(mapped);
            Assert.Equal(expectedAction, action);
        }

        [Theory]
        [InlineData(Key.Space, ShortcutAction.StartStop)]
        [InlineData(Key.R, ShortcutAction.Reset)]
        [InlineData(Key.O, ShortcutAction.ToggleOverlay)]
        [InlineData(Key.L, ShortcutAction.Lap)]
        [InlineData(Key.C, ShortcutAction.ToggleClock)]
        [InlineData(Key.N, ShortcutAction.NewTimer)]
        [InlineData(Key.T, ShortcutAction.NextTimer)]
        [InlineData(Key.X, ShortcutAction.CloseTimer)]
        [InlineData(Key.P, ShortcutAction.RenameTimer)]
        [InlineData(Key.D, ShortcutAction.OpenDashboard)]
        [InlineData(Key.W, ShortcutAction.OpenController)]
        [InlineData(Key.E, ShortcutAction.EditTimer)]
        [InlineData(Key.U, ShortcutAction.UndoTimerEdit)]
        [InlineData(Key.A, ShortcutAction.AddRecord)]
        [InlineData(Key.S, ShortcutAction.SyncActivityWatch)]
        public void TryGetAction_FromWpfKey_MapsToExpectedAction(Key key, ShortcutAction expectedAction)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(key, out ShortcutAction action);
            Assert.True(mapped);
            Assert.Equal(expectedAction, action);
        }

        [Theory]
        [InlineData('q')]
        [InlineData('b')]
        [InlineData('f')]
        [InlineData('z')]
        [InlineData('1')]
        [InlineData('9')]
        [InlineData('\r')]
        [InlineData('\t')]
        public void TryGetAction_UnsupportedKeys_ReturnFalse(char unsupportedChar)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(unsupportedChar, out _);
            Assert.False(mapped);
        }

        [Theory]
        [InlineData(0x70u /* VK_F1 */)]
        [InlineData(0x71u /* VK_F2 */)]
        [InlineData(0x72u /* VK_F3 */)]
        [InlineData(0x73u /* VK_F4 */)]
        [InlineData(0x74u /* VK_F5 */)]
        [InlineData(0x61u /* VK_NUMPAD1 */)]
        [InlineData(0x62u /* VK_NUMPAD2 */)]
        public void TryGetAction_FunctionAndNumpadKeys_DoNotMapToAction(uint vk)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(vk, out var action);
            Assert.False(mapped);
            Assert.Equal((ShortcutAction)0, action);
        }

        [Fact]
        public void IsEscape_CorrectlyIdentifiesEscapeKey()
        {
            Assert.True(ShortcutCommandMode.IsEscape(ShortcutCommandMode.VK_ESCAPE));
            Assert.False(ShortcutCommandMode.IsEscape(ShortcutCommandMode.VK_SPACE));
            Assert.False(ShortcutCommandMode.IsEscape(ShortcutCommandMode.VK_KEY_R));
        }

        [Theory]
        [InlineData(ShortcutCommandMode.VK_SHIFT)]
        [InlineData(ShortcutCommandMode.VK_CONTROL)]
        [InlineData(ShortcutCommandMode.VK_MENU)]
        [InlineData(ShortcutCommandMode.VK_LWIN)]
        [InlineData(ShortcutCommandMode.VK_RWIN)]
        [InlineData(ShortcutCommandMode.VK_LSHIFT)]
        [InlineData(ShortcutCommandMode.VK_RSHIFT)]
        [InlineData(ShortcutCommandMode.VK_LCONTROL)]
        [InlineData(ShortcutCommandMode.VK_RCONTROL)]
        [InlineData(ShortcutCommandMode.VK_LMENU)]
        [InlineData(ShortcutCommandMode.VK_RMENU)]
        [InlineData(ShortcutCommandMode.VK_CAPITAL)]
        public void IsModifierKey_IdentifiesModifiers(uint vk)
        {
            Assert.True(ShortcutCommandMode.IsModifierKey(vk));
        }

        [Theory]
        [InlineData(ShortcutCommandMode.VK_SPACE)]
        [InlineData(ShortcutCommandMode.VK_KEY_R)]
        [InlineData(ShortcutCommandMode.VK_KEY_N)]
        [InlineData(ShortcutCommandMode.VK_ESCAPE)]
        public void IsModifierKey_NonModifiers_ReturnFalse(uint vk)
        {
            Assert.False(ShortcutCommandMode.IsModifierKey(vk));
        }

        [Fact]
        public void GuidanceText_ContainsRequiredCommandGuidance()
        {
            Assert.Contains("Space Start/Stop", ShortcutCommandMode.GuidanceLine1);
            Assert.Contains("R Reset", ShortcutCommandMode.GuidanceLine1);
            Assert.Contains("O Overlay", ShortcutCommandMode.GuidanceLine1);
            Assert.Contains("L Lap", ShortcutCommandMode.GuidanceLine1);
            Assert.Contains("C Clock", ShortcutCommandMode.GuidanceLine2);
            Assert.Contains("N New", ShortcutCommandMode.GuidanceLine2);
            Assert.Contains("T Next", ShortcutCommandMode.GuidanceLine2);
            Assert.Contains("X Close", ShortcutCommandMode.GuidanceLine2);
            Assert.Contains("P Project", ShortcutCommandMode.GuidanceLine2);
            Assert.Contains("D Dashboard", ShortcutCommandMode.GuidanceLine2);
        }

        [Fact]
        public void CommandMode_EnterAndCancel_LifecycleBehavesCorrectly()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            bool started = false;
            bool cancelled = false;

            commandMode.ModeStarted += () => started = true;
            commandMode.Cancelled += () => cancelled = true;

            Assert.False(commandMode.IsActive);

            commandMode.Enter();
            Assert.True(commandMode.IsActive);
            Assert.True(started);

            commandMode.Cancel();
            Assert.False(commandMode.IsActive);
            Assert.True(cancelled);
        }

        [Fact]
        public void CommandMode_Dispose_CleansUpState()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var commandMode = new ShortcutCommandMode(dispatcher);

            commandMode.Enter();
            Assert.True(commandMode.IsActive);

            commandMode.Dispose();
            Assert.False(commandMode.IsActive);
        }

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        [Fact]
        public void CommandMode_ContinuesWithHalfSecondTimeout_AfterActionTriggered()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            ShortcutAction? triggeredAction = null;
            commandMode.ActionTriggered += a => triggeredAction = a;

            commandMode.Enter();
            Assert.True(commandMode.IsActive);
            Assert.Equal(0, commandMode.ActionCount);
            Assert.Equal(TimeSpan.FromSeconds(2), commandMode.CurrentTimeoutInterval);

            // Press 'R'
            var resultDown = commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            Assert.Equal((IntPtr)1, resultDown); // Suppressed
            Assert.True(commandMode.IsActive); // Remains active!
            Assert.Equal(1, commandMode.ActionCount);
            Assert.Equal(TimeSpan.FromMilliseconds(500), commandMode.CurrentTimeoutInterval);

            // Release 'R'
            var resultUp = commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);
            Assert.Equal((IntPtr)1, resultUp); // Suppressed
            Assert.True(commandMode.IsActive); // Still active for chaining
        }

        [Fact]
        public void CommandMode_ChainsMultipleActions_Sequentially()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            var actions = new System.Collections.Generic.List<ShortcutAction>();
            commandMode.ActionTriggered += a => actions.Add(a);

            commandMode.Enter();

            // Press 'R' -> 'T' -> Space
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);

            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_T);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_T);

            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_SPACE);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_SPACE);

            Assert.Equal(3, commandMode.ActionCount);
            Assert.True(commandMode.IsActive);
            Assert.Equal(TimeSpan.FromMilliseconds(500), commandMode.CurrentTimeoutInterval);
        }

        [Fact]
        public void CommandMode_EscapeDuringContinuation_CancelsImmediately()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            bool cancelled = false;
            commandMode.Cancelled += () => cancelled = true;

            commandMode.Enter();

            // First command: 'R'
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);
            Assert.True(commandMode.IsActive);

            // Now press Escape
            var resultEsc = commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_ESCAPE);
            Assert.Equal((IntPtr)1, resultEsc); // Suppressed
            Assert.False(commandMode.IsActive); // Cancelled immediately
            dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.True(cancelled);
        }

        [Fact]
        public void CommandMode_UnsupportedKeyDuringContinuation_ExitsAndPassesThrough()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            bool unknown = false;
            commandMode.UnknownCommand += () => unknown = true;

            commandMode.Enter();

            // First command: 'R'
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);
            Assert.True(commandMode.IsActive);

            // Press unsupported key 'Q' (0x51)
            var resultQ = commandMode.ProcessKeyEvent(WM_KEYDOWN, 0x51u);
            Assert.Equal(IntPtr.Zero, resultQ); // Passes through to OS!
            Assert.False(commandMode.IsActive); // Exited command mode
            dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.True(unknown);
        }

        [Fact]
        public void CommandMode_PressingF3_DoesNotTriggerResetAndPassesThrough()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            bool actionFired = false;
            ShortcutAction triggeredAction = 0;
            commandMode.ActionTriggered += action =>
            {
                actionFired = true;
                triggeredAction = action;
            };

            bool unknownFired = false;
            commandMode.UnknownCommand += () => unknownFired = true;

            commandMode.Enter();
            Assert.True(commandMode.IsActive);

            // User presses F3 (0x72) while in command mode
            const uint VK_F3 = 0x72u;
            var result = commandMode.ProcessKeyEvent(WM_KEYDOWN, VK_F3);

            // Must NOT trigger Reset or any action!
            Assert.False(actionFired);
            Assert.Equal((ShortcutAction)0, triggeredAction);

            // Must exit command mode and let the key pass through to the OS
            Assert.False(commandMode.IsActive);
            Assert.Equal(IntPtr.Zero, result);

            dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.True(unknownFired);
        }

        [Fact]
        public void CommandMode_HoldingKey_SuppressesAutoRepeat()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            commandMode.Enter();

            // Initial key down
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            Assert.Equal(1, commandMode.ActionCount);

            // Repeated key down without key up (auto-repeat)
            var resultRepeat = commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            Assert.Equal((IntPtr)1, resultRepeat);
            Assert.Equal(1, commandMode.ActionCount); // Not re-triggered!

            // Key up
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);

            // Subsequent key down after key up is allowed
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            Assert.Equal(2, commandMode.ActionCount);
        }

        [Fact]
        public void CommandMode_ModifierKeys_PassThroughWithoutChangingState()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            commandMode.Enter();

            var resultShift = commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_SHIFT);
            Assert.Equal(IntPtr.Zero, resultShift); // Passes through
            Assert.True(commandMode.IsActive);
            Assert.Equal(0, commandMode.ActionCount);
            Assert.Equal(TimeSpan.FromSeconds(2), commandMode.CurrentTimeoutInterval);
        }

        [Fact]
        public void CommandMode_Exit_ExitsCleanlyWithoutFiringCancelled()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            bool cancelled = false;
            commandMode.Cancelled += () => cancelled = true;

            commandMode.Enter();
            Assert.True(commandMode.IsActive);

            commandMode.Exit();
            Assert.False(commandMode.IsActive);
            Assert.False(cancelled);
        }

        [Fact]
        public void CommandMode_CustomContinuationTimeoutInConstructor_AppliesAfterAction()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            var customTimeout = TimeSpan.FromSeconds(1.2);
            using var commandMode = new ShortcutCommandMode(dispatcher, continuationTimeout: customTimeout);

            Assert.Equal(customTimeout, commandMode.ContinuationTimeout);

            commandMode.Enter();
            Assert.Equal(TimeSpan.FromSeconds(2), commandMode.CurrentTimeoutInterval);

            // Trigger action 'R'
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);

            Assert.True(commandMode.IsActive);
            Assert.Equal(1, commandMode.ActionCount);
            Assert.Equal(customTimeout, commandMode.CurrentTimeoutInterval);
        }

        [Fact]
        public void CommandMode_SetContinuationTimeout_UpdatesTimeoutIntervalDynamically()
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            using var commandMode = new ShortcutCommandMode(dispatcher);

            // Default continuation timeout is 0.5s
            Assert.Equal(TimeSpan.FromMilliseconds(500), commandMode.ContinuationTimeout);

            // Update to 0.8s
            commandMode.SetContinuationTimeout(TimeSpan.FromSeconds(0.8));
            Assert.Equal(TimeSpan.FromSeconds(0.8), commandMode.ContinuationTimeout);

            commandMode.Enter();
            commandMode.ProcessKeyEvent(WM_KEYDOWN, ShortcutCommandMode.VK_KEY_R);
            commandMode.ProcessKeyEvent(WM_KEYUP, ShortcutCommandMode.VK_KEY_R);

            Assert.Equal(TimeSpan.FromSeconds(0.8), commandMode.CurrentTimeoutInterval);

            // Dynamic change while actively chaining
            commandMode.SetContinuationTimeout(TimeSpan.FromSeconds(1.5));
            Assert.Equal(TimeSpan.FromSeconds(1.5), commandMode.CurrentTimeoutInterval);
        }
    }
}
