using Avalonia.Controls;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaResultsWindowService(Window owner) : IResultsWindowService
{
    // A fresh window per click (the original behavior) left a pile of
    // stale-data Results windows behind every repeat Export click - reuse
    // the single existing instance (refreshing its data) instead.
    private ResultsWindow? _window;

    public void Show(ResultsViewModel viewModel)
    {
        if (_window is not null)
        {
            _window.DataContext = viewModel;
            _window.Activate();
            return;
        }

        _window = new ResultsWindow { DataContext = viewModel };
        _window.Closed += (_, _) => _window = null;
        _window.Show(owner);
    }
}
