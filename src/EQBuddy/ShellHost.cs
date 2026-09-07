using System.Windows;

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
/// oversight** (logged in DECISIONS.md). Its rail has four rows of a planned seven (PR 3),
/// Evolved is local-only until the owner opens the channel, and a menu entry into a
/// half-built shell is the unexplained-empty the Phase 2 gate forbids — a count that rises
/// with each room does not change that answer, only the date it stops applying. What it
/// DOES have is a reviewable door —
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
            // The address goes to the CONSTRUCTOR. Building the window and then navigating
            // it would land on the default room first — building and painting a room nobody
            // asked for, which stopped being free the moment the default became Home.
            main._shellWindow = new ShellWindow(main, address);
            main._shellWindow.Closed += (_, _) => main._shellWindow = null;
            main._shellWindow.Show();
        }
        else if (address is { Length: > 0 }) main._shellWindow.Navigate(address);
        main._shellWindow.Activate();
    }

    /// <summary>
    /// The review hook. <c>EQBUDDY_SHELL=1</c> opens the shell on its default room;
    /// <c>EQBUDDY_SHELL=progress:raids</c> opens it there, in the address grammar every
    /// other navigation path uses.
    ///
    /// **The bare form passes NO address, and that is a fix rather than a tidy-up.** It used
    /// to translate <c>"1"</c> into an explicit <c>progress</c> address — a third copy of
    /// "what the default room is", in a third file, and the one every capture built on this
    /// hook actually exercises. Nothing forced it to agree with
    /// <see cref="ShellWindow"/>'s own default, so a screenshot taken through it would have
    /// gone on showing the old room after the window changed, and looked entirely healthy
    /// doing it (trap 24's shape, reached through the hook instead of through a title).
    /// **A hook that says "open on the default" must not know what the default is** — so
    /// <see cref="Show"/> is called with none and the window's constructor is the only
    /// place the answer exists. <c>TheShellOpensOnHomeWithNoAddressAtAll</c> is the E2E that
    /// walks this exact path; before E-3 PR 4 nothing did.
    /// </summary>
    public static void ApplyEnvHook(MainWindow main)
    {
        var address = Environment.GetEnvironmentVariable("EQBUDDY_SHELL");
        // **`EQBUDDY_SETUP=1` — the first-run Setup screen (OE-6), and it OPENS THE SHELL on
        // its own** rather than requiring both names. Setup is not a room and has no
        // address, so there is nothing for `EQBUDDY_SHELL` to point at; and on a profile
        // that has already run a dump, or already dismissed it, the auto-launch predicate
        // correctly says no — which would leave the screen with no way to be photographed or
        // asserted at all (trap 22, and trap 29: an absent screen photographs as an ordinary
        // room). Set both when the room UNDER the screen matters to the picture.
        var setup = Environment.GetEnvironmentVariable("EQBUDDY_SETUP") == "1";
        if (address is not { Length: > 0 } && !setup) return;
        main.Loaded += (_, _) => main.Dispatcher.BeginInvoke(() =>
        {
            Show(main, address is { Length: > 0 } && address != "1" ? address : null);
            ApplySizeHook(main);
            // AFTER Show, and it is a forced open rather than a re-run of the predicate:
            // the hook's whole job is reaching a state the predicate would refuse.
            if (setup) main._shellWindow?.ShowSetup();
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
