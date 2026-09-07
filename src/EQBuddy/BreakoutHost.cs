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
/// </summary>
internal sealed class BreakoutHost(MainWindow main, AppSettings settings)
{
    private readonly Dictionary<BreakoutKind, BreakoutWindow> _windows = new();

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
    /// minimized and its condition holds — a star for the stat kinds, any 📌-pinned rule
    /// for the Watch list — unless ✕-disabled (persistent, re-enable in Options: the old
    /// until-next-minimize dismissal made the window whack-a-mole, discussion #45) or
    /// hidden with the game unfocused.</summary>
    public void Update(StatsSnapshot s)
    {
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            // Which star opens which window comes from UI.Shared, not from a switch here.
            // It was a switch here, and Options grew a tick box for the same question that
            // could not answer it — so a player ticking "Pet" changed nothing and went to
            // ask on Reddit. Since SA-1 the two halves are separate conditions rather than
            // one ternary: Damage and Healing have no star (dps/hps are always-on HUD
            // numbers), so "no star" and "needs a pinned rule" stopped being one case.
            var name = BreakoutPresentation.Kind(kind);
            var want = settings.Minimized && !main._hiddenForFocus &&
                       !settings.DisabledBreakouts.Contains(kind.ToString())
                       && (BreakoutPresentation.StarKey(name) is not { } star
                           || settings.MiniStats.Contains(star))
                       // A pinned rule is the WHOLE Watch condition since SA-R — see WatchPinMigration.
                       && (!BreakoutPresentation.NeedsPinnedRule(name)
                           || settings.TrackedRules.Any(r => r.Enabled && r.Pinned));
            _windows.TryGetValue(kind, out var w);
            if (want)
            {
                if (w is not { IsLoaded: true })
                {
                    _windows[kind] = w = new BreakoutWindow(settings, kind) { Main = main };
                    w.Dismissed += k =>
                    {
                        if (!settings.DisabledBreakouts.Contains(k.ToString()))
                            settings.DisabledBreakouts.Add(k.ToString());
                        settings.Save();
                        // OE-1 lock 7: a float this bar popped has been closed, so the bar
                        // goes back to just the bar. Raised BEFORE the nag returns below,
                        // or the one path that returns early would skip it.
                        Dismissed?.Invoke(k);
                        // With double-click-toggle on, the ✕ is no longer a one-way trap —
                        // a double-click on the chip brings the window straight back — so the
                        // nag and its log entry would just be noise. Off, they stay: the ✕ is
                        // a small target over a game screen, and until the alert existed the
                        // only trace of hitting it was a window that quietly never came back
                        // (David lost his DPS breakout to exactly that, 2026-08-08). A
                        // permanent, hard-to-reverse state change must announce itself.
                        if (settings.DoubleClickChipsToggleBreakouts) return;
                        main.AlertTile.ShowAlert($"{k} breakout hidden — re-enable in {BreakoutPresentation.ReEnableRoute}");
                        CoreLog.Error($"{k} breakout hidden via its close button (re-enable: {BreakoutPresentation.ReEnableRoute})");
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

    /// <summary>Toggle a stat's breakout window from its mini chip: show it if hidden,
    /// hide it if showing. Rides the same persistent DisabledBreakouts flag the ✕ uses,
    /// so the choice sticks and <see cref="Update"/> applies it on the spot — a
    /// double-click is just a friendlier reach for it than the ✕ or Options
    /// (asked for: "let me pop the DPS or Loot window up only when I want it").</summary>
    public void Toggle(BreakoutKind kind)
    {
        var name = kind.ToString();
        if (!settings.DisabledBreakouts.Remove(name))
            settings.DisabledBreakouts.Add(name);
        settings.Save();
        Apply();
    }

    /// <summary>
    /// SHOW it — OE-1's ⧉, which is <see cref="Toggle"/> with the toggle taken out.
    ///
    /// The pop-out has one destination and no second meaning: "the float carries the detail"
    /// (lock 6). Reusing <see cref="Toggle"/> would have made the first ⧉ on a DPS float the
    /// widget had already auto-shown *close* it — the default profile ships
    /// <c>DisabledBreakouts = ["Healing"]</c>, so a minimized widget already has the Damage
    /// window up and that is precisely the common case.
    /// </summary>
    public void Open(BreakoutKind kind)
    {
        settings.DisabledBreakouts.Remove(kind.ToString());
        settings.Save();
        Apply();
        if (_windows.TryGetValue(kind, out var w) && w.IsVisible) w.Activate();
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
