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
        // Lock 7 — "close floated window → just the mini-bar, nothing expanded". Keyed on
        // the kind, so a ✕ on some other float, or on one this bar has moved on from,
        // collapses nothing (HudExpand.WindowClosed's own guard).
        breakouts.Dismissed += kind =>
        {
            if (HudExpand.TargetForBreakout(kind.ToString()) is not { } target) return;
            _model.WindowClosed(target);
            Apply();
        };
    }

    /// <summary>The <c>hudExpand</c> / <c>hudExpandMode</c> dump facts, and the panel's own
    /// two. Four keys because the fold has four separable ways to go wrong and one "is
    /// something expanded" could not tell them apart — most of all peek from pinned, which
    /// renders identically and is the whole of lock 4.</summary>
    public string TargetKey => _model.TargetKey;
    public string ModeKey => _model.ModeKey;
    public bool PanelVisible => _panel is { IsVisible: true };
    public int RowCount => _panel?.RowCount ?? 0;

    /// <summary>What the bar's chips light for: the tracker whose panel is on screen, or
    /// null. Read by <see cref="HudBarView"/> every tick, so the lit chip and the panel
    /// cannot disagree (one fact, one source — trap 4).</summary>
    public HudExpandTarget? Shown => _model.IsInline ? _model.Target : null;

    /// <summary>The panel's height plus its gap, for <see cref="HudChipRowWindow"/> to park
    /// BELOW rather than on top of. Both are slaved to the same widget edge, so without this
    /// the deadline chicklets and the panel would occupy the same strip of screen.</summary>
    public double OccupiedHeight => _panel?.OccupiedHeight ?? 0;

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
    /// Lock 6: ⧉ — the under-bar panel collapses and the float carries the detail.
    ///
    /// **Progress goes to the Progress WINDOW, not to a float**, and that is a signed fold
    /// rather than a preference: <c>Progress</c> left <c>BreakoutKind</c> on 2026-08-25
    /// ("reuse the existing theme window on its current tab"), <c>DocumentationSizeTests</c>
    /// pins that list, and giving it a breakout here would revert it.
    /// </summary>
    public void PopOut()
    {
        _away.Stop();
        var target = _model.Target;
        _model.PopOut();
        Apply();
        if (target == HudExpandTarget.Progress) { _main.ShowProgressWindow(); return; }
        _breakouts.Open(BreakoutFor(target));
    }

    /// <summary>The Progress window closed. Lock 7's other half — the one destination that
    /// is not a <c>BreakoutKind</c> and therefore never reaches
    /// <c>BreakoutHost.Dismissed</c>.</summary>
    public void ProgressWindowClosed()
    {
        _model.WindowClosed(HudExpandTarget.Progress);
        Apply();
    }

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

    private void BringForward(HudExpandTarget target)
    {
        if (target == HudExpandTarget.Progress) { _main.ShowProgressWindow(); return; }
        _breakouts.Visible(BreakoutFor(target))?.Activate();
    }

    /// <summary>DPS and HPS are Damage and Healing. Progress is neither and never reaches
    /// here — <see cref="PopOut"/> and <see cref="BringForward"/> both branch on it first,
    /// so this method has no case that could quietly re-add it to the enum.</summary>
    private static BreakoutKind BreakoutFor(HudExpandTarget target) =>
        target == HudExpandTarget.Hps ? BreakoutKind.Healing : BreakoutKind.Damage;
}
