using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaAboutWindowService(Window owner) : IAboutWindowService
{
    public void Show()
    {
        var window = new AboutWindow();
        window.Show(owner);
    }
}
