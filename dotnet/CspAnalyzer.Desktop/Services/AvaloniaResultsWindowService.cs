using Avalonia.Controls;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop.Services;

public sealed class AvaloniaResultsWindowService(Window owner) : IResultsWindowService
{
    public void Show(ResultsViewModel viewModel)
    {
        var window = new ResultsWindow { DataContext = viewModel };
        window.Show(owner);
    }
}
