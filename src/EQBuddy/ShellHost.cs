using System.Windows;

namespace EQBuddy;

/// <summary>
/// Opening (or fronting) the Evolved shell, and the one address it lands on.
///
/// **It lives here rather than in <c>MainWindow</c> for the reason the hotspot ratchet
/// exists.** The widget was at 4,699 lines against a 4,700 limit when E-3 opened ΓÇö one
/// line of headroom, deliberately, because Fable's plan makes that number E-3's
/// decomposition budget. Opening a window the widget does not own is not window logic,
/// the same argument <c>FollowingSurfaces</c> and <c>WidgetDump</c> already carry.
///
/// **The shell's player door is the widget's context-menu row "Open EQBuddyΓÇª", and it
/// landed here in OE-2** (Bevel item 3, Helm-signed #347 item 3; the decision and its
/// reversal are both in DECISIONS.md). It used to have no door at all, deliberately: at
/// PR 1 the rail had ONE row, Evolved was local-only, and a menu entry into a one-room
/// shell is the unexplained-empty the Phase 2 gate forbids.
///
/// **That premise expired at SR-5 and nobody came back to re-check it** ΓÇö
/// <see cref="EQBuddy.UI.Shared.ShellPages.Landed"/> is the whole seven-room enum now. What
/// turned a stale deferral into a `must-fix` is what the Γ£ò does: the shell has native
/// chrome, closing it releases the rooms and clears <see cref="Window"/>, and with
/// <c>EQBUDDY_SHELL</c> as the only entrance the player who closed it was stranded until
/// they restarted EQBuddy. A window a player can close and cannot reopen is the
/// "silent no-ops are broken" rule with the switch on the other side.
///
/// **The door is a CONTEXT-MENU ROW because a hotkey is not a door** (trap 59): nothing is
/// bound by default, so an affordance that exists only once the player has configured
/// something is absent on the profile every player starts with. The row sits with
/// <c>WorldΓÇª</c> and <c>QuestsΓÇª</c>, which are there for the same reason.
///
/// <c>EQBUDDY_SHELL</c> stays beside it and is not replaced by it ΓÇö a surface nobody can
/// reach in a test or a shot reads as reviewed anyway (trap 22), and an absent control
/// photographs as an unremarkable window (trap 29), so the hook is what lets a capture
/// land on a named room without a human clicking one.
/// </summary>
internal static class ShellHost
{
    /// <summary>
    /// The Evolved shell, while one is open ΓÇö <c>null</c> the moment its Γ£ò is used.
    ///
    /// **It lives here rather than on <c>MainWindow</c> as of OE-2, which is what the
    /// field's own doc comment there had been asserting since PR 1**: *"Opened by
    /// <c>ShellHost</c>, never from here ΓÇö the widget does not own it."* A reference the
    /// widget holds, never assigns and never reads is not ownership, and the file it sat in
    /// is the one the hotspot ratchet exists to keep from absorbing things. Every reader
    /// (<see cref="FollowingSurfaces"/>, <c>WidgetDump</c>) asks the owner now.
    ///
    /// Static because the process has exactly one widget ΓÇö <c>SingleInstance</c> is what
    /// makes that true, and it is enforced per PROFILE rather than per toolkit (trap 13).
    /// </summary>
    internal static ShellWindow? Window { get; private set; }

    /// <summary>Open the shell, or front it if it is already up, on a <c>page:room</c>
    /// address. Null or unrecognised leaves it wherever it is ΓÇö <see cref="ShellWindow.Navigate"/>
    /// refuses rather than snapping to a default.</summary>
    public static void Show(MainWindow main, string? address = null)
    {
        if (Window is not { IsLoaded: true })
        {
            // The address goes to the CONSTRUCTOR. Building the window and then navigating
            // it would land on the default room first ΓÇö building and painting a room nobody
            // asked for, which stopped being free the moment the default became Home.
            Window = new ShellWindow(main, address);
            Window.Closed += (_, _) => Window = null;
            Window.Show();
        }
        else if (address is { Length: > 0 }) Window.Navigate(address);
        // **A MINIMIZED window is not fronted by `Activate`**, so without this the door
        // would do visibly nothing for the player who put the shell out of the way rather
        // than closing it ΓÇö "silent no-ops are broken" reached by the one state the Γ£ò path
        // does not cover. Normal rather than a remembered maximize: a shell that comes back
        // the wrong SIZE is a nuisance, and one that does not come back is the bug.
        if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
        Window.Activate();
    }

    /// <summary>
    /// THE PLAYER'S DOOR ΓÇö the widget's <c>Open EQBuddyΓÇª</c> context-menu row (OE-2).
    ///
    /// **It passes NO address, and that is the same fix <see cref="ApplyEnvHook"/>'s bare
    /// form carries**: a caller that means "open it" must not know what the default room
    /// is, or the answer exists in two places and only one of them gets taught when it
    /// changes. The window's constructor is the only place it lives.
    ///
    /// **On an already-open shell this FRONTS it and changes nothing else** ΓÇö no address,
    /// so <see cref="Show"/> reaches <c>Activate</c> without navigating. A door that
    /// snapped a player back to Home from the room they were reading would be a second
    /// defect wearing the fix's clothes, and it is what "recover the guidance hub" would
    /// mean if it were read as "go to Home".
    /// </summary>
    public static void OpenDoor(MainWindow main) => Show(main);

    /// <summary>
    /// The review hook. <c>EQBUDDY_SHELL=1</c> opens the shell on its default room;
    /// <c>EQBUDDY_SHELL=progress:raids</c> opens it there, in the address grammar every
    /// other navigation path uses.
    ///
    /// **The bare form passes NO address, and that is a fix rather than a tidy-up.** It used
    /// to translate <c>"1"</c> into an explicit <c>progress</c> address ΓÇö a third copy of
    /// "what the default room is", in a third file, and the one every capture built on this
    /// hook actually exercises. Nothing forced it to agree with
    /// <see cref="ShellWindow"/>'s own default, so a screenshot taken through it would have
    /// gone on showing the old room after the window changed, and looked entirely healthy
    /// doing it (trap 24's shape, reached through the hook instead of through a title).
    /// **A hook that says "open on the default" must not know what the default is** ΓÇö so
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
            ApplySizeHook();
            // AFTER Show, and it is a forced open rather than a re-run of the predicate:
            // the hook's whole job is reaching a state the predicate would refuse.
            if (setup) Window?.ShowSetup();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
