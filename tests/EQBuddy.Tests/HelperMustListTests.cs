using System.Reflection;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE HELPER'S MUST-LISTS — trap 34's other half, four times over.**
///
/// <para>A negative rule cannot see a MISSING thing. "No line claims a camp is safe" is a
/// ban, and a ban passes perfectly over a goal nobody decided about, a fact nobody worded and
/// a door that leads nowhere: the surface renders, the build is green, and the player gets a
/// chip that does nothing or a blank line under a headline. So every closed set the Helper
/// has is walked here, from the ENUM rather than from a list beside it (trap 30 — a
/// hand-maintained list stops covering the set the day the set grows).</para>
///
/// <para>Four sets, and each one has already been the shape of a real bug somewhere in this
/// repo: the goals (an undecided member falling through a default arm),
/// the <see cref="WhyFact"/> subtypes (a fact with no sentence — <c>UnlockGuidance</c>'s own
/// <c>ShapeFor</c> answers null for exactly this reason), the doors (an affordance that opens
/// nothing, the rail's own forbidden shape one level in), and the gap reasons (an empty state
/// that renders as a blank panel).</para>
/// </summary>
public class HelperMustListTests
{
    // ---- 1. every goal has a DECIDED shape ---------------------------------------------

    /// <summary>
    /// **Null is "nobody decided", and it is the only thing this can catch.**
    ///
    /// <c>Recommendations.ShapeFor</c> has no default arm answering
    /// <see cref="HelperGoalShape.Deferred"/>, deliberately: "a later slice owns this" and
    /// "nobody thought about this" would have looked identical on screen, and the second one
    /// is the one that ships a chip doing nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryGoalHasADecidedShape(HelperGoal goal) =>
        Assert.NotNull(Recommendations.ShapeFor(goal));

    /// <summary>The Founder's nine, and the count asserted out loud so a tenth arriving is a
    /// deliberate edit here rather than a line someone slid into the enum.</summary>
    [Fact]
    public void TheGoalListIsTheFoundersNine()
    {
        Assert.Equal(9, Recommendations.All.Count);
        Assert.Equal(
            ["Level Up", "Farm Gear", "Unlock Classes", "Unlock Races", "Farm Motes",
             "Work on Faction", "Farm Materials", "Make Money", "Achievements"],
            Recommendations.All.Select(HelperPresentation.GoalLabel).ToArray());
    }

    /// <summary>
    /// **"Continue quests" is HOME-001's fifth default category and it is deliberately NOT a
    /// goal.** The Guide room already is that answer, and a chip that opened a second copy of
    /// it would be a room competing with itself. Asserted rather than left as a comment: a
    /// well-meaning "the PRD lists five, we have four of them" reading is exactly how it
    /// would arrive.
    /// </summary>
    [Fact]
    public void ThereIsNoContinueQuestsChip() =>
        Assert.DoesNotContain(Recommendations.All,
            g => HelperPresentation.GoalLabel(g).Contains("quest", StringComparison.OrdinalIgnoreCase));

    /// <summary>Every goal's chip has a label and a tooltip. A chip whose tooltip was empty
    /// would be a pill with no explanation at the one width where the room has least room
    /// for prose.</summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryGoalHasAChipLabelAndATip(HelperGoal goal)
    {
        Assert.NotEmpty(HelperPresentation.GoalLabel(goal));
        Assert.NotEmpty(HelperPresentation.GoalTip(goal));
    }

    /// <summary>
    /// **A deferred goal says so AND points somewhere.**
    ///
    /// This is the pairing that keeps five of the nine chips from being dead affordances
    /// while their engines are built. The rail's own rule is that an affordance which opens
    /// nothing is a trap; a chip producing one apologetic sentence and no door would be that
    /// rule broken one level in, where the rail's guard cannot see it. Both halves are
    /// asserted together because either one alone is the bug.
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryDeferredGoalNamesTheRoomThatAnswersItToday(HelperGoal goal)
    {
        var deferred = Recommendations.ShapeFor(goal) == HelperGoalShape.Deferred;
        Assert.Equal(deferred, HelperPresentation.NotAnsweredYet(goal).Length > 0);
        Assert.Equal(deferred, HelperPresentation.NotAnsweredDoor(goal) is not null);

        if (!deferred) return;
        var kind = HelperPresentation.NotAnsweredDoor(goal)!.Value;
        var address = HelperPresentation.AddressFor(kind);
        Assert.NotNull(address);
        var parsed = ShellPages.ParseAddress(address);
        Assert.NotNull(parsed);
        Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
    }

    /// <summary>The engines that exist, named. This row is meant to be EDITED — a goal moving
    /// from Deferred to Answered is the whole of what a later slice delivers, and it should
    /// be a deliberate line in a diff rather than something that happens as a side effect.
    /// Four in D1; Farm Gear joined them in DRA-71 D6; Farm Motes and Make Money in D7.</summary>
    [Fact]
    public void SevenGoalsAreAnsweredInThisDelivery() =>
        Assert.Equal(
            [HelperGoal.LevelUp, HelperGoal.FarmGear, HelperGoal.UnlockClasses,
             HelperGoal.UnlockRaces, HelperGoal.FarmMotes, HelperGoal.WorkOnFaction,
             HelperGoal.MakeMoney],
            Recommendations.All
                .Where(g => Recommendations.ShapeFor(g) == HelperGoalShape.Answered)
                .ToArray());

    /// <summary>
    /// **THE GEAR INTENTS ARE THEIR OWN MUST-LIST** (DRA-71 D6, plan P8) — trap 34 one level
    /// below the goals'.
    ///
    /// <para>Farm Gear is one Answered goal asking three different questions, and two of them
    /// are built. Null is "nobody decided", reachable only by adding a member to
    /// <see cref="GearIntent"/>; <c>GearUpgrades.ShapeFor</c> has no default arm for the same
    /// reason <c>Recommendations.ShapeFor</c> has none.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Intents))]
    public void EveryGearIntentHasADecidedShapeALabelAndATip(GearIntent intent)
    {
        Assert.NotNull(GearUpgrades.ShapeFor(intent));
        Assert.NotEmpty(HelperPresentation.GearIntentLabel(intent));
        Assert.NotEmpty(HelperPresentation.GearIntentTip(intent));
    }

    /// <summary>The Founder's three, in his order, and the count said out loud — a fourth
    /// arriving is a deliberate edit here rather than a line someone slid into the enum.</summary>
    [Fact]
    public void TheIntentListIsTheFoundersThree()
    {
        Assert.Equal(3, GearUpgrades.All.Count);
        Assert.Equal(
            ["Upgrade what I wear", "Replace with better", "Farm to sell"],
            GearUpgrades.All.Select(HelperPresentation.GearIntentLabel).ToArray());
    }

    /// <summary>All three answer since DRA-71 D7. The same "meant to be EDITED" row as the
    /// goals' above it — and a fourth intent arriving must decide rather than inherit.</summary>
    [Fact]
    public void AllThreeGearIntentsAreAnsweredInThisDelivery() =>
        Assert.Equal(
            [GearIntent.UpgradeWorn, GearIntent.ReplaceSlot, GearIntent.FarmToSell],
            GearUpgrades.All
                .Where(i => GearUpgrades.ShapeFor(i) == GearIntentShape.Answered)
                .ToArray());

    /// <summary>
    /// **THE DEFERRED-INTENT SENTENCE SURVIVES THE SLICE THAT EMPTIED ITS CASELOAD** (DRA-71
    /// D7).
    ///
    /// <para>No intent is Deferred any more, so <c>Recommendations.FarmGear</c>'s arm for it is
    /// unreachable today. That is exactly when an empty state rots: the next intent to arrive
    /// Deferred would return an empty list a room draws as "your gear is perfect". So the
    /// sentence and its door are still asserted, and the door is still checked against the
    /// rooms that have actually landed.</para>
    /// </summary>
    [Fact]
    public void TheDeferredGearIntentSentenceStillExistsAndStillPointsSomewhere()
    {
        var gap = new GoalGap(HelperGoal.FarmGear, GoalGapReason.GearIntentNotAnsweredYet);
        Assert.NotEmpty(HelperPresentation.Gap(gap));

        var address = HelperPresentation.AddressFor(HelperDoorKind.Wealth);
        Assert.NotNull(address);
        Assert.Contains(ShellPages.ParseAddress(address)!.Value.Page, ShellPages.Landed);
    }

    public static TheoryData<GearIntent> Intents() => [.. GearUpgrades.All];

    // ---- 2. every why-fact has a sentence ----------------------------------------------

    /// <summary>
    /// **Every <see cref="WhyFact"/> in the assembly is worded.**
    ///
    /// <para>Reflected rather than listed, so a fact shape added tomorrow is covered
    /// tomorrow. <c>HelperPresentation.Why</c>'s switch has a default arm answering "" —
    /// which is correct, because a room must not throw over a fact it does not recognise —
    /// and that arm is precisely what makes this test necessary: without it, a new fact draws
    /// a blank line under a headline and nothing anywhere says so.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WhyFactTypes))]
    public void EveryWhyFactShapeHasASentence(Type type)
    {
        var fact = Build(type);
        Assert.NotEmpty(HelperPresentation.Why(fact));
    }

    /// <summary>The reflection's own liveness check. A rename that emptied the sweep above
    /// would otherwise pass in silence, which is how a guard reads as coverage while seeing
    /// nothing (trap 78's shape: a guard aimed at nothing is green).</summary>
    [Fact]
    public void TheWhyFactSweepActuallyFindsFacts() =>
        Assert.True(Subtypes().Count >= 6,
            $"Only {Subtypes().Count} WhyFact shapes were reached — the reflection has "
            + "stopped finding them. Check the type, not the assertion.");

    /// <summary>
    /// **HOME-004, applied by construction.** The label is appended by <c>Why</c> itself
    /// rather than by each arm, so a catalog-sourced fact added later is labelled by the
    /// switch that has to grow to accommodate it — and cannot be added unlabelled by
    /// somebody who did not know the rule.
    /// </summary>
    [Theory]
    [MemberData(nameof(WhyFactTypes))]
    public void ACatalogLineCarriesTheEstimateLabelAndAPersonalOneDoesNot(Type type)
    {
        var fact = Build(type);
        var line = HelperPresentation.Why(fact);
        Assert.Equal(fact.Evidence == Evidence.Catalog, line.Contains(HelperPresentation.CatalogLabel));
    }

    /// <summary>
    /// **The prove-fail for the row above** (trap 34: green-only is vacuous coverage). The
    /// same fact, re-tagged Catalog, must gain the label — so the assertion is reading the
    /// TAG rather than agreeing with a sentence that happened to contain the words.
    /// </summary>
    [Fact]
    public void RetaggingALineAsCatalogIsWhatAddsTheLabel()
    {
        var personal = new WordedFact("Your kills of a froglok tad moved it +5 each.", Evidence.Personal);
        var catalog = personal with { Evidence = Evidence.Catalog };

        Assert.DoesNotContain(HelperPresentation.CatalogLabel, HelperPresentation.Why(personal));
        Assert.Contains(HelperPresentation.CatalogLabel, HelperPresentation.Why(catalog));
        // And the sentence itself survives the re-tag untouched — the label is added, not
        // substituted, so a pass-through line keeps the words its own producer chose.
        Assert.Contains(personal.Text, HelperPresentation.Why(catalog));
    }

    // ---- 3. every door opens something --------------------------------------------------

    /// <summary>
    /// **A door either resolves to a LANDED room or opens the wiki, and nothing else.**
    ///
    /// <para>The null arm of <c>AddressFor</c> is a real answer and not an omission —
    /// <see cref="HelperDoorKind.WikiFaction"/> opens a browser — so the assertion is written
    /// as an exclusive choice rather than as "not null". A caller that treated null as "no
    /// door" would silently drop the one affordance a player with no farming history has.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(DoorKinds))]
    public void EveryDoorEitherLandsOnARoomOrOpensTheWiki(HelperDoorKind kind)
    {
        Assert.NotEmpty(HelperPresentation.DoorLabel(kind));
        Assert.NotEmpty(HelperPresentation.DoorTip(new HelperDoor(kind, "Faydark Rangers")));

        var address = HelperPresentation.AddressFor(kind);
        // DRA-71 D8 added the second wiki door, and it is listed here rather than pattern
        // matched on the name: "a kind whose name starts with Wiki" is a proxy, and the fact
        // is that these two open a browser (trap 64b).
        if (kind is HelperDoorKind.WikiFaction or HelperDoorKind.WikiSkill)
        {
            Assert.Null(address);
            return;
        }

        Assert.NotNull(address);
        var parsed = ShellPages.ParseAddress(address);
        Assert.NotNull(parsed);
        // Filtered through the SAME list the rail draws from. A door that named a room which
        // has not landed is the dead affordance `helperDeadDoors` counts from a launched app;
        // this catches it without one.
        Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
    }

    // ---- 4. every gap has an empty state ------------------------------------------------

    /// <summary>Every reason a selected goal can come back empty is worded. A gap that
    /// rendered as a blank line is the "silent no-op" rule with the switch on the other
    /// side.</summary>
    [Theory]
    [MemberData(nameof(GapReasons))]
    public void EveryGapReasonIsWordedForEveryAnsweredGoal(GoalGapReason reason)
    {
        foreach (var goal in Recommendations.All
                     .Where(g => Recommendations.ShapeFor(g) == HelperGoalShape.Answered))
            Assert.NotEmpty(HelperPresentation.Gap(new GoalGap(goal, reason)));
    }

    // ---- 5. every ENGINE decided about the character's level (DRA-71 D3, plan P5) --------

    /// <summary>
    /// **THE FOUNDER'S MUST, PAIRED BOTH WAYS** (smoke item 2: *"recs MUST factor it"*).
    ///
    /// <para>An engine is an <see cref="HelperGoalShape.Answered"/> goal, so the two tables
    /// have to agree in both directions: a goal that gained an engine without a level decision
    /// fails here, and a level decision left behind by a goal that went back to Deferred fails
    /// here too. Null is "nobody decided", which is the only thing a pairing can catch — *"level
    /// does not apply to this one"* and *"nobody thought about level for this one"* look
    /// identical on screen, and the second is how a MUST quietly becomes a maybe.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryEngineHasADecidedLevelUseAndOnlyEnginesHaveOne(HelperGoal goal) =>
        Assert.Equal(
            Recommendations.ShapeFor(goal) == HelperGoalShape.Answered,
            Recommendations.LevelUseFor(goal) is not null);

    /// <summary>An exemption owes a REASON, and a consuming engine must not carry one. A
    /// reason that drifted onto the wrong row is as wrong as one that went missing — the
    /// pairing is what makes the table readable as a decision rather than as prose.</summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void AnExemptEngineOwesAReasonAndAConsumingOneDoesNot(HelperGoal goal) =>
        Assert.Equal(
            Recommendations.LevelUseFor(goal) == Recommendations.LevelUse.Exempt,
            Recommendations.LevelExemptReason(goal).Length > 0);

    /// <summary>
    /// **THE ROW WITH TEETH: a declared level use is asserted as BEHAVIOUR, not as a table
    /// entry.**
    ///
    /// <para>The same fixture is ranked at level 12 and at level 60. An engine that says it
    /// CONSUMES the level must answer differently — otherwise its row is a comment, which is
    /// exactly the shape trap 34 warns about one level up ("a guard that forbids the wrong
    /// thing cannot see a missing thing", and a table nobody checks forbids nothing at all).
    /// An engine that says it is EXEMPT must answer identically, so an exemption that stops
    /// being true fails rather than going quietly stale.</para>
    ///
    /// <para><b>The fixture is asserted to produce candidates first</b>, because "identical at
    /// two levels" is vacuously true of an engine that returned nothing (trap 78: a guard
    /// aimed at nothing is green). That check is what makes the exempt half worth
    /// running.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void ADeclaredLevelUseIsProvedByRunningTheEngineAtTwoLevels(HelperGoal goal)
    {
        if (Recommendations.LevelUseFor(goal) is not { } use) return;

        var low = Answers(goal, 12);
        var high = Answers(goal, 60);

        Assert.NotEmpty(low);
        if (use == Recommendations.LevelUse.Consumes) Assert.NotEqual(low, high);
        else Assert.Equal(low, high);
    }

    /// <summary>
    /// One engine's answers as a comparable projection — what the player would READ, not what
    /// the engine happens to hold. Zone, weight and the rendered why-lines, so both a
    /// re-order and a changed sentence count as a difference; rendering through
    /// <see cref="HelperPresentation.Why"/> means a fact that moved the ranking without ever
    /// reaching the screen would not be mistaken for the engine consuming a level.
    /// </summary>
    private static string[] Answers(HelperGoal goal, int level) =>
        [.. Recommendations.Rank(LevelFixture(level), [goal]).Top.Select(r =>
            $"{r.Zone}|{r.Subject}|{r.Weight:0.0000}|"
            + string.Join("¦", r.Why.Select(HelperPresentation.Why)))];

    /// <summary>
    /// One fixture that feeds all FIVE engines at once — a farmed low-level camp, a faction
    /// those same kills move, a race unlock that wants it, and (since DRA-71 D6) a worn item
    /// with a catalog upgrade that drops in the same zone.
    ///
    /// <para>The creatures conned L8–12, so a level-60 character has outgrown them by any
    /// reading and a level-12 one has not. Everything else is held still: the point of the
    /// row above is that the ONLY thing that differs between the two runs is the level.</para>
    ///
    /// <para><b>The gear half is deliberately in the SAME zone.</b> A Farm Gear answer that
    /// named a zone with no history could not be affected by the level under any reading, so a
    /// claim about level would pass for the wrong reason — "identical at two levels" is vacuous
    /// against an engine the level could never have reached. Anchoring it on Lower Guk, which
    /// IS the outgrown zone, is the arrangement where a borrowed P6 discount would show
    /// up.</para>
    ///
    /// <para><b>DRA-84 D2 gives it a <see cref="ZoneLevels"/>, and the band is the FIXTURE's
    /// own rather than the shipped page's.</b> It is 8–12 because that is what this fixture's
    /// creatures conned at, so the gate's TOP arm is the thing that moves between the two runs:
    /// at 12 the band is in reach and the row stands, at 60 it is 48 levels under and the row is
    /// refused. The real eqlwiki row for Lower Guk is `30-50+`, which is an OPEN top and would
    /// exercise the other arm — it is asserted against the shipped catalog in
    /// <c>RecommendationsGearTests</c>, which is where a claim about the wiki belongs. Nothing
    /// here is a statement about a wiki page.</para>
    /// </summary>
    private static HelperInputs LevelFixture(int level)
    {
        var dump = new FactionsFile.Snapshot("factions.txt", DateTime.Today,
            [new FactionsFile.Standing(1, "Frogloks of Guk", 1200, 800)]);
        MobSummary[] pool =
        [
            // The loot is DRA-71 D7's half: a mote, so Farm Motes has something to rank, and a
            // sellable drop, so Make Money and Farm to Sell do. Both ride the SAME creature in
            // the SAME zone as everything else — see the summary for why every engine has to
            // be anchored on Lower Guk or an exemption passes for the wrong reason.
            new("a froglok tad", 200, 200, 30, 0, 0,
                [new MobLoot("Mote of Major Potential", 6, 3.0),
                 new MobLoot("Froglok Blood", 12, 6.0)])
            {
                Zone = "Lower Guk",
                LevelMin = 8,
                LevelMax = 12,
                Factions = [new MobFactionHit("Frogloks of Guk", 5, 200)],
            },
        ];
        SessionRow[] sessions =
        [
            // Copper is non-zero so the Make Money engine has a rate to rank — a zone that
            // earned nothing draws its gap instead, and "identical at two levels" would then be
            // vacuously true of an engine that returned nothing (trap 78).
            new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(5),
                5 * 3600, 5 * 3600, "ended", "Lower Guk", 0, 60, 50_000, 0, 0, 0, "", ""),
        ];
        UnlockProgress[] races =
        [
            new("Untapped Potential: Races", "Race Unlock - Froglok", "Froglok", false, false,
                [new UnlockCriterion(
                    UnlockNeed.MaxFaction, "Get maximum faction with Frogloks of Guk.",
                    "Frogloks of Guk", false)]),
        ];

        var worn = new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
            ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]));
        var catalog = new ItemCatalog([
            new ItemCatalog.Record
            {
                Name = "Froglok Bone Helm", StatsText = "Slot: HEAD\nAC: 9",
                Slots = ["HEAD"], Ac = 9, DropZones = ["Lower Guk"],
            },
        ]);

        var zones = ZoneHistory.Fold(sessions, pool);
        return new HelperInputs(
            zones, pool, dump, ["Frogloks of Guk"],
            races, races, [], true, [], [], null,
            new ResolvedLevel(level, LevelSource.Observed, new DateTime(2026, 9, 12, 20, 0, 0)))
        {
            Worn = [worn],
            Items = catalog,
            // DRA-71 D7. The mote fold over the same pool and the same rollup — never a second
            // one — and a sale so the money engines price a drop from measurement rather than
            // falling through to the catalog arm this fixture does not exercise.
            Motes = MoteHistory.Fold(pool, zones),
            Sales = [new SaleRoll("Froglok Blood", 4, 320)],
            // DRA-84 D2. See the summary: the fixture's own band, matching the fixture's own
            // conned creatures, so the gate's TOP arm is what differs between 12 and 60.
            Bands = new ZoneLevels(
                new Dictionary<string, ZoneLevels.Band> { ["Lower Guk"] = new(8, 12, "8-12") },
                new Dictionary<string, string>()),
        };
    }

    // ---- fixtures ------------------------------------------------------------------------

    public static TheoryData<HelperGoal> Goals()
    {
        var data = new TheoryData<HelperGoal>();
        foreach (var goal in Enum.GetValues<HelperGoal>()) data.Add(goal);
        return data;
    }

    public static TheoryData<HelperDoorKind> DoorKinds()
    {
        var data = new TheoryData<HelperDoorKind>();
        foreach (var kind in Enum.GetValues<HelperDoorKind>()) data.Add(kind);
        return data;
    }

    public static TheoryData<GoalGapReason> GapReasons()
    {
        var data = new TheoryData<GoalGapReason>();
        foreach (var reason in Enum.GetValues<GoalGapReason>()) data.Add(reason);
        return data;
    }

    public static TheoryData<Type> WhyFactTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in Subtypes()) data.Add(type);
        return data;
    }

    internal static List<Type> Subtypes() =>
        [.. typeof(WhyFact).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(WhyFact)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>One of every fact shape, built from its own constructor with plausible
    /// values. Reflection rather than nine hand-written instances, for the reason the sweep
    /// itself exists: a hand-written list of fixtures stops covering the set the day the set
    /// grows, which is the same trap one level down.</summary>
    internal static WhyFact Build(Type type)
    {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();
        return (WhyFact)ctor.Invoke(args);
    }

    private static object Sample(Type type)
    {
        if (type == typeof(string)) return "Lower Guk";
        if (type == typeof(int)) return 3;
        if (type == typeof(double)) return 7.5;
        // Coin is a long everywhere in this repo (StatsSnapshot.FormatCoin takes one), so the
        // two DRA-71 D7 money facts take one. 320 copper is "3s 2c" — a value the formatter
        // prints with two denominations, so a sentence that dropped one would be visible.
        if (type == typeof(long)) return 320L;
        if (type == typeof(Evidence)) return Evidence.Personal;
        throw new InvalidOperationException(
            $"A WhyFact takes a {type.Name}, which this fixture cannot make up. Add an arm — "
            + "the sweep is only as complete as the values it can construct.");
    }
}
