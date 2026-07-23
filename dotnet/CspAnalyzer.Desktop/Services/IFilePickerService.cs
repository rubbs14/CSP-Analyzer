using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Replaces WinForms' OpenFileDialog/FolderBrowserDialog (CSPv2/Form1.cs's
/// Load_ref_Click/load_ds_button_Click) with Avalonia's cross-platform
/// IStorageProvider, behind an interface so MainViewModel doesn't need a
/// live Avalonia window to be constructed (design-time DataContext, tests).
/// </summary>
public interface IFilePickerService
{
    Task<string?> PickXmlFileAsync(string title);

    Task<string?> PickFolderAsync(string title);
}
