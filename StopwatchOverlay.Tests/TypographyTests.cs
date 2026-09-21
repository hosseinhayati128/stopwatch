using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace StopwatchOverlay.Tests;

[Collection("Acanthus visual resources")]
public class TypographyTests
{
    [Fact]
    public void NormalizationAndStoragePreserveDisabledPanelPreferences()
    {
        var settings = new AppSettings();
        settings.Typography.Global.SizePercent = double.NaN;
        settings.Typography.Global.Color = " #abc ";
        settings.Typography.Sections["Notes"] = new TypographyStyle { Enabled = false, SizePercent = 999, Color = "#207f78" };
        settings.NormalizeForRuntime();
        Assert.Equal(100, settings.Typography.Global.SizePercent);
        Assert.Equal("#AABBCC", settings.Typography.Global.Color);
        Assert.Equal(175, settings.Typography.Sections["Notes"].SizePercent);
        Assert.Same(settings.Typography.Global, settings.Typography.Resolve("Notes"));
        string path = Path.Combine(Path.GetTempPath(), "typography-" + Guid.NewGuid() + ".json");
        try
        {
            Assert.True(SettingsStore.Save(settings, path));
            var loaded = SettingsStore.Load(path).Typography;
            Assert.False(loaded.Sections["Notes"].Enabled);
            Assert.Equal("#207F78", loaded.Sections["Notes"].Color);
            Assert.Equal(175, loaded.Sections["Notes"].SizePercent);
            Assert.Equal("#AABBCC", loaded.Global.Color);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ScaleReplacesLocalAndBoundValuesWithoutCompoundingOrDestroyingSources() => RunSta(() =>
    {
        var settings = new TypographySettings();
        settings.Global.SizePercent = 125;
        TypographyManager.Apply(settings);
        var source = new TextBlock { FontSize = 20 };
        var text = new TextBlock { Text = "Heading" };
        text.SetBinding(TextBlock.FontSizeProperty, new Binding("FontSize") { Source = source });
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(25, text.FontSize, 3);
        settings.Global.SizePercent = 150;
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(30, text.FontSize, 3);
        source.FontSize = 24;
        Pump();
        Assert.Equal(36, text.FontSize, 3);
        settings.Global.SizePercent = 100;
        TypographyManager.ApplyElement(text);
        Assert.Equal(24, text.FontSize, 3);
        Assert.True(BindingOperations.IsDataBound(text, TextBlock.FontSizeProperty));
    });

    [Fact]
    public void PanelOverrideAndUncheckingRestoreGlobalAndThemeBrushes() => RunSta(() =>
    {
        var settings = new TypographySettings();
        settings.Normalize();
        settings.Global.SizePercent = 120;
        settings.Global.Color = "#ABCDEF";
        settings.Sections["Notes"] = new TypographyStyle { Enabled = true, SizePercent = 150, Color = "#112233" };
        TypographyManager.Apply(settings);
        var text = new TextBlock { FontSize = 20 };
        text.Resources["TextColor"] = Brushes.Red;
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextColor");
        TypographyManager.SetScope(text, "Notes");
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(30, text.FontSize, 3);
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ((SolidColorBrush)text.Foreground).Color);
        settings.Sections["Notes"].Enabled = false;
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(24, text.FontSize, 3);
        Assert.Equal(Color.FromRgb(0xAB, 0xCD, 0xEF), ((SolidColorBrush)text.Foreground).Color);
        text.Resources["TextColor"] = Brushes.Green;
        settings.Global.Color = TypographySettings.ThemeDefault;
        TypographyManager.ApplyElement(text);
        Assert.Same(Brushes.Green, text.Foreground);
        Assert.Equal("#112233", settings.Sections["Notes"].Color);
    });

    [Fact]
    public void LoadedTextAndThemePresetsUpdateWithoutRecoloringButtonArtwork() => RunSta(() =>
    {
        var settings = new TypographySettings();
        settings.Global.SizePercent = 150;
        settings.Global.Color = TypographySettings.ThemeAccent;
        TypographyManager.Apply(settings);
        var button = new Button { Foreground = Brushes.Gold };
        var text = new TextBlock { Text = "New card", FontSize = 12 };
        text.Resources["AccentBrush"] = Brushes.Teal;
        button.Content = text;
        text.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Pump();
        Assert.Equal(18, text.FontSize, 3);
        Assert.Equal(Colors.Teal, ((SolidColorBrush)text.Foreground).Color);
        Assert.Same(Brushes.Gold, button.Foreground);
        text.Resources["AccentBrush"] = Brushes.Orange;
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(Colors.Orange, ((SolidColorBrush)text.Foreground).Color);
    });

    [Theory]
    [InlineData("#12", false)]
    [InlineData("transparent", false)]
    [InlineData("#11223344", false)]
    [InlineData("#aBc", true)]
    [InlineData("123456", true)]
    public void CustomColorsRequireOpaqueRgb(string text, bool valid)
        => Assert.Equal(valid, TypographySettings.TryHexColor(text, out _));

    [Fact]
    public void EngravedControllerTimerScalesItsFittingLimitAndRestoresPaintedDefault() => RunSta(() =>
    {
        var settings = new TypographySettings();
        TypographyManager.Apply(settings);
        var timer = new Themes.PirateTimerText { Text = "00:05:10" };
        TypographyManager.SetScope(timer, "Controller");
        var host = new Viewbox { Child = timer, StretchDirection = System.Windows.Controls.StretchDirection.DownOnly };
        host.SetBinding(FrameworkElement.MaxHeightProperty, new Binding("MaximumDisplayHeight") { Source = timer });
        host.Measure(new Size(3000, 3000));
        double defaultHeight = host.DesiredSize.Height;
        settings.Global.SizePercent = 150;
        settings.Global.Color = "#207F78";
        TypographyManager.ApplyElement(timer);
        host.Measure(new Size(3000, 3000));
        Assert.Equal(177, host.MaxHeight);
        Assert.True(host.DesiredSize.Height > defaultHeight * 1.4);
        Assert.Equal(Color.FromRgb(0x20, 0x7F, 0x78), Assert.IsType<SolidColorBrush>(timer.TextBrush).Color);
        settings.Global.SizePercent = 100;
        settings.Global.Color = TypographySettings.ThemeDefault;
        TypographyManager.ApplyElement(timer);
        host.Measure(new Size(3000, 3000));
        Assert.Equal(defaultHeight, host.DesiredSize.Height, 3);
        Assert.Null(timer.TextBrush);
    });

    [Fact]
    public void ApplyWindowReachesMeasuredContentBeforeNativeWindowExists() => RunSta(() =>
    {
        var settings = new TypographySettings { Global = new TypographyStyle { SizePercent = 125, Color = "#123456" } };
        TypographyManager.Apply(settings);
        var input = new TextBox { FontSize = 20, Text = "Offscreen editor" };
        var content = new StackPanel();
        content.Children.Add(input);
        var window = new Window { Content = content };
        try
        {
            content.Measure(new Size(400, 250));
            content.Arrange(new Rect(0, 0, 400, 250));
            TypographyManager.ApplyWindow(window);
            Assert.Equal(25, input.FontSize, 3);
            Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), ((SolidColorBrush)input.Foreground).Color);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void InheritedTextAnimatesTracksParentAndRestoresItsOriginalUnsetSource() => RunSta(() =>
    {
        var settings = new TypographySettings { Global = new TypographyStyle { SizePercent = 150, Color = "#123456" } };
        TypographyManager.Apply(settings);
        var text = new TextBlock { Text = "Inherited label", Style = null };
        var parent = new Border { Child = text };
        TextElement.SetFontSize(parent, 20);
        TextElement.SetForeground(parent, Brushes.Red);
        Assert.Equal(BaseValueSource.Inherited, DependencyPropertyHelper.GetValueSource(text, TextBlock.ForegroundProperty).BaseValueSource);
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.True(DependencyPropertyHelper.GetValueSource(text, TextBlock.ForegroundProperty).IsAnimated);
        Assert.Equal(30, text.FontSize, 3);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), ((SolidColorBrush)text.Foreground).Color);
        TextElement.SetFontSize(parent, 24);
        TextElement.SetForeground(parent, Brushes.Green);
        Pump();
        Assert.Equal(36, text.FontSize, 3);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), ((SolidColorBrush)text.Foreground).Color);
        settings.Global.SizePercent = 100;
        settings.Global.Color = TypographySettings.ThemeDefault;
        TypographyManager.ApplyElement(text);
        Assert.Same(DependencyProperty.UnsetValue, text.ReadLocalValue(TextBlock.FontSizeProperty));
        Assert.Same(DependencyProperty.UnsetValue, text.ReadLocalValue(TextBlock.ForegroundProperty));
        Assert.Equal(24, text.FontSize, 3);
        Assert.Same(Brushes.Green, text.Foreground);
        TextElement.SetFontSize(parent, 28);
        TextElement.SetForeground(parent, Brushes.Gold);
        Assert.Equal(28, text.FontSize, 3);
        Assert.Same(Brushes.Gold, text.Foreground);
    });

    [Fact]
    public void ThemePresetRefreshesWhenThemeMutatesTheSameBrushInstance() => RunSta(() =>
    {
        var settings = new TypographySettings { Global = new TypographyStyle { Color = TypographySettings.ThemeAccent } };
        TypographyManager.Apply(settings);
        var text = new TextBlock { Text = "Theme label", Style = null };
        var parent = new Border { Child = text };
        TextElement.SetForeground(parent, Brushes.Black);
        var accent = new SolidColorBrush(Colors.Teal);
        text.Resources["AccentBrush"] = accent;
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(Colors.Teal, ((SolidColorBrush)text.Foreground).Color);
        accent.Color = Colors.Orange;
        TypographyManager.ApplyElement(text);
        Pump();
        Assert.Equal(Colors.Orange, ((SolidColorBrush)text.Foreground).Color);
    });

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            // Cleanup is part of the test result too: an exception escaping an
            // STA worker terminates the test host instead of reporting a failure.
            try { TypographyManager.Apply(new TypographySettings()); }
            catch (Exception ex)
            {
                failure = failure == null ? ex : new AggregateException(failure, ex);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA typography test timed out");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
