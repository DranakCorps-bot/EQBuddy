using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// THE BUFF ROSTER — every buff believed active on you, as chips (OE-4, Bevel item 1,
/// Helm-signed #347).
///
/// **What changed and why.** The roster was one full-width row per buff (name left, clock
/// right), which the owner read as "needs to be far more compact". The row was already as
/// tight as a row gets — 1px margin, no icon — so the lever was never margin, it was
/// SHAPE: N buffs now fill the card's width before they grow its height.
///
/// **Same chips as the HUD row, not a second chip style.** These are
/// <see cref="HudChipEntry"/> values in <see cref="HudChipFamily.Buff"/>, drawn by the same
/// renderer the deadline row uses, so the two buff surfaces cannot drift into two visual
/// languages. What they do NOT share is the warn-window GATE: the HUD row shows a buff only
/// once it is expiring (a deadline with an action), and this is the full roster (a list you
/// look away for). One family, two audiences — the filter lives in <see cref="Shown"/>.
///
/// **The gauge measures a different thing here, on purpose.**
/// <see cref="HudChipRow.BuffChips"/> draws the share of the WARNING WINDOW left, because a
/// 27-minute Clarity that only earns a chicklet for its last minute would otherwise draw a
/// bar frozen at 99% for the whole of that chicklet's life. A roster chip is on screen for
/// the buff's whole life, so its bar is the share of the BUFF left — an actual buff bar,
/// which is the only reading that means anything across a full list.
///
/// Framework-free like everything in this project: the WPF layer has no unit tests
/// (docs/TestPlan.md §5), so the decisions live here where they can be asserted with no
/// window.
/// </summary>
public static class BuffRosterPresentation
{
    /// <summary>How wide a chip's NAME may get before it trims, in pre-scale units.
    ///
    /// Smaller than the HUD row's 180 and that is the entire density argument: the row is
    /// hosted by a window that is as wide as its chips, and the card is hosted by a widget
    /// that is 320 units wide, full stop. At 180 a single "Riftwind's Protection" would eat
    /// a whole line and the wrap would buy nothing over the rows it replaced. The full name
    /// is never lost — it is the first thing in <see cref="Detail"/>, which is the chip's
    /// hover.</summary>
    public const double NameMaxWidth = 104;

    /// <summary>Which buffs the card draws this tick, and how many it is staying quiet
    /// about.
    ///
    /// Expiring-only mode (David) keeps the card silent until a buff is inside the warning
    /// window — "tell me when it matters", with the rest counted honestly rather than
    /// dropped. <paramref name="quiet"/> is that count, and the empty line says it out loud
    /// (see <see cref="EmptyLine"/>): a list that hides rows and looks complete is a silent
    /// no-op with the switch on the other side.</summary>
    public static List<BuffState> Shown(
        IReadOnlyList<BuffState> active, DateTime now, bool expiringOnly, double warnSeconds,
        out int quiet)
    {
        if (!expiringOnly || active.Count == 0)
        {
            quiet = 0;
            return [.. active];
        }
        var warn = HudChipRow.BuffWarnWindow(warnSeconds);
        var urgent = active.Where(b => b.RemainingSeconds(now) is { } r && r <= warn).ToList();
        quiet = active.Count - urgent.Count;
        return urgent;
    }

    /// <summary>The countdown face: "28:50", "0:00", or "?" when the landing could not be
    /// attributed to anything with a duration — with " est" appended when the caller's
    /// surface has room to say so.
    ///
    /// One formatter for both buff surfaces, so a card and a chicklet cannot disagree about
    /// the digits. Which of them spends four characters on the estimate marker is
    /// <see cref="RosterFace"/>'s decision on one side and
    /// <see cref="HudChipRow.BuffChips"/>'s on the other.</summary>
    public static string Clock(double? remaining, bool estimated) => remaining is { } r
        ? $"{(int)r / 60}:{(int)r % 60:00}{(estimated ? " est" : "")}"
        : "?";

    /// <summary>
    /// The ROSTER chip's face — the countdown, and NOT the "est" marker.
    ///
    /// **This is the one thing Bevel's item flagged as unanswerable from source, and it was
    /// measured rather than assumed** (`docs/screenshots/buffs-card.png`, shot twice on
    /// 2026-09-07 against the same eight staged buffs): with " est" on every face the roster
    /// wraps to FIVE rows, without it to FOUR. The suffix is ~22 of the card's ~306 usable
    /// units, and at five rows the wrap is roughly a wash against the eight full-width rows
    /// it replaced — so on this surface the marker is not a four-character cost, it is most
    /// of the density the change exists to deliver.
    ///
    /// **And it marks nothing here.** <see cref="BuffTracker"/> sets
    /// <see cref="BuffState.Estimated"/> on every landing whose duration has not been taught
    /// by a natural fade, which is nearly all of them — so a full roster is a column of
    /// identical suffixes, spending width on a word that does not tell one chip from
    /// another. The claim itself survives where it has room and where it is read one buff at
    /// a time: <see cref="Detail"/> spells the whole sentence out on hover.
    ///
    /// **The HUD chicklet keeps it on the face, deliberately.** There it appears one or two
    /// at a time, in a window that measures to its own contents, at the moment the precision
    /// claim matters most — you are deciding whether to recast. Same fact, two hosts, and
    /// only one of them is short of room.
    /// </summary>
    public static string RosterFace(BuffState b, DateTime now) =>
        Clock(b.RemainingSeconds(now), estimated: false);

    /// <summary>The chip's hover — the full name first (the face may have trimmed it), then
    /// what is known about where this buff came from, and on the roster the estimate claim
    /// the face no longer spends four characters on (see <see cref="RosterFace"/>).
    ///
    /// ONE producer, read by the card AND by <see cref="HudChipRow.BuffChips"/>: the two
    /// surfaces described the same buff in two hand-written strings, which is trap 4 waiting
    /// for one of them to be edited.</summary>
    public static string Detail(BuffState b) => string.Join(" · ", new[]
    {
        b.Candidates.Length > 1 ? "One of: " + string.Join(", ", b.Candidates) : b.Label,
        b.Caster.Length > 0 ? $"cast by {b.Caster}" : "",
        $"landed {b.LandedAt:h:mm:ss tt}",
        // "catalog length", not "wiki base": for a ranked spell somebody has MEASURED
        // (RankedBuffDurationLedger) the number is no longer the wiki's, and a tooltip that
        // names its own source wrongly is the kind of small lie that outlives the code it
        // described. What "est" still means is unchanged — this is a shipped length plus
        // your own SCR, not a length your log has timed.
        b.Estimated ? "est = catalog length; a natural fade teaches your real duration" : "",
    }.Where(part => part.Length > 0));

    /// <summary>Is this buff urgent — warn ink on the countdown and a warn border on the
    /// chip?
    ///
    /// **The player's own <c>BuffWarnSeconds</c>, through the one window function**, not the
    /// hard-coded 60 the rows used to tint at. The player already answered "when does a buff
    /// become urgent" once; a card answering it differently from the chip and from
    /// expiring-only mode is one fact with three sources. The default is 60, so a profile
    /// that never touched the setting sees exactly what it saw before.</summary>
    public static bool IsUrgent(BuffState b, DateTime now, double warnSeconds) =>
        b.RemainingSeconds(now) is { } r && r <= HudChipRow.BuffWarnWindow(warnSeconds);

    /// <summary>The ELAPSED share of this buff's own duration, 0..1, or null when nothing is
    /// known (the chip's track hides rather than lying about progress).
    /// <see cref="HudChipRow.GaugeShare"/> turns it into the drained bar.</summary>
    public static double? ElapsedShare(BuffState b, DateTime now)
    {
        if (b.ExpiresAt is not { } expires) return null;
        var span = (expires - b.LandedAt).TotalSeconds;
        return span <= 0 ? 1 : Math.Clamp((now - b.LandedAt).TotalSeconds / span, 0, 1);
    }

    /// <summary>The roster as chips, in the tracker's order (soonest to fade first).</summary>
    public static List<HudChipEntry> Chips(
        IReadOnlyList<BuffState> shown, DateTime now, double warnSeconds) =>
        shown.Select(b => new HudChipEntry(HudChipFamily.Buff, new SpawnChip(
            Zone: "", Name: b.Label,
            CountdownText: RosterFace(b, now),
            IsDue: IsUrgent(b, now, warnSeconds),
            Detail: Detail(b),
            Icon: HudChipRow.Emblem(HudChipFamily.Buff))
        {
            Fraction = ElapsedShare(b, now),
        })).ToList();

    /// <summary>The card's one line when it is drawing no chips — two different facts, and
    /// the difference matters: nothing is running, versus you asked to be told late and
    /// <paramref name="quiet"/> buffs are running.</summary>
    public static string EmptyLine(bool expiringOnly, int quiet, double warnSeconds) =>
        expiringOnly && quiet > 0
            ? $"{quiet} running quietly — timers appear at {HudChipRow.BuffWarnWindow(warnSeconds):0}s left."
            : "Nothing running — a buff landing on you starts its countdown here.";

    /// <summary>What has to change before the card rebuilds its elements rather than
    /// updating text in place. Countdowns are deliberately ABSENT: they move every tick, and
    /// a signature that included them would rebuild the whole panel once a second (trap 8's
    /// rule, one surface over).</summary>
    public static string Signature(
        IReadOnlyList<BuffState> shown, int quiet,
        IReadOnlyList<string> missing, IReadOnlyList<string> notSeen, IReadOnlyList<string> expiring,
        IEnumerable<string> suggestions) =>
        string.Join("|", shown.Select(b => b.Label + (b.Estimated ? "~" : ""))) + "·" + quiet
            + "§" + string.Join(",", missing)
            + "§" + string.Join(",", notSeen)
            + "§" + string.Join(",", expiring)
            + "§" + string.Join(",", suggestions);
}
