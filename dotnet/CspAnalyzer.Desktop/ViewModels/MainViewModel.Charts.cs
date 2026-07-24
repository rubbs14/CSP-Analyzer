using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
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
    // Semaphore scheme: normal is a neutral gray (not "safe green" - a
    // quiet zone shouldn't draw the eye), check is amber/warning, broken is
    // red/danger - the escalating zones are what should stand out.
    private static readonly SKColor BrokenSpectrumColor = new(220, 53, 69, 32);
    private static readonly SKColor FineSpectrumColor = new(158, 158, 158, 8);
    private static readonly SKColor CheckSpectrumColor = new(255, 193, 7, 22);
    private static readonly SKColor AllSpectraFillColor = new(250, 163, 0, 180);
    private static readonly SKColor CurrentMarkerTextColor = new(255, 255, 255, 200);

    // Same hues as the (near-transparent) zone Fill colors above, but
    // opaque - used for each zone's Label text so it visually matches the
    // band it names instead of every label sharing one plain white color.
    private static readonly SKColor BrokenSpectrumTextColor = new(220, 53, 69, 255);
    private static readonly SKColor FineSpectrumTextColor = new(220, 220, 220, 255);
    private static readonly SKColor CheckSpectrumTextColor = new(255, 193, 7, 255);
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);

    // Legacy CSPv2 Form1.cs:1637-1663's exact solidGaugeActives/
    // solidGaugeInactives colors (LiveCharts.WinForms.SolidGauge's
    // FromColor/ToColor arc gradient over a shared semi-transparent gray
    // GaugeBackground) - WPF Color.FromArgb(a,r,g,b) reordered to
    // SkiaSharp's SKColor(r,g,b,a) constructor.
    private static readonly SKColor GaugeTrackColor = new(76, 76, 76, 76);
    private static readonly SKColor ActiveGaugeFromColor = new(50, 205, 50, 255); // Colors.LimeGreen
    private static readonly SKColor ActiveGaugeToColor = new(29, 195, 88, 76);
    private static readonly SKColor InactiveGaugeFromColor = new(245, 245, 245, 255); // Colors.WhiteSmoke
    private static readonly SKColor InactiveGaugeToColor = new(200, 0, 0, 50);
    private static readonly SKColor GridSeparatorColor = new(255, 255, 255, 30);

    // Axis.Name (the "ΔPeaks"/"Experiment No."-style axis title) has its
    // own NameTextSize/NamePaint, separate from TextSize/LabelsPaint which
    // only style the tick labels - left unset, it renders at the theme's
    // default size (much larger than the TextSize=7 tick labels) in the
    // default text color, indistinguishable from any other text.
    private static readonly SKColor AxisNameColor = new(100, 181, 246, 255);
    private const double AxisNameTextSize = 11;

    [ObservableProperty]
    private ISeries[] _peakDiffSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _peakDiffXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _peakDiffYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _peakDiffSections = Array.Empty<RectangularSection>();

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
                NameTextSize = AxisNameTextSize,
                NamePaint = new SolidColorPaint(AxisNameColor),
                LabelsRotation = 30,
                TextSize = 7,
                LabelsDensity = 0,
                MinStep = 1,
                ForceStepToMin = true,
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(GridSeparatorColor),
                MinLimit = 0,
                MaxLimit = DatasetSpectra.Count,
                Labels = DatasetSpectra.Select(s => s.ExpNumber.ToString()).ToList(),
            },
        };
        PeakDiffYAxes = new[]
        {
            new Axis { Name = "ΔPeaks", NameTextSize = AxisNameTextSize, NamePaint = new SolidColorPaint(AxisNameColor), TextSize = 7, MinLimit = -80, MaxLimit = 80 },
        };
        // Bars colored per-experiment by the same |ΔPeaks| semaphore
        // thresholds as the background zones, mirroring the probability
        // chart's active/inactive masking below (three same-length,
        // index-aligned series, each null everywhere but its own zone).
        int?[] safeDiffs = diffs.Select(d => Math.Abs(d) <= 15 ? (int?)d : null).ToArray();
        int?[] checkDiffs = diffs.Select(d => Math.Abs(d) > 15 && Math.Abs(d) <= 30 ? (int?)d : null).ToArray();
        int?[] brokenDiffs = diffs.Select(d => Math.Abs(d) > 30 ? (int?)d : null).ToArray();
        PeakDiffSeries = new ISeries[]
        {
            new ColumnSeries<int?> { Name = "Safe range", Values = safeDiffs, Fill = new SolidColorPaint(FineSpectrumTextColor) },
            new ColumnSeries<int?> { Name = "Check PP", Values = checkDiffs, Fill = new SolidColorPaint(CheckSpectrumTextColor) },
            new ColumnSeries<int?> { Name = "Broken Spectrum", Values = brokenDiffs, Fill = new SolidColorPaint(BrokenSpectrumTextColor) },
        };
        PeakDiffSections = BuildThresholdZoneSections()
            .Concat(BuildCurrentSpectrumMarkerSections())
            .ToArray();
    }

    // Thresholds on |ΔPeaks|: <=15 safe, <=30 check, >30 broken - specular
    // around the X axis. Background-only (Fill, no Label): LiveChartsCore
    // mispositions RectangularSection.Label once more than one finite-Yi/Yj
    // section shares an X window (confirmed by measuring rendered label
    // pixel positions against their Yi/Yj - each text landed one zone-slot
    // off from where its own section was drawn), so the zone captions are
    // rendered as a static XAML overlay instead (MainWindow.axaml, over
    // PeakDiffChart) which is immune to that and, being screen-space, also
    // can't drift under zoom/pan.
    private static RectangularSection[] BuildThresholdZoneSections() => new[]
    {
        new RectangularSection { Yi = -80, Yj = -30, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = -30, Yj = -15, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = -15, Yj = 0, Fill = new SolidColorPaint(FineSpectrumColor) },
        new RectangularSection { Yi = 0, Yj = 15, Fill = new SolidColorPaint(FineSpectrumColor) },
        new RectangularSection { Yi = 15, Yj = 30, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = 30, Yj = 80, Fill = new SolidColorPaint(BrokenSpectrumColor) },
    };

    // Marks which experiment the player/overlay are currently on, in both
    // the peak-diff and probability charts - originally a LabelVisual
    // pinned to a fixed data-space (X, Y) point, which drifted out of
    // registration under zoom/pan just like the zone labels did, and
    // never updated on navigation since nothing called its rebuild outside
    // the initial chart build. A RectangularSection anchored purely to X
    // (Yi/Yj left unset, so it spans the chart's full visible height) is
    // both zoom-stable and trivial to keep in sync: RaiseNavigationChanged
    // recomputes it on every Next/Previous/GoTo.
    private RectangularSection[] BuildCurrentSpectrumMarkerSections()
    {
        // The chart's X axis always spans the FULL DatasetSpectra list, but
        // CurrentIndex is an index into CurrentView - the actives/inactives
        // filtered subset (S10b). With a filter active those two diverge
        // (e.g. CurrentIndex 0..7 for 8 filtered actives vs. their real,
        // scattered positions among 64 total experiments), so the marker
        // must be placed at the current spectrum's index within
        // DatasetSpectra, not at CurrentIndex directly.
        int fullIndex = CurrentSpectrum is null ? -1 : DatasetSpectra.IndexOf(CurrentSpectrum);
        if (fullIndex < 0)
        {
            return Array.Empty<RectangularSection>();
        }

        double halfWidth = Math.Max(DatasetSpectra.Count * 0.01, 0.5);
        return new[]
        {
            new RectangularSection
            {
                Xi = fullIndex - halfWidth,
                Xj = fullIndex + halfWidth,
                Stroke = new SolidColorPaint(CurrentMarkerTextColor) { StrokeThickness = 1.5f },
                Label = "Current Spectrum",
                LabelPaint = new SolidColorPaint(CurrentMarkerTextColor),
                LabelSize = 10,
            },
        };
    }

    // Called from RaiseNavigationChanged so the marker actually tracks
    // Next/Previous/GoTo - the zone/threshold sections don't depend on
    // CurrentIndex, so recomputing them here alongside the marker is cheap
    // and avoids a separate "just the marker changed" code path.
    public void RebuildCurrentSpectrumMarkers()
    {
        if (PeakDiffXAxes.Length > 0)
        {
            PeakDiffSections = BuildThresholdZoneSections()
                .Concat(BuildCurrentSpectrumMarkerSections())
                .ToArray();
        }

        if (ProbabilityXAxes.Length > 0)
        {
            ProbabilitySections = BuildProbabilityStaticSections()
                .Concat(BuildCurrentSpectrumMarkerSections())
                .ToArray();
        }
    }

    [ObservableProperty]
    private ISeries[] _probabilitySeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _probabilityXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _probabilityYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private RectangularSection[] _probabilitySections = Array.Empty<RectangularSection>();

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
            NameTextSize = AxisNameTextSize,
            NamePaint = new SolidColorPaint(AxisNameColor),
            LabelsRotation = 30,
            TextSize = 7,
            LabelsDensity = 0,
            MinStep = 1,
            ForceStepToMin = true,
            ShowSeparatorLines = true,
            SeparatorsPaint = new SolidColorPaint(GridSeparatorColor),
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
        ProbabilityYAxes = new[] { new Axis { Name = "Probability", NameTextSize = AxisNameTextSize, NamePaint = new SolidColorPaint(AxisNameColor), TextSize = 7, MinLimit = 0, MaxLimit = 1 } };

        // Each bar colored by its own active/inactive classification (like
        // legacy's per-bar coloring) rather than one flat color for every
        // bar - two same-length, index-aligned series with nulls at the
        // indices that belong to the other category, so each renders only
        // its own bars without a zero-height column at the rest.
        double?[] activeProbs = probs.Select(p => p >= ManualProbabilityThreshold ? (double?)p : null).ToArray();
        double?[] inactiveProbs = probs.Select(p => p < ManualProbabilityThreshold ? (double?)p : null).ToArray();
        ProbabilitySeries = new ISeries[]
        {
            new ColumnSeries<double?> { Name = "Inactive", Values = inactiveProbs, Fill = new SolidColorPaint(InactiveAutoColor) },
            new ColumnSeries<double?> { Name = "Active", Values = activeProbs, Fill = new SolidColorPaint(ActiveAutoColor) },
        };
        ProbabilitySections = BuildProbabilityStaticSections()
            .Concat(BuildCurrentSpectrumMarkerSections())
            .ToArray();
    }

    private RectangularSection[] BuildProbabilityStaticSections() => new[]
    {
        new RectangularSection { Yi = 0, Yj = 0.35, Fill = new SolidColorPaint(BrokenSpectrumColor) },
        new RectangularSection { Yi = 0.35, Yj = 0.75, Fill = new SolidColorPaint(CheckSpectrumColor) },
        new RectangularSection { Yi = 0.75, Yj = 1, Fill = new SolidColorPaint(FineSpectrumColor) },
        new RectangularSection
        {
            Yi = ManualProbabilityThreshold,
            Yj = ManualProbabilityThreshold,
            Stroke = new SolidColorPaint(ActiveAutoColor) { StrokeThickness = 1.5f },
        },
        new RectangularSection
        {
            // Label on a second, narrow, centered section rather than
            // the full-width threshold line itself, so the text sits
            // centered instead of pinned to the line's left edge (same
            // fix as the peak-diff chart's zone labels).
            Yi = ManualProbabilityThreshold,
            Yj = ManualProbabilityThreshold,
            Xi = DatasetSpectra.Count / 2.0 - Math.Max(DatasetSpectra.Count * 0.15, 1),
            Xj = DatasetSpectra.Count / 2.0 + Math.Max(DatasetSpectra.Count * 0.15, 1),
            Label = "Decision Threshold",
            LabelPaint = new SolidColorPaint(CurrentMarkerTextColor),
            LabelSize = 10,
        },
    };

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

        // BuildSolidGauge's default builder also draws the value as a
        // built-in data label centered on the arc - suppressed here since
        // MainWindow.axaml overlays its own (correctly positioned below the
        // arc, matching the 0/max labels) and the two were rendering on top
        // of each other.
        ActivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(actives, series =>
            {
                series.Name = "Actives";
                series.Fill = new LinearGradientPaint(ActiveGaugeFromColor, ActiveGaugeToColor);
                series.DataLabelsPaint = null;
            }));
        ((PieSeries<ObservableValue>)ActivesGaugeSeries[1]).Fill = new SolidColorPaint(GaugeTrackColor);

        InactivesGaugeSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(inactives, series =>
            {
                series.Name = "Inactives";
                series.Fill = new LinearGradientPaint(InactiveGaugeFromColor, InactiveGaugeToColor);
                series.DataLabelsPaint = null;
            }));
        ((PieSeries<ObservableValue>)InactivesGaugeSeries[1]).Fill = new SolidColorPaint(GaugeTrackColor);
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
        OverlayXAxes = new[]
        {
            new Axis
            {
                Name = "1H ppm",
                NameTextSize = AxisNameTextSize,
                NamePaint = new SolidColorPaint(AxisNameColor),
                TextSize = 9,
                MinLimit = -HMax,
                MaxLimit = -HMin,
                Labeler = value => Math.Abs(value).ToString("0.##"),
                // Y already shows separator lines by default - X needs an
                // explicit SeparatorsPaint or it stays invisible (same
                // ShowSeparatorLines quirk as the peak-diff/probability
                // charts' X axes).
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(GridSeparatorColor),
            },
        };
        OverlayYAxes = new[]
        {
            new Axis
            {
                Name = "15N ppm",
                NameTextSize = AxisNameTextSize,
                NamePaint = new SolidColorPaint(AxisNameColor),
                Position = AxisPosition.End,
                TextSize = 9,
                MinLimit = -NMax,
                MaxLimit = -NMin,
                Labeler = value => Math.Abs(value).ToString("0.##"),
            },
        };
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
