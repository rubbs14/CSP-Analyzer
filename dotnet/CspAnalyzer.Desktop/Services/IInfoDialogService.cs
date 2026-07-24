using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Read-only info display (S12) - mirrors IConfirmDialogService's reasoning
/// but OK-button-only, no return value. Used for listing corrupted/out-of-
/// range experiment names (CSPv2/Form1.cs's MessageBox.Show(..., OK) calls
/// behind legacy's Ctrl+Alt+F/Ctrl+Alt+O; this port binds Ctrl+Alt+F/
/// Ctrl+Alt+Y instead, since Ctrl+Alt+O was already taken here).
/// </summary>
public interface IInfoDialogService
{
    Task ShowAsync(string title, string message);
}
