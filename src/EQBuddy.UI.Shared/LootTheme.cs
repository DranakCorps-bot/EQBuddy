using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the GEAR &amp; LOOT theme's tabs say in their badges, and what its launcher card
/// says on the widget (docs/Themes.md, step 3).
///
/// <see cref="LootSurface"/> in Core owns which tabs exist, their order, their labels and
/// their keys. This owns the NUMBERS beside those labels — which needs a
/// <see cref="StatsSnapshot"/> and the settings the gear list lives in, so it belongs here
/// with the rest of the presentation rather than in Core.
///
/// Same reason <see cref="ProgressTheme"/> exists, and #210's rule: the moment a fold puts
/// two card headers into one tab strip is the moment a third copy of those strings gets
/// hand-rolled somewhere and the phone starts reporting a different number than the
/// window. Decided once, here; all three surfaces call it.
/// </summary>
public static class LootTheme
{
    /// <summary>The Loot badge — what the Loot card's own header carried, so the glance a
    /// player already reads survives the fold verbatim. "39 items (+4 made)".</summary>
    public static string Loot(StatsSnapshot s) =>
        s.CraftedTotal > 0
            ? $"{s.LootTotal} item{(s.LootTotal == 1 ? "" : "s")} (+{s.CraftedTotal} made)"
            : $"{s.LootTotal} item{(s.LootTotal == 1 ? "" : "s")}";

    /// <summary>The Gear badge — acquired over total, which is the whole question that card
    /// answered. **Blank until there is a list** (David, 2026-08-20): "0/0" reads as a
    /// score you are losing, and an em dash reads as a glyph that failed to load. The body
    /// underneath already says "No gear list imported" in words, so the badge has nothing
    /// left to add and says nothing.</summary>
    public static string Gear(IReadOnlyCollection<GearChecklistItem> checklist) =>
        checklist.Count == 0 ? "" : $"{checklist.Count(i => i.Acquired)}/{checklist.Count}";

    /// <summary>The full strip, badges included — what the window's tab row and the
    /// mobile page's tab row both build from.</summary>
    public static IReadOnlyList<LootTabHeader> Tabs(
        StatsSnapshot s, IReadOnlyCollection<GearChecklistItem> checklist,
        string? locker = null) =>
        LootSurface.Tabs(loot: Loot(s), gear: Gear(checklist), locker: locker);

    /// <summary>
    /// The launcher card's one line — the line that has to justify replacing two card
    /// headers with one. Delegates the assembly, and the "omit a part with nothing to
    /// say" rule, to <see cref="LootSurface.LauncherSummary"/>, which is unit-tested in
    /// Core; this only decides which numbers go in.
    /// </summary>
    /// <remarks>
    /// **What moves while you play leads.** Loot count and what you have crafted change
    /// every few minutes; a gear list changes on the day you finally get the drop. So loot
    /// leads and gear rides at the end as a fraction — present, because "3/12" is a real
    /// glance, but last, because it is the part that can be dropped if the line ever has
    /// to give.
    ///
    /// That ordering is #219's lesson taken in advance rather than after. The Progress
    /// launcher was trimmed to fit by dropping the mote RATE, and the player who used that
    /// number arrived within the hour of the release. A summary replacing card headers
    /// gets to choose WHICH numbers survive — it does not get to lose one quietly.
    /// </remarks>
    public static string LauncherSummary(
        StatsSnapshot s, IReadOnlyCollection<GearChecklistItem> checklist) =>
        LootSurface.LauncherSummary(
            items: s.LootTotal,
            crafted: s.CraftedTotal,
            gearTotal: checklist.Count,
            gearAcquired: checklist.Count(i => i.Acquired));
}
