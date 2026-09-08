namespace EQBuddy.UI.Shared;

/// <summary>
/// Which tracker the mini bar is expanding.
///
/// OE-1 shipped three (owner lock 8): the owner tested the mechanics on DPS, HPS and
/// Progress before every other tracker followed on the same pattern. Lock 9 forbids a
/// one-off exception for any of the ones that come later, so **this enum grows and nothing
/// else about the model does** — which is exactly what OE-7 does to it.
///
/// **OE-7 adds the other four floating-window kinds, and the reason is not symmetry.** A
/// float's ✕ used to write <c>AppSettings.DisabledBreakouts</c>, because auto-show-while-
/// minimized was the only thing that could ever bring one back and a dismissal had nowhere
/// else to live — that is discussion #45's whack-a-mole, solved by making the ✕ permanent.
/// A bar chip is a SUMMON, so once every kind has one the ✕ can be a transient close and
/// the persistent flag goes back to being what Options means by it. **A kind with no chip
/// could not have made that trade**, which is why the four arrive together rather than one
/// per release.
/// </summary>
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

    /// <summary>Pet damage — the 🐾 cell while its star is set, and the always-on row's one
    /// INSERTABLE slot while <c>AppSettings.HudGlancePet</c> is (SIGNED #422). **One target
    /// either way, unchanged**: hover peek, click pin, opt-in double-click to the Pet float,
    /// wherever the chip is sitting. That is what makes insertion cost no entrance (trap 59)
    /// — the door does not move when the chip does.</summary>
    Pet,

    /// <summary>The 🎯 watch chips — one per 📌-pinned rule, all of them summoning the one
    /// Watch window. Several chips, one target: the float is a list of every pinned rule,
    /// so a per-rule target would be four names for one window.</summary>
    Watch,

    /// <summary>The 🎒 loot cell, while its star is set.</summary>
    Loot,

    /// <summary>The buff set. **The one kind whose chip did not exist before OE-7** —
    /// <c>MiniBarPresentation.Order</c> has never drawn a "buffs" cell, so its window's only
    /// door was Options plus an opt-in double-click on a chip that was not there. The chip
    /// is drawn by <c>HudBarView</c> from the buff tracker's own count rather than from a
    /// snapshot field, because there is no buff state on <c>StatsSnapshot</c> at all.</summary>
    Buffs,

    // ---- OE-9: THE REST OF THE TRAY, LESS DEATHS -----------------------------------
    //
    // The four below are the signed #389 plan's four-target carve, on Bevel #371's
    // reading, and they arrive together rather than one per release: a bar where some
    // chips answered a hover and the rest did not is the per-tracker exception lock 9
    // forbids, and these four are what the plan authorises closing it with.
    //
    // **`deaths` IS DELIBERATELY NOT HERE.** A Deaths target, peek and World → Travels
    // route were built and then STRIPPED on Helm's 2026-09-07 sign of #400 — "#389 Deaths
    // OUT stands", the Deaths gate — so the absence is a product decision rather than the
    // gap trap 20 describes, and it is written down here because nothing else can say so:
    // `deaths` is still a live `MiniBarPresentation.Order` cell and its chip still draws.
    // What it does not do is peek or pop out, and the `NoExpansion` row in
    // `HudExpandTests.EveryMiniBarCellHasAnExpansionTargetExceptTheSignedExemptions`
    // carries the ruling so the decision is an assertion rather than this comment. Adding
    // the member back is a product change, not a fix — and that test fails either way
    // round until the row goes with it.

    /// <summary>The ✨ motes cell. Its ⧉ is the Progress window's WEALTH tab, not a float
    /// of its own — <c>ProgressSurface.TabForKey</c> already answered "motes" with Wealth
    /// long before this was asked, which is the app agreeing with the routing in advance.
    /// </summary>
    Motes,

    /// <summary>The 💀 kills cell. Its ⧉ is the Kills &amp; Drops window on its Kills tab —
    /// the FIRST target whose destination is neither a float nor Progress, and therefore the
    /// one that turned <see cref="HudExpand.DestinationOf"/>'s predecessor from an exact
    /// proxy into a wrong one (trap 64; see that method).</summary>
    Kills,

    /// <summary>The ⚡ weapon-procs cell. Its ⧉ is the DAMAGE float, which gains a procs
    /// block rather than a tenth always-on-top window: procs are a damage-surface fact
    /// everywhere else in the app (the Combat card, the Evolved shell's Live room), and a
    /// new float for five rows is the proliferation SA-2 was signed to end.</summary>
    Procs,

    /// <summary>The 🪙 coin cell. Progress → Wealth, same as <see cref="Motes"/> — the
    /// owner's own lock reads Money's breakout as "the Wealth section of Progress", which is
    /// what makes a SECTION of an existing window a destination this vocabulary allows.
    /// </summary>
    Money,
}

/// <summary>Which WINDOW a target's ⧉ opens. Three, since OE-9 — and the count is the
/// argument for <see cref="HudDestination"/> existing at all.</summary>
public enum HudDestinationHost
{
    /// <summary>One of the six <c>BreakoutKind</c> floats.</summary>
    Float,

    /// <summary>The Progress theme window (Experience / Wealth / Faction / Raids).</summary>
    ProgressWindow,

    /// <summary>The Kills &amp; Drops window.</summary>
    CreatureWindow,
}

/// <summary>
/// WHERE A TARGET'S ⧉ SENDS ITS DETAIL — the window, and the tab on it.
/// </summary>
/// <param name="Host">Which window family.</param>
/// <param name="BreakoutName">The <c>BreakoutKind</c> member NAME when
/// <paramref name="Host"/> is <see cref="HudDestinationHost.Float"/>, else null. A NAME
/// rather than the enum for the reason <see cref="BreakoutPresentation"/> gives: that enum
/// is a WPF type, and taking it here would put the decision back inside the layer that
/// cannot test it.</param>
/// <param name="FloatKind">The <see cref="BreakoutPresentation"/> kind that supplies the
/// float's WORDS. Carried beside the member name rather than derived from it, so the
/// tooltip does not depend on the two happening to differ only in case.</param>
/// <param name="Tab">The tab key the window opens on, or null for "wherever it was".</param>
public sealed record HudDestination(
    HudDestinationHost Host, string? BreakoutName, string? FloatKind, string? Tab);

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

    /// <summary>
    /// A target's one-word key, for the dump and for the <c>EQBUDDY_HUDEXPAND</c> hook.
    /// <see cref="TargetForKey"/> is the inverse and they are tested as a pair, so a name can
    /// never be readable in one direction only.
    ///
    /// **Every key here that names a tray cell IS that cell's <c>MiniStats</c> key**, which
    /// is not a coincidence and is load-bearing since OE-9: <see cref="TargetForKey"/> is the
    /// bridge <c>HudBarView</c> uses to turn a <see cref="MiniBarCell"/> into an expansion
    /// chip, so the chip, the panel, the title, the icon and the ⧉ all read one table
    /// (trap 4). The hand switch that used to pick a target per cell is gone with it.
    /// </summary>
    public static string Key(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Hps => "hps",
        HudExpandTarget.Progress => "progress",
        HudExpandTarget.Pet => "pet",
        HudExpandTarget.Watch => "watch",
        HudExpandTarget.Loot => "loot",
        HudExpandTarget.Buffs => "buffs",
        HudExpandTarget.Motes => "motes",
        HudExpandTarget.Kills => "kills",
        HudExpandTarget.Procs => "procs",
        HudExpandTarget.Money => "money",
        _ => "dps",
    };

    /// <summary>The key back to a target, or null for anything else. Case-insensitive: it
    /// reads an environment variable a human types — and, since OE-9, a
    /// <see cref="MiniBarCell.Key"/> the bar hands it.</summary>
    public static HudExpandTarget? TargetForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "dps" => HudExpandTarget.Dps,
        "hps" => HudExpandTarget.Hps,
        "progress" or "xp" => HudExpandTarget.Progress,
        "pet" => HudExpandTarget.Pet,
        "watch" => HudExpandTarget.Watch,
        "loot" => HudExpandTarget.Loot,
        "buffs" => HudExpandTarget.Buffs,
        "motes" => HudExpandTarget.Motes,
        "kills" => HudExpandTarget.Kills,
        "procs" => HudExpandTarget.Procs,
        "money" => HudExpandTarget.Money,
        // No "deaths": the Deaths target was stripped on Helm's #400 sign (2026-09-07,
        // "#389 Deaths OUT stands"). `HudBarView` reads this to turn a cell into an
        // expansion chip, so the null here is what leaves the deaths cell a plain chip —
        // it is the mechanism of that decision, not an omission.
        _ => null,
    };

    /// <summary>
    /// WHERE ⧉ SENDS A TARGET — total over the enum, and the whole reason OE-9 was a plan
    /// rather than a diff.
    ///
    /// **It replaces the SECOND proxy in this lineage, and the first one is why the second
    /// was worth catching before it bit** (trap 64). <c>HudExpandBar</c> once chose the float
    /// with <c>target == Hps ? Healing : Damage</c> — exact while the enum held three
    /// members, and silently routing four new ones to the Damage window the day it did not.
    /// OE-7 fixed that with a name table and left the SAME shape one level up: the caller
    /// read *"no breakout name → the Progress window"*, which was exact while Progress was
    /// the only non-float destination. <see cref="HudExpandTarget.Kills"/> is the member that
    /// makes it wrong, and it would have gone to Progress with **no line of that method
    /// changing**. There are three destination hosts now, so the fact is named instead of
    /// being inferred from an absence — and the count being three rather than four is the
    /// Deaths strip (Helm's #400 sign, 2026-09-07), not the proxy coming back.
    ///
    /// **A destination is not a per-target special case — it is a WINDOW and a TAB**, which
    /// is what lets two targets share one (Dps and Procs both mean the Damage float; Money
    /// and Motes both mean Progress → Wealth) and what lock 7's close handling keys on. See
    /// <see cref="SameWindow"/>.
    /// </summary>
    public static HudDestination DestinationOf(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Hps => Float("Healing", BreakoutPresentation.Healing),
        HudExpandTarget.Pet => Float("Pet", BreakoutPresentation.Pet),
        HudExpandTarget.Watch => Float("Watch", BreakoutPresentation.Watch),
        HudExpandTarget.Loot => Float("Loot", BreakoutPresentation.Loot),
        HudExpandTarget.Buffs => Float("Buffs", BreakoutPresentation.Buffs),
        // The Progress WINDOW on whatever tab it was left on — the 2026-08-25 fold ("reuse
        // the existing theme window on its current tab"), untouched.
        HudExpandTarget.Progress =>
            new(HudDestinationHost.ProgressWindow, null, null, null),
        // "wealth" because that is what ProgressSurface.TabForKey already answers for both
        // "motes" and "money" — the app agreed with this routing before it was asked.
        HudExpandTarget.Motes or HudExpandTarget.Money =>
            new(HudDestinationHost.ProgressWindow, null, null, "wealth"),
        HudExpandTarget.Kills =>
            new(HudDestinationHost.CreatureWindow, null, null, "kills"),
        // PROCS SHARES THE DAMAGE FLOAT rather than getting a tenth always-on-top window.
        // Procs are a damage-surface fact everywhere else in the app, and the float carries
        // the detail (lock 6) — so the float GAINS the procs block the Live room already
        // draws inside the damage surface, off this same peek builder.
        HudExpandTarget.Procs => Float("Damage", BreakoutPresentation.Damage),
        _ => Float("Damage", BreakoutPresentation.Damage),
    };

    private static HudDestination Float(string name, string kind) =>
        new(HudDestinationHost.Float, name, kind, null);

    /// <summary>
    /// Do these two destinations mean the SAME window on screen?
    ///
    /// **Lock 7 goes destination-keyed because two targets can now share a window**, so
    /// "which close collapses the model" cannot key on the breakout kind alone: a ✕ on the
    /// Damage float has to collapse a pinned PROCS panel that popped there, and closing the
    /// Progress window has to collapse Motes and Money as well as Progress. The TAB is
    /// deliberately not compared — a player who closes the Progress window has closed it
    /// whichever tab they wandered to.
    /// </summary>
    public static bool SameWindow(HudDestination a, HudDestination b) =>
        a.Host == b.Host
        && (a.Host != HudDestinationHost.Float
            || string.Equals(a.BreakoutName, b.BreakoutName, StringComparison.Ordinal));

    /// <summary>
    /// The <c>BreakoutKind</c> member NAME a target pops to, or null when its destination is
    /// not a float at all. A thin read of <see cref="DestinationOf"/> and NOT a second table:
    /// the caller that parses this into the WPF enum and the caller that decides which window
    /// closed must not be able to disagree.
    /// </summary>
    public static string? BreakoutName(HudExpandTarget target) =>
        DestinationOf(target).BreakoutName;

    /// <summary>
    /// The <see cref="BreakoutPresentation"/> kind a target's words and vector come from, or
    /// **null for the four tray cells whose surface has no float** — in which case
    /// <see cref="MiniBarPresentation"/> supplies both, keyed by the same
    /// <see cref="Key"/> the cell already had.
    ///
    /// ONE switch, so <see cref="Title"/> and <see cref="Icon"/> cannot disagree about which
    /// surface a target means (trap 4). **The null is not laziness and it is not the same
    /// question as <see cref="DestinationOf"/>**: Procs POPS to the Damage float and is not
    /// called "Your damage", so a target's words and a target's window are two facts. Reading
    /// one off the other is how the Procs chip would have grown a sword.
    /// </summary>
    public static string? KindOf(HudExpandTarget target) => target switch
    {
        HudExpandTarget.Dps => BreakoutPresentation.Damage,
        HudExpandTarget.Hps => BreakoutPresentation.Healing,
        HudExpandTarget.Progress => BreakoutPresentation.Progress,
        HudExpandTarget.Pet => BreakoutPresentation.Pet,
        HudExpandTarget.Watch => BreakoutPresentation.Watch,
        HudExpandTarget.Loot => BreakoutPresentation.Loot,
        HudExpandTarget.Buffs => BreakoutPresentation.Buffs,
        _ => null,
    };

    /// <summary>What the panel calls itself. The four tray cells take the name their CELL
    /// already has (<see cref="MiniBarPresentation.Names"/>) rather than a third naming table
    /// — the chip and the panel it opens must not be two different words for one stat.
    /// </summary>
    public static string Title(HudExpandTarget target) =>
        KindOf(target) is { } kind
            ? BreakoutPresentation.Title(kind)
            : MiniBarPresentation.Names[Key(target)];

    /// <summary>The panel header's vector — the same one the bar's own chip wears, so the
    /// panel and the chip that opened it cannot be read as two different things. Same source
    /// split as <see cref="Title"/>, for the same reason.</summary>
    public static string Icon(HudExpandTarget target) =>
        KindOf(target) is { } kind
            ? BreakoutPresentation.Icon(kind)
            : MiniBarPresentation.Icons[Key(target)];

    /// <summary>
    /// Where ⧉ sends this tracker's detail, in words, for the pop-out's tooltip.
    ///
    /// **It reads the DESTINATION, never the target's own title** — which matters more since
    /// OE-9 than it did when it was written, because three of the four new chips do not go to
    /// a window named after themselves and one of them (Procs) goes to a float called
    /// something else entirely. A tooltip built from the chip's name would have promised a
    /// "Weapon procs window" that does not exist. That is the #233 "X is now Y" rule inside a
    /// hover, and it is the only warning a player gets before a different window appears.
    /// </summary>
    public static string PopOutTip(HudExpandTarget target) => $"Open {Words(DestinationOf(target))}";

    /// <summary>A destination in words, for <see cref="PopOutTip"/> and for anything else
    /// that has to name where a surface went.</summary>
    public static string Words(HudDestination destination) => destination.Host switch
    {
        HudDestinationHost.Float =>
            $"the floating {BreakoutPresentation.Title(destination.FloatKind!)} window",
        HudDestinationHost.ProgressWindow => destination.Tab is { Length: > 0 } tab
            ? $"the Progress window ({char.ToUpperInvariant(tab[0]) + tab[1..]})"
            : "the Progress window",
        _ => "the Kills & Drops window",
    };

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
