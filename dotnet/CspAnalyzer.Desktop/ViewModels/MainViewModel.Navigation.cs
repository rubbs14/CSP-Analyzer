using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: which subset of DatasetSpectra the player/overlay/gauges are
/// currently showing - replaces CSPv2/Form1.cs's two independently-mutable
/// ShowActives/ShowInactives bools (each checkbox handler manually
/// unchecked the other, leaving several "both true" branches in
/// update_graphs/update_player that could never actually be reached
/// through the UI) with a single nullable enum.
/// </summary>
public enum ExperimentFilter
{
    Actives,
    Inactives,
}

/// <summary>
/// S10b: player navigation (CurrentIndex/CurrentView/First-Previous-Next-
/// Last/GoToExperiment) and the actives/inactives filter. Port of
/// CSPv2/Form1.cs's update_player/go_to_experiment/CheckBoxActives_
/// CheckedChanged region. Bounds are enforced via RelayCommand CanExecute
/// instead of legacy's pattern of relying on update_player() to disable
/// buttons after the fact.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private ExperimentFilter? _currentFilter;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _goToExperimentText = "";

    [ObservableProperty]
    private string _goToStatusText = "";

    public bool IsActivesFilterChecked
    {
        get => CurrentFilter == ExperimentFilter.Actives;
        set => CurrentFilter = value ? ExperimentFilter.Actives : null;
    }

    public bool IsInactivesFilterChecked
    {
        get => CurrentFilter == ExperimentFilter.Inactives;
        set => CurrentFilter = value ? ExperimentFilter.Inactives : null;
    }

    private bool CanToggleAutoFilter() => RunResults.Count > 0;

    [RelayCommand(CanExecute = nameof(CanToggleAutoFilter))]
    private void ToggleAutoActivesFilter() => IsActivesFilterChecked = !IsActivesFilterChecked;

    [RelayCommand(CanExecute = nameof(CanToggleAutoFilter))]
    private void ToggleAutoInactivesFilter() => IsInactivesFilterChecked = !IsInactivesFilterChecked;

    private Dictionary<int, SpectrumResult> ResultsByExpNumber => RunResults.ToDictionary(r => r.ExpNumber);

    public IReadOnlyList<PeaklistSpectrum> CurrentView => CurrentFilter switch
    {
        ExperimentFilter.Actives => DatasetSpectra.Where(IsAutoActive).ToList(),
        ExperimentFilter.Inactives => DatasetSpectra.Where(s => !IsAutoActive(s)).ToList(),
        _ => DatasetSpectra.ToList(),
    };

    private bool IsAutoActive(PeaklistSpectrum spectrum) =>
        ResultsByExpNumber.TryGetValue(spectrum.ExpNumber, out SpectrumResult? result) && IsEffectivelyActive(result);

    public PeaklistSpectrum? CurrentSpectrum =>
        CurrentIndex >= 0 && CurrentIndex < CurrentView.Count ? CurrentView[CurrentIndex] : null;

    public string CurrentExperimentNumber => CurrentSpectrum is null ? "-" : CurrentSpectrum.ExpNumber.ToString();

    public string CurrentCounterText => CurrentView.Count == 0 ? "- / -" : $"{CurrentIndex + 1} / {CurrentView.Count}";

    public int? CurrentPeakDifference =>
        CurrentSpectrum is null || ReferenceSpectrum is null ? null : CurrentSpectrum.TotReadPeaks - ReferenceSpectrum.TotReadPeaks;

    public int? CurrentReadPeaks => CurrentSpectrum?.TotReadPeaks;

    public double? CurrentMinIntensity =>
        CurrentSpectrum is null || CurrentSpectrum.Peaklist.Count == 0 ? null : CurrentSpectrum.Peaklist.Min(p => p.Intensity);

    public double? CurrentMaxIntensity =>
        CurrentSpectrum is null || CurrentSpectrum.Peaklist.Count == 0 ? null : CurrentSpectrum.Peaklist.Max(p => p.Intensity);

    public string CurrentManualStatusText => CurrentSpectrum?.UserSelection ?? "-";

    public string CurrentAutomaticStatusText =>
        CurrentSpectrum is null ? "-" :
        ResultsByExpNumber.TryGetValue(CurrentSpectrum.ExpNumber, out SpectrumResult? result)
            ? (IsEffectivelyActive(result) ? "ACTIVE" : "INACTIVE")
            : "Run analysis";

    partial void OnCurrentFilterChanged(ExperimentFilter? value)
    {
        OnPropertyChanged(nameof(IsActivesFilterChecked));
        OnPropertyChanged(nameof(IsInactivesFilterChecked));
        CurrentIndex = 0;
        RaiseNavigationChanged();
    }

    private bool CanGoPrevious() => CurrentIndex > 0;
    private bool CanGoNext() => CurrentIndex < CurrentView.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void First()
    {
        CurrentIndex = 0;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        CurrentIndex--;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        CurrentIndex++;
        RaiseNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Last()
    {
        CurrentIndex = CurrentView.Count - 1;
        RaiseNavigationChanged();
    }

    [RelayCommand]
    private void GoToExperiment()
    {
        if (!int.TryParse(GoToExperimentText, out int expNumber))
        {
            GoToStatusText = "Enter a valid experiment number.";
            return;
        }

        int index = CurrentView.ToList().FindIndex(s => s.ExpNumber == expNumber);
        if (index < 0)
        {
            GoToStatusText = $"Experiment {expNumber} not found.";
            return;
        }

        CurrentIndex = index;
        GoToStatusText = "";
        RaiseNavigationChanged();
    }

    /// <summary>
    /// Called by chart click-to-navigate (MainWindow.axaml.cs) and by
    /// LoadDatasetAsync/RunAsync/manual-override mutations - anything that
    /// changes DatasetSpectra, RunResults, or a spectrum's UserSelection
    /// must call this afterward so the computed display properties and
    /// nav-command CanExecute states refresh (they don't auto-cascade from
    /// ObservableProperty since CurrentView/CurrentSpectrum are plain
    /// computed properties, not backed by their own [ObservableProperty]).
    /// </summary>
    public void RaiseNavigationChanged()
    {
        OnPropertyChanged(nameof(CurrentView));
        OnPropertyChanged(nameof(CurrentSpectrum));
        OnPropertyChanged(nameof(CurrentExperimentNumber));
        OnPropertyChanged(nameof(CurrentCounterText));
        OnPropertyChanged(nameof(CurrentPeakDifference));
        OnPropertyChanged(nameof(CurrentReadPeaks));
        OnPropertyChanged(nameof(CurrentMinIntensity));
        OnPropertyChanged(nameof(CurrentMaxIntensity));
        OnPropertyChanged(nameof(CurrentManualStatusText));
        OnPropertyChanged(nameof(CurrentAutomaticStatusText));
        FirstCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        LastCommand.NotifyCanExecuteChanged();
        RebuildOverlayPoints();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ShowReferencePpDetails() =>
        await _confirmDialogService.ConfirmAsync("Reference PP Details", ReferenceSpectrum?.PpInfo ?? "No reference loaded.");

    [RelayCommand]
    private async System.Threading.Tasks.Task ShowExperimentPpDetails() =>
        await _confirmDialogService.ConfirmAsync("Experiment PP Details", CurrentSpectrum?.PpInfo ?? "No experiment selected.");

    public void NavigateToChartIndex(int index)
    {
        if (index < 0 || index >= CurrentView.Count)
        {
            return;
        }

        CurrentIndex = index;
        RaiseNavigationChanged();
    }
}
