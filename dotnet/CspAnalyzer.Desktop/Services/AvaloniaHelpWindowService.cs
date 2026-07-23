using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaHelpWindowService(Window owner) : IHelpWindowService
{
    public void Show()
    {
        var window = new HelpWindow();
        window.Show(owner);
    }
}
