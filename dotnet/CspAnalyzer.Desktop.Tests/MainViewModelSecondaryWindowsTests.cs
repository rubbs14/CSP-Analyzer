using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelSecondaryWindowsTests
{
    private sealed class RecordingAboutWindowService : IAboutWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    private sealed class RecordingShortcutsWindowService : IShortcutsWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    private sealed class RecordingHelpWindowService : IHelpWindowService
    {
        public int ShowCallCount;
        public void Show() => ShowCallCount++;
    }

    [Fact]
    public void OpenAboutWindowCommand_CallsAboutWindowServiceShow()
    {
        var aboutService = new RecordingAboutWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            aboutService, new NullShortcutsWindowService(), new NullHelpWindowService());

        vm.OpenAboutWindowCommand.Execute(null);

        Assert.Equal(1, aboutService.ShowCallCount);
    }

    [Fact]
    public void OpenShortcutsWindowCommand_CallsShortcutsWindowServiceShow()
    {
        var shortcutsService = new RecordingShortcutsWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), shortcutsService, new NullHelpWindowService());

        vm.OpenShortcutsWindowCommand.Execute(null);

        Assert.Equal(1, shortcutsService.ShowCallCount);
    }

    [Fact]
    public void OpenHelpWindowCommand_CallsHelpWindowServiceShow()
    {
        var helpService = new RecordingHelpWindowService();
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
            new NullAboutWindowService(), new NullShortcutsWindowService(), helpService);

        vm.OpenHelpWindowCommand.Execute(null);

        Assert.Equal(1, helpService.ShowCallCount);
    }

    [Fact]
    public void ResetAllImportAndThresholdControlsCommand_ResetsAllSixFieldsToHardcodedDefaults()
    {
        var vm = new MainViewModel
        {
            NMin = 1,
            NMax = 2,
            HMin = 3,
            HMax = 4,
            ReferenceIntensityThreshold = 999,
            DatasetIntensityThreshold = 888,
        };

        vm.ResetAllImportAndThresholdControlsCommand.Execute(null);

        Assert.Equal(100, vm.NMin);
        Assert.Equal(140, vm.NMax);
        Assert.Equal(5, vm.HMin);
        Assert.Equal(12, vm.HMax);
        Assert.Equal(5000, vm.ReferenceIntensityThreshold);
        Assert.Equal(2000, vm.DatasetIntensityThreshold);
    }
}
