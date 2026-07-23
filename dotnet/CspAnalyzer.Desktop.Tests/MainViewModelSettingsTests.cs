using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelSettingsTests
{
    [Fact]
    public void CurrentSettings_ReflectsLiveViewModelState()
    {
        var vm = new MainViewModel
        {
            NMin = 55,
            NMax = 160,
            HMin = 3,
            HMax = 15,
            ReferenceIntensityThreshold = 4000,
            DatasetIntensityThreshold = 1500,
            BinsPerArrayDimension = 24,
        };

        AppSettings settings = vm.CurrentSettings();

        Assert.Equal(55, settings.NMin);
        Assert.Equal(160, settings.NMax);
        Assert.Equal(3, settings.HMin);
        Assert.Equal(15, settings.HMax);
        Assert.Equal(4000, settings.ReferenceIntensityThreshold);
        Assert.Equal(1500, settings.DatasetIntensityThreshold);
        Assert.Equal(24, settings.BinsPerArrayDimension);
    }

    [Fact]
    public void ApplySettings_OverwritesFilterFieldsFromSettings()
    {
        var vm = new MainViewModel();
        var settings = new AppSettings
        {
            NMin = 10,
            NMax = 20,
            HMin = 1,
            HMax = 2,
            ReferenceIntensityThreshold = 999,
            DatasetIntensityThreshold = 888,
            BinsPerArrayDimension = 16,
        };

        vm.ApplySettings(settings);

        Assert.Equal(10, vm.NMin);
        Assert.Equal(20, vm.NMax);
        Assert.Equal(1, vm.HMin);
        Assert.Equal(2, vm.HMax);
        Assert.Equal(999, vm.ReferenceIntensityThreshold);
        Assert.Equal(888, vm.DatasetIntensityThreshold);
        Assert.Equal(16, vm.BinsPerArrayDimension);
    }

    [Fact]
    public void ApplySettings_WithManualProbabilityThreshold_SetsProperty()
    {
        var vm = new MainViewModel();
        var settings = new AppSettings { ManualProbabilityThreshold = 0.72 };

        vm.ApplySettings(settings);

        Assert.Equal(0.72, vm.ManualProbabilityThreshold);
    }

    [Fact]
    public void ApplySettings_WithNullManualProbabilityThreshold_LeavesExistingValueUnchanged()
    {
        var vm = new MainViewModel();
        double before = vm.ManualProbabilityThreshold;
        var settings = new AppSettings { ManualProbabilityThreshold = null };

        vm.ApplySettings(settings);

        Assert.Equal(before, vm.ManualProbabilityThreshold);
    }

    [Fact]
    public void ResetImportControlsCommand_ResetsToHardcodedDefaults_RegardlessOfAppliedSettings()
    {
        var vm = new MainViewModel();
        vm.ApplySettings(new AppSettings { NMin = 1, NMax = 2, HMin = 3, HMax = 4 });

        vm.ResetImportControlsCommand.Execute(null);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
        Assert.Equal(5, vm.HMin);
        Assert.Equal(12, vm.HMax);
    }
}
