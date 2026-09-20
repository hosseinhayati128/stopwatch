using System;
using System.IO;
using System.Linq;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class ObsidianNotesSyncTests : IDisposable
{
    private readonly string _testDirectory;

    public ObsidianNotesSyncTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StopwatchOverlayNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void AppendEntry_CreatesNotesFolderAndFiles()
    {
        DateTime time = new(2026, 9, 20, 14, 30, 0);

        var todoResult = ObsidianNotesSync.AppendEntry(
            _testDirectory,
            NoteType.Todo,
            "Buy groceries",
            "Notes",
            time);

        Assert.True(todoResult.Success);
        string todoPath = Path.Combine(_testDirectory, "Notes", "Todos.md");
        Assert.True(File.Exists(todoPath));

        string todoContent = File.ReadAllText(todoPath);
        Assert.Contains("---", todoContent);
        Assert.Contains("type: todos", todoContent);
        Assert.Contains("# Todos", todoContent);
        Assert.Contains("## 2026-09-20", todoContent);
        Assert.Contains("- [ ] 14:30 Buy groceries", todoContent);

        // Append note
        var noteResult = ObsidianNotesSync.AppendEntry(
            _testDirectory,
            NoteType.Note,
            "Design review thoughts",
            "Notes",
            time);

        Assert.True(noteResult.Success);
        string notePath = Path.Combine(_testDirectory, "Notes", "Notes.md");
        Assert.True(File.Exists(notePath));

        string noteContent = File.ReadAllText(notePath);
        Assert.Contains("## 2026-09-20", noteContent);
        Assert.Contains("- **14:30** Design review thoughts", noteContent);

        // Append reminder
        var reminderResult = ObsidianNotesSync.AppendEntry(
            _testDirectory,
            NoteType.Reminder,
            "Call doctor tomorrow",
            "Notes",
            time);

        Assert.True(reminderResult.Success);
        string reminderPath = Path.Combine(_testDirectory, "Notes", "Reminders.md");
        Assert.True(File.Exists(reminderPath));

        string reminderContent = File.ReadAllText(reminderPath);
        Assert.Contains("## 2026-09-20", reminderContent);
        Assert.Contains("- [ ] 14:30 ⏰ Call doctor tomorrow", reminderContent);
    }

    [Fact]
    public void AppendEntry_MultipleEntriesOnSameDate_GroupUnderSameHeader()
    {
        DateTime time1 = new(2026, 9, 20, 10, 0, 0);
        DateTime time2 = new(2026, 9, 20, 15, 45, 0);

        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Todo, "Task 1", "Notes", time1);
        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Todo, "Task 2", "Notes", time2);

        string todoPath = Path.Combine(_testDirectory, "Notes", "Todos.md");
        string content = File.ReadAllText(todoPath);

        // Only one ## 2026-09-20 header should exist
        int headerCount = content.Split("## 2026-09-20").Length - 1;
        Assert.Equal(1, headerCount);
        Assert.Contains("- [ ] 10:00 Task 1", content);
        Assert.Contains("- [ ] 15:45 Task 2", content);
    }

    [Fact]
    public void AppendEntry_DifferentDates_CreatesSeparateHeaders()
    {
        DateTime day1 = new(2026, 9, 19, 11, 0, 0);
        DateTime day2 = new(2026, 9, 20, 12, 0, 0);

        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Todo, "Old Task", "Notes", day1);
        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Todo, "New Task", "Notes", day2);

        string todoPath = Path.Combine(_testDirectory, "Notes", "Todos.md");
        string content = File.ReadAllText(todoPath);

        Assert.Contains("## 2026-09-19", content);
        Assert.Contains("## 2026-09-20", content);
        Assert.Contains("- [ ] 11:00 Old Task", content);
        Assert.Contains("- [ ] 12:00 New Task", content);
    }

    [Fact]
    public void LoadAllNotes_LoadsAndParsesEntriesCorrectly()
    {
        DateTime time1 = new(2026, 9, 20, 10, 0, 0);
        DateTime time2 = new(2026, 9, 20, 11, 30, 0);
        DateTime time3 = new(2026, 9, 19, 16, 0, 0);

        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Todo, "Task Alpha", "Notes", time1);
        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Note, "Quick Note Beta", "Notes", time2);
        ObsidianNotesSync.AppendEntry(_testDirectory, NoteType.Reminder, "Reminder Gamma", "Notes", time3);

        var allNotes = ObsidianNotesSync.LoadAllNotes(_testDirectory, "Notes");

        Assert.Equal(3, allNotes.Count);

        // Ordered descending by timestamp
        Assert.Equal("Quick Note Beta", allNotes[0].Text);
        Assert.Equal(NoteType.Note, allNotes[0].Type);

        Assert.Equal("Task Alpha", allNotes[1].Text);
        Assert.Equal(NoteType.Todo, allNotes[1].Type);

        Assert.Equal("Reminder Gamma", allNotes[2].Text);
        Assert.Equal(NoteType.Reminder, allNotes[2].Type);
    }
}
