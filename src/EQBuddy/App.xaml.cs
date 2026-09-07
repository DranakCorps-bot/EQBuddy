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
        // The probe runs WITHOUT the single-instance lock, so it must not write: Load
        // persists migrations and generated rule ids, which is a whole-file Save under a
        // live widget — trap 13 exactly. Narrow (only when the probe exe is newer than the
        // running widget) and closed rather than argued about. Fable 5 found this against
        // my own claim that Load never saves; it does, at the bottom of Load.
        var settings = Core.AppSettings.Load(persistMigrations: !probing);
        // And under Wine only: whole-pixel glyph positioning. Wine truncates the
        // fractional advances WPF's default Ideal mode relies on, which pulls letters
        // apart mid-word ("an d th is") in text whose font metrics are perfectly correct
        // — see TextRenderingPolicy. It reads a setting, so it has to come after the
        // load; it must still come before any window is constructed, and MainWindow is
        // built at the bottom of this method.
        WineText.ApplyIfNeeded(settings);
        // And on every platform: bound how long a tooltip may stay up. WPF's own default
        // for it is int.MaxValue ms, which overflows the int32 arithmetic behind
        // DispatcherTimer and hands the ONE Win32 timer every DispatcherTimer on this
        // thread shares to a due date 24 days out — killing the 1 s tick and the 50 ms
        // Mobile pump while the window goes on painting and answering clicks. See
        // ToolTipPolicy for the arithmetic and CLAUDE.md trap 63 for how CI caught it.
        // Reads no setting, so it could sit higher up; it is here because it belongs to
        // the same "decide it once, before any window exists" group as the two above.
        ToolTipDefaults.ApplyOnce();
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
            // The SAME settings instance, not a second Load: the probe used to load
            // again to answer "what does the policy say", which doubled the write window
            // above for no gain.
            MainWindow = new TextProbeWindow(settings);
            MainWindow.Show();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
