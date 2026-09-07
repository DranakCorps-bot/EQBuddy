namespace EQBuddy.UI.Shared;

/// <summary>Which tracker the mini bar is expanding. The FIRST SHIP is these three and
/// only these three (owner lock 8, `BEVEL.md` §4 / `FABLE.md` OE-1): the owner tests the
/// mechanics on DPS, HPS and Progress before every other tracker follows on the same
/// pattern. Lock 9 forbids a one-off exception for any of the ones that come later, so
/// this enum grows and nothing else about the model does.</summary>
public enum HudExpandTarget
{
    /// <summary>The HUD glance's always-on DPS slot.</summary>
    Dps,

    /// <summary>The glance's third slot while healing owns it (<c>HudThird.Healing</c>).</summary>
    Hps,

    /// <summary>The glance's third slot while the XP rate owns it. Its pop-out is the
    /// Progress WINDOW, not a breakout — <c>Progress</c> left <c>BreakoutKind</c> on
    /// 2026-08-25 by a signed fold ("reuse the existing theme window on its current tab"),
    /// and re-adding it would revert that fold.</summary>
    Progress,
}

/// <summary>
/// THE MINI BAR'S EXPANSION — which tracker is showing under the bar, whether it is a
/// transient peek or a pinned panel, and whether the detail has been popped to a float.
///
/// **It is <see cref="ThemeHost{TTab}"/> with interaction rules ON it, not a second state
/// machine** (Fable's OE-1 framing, on Bevel's signed §4 shape). The placement — Collapsed
/// / Inline / Window — and the one invariant that matters (exactly one owner of the body)
/// stay ThemeHost's, unchanged and already tested. What lives here is the four verbs the
/// owner's locks 1–7 name, and the ONE piece of bookkeeping ThemeHost has no opinion about:
/// a peek is transient and a pin is not.
///
/// The owner's locks, each named where it is enforced:
/// <list type="number">
/// <item>**One under-bar expansion at a time.** Structural: one host, one
/// <see cref="Target"/>. There is no list here to grow a second entry in.</item>
/// <item>Chips look like buttons — the VIEW's job (<c>HudBarView</c>), not a state.</item>
/// <item>**Hover = peek** (<see cref="Hover"/>), **mouse-away = collapse**
/// (<see cref="Away"/>).</item>
/// <item>**Click = stay open** (<see cref="Click"/> sets <see cref="Pinned"/>).</item>
/// <item>**✕ on the panel = collapse back to the bar** (<see cref="Collapse"/>).</item>
/// <item>**Pop-out collapses the under-bar panel** (<see cref="PopOut"/> — ThemeHost's own
/// <c>PopOut</c> rule, which is why this is a delegation and not a re-decision).</item>
/// <item>**Closing the float leaves nothing expanded** (<see cref="WindowClosed"/> —
/// ThemeHost's <c>WindowClosed</c>, Collapsed and never silently back to Inline).</item>
/// </list>
///
/// Framework-free and unit-tested for the reason CLAUDE.md gives for every window sum: the
/// WPF layer has no unit tests (docs/TestPlan.md §5), so an interaction rule expressed only
/// in a mouse handler is a rule nothing can check — and every one of these seven arrived as
/// a sentence in an owner interview rather than as code, which is exactly the kind of rule
/// that rots silently.
/// </summary>
public sealed class HudExpand
{
    private readonly ThemeHost<HudExpandTarget> _host = new(HudExpandTarget.Dps);

    /// <summary>The tracker a CLICK pinned, or null when nothing is pinned. Held beside the
    /// placement rather than inside it because a peek and a pin are the same placement —
    /// Inline — differing only in what mouse-away does. That is an interaction rule, and
    /// giving it a fourth <see cref="ThemePlacement"/> would have reopened the signed shape
    /// to say something the shape was never about.</summary>
    private HudExpandTarget? _pinned;

    /// <summary>Where the body is right now. ThemeHost's, verbatim.</summary>
    public ThemePlacement Placement => _host.Placement;

    /// <summary>Which tracker the panel (or the popped float) is showing.</summary>
    public HudExpandTarget Target => _host.SelectedTab;

    public bool IsInline => _host.IsInline;
    public bool IsWindowOpen => _host.IsWindowOpen;

    /// <summary>The under-bar panel is up because a CLICK put it there, so mouse-away
    /// leaves it alone (lock 4). A peek over a pinned panel reads false while the pointer
    /// is elsewhere — see <see cref="Away"/>, which puts the pin back.</summary>
    public bool Pinned => _pinned is { } p && _host.IsInline && p == Target;

    /// <summary>The caller must bring the existing float forward instead of drawing
    /// anything — ThemeHost's answer to "they clicked while the window is up".</summary>
    public bool ShouldBringWindowForward => _host.ShouldBringWindowForward;

    /// <summary>The <c>hudExpand</c> dump fact: the tracker on screen, or "none". One word,
    /// because the dump is space-separated <c>key=value</c>.</summary>
    public string TargetKey => _host.Placement == ThemePlacement.Collapsed ? "none" : Key(Target);

    /// <summary>The <c>hudExpandMode</c> dump fact. Four words for four states a single
    /// boolean could not tell apart — and "peek" vs "pinned" is precisely the pair a
    /// screenshot cannot settle, since both render the same panel.</summary>
    public string ModeKey => _host.Placement switch
    {
        ThemePlacement.Window => "window",
        ThemePlacement.Inline => Pinned ? "pinned" : "peek",
        _ => "collapsed",
    };

    /// <summary>A target's one-word key, for the dump and for the <c>EQBUDDY_HUDEXPAND</c>
    /// hook. <see cref="TargetForKey"/> is the inverse and they are tested as a pair, so a
    /// name can never be readable in one direction only.</summary>
    public static string Key(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Hps => "hps",
        HudExpandTarget.Progress => "progress",
        _ => "dps",
    };

    /// <summary>The key back to a target, or null for anything else. Case-insensitive: it
    /// reads an environment variable a human types.</summary>
    public static HudExpandTarget? TargetForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "dps" => HudExpandTarget.Dps,
        "hps" => HudExpandTarget.Hps,
        "progress" or "xp" => HudExpandTarget.Progress,
        _ => null,
    };

    /// <summary>
    /// The tracker a <c>BreakoutKind</c> member belongs to, or null for the four kinds the
    /// bar does not expand.
    ///
    /// **Keyed by the enum member's NAME, for the reason <see cref="BreakoutPresentation"/>
    /// already gives**: the breakout enum is a WPF type, so taking it here would put a
    /// decision back inside the layer that cannot test it. Progress is deliberately absent
    /// on both sides — it left <c>BreakoutKind</c> by a signed fold on 2026-08-25.
    /// </summary>
    public static HudExpandTarget? TargetForBreakout(string enumMemberName) => enumMemberName switch
    {
        "Damage" => HudExpandTarget.Dps,
        "Healing" => HudExpandTarget.Hps,
        _ => null,
    };

    /// <summary>What the panel calls itself. The pop-out's tooltip names the destination in
    /// the "X is now Y" spirit — a player who pops the panel out should already know which
    /// window is about to appear.</summary>
    public static string Title(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Hps => BreakoutPresentation.Title(BreakoutPresentation.Healing),
        HudExpandTarget.Progress => BreakoutPresentation.Title(BreakoutPresentation.Progress),
        _ => BreakoutPresentation.Title(BreakoutPresentation.Damage),
    };

    /// <summary>The panel header's vector — the same one the bar's own chip wears, so the
    /// panel and the chip that opened it cannot be read as two different things.</summary>
    public static string Icon(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Hps => BreakoutPresentation.Icon(BreakoutPresentation.Healing),
        HudExpandTarget.Progress => BreakoutPresentation.Icon(BreakoutPresentation.Progress),
        _ => BreakoutPresentation.Icon(BreakoutPresentation.Damage),
    };

    /// <summary>Where ⧉ sends this tracker's detail, in words, for the pop-out's tooltip.
    /// Progress names a WINDOW rather than a float on purpose: it is a different destination
    /// and a tooltip that hid the difference would be the #233 "X is now Y" complaint inside
    /// a hover.</summary>
    public static string PopOutTip(HudExpandTarget target) => target == HudExpandTarget.Progress
        ? "Open the Progress window"
        : $"Open the floating {Title(target)} window";

    /// <summary>
    /// The pointer arrived on a chip — PEEK (lock 3).
    ///
    /// A peek over a PINNED panel is allowed and does not disturb the pin: lock 9 says every
    /// tracker answers a hover, and a bar where two of the chips went inert the moment you
    /// pinned a third would be the exception lock 9 forbids. <see cref="Away"/> puts the pin
    /// back when the pointer leaves.
    ///
    /// Inert while a float is up: the float IS the detail (lock 6), and drawing the same
    /// surface under the bar as well is exactly the two-owners state ThemeHost exists to
    /// prevent.
    /// </summary>
    public void Hover(HudExpandTarget target)
    {
        if (_host.IsWindowOpen) return;
        _host.SelectTab(target);
        if (!_host.IsInline) _host.ToggleCard();
    }

    /// <summary>The pointer left the bar AND the panel — collapse (lock 3), unless a click
    /// pinned something, in which case the pin comes back.</summary>
    public void Away()
    {
        if (_host.IsWindowOpen) return;
        if (_pinned is { } pin) { _host.SelectTab(pin); return; }
        if (_host.IsInline) _host.ToggleCard();
    }

    /// <summary>
    /// A chip was clicked — PIN it open (lock 4), or close it if it is the pinned one
    /// already. Lock 1 falls out of the assignment: pinning a second tracker is not an
    /// addition, it is a replacement.
    ///
    /// **While a float is up, a click on the SAME chip brings it forward** (ThemeHost's own
    /// answer) and a click on a DIFFERENT chip starts a fresh expansion for that tracker.
    /// The float is not closed: from the moment it was popped it is an ordinary floating
    /// window with its own ✕ and its own Options row, which is what lock 6 means by "the
    /// float carries the detail". <see cref="WindowClosed"/> is keyed on the target for
    /// exactly this reason — closing a float this bar has moved on from must not collapse
    /// whatever the bar is showing now.
    /// </summary>
    public void Click(HudExpandTarget target)
    {
        if (_host.IsWindowOpen)
        {
            if (Target == target) { _host.ToggleCard(); return; }   // bring the float forward
            _host.Reset();
        }
        if (_pinned == target && _host.IsInline) { Collapse(); return; }
        _pinned = target;
        _host.SelectTab(target);
        if (!_host.IsInline) _host.ToggleCard();
    }

    /// <summary>✕ on the under-bar panel — back to just the bar (lock 5). The pin goes with
    /// it: a panel dismissed by hand that a stray hover could restore *pinned* would be a ✕
    /// that only half worked.</summary>
    public void Collapse()
    {
        _pinned = null;
        if (_host.IsInline) _host.ToggleCard();
    }

    /// <summary>⧉ on the panel — the float takes the detail and the under-bar panel
    /// collapses (lock 6).</summary>
    public void PopOut()
    {
        _pinned = null;
        _host.PopOut();
    }

    /// <summary>The float this bar popped was closed — just the mini bar, nothing expanded
    /// (lock 7). **Keyed on the target**: a ✕ on some other floating window, or on a float
    /// the bar has since moved on from, must not collapse the panel the player is looking
    /// at.</summary>
    public void WindowClosed(HudExpandTarget target)
    {
        if (!_host.IsWindowOpen || Target != target) return;
        _pinned = null;
        _host.WindowClosed();
    }

    /// <summary>Back to first-run — the widget left the collapsed HUD, or the profile was
    /// reset. The panel belongs to the bar, and the bar is only on screen while minimized.
    /// </summary>
    public void Reset()
    {
        _pinned = null;
        _host.Reset();
    }
}
