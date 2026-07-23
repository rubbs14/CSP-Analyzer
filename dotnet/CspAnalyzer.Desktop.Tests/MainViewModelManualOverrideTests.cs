using System.Threading.Tasks;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelManualOverrideTests
{
    private static MainViewModel MakeViewModel(params int[] expNumbers)
    {
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService());
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = 80 };
        foreach (int exp in expNumbers)
        {
            vm.DatasetSpectra.Add(new PeaklistSpectrum { ExpNumber = exp, DsName = "ds", TotReadPeaks = 80 + exp });
        }
        vm.RaiseNavigationChanged();
        return vm;
    }

    [Fact]
    public void MarkActive_sets_the_current_spectrums_UserSelection_and_updates_counts()
    {
        MainViewModel vm = MakeViewModel(101, 102);

        vm.MarkActiveCommand.Execute(null);

        Assert.Equal("ACTIVE (MAN)", vm.CurrentSpectrum!.UserSelection);
        Assert.Equal(1, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(1, vm.NotSetManualCount);
    }

    [Fact]
    public void MarkInactive_then_ResetManualStatus_returns_to_Not_set()
    {
        MainViewModel vm = MakeViewModel(101);

        vm.MarkInactiveCommand.Execute(null);
        Assert.Equal("INACTIVE (MAN)", vm.CurrentSpectrum!.UserSelection);

        vm.ResetManualStatusCommand.Execute(null);

        Assert.Equal("Not set", vm.CurrentSpectrum!.UserSelection);
        Assert.Equal(0, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(1, vm.NotSetManualCount);
    }

    [Fact]
    public async Task ResetAllManualFlags_resets_every_spectrum_when_confirmed()
    {
        MainViewModel vm = MakeViewModel(101, 102, 103);
        vm.MarkActiveCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.MarkInactiveCommand.Execute(null);

        await vm.ResetAllManualFlagsCommand.ExecuteAsync(null);

        Assert.All(vm.DatasetSpectra, s => Assert.Equal("Not set", s.UserSelection));
        Assert.Equal(0, vm.ActivesManualCount);
        Assert.Equal(0, vm.InactivesManualCount);
        Assert.Equal(3, vm.NotSetManualCount);
    }
}
