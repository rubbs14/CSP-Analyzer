using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaAboutWindowService(Window owner) : IAboutWindowService
{
    // A fresh window per click (the original behavior) left one pile of
    // stacked identical About windows behind every repeat click - reuse
    // (and just refocus) the single existing instance instead.
    private AboutWindow? _window;

    public void Show()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        _window = new AboutWindow();
        _window.Closed += (_, _) => _window = null;
        _window.Show(owner);
    }
}
