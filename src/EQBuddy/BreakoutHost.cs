using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// THE SIX FLOATING STAT WINDOWS' lifecycle — who is open, who should be, and the two ways
/// a player turns one on or off.
///
/// **Lifted out of <c>MainWindow</c> verbatim for OE-1**, and the lift is what pays for the
/// feature: the widget sat at 4,283 lines against a 4,284 ceiling, so the mini-bar expand
/// had one line of headroom and the standing move is to lift a surface rather than raise the
/// bar. This is the surface OE-1 extends — the under-bar panel's ⧉ pops to one of these
/// windows — so it is the honest one to take, and <c>ArchitectureTests.Hotspots</c> comes
/// down in the same commit or the freed room quietly refills.
///
/// **A view class, not another <c>MainWindow.*.xaml.cs</c> partial**: <c>ArchitectureTests</c>
/// SUMS the glob's matches on purpose, so a partial buys nothing and leaves exactly as much
/// untestable window logic as before.
///
/// **Nothing here is a rewrite.** The gate, its comments and the ✕'s nag are the ones that
/// were in <c>UpdateBreakouts</c>; the only net-new member is <see cref="Open"/>, which is
/// <see cref="Toggle"/> without the toggle — OE-1's pop-out needs "show this window" and a
/// toggle would have closed an already-open float on the first ⧉.
///
/// **Auto-show-while-minimized is untouched**, which is the owner's explicit constraint on
/// OE-1 (restated in the #347 sign): the mini-bar expand ADDS states, it does not replace
/// this path.
///
/// **OE-7 changed WHO writes the off-switch, and nothing else about the gate.** The ✕ used
/// to add the kind to <c>AppSettings.DisabledBreakouts</c> and announce it, because
/// auto-show was the only thing that could bring a float back and a dismissal that lasted
/// until the next minimize was discussion #45's whack-a-mole. Every kind has a HUD chip
/// that summons it now (<see cref="HudExpandTarget"/>), so the ✕ is a transient close held
/// in <see cref="_intent"/> — in memory, never persisted — and
/// <c>AppSettings.DisabledBreakouts</c> has ONE writer. That setting stops meaning "this
/// window may exist" and starts meaning "open it without being asked", which is the only
/// thing a switch could honestly promise once a chip can summon one.
///
/// **DRA-352 D2 moved that one writer, and nothing else about the gate.** It was the Options
/// Floating windows tick list; the Founder asked for the list off Options, so the write
/// moved HERE first (traps 20/26) — <see cref="SetAutoOpen"/>, raised by the pin on each
/// window's title bar, over <see cref="BreakoutAutoOpen"/>'s rule, which is the same two
/// halves the list wrote. The ✕ still writes nothing. Summoning a window does not write
/// either: the plan offered "opening it clears the disable", and that was declined because
/// every chip peek would then become a persistent edit — the exact permanence OE-7 took
/// off the double-click — while the pin on the summoned window already IS the way back.
/// </summary>
internal sealed class BreakoutHost(MainWindow main, AppSettings settings)
{
    private readonly Dictionary<BreakoutKind, BreakoutWindow> _windows = new();

    /// <summary>
    /// **What the PLAYER asked for this run, overriding the auto rule** — true from a chip
    /// summon or a ⧉, false from a ✕. Absent means "whatever the pin and the stars say".
    ///
    /// **It is deliberately not persisted, and it deliberately does not clear on
    /// un-minimize.** Persisting it would be <c>DisabledBreakouts</c> with a second name —
    /// the trap 20/26 shape where one setting grows a shadow — and the pin is the switch
    /// that is meant to stick. Clearing it on un-minimize would be worse: a float you closed
    /// coming back the next time you minimize is discussion #45's whack-a-mole verbatim, and
    /// #45 is the reason the ✕ was made permanent in the first place. So it lasts as long as
    /// EQBuddy is running, which is the same lifetime <c>HudExpand</c> gives every other
    /// decision the bar makes.
    /// </summary>
    private readonly Dictionary<BreakoutKind, bool> _intent = new();

    /// <summary>The kinds a ✕ has closed this run, for the <c>EQBUDDY_EXPAND</c> dump. A
    /// COUNT rather than a boolean, so an assertion can tell "one float was closed" from
    /// "the gate collapsed and closed all six".</summary>
    public int ClosedCount => _intent.Count(kv => !kv.Value);

    /// <summary>Raised when a player ✕-dismisses a float. OE-1's lock 7 — "close floated
    /// window → just the mini-bar, nothing expanded" — is the only subscriber, and it is
    /// keyed on the kind so a ✕ on a float the bar has moved on from collapses nothing.
    /// </summary>
    public event Action<BreakoutKind>? Dismissed;

    /// <summary>The window for a kind while it is on screen, or null. The Buffs editor asks,
    /// because an edit made in Options must repaint the float NOW rather than a tick later.
    /// </summary>
    public BreakoutWindow? Visible(BreakoutKind kind) =>
        _windows.TryGetValue(kind, out var w) && w.IsVisible ? w : null;

    /// <summary>Open/refresh/hide the breakout windows: each shows while the widget is
    /// minimized and the player has not closed it this run — and, when they have expressed
    /// no preference, while its auto condition holds: pinned on the window, a star for
    /// the stat kinds, any 📌-pinned rule for the Watch list. Hidden with the game
    /// unfocused, whatever any of that says.</summary>
    public void Update(StatsSnapshot s)
    {
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            // The player's own answer wins over the auto rule, in both directions: a ✕
            // closes a window the pin would open, and a chip summon opens one that is
            // unpinned. The second half is what stops a summon being a silent no-op —
            // ticking a box is how you say "open it for me", and clicking its chip is how
            // you say "open it NOW", and only one of those is a preference.
            var want = settings.Minimized && !main._hiddenForFocus
                       && (_intent.TryGetValue(kind, out var asked) ? asked : AutoWants(kind));
            _windows.TryGetValue(kind, out var w);
            if (want)
            {
                if (w is not { IsLoaded: true })
                {
                    _windows[kind] = w = new BreakoutWindow(settings, kind) { Main = main };
                    w.AutoOpenChanged += SetAutoOpen;
                    w.Dismissed += k =>
                    {
                        // TRANSIENT (OE-7). No settings write, no Save, and no banner:
                        // the ✕ closed a window whose own HUD chip is still on the bar
                        // waiting to bring it back, so there is nothing to warn about.
                        //
                        // The banner that used to fire here — and its error-log twin —
                        // existed because the ✕ was permanent and hard to reverse (David
                        // lost his DPS float to exactly that, 2026-08-08). It was already
                        // suppressed whenever DoubleClickChipsToggleBreakouts was on, on
                        // the stated grounds that a chip could bring the window back; the
                        // chip is unconditional now, so the exception became the rule.
                        _intent[k] = false;
                        // OE-1 lock 7: a float this bar popped has been closed, so the bar
                        // goes back to just the bar.
                        Dismissed?.Invoke(k);
                    };
                }
                if (!w.IsVisible) w.Show();
                w.Update(s);
            }
            else if (w is { IsVisible: true })
            {
                w.SavePosition();
                w.Hide();
            }
        }
    }

    /// <summary>
    /// **Would this kind open on its own?** — the window's pin (<see cref="BreakoutAutoOpen"/>,
    /// which is DisabledBreakouts plus the star), and the Watch list's pinned rule, and
    /// nothing about what the player has clicked this run.
    ///
    /// Which star opens which window comes from UI.Shared, not from a switch here. It was a
    /// switch here, and Options grew a tick box for the same question that could not answer
    /// it — so a player ticking "Pet" changed nothing and went to ask on Reddit. Since SA-1
    /// the two halves are separate conditions rather than one ternary: Damage and Healing
    /// have no star HERE, so "no star" and "needs a pinned rule" stopped being one case.
    /// Since DRA-81's Founder LOCK `dps` and `hps` are ★s again — what they are not is
    /// ★s that gate a WINDOW, which is a decision `BreakoutPresentation.StarKey` now
    /// carries on purpose rather than by the keys not existing.
    /// </summary>
    private bool AutoWants(BreakoutKind kind)
    {
        var name = BreakoutPresentation.Kind(kind);
        return BreakoutAutoOpen.IsOn(settings, kind.ToString())
               // A pinned rule is the WHOLE Watch condition since SA-R — see WatchPinMigration.
               && (!BreakoutPresentation.NeedsPinnedRule(name)
                   || settings.TrackedRules.Any(r => r.Enabled && r.Pinned));
    }

    /// <summary>Toggle a stat's window from its HUD chip: show it if hidden, hide it if
    /// showing (asked for: "let me pop the DPS or Loot window up only when I want it").
    ///
    /// **Transient since OE-7, and that is the only change.** It wrote
    /// <c>DisabledBreakouts</c> and saved, which made an opt-in double-click a persistent
    /// setting edit — the same permanence the ✕ had, reached by a gesture that reads as a
    /// glance. It flips this run's intent instead; the pin is where a choice sticks.</summary>
    public void Toggle(BreakoutKind kind)
    {
        var showing = _windows.TryGetValue(kind, out var w) && w.IsVisible;
        _intent[kind] = !showing;
        Apply();
    }

    /// <summary>
    /// SHOW it — OE-1's ⧉ and OE-7's chip summon, which is <see cref="Toggle"/> with the
    /// toggle taken out.
    ///
    /// The pop-out has one destination and no second meaning: "the float carries the detail"
    /// (lock 6). Reusing <see cref="Toggle"/> would have made the first ⧉ on a DPS float the
    /// widget had already auto-shown *close* it — the default profile ships
    /// <c>DisabledBreakouts = ["Healing"]</c>, so a minimized widget already has the Damage
    /// window up and that is precisely the common case.
    ///
    /// **It overrides an unpinned window, deliberately.** A ⧉ is the player asking for this
    /// window right now; refusing because a tick box says "not on its own" would be the ✕'s
    /// old one-way trap arriving from the other side, and there would be nothing on screen
    /// to say why the click did nothing. Nothing is written, so their tick still means what
    /// it meant the next time EQBuddy starts.
    /// </summary>
    public void Open(BreakoutKind kind)
    {
        _intent[kind] = true;
        Apply();
        if (_windows.TryGetValue(kind, out var w) && w.IsVisible) w.Activate();
    }

    /// <summary>
    /// **The one writer of <c>AppSettings.DisabledBreakouts</c>** (DRA-352 D2) — the pin on a
    /// floating window's title bar. The body is the Options tick list's <c>Set</c>, moved:
    /// the rule is <see cref="BreakoutAutoOpen.Set"/>, then save, then re-sync the widget's
    /// ★s because pinning a starred kind sets its star (the panel's ★ is the same setting
    /// seen from the other side, and must not go on showing the old one).
    ///
    /// **It does not touch this run's intent.** The window the pin is on is open and stays
    /// open; unpinning means "do not open it without being asked" from the next minimise,
    /// never "close it now" — that is the ✕'s job.
    /// </summary>
    public void SetAutoOpen(BreakoutKind kind, bool on)
    {
        if (BreakoutAutoOpen.Set(settings, kind.ToString(), on)) settings.Save();
        main.SyncStarsFromSettings();
        if (_windows.TryGetValue(kind, out var w)) w.SyncAutoOpenPin();
    }

    /// <summary>Re-run the gate against the tick's own snapshot, so a change reaches the
    /// screen now rather than up to a second later — a window that waits for the next tick
    /// reads as a click that did nothing.</summary>
    private void Apply()
    {
        if (main._latestSnapshot is { } snap) Update(snap);
    }

    /// <summary>Application exit. Each window persists its spot in its own Closed handler.
    /// </summary>
    public void CloseAll()
    {
        foreach (var w in _windows.Values) w.Close();
    }
}
