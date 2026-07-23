using CommunityToolkit.Mvvm.ComponentModel;
using CspAnalyzer.Desktop.Models;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// Settings persistence integration (S11b). CurrentSettings/ApplySettings
/// cover only the filter/threshold fields that live on this ViewModel -
/// theme/background/window-geometry are MainWindow's responsibility
/// (ApplyAppearanceSettings/PopulateAppearanceSettings) and get merged
/// into the same AppSettings instance by App.axaml.cs.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private int? _binsPerArrayDimension;

    public AppSettings CurrentSettings() => new()
    {
        NMin = NMin,
        NMax = NMax,
        HMin = HMin,
        HMax = HMax,
        ReferenceIntensityThreshold = ReferenceIntensityThreshold,
        DatasetIntensityThreshold = DatasetIntensityThreshold,
        ManualProbabilityThreshold = ManualProbabilityThreshold,
        BinsPerArrayDimension = BinsPerArrayDimension,
    };

    public void ApplySettings(AppSettings settings)
    {
        NMin = settings.NMin;
        NMax = settings.NMax;
        HMin = settings.HMin;
        HMax = settings.HMax;
        ReferenceIntensityThreshold = settings.ReferenceIntensityThreshold;
        DatasetIntensityThreshold = settings.DatasetIntensityThreshold;
        BinsPerArrayDimension = settings.BinsPerArrayDimension;

        if (settings.ManualProbabilityThreshold is double threshold)
        {
            // Bypass the property setter, same reason RunAsync does below:
            // OnManualProbabilityThresholdChanged rebuilds charts/gauges
            // that don't exist yet at startup (nothing loaded/run yet).
            _manualProbabilityThreshold = threshold;
            OnPropertyChanged(nameof(ManualProbabilityThreshold));
        }
    }
}
