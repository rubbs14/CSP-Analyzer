using System.IO;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class SettingsServiceTests
{
    private static string TempSettingsPath() =>
        Path.Combine(Directory.CreateTempSubdirectory("csp_settings_test_").FullName, "settings.json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var service = new SettingsService(TempSettingsPath());

        AppSettings settings = service.Load();

        Assert.Equal(new AppSettings().ThemeVariant, settings.ThemeVariant);
        Assert.Equal(new AppSettings().NMin, settings.NMin);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        string path = TempSettingsPath();
        File.WriteAllText(path, "{ not valid json ][");
        var service = new SettingsService(path);

        AppSettings settings = service.Load();

        Assert.Equal("System", settings.ThemeVariant);
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenAbsent()
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("csp_settings_test_").FullName, "nested", "settings.json");
        var service = new SettingsService(path);

        service.Save(new AppSettings());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        string path = TempSettingsPath();
        var service = new SettingsService(path);
        var original = new AppSettings
        {
            ThemeVariant = "Dark",
            BackgroundColorHex = "#FF1B2A38",
            WindowWidth = 1600,
            WindowHeight = 900,
            WindowX = 50,
            WindowY = 75,
            WindowState = "Maximized",
            ReferenceIntensityThreshold = 4321,
            DatasetIntensityThreshold = 1234,
            NMin = 90,
            NMax = 150,
            HMin = 4,
            HMax = 13,
            ManualProbabilityThreshold = 0.62,
            BinsPerArrayDimension = 32,
        };

        service.Save(original);
        AppSettings loaded = service.Load();

        Assert.Equal(original.ThemeVariant, loaded.ThemeVariant);
        Assert.Equal(original.BackgroundColorHex, loaded.BackgroundColorHex);
        Assert.Equal(original.WindowWidth, loaded.WindowWidth);
        Assert.Equal(original.WindowHeight, loaded.WindowHeight);
        Assert.Equal(original.WindowX, loaded.WindowX);
        Assert.Equal(original.WindowY, loaded.WindowY);
        Assert.Equal(original.WindowState, loaded.WindowState);
        Assert.Equal(original.ReferenceIntensityThreshold, loaded.ReferenceIntensityThreshold);
        Assert.Equal(original.DatasetIntensityThreshold, loaded.DatasetIntensityThreshold);
        Assert.Equal(original.NMin, loaded.NMin);
        Assert.Equal(original.NMax, loaded.NMax);
        Assert.Equal(original.HMin, loaded.HMin);
        Assert.Equal(original.HMax, loaded.HMax);
        Assert.Equal(original.ManualProbabilityThreshold, loaded.ManualProbabilityThreshold);
        Assert.Equal(original.BinsPerArrayDimension, loaded.BinsPerArrayDimension);
    }
}
