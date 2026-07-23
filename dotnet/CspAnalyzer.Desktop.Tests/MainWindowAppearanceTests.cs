using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CspAnalyzer.Desktop.Views;
using Xunit;

namespace CspAnalyzer.Desktop.Tests;

/// <summary>
/// Real-click coverage for the Appearance section added to MainWindow's
/// sidebar (Light/Dark/System theme buttons + background color swatches).
/// Uses Avalonia.Headless to construct a real MainWindow, show it, and
/// raise actual Button.ClickEvent on the controls found by walking the
/// visual tree - not just asserting the view-model/code-behind logic in
/// isolation. Pattern is reusable: any future MainWindow control can be
/// found the same way (GetVisualDescendants + a predicate) and clicked the
/// same way (RaiseEvent(Button.ClickEvent)).
/// </summary>
public class MainWindowAppearanceTests
{
    private static Button FindButtonByContent(Window window, string content) =>
        window.GetVisualDescendants().OfType<Button>().First(b => (b.Content as string) == content);

    private static Button FindButtonByTag(Window window, string tag) =>
        window.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as string) == tag);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void LightButton_SetsThemeVariantToLight()
    {
        var window = new MainWindow();
        window.Show();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

        Click(FindButtonByContent(window, "Light"));

        Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void DarkButton_SetsThemeVariantToDark()
    {
        var window = new MainWindow();
        window.Show();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;

        Click(FindButtonByContent(window, "Dark"));

        Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void SystemButton_SetsThemeVariantToDefault()
    {
        var window = new MainWindow();
        window.Show();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

        Click(FindButtonByContent(window, "System"));

        Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void ColorSwatch_SetsWindowBackgroundToMatchingColor()
    {
        var window = new MainWindow();
        window.Show();

        Click(FindButtonByTag(window, "#1B2A38"));

        var brush = Assert.IsType<SolidColorBrush>(window.Background);
        Assert.Equal(Color.Parse("#1B2A38"), brush.Color);
    }

    [AvaloniaFact]
    public void ResetButton_ClearsCustomBackground()
    {
        var window = new MainWindow();
        window.Show();
        Click(FindButtonByTag(window, "#1E1E2E"));
        Assert.IsType<SolidColorBrush>(window.Background);

        Click(FindButtonByContent(window, "Reset"));

        Assert.False(window.Background is SolidColorBrush b && b.Color == Color.Parse("#1E1E2E"));
    }
}
