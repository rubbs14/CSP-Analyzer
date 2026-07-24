using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaShortcutsWindowService(Window owner) : IShortcutsWindowService
{
    // See AvaloniaAboutWindowService's comment - same fix, same reason.
    private ShortcutsWindow? _window;

    public void Show()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        _window = new ShortcutsWindow();
        _window.Closed += (_, _) => _window = null;
        _window.Show(owner);
    }
}
