using Avalonia.Controls;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaHelpWindowService(Window owner) : IHelpWindowService
{
    // See AvaloniaAboutWindowService's comment - same fix, same reason.
    private HelpWindow? _window;

    public void Show()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        _window = new HelpWindow();
        _window.Closed += (_, _) => _window = null;
        _window.Show(owner);
    }
}
