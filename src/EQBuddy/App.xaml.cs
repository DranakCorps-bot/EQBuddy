using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace EQBuddy;

public partial class App : Application
{
    private static readonly string ErrorLog = Core.AppPaths.File("error.log");

    /// <summary>Held for the process's lifetime — releasing it is what lets the next
    /// launch claim the profile.</summary>
    private IDisposable? _instanceLock;

    public static void LogError(object? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLog)!);
            File.AppendAllText(ErrorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { /* never crash on logging */ }
    }

    /// <summary>
    /// Only one EQBuddy per profile. A second copy would tail the same logs twice, fight
    /// over the global hotkeys, and race on settings.json — and double-clicking the
    /// shortcut again is the obvious thing to do when the widget is hidden or behind the
    /// game. Instead the second copy asks the first to show itself, then exits.
    ///
    /// Keyed on the profile directory, not the machine, so an isolated EQBUDDY_APPDATA
    /// instance still runs alongside a normal one — that's how the app gets tested.
    ///
    /// **This was a named mutex, and a named mutex is invisible to the other build.**
    /// The Avalonia app guards the same profile with <see cref="SingleInstance"/>'s lock
    /// FILE, so on Windows the two mechanisms could not see each other and both widgets
    /// ran at once: two tailers on one log, two whole-file writers racing on
    /// settings.json, and two servers wanting the EQBuddy Mobile port. David's error.log
    /// has all three — the settings-overwrite warning of trap 13 fired twice, each time
    /// directly after a line only the Avalonia build writes, and the companion's
    /// "Only one usage of each socket address" sits at the same timestamps (2026-08-19).
    ///
    /// One mechanism now, the shared one, so the guard is per PROFILE and not per
    /// toolkit. The running copy picks the request up on its own tick (MainWindow), so
    /// there is no waiter thread and no mutex.
    /// </summary>
    private bool ClaimSingleInstance()
    {
        try
        {
            _instanceLock = EQBuddy.UI.Shared.SingleInstance.TryClaim(Core.AppPaths.Dir);
            if (_instanceLock is not null) return true;

            // Held — but only stand down if a live copy actually ANSWERS. A stale lock
            // file must never be the reason EQBuddy won't launch: a widget that will not
            // start is a far worse bug than two of them.
            if (EQBuddy.UI.Shared.SingleInstance.AskRunningCopyToShow(
                    Core.AppPaths.Dir, TimeSpan.FromSeconds(4)))
                return false;

            Core.CoreLog.Error("Another EQBuddy holds this profile's lock but did not " +
                "answer a show request; starting anyway.");
            return true;
        }
        catch (Exception ex)
        {
            // Never let instance coordination stop the app from starting.
            LogError(ex);
            return true;
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Every EQBuddy window but Session History is layered (AllowsTransparency), which
        // WPF renders in software. The lone hardware-rendered window came up solid white —
        // fully laid out per UI Automation, blank on screen — on a machine whose GPU path
        // WPF stopped driving after a driver/OS update (2026-08-01: reproduced across app
        // builds three months apart, cured by WPF's DisableHWAcceleration switch). One
        // rendering path for everything: for a widget this size the cost is unmeasurable,
        // and a History window that always paints beats a hardware path only one window
        // ever used.
        System.Windows.Media.RenderOptions.ProcessRenderMode =
            System.Windows.Interop.RenderMode.SoftwareOnly;
        // Under Wine only: swap in the bundled icon font so section icons render
        // instead of boxing — the whole story lives in WineFonts.cs.
        WineFonts.ApplyIfNeeded(Resources);
        Core.CoreLog.Sink = LogError;
        // The probe reads settings and never writes them, so it does not need the
        // profile lock — and taking it would make the diagnostic impossible to run in
        // the situation people actually run it in, with the widget already up.
        var probing = TextProbeWindow.Requested(e.Args);
        if (!probing && !ClaimSingleInstance())
        {
            Shutdown();
            return;
        }
        var settings = Core.AppSettings.Load();
        // Under Wine only, and only when opted in: float the widget over a fullscreen
        // game and stop clicks from foregrounding the Wine process — see WineOverlay.cs.
        // Inert on Windows and off by default.
        WineOverlay.Configure(settings);
        // Applied before MainWindow is constructed, so the saved theme is already live for
        // the very first frame. There's no StartupUri (App.xaml) creating that window for
        // us — WPF queues a StartupUri window's construction independently of OnStartup, so
        // it could still land even after the ClaimSingleInstance() bailout above, crashing
        // on a theme that was never applied. Building it explicitly here, only on the
        // success path, closes that race.
        // The design tokens (type roles, spacing, radii, control sizes) composed from
        // EQBuddy.UI.Shared.DesignTokens. Static — a theme switch repaints, it does not
        // re-scale — so this is merged once here rather than swapped like the palette,
        // and it must land before any window is built: Theme.xaml's Eq* components
        // resolve their sizes out of it.
        try { Resources.MergedDictionaries.Add(DesignSystem.Tokens()); }
        catch (Exception ex) { LogError(ex); }
        try { ThemeManager.Apply(settings); }
        catch (Exception ex) { LogError(ex); }
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };
        // Opt-in diagnostic, inert unless asked for: one window that says which font WPF
        // resolved for each weight and how the same sentence renders under every text
        // mode. It replaces the widget rather than joining it, so the picture is of the
        // probe and nothing else. See TextProbeWindow.cs.
        if (probing)
        {
            MainWindow = new TextProbeWindow();
            MainWindow.Show();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
