using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>A whole-room empty in the two halves every one of them has: the fact, and the
/// answer. Kept as a pair rather than two loose strings so a room cannot ship one without
/// the other — an empty state with a heading and no explanation is a blank panel with a
/// label on it.</summary>
/// <param name="Heading">What is true, in the player's terms. Never "no data".</param>
/// <param name="Explanation">What is missing, what to do, where, and what happens next —
/// the inventory-dump voice <see cref="HomeReadout.EmptyIdentity"/> set for this shell.
/// </param>
public sealed record RoomEmptyMessage(string Heading, string Explanation);

/// <summary>
/// **THE FOUR DATA ROOMS' WHOLE-ROOM EMPTY** — Progress, Gear, World and Quests, the rooms
/// that came into the Evolved shell as MOVES and LIFTS of v1 windows and therefore arrived
/// with a v1 window's assumption baked in: that a character is already known, because the
/// only way to open the window was from a widget that was already following one.
///
/// Bevel ruled the shape on 2026-09-04 (Helm-signed ~11:15 PM CT) and restated it twice
/// more without it being built: **position is a ROOM rule, canvas treatment is
/// per-surface.** <c>HomeRoom</c> was the first consumer and <c>LiveRoom</c> the second;
/// this is the other four, and it is in <c>UI.Shared</c> for the standing reason — the WPF
/// layer has no unit tests (docs/TestPlan.md §5), so a sentence or a state rule left inline
/// in a room is one nothing can check.
///
/// **ONE ROOT CONDITION, FOUR SENTENCES, AND THE ROOT IS THE PART WORTH ARGUING FOR.**
/// Every one of these four rooms is downstream of the log: Progress is arithmetic over the
/// session, Gear reads loot from the log and bags from a dump the game names after the
/// character, World starts from the zone the log last saw, and the quest tracker ticks off
/// lines the log carries. With no character there is no log, and with no log every one of
/// them is a set of empty tabs above an empty body — which is Bevel's own sentence
/// (*"Gear/Quests/World/Progress all assume a character is already known"*) turned into a
/// predicate rather than left as an observation.
///
/// **EACH ONE STILL CARRIES ITS OWN GUARD, AND THE GUARD IS NOT DECORATION.** The room-level
/// empty COLLAPSES the tab strip and the body, so a predicate that fires while a room has
/// something in it is not a cosmetic slip — it is a capability the player can no longer
/// reach, which is "silent no-ops are broken" with the switch on the other side. Two of the
/// four have genuinely character-independent sources and would be wrong without a guard:
/// Gear's wishlist is a settings list, and World's camp markers are dropped by a button that
/// works whether or not a log has ever been read. So each predicate names what would have to
/// be true for the room to be honestly empty, and every one of those clauses has a failing
/// row in <c>ShellRoomEmptyTests</c> — the same shape <see cref="LivePresentation.RoomIsEmpty"/>
/// uses for the raid ledger it must not swallow.
///
/// **WHAT THIS DELIBERATELY DOES NOT SWALLOW: a ⧉ copy.** Live's entry names the hazard
/// exactly — a room-level empty drawn over a surface whose own empty state ships the command
/// that fixes it is trap 34, an affordance removed by something that reads as polish. Here
/// the answer is not a guard but an ORDER: <c>/outputfile inventory</c> and
/// <c>/outputfile achievements</c> both need a character logged in, so on a profile with no
/// character they are not the next thing to do — <c>/log on</c> is. The ⧉ copies stay where
/// they already are, on Home's readiness rows, which is the room the shell opens on and the
/// room that owns "what is EQBuddy still missing". These four say what they are waiting for
/// and point at the same first step.
/// </summary>
public static class ShellRoomEmpty
{
    /// <summary>
    /// The root condition, asked through <see cref="HomeReadout.Identity"/> rather than by
    /// testing the string here.
    ///
    /// **One producer for "does EQBuddy know who it is following", and it already existed.**
    /// Re-spelling <c>identity.Character.Length == 0</c> in a second place would be two
    /// answers to one question that agree today and would drift the first time the identity
    /// rule gains a clause — trap 33 in a boolean. The rooms call the room-specific
    /// predicates below rather than this, so the guard cannot be forgotten at a call site.
    /// </summary>
    public static bool NoCharacterYet((string Server, string Character) identity) =>
        HomeReadout.Identity(identity) == IdentityState.NoCharacter;

    // ---- Progress -----------------------------------------------------------------

    public static readonly RoomEmptyMessage Progress = new(
        "Nothing to chart yet",
        "Experience, coin, motes and faction are all read from your character's log. Turn "
        + "logging on in game with /log on, then point EQBuddy at your Logs folder in "
        + "Options — the first line it reads starts filling this room in.");

    /// <summary>
    /// Progress is the one room with no character-independent source at all: every tab is
    /// arithmetic over the session snapshot, and a session is a log being read. The clauses
    /// are still spelled out rather than collapsed to <see cref="NoCharacterYet"/>, because
    /// a snapshot carrying rows with no character is a state this room should SHOW and
    /// argue about, not one it should paint over.
    /// </summary>
    public static bool ProgressIsEmpty((string Server, string Character) identity, StatsSnapshot s) =>
        NoCharacterYet(identity)
        && s.XpTicks == 0 && s.Levels.Count == 0 && s.SkillUps.Count == 0
        && s.AaAbilities.Count == 0 && s.Copper == 0
        // Motes are summarised out of the loot list (MotesCardView), so one clause covers
        // the Wealth tab's second half as well as its first.
        && s.Loot.Count == 0 && s.Faction.Count == 0;

    // ---- Gear ---------------------------------------------------------------------

    public static readonly RoomEmptyMessage Gear = new(
        "No bags, loot or wishlist yet",
        "Gear reads what you pick up from your log, and what you are carrying from an "
        + "/outputfile inventory dump the game names after your character. Turn logging on "
        + "in game with /log on and point EQBuddy at your Logs folder in Options; Home then "
        + "hands you the dump command when it is the next thing to do.");

    /// <summary>
    /// <paramref name="wishlistCount"/> is <c>AppSettings.GearChecklist</c>, and it is the
    /// clause that stops this being wrong. **The wishlist is a settings list**: it is edited
    /// by hand, it survives a profile that has never seen a log line, and it is the one thing
    /// in this room a player can build before they ever play. Collapsing the room over it
    /// would take away a list they typed themselves.
    /// </summary>
    public static bool GearIsEmpty(
        (string Server, string Character) identity, StatsSnapshot s, int wishlistCount) =>
        NoCharacterYet(identity) && wishlistCount == 0 && s.Loot.Count == 0;

    // ---- World --------------------------------------------------------------------

    public static readonly RoomEmptyMessage World = new(
        "No zone seen yet",
        "The map, your camps, the route planner and your travels all start from the zone "
        + "your log says you are standing in. Turn logging on in game with /log on, then "
        + "point EQBuddy at your Logs folder in Options — zone once and the map draws itself.");

    /// <summary>
    /// **The Path tab was the reason to check rather than assume**, and checking is what
    /// kept this predicate honest: route planning looks character-independent (pick two
    /// zones, get hops) and is not — <c>TravelView</c> plans FROM
    /// <c>IZoneHost.CurrentZoneName</c> and says *"From: (no zone seen in the log yet)"*
    /// when there is none, so with no zone it can produce no route. The zone clause is
    /// therefore the whole Path tab as well as the whole Map tab.
    ///
    /// The markers clause is the other one that is not decoration: "Drop camp marker" is
    /// room chrome and its button works whether or not a log has been read, so a profile
    /// with markers on it has something this room must keep showing.
    /// </summary>
    public static bool WorldIsEmpty((string Server, string Character) identity, StatsSnapshot s) =>
        NoCharacterYet(identity)
        && s.CurrentZone.Length == 0 && s.Zones.Count == 0
        && s.Markers.Count == 0 && s.Deaths.Count == 0;

    // ---- Quests -------------------------------------------------------------------

    public static readonly RoomEmptyMessage Quests = new(
        "No quests tracked yet",
        "The tracker follows your log for turn-ins and ticks Epic and Plane of Sky steps "
        + "off by itself as the pieces arrive. Turn logging on in game with /log on, then "
        + "point EQBuddy at your Logs folder in Options, and this room fills in as you play.");

    /// <summary>
    /// <paramref name="tickedSteps"/> is every Epic and Sky step already marked done —
    /// <c>AppSettings.EpicQuestCompleted</c> plus <c>SkyQuestCompleted</c>. Those are
    /// SETTINGS and not log facts: a player can tick a step by hand, and #204/#209, #210 and
    /// #212 are three separate bugs about those very lists losing their writer, so a room
    /// that hid them the moment the identity was unknown would be a fourth in the same
    /// family — the data survives and the thing that showed it does not.
    /// </summary>
    public static bool QuestsIsEmpty((string Server, string Character) identity, int tickedSteps) =>
        NoCharacterYet(identity) && tickedSteps == 0;
}
