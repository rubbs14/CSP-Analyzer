namespace CspAnalyzer.Desktop.Services;

/// <summary>No-op for the Avalonia design-time DataContext, where no real window exists.</summary>
public sealed class NullShortcutsWindowService : IShortcutsWindowService
{
    public void Show()
    {
    }
}
