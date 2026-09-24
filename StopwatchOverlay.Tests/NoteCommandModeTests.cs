using System;
using System.Windows.Input;
using Xunit;

namespace StopwatchOverlay.Tests;

public class NoteCommandModeTests
{
    [Theory]
    [InlineData(NoteCommandMode.VK_KEY_1, NoteCommandAction.AddTodo)]
    [InlineData(NoteCommandMode.VK_NUMPAD1, NoteCommandAction.AddTodo)]
    [InlineData(NoteCommandMode.VK_KEY_T, NoteCommandAction.AddTodo)]
    [InlineData(NoteCommandMode.VK_KEY_2, NoteCommandAction.AddNote)]
    [InlineData(NoteCommandMode.VK_NUMPAD2, NoteCommandAction.AddNote)]
    [InlineData(NoteCommandMode.VK_KEY_N, NoteCommandAction.AddNote)]
    [InlineData(NoteCommandMode.VK_KEY_3, NoteCommandAction.AddReminder)]
    [InlineData(NoteCommandMode.VK_NUMPAD3, NoteCommandAction.AddReminder)]
    [InlineData(NoteCommandMode.VK_KEY_R, NoteCommandAction.AddReminder)]
    [InlineData(NoteCommandMode.VK_KEY_4, NoteCommandAction.ViewNotes)]
    [InlineData(NoteCommandMode.VK_NUMPAD4, NoteCommandAction.ViewNotes)]
    [InlineData(NoteCommandMode.VK_KEY_V, NoteCommandAction.ViewNotes)]
    [InlineData(NoteCommandMode.VK_KEY_O, NoteCommandAction.ViewNotes)]
    public void TryGetAction_VirtualKeys_MapToExpectedAction(uint vk, NoteCommandAction expected)
    {
        bool mapped = NoteCommandMode.TryGetAction(vk, out var action);
        Assert.True(mapped);
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData('1', NoteCommandAction.AddTodo)]
    [InlineData('t', NoteCommandAction.AddTodo)]
    [InlineData('T', NoteCommandAction.AddTodo)]
    [InlineData('2', NoteCommandAction.AddNote)]
    [InlineData('n', NoteCommandAction.AddNote)]
    [InlineData('N', NoteCommandAction.AddNote)]
    [InlineData('3', NoteCommandAction.AddReminder)]
    [InlineData('r', NoteCommandAction.AddReminder)]
    [InlineData('R', NoteCommandAction.AddReminder)]
    [InlineData('4', NoteCommandAction.ViewNotes)]
    [InlineData('v', NoteCommandAction.ViewNotes)]
    [InlineData('V', NoteCommandAction.ViewNotes)]
    [InlineData('o', NoteCommandAction.ViewNotes)]
    [InlineData('O', NoteCommandAction.ViewNotes)]
    public void TryGetAction_Chars_MapToExpectedAction(char c, NoteCommandAction expected)
    {
        bool mapped = NoteCommandMode.TryGetAction(c, out var action);
        Assert.True(mapped);
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData(0x70u /* VK_F1 */)]
    [InlineData(0x71u /* VK_F2 */)]
    [InlineData(0x72u /* VK_F3 */)]
    [InlineData(0x73u /* VK_F4 */)]
    public void TryGetAction_FunctionKeys_ReturnFalse(uint vk)
    {
        bool mapped = NoteCommandMode.TryGetAction(vk, out var action);
        Assert.False(mapped);
        Assert.Equal((NoteCommandAction)0, action);
    }

    [Theory]
    [InlineData('x')]
    [InlineData('a')]
    [InlineData('5')]
    [InlineData(' ')]
    public void TryGetAction_UnsupportedChars_ReturnFalse(char c)
    {
        bool mapped = NoteCommandMode.TryGetAction(c, out _);
        Assert.False(mapped);
    }

    [Fact]
    public void IsEscape_IdentifiesEscapeKey()
    {
        Assert.True(NoteCommandMode.IsEscape(NoteCommandMode.VK_ESCAPE));
        Assert.False(NoteCommandMode.IsEscape(NoteCommandMode.VK_KEY_1));
    }
}
