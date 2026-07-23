using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
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

    public void BuildGauges()
    {
        int actives = RunResults.Count(r => r.IsActive);
        int inactives = RunResults.Count - actives;

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
}
