using System;
using System.IO;
using System.Linq;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public sealed class ProjectCategoryTests
    {
        [Fact]
        public void AppSettings_DefaultProjectCategories_ContainsExpectedPresets()
        {
            var settings = new AppSettings();
            Assert.NotNull(settings.ProjectCategories);
            Assert.Contains("Work", settings.ProjectCategories);
            Assert.Contains("Learning", settings.ProjectCategories);
            Assert.Contains("Health", settings.ProjectCategories);
            Assert.Contains("Hobbies", settings.ProjectCategories);
            Assert.Contains("Chores", settings.ProjectCategories);
            Assert.Contains("Social", settings.ProjectCategories);
            Assert.Contains("Rest", settings.ProjectCategories);
        }

        [Fact]
        public void AppSettings_NormalizeForRuntime_DeduplicatesAndCleansCategories()
        {
            var settings = new AppSettings
            {
                ProjectCategories = ["  Work  ", "work", "Learning", "", "   ", "Study", "Study "]
            };

            settings.NormalizeForRuntime();

            Assert.Equal(3, settings.ProjectCategories.Count);
            Assert.Equal("Work", settings.ProjectCategories[0]);
            Assert.Equal("Learning", settings.ProjectCategories[1]);
            Assert.Equal("Study", settings.ProjectCategories[2]);
        }

        [Fact]
        public void AppSettings_NormalizeForRuntime_RestoresDefaultsWhenEmpty()
        {
            var settings = new AppSettings
            {
                ProjectCategories = ["", "  "]
            };

            settings.NormalizeForRuntime();

            Assert.Equal(AppSettings.DefaultProjectCategories, settings.ProjectCategories);
        }

        [Fact]
        public void AppSettings_AddProjectCategory_AddsNewCategory()
        {
            var settings = new AppSettings();
            bool added = settings.AddProjectCategory("Deep Work");
            Assert.True(added);
            Assert.Contains("Deep Work", settings.ProjectCategories);

            // Adding same category case-insensitively should return false and not duplicate
            bool duplicate = settings.AddProjectCategory("deep work");
            Assert.False(duplicate);
            Assert.Single(settings.ProjectCategories, c => c.Equals("Deep Work", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TimerSession_DefaultsToWorkCategory()
        {
            var session = new TimerSession(1);
            Assert.Equal("Work", session.Category);

            session.Category = "Rest";
            Assert.Equal("Rest", session.Category);
        }

        [Fact]
        public void TimerWorkspaceStore_PersistsAndRestoresCategory()
        {
            var manager = new TimerSessionManager();
            var timer1 = manager.Create();
            timer1.Name = "Programming";
            timer1.Category = "Productive";

            var timer2 = manager.Create();
            timer2.Name = "Nap";
            timer2.Category = "Rest";

            DateTime now = DateTime.UtcNow;
            var snapshot = TimerWorkspaceStore.Capture(manager, now);

            var restoredManager = new TimerSessionManager();
            TimerWorkspaceStore.Restore(snapshot, restoredManager, now, DateTime.Now);

            Assert.Equal(2, restoredManager.Sessions.Count);
            var restored1 = restoredManager.Sessions.First(s => s.Id == timer1.Id);
            var restored2 = restoredManager.Sessions.First(s => s.Id == timer2.Id);

            Assert.Equal("Productive", restored1.Category);
            Assert.Equal("Rest", restored2.Category);
        }

        [Fact]
        public void ProjectTimeHistory_RegisterProjectWithCategory_PersistsCategory()
        {
            var history = new ProjectTimeHistory();
            history.RegisterProject("Sleep", "Rest");
            history.RegisterProject("Coding", "Work");

            Assert.Equal("Rest", history.GetProjectCategory("Sleep"));
            Assert.Equal("Work", history.GetProjectCategory("Coding"));

            var view = history.CreateView(DateTime.UtcNow);
            var sleepView = view.Projects.FirstOrDefault(p => p.Name == "Sleep");
            var codingView = view.Projects.FirstOrDefault(p => p.Name == "Coding");

            Assert.NotNull(sleepView);
            Assert.Equal("Rest", sleepView.Category);
            Assert.NotNull(codingView);
            Assert.Equal("Work", codingView.Category);
        }

        [Fact]
        public void ProjectTimeHistory_SetProjectCategory_UpdatesExistingCategory()
        {
            var history = new ProjectTimeHistory();
            history.RegisterProject("Exercise", "Routine");
            Assert.Equal("Routine", history.GetProjectCategory("Exercise"));

            history.SetProjectCategory("Exercise", "Productive");
            Assert.Equal("Productive", history.GetProjectCategory("Exercise"));

            var view = history.CreateView(DateTime.UtcNow);
            var exerciseView = view.Projects.FirstOrDefault(p => p.Name == "Exercise");
            Assert.NotNull(exerciseView);
            Assert.Equal("Productive", exerciseView.Category);
        }

        [Fact]
        public void ProjectTimeStore_SavesAndRestoresCategoryToDisk()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"project-category-test-{Guid.NewGuid():N}.json");
            try
            {
                var history = new ProjectTimeHistory();
                history.RegisterProject("Antigravity Dev", "Productive");
                history.RegisterProject("Meditation", "Rest");

                var store = new ProjectTimeStore(tempFile);
                bool saved = store.Save(history);
                Assert.True(saved);

                bool loaded = store.TryLoad(out var loadedHistory);
                Assert.True(loaded);
                Assert.NotNull(loadedHistory);

                Assert.Equal("Productive", loadedHistory!.GetProjectCategory("Antigravity Dev"));
                Assert.Equal("Rest", loadedHistory.GetProjectCategory("Meditation"));

                var view = loadedHistory.CreateView(DateTime.UtcNow);
                var devProj = view.Projects.First(p => p.Name == "Antigravity Dev");
                var medProj = view.Projects.First(p => p.Name == "Meditation");

                Assert.Equal("Productive", devProj.Category);
                Assert.Equal("Rest", medProj.Category);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(tempFile + ".bak")) File.Delete(tempFile + ".bak");
            }
        }
    }
}
