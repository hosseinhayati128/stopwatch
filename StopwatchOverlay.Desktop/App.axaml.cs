using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace StopwatchOverlay.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ThemeResources.ApplyApplication(SettingsStore.Load().ThemeMode);
        ConfigureBackgroundValidation();
    }

    internal static void ConfigureBackgroundValidation()
    {
        AppBackgroundCatalog.ImageDimensionReader = (stream, extension) =>
        {
            if (!AppBackgroundCatalog.TryReadImageDimensions(stream, extension.ToLowerInvariant(), out int width, out int height))
                throw new InvalidDataException("The background image header is invalid.");
            return (width, height);
        };
        AppBackgroundCatalog.ImageValidator = (stream, extension) =>
        {
            if (!AppBackgroundCatalog.TryReadImageDimensions(stream, extension.ToLowerInvariant(), out int width, out int height))
                throw new InvalidDataException("The background image header is invalid.");
            stream.Position=0;
            using var validated = DesktopPageAppearance.DecodeBackground(stream, extension);
            return (width, height);
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // By default on desktop, don't exit when the last window closes if tray icon is active
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DesktopPageAppearance.Initialize(SettingsStore.Load());
            desktop.Exit += (_, _) => DesktopPageAppearance.Shutdown();
            desktop.MainWindow = new Views.ControllerWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenControllerClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                desktop.MainWindow = new Views.ControllerWindow();
            }
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }
    }

    private void OnOpenDashboardClicked(object? sender, EventArgs e)
    {
        var dashboard = new Views.ProjectDashboardWindow();
        dashboard.Show();
        dashboard.Activate();
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
