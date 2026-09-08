using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Which chip FAMILY a chicklet belongs to. The row is ordered by family, and a family is
/// the unit SA-4's Place and Mute verbs operate on (B3 §3, Helm-signed 2026-09-05).
///
/// **<see cref="Mez"/> is the whole FIGHT family — mez chips AND slow chips.** They shared
/// one window and one saved position before SA-2, they share a family now, and the signed
/// Edit-mode default order names four families ("mez, spawn, watch-fire, buff") rather than
/// five. Splitting slow out would invent a fifth family the sign does not contain, and it
/// would have to be given an order and a mute of its own by an executor rather than by
/// Bevel. The two halves already agree on every family-level trait below.
/// </summary>
public enum HudChipFamily
{
    /// <summary>The fight family: active mezzes and landed slows. First in the default
    /// order — combat-urgent, which is the distinction the two retired windows' own doc
    /// comments drew ("mez chips get parked next to the fight, spawn chips are ambient").
    /// </summary>
    Mez,

    /// <summary>Spawn countdowns: ambient camp furniture, every running timer on the
    /// server regardless of zone.</summary>
    Spawn,

    /// <summary>A watch rule that has just fired and is still lingering (SA-3). Net-new UI —
    /// nothing visual existed for this before the row; the only on-screen form a firing rule
    /// had was <c>AlertWindow</c>'s six-second banner. See <see cref="WatchFireLedger"/>.
    /// </summary>
    WatchFire,

    /// <summary>A buff on you that is inside its expiry warning window (SA-3). Also net-new:
    /// the Buffs card has drawn these countdowns since #120, but only where a player has to
    /// look away from the game to read them.</summary>
    Buff,
}

/// <summary>One chicklet on the row, with the family it came from. The family is carried
/// rather than re-derived from the icon: two families can legitimately draw the same
/// vector, and SA-4's Mute is keyed on the family.</summary>
public readonly record struct HudChipEntry(HudChipFamily Family, SpawnChip Chip);

/// <summary>
/// THE ONE CHIP ROW (Surface A / SA-2) — which families are on it, in what order, and how
/// each family's chicklet reads.
///
/// **Consolidation, not extension** (#324 item 2, Helm-signed): <c>SpawnChipsWindow</c> and
/// <c>MezChipsWindow</c> were two always-on-top floats with two saved positions, two
/// grow-up settings and two near-copies of one chicklet renderer — which is how #122 and
/// #152 happened twice each. They are one row now, hosted in a companion window slaved to
/// the HUD's position with no geometry of its own.
///
/// **<see cref="ChipStackPlan"/> is still the "should this family show at all" answer** and
/// is unchanged by this file: its <c>SpawnStack</c> and <c>FightStack</c> rules — the
/// Bevel-signed Camps hide-rule, focus-hide, the two Options toggles — decide whether a
/// family contributes chips, and <see cref="Merge"/> decides what the row looks like once
/// they have. Two questions, two homes.
///
/// **The family-level traits below are the fold's honest bookkeeping** (traps 20/26): the
/// two windows did NOT render identically, and a merge that flattened the difference would
/// have been a silent behaviour change wearing a refactor's clothes.
/// <list type="bullet">
/// <item>A due SPAWN chip flips its countdown to the word "DUE"; a due (about-to-wake) mez
/// chip keeps counting and only takes the warning tint. Both are preserved.</item>
/// <item>The spawn gauge FILLS with elapsed time; the fight gauge DRAINS the remaining
/// share, like a buff bar. Both take the same 0..1 elapsed <see cref="SpawnChip.Fraction"/>
/// and differ only in which side they paint.</item>
/// </list>
///
/// Framework-free, like everything in this project (a test enforces it) — the WPF layer has
/// no unit tests (docs/TestPlan.md §5), so every decision the row makes lives here where it
/// can be asserted without a window.
/// </summary>
public static class HudChipRow
{
    /// <summary>Family order on the row, left to right. Mez first: combat-urgent before
    /// ambient. SA-4 makes this a stored <c>HudChipOrder</c> the player can nudge; until
    /// then <see cref="Merge"/>'s optional order argument is the seam that will read it, so
    /// the setting arrives without reshaping this file.
    ///
    /// **The four names are the ones SA-4's signed default order already spells** — "mez,
    /// spawn, watch-fire, buff, urgency order" — so SA-3 extends this list to exactly the
    /// shape the setting will ship with rather than inventing an order an executor would have
    /// to reconcile later.</summary>
    public static readonly IReadOnlyList<HudChipFamily> DefaultOrder =
        [HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire, HudChipFamily.Buff];

    /// <summary>What the player calls this family in "Edit HUD…" — the only place a family
    /// has ever needed a name of its own, because until SA-4 nothing addressed one.
    ///
    /// **"Mez &amp; slow" says both halves out loud.** <see cref="HudChipFamily.Mez"/> is the
    /// whole fight family and a label reading "Mez" would be a mute switch that silently
    /// takes slow chips too — the tick box that lies, with the switch on the other side.
    /// </summary>
    public static string Label(HudChipFamily family) => family switch
    {
        HudChipFamily.Mez => "Mez & slow",
        HudChipFamily.Spawn => "Spawn timers",
        HudChipFamily.WatchFire => "Watch alerts",
        _ => "Buffs",
    };

    /// <summary>The family's emblem on an Edit-mode chicklet — an <c>IconPaths</c> name, never
    /// a glyph (#148/#166), and the same vector its chips wear so the edit row and the live
    /// row cannot be read as describing different things.
    ///
    /// The fight family draws its MEZ half's Moon: its two halves genuinely wear two icons
    /// (Moon and ChevronsDown) and one of them has to stand for the pair, so the name says
    /// what the emblem cannot.</summary>
    public static string Emblem(HudChipFamily family) => family switch
    {
        HudChipFamily.Mez => "Moon",
        HudChipFamily.Spawn => "Timer",
        HudChipFamily.WatchFire => "Bell",
        _ => "Hourglass",
    };

    /// <summary>Does a due chip in this family replace its countdown with "DUE"?
    /// Spawn does (the camp has popped and the chip has said its piece — click it away);
    /// nothing else does. A mez at 0:04 is still counting toward a wake-up and the last-tick
    /// warning tint is the whole signal; a buff at 0:04 is the same sentence about a recast;
    /// and a watch-fire chip's countdown is its own linger, which is not a deadline the
    /// player acts on at all.</summary>
    public static bool FlipsToDue(HudChipFamily family) => family == HudChipFamily.Spawn;

    /// <summary>Does this family's gauge drain rather than fill? Spawn is the only family
    /// that FILLS — it draws elapsed progress toward a respawn, which is a thing arriving.
    /// Every other family draws the REMAINING share, shrinking, like a buff bar: a mez, a
    /// slow, a warned buff and a lingering alert are all things going away.
    /// <see cref="SpawnChip.Fraction"/> is the elapsed share in every case, so the one
    /// subtraction lives here rather than in four builders.</summary>
    public static bool GaugeDrains(HudChipFamily family) => family != HudChipFamily.Spawn;

    /// <summary>The chicklet's countdown face. The one place the DUE flip is decided, so a
    /// second host of the row cannot answer it differently (trap 58's shape).</summary>
    public static string FaceText(HudChipEntry entry) =>
        entry.Chip.IsDue && FlipsToDue(entry.Family) ? "DUE" : entry.Chip.CountdownText;

    /// <summary>The share of the gauge track to paint, 0..1, or null when this chip has no
    /// gauge at all (no known duration — the track hides rather than lying about progress).
    /// A DUE spawn chip fills solid: the countdown is over, and a bar frozen at 97% under
    /// the word "DUE" is two answers to one question.</summary>
    public static double? GaugeShare(HudChipEntry entry)
    {
        if (entry.Chip.IsDue && !GaugeDrains(entry.Family)) return 1.0;
        if (entry.Chip.Fraction is not { } elapsed) return null;
        return GaugeDrains(entry.Family) ? 1 - elapsed : elapsed;
    }

    /// <summary>
    /// The row: every family's chips, in family order, instance order preserved within each.
    ///
    /// **Instance order is NOT re-sorted across families** — no "soonest first" over the
    /// whole row. Each family already orders its own chips the way its surface always did
    /// (spawn timers soonest-first from <see cref="SpawnsViewModel.Chips"/>, mezzes in
    /// landing order), and a global re-sort would make chips swap places under the cursor
    /// every second, which is a click-to-dismiss surface changing its target mid-click.
    /// </summary>
    /// <param name="mez">The fight family's chips — mez then slow, exactly as the one
    /// window concatenated them. Empty when <see cref="ChipStackPlan.FightStack"/> says the
    /// family is not showing.</param>
    /// <param name="spawn">Spawn countdowns. Empty when
    /// <see cref="ChipStackPlan.SpawnStack"/> says the family is not showing.</param>
    /// <param name="watchFire">Watch rules still lingering after they fired (SA-3).</param>
    /// <param name="buff">Buffs inside their expiry warning window (SA-3).</param>
    /// <param name="order">Family order; <see cref="DefaultOrder"/> when null. A family
    /// missing from a supplied order is DROPPED rather than appended — SA-4's Mute is a
    /// per-family absence, and an order that silently re-adds what mute removed would be
    /// two answers to one question.</param>
    public static List<HudChipEntry> Merge(
        IReadOnlyList<SpawnChip> mez, IReadOnlyList<SpawnChip> spawn,
        IReadOnlyList<SpawnChip>? watchFire = null, IReadOnlyList<SpawnChip>? buff = null,
        IReadOnlyList<HudChipFamily>? order = null)
    {
        var row = new List<HudChipEntry>();
        foreach (var family in order ?? DefaultOrder)
        {
            var chips = family switch
            {
                HudChipFamily.Mez => mez,
                HudChipFamily.Spawn => spawn,
                HudChipFamily.WatchFire => watchFire ?? [],
                HudChipFamily.Buff => buff ?? [],
                _ => [],
            };
            foreach (var chip in chips) row.Add(new HudChipEntry(family, chip));
        }
        return row;
    }

    // ---- PLACE and MUTE (Surface A / SA-4) ----
    //
    // Two settings, two verbs, one reconciliation. `HudChipOrder` says what order the
    // families sit in; `MutedChipFamilies` says which ones are not on the row at all. They
    // are separate keys on purpose: an order that could also REMOVE a family would be two
    // answers to one question, which is the sentence Merge's own doc has carried since SA-2.

    /// <summary>
    /// The player's family order — every family, exactly once, in the order they chose.
    ///
    /// **A family the setting omits is APPENDED in its default position, never dropped.**
    /// <see cref="Merge"/> drops a family missing from the order it is handed, so an omission
    /// reaching it unrepaired would be a permanent, invisible mute nothing could undo: a
    /// hand-edited file, a profile written by a release before a family existed, or a fifth
    /// family added later would all silently lose chips with no switch naming the loss (trap
    /// 20's shape, and #219's mechanism). Muting is the ONLY thing that removes a family, and
    /// it says so in its own key.
    ///
    /// Unknown names are ignored and duplicates collapse to their first appearance, so a
    /// typed-in file cannot produce a row that renders a family twice.
    /// </summary>
    public static IReadOnlyList<HudChipFamily> ResolveOrder(AppSettings settings)
    {
        var order = new List<HudChipFamily>();
        foreach (var name in settings.HudChipOrder)
            if (Enum.TryParse<HudChipFamily>(name, ignoreCase: true, out var family)
                && !order.Contains(family))
                order.Add(family);
        foreach (var family in DefaultOrder)
            if (!order.Contains(family)) order.Add(family);
        return order;
    }

    /// <summary>Is this family muted — off the row, sounds and trackers untouched?</summary>
    public static bool IsMuted(AppSettings settings, HudChipFamily family) =>
        settings.MutedChipFamilies.Any(
            name => Enum.TryParse<HudChipFamily>(name, ignoreCase: true, out var m) && m == family);

    /// <summary>The families that actually reach the row, in the player's order: their order
    /// minus their mutes. This is what <see cref="Build"/> hands <see cref="Merge"/>, and it
    /// is the single place the two settings are combined.</summary>
    public static IReadOnlyList<HudChipFamily> VisibleOrder(AppSettings settings) =>
        [.. ResolveOrder(settings).Where(family => !IsMuted(settings, family))];

    /// <summary>
    /// PLACE: move one family one step left (<paramref name="delta"/> -1) or right (+1).
    ///
    /// Nudge rather than drag — cheap, testable, and reachable with a click on a row that has
    /// no position of its own to drag within. At either end it is a NO-OP that still returns a
    /// list, so the caller has one path rather than a guard at every call site; the button that
    /// would do nothing is disabled rather than silently swallowing the click (trap 17's other
    /// half — a control that looks live and is not).
    ///
    /// Mute is not consulted here. A muted family keeps its place in the order, so unmuting
    /// puts it back where the player left it instead of at the end.
    /// </summary>
    public static List<HudChipFamily> Nudge(
        IReadOnlyList<HudChipFamily> order, HudChipFamily family, int delta)
    {
        var moved = new List<HudChipFamily>(order);
        var from = moved.IndexOf(family);
        var to = from + Math.Sign(delta);
        if (from < 0 || to < 0 || to >= moved.Count) return moved;
        (moved[from], moved[to]) = (moved[to], moved[from]);
        return moved;
    }

    /// <summary>Writes a new family order into the profile. The WRITER half of
    /// <c>AppSettings.HudChipOrder</c>, shipping in the same PR as its reader — the
    /// <c>DeadSettingTests</c> posture, which exists because three player-facing bugs came
    /// from data that survived a move and a write path that did not.</summary>
    public static void SetOrder(AppSettings settings, IReadOnlyList<HudChipFamily> order) =>
        settings.HudChipOrder = [.. order.Select(family => family.ToString())];

    /// <summary>Mutes or unmutes one family. The WRITER for
    /// <c>AppSettings.MutedChipFamilies</c>, for the same reason.</summary>
    public static void SetMuted(AppSettings settings, HudChipFamily family, bool muted)
    {
        settings.MutedChipFamilies.RemoveAll(
            name => Enum.TryParse<HudChipFamily>(name, ignoreCase: true, out var m) && m == family);
        if (muted) settings.MutedChipFamilies.Add(family.ToString());
    }

    /// <summary>A family list as one space-free token for the <c>EQBUDDY_EXPAND</c> dump
    /// ("Spawn,Mez,WatchFire,Buff"), or "-" when there is nothing in it. The dump is
    /// space-separated key=value, so a value with a space in it would silently become two
    /// keys; "-" rather than "" because a key with an empty value cannot be waited on.</summary>
    public static string OrderKey(IEnumerable<HudChipFamily> families) =>
        string.Join(",", families) is { Length: > 0 } key ? key : "-";

    /// <summary>
    /// THE WHOLE ROW FOR ONE TICK: ask every family's gate, ask the families that pass, merge.
    ///
    /// **This came out of `MainWindow.RefreshHudChips` in SA-3, and the reason is coverage.**
    /// SA-2 left "which trackers to ask" in the window on the grounds that it was the window's
    /// own business, and with two families it was — the whole body was two gate calls. With
    /// four it is a decision: four gates, four probes, three settings and a threshold, none of
    /// which the WPF layer can test (docs/TestPlan.md §5). The window keeps what is genuinely
    /// its own — the row window's lifecycle, and whether the World window is showing Camps,
    /// which is a question about a <c>Window</c>.
    ///
    /// Every argument is a Core or UI.Shared type, so this stays framework-free.
    ///
    /// **The emptiness probes are not an optimisation, they are the contract.** Each family is
    /// asked "have you got anything" before its full list is built, so the row does not build
    /// four lists once a second to learn they were empty.
    /// </summary>
    /// <param name="hiddenForFocus">The widget is hidden because the game lost focus. Every
    /// family goes with it — a chip row over someone's browser is the thing focus-hide
    /// exists to prevent.</param>
    /// <param name="worldOnCamps">The World window is up AND showing Camps, so the spawn
    /// family's timers are already on screen there (the Bevel-signed hide-rule).</param>
    public static List<HudChipEntry> Build(
        AppSettings settings, bool hiddenForFocus, bool worldOnCamps,
        SpawnsViewModel spawns, MezTracker mez, SlowTracker slow,
        WatchFireLedger fires, BuffTracker buffs, DateTime now)
    {
        var spawnChips = ChipStackPlan.SpawnStack(settings.TrackSpawns, hiddenForFocus,
            worldOnCamps, spawns.HasActiveTimers(now))
            ? spawns.Chips(now) : [];

        var mezOn = settings.MezChipsEnabled;
        var slowOn = settings.SlowAlertEnabled
            && (!settings.SlowAlertRaidOnly || slow.InRaid(now));
        var fightChips = ChipStackPlan.FightStack(hiddenForFocus,
            mezHasChips: mezOn && mez.Any(now),
            slowHasChips: slowOn && slow.Any(now))
            ? [.. mezOn ? MezChips(mez, now) : [], .. slowOn ? SlowChips(slow, now) : []]
            : new List<SpawnChip>();

        var watchChips = ChipStackPlan.WatchFireStack(hiddenForFocus, fires.Any(now))
            ? WatchChips(fires, now) : [];
        var buffChips = ChipStackPlan.BuffStack(hiddenForFocus, buffs.ActiveCount > 0)
            ? BuffChips(buffs, now, settings.BuffWarnSeconds) : [];

        // PLACE and MUTE, in ONE argument (SA-4). A muted family is absent from this list and
        // Merge drops it — which is the seam SA-2 built the order argument for, rather than a
        // second gate beside the four above. The probes still run for a muted family and that
        // is deliberate: skipping them would be a second answer to "is this family on the
        // row", and they are four cheap emptiness questions.
        return Merge(fightChips, spawnChips, watchChips, buffChips,
            order: VisibleOrder(settings));
    }

    /// <summary>How many chips this family put on the row — the <c>hudChips</c> dump's
    /// per-family counts.</summary>
    public static int CountOf(IReadOnlyList<HudChipEntry> row, HudChipFamily family)
    {
        var n = 0;
        foreach (var entry in row) if (entry.Family == family) n++;
        return n;
    }

    /// <summary>How many chips on the row are showing their DUE face right now.</summary>
    public static int DueCount(IReadOnlyList<HudChipEntry> row)
    {
        var n = 0;
        foreach (var entry in row) if (entry.Chip.IsDue) n++;
        return n;
    }

    /// <summary>What must change before the row is REBUILT rather than ticked in place.
    ///
    /// The two windows each kept their own version of this and each got it subtly wrong
    /// once: the spawn one had to add <c>Zone</c> after a fix, and both needed a non-empty
    /// SENTINEL on dismiss because clearing the last chip makes the new signature ""
    /// too — a matching reset skips the rebuild and leaves a ghost chip painted (PR #67).
    /// One signature, one place, and <see cref="DismissedSignature"/> is the sentinel.
    /// </summary>
    public static string Signature(IReadOnlyList<HudChipEntry> row) =>
        string.Join("", row.Select(e => $"{e.Family}|{e.Chip.Zone}|{e.Chip.Name}|{e.Chip.IsDue}"));

    /// <summary>The "force the next render to rebuild" value. Not "" — see
    /// <see cref="Signature"/>.</summary>
    public const string DismissedSignature = "￿";

    /// <summary>Vertical gap between the widget and the chip row, in the same units the
    /// caller's window geometry uses.</summary>
    public const double HudGap = 4;

    /// <summary>
    /// Where the slaved companion goes, given where the HUD is.
    ///
    /// **This is the DEFAULT and it is still the whole of an untouched profile** (the SA-2
    /// hosting amendment, Helm-signed 2026-09-05). Recomputed from the widget every tick, so
    /// there is no saved x/y to walk up the screen across reopens — which is what trap 2
    /// (#122/#152) was about, and why <c>ChipStackAnchor</c> retires with the two windows.
    ///
    /// **OE-8 adds the other branch and does not change this one.** A player who drags a
    /// companion window parks it (<see cref="ParkedPlacement"/>); until they do, the pair in
    /// the profile is NaN and this method is the only thing that places the window. The one
    /// writer of that pair is the end of the player's drag, so neither the follower nor the
    /// toolkit can reach it — trap 49's three actors, separated by construction rather than
    /// by a <c>selfSet</c> flag.
    ///
    /// Directly under the widget by default, left edges aligned. If the row would hang off
    /// the bottom of the work area it goes ABOVE the widget instead: a chicklet half off
    /// the screen is the same defect as one that never drew.
    ///
    /// **The under-bar PANEL takes its Left from <see cref="AnchoredLeft"/> instead** — it
    /// hangs off one CHIP and the row hangs off the whole bar, which is a difference this
    /// method has no chip to express. Everything else about where that panel goes, the
    /// vertical flip included, is still this arithmetic.
    ///
    /// **One unit space, the caller's, used consistently** (trap 1). WPF hands DIPs
    /// throughout — <c>Window.Left/Top/ActualHeight</c> and <c>SystemParameters.WorkArea</c>
    /// agree there — and nothing under the widget's UI-scale transform is involved: this
    /// positions a WINDOW, not a control inside one.
    ///
    /// **No horizontal clamp, on purpose**, for the reason
    /// <see cref="WidgetMetrics.RightAnchoredLeft"/> gives: a negative Left is legitimate on
    /// a multi-monitor desk and clamping against the primary monitor's area would yank a
    /// secondary-monitor row away from the widget it is slaved to. The vertical flip uses
    /// the work area the CALLER passes, which is the widget's own monitor.
    ///
    /// A height that is not real yet — 0 on the first layout pass, NaN — takes the space
    /// below the widget without a flip: "we cannot tell yet" and "draw where you always
    /// draw" are the same instruction.
    ///
    /// **<paramref name="growUp"/> (#425) flips WHICH END OF THE STACK touches the widget,
    /// and it is the whole of the owner's "toggle grow up or down".** The stack is a column
    /// now, so a direction is a thing it can have; a horizontal row's was always "right".
    /// Down — the default and today's app — pins the column's TOP under the widget, so a
    /// chicklet arriving pushes the column further down the screen. Up pins its BOTTOM just
    /// above the widget, so an arriving chicklet moves the top edge upward and the row stays
    /// welded to the widget. That is v1's "boss timers above mez timers, each growing away
    /// from the other" applied to the one row SA-2 left.
    ///
    /// **Both directions FALL BACK to below rather than half off the screen**, which is the
    /// same rule the flip has always had in the other direction: a chicklet the monitor
    /// cannot show is the same defect as one that never drew. Below is the fallback in both
    /// cases because below is the direction that has the widget's own space to give.
    ///
    /// **<paramref name="hudHeight"/> is the widget AND whatever hangs under it** (the
    /// under-bar panel's <c>SlavedOccupiedHeight</c>) and that is exactly why the up branch
    /// measures from <paramref name="hudTop"/> instead: the panel hangs BELOW the widget, so
    /// it is not in the space above it — but the moment the up branch falls back it is, and
    /// the fallback uses the same <c>below</c> every other path does. One arithmetic knows
    /// both, which is the seam #425's §3 named.
    /// </summary>
    public static (double Left, double Top) Placement(
        double hudLeft, double hudTop, double hudHeight, double rowHeight,
        double workAreaTop, double workAreaBottom, bool growUp = false)
    {
        var below = hudTop + Math.Max(0, Real(hudHeight)) + HudGap;
        if (!double.IsFinite(rowHeight) || rowHeight <= 0) return (hudLeft, below);
        var above = hudTop - HudGap - rowHeight;
        if (growUp) return (hudLeft, above >= workAreaTop ? above : below);
        if (below + rowHeight <= workAreaBottom) return (hudLeft, below);
        return (hudLeft, above >= workAreaTop ? above : below);
    }

    private static double Real(double v) => double.IsFinite(v) ? v : 0;

    /// <summary>
    /// WHERE THE UNDER-BAR PANEL'S LEFT EDGE GOES — under the CHIP that was hovered, not
    /// under the bar.
    ///
    /// **This is a bug fix, not a preference** (owner repro, 2026-09-07 ~3:50 PM CT: *"the
    /// peek/expand panel always docks under the leftmost tray chip, not under the chip
    /// actually hovered"*). <see cref="Placement"/> answers with <c>hudLeft</c> — the
    /// WIDGET's left edge — which was exact while the panel was the only thing hanging off
    /// the bar and became wrong the moment every tray cell grew a peek (OE-9): twelve chips
    /// spread across the bar, one panel, and it opened under the first of them every time.
    /// The vertical half of the question is unchanged and still <see cref="Placement"/>'s;
    /// only the horizontal one moved, because only the horizontal one has a chip in it.
    ///
    /// **<paramref name="chipOffsetX"/> is the chip's own offset from the widget's left edge,
    /// in the caller's units** (DIPs — the caller reads it through a visual transform, so the
    /// widget's UI-scale <c>LayoutTransform</c> is already accounted for and no pixel
    /// arithmetic happens here or there; trap 1). A value that is not real yet — no chip for
    /// this target, a bar that has not laid out, a target reached by the
    /// <c>EQBUDDY_HUDEXPAND</c> hook before the bar drew — answers <paramref name="hudLeft"/>,
    /// which is exactly what shipped before this change: "we cannot tell yet" and "draw where
    /// you always drew" are the same instruction, the same way an unmeasured height does not
    /// flip in <see cref="Placement"/>.
    ///
    /// **The clamp keeps a right-hand chip's panel on the monitor, and REFUSES to act when
    /// the anchor is not on the area it was handed.** A panel hanging off the right edge is
    /// the same defect as a chicklet half off the screen; but <see cref="Placement"/> declines
    /// to clamp horizontally at all, on purpose, because a negative Left is legitimate on a
    /// multi-monitor desk and the primary monitor's area would yank a secondary-monitor panel
    /// away from the bar it belongs to. Both facts survive here: the caller passes the
    /// WIDGET's own monitor (<c>ScreenGuard.WorkAreaAt</c>), and an anchor outside the area it
    /// was given is taken as evidence that the area is not this window's monitor — so it is
    /// left alone rather than dragged onto one.
    /// </summary>
    /// <param name="hudLeft">The widget's left edge — the answer when there is no chip.</param>
    /// <param name="chipOffsetX">The hovered chip's offset from that edge, or NaN.</param>
    /// <param name="panelWidth">The panel's drawn width, for the right-edge clamp.</param>
    /// <param name="areaLeft">Work-area left of the monitor the widget is on.</param>
    /// <param name="areaRight">…and its right.</param>
    public static double AnchoredLeft(
        double hudLeft, double chipOffsetX, double panelWidth,
        double areaLeft, double areaRight)
    {
        if (!double.IsFinite(chipOffsetX) || chipOffsetX <= 0) return hudLeft;
        var anchor = hudLeft + chipOffsetX;
        if (!double.IsFinite(panelWidth) || panelWidth <= 0) return anchor;
        if (!double.IsFinite(areaLeft) || !double.IsFinite(areaRight)) return anchor;
        if (areaRight - areaLeft < panelWidth) return anchor;
        // Not on the area we were handed: that area is some other monitor, and clamping to it
        // would tear the panel off the chip it is anchored to.
        if (anchor < areaLeft || anchor > areaRight) return anchor;
        return Math.Clamp(anchor, areaLeft, areaRight - panelWidth);
    }

    // ---- FREE PLACEMENT (OE-8): NaN means SLAVED, a finite pair means PARKED ----
    //
    // Everything below is the OTHER BRANCH of the question Placement above answers —
    // "where does this companion window go this tick" — and it lives beside it for that
    // reason rather than in a file of its own. Two homes for one question is trap 4's shape,
    // and the parked branch has to agree with the slaved one about units (DIPs, the caller's,
    // trap 1) and about the fact that neither of them may touch a setting.
    //
    // **The default IS today's app.** An untouched profile carries NaN, answers
    // HudParkMode.Slaved, and runs Placement byte-for-byte — the Helm-signed SA-2 behaviour,
    // with no migration step and nothing for trap 55's class of bug to chew.

    /// <summary>
    /// Which rule is placing a companion window — THREE states, not two, and the third is
    /// the one that matters (trap 20's shape: the thing you are looking for is what is not
    /// there).
    ///
    /// <list type="bullet">
    /// <item><see cref="Slaved"/> — the profile holds no park. Recomputed from the widget
    /// every tick, which is the shipped SA-2 behaviour and the default.</item>
    /// <item><see cref="Parked"/> — the player dropped it somewhere and that point is
    /// reachable. Screen-ABSOLUTE: the follower actor retires for this window.</item>
    /// <item><see cref="Unreachable"/> — the profile holds a park the desk cannot show right
    /// now (a detached monitor, an RDP hop, a resolution change). The window runs SLAVED FOR
    /// THE SESSION and **the setting survives untouched**, so the monitors coming back bring
    /// the park back — #117's rule, reused rather than reinvented. Reporting it as a third
    /// state is what lets a test say "it ran slaved AND the point is still in the profile",
    /// which is the whole assertion.</item>
    /// </list>
    /// </summary>
    public enum HudParkMode
    {
        /// <summary>No park in the profile: follow the widget, as SA-2 shipped.</summary>
        Slaved,
        /// <summary>Parked at a reachable point: screen-absolute, the follower retires.</summary>
        Parked,
        /// <summary>Parked at a point this desk cannot show: slaved for the session, setting
        /// kept.</summary>
        Unreachable,
    }

    /// <summary>Is this settings pair a park at all? A pair is parked only when BOTH halves
    /// are finite — a half-written pair (a hand-edited file, an interrupted write) is not a
    /// position, and treating it as one would put a window at NaN, which WPF renders
    /// nowhere.</summary>
    public static bool IsParked(double left, double top) =>
        double.IsFinite(left) && double.IsFinite(top);

    /// <summary>
    /// THE RESTORE DECISION, and it is a sum so it can be asserted without a window (the
    /// standing move — the WPF layer has no unit tests, docs/TestPlan.md §5).
    ///
    /// <paramref name="reachable"/> is the caller's <c>ScreenGuard.OnScreen</c> answer —
    /// <c>WindowPlacement.IsReachable</c>'s 40px grab area against the VIRTUAL screen, all
    /// monitors. It is asked of the caller rather than computed here because this project is
    /// framework-free and <c>SystemParameters</c> is not.
    /// </summary>
    public static HudParkMode ParkMode(double savedLeft, double savedTop, bool reachable) =>
        !IsParked(savedLeft, savedTop) ? HudParkMode.Slaved
        : reachable ? HudParkMode.Parked
        : HudParkMode.Unreachable;

    /// <summary>
    /// Where a PARKED companion window sits, given the corner the player dropped it at and
    /// the work area of THE MONITOR THAT POINT IS ON (§2.2's named implement check — the
    /// primary monitor's area would yank a secondary-monitor park, which is the same reason
    /// <see cref="WidgetMetrics.RightAnchoredLeft"/> refuses to clamp at all).
    ///
    /// **The pair pins the ANCHORED CORNER and growth runs away from it.** A chicklet
    /// arriving makes the toolkit widen a <c>SizeToContent</c> window; the anchor does not
    /// move, so the row grows right and down from where it was put. That corner is exactly
    /// where #122/#152 lived — a self-moving window rewriting its own anchor — and here the
    /// anchor has ONE writer (drag end) and the toolkit cannot reach it.
    ///
    /// **It CLAMPS rather than flips, and the difference is the point.** The slaved rule
    /// flips above the widget because the widget is occupying the space below; a parked
    /// window has nothing to avoid, so the honest rule is "stay where you were put until the
    /// monitor's edge stops you". A flip would teleport a window a full width away from a
    /// corner the player deliberately chose.
    ///
    /// A size that is not real yet — 0 on the first layout pass, NaN — answers "draw at the
    /// anchor": "we cannot tell yet" and "draw where you were put" are the same instruction,
    /// exactly as they are in <see cref="Placement"/>.
    /// </summary>
    public static (double Left, double Top) ParkedPlacement(
        double parkLeft, double parkTop, double width, double height,
        double areaLeft, double areaTop, double areaRight, double areaBottom) =>
        (Fit(parkLeft, width, areaLeft, areaRight), Fit(parkTop, height, areaTop, areaBottom));

    private static double Fit(double anchor, double extent, double min, double max)
    {
        if (!double.IsFinite(anchor)) return anchor;
        if (!double.IsFinite(extent) || extent <= 0) return anchor;
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min) return anchor;
        // Bigger than the monitor it is parked on: the leading edge wins, because the corner
        // the player can still grab is the one they parked at.
        if (max - min < extent) return min;
        return Math.Clamp(anchor, min, max - extent);
    }

    /// <summary>
    /// The <c>MaxWidth</c> that makes the row's <c>WrapPanel</c> WRAP instead of growing a
    /// window wider than the screen (trap 25 — a strip whose contents are not fixed-width
    /// belongs in a WrapPanel, and a WrapPanel with no cap never reaches one).
    ///
    /// One function so the slaved and parked paths cannot answer it differently: the slaved
    /// row passes the widget's monitor, the parked row passes the parked point's, and both
    /// get the same arithmetic. The floor exists because a work area measured as zero (a
    /// half-initialised host, a headless run) would otherwise cap the row at nothing.
    /// </summary>
    public static double WrapWidth(double areaWidth) =>
        double.IsFinite(areaWidth) ? Math.Max(MinWrapWidth, areaWidth) : MinWrapWidth;

    /// <summary>The narrowest the row is ever capped at — one chicklet's worth. It was an
    /// inline 120 in <c>HudChipRowWindow.Park</c> before OE-8 gave the cap two callers.
    /// </summary>
    public const double MinWrapWidth = 120;

    /// <summary>
    /// The <c>MaxHeight</c> that makes the VERTICAL stack wrap into a second column instead
    /// of growing a window taller than the screen (#425) — the exact mirror of
    /// <see cref="WrapWidth"/>, and mirrored rather than re-derived so the two axes cannot
    /// answer trap 25 differently.
    ///
    /// The stack is a <c>WrapPanel</c> and not a vertical <c>StackPanel</c> for the reason
    /// traps 14 and 25 already state: a stack measures INFINITE in the stacking direction, so
    /// a long list of countdowns would run off the bottom of the monitor with no ellipsis and
    /// no overflow — correct, and not on screen. A wrap without a cap never reaches one.
    /// </summary>
    public static double WrapHeight(double areaHeight) =>
        double.IsFinite(areaHeight) ? Math.Max(MinWrapHeight, areaHeight) : MinWrapHeight;

    /// <summary>The shortest the stack is ever capped at, and it is the same 120 as
    /// <see cref="MinWrapWidth"/> on purpose: the floor exists for a work area measured as
    /// zero (a half-initialised host, a headless run), and a floor of one chicklet's HEIGHT
    /// would put every chicklet in its own column — the horizontal row wearing a vertical
    /// panel's clothes, which is the one outcome this change is not allowed to produce.
    /// </summary>
    public const double MinWrapHeight = 120;

    /// <summary>
    /// What the Edit-HUD toggle says about <c>AppSettings.HudChipRowGrowUp</c>, and the ONE
    /// place this direction is put into words.
    ///
    /// **It reads the STATE rather than commanding an action** — "Stack grows: Down", not
    /// "Grow up" — the same way the mute tick says "muted" rather than "mute", so a player
    /// who never clicks it can still read what their row is doing.
    ///
    /// **And it never says a bare "grow down".** <c>HudExpandWindow.Reveal</c> owns an
    /// unrelated, owner-locked "grow down" (the peek panel's reveal animation) and a player
    /// meeting the same two words on two surfaces will reasonably assume one control governs
    /// both. The word "Stack" is what keeps them apart, so it is in the string rather than
    /// in a tooltip beside it.
    /// </summary>
    public static string GrowLabel(bool growUp) => growUp ? "Stack grows: Up" : "Stack grows: Down";

    /// <summary>The direction as the <c>EQBUDDY_EXPAND</c> dump reports it. Space-free, like
    /// every other value on a space-separated key=value line.</summary>
    public static string GrowKey(bool growUp) => growUp ? "up" : "down";

    /// <summary>
    /// A companion window's park as the <c>EQBUDDY_EXPAND</c> dump reports it: "slaved", or
    /// "left,top" rounded to whole units.
    ///
    /// **The dump carries this twice per window — the EFFECT and the SETTING — because "in
    /// the profile" and "on the screen" are different claims (trap 42), and OE-8's whole
    /// unreachable rule is a disagreement between them.** No space in the value: the dump is
    /// space-separated key=value, so a value with a space in it would silently become two
    /// keys.
    /// </summary>
    public static string ParkKey(double left, double top) =>
        IsParked(left, top) ? $"{Math.Round(left)},{Math.Round(top)}" : "slaved";

    /// <summary>
    /// The under-bar panel's width after a player has taken one (OE-1b lock 3), or the
    /// shipped single width when they have not.
    ///
    /// **NaN means "the OE-7 width", the same way a NaN park means "slaved"** — one sentinel
    /// convention for the whole feature, so a reset profile gets today's app by construction
    /// rather than by a migration. A taken width is clamped to the monitor it is being drawn
    /// on and to a floor a header can still render in; OE-7's one-width rule is untouched by
    /// this, because that rule was about CONTENT-driven wobble on a tick and this width only
    /// ever changes when a player drags an edge (trap 12 permits exactly that).
    /// </summary>
    public static double PanelWidth(double savedWidth, double defaultWidth, double areaWidth)
    {
        if (!double.IsFinite(savedWidth) || savedWidth <= 0) return defaultWidth;
        var ceiling = double.IsFinite(areaWidth) && areaWidth > MinPanelWidth
            ? areaWidth : Math.Max(MinPanelWidth, defaultWidth);
        return Math.Clamp(savedWidth, MinPanelWidth, ceiling);
    }

    /// <summary>The narrowest the under-bar panel may be dragged to. Below this the header's
    /// icon, title and two buttons stop being a header and start being an ellipsis.</summary>
    public const double MinPanelWidth = 180;

    /// <summary>
    /// An edge drag turned into a panel width. The cursor travels in SCREEN units while the
    /// panel's chrome lives under <c>ChipScale</c>'s <c>LayoutTransform</c>, so the delta is
    /// divided rather than added raw — trap 1, and the same shape as
    /// <see cref="WidgetMetrics.ContentHeightFromDrag"/>, which is the precedent this
    /// deliberately copies.
    /// </summary>
    /// <param name="grip">+1 when the RIGHT edge is being dragged (moving right widens),
    /// -1 for the LEFT edge (moving right narrows). The window's own Left is moved by the
    /// caller on a left-edge drag, which is what makes that edge feel anchored.</param>
    public static double PanelWidthFromDrag(
        double startWidth, double cursorDelta, double chipScale, int grip) =>
        Math.Max(MinPanelWidth,
            startWidth + grip * cursorDelta / WidgetMetrics.SafeScale(chipScale));

    /// <summary>
    /// The fight family's mez half: who is asleep and the wake-up countdown ("?" until the
    /// spell's duration is known), warning tint inside the last tick. Same-named entries are
    /// numbered — "orc pawn (2)" — since the log cannot tell the creatures apart (#32 asked
    /// for separate timers rather than one merged chip).
    ///
    /// Lifted out of <c>MainWindow</c> in SA-2 with the row: the WPF layer has no unit tests
    /// and this numbering had none either, so it lived where nothing could assert it.
    /// </summary>
    public static List<SpawnChip> MezChips(MezTracker tracker, DateTime now)
    {
        var states = tracker.Snapshot(now);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return states.Select(m =>
        {
            var n = seen[m.Target] = seen.GetValueOrDefault(m.Target) + 1;
            var dupe = states.Count(x => x.Target.Equals(m.Target, StringComparison.OrdinalIgnoreCase)) > 1;
            var remaining = m.RemainingSeconds(now);
            var text = remaining is { } r
                ? $"{(int)r / 60}:{(int)r % 60:00}"
                : "?";
            return new SpawnChip(
                Zone: "", Name: dupe ? $"{m.Target} ({n})" : m.Target, CountdownText: text,
                IsDue: remaining is <= 6,
                Detail: $"{m.Spell} by {m.Caster} · landed {m.LandedAt:h:mm:ss tt}",
                Icon: "Moon")
            {
                // Elapsed share for the gauge; the fight family DRAINS it (see
                // GaugeDrains), so the 1 - x lives in one place rather than in a renderer.
                Fraction = m.ExpiresAt is { } exp && (exp - m.LandedAt).TotalSeconds is > 0 and var dur
                    ? Math.Clamp((now - m.LandedAt).TotalSeconds / dur, 0, 1)
                    : null,
            };
        }).ToList();
    }

    /// <summary>The fight family's slow half (#94): the debuff's honest % (a range when
    /// several slows share the landing line), time left when the wiki documents a duration,
    /// and the cure line in the tooltip — "how do I get rid of this" attached to the alert.
    /// Lifted with <see cref="MezChips"/>, for the same reason.</summary>
    public static List<SpawnChip> SlowChips(SlowTracker tracker, DateTime now) =>
        tracker.Snapshot(now).Select(s =>
        {
            var remaining = s.RemainingSeconds(now);
            var detail = string.Join(" · ", new[]
            {
                s.Spells.Length == 1 ? s.Spells[0] : "One of: " + string.Join(", ", s.Spells),
                s.CounterText,
                tracker.CureLine(s),
                $"landed {s.LandedAt:h:mm:ss tt}",
            }.Where(part => part.Length > 0));
            return new SpawnChip(
                Zone: "", Name: SlowChipText.Label(s),
                CountdownText: remaining is { } r ? $"{(int)r / 60}:{(int)r % 60:00}" : "?",
                IsDue: false, Detail: detail + " · right-click to dismiss", Icon: "ChevronsDown")
            {
                Fraction = s.ExpiresAt is { } exp && (exp - s.LandedAt).TotalSeconds is > 0 and var dur
                    ? Math.Clamp((now - s.LandedAt).TotalSeconds / dur, 0, 1)
                    : null,
                OnDismiss = () => tracker.Dismiss(s.Message),
            };
        }).ToList();

    /// <summary>
    /// The watch-fire family (SA-3): one chicklet per rule that has fired and is still inside
    /// its <see cref="WatchFireLedger.Linger"/>.
    ///
    /// **The face is the rule's NAME and the countdown is the chip's own linger** — not the
    /// match. A Text rule's label is a trimmed log line of up to eighty characters, and the
    /// chicklet's name column trims at 180px with no ellipsis budget to spare, so putting the
    /// match on the face would clip the one thing it was carrying. The label goes in the
    /// tooltip, where it has room, beside the time the rule fired.
    ///
    /// **The countdown is honest about what it counts.** It is the linger, not a deadline the
    /// player acts on — which is why this family does not flip to "DUE" (there is no moment)
    /// and why the gauge drains: the chicklet is visibly on its way out, so a player can tell
    /// "this is about to stop reminding me" from "this is about to happen".
    ///
    /// <c>Bell</c>, not the Watch card's <c>Target</c>: on this row the icon says what kind of
    /// EVENT a chicklet is, the way <c>Moon</c> and <c>Timer</c> do, and the card's icon
    /// belongs to a different object (B3 §3 — breakouts and cards are not HUD chips). Reusing
    /// <c>Timer</c> would have made a watch chip and a spawn chip the same shape at a glance,
    /// which is #148/#166's three-identical-boxes failure with vectors instead of emoji.
    /// </summary>
    public static List<SpawnChip> WatchChips(WatchFireLedger ledger, DateTime now) =>
        ledger.Snapshot(now).Select(f =>
        {
            var left = WatchFireLedger.Remaining(f, now);
            return new SpawnChip(
                Zone: "", Name: f.RuleName,
                CountdownText: $"{(int)left / 60}:{(int)left % 60:00}",
                IsDue: false,
                Detail: $"{f.Label}\nfired {f.FiredAt:h:mm:ss tt}",
                Icon: "Bell")
            {
                Fraction = WatchFireLedger.Spent(f, now),
                OnDismiss = () => ledger.Dismiss(f.RuleId),
            };
        }).ToList();

    /// <summary>
    /// The buff-expiring family (SA-3): every buff believed active on you whose countdown has
    /// come inside <paramref name="warnSeconds"/>, soonest-fading first — the order
    /// <see cref="BuffTracker.Snapshot"/> already hands over.
    ///
    /// **The threshold is <c>AppSettings.BuffWarnSeconds</c>, not a new constant.** SA-3 ships
    /// no settings surface, and the player already answered "when does a buff become urgent"
    /// for the Buffs card — asking them again in a second place, or answering it differently
    /// here, is one fact with two sources (trap 4). <see cref="BuffWarnWindow"/> is that one
    /// source, floor included.
    ///
    /// **A buff with no known duration gets no chicklet.** <see cref="BuffState.ExpiresAt"/> is
    /// null when the landing could not be attributed at all, and a deadline chip with no
    /// deadline is a chip that can never leave.
    ///
    /// **"est" rides on the face, exactly as it does on the card.** A wiki-base duration is a
    /// floor — ranks and AAs lengthen buffs — and a chicklet reading a bare "0:45" for a
    /// number that might be two minutes out is the chip claiming a precision the tracker does
    /// not have. It costs four characters and it is the same word the card uses, so the two
    /// surfaces cannot be read as disagreeing.
    ///
    /// **The gauge is the share of the WARNING WINDOW left, not of the buff.** A 27-minute
    /// Clarity that only earns a chicklet for its last minute would otherwise draw a bar
    /// frozen at 99% for the whole of that chicklet's life — technically the elapsed share of
    /// the spell, and useless. Measured against the window the chip exists inside, the gauge
    /// empties as the chip's own reason to be there does. (<see cref="SpawnChip.Fraction"/>
    /// stays the ELAPSED share, as it is for every other family;
    /// <see cref="GaugeDrains"/> does the subtraction.)
    ///
    /// **Not dismissible**, following the mez precedent: it clears itself when the buff fades
    /// or is recast, and a per-instance dismissal would need state that outlives a re-landing
    /// to mean anything. SA-4's per-family Mute is the answer to "I never want these".
    /// </summary>
    public static List<SpawnChip> BuffChips(BuffTracker tracker, DateTime now, double warnSeconds)
    {
        var warn = BuffWarnWindow(warnSeconds);
        return tracker.Snapshot(now)
            .Where(b => b.RemainingSeconds(now) is { } r && r <= warn)
            .Select(b =>
            {
                var left = b.RemainingSeconds(now) ?? 0;
                return new SpawnChip(
                    Zone: "", Name: b.Label,
                    // The face and the hover are BuffRosterPresentation's, not this file's:
                    // the Buffs card draws the same buff and used to spell both strings out
                    // itself, which is one fact with two sources (trap 4) waiting for one of
                    // them to be edited.
                    CountdownText: BuffRosterPresentation.Clock(left, b.Estimated),
                    // The last server tick, the same window a mez chip takes its warning
                    // tint in. BuffTracker.ServerTickSeconds is where six comes from.
                    IsDue: left <= BuffTracker.ServerTickSeconds,
                    Detail: BuffRosterPresentation.Detail(b),
                    Icon: "Hourglass")
                {
                    Fraction = Math.Clamp(1 - left / warn, 0, 1),
                };
            }).ToList();
    }

    /// <summary>The buff-expiring family's T-minus threshold, in seconds: the player's own
    /// <c>AppSettings.BuffWarnSeconds</c> with the same ten-second floor the Buffs card has
    /// always applied. One place, because the card and the chip must not be able to disagree
    /// about when a buff has become urgent.</summary>
    public static double BuffWarnWindow(double warnSeconds) => Math.Max(10, warnSeconds);
}
