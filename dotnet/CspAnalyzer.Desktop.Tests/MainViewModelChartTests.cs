using System.Linq;
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
    public void BuildPeakDiffChart_sets_six_labeled_threshold_zone_sections()
    {
        MainViewModel vm = MakeViewModel(80, 85);

        vm.BuildPeakDiffChart();

        Assert.Equal(12, vm.PeakDiffSections.Length);
        Assert.Equal(2, vm.PeakDiffSections.Count(s => s.Label == "Broken Spectrum"));
        Assert.Equal(2, vm.PeakDiffSections.Count(s => s.Label == "Check PP"));
        Assert.Equal(2, vm.PeakDiffSections.Count(s => s.Label == "Safe range"));
        Assert.All(
            vm.PeakDiffSections.Where(s => !string.IsNullOrEmpty(s.Label)),
            s => Assert.True(s.Xi.HasValue && s.Xj.HasValue));
    }

    [Fact]
    public void BuildProbabilityChart_produces_one_bar_per_experiment_from_RunResults()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 100, IsActive = true, ActivePseudoprobability = 0.91 });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = false, ActivePseudoprobability = 0.1 });

        vm.BuildProbabilityChart();

        Assert.Equal(2, vm.ProbabilitySeries.Length);
        var inactive = Assert.IsType<LiveChartsCore.SkiaSharpView.ColumnSeries<double?>>(vm.ProbabilitySeries[0]);
        var active = Assert.IsType<LiveChartsCore.SkiaSharpView.ColumnSeries<double?>>(vm.ProbabilitySeries[1]);
        Assert.Equal(new double?[] { null, 0.1 }, inactive.Values);
        Assert.Equal(new double?[] { 0.91, null }, active.Values);
    }

    [Fact]
    public void ComputeAutoProbabilityThreshold_returns_the_minimum_active_probability()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40, 90);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 100, IsActive = true, ActivePseudoprobability = 0.91 });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = true, ActivePseudoprobability = 0.62 });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 102, IsActive = false, ActivePseudoprobability = 0.1 });

        Assert.Equal(0.62, vm.ComputeAutoProbabilityThreshold());
    }

    [Fact]
    public void ComputeAutoProbabilityThreshold_falls_back_to_0_5_when_nothing_is_active()
    {
        MainViewModel vm = MakeViewModel(80, 85);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 100, IsActive = false, ActivePseudoprobability = 0.1 });

        Assert.Equal(0.5, vm.ComputeAutoProbabilityThreshold());
    }

    [Fact]
    public void BuildProbabilityChart_draws_the_decision_threshold_line_at_ManualProbabilityThreshold()
    {
        MainViewModel vm = MakeViewModel(80, 85);
        vm.ManualProbabilityThreshold = 0.73;

        var thresholdLine = vm.ProbabilitySections.Single(s => s.Label == "Decision Threshold");
        Assert.Equal(0.73, thresholdLine.Yi);
        Assert.Equal(0.73, thresholdLine.Yj);
    }

    [Fact]
    public void Dragging_ManualProbabilityThreshold_reclassifies_gauges_live()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 100, IsActive = true, ActivePseudoprobability = 0.6 });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = false, ActivePseudoprobability = 0.4 });
        vm.BuildGauges();

        // Raise the threshold above both probabilities - both become inactive.
        vm.ManualProbabilityThreshold = 0.9;

        var actives = (LiveChartsCore.SkiaSharpView.PieSeries<LiveChartsCore.Defaults.ObservableValue>)vm.ActivesGaugeSeries[0];
        Assert.Equal(0, actives.Values.Single().Value!.Value);
    }

    [Fact]
    public void BuildProbabilityChart_shares_its_X_axis_with_the_peak_diff_chart_for_zoom_sync()
    {
        MainViewModel vm = MakeViewModel(80, 85);
        vm.BuildPeakDiffChart();

        vm.BuildProbabilityChart();

        Assert.Contains(vm.PeakDiffXAxes[0], vm.ProbabilityXAxes[0].SharedWith);
        Assert.Contains(vm.ProbabilityXAxes[0], vm.PeakDiffXAxes[0].SharedWith);
    }

    [Fact]
    public void BuildGauges_produces_a_solid_gauge_series_for_actives_and_inactives()
    {
        MainViewModel vm = MakeViewModel(80, 85, 40, 90);
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 100, IsActive = true });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 101, IsActive = false });
        vm.RunResults.Add(new SpectrumResult { ExpNumber = 102, IsActive = true });

        vm.BuildGauges();

        Assert.NotEmpty(vm.ActivesGaugeSeries);
        Assert.NotEmpty(vm.InactivesGaugeSeries);
    }

    [Fact]
    public void BuildOverlayAxes_ranges_match_the_inverted_import_bounds()
    {
        var vm = new MainViewModel();
        vm.NMin = 100;
        vm.NMax = 140;
        vm.HMin = 5;
        vm.HMax = 12;

        vm.BuildOverlayAxes();

        Assert.Equal(-12, vm.OverlayXAxes[0].MinLimit);
        Assert.Equal(-5, vm.OverlayXAxes[0].MaxLimit);
        Assert.Equal(-140, vm.OverlayYAxes[0].MinLimit);
        Assert.Equal(-100, vm.OverlayYAxes[0].MaxLimit);
    }

    [Fact]
    public void BuildOverlayAxes_labels_render_as_positive_ppm_despite_inverted_limits()
    {
        var vm = new MainViewModel();
        vm.NMin = 100;
        vm.NMax = 140;
        vm.HMin = 5;
        vm.HMax = 12;

        vm.BuildOverlayAxes();

        Assert.NotNull(vm.OverlayXAxes[0].Labeler);
        Assert.NotNull(vm.OverlayYAxes[0].Labeler);
        Assert.Equal("12", vm.OverlayXAxes[0].Labeler!(-12));
        Assert.Equal("5", vm.OverlayXAxes[0].Labeler!(-5));
        Assert.Equal("140", vm.OverlayYAxes[0].Labeler!(-140));
        Assert.Equal("100", vm.OverlayYAxes[0].Labeler!(-100));
    }

    [Fact]
    public void RebuildOverlayPoints_plots_the_current_spectrums_peaks_inverted()
    {
        var vm = new MainViewModel();
        vm.BuildOverlayAxes();
        vm.ReferenceSpectrum = new PeaklistSpectrum { ExpNumber = 1, DsName = "ref", TotReadPeaks = 1 };
        vm.DatasetSpectra.Add(new PeaklistSpectrum
        {
            ExpNumber = 101,
            DsName = "ds",
            Peaklist = { new Peak { Number = 1, F1 = 120.0, F2 = 8.0, Intensity = 9000 } },
        });
        vm.RaiseNavigationChanged();

        var current = (LiveChartsCore.SkiaSharpView.ScatterSeries<LiveChartsCore.Defaults.WeightedPoint>)vm.OverlaySeries[1];
        var point = Assert.Single(current.Values);
        Assert.Equal(-8.0, point.X);
        Assert.Equal(-120.0, point.Y);
        Assert.Equal(9000.0, point.Weight);
    }
}
