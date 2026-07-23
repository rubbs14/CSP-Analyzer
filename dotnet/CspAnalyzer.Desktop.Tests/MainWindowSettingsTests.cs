using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using CspAnalyzer.Desktop.Models;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

public class MainWindowSettingsTests
{
    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsThemeVariant()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Dark" });
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Light" });
        Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);

        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "System" });
        Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsBackgroundColor_WhenHexProvided()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = "#1B2A38" });

        var brush = Assert.IsType<SolidColorBrush>(window.Background);
        Assert.Equal(Color.Parse("#1B2A38"), brush.Color);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_ClearsBackground_WhenHexIsNull()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = "#1E1E2E" });

        window.ApplyAppearanceSettings(new AppSettings { BackgroundColorHex = null });

        Assert.False(window.Background is SolidColorBrush b && b.Color == Color.Parse("#1E1E2E"));
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsWindowSizeAndPosition()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { WindowWidth = 1600, WindowHeight = 900, WindowX = 42, WindowY = 84 });

        Assert.Equal(1600, window.Width);
        Assert.Equal(900, window.Height);
        Assert.Equal(new PixelPoint(42, 84), window.Position);
    }

    [AvaloniaFact]
    public void ApplyAppearanceSettings_SetsMaximizedState()
    {
        var window = new MainWindow();
        window.Show();

        window.ApplyAppearanceSettings(new AppSettings { WindowState = "Maximized" });

        Assert.Equal(Avalonia.Controls.WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void PopulateAppearanceSettings_RoundTripsThemeAndColor()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { ThemeVariant = "Dark", BackgroundColorHex = "#1B2A38" });

        var gathered = new AppSettings();
        window.PopulateAppearanceSettings(gathered);

        Assert.Equal("Dark", gathered.ThemeVariant);
        var reapplied = new MainWindow();
        reapplied.Show();
        reapplied.ApplyAppearanceSettings(gathered);
        var brush = Assert.IsType<SolidColorBrush>(reapplied.Background);
        Assert.Equal(Color.Parse("#1B2A38"), brush.Color);
    }

    [AvaloniaFact]
    public void PopulateAppearanceSettings_CapturesWindowGeometry()
    {
        var window = new MainWindow();
        window.Show();
        window.ApplyAppearanceSettings(new AppSettings { WindowWidth = 1500, WindowHeight = 850 });

        var gathered = new AppSettings();
        window.PopulateAppearanceSettings(gathered);

        Assert.Equal(1500, gathered.WindowWidth);
        Assert.Equal(850, gathered.WindowHeight);
    }
}
