using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// OE-1's glue: the mini bar's expansion model, the panel window it drives, and the two
/// destinations a ⧉ can send a tracker to.
///
/// **It exists so <c>MainWindow</c> gains six lines rather than sixty.** The widget's
/// hotspot ratchet had one line of headroom when this landed; everything here is either
/// interaction wiring (which belongs beside the surface) or a decision that is already in
/// <see cref="HudExpand"/> (which belongs in <c>UI.Shared</c>, where it is unit-tested).
/// What stays in the widget is the same thing trap 15 always leaves there: WHEN the bar is
/// on screen.
///
/// **The grace timer is the one piece of real machinery here, and it is not a flourish.**
/// The panel is a separate top-level window, so moving the pointer from a chip onto the
/// panel fires <c>MouseLeave</c> on the bar BEFORE <c>MouseEnter</c> on the panel — the
/// hover expand would collapse out from under the cursor that is reaching for its ⧉, which
/// is a peek nobody can use. A short deferral, cancelled by the panel's own enter, is what
/// bridges the 4-unit gap <see cref="HudChipRow.HudGap"/> leaves.
/// </summary>
internal sealed class HudExpandBar
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private readonly BreakoutHost _breakouts;
    private readonly HudExpand _model = new();
    private readonly DispatcherTimer _away;
    private HudExpandWindow? _panel;
    private bool _pointerOnPanel;

    /// <summary>Long enough to cross a 4-unit gap with a normal mouse, short enough that a
    /// deliberate move off the bar still feels immediate.</summary>
    private static readonly TimeSpan AwayGrace = TimeSpan.FromMilliseconds(220);

    public HudExpandBar(MainWindow main, AppSettings settings, BreakoutHost breakouts)
    {
        _main = main;
        _settings = settings;
        _breakouts = breakouts;
        _away = new DispatcherTimer { Interval = AwayGrace };
        _away.Tick += (_, _) =>
        {
            _away.Stop();
            if (_pointerOnPanel) return;
            _model.Away();
            Apply();
        };
        // Lock 7 — "close floated window → just the mini-bar, nothing expanded".
        //
        // **DESTINATION-keyed since OE-9, not kind-keyed**, and the difference is a real bug
        // rather than a refactor: two targets can now share one window (Dps and Procs both
        // pop to the Damage float; Progress, Motes and Money all pop to the Progress window),
        // so "which target did this kind belong to" no longer has one answer. The old
        // `TargetForBreakout` lookup would have collapsed a pinned Dps panel on a ✕ the Procs
        // panel had opened, and left the Procs one up. What the model needs to know is
        // whether the window that closed is the window ITS target went to.
        breakouts.Dismissed += kind => WindowClosed(HudDestinationHost.Float, kind.ToString());
    }

    /// <summary>
    /// A window this bar may have popped to has closed (lock 7).
    ///
    /// The model's own target-keyed guard stays underneath — a ✕ on a float the bar has since
    /// moved on from must not collapse what the bar is showing NOW — and this adds the half
    /// that guard cannot see: WHICH window closed, compared against where the current target
    /// actually went. <see cref="HudExpand.SameWindow"/> is the comparison, and it ignores the
    /// tab on purpose (closing the Progress window closes it whichever tab you wandered to).
    /// </summary>
    private void WindowClosed(HudDestinationHost host, string? breakoutName = null)
    {
        if (!_model.IsWindowOpen) return;
        var closed = new HudDestination(host, breakoutName, null, null);
        if (!HudExpand.SameWindow(HudExpand.DestinationOf(_model.Target), closed)) return;
        _model.WindowClosed(_model.Target);
        Apply();
    }

    /// <summary>The <c>hudExpand</c> / <c>hudExpandMode</c> dump facts, and the panel's own
    /// two. Four keys because the fold has four separable ways to go wrong and one "is
    /// something expanded" could not tell them apart — most of all peek from pinned, which
    /// renders identically and is the whole of lock 4.</summary>
    public string TargetKey => _model.TargetKey;
    public string ModeKey => _model.ModeKey;
    public bool PanelVisible => _panel is { IsVisible: true };
    public int RowCount => _panel?.RowCount ?? 0;

    /// <summary>The <c>hudExpandBody</c> fact — which surface's rows the panel last drew.
    /// See <c>HudExpandWindow.BodyKind</c>: <see cref="TargetKey"/> is the header's claim
    /// and this is the body's, and OE-7 made them two decisions rather than one.</summary>
    public string BodyKind => _panel?.BodyKind ?? "none";

    /// <summary>The <c>hudExpandEmpty</c> fact — WHICH empty state, since OE-9 lock 2 made
    /// the Loot peek's two of them the thing under test. See
    /// <see cref="HudExpandWindow.EmptyKey"/>.</summary>
    public string EmptyKey => _panel?.EmptyKey ?? "none";

    /// <summary>What the bar's chips light for: the tracker whose panel is on screen, or
    /// null. Read by <see cref="HudBarView"/> every tick, so the lit chip and the panel
    /// cannot disagree (one fact, one source — trap 4).</summary>
    public HudExpandTarget? Shown => _model.IsInline ? _model.Target : null;

    /// <summary>How much of the line under the widget the panel is taking, for
    /// <see cref="HudChipRowWindow"/> to park BELOW rather than on top of. While both are
    /// slaved to the same widget edge, without this the deadline chicklets and the panel
    /// would occupy the same strip of screen; a panel the player has PARKED elsewhere (OE-8)
    /// answers zero, because it is not on that line at all.</summary>
    public double SlavedOccupiedHeight => _panel?.SlavedOccupiedHeight ?? 0;

    /// <summary>The <c>hudPanelPark</c> / <c>hudPanelParkSaved</c> dump facts and the
    /// Edit-HUD un-park's reach into this window (OE-8). "slaved" whenever there is no panel:
    /// a key that disappears with its window is a key a test cannot assert (trap 62), and a
    /// panel that has never been built has never been parked.</summary>
    public string ParkKey => _panel?.ParkKey ?? "slaved";
    public string ParkSavedKey =>
        _panel?.ParkSavedKey
        ?? UI.Shared.HudChipRow.ParkKey(_settings.HudPanelParkLeft, _settings.HudPanelParkTop);
    public double DrawnWidth => _panel?.DrawnWidth ?? 0;
    public string GripKey => _panel?.GripKey ?? "0,0";

    /// <summary>The <c>hudPanelAnchor</c> / <c>hudChipAnchor</c> dump facts — where the panel
    /// sits relative to the widget, and the chip offset it was placed from. NaN with no panel
    /// on screen; the dump reports that as -1, which is a state no anchored panel can reach
    /// (a chip is always right of the widget's own edge).</summary>
    public double AnchorOffset => _panel?.AnchorOffset ?? double.NaN;
    public double ChipAnchor => _panel?.ChipAnchor ?? double.NaN;

    /// <summary>Is the panel parked — read by Edit HUD's "Follow the HUD again" to decide
    /// whether it has anything to undo. **The SETTING, not the window, when no panel exists
    /// yet**: the pair outlives every panel instance, and an un-park control that went dead
    /// because the player had not hovered a chip this session would be a way back that is
    /// only there when you do not need it.</summary>
    public bool IsParked => _panel?.IsParked
        ?? UI.Shared.HudChipRow.IsParked(_settings.HudPanelParkLeft, _settings.HudPanelParkTop);

    /// <summary>"Follow the HUD again", reaching the panel through its bar. Clears the pair
    /// whether or not a panel is on screen, so the next one built is slaved — otherwise the
    /// undo would silently do nothing at exactly the moment the panel was hidden.</summary>
    public void Unpark()
    {
        if (_panel is { } panel) { panel.Unpark(); return; }
        _settings.HudPanelParkLeft = double.NaN;
        _settings.HudPanelParkTop = double.NaN;
    }

    /// <summary>Lock 3, arriving side: a chip is under the pointer.</summary>
    public void Hover(HudExpandTarget target)
    {
        _away.Stop();
        _model.Hover(target);
        Apply();
    }

    /// <summary>Lock 3, leaving side — deferred by <see cref="AwayGrace"/>. See this class's
    /// header: an immediate collapse here is a peek that cannot be reached.</summary>
    public void Away()
    {
        _away.Stop();
        _away.Start();
    }

    /// <summary>Lock 4: a click pins. If the tracker's float is already the owner of the
    /// body, ThemeHost's answer is "bring it forward" rather than "draw it twice".</summary>
    public void Click(HudExpandTarget target)
    {
        _away.Stop();
        _model.Click(target);
        if (_model.ShouldBringWindowForward) { BringForward(target); return; }
        Apply();
    }

    /// <summary>The pointer crossed onto (or off) the panel itself.</summary>
    public void PointerOnPanel(bool inside)
    {
        _pointerOnPanel = inside;
        if (inside) _away.Stop(); else Away();
    }

    /// <summary>Lock 5: the ✕ on the panel.</summary>
    public void Collapse()
    {
        _away.Stop();
        _model.Collapse();
        Apply();
    }

    /// <summary>
    /// Lock 6: ⧉ — the under-bar panel collapses and the destination carries the detail.
    ///
    /// **THREE destinations since OE-9, and the routing is a total map rather than a
    /// fallback.** This method used to read *"no breakout kind → the Progress window"*, which
    /// was exact while Progress was the only non-float destination and would have sent Kills
    /// there with no line of it changing (trap 64 — the second proxy in this lineage; see
    /// <see cref="HudExpand.DestinationOf"/>).
    /// </summary>
    public void PopOut()
    {
        _away.Stop();
        var target = _model.Target;
        _model.PopOut();
        Apply();
        Open(HudExpand.DestinationOf(target));
    }

    /// <summary>Open (or front) whichever window a target's ⧉ names. The three theme windows
    /// front and activate themselves; a float is the one that needs telling apart, because
    /// <see cref="BreakoutHost.Open"/> and <see cref="BreakoutHost.Visible"/> are two calls.
    /// </summary>
    private void Open(HudDestination destination, bool bringForward = false)
    {
        switch (destination.Host)
        {
            case HudDestinationHost.ProgressWindow:
                _main.ShowProgressWindow(destination.Tab);
                break;
            case HudDestinationHost.CreatureWindow:
                _main.ShowCreatureWindow(CreatureSurface.TabForKey(destination.Tab));
                break;
            default:
                var kind = Enum.Parse<BreakoutKind>(destination.BreakoutName!);
                if (bringForward) _breakouts.Visible(kind)?.Activate();
                else _breakouts.Open(kind);
                break;
        }
    }

    /// <summary>The Progress window closed. Lock 7 for the destinations that are not a
    /// <c>BreakoutKind</c> and therefore never reach <c>BreakoutHost.Dismissed</c> —
    /// there are two of them now, and each one needs a hook where its window is built.
    /// </summary>
    public void ProgressWindowClosed() => WindowClosed(HudDestinationHost.ProgressWindow);

    /// <summary>The Kills &amp; Drops window closed — the hook OE-9 had to add, because
    /// <see cref="HudExpandTarget.Kills"/> is the first target that pops to it.</summary>
    public void CreatureWindowClosed() => WindowClosed(HudDestinationHost.CreatureWindow);

    /// <summary>The widget left (or re-entered) the collapsed HUD. The panel is the BAR's,
    /// and a slaved companion left parked under an expanded widget is trap 12's mechanism
    /// wearing a stale window.</summary>
    public void SetBarVisible(bool visible)
    {
        if (visible) return;
        _away.Stop();
        _model.Reset();
        Apply();
    }

    /// <summary>The widget's once-a-second tick. Only repaints what is on screen: the panel
    /// is hidden the rest of the time, and a hidden window that keeps rebuilding rows is the
    /// cost trap 12 charges for nothing.</summary>
    public void Follow(StatsSnapshot s)
    {
        if (!_model.IsInline || _panel is not { IsVisible: true } panel) return;
        panel.Follow(s, _model.Target);
    }

    /// <summary>Bring the model's decision to the screen: draw and grow, or collapse and
    /// hide. The chip row is re-parked either way, because it sits BELOW the panel and the
    /// panel just changed height.</summary>
    private void Apply()
    {
        if (_model.IsInline)
        {
            var panel = _panel ??= new HudExpandWindow(_main, _settings, this);
            panel.Follow(_main.CurrentSnapshot(), _model.Target);
            panel.Reveal();
        }
        else _panel?.Dismiss();
        _main.RefreshHudChips();
    }

    private void BringForward(HudExpandTarget target) =>
        Open(HudExpand.DestinationOf(target), bringForward: true);
}
