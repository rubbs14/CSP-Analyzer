namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the About window (S11c) - mirrors IResultsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IAboutWindowService
{
    void Show();
}
