namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the Shortcuts window (S11c) - mirrors IResultsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IShortcutsWindowService
{
    void Show();
}
