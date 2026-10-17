using StopwatchOverlay;

internal static class TodoOrganizationChecks
{
    public static void Run(string directory)
    {
        string vault = Path.Combine(directory, "organization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        try
        {
            var time = new DateTime(2026, 10, 17, 9, 0, 0);
            var normal = new NoteEntry(NoteType.Todo, time, "Normal");
            var urgent = normal with { Text = "Urgent", Timestamp = time.AddMinutes(1) };
            var low = normal with { Text = "Low", Timestamp = time.AddMinutes(2) };
            var done = normal with { Text = "Done", IsCompleted = true };
            var initial = TodoOrganizationStore.Load(vault);
            Check(initial.SortMode == "Priority" && initial.GetPriority(normal) == 3, "Defaults are not Priority/P3.");
            Success(TodoOrganizationStore.SetPriority(vault, urgent, 1));
            Success(TodoOrganizationStore.SetPriority(vault, low, 4));
            Success(TodoOrganizationStore.SetPriority(vault, done, 1));
            var loaded = TodoOrganizationStore.Load(vault);
            Check(loaded.GetPriority(urgent) == 1, "Priority did not survive reload.");
            Order(loaded.Arrange(new[] { low, done, normal, urgent }), urgent, normal, low, done);
            Check(TodoOrganizationStore.GetKey(normal) == TodoOrganizationStore.GetKey(normal with
            { Text = "  Normal\r\n", Timestamp = time.AddSeconds(42), IsCompleted = true }), "Identity depends on completion, seconds, or normalized whitespace.");
            Check(TodoOrganizationStore.GetKey(normal) != TodoOrganizationStore.GetKey(urgent), "Different todos share an identity.");
            Success(TodoOrganizationStore.SetManualOrder(vault, new[] { low, normal, urgent, done }));
            Check(TodoOrganizationStore.Load(vault).SortMode == "Priority", "Saving manual order changed the selected mode.");
            Success(TodoOrganizationStore.SetSortMode(vault, "Manual"));
            loaded = TodoOrganizationStore.Load(vault);
            Order(loaded.Arrange(new[] { done, normal, urgent, low }), low, normal, urgent, done);
            // Partial visible reorders retain hidden tasks in their stored slots.
            Success(TodoOrganizationStore.SetManualOrder(vault, new[] { urgent, low }));
            loaded = TodoOrganizationStore.Load(vault);
            Order(loaded.Arrange(new[] { normal, low, urgent, done }), urgent, normal, low, done);
            Success(TodoOrganizationStore.SetSortMode(vault, "Priority"));
            Order(TodoOrganizationStore.Load(vault).Arrange(new[] { low, normal, urgent }), urgent, normal, low);
            Success(TodoOrganizationStore.SetSortMode(vault, "Manual"));
            Order(TodoOrganizationStore.Load(vault).Arrange(new[] { low, normal, urgent }), urgent, normal, low);
            Check(TodoOrganizationStore.Load(vault).GetPriority(done with { IsCompleted = false }) == 1, "Completed task metadata was lost.");
            Success(TodoOrganizationStore.SetPriority(vault, urgent, 2, "Other"));
            Check(TodoOrganizationStore.Load(vault, "Other").GetPriority(urgent) == 2 && TodoOrganizationStore.Load(vault).GetPriority(urgent) == 1, "Notes folders are not isolated.");
            Check(!TodoOrganizationStore.SetPriority(vault, normal, 0).Success && !TodoOrganizationStore.SetSortMode(vault, "Unknown").Success, "Invalid preferences were accepted.");
            string file = Path.Combine(vault, "Notes", ".stopwatch-todos.json");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Success(TodoOrganizationStore.SetPriority(vault, normal, 2));
                Check(File.GetUnixFileMode(file) == (UnixFileMode.UserRead | UnixFileMode.UserWrite), "Atomic replacement changed permissions.");
            }
            Parallel.For(0, 12, i => Success(TodoOrganizationStore.SetPriority(vault, normal with { Text = "Parallel " + i }, 2)));
            loaded = TodoOrganizationStore.Load(vault);
            Check(Enumerable.Range(0, 12).All(i => loaded.GetPriority(normal with { Text = "Parallel " + i }) == 2), "Concurrent writes lost metadata.");
            foreach (string corrupt in new[] { "{broken", "null", "{}", "{\"Version\":999}", "{\"Version\":1,\"SortMode\":\"Unknown\"}" })
            {
                File.WriteAllText(file, corrupt);
                Check(TodoOrganizationStore.Load(vault).GetPriority(normal) == 3, "Corrupt metadata did not fall back safely.");
                Check(!TodoOrganizationStore.SetPriority(vault, normal, 1).Success && File.ReadAllText(file) == corrupt, "Corrupt metadata was overwritten.");
            }
            Check(!Directory.EnumerateFiles(vault, "*.md", SearchOption.AllDirectories).Any(), "Organization wrote Markdown.");
        }
        finally { Directory.Delete(vault, recursive: true); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Success(NoteSyncResult result) => Check(result.Success, result.Message ?? "Organization update failed.");
    private static void Order(NoteEntry[] actual, params NoteEntry[] expected) => Check(actual.SequenceEqual(expected), "Todo order was incorrect.");
}
