using System.Threading.Tasks;
using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaInfoDialogService(Window owner) : IInfoDialogService
{
    public async Task ShowAsync(string title, string message)
    {
        var dialog = new InfoDialog(title, message);
        await dialog.ShowDialog(owner);
    }
}
