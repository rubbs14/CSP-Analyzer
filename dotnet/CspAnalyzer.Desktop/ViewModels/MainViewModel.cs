using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using CspAnalyzer.Desktop.Services;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// Dataset loading (S8): ports CSPv2/Form1.cs's Load_ref_Click and
/// load_ds_button_Click into MVVM commands over PeaklistImporter. Import
/// range/threshold defaults match Form1.Designer.cs's textbox defaults
/// (NMin=100, NMax=140, HMin=5, HMax=12, RefInt=5000, DSInt=2000).
///
/// Run flow (S9): RunCommand serializes the loaded reference+dataset
/// spectra to a temp JSON file matching backend/io.py:json_parser's
/// expected shape (PeaklistSpectrum's property names already mirror it -
/// see S8), shells out via BackendCliRunner.RunAsync on the S6 CLI
/// contract, and parses processed_spectra.json back via SpectrumResult.
/// No incremental progress protocol exists on the python side (single
/// blocking call), so "progress reporting" here is an indeterminate
/// IsRunning state rather than a percentage. CancelRunCommand cancels the
/// CancellationTokenSource, which BackendCliRunner.RunAsync turns into a
/// process-tree kill. Results table/charts are S10 - RunResults just holds
/// the parsed array for that session to bind to.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IFilePickerService _filePicker;
    private readonly IResultsWindowService _resultsWindowService;

    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private double _nMin = 100;

    [ObservableProperty]
    private double _nMax = 140;

    [ObservableProperty]
    private double _hMin = 5;

    [ObservableProperty]
    private double _hMax = 12;

    [ObservableProperty]
    private double _referenceIntensityThreshold = 5000;

    [ObservableProperty]
    private double _datasetIntensityThreshold = 2000;

    [ObservableProperty]
    private PeaklistSpectrum? _referenceSpectrum;

    [ObservableProperty]
    private string _referenceStatusText = "No Reference Loaded";

    [ObservableProperty]
    private int _referencePeakCount;

    [ObservableProperty]
    private double _referenceMinIntensity;

    [ObservableProperty]
    private double _referenceMaxIntensity;

    [ObservableProperty]
    private string _datasetStatusText = "No Dataset Loaded";

    [ObservableProperty]
    private int _totalSubfoldersFound;

    [ObservableProperty]
    private int _peaklistFilesFoundCount;

    public ObservableCollection<PeaklistSpectrum> DatasetSpectra { get; } = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _runStatusText = "";

    public ObservableCollection<SpectrumResult> RunResults { get; } = new();

    private CancellationTokenSource? _runCts;

    public bool IsReferenceLoaded => ReferenceSpectrum is not null;

    public MainViewModel() : this(new NullFilePickerService(), new NullResultsWindowService())
    {
    }

    public MainViewModel(IFilePickerService filePicker, IResultsWindowService resultsWindowService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
    }

    private PeakImportFilter ReferenceFilter => new(ReferenceIntensityThreshold, NMin, NMax, HMin, HMax);

    private PeakImportFilter DatasetFilter => new(DatasetIntensityThreshold, NMin, NMax, HMin, HMax);

    [RelayCommand]
    private async Task LoadReferenceAsync()
    {
        string? path = await _filePicker.PickXmlFileAsync("Select Reference peaklist.xml");
        if (path is null)
        {
            return;
        }

        ReferenceStatusText = "Loading Reference...";
        var spectrum = PeaklistImporter.Import(path, ReferenceFilter, jsonData: "Reference");

        if (spectrum.Peaklist.Count == 0)
        {
            ReferenceSpectrum = null;
            ReferenceStatusText = "No peaks found in the Reference Peaklist - check import limits.";
            OnPropertyChanged(nameof(IsReferenceLoaded));
            return;
        }

        ReferenceSpectrum = spectrum;
        ReferencePeakCount = spectrum.Peaklist.Count;
        ReferenceMinIntensity = spectrum.Peaklist.Min(p => p.Intensity);
        ReferenceMaxIntensity = spectrum.Peaklist.Max(p => p.Intensity);
        ReferenceStatusText = "Reference Loaded";
        OnPropertyChanged(nameof(IsReferenceLoaded));
        RunCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadDatasetAsync()
    {
        if (!IsReferenceLoaded)
        {
            DatasetStatusText = "Load a Reference first.";
            return;
        }

        string? folder = await _filePicker.PickFolderAsync("Select Dataset folder");
        if (folder is null)
        {
            return;
        }

        DatasetStatusText = "Loading Dataset...";
        DatasetSpectra.Clear();

        // Port of load_ds_button_Click / Add_DSSpectra's effective behavior:
        // every immediate subfolder with a pdata/1/peaklist.xml is a valid
        // experiment (the original's EndsWith("1")-filtered EXP_NAMES list
        // was dead-end bookkeeping - VALID_EXP, the list that actually feeds
        // VALID_DS_SPECTRA, was built from the unfiltered subfolder list).
        string[] subfolders = Directory.GetDirectories(folder);
        TotalSubfoldersFound = subfolders.Length;

        int found = 0;
        foreach (string dir in subfolders)
        {
            string peaklistPath = Path.Combine(dir, "pdata", "1", "peaklist.xml");
            if (!File.Exists(peaklistPath))
            {
                continue;
            }

            found++;
            DatasetSpectra.Add(PeaklistImporter.Import(peaklistPath, DatasetFilter, jsonData: "Experiment"));
        }

        PeaklistFilesFoundCount = found;

        List<PeaklistSpectrum> sorted = DatasetSpectra.OrderBy(s => s.ExpNumber).ToList();
        DatasetSpectra.Clear();
        foreach (PeaklistSpectrum spectrum in sorted)
        {
            DatasetSpectra.Add(spectrum);
        }

        DatasetStatusText = found > 0
            ? $"Dataset Loaded ({found} experiments)"
            : "No experiments were found in this folder.";
        RunCommand.NotifyCanExecuteChanged();
    }

    private bool CanRun() => IsReferenceLoaded && DatasetSpectra.Count > 0 && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        string? python = BackendEnvironment.PythonExecutable;
        if (python is null)
        {
            RunStatusText = "csp_modern python environment not found - cannot run.";
            return;
        }

        _runCts = new CancellationTokenSource();
        IsRunning = true;
        RunStatusText = "Running CSP analysis...";
        RunCommand.NotifyCanExecuteChanged();
        CancelRunCommand.NotifyCanExecuteChanged();

        string runDir = Directory.CreateTempSubdirectory("csp_analyzer_run_").FullName;
        try
        {
            string jsonIn = Path.Combine(runDir, "spectra_in.json");
            var allSpectra = new[] { ReferenceSpectrum! }.Concat(DatasetSpectra);
            File.WriteAllText(jsonIn, PeaklistSpectrum.SerializeAll(allSpectra));

            BackendRunResult result = await BackendCliRunner.RunAsync(
                python,
                jsonIn,
                runDir,
                BackendEnvironment.ModelDir,
                BackendEnvironment.RepoRoot,
                binsPerArrayDimension: null,
                _runCts.Token);

            if (result.IsSuccess)
            {
                SpectrumResult[] parsed = SpectrumResult.ParseArray(File.ReadAllText(result.OutputPath!));
                RunResults.Clear();
                foreach (SpectrumResult r in parsed)
                {
                    RunResults.Add(r);
                }
                OpenResultsWindowCommand.NotifyCanExecuteChanged();
                RunStatusText = $"Run complete: {parsed.Length} experiments classified.";
            }
            else
            {
                RunStatusText = $"Run failed: {result.StdErr.Trim()}";
            }
        }
        catch (OperationCanceledException)
        {
            RunStatusText = "Run cancelled.";
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
            RunCommand.NotifyCanExecuteChanged();
            CancelRunCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCancelRun() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancelRun))]
    private void CancelRun() => _runCts?.Cancel();

    private bool CanOpenResultsWindow() => RunResults.Count > 0 && ReferenceSpectrum is not null;

    [RelayCommand(CanExecute = nameof(CanOpenResultsWindow))]
    private void OpenResultsWindow()
    {
        var resultsViewModel = new ResultsViewModel(_filePicker, ReferenceSpectrum!, DatasetSpectra.ToList(), RunResults.ToList());
        _resultsWindowService.Show(resultsViewModel);
    }
}
