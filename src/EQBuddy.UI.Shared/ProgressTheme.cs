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

    /// <summary>The Wealth badge. It carries BOTH halves of the merge — coin and motes —
    /// because Wealth is the one tab that absorbs two cards, and a badge that named only
    /// coin would quietly answer half the question the Motes card used to answer on its
    /// own. Motes are dropped from the string when there are none rather than printed as
    /// a zero: a fresh character is exactly who is looking at a fresh widget.</summary>
    public static string Wealth(StatsSnapshot s)
    {
        var coin = StatsSnapshot.FormatCoin(s.Copper);
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        return motes.Total > 0
            ? $"{coin} · {motes.Total} mote{(motes.Total == 1 ? "" : "s")}"
            : coin;
    }

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
    /// unit-tested in Core; this only decides which numbers go in.</summary>
    /// <remarks>
    /// The parts here are SHORTER than the tab badges they come from, and that was found
    /// by looking at the card rather than by reasoning about it: the first version passed
    /// <see cref="Experience"/> and <see cref="Wealth"/> straight through and the widget
    /// rendered "16.0% xp, +1 aa · 5p 1g 4s 8c · 1 mot…" — clipped mid-word, with the
    /// faction count lost off the end entirely. A summary that replaces five card headers
    /// and then truncates has not kept the glance; it has just hidden the loss.
    ///
    /// So the launcher takes the headline of each part and the TABS carry the detail: the
    /// AA and level counts belong to Experience's badge, the mote count to Wealth's. Four
    /// short parts fit the widget's width; four full ones do not.
    /// </remarks>
    public static string LauncherSummary(StatsSnapshot s, int dingUnlocks, int raidsDefeated) =>
        ProgressSurface.LauncherSummary(
            xp: $"{s.XpPercent:0.0}% xp",
            coin: StatsSnapshot.FormatCoin(s.Copper),
            factions: s.Faction.Count,
            raidsCleared: raidsDefeated);
}
