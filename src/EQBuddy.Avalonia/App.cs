using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

public sealed class App : Application
{
    private static readonly string ErrorLog = Core.AppPaths.File("error.log");

    public static void LogError(object? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLog)!);
            File.AppendAllText(ErrorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        EQBuddy.Core.CoreLog.Sink = LogError;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };

        // Applied before MainWindow is constructed so the saved theme is already live
        // for the very first frame (mirrors the WPF app's App.xaml.cs).
        try { AppTheme.Apply(Core.AppSettings.Load()); }
        catch (Exception ex) { LogError(ex); }

        // The profile claim happens in Program.Main, before Avalonia is built — standing
        // down here meant calling Shutdown() before the main loop existed, which crashed
        // rather than exited (see Program).
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
