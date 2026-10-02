using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Dump-proven ReadyNow, as ONE fact and a <see cref="Recommendations.Rank"/> key**
/// (DRA-748, DRA-728 D3; plan §5 option (a), Founder answer 2).
///
/// <para>Every Sky reward here is a REAL shipped one, found in <see cref="SkyChecklistRows"/>
/// rather than named — a hard-coded reward would be a second copy of the Sky table living in a
/// test. Every route is a real shipped <c>FactionRoutes.json</c> row over Hateborne's real
/// faction dump.</para>
///
/// <para><b>The committed negative is the card's own</b>: a checklist that ticks every piece,
/// beside an inventory dump that lacks one, is NOT ready — the tick is a claim nothing
/// un-ticks, and the dump is the only thing that can prove the bags.</para>
/// </summary>
public class ReadyNowTests
{
    private static readonly FactionRoutes Routes = FactionRoutes.LoadEmbedded();
    private static readonly QuestCatalog Catalog = QuestCatalog.LoadEmbedded();

    private static FactionsFile.Snapshot Hateborne()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "factions", "hateborne.txt");
        return new FactionsFile.Snapshot(path, DateTime.Now,
            FactionsFile.Parse(File.ReadAllLines(path)));
    }

    private static InventoryFile.Snapshot Bags(params (string Item, int Count)[] rows) =>
        new("inventory.txt", DateTime.Today,
            rows.ToDictionary(r => r.Item, r => r.Count, StringComparer.OrdinalIgnoreCase));

    private static InventoryFile.Snapshot BagsHolding(IEnumerable<SkyQuestChecklistItem> rows) =>
        Bags([.. rows.Select(r => r.QuestItem).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => (n, 1))]);

    /// <summary>The first shipped Sky reward with at least two pieces — two, so "missing one"
    /// and "holding one" are different states — as a class unlock whose only actionable row is
    /// "Obtain" that reward.</summary>
    private static (UnlockProgress Unlock, UnlockCriterion Criterion, List<SkyQuestChecklistItem> Sky)
        SkyReward(bool ticked = false)
    {
        var group = SkyChecklistRows.Items
            .GroupBy(i => (i.ClassName, i.Reward))
            .First(g => g.Count() >= 2 && g.All(i => i.QuestItem.Length > 0));
        var rows = group.Select(i => { var c = i.Clone(); c.Acquired = ticked; return c; }).ToList();
        var criterion = new UnlockCriterion(
            UnlockNeed.Obtain, $"Obtain {group.Key.Reward}.", group.Key.Reward, false);
        var unlock = new UnlockProgress("Untapped Potential: Classes",
            $"Class Unlock - {group.Key.ClassName}", group.Key.ClassName, false, false, [criterion]);
        return (unlock, criterion, rows);
    }

    private static UnlockGuidanceRow Sky(
        (UnlockProgress Unlock, UnlockCriterion Criterion, List<SkyQuestChecklistItem> Sky) r,
        InventoryFile.Snapshot? bags, IReadOnlyCollection<string>? completed = null) =>
        UnlockGuidance.Resolve(r.Unlock, r.Criterion, null, [], r.Sky, completed, null, null, bags);

    // ---- the Sky reward ------------------------------------------------------------------

    /// <summary>Every piece in the inventory dump and not turned in: ready, said as the DUMP's
    /// claim. The checklist is unticked on purpose — the dump proves it on its own.</summary>
    [Fact]
    public void ASkyRewardIsReadyWhenTheInventoryDumpHoldsEveryPiece()
    {
        var r = SkyReward(ticked: false);

        var g = Sky(r, BagsHolding(r.Sky));

        Assert.True(g.ReadyNow);
        Assert.Equal($"Your inventory dump holds all {r.Sky.Count} pieces — ready to turn in now.",
            g.Pieces);
    }

    /// <summary>**THE COMMITTED NEGATIVE** (the card's own): every piece ticked on the checklist,
    /// and an inventory dump that lacks one. Not ready, and the line says whose claim the ticks
    /// are and which piece the dump is missing.</summary>
    [Fact]
    public void AChecklistTickWithAnInventoryDumpLackingThePieceIsNotReady()
    {
        var r = SkyReward(ticked: true);
        var lacking = r.Sky[^1].QuestItem;

        var g = Sky(r, BagsHolding(r.Sky.Take(r.Sky.Count - 1)));

        Assert.False(g.ReadyNow);
        Assert.StartsWith(UnlockGuidance.SkyChecklistSaysAll, g.Pieces);
        Assert.Contains($"your inventory dump is missing {lacking}", g.Pieces);
        Assert.EndsWith("so it is not ready to turn in.", g.Pieces);
    }

    /// <summary>All ticked and NO dump: the checklist's claim, and the command that would prove
    /// it — never "ready", and never "in hand".</summary>
    [Fact]
    public void AllTickedWithNoInventoryDumpSaysTheChecklistSaysSoAndIsNotReady()
    {
        var g = Sky(SkyReward(ticked: true), bags: null);

        Assert.False(g.ReadyNow);
        Assert.Equal("The Sky checklist says all pieces acquired — run /outputfile inventory to "
            + "check they are still in your bags.", g.Pieces);
        Assert.DoesNotContain("in hand", g.Pieces);
    }

    /// <summary>Marked turned in on the Sky tab: the pieces in the bags buy nothing more, so it
    /// is not ready, whatever the dump holds.</summary>
    [Fact]
    public void ARewardMarkedTurnedInIsNotReadyWhateverTheBagsHold()
    {
        var r = SkyReward(ticked: true);
        var key = QuestChecklistLayout.RewardKey(r.Unlock.Subject, r.Criterion.Subject);

        var g = Sky(r, BagsHolding(r.Sky), completed: [key]);

        Assert.False(g.ReadyNow);
        Assert.EndsWith("· marked turned in on the Plane of Sky tab.", g.Pieces);
    }

    // ---- the faction route ---------------------------------------------------------------

    private static UnlockGuidanceRow Route(InventoryFile.Snapshot? bags, params MobSummary[] pool) =>
        UnlockGuidance.Faction("Dark Bargainers", Hateborne(), pool, Routes, Catalog, bags);

    /// <summary>The route the row SHOWS (Book of Turmoil Quest, one Dark Elf Decapitated Head
    /// per turn-in): ready at one held turn-in, not ready at none, and not ready with no dump.
    /// </summary>
    [Fact]
    public void AFactionRouteIsReadyWhenTheHeldItemsCoverOneTurnIn()
    {
        Assert.True(Route(Bags(("Dark Elf Decapitated Head", 1))).ReadyNow);
        Assert.False(Route(Bags(("Red Wine", 1))).ReadyNow);
        Assert.False(Route(bags: null).ReadyNow);
    }

    /// <summary>Ready is read over the SHOWN route only: Red Wine raises Dark Bargainers too,
    /// but the cap names it on the "more routes" line rather than spelling it out, and a rank
    /// that jumped on a route the row does not show would have no sentence under it.</summary>
    [Fact]
    public void AHeldItemForARouteTheCapDidNotSpellOutDoesNotMakeTheRowReady()
    {
        var g = Route(Bags(("Red Wine", 5)));

        Assert.Contains(g.RouteLines, l => l.Text.Contains("Bottle of Red Wine"));
        Assert.False(g.ReadyNow);
    }

    /// <summary>With a raiser in the pool the row is the player's own grind and the route is one
    /// "also" line — no route is shown, so nothing is ready (D2's rule, kept).</summary>
    [Fact]
    public void WithARaiserThePersonalRowIsNotMadeReadyByTheRoute()
    {
        var raiser = new MobSummary("a Neriak guard", 40, 40, 12.0, 0, 0, [])
        {
            Zone = "Neriak Foreign Quarter",
            Factions = [new MobFactionHit("Dark Bargainers", 4, 40)],
        };

        Assert.False(Route(Bags(("Dark Elf Decapitated Head", 3)), raiser).ReadyNow);
    }

    // ---- through Rank() -------------------------------------------------------------------

    private static SessionRow Session(string zone, double hours, double xp) =>
        new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, xp, 0, 0, 0, 0, "", "");

    /// <summary>A camp the Level Up engine answers for: four hours and real kills, the shape
    /// <c>RecommendationsTests</c> uses.</summary>
    private static readonly MobSummary Hierophant =
        new("a hierophant", 80, 80, 30, 0, 0, []) { Zone = "Kaesora" };

    private static HelperInputs WithCampAndUnlock(InventoryFile.Snapshot? bags)
    {
        var r = SkyReward(ticked: false);
        return new HelperInputs(
            ZoneHistory.Fold([Session("Kaesora", 4, 60)], [Hierophant]), [Hierophant], null, [],
            [], [r.Unlock], [], true, r.Sky, [], null)
        {
            Bags = bags,
        };
    }

    /// <summary>
    /// **A ready turn-in outranks a one-goal personal XP camp** — the cross-engine change the
    /// Founder said yes to. Without the dump the same unlock sits UNDER the camp, on
    /// <see cref="Recommendation.Weight"/> (0 of 1 done against the camp's rate), which is what
    /// proves the jump is the ReadyNow key and nothing else.
    /// </summary>
    [Fact]
    public void AReadyUnlockOutranksAOneGoalCampAndAnUnreadyOneDoesNot()
    {
        var sky = SkyReward().Sky;
        var goals = new[] { HelperGoal.LevelUp, HelperGoal.UnlockClasses };

        var ready = Recommendations.Rank(WithCampAndUnlock(BagsHolding(sky)), goals);
        var unready = Recommendations.Rank(WithCampAndUnlock(Bags()), goals);

        Assert.True(ready.Top[0].ReadyNow);
        Assert.Equal(RecommendationKind.Unlock, ready.Top[0].Kind);
        Assert.Equal("Kaesora", ready.Top[1].Zone);

        Assert.Equal("Kaesora", unready.Top[0].Zone);
        Assert.All(unready.Top, t => Assert.False(t.ReadyNow));
    }

    /// <summary>Goals.Count still sorts FIRST: a place serving two picked goals outranks a
    /// one-goal ready turn-in, because readiness is the second key and not the first.</summary>
    [Fact]
    public void ATwoGoalPlaceStillOutranksAReadyTurnIn()
    {
        var sky = SkyReward().Sky;
        var r = SkyReward();
        var inputs = new HelperInputs(
            ZoneHistory.Fold([Session("Lower Guk", 4, 40)],
                [new MobSummary("a froglok tad", 200, 200, 30, 0, 0, [])
                {
                    Zone = "Lower Guk",
                    Factions = [new MobFactionHit("Frogloks of Guk", 5, 200)],
                }]),
            [new MobSummary("a froglok tad", 200, 200, 30, 0, 0, [])
            {
                Zone = "Lower Guk",
                Factions = [new MobFactionHit("Frogloks of Guk", 5, 200)],
            }],
            new FactionsFile.Snapshot("factions.txt", DateTime.Today,
                [new FactionsFile.Standing(1, "Frogloks of Guk", 500, 1500)]),
            ["Frogloks of Guk"], [], [r.Unlock], [], true, r.Sky, [], null)
        {
            Bags = BagsHolding(sky),
        };

        var set = Recommendations.Rank(inputs,
            [HelperGoal.LevelUp, HelperGoal.WorkOnFaction, HelperGoal.UnlockClasses]);

        Assert.Equal("Lower Guk", set.Top[0].Zone);
        Assert.Equal(2, set.Top[0].Goals.Count);
        Assert.True(set.Top[1].ReadyNow);
    }

    /// <summary>The engine's OWN pick keeps a ready unlock: seven open unlocks, the ready one
    /// the least complete, and the pick of six still carries it to Rank() — or the key could
    /// never fire for the unlock that needs it most.</summary>
    [Fact]
    public void TheUnlockEnginesPickKeepsAReadyUnlockThatIsFarFromDone()
    {
        var r = SkyReward();
        // Six unlocks one task from done (1 of 2), and the ready one at 0 of 2.
        var nearlyDone = Enumerable.Range(1, 6).Select(i => new UnlockProgress(
            "Untapped Potential: Classes", $"Class Unlock - Other{i}", $"Other{i}", false, false,
            [
                new UnlockCriterion(UnlockNeed.Task, "Complete the 'X' Task.", "X", true),
                new UnlockCriterion(UnlockNeed.Task, "Complete the 'Y' Task.", "Y", false),
            ])).ToList();
        var ready = r.Unlock with
        {
            Criteria = [r.Criterion,
                new UnlockCriterion(UnlockNeed.Task, "Complete the 'Z' Task.", "Z", false)],
        };
        var inputs = new HelperInputs([], [], null, [], [], [.. nearlyDone, ready], [], true,
            r.Sky, [], null)
        {
            Bags = BagsHolding(r.Sky),
        };

        var set = Recommendations.Rank(inputs, [HelperGoal.UnlockClasses], cap: 20);

        Assert.Equal(ready.Subject, set.Top[0].Subject);
        Assert.True(set.Top[0].ReadyNow);
    }

    /// <summary>The two engines that can never prove a dump never set it — the forbid half,
    /// paired with the must-list above (trap 34).</summary>
    [Fact]
    public void NoOtherEngineSetsReadyNow()
    {
        var set = Recommendations.Rank(new HelperInputs(
            ZoneHistory.Fold([Session("Kaesora", 4, 60)], [Hierophant]), [Hierophant], null, [],
            [], [], [], false,
            [], [], null) { Bags = Bags(("Red Wine", 9)) }, [HelperGoal.LevelUp]);

        Assert.NotEmpty(set.Top);
        Assert.All(set.Top, t => Assert.False(t.ReadyNow));
    }

    // ---- the §5 divergence, closed ---------------------------------------------------------

    /// <summary>
    /// **One answer, three readers** (trap 4). A faction the achievements dump flags DONE and
    /// the faction dump puts at 5/1995, and one it flags NOT done that the faction dump puts at
    /// the cap. The Unlocks tab's ticks, the tab header's count and the Helper's score must all
    /// say 1 of 2 — the faction dump's answer — where they used to say 1 (tab) and 1 (Helper)
    /// for opposite reasons, and disagree the moment the two dumps did.
    /// </summary>
    [Fact]
    public void TheTabsTicksAndTheHelpersScoreAreOneAnswerFromTheFactionDump()
    {
        var dump = new FactionsFile.Snapshot("factions.txt", DateTime.Today,
        [
            new FactionsFile.Standing(1, "Dreadguard Outer", 5, 1995),
            new FactionsFile.Standing(2, "Dark Bargainers", 2000, 0),
        ]);
        var unlock = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test", "Test",
            false, false,
            [
                new UnlockCriterion(UnlockNeed.MaxFaction,
                    "Get maximum faction with Dreadguard Outer.", "Dreadguard Outer", Done: true),
                new UnlockCriterion(UnlockNeed.MaxFaction,
                    "Get maximum faction with Dark Bargainers.", "Dark Bargainers", Done: false),
            ]);

        var tab = UnlockLayout.Groups([unlock], dump, UnlockLayout.RacesHeading).Single();
        Assert.Equal([false, true], tab.Rows.Select(r => r.Acquired));
        Assert.Equal((1, 2), unlock.Score(dump));

        var set = Recommendations.Rank(new HelperInputs(
            [], [], dump, [], [unlock], [], [], true, [], [], null), [HelperGoal.UnlockRaces]);
        var fact = Assert.Single(set.Top[0].Why.OfType<UnlockScoreFact>());
        Assert.Equal((tab.Rows.Count(r => r.Acquired), tab.Rows.Count), (fact.Done, fact.Total));
        // And the criterion the Helper still offers is the one the TAB shows unticked.
        Assert.DoesNotContain(set.Top[0].Doors,
            d => d.Kind == HelperDoorKind.WikiFaction && d.Target == "Dark Bargainers");
        Assert.Contains(set.Top[0].Doors,
            d => d.Kind == HelperDoorKind.WikiFaction && d.Target == "Dreadguard Outer");
    }

    /// <summary>With no faction dump the achievements flag is the only record, and the tab's
    /// tick now reads it too — said as the achievements record's, beside the command that would
    /// give the standing.</summary>
    [Fact]
    public void WithNoFactionDumpTheTabTicksFromTheAchievementsFlagAndSaysSo()
    {
        var unlock = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test", "Test",
            false, false,
            [new UnlockCriterion(UnlockNeed.MaxFaction,
                "Get maximum faction with Dark Bargainers.", "Dark Bargainers", Done: true)]);

        var row = UnlockLayout.Groups([unlock], null, UnlockLayout.RacesHeading).Single().Rows.Single();

        Assert.True(row.Acquired);
        Assert.Equal((1, 1), unlock.Score(null));
        Assert.StartsWith("done by the game's achievements record", row.Detail);
    }

    /// <summary>A GRANTED unlock's flags are not evidence anywhere — the Dark Elf guard, now
    /// applied to the count as well as the tick, so the header no longer says 3/3 over three
    /// unticked rows.</summary>
    [Fact]
    public void AGrantedUnlocksFlagsCountForNothingInTheScoreEither()
    {
        var granted = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test", "Test",
            true, true,
            [
                new UnlockCriterion(UnlockNeed.MaxFaction,
                    "Get maximum faction with Dark Bargainers.", "Dark Bargainers", Done: true),
                new UnlockCriterion(UnlockNeed.Bypass,
                    "This achievement can be bypassed using a Race Unlock Token.", "", Done: true),
            ]);

        Assert.Equal((0, 1), granted.Score(null));
        Assert.Equal((0, 1), granted.Score(Hateborne()));
    }

    /// <summary>The Helper's row sentence names both dumps, because since D3 it counts from
    /// both.</summary>
    [Fact]
    public void TheScoreSentenceNamesBothDumps() =>
        Assert.Equal("Test: 1 of 2 requirements done, by your achievements and faction dumps.",
            HelperPresentation.Why(new UnlockScoreFact("Test", 1, 2)));
}
