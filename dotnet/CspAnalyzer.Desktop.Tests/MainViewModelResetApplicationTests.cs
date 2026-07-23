using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelResetApplicationTests
{
    private sealed class DecliningConfirmDialogService : IConfirmDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
    }

    private static MainViewModel MakeFullyLoadedViewModel(IConfirmDialogService confirmDialog, SettingsService settingsService)
    {
        var vm = new MainViewModel(
            new NullFilePickerService(), new NullResultsWindowService(), confirmDialog,
            new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
            new NullInfoDialogService(), settingsService);

        vm.ReferenceSpectrum = new PeaklistSpectrum
        {
            ExpNumber = 1,
            DsName = "ref",
            Peaklist = { new Peak { Number = 1, F1 = 120, F2 = 8, Intensity = 9000 } },
        };
        vm.DatasetSpectra.Add(new PeaklistSpectrum
        {
            ExpNumber = 2,
            DsName = "ds",
            Peaklist = { new Peak { Number = 1, F1 = 121, F2 = 8, Intensity = 9000 } },
            UserSelection = "ACTIVE (MAN)",
        });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 2, IsActive = true, ActivePseudoprobability = 0.9 });
        vm.CorruptedPeaklistExperiments.Add("3");
        vm.OutOfImportRangeExperiments.Add("4");
        vm.NMin = 1;
        vm.NMax = 2;
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public async Task ResetApplicationCommand_declined_leaves_everything_loaded()
    {
        var vm = MakeFullyLoadedViewModel(new DecliningConfirmDialogService(), new SettingsService(Path.GetTempFileName()));

        await ((IAsyncRelayCommand)vm.ResetApplicationCommand).ExecuteAsync(null);

        Assert.True(vm.IsReferenceLoaded);
        Assert.Single(vm.DatasetSpectra);
        Assert.Single(vm.RunResults);
    }

    [Fact]
    public async Task ResetApplicationCommand_confirmed_clears_loaded_data_and_reapplies_persisted_settings()
    {
        string settingsPath = Path.Combine(Directory.CreateTempSubdirectory("csp_reset_test_").FullName, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        settingsService.Save(new AppSettings { NMin = 77, NMax = 88, HMin = 3, HMax = 9, ReferenceIntensityThreshold = 1111, DatasetIntensityThreshold = 2222 });

        var vm = MakeFullyLoadedViewModel(new NullConfirmDialogService(), settingsService);

        await ((IAsyncRelayCommand)vm.ResetApplicationCommand).ExecuteAsync(null);

        Assert.False(vm.IsReferenceLoaded);
        Assert.Empty(vm.DatasetSpectra);
        Assert.Empty(vm.RunResults);
        Assert.Empty(vm.CorruptedPeaklistExperiments);
        Assert.Empty(vm.OutOfImportRangeExperiments);
        Assert.Equal("No Reference Loaded", vm.ReferenceStatusText);
        Assert.Equal("No Dataset Loaded", vm.DatasetStatusText);
        Assert.Equal(77, vm.NMin);
        Assert.Equal(88, vm.NMax);
        Assert.Equal(3, vm.HMin);
        Assert.Equal(9, vm.HMax);
        Assert.Equal(1111, vm.ReferenceIntensityThreshold);
        Assert.Equal(2222, vm.DatasetIntensityThreshold);
        Assert.False(vm.RunCommand.CanExecute(null));
        Assert.False(vm.OpenResultsWindowCommand.CanExecute(null));
    }
}
