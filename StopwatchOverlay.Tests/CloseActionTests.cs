using System.Text.Json;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class CloseActionTests
{
    [Theory]
    [InlineData(null, CloseActionChoice.Ask)]
    [InlineData("", CloseActionChoice.Ask)]
    [InlineData("   ", CloseActionChoice.Ask)]
    [InlineData("unknown", CloseActionChoice.Ask)]
    [InlineData("Ask", CloseActionChoice.Ask)]
    [InlineData("ask", CloseActionChoice.Ask)]
    [InlineData("  Ask  ", CloseActionChoice.Ask)]
    [InlineData("Minimize", CloseActionChoice.Minimize)]
    [InlineData("minimize", CloseActionChoice.Minimize)]
    [InlineData("Minimize to tray", CloseActionChoice.Minimize)]
    [InlineData("Minimize to system tray", CloseActionChoice.Minimize)]
    [InlineData("Exit", CloseActionChoice.Exit)]
    [InlineData("exit", CloseActionChoice.Exit)]
    [InlineData("Exit application", CloseActionChoice.Exit)]
    [InlineData("Close completely", CloseActionChoice.Exit)]
    public void Normalize_ReturnsExpectedChoice(string? input, string expected)
    {
        Assert.Equal(expected, CloseActionChoice.Normalize(input));
    }

    [Fact]
    public void ChoicesList_ContainsStableOptions()
    {
        Assert.Contains(CloseActionChoice.Ask, CloseActionChoice.All);
        Assert.Contains(CloseActionChoice.Minimize, CloseActionChoice.All);
        Assert.Contains(CloseActionChoice.Exit, CloseActionChoice.All);
    }

    [Fact]
    public void AppSettings_CloseAction_DefaultsToAsk()
    {
        var settings = new AppSettings();
        Assert.Equal(CloseActionChoice.Ask, settings.CloseAction);
    }

    [Fact]
    public void AppSettings_CloseAction_RoundTripsJsonSerialization()
    {
        var settings = new AppSettings
        {
            CloseAction = CloseActionChoice.Minimize
        };

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(CloseActionChoice.Minimize, deserialized.CloseAction);
    }

    [Fact]
    public void AppSettings_NormalizeForRuntime_NormalizesCloseAction()
    {
        var settings = new AppSettings
        {
            CloseAction = "  close completely  "
        };
        settings.NormalizeForRuntime();

        Assert.Equal(CloseActionChoice.Exit, settings.CloseAction);
    }

    [Fact]
    public void AppSettings_NormalizeForRuntime_PreservesAskAndMinimize()
    {
        var settingsAsk = new AppSettings { CloseAction = "ask" };
        settingsAsk.NormalizeForRuntime();
        Assert.Equal(CloseActionChoice.Ask, settingsAsk.CloseAction);

        var settingsMinimize = new AppSettings { CloseAction = "minimize to tray" };
        settingsMinimize.NormalizeForRuntime();
        Assert.Equal(CloseActionChoice.Minimize, settingsMinimize.CloseAction);
    }
}
