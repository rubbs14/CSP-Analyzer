using System.Threading.Tasks;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using LiveChartsCore.SkiaSharpView;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelManualOverrideTests
{
    private static MainViewModel MakeViewModel(params int[] expNumbers)
    {
        var vm = new MainViewModel(new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(), new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(), new NullInfoDialogService(), new SettingsService());
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

    // Regression: with no explicit XAxes, LiveChartsCore auto-ranges the X
    // axis to the single shared category (index 0) that all three
    // single-value ColumnSeries plot at, producing a zero-width MinLimit==
    // MaxLimit range that silently renders no bars at all - "Mark as
    // Active/Inactive" looked like a no-op even though the underlying
    // counts were correct. A real (non-zero-width) range fixes it.
    [Fact]
    public void RebuildManualResults_gives_the_chart_a_non_zero_width_x_axis_range()
    {
        MainViewModel vm = MakeViewModel(101, 102);

        vm.MarkActiveCommand.Execute(null);

        Axis axis = Assert.Single(vm.ManualResultsXAxes);
        Assert.NotEqual(axis.MinLimit, axis.MaxLimit);
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
