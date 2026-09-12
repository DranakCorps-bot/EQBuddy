using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE RECOMMENDER** (DRA-70 D1; PRD §12 HOME-001..006).
///
/// <para>The rows that matter most are the JOIN (HOME-005 — a place serving two of your
/// goals outranks either alone, which the PRD calls the key differentiator) and the
/// SILENCES: a goal with no evidence must produce a named gap rather than a template with a
/// guessed number in it (trap 73), and a zone you have never died in must produce no
/// survival claim at all (HOME-006).</para>
/// </summary>
public class RecommendationsTests
{
    // ---- fixtures -------------------------------------------------------------------

    private static SessionRow Session(string zone, double hours, double xp, int deaths = 0) =>
        new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, xp, 0, 0, deaths, 0, "", "");

    private static MobSummary Mob(
        string name, string zone, int kills, double fightSeconds = 30,
        params (string Faction, int Delta)[] factions) =>
        new(name, kills, kills, fightSeconds, 0, 0, [])
        {
            Zone = zone,
            Factions = [.. factions.Select(f => new MobFactionHit(f.Faction, f.Delta, kills))],
        };

    private static FactionsFile.Snapshot Dump(params (string Name, int Value, int ToMax)[] rows) =>
        new("factions.txt", DateTime.Today,
            [.. rows.Select((r, i) => new FactionsFile.Standing(i + 1, r.Name, r.Value, r.ToMax))]);

    private static UnlockProgress Unlock(
        string subject, bool complete, params UnlockCriterion[] criteria) =>
        new("Untapped Potential: Races", $"Race Unlock - {subject}", subject, complete, false,
            criteria);

    private static UnlockCriterion Faction(string name, bool done = false) =>
        new(UnlockNeed.MaxFaction, $"Get maximum faction with {name}.", name, done);

    private static HelperInputs Inputs(
        IReadOnlyList<SessionRow>? sessions = null,
        IReadOnlyList<MobSummary>? pool = null,
        FactionsFile.Snapshot? factions = null,
        IReadOnlyList<string>? picked = null,
        IReadOnlyList<UnlockProgress>? races = null,
        bool hasAchievements = false)
    {
        var mobs = pool ?? [];
        return new HelperInputs(
            ZoneHistory.Fold(sessions ?? [], mobs), mobs, factions, picked ?? [],
            races ?? [], [], hasAchievements, [], [], null);
    }

    // ---- 1. the join, which is the feature -------------------------------------------

    /// <summary>
    /// **HOME-005, and the shape of the PRD's own worked example.**
    ///
    /// Lower Guk is where this character levels fastest AND where the creatures that raise
    /// the faction they picked live. Kaesora is faster on paper. The answer is Lower Guk,
    /// because the player's evening is spent in one place and serving two goals in one place
    /// is worth more than being marginally better at one of them.
    /// </summary>
    [Fact]
    public void APlaceServingTwoGoalsOutranksAFasterPlaceServingOne()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Kaesora", 4, 60), Session("Lower Guk", 4, 40)],
            pool:
            [
                Mob("a froglok tad", "Lower Guk", 200, factions: ("Frogloks of Guk", 5)),
                Mob("a hierophant", "Kaesora", 80),
            ],
            factions: Dump(("Frogloks of Guk", 500, 1500)),
            picked: ["Frogloks of Guk"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        var top = set.Top[0];
        Assert.Equal("Lower Guk", top.Zone);
        Assert.Equal(2, top.Goals.Count);
        Assert.Contains(HelperGoal.LevelUp, top.Goals);
        Assert.Contains(HelperGoal.WorkOnFaction, top.Goals);
        // Kaesora is still offered — the join is a SORT, never a filter that hides the
        // faster camp from somebody who only wanted experience.
        Assert.Contains(set.Top, r => r.Zone == "Kaesora");
        // And the merged row carries both engines' reasons and both engines' doors.
        Assert.Contains(top.Why, w => w is ZoneXpRateFact);
        Assert.Contains(top.Why, w => w is FactionStandingFact);
        Assert.Contains(top.Doors, d => d.Kind == HelperDoorKind.WikiFaction);
    }

    /// <summary>**The negative that keeps the join from being vacuous** (trap 39): the same
    /// two goals in DIFFERENT places do not merge, and the faster camp wins on its own
    /// terms.</summary>
    [Fact]
    public void TwoGoalsInDifferentPlacesStayTwoAnswers()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Kaesora", 4, 60), Session("Lower Guk", 4, 40)],
            pool:
            [
                Mob("a froglok tad", "Lower Guk", 200),
                Mob("a hierophant", "Kaesora", 80),
                Mob("a dervish cutthroat", "North Ro", 50, factions: ("Freeport Militia", 5)),
            ],
            factions: Dump(("Freeport Militia", 500, 1500)),
            picked: ["Freeport Militia"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        Assert.All(set.Top, r => Assert.Single(r.Goals));
        Assert.Equal("Kaesora", set.Top[0].Zone);
        Assert.Contains(set.Top, r => r.Zone == "North Ro");
    }

    /// <summary>An unlock whose faction grind happens where you already level joins the same
    /// way — the cross-domain chain is not a special case of two particular goals.</summary>
    [Fact]
    public void AnUnlocksFactionGrindJoinsTheZoneYouLevelIn()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Neriak Foreign Quarter", 5, 30)],
            pool: [Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
                factions: ("Dark Bargainers", 4))],
            factions: Dump(("Dark Bargainers", 1200, 800)),
            races: [Unlock("Dark Elf", false, Faction("Dark Bargainers"))],
            hasAchievements: true),
            [HelperGoal.LevelUp, HelperGoal.UnlockRaces]);

        var top = set.Top[0];
        Assert.Equal("Neriak Foreign Quarter", top.Zone);
        Assert.Equal(RecommendationKind.Zone, top.Kind);
        Assert.Equal(2, top.Goals.Count);
        // The unlock's own name survives inside its why-line, which is what makes the merged
        // headline honest: the row is named for the place you travel to, and the thing you
        // are working on is still said out loud.
        Assert.Contains(top.Why, w => w is UnlockScoreFact { Subject: "Dark Elf" });
    }

    // ---- 2. the cap says so out loud ---------------------------------------------------

    /// <summary>HOME-002 wants three, and trap 50 wants the cap to admit what it held
    /// back — the fourth-best camp is exactly the one somebody is hunting for.</summary>
    [Fact]
    public void TheCapIsThreeAndItReportsWhatItWithheld()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("A", 2, 20), Session("B", 2, 18), Session("C", 2, 16),
                       Session("D", 2, 14), Session("E", 2, 12)],
            pool: [Mob("m", "A", 5), Mob("m", "B", 5), Mob("m", "C", 5),
                   Mob("m", "D", 5), Mob("m", "E", 5)]),
            [HelperGoal.LevelUp]);

        Assert.Equal(3, set.Top.Count);
        Assert.Equal(2, set.Withheld);
        Assert.Contains("2 more answers", HelperPresentation.Cap(set.Withheld));
    }

    /// <summary>A per-row cap on the reasons, with the same rule: personal evidence survives
    /// the trim first, and the trimmed row keeps the order the engines emitted.</summary>
    [Fact]
    public void AnOverLongWhyListIsTrimmedAndSaysSo()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Neriak Foreign Quarter", 5, 30, deaths: 2)],
            pool:
            [
                Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
                    factions: ("Dark Bargainers", 4)),
                Mob("a Neriak trader", "Neriak Foreign Quarter", 60,
                    factions: ("Dark Bargainers", 3)),
                Mob("a Neriak smith", "Neriak Foreign Quarter", 30,
                    factions: ("Dark Bargainers", 2)),
                Mob("a Neriak servant", "Neriak Foreign Quarter", 10,
                    factions: ("Dark Bargainers", 1)),
            ],
            factions: Dump(("Dark Bargainers", 1200, 800)),
            picked: ["Dark Bargainers"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        var top = set.Top[0];
        Assert.True(top.Why.Count <= Recommendations.WhyCap);
        Assert.True(top.WithheldWhy > 0);
        Assert.NotEmpty(HelperPresentation.WithheldWhy(top.WithheldWhy));
    }

    // ---- 3. HOME-006 ---------------------------------------------------------------------

    /// <summary>
    /// **A zone you have never died in produces NO survival fact at all.**
    ///
    /// <para>This is HOME-006 at the only place it can be enforced — the engine. "You have
    /// not died here" is one sitting away from being false, and a recommender that offered it
    /// would be making exactly the claim the requirement forbids. The absence of evidence is
    /// silence, not reassurance.</para>
    /// </summary>
    [Fact]
    public void AZoneWithNoDeathsSaysNothingAboutDying()
    {
        var clean = Recommendations.Rank(Inputs(
            sessions: [Session("Lower Guk", 4, 40, deaths: 0)],
            pool: [Mob("a froglok tad", "Lower Guk", 200)]), [HelperGoal.LevelUp]);
        Assert.DoesNotContain(clean.Top[0].Why, w => w is ZoneDeathsFact);

        // The positive half, so the row above is not merely asserting that the fixture has
        // no deaths in it: the same zone with deaths DOES report them, as a count.
        var died = Recommendations.Rank(Inputs(
            sessions: [Session("Lower Guk", 4, 40, deaths: 3)],
            pool: [Mob("a froglok tad", "Lower Guk", 200)]), [HelperGoal.LevelUp]);
        var fact = Assert.Single(died.Top[0].Why.OfType<ZoneDeathsFact>());
        Assert.Equal(3, fact.Deaths);
    }

    // ---- 4. evidence tagging (HOME-003 / HOME-004) ----------------------------------------

    /// <summary>Every line the engine produces is tagged. An untagged line is not
    /// constructible — <see cref="WhyFact"/> takes the tag in its own constructor — so what
    /// this asserts is that no engine reaches for the wrong one.</summary>
    [Fact]
    public void EveryLineFromTheLevelUpAndFactionEnginesIsThePlayersOwnEvidence()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Lower Guk", 4, 40, deaths: 1)],
            pool: [Mob("a froglok tad", "Lower Guk", 200, factions: ("Frogloks of Guk", 5))],
            factions: Dump(("Frogloks of Guk", 500, 1500)),
            picked: ["Frogloks of Guk"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        Assert.All(set.Top.SelectMany(r => r.Why),
            w => Assert.Equal(Evidence.Personal, w.Evidence));
    }

    /// <summary>
    /// **The one catalog-sourced line D1 ships, asserted as a fact about this delivery.**
    ///
    /// <para>The Level Up engine has no generic camp catalog to fall back on and the plan
    /// PARKS one; the faction and unlock engines read the player's own dumps. So a Task
    /// criterion matching the shipped quest catalog by name is the only claim here that comes
    /// from a file EQBuddy ships — and it is the only one that carries the estimate label.</para>
    ///
    /// <para>This row is meant to be EDITED. The day D2's gear or motes engine adds a catalog
    /// line, somebody has to come here and say so, which is the difference between a decision
    /// and a drift.</para>
    /// </summary>
    [Fact]
    public void TheOnlyCatalogSourcedLineInThisDeliveryIsAMatchedQuestName()
    {
        var catalog = new QuestCatalog
        {
            Quests = [new QuestEntry { Name = "Aid the Kerrans of Kerra Isle" }],
        };
        var inputs = Inputs(hasAchievements: true,
            races: [Unlock("Kerra", false,
                new UnlockCriterion(UnlockNeed.Task,
                    "Complete the 'Aid the Kerrans of Kerra Isle' Task.",
                    "Aid the Kerrans of Kerra Isle", false))])
            with { Catalog = catalog };

        var set = Recommendations.Rank(inputs, [HelperGoal.UnlockRaces]);
        var line = Assert.Single(set.Top.SelectMany(r => r.Why).OfType<CatalogQuestFact>());
        Assert.Equal("Aid the Kerrans of Kerra Isle", line.Quest);
        Assert.Equal(Evidence.Catalog, line.Evidence);
        Assert.Contains(HelperPresentation.CatalogLabel, HelperPresentation.Why(line));
    }

    /// <summary>A quest the catalog does NOT know produces no line and no door — silence is
    /// the honest answer and a fuzzy match would open the wrong page with complete
    /// confidence (<c>UnlockGuidance</c>'s own rule, inherited).</summary>
    [Fact]
    public void AQuestTheCatalogDoesNotKnowProducesNoCatalogLine()
    {
        var inputs = Inputs(hasAchievements: true,
            races: [Unlock("Kerra", false,
                new UnlockCriterion(UnlockNeed.Task, "Complete the 'Something Else' Task.",
                    "Something Else", false))])
            with { Catalog = new QuestCatalog() };

        var set = Recommendations.Rank(inputs, [HelperGoal.UnlockRaces]);
        Assert.Empty(set.Top.SelectMany(r => r.Why).OfType<CatalogQuestFact>());
        Assert.DoesNotContain(set.Top.SelectMany(r => r.Doors),
            d => d.Kind == HelperDoorKind.QuestCatalog);
    }

    /// <summary>
    /// **HOME-003 is the SORT, and it is a BOOLEAN sort.**
    ///
    /// <para>"Personal evidence outranks generic advice" does not say more sentences outrank
    /// fewer, and ranking on the line count makes it say that — a faction grind with four
    /// movers would beat the fastest camp this character has ever farmed, on volume. A count
    /// is a proxy for confidence and a proxy is a claim about the world (trap 64b). So the
    /// question asked is HOME-003's own: does this rest on your play at all.</para>
    /// </summary>
    [Fact]
    public void PersonalEvidenceIsAskedAsAYesNoAndNotAsALineCount()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Lower Guk", 4, 40, deaths: 2)],
            pool: [Mob("a froglok tad", "Lower Guk", 200, fightSeconds: 25)]),
            [HelperGoal.LevelUp]);

        // Rate + cadence + deaths: three personal lines, all from the same zone's own row.
        Assert.Equal(3, set.Top[0].PersonalWhy);
        Assert.True(set.Top[0].HasPersonalEvidence);
    }

    /// <summary>
    /// **The sort that the line count used to get wrong**, kept as its own row because it is
    /// the one a reader would otherwise have to take on trust.
    ///
    /// <para>Kaesora is the fastest camp this character has. The faction grind in North Ro
    /// carries more SENTENCES — a standing, three movers and an estimate — and both rows serve
    /// exactly one goal and both rest on the player's own play. Ranking on the count put North
    /// Ro first, which is a recommender preferring the answer it had more to say about.</para>
    /// </summary>
    [Fact]
    public void MoreSentencesDoesNotOutrankABetterMeasuredCamp()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Kaesora", 4, 60)],
            pool:
            [
                Mob("a hierophant", "Kaesora", 80),
                Mob("a dervish cutthroat", "North Ro", 50, factions: ("Freeport Militia", 5)),
                Mob("a dervish priest", "North Ro", 40, factions: ("Freeport Militia", 4)),
                Mob("a dervish thief", "North Ro", 30, factions: ("Freeport Militia", 3)),
            ],
            factions: Dump(("Freeport Militia", 500, 1500)),
            picked: ["Freeport Militia"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        Assert.Equal("Kaesora", set.Top[0].Zone);
        Assert.True(set.Top.First(r => r.Zone == "North Ro").PersonalWhy > set.Top[0].PersonalWhy);
    }

    // ---- 5. the silences -------------------------------------------------------------------

    /// <summary>A goal with nothing behind it names the store it is waiting for, and does not
    /// invent a level-range table EQBuddy has never had (trap 73).</summary>
    [Fact]
    public void EachAnswerableGoalWithNoEvidenceProducesItsOwnNamedGap()
    {
        var set = Recommendations.Rank(HelperInputs.Nothing,
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction, HelperGoal.UnlockRaces]);

        Assert.Empty(set.Top);
        Assert.Contains(set.Gaps, g => g is { Goal: HelperGoal.LevelUp, Reason: GoalGapReason.NoPlayHistory });
        Assert.Contains(set.Gaps, g => g is { Goal: HelperGoal.WorkOnFaction, Reason: GoalGapReason.NoFactionDump });
        Assert.Contains(set.Gaps, g => g is { Goal: HelperGoal.UnlockRaces, Reason: GoalGapReason.NoAchievementsDump });
        Assert.All(set.Gaps, g => Assert.NotEmpty(HelperPresentation.Gap(g)));
    }

    /// <summary>A dump that exists with nothing picked is a different state from no dump at
    /// all, and the answer is a picker rather than a command.</summary>
    [Fact]
    public void AFactionDumpWithNothingPickedAsksForAPickAndNotForACommand()
    {
        var set = Recommendations.Rank(
            Inputs(factions: Dump(("Frogloks of Guk", 500, 1500))), [HelperGoal.WorkOnFaction]);

        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoFactionPicked, gap.Reason);
        Assert.DoesNotContain("command", HelperPresentation.Gap(gap), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A maxed faction is a finished job and not a recommendation — and when every
    /// pick is finished, the goal says THAT rather than looking like a data gap.</summary>
    [Fact]
    public void EveryPickedFactionMaxedReadsAsFinishedRatherThanAsMissingData()
    {
        var set = Recommendations.Rank(Inputs(
            factions: Dump(("Frogloks of Guk", 2000, 0)),
            picked: ["Frogloks of Guk"]), [HelperGoal.WorkOnFaction]);

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NothingLeftToDo, Assert.Single(set.Gaps).Reason);
    }

    /// <summary>An unlock that is already complete is not recommended, and a half the player
    /// has finished entirely reads as finished.</summary>
    [Fact]
    public void ACompletedUnlockIsNotRecommended()
    {
        var set = Recommendations.Rank(Inputs(
            hasAchievements: true,
            races: [Unlock("Dark Elf", true, Faction("Dark Bargainers", done: true))]),
            [HelperGoal.UnlockRaces]);

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NothingLeftToDo, Assert.Single(set.Gaps).Reason);
    }

    // ---- 6. the chips ----------------------------------------------------------------------

    /// <summary>**Nothing picked means all of them** — HOME-001's "goals/filters rather than a
    /// permanent wall of sections", read the way every other filter in this app works.</summary>
    [Fact]
    public void NoChipsPickedWeighsEveryGoal()
    {
        var inputs = Inputs(
            sessions: [Session("Lower Guk", 4, 40)],
            pool: [Mob("a froglok tad", "Lower Guk", 200)]);

        var none = Recommendations.Rank(inputs, []);
        var all = Recommendations.Rank(inputs, Recommendations.All);

        Assert.Equal(all.Top.Count, none.Top.Count);
        Assert.Equal(all.NotAnsweredYet, none.NotAnsweredYet);
        Assert.Equal(all.Gaps.Count, none.Gaps.Count);
        // And null is the same answer as empty — a caller with no stored selection at all.
        Assert.Equal(all.Top.Count, Recommendations.Rank(inputs, null).Top.Count);
    }

    /// <summary>A goal that is NOT picked contributes nothing — not a recommendation, not a
    /// gap and not a "not yet" line. A filter that still reported about what it filtered out
    /// would not be a filter.</summary>
    [Fact]
    public void AnUnpickedGoalIsSilentInEveryDirection()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Lower Guk", 4, 40)],
            pool: [Mob("a froglok tad", "Lower Guk", 200)]), [HelperGoal.LevelUp]);

        Assert.Empty(set.NotAnsweredYet);
        Assert.Empty(set.Gaps);
        Assert.All(set.Top, r => Assert.Equal([HelperGoal.LevelUp], r.Goals));
    }

    /// <summary>A deferred goal that IS picked comes back as one, so the room can say so.
    /// It is not a gap — a gap means a store is missing, and these are waiting on code.</summary>
    [Fact]
    public void APickedDeferredGoalComesBackAsNotAnsweredYetRatherThanAsAGap()
    {
        var set = Recommendations.Rank(HelperInputs.Nothing, [HelperGoal.FarmGear]);
        Assert.Equal([HelperGoal.FarmGear], set.NotAnsweredYet);
        Assert.Empty(set.Gaps);
        Assert.NotEmpty(HelperPresentation.NotAnsweredYet(HelperGoal.FarmGear));
    }

    // ---- 7. the doors are real ---------------------------------------------------------------

    /// <summary>
    /// **Every door a real ranking produces lands on a room that has actually landed, or on
    /// the wiki.**
    ///
    /// <para>The room counts this from a launched app (<c>helperDeadDoors</c>) because only a
    /// launched app can say a control exists; this catches the same failure without one, over
    /// doors the ENGINE emitted rather than doors a fixture named.</para>
    /// </summary>
    [Fact]
    public void NoDoorFromARealRankingLeadsNowhere()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Neriak Foreign Quarter", 5, 30)],
            pool: [Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
                factions: ("Dark Bargainers", 4))],
            factions: Dump(("Dark Bargainers", 1200, 800)),
            picked: ["Dark Bargainers"],
            races: [Unlock("Dark Elf", false, Faction("Dark Bargainers"))],
            hasAchievements: true),
            Recommendations.All);

        var doors = set.Top.SelectMany(r => r.Doors).ToList();
        Assert.NotEmpty(doors);
        foreach (var door in doors)
        {
            var address = HelperPresentation.AddressFor(door.Kind);
            if (address is null) { Assert.Equal(HelperDoorKind.WikiFaction, door.Kind); continue; }
            var parsed = ShellPages.ParseAddress(address);
            Assert.NotNull(parsed);
            Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
        }
    }

    /// <summary>One door per destination on a merged row. Two engines both offering the World
    /// room for the same zone is one link, not two — a row with the same word twice reads as
    /// a rendering bug.</summary>
    [Fact]
    public void AMergedRowDoesNotDrawTheSameDoorTwice()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Neriak Foreign Quarter", 5, 30)],
            pool: [Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
                factions: ("Dark Bargainers", 4))],
            factions: Dump(("Dark Bargainers", 1200, 800)),
            picked: ["Dark Bargainers"]),
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction]);

        var top = set.Top[0];
        Assert.Equal(2, top.Goals.Count);
        Assert.Equal(top.Doors.Count, top.Doors.Distinct().Count());
        Assert.Single(top.Doors, d => d.Kind == HelperDoorKind.World);
    }

    // ---- 8. the whole answer renders -----------------------------------------------------------

    /// <summary>
    /// **Every sentence a real ranking produces is non-empty.**
    ///
    /// <para>The must-list walks fact SHAPES; this walks what an actual fixture produced,
    /// including the pass-through lines <c>UnlockGuidance</c> wrote — which no reflection over
    /// <c>WhyFact</c> subtypes can reach, because they are one shape carrying many
    /// sentences.</para>
    /// </summary>
    [Fact]
    public void EverySentenceARealRankingProducesIsDrawable()
    {
        var set = Recommendations.Rank(Inputs(
            sessions: [Session("Neriak Foreign Quarter", 5, 30, deaths: 1)],
            pool: [Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
                factions: ("Dark Bargainers", 4))],
            factions: Dump(("Dark Bargainers", 1200, 800)),
            picked: ["Dark Bargainers"],
            races: [Unlock("Dark Elf", false, Faction("Dark Bargainers"))],
            hasAchievements: true),
            Recommendations.All);

        Assert.NotEmpty(set.Top);
        foreach (var rec in set.Top)
        {
            Assert.NotEmpty(HelperPresentation.Headline(rec));
            Assert.NotEmpty(rec.Why);
            foreach (var fact in rec.Why) Assert.NotEmpty(HelperPresentation.Why(fact));
            Assert.NotEmpty(rec.Doors);
        }
    }

    /// <summary>The faction sentences are <c>UnlockGuidance</c>'s own, word for word. Two
    /// surfaces wording one arithmetic are two answers, and this is what says the Helper did
    /// not grow a second phrasing.</summary>
    [Fact]
    public void TheFactionLinesAreUnlockGuidancesOwnSentences()
    {
        var pool = new[] { Mob("a Neriak guard", "Neriak Foreign Quarter", 120,
            factions: ("Dark Bargainers", 4)) };
        var dump = Dump(("Dark Bargainers", 1200, 800));

        var set = Recommendations.Rank(
            Inputs(pool: pool, factions: dump, picked: ["Dark Bargainers"]),
            [HelperGoal.WorkOnFaction]);

        var expected = UnlockGuidance.Faction("Dark Bargainers", dump, pool).Lines;
        Assert.NotEmpty(expected);
        var drawn = set.Top.SelectMany(r => r.Why).OfType<WordedFact>().Select(w => w.Text).ToList();
        Assert.Equal(expected, drawn);
    }

    /// <summary>A character with no history at all draws no recommendations and throws
    /// nothing — the state a fresh profile is in, which is the first one a new player
    /// sees.</summary>
    [Fact]
    public void AFreshProfileRanksNothingAndDoesNotThrow()
    {
        var set = Recommendations.Rank(HelperInputs.Nothing, Recommendations.All);
        Assert.Empty(set.Top);
        Assert.Equal(0, set.Withheld);
        Assert.Equal(5, set.NotAnsweredYet.Count);
        Assert.Equal(4, set.Gaps.Count);
    }
}
