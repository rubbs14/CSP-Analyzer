using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Read-only info display (S12) - mirrors IConfirmDialogService's reasoning
/// but OK-button-only, no return value. Used for listing corrupted/out-of-
/// range experiment names (CSPv2/Form1.cs's MessageBox.Show(..., OK) calls
/// behind Ctrl+Alt+F/Ctrl+Alt+O).
/// </summary>
public interface IInfoDialogService
{
    Task ShowAsync(string title, string message);
}
