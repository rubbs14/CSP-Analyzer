using System.Threading.Tasks;
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaConfirmDialogService(Window owner) : IConfirmDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        return await dialog.ShowDialog<bool>(owner);
    }
}
