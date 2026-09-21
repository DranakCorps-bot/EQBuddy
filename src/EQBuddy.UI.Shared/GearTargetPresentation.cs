using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **EVERY WORD THE MAP'S TARGET LAYER SAYS** (DRA-216 D5, S13/S14).
///
/// <para><see cref="GearTargets"/> decides which dot belongs to which goal; nothing in that
/// file picks a word and nothing in this one decides a match. It is
/// <see cref="HelperPresentation"/>'s split, on the surface next door, and it is here rather
/// than in <c>MapView</c> for the reason <c>docs/TestPlan.md</c> §5 gives: the WPF layer has no
/// unit tests, so a sentence written inline is a sentence nothing can check — and the phone's
/// map draws the same block from the same strings (trap 32: every sentence rides the wire).</para>
///
/// <para><b>THE ONE CLAIM THIS BLOCK MUST NEVER MAKE IS THE ONE IT LOOKS LIKE IT IS MAKING.</b>
/// EQBuddy does not know where anything spawns. <see cref="SpawnPointLedger"/> archives where
/// <i>you</i> killed something while a fresh <c>/loc</c> was on the log, and the wiki's item
/// page names a creature and a zone. So a ring says "you have killed one of these here", never
/// "it spawns here", and a zone with no ring says nothing about the game at all — it says you
/// have not archived a kill there yet. <see cref="PointsNote"/> carries that, once, under the
/// block (trap 73).</para>
/// </summary>
public static class GearTargetPresentation
{
    /// <summary>The block's heading. It names the ZONE because the map's other block does
    /// (<i>"Named — Befallen"</i>) and because the whole block is about one zone: a heading that
    /// did not say which would read as the whole tracked list, which is the Helper room's block
    /// and a different claim.</summary>
    public static string Heading(string? zone) =>
        zone is { Length: > 0 } z ? $"Going after — {z}" : "Going after";

    /// <summary>
    /// **WHERE THE RINGS COME FROM, SAID ONCE UNDER THE BLOCK** — the sentence that keeps this
    /// layer from reading as a spawn database.
    ///
    /// <para>Both halves are things the player can act on: the wiki named the creature, and
    /// YOUR log put the dot there. Neither half is a claim about where the game spawns
    /// anything, and the second half is why a zone can be right and still have no ring in it.</para>
    /// </summary>
    public const string PointsNote =
        "A ring marks a spawn point you have already killed one of these at — eqlwiki names the "
        + "creature, your own /loc at the kill put the dot there. EQBuddy does not know where "
        + "anything spawns, so a zone with no ring only means you have not archived a kill in it "
        + "yet.";

    /// <summary>
    /// One goal's row: what you are going after, and who drops it HERE.
    ///
    /// <para>The creatures are the page's own order and never ranked — the
    /// <see cref="GearUpgradeFact.Who"/> rule verbatim, and for its reason: nothing in the
    /// catalog says which of six creatures drops it more often, so any order EQBuddy imposed
    /// would be an invented one. The cap says how much it held back (trap 50).</para>
    ///
    /// <para><b>A zone the page named with nobody under it gets its own sentence</b> rather
    /// than a row trailing an empty list. It is a real answer — the item does drop here — and
    /// it is also the one row that can never light a ring, so saying only that half would leave
    /// a player hunting a dot that cannot appear.</para>
    /// </summary>
    public static string GoalRow(GearTargetZone goal)
    {
        if (goal.Creatures.Count == 0)
            return $"{goal.Item} — eqlwiki names this zone and nobody in it, so there is nothing "
                   + "here for a ring to match.";
        var shown = goal.Creatures.Take(Recommendations.GearMobsPerItem).ToList();
        var more = goal.Creatures.Count - shown.Count;
        var who = string.Join(", ", shown);
        return more <= 0
            ? $"{goal.Item} — {who}."
            : $"{goal.Item} — {who}, and {more:N0} more on its page.";
    }

    /// <summary>How many of this zone's archived spawn points carry one of the goals' creatures.
    /// The DENOMINATOR is in the sentence, as every counted line in this repo keeps it: "2 of
    /// your 14" is a fact somebody can argue with and "2 rings" is not.</summary>
    public static string PointsHere(int marked, int archived) => marked <= 0
        ? archived <= 0
            ? "No spawn points archived here yet — kill one with a fresh /loc on the log and the "
              + "dot appears."
            : $"None of your {archived:N0} archived spawn points here has seen one of these. Kill "
              + "one with a fresh /loc and its dot joins them."
        : marked == 1
            ? $"1 of your {archived:N0} archived spawn points here is one of these."
            : $"{marked:N0} of your {archived:N0} archived spawn points here are one of these.";

    /// <summary>
    /// Where else this goal drops — the "so where do I go" half, for a player reading the map of
    /// a zone that has nothing for them.
    ///
    /// <para>Capped at <see cref="GearTargets.ZonesPerItem"/> with the rest counted:
    /// <i>Bronze Long Sword</i> names twenty-three zones in the shipped catalog, and the cap is
    /// what keeps a side panel a side panel. The zones are the page's own order for
    /// <see cref="GoalRow"/>'s reason — the map has no evidence with which to rank a place, and
    /// the room that does is not this surface.</para>
    /// </summary>
    public static string Elsewhere(string item, IReadOnlyList<string> zones)
    {
        if (zones.Count == 0) return "";
        var shown = zones.Take(GearTargets.ZonesPerItem).ToList();
        var more = zones.Count - shown.Count;
        var where = string.Join(", ", shown);
        return more <= 0
            ? $"{item} — drops in {where}."
            : $"{item} — drops in {where}, and {more:N0} more zones on its page.";
    }

    /// <summary>The heading over the elsewhere rows. Separate from <see cref="Heading"/> because
    /// it is a different claim: that block is about the dots on THIS map, this one is about
    /// somewhere the player is not.</summary>
    public const string ElsewhereHeading = "Drops somewhere else";

    /// <summary>
    /// The goals EQBuddy's catalog holds no page for, named and counted.
    ///
    /// <para><b>The subject is EQBuddy's catalog and never the game</b> — the posture
    /// <see cref="HelperPresentation.UnreadWorn"/> keeps one surface along, and the reason is
    /// the same: "that item drops nowhere" is a claim about EverQuest, and what actually
    /// happened is that a name did not match a page we ship.</para>
    /// </summary>
    public static string Unreadable(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return "";
        var shown = items.Take(Named).ToList();
        var more = items.Count - shown.Count;
        var names = string.Join(", ", shown);
        return more <= 0
            ? $"No item page shipped for {names} — EQBuddy cannot say where it drops. Check the "
              + "name on eqlwiki."
            : $"No item page shipped for {names} and {more:N0} more — EQBuddy cannot say where "
              + "they drop. Check the names on eqlwiki.";
    }

    /// <summary>The goals whose page names nowhere to go. It is a different sentence from
    /// <see cref="Unreadable"/> because it is a different fact and the player can act on it
    /// differently: the page exists and simply lists no drop zone, which is the ordinary state
    /// of a quest reward, vendor stock or a crafted item (3,104 of the 6,844 wearable records
    /// in the shipped catalog).</summary>
    public static string NoDropZone(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return "";
        var shown = items.Take(Named).ToList();
        var more = items.Count - shown.Count;
        var names = string.Join(", ", shown);
        return more <= 0
            ? $"{names} — its eqlwiki page names no drop zone, so it is a quest, vendor or craft "
              + "item as far as this map is concerned."
            : $"{names} and {more:N0} more — their eqlwiki pages name no drop zone, so they are "
              + "quest, vendor or craft items as far as this map is concerned.";
    }

    /// <summary>How many goals a refusal sentence NAMES before it counts the rest.
    /// <see cref="HelperPresentation.GearBandNamed"/>'s number and its argument — item names
    /// are long, and a caption listing eleven of them is a table pretending to be a sentence.
    /// The count is always the WHOLE count (trap 50).</summary>
    public const int Named = 3;

    /// <summary>
    /// The line a target ring adds to the circle's own hover, under everything the circle
    /// already said.
    ///
    /// <para>It NAMES the creature as well as the goal, because a point that has seen five
    /// different mobs would otherwise be marked without saying which one to wait for — and the
    /// countdown already on that hover is about the point, not about this creature.</para>
    /// </summary>
    public static string CircleTip(IReadOnlyList<GearTargetHit> hits) => hits.Count == 0
        ? ""
        : "Going after: " + string.Join(", ", hits.Select(h => $"{h.Item} ({h.Creature})"));

    /// <summary>What a ring MEANS, on the hover of the block's own heading — one place, so the
    /// per-circle line above can stay one line.</summary>
    public const string RingTip =
        "Rings mark the archived spawn points where you have killed something that drops an item "
        + "you are tracking. Track and untrack in the Helper room on your PC.";

    /// <summary>
    /// The map toolbar's on/off control for this whole layer — the rings and the block together
    /// (D5 Planner review, finding D5-1).
    ///
    /// <para>It carries <see cref="Heading"/>'s noun on purpose: the control and the thing it
    /// switches have to be recognisably one feature, and "Targets" would be a second name for
    /// something the panel two inches away already calls something else.</para>
    /// </summary>
    public const string ToggleLabel = "Going after";

    /// <summary>
    /// <b>The tip's job is the sentence that is not about the map at all.</b> A player hiding a
    /// layer on a busy screen has to know the goal itself survives — the alternative way to get
    /// a clean map was to untrack, and untracking throws away the decision the tracked list
    /// exists to remember. So the second sentence is the load-bearing one and it says the same
    /// thing in both states.
    ///
    /// <para>Written as one method over the state rather than two constants so the two halves
    /// cannot drift into disagreeing about what the control does.</para>
    /// </summary>
    public static string ToggleTip(bool on) =>
        (on
            ? "Showing the spawn points and rows for the upgrades you are tracking. Click to hide "
              + "them and leave the rest of the map alone. "
            : "Hidden. Click to show the spawn points and rows for the upgrades you are tracking. ")
        + "Nothing is untracked either way — this only changes what this map draws.";
}
