using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext and tests, where there's no real dialog to show.</summary>
public sealed class NullInfoDialogService : IInfoDialogService
{
    public Task ShowAsync(string title, string message) => Task.CompletedTask;
}
