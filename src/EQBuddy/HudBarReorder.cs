using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// DRAG-TO-REORDER for the minimized bar's chips (#191, TheMegaSage; owner lock 2026-09-07
/// ~4:44 PM CT), and the ONE code path that may write <c>AppSettings.MiniBarOrder</c> —
/// or, since SIGNED #422, <c>AppSettings.HudGlancePet</c>.
///
/// **The pet chip has one slot the others do not: the always-on row's insertion gap between
/// DPS and the third number.** Carrying it there inserts, carrying it back down ejects, and
/// both are this same gesture family — same capture, same threshold, same SizeWE cursor,
/// same adorner. OE-8's free-drag is untouched and not reopened, and no FIXED slot became a
/// drop target, so #413's reasoning about a slot that swaps identity mid-session still
/// stands.
///
/// **The chip is the HANDLE, and the vocabulary is one sentence: a drag that starts on a
/// CHIP is about the chip; a drag that starts on GROUND is about the window.** OE-8 taught
/// the two companion windows that their body places the window; the widget's own ground
/// still starts a <c>DragMove</c> from <c>MainWindow.OnDrag</c>. Neither is touched here,
/// and neither can be reached from a chip: <c>HudBarView.AttachGestures</c> has always set
/// <c>Handled</c> on a chip's mouse-down precisely to keep clicks out of that
/// <c>DragMove</c>, so press-and-move on a chip was DEAD SPACE before this class existed.
/// Reorder claims dead space rather than competing for a live gesture, which is why no
/// window-level threshold against free-drag is needed at all.
///
/// **What it does move is the CLICK.** A chip used to act on mouse-DOWN; it now acts on the
/// UP that never crossed the threshold. <see cref="Consumed"/> is how the chip's own handler
/// asks whether the gesture turned into something else — read while this class is still
/// mid-gesture, which is why its handler on the host is a BUBBLING one: the chip's up runs
/// first, then this one finishes.
///
/// **A manual move loop, and a manual capture, for <c>HudDragGrip</c>'s reasons.** A drag
/// needs an unambiguous END — the order is written there and nowhere else, which is what
/// keeps the toolkit's own once-a-second rebuild out of the setting by construction rather
/// than by a flag (trap 49) — and a press must still be able to be a click.
///
/// **The chips are SMALL, so capture is taken on the press and in SubTree mode**, the half
/// <c>HudDragGrip</c> paid for: a chip is about twenty units tall, so a horizontal drag that
/// starts at its centre has the pointer off the element well before it has travelled the
/// system's drag distance, and an uncaptured element stops receiving <c>MouseMove</c> the
/// moment the pointer leaves it. SubTree rather than the default so hit-testing INSIDE the
/// bar is untouched and only events outside it are routed here.
///
/// **Nothing this class draws changes a measurement** (trap 12). The carried chip moves by
/// <c>RenderTransform</c> and the insertion mark is an ADORNER — both are ink in a layer
/// above the layout — because the widget is <c>SizeToContent</c> over a fullscreen game and
/// a drag that measured the bar wider would ask the window manager to resize an
/// always-on-top window on every mouse-move (#173's mechanism).
///
/// The affordance FACE — the drag sentence in the tooltip, the cursor, and where a "back to
/// the default order" control lives — is Bevel's at the review. This is the mechanism under
/// whichever face lands.
/// </summary>
internal sealed class HudBarReorder
{
    private readonly Panel _host;
    private readonly AppSettings _settings;
    private readonly Action _onDropped;
    private readonly Action _onGestureStart;

    /// <summary>The reorderable chips of the CURRENT render, in draw order:
    /// <c>MiniBarPresentation.DrawnKeys</c>'s answer with an element attached to each. The
    /// always-on row's fixed slots and the pinned watch chips are deliberately not in here —
    /// they register nothing, so a press on one is a press this class never saw.
    ///
    /// **The one exception is the INSERTED pet slot** (SIGNED #422), which registers from
    /// <c>RenderGlance</c> and therefore leads this list: the way back down is to carry it,
    /// so a slot nobody could take hold of would be a one-way door. It is a fixed slot that
    /// is a drag SOURCE and never a drag TARGET, which is why #413's "no drop may land on a
    /// slot that swaps identity" is untouched.</summary>
    private readonly List<(string Key, FrameworkElement Chip)> _chips = [];

    private Point _pressScreen;
    private int _from = -1;
    private bool _pressed;
    private bool _dragging;
    private bool _dead;
    private int _to = -1;
    private IReadOnlyList<double> _midpoints = [];
    private TranslateTransform? _carry;
    private InsertionMark? _mark;

    /// <summary>The always-on DPS chip of the current render, or null before one is drawn —
    /// the LEFT-HAND side of the insertion gap SIGNED #422 opened between it and the third
    /// slot.
    ///
    /// **The element, not a number, because the number is only true after layout.** Both
    /// neighbours in that gap are `ExpandChip`s and that branch draws no divider, so there is
    /// nothing in the tree to hang a mark off; the x is measured off this chip's own box the
    /// way <see cref="LeftOf"/> already measures a cell boundary (Bevel's §1 note). Cleared
    /// with the chips, since a chip from last tick is detached and can only answer NaN.</summary>
    private FrameworkElement? _glanceGapAfter;

    /// <summary>True while a chip is actually being carried. <c>HudBarView.Render</c> reads
    /// it and returns: the bar rebuilds every second, and replacing the elements under a
    /// captured drag would take the chip out of the player's hand mid-gesture. Everything
    /// else on the widget goes on ticking; only this one panel defers, and only until the
    /// drop.</summary>
    public bool Dragging => _dragging;

    /// <summary>Has this gesture stopped being available as a CLICK? True once it has become
    /// a drag, and true for the vertical wipe that means nothing — a press that left the bar
    /// downwards must not pin a peek on the way out.</summary>
    public bool Consumed => _dragging || _dead;

    /// <summary>
    /// Presses this bar has SEEN and drops it has WRITTEN — the <c>hudCellGrip</c> dump
    /// fact, and it exists for the reason <c>HudDragGrip</c>'s twin does: when a harness
    /// reports "a chip drag reordered nothing", that is equally consistent with the pointer
    /// never reaching a chip, the move never crossing the threshold, and the whole gesture
    /// running with the write not happening. Two counters separate the three in one run
    /// (trap 56 — ship the instrument before the third theory).
    /// </summary>
    public int PressCount { get; private set; }

    /// <summary>See <see cref="PressCount"/>.</summary>
    public int DropCount { get; private set; }

    /// <param name="onDropped">The DROP: persist and rebuild. The only path that may write
    /// <c>MiniBarOrder</c> — see this class's note above.</param>
    /// <param name="onGestureStart">A carry has begun: close an unpinned peek, so a panel
    /// does not flicker under a moving chip. A PINNED panel is left alone and re-anchors on
    /// the next render, which <c>AnchorOf</c> already recomputes per render (#404).</param>
    public HudBarReorder(Panel host, AppSettings settings, Action onDropped, Action onGestureStart)
    {
        _host = host;
        _settings = settings;
        _onDropped = onDropped;
        _onGestureStart = onGestureStart;
        // PREVIEW on the way down, so the press is seen before the chip's own handler sets
        // Handled (it must set it — that is the DragMove suppression this bar has always
        // needed). Not handled here, so the chip still gets it and the gesture can still
        // turn out to be its click.
        host.PreviewMouseLeftButtonDown += OnDown;
        host.PreviewMouseMove += OnMove;
        // BUBBLING on the way up, and handledEventsToo, so this runs AFTER the chip's own up
        // handler — which reads Consumed to decide whether it still owns a click. A preview
        // handler here would answer that question before it was asked.
        host.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnUp), handledEventsToo: true);
        host.LostMouseCapture += (_, _) => Finish(commit: true);
    }

    /// <summary>Called by <c>HudBarView.Render</c> as it builds the bar: this chip draws
    /// <paramref name="key"/> and may be carried. Order of registration IS draw order.</summary>
    public void Register(string key, FrameworkElement chip) => _chips.Add((key, chip));

    /// <summary>Called by <c>HudBarView.RenderGlance</c>: this is the chip the insertion gap
    /// sits to the RIGHT of. See <see cref="_glanceGapAfter"/>.</summary>
    public void SetGlanceGap(FrameworkElement dpsChip) => _glanceGapAfter = dpsChip;

    /// <summary>Called at the top of every render, before the chips are rebuilt.</summary>
    public void Clear()
    {
        _chips.Clear();
        _glanceGapAfter = null;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        // A double-click is somebody's verb — the opt-in gesture opens a chip's window — and
        // never a drag. Letting it arm would start a carry from the second press.
        if (e.ClickCount > 1) { _pressed = false; return; }
        _from = IndexOf(e.OriginalSource as DependencyObject);
        if (_from < 0) return;             // a fixed slot, a watch pin, or the bar's own ground
        PressCount++;
        _pressed = true;
        _dragging = false;
        _dead = false;
        // WHERE THIS CHIP ALREADY IS, which for the inserted pet slot is the gap and not
        // index 0 — the two are the same number and only one of them is where the player can
        // see the chip (SIGNED #422; the same ambiguity `MiniBarDrag.DropKind` exists for).
        _to = CarryingInsertedPet(_from) ? MiniBarDrag.GlanceSlot : _from;
        _pressScreen = _host.PointToScreen(e.GetPosition(_host));
        // Measured ONCE, here, off the untransformed chips: every later move compares the
        // pointer against these, so the numbers cannot drift as the carried chip is
        // translated away from where it was drawn.
        _midpoints = Midpoints();
        Mouse.Capture(_host, CaptureMode.SubTree);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _dead || e.LeftButton != MouseButtonState.Pressed) return;
        var now = _host.PointToScreen(e.GetPosition(_host));
        var dx = now.X - _pressScreen.X;
        var dy = now.Y - _pressScreen.Y;

        if (!_dragging)
        {
            // The SYSTEM's own thresholds, never a number invented here. The decision is
            // MiniBarDrag's and is unit-tested with no window.
            switch (MiniBarDrag.Classify(dx, dy,
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance))
            {
                case MiniBarGesture.Pending: return;
                case MiniBarGesture.Dead: _dead = true; return;
                default: break;
            }
            _dragging = true;
            _onGestureStart();
            BeginCarry();
        }

        var chip = _chips[_from].Chip;
        if (_carry is not null) _carry.X = dx;
        chip.Opacity = 0.65;
        // THE ONE CHIP WITH A SLOT BEYOND INDEX 0 (SIGNED #422). Only the pet chip may take
        // the always-on row's insertion gap, and only once the pointer has crossed the DPS
        // chip's own right edge — not a wider "anywhere near the row" zone, which would smear
        // the mark across three fixed slots that are not drop targets at all (Bevel's arm
        // condition, Helm-signed 2026-09-08). Every other chip clamps at 0 exactly as before.
        _to = _chips[_from].Key == MiniBarPresentation.PetKey
            ? MiniBarDrag.PetDropIndex(_midpoints, _from, PointerX(now), GlanceGapEndsAt(_from))
            : MiniBarDrag.DropIndex(_midpoints, _from, PointerX(now));
        _mark?.MoveTo(MarkX(_to));
        e.Handled = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        var wasDrag = _dragging;
        Finish(commit: true);
        if (wasDrag) e.Handled = true;
    }

    /// <summary>The end of the gesture, from either side — the button coming up, or the
    /// capture being taken away (an Alt-Tab, a lock screen, the app closing). Both are ends,
    /// and an order the player has visibly made must not be lost because the second kind
    /// happened.</summary>
    private void Finish(bool commit)
    {
        var wasDragging = _dragging;
        var wasPressed = _pressed;
        var from = _from;
        var to = _to;
        _pressed = false;
        _dragging = false;
        _dead = false;
        _from = -1;
        _to = -1;
        EndCarry(from);
        // Released whether or not the gesture became a drag: the capture was taken on the
        // press, so a plain click has to hand it back too or the next click anywhere on the
        // desk goes nowhere.
        if (wasPressed && ReferenceEquals(Mouse.Captured, _host)) Mouse.Capture(null);
        if (!wasDragging || !commit) return;
        Land(from, to);
    }

    /// <summary>
    /// THE WRITE, and the only one — what a completed carry actually persists.
    ///
    /// **Three settings answers from one gesture, decided by <c>MiniBarDrag.DropKind</c>
    /// rather than by comparing indices here** (SIGNED #422 §3/§6). An inserted pet slot is
    /// drawn first, so its index is 0 — the same number that means "the leftmost cell" for
    /// every other chip — and a caller that read `to == from` as "nothing happened" would
    /// make the eject silently do nothing.
    ///
    /// <c>MiniBarDrag.Move</c> re-inserts the carried key beside its new NEIGHBOUR in the
    /// FULL order, so the keys with no ★ keep the places the player left them in. An eject
    /// that landed back at slot 0 finds itself as its own neighbour and writes no order at
    /// all, which is the right answer: §2 says the remembered slot is what an eject restores.
    /// </summary>
    private void Land(int from, int to)
    {
        var carriedIsPet = from >= 0 && from < _chips.Count
            && _chips[from].Key == MiniBarPresentation.PetKey;
        var kind = MiniBarDrag.DropKind(carriedIsPet, _settings.HudGlancePet, from, to);
        if (kind == MiniBarDrop.None) { _onDropped(); return; }
        // BOTH FACTS IN THE ONE MOMENT for an eject that also moved: one gesture, one write
        // (§3). The membership flag goes first so the redraw below cannot see a half-applied
        // state.
        if (kind is MiniBarDrop.Insert or MiniBarDrop.Eject)
            _settings.HudGlancePet = kind == MiniBarDrop.Insert;
        // An INSERT writes no order on purpose: MiniBarOrder keeps "pet"'s slot while it is
        // away, so the chip comes back where the player had it rather than where the
        // canonical list would put it (§2).
        //
        // Neither does an EJECT that came down at the head of the cells — it is its own
        // neighbour there, so `Move` would hand back the order unchanged and writing it
        // would materialise the canonical list into a setting whose EMPTY value is the
        // floor: the bar would look identical and "Restore default order" would light up for
        // a player who has never reordered anything.
        if (to != from && kind is MiniBarDrop.Reorder or MiniBarDrop.Eject)
            MiniBarPresentation.SetOrder(_settings, MiniBarDrag.Move(
                MiniBarPresentation.ResolveOrder(_settings),
                [.. _chips.Select(c => c.Key)], from, to));
        DropCount++;
        _onDropped();
    }

    /// <summary>
    /// The DROP of a carried chip, driven by key and landing slot instead of by a pointer —
    /// the <c>EQBUDDY_PETDROP</c> rendezvous (SIGNED #422 §8), armed only by that hook.
    ///
    /// Returns false when this bar is not drawing the key at all, so the probe reports a
    /// staging mistake as a staging mistake rather than as a feature that did not fire.
    ///
    /// **It enters at <see cref="Land"/>, which is the same method a mouse-up enters** — the
    /// door probe's rule, one surface over: what a probe proves has to be the real path, or
    /// it proves the probe. What it deliberately does NOT drive is the pointer arithmetic;
    /// that is <c>MiniBarDrag</c>'s and is unit-tested with no window, because nothing in the
    /// E2E suite can move a pointer onto a control inside the widget and nothing in it may
    /// assert the screen.</summary>
    public bool ProbeDrop(string key, int slot)
    {
        var from = _chips.FindIndex(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        if (from < 0) return false;
        Land(from, slot);
        return true;
    }

    // ---- INK ------------------------------------------------------------------------
    //
    // Everything below moves pixels and measures nothing (trap 12).

    /// <summary>Is the chip at this index the INSERTED pet slot — the one whose list index
    /// and landing slot mean different places? (SIGNED #422.)</summary>
    private bool CarryingInsertedPet(int index) =>
        _settings.HudGlancePet && index >= 0 && index < _chips.Count
        && _chips[index].Key == MiniBarPresentation.PetKey;

    private void BeginCarry()
    {
        var chip = _chips[_from].Chip;
        _carry = new TranslateTransform();
        chip.RenderTransform = _carry;
        chip.Cursor = Cursors.SizeWE;
        if (AdornerLayer.GetAdornerLayer(_host) is { } layer)
        {
            _mark = new InsertionMark(_host);
            layer.Add(_mark);
            _mark.MoveTo(MarkX(CarryingInsertedPet(_from) ? MiniBarDrag.GlanceSlot : _from));
        }
    }

    private void EndCarry(int from)
    {
        if (from >= 0 && from < _chips.Count)
        {
            var chip = _chips[from].Chip;
            chip.RenderTransform = Transform.Identity;
            chip.Opacity = 1;
            chip.Cursor = Cursors.Hand;
        }
        _carry = null;
        if (_mark is null) return;
        AdornerLayer.GetAdornerLayer(_host)?.Remove(_mark);
        _mark = null;
    }

    // ---- MEASUREMENT ----------------------------------------------------------------

    /// <summary>Each drawn chip's horizontal centre, in the HOST's own coordinate space.
    /// Through the framework's transform rather than by adding up margins (trap 1): the bar
    /// sits under the widget's UI-scale <c>LayoutTransform</c>, and the pointer positions
    /// these are compared against come from the same <c>_host</c>.</summary>
    private IReadOnlyList<double> Midpoints()
    {
        var mids = new List<double>(_chips.Count);
        foreach (var (_, chip) in _chips)
        {
            var left = LeftOf(chip);
            mids.Add(double.IsNaN(left) ? double.MaxValue : left + chip.ActualWidth / 2);
        }
        return mids;
    }

    private double LeftOf(FrameworkElement chip)
    {
        if (!chip.IsVisible) return double.NaN;
        try { return chip.TransformToAncestor(_host).Transform(default).X; }
        catch (InvalidOperationException) { return double.NaN; }
    }

    /// <summary>The pointer, in the same space <see cref="Midpoints"/> answers in.</summary>
    private double PointerX(Point screen) => _host.PointFromScreen(screen).X;

    /// <summary>
    /// Where the always-on row's insertion gap ENDS and the cells begin, for a carried pet
    /// chip — or NaN when nothing on this render can say, which
    /// <c>MiniBarDrag.PetDropIndex</c> reads as "there is no gap".
    ///
    /// **Two readings of one sentence, because the chip is coming from a different place
    /// each way.** Carried UP from the cells it is in the gap once the pointer passes the DPS
    /// chip's own right edge — Bevel's arm condition, so the mark appears for that one slot
    /// instead of smearing across three fixed ones. Carried back DOWN it is in the gap until
    /// the pointer reaches the CELLS, which is the same rule read from the other side: an
    /// inserted chip is already right of the DPS edge, so that boundary would eject it on the
    /// first pixel past the threshold.
    ///
    /// With no cells to reach — pet inserted and nothing else starred — the boundary is the
    /// carried chip's own right edge, so carrying it rightwards still ejects it. A slot with
    /// no way back is the one-way door trap 59 is about.
    /// </summary>
    private double GlanceGapEndsAt(int from)
    {
        if (!_settings.HudGlancePet)
        {
            if (_glanceGapAfter is not { } dps) return double.NaN;
            var dpsLeft = LeftOf(dps);
            return double.IsNaN(dpsLeft) ? double.NaN : dpsLeft + dps.ActualWidth;
        }
        for (var i = 0; i < _chips.Count; i++)
            if (i != from) return LeftOf(_chips[i].Chip);
        if (from < 0 || from >= _chips.Count) return double.NaN;
        var carried = _chips[from].Chip;
        var left = LeftOf(carried);
        return double.IsNaN(left) ? double.NaN : left + carried.ActualWidth;
    }

    /// <summary>Where the insertion MARK is drawn while a landing in the gap is armed: the
    /// middle of the space between the DPS chip and whatever follows it. **The gap holds no
    /// element to hang it off** — both neighbours are `ExpandChip`s and that branch draws no
    /// divider — so it is measured off the DPS chip's own box plus half its trailing margin,
    /// the way <see cref="LeftOf"/> already measures a cell boundary (Bevel's §1 note,
    /// Helm-signed 2026-09-08). Both directions of the drag show the mark in the same place,
    /// because it says where the chip would LAND and that is one place.</summary>
    private double GlanceMarkX()
    {
        if (_glanceGapAfter is not { } dps) return 0;
        var left = LeftOf(dps);
        return double.IsNaN(left) ? 0 : left + dps.ActualWidth + ChipStyle.Gap.Right / 2;
    }

    /// <summary>Where the insertion mark is drawn for a landing slot: the leading edge of the
    /// chip that will be to its right, or the trailing edge of the bar for the last slot —
    /// and, for <c>MiniBarDrag.GlanceSlot</c>, the middle of the gap between the DPS chip and
    /// whatever follows it on the always-on row.</summary>
    private double MarkX(int slot)
    {
        if (slot == MiniBarDrag.GlanceSlot) return GlanceMarkX();
        if (slot < 0 || _chips.Count == 0) return 0;
        // Landing to the RIGHT of where it started means the mark belongs after that chip;
        // landing to the left means before it. Both are read off the chip's own box.
        var chip = _chips[Math.Min(slot, _chips.Count - 1)].Chip;
        var left = LeftOf(chip);
        if (double.IsNaN(left)) return 0;
        return slot > _from ? left + chip.ActualWidth : left;
    }

    private int IndexOf(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelperParent(node))
            for (var i = 0; i < _chips.Count; i++)
                if (ReferenceEquals(_chips[i].Chip, node)) return i;
        return -1;
    }

    private static DependencyObject? VisualTreeHelperParent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    /// <summary>The insertion caret: one accent hairline in the adorner layer, at the x the
    /// carried chip would land at. **An adorner and not a spacer**, because a spacer is
    /// layout and this bar's layout is the widget's width (trap 12).</summary>
    private sealed class InsertionMark : Adorner
    {
        private double _x;

        // Never hit-testable: an adorner sits above the content, and one that answered the
        // mouse would take the drop out of the hand carrying it.
        public InsertionMark(UIElement adorned) : base(adorned) => IsHitTestVisible = false;

        public void MoveTo(double x) { _x = x; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            var brush = (Application.Current?.TryFindResource("AccentBrush") as Brush)
                ?? Brushes.White;
            var height = ((FrameworkElement)AdornedElement).ActualHeight;
            dc.DrawRectangle(brush, null, new Rect(_x - 1, 0, 2, height));
        }
    }
}
