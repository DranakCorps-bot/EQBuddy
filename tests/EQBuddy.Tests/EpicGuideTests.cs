using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Delivery 3 (DRA-41): <b>Epic 1.0 on the guided model</b> — the third authoring state, the
/// fourth progress home, and the Epic tab's own cutover.
///
/// <para>Three claims are held open here, and each one is asserted from BOTH sides because a
/// guard that only ever goes green is vacuous coverage (trap 34):</para>
///
/// <list type="number">
/// <item><b><see cref="GuideAuthoring.Transcribed"/> says one thing and refuses the rest.</b>
///   The page states a sentence; we carry it in WHAT. Who, where, when and how are REFUSED
///   rather than merely unrequired — "not required" is what lets a transformer fill them the
///   first time one looks easy, and 486 of those would be trap 73 at scale.</item>
/// <item><b>An epic objective's tick is its checklist ROW's box.</b> The row the objective was
///   generated from, in the store the loot auto-tick and the master "Epic complete" button
///   already write — never a second copy of it (trap 4).</item>
/// <item><b>The cutover is per CLASS and total.</b> A guided class's section groups become one
///   guided group; a class with no guide comes back the same object (Founder lock 5).</item>
/// </list>
/// </summary>
public sealed class EpicGuideTests : IDisposable
{
    private const string Dranak = "dranak_freeport";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"epic-guide-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static GuideCatalog Shipped => GuideCatalog.Default;

    private static IReadOnlyList<Guide> Epics =>
        [.. Shipped.Guides.Where(g => g.GuideType == GuideType.EpicQuest)];

    private static Guide Epic(string className) =>
        GuideChecklistProjection.EpicGuideFor(Shipped, className)
        ?? throw new InvalidOperationException($"no epic guide for {className}");

    /// <summary>The shipped epic checklist, seeded exactly as <c>AppSettings</c> seeds it —
    /// the same rows, with the same ids, so a routing assertion is about the real data rather
    /// than about a fixture that happens to line up.</summary>
    private static AppSettings SettingsWithEpicRows()
    {
        var settings = new AppSettings();
        settings.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        return settings;
    }

    private static List<EpicQuestChecklistItem> RowsOf(AppSettings settings, string className) =>
        [.. settings.EpicQuestChecklist
            .Where(i => i.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))];

    // ---- 1. Transcribed: what it claims, and what it refuses -------------------------

    /// <summary>The shippable shape, so every refusal below is proved to be about the thing it
    /// names rather than about the fixture being broken in some other way.</summary>
    [Fact]
    public void ATranscribedStepCarryingOnlyThePagesSentenceIsShippable()
    {
        Assert.Empty(Fixture().Validate([]));
    }

    [Theory]
    [InlineData("who", "WHO")]
    [InlineData("where", "WHERE")]
    [InlineData("when", "WHEN")]
    [InlineData("how", "HOW")]
    public void ATranscribedStepThatFillsOneOfTheFourForbiddenQuestionsIsRefused(
        string field, string named)
    {
        var problems = Fixture($""", "{field}": "something the page never said" """).Validate([]);

        Assert.Contains(problems, p => p.Contains($"fills {named}", StringComparison.Ordinal));
    }

    /// <summary>The one thing it DOES claim. A transcribed step with no sentence is a row that
    /// asserts a page said something and cannot say what.</summary>
    [Fact]
    public void ATranscribedStepWithNoSentenceIsRefused()
    {
        var problems = GuideCatalog.FromJson(FixtureJson("""
            { "id": "step", "order": 1, "objectiveType": "Custom", "authoring": "Transcribed",
              "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-08-07" }] }
            """)).Validate([]);

        Assert.Contains(problems, p => p.Contains("carries no sentence", StringComparison.Ordinal));
    }

    /// <summary>The sentence lives in WHAT and nowhere else — a title beside it is a second
    /// copy of one string, which is trap 4 inside one record and, at 486 rows, most of the
    /// file. <c>GuidePresentation.StepTitle</c> is what draws it.</summary>
    [Theory]
    [InlineData("title", "carries a title")]
    [InlineData("shortInstruction", "carries a short instruction")]
    public void ATranscribedStepThatAlsoCarriesADrawnNameIsRefused(string field, string says)
    {
        var problems = Fixture($""", "{field}": "Talk to Konia Swiftfoot" """).Validate([]);

        Assert.Contains(problems, p => p.Contains(says, StringComparison.Ordinal));
    }

    [Fact]
    public void ATranscribedStepWithNoSourceIsRefused()
    {
        var problems = GuideCatalog.FromJson(FixtureJson("""
            { "id": "step", "order": 1, "objectiveType": "Custom", "authoring": "Transcribed",
              "what": "Talk to Konia Swiftfoot in Western Karana." }
            """)).Validate([]);

        Assert.Contains(problems, p => p.Contains("cites no page", StringComparison.Ordinal));
    }

    [Fact]
    public void ATranscribedStepCarryingAStubNoteIsRefused()
    {
        var problems = Fixture(""", "stubNote": "we do not know where this is" """).Validate([]);

        Assert.Contains(problems, p => p.Contains("carries a stub note", StringComparison.Ordinal));
    }

    /// <summary>WHY is the one question this state may answer, so it is the one place the #480
    /// invention could come back — and the deny-list reaches it. Optional does not mean
    /// unpoliced.</summary>
    [Fact]
    public void AnInventedSentenceInTheOneFieldTranscribedMayFillIsStillRefused()
    {
        var problems = Fixture(
            """, "why": "One named on a spawn cycle, so the wait is the cycle." """).Validate([]);

        Assert.Contains(problems, p => p.Contains("spawn cycle", StringComparison.Ordinal));
    }

    // ---- 1b. …and the shipped 486 keep to it -----------------------------------------

    [Fact]
    public void EveryShippedEpicStepIsTranscribedAndAnswersNothingButTheSentence()
    {
        var steps = Epics.SelectMany(g => g.AllObjectives).ToList();

        Assert.Equal(486, steps.Count);
        foreach (var o in steps)
        {
            Assert.Equal(GuideAuthoring.Transcribed, o.Authoring);
            Assert.False(string.IsNullOrWhiteSpace(o.What), $"{o.Id}: no sentence");
            Assert.Equal("", o.Who);
            Assert.Equal("", o.Where);
            Assert.Equal("", o.When);
            Assert.Equal("", o.How);
            Assert.Equal("", o.Why);
            Assert.Equal("", o.Title);
            Assert.Equal("", o.ShortInstruction);
            Assert.Equal("", o.StubNote);
            Assert.Equal("", o.RewardKey);
            Assert.Empty(o.ItemNames);
            Assert.NotEmpty(o.Sources);
        }
    }

    /// <summary>
    /// <b>486 = 486.</b> Every epic checklist row has exactly one objective and every objective
    /// names a row — the must-list, stated positively (trap 34), because a guard that forbids
    /// the wrong thing cannot see a MISSING thing and a dropped row is a step the player never
    /// sees and never knows is gone.
    /// </summary>
    [Fact]
    public void EveryEpicChecklistRowHasExactlyOneObjectiveAndEveryObjectiveNamesARow()
    {
        var rows = EpicQuestDefaults.Items();
        var objectives = Epics.SelectMany(g => g.AllObjectives).ToList();

        Assert.Equal(486, rows.Count);
        Assert.Equal(rows.Count, objectives.Count);

        var rowIds = new HashSet<string>(rows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var objectiveIds = new HashSet<string>(
            objectives.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);

        Assert.Equal(rows.Count, rowIds.Count);
        Assert.Equal(objectives.Count, objectiveIds.Count);
        Assert.Empty(rowIds.Except(objectiveIds, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(objectiveIds.Except(rowIds, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>A guide per class, keyed the way the Epic tab keys it. Enumerated from the
    /// CHECKLIST rather than typed here, so a fifteenth class's epic landing in the catalog
    /// fails this rather than quietly falling outside the guided set.</summary>
    [Fact]
    public void EveryClassTheEpicChecklistKnowsHasAGuide()
    {
        var classes = EpicQuestDefaults.Items()
            .Select(i => i.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(14, classes.Count);
        foreach (var className in classes)
            Assert.NotNull(GuideChecklistProjection.EpicGuideFor(Shipped, className));

        Assert.Null(GuideChecklistProjection.EpicGuideFor(Shipped, "Beastlord"));
        Assert.Null(GuideChecklistProjection.EpicGuideFor(Shipped, ""));
    }

    /// <summary><b>No prerequisite is invented.</b> Reading order is the only sequence an epic
    /// guide claims — which is what keeps
    /// <c>OnlyATurnInCarriesPrerequisitesSoTheBlockedSentenceCanNameTheHandIn</c> true, and
    /// with it the card's right to say "the hand-in waits on…".</summary>
    [Fact]
    public void NoEpicObjectiveCarriesAPrerequisite()
    {
        var invented = Epics.SelectMany(g => g.AllObjectives)
            .Where(o => o.PrerequisiteObjectiveIds.Count > 0)
            .Select(o => o.Id)
            .ToList();

        Assert.Empty(invented);
    }

    /// <summary>
    /// The other side of the rule, and the reason the one above had to be narrowed rather than
    /// deleted: <b>an epic guide claims NO reward key, and that is what routes its steps</b>.
    ///
    /// <para>An epic's store is the <c>EpicQuestChecklistItem</c> row each objective was
    /// generated from (<c>GuideProgressHome.EpicItem</c>), which the loot auto-tick and the
    /// master "Epic complete" button already write. A reward key on one of these steps would
    /// send its tick to the Sky turn-in store instead — a Bard's epic marking a Plane of Sky
    /// reward turned in — so "claims none" is load-bearing rather than a shape observation.</para>
    /// </summary>
    [Fact]
    public void AnEpicGuideClaimsNoSkyRewardKeyBecauseItsStoreIsTheEpicRow()
    {
        var epics = GuideCatalog.Default.Guides
            .Where(g => g.GuideType == GuideType.EpicQuest).ToList();

        Assert.Equal(14, epics.Count);
        foreach (var guide in epics)
        {
            Assert.Equal("", GuideChecklistProjection.RewardKeyOf(guide));
            Assert.All(guide.AllObjectives, o => Assert.Equal("", o.RewardKey));
        }
    }

    /// <summary><c>ForClass</c> answers "every guide that applies to this class", ACROSS types —
    /// six Plane of Sky guides and one Epic 1.0 guide for a Warrior since Delivery 3. Asserted
    /// rather than assumed, because for as long as Sky was the only kind there was, this method
    /// and "the Sky tab's guides" were the same list, and five E2E assertions were quietly
    /// reading one for the other.</summary>
    [Fact]
    public void ForClassAnswersAcrossGuideTypes()
    {
        var warrior = Shipped.ForClass("Warrior");

        Assert.Equal(6, warrior.Count(g => g.GuideType == GuideType.PlaneOfSkyQuest));
        Assert.Single(warrior, g => g.GuideType == GuideType.EpicQuest);
        Assert.Equal(7, warrior.Count);
    }

    // ---- 2. The attachment seam ------------------------------------------------------

    /// <summary>
    /// The hook is real enough to be wrong in a test, and <b>empty in the shipped file</b>.
    ///
    /// <para>Founder, 2026-09-11: guides must be able to integrate later with gear-upgrade,
    /// recommended XP farms and gear farms — <i>and</i> "do not fake-ship a gear recommender".
    /// Those two together are exactly this: a typed reference a later system can hang an answer
    /// on, and nothing in the catalog pretending to be that answer. The day the recommender
    /// exists, this test changes with it.</para></summary>
    [Fact]
    public void NoShippedGuideCarriesAnAttachmentYet()
    {
        var placed = Shipped.Guides
            .SelectMany(g => g.Stages.SelectMany(s =>
                s.Attachments.Select(a => $"{g.Id}/{s.Id}: {a.Kind}")
                    .Concat(s.Objectives.SelectMany(o =>
                        o.Attachments.Select(a => $"{g.Id}/{o.Id}: {a.Kind}")))))
            .ToList();

        Assert.Empty(placed);
    }

    [Fact]
    public void AnAttachmentOfAKnownKindNamingSomethingIsShippable()
    {
        var catalog = Fixture();
        catalog.Guides[0].Stages[0].Objectives[0].Attachments =
            [new GuideAttachment { Kind = "GearUpgrade", Key = "Fiery Defender" }];

        Assert.Empty(catalog.Validate([]));
    }

    [Fact]
    public void AnAttachmentOfAnUnknownKindIsRefused()
    {
        var catalog = Fixture();
        catalog.Guides[0].Stages[0].Attachments =
            [new GuideAttachment { Kind = "BestInSlot", Key = "Fiery Defender" }];

        Assert.Contains(catalog.Validate([]),
            p => p.Contains("'BestInSlot' is not one of", StringComparison.Ordinal));
    }

    /// <summary>An attachment with no key is the beginning of a recommendation: the next thing
    /// anybody would do is put a sentence in it.</summary>
    [Fact]
    public void AnAttachmentThatNamesNothingIsRefused()
    {
        var catalog = Fixture();
        catalog.Guides[0].Stages[0].Objectives[0].Attachments =
            [new GuideAttachment { Kind = "XpFarm", Key = "  " }];

        Assert.Contains(catalog.Validate([]),
            p => p.Contains("names nothing", StringComparison.Ordinal));
    }

    // ---- 3. The fourth home: the objective's tick IS the row's box --------------------

    [Fact]
    public void AnObjectiveWhoseIdNamesAnEpicRowIsThatRowsBox()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");
        var objective = Epic("Paladin").AllObjectives.First();

        var home = GuideProgressRouter.HomeFor(objective, [], rows, out var sky, out var row);

        Assert.Equal(GuideProgressHome.EpicItem, home);
        Assert.Null(sky);
        Assert.Equal(objective.Id, row!.Id);
    }

    /// <summary>The negative half: the same objective with no epic rows in hand has no box to
    /// read, and falls to the guide ledger rather than to a guess.</summary>
    [Fact]
    public void TheSameObjectiveWithNoEpicRowsInHandFallsToTheGuideLedger()
    {
        var objective = Epic("Paladin").AllObjectives.First();

        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(objective));
    }

    /// <summary>One class's rows never resolve inside another class's guide — the same scoping
    /// the Sky item home has, and for the same reason: a row is one class's fact.</summary>
    [Fact]
    public void APaladinObjectiveDoesNotResolveAgainstTheDruidsRows()
    {
        var settings = SettingsWithEpicRows();
        var objective = Epic("Paladin").AllObjectives.First();

        var home = GuideProgressRouter.HomeFor(
            objective, [], RowsOf(settings, "Druid"), out _, out var row);

        Assert.Equal(GuideProgressHome.GuideLedger, home);
        Assert.Null(row);
    }

    [Fact]
    public void TickingAnEpicObjectiveWritesTheRowAndNothingElse()
    {
        var settings = SettingsWithEpicRows();
        var ledger = Store();
        var guide = Epic("Paladin");
        var rows = RowsOf(settings, "Paladin");
        var objective = guide.AllObjectives.First();
        var row = rows.First(r => r.Id == objective.Id);
        // A parked auto-tick: the player deciding IS its resolution, exactly as a click on the
        // classic Epic row treats it.
        row.AcquiredUnassigned = true;

        GuideProgressRouter.SetDone(settings, ledger, Dranak, guide.Id, objective, [], rows, true);

        Assert.True(row.Acquired);
        Assert.False(row.AcquiredUnassigned);
        // The guide ledger holds NO copy of it — one fact, one store (trap 4).
        Assert.DoesNotContain(objective.Id,
            ledger.GuideProgressFor(Dranak, guide.Id).DoneObjectiveIds);
        Assert.True(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, guide.Id, objective, [], rows));
    }

    /// <summary>
    /// <b>The loot auto-tick lights the guide row.</b> One physical drop, one box, and the
    /// guide reads it the same tick — which is the whole argument for the fourth home rather
    /// than a tick of the guide's own.
    /// </summary>
    [Fact]
    public void AnEpicLootLineTicksTheGuideStepThroughTheRowItShares()
    {
        var settings = SettingsWithEpicRows();
        var ledger = Store();
        var guide = Epic("Bard");
        var rows = RowsOf(settings, "Bard");

        // A row the auto-tick can actually prove — one whose text names a catalog item.
        var row = rows.First(r => r.ItemNames.Count > 0);
        var objective = guide.AllObjectives.First(o => o.Id == row.Id);
        Assert.False(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, guide.Id, objective, [], rows));

        Assert.True(EpicLootAutoCheck.Apply(
            settings.EpicQuestChecklist, row.ItemNames[0], 1, ["Bard"], "Bard"));

        Assert.True(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, guide.Id, objective, [], rows));
    }

    // ---- 4. The tab's cutover --------------------------------------------------------

    /// <summary>A guided class's several SECTION groups become ONE guided group whose rows are
    /// the whole walkthrough, under the section names as stage headings.</summary>
    [Fact]
    public void AGuidedClassesSectionGroupsBecomeOneGuidedGroup()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");
        var classic = QuestChecklistLayout.Epic(rows);

        // The Paladin's page has two sections, so the classic tab drew two groups.
        Assert.Equal(2, classic.Count);

        var projected = GuideChecklistProjection.ApplyEpic(
            classic, rows, Shipped, settings, Store(), Dranak);

        var group = Assert.Single(projected);
        Assert.Equal("epic-paladin", group.GuideId);
        Assert.Equal(GuidePresentation.EpicTitle, group.Title);
        Assert.Equal(14, group.Rows.Count);
        Assert.Equal(rows.Count, group.Rows.Count);
        // The sections survive as the stage headings above the rows.
        Assert.Equal(["Checklist", "Resources"],
            group.Rows.Select(r => r.IslandHeading).Distinct());
        // Epic completion is per class and lives in its own store; a group-level turn-in
        // control here would be a second writer of it.
        Assert.Null(group.CompletionKey);
    }

    /// <summary>Founder lock 5, on the Epic tab: a class with no guide is handed back the SAME
    /// OBJECT. Not an equal one — identity, because "untouched" is the claim.</summary>
    [Fact]
    public void AClassWithNoEpicGuideComesBackTheSameObject()
    {
        var settings = new AppSettings();
        settings.EpicQuestChecklist.Add(new EpicQuestChecklistItem
        {
            Id = "made-up-1", ClassName = "Beastlord", Section = "Checklist",
            QuestItem = "Something", Order = 0,
        });
        var rows = RowsOf(settings, "Beastlord");
        var classic = QuestChecklistLayout.Epic(rows);

        var projected = GuideChecklistProjection.ApplyEpic(
            classic, rows, Shipped, settings, Store(), Dranak);

        Assert.Same(classic[0], Assert.Single(projected));
    }

    /// <summary>…and so is a class that HAS a guide but whose rows are not the ones the guide
    /// was built from. Without this the projection would conjure a 31-step Bard walkthrough
    /// over somebody else's two-row list — the shape a fixture, a migration or a future catalog
    /// refresh produces, and the Sky side is protected from it by the reward key.</summary>
    [Fact]
    public void AGuidedClassWhoseRowsTheGuideDoesNotKnowComesBackUntouched()
    {
        var settings = new AppSettings();
        settings.EpicQuestChecklist.Add(new EpicQuestChecklistItem
        {
            Id = "e1", ClassName = "Bard", Section = "Pieces", QuestItem = "x", Order = 0,
        });
        var rows = RowsOf(settings, "Bard");
        var classic = QuestChecklistLayout.Epic(rows);

        var projected = GuideChecklistProjection.ApplyEpic(
            classic, rows, Shipped, settings, Store(), Dranak);

        Assert.Same(classic[0], Assert.Single(projected));
    }

    /// <summary>The long chain Bevel is being shown beside the short one (Fable §4): 66 rows in
    /// one section for the Druid, 14 in two for the Paladin. Pinned because the frames
    /// <c>shell-quests-epic-guide</c> and <c>shell-quests-epic-guide-long</c> are predicted on
    /// exactly these numbers, and a prediction nothing compiles goes stale silently.</summary>
    [Theory]
    [InlineData("Paladin", 14, 2)]
    [InlineData("Druid", 66, 1)]
    public void TheFramesBevelIsShownAreTheseSizes(string className, int rows, int stages)
    {
        var guide = Epic(className);

        Assert.Equal(rows, guide.AllObjectives.Count());
        Assert.Equal(stages, guide.Stages.Count);
    }

    /// <summary>
    /// The classic-era lens reaches a GUIDED class, through the row's own flag and not through
    /// a second copy of the predicate.
    ///
    /// <para>And it reaches the card and the counts with it: a guide drawing 14 rows while its
    /// caption counts 31 is the surface contradicting itself, which is what
    /// <c>GuideProgressRouter.Drawn</c> exists to stop.</para></summary>
    [Fact]
    public void TheClassicEraLensDropsAGuidedStepWhoseRowItDropped()
    {
        var settings = SettingsWithEpicRows();
        var all = RowsOf(settings, "Bard");
        var classic = all.Where(r => r.AvailableInClassic).ToList();

        // The Bard's epic has rows on both sides of the lens, which is what makes this a test.
        Assert.NotEmpty(classic);
        Assert.True(classic.Count < all.Count);

        var projected = GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(classic), classic, Shipped, settings, Store(), Dranak);

        var group = Assert.Single(projected);
        Assert.Equal(classic.Count, group.Rows.Count);
        Assert.Equal(classic.Count, group.Total);
        // …and the card's next step is one of the rows on screen.
        var next = group.GuideCard!.RowId;
        Assert.Contains(group.Rows, r => r.Id == next);
    }

    /// <summary><c>EpicCompleteToggle</c> stays the guide-complete store: the master button
    /// ticks every row, so every objective reads done and the card says so. Nothing new was
    /// built for "this guide is finished" — the button that already meant it still does.</summary>
    [Fact]
    public void MarkingAClassEpicCompleteFinishesItsGuide()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");

        EpicCompleteToggle.MarkComplete(settings, "Paladin",
            EpicCompleteToggle.ItemsFor(settings.EpicQuestChecklist, "Paladin", classicOnly: false));

        var projected = GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak);

        var group = Assert.Single(projected);
        Assert.Equal(group.Total, group.Done);
        Assert.Equal("", group.GuideCard!.RowId);
        Assert.Equal(GuidePresentation.AllDone, group.GuideCard.Instruction);
    }

    /// <summary>The fold is remembered under the guide's id on Epic and the reward key on Sky —
    /// ONE producer, because the control writes this list and the projection reads it. Spelled
    /// twice they drifted the first time a group had no completion key, which is every group on
    /// this tab.</summary>
    [Fact]
    public void AGuidedEpicGroupFoldsUnderItsGuideIdAndTheProjectionReadsTheSameKey()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");

        var folded = Assert.Single(GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak));
        Assert.True(folded.Collapsed);
        Assert.Equal("epic-paladin", GuideChecklistProjection.FoldKey(folded));

        settings.GuideExpanded.Add(GuideChecklistProjection.FoldKey(folded));

        var open = Assert.Single(GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak));
        Assert.False(open.Collapsed);
    }

    /// <summary>
    /// The heading's link, and why the group had to start carrying a page name.
    ///
    /// <para>The heading has always opened <c>EqlWiki.PageUrl(Title)</c>, which is right for a
    /// Sky reward — the title IS an item page. A guided epic group's title is "Epic 1.0", so
    /// without <c>WikiPage</c> the one link on the heading would 404 on every class.</para></summary>
    [Fact]
    public void AGuidedEpicGroupsHeadingOpensTheClassEpicPage()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");

        var group = Assert.Single(GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak));

        Assert.Equal("Paladin Epic Quest", group.HeadingPage);
        // …and a group that names no page still answers with its title, as it always did.
        Assert.Equal("Fiery Defender",
            new QuestChecklistGroup("Paladin", "Fiery Defender", []).HeadingPage);
    }

    /// <summary>
    /// An epic's REWARD is not one item, and the surface must not pick one.
    ///
    /// <para>eqlwiki's own <c>== Rewards ==</c> lists four items for the Shadow Knight and six
    /// for the Necromancer and calls none of them "the epic". Choosing would be EQBuddy
    /// departing from the wiki on game data (David, 2026-08-14) — on the one part of the game
    /// David cannot check for us.</para></summary>
    [Fact]
    public void AGuidedEpicGroupListsEveryRewardAndPicksNone()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Shadow Knight");

        var group = Assert.Single(GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak));

        Assert.StartsWith("Rewards ", group.RewardSummary, StringComparison.Ordinal);
        Assert.Contains("Innoruuk's Curse", group.RewardSummary, StringComparison.Ordinal);
        Assert.Contains("Corrupted Ghoulbane", group.RewardSummary, StringComparison.Ordinal);
        // No single item window, because there is no single item.
        Assert.Equal("", group.RewardCard);
        Assert.Equal("Works toward your Shadow Knight epic.", group.GuideCard!.Why);
    }

    /// <summary>The row IS the sentence, so the hover is not a second copy of it — on the phone
    /// that would be the same line printed twice, one under the other (trap 35's neighbour).
    /// The share-back door still opens, because the sentence is the page's and the fix is the
    /// page.</summary>
    [Fact]
    public void ATranscribedRowDrawsThePagesSentenceOnceAndStillOffersTheShareBackDoor()
    {
        var settings = SettingsWithEpicRows();
        var rows = RowsOf(settings, "Paladin");
        var guide = Epic("Paladin");

        var group = Assert.Single(GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, Shipped, settings, Store(), Dranak));

        var first = group.Rows[0];
        var objective = guide.AllObjectives.First();
        Assert.Equal(objective.What, first.Title);
        Assert.Equal("", first.GuideFacts);
        Assert.Equal("", first.StubNote);
        Assert.Equal(guide.Id, first.GuideRowKey);
        Assert.Contains("Paladin_Epic_Quest",
            GuidePresentation.ImproveUrl(guide, objective), StringComparison.Ordinal);
    }

    // ---- Fixture ---------------------------------------------------------------------

    /// <summary>A one-step transcribed guide, valid in every respect the test under it is not
    /// about — so a failing assertion names the rule being exercised and nothing else.</summary>
    private static GuideCatalog Fixture(string extraFields = "") =>
        GuideCatalog.FromJson(FixtureJson($$"""
            { "id": "step", "order": 1, "objectiveType": "Custom", "authoring": "Transcribed",
              "what": "Talk to Konia Swiftfoot in Western Karana (guard tower #4)."{{extraFields}},
              "sources": [{ "url": "https://eqlwiki.com/Bard_Epic_Quest",
                            "title": "Bard Epic Quest", "retrievedAt": "2026-08-07" }] }
            """));

    private static string FixtureJson(string objectivesJson) => $$"""
        {
          "guides": [
            {
              "id": "epic-fixture",
              "name": "Fixture Epic 1.0",
              "guideType": "EpicQuest",
              "zoneNames": ["Dreadlands"],
              "applicableClasses": ["Bard"],
              "sources": [{ "url": "https://eqlwiki.com/Bard_Epic_Quest",
                            "title": "Bard Epic Quest", "retrievedAt": "2026-08-07" }],
              "stages": [
                { "id": "s1", "name": "The stage", "order": 1, "objectives": [ {{objectivesJson}} ] }
              ]
            }
          ]
        }
        """;
}
