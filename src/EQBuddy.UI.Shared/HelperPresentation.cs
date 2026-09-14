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

    // ---- the goal picker's face (DRA-71 D2) ---------------------------------------------

    /// <summary>
    /// How wide the goal and faction faces may get before they count instead of listing.
    ///
    /// <para><see cref="PickerFace.MaxChars"/> is 16 because the quest window's class face
    /// SHARES its row with the era combo, the state combo and the mode strip — #184 was that
    /// row running out of width. These faces own their own row in a column of
    /// <c>ShellLayoutPolicy.MinRoomWidth</c>, so the budget is raised to hold the two longest
    /// goal names together ("Work on Faction · Farm Materials" is 32) and no further. Three
    /// goals can reach 49 and are counted, which is the cap doing its job rather than failing
    /// at it.</para>
    /// </summary>
    public const int FaceChars = 34;

    /// <summary>Hover copy on the goals face. It says what the control IS, because a button
    /// reading "Any goal" gives a player no reason to suspect nine rows are behind it.</summary>
    public const string GoalPickerTip =
        "Pick what you are working toward — any number of them.";

    /// <summary>What the goals face reads. Empty is "Any goal", which is the same sentence
    /// <see cref="GoalStripNote"/> makes above it in the control's own words: nothing picked
    /// means EQBuddy weighs all nine.</summary>
    public static string GoalFace(IReadOnlyList<HelperGoal> picked) => PickerFace.For(
        [.. picked.Select(GoalLabel)], "goal", "goals",
        offered: Recommendations.All.Count, maxChars: FaceChars);

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
        // A hand-in rather than a camp (DRA-71 D6). The suffix is what stops a quest title
        // from reading as somewhere to travel to, which is the same job the two above it do.
        RecommendationKind.Quest => $"{r.Subject} — quest",
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

        // The cadence, and — since DRA-71 D4 — what it is against YOUR OWN average when
        // there is more than one zone to have averaged. The comparison clause is silent at a
        // baseline of 0 (one measured zone, so the comparison would be a zone against
        // itself) and silent when the two round to the same number, because "they run 41 sec;
        // you average 41 sec" is a sentence that spends a line to say nothing.
        ZoneCadenceFact f =>
            $"Your fights here run {Seconds(f.AvgFightSeconds)} on average, over "
            + $"{f.Kills:N0} {(f.Kills == 1 ? "kill" : "kills")} you have recorded."
            + (f.BaselineSeconds > 0 && Seconds(f.BaselineSeconds) != Seconds(f.AvgFightSeconds)
                ? $" Everywhere EQBuddy has measured you, they run {Seconds(f.BaselineSeconds)}."
                : ""),

        // **The throughput line** (DRA-71 D4, plan P7; Founder smoke item 3). Three
        // measurements and no verdict: what you put out, how long you were fighting, and what
        // you put out everywhere else. No sentence here says the zone is a good or a bad match
        // — a player reading "22.4 here against your usual 61.8" has the whole finding, and
        // EQBuddy has no mob-HP model with which to draw a conclusion from it.
        ZoneThroughputFact f =>
            $"You put out {f.Dps:0.0} damage a second here, over "
            // HOURS and not the fight-length shape. This is a pooled all-time figure — every
            // session in the zone added together — so it is the same kind of number the
            // experience rate's scope is, and "70.0 min of fighting" is a way of saying 1.2
            // hours that nobody has ever said out loud.
            + $"{Hours(f.CombatSeconds / 3600)} of fighting."
            + (HealingWorthSaying(f) ? $" You healed {f.Hps:0.0} a second." : "")
            + (BaselineWorthSaying(f)
                ? $" Across the {f.Zones:N0} zones EQBuddy has measured, your damage and "
                  + $"healing together run {f.BaselineOutput:0.0} a second; here, {f.Output:0.0}."
                : ""),

        // **The downtime line.** It says WHAT was measured and never why: the active figure
        // counts two-minute stretches that contained an event, so medding, travelling, a bank
        // trip and a corpse run are one thing to it. Naming a cause would be inventing the
        // half the log did not record (trap 73).
        ZoneDowntimeFact f =>
            $"Across {Count(f.Sessions, "session", "sessions")} here ({Hours(f.Hours)}), "
            + $"{f.Share * 100:0}% of the time had nothing happening in it.",

        // **The tier line.** The game's own difficulty word, from the player's own zone
        // line — which is why it reads "your own zone line recorded" and not "this zone is".
        // EQBuddy did not look it up and does not rank on it.
        ZoneTierFact f =>
            $"Your own zone line recorded this as a {InstanceTier.Badge(f.Tier)} instance.",

        // HOME-006's ONLY survival-adjacent sentence, and it reports rather than advises.
        // There is no arm for zero deaths — see this class's summary.
        ZoneDeathsFact f =>
            $"You have died here {Count(f.Deaths, "time", "times")}, across "
            + $"{Count(f.Sessions, "session", "sessions")}.",

        // **The P6 sentence** (DRA-71 D3). Two measured numbers and a dash between them,
        // and no verdict on either side of it: it does not say the zone is finished with,
        // does not say it is easy, and does not predict what the next hour there would pay.
        // A player who reads "L8-12, you are 30" has everything they need to decide, and
        // EQBuddy has no XP curve with which to decide it for them.
        ZoneOutgrownFact f =>
            $"The creatures you conned here ran L{f.ConnedMin}–{f.ConnedMax}, "
            + $"across {Count(f.Kills, "kill", "kills")} — you are level {f.Level}.",

        // **THE FARM GEAR LINE** (DRA-71 D6, plan P8). It names what it beats, always, because
        // an upgrade with no anchor is a claim about the GAME — "this is the best helm" — and
        // that is the line the Gear Locker's "never BiS" lock draws. The catalog label is
        // appended by Why() above, so this sentence can never read as a measurement of play.
        //
        // The WHO clause is silent where the wiki named nobody, which is every row until the
        // weekly refresh rebuilds the catalog with DropMobs in it. An unanswered question
        // draws nothing (trap 73) — and where the player's OWN kills answered it, the fact
        // beside this one carries it instead and this clause is empty by construction.
        GearUpgradeFact f =>
            $"{f.Item} beats the {f.Over} in your {Slot(f.Slot)} — {Gain(f)}."
            + (f.Who.Length > 0 ? $" {f.Who} drops it." : ""),

        // The personal half: measured, with its denominator, and the creature named from your
        // own pooled kills rather than from a page.
        GearDropSeenFact f =>
            $"You have seen {f.Item} drop from {f.Mob} in {f.Zone} — "
            + $"{f.Drops:N0} of your {f.Kills:N0} {(f.Kills == 1 ? "kill" : "kills")} there.",

        // **THE MOTE RATE** (DRA-71 D7, plan P10; Founder smoke item 5). Potency first, because
        // a hundred Infinitesimal motes and a hundred Infinite motes are not the same hour
        // (#154) — and the COUNT beside it, because a player one mote short of an upgrade is
        // counting motes. The scope is the experience rate's own wording for its own reason:
        // the hours are attributed by the session's main zone.
        ZoneMoteRateFact f =>
            $"{f.PotencyPerHour:0.0} mote experience an hour here — {f.MotesPerHour:0.0} motes "
            + $"an hour, {Count(f.Motes, "mote", "motes")} in all, across "
            + $"{Count(f.Sessions, "session", "sessions")} ({Hours(f.Hours)})."
            // The raid mote the ladder gives no number to. The clause exists because the
            // potency figure CANNOT see it: a zone whose only motes were Void-Touched reads as
            // "0.0 an hour", and stopping there would tell a player their raid night paid
            // nothing. The wiki publishes no experience value for it and none is invented here.
            + (f.VoidTouched > 0
                ? $" {Count(f.VoidTouched, "of them was", "of them were")} "
                  + $"{Motes.VoidTouched}, which raises an item a whole tier instead of "
                  + "carrying experience — so it counts above and weighs nothing in the rate."
                : ""),

        // WHO, measured, with its denominator — the same grammar the gear rows' observed-drop
        // line uses, because it is the same claim about the same pool.
        //
        // **The first staged shot of this slice is why the pronouns are gone** (trap 23). The
        // take read "Shadowed man gave you 8 motes of it (40 experience) across your 50 kills
        // of it" — two "it"s in one sentence pointing at different things, and the first one
        // pointing at nothing at all. The assertion passed; the sentence was unreadable.
        MoteSourceFact f =>
            $"{f.Mob} gave you {Count(f.Motes, "mote", "motes")} ({f.Potency:N0} experience) "
            + $"in {Count(f.Kills, "kill", "kills")} of it.",

        // The cadence, against the player's own pooled rate. Drawn only where the discount
        // fired, so the numbers in it are always the ones that moved the order.
        ZoneKillRateFact f =>
            $"You kill {f.KillsPerHour:0.0} things an hour here, over {f.Kills:N0} "
            + $"{(f.Kills == 1 ? "kill" : "kills")}. Across the {f.Zones:N0} zones motes have "
            + $"dropped for you, you average {f.BaselineKillsPerHour:0.0} an hour.",

        // **The tier preference.** It reports the game's own word and the band it is outside,
        // and stops: no sentence here says a D1 is easy or a D4 is hard, which is HOME-006 and
        // also simply what EQBuddy knows. "You said" is not in it either — the band came from
        // the plan, so the sentence attributes it to EQBuddy's own preference rather than
        // quoting the player back at themselves.
        ZoneTierPreferenceFact f =>
            $"Your own zone line recorded this as a {InstanceTier.Badge(f.Tier)} instance. "
            + $"EQBuddy ranks motes toward D{f.PreferredMin}–D{f.PreferredMax}, so this one "
            + "sits lower than its rate alone would put it.",

        // **THE COIN RATE** (DRA-71 D7, plan P9). The experience rate's twin, down to the
        // scope clause, because it is the same division over the same rows — and the coin is
        // formatted by the one formatter every other surface uses.
        ZoneCoinRateFact f =>
            f.Sessions == 1
                ? $"{StatsSnapshot.FormatCoin((long)f.CopperPerHour)} an hour here, from 1 "
                  + $"stored session ({Hours(f.Hours)})."
                : $"{StatsSnapshot.FormatCoin((long)f.CopperPerHour)} an hour here, across "
                  + $"{f.Sessions:N0} of your sessions ({Hours(f.Hours)}).",

        // Both halves measured: what dropped, from what, how often — and what a vendor paid
        // YOU for one. No sentence in this file quotes a price somebody else was given without
        // saying so, which is the whole of what the catalog arm below is careful about.
        // The same pronoun lesson as the mote line above it: the take read "Bone Chips drops
        // here from Shadowed man — 3 of your 50 kills of it", where the 50 belongs to the
        // CREATURE and reads as if it were kills of the item. The subject is now the player, as
        // it is in every other personal line, and the denominator is attached to the creature
        // it actually counts.
        SellableDropFact f =>
            $"You have looted {f.Item} here from {f.Mob} — {f.Drops:N0} in "
            + $"{Count(f.Kills, "kill", "kills")} of it — and a vendor has paid you "
            + $"{StatsSnapshot.FormatCoin(f.CopperEach)} each for them.",

        // **The catalog price, with the condition it was quoted at.** The condition is NOT
        // optional decoration: a vendor price in EQ moves with your Charisma and your faction,
        // and the wiki says so on the pages that carry one. Where the page stated none, the
        // sentence says what it does know and stops — an unanswered question draws nothing
        // (trap 73) rather than a caveat this file made up. `Why` appends the estimate label to
        // this line by construction, because the fact is tagged Catalog.
        CatalogValueFact f =>
            $"EQBuddy has read that a vendor pays {StatsSnapshot.FormatCoin(f.Copper)} for "
            + $"{f.Item}."
            + (f.Condition.Length > 0
                ? $" The page quotes that as \"{f.Condition}\" — a vendor's price moves with "
                  + "your Charisma and your faction, so yours will differ."
                : ""),

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

    // ---- the two clauses the STAGED SHOT caught (DRA-71 D4) --------------------------------

    /// <summary>
    /// How much of a character's output has to be healing before the sentence mentions it.
    ///
    /// <para><b>The first staged shot of this slice is why this constant exists</b>, and it is
    /// trap 23 doing its job. The arm was <c>Hps &gt; 0</c>, which is the obvious reading of
    /// "only when there was some" — and the fixture's warrior came back saying *"You healed 0.1
    /// a second"*, because a log with regen ticks and a bandage in it is not a log with zero
    /// healing. A trace is not a contribution, and a clause reporting one reads as a defect on
    /// a character who does not heal: exactly the furniture the <c>0.0</c> guard was written to
    /// avoid, arriving one decimal place up. No assertion in the repo could have seen it — the
    /// sentence was correct, the number was real, and the shape was right.</para>
    ///
    /// <para>A twentieth of the output. <b>The WEIGHT is untouched by this</b>: every point
    /// healed still counts toward <see cref="ZoneRoll.OutputPerSecond"/>, because it was
    /// measured and it is the player's own contribution. This decides only whether a clause is
    /// worth a line, which is a question about language and therefore this file's.</para>
    /// </summary>
    public const double HealingClauseShare = 0.05;

    /// <summary>
    /// How far a zone's output has to sit from the player's own pooled figure before the
    /// sentence draws the comparison.
    ///
    /// <para><b>Also found by the staged shot.</b> The first take read *"your damage and
    /// healing together run 13.2 a second; here, 13.4"* — a clause spending a whole line to
    /// say a zone is exactly average, on the row where it is least interesting. It is the
    /// same lesson the cadence clause already carried (it goes silent when the two round to
    /// the same words) and the same one the downtime line is built on: a line that never
    /// varies tells a player nothing, and the primary figure is on screen either way.</para>
    ///
    /// <para>A tenth, either side. <b>It is deliberately far tighter than
    /// <see cref="Recommendations.ThroughputShortfall"/></b> (three fifths), so a zone that
    /// takes the discount is always well outside this band and its explanation can never be
    /// the clause that got suppressed — a zone marked down in silence is the one failure this
    /// slice had to refuse, and <c>HelperPresentationTests</c> asserts the two thresholds in
    /// that relationship rather than trusting the two numbers to stay apart.</para>
    /// </summary>
    public const double BaselineClauseGap = 0.10;

    private static bool HealingWorthSaying(ZoneThroughputFact f) =>
        f.Hps > 0 && f.Output > 0 && f.Hps >= f.Output * HealingClauseShare;

    private static bool BaselineWorthSaying(ZoneThroughputFact f) =>
        f.BaselineOutput > 0
        && Math.Abs(f.Output - f.BaselineOutput) >= f.BaselineOutput * BaselineClauseGap;

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

        // ---- DRA-71 D6 ----------------------------------------------------------------
        GoalGapReason.NoInventoryDump =>
            $"{GoalLabel(gap.Goal)}: EQBuddy has not been told what you are wearing. Run the "
            + "inventory command in game and this fills in.",

        // **THE SUBJECT OF THIS SENTENCE IS THE CATALOG AND NOT THE GAME**, and that is the
        // whole of why it is three clauses instead of four words. "Nothing beats what you are
        // wearing" is a best-in-slot claim with a minus sign in front of it, and the Gear
        // Locker has refused to make that claim since #104. What EQBuddy actually knows is
        // what it has read.
        GoalGapReason.NoCatalogUpgrade =>
            $"{GoalLabel(gap.Goal)}: nothing EQBuddy has read about beats what you are "
            + "wearing in these slots. That is a statement about EQBuddy's own catalog rather "
            + "than about the game — an item it has never read about cannot be compared, and "
            + "a \"+N\" on something you wear raises it by an amount the wiki does not state.",

        GoalGapReason.GearIntentNotAnsweredYet =>
            $"{GoalLabel(gap.Goal)}: EQBuddy is not ranking that gear question yet. What your "
            + "own sessions have earned is under Progress → Wealth meanwhile.",

        // ---- DRA-71 D7 ----------------------------------------------------------------

        // **The subject of this sentence is the MOTES and not the zones.** "No good mote camps"
        // would be a claim about the game; what EQBuddy knows is that nothing it has stored
        // contains one. There is no command that fixes it and no catalog behind it — the eleven
        // mote records in the shipped catalog name no real zone — so the sentence says what
        // would fill it and offers nothing it cannot do.
        GoalGapReason.NoMotesSeen =>
            $"{GoalLabel(gap.Goal)}: no mote has dropped in a zone EQBuddy has enough of your "
            + "play stored for. It answers this one from where motes have actually dropped for "
            + "you — the item pages it ships say only \"Various Zones\", which is not somewhere "
            + "you can go.",

        GoalGapReason.NoCoinEarned =>
            $"{GoalLabel(gap.Goal)}: your stored sessions have not earned coin in a zone "
            + "EQBuddy can quote a rate for yet.",

        GoalGapReason.NoSellEvidence =>
            $"{GoalLabel(gap.Goal)}: EQBuddy prices a drop by what a vendor has actually paid "
            + "YOU for one, and it has not seen a sale yet. Its own item pages carry vendor "
            + "values quoted at somebody else's Charisma and faction, so they are a fallback "
            + "rather than the answer — and this build's catalog does not carry them yet.",

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
        // Farm Gear LEFT this switch in DRA-71 D6 — its engine landed, and the pairing test
        // beside it (EveryDeferredGoalNamesTheRoomThatAnswersItToday) is what would have
        // caught a sentence left behind. Its one unanswered INTENT says so in its own place,
        // through GoalGapReason.GearIntentNotAnsweredYet.
        // Farm Motes and Make Money LEFT this switch in DRA-71 D7 — their engines landed, and
        // the pairing test beside it (EveryDeferredGoalNamesTheRoomThatAnswersItToday) is what
        // would have caught a sentence left behind. Farm Gear left the same way in D6.
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

    /// <summary>Hover copy on the faction face, same job as <see cref="GoalPickerTip"/>.</summary>
    public const string FactionPickerTip =
        "Pick the factions you are working on — any number of them.";

    /// <summary>
    /// What the faction face reads.
    ///
    /// <para><b>It is never told how many are offered</b>, so it never says "All factions".
    /// The list is capped at <see cref="FactionPickerCap"/> and a player who ticked every row
    /// on screen has not picked every standing in their dump — a face claiming otherwise would
    /// contradict the cap note printed directly under it (trap 50 is about saying what was
    /// withheld; this is about not un-saying it one control up).</para>
    /// </summary>
    public static string FactionFace(IReadOnlyList<string> picked) =>
        PickerFace.For(picked, "faction", "factions", maxChars: FaceChars);

    /// <summary>One faction row: the name and how far there is to go, which is what makes
    /// the list pickable rather than alphabetical.</summary>
    public static string FactionChip(FactionsFile.Standing standing) =>
        standing.Maxed
            ? $"{standing.Name} — at the top"
            : $"{standing.Name} — {standing.PointsToMax:N0} to go";

    /// <summary>How many factions the picker offers before it stops. The dump is long and a
    /// picker is not a browser — Progress → Faction is where every standing lives, and the
    /// door under the picker says so.</summary>
    public const int FactionPickerCap = 12;

    /// <summary>
    /// The heading over the unlock sub-picker (DRA-71 D5).
    ///
    /// <para>It names BOTH goals rather than either, because one picker serves them: the
    /// faction block above it can borrow its goal's own label, and this one cannot without
    /// claiming to be about only half of what it offers. The picker's own words — its note,
    /// its tip, its rows and its face — come from <see cref="UnlockPickReadout"/>, which the
    /// Quests window reads too; only this heading is the Helper's, because only the Helper has
    /// blocks to head.</para>
    /// </summary>
    public const string UnlockPickerHeading = "Races and classes you are unlocking";

    // ---- the gear intent strip and its picker (DRA-71 D6) ---------------------------------

    /// <summary>
    /// The intent strip's own sentence. It says the strip is a QUESTION rather than a filter,
    /// which is the one thing about it that differs from every other control in this room —
    /// the goals above it are multi-select and these three are not.
    /// </summary>
    public const string GearIntentNote =
        "What are you asking about gear? One at a time — these are different questions, not "
        + "filters.";

    /// <summary>
    /// The segment labels — <b>the Founder's own three, verbatim</b> (smoke items 4a/4b/4c).
    ///
    /// <para>Kept as he wrote them for <see cref="GoalLabel"/>'s reason: this is the list the
    /// person who asked for the feature typed out, and making the three parallel would be a
    /// design opinion about a decision that was already made.</para>
    /// </summary>
    public static string GearIntentLabel(GearIntent intent) => intent switch
    {
        GearIntent.UpgradeWorn => "Upgrade what I wear",
        GearIntent.ReplaceSlot => "Replace with better",
        GearIntent.FarmToSell => "Farm to sell",
        _ => "",
    };

    /// <summary>What each intent DOES, on hover. It names the anchor, because the difference
    /// between the first two is exactly which thing they are answering about, and a player
    /// who cannot tell them apart will read the second as a duplicate of the first.</summary>
    public static string GearIntentTip(GearIntent intent) => intent switch
    {
        GearIntent.UpgradeWorn =>
            "Pick the items you want to improve, and EQBuddy names catalog items that beat "
            + "them and where they drop.",
        GearIntent.ReplaceSlot =>
            "The same comparison across every slot you have something in — no picking, and "
            + "EQBuddy sorts the places by how many of your slots they can improve.",
        // DRA-71 D7. It names the ANCHOR, as the two above it do, and the anchor is the whole
        // difference: this one never looks at what you are wearing.
        GearIntent.FarmToSell =>
            "Where your own drops are worth the most, priced at what a vendor has actually "
            + "paid you for them. It does not look at what you are wearing.",
        _ => "",
    };

    /// <summary>
    /// **THE SENTENCE THAT KEEPS THIS OFF THE "BEST IN SLOT" SIDE OF THE LINE**, printed
    /// under the answers rather than buried in a tooltip.
    ///
    /// <para>The Gear Locker has said since #104 that it compares your bags and never the
    /// game. This room compares the shipped catalog — knowingly, under a Helm-signed plan —
    /// and the player is owed the same honesty the Locker gives them: what EQBuddy has read
    /// about is not what exists, base numbers are not the numbers on a "+N", and nothing here
    /// is a claim that an item is the best one.</para>
    /// </summary>
    public const string GearCatalogNote =
        "Gear answers come from the item pages EQBuddy ships, compared against what you are "
        + "wearing. They are never a \"best in slot\": EQBuddy can only compare what it has "
        + "read about, and the numbers are the wiki's base values — a \"+N\" raises an item "
        + "in game by an amount the page does not state.";

    /// <summary>Over the worn-item picker, and only ever drawn for the intent that has
    /// one.</summary>
    public const string WornPickerNote =
        "Which of the things you are wearing do you want to improve? Nothing picked means all "
        + "of them.";

    /// <summary>Hover copy on the worn face, same job as <see cref="GoalPickerTip"/>.</summary>
    public const string WornPickerTip =
        "Pick the worn items you are trying to upgrade — any number of them.";

    /// <summary>The picker's own empty state — no inventory dump has ever been read, so there
    /// is nothing to pick from. The command rides beside it in the room
    /// (<c>GameCommands.OutputfileInventory</c>), never as a literal here.</summary>
    public const string WornPickerNoDump =
        "No inventory dump yet, so EQBuddy does not know what you are wearing.";

    /// <summary>What the worn face reads. Told how many are OFFERED, unlike the faction face:
    /// the list is every worn item and is not capped, so "Any worn item" is true.</summary>
    public static string WornFace(IReadOnlyList<string> picked, int offered) =>
        PickerFace.For(picked, "worn item", "worn items", offered: offered, maxChars: FaceChars);

    /// <summary>One worn row: the item and the slot it is in, because a character wearing two
    /// rings needs to be able to tell which row is which.</summary>
    public static string WornRow(WornItem item) => $"{item.Name} — {Slot(item.Slot)}";

    /// <summary>The include-quests toggle's label — the Founder's own "± quests".</summary>
    public const string IncludeQuestsLabel = "Include quest rewards";

    /// <summary>Why it is a choice rather than a default. Farming and questing are different
    /// evenings, which is the whole reason the toggle exists.</summary>
    public const string IncludeQuestsTip =
        "Off by default: farming a camp and running a quest chain are different evenings. "
        + "Turn it on and items a quest hands out are offered too, each with its quest named.";

    /// <summary>
    /// **THE SENTENCE UNDER A MONEY ANSWER**, and it is the twin of
    /// <see cref="GearCatalogNote"/> above (DRA-71 D7, plan P9).
    ///
    /// <para>The Gear note exists because the catalog's stats are base values. This one exists
    /// because the catalog's PRICES are worse than that: the survey of the cached item pages
    /// found 262 of the 975 that state a vendor value heading it "VALUE TO VENDOR with CHA :
    /// 80 and faction at Indifferently", at a Charisma that differs per page. So a vendor
    /// price is not a property of an item at all, and the player is owed that in the room
    /// rather than in a tooltip — especially since the sentence explains why EQBuddy leads
    /// with what THEY were paid.</para>
    /// </summary>
    public const string MoneyPriceNote =
        "Money answers are priced from what a vendor has actually paid you. EQBuddy falls back "
        + "to the price on the item pages it ships only where you have never sold one — and "
        + "those are quoted at a particular Charisma and faction standing, so they are an "
        + "estimate and the page's own conditions are printed with them.";

    /// <summary>Said when the sweep's per-anchor cap held upgrades back — the one count that
    /// cannot ride a row, because it is spent before any row exists (trap 50). The door under
    /// it is the Gear room, which has the whole list.</summary>
    public static string GearWithheld(int withheld) => withheld <= 0
        ? ""
        : $"{withheld:N0} more {(withheld == 1 ? "upgrade" : "upgrades")} matched and are not "
          + "listed — EQBuddy names a few per slot rather than every one it has read about.";

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
        // The room a player reads as "Character" — the page key stayed `Home` when DRA-66
        // relabelled it, the same discipline the Guide room's `quests` key keeps.
        HelperDoorKind.Character => ShellPages.Address(ShellPage.Home),
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
        HelperDoorKind.Character => "Character",
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
        HelperDoorKind.Character =>
            "Open the Character room, where you can tell EQBuddy what level this character "
            + "is. It reads the level from your log when you ding, and until then it has "
            + "nothing to weigh against.",
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

    /// <summary>A slot the way a person says it. The stats block prints SHOULDERS and the
    /// sentence wants shoulders; nothing else is changed, so a slot word this build does not
    /// know still reads as itself rather than as a blank.</summary>
    private static string Slot(string slot) => slot.ToLowerInvariant();

    /// <summary>
    /// The improvement, as a signed number against the metric's own name.
    ///
    /// <para>Weapon ratio is the one that needs a decimal — it is damage per point of delay
    /// and "+1 ratio" would round a 0.77→0.92 swap into a lie. Everything else in the block is
    /// an integer, and printing "+12.0 AC" reads as a precision the wiki never claimed.</para>
    /// </summary>
    private static string Gain(GearUpgradeFact f) =>
        f.GainMetric.Equals("ratio", StringComparison.OrdinalIgnoreCase)
            ? $"{f.GainBy:+0.00;-0.00} ratio"
            : $"{f.GainBy:+#;-#;0} {f.GainMetric}";
}
