using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: the three charts embedded in Form1 itself (peak-diff bar,
/// probability bar, spectra-overlay scatter) plus the actives/inactives
/// gauges - separate from S10's ResultsWindow charts, which are a
/// different window built from the same run's data. Colors are
/// CSPv2/Form1.cs:100-119's exact ARGB values reordered to SkiaSharp's
/// (r,g,b,a) constructor.
/// </summary>
public partial class MainViewModel
{
    private static readonly SKColor BrokenSpectrumColor = new(254, 132, 132, 5);
    private static readonly SKColor FineSpectrumColor = new(45, 161, 63, 5);
    private static readonly SKColor CheckSpectrumColor = new(204, 204, 204, 25);
    private static readonly SKColor AllSpectraFillColor = new(250, 163, 0, 180);
    private static readonly SKColor CurrentMarkerTextColor = new(255, 255, 255, 200);
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);

    [ObservableProperty]
    private ISeries[] _peakDiffSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _peakDiffXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _peakDiffYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _peakDiffSections = Array.Empty<RectangularSection>();

    [ObservableProperty]
    private LabelVisual[] _peakDiffAnnotations = Array.Empty<LabelVisual>();

    public void BuildPeakDiffChart()
    {
        if (ReferenceSpectrum is null)
        {
            return;
        }

        int[] diffs = DatasetSpectra.Select(s => s.TotReadPeaks - ReferenceSpectrum.TotReadPeaks).ToArray();

        PeakDiffXAxes = new[]
        {
            new Axis
            {
                Name = "Experiment No.",
                LabelsRotation = 30,
                MinLimit = 0,
                MaxLimit = DatasetSpectra.Count,
                Labels = DatasetSpectra.Select(s => s.ExpNumber.ToString()).ToList(),
            },
        };
        PeakDiffYAxes = new[]
        {
            new Axis { Name = "ΔPeaks", MinLimit = -80, MaxLimit = 80 },
        };
        PeakDiffSeries = new ISeries[]
        {
            new ColumnSeries<int> { Name = "ΔPeaks", Values = diffs, Fill = new SolidColorPaint(AllSpectraFillColor) },
        };
        PeakDiffSections = BuildThresholdZoneSections();
        RebuildPeakDiffAnnotations(diffs);
    }

    // Port of CSPv2/Form1.cs:298-334's AxisSection zones: Broken [-80,-40]
    // and [40,80], Check [-45,-25] and [25,45], Safe/Fine [-30,30].
    private static RectangularSection[] BuildThresholdZoneSections() => new[]
    {
        new RectangularSection { Yi = -80, Yj = -40, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = 25, Yj = 45, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = -45, Yj = -25, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = 40, Yj = 80, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = -30, Yj = 30, Fill = new SolidColorPaint(FineSpectrumColor) },
    };

    private void RebuildPeakDiffAnnotations(int[] diffs)
    {
        double center = diffs.Length / 2.0;
        var annotations = new List<LabelVisual>
        {
            Label(center, 15, "Safe range"),
            Label(center, -15, "Safe range"),
            Label(center, 35, "Check PP"),
            Label(center, -35, "Check PP"),
            Label(center, 65, "Broken Spectrum"),
            Label(center, -65, "Broken Spectrum"),
        };

        if (CurrentIndex >= 0 && CurrentIndex < diffs.Length)
        {
            annotations.Add(Label(CurrentIndex, diffs[CurrentIndex], "Current Spectrum"));
        }

        PeakDiffAnnotations = annotations.ToArray();
    }

    private static LabelVisual Label(double x, double y, string text) => new()
    {
        X = x,
        Y = y,
        Text = text,
        TextSize = 10,
        Paint = new SolidColorPaint(CurrentMarkerTextColor),
    };

    [ObservableProperty]
    private ISeries[] _probabilitySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _probabilityXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _probabilityYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _probabilitySections = Array.Empty<RectangularSection>();

    [ObservableProperty]
    private LabelVisual[] _probabilityAnnotations = Array.Empty<LabelVisual>();

    // User-adjustable decision boundary (a slider in MainWindow.axaml) -
    // defaults each run to the backend's own ProbThreshold (the minimum
    // probability among predicted actives, or 0.5 if nothing was
    // classified active - port of CSPv2/Form1.cs's ProbThreshold), but can
    // be dragged to explore other cutoffs. Only affects this window's live
    // view (gauges, actives/inactives filter, current-experiment status) -
    // ResultsWindow/export still report the backend's original fixed
    // classification (SpectrumResult.IsActive), by design: the exported
    // report stays an objective record of what the model actually
    // predicted, separate from interactive what-if exploration here.
    [ObservableProperty]
    private double _manualProbabilityThreshold = 0.5;

    partial void OnManualProbabilityThresholdChanged(double value)
    {
        BuildProbabilityChart();
        BuildGauges();
        RaiseNavigationChanged();
    }

    public bool IsEffectivelyActive(SpectrumResult result) => result.ActivePseudoprobability >= ManualProbabilityThreshold;

    // Port of CSPv2/Form1.cs's ProbThreshold. Public (like the Build*
    // methods) so it's directly testable without going through RunAsync's
    // real-subprocess flow.
    public double ComputeAutoProbabilityThreshold()
    {
        List<double> activeProbs = RunResults.Where(r => r.IsActive).Select(r => r.ActivePseudoprobability).ToList();
        return activeProbs.Count > 0 ? activeProbs.Min() : 0.5;
    }

    public void BuildProbabilityChart()
    {
        double[] probs = DatasetSpectra
            .Select(s => ResultsByExpNumber.TryGetValue(s.ExpNumber, out var r) ? r.ActivePseudoprobability : 0.0)
            .ToArray();

        var xAxis = new Axis
        {
            Name = "Experiment No.",
            LabelsRotation = 30,
            MinLimit = 0,
            MaxLimit = DatasetSpectra.Count,
            Labels = DatasetSpectra.Select(s => s.ExpNumber.ToString()).ToList(),
        };

        // Port of CSPv2/Form1.cs's Axis_RangeChanged zoom-sync hack - the
        // modern LiveChartsCore way is just sharing axes with each other.
        if (PeakDiffXAxes.Length > 0)
        {
            xAxis.SharedWith = new[] { PeakDiffXAxes[0] };
            PeakDiffXAxes[0].SharedWith = new[] { xAxis };
        }

        ProbabilityXAxes = new[] { xAxis };
        ProbabilityYAxes = new[] { new Axis { Name = "Probability", MinLimit = 0, MaxLimit = 1 } };
        ProbabilitySeries = new ISeries[]
        {
            new ColumnSeries<double> { Name = "Probability", Values = probs, Fill = new SolidColorPaint(InactiveAutoColor) },
        };
        ProbabilitySections = new[]
        {
            new RectangularSection { Yi = 0, Yj = 0.35, Fill = new SolidColorPaint(BrokenSpectrumColor) },
            new RectangularSection { Yi = 0.35, Yj = 0.75, Fill = new SolidColorPaint(CheckSpectrumColor) },
            new RectangularSection { Yi = 0.75, Yj = 1, Fill = new SolidColorPaint(FineSpectrumColor) },
            new RectangularSection
            {
                Yi = ManualProbabilityThreshold,
                Yj = ManualProbabilityThreshold,
                Stroke = new SolidColorPaint(ActiveAutoColor) { StrokeThickness = 1.5f },
                Label = "Decision Threshold",
                LabelPaint = new SolidColorPaint(CurrentMarkerTextColor),
                LabelSize = 10,
            },
        };

        if (CurrentIndex >= 0 && CurrentIndex < probs.Length)
        {
            ProbabilityAnnotations = new[] { Label(CurrentIndex, probs[CurrentIndex], "Current Spectrum") };
        }
    }

    [ObservableProperty]
    private ISeries[] _activesGaugeSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _inactivesGaugeSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private int _activesAutoCount;

    [ObservableProperty]
    private int _inactivesAutoCount;

    public void BuildGauges()
    {
        int actives = RunResults.Count(IsEffectivelyActive);
        int inactives = RunResults.Count - actives;
        ActivesAutoCount = actives;
        InactivesAutoCount = inactives;

        ActivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(actives, series =>
            {
                series.Name = "Actives";
                series.Fill = new SolidColorPaint(ActiveAutoColor);
            }));

        InactivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(inactives, series =>
            {
                series.Name = "Inactives";
                series.Fill = new SolidColorPaint(InactiveAutoColor);
            }));
    }

    [ObservableProperty]
    private ISeries[] _overlaySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _overlayXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _overlayYAxes = Array.Empty<Axis>();

    private readonly ScatterSeries<WeightedPoint> _referenceOverlaySeries = new()
    {
        Name = "Reference",
        Fill = new SolidColorPaint(new SKColor(64, 79, 86, 220)),
    };

    private readonly ScatterSeries<WeightedPoint> _currentOverlaySeries = new()
    {
        Name = "Current Experiment",
        Fill = new SolidColorPaint(AllSpectraFillColor),
    };

    private readonly ScatterSeries<WeightedPoint> _activesOverlaySeries = new()
    {
        Name = "Actives",
        Fill = new SolidColorPaint(ActiveAutoColor),
    };

    private readonly ScatterSeries<WeightedPoint> _inactivesOverlaySeries = new()
    {
        Name = "Inactives",
        Fill = new SolidColorPaint(InactiveAutoColor),
    };

    public void BuildOverlayAxes()
    {
        OverlayXAxes = new[] { new Axis { Name = "1H ppm", MinLimit = -HMax, MaxLimit = -HMin, Labeler = value => Math.Abs(value).ToString("0.##") } };
        OverlayYAxes = new[] { new Axis { Name = "15N ppm", MinLimit = -NMax, MaxLimit = -NMin, Labeler = value => Math.Abs(value).ToString("0.##") } };
        OverlaySeries = new ISeries[] { _referenceOverlaySeries, _currentOverlaySeries, _activesOverlaySeries, _inactivesOverlaySeries };
    }

    public void RebuildOverlayPoints()
    {
        _referenceOverlaySeries.Values = ToOverlayPoints(ReferenceSpectrum);
        _currentOverlaySeries.Values = ToOverlayPoints(CurrentSpectrum);
        _activesOverlaySeries.Values = CurrentFilter == ExperimentFilter.Actives ? ToOverlayPoints(CurrentSpectrum) : Array.Empty<WeightedPoint>();
        _inactivesOverlaySeries.Values = CurrentFilter == ExperimentFilter.Inactives ? ToOverlayPoints(CurrentSpectrum) : Array.Empty<WeightedPoint>();
    }

    private static WeightedPoint[] ToOverlayPoints(PeaklistSpectrum? spectrum) =>
        spectrum is null
            ? Array.Empty<WeightedPoint>()
            : spectrum.Peaklist.Select(p => new WeightedPoint(-p.F2, -p.F1, p.Intensity)).ToArray();

    [RelayCommand]
    private void ResetOverlayZoom()
    {
        if (OverlayXAxes.Length == 0)
        {
            return;
        }

        OverlayXAxes[0].MinLimit = -HMax;
        OverlayXAxes[0].MaxLimit = -HMin;
        OverlayYAxes[0].MinLimit = -NMax;
        OverlayYAxes[0].MaxLimit = -NMin;
    }

    [RelayCommand]
    private void FitOverlayZoomToReference()
    {
        if (ReferenceSpectrum is null || ReferenceSpectrum.Peaklist.Count == 0 || OverlayXAxes.Length == 0)
        {
            return;
        }

        OverlayXAxes[0].MinLimit = -(ReferenceSpectrum.Peaklist.Max(p => p.F2) + 0.5);
        OverlayXAxes[0].MaxLimit = -(ReferenceSpectrum.Peaklist.Min(p => p.F2) - 0.5);
        OverlayYAxes[0].MinLimit = -(ReferenceSpectrum.Peaklist.Max(p => p.F1) + 3);
        OverlayYAxes[0].MaxLimit = -(ReferenceSpectrum.Peaklist.Min(p => p.F1) - 3);
    }

    [RelayCommand]
    private void ResetBarChartZoom()
    {
        if (PeakDiffXAxes.Length > 0)
        {
            PeakDiffXAxes[0].MinLimit = 0;
            PeakDiffXAxes[0].MaxLimit = DatasetSpectra.Count;
        }

        if (ProbabilityXAxes.Length > 0)
        {
            ProbabilityXAxes[0].MinLimit = 0;
            ProbabilityXAxes[0].MaxLimit = DatasetSpectra.Count;
        }
    }

    [RelayCommand]
    private void ResetAllZoom()
    {
        ResetOverlayZoom();
        ResetBarChartZoom();
    }
}
