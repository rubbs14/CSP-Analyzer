using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.Desktop.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.VisualElements;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S12: Ctrl+R "Reset Application" (CSPv2/Form1.cs:2744-2758's
/// buttonReset_Click, which called WinForms' Application.Restart()). .NET 8
/// has no equivalent that behaves identically across a self-contained
/// publish on every platform, so this clears all loaded/derived state back
/// to construction defaults and re-applies persisted settings via
/// ApplySettings - the same two things a real process restart would
/// actually do (App.axaml.cs re-runs SettingsService.Load()/ApplySettings
/// on every launch), without the cross-platform relaunch risk.
/// </summary>
public partial class MainViewModel
{
    [RelayCommand]
    private async Task ResetApplicationAsync()
    {
        bool confirmed = await _confirmDialogService.ConfirmAsync(
            "Reset Application",
            "This will clear the loaded reference, dataset, and all results, and reload your saved settings." +
            Environment.NewLine + Environment.NewLine + "Continue?");

        if (!confirmed)
        {
            return;
        }

        ReferenceSpectrum = null;
        ReferenceStatusText = "No Reference Loaded";
        ReferencePeakCount = 0;
        ReferenceMinIntensity = 0;
        ReferenceMaxIntensity = 0;
        OnPropertyChanged(nameof(IsReferenceLoaded));

        DatasetSpectra.Clear();
        DatasetStatusText = "No Dataset Loaded";
        TotalSubfoldersFound = 0;
        PeaklistFilesFoundCount = 0;
        ValidXmlPeaklistCount = 0;
        CorruptedXmlPeaklistCount = 0;
        OutOfPeakImportRangeCount = 0;
        ValidExperimentsCount = 0;
        CorruptedPeaklistExperiments.Clear();
        OutOfImportRangeExperiments.Clear();
        DatasetAveragePeakCount = 0;
        DatasetAverageMinIntensity = 0;
        DatasetAverageMaxIntensity = 0;

        RunResults.Clear();
        IsRunning = false;
        RunCompletedSuccessfully = false;
        RunStatusText = "";

        CurrentFilter = null;
        CurrentIndex = 0;
        GoToExperimentText = "";
        GoToStatusText = "";

        // Bypass the ManualProbabilityThreshold setter - same reason
        // ApplySettings/RunAsync do below: OnManualProbabilityThresholdChanged
        // rebuilds charts/gauges that are about to be cleared anyway.
        _manualProbabilityThreshold = 0.5;
        OnPropertyChanged(nameof(ManualProbabilityThreshold));

        AppSettings settings = _settingsService.Load();
        ApplySettings(settings);

        PeakDiffSeries = Array.Empty<ISeries>();
        PeakDiffXAxes = Array.Empty<Axis>();
        PeakDiffYAxes = Array.Empty<Axis>();
        PeakDiffSections = Array.Empty<RectangularSection>();
        PeakDiffAnnotations = Array.Empty<LabelVisual>();

        ProbabilitySeries = Array.Empty<ISeries>();
        ProbabilityXAxes = Array.Empty<Axis>();
        ProbabilityYAxes = Array.Empty<Axis>();
        ProbabilitySections = Array.Empty<RectangularSection>();
        ProbabilityAnnotations = Array.Empty<LabelVisual>();

        ActivesGaugeSeries = Array.Empty<ISeries>();
        InactivesGaugeSeries = Array.Empty<ISeries>();
        ActivesAutoCount = 0;
        InactivesAutoCount = 0;

        BuildOverlayAxes();
        RaiseNavigationChanged();

        RunCommand.NotifyCanExecuteChanged();
        OpenResultsWindowCommand.NotifyCanExecuteChanged();
        ToggleAutoActivesFilterCommand.NotifyCanExecuteChanged();
        ToggleAutoInactivesFilterCommand.NotifyCanExecuteChanged();
        ShowCorruptedPeaklistExpCommand.NotifyCanExecuteChanged();
        ShowOutOfImportRangeExpCommand.NotifyCanExecuteChanged();
    }
}
