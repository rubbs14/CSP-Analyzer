using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Services;
using CspAnalyzer.Desktop.ViewModels;
using CspAnalyzer.Desktop.Views;

namespace CspAnalyzer.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var settingsService = new SettingsService();
            var viewModel = new MainViewModel(
                new AvaloniaFilePickerService(window),
                new AvaloniaResultsWindowService(window),
                new AvaloniaConfirmDialogService(window),
                new AvaloniaAboutWindowService(window),
                new AvaloniaShortcutsWindowService(window),
                new AvaloniaHelpWindowService(window),
                new AvaloniaInfoDialogService(window),
                settingsService);
            window.DataContext = viewModel;

            AppSettings settings = settingsService.Load();
            window.ApplyAppearanceSettings(settings);
            viewModel.ApplySettings(settings);

            window.Closing += (_, _) =>
            {
                AppSettings toSave = viewModel.CurrentSettings();
                window.PopulateAppearanceSettings(toSave);
                settingsService.Save(toSave);
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}