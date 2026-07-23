using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>
/// S11d: backs HelpWindow's TopSpin peak-picking command generator, ported
/// from CSPv2/FormHelp.cs's button_generate/button1_Click/button2_Click.
/// Plain ObservableObject (no Avalonia dependency) so it's unit-testable
/// without a headless window - HelpWindow instantiates it directly since
/// it has no external dependencies to inject.
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nMaxText = "";

    [ObservableProperty]
    private string _nMinText = "";

    [ObservableProperty]
    private string _hMaxText = "";

    [ObservableProperty]
    private string _hMinText = "";

    [ObservableProperty]
    private string _miText = "";

    [ObservableProperty]
    private string _ppNumText = "";

    [ObservableProperty]
    private string _generatedCommandText = "";

    partial void OnNMaxTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnNMinTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnHMaxTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnHMinTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnMiTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnPpNumTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();

    private bool CanGenerate() =>
        double.TryParse(NMaxText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(NMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(HMaxText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(HMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(MiText, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        int.TryParse(PpNumText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void Generate()
    {
        GeneratedCommandText =
            $"1 F1P {NMaxText}; 2 F1P {HMaxText}; 1 F2P {NMinText}; 2 F2P {HMinText}; MI {MiText}; PPNUM {PpNumText}; pp2d nodia";
    }

    [RelayCommand]
    private void Reset()
    {
        NMaxText = "";
        NMinText = "";
        HMaxText = "";
        HMinText = "";
        MiText = "";
        PpNumText = "";
        GeneratedCommandText = "";
    }
}
