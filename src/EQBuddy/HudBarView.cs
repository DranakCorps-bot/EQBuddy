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

    /// <summary>Which number the glance's third slot currently is. Held here because the
    /// swap has hysteresis: <see cref="HudGlance"/> is a pure decision and needs to be
    /// told what it decided last time. Also the <c>hudGlance</c> dump fact.</summary>
    public HudThird Third { get; private set; } = HudThird.Experience;

    /// <summary>The <c>hudGlance</c> dump value — the dump is space-separated
    /// <c>key=value</c>, so this is one word.</summary>
    public string GlanceKey => Third == HudThird.Healing ? "hps" : "xp";

    /// <param name="cuesDue">The alert scheduler's "when does each rule's cue fire" map.
    /// The bar cannot derive this from a snapshot — a cue is scheduled by the alert path,
    /// not by the session — so it is handed in rather than reached for.</param>
    /// <param name="toggleBreakout">Show or hide a breakout window; a chip's
    /// double-click.</param>
    /// <param name="openProgress">Open the Progress window; the xp chip's
    /// double-click.</param>
    public HudBarView(Panel host, AppSettings settings,
        Func<DateTime, IReadOnlyDictionary<string, DateTime>> cuesDue,
        Action<BreakoutKind> toggleBreakout, Action openProgress)
    {
        _host = host;
        _settings = settings;
        _cuesDue = cuesDue;
        _toggleBreakout = toggleBreakout;
        _openProgress = openProgress;
    }

    /// <summary>One mini-dashboard stat (2026-08-11, take two — David: no ovals):
    /// glyph + semibold tabular value as clean text, separated from its neighbor by
    /// a thin hairline divider rather than any chip chrome. A counting-down watch
    /// rule still announces itself by color alone. A chip whose stat has a breakout
    /// window takes a double-click to toggle it.</summary>
    /// <summary>
    /// <paramref name="onDoubleClick"/> is what the gesture DOES, and it is pluggable because
    /// not every chip toggles a breakout any more: the xp chip opens the Progress WINDOW, which
    /// has the tabs (Bevel's fold, Helm-signed — "reuse existing theme window on current tab …
    /// retire tab-less 272×135 float"). The gesture is keyed on <paramref name="clickKey"/>
    /// rather than on a BreakoutKind so a chip with no breakout can still own a double-click.
    /// </summary>
    private StackPanel Chip(string iconName, string value, string valueBrush, string? edgeBrush = null,
        BreakoutKind? breakout = null, string? clickKey = null, Action? onDoubleClick = null,
        string? doubleClickHint = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, Tok.SpaceL, 0),
        };
        AttachDoubleClick(panel, clickKey ?? breakout?.ToString(),
            onDoubleClick ?? (breakout is { } bk ? () => _toggleBreakout(bk) : null),
            doubleClickHint);
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
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXxs, 0, Tok.SpaceXxs),
        };
        divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
        panel.Children.Add(divider);
        return panel;
    }

    /// <summary>Give an element the bar's opt-in double-click gesture.
    ///
    /// Lifted out of the chip builder in SA-1 so the always-on XP slot can carry it too:
    /// while the widget is minimized the xp cell was the ONLY door to the Progress window,
    /// and promoting the number must not shut a door (trap 59 — enumerate the entrances
    /// before you subtract a surface).
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
    /// non-chip part of the bar; with the opt-in off the cell stays inert and a
    /// double-click expands the widget as before.</summary>
    private void AttachDoubleClick(Panel element, string? key, Action? act, string? hint)
    {
        if (key is null || act is null || !_settings.DoubleClickChipsToggleBreakouts) return;
        element.Background = System.Windows.Media.Brushes.Transparent;
        element.Cursor = Cursors.Hand;
        element.ToolTip = hint ?? $"Double-click to show or hide the {key} breakout";
        element.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var now = DateTime.Now;
            if (_lastChipClickKey == key && now - _lastChipClickAt <= DoubleClickWindow)
            {
                _lastChipClickKey = null;   // consume, so a third click starts fresh
                act();
            }
            else
            {
                _lastChipClickKey = key;
                _lastChipClickAt = now;
            }
        };
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
    private StackPanel GlanceSlot(string? iconName, string text, double width, string? tip,
        string? clickKey = null, Action? onDoubleClick = null, string? doubleClickHint = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, Tok.SpaceL, 0),
            ToolTip = tip,
        };
        AttachDoubleClick(panel, clickKey, onDoubleClick, doubleClickHint);
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
        _host.Children.Add(GlanceSlot(HudGlance.DpsIcon, glance.Dps,
            HudGlance.MetricReservedWidth, "Damage per second"));
        // The xp cell's double-click SURVIVES the promotion, on the slot that replaced it.
        // While the widget is minimized it was the only door to the Progress window — the
        // Progress card is on the expanded widget, so it is not one — and a promotion must
        // not shut a door (trap 59). It is attached only while the slot IS the xp number:
        // a gesture that silently means something else half the time is worse than none.
        _host.Children.Add(glance.Third == HudThird.Healing
            ? GlanceSlot(glance.ThirdIcon, glance.ThirdText, HudGlance.MetricReservedWidth,
                "Healing per second — while healing is the weight of the last half-minute")
            : GlanceSlot(glance.ThirdIcon, glance.ThirdText, HudGlance.MetricReservedWidth,
                tip: null, clickKey: "xp", onDoubleClick: _openProgress,
                doubleClickHint: "Experience per hour — double-click to open the Progress window"));
    }

    /// <param name="characterName">Whoever the log is naming. Handed in rather than taken
    /// off the snapshot because the snapshot does not carry it — the session does, and the
    /// widget already passes it the same way to EQBuddy Mobile.</param>
    public void Render(StatsSnapshot s, string? characterName)
    {
        _host.Children.Clear();
        // FIRST, and unconditionally: the three numbers that no longer have a toggle.
        RenderGlance(s, characterName);
        // Which cells, in which order, with which icon and what each reads: all from
        // UI.Shared. Both widgets carried this table by hand, identically, comments and
        // all — and the Avalonia one is the lane that historically drifted.
        foreach (var cell in UI.Shared.MiniBarPresentation.Cells(s, _settings.MiniStats))
        {
            // dps and hps left this map with their cells (SA-1). Their windows are
            // untouched and Options -> Cards & windows still turns each on and off, which
            // is the door a default profile has: the double-click gesture is opt-in and
            // OFF out of the box (DoubleClickChipsToggleBreakouts), so no player who has
            // configured nothing loses a way in (trap 59).
            BreakoutKind? breakout = cell.Key switch
            {
                "pet" => BreakoutKind.Pet,
                "loot" => BreakoutKind.Loot,
                _ => null,   // kills/procs/motes/money/deaths have no breakout
            };
            // There is no "xp" case here any more: xp is an always-on trio slot since
            // SA-1, and RenderGlance above carries both its number and the double-click
            // that opens the Progress window (Bevel's fold, Helm-signed 2026-08-24 —
            // "reuse existing theme window on current tab … retire tab-less 272x135
            // float"). A branch for a key MiniBarPresentation.Order no longer contains
            // would be unreachable code claiming to be a feature.
            _host.Children.Add(Chip(cell.Icon, cell.Text, "AccentBrush", breakout: breakout));
        }

        // Per-rule pins: only the rules you picked (📌 in Options), not every enabled one.
        //
        // THE MASTER TOGGLE IS GONE (Surface A / SA-R). `AppSettings.PinWatchChips` gated
        // this loop as well, which made two switches answer one question — "does this chip
        // show" — with the pin already on the rule row and already the only one the Evolved
        // shell's Alerts tab carries. Helm's #341 sign was to reduce them to one, and the
        // pin is the survivor. `WatchPinMigration.RetireGroupPin` translates an unticked
        // master into per-rule unpins once, so nobody's bar changes under them.
        var due = _cuesDue(DateTime.Now);
        foreach (var rule in _settings.TrackedRules.Where(r => r.Enabled && r.Pinned))
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t => t.Id == rule.Id);
            // A rule with a cue in flight shows time remaining instead of its count: while
            // something is counting down, when it fires is the only thing you want to know.
            var counting = due.TryGetValue(rule.Id, out var at);
            // A counting-down chip wears the warn edge too — state has a shape.
            _host.Children.Add(counting
                ? Chip("Timer", $"{name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}",
                    "WarnBrush", edgeBrush: "WarnBrush", breakout: BreakoutKind.Watch)
                : Chip("Target", $"{name} {result?.TotalQuantity ?? 0}", "AccentBrush",
                    breakout: BreakoutKind.Watch));
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
