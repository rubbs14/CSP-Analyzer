using CspAnalyzer.Desktop.Models;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultConstructor_MatchesTodaysHardcodedAppDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("System", settings.ThemeVariant);
        Assert.Null(settings.BackgroundColorHex);

        Assert.Equal(1400, settings.WindowWidth);
        Assert.Equal(820, settings.WindowHeight);
        Assert.Null(settings.WindowX);
        Assert.Null(settings.WindowY);
        Assert.Equal("Normal", settings.WindowState);

        Assert.Equal(5000, settings.ReferenceIntensityThreshold);
        Assert.Equal(2000, settings.DatasetIntensityThreshold);
        Assert.Equal(100, settings.NMin);
        Assert.Equal(140, settings.NMax);
        Assert.Equal(5, settings.HMin);
        Assert.Equal(12, settings.HMax);

        Assert.Null(settings.ManualProbabilityThreshold);
        Assert.Null(settings.BinsPerArrayDimension);
    }
}
