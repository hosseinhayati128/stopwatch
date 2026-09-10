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
        public void TryGetAction_FromWpfKey_MapsToExpectedAction(Key key, ShortcutAction expectedAction)
        {
            bool mapped = ShortcutCommandMode.TryGetAction(key, out ShortcutAction action);
            Assert.True(mapped);
            Assert.Equal(expectedAction, action);
        }

        [Theory]
        [InlineData('a')]
        [InlineData('b')]
        [InlineData('e')]
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
    }
}
