using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The COLLAPSED HUD bar — the row of numbers the widget shows while minimized, which is
/// the surface on screen for the whole time a player is farming.
///
/// Lifted out of <c>MainWindow</c> for Surface A / SA-1. **A view class, not another
/// <c>MainWindow.*.xaml.cs</c> partial**: <c>ArchitectureTests</c> sums the glob's matches
/// on purpose, so a partial buys nothing and leaves exactly as much untestable window
/// logic as before. The ratchet had zero headroom (4,516 lines against 4106 × 1.1 =
/// 4,516.6) and the standing move is to lift a surface rather than raise the ceiling.
///
/// Its behaviour was pinned in <c>tests/EQBuddy.E2E</c> BEFORE the move (<c>hudCells</c>,
/// green on the pre-move tree) — the WPF layer has no unit tests (docs/TestPlan.md §5),
/// so that assertion is the only thing standing between this move and a silent
/// regression. Same discipline as <c>WatchCardView</c> and <c>TravelsView</c>.
///
/// **It is not an <see cref="IWidgetCard"/> and takes no <see cref="ICardContext"/>.** It
/// is not a card: it has no section key, hangs in no expander, and needs none of the six
/// item/wiki services that interface exists for. What it genuinely cannot answer for
/// itself is handed in — the alert scheduler's due map, and the two windows a chip's
/// double-click opens — which is the same rule <c>ICardContext</c> applies one level up.
///
/// **Visibility and spacing stay with the host** (trap 15): this fills a panel the widget
/// owns and shows or hides nothing. <c>MainWindow</c> decides when the bar is on screen.
/// </summary>
internal sealed class HudBarView
{
    private readonly Panel _host;
    private readonly AppSettings _settings;
    private readonly Func<DateTime, IReadOnlyDictionary<string, DateTime>> _cuesDue;
    private readonly Action<BreakoutKind> _toggleBreakout;
    private readonly Action _openProgress;
    private readonly HudExpandBar _expand;
    private readonly Func<int?> _trackedLevel;
    private readonly Func<int> _activeBuffs;
    private readonly HudBarReorder _reorder;

    /// <summary>The single click a chip is still owed, armed on its mouse-DOWN and fired on
    /// the UP that never became a drag.
    ///
    /// **The click moved from down to up, and that is the whole gesture split.** A chip acted
    /// on the press before drag-reorder existed, which cannot survive a press that might turn
    /// into a carry — a peek would pin itself the instant the player took hold of the chip.
    /// It is a VIEW-level field for <see cref="AttachGestures"/>'s own reason: the panels are
    /// rebuilt every second, so the element that saw the down may not be the element that
    /// sees the up.</summary>
    private Action? _pendingClick;

    // Double-click state for the breakout chips, at the level of THIS view rather than of
    // an element: the chips are rebuilt every tick, so a rebuild landing between the two
    // clicks would leave the second one on a brand-new element with ClickCount back at 1.
    // Threshold reads the user's own Windows double-click speed; floor for a stray zero.
    private string? _lastChipClickKey;
    private DateTime _lastChipClickAt = DateTime.MinValue;
    private static readonly TimeSpan DoubleClickWindow =
        TimeSpan.FromMilliseconds(Math.Max(200, GetDoubleClickTime()));

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>Cells currently on the bar, for the <c>EQBUDDY_EXPAND</c> dump the E2E
    /// suite asserts on. Recorded by <see cref="Render"/> rather than read back off the
    /// panel, because a panel count would include the trio's own separator chrome.</summary>
    public int CellCount { get; private set; }

    /// <summary>The reorderable chips' keys as DRAWN, left to right — the <c>hudCellOrder</c>
    /// dump fact.
    ///
    /// **Recorded on the way past rather than recomputed for the dump** (trap 42). The whole
    /// feature is that a saved order reaches the control, so a dump that asked
    /// <c>MiniBarPresentation.ResolveOrder</c> again would report it working on a tree where
    /// the bar had gone on drawing the canonical order. It names only the chips the order
    /// governs: the trio is fixed leftmost and the pinned watch chips are a block after
    /// these, and neither is in the setting.</summary>
    public string CellOrderKey { get; private set; } = "-";

    /// <summary>Chip presses seen and drops written — see <see cref="HudBarReorder.PressCount"/>.
    /// One dump fact, because the two numbers are only ever read together.</summary>
    public string GripKey => $"{_reorder.PressCount},{_reorder.DropCount}";

    // ---- WHICH CHIP THE PANEL HANGS UNDER (the ~3:50 PM CT anchor fix) ----
    //
    // The under-bar panel used to open at the WIDGET's left edge, so it docked under the
    // leftmost chip whichever one was hovered. What it needs is the hovered chip's own
    // offset, and the bar is the only thing that knows where its chips are.
    //
    // **ONE field, written in two places, with a stated precedence — not two sources for
    // one fact (trap 4).** `ExpandChip` records the FIRST chip built for a target, which is
    // what answers for the `EQBUDDY_HUDEXPAND` hook and for any path that has a target and
    // no pointer; a real `MouseEnter` overwrites it with the chip actually under the cursor,
    // which is the only thing that can tell four Watch chips apart (they all share one
    // target). Cleared at the top of every `Render`, and re-armed by the same tick's enter —
    // the pointer resting on a chip leaves the OLD element and enters the NEW one once per
    // second, which is the behaviour `HudExpandWindow.Reveal` already guards against.

    private readonly Dictionary<HudExpandTarget, FrameworkElement> _chips = [];
    private FrameworkElement? _firstChip;

    /// <summary>
    /// A target's chip offset from the widget's left edge, in <c>Window.Left</c>'s own units,
    /// or NaN when this bar has no chip for it (the third slot is HPS and Progress was asked
    /// for; the hook fired before the bar drew; the widget is not minimized at all).
    ///
    /// **The transform is the framework's, so trap 1 cannot happen here**: the bar's content
    /// sits under the widget's UI-scale <c>LayoutTransform</c> and
    /// <see cref="Visual.TransformToAncestor"/> walks it, so what comes back is in the same
    /// DIP space as <c>Window.Left</c> rather than in pre-scale units. NaN is the honest
    /// answer for "cannot tell", and <see cref="HudChipRow.AnchoredLeft"/> reads it as "draw
    /// where you always drew".
    /// </summary>
    public double AnchorOf(HudExpandTarget target) =>
        _chips.TryGetValue(target, out var chip) ? OffsetOf(chip) : double.NaN;

    /// <summary>The LEFTMOST expansion chip's offset — the answer the panel used to give for
    /// every target, and therefore the one an assertion has to be able to compare against
    /// (the <c>hudChipAnchorFirst</c> dump fact). A test that only knew where the panel IS
    /// could not say it was not still docking under the first chip.</summary>
    public double FirstAnchor => _firstChip is { } chip ? OffsetOf(chip) : double.NaN;

    private static double OffsetOf(FrameworkElement chip)
    {
        if (!chip.IsVisible || Window.GetWindow(chip) is not { } window) return double.NaN;
        // A chip from a previous render that has already been detached measures nothing and
        // throws rather than answering; NaN is what "cannot tell" is spelled as here.
        try { return chip.TransformToAncestor(window).Transform(default).X; }
        catch (InvalidOperationException) { return double.NaN; }
    }

    /// <summary>Which number the glance's third slot currently is. Held here because the
    /// swap has hysteresis: <see cref="HudGlance"/> is a pure decision and needs to be
    /// told what it decided last time. Also the <c>hudGlance</c> dump fact.</summary>
    public HudThird Third { get; private set; } = HudThird.Experience;

    /// <summary>The <c>hudGlance</c> dump value — the dump is space-separated
    /// <c>key=value</c>, so this is one word.</summary>
    public string GlanceKey => Third == HudThird.Healing ? "hps" : "xp";

    /// <summary>The xp chip's hover text as it was last DRAWN, or null while the third
    /// slot is HPS and there is no xp chip to hover (OE-3).
    ///
    /// Recorded at the point the string is handed to the control rather than recomputed
    /// for the dump: "the tooltip says level 27" and "the app would compute 27 if asked"
    /// are different claims, and only the first one is the feature (trap 42). Null is the
    /// honest answer for the swapped-away state — a stale last-known level would read as a
    /// chip that is on the bar.</summary>
    public HudXpTip? XpTip { get; private set; }

    /// <param name="cuesDue">The alert scheduler's "when does each rule's cue fire" map.
    /// The bar cannot derive this from a snapshot — a cue is scheduled by the alert path,
    /// not by the session — so it is handed in rather than reached for.</param>
    /// <param name="toggleBreakout">Show or hide a breakout window; a chip's
    /// double-click.</param>
    /// <param name="openProgress">Open the Progress window; the xp chip's
    /// double-click.</param>
    /// <param name="expand">OE-1's under-bar expansion. The bar reports gestures to it and
    /// asks it which chip is lit; every decision about WHAT that means is
    /// <see cref="HudExpand"/>'s, unit-tested with no window.</param>
    /// <param name="trackedLevel">The durable per-character level from the quest ledger, or
    /// null when it has never recorded one (OE-3). Handed in for the same reason
    /// <paramref name="cuesDue"/> is: the bar cannot derive it from a snapshot — the ledger
    /// is the half that survives a restart and a truncated log — and reaching for the
    /// widget's store from here would put a service on a view that has none.</param>
    /// <param name="activeBuffs">How many buffs are up right now (OE-7's Buffs chip). Handed
    /// in for a reason the other two do not share: there is NO buff state on
    /// <see cref="StatsSnapshot"/> at all, so unlike every other cell this one cannot be
    /// formatted by <see cref="MiniBarPresentation"/> — which is also why "buffs" has never
    /// had a row in that table.</param>
    /// <param name="persist">Save the profile. Reached by exactly one path — the DROP of a
    /// chip drag, which is the only thing on this bar that writes a setting.</param>
    public HudBarView(Panel host, AppSettings settings,
        Func<DateTime, IReadOnlyDictionary<string, DateTime>> cuesDue,
        Action<BreakoutKind> toggleBreakout, Action openProgress, HudExpandBar expand,
        Func<int?> trackedLevel, Func<int> activeBuffs, Action persist)
    {
        _host = host;
        _settings = settings;
        _cuesDue = cuesDue;
        _toggleBreakout = toggleBreakout;
        _openProgress = openProgress;
        _expand = expand;
        _trackedLevel = trackedLevel;
        _activeBuffs = activeBuffs;
        _reorder = new HudBarReorder(host, settings,
            // THE DROP: persist, then redraw in the new order. The redraw is what the render
            // deferral above was holding back, so it happens here rather than a tick later —
            // a chip that snapped into place a second after the player let go would read as
            // a drag that did not take.
            onDropped: () => { persist(); Redraw(); },
            // A carry begins: let an unpinned peek go, so a panel does not flicker under a
            // moving chip. A pinned one stays and re-anchors on the next render (#404).
            onGestureStart: () => _expand.Away());
    }

    // The last numbers the bar drew, so a DROP can repaint immediately instead of waiting
    // for the next tick — a chip that snapped into place a second after the player let go
    // would read as a drag that did not take. Nothing else reads these: the tick brings its
    // own snapshot.
    private StatsSnapshot? _last;
    private string? _lastName;

    private void Redraw() { if (_last is { } s) Render(s, _lastName); }

    /// <summary>One mini-dashboard stat (2026-08-11, take two — David: no ovals):
    /// glyph + semibold tabular value as clean text, separated from its neighbor by
    /// a thin hairline divider rather than any chip chrome. A counting-down watch
    /// rule still announces itself by color alone. A chip whose stat has a breakout
    /// window takes a double-click to toggle it.</summary>
    /// <param name="expand">When set, the cell is an expansion chip on the OE-1 model —
    /// button chrome (lock 2), peek on hover (lock 3), pin on click (lock 4) — and it
    /// carries no hairline divider, because a button separates itself from its neighbour.
    ///
    /// **Every cell whose stat owns a floating window passes one, as of OE-7**, and that is
    /// the seat rather than a flourish: the ✕ on a float stopped writing
    /// <c>DisabledBreakouts</c>, so the chip is now the ONLY way back to a window a player
    /// has closed. A cell that took the old opt-in double-click and no target would be a
    /// float with no door for anyone who has never opened Settings (trap 59).</param>
    /// <param name="tip">A hover the cell writes itself, when its title alone would not say
    /// what the number means. The opt-in gesture is appended to it either way.</param>
    /// <param name="reorderable">This chip is one the player can carry (#191) — every starred
    /// cell and the buff set, and nothing else. The pinned watch chips pass false: they are a
    /// BLOCK after the cells this pass, their internal order is the rule list's, and a
    /// tooltip promising a drag they do not answer would be worse than silence.</param>
    private FrameworkElement Chip(string iconName, string value, string valueBrush,
        string? edgeBrush = null, BreakoutKind? breakout = null,
        HudExpandTarget? expand = null, string? tip = null, bool reorderable = false)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = expand is null ? new Thickness(0, 0, Tok.SpaceL, 0) : default,
        };
        var act = breakout is { } bk ? () => _toggleBreakout(bk) : (Action?)null;
        // A vector, not a glyph (#148, #166): the collapsed bar is on screen the whole
        // time a player farms, and it is exactly where a box instead of a skull would go
        // unnoticed on a Wine prefix.
        var icon = DesignSystem.Icon(iconName, "AccentBrush", size: Tok.IconInline);
        icon.Opacity = 0.9;
        icon.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        panel.Children.Add(icon);
        var v = new TextBlock
        {
            Text = value, FontSize = Tok.Spec(Tok.TypeRole.TitleSection).Size,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        v.SetResourceReference(TextBlock.ForegroundProperty, edgeBrush ?? valueBrush);
        panel.Children.Add(v);
        // A button gets no divider, and it must not get one: TrimLastDivider walks the LAST
        // child of the last StackPanel, so a divider inside a chip would be the thing it
        // collapsed when the bar's last cell is an expansion chip.
        if (expand is { } target)
        {
            // The three clauses in the order Bevel's face pass settled: what it is and the
            // click, then the drag if this chip can be dragged, then the opt-in
            // double-click LAST — `WithDoubleClick` has always been the tail of the
            // sentence, and a reorder clause after it would read as a qualifier on the
            // gesture a player had to go and switch on.
            var hover = tip ?? PeekTip(target);
            if (reorderable) hover = WithDragToReorder(hover);
            return ExpandChip(panel, target, WithDoubleClick(hover), act);
        }
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXxs, 0, Tok.SpaceXxs),
        };
        divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
        panel.Children.Add(divider);
        return panel;
    }

    /// <summary>An expansion chip's hover text: what it is, then the gesture.
    ///
    /// **The gesture sentence is the same words for every chip on the bar**, which is lock
    /// 9 in a tooltip — a chip that described its own private way of opening would be the
    /// per-tracker exception the lock forbids, and there are seven of them now. The DPS and
    /// HPS slots keep their own richer first halves (what the number MEANS) and pass a
    /// <c>tip</c>; everything else has a title and nothing to add.</summary>
    ///
    /// **It no longer appends the double-click itself** (Bevel's face pass, #418): `Chip` adds
    /// that clause last, after the reorder one, so the three sentences arrive in one place in
    /// a fixed order instead of two of them being wrapped around the third.
    private static string PeekTip(HudExpandTarget target) =>
        $"{HudExpand.Title(target)} — hover to peek, click to keep it open";

    /// <summary>The reorder sentence, appended to whatever a reorderable chip's hover already
    /// says (#191).
    ///
    /// **A gesture with no tell is a gesture nobody finds**, and drag-to-reorder is invisible
    /// by nature: there is no arrow, no handle and no shape on the bar that says a chip can be
    /// carried. Same words on every chip that can be, for the same reason
    /// <see cref="PeekTip"/> says the same words on every chip that peeks — a chip describing
    /// its own private gesture is the per-tracker exception lock 9 forbids.
    ///
    /// It is NOT under an opt-in the way the double-click is: that gesture competes with a
    /// click and had to be asked for, while a drag is dead space on this bar and costs a
    /// player who never uses it nothing. The wording is Bevel's to settle at the face
    /// review.</summary>
    private static string WithDragToReorder(string tip) => tip + ", drag to reorder";

    /// <summary>Append the opt-in gesture, and only for players who have opted in.
    ///
    /// It is the sentence the deleted <c>AttachDoubleClick</c> used to carry, and it is here
    /// rather than gone because that helper's tooltip was the ONLY place on the bar the
    /// double-click was advertised (traps 20/26 — a fold owes an account of every control it
    /// absorbed, and the account for this one is this method). Silent off, so a bar nobody
    /// configured does not describe a gesture that does nothing.</summary>
    private string WithDoubleClick(string tip) => _settings.DoubleClickChipsToggleBreakouts
        ? tip + ", or double-click to open its window straight away"
        : tip;

    // `AttachDoubleClick` LIVED HERE AND IS GONE (OE-7), which is lock 6's sweep rather than
    // tidying: every cell that owned a floating window is an ExpandChip now, and ExpandChip
    // attaches the opt-in double-click itself alongside the single click. The old helper's
    // last two callers both stopped passing it a key, so it was a method that could no longer
    // fire — trap 43's polarity (a producer with no consumer), and the kind of thing that
    // reads as coverage while doing nothing.
    //
    // **What it also carried was a SENTENCE, and that is the half a deletion loses silently**
    // (traps 20/26): its tooltip was the only place the double-click gesture was advertised on
    // the bar. It moved into `PeekTip`, which appends it under the same opt-in — so a player
    // who turned the gesture on is still told about it, on the chip, where they were before.
    //
    // Its two hard-won notes belong to `AttachGestures` below and are stated there.

    /// <summary>
    /// **ONE mouse-down handler per element, whatever gestures it carries** (OE-1).
    ///
    /// Transparent (not null) so the gaps between glyph and value are hit-testable too.
    /// Two things conspired against WPF's own double-click here, so it is detected on the
    /// VIEW instead:
    ///   1. The bar's OnDrag starts a modal window DragMove on the FIRST left-click
    ///      anywhere on the bar; that capture disrupted the click sequence and the cursor
    ///      flickered into drag mode (the tell). Eating the click stops it.
    ///   2. Render rebuilds these panels every 1 s tick, so a rebuild landing between the
    ///      two clicks left the second click on a brand-new element and reset ClickCount
    ///      to 1 — an intermittent miss.
    /// Keying on (key, time) at this level survives both: the panel can be replaced
    /// mid-gesture and the second click still lands. The widget is still dragged from any
    /// non-chip part of the bar.
    ///
    /// WPF stops calling handlers once one sets <c>Handled</c>, including later ones on the
    /// SAME element — and this element must set it, or the bar's <c>OnDrag</c> starts a modal
    /// <c>DragMove</c> on the first click and eats the sequence (the note above). So a second
    /// `+=` for the single click would simply never run, silently, with nothing in a diff to
    /// say so. The two gestures share one handler instead, and the double-click keeps
    /// priority: <c>DoubleClickChipsToggleBreakouts</c> is untouched by OE-1 and a player who
    /// opted into it must not lose it to the new primary path.
    /// </summary>
    private void AttachGestures(FrameworkElement element, string key, Action? single, Action? doubleClick)
    {
        // Transparent, not null, so the gaps between glyph and value are hit-testable too.
        // A Border already has a ground of its own (ExpandChip paints one).
        if (element is Panel panel) panel.Background = System.Windows.Media.Brushes.Transparent;
        element.Cursor = Cursors.Hand;
        element.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var now = DateTime.Now;
            var isDouble = _lastChipClickKey == key && now - _lastChipClickAt <= DoubleClickWindow;
            _lastChipClickKey = isDouble ? null : key;   // consume, so a third click starts fresh
            _lastChipClickAt = now;
            // The DOUBLE-click still fires on the second press, exactly as it did: it is a
            // gesture the player opted into and it must not start costing them an extra
            // mouse-up. It cancels any single the first press armed.
            if (isDouble && doubleClick is not null) { _pendingClick = null; doubleClick(); }
            else _pendingClick = single;
        };
        // THE SINGLE CLICK NOW FIRES HERE, and only if the press did not become something
        // else. `Consumed` is read while HudBarReorder is still mid-gesture — its own handler
        // on the host bubbles and therefore runs after this one, which is what makes the
        // question answerable at all.
        element.MouseLeftButtonUp += (_, _) =>
        {
            var pending = _pendingClick;
            _pendingClick = null;
            if (pending is not null && !_reorder.Consumed) pending();
        };
    }

    /// <summary>
    /// A glance slot that EXPANDS — owner locks 2, 3 and 4 on one control.
    ///
    /// **Lock 2 ("chips must look like buttons") is the border, and it is drawn from
    /// <see cref="ChipStyle"/> rather than invented**: the standing pill rule says there is
    /// one selectable-pill vocabulary in this app and sixteen hand-built copies is how it got
    /// one. The COMPACT variant, because this sits on the bar that is on screen the whole
    /// time a player farms and the card pill's weight would double the HUD's height.
    ///
    /// **The chrome is fixed-size and only its INK changes** (trap 12). Padding, radius and
    /// border thickness are constants and the value keeps its reserved width, so a hover, a
    /// pin and a new sample all repaint identical pixels and measure identically — which is
    /// the whole reason the widget can be <c>SizeToContent</c> over a fullscreen game.
    ///
    /// **Only the two expandable slots wear it in this PR**, which is lock 8: DPS, HPS and
    /// Progress ship first and the owner tests the mechanics before every other tracker
    /// follows on the same pattern (lock 9 — no exceptions, later, not never).
    /// </summary>
    private Border ExpandChip(UIElement content, HudExpandTarget target, string tip,
        Action? doubleClick)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(ChipStyle.CompactRadius),
            BorderThickness = new Thickness(ChipStyle.BorderThickness),
            Padding = new Thickness(ChipStyle.CompactPadding.Left, ChipStyle.CompactPadding.Top,
                ChipStyle.CompactPadding.Right, ChipStyle.CompactPadding.Bottom),
            Margin = new Thickness(0, 0, ChipStyle.Gap.Right, 0),
            Child = content,
            ToolTip = tip,
        };
        // Lit while THIS tracker's panel is the one on screen. Read off the model on every
        // rebuild, never remembered here: "the chip is lit" and "the panel is up" are one
        // fact and a second copy of it is trap 4.
        var lit = _expand.Shown == target;
        if (lit) chip.SetResourceReference(Border.BackgroundProperty, "ToggleHighlightBrush");
        else chip.Background = System.Windows.Media.Brushes.Transparent;
        chip.SetResourceReference(Border.BorderBrushProperty,
            lit ? "AccentBrush" : "HairlineBrush");
        // WHERE THE PANEL HANGS FROM. The first chip built for a target answers for the
        // pointer-less paths; the pointer, when there is one, names the chip itself — see
        // the field's own note above for why both write one field.
        _chips.TryAdd(target, chip);
        _firstChip ??= chip;
        chip.MouseEnter += (_, _) => { _chips[target] = chip; _expand.Hover(target); };
        chip.MouseLeave += (_, _) => _expand.Away();
        // The double-click stays behind its own opt-in, exactly as it was: OE-1 does not
        // touch DoubleClickChipsToggleBreakouts, and honouring the gesture for a player who
        // never turned it on would be this PR changing a setting's meaning by accident.
        AttachGestures(chip, HudExpand.Key(target), single: () => _expand.Click(target),
            doubleClick: _settings.DoubleClickChipsToggleBreakouts ? doubleClick : null);
        return chip;
    }

    /// <summary>The last chip's divider has nothing to divide — trim it.</summary>
    private void TrimLastDivider()
    {
        if (_host.Children.Count > 0 && _host.Children[^1] is StackPanel { Children.Count: > 0 } last
            && last.Children[^1] is Border divider)
            divider.Visibility = Visibility.Collapsed;
    }

    /// <summary>A fixed-width slot on the collapsed HUD: an optional icon and one string
    /// whose measured size never changes.
    ///
    /// **The reserved width is the trap-12 guard, and it is the half a diff cannot see.**
    /// The widget is <c>SizeToContent</c>, so a readout that measures wider resizes an
    /// always-on-top transparent window over a fullscreen game — every second, forever
    /// (#173, KoboldCoterie). <see cref="HudGlance"/> pads every string to one length and
    /// this pins the control to one width; both together mean a new sample changes pixels
    /// and nothing else. <c>PerfReadout</c>'s label is the worked example.</summary>
    /// <param name="expand">When set, the slot is an OE-1 expansion chip: it wears button
    /// chrome (lock 2), peeks on hover (lock 3) and pins on click (lock 4), and it carries no
    /// hairline divider — a button separates itself from its neighbour.</param>
    private FrameworkElement GlanceSlot(string? iconName, string text, double width, string? tip,
        Action? onDoubleClick = null, string? doubleClickHint = null,
        HudExpandTarget? expand = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = expand is null ? new Thickness(0, 0, Tok.SpaceL, 0) : default,
            ToolTip = expand is null ? tip : null,
        };
        if (iconName is not null)
        {
            // A vector, never a glyph (#148, #166) — same rule as the starred cells below.
            var icon = DesignSystem.Icon(iconName, "AccentBrush", size: Tok.IconInline);
            icon.Opacity = 0.9;
            icon.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
            panel.Children.Add(icon);
        }
        var value = new TextBlock
        {
            Text = text,
            Width = width,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = Tok.Spec(Tok.TypeRole.TitleSection).Size,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        panel.Children.Add(value);
        // A button gets no divider, and it must not get one: TrimLastDivider walks the LAST
        // child of the last StackPanel, so a divider inside a chip would be the thing it
        // collapsed when the bar has no starred cells at all.
        if (expand is { } target)
            return ExpandChip(panel, target,
                WithDoubleClick(doubleClickHint ?? tip ?? HudExpand.Title(target)),
                onDoubleClick);
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXxs, 0, Tok.SpaceXxs),
        };
        divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
        panel.Children.Add(divider);
        return panel;
    }

    /// <summary>The always-on trio — character name, DPS, and XP%/hr or HPS — ahead of
    /// every starred cell (Surface A / SA-1, spec §3).
    ///
    /// The DECISION is <see cref="HudGlance"/>'s and is unit-tested with no window; what
    /// happens here is drawing. The name slot carries no icon: it is a label, not a
    /// metric, and inventing a person vector for it would be geometry nobody asked
    /// for.</summary>
    private void RenderGlance(StatsSnapshot s, string? characterName)
    {
        var glance = HudGlance.Next(Third, s, characterName);
        Third = glance.Third;
        _host.Children.Add(GlanceSlot(null, glance.Name, HudGlance.NameReservedWidth,
            glance.Name.Length > 0 ? null : HudGlance.EmptyNameTooltip));
        // THE TWO EXPANSION CHIPS (OE-1). DPS is always slot two; slot three is HPS or the
        // XP rate, and the tracker the panel opens FOLLOWS that swap — which is why the
        // target is decided here, from the glance's own answer, rather than by the panel
        // guessing what the third slot currently means.
        _host.Children.Add(GlanceSlot(HudGlance.DpsIcon, glance.Dps,
            HudGlance.MetricReservedWidth,
            "Damage per second — hover to peek, click to keep it open",
            expand: HudExpandTarget.Dps));
        // The xp cell's double-click SURVIVES the promotion, on the slot that replaced it.
        // While the widget is minimized it was the only door to the Progress window — the
        // Progress card is on the expanded widget, so it is not one — and a promotion must
        // not shut a door (trap 59). It is attached only while the slot IS the xp number:
        // a gesture that silently means something else half the time is worse than none.
        // The opt-in double-click is untouched by OE-1 and keeps priority on this chip; the
        // single click is the primary, discoverable path Bevel's §4 asked for.
        // OE-3: the xp slot's hover now carries the next-level ETA and the tracked level —
        // both of which the app has always had and neither of which was on any screen (see
        // HudXpTooltip). The wording is UI.Shared's and the ETA sentence is the Progress
        // room's own, so the two surfaces cannot forecast one session differently (trap 4).
        // Recorded on the way past for the dump: what was DRAWN, not what could be
        // computed (trap 42). Null while the slot is HPS — there is no xp chip then.
        XpTip = glance.Third == HudThird.Healing ? null : HudXpTooltip.For(s, _trackedLevel());
        _host.Children.Add(glance.Third == HudThird.Healing
            ? GlanceSlot(glance.ThirdIcon, glance.ThirdText, HudGlance.MetricReservedWidth,
                "Healing per second — while healing is the weight of the last half-minute; "
                + "hover to peek, click to keep it open",
                expand: HudExpandTarget.Hps)
            : GlanceSlot(glance.ThirdIcon, glance.ThirdText, HudGlance.MetricReservedWidth,
                tip: null, onDoubleClick: _openProgress,
                doubleClickHint: XpTip!.Value.Text,
                expand: HudExpandTarget.Progress));
    }

    /// <summary>
    /// THE BUFF SET'S CHIP (OE-7), and the one chip on this bar that
    /// <see cref="MiniBarPresentation"/> cannot format. "buffs" has always been a valid
    /// <c>MiniStats</c> key that gated the Buffs window and drew nothing — so that window's
    /// only doors were Options and an opt-in double-click on a chip that did not exist. Once
    /// the ✕ became a transient close it needed a real one (trap 59: a hotkey is not a door,
    /// and neither is a Settings tick). The count comes from the buff tracker because no
    /// snapshot field carries it; the star is unchanged, so nobody who has not asked for the
    /// window gets a new chip.
    ///
    /// **It is called from inside the ordered walk since #191**, so it can be dragged past
    /// its neighbours like any other chip: its PLACE is a member of
    /// <see cref="MiniBarPresentation.CanonicalOrder"/> even though its FACE is built here.
    /// </summary>
    private void RenderBuffs()
    {
        var up = _activeBuffs();
        var chip = Chip(
            BreakoutPresentation.Icon(BreakoutPresentation.Buffs), $"{up}", "AccentBrush",
            breakout: BreakoutKind.Buffs, expand: HudExpandTarget.Buffs,
            tip: $"{up} buff{(up == 1 ? "" : "s")} up — hover to peek, click to keep it open",
            reorderable: true);
        _host.Children.Add(chip);
        _reorder.Register(MiniBarPresentation.BuffsKey, chip);
    }

    /// <param name="characterName">Whoever the log is naming. Handed in rather than taken
    /// off the snapshot because the snapshot does not carry it — the session does, and the
    /// widget already passes it the same way to EQBuddy Mobile.</param>
    public void Render(StatsSnapshot s, string? characterName)
    {
        _last = s;
        _lastName = characterName;
        // A LIVE CARRY OWNS THE BAR. The row rebuilds every second, and replacing the
        // elements under a captured drag takes the chip out of the player's hand mid-gesture
        // — the chip-row's own "no re-sort under the cursor" rule, one gesture deeper.
        // Everything else on the widget goes on ticking; this one panel defers until the
        // drop, which repaints it immediately.
        if (_reorder.Dragging) return;
        _host.Children.Clear();
        _reorder.Clear();
        // The anchors belong to the elements this render is about to replace: a chip from
        // last tick is detached and can only answer NaN, which would drop the panel back to
        // the widget's edge for one tick every second.
        _chips.Clear();
        _firstChip = null;
        // FIRST, and unconditionally: the three numbers that no longer have a toggle.
        RenderGlance(s, characterName);
        // Which cells, in which order, with which icon and what each reads: all from
        // UI.Shared. Both widgets carried this table by hand, identically, comments and
        // all — and the Avalonia one is the lane that historically drifted.
        //
        // **WHICH ORDER, SINCE #191: the player's.** `DrawnKeys` is `MiniBarOrder`
        // reconciled against the canonical list and filtered to the stats with a ★ — one
        // membership decision, so the bar cannot draw a chip the order does not know about
        // (trap 4). An untouched profile's empty setting resolves to exactly the bar that
        // shipped before it existed.
        //
        // **"buffs" is IN this walk now**, which is the one thing that changes about it
        // besides the order: it used to be appended after the loop, because it is the one
        // cell `MiniBarPresentation` cannot format. A chip drawn outside the ordered walk is
        // a chip that can never be dragged past its neighbours, so its PLACE is in the list
        // (`CanonicalOrder`) while its FACE is still built here.
        var drawn = UI.Shared.MiniBarPresentation.DrawnKeys(_settings);
        foreach (var key in drawn)
        {
            if (key == UI.Shared.MiniBarPresentation.BuffsKey) { RenderBuffs(); continue; }
            // Non-null by construction: `DrawnKeys` has already refused any key this table
            // cannot put a face on, which is how a settings file from a later version leaves
            // no hole in the bar.
            var cell = UI.Shared.MiniBarPresentation.Cell(s, key)!;
            // **EVERY CELL IS AN EXPANSION CHIP AS OF OE-9** — the owner's ~1:29 PM CT amend
            // (2026-09-07): *"Everything on the minimized bar MUST have hover peek +
            // pop-out"*. The hand switch that used to pick a target per cell is gone: the
            // cell's own MiniStats key IS the HudExpand key, so one lookup answers for the
            // chip, the panel, the title, the icon and the ⧉ (trap 4). A `null` here would
            // now mean a cell HudExpand has never heard of, which is a settings file from a
            // later version — and it gets the old plain chip rather than a hole in the bar.
            //
            // There is no "xp" case here any more: xp is an always-on trio slot since
            // SA-1, and RenderGlance above carries both its number and the double-click
            // that opens the Progress window (Bevel's fold, Helm-signed 2026-08-24 —
            // "reuse existing theme window on current tab … retire tab-less 272x135
            // float"). A branch for a key MiniBarPresentation.Order no longer contains
            // would be unreachable code claiming to be a feature.
            var target = HudExpand.TargetForKey(cell.Key);
            // The opt-in double-click is unchanged and still only ever toggles a FLOAT:
            // `DoubleClickChipsToggleBreakouts` means "open its window straight away", and
            // for the cells whose destination is a theme window the single click's panel and
            // its ⧉ are the path. Read off the destination rather than a second switch, so a
            // chip cannot double-click to one window and pop out to another.
            BreakoutKind? breakout =
                target is { } t && HudExpand.DestinationOf(t).BreakoutName is { } name
                    ? Enum.Parse<BreakoutKind>(name)
                    : null;
            var chip = Chip(cell.Icon, cell.Text, "AccentBrush", breakout: breakout,
                expand: target, reorderable: true);
            _host.Children.Add(chip);
            _reorder.Register(key, chip);
        }
        CellOrderKey = UI.Shared.MiniBarPresentation.OrderKey(drawn);

        // Per-rule pins: only the rules you picked (📌 in Options), not every enabled one.
        //
        // THE MASTER TOGGLE IS GONE (Surface A / SA-R). `AppSettings.PinWatchChips` gated
        // this loop as well, which made two switches answer one question — "does this chip
        // show" — with the pin already on the rule row and already the only one the Evolved
        // shell's Alerts tab carries. Helm's #341 sign was to reduce them to one, and the
        // pin is the survivor. `WatchPinMigration.RetireGroupPin` translates an unticked
        // master into per-rule unpins once, so nobody's bar changes under them.
        //
        // **THEY ARE A BLOCK, AFTER THE CELLS, AND NOT REORDERABLE** (#191's stated scope,
        // Helm-signed 2026-09-07: the block is confirmed). `MiniBarOrder` is keyed by STAT
        // KEY and these are keyed by rule id, so per-rule placement widens that setting
        // rather than reusing it — the seam is named in the setting's own doc and not built.
        // Their chips pass `reorderable: false`, so no tooltip here promises a drag that
        // does not answer.
        //
        // **The order is the RULE LIST's, which is not `WatchSortMode`'s** (Helm's item 3,
        // same sign: a note, not a block). That setting sorts the Watch surface; this loop
        // has always walked `TrackedRules` as stored, and the two have never agreed by
        // construction. Left alone deliberately — making the chips follow a sort the player
        // picked for a WINDOW would be one setting answering two questions, and the What's-new
        // entry says the two are separate rather than leaving it to be discovered.
        var due = _cuesDue(DateTime.Now);
        foreach (var rule in _settings.TrackedRules.Where(r => r.Enabled && r.Pinned))
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t => t.Id == rule.Id);
            // A rule with a cue in flight shows time remaining instead of its count: while
            // something is counting down, when it fires is the only thing you want to know.
            var counting = due.TryGetValue(rule.Id, out var at);
            // A counting-down chip wears the warn edge too — state has a shape.
            //
            // OE-7: every pinned rule's chip expands, and every one of them expands the SAME
            // target — the Watch float is a list of all of them, so a per-rule target would
            // be four names for one window. Lock 1 then does the rest: hovering a second
            // rule's chip replaces the peek rather than stacking a second panel.
            _host.Children.Add(counting
                ? Chip("Timer", $"{name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}",
                    "WarnBrush", edgeBrush: "WarnBrush", breakout: BreakoutKind.Watch,
                    expand: HudExpandTarget.Watch)
                : Chip("Target", $"{name} {result?.TotalQuantity ?? 0}", "AccentBrush",
                    breakout: BreakoutKind.Watch, expand: HudExpandTarget.Watch));
        }

        TrimLastDivider();
        CellCount = _host.Children.Count;

        // THE EMPTY-STATE HINT IS GONE, and this is where it went (traps 20/26 — a fold
        // has to say what happened to every control it absorbed).
        //
        // "★ star stats in full view" used to render whenever nothing was starred and no
        // rule was pinned. Since SA-1 the trio is drawn unconditionally, so that condition
        // can never hold again: the bar is never empty, and a hint that can never appear is
        // worse than no hint — it reads as coverage while being unreachable. The job it did
        // (a bare bar teaching you how to fill it) is done better by the bar not being bare.
        // Nothing else pointed at it, and the stars it named still exist for the six keys
        // that kept one.
    }
}
