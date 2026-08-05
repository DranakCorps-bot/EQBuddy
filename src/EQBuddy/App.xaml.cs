using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace EQBuddy;

public partial class App : Application
{
    private static readonly string ErrorLog = Core.AppPaths.File("error.log");

    private Mutex? _instanceLock;
    private EventWaitHandle? _showRequest;

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
    /// </summary>
    private bool ClaimSingleInstance()
    {
        try
        {
            var key = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(Core.AppPaths.Dir.ToLowerInvariant())))[..16];
            _instanceLock = new Mutex(initiallyOwned: true, $"Local\\EQBuddy_{key}", out var isFirst);
            _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\EQBuddyShow_{key}");

            if (!isFirst)
            {
                _showRequest.Set();   // ask the running copy to surface, then stand down
                return false;
            }

            // Background waiter: no UI thread cost while idle.
            var waiter = new Thread(() =>
            {
                while (_showRequest.WaitOne())
                    Dispatcher.BeginInvoke(() =>
                        (MainWindow as MainWindow)?.RestoreFromAnotherInstance());
            })
            { IsBackground = true, Name = "EQBuddy single-instance listener" };
            waiter.Start();
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
        Core.CoreLog.Sink = LogError;
        if (!ClaimSingleInstance())
        {
            Shutdown();
            return;
        }
        // Applied before MainWindow is constructed, so the saved theme is already live for
        // the very first frame. There's no StartupUri (App.xaml) creating that window for
        // us — WPF queues a StartupUri window's construction independently of OnStartup, so
        // it could still land even after the ClaimSingleInstance() bailout above, crashing
        // on a theme that was never applied. Building it explicitly here, only on the
        // success path, closes that race.
        try { ThemeManager.Apply(Core.AppSettings.Load()); }
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
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
