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

    // ---- what a surface that cannot OPERATE this room says instead (DRA-71 D9) ----------
    //
    // EQBuddy Mobile ranks with the same engine and draws the same sentences, and it has
    // none of the room's controls. That is trap 35's answer rather than a shortfall: every
    // picker here WRITES to the profile the PC is playing from, and one of the doors has a
    // side effect behind it, so a tap on a phone would reach across the LAN and change how
    // the PC ranks while somebody is playing at it. The affordance ports as INTENT — the
    // phone shows what is picked, in the picker face's own words, and says where it is
    // changed. Both leads live here rather than in the page, because a page-side literal can
    // sit on an open phone for weeks after the PC has moved on (trap 32).

    /// <summary>Over the block of picks, on a surface that can only show them. It names the
    /// ROOM as well as the machine: "on your PC" alone is the defect one level down, the same
    /// one <see cref="CommandPrompts.Lead"/> exists to avoid.</summary>
    public const string PicksOnPc =
        "What you are working toward, picked in EQBuddy's Helper room on your PC.";

    /// <summary>Over a row's doors, on a surface that cannot open them. Short, because it
    /// repeats under every answer.</summary>
    public const string DoorsOnPc = "On your PC, in EQBuddy:";

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
        // **THE WHO CLAUSE IS PLURAL SINCE DRA-84 D4** (plan P3). It is empty in exactly two
        // states and neither is a guess: the player's OWN kills answered, so the fact beside
        // this one carries it instead (trap 4), or this is a QUEST row, where the quest is the
        // path and no creature drops the thing at all. A zone row that could name nobody is no
        // longer drawn — see GearWhoWithheld, which counts them out loud.
        //
        // **THE CLAIM IS BASE-vs-BASE SINCE DRA-149 D1** (plan P1). It used to read "beats the
        // X in your Y", which the tier rule made true — and which that rule also made
        // unreachable, since no catalog name carries a "+N" and so nothing was ever offered
        // against a plussed item at all. The sweep now compares base numbers, so the sentence
        // says base numbers. The "+N" caveat is said ONCE under the block
        // (GearBaseClaimNote) and never templated onto the row: eight rows repeating one
        // caveat is the distinct-count tell trap 73 is about, and it would bury the one thing
        // each row is actually for.
        // **THE PROC CLAUSE IS A REPORT, AND ITS GRAMMAR IS THE RULING** (DRA-241, Helm ruling
        // 27302878). It is its own sentence AFTER the comparison rather than another clause
        // inside it, because everything before that full stop is something EQBuddy MEASURED and
        // this is something the item's page SAYS. "+12 AC and it procs Ykesha" would read as a
        // second entry on one list of improvements — which is pricing it, at a weight the reader
        // supplies. "It procs Ykesha." reads as what it is.
        //
        // No adverb and no conjunction: not "also", not "and it even", not "with a Ykesha proc
        // on top". Every one of those is a valuation, and the one thing this sentence may not do
        // is imply how much the proc is worth. The caveat saying EQBuddy cannot is under the
        // block, once (GearProcNote) — never templated onto the row, for the reason the "+N"
        // caveat above it is not (trap 73).
        GearUpgradeFact f =>
            $"{f.Item} is a better base item than the {f.Over} in your {Slot(f.Slot)} — "
            + $"{Gain(f)}."
            + Who(f.Who, f.WhoWithheld)
            + (f.Proc.Length > 0 ? $" It procs {f.Proc}." : ""),

        // **THE FARM MATERIALS LINE** (DRA-149 D3, plan P4; the Founder's FAIL item 3a).
        //
        // It names the PROFESSION first, because the player picked professions and a row that
        // said only "Amber drops here" would leave them to remember which of eight needs amber.
        // The recipe is the EVIDENCE for the association and it is the page's own line, quoted
        // rather than re-phrased: "the page lists this under Jewelcrafting, for <recipe>" is
        // checkable, "amber is a jewelcrafting material" is a claim. What the page listed
        // BEYOND the first recipe is counted rather than listed — the row is about where to go,
        // not a recipe book, and the cap says so (trap 50).
        //
        // The who clause is the gear row's own, from the same helper: the player's own kills
        // answered instead (the fact beside this one), or the page named somebody. A zone row
        // that could name nobody is not drawn at all.
        TradeskillMaterialFact f =>
            $"{f.Item} — {Tradeskills.For(f.Skill).Name}"
            + (f.Recipe.Length > 0 ? $", for {f.Recipe}" : "")
            + (f.OtherRecipes > 0
                ? $" and {f.OtherRecipes:N0} more {(f.OtherRecipes == 1 ? "recipe" : "recipes")} on its page"
                : "")
            + "."
            + Who(f.Who, f.WhoWithheld),

        // **THE QUEST ROW'S SIX-QUESTION LINE** (DRA-219, S11; acceptance S25 AC 1–6).
        //
        // A drop row has answered who/where/when/how since DRA-84 D4; a quest row answered a
        // name. This is the other half, and every clause in it is CONDITIONAL because the
        // catalog's fields are (trap 73): a quest whose page named no giver gets no "from"
        // clause rather than "from an unknown NPC", and a MinLevel of 0 means the page stated no
        // level, which is not level 1.
        //
        // The order is the player's own reading order — who, where, when, then what it takes —
        // and the sentence stops at the first thing the catalog cannot answer rather than
        // padding. `Why` appends the estimate label, because this is a file EQBuddy ships.
        QuestSourceFact f => QuestSource(f),

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

        // ---- DRA-149 D2 ----------------------------------------------------------------
        //
        // **IT MUST NOT ASK FOR THE DUMP AGAIN**, which is the whole reason this is not
        // NoInventoryDump. The dump is there and EQBuddy read every row of it; what it could
        // not do is recognise a single item in it. "Run the inventory command" would send the
        // player round a loop that ends where it started, and the names that explain it are on
        // the caption directly above this line (`UnreadWorn`).
        GoalGapReason.NothingWornIsReadable =>
            $"{GoalLabel(gap.Goal)}: your inventory dump is here, and EQBuddy could not match "
            + "a single worn item in it to an item page it ships — so there is nothing to "
            + "compare against. Another dump will not change that; the names above are what to "
            + "check on eqlwiki.",

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

        // ---- DRA-84 D2 ----------------------------------------------------------------

        // **THE SUBJECT IS THE BANDS AND NOT THE PLAYER.** "Nowhere is right for your level"
        // reads as a verdict on the character; what EQBuddy did was read eqlwiki's own numbers
        // for the places its catalog names and find all of them outside a range it will
        // recommend at. The count and each band arrive under this line (GearBandRefused), so
        // this sentence says WHAT happened and leaves the numbers to the one that has them.
        GoalGapReason.EveryZoneOutsideYourBand =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found upgrades in its catalog and every place they "
            + "drop has a creature level band on eqlwiki that sits outside yours. The bands and "
            + "your level are below — nothing here is a claim about the game, only about which "
            + "zones EQBuddy will put in this list.",

        GoalGapReason.NoSellEvidence =>
            $"{GoalLabel(gap.Goal)}: EQBuddy prices a drop by what a vendor has actually paid "
            + "YOU for one, and it has not seen a sale yet. Its own item pages carry vendor "
            + "values quoted at somebody else's Charisma and faction, so they are a fallback "
            + "rather than the answer — and nothing you loot carries one.",

        // ---- DRA-84 D4 ----------------------------------------------------------------

        // **THE SUBJECT IS THE PAGES AND NOT THE PLACES.** "Those zones have no camps" is a
        // claim about the game; what happened is that every item EQBuddy would have offered is
        // one whose own page names nobody in the zone it drops in, and that this character has
        // never looted one there either. Both halves are in the sentence because both are
        // things a player can change.
        GoalGapReason.NoUpgradeNamesACreature =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found upgrades in its catalog and cannot tell you "
            + "what to kill for any of them — no item page names a creature in the zone it "
            + "drops in, and you have not looted one there. It would rather say that than send "
            + "you to a zone with only a name in hand.",

        // ---- DRA-219 -------------------------------------------------------------------

        // The sibling of the line above it, on the other acquisition path — and the subject is
        // EQBuddy's OWN quest list rather than the game or the wiki. "That quest does not exist"
        // would be false; what happened is that the item pages named quests the shipped quest
        // list does not hold, so a row could print a title and nothing else.
        GoalGapReason.NoUpgradeNamesAQuestPath =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found upgrades its catalog says come from quests, "
            + "and cannot tell you how to run any of them — none of those quests is in the quest "
            + "list EQBuddy ships, so there is no quest giver, no start zone and no step list to "
            + "hand you. It would rather say that than show you a title with nothing behind it.",

        // ---- DRA-149 D3 ----------------------------------------------------------------

        // **THE SUBJECT IS THE PAGES AND NOT THE PROFESSION.** "Nothing to farm for Fletching"
        // is a claim about the game and it is false — a fletcher farms plenty. What happened is
        // that every ingredient EQBuddy read for the picked professions is one whose own page
        // names no place it drops: Fletching's 33 materials carry ZERO drop zones between them,
        // because they are bought, foraged and crafted. The sentence says which of the two it
        // is, and names the other half of the answer rather than stopping at a refusal.
        GoalGapReason.NoMaterialDrops =>
            $"{GoalLabel(gap.Goal)}: EQBuddy read the recipes for the professions you picked "
            + "and none of their ingredients has a page saying where it drops. That is a "
            + "statement about its own item pages rather than about the game — plenty of "
            + "materials are bought, foraged or made, and eqlwiki is where to check which.",

        GoalGapReason.EveryMaterialZoneOutsideYourBand =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found the ingredients your professions need and "
            + "every place they drop has a creature level band on eqlwiki that sits outside "
            + "yours. The bands and your level are below — nothing here is a claim about the "
            + "game, only about which zones EQBuddy will put in this list.",

        GoalGapReason.NoMaterialNamesACreature =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found the ingredients your professions need and "
            + "cannot tell you what to kill for any of them — no item page names a creature in "
            + "the zone it drops in, and you have not looted one there. It would rather say "
            + "that than send you to a zone with only a name in hand.",

        // ---- DRA-180 D2 ----------------------------------------------------------------

        // **THE SUBJECT IS EQLWIKI'S DATING OF THE CONTENT, NOT THE SERVER AND NOT THE PLAYER.**
        // "That content is not in the game yet" is a claim about the server that EQBuddy has no
        // standing to make; what it actually did was read the era banner each place's own wiki
        // page carries and compare it against the one era this repo was told. The eras and the
        // world's own arrive under this line, so the sentence says WHAT happened and leaves the
        // words each page used to the one that quotes them.
        //
        // It says what IS true rather than stopping at a refusal — the upgrades exist, which is
        // the fact the Founder's empty screen destroyed. HOME-006's ban is untouched: nothing
        // here calls anywhere safe, easy or survivable.
        GoalGapReason.EverythingIsLaterThanTheWorld =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found upgrades in its catalog and eqlwiki dates "
            + "every place and quest they come from to content later than the era it has been "
            + "told the world is at. Those upgrades are real — this is about when their content "
            + "opens, and the eras are below. Nothing in reach beats what you are wearing.",

        GoalGapReason.EveryMaterialZoneLaterThanTheWorld =>
            $"{GoalLabel(gap.Goal)}: EQBuddy found the ingredients your professions need and "
            + "eqlwiki dates every place they drop to content later than the era it has been "
            + "told the world is at. The eras are below — that is a statement about the wiki's "
            + "own dating of those zones rather than about the game.",

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
        // DRA-71 D8 reworded this one and did NOT answer it. The professions block above now
        // carries the standings, the watch preset and the wiki door, so the sentence has to
        // say which half is missing — otherwise a player reading "not ranking this one yet"
        // over a block full of their own numbers would think the block was the failure.
        // DRA-149 D3 took Farm Materials OUT of this switch — its engine landed. The pairing
        // test beside it (EveryDeferredGoalNamesTheRoomThatAnswersItToday) is what would have
        // caught a sentence left behind, which is exactly what happened to this one's
        // predecessor: it told players EQBuddy was "not ranking WHERE to farm the materials
        // yet" because "the wiki's item pages hardly ever say which profession an ingredient
        // belongs to" — a true sentence about the CATEGORIES column, printed over a question
        // the RECIPES column answers for 1,276 pages.
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
        // Farm Materials left with its sentence in DRA-149 D3 — the pairing is read together
        // or neither is, so a door left behind here would point at a room for a question this
        // one now answers.
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

    // ---- the profession picker and its standings (DRA-71 D8) ----------------------------

    /// <summary>
    /// Over the profession picker. It says what the list IS and what the pick does, because a
    /// control whose empty state shows everything has to explain that before a player ticks
    /// one row and watches seven disappear.
    /// </summary>
    public const string ProfessionPickerNote =
        "Which professions are you raising? EQBuddy lists the ones you pick, and all eight "
        + "while you have picked none.";

    /// <summary>
    /// **WHERE THE NUMBERS COME FROM, SAID ONCE** (DRA-71 D8).
    ///
    /// <para>It began inside <see cref="ProfessionStanding"/>'s unknown arm, and the first
    /// staged shot of the default state is what took it out: eight professions, none of them
    /// raised yet, and the same thirty-word explanation repeated eight times down the block.
    /// Every copy was correct and the list was a wall — distinct-count is the tell in prose
    /// exactly as it is in data (trap 73), and a fact about where EQBuddy gets its numbers
    /// belongs to the BLOCK rather than to each row in it.</para>
    /// </summary>
    public const string ProfessionLearnNote =
        "EQBuddy reads your standing from the game's own “You have become better at…” "
        + "line, so a profession you raised before it was watching starts again from your next "
        + "skill-up.";

    /// <summary>Hover copy on the profession face, same job as <see cref="GoalPickerTip"/>.</summary>
    public const string ProfessionPickerTip =
        "Pick the professions you want on this list — any number of them.";

    /// <summary>
    /// What the profession face reads.
    ///
    /// <para><b>It IS told how many are offered</b>, unlike the faction face beside it, so it
    /// can say "All professions" and be telling the truth: this list is the whole curated set
    /// of eight and nothing is capped away. The faction face may never make that claim because
    /// its offer is capped — the difference is a fact about the offer, not a style choice.</para>
    /// </summary>
    public static string ProfessionFace(IReadOnlyList<string> picked, int offered) =>
        PickerFace.For(picked, "profession", "professions", maxChars: FaceChars, offered: offered);

    /// <summary>One picker row: the profession and where it stands, so the list is pickable
    /// on evidence rather than alphabetically.</summary>
    public static string ProfessionRow(TradeskillStanding standing)
    {
        var name = Tradeskills.For(standing.Skill).Name;
        return standing.Known ? $"{name} — {standing.Value}" : $"{name} — not seen yet";
    }

    /// <summary>
    /// The standing line under the picker — <b>the whole of what this slice can honestly say
    /// about a profession</b>.
    ///
    /// <para>It reports a number the game printed and the day it printed it, and it predicts
    /// nothing. The unknown arm is the one that matters: it does NOT say zero, because "you
    /// have never raised this" and "you are at 0" are different claims and the second is false
    /// for everybody who crafted before EQBuddy was watching.</para>
    ///
    /// <para><b>And that arm is SHORT, which it was not when it was written.</b> It used to
    /// carry the explanation of where the number comes from, and the first staged shot of the
    /// default state is what took it out — eight professions repeating one thirty-word
    /// sentence down the block. The explanation lives in <see cref="ProfessionLearnNote"/>
    /// now, said once.</para>
    /// </summary>
    public static string ProfessionStanding(TradeskillStanding standing)
    {
        var name = Tradeskills.For(standing.Skill).Name;
        return standing.Known
            ? $"{name} — your log last raised it to {standing.Value}"
              + (standing.At == default ? "." : $", on {standing.At:MMM d}.")
            : $"{name} — no skill-up in your log yet.";
    }

    /// <summary>
    /// The watch control's label, in its two states.
    ///
    /// <para>Two labels rather than one, because the control DOES something the first time and
    /// only opens afterwards, and a player who clicked it twice should be able to tell that the
    /// second click added nothing. The state is read from the rules the player actually has —
    /// not from a flag this room set — so a rule deleted in Options takes the label back with
    /// it.</para>
    /// </summary>
    public static string WatchPresetLabel(bool watching) =>
        watching ? "Watching skill-ups" : "Watch skill-ups";

    /// <summary>The name a rule this room creates gets, so the player can find it in a list
    /// they may already have twenty rows in. The profession's own name, because that is what
    /// they picked.</summary>
    public static string WatchRuleName(Tradeskill skill) =>
        $"{Tradeskills.For(skill).Name} skill-ups";

    /// <summary>
    /// **WHERE THE FARMING ANSWER COMES FROM, AND WHAT IT STILL CANNOT SEE** (DRA-149 D3,
    /// plan P4; the Founder's own "plan honestly on gaps", kept).
    ///
    /// <para><b>This sentence used to be a PARK and the park was about the wrong column.</b>
    /// From DRA-71 D8 until this slice it read: *"EQBuddy does not rank where to farm materials
    /// yet. Of the 11,197 item pages it has read, 14 say which profession an ingredient belongs
    /// to."* Every word of that was true and it was measured on <c>[[Category:…]]</c> tags —
    /// which answer a different question. The <c>Recipes</c> field is the one that carries the
    /// association, and the shipped report has said so in the same file the whole time: 1,276
    /// pages carry a recipe list and all eight professions appear in them as headings.</para>
    ///
    /// <para><b>The honest gap moved rather than closed, and this sentence still carries
    /// it.</b> An ingredient that is bought, foraged or crafted has no page saying where it
    /// drops, so it is not in the rows below — which is the whole of Fletching (33 materials,
    /// zero drop zones) and is why that profession draws
    /// <see cref="GoalGapReason.NoMaterialDrops"/> rather than an empty list.</para>
    ///
    /// <para><b>THE NUMBERS ARE A CLAIM ABOUT THE SHIPPED CATALOG, SO THEY MOVE WHEN IT DOES</b>
    /// — the DRA-84 D3 lesson, kept exactly. The pinning test reads
    /// <c>items-catalog-report.md</c>, which <c>itemcatalog-build</c> rewrites on every refresh,
    /// rather than this sentence's own literal: a guard that can only catch somebody DELETING a
    /// number, never the number going wrong, is what let the previous version of this sentence
    /// ship a survey result its own report contradicted.</para>
    /// </summary>
    public const string ProfessionsFarmNote =
        "Where to farm these is below, read off the recipes on EQBuddy's own item pages — "
        + "1,276 of the 11,197 it has read carry a recipe list, and all eight professions "
        + "appear in them. An ingredient that is bought, foraged or crafted has no page saying "
        + "where it drops, so it is not in that list.";

    // ---- the vendor half: merchants eqlwiki's zone pages name (DRA-149 D4) -----------------

    /// <summary>
    /// **HOW MANY MERCHANT LINES ONE PROFESSION SHOWS BEFORE THE REST BECOME A COUNT.**
    ///
    /// <para>Three, and the surviving cap SAYS so (trap 50) — the same shape
    /// <see cref="UnreadWorn"/> and the who rule already keep. Three is not arbitrary: with
    /// eight professions listed by default, a cap of three puts at most twenty-four transcribed
    /// sentences under the block, and these sentences are long because they are the wiki's own.
    /// Jewelcrafting alone matches thirty.</para>
    /// </summary>
    public const int MerchantLineCap = 3;

    /// <summary>
    /// **WHERE THE VENDOR LINES COME FROM, SAID ONCE OVER THE BLOCK** (DRA-149 D4, plan P5 —
    /// the Founder's FAIL item 3, second half: *"named vendors + where they are when shopping
    /// vendors"*).
    ///
    /// <para>It is a caption about the SOURCE, not about any row, for the reason
    /// <see cref="ProfessionLearnNote"/> is: a fact about where EQBuddy gets its sentences
    /// belongs to the block, and repeating it under eight professions is the wall trap 73 names
    /// in prose.</para>
    ///
    /// <para><b>It says "the wiki's own words" because that is the claim the data supports.</b>
    /// These lines are transcribed from the map key under each zone page's map image; EQBuddy
    /// matched them on the profession's own vocabulary and did not re-word them, so what a
    /// player reads is checkable against the page the door opens.</para>
    /// </summary>
    public const string MerchantsNote =
        "Shops eqlwiki's zone maps name, in the wiki's own words — EQBuddy matched them on each "
        + "profession's materials and tools and changed nothing else. Where a page named the "
        + "vendor, the name is in the line.";

    /// <summary>One merchant row: the zone, then the page's sentence. The zone leads because it
    /// is the answer to "where", which is what the line itself often does not say.</summary>
    public static string MerchantRow(MerchantLine merchant) =>
        $"{merchant.Zone} — {merchant.Line}";

    /// <summary>
    /// **WHICH LINES A SURFACE SHOWS — one producer, because both of them show the same ones**
    /// (trap 4).
    ///
    /// <para><b>One line per ZONE before a second from the same one.</b> The cap is a travel
    /// budget, and three shops in Freeport is one destination rather than three: without this
    /// rule Freeport fills every profession's list on its own, because it is the page with the
    /// most map-key entries in the cache. Within a zone the FIRST line wins, which is the page's
    /// own order — not a ranking, and nothing here invents one.</para>
    /// </summary>
    public static IReadOnlyList<MerchantLine> MerchantsShown(ZoneMerchants catalog, Tradeskill skill)
    {
        var shown = new List<MerchantLine>();
        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var merchant in catalog.For(skill))
        {
            if (shown.Count >= MerchantLineCap) break;
            if (!zones.Add(merchant.Zone)) continue;
            shown.Add(merchant);
        }
        return shown;
    }

    /// <summary>
    /// The cap's own sentence — <b>what was held back and where the rest of it is</b> (trap 50).
    ///
    /// <para>It counts ZONES rather than lines, because a player deciding where to travel cares
    /// how many places are on offer and not how many shops are in them. The number of lines is
    /// what the cap acts on; the number of zones is what the sentence is about.</para>
    /// </summary>
    public static string MerchantsCapped(int shown, int zones)
    {
        var more = zones - shown;
        return more <= 0
            ? ""
            : $"and {more} more {(more == 1 ? "zone" : "zones")} — eqlwiki's zone pages have the "
              + "rest.";
    }

    /// <summary>
    /// The honest empty state for a profession no zone page names.
    ///
    /// <para>The subject is <b>eqlwiki's zone pages</b>, never the game: a trade with no shop in
    /// this list has shops, and what is missing is a line on a wiki page. Saying "nowhere sells
    /// this" would be a claim about Norrath that this data cannot make.</para>
    /// </summary>
    public static string NoMerchantsFor(Tradeskill skill) =>
        $"No zone page's map key names a {Tradeskills.For(skill).Name} shop. That is a gap in "
        + "eqlwiki's maps, not a statement about the game.";

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

    // ---- Track Upgrade (DRA-216 D4, S12) ------------------------------------------------

    /// <summary>The tracked block's heading. It names the PLAYER's decision rather than
    /// EQBuddy's list, because that is whose it is — everything else in this room is something
    /// EQBuddy worked out.</summary>
    public const string TrackedHeading = "What you are going after";

    /// <summary>
    /// **WHAT A TRACKED UPGRADE IS, AND THE TWO THINGS IT IS NOT** — said once under the block
    /// rather than on each row (trap 73), in <see cref="GearBaseClaimNote"/>'s own idiom.
    ///
    /// <para>It has to carry the park out loud. S8 is parked, so EQBuddy holds no enhanced-item
    /// stats at all and cannot tell anybody whether the base item they are chasing beats the
    /// "+N" they are wearing; and with no honest completion condition, a goal ends when the
    /// player says it does. A list that quietly ticked itself off — or that implied a
    /// comparison EQBuddy cannot make — would be worse than no list, because the player would
    /// act on it.</para>
    /// </summary>
    public const string TrackedNote =
        "These stay here until you untrack them. EQBuddy does not tick one off for you: it "
        + "compares the base item on eqlwiki against the base item you are wearing, and it has "
        + "no numbers at all for what a \"+N\" adds to either — so whether the one you are "
        + "chasing beats the one you have is your call, not ours.";

    /// <summary>
    /// One tracked row: what you are going after, what it was offered against, and when you
    /// decided.
    ///
    /// <para><b>The anchor is in the sentence for the reason it is in
    /// <see cref="GearUpgradeFact"/>'s:</b> an upgrade with nothing behind it is a claim about
    /// the game rather than about this character, and that is the line <c>GearUpgrades</c>'
    /// lock draws. The worn item keeps the DUMP's spelling — the "+N" the player can see on
    /// their own character sheet — and nothing here does arithmetic with it.</para>
    ///
    /// <para>The date is absolute and never relative: "3 days ago" drifts on a clock, and every
    /// surface that folds this sentence into a repaint key would then move on its own (trap 8).
    /// It is the decision's own stamp, which is the one fact about a goal that nothing else in
    /// the room can recover.</para>
    /// </summary>
    public static string TrackedRow(TrackedUpgrade tracked)
    {
        var over = tracked.Over is { Length: > 0 } worn
            ? $" — to replace {worn}{(tracked.Slot is { Length: > 0 } s ? $" ({Slot(s)})" : "")}"
            : "";
        return $"{tracked.Item}{over}. Tracked {tracked.TrackedAt:d MMM yyyy}.";
    }

    /// <summary>The per-row control's label, both ways round. A toggle whose label did not
    /// change is a control that lies about what it just did.</summary>
    public static string TrackLabel(bool tracked) => tracked ? "Tracked ✓" : "Track";

    /// <summary>What the control does, on the hover the desktop gives it. It names the ITEM,
    /// because a row carries several gear lines and a tip reading "track this" would not say
    /// which — and it says where the row goes, which is the whole point of the block.</summary>
    public static string TrackTip(bool tracked, string item) => tracked
        ? $"Stop going after {item}. It leaves \"{TrackedHeading}\" — nothing else changes, and "
          + "you can track it again from here."
        : $"Go after {item}. It is added to \"{TrackedHeading}\" above and stays there, whatever "
          + "this list says next time.";

    /// <summary>
    /// **WHERE A TRACKED GOAL IS CHANGED, for the surface that cannot change it** (trap 35).
    ///
    /// <para>Tracking writes the profile the PC is playing from, so the phone ports the list as
    /// what it is — a read of a decision made on the PC — rather than growing a control that
    /// writes back. It is the <c>PicksOnPc</c> rule one block along, and it is drawn ONLY where
    /// there is a list to explain.</para>
    /// </summary>
    public const string TrackedOnPc =
        "Tracked on your PC, from the answers below — each gear line there has a Track button.";

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

    /// <summary>
    /// **WHAT A GEAR ROW IS AND IS NOT COMPARING — SAID ONCE, UNDER THE BLOCK** (DRA-149 D1,
    /// plan P1).
    ///
    /// <para>The sweep compares the wiki's BASE numbers on both sides, and the player's own
    /// item is very likely carrying a "+N" the wiki publishes no value for. That is a real
    /// limit on every row at once, so it is stated at the block exactly once — <b>never
    /// appended to each row</b>. Eight rows carrying one identical caveat is the distinct-count
    /// shape trap 73 was written about, and it would crowd out the thing each row exists to
    /// say.</para>
    ///
    /// <para><b>It states the gap; it does not close it.</b> No arithmetic converts a "+N" into
    /// stats anywhere in this repo, because eqlwiki does not state one — so the honest sentence
    /// names what EQBuddy compared and hands the judgement back, rather than estimating and
    /// being uniquely wrong.</para>
    /// </summary>
    public const string GearBaseClaimNote =
        "These compare the base item on eqlwiki against the base item you are wearing. Yours "
        + "carries its \"+N\" on top, and the wiki does not state what that is worth — so at "
        + "the same \"+\" the listed item wins, and EQBuddy cannot tell you whether it still "
        + "wins against yours as it stands.";

    /// <summary>
    /// **WHAT A PROC IS AND IS NOT DOING TO THE ORDER — SAID ONCE, UNDER THE BLOCK** (DRA-241,
    /// Helm ruling <c>27302878</c>: report only, never price).
    ///
    /// <para><see cref="GearBaseClaimNote"/>'s twin, and it exists for the opposite reason. That
    /// one says a number EQBuddy compared is incomplete. This one says a fact EQBuddy PRINTED
    /// was never in the comparison at all — because there is no number anywhere for what a proc
    /// is worth, and inventing an exchange rate between a Ykesha proc and +40 Mana is the line
    /// the Gear Locker's lock draws.</para>
    ///
    /// <para><b>Without it the rows are silently misleading in the helpful direction.</b> A
    /// player reading "It procs Ykesha." under a ranked list will reasonably assume the ranking
    /// knew that — every other sentence on those rows IS something the comparison weighed. The
    /// sentence's whole job is to say the proc rode along beside the ranking rather than inside
    /// it, and to hand the judgement back rather than make it.</para>
    ///
    /// <para><b>Once per block, never per row</b> (trap 73), and drawn only where a row actually
    /// names a proc — a caveat about procs over a list with none in it is the disclosure-line
    /// rule broken one caption along.</para>
    /// </summary>
    public const string GearProcNote =
        "Where a weapon's page names a combat proc, it is printed on the row — and it counted "
        + "for nothing in the order above. EQBuddy has no way to say what a proc is worth "
        + "against armour class or mana, so it tells you which weapons have one and leaves that "
        + "trade to you.";

    /// <summary>Said when the sweep's per-anchor cap held upgrades back — the one count that
    /// cannot ride a row, because it is spent before any row exists (trap 50). The door under
    /// it is the Gear room, which has the whole list.</summary>
    public static string GearWithheld(int withheld) => withheld <= 0
        ? ""
        : $"{withheld:N0} more {(withheld == 1 ? "upgrade" : "upgrades")} matched and are not "
          + "listed — EQBuddy names a few per slot rather than every one it has read about.";

    /// <summary>
    /// **WHAT THE WHO RULE HELD BACK** (DRA-84 D4, plan P3; trap 50).
    ///
    /// <para><b>Its own sentence, deliberately not folded into
    /// <see cref="GearWithheld"/>.</b> That one is a CAP — EQBuddy naming a few of the many it
    /// could have named — and this one is a RULE with a different cause and a different remedy.
    /// Summing them would produce one number that can explain neither, which is the failure
    /// trap 50 is about rather than a tidier surface.</para>
    ///
    /// <para><b>The subject is EQBuddy's own knowledge, never the game.</b> "Nothing drops it
    /// there" would be a claim about the world; what actually happened is that the item's page
    /// named no creature for that zone and this character has never looted one there. The
    /// sentence says both halves, because both are things a player can change — one by playing,
    /// one by editing the page.</para>
    ///
    /// <para><b>It is NOT gear-specific and since DRA-149 D3 it is not named as though it
    /// were.</b> Farm Materials runs the identical rule over the identical catalog, and the
    /// sentence it needs is this one word for word — the subject is a drop offer and the remedy
    /// is the same page. A second copy reading "material" instead of "item" would be one
    /// sentence's worth of prose that could go stale on its own (trap 4).</para>
    /// </summary>
    public static string DropOffersWithheld(int withheld) => withheld <= 0
        ? ""
        : withheld == 1
            ? "1 more drop offer is not listed: the item's page names nothing that drops it in "
              + "that zone, and you have not looted one there. EQBuddy leaves out a camp it "
              + "cannot tell you what to kill at."
            : $"{withheld:N0} more drop offers are not listed: their item pages name nothing "
              + "that drops them in those zones, and you have not looted one there. EQBuddy "
              + "leaves out a camp it cannot tell you what to kill at.";

    /// <summary>
    /// **WHAT THE QUEST-SOURCE RULE HELD BACK** (DRA-219, S10/S11; trap 50).
    ///
    /// <para><see cref="DropOffersWithheld"/>'s sibling on the other acquisition path, and it
    /// needs its own words rather than that one's: the remedy is different. A withheld drop offer
    /// means an ITEM page names no creature; this means EQBuddy's shipped QUEST list does not hold
    /// the quest an item page pointed at — a gap in a different catalog, fixed by a different
    /// edit, and on the shipped data the common case rather than the corner one (1,028 of 2,380
    /// wearable offers).</para>
    ///
    /// <para><b>The subject is EQBuddy's own quest list, never the game.</b> "That quest does not
    /// exist" would be a claim about the world, and it would be false — the item page is usually
    /// right and the quest list is usually the one that has not caught up.</para>
    /// </summary>
    public static string QuestOffersWithheld(int withheld) => withheld <= 0
        ? ""
        : withheld == 1
            ? "1 more quest reward is not listed: the quest its item page names is not in the "
              + "quest list EQBuddy ships, so there is no giver, no start zone and no step list "
              + "to hand you. EQBuddy leaves out a quest it can only give you the title of."
            : $"{withheld:N0} more quest rewards are not listed: the quests their item pages name "
              + "are not in the quest list EQBuddy ships, so there is no giver, no start zone and "
              + "no step list to hand you. EQBuddy leaves out a quest it can only give you the "
              + "title of.";

    /// <summary>
    /// **UPGRADES WITH NO ACQUISITION SOURCE AT ALL** (DRA-219, S10.1/S19.2; trap 50).
    ///
    /// <para><b>Every other sentence in this block is about an offer that existed and was
    /// removed. This one is about an item that never got that far.</b> The sweep compared it,
    /// found it better than what the player wears, and dropped it because no page says where it
    /// comes from — 1,772 of the shipped catalog's 6,844 wearable records carry neither a drop
    /// zone nor a quest. That refusal is right; its silence is the Founder's bow one layer up,
    /// where an absence nobody counts cannot be told apart from a slot with nothing better in
    /// it.</para>
    ///
    /// <para><b>It names no remedy, because there is none the player can act on.</b> The page is
    /// the answer and the wiki is where it changes — the sentence says which of the two things
    /// happened and stops rather than inventing an instruction.</para>
    /// </summary>
    public static string SourcelessUpgrades(int found) => found <= 0
        ? ""
        : found == 1
            ? "1 better base item is not listed at all: its eqlwiki page names no zone it drops "
              + "in and no quest that hands it out, so there is nowhere to send you."
            : $"{found:N0} better base items are not listed at all: their eqlwiki pages name no "
              + "zone they drop in and no quest that hands them out, so there is nowhere to send "
              + "you.";

    /// <summary>
    /// **UPGRADES THE INCLUDE-QUESTS TOGGLE IS HIDING** (DRA-219, S10.1 — *"do not restrict
    /// recommendations to direct creature drops"*; trap 50).
    ///
    /// <para><b>The one refusal in this whole block the player can undo from where they are
    /// standing</b>, which is exactly why it must be said. 1,284 of the shipped catalog's
    /// wearable records are quest-only, so a character with the toggle off is routinely shown a
    /// narrower list than EQBuddy found — and the toggle is a checkbox whose consequence nobody
    /// could see. It names the control rather than describing it, because the control is on the
    /// same screen.</para>
    /// </summary>
    public static string QuestOnlyUpgrades(int found) => found <= 0
        ? ""
        : found == 1
            ? "1 better base item comes only from a quest, and quest rewards are switched off — "
              + "turn on \"include quests\" above to see it."
            : $"{found:N0} better base items come only from quests, and quest rewards are "
              + "switched off — turn on \"include quests\" above to see them.";
    /// **WHAT THE OFF-HAND RULE REFUSED** (DRA-222 D6, S7.3; trap 50).
    ///
    /// <para><b>Its own sentence, beside the cap and the who rule and not folded into
    /// either.</b> Those two are about a LIST and a PLACE; this one is about the player's hands,
    /// and the remedy is different again — nothing is wrong with the data and nothing needs
    /// editing, the item is simply a trade the player might still want to make with their eyes
    /// open. Summing three causes into one number is the failure trap 50 is about.</para>
    ///
    /// <para><b>It names the cost and does not make the judgement.</b> EQBuddy has no way to
    /// price an off-hand — that depends on the shield, the second weapon and the class, none of
    /// which this repo has measured — so the sentence says what the swap would take and leaves
    /// the call with the player, who can see it in the Gear room. The one thing it must not do
    /// is what the rows used to do, which is offer the greatsword with no mention of the hand
    /// it costs.</para>
    ///
    /// <para><b>The subject is what the player is wearing, never the item.</b> "Two-handed
    /// weapons are worse" is a claim about the game; "you are holding something in your off
    /// hand" is a row in their own dump.</para>
    /// </summary>
    public static string OffHandRefused(int refused) => refused <= 0
        ? ""
        : refused == 1
            ? "1 upgrade is not listed: it is two-handed, and you have something in your off "
              + "hand. EQBuddy cannot price what you would be putting down, so it leaves the "
              + "swap to you."
            : $"{refused:N0} upgrades are not listed: they are two-handed, and you have "
              + "something in your off hand. EQBuddy cannot price what you would be putting "
              + "down, so it leaves the swap to you.";

    /// <summary>
    /// How many unread worn items are NAMED before the sentence counts the rest.
    ///
    /// <para>Three, which is <see cref="GearBandNamed"/>'s number and its argument: the names
    /// are long — <i>"Deterioriated Ancient Faydark Longbow +2"</i> is one of them — and a
    /// caption listing eleven of them would be a table pretending to be a sentence. The count
    /// is the WHOLE count either way (trap 50): a cap that hid how much it was hiding is the
    /// silence this whole sentence exists to end.</para>
    /// </summary>
    public const int UnreadWornNamed = 3;

    /// <summary>
    /// **WHAT EQBUDDY IS WEARING AND CANNOT READ ABOUT** (DRA-149 D2, plan P2; trap 50).
    ///
    /// <para><b>The Founder failed Farm Gear on this sentence not existing.</b> His dump has
    /// twenty-one worn rows; EQBuddy could describe twenty, and the twenty-first — a bow the
    /// game spells <c>Deterioriated</c> and eqlwiki spells <c>Deteriorated</c> — was dropped
    /// without a word. From his side of the screen that is indistinguishable from a Range slot
    /// with nothing better available, which is why he reported the bow as MISSING rather than
    /// as unbeaten.</para>
    ///
    /// <para><b>The subject is EQBuddy's own catalog, never the game and never the player.</b>
    /// "That item does not exist" would be a claim about the world; what happened is that a
    /// name did not match anything in the pages EQBuddy ships. The sentence says which names,
    /// says that the two sources do not always spell an item the same way, and stops — the
    /// wiki door under it is where a player can check, and <see cref="ItemNameAliases"/> is
    /// where a checked answer lands.</para>
    ///
    /// <para><b>The dump's own spelling, "+N" and all</b>, because that is the string on the
    /// player's screen. Naming the folded base name would report a real miss under a name
    /// nobody has seen.</para>
    /// </summary>
    public static string UnreadWorn(IReadOnlyList<string> unread)
    {
        if (unread is not { Count: > 0 }) return "";

        var named = unread.Take(UnreadWornNamed).ToList();
        var rest = unread.Count - named.Count;
        var list = string.Join(", ", named) + (rest > 0 ? $", and {rest} more" : "");

        return $"EQBuddy has never read about {unread.Count:N0} "
            + $"{(unread.Count == 1 ? "thing" : "things")} you are wearing: {list}. "
            + $"{(unread.Count == 1 ? "It is" : "They are")} not in the item pages EQBuddy "
            + $"ships under {(unread.Count == 1 ? "that name" : "those names")}, so nothing "
            + $"below is anchored on {(unread.Count == 1 ? "it" : "them")} — the game and "
            + "eqlwiki do not always spell an item the same way.";
    }

    /// <summary>How many refused zones are NAMED before the sentence counts the rest. Three,
    /// which is <c>GearNamedPerRow</c> and <c>DefaultCap</c>'s reason one surface out: a
    /// caption that listed eleven zones with eleven bands would be a table pretending to be a
    /// sentence.</summary>
    public const int GearBandNamed = 3;

    /// <summary>The noun <see cref="BandRefused"/> takes for each engine, said once so the two
    /// call sites cannot drift into describing each other's list.</summary>
    public const string BandRefusedUpgrades = "upgrades";

    /// <inheritdoc cref="BandRefusedUpgrades"/>
    public const string BandRefusedMaterials = "materials";

    /// <summary>
    /// **A BAND IN WORDS, AND THERE IS ONE PRODUCER OF THEM** (DRA-84 D2).
    ///
    /// <para>Three shapes, because the data has three: a closed band, a single level, and an
    /// OPEN TOP where <see cref="ZoneLevels.Band.Max"/> is null because the page said "and
    /// above" (41 of the 87 shipped bands). The open one must not render as a range with a
    /// missing end — "5–" is a typo and "5–99" is the invented maximum the ruling refused.</para>
    /// </summary>
    public static string BandPhrase(int min, int? max) => max switch
    {
        null => $"{min} and above",
        { } m when m == min => $"{min}",
        { } m => $"{min}–{m}",
    };

    /// <summary>
    /// **WHAT THE BAND GATE HELD BACK, WITH THE NUMBERS IT HELD IT BACK ON** (DRA-84 D2, plan
    /// P2; trap 50).
    ///
    /// <para><b>A refusal that says nothing is worse than a cap that says nothing</b>, which is
    /// why this exists as well as the gate. A zone missing from the list is indistinguishable
    /// from a zone the catalog has nothing in, and the player has no way to discover that
    /// EQBuddy decided for them — so the count, the rule and each band are said out loud, and
    /// the Gear room's door under it has the whole wishlist.</para>
    ///
    /// <para><b>Two numbers and a source, and no adjective</b> (HOME-006). It quotes eqlwiki's
    /// own row and this character's own level and then stops: "outside yours" is a statement
    /// about two ranges, where "too tough for you" would be a claim about the place and about
    /// the player that nothing here measured. The rule is named in full so the reader can
    /// disagree with the judgement rather than just with the outcome.</para>
    /// </summary>
    /// <param name="what">What EQBuddy found in those zones, as a plural noun — "upgrades" for
    /// Farm Gear, "materials" for Farm Materials. <b>It is a required argument rather than a
    /// default since DRA-149 D3</b>: the arithmetic, the arms and the thresholds are one
    /// producer's, and the ONE word that differs between the two lists is the one a caller must
    /// state, so neither caption can silently describe the other engine's refusals.</param>
    public static string BandRefused(IReadOnlyList<GearBandRefusal> refused, string what)
    {
        if (refused.Count == 0) return "";

        var named = refused
            .Take(GearBandNamed)
            .Select(r => $"{r.Zone} ({BandPhrase(r.Min, r.Max)})")
            .ToList();
        var rest = refused.Count - named.Count;
        var list = string.Join(", ", named) + (rest > 0 ? $", and {rest} more" : "");

        // Only the arms that actually fired, so the sentence never quotes a threshold that
        // decided nothing in this list.
        var arms = new List<string>();
        if (refused.Any(r => r.Arm == GearBandArm.TopUnder))
            arms.Add($"tops out {Recommendations.OutgrownBy} or more levels under you");
        if (refused.Any(r => r.Arm == GearBandArm.BottomOver))
            arms.Add($"starts {Recommendations.GearBandReachAbove} or more levels over you");

        return $"{refused.Count:N0} {(refused.Count == 1 ? "zone" : "zones")} EQBuddy has "
            + $"{what} for {(refused.Count == 1 ? "is" : "are")} not listed at your level "
            + $"{refused[0].Level}: {list}. Those are eqlwiki's own creature levels — EQBuddy "
            + $"leaves a zone out of this list when its band {string.Join(" or ", arms)}.";
    }

    /// <summary>How many refused subjects are NAMED before the sentence counts the rest.
    /// <see cref="GearBandNamed"/>'s own number and its reason, one gate over: a caption
    /// listing eleven zones with eleven era words is a table pretending to be a
    /// sentence.</summary>
    public const int GearEraNamed = 3;

    /// <summary>
    /// **WHAT THE ERA GATE HELD BACK, WITH THE DATES IT HELD IT BACK ON** (DRA-180 D2, plan
    /// P3; trap 50).
    ///
    /// <para><b>This is the sentence the Founder's screen owed him, and it is owed twice
    /// over.</b> His Replace list drew Kael Drakkel with nothing marking it as unreachable; his
    /// bow and Baron screens drew nothing at all with no sentence saying why. A refusal that
    /// says nothing is indistinguishable from a catalog with nothing in it — the same reason
    /// <see cref="BandRefused"/> exists, one axis over.</para>
    ///
    /// <para><b>Two era words and a source, and no adjective</b> (HOME-006). It quotes the
    /// wiki's own dating and the one era this repo was told, and then stops. "Not in the game
    /// yet" would be a claim about the SERVER that nothing here measured and that EQBuddy has
    /// no standing to make; "eqlwiki dates it to Velious" is a statement about a wiki page,
    /// which is exactly what was read.</para>
    /// </summary>
    /// <param name="what">What EQBuddy found there, as a plural noun —
    /// <see cref="BandRefusedUpgrades"/> or <see cref="BandRefusedMaterials"/>. Required for
    /// the reason it is required there: one producer, and the single word that differs between
    /// the two lists is the caller's to state, so neither caption can describe the other
    /// engine's refusals.</param>
    public static string EraRefused(IReadOnlyList<GearEraRefusal> refused, string what)
    {
        if (refused.Count == 0) return "";

        var named = refused
            .Take(GearEraNamed)
            .Select(r => $"{r.Subject} ({r.Era})")
            .ToList();
        var rest = refused.Count - named.Count;
        var list = string.Join(", ", named) + (rest > 0 ? $", and {rest} more" : "");

        var one = refused.Count == 1;
        return $"{refused.Count:N0} {(one ? "place" : "places")} EQBuddy has {what} for "
            + $"{(one ? "sits" : "sit")} in content eqlwiki dates later than {refused[0].World}: "
            + $"{list}. That is the era each page gives itself, against the era EQBuddy has "
            + "been told the world is at — it leaves them out rather than sending you somewhere "
            + "you cannot go yet.";
    }

    /// <summary>How many emptied anchors are NAMED before the sentence counts the rest.
    /// <see cref="GearBandNamed"/>'s number and its reason: "Replace what I wear" anchors on
    /// EVERY worn slot, so a character early enough in the game can empty a dozen of them at
    /// once, and a dozen of these sentences is a wall rather than an answer.</summary>
    public const int GearAnchorsNamed = 3;

    /// <summary>
    /// **THE SENTENCE THE FOUNDER WAS OWED AND DID NOT GET** (DRA-180 D3, plan P3).
    ///
    /// <para>He asked for upgrades to a worn bow, got an empty list, and filed a FAIL. The list
    /// was RIGHT: two catalog items beat that bow's base, both drop in Sleeper's Tomb, and the
    /// band gate refused both at level 29. Every fact needed to say so was in the engine and
    /// none of it was on the screen, so the correct answer and a broken sweep looked
    /// identical.</para>
    ///
    /// <para><b>It leads with what EQBuddy FOUND, and that ordering is the point.</b> "Nothing
    /// to show you" and "two things, both out of reach" are different states of the world, and
    /// a sentence that opened with the absence would read as the same shrug the old empty list
    /// gave. The count comes first because the count is the evidence that the sweep ran.</para>
    ///
    /// <para><b>Only the causes that actually removed something are named</b> — the
    /// <see cref="BandRefused"/> arm rule, one surface over, and the plan's own instruction that
    /// a sentence about a gate that did not run is furniture. Wherever the era gate stands down
    /// (no world era, or no era table) <see cref="GearAnchorRemoved.LaterContent"/> is 0 and no
    /// era clause is drawn at all. Since D5 the world is Classic and the era clause LEADS — the
    /// era rule runs first, so the Founder's bow reads "2 come from content eqlwiki dates
    /// later…" where D3 drew "2 drop only where eqlwiki lists creature levels outside yours"
    /// (<c>FounderResmokeTests</c>, DRA-180 D5 section).</para>
    ///
    /// <para><b>The subject is EQBuddy's catalog and eqlwiki's numbers, never the game.</b> It
    /// does not say the bow is the best bow in EverQuest — the never-BiS lock forbids exactly
    /// that, and the catalog cannot support it. It says what EQBuddy read and what it did with
    /// it. HOME-006 is untouched: no clause here describes what a place is like, only which
    /// numbers a page carries.</para>
    /// </summary>
    public static string AnchorAllRemoved(GearAnchorRemoved anchor)
    {
        if (anchor.Found <= 0) return "";

        // Only the causes that spent something. A "0 sit in later content" clause would be a
        // sentence about a gate that did not run, which is the plan's own worked example of
        // furniture — and on every shipped build so far that is the era gate every time.
        var causes = new List<string>();
        if (anchor.LaterContent > 0)
            causes.Add($"{anchor.LaterContent} {(anchor.LaterContent == 1 ? "comes" : "come")} "
                + "from content eqlwiki dates later than the era EQBuddy has been told the "
                + "world is at");
        if (anchor.OutsideBand > 0)
            causes.Add($"{anchor.OutsideBand} {(anchor.OutsideBand == 1 ? "drops" : "drop")} "
                + "only where eqlwiki lists creature levels outside yours");
        if (anchor.NoCreature > 0)
            causes.Add($"{anchor.NoCreature} {(anchor.NoCreature == 1 ? "sits" : "sit")} on "
                + "pages that name nothing that drops them");
        // DRA-219's cause, last because its rule runs last. It is 0 for every character with
        // quest rewards switched off — those candidates never reach a bucket at all — so this
        // clause stays off the screen unless the player has actually asked for quests.
        if (anchor.NoQuestPath > 0)
            causes.Add($"{anchor.NoQuestPath} {(anchor.NoQuestPath == 1 ? "comes" : "come")} "
                + "only from quests that are not in the quest list EQBuddy ships");

        var one = anchor.Found == 1;
        return $"{anchor.Anchor} — {Slot(anchor.Slot)}: EQBuddy has read about {anchor.Found:N0} "
            + $"better base {(one ? "item" : "items")} and left {(one ? "it" : "every one")} "
            + $"out. {Join(causes)}. Nothing in reach beats this item's base.";

        // "a and b" / "a, b and c" — the shape a person writes, and the count can only ever be
        // one to four because there are four rules (DRA-219 added the quest-source one).
        static string Join(List<string> parts) => parts.Count switch
        {
            0 => "",
            1 => Capitalise(parts[0]),
            2 => Capitalise($"{parts[0]} and {parts[1]}"),
            _ => Capitalise(string.Join(", ", parts.Take(parts.Count - 1))
                 + $" and {parts[^1]}"),
        };

        // Every clause starts with a digit today, so this is a no-op that stays honest if a
        // future cause opens with a word instead.
        static string Capitalise(string s) =>
            s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>Said when more worn items got the same answer than
    /// <see cref="GearAnchorsNamed"/> will name (trap 50: a surviving cap says so).</summary>
    public static string AnchorsNotNamed(int held) => held <= 0
        ? ""
        : $"{held:N0} more worn {(held == 1 ? "item" : "items")} got the same answer. "
          + "Gear & Loot lists what EQBuddy has read about each of them.";

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
        // DRA-71 D8. `tracked` is the Watch tab's wire key and NOT a typo — it is the v1
        // settings tag, deliberately reused so one destination keeps one spelling
        // (SettingsSurface resolves the Alerts sub-tabs through AlertSurface's own keys).
        HelperDoorKind.WatchRules =>
            ShellPages.Address(ShellPage.Settings, AlertSurface.KeyFor(AlertTab.Watch)),
        // Null for the same reason WikiFaction is: a wiki page is not a room, and a door that
        // claimed a `page:room` address for one would be a second navigation grammar.
        HelperDoorKind.WikiSkill => null,
        HelperDoorKind.WikiItem => null,
        HelperDoorKind.WikiZone => null,
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
        // Two labels for one control, decided by whether the rule is already there — see
        // WatchPresetLabel, which is what the room actually draws.
        HelperDoorKind.WatchRules => WatchPresetLabel(false),
        HelperDoorKind.WikiSkill => "eqlwiki",
        HelperDoorKind.WikiItem => "eqlwiki",
        HelperDoorKind.WikiZone => "eqlwiki",
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
        HelperDoorKind.WatchRules =>
            "Add a watch rule for this skill's ups and open Settings → Alerts → Watch rules, "
            + "where you can give it a sound. If you already have one, this just opens the "
            + "list.",
        HelperDoorKind.WikiSkill =>
            door.Target.Length > 0
                ? $"Open {door.Target} on eqlwiki — what this profession makes and what it "
                  + "needs is the wiki's answer, and you open the page yourself. EQBuddy never "
                  + "fetches it for you."
                : "Open this profession's page on eqlwiki. You open it yourself; EQBuddy "
                  + "never fetches it for you.",
        // DRA-149 D2. A SEARCH, and the tip says so: this door is under a name that matched no
        // page, so "open its page" would promise the thing that just failed.
        HelperDoorKind.WikiItem =>
            door.Target.Length > 0
                ? $"Search eqlwiki for {door.Target} — if the wiki spells it differently, that "
                  + "is why EQBuddy could not read it. You open the search yourself; EQBuddy "
                  + "never fetches it for you."
                : "Search eqlwiki for this item. You open the search yourself; EQBuddy never "
                  + "fetches it for you.",
        // DRA-149 D4. The tip names the MAP, because that is what the page has and this room
        // does not: the line beside this door came out of a map key, and the numbered marker it
        // belongs to is on the page's own image.
        HelperDoorKind.WikiZone =>
            door.Target.Length > 0
                ? $"Open {door.Target} on eqlwiki — this line is from that page's map key, and "
                  + "the map showing where the shop is is on it. You open the page yourself; "
                  + "EQBuddy never fetches it for you."
                : "Open this zone's page on eqlwiki. You open it yourself; EQBuddy never "
                  + "fetches it for you.",
        _ => "",
    };

    // ---- a guide step's reference, answered (DRA-83) --------------------------------------

    /// <summary>
    /// How many of the Helper's sentences a GUIDE STEP draws before it says it is holding some
    /// back.
    ///
    /// <para><b>Two, where the room draws <see cref="Recommendations.WhyCap"/> six</b>, and the
    /// difference is what the two surfaces are for. The room's whole job is one evening's
    /// argument and it can spend six lines on it; a guide step is one line of a checklist a
    /// player is scrolling, and a walkthrough where every row grew a paragraph would have
    /// buried the walkthrough. The cap SAYS what it withheld and names where the rest is
    /// (trap 50) — the room, which is one click away and is where the answer lives.</para>
    /// </summary>
    public const int AttachedWhyCap = 2;

    /// <summary>
    /// **What the Helper says about the subject this step points at** — the whole line, worded
    /// once for all three surfaces (DRA-83).
    ///
    /// <para><b>Every sentence in it is the Helper's own, passed through.</b> The why-lines go
    /// through <see cref="Why"/> — the same call the Helper room and the phone's Helper screen
    /// make — so the catalog label arrives by construction and a guide row cannot word a
    /// measurement differently from the room that measured it. The only words this method adds
    /// are the lead clause naming WHICH reference is being answered, and the cap sentence.</para>
    ///
    /// <para>Empty is a real answer and the common one: a step whose reference the Helper
    /// could not answer draws nothing at all. See <c>Recommendations.Attached</c> — silence
    /// rather than an empty-state sentence repeated down a checklist.</para>
    /// </summary>
    public static string Attached(GuideAttachmentAnswer answer)
    {
        var lines = answer.Why
            .Select(Why)
            .Where(s => s.Length > 0)
            .ToList();
        if (lines.Count == 0) return "";

        var shown = lines.Take(AttachedWhyCap).ToList();
        var parts = new List<string> { AttachedLead(answer) };
        parts.AddRange(shown);
        // The row's own withheld count is the Helper's (the engine already trimmed to WhyCap
        // and reported it) PLUS what this cap held: one number for "there is more", because two
        // counts on a checklist row would be arithmetic the player has to do.
        var withheld = lines.Count - shown.Count + answer.Answer.WithheldWhy;
        if (withheld > 0) parts.Add(AttachedWithheld(withheld));
        return string.Join(" ", parts);
    }

    /// <summary>
    /// The clause that says which reference is being answered, per kind.
    ///
    /// <para><b>It names the SUBJECT and the GOAL, and nothing about the step.</b> A sentence
    /// like "this is a good place to do this" would be the Helper deciding something about the
    /// guide, which is the wrong way round: the guide says what the step relates to and the
    /// Helper answers from this character's own play.</para>
    ///
    /// <para>The default arm is unreachable from <c>Recommendations.Attached</c>, which refuses
    /// a kind <c>GoalFor</c> does not map — and it answers with the GOAL's own label rather
    /// than with nothing, so a fourth kind arriving before this switch knows it still says
    /// which engine spoke instead of drawing a headless block of numbers.</para>
    /// </summary>
    public static string AttachedLead(GuideAttachmentAnswer answer) => answer.Attachment.Kind switch
    {
        GuideAttachment.XpFarm => $"Your Helper on levelling in {answer.Attachment.Key}:",
        GuideAttachment.GearFarm => $"Your Helper on farming gear in {answer.Attachment.Key}:",
        GuideAttachment.GearUpgrade => $"Your Helper on {answer.Attachment.Key}:",
        _ => $"Your Helper on {GoalLabel(answer.Goal)} — {answer.Attachment.Key}:",
    };

    /// <summary>What the step is not drawing, and where it is. Same shape as
    /// <see cref="WithheldWhy"/>, and it names the room rather than offering a link: on the
    /// phone this line rides a row that cannot open one (trap 35).</summary>
    public static string AttachedWithheld(int withheld) => withheld <= 0
        ? ""
        : withheld == 1
            ? "One more reason is in the Helper room."
            : $"{withheld:N0} more reasons are in the Helper room.";

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

    /// <summary>
    /// **WHAT DROPS IT, IN THE WIKI'S OWN WORDS AND ITS OWN ORDER** (DRA-84 D4, plan P3;
    /// Founder acceptance item 2).
    ///
    /// <para>The list joins with the ordinary English comma-and rather than a bulleted run,
    /// because this rides INSIDE a sentence about an item and on the phone it rides the row
    /// with no hover to escape to (trap 35). The verb agrees with the count — "a bandit drops
    /// it" and "a bandit, a hill giant and a ghoul drop it" — which matters more than it looks
    /// like it should: the creature names are lower-case nouns from the page, so the verb is
    /// the only thing in the clause telling a reader whether they are looking at one name or a
    /// list.</para>
    ///
    /// <para><b>What the cap held back is named as the PAGE's, not as EQBuddy's</b>
    /// (trap 50). "and 4 more on its page" says where the rest are and implies the door;
    /// "and 4 more" alone would read as EQBuddy having measured something it has not.</para>
    /// </summary>
    /// <remarks>It takes the two fields rather than a <c>GearUpgradeFact</c> since DRA-149 D3:
    /// the materials row asks the same question of the same catalog and prints the same clause,
    /// and a second copy of this grammar is the sentence that goes stale (trap 4).</remarks>
    private static string Who(IReadOnlyList<string> who, int withheld)
    {
        if (who.Count == 0) return "";

        var names = who.Count == 1
            ? who[0]
            : string.Join(", ", who.Take(who.Count - 1)) + " and " + who[^1];
        var verb = who.Count == 1 && withheld == 0 ? "drops" : "drop";
        var more = withheld > 0
            ? $", and {withheld:N0} more on its page"
            : "";
        return $" {names}{more} {verb} it.";
    }

    /// <summary>
    /// **HOW TO RUN THE QUEST, IN THE QUEST CATALOG'S OWN FIELDS** (DRA-219, S11; acceptance
    /// S25 AC 1–6).
    ///
    /// <para><b>Every clause is conditional and none is padded</b> (trap 73). The schema has a
    /// field per question and that is not a licence to answer every question: a page that named
    /// no giver gets no "from" clause rather than "from an unknown NPC", and a
    /// <see cref="QuestSourceFact.MinLevel"/> of 0 is a page that stated no level rather than a
    /// quest you can take at 1. Measured on the shipped catalog, the four clauses are answered
    /// on 532 / 529 / 504 / 386 of the 537 quests this engine can reach, so the common row
    /// carries all four and the empty ones are real.</para>
    ///
    /// <para><b>The subject is the wiki's own record, and the verb says so.</b> "Kanthuk Tar
    /// starts it in Cabilis" is a claim about the game; "eqlwiki has it starting with Kanthuk Tar
    /// in Cabilis" is a claim about a page, which is what was actually read. The
    /// <c>Evidence.Catalog</c> label arrives on the end of it by construction.</para>
    ///
    /// <para><b>The component clause counts the whole list and names up to
    /// <c>Recommendations.QuestItemsPerRow</c></b> (trap 50): a row that showed three of nine
    /// turn-ins without saying so would read as a short quest.</para>
    ///
    /// <para>Returns "" when the catalog answered none of the four. The engine does not build
    /// a row in that state at all (<c>Recommendations.QuestSourceRule</c>), so this is the
    /// belt beside that brace rather than a reachable screen — and it draws nothing rather
    /// than a sentence with no facts in it.</para>
    /// </summary>
    private static string QuestSource(QuestSourceFact f)
    {
        var clauses = new List<string>();
        if (f.Giver.Length > 0) clauses.Add($"starting with {f.Giver}");
        if (f.StartZone.Length > 0) clauses.Add($"in {f.StartZone}");
        if (f.MinLevel > 0) clauses.Add($"from level {f.MinLevel}");
        if (clauses.Count == 0 && f.Components == 0) return "";

        var lead = clauses.Count > 0
            ? $"eqlwiki has {f.Quest} {string.Join(" ", clauses)}."
            : $"eqlwiki has {f.Quest}.";

        if (f.Components == 0) return lead;

        // The named items and the whole count, in the who clause's grammar one path over — the
        // page's own order, because nothing here has measured which component is hardest.
        var named = f.Items.Count switch
        {
            0 => "",
            1 => f.Items[0],
            _ => string.Join(", ", f.Items.Take(f.Items.Count - 1)) + " and " + f.Items[^1],
        };
        var takes = $" It takes {Count(f.Components, "turn-in item", "turn-in items")}";
        return named.Length == 0
            ? lead + takes + "."
            : lead + takes + $" — {named}"
              + (f.Components > f.Items.Count ? ", and the rest on its page." : ".");
    }
}
