using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaShortcutsWindowService(Window owner) : IShortcutsWindowService
{
    public void Show()
    {
        var window = new ShortcutsWindow();
        window.Show(owner);
    }
}
