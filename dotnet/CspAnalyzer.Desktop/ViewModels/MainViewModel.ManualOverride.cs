using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CspAnalyzer.BackendInterop;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S10b: port of CSPv2/Form1.cs's "Buttons Manual UserSelection" region.
/// PeaklistSpectrum.UserSelection already exists and already flows into
/// ResultsBuilder -> ResultsWindow's Manual Flag column/pie (S10) - this
/// file is what actually mutates it, which nothing did before S10b.
/// </summary>
public partial class MainViewModel
{
    // CSPv2/Form1.cs:100-119's exact ARGB values, reordered to SkiaSharp's
    // (r,g,b,a) constructor - same values ResultsViewModel.cs already uses
    // for the ResultsWindow pie charts, kept consistent here.
    private static readonly SKColor ActiveManualColor = new(123, 217, 157, 200);
    private static readonly SKColor InactiveManualColor = new(199, 137, 137, 180);
    private static readonly SKColor NotSetManualColor = new(178, 178, 178, 180);

    [ObservableProperty]
    private int _activesManualCount;

    [ObservableProperty]
    private int _inactivesManualCount;

    [ObservableProperty]
    private int _notSetManualCount;

    [ObservableProperty]
    private ISeries[] _manualResultsSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _manualResultsXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _manualResultsYAxes = Array.Empty<Axis>();

    [RelayCommand]
    private void MarkActive() => SetCurrentUserSelection("ACTIVE (MAN)");

    [RelayCommand]
    private void MarkInactive() => SetCurrentUserSelection("INACTIVE (MAN)");

    [RelayCommand]
    private void ResetManualStatus() => SetCurrentUserSelection("Not set");

    private void SetCurrentUserSelection(string value)
    {
        if (CurrentSpectrum is null)
        {
            return;
        }

        CurrentSpectrum.UserSelection = value;
        RebuildManualResults();
        RaiseNavigationChanged();
    }

    [RelayCommand]
    private async Task ResetAllManualFlags()
    {
        bool confirmed = await _confirmDialogService.ConfirmAsync(
            "Manual Flag Reset",
            "Are you sure you want to reset your manual selection?" + Environment.NewLine + Environment.NewLine +
            "WARNING: All the Spectra Manual Flags will be reset to \"Not set\".");

        if (!confirmed)
        {
            return;
        }

        foreach (PeaklistSpectrum spectrum in DatasetSpectra)
        {
            spectrum.UserSelection = "Not set";
        }

        RebuildManualResults();
        RaiseNavigationChanged();
    }

    public void RebuildManualResults()
    {
        ActivesManualCount = DatasetSpectra.Count(s => s.UserSelection == "ACTIVE (MAN)");
        InactivesManualCount = DatasetSpectra.Count(s => s.UserSelection == "INACTIVE (MAN)");
        NotSetManualCount = DatasetSpectra.Count(s => s.UserSelection == "Not set");

        ManualResultsSeries = new ISeries[]
        {
            new ColumnSeries<int> { Name = "Active", Values = new[] { ActivesManualCount }, Fill = new SolidColorPaint(ActiveManualColor) },
            new ColumnSeries<int> { Name = "Inactive", Values = new[] { InactivesManualCount }, Fill = new SolidColorPaint(InactiveManualColor) },
            new ColumnSeries<int> { Name = "Not set", Values = new[] { NotSetManualCount }, Fill = new SolidColorPaint(NotSetManualColor) },
        };
        ManualResultsXAxes = new[] { new Axis { MinLimit = -1, MaxLimit = 1, IsVisible = false } };
        ManualResultsYAxes = new[] { new Axis { MinLimit = 0 } };
    }
}
