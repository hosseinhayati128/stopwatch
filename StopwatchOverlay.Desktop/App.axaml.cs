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

        AppBackgroundCatalog.ImageDimensionReader = (stream, _) =>
        {
            using var bmp = new Bitmap(stream);
            return (bmp.PixelSize.Width, bmp.PixelSize.Height);
        };
        AppBackgroundCatalog.ImageValidator = (stream, _) =>
        {
            using var bmp = new Bitmap(stream);
            return (bmp.PixelSize.Width, bmp.PixelSize.Height);
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // By default on desktop, don't exit when the last window closes if tray icon is active
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new Views.MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnOpenControllerClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                desktop.MainWindow = new Views.MainWindow();
            }
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }
    }

    private void OnOpenDashboardClicked(object? sender, EventArgs e)
    {
        // Placeholder for Dashboard window activation in Phase 4
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
