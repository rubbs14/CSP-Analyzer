using System.Threading.Tasks;

namespace CspAnalyzer.Desktop.Services;

/// <summary>Always confirms - used by the design-time DataContext and by tests, where there's no real dialog to show.</summary>
public sealed class NullConfirmDialogService : IConfirmDialogService
{
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
}
