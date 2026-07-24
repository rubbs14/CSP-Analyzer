using Avalonia;
using Avalonia.Dialogs;
using System;

namespace CspAnalyzer.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Linux's native file dialogs go through the xdg-desktop-portal
        // DBus service, which on some desktop environments (observed on
        // Cinnamon) never returns and leaves the calling window stuck with
        // no visible dialog - looking exactly like the Export/Save buttons
        // do nothing. The managed (Avalonia-drawn) dialog sidesteps the
        // portal entirely, so it always renders. Windows/macOS keep their
        // native dialogs, which don't have this failure mode.
        if (OperatingSystem.IsLinux())
        {
            builder = builder.UseManagedSystemDialogs();
        }

        return builder;
    }
}
