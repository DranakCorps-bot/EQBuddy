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
    /// the setting arrives without reshaping this file.</summary>
    public static readonly IReadOnlyList<HudChipFamily> DefaultOrder =
        [HudChipFamily.Mez, HudChipFamily.Spawn];

    /// <summary>Does a due chip in this family replace its countdown with "DUE"?
    /// Spawn does (the camp has popped and the chip has said its piece — click it away);
    /// the fight family does not (a mez at 0:04 is still counting toward a wake-up, and
    /// the last-tick warning tint is the whole signal).</summary>
    public static bool FlipsToDue(HudChipFamily family) => family == HudChipFamily.Spawn;

    /// <summary>Does this family's gauge drain rather than fill? The fight family draws the
    /// REMAINING share, shrinking, like a buff bar; spawn draws elapsed progress toward the
    /// respawn. <see cref="SpawnChip.Fraction"/> is the elapsed share in both cases.</summary>
    public static bool GaugeDrains(HudChipFamily family) => family == HudChipFamily.Mez;

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
    /// <param name="order">Family order; <see cref="DefaultOrder"/> when null. A family
    /// missing from a supplied order is DROPPED rather than appended — SA-4's Mute is a
    /// per-family absence, and an order that silently re-adds what mute removed would be
    /// two answers to one question.</param>
    public static List<HudChipEntry> Merge(
        IReadOnlyList<SpawnChip> mez, IReadOnlyList<SpawnChip> spawn,
        IReadOnlyList<HudChipFamily>? order = null)
    {
        var row = new List<HudChipEntry>();
        foreach (var family in order ?? DefaultOrder)
        {
            var chips = family switch
            {
                HudChipFamily.Mez => mez,
                HudChipFamily.Spawn => spawn,
                _ => [],
            };
            foreach (var chip in chips) row.Add(new HudChipEntry(family, chip));
        }
        return row;
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
    /// **The row has NO geometry of its own and nothing is persisted** (the SA-2 hosting
    /// amendment, Helm-signed 2026-09-05). It is recomputed from the widget every tick, so
    /// there is no saved x/y to walk up the screen across reopens — which is what trap 2
    /// (#122/#152) was about, and why <c>ChipStackAnchor</c> retires with the two windows.
    ///
    /// Directly under the widget by default, left edges aligned. If the row would hang off
    /// the bottom of the work area it goes ABOVE the widget instead: a chicklet half off
    /// the screen is the same defect as one that never drew.
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
    /// </summary>
    public static (double Left, double Top) Placement(
        double hudLeft, double hudTop, double hudHeight, double rowHeight,
        double workAreaTop, double workAreaBottom)
    {
        var below = hudTop + Math.Max(0, Real(hudHeight)) + HudGap;
        if (!double.IsFinite(rowHeight) || rowHeight <= 0) return (hudLeft, below);
        if (below + rowHeight <= workAreaBottom) return (hudLeft, below);
        var above = hudTop - HudGap - rowHeight;
        return (hudLeft, above >= workAreaTop ? above : below);
    }

    private static double Real(double v) => double.IsFinite(v) ? v : 0;

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
}
