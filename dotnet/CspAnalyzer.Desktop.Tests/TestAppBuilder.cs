using Avalonia;
using Avalonia.Headless;
using CspAnalyzer.Desktop;
using CspAnalyzer.Desktop.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CspAnalyzer.Desktop.Tests;

/// <summary>
/// Headless Avalonia bootstrap for this test project - reusable across any
/// future UI test, not just the Appearance ones this was added for. Any new
/// [AvaloniaFact] test in this assembly rides on this same headless
/// platform (no real display, no window manager needed), which is what
/// lets UI interaction be tested in CI/headless environments where earlier
/// sessions had to fall back to manual gnome-screenshot verification (see
/// SESSIONS.md S7-S10b history - no xdotool/GUI automation on this box).
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
