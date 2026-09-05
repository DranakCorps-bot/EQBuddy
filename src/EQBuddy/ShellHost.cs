using System.Windows;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Opening (or fronting) the Evolved shell, and the one address it lands on.
///
/// **It lives here rather than in <c>MainWindow</c> for the reason the hotspot ratchet
/// exists.** The widget was at 4,699 lines against a 4,700 limit when E-3 opened — one
/// line of headroom, deliberately, because Fable's plan makes that number E-3's
/// decomposition budget. Opening a window the widget does not own is not window logic,
/// the same argument <c>FollowingSurfaces</c> and <c>WidgetDump</c> already carry.
///
/// **The shell has no player-facing door yet, and that is a decision rather than an
/// oversight** (logged in DECISIONS.md). Its rail has one row, Evolved is local-only
/// until the owner opens the channel, and a menu entry into a one-room shell is the
/// unexplained-empty the Phase 2 gate forbids. What it DOES have is a reviewable door —
/// <c>EQBUDDY_SHELL</c>, the same family as <c>EQBUDDY_PROGRESS</c> / <c>EQBUDDY_QUESTS</c>
/// — because a surface nobody can reach reads as reviewed anyway (trap 22) and an absent
/// control photographs as an unremarkable window (trap 29). The door for players lands
/// with the HUD's "Open EQBuddy", which is the PR after this one.
/// </summary>
internal static class ShellHost
{
    /// <summary>Open the shell, or front it if it is already up, on a <c>page:room</c>
    /// address. Null or unrecognised leaves it wherever it is — <see cref="ShellWindow.Navigate"/>
    /// refuses rather than snapping to a default.</summary>
    public static void Show(MainWindow main, string? address = null)
    {
        if (main._shellWindow is not { IsLoaded: true })
        {
            main._shellWindow = new ShellWindow(main);
            main._shellWindow.Closed += (_, _) => main._shellWindow = null;
            main._shellWindow.Show();
        }
        if (address is { Length: > 0 }) main._shellWindow.Navigate(address);
        main._shellWindow.Activate();
    }

    /// <summary>The review hook. <c>EQBUDDY_SHELL=1</c> opens the shell on its default
    /// room; <c>EQBUDDY_SHELL=progress:raids</c> opens it there, in the address grammar
    /// every other navigation path uses.</summary>
    public static void ApplyEnvHook(MainWindow main)
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHELL") is not { Length: > 0 } address)
            return;
        main.Loaded += (_, _) => main.Dispatcher.BeginInvoke(() =>
        {
            Show(main, address == "1" ? ShellPages.Address(ShellPage.Progress) : address);
            ApplySizeHook(main);
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// <c>EQBUDDY_SHELL_SIZE=580x480</c> — open the shell at a given size, so its
    /// DEGRADED state can be photographed.
    ///
    /// **This is the only way that state can be reached by a capture**, and it is the
    /// same argument every hook in <see cref="DebugHooks"/> is built on: a surface with
    /// no way to reach its state reads as reviewed anyway (trap 22). The rail collapsing
    /// to icons is a visual claim — a unit test can prove the arithmetic and cannot prove
    /// the wiring applied it, and the difference between those two is trap 42, which cost
    /// two builds.
    ///
    /// Sizes below the floor are clamped by WPF's own <c>MinWidth</c>/<c>MinHeight</c>,
    /// which is the behaviour under review rather than something to work around.
    /// </summary>
    private static void ApplySizeHook(MainWindow main)
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHELL_SIZE") is not { Length: > 0 } size
            || main._shellWindow is not { } shell) return;
        var parts = size.Split('x', 'X');
        if (parts.Length != 2
            || !double.TryParse(parts[0], out var w) || !double.TryParse(parts[1], out var h))
            return;
        shell.Width = w;
        shell.Height = h;
    }
}
