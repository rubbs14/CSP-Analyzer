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
    private readonly IConfirmDialogService _confirmDialogService;
    private readonly IAboutWindowService _aboutWindowService;
    private readonly IShortcutsWindowService _shortcutsWindowService;
    private readonly IHelpWindowService _helpWindowService;
    private readonly IInfoDialogService _infoDialogService;
    private readonly SettingsService _settingsService;

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

    [ObservableProperty]
    private int _validXmlPeaklistCount;

    [ObservableProperty]
    private int _corruptedXmlPeaklistCount;

    [ObservableProperty]
    private int _outOfPeakImportRangeCount;

    [ObservableProperty]
    private int _validExperimentsCount;

    [ObservableProperty]
    private double _datasetAveragePeakCount;

    [ObservableProperty]
    private double _datasetAverageMinIntensity;

    [ObservableProperty]
    private double _datasetAverageMaxIntensity;

    public ObservableCollection<PeaklistSpectrum> DatasetSpectra { get; } = new();

    public ObservableCollection<string> CorruptedPeaklistExperiments { get; } = new();

    public ObservableCollection<string> OutOfImportRangeExperiments { get; } = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _runCompletedSuccessfully;

    [ObservableProperty]
    private string _runStatusText = "";

    public ObservableCollection<SpectrumResult> RunResults { get; } = new();

    private CancellationTokenSource? _runCts;

    public bool IsReferenceLoaded => ReferenceSpectrum is not null;

    public MainViewModel() : this(
        new NullFilePickerService(), new NullResultsWindowService(), new NullConfirmDialogService(),
        new NullAboutWindowService(), new NullShortcutsWindowService(), new NullHelpWindowService(),
        new NullInfoDialogService(), new SettingsService())
    {
    }

    public MainViewModel(
        IFilePickerService filePicker,
        IResultsWindowService resultsWindowService,
        IConfirmDialogService confirmDialogService,
        IAboutWindowService aboutWindowService,
        IShortcutsWindowService shortcutsWindowService,
        IHelpWindowService helpWindowService,
        IInfoDialogService infoDialogService,
        SettingsService settingsService)
    {
        _filePicker = filePicker;
        _resultsWindowService = resultsWindowService;
        _confirmDialogService = confirmDialogService;
        _aboutWindowService = aboutWindowService;
        _shortcutsWindowService = shortcutsWindowService;
        _helpWindowService = helpWindowService;
        _infoDialogService = infoDialogService;
        _settingsService = settingsService;
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
        BuildOverlayAxes();
        RaiseNavigationChanged();
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
        CorruptedPeaklistExperiments.Clear();
        OutOfImportRangeExperiments.Clear();

        // Port of load_ds_button_Click / Add_DSSpectra's effective behavior:
        // every immediate subfolder with a pdata/1/peaklist.xml is a valid
        // experiment (the original's EndsWith("1")-filtered EXP_NAMES list
        // was dead-end bookkeeping - VALID_EXP, the list that actually feeds
        // VALID_DS_SPECTRA, was built from the unfiltered subfolder list).
        string[] subfolders = Directory.GetDirectories(folder);
        TotalSubfoldersFound = subfolders.Length;

        int found = 0;
        int validXml = 0;
        int corruptedXml = 0;
        int outOfRange = 0;
        foreach (string dir in subfolders)
        {
            string peaklistPath = Path.Combine(dir, "pdata", "1", "peaklist.xml");
            if (!File.Exists(peaklistPath))
            {
                CorruptedPeaklistExperiments.Add(Path.GetFileName(dir));
                continue;
            }

            found++;
            PeaklistSpectrum spectrum;
            try
            {
                spectrum = PeaklistImporter.Import(peaklistPath, DatasetFilter, jsonData: "Experiment");
            }
            catch (System.Xml.XmlException)
            {
                corruptedXml++;
                CorruptedPeaklistExperiments.Add(Path.GetFileName(dir));
                continue;
            }

            validXml++;
            if (spectrum.Peaklist.Count == 0)
            {
                outOfRange++;
                OutOfImportRangeExperiments.Add(spectrum.ExpNumber.ToString());
                continue;
            }

            DatasetSpectra.Add(spectrum);
        }

        PeaklistFilesFoundCount = found;
        ValidXmlPeaklistCount = validXml;
        CorruptedXmlPeaklistCount = corruptedXml;
        OutOfPeakImportRangeCount = outOfRange;
        ValidExperimentsCount = DatasetSpectra.Count;

        ShowCorruptedPeaklistExpCommand.NotifyCanExecuteChanged();
        ShowOutOfImportRangeExpCommand.NotifyCanExecuteChanged();

        List<PeaklistSpectrum> sorted = DatasetSpectra.OrderBy(s => s.ExpNumber).ToList();
        DatasetSpectra.Clear();
        foreach (PeaklistSpectrum spectrum in sorted)
        {
            DatasetSpectra.Add(spectrum);
        }

        DatasetAveragePeakCount = DatasetSpectra.Count > 0 ? DatasetSpectra.Average(s => s.TotReadPeaks) : 0;
        DatasetAverageMinIntensity = DatasetSpectra.Count > 0 ? DatasetSpectra.Average(s => s.Peaklist.Min(p => p.Intensity)) : 0;
        DatasetAverageMaxIntensity = DatasetSpectra.Count > 0 ? DatasetSpectra.Average(s => s.Peaklist.Max(p => p.Intensity)) : 0;

        DatasetStatusText = found > 0
            ? $"Dataset Loaded ({found} experiments)"
            : "No experiments were found in this folder.";
        RunCommand.NotifyCanExecuteChanged();

        BuildPeakDiffChart();
        RaiseNavigationChanged();
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
        RunCompletedSuccessfully = false;
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
                binsPerArrayDimension: BinsPerArrayDimension,
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
                ToggleAutoActivesFilterCommand.NotifyCanExecuteChanged();
                ToggleAutoInactivesFilterCommand.NotifyCanExecuteChanged();
                CurrentIndex = 0;
                // Bypass the ManualProbabilityThreshold setter here (it
                // would trigger its own Build*/RaiseNavigationChanged via
                // OnManualProbabilityThresholdChanged, and CommunityToolkit
                // skips that entirely if the new value happens to equal
                // the old one - e.g. two runs both landing on the 0.5
                // fallback) - the explicit calls below always run instead.
                _manualProbabilityThreshold = ComputeAutoProbabilityThreshold();
                OnPropertyChanged(nameof(ManualProbabilityThreshold));
                BuildProbabilityChart();
                BuildGauges();
                RaiseNavigationChanged();
                RunStatusText = $"Run complete: {parsed.Length} experiments classified.";
                RunCompletedSuccessfully = true;
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

    [RelayCommand]
    private void ResetImportControls()
    {
        NMin = 100;
        NMax = 140;
        HMin = 5;
        HMax = 12;
    }

    [RelayCommand]
    private void ResetPeakFiltering()
    {
        ReferenceIntensityThreshold = 5000;
        DatasetIntensityThreshold = 2000;
    }

    [RelayCommand]
    private void ResetAllImportAndThresholdControls()
    {
        ResetImportControls();
        ResetPeakFiltering();
    }

    private bool CanOpenResultsWindow() => RunResults.Count > 0 && ReferenceSpectrum is not null;

    [RelayCommand(CanExecute = nameof(CanOpenResultsWindow))]
    private void OpenResultsWindow()
    {
        var resultsViewModel = new ResultsViewModel(_filePicker, ReferenceSpectrum!, DatasetSpectra.ToList(), RunResults.ToList());
        _resultsWindowService.Show(resultsViewModel);
    }

    private bool CanShowCorruptedPeaklistExp() => CorruptedPeaklistExperiments.Count > 0;

    [RelayCommand(CanExecute = nameof(CanShowCorruptedPeaklistExp))]
    private Task ShowCorruptedPeaklistExpAsync() =>
        _infoDialogService.ShowAsync("Corrupted Peaklist Experiments", string.Join(Environment.NewLine, CorruptedPeaklistExperiments));

    private bool CanShowOutOfImportRangeExp() => OutOfImportRangeExperiments.Count > 0;

    [RelayCommand(CanExecute = nameof(CanShowOutOfImportRangeExp))]
    private Task ShowOutOfImportRangeExpAsync() =>
        _infoDialogService.ShowAsync("Out-of-Import-Range Experiments", string.Join(Environment.NewLine, OutOfImportRangeExperiments));
}
