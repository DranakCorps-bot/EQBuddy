using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the PROGRESS THEME's four tabs say in their badges, and what its launcher card
/// says on the widget (docs/Themes.md, step 3).
///
/// <see cref="ProgressSurface"/> in Core owns which tabs exist, their order, their labels
/// and their keys. This owns the NUMBERS beside those labels — which needs a
/// <see cref="StatsSnapshot"/> and the same formatters the cards already use, so it lives
/// here with the rest of the presentation rather than in Core.
///
/// It exists for the reason #210 exists. Five cards each carried their own header string,
/// and the desktop, the Avalonia widget and EQBuddy Mobile each built those five strings
/// for themselves. Folding them into one tab strip is exactly the moment a fourth copy
/// gets hand-rolled and the phone starts reporting a different number than the window —
/// which is what <c>CompanionProjection.Checklists.cs</c> did to the quest checklists
/// until <c>QuestChecklistLayout</c> took the job back. So the strings are decided once,
/// here, and all three surfaces call it.
/// </summary>
public static class ProgressTheme
{
    /// <summary>The Experience badge — unchanged from the Progress card's own header, so
    /// the glance a player already reads survives the fold verbatim.</summary>
    public static string Experience(StatsSnapshot s, int dingUnlocks) =>
        ProgressText.Header(s, dingUnlocks);

    /// <summary>
    /// The Raids GLANCE line — what an expanded Raids room says instead of drawing its
    /// 29-row ledger (Bevel, Helm-signed 2026-08-22).
    ///
    /// **It says what the chip cannot.** The first build printed `Raids — 2 / 21` directly
    /// under a chip already reading `Raids 2 / 21`, which is one fact twice, an inch apart.
    /// Bevel's ruling: the chip stays the scoreboard, the line carries the REMAINDER — and
    /// deleting the line instead is equally wrong, because an empty body under a selected
    /// tab reads as broken. No second "Raids" and no second fraction.
    ///
    /// In UI.Shared rather than in either card, so the Avalonia widget says the same words
    /// when its inline card lands (`FABLE.md`) — parity by shared module, never by feature
    /// list (#210).
    /// </summary>
    public static string RaidsGlance(int defeated, int total)
    {
        var left = Math.Max(0, total - defeated);
        // "all cleared" rather than "0 left": a zero is a number to read, and this is the
        // one state that is an achievement rather than a measurement.
        return left == 0 ? "all cleared" : $"{left} left";
    }

    /// <summary>The Wealth badge: **COIN ONLY** (Bevel, Helm-signed 2026-08-22).
    ///
    /// It carried the mote rate too, from the days when Wealth was the one tab that
    /// absorbed two cards. #227 settled that **Wealth is coin and the Motes card owns the
    /// rate**, and Inline themes PR 1 made the inconsistency impossible to keep: the
    /// expanded card's Wealth body is the four coin lines and nothing else, so a chip
    /// naming a mote rate sat an inch above a body that refused to. Bevel's rule is that
    /// the chip must match the body.
    ///
    /// **This deliberately changes the WINDOW's strip as well**, because it is the same
    /// shared badge — and that is right rather than collateral: the window's Wealth tab is
    /// coin too. Do not add the rate back for consistency with anything; the launcher line
    /// may still carry motes/hr, which is a different surface answering a different
    /// question.</summary>
    public static string Wealth(StatsSnapshot s) => StatsSnapshot.FormatCoin(s.Copper);

    /// <summary>The mote headline: count AND rate, the two halves the Motes card's own
    /// header carried ("3 · 0.9/hr"). Null when nothing has dropped.
    ///
    /// The RATE is the point and it is the half that went missing in 1.96.0 — #219
    /// (typical-usual-chaos): "Where'd motes/hour go? That was the most useful stat and
    /// the main reason I opened EQBuddy." A farmer is measuring a camp against the clock,
    /// so a running total answers a different question than the one being asked.</summary>
    /// <remarks>The words moved to <see cref="MotesPresentation.RateLine"/> when the
    /// Progress Experience room grew the same line (David, 2026-08-23); this stays as the
    /// snapshot-shaped door onto it, because a card header has a snapshot and not a
    /// summary. Two formatters for one rate is what put a mote rate on the Wealth chip
    /// above a body that refused to carry one.</remarks>
    public static string? MoteRate(StatsSnapshot s) =>
        MotesPresentation.RateLine(Motes.Summarize(s.Loot, s.Elapsed));

    /// <summary>The Faction badge — the Faction card's own header, with the plural fixed.
    /// The card said "1 factions" and had done since it was written; it was easy to miss
    /// on a card header a player rarely sees at exactly one, and reads as a bug the moment
    /// it is a tab label sitting beside three others.</summary>
    public static string Faction(StatsSnapshot s) => s.Faction.Count switch
    {
        0 => "—",
        1 => "1 faction",
        var n => $"{n} factions",
    };

    /// <summary>The Raids badge — cleared over the catalog's total, the Raids card's own
    /// header. Passed in rather than read here: the ledger is per character and rebuilt
    /// when the followed character changes, which is a thing a formatter should not
    /// know about.</summary>
    public static string Raids(int defeated, int total) => $"{defeated} / {total}";

    /// <summary>The full strip, badges included — what a window's tab row and the mobile
    /// page's tab row both build from.</summary>
    public static IReadOnlyList<ProgressTabHeader> Tabs(
        StatsSnapshot s, int dingUnlocks, int raidsDefeated, int raidsTotal) =>
        ProgressSurface.Tabs(
            experience: Experience(s, dingUnlocks),
            wealth: Wealth(s),
            faction: Faction(s),
            raids: Raids(raidsDefeated, raidsTotal));

    /// <summary>The launcher card's one line — the line that has to justify replacing
    /// five card headers with one. Delegates the assembly (and the "omit a part that has
    /// nothing to say" rule) to <see cref="ProgressSurface.LauncherSummary"/>, which is
    /// unit-tested in Core; this only decides which numbers go in.
    /// </summary>
    /// <remarks>
    /// **It carries what MOVES WHILE YOU PLAY: xp, coin, and the mote rate.** Faction and
    /// raid clears are review-time facts and badge their own tabs instead — one click,
    /// no scrolling.
    ///
    /// That rule was arrived at twice, both times by looking at the card rather than
    /// reasoning about it. Passing the tab badges through verbatim rendered
    /// "16.0% xp, +1 aa · 5p 1g 4s 8c · 1 mot…" — clipped mid-word, faction lost off the
    /// end. Trimming each part to a headline then fitted, but the part dropped to make it
    /// fit was the mote rate, and #219 (typical-usual-chaos) arrived within the hour of
    /// 1.96.0: "Where'd motes/hour go? That was the most useful stat and the main reason I
    /// opened EQBuddy."
    ///
    /// So the line is chosen by what changes while you are playing, not by what happens to
    /// fit. A lifetime raid count and a faction TALLY ("5 factions" names neither which nor
    /// how much) are the two weakest things that were on it; the mote rate is why a farmer
    /// has the widget open at all.
    /// </remarks>
    public static string LauncherSummary(StatsSnapshot s) =>
        ProgressSurface.LauncherSummary(
            xp: $"{s.XpPercent:0.0}% xp",
            coin: StatsSnapshot.FormatCoin(s.Copper),
            motes: Motes.Summarize(s.Loot, s.Elapsed) is { Total: > 0 } m
                ? $"{m.PerHour:0.#} motes/hr" : null);
}
