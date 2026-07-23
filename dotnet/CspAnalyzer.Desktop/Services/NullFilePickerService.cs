using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op picker for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullFilePickerService : IFilePickerService
{
    public Task<string?> PickXmlFileAsync(string title) => Task.FromResult<string?>(null);

    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
}
