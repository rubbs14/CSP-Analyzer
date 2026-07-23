using CspAnalyzer.Desktop.ViewModels;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Opens the S10 results window without MainViewModel constructing an
/// Avalonia Window directly - mirrors IFilePickerService's reasoning (S8):
/// keeps the ViewModel usable in a no-window context (design-time, tests).
/// </summary>
public interface IResultsWindowService
{
    void Show(ResultsViewModel viewModel);
}
