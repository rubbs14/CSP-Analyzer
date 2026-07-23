using CommunityToolkit.Mvvm.Input;

namespace CspAnalyzer.Desktop.ViewModels;

/// <summary>S11c: opens the About/Shortcuts windows, mirroring OpenResultsWindow's service-call pattern in MainViewModel.cs.</summary>
public partial class MainViewModel
{
    [RelayCommand]
    private void OpenAboutWindow() => _aboutWindowService.Show();

    [RelayCommand]
    private void OpenShortcutsWindow() => _shortcutsWindowService.Show();
}
