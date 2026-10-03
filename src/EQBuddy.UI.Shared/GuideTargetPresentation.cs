namespace EQBuddy.UI.Shared;

/// <summary>
/// **EVERY WORD THE MAP'S GUIDE-STEP LAYER SAYS** (DRA-42 D3, requirements §20).
///
/// <para><see cref="GuideTargets"/> decides which dot serves which step; nothing there picks a
/// word and nothing here decides a match — <see cref="GearTargetPresentation"/>'s split, for its
/// reason: the WPF layer has no unit tests, and the phone draws these same strings off the wire
/// (trap 32).</para>
///
/// <para><b>The claim it must never make is the one a mark on a map looks like it is making.</b>
/// EQBuddy does not know where anything spawns. A diamond means YOUR log archived a kill of one
/// of the step's droppers at that point; a zone with no diamond says only that you have not
/// archived one yet. <see cref="PointsNote"/> says so once, under the block (trap 73).</para>
///
/// <para>The row's wording is <see cref="WhileHerePresentation"/>'s — the step title, then its
/// quest and who — so a step reads the same on the map as in the Guide room's block it came
/// from.</para>
/// </summary>
public static class GuideTargetPresentation
{
    /// <summary>The block's heading. It names the zone for <see cref="GearTargetPresentation.Heading"/>'s
    /// reason: the block is about one zone, and without the name it would read as every step
    /// the player is on.</summary>
    public static string Heading(string? zone) =>
        zone is { Length: > 0 } z ? $"Guide steps — {z}" : "Guide steps";

    /// <summary>How many step rows the block draws before it counts the rest — the Guide room's
    /// own per-group cap, because the map's panel is narrower than the room, not wider.</summary>
    public const int StepsShown = WhileHerePresentation.StepsPerGroup;

    /// <summary>One step row: the step as its tab words it, then its quest and the creatures
    /// its item drops from here, in the page's order (never ranked — the catalog has no
    /// evidence to rank droppers with).</summary>
    public static string StepRow(WhileHereStep step) =>
        $"{step.Step} — {WhileHerePresentation.StepDetail(step)}";

    /// <summary>The cap line under the rows (trap 50: a surviving cap says so, and says where the
    /// rest are).</summary>
    public static string MoreSteps(int hidden) =>
        hidden == 1
            ? "…and 1 more step here — the Guide room's While-you're-here block lists them all."
            : $"…and {hidden:N0} more steps here — the Guide room's While-you're-here block lists them all.";

    /// <summary>
    /// How many of this zone's archived spawn points carry a dropper of one of these steps, with
    /// the DENOMINATOR in the sentence (<see cref="GearTargetPresentation.PointsHere"/>'s rule).
    /// </summary>
    public static string PointsHere(int marked, int archived) => marked <= 0
        ? archived <= 0
            ? "No spawn points archived here yet — kill one of these with a fresh /loc on the log "
              + "and its dot appears."
            : $"None of your {archived:N0} archived spawn points here has seen one of these. Kill "
              + "one with a fresh /loc and its dot joins them."
        : marked == 1
            ? $"1 of your {archived:N0} archived spawn points here serves one of these steps."
            : $"{marked:N0} of your {archived:N0} archived spawn points here serve one of these steps.";

    /// <summary>Where the diamonds come from, said once — the sentence that keeps this layer from
    /// reading as a spawn database.</summary>
    public const string PointsNote =
        "A diamond marks a spawn point where you have already killed something a step's item drops "
        + "from — eqlwiki names the creature, your own /loc at the kill put the dot there. EQBuddy "
        + "does not know where anything spawns, so a zone with no diamond only means you have not "
        + "archived a kill in it yet.";

    /// <summary>The steps that are here and can never wear a mark — done at a person, or with
    /// no creature named — counted so they do not read as marks the map forgot (trap 50).</summary>
    public static string Unmarkable(int count) => count <= 0
        ? ""
        : count == 1
            ? "1 more of your steps here is done at a person or names nobody to kill, so no spawn "
              + "point can carry it — the Guide room lists it."
            : $"{count:N0} more of your steps here are done at a person or name nobody to kill, so "
              + "no spawn point can carry them — the Guide room lists them.";

    /// <summary>The line a diamond adds to the circle's own hover, under everything the circle
    /// already said. It names the creature for <see cref="GearTargetPresentation.CircleTip"/>'s
    /// reason.</summary>
    public static string CircleTip(IReadOnlyList<GuideTargetHit> hits) => hits.Count == 0
        ? ""
        : "Guide step: " + string.Join(", ", hits.Select(h => $"{h.Step} for {h.Quest} ({h.Creature})"));

    /// <summary>What a diamond MEANS, on the hover of the block's heading.</summary>
    public const string MarkTip =
        "Diamonds mark the archived spawn points where you have killed something that drops an "
        + "item for an open step of a quest you track or have started. Track quests on the "
        + "Guide room's Quests tab on your PC.";

    /// <summary>The map toolbar's on/off control for the whole layer. It carries
    /// <see cref="Heading"/>'s noun so the switch and what it switches read as one feature.</summary>
    public const string ToggleLabel = "Guide steps";

    /// <summary>The toggle's tip. Its second sentence is the one that matters: hiding the layer
    /// untracks nothing.</summary>
    public static string ToggleTip(bool on) =>
        (on
            ? "Showing the spawn points and rows for the guide steps you can do in this zone. Click "
              + "to hide them and leave the rest of the map alone. "
            : "Hidden. Click to show the spawn points and rows for the guide steps you can do in "
              + "this zone. ")
        + "No quest is untracked and no step is ticked either way — this only changes what this "
        + "map draws.";
}
