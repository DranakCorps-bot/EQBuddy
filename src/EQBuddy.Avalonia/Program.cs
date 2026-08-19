using Avalonia;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

internal static class Program
{
    /// <summary>Held for the process's lifetime — Main blocks until the app exits, so a
    /// local is exactly the right lifetime. Releasing it is what lets the next launch
    /// claim the profile.</summary>
    private static IDisposable? _instanceLock;

    [STAThread]
    public static void Main(string[] args)
    {
        // The profile claim happens BEFORE Avalonia exists, and that is the point.
        //
        // It used to run inside App.OnFrameworkInitializationCompleted, which stood a
        // second copy down with desktop.Shutdown() — before the main loop had started.
        // StartCore then entered a dispatcher that was already shut down and the process
        // died with an unhandled InvalidOperationException and a stack trace, where the
        // whole intent was to exit quietly and let the running copy surface.
        //
        // It had been that way since the guard was added and nobody had seen it, because
        // on Linux and macOS you have to launch a second copy to get there. Making the
        // WPF build share this same lock (2026-08-19) turned it into the COMMON case:
        // one widget already running is exactly when someone starts the other one.
        //
        // Here there is no dispatcher to shut down. Standing down is a return.
        if (!ClaimSingleInstance()) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Second launches surface the running copy instead of starting a twin — the usual
    /// reason to relaunch is that the widget is hidden behind a fullscreen game.
    ///
    /// <see cref="SingleInstance"/> is keyed on the profile directory and used by BOTH
    /// builds, so it holds across toolkits: an EQBuddy already running on this profile
    /// stops this one whether it is the WPF widget or this one. An isolated
    /// EQBUDDY_APPDATA instance is a different profile and still runs alongside — that is
    /// how the app gets tested.
    /// </summary>
    private static bool ClaimSingleInstance()
    {
        try
        {
            _instanceLock = SingleInstance.TryClaim(Core.AppPaths.Dir);
            if (_instanceLock is not null) return true;

            // Held — but only stand down if a live copy actually answers. A stale lock
            // file must never be the reason EQBuddy won't launch.
            if (SingleInstance.AskRunningCopyToShow(Core.AppPaths.Dir, TimeSpan.FromSeconds(4)))
                return false;

            App.LogError("Another EQBuddy holds this profile's lock but did not answer a " +
                "show request; starting anyway.");
            return true;
        }
        catch (Exception ex)
        {
            // Never let instance coordination stop the app from starting.
            App.LogError(ex);
            return true;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
