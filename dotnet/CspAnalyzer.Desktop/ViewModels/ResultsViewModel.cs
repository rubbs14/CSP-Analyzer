using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10: backs ResultsWindow, the port of CSPv2/FormOutputTable.cs. Built
/// fresh from the reference/dataset/run-results snapshot MainViewModel
/// passes in when the window is opened - no back-reference to
/// MainViewModel, so this is independently constructible and (mechanically)
/// testable without a live MainViewModel or Window.
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    // CSPv2/FormOutputTable.cs's SolidColorBrush fields use
    // System.Windows.Media.Color.FromArgb(a, r, g, b); SkiaSharp's SKColor
    // constructor is (r, g, b, a) - reordered here, not a color change.
    private static readonly SKColor ActiveAutoColor = new(45, 161, 63, 200);
    private static readonly SKColor InactiveAutoColor = new(225, 9, 20, 180);
    private static readonly SKColor ActiveManualColor = new(123, 217, 157, 200);
    private static readonly SKColor InactiveManualColor = new(199, 137, 137, 180);
    private static readonly SKColor NotSetManualColor = new(178, 178, 178, 180);

    private readonly IFilePickerService _filePicker;
    private readonly PeaklistSpectrum _reference;
    private readonly IReadOnlyList<PeaklistSpectrum> _datasetSpectra;
    private readonly IReadOnlyList<SpectrumResult> _runResults;

    public ObservableCollection<ResultRow> Rows { get; } = new();

    [ObservableProperty]
    private int _totalExperiments;

    [ObservableProperty]
    private int _activesAuto;

    [ObservableProperty]
    private int _inactivesAuto;

    [ObservableProperty]
    private int _activesManual;

    [ObservableProperty]
    private int _inactivesManual;

    [ObservableProperty]
    private int _notSetManual;

    [ObservableProperty]
    private ISeries[] _overviewSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _autoSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _manualSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private string _exportStatusText = "";

    public ResultsViewModel(
        IFilePickerService filePicker,
        PeaklistSpectrum reference,
        IReadOnlyList<PeaklistSpectrum> datasetSpectra,
        IReadOnlyList<SpectrumResult> runResults)
    {
        _filePicker = filePicker;
        _reference = reference;
        _datasetSpectra = datasetSpectra;
        _runResults = runResults;
        Rebuild();
    }

    [RelayCommand]
    private void Refresh() => Rebuild();

    private void Rebuild()
    {
        IReadOnlyList<ResultRow> rows = ResultsBuilder.Build(_reference, _datasetSpectra, _runResults);

        Rows.Clear();
        foreach (ResultRow row in rows)
        {
            Rows.Add(row);
        }

        TotalExperiments = Rows.Count - 1;
        ActivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Active");
        InactivesAuto = Rows.Count(r => r.AutomaticAnalysis == "Inactive");
        ActivesManual = Rows.Count(r => r.ManualFlag == "ACTIVE (MAN)");
        InactivesManual = Rows.Count(r => r.ManualFlag == "INACTIVE (MAN)");
        NotSetManual = Rows.Count(r => r.ManualFlag == "Not set");

        OverviewSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
        };

        AutoSeries = new ISeries[]
        {
            PieSlice("Actives", ActivesAuto, ActiveAutoColor),
            PieSlice("Inactives", InactivesAuto, InactiveAutoColor),
        };

        ManualSeries = new ISeries[]
        {
            PieSlice("Manual: Actives", ActivesManual, ActiveManualColor),
            PieSlice("Manual: Inactives", InactivesManual, InactiveManualColor),
            PieSlice("Manual: Not set", NotSetManual, NotSetManualColor),
        };
    }

    private static PieSeries<int> PieSlice(string name, int value, SKColor color) => new()
    {
        Name = name,
        Values = new[] { value },
        Fill = new SolidColorPaint(color),
    };
}
