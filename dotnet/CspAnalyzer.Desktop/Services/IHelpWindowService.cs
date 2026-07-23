namespace CspAnalyzer.Desktop.Services;

/// <summary>Opens the Help window (S11d) - mirrors IAboutWindowService/IShortcutsWindowService's reasoning: keeps MainViewModel usable with no live Window (design-time, tests).</summary>
public interface IHelpWindowService
{
    void Show();
}
