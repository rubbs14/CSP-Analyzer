using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaFilePickerService(TopLevel topLevel) : IFilePickerService
{
    // MainViewModel's picker instance is constructed once against
    // MainWindow and reused as-is for the Results window's exports
    // (S10/S13). A fixed TopLevel means every dialog is owned by
    // MainWindow regardless of which window the user is actually
    // interacting with - on Linux this let the OS save dialog open
    // behind whichever secondary window was focused (e.g. clicking
    // "Export PDF" in the Export Data window), looking like the button
    // just didn't do anything. Resolving the active window per call
    // instead keeps the dialog anchored to whatever's actually focused.
    private TopLevel ActiveTopLevel =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { } lifetime
            ? lifetime.Windows.FirstOrDefault(w => w.IsActive) ?? topLevel
            : topLevel;

    public async Task<string?> PickXmlFileAsync(string title)
    {
        var files = await ActiveTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("XML Files") { Patterns = new[] { "*.xml" } } },
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await ActiveTopLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string extension)
    {
        var file = await ActiveTopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = new[] { $"*.{extension}" } } },
        });
        return file?.TryGetLocalPath();
    }
}
