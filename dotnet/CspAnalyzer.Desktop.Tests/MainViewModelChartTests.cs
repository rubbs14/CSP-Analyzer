using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.ViewModels;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainViewModelChartTests
{
    private static MainViewModel MakeViewModel(int refTotReadPeaks, params int[] datasetTotReadPeaks)
    {
        var vm = new MainViewModel();
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = refTotReadPeaks };
        for (int i = 0; i < datasetTotReadPeaks.Length; i++)
        {
            vm.DatasetSpectra.Add(new PeaklistSpectrum { ExpNumber = 100 + i, DsName = "ds", TotReadPeaks = datasetTotReadPeaks[i] });
        }
        return vm;
    }

    [Fact]
    public void BuildPeakDiffChart_produces_one_bar_per_experiment_valued_at_TotReadPeaks_minus_reference()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40, 80);

        vm.BuildPeakDiffChart();

        var series = Assert.Single(vm.PeakDiffSeries);
        var column = Assert.IsType<LiveChartsCore.SkiaSharpView.ColumnSeries<int>>(series);
        Assert.Equal(new[] { 5, -40, 0 }, column.Values);
    }

    [Fact]
    public void BuildPeakDiffChart_sets_five_threshold_zone_sections()
    {
        MainViewModel vm = MakeViewModel(80, 85);

        vm.BuildPeakDiffChart();

        Assert.Equal(5, vm.PeakDiffSections.Length);
    }
}
