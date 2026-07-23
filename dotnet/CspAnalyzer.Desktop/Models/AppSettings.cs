namespace CspAnalyzer.Desktop.Models;

/// <summary>
/// Persisted app state (S11b). Defaults on every property reproduce the
/// hardcoded literals the app used before persistence existed, so
/// "no settings file" and "freshly-defaulted settings" behave identically.
/// </summary>
public class AppSettings
{
    public string ThemeVariant { get; set; } = "System";
    public string? BackgroundColorHex { get; set; }

    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 820;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public string WindowState { get; set; } = "Normal";

    public double ReferenceIntensityThreshold { get; set; } = 5000;
    public double DatasetIntensityThreshold { get; set; } = 2000;
    public double NMin { get; set; } = 100;
    public double NMax { get; set; } = 140;
    public double HMin { get; set; } = 5;
    public double HMax { get; set; } = 12;

    public double? ManualProbabilityThreshold { get; set; }
    public int? BinsPerArrayDimension { get; set; }
}
