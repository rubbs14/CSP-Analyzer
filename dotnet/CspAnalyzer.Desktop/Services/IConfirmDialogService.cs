using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Replaces WinForms' MessageBox.Show(..., YesNo) (CSPv2/Form1.cs's
/// buttonResetAllManualFlags_Click) - Avalonia has no built-in MessageBox.
/// Same reasoning as IFilePickerService/IResultsWindowService: keeps
/// MainViewModel usable with no live Window (design-time, tests).
/// </summary>
public interface IConfirmDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}
