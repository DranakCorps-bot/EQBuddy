using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **EVERY WORD THE HELPER SAYS, IN ONE PLACE** (DRA-70 D1; PRD §12 HOME-001..006).
///
/// <para><b>Why the words left Core.</b> <c>UnlockGuidance</c> — the mini-recommender this
/// feature copies its manners from — phrases its own four sentences in Core, and that is
/// right for four sentences drawn on one surface. The Helper says something about nine goals
/// across two hosts, and the rule it has to keep is a rule about <b>vocabulary</b>: HOME-006
/// forbids any line that claims a camp is safe, easy or survivable. A guard over vocabulary
/// can only be written where the vocabulary is. So the engine carries numbers and this file
/// carries language, and <c>HelperPresentationTests</c> sweeps every sentence this file can
/// produce — including the ones it assembles from a fixture, not only its constants, because
/// a ban that only reads <c>const</c> fields cannot see a word that arrives through an
/// interpolation.</para>
///
/// <para><b>HOME-006 IS A REFUSAL, NOT A CAVEAT.</b> There is no sentence here that says a
/// place is safe, and no sentence that says one is dangerous either. Where the player has
/// died, <see cref="Why"/> reports the deaths as their own history and stops; where they
/// have not, nothing is said at all — "you have never died here" is one sitting away from
/// being false, and a recommender that offered it would be making exactly the claim the
/// requirement exists to forbid. The rule is not that the wording is careful. The rule is
/// that the sentence does not exist.</para>
///
/// <para><b>HOME-004 arrives as a suffix nobody can forget.</b> Every fact carries an
/// <see cref="Evidence"/> tag, and <see cref="Why"/> appends <see cref="CatalogLabel"/> to
/// every <see cref="Evidence.Catalog"/> line by construction rather than by remembering —
/// so a new catalog-sourced fact is labelled the day it is added, by the switch that has to
/// grow to accommodate it.</para>
///
/// <para><b>HOME-003 arrives as a scope.</b> A personal line names what it rests on —
/// "across 14 of your sessions here" — because a rate with no denominator is a claim nobody
/// can argue with, and the player is the only person who can tell whether fourteen sessions
/// in Befallen is a lot.</para>
///
/// <para><b>Doors resolve through <see cref="ShellPages"/> and nowhere else.</b> Core says
/// THAT a recommendation has a door and what it points at; this file turns that into the
/// <c>page:room</c> address the rail, the palette and <c>EQBUDDY_SHELL</c> already share.
/// Two ways to land on a room is trap 33 lifted from data into navigation.</para>
/// </summary>
public static class HelperPresentation
{
    // ---- the room's own chrome ---------------------------------------------------------

    /// <summary>The question the room exists to answer, in the Founder's own words.</summary>
    public const string RoomQuestion = "What should I do next?";

    /// <summary>Above the chips. It says what the chips DO, because a strip of nine pills
    /// with no sentence over it reads as a filter bar whose off-state is a mystery.</summary>
    public const string GoalStripNote =
        "Pick what you are working toward. Nothing picked means EQBuddy weighs all of them.";

    /// <summary>The heading over the answers.</summary>
    public const string AnswersHeading = "Worth doing next";

    /// <summary>The heading over the chips.</summary>
    public const string GoalsHeading = "Your goals";

    /// <summary>The line under the answers that says where they come from. It is the whole
    /// values line in one sentence: your log, your bags, your dumps, and nobody else's.</summary>
    public const string SourceNote =
        "Every answer below is read from your own log and the files the game writes for you. "
        + "EQBuddy never looks at anyone else's play.";

    // ---- the nine goals, the Founder's wording ------------------------------------------

    /// <summary>
    /// The chip's label — <b>the Founder's own nine, verbatim</b> (DRA-70, 2026-09-12).
    ///
    /// <para>They are not tidied, shortened or made parallel. "Work on Faction" is longer
    /// than "Farm Gear" and stays that way: this is the list the person who asked for the
    /// feature wrote down, and re-phrasing it would make the chip strip a design opinion
    /// about a decision that was already made.</para>
    /// </summary>
    public static string GoalLabel(HelperGoal goal) => goal switch
    {
        HelperGoal.LevelUp => "Level Up",
        HelperGoal.FarmGear => "Farm Gear",
        HelperGoal.UnlockClasses => "Unlock Classes",
        HelperGoal.UnlockRaces => "Unlock Races",
        HelperGoal.FarmMotes => "Farm Motes",
        HelperGoal.WorkOnFaction => "Work on Faction",
        HelperGoal.FarmMaterials => "Farm Materials",
        HelperGoal.MakeMoney => "Make Money",
        HelperGoal.Achievements => "Achievements",
        _ => "",
    };

    /// <summary>The chip's tooltip: what EQBuddy reads to answer this goal. It names a
    /// SOURCE rather than promising an outcome, so a goal that has nothing to say has
    /// already told the player why before they click it.</summary>
    public static string GoalTip(HelperGoal goal) => goal switch
    {
        HelperGoal.LevelUp =>
            "Where you have levelled fastest, measured from your own stored sessions.",
        HelperGoal.FarmGear =>
            "Your wishlist, your bags and what you have seen drop.",
        HelperGoal.UnlockClasses =>
            "The class unlocks you are closest to, from your achievements dump.",
        HelperGoal.UnlockRaces =>
            "The race unlocks you are closest to, from your achievements and faction dumps.",
        HelperGoal.FarmMotes =>
            "Where motes have actually dropped for you.",
        HelperGoal.WorkOnFaction =>
            "Standings from your faction dump, and which of your own kills move them.",
        HelperGoal.FarmMaterials =>
            "What is in your bags and which skills you have been raising.",
        HelperGoal.MakeMoney =>
            "The coin your own sessions have actually earned.",
        HelperGoal.Achievements =>
            "Which achievements you are closest to finishing.",
        _ => "",
    };

    // ---- a recommendation's headline ----------------------------------------------------

    /// <summary>
    /// What the row is called. A place is its own name; everything else is named for the
    /// thing you are working on, with the kind of thing after it so "Dark Elf" is not
    /// mistaken for a zone.
    /// </summary>
    public static string Headline(Recommendation r) => r.Kind switch
    {
        RecommendationKind.Zone => r.Subject,
        RecommendationKind.Unlock => $"{r.Subject} — unlock",
        RecommendationKind.Faction => $"{r.Subject} — faction",
        _ => r.Subject,
    };

    /// <summary>Which of your goals this one answers, under the headline. It is the
    /// cross-domain chain said out loud: a row that serves two goals has earned its place
    /// and the player should be able to see why without counting the why-lines.</summary>
    public static string Serves(Recommendation r) =>
        r.Goals.Count == 0 ? "" : string.Join(" · ", r.Goals.Select(GoalLabel));

    // ---- the why-lines ------------------------------------------------------------------

    /// <summary>
    /// The label every <see cref="Evidence.Catalog"/> line ends with — <b>HOME-004</b>.
    ///
    /// <para>It says what the line is NOT, because that is the confusion the requirement
    /// names: a number from a file EQBuddy ships must never read as "your expected rate".</para>
    /// </summary>
    public const string CatalogLabel = "From EQBuddy's own catalog — not a measurement of your play.";

    /// <summary>
    /// One why-line, worded.
    ///
    /// <para><b>The switch is the must-list</b> (trap 34): a fact shape with no arm falls to
    /// the default and answers empty, and <c>HelperPresentationTests</c> walks every
    /// <see cref="WhyFact"/> subtype in the assembly and fails on an empty answer. A negative
    /// rule — "no line says 'safe'" — cannot see a fact NOBODY WORDED, and a recommendation
    /// drawing a blank line under its headline is the shape that would ship.</para>
    ///
    /// <para>The catalog label is appended HERE rather than by each arm, so a new
    /// catalog-sourced fact cannot arrive unlabelled.</para>
    /// </summary>
    public static string Why(WhyFact fact)
    {
        var text = Sentence(fact);
        if (text.Length == 0) return "";
        return fact.Evidence == Evidence.Catalog ? $"{text} {CatalogLabel}" : text;
    }

    private static string Sentence(WhyFact fact) => fact switch
    {
        // The rate, and immediately what it rests on. "Across N of your sessions here" is
        // HOME-003's scope: the attribution is by the session's main zone, so the sentence
        // says "sessions" and never "in this zone", which would claim a precision the
        // stored row does not have.
        ZoneXpRateFact f =>
            // TWO shapes and not one with a plural switch in the middle of it. "across 1 of
            // your session" is what a single plural toggle produces, and the first staged
            // screenshot of this room is where it showed up (trap 23: a shot whose words you
            // did not predict has not been reviewed). "across N of your sessions" needs an N
            // there are others beside.
            f.Sessions == 1
                ? $"{f.XpPerHour:0.0}%/hr here, from 1 stored session ({Hours(f.Hours)})."
                : $"{f.XpPerHour:0.0}%/hr here, across {f.Sessions:N0} of your sessions "
                  + $"({Hours(f.Hours)}).",

        ZoneCadenceFact f =>
            $"Your fights here run {Seconds(f.AvgFightSeconds)} on average, over "
            + $"{f.Kills:N0} {(f.Kills == 1 ? "kill" : "kills")} you have recorded.",

        // HOME-006's ONLY survival-adjacent sentence, and it reports rather than advises.
        // There is no arm for zero deaths — see this class's summary.
        ZoneDeathsFact f =>
            $"You have died here {Count(f.Deaths, "time", "times")}, across "
            + $"{Count(f.Sessions, "session", "sessions")}.",

        UnlockScoreFact f =>
            $"{f.Subject}: {f.Done} of {f.Total} requirements done, by the game's own record.",

        // An EM DASH and not a comma between the two numbers. The first staged screenshot
        // read "you stand at 1,000, 1,000 from the top", where the comma reads as a
        // thousands separator and the sentence looks like a rendering fault (trap 23 again:
        // it is the picture that finds these, not the assertion).
        FactionStandingFact f =>
            $"{f.Faction}: you stand at {f.Value:N0} — {f.PointsToMax:N0} from the top.",

        CatalogQuestFact f => $"The quest '{f.Quest}' is in EQBuddy's quest list.",

        // Already measured AND already phrased by the producer that owns it. Drawn exactly
        // as it arrived: re-wording it here would be a second answer to one arithmetic.
        WordedFact f => f.Text,

        _ => "",
    };

    /// <summary>Said only when the per-row cap actually held something back — a surviving
    /// cap says so out loud (trap 50).</summary>
    public static string WithheldWhy(int count) => count <= 0
        ? ""
        : $"{count} more {(count == 1 ? "reason" : "reasons")} not shown.";

    // ---- the list's own cap -------------------------------------------------------------

    /// <summary>
    /// The sentence under the list when there were more answers than the cap.
    ///
    /// <para>HOME-002 asks for three strong recommendations, and trap 50 asks the cap to
    /// admit itself: the fourth-best camp is exactly the one somebody is hunting for, and a
    /// list that quietly stops at three teaches a player there is nothing else.</para>
    /// </summary>
    public static string Cap(int withheld) => withheld <= 0
        ? ""
        : $"{withheld} more {(withheld == 1 ? "answer" : "answers")} matched your goals. "
          + "EQBuddy shows the three it can say the most about.";

    // ---- empty states -------------------------------------------------------------------

    /// <summary>Nothing at all to say — no goal produced an answer and none of them named a
    /// missing store either. The honest whole-room state.</summary>
    public static readonly RoomEmptyMessage Nothing = new(
        "Nothing to suggest yet",
        "EQBuddy answers this from your own play: the sessions it has stored, the bags and "
        + "standings the game writes when you run an /outputfile command, and the quests you "
        + "are tracking. Play a session or run a dump and this fills in.");

    /// <summary>
    /// What a selected, answerable goal is waiting for — and, where the answer is a file the
    /// game writes, the command that writes it.
    ///
    /// <para>The command itself is NOT spelled here: <c>HelperRoom</c> hands over the
    /// constant off <c>GameCommands</c> beside this sentence, which is what
    /// <c>GameCommandsTests</c> asserts and <c>NoCopySurfaceCarriesItsOwnCommandLiteral</c>
    /// forbids the other way round. A surface that tells a player to import something without
    /// saying how is a silent no-op (David, 2026-08-20).</para>
    /// </summary>
    public static string Gap(GoalGap gap) => gap.Reason switch
    {
        GoalGapReason.NoFactionDump =>
            $"{GoalLabel(gap.Goal)}: the log only ever sees faction CHANGES, never where you "
            + "stand. Run the faction command in game and this fills in.",
        GoalGapReason.NoFactionPicked =>
            $"{GoalLabel(gap.Goal)}: pick the factions you are working on and EQBuddy will "
            + "say which of your own kills move them.",
        GoalGapReason.NoAchievementsDump =>
            $"{GoalLabel(gap.Goal)}: unlocks are the game's own record. Run the achievements "
            + "command in game and this fills in.",
        GoalGapReason.NothingLeftToDo =>
            $"{GoalLabel(gap.Goal)}: nothing left here — everything you picked is finished.",
        GoalGapReason.NoPlayHistory =>
            $"{GoalLabel(gap.Goal)}: EQBuddy has not stored enough of your play to divide yet. "
            + "It needs a sitting of about fifteen minutes in a zone before it will quote a "
            + "rate, because a shorter one measures one lucky pull.",
        _ => "",
    };

    /// <summary>
    /// A goal whose engine is a later slice.
    ///
    /// <para>It names the room that answers the question TODAY, which is the difference
    /// between an honest "not yet" and a dead affordance: the rail's own rule is that an
    /// affordance which opens nothing is a trap, and a chip that produced one apologetic
    /// sentence and pointed nowhere would be that rule broken one level in.</para>
    /// </summary>
    public static string NotAnsweredYet(HelperGoal goal) => goal switch
    {
        HelperGoal.FarmGear =>
            "Farm Gear: EQBuddy is not ranking this one yet. Your wishlist, your bags and "
            + "what has dropped for you are in Gear meanwhile.",
        HelperGoal.FarmMotes =>
            "Farm Motes: EQBuddy is not ranking this one yet. Your mote totals are under "
            + "Progress → Wealth meanwhile.",
        HelperGoal.MakeMoney =>
            "Make Money: EQBuddy is not ranking this one yet. What your sessions have earned "
            + "is under Progress → Wealth meanwhile.",
        HelperGoal.FarmMaterials =>
            "Farm Materials: EQBuddy is not ranking this one yet. What is in your bags is in "
            + "Gear meanwhile.",
        HelperGoal.Achievements =>
            "Achievements: EQBuddy is not ranking this one yet. What the game's dump says is "
            + "on the Guide room's Unlocks tab meanwhile.",
        _ => "",
    };

    /// <summary>Where a not-yet goal's door leads — the room that answers its question
    /// today. Null for a goal that has an engine, which is what keeps the pairing from
    /// drifting: the sentence above and this door are read together or neither is.</summary>
    public static HelperDoorKind? NotAnsweredDoor(HelperGoal goal) => goal switch
    {
        HelperGoal.FarmGear => HelperDoorKind.Gear,
        HelperGoal.FarmMotes => HelperDoorKind.Wealth,
        HelperGoal.MakeMoney => HelperDoorKind.Wealth,
        HelperGoal.FarmMaterials => HelperDoorKind.Gear,
        HelperGoal.Achievements => HelperDoorKind.Unlocks,
        _ => null,
    };

    // ---- the faction sub-picker ----------------------------------------------------------

    /// <summary>Over the faction picker. It says why the list is short: a dump carries
    /// hundreds of standings and recommending against all of them is thirty weak answers,
    /// which is what HOME-002 asks for the opposite of.</summary>
    public const string FactionPickerNote =
        "Which factions are you working on? EQBuddy weighs the ones you pick.";

    /// <summary>The picker's own empty state — the dump has never been read.</summary>
    public const string FactionPickerNoDump =
        "No faction dump yet, so there is nothing to pick from.";

    /// <summary>One faction chip: the name and how far there is to go, which is what makes
    /// the list pickable rather than alphabetical.</summary>
    public static string FactionChip(FactionsFile.Standing standing) =>
        standing.Maxed
            ? $"{standing.Name} — at the top"
            : $"{standing.Name} — {standing.PointsToMax:N0} to go";

    /// <summary>How many factions the picker offers before it stops. The dump is long and a
    /// picker is not a browser — Progress → Faction is where every standing lives, and the
    /// door under the picker says so.</summary>
    public const int FactionPickerCap = 12;

    /// <summary>Said when the picker held standings back. Trap 50 again, one surface
    /// down.</summary>
    public static string FactionPickerCapNote(int withheld) => withheld <= 0
        ? ""
        : $"{withheld} more {(withheld == 1 ? "faction" : "factions")} in your dump. "
          + "Progress → Faction has every one of them.";

    // ---- doors ----------------------------------------------------------------------------

    /// <summary>
    /// **THE ONE ADDRESS**, resolved for a door — <c>page:room</c>, the grammar the rail, the
    /// Ctrl+K palette, the widget's Guide… row and <c>EQBUDDY_SHELL</c> already share.
    ///
    /// <para>Null for a door that is not a room: <see cref="HelperDoorKind.WikiFaction"/>
    /// opens a browser, and a caller that treated null as "no door" would silently drop it.
    /// Both arms are exercised by the tests for that reason.</para>
    ///
    /// <para>Each address is filtered through <see cref="ShellPages.Landed"/> by the room
    /// that draws it — the same list the rail is built from — so a door can never offer a
    /// room that does not exist.</para>
    /// </summary>
    public static string? AddressFor(HelperDoorKind kind) => kind switch
    {
        HelperDoorKind.World => ShellPages.Address(ShellPage.World, WorldSurface.KeyFor(WorldTab.Map)),
        HelperDoorKind.Unlocks => ShellPages.Address(ShellPage.Quests, QuestSurface.KeyFor(QuestTab.Unlocks)),
        HelperDoorKind.SkyRewards => ShellPages.Address(ShellPage.Quests, QuestSurface.KeyFor(QuestTab.Sky)),
        HelperDoorKind.QuestCatalog => ShellPages.Address(ShellPage.Quests, QuestSurface.KeyFor(QuestTab.General)),
        HelperDoorKind.FactionStandings => ShellPages.Address(ShellPage.Progress, ProgressSurface.KeyFor(ProgressTab.Faction)),
        HelperDoorKind.Gear => ShellPages.Address(ShellPage.Gear),
        HelperDoorKind.Wealth => ShellPages.Address(ShellPage.Progress, ProgressSurface.KeyFor(ProgressTab.Wealth)),
        _ => null,
    };

    /// <summary>The door's own words. Short, because it sits on a row.</summary>
    public static string DoorLabel(HelperDoorKind kind) => kind switch
    {
        HelperDoorKind.World => "Map",
        HelperDoorKind.Unlocks => "Unlocks",
        HelperDoorKind.SkyRewards => "Plane of Sky",
        HelperDoorKind.QuestCatalog => "Quests",
        HelperDoorKind.FactionStandings => "Standings",
        HelperDoorKind.WikiFaction => "eqlwiki",
        HelperDoorKind.Gear => "Gear",
        HelperDoorKind.Wealth => "Wealth",
        _ => "",
    };

    /// <summary>The door's tooltip — what opens, and for the wiki one, what EQBuddy does
    /// NOT do. The request policy toward eqlwiki is the player's click and nothing
    /// else.</summary>
    public static string DoorTip(HelperDoor door) => door.Kind switch
    {
        HelperDoorKind.World =>
            door.Target.Length > 0
                ? $"Open the World room. Its map follows the zone you are in — {door.Target} "
                  + "when you get there."
                : "Open the World room: the map, your camps and how to get there.",
        HelperDoorKind.Unlocks =>
            "Open the Guide room's Unlocks tab, where every race and class unlock stands.",
        HelperDoorKind.SkyRewards =>
            "Open the Plane of Sky tab — its pieces, where they drop and who takes the "
            + "turn-in.",
        HelperDoorKind.QuestCatalog => "Open this quest on the Guide room's quest list.",
        HelperDoorKind.FactionStandings =>
            "Open Progress → Faction, where every standing in your dump is listed.",
        HelperDoorKind.WikiFaction =>
            "Open this faction's page on eqlwiki — where to raise it is the wiki's answer, "
            + "and you open the page yourself. EQBuddy never fetches it for you.",
        HelperDoorKind.Gear => "Open the Gear room: your bags, your wishlist and what dropped.",
        HelperDoorKind.Wealth => "Open Progress → Wealth: coin, motes and what you sold.",
        _ => "",
    };

    // ---- number shapes -------------------------------------------------------------------

    /// <summary>A fight length a player would recognise. Seconds under a minute, because
    /// "0.8 minutes" is a number nobody has ever said out loud about a pull.</summary>
    private static string Seconds(double seconds) => seconds < 60
        ? $"{seconds:0} sec"
        : $"{seconds / 60:0.0} min";

    /// <summary>Time played, rounded the way a person would say it.</summary>
    private static string Hours(double hours) => hours < 1
        ? $"{hours * 60:0} minutes"
        : $"{hours:0.0} hours";

    private static string Count(int n, string one, string many) =>
        $"{n:N0} {(n == 1 ? one : many)}";
}
