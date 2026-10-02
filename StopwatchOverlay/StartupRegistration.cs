namespace StopwatchOverlay;

/// <summary>
/// Manages user sign-in launch registration across platforms via PlatformServices.
/// </summary>
public static class StartupRegistration
{
    public static void SetEnabled(bool enabled)
    {
        Platform.PlatformServices.Startup.SetEnabled(enabled);
    }

    public static bool IsEnabled()
    {
        return Platform.PlatformServices.Startup.IsEnabled();
    }
}
