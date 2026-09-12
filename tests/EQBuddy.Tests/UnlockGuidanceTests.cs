using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// What a player can DO about an unlock criterion (DRA-65, Founder ask: *"each Unlock needs
/// more guided detail"*).
///
/// The achievement fixtures are Hateborne's and Averaj's own dumps, and the faction fixture
/// is Hateborne's pair — the same real files <see cref="UnlockRequirementsTests"/> reads,
/// for the same reason: the four spellings that disagree between the two dumps only exist in
/// real data, and the LOG is now a third spelling in the same comparison.
///
/// The POOL is seeded by hand, because the point of every assertion here is what the
/// guidance says for a given observation — and a fixture log that happened to contain the
/// right faction hits would be staging the wrong thing (trap 23's shape: a real state, and
/// the wrong one).
/// </summary>
public class UnlockGuidanceTests
{
    private static List<UnlockProgress> Races(string who = "hateborne") =>
        UnlockRequirements.Races(AchievementsImport.Parse(File.ReadAllLines(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "fixtures", "achievements", $"{who}.txt"))));

    private static List<UnlockProgress> Classes(string who = "hateborne") =>
        UnlockRequirements.Classes(AchievementsImport.Parse(File.ReadAllLines(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "fixtures", "achievements", $"{who}.txt"))));

    private static FactionsFile.Snapshot Factions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "factions", "hateborne.txt");
        return new FactionsFile.Snapshot(path, DateTime.Now,
            FactionsFile.Parse(File.ReadAllLines(path)));
    }

    /// <summary>A pooled creature that moved one faction — <see cref="MobHistory.Pool"/>'s own
    /// output shape, keyed on (name, zone) with the per-kill delta and the hit count.</summary>
    private static MobSummary Mover(string name, string zone, string faction, int delta, int hits) =>
        new(name, hits, hits, 12.0, 0, 0, [])
        {
            Zone = zone,
            Factions = [new MobFactionHit(faction, delta, hits)],
        };

    private static UnlockProgress Race(string subject, string who = "hateborne") =>
        Races(who).First(u => u.Subject == subject);

    private static UnlockCriterion Faction(UnlockProgress u, string faction) =>
        u.Actionable.First(c => c.Need == UnlockNeed.MaxFaction && c.Subject == faction);

    private static UnlockGuidanceRow Resolve(
        UnlockProgress u, UnlockCriterion c, IReadOnlyList<MobSummary>? pool = null,
        FactionsFile.Snapshot? factions = null,
        IEnumerable<SkyQuestChecklistItem>? sky = null,
        IReadOnlyCollection<string>? completed = null,
        QuestCatalog? catalog = null) =>
        UnlockGuidance.Resolve(u, c, factions, pool, sky, completed, catalog);

    // ---- A1: the faction row, with and without a mover -------------------------------

    /// <summary>
    /// **The row with NO observed movers gains a door and nothing else.** Absence of evidence
    /// is silence — the one rule trap 73 charged us for, and the one a "guided detail"
    /// feature is most likely to break by filling every field it has.
    /// </summary>
    [Fact]
    public void AFactionNobodyHasFarmedGainsTheDoorAndNoSentences()
    {
        var highElf = Race("High Elf");
        var keepers = Faction(highElf, "Keepers of the Art");

        var g = Resolve(highElf, keepers, pool: [], factions: Factions());

        Assert.Empty(g.Movers);
        Assert.Equal("", g.Estimate);
        Assert.Equal("", g.CapNote);
        Assert.Equal("", g.Pieces);
        Assert.Empty(g.Lines);
        // The door is the half that does not depend on having played: it is a name and a
        // link, and it costs eqlwiki nothing until the player clicks it.
        Assert.NotNull(g.Door);
        Assert.Equal(UnlockDoorKind.WikiFaction, g.Door!.Kind);
        Assert.Equal("Keepers of the Art", g.Door.Target);
        Assert.False(g.IsEmpty);
    }

    /// <summary>
    /// **The seeded mover, and the arithmetic off the real dump.** Keepers of the Art sits at
    /// -950 in Hateborne's own faction dump, which the dump itself calls 2,950 from maxed —
    /// so at +4 a kill the estimate is ceil(2950 / 4) = 738. Predicted before the assertion
    /// was written, from the fixture rather than from a run.
    /// </summary>
    [Fact]
    public void AnObservedRaiserGetsItsLineAndTheKillsToGoEstimate()
    {
        var highElf = Race("High Elf");
        var keepers = Faction(highElf, "Keepers of the Art");
        var pool = new List<MobSummary>
        {
            Mover("a Felwithe guard", "Northern Felwithe", "Keepers of the Art", 4, 12),
        };

        var g = Resolve(highElf, keepers, pool, Factions());

        var mover = Assert.Single(g.Movers);
        Assert.Equal(
            "Your kills of a Felwithe guard in Northern Felwithe moved it +4 each — seen on 12 of your kills.",
            mover);
        Assert.Equal(
            "≈738 more kills of a Felwithe guard in Northern Felwithe at +4 each — an estimate from your own log, not a target.",
            g.Estimate);
        // The two sentences are drawn in this order, and the ORDER has one producer too.
        Assert.Equal([mover, g.Estimate], g.Lines);
    }

    /// <summary>A cost is the same measurement read the other way, and it is the one a
    /// grinder most needs. Negative movers carry no estimate — there is no rate at which
    /// losing faction reaches a maximum.</summary>
    [Fact]
    public void AnObservedCostIsNamedAsACostAndNeverEstimated()
    {
        var highElf = Race("High Elf");
        var keepers = Faction(highElf, "Keepers of the Art");
        var pool = new List<MobSummary>
        {
            Mover("a High Elf citizen", "Northern Felwithe", "Keepers of the Art", -5, 3),
        };

        var g = Resolve(highElf, keepers, pool, Factions());

        Assert.Equal(
            "Your kills of a High Elf citizen in Northern Felwithe cost you 5 each — seen on 3 of your kills.",
            Assert.Single(g.Movers));
        Assert.Equal("", g.Estimate);
    }

    /// <summary>
    /// The cap is three EACH WAY, and a cap that withheld something says so (trap 50). Four
    /// raisers and four costs: six lines, and one sentence naming the two that are missing.
    /// </summary>
    [Fact]
    public void TheCapIsThreeEachWayAndTheSurvivingCapSaysSo()
    {
        var highElf = Race("High Elf");
        var keepers = Faction(highElf, "Keepers of the Art");
        var pool = new List<MobSummary>
        {
            Mover("raiser one", "Felwithe", "Keepers of the Art", 9, 4),
            Mover("raiser two", "Felwithe", "Keepers of the Art", 7, 4),
            Mover("raiser three", "Felwithe", "Keepers of the Art", 5, 4),
            Mover("raiser four", "Felwithe", "Keepers of the Art", 1, 40),
            Mover("cost one", "Felwithe", "Keepers of the Art", -9, 4),
            Mover("cost two", "Felwithe", "Keepers of the Art", -7, 4),
            Mover("cost three", "Felwithe", "Keepers of the Art", -5, 4),
            Mover("cost four", "Felwithe", "Keepers of the Art", -1, 40),
        };

        var g = Resolve(highElf, keepers, pool, Factions());

        Assert.Equal(6, g.Movers.Count);
        // Biggest mover first each way, so the fourth-best is the one withheld — not the
        // rarest, which is what a kill-count sort would have dropped.
        Assert.Contains("raiser one", g.Movers[0]);
        Assert.Contains("cost one", g.Movers[3]);
        Assert.DoesNotContain(g.Movers, m => m.Contains("raiser four"));
        Assert.Equal(
            "2 more creatures in your log move this faction — the 3 biggest each way are shown.",
            g.CapNote);
        // The estimate reads off the BEST raiser: 2,950 / 9 = 328 (ceiling).
        Assert.Contains("≈328 more kills of raiser one", g.Estimate);
    }

    /// <summary>
    /// **A GRANTED unlock still shows the movers, and the tick is untouched.** Hateborne's
    /// Dark Elf unlock is complete with all three faction criteria flagged, while the faction
    /// dump reads 0/2000 for those factions — the children were marked when the parent
    /// completed. That is a fact about the TICK, and the movers are a fact about the player's
    /// own kills: suppressing them would be the inheritance bug leaking into a surface it has
    /// no business reaching.
    /// </summary>
    [Fact]
    public void AnInheritedUnlockStillShowsWhatYourOwnKillsDid()
    {
        var darkElf = Race("Dark Elf");
        Assert.True(darkElf.Inherited, "the Dark Elf fixture is the granted-unlock case");
        var criterion = darkElf.Actionable.First(c => c.Need == UnlockNeed.MaxFaction);
        var pool = new List<MobSummary>
        {
            Mover("a spurned coalition thug", "Neriak", criterion.Subject, 3, 6),
        };

        var g = Resolve(darkElf, criterion, pool, Factions());

        Assert.Contains("a spurned coalition thug", Assert.Single(g.Movers));
        // Nothing here reports "done" at all — the tick is UnlockLayout's, off the faction
        // dump, and this type has no field for it by construction.
        Assert.NotNull(g.Door);
    }

    /// <summary>
    /// The LOG's spelling is a third source, and it is folded by <c>FactionNames</c> rather
    /// than by a comparison written here — the dump says "Coalition of Tradefolk" and the
    /// achievements text says "Coalition of Tradesfolk", one letter apart.
    /// </summary>
    [Fact]
    public void AMoverIsMatchedThroughTheSameNameFoldTheDumpGoesThrough()
    {
        var freeport = Race("Human (Freeport)");
        var tradesfolk = Faction(freeport, "Coalition of Tradesfolk");
        // The pool carries the FACTION DUMP's spelling, which is what the log writes.
        var pool = new List<MobSummary>
        {
            Mover("a Freeport guard", "Freeport", "Coalition of Tradefolk", 6, 8),
        };

        var g = Resolve(freeport, tradesfolk, pool, Factions());

        Assert.Contains("a Freeport guard", Assert.Single(g.Movers));
    }

    /// <summary>A maxed standing has nothing left to divide. The mover is still true and is
    /// still shown; the estimate is silence rather than "0 more kills".</summary>
    [Fact]
    public void AMaxedFactionKeepsItsMoversAndDropsTheEstimate()
    {
        // Frogloks of Guk is 2,000/2,000 in the fixture — asserted in UnlockRequirementsTests.
        var u = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test", "Test",
            false, false, [UnlockRequirements.Classify("Get maximum faction with Frogloks of Guk.", false)]);
        var pool = new List<MobSummary> { Mover("a guk froglok", "Guk", "Frogloks of Guk", 5, 20) };

        var g = Resolve(u, u.Actionable[0], pool, Factions());

        Assert.Single(g.Movers);
        Assert.Equal("", g.Estimate);
    }

    /// <summary>No faction dump at all: the door still opens and the movers are still the
    /// player's own, but there is no distance to divide so there is no estimate. "We have
    /// never been told where you stand" is a different state from "you are far away".</summary>
    [Fact]
    public void WithNoFactionDumpThereIsNoEstimateAndStillADoor()
    {
        var highElf = Race("High Elf");
        var keepers = Faction(highElf, "Keepers of the Art");
        var pool = new List<MobSummary> { Mover("a Felwithe guard", "Felwithe", "Keepers of the Art", 4, 12) };

        var g = Resolve(highElf, keepers, pool, factions: null);

        Assert.Single(g.Movers);
        Assert.Equal("", g.Estimate);
        Assert.Equal(UnlockDoorKind.WikiFaction, g.Door!.Kind);
    }

    // ---- A3: the Obtain row against the Sky checklist ---------------------------------

    /// <summary>
    /// **The piece count is the BAGS and the door is the guide.** Averaj's dump carries the
    /// class unlocks; the reward name on an Obtain row is the Sky checklist's own reward
    /// group, which is the join the two surfaces have never made.
    /// </summary>
    [Fact]
    public void AnObtainRowWithTheRewardOnTheChecklistCountsItsPiecesAndOpensTheSkyTab()
    {
        var (unlock, criterion, sky) = FirstObtainBackedBySky();

        var g = UnlockGuidance.Resolve(unlock, criterion, null, [], sky, null, null);

        var have = sky.Count(i => i.Acquired);
        Assert.Equal($"{have} of {sky.Count} pieces in hand — the Plane of Sky tab has the guide.",
            g.Pieces);
        Assert.Equal(UnlockDoorKind.SkyTab, g.Door!.Kind);
        Assert.Equal(QuestChecklistLayout.RewardKey(unlock.Subject, criterion.Subject), g.Door.Target);
        // Nothing about a faction, and no estimate: the shapes do not bleed into each other.
        Assert.Empty(g.Movers);
        Assert.Equal("", g.Estimate);
    }

    /// <summary>The turn-in is the Sky tab's own store and a different fact from the pieces —
    /// said as the Sky tab's answer, never folded into the count.</summary>
    [Fact]
    public void ARewardTheSkyTabHasMarkedTurnedInSaysSoBesideTheCount()
    {
        var (unlock, criterion, sky) = FirstObtainBackedBySky();
        var key = QuestChecklistLayout.RewardKey(unlock.Subject, criterion.Subject);

        var g = UnlockGuidance.Resolve(unlock, criterion, null, [], sky, [key], null);

        Assert.EndsWith("· marked turned in on the Plane of Sky tab.", g.Pieces);
        Assert.Contains($"of {sky.Count} pieces in hand", g.Pieces);
    }

    /// <summary>**A reward the checklist does not know gains NOTHING.** The row draws exactly
    /// what it drew before this feature existed — no piece count off an empty group, and no
    /// door onto a tab that has nothing to show.</summary>
    [Fact]
    public void AnObtainRowTheChecklistDoesNotKnowIsUntouched()
    {
        var (unlock, criterion, sky) = FirstObtainBackedBySky();
        // The same checklist, with this reward's rows removed: the "we have no group for
        // that" state, staged by subtraction rather than by an empty list — an empty
        // checklist would pass this assertion for the wrong reason.
        var others = SkyQuestDefaults.Items
            .Where(i => !i.Reward.Equals(criterion.Subject, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(others);
        Assert.NotEmpty(sky);

        var g = UnlockGuidance.Resolve(unlock, criterion, null, [], others, null, null);

        Assert.True(g.IsEmpty);
        Assert.Null(g.Door);
        Assert.Equal("", g.Pieces);
    }

    /// <summary>The first class-unlock Obtain row in a real dump whose reward the shipped Sky
    /// table actually carries, with that reward's rows. Found rather than hard-coded: the
    /// assertion is about the JOIN, and a hard-coded reward name would be a second copy of
    /// the Sky table living in a test.</summary>
    private static (UnlockProgress Unlock, UnlockCriterion Criterion, List<SkyQuestChecklistItem> Sky)
        FirstObtainBackedBySky()
    {
        foreach (var u in Classes("averaj").Concat(Classes()))
            foreach (var c in u.Actionable.Where(c => c.Need == UnlockNeed.Obtain))
            {
                var rows = SkyQuestDefaults.Items
                    .Where(i => i.ClassName.Equals(u.Subject, StringComparison.OrdinalIgnoreCase)
                                && i.Reward.Equals(c.Subject, StringComparison.OrdinalIgnoreCase))
                    .Select(i => i.Clone())
                    .ToList();
                if (rows.Count > 0) return (u, c, rows);
            }
        throw new InvalidOperationException(
            "no class-unlock Obtain row in either fixture names a shipped Sky reward — the "
            + "join this feature is built on has gone, and the tests above would pass vacuously.");
    }

    // ---- A4: the Task row -------------------------------------------------------------

    /// <summary>
    /// **Kerran's task keeps the dump's sentence and gains nothing.** 'Aid the Kerrans of
    /// Kerra Isle' is a modern server Task and is not in the classic wiki catalog; a fuzzy
    /// match would open the wrong page with complete confidence.
    /// </summary>
    [Fact]
    public void TheKerranTaskGetsSilenceRatherThanAStub()
    {
        var task = Races("averaj").Concat(Classes("averaj"))
            .SelectMany(u => u.Actionable.Select(c => (Unlock: u, Criterion: c)))
            .First(x => x.Criterion.Need == UnlockNeed.Task
                        && x.Criterion.Text.Contains("Kerra", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Aid the Kerrans of Kerra Isle",
            UnlockGuidance.QuotedQuestName(task.Criterion.Text));
        var catalog = QuestCatalog.LoadEmbedded();
        Assert.DoesNotContain(catalog.Quests,
            q => q.Name.Equals("Aid the Kerrans of Kerra Isle", StringComparison.OrdinalIgnoreCase));

        var g = UnlockGuidance.Resolve(task.Unlock, task.Criterion, null, [], null, null, catalog);

        Assert.True(g.IsEmpty);
        Assert.Null(g.Door);
    }

    /// <summary>A task whose quoted name the catalog DOES know gets the General-tab door. The
    /// quest is taken out of the shipped catalog rather than invented, so this proves the
    /// match rule and not a fixture.</summary>
    [Fact]
    public void ATaskNamingACatalogQuestGetsTheGeneralTabDoor()
    {
        var catalog = QuestCatalog.LoadEmbedded();
        var known = catalog.Quests.First(q => q.Name.Length > 3 && !q.Name.Contains('\''));
        var criterion = UnlockRequirements.Classify($"Complete the '{known.Name}' Task.", false);
        Assert.Equal(UnlockNeed.Task, criterion.Need);
        var unlock = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test",
            "Test", false, false, [criterion]);

        var g = UnlockGuidance.Resolve(unlock, criterion, null, [], null, null, catalog);

        Assert.Equal(UnlockDoorKind.GeneralTabQuest, g.Door!.Kind);
        Assert.Equal(known.Name, g.Door.Target);
        // A DOOR and no sentences: the catalog pane answers what the quest is, and saying it
        // twice is two producers of one fact.
        Assert.Empty(g.Lines);
    }

    /// <summary>The quoted name runs to the LAST quote, because a quest name may contain an
    /// apostrophe and the closing quote is the last one either way.</summary>
    [Fact]
    public void TheQuotedNameSurvivesAnApostropheInsideIt()
    {
        Assert.Equal("Ykesha's Fall",
            UnlockGuidance.QuotedQuestName("Complete the 'Ykesha's Fall' Task."));
        Assert.Equal("", UnlockGuidance.QuotedQuestName("Complete the daily Task."));
        Assert.Equal("", UnlockGuidance.QuotedQuestName(""));
    }

    /// <summary>A task with a name and NO catalog is silence too — the tab is readable with
    /// no catalog loaded, and a null store must not throw on the way past it.</summary>
    [Fact]
    public void ATaskWithNoCatalogAtAllIsSilent()
    {
        var criterion = UnlockRequirements.Classify("Complete the 'Whatever' Task.", false);
        var unlock = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test",
            "Test", false, false, [criterion]);

        Assert.True(UnlockGuidance.Resolve(unlock, criterion, null, null, null, null, null).IsEmpty);
    }

    // ---- the shapes that are decided to say nothing -----------------------------------

    /// <summary>Derived and Bypass rows are facts about how the unlock can happen, not work.
    /// They never reach a surface today — <see cref="UnlockProgress.Actionable"/> filters
    /// them — and the resolver is asked anyway, because "it cannot arrive" is a claim about
    /// today's filter and not about the resolver.</summary>
    [Theory]
    [InlineData("This achievement will autocomplete when you unlock Human or Wood Elf.")]
    [InlineData("This achievement can be bypassed using a Race Unlock Token.")]
    [InlineData("This achievement will autocomplete if your character was created as a Barbarian.")]
    public void ARowThatIsNotWorkGainsNothing(string text)
    {
        var criterion = UnlockRequirements.Classify(text, false);
        var unlock = new UnlockProgress("Untapped Potential: Races", "Race Unlock - Test",
            "Test", false, false, [criterion]);

        var g = UnlockGuidance.Resolve(unlock, criterion, Factions(), [], SkyQuestDefaults.Items,
            null, QuestCatalog.LoadEmbedded());

        Assert.True(g.IsEmpty);
    }
}
