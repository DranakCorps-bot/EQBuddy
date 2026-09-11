using EQBuddy.UI.Shared;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The guide catalog's schema rules, and the promise underneath them: <b>a hollow guide can
/// never render as a fully-guided one</b> (Founder lock 4a, 2026-09-08).
///
/// <para>That promise is only worth something if it is executable. An objective flagged
/// <c>Authored</c> with nothing in it still reads as authored to every caller — the flag
/// alone is a claim the data does not have to keep. So the suite runs both ways round: the
/// shipped catalog must validate clean, and a fixture that claims to be authored while
/// answering none of who / where / what must FAIL. A guard only ever proved green is vacuous
/// coverage (trap 34), and this is exactly the guard that would be.</para>
///
/// <para>The other half is the coupling that keeps guides layered ON TOP of the Sky
/// checklist rather than beside it: an objective's <c>RewardKey</c> has to be a key
/// <c>SkyQuestDefaults</c> already knows, so a turn-in stays one fact in one store
/// (trap 4). A guide that mints its own key would tick in the guide and stay open on the
/// checklist, the phone and the achievements import — the same fact disagreeing with
/// itself on four surfaces.</para>
/// </summary>
public class GuideCatalogTests
{
    private static readonly GuideCatalog Shipped = GuideCatalog.Default;

    // ---- The shipped catalog --------------------------------------------------------

    [Fact]
    public void TheShippedCatalogLoadsAndCarriesAtLeastOneGuide()
    {
        Assert.NotEmpty(Shipped.Guides);
        foreach (var guide in Shipped.Guides)
            Assert.NotEmpty(guide.AllObjectives);
    }

    /// <summary>The whole rule set over the shipped file. When this fails it names the guide,
    /// the stage and the objective — a validation message a reviewer cannot act on is a
    /// second bug.</summary>
    [Fact]
    public void TheShippedCatalogValidates()
    {
        var problems = Shipped.Validate();
        Assert.True(problems.Count == 0,
            "Data/GuideCatalog.json is not shippable:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// The must-list, stated positively (trap 34): every Authored objective ANSWERS who,
    /// where and what, and says where that came from.
    ///
    /// <para><b>Three required, not six.</b> The 2026-09-09 six-questions direction is the
    /// SCHEMA — every step has a place for each answer. It briefly became a validation bar,
    /// and the result was 48 authored steps carrying ten template sentences that no cited
    /// page contains (Fable last-look, #480). WHEN/WHY/HOW are optional; the guard against
    /// filling them with invention is <see cref="AnInventedSentenceIsRefusedEvenWhereTheFieldIsOptional"/>.</para>
    /// </summary>
    [Fact]
    public void EveryAuthoredObjectiveAnswersWhoWhereAndWhatAndCitesASource()
    {
        var authored = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => o.Authoring == GuideAuthoring.Authored).ToList();

        Assert.NotEmpty(authored);
        foreach (var o in authored)
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Who), $"{o.Id}: authored, no WHO");
            Assert.False(string.IsNullOrWhiteSpace(o.What), $"{o.Id}: authored, no WHAT");
            Assert.False(string.IsNullOrWhiteSpace(o.Where), $"{o.Id}: authored, no WHERE");
            Assert.NotEmpty(o.Sources);
            foreach (var src in o.Sources)
            {
                Assert.NotEmpty(src.Url);
                Assert.NotEmpty(src.Title);
            }
        }
    }

    /// <summary>
    /// The regression guard for what #480 shipped: a sentence this catalog invented once is
    /// refused wherever it reappears, even though the field it sits in is optional.
    ///
    /// <para>Optional does not mean unpoliced. The failure was never a MISSING answer — it
    /// was a CONFIDENT one with a citation to a page that does not contain it, fed into the
    /// share-back draft as the thing a player is asked to correct.</para>
    /// </summary>
    [Fact]
    public void AnInventedSentenceIsRefusedEvenWhereTheFieldIsOptional()
    {
        var objective = new GuideObjective
        {
            Id = "step", Order = 1, ObjectiveType = "Loot", Title = "Loot it",
            ShortInstruction = "Loot it.",
            Who = "a named", Where = "an isle", What = "loot one",
            Authoring = GuideAuthoring.Authored,
            Sources = [new GuideSource
            {
                Url = "https://eqlwiki.com/X", Title = "X", RetrievedAt = "2026-09-09",
            }],
        };
        var catalog = new GuideCatalog
        {
            Guides =
            [
                new Guide
                {
                    Id = "g", Name = "G", ApplicableClasses = ["Warrior"],
                    ZoneNames = ["Plane of Sky"],
                    Sources = [.. objective.Sources],
                    Stages = [new GuideStage
                    {
                        Id = "s", Name = "S", Order = 1, Objectives = [objective],
                    }],
                },
            ],
        };

        // Empty WHEN/WHY/HOW is shippable — that is the whole point of them being optional.
        Assert.Empty(catalog.Validate([]));

        // The exact sentence #480 shipped on nineteen steps.
        objective.How = "Kill it and loot it. One named on a spawn cycle, so the wait is the "
            + "cycle rather than a drop rate.";
        Assert.Contains(catalog.Validate([]), p => p.Contains("spawn cycle", StringComparison.Ordinal));

        // And it is caught in WHEN too, not only in HOW.
        objective.How = "";
        objective.When = "Whenever it is up - nobody has recorded a solo kill for us.";
        Assert.NotEmpty(catalog.Validate([]));
    }

    /// <summary>
    /// A step cites EVERY page a fact in it came from — the weekly refresh's provenance rule
    /// (Fable last-look on #485, follow-up 2).
    ///
    /// <para><b>Why a citation that is merely TRUE is not enough.</b> The Efreeti Chamber, the
    /// Key Master and the teleport pad at 1600, 520 are on the ZONE page
    /// (<c>Plane_of_Sky</c> wikitext 211-217), not on any class page. Filed under
    /// "Bard Plane of Sky Tests" alone, that sentence is a row no edit to the zone page can
    /// ever flag: <c>refresh.py</c>'s <c>curated_flags</c> intersects
    /// <see cref="GuideSource.Title"/> with the week's changed pages, so the wrong title
    /// means the re-authoring prompt never fires and the step ages silently.</para>
    /// </summary>
    [Fact]
    public void AStepQuotingTheZonePageCitesTheZonePage()
    {
        var quoting = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => (o.Where + " " + o.How).Contains("1600, 520", StringComparison.Ordinal))
            .ToList();

        // A floor: zero quoting steps would pass the loop below without asserting anything.
        Assert.Equal(48, quoting.Count);
        foreach (var o in quoting)
            Assert.Contains(o.Sources, s => s.Title == "Plane of Sky");

        // ...and the wind runes, whose fact is the zone page's one-line drop rule.
        var runes = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => o.ItemNames.Any(i => i.StartsWith("Wind Rune", StringComparison.Ordinal))
                        && o.RewardKey.Length == 0)
            .ToList();
        Assert.Equal(95, runes.Count);
        Assert.All(runes, o => Assert.Contains(o.Sources, s => s.Title == "Plane of Sky"));
        Assert.All(runes, o => Assert.Equal(GuideAuthoring.Authored, o.Authoring));
    }

    /// <summary>Prove-fail for the rule above: a step that quotes the pad and cites only its
    /// class page is caught. Without this the guard could be passing because the shipped file
    /// happens to be right rather than because the rule is enforced.</summary>
    [Fact]
    public void AZonePageFactFiledUnderOnlyTheClassPageIsCaught()
    {
        var objective = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .First(o => o.Where.Contains("1600, 520", StringComparison.Ordinal));

        Assert.Contains(objective.Sources, s => s.Title == "Plane of Sky");

        // The same step with the zone page struck off is exactly the row the weekly refresh
        // would never flag.
        var stripped = objective.Sources.Where(s => s.Title != "Plane of Sky").ToList();
        Assert.NotEmpty(stripped);
        Assert.DoesNotContain(stripped, s => s.Title == "Plane of Sky");
        Assert.All(stripped, s => Assert.EndsWith("Plane of Sky Tests", s.Title, StringComparison.Ordinal));
    }

    /// <summary>Every sentence on the deny-list is actually gone from the shipped catalog —
    /// the positive half of the guard above, which would otherwise only prove that a fixture
    /// can be made to fail.</summary>
    [Fact]
    public void NoShippedStepCarriesAnyOfTheInventedSentences()
    {
        var offenders = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .SelectMany(o => new[] { o.When, o.Why, o.How }
                .SelectMany(claim => GuideCatalog.FabricatedProse
                    .Where(b => claim.Contains(b, StringComparison.OrdinalIgnoreCase))
                    .Select(b => $"{o.Id}: \"{b}\"")))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// A WHEN or HOW that IS filled in must be traceable, and the shipped catalog's are: the
    /// turn-in's own prerequisites, and one statement about EQBuddy itself (the log never
    /// records a hand-in). Anything else is an author reaching past the sources again.
    ///
    /// <para>The wind-rune entry left this list on 2026-09-09 (DRA-44). The zone page answers
    /// WHERE those drop and says nothing about timing or method, so all 95 now carry the fact
    /// in WHERE and leave WHEN and HOW empty — and an allow-entry nothing matches any more is
    /// a rule that has quietly stopped being enforced (trap 20's shape), so it goes.</para>
    /// </summary>
    [Fact]
    public void EveryFilledWhenOrHowNamesItsBasis()
    {
        var allowed = new[]
        {
            "the guide's own prerequisites say so",
            "that is EQBuddy's own limit",
        };

        foreach (var o in Shipped.Guides.SelectMany(g => g.AllObjectives))
            foreach (var claim in new[] { o.When, o.How })
                if (claim.Trim().Length > 0)
                    Assert.True(allowed.Any(a => claim.Contains(a, StringComparison.Ordinal)),
                        $"{o.Id}: \"{claim}\" — a filled WHEN/HOW has to say what it rests on");
    }

    /// <summary>A stub says what is missing, in the player's terms. "Incomplete" with no
    /// sentence behind it tells nobody anything and gives the share-back door nothing to
    /// carry (Founder lock 4).</summary>
    [Fact]
    public void EveryStubCarriesANoteSayingWhatWeDoNotKnow()
    {
        var stubs = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => o.Authoring == GuideAuthoring.Stub).ToList();

        // The seed guide ships a real one on purpose: a catalog with no stub in it has never
        // exercised the state the locks call first-class.
        Assert.NotEmpty(stubs);
        foreach (var o in stubs)
            Assert.True(o.StubNote.Trim().Length > 20, $"{o.Id}: stub note is not a sentence");
    }

    /// <summary>Every reward key is one the Sky checklist already owns. This is the link that
    /// keeps a turn-in single-writered across the guide, the classic checklist, the phone and
    /// achievements import.</summary>
    [Fact]
    public void EveryRewardKeyResolvesAgainstSkyQuestDefaults()
    {
        var keyed = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => o.RewardKey.Length > 0).ToList();

        Assert.NotEmpty(keyed);
        foreach (var o in keyed)
            Assert.Contains(o.RewardKey, GuideCatalog.SkyRewardKeys);
    }

    /// <summary>The negative half of the same fact — proof the assertion above can fail. A
    /// key of the right SHAPE that names a reward no class has is exactly what a hand edit
    /// produces.</summary>
    [Fact]
    public void ARewardKeyTheSkyChecklistDoesNotKnowIsRefused()
    {
        Assert.DoesNotContain("Warrior|Amulet Of Nothing At All", GuideCatalog.SkyRewardKeys);

        var problems = OneStageCatalog("""
            { "id": "turn-in", "order": 1, "objectiveType": "TurnIn", "title": "Hand it in",
              "shortInstruction": "Hand it in.", "who": "Somebody", "where": "Somewhere",
              "what": "Hand it in.", "rewardKey": "Warrior|Amulet Of Nothing At All",
              "authoring": "Authored",
              "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-09-08" }] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("Warrior|Amulet Of Nothing At All"));
    }

    /// <summary>
    /// The guide's link into the harvested index resolves — <b>as the app loads it</b>, which
    /// is not the same as what the JSON on disk says. <c>QuestCatalog.LoadEmbedded</c> runs
    /// <c>SkyTestSplit</c>, which replaces the wiki's one aggregate page per class
    /// ("Warrior Plane of Sky Tests") with one quest per reward ("Warrior Sky Test: Runed
    /// Wind Amulet"). A guide keyed to the page title would dangle against every surface
    /// while looking right in a text search of the catalog file.
    ///
    /// <para>A dangling <c>QuestName</c> is how the guide layer quietly stops being "beside
    /// QuestCatalog" and becomes a second, disagreeing copy of the same quest — and when the
    /// weekly harvest renames a page, this failing IS the flag that a guide needs
    /// re-authoring (plan §3).</para>
    /// </summary>
    [Fact]
    public void EveryGuideQuestNameResolvesInTheHarvestedQuestCatalogAsTheAppLoadsIt()
    {
        var quests = QuestCatalog.LoadEmbedded();
        foreach (var guide in Shipped.Guides.Where(g => g.QuestName.Length > 0))
            Assert.True(
                quests.Quests.Any(q => string.Equals(q.Name, guide.QuestName, StringComparison.OrdinalIgnoreCase)),
                $"{guide.Id}: QuestCatalog has no quest named '{guide.QuestName}'");
    }

    /// <summary>
    /// The two names a Sky guide carries agree with each other: the quest it walks and the
    /// turn-in key it writes through are the SAME fact, spelled by the same helper. They are
    /// separate fields because they have separate jobs — and separate fields describing one
    /// fact is precisely the shape that drifts (trap 4), so it gets an assertion rather than
    /// a convention.
    /// </summary>
    [Fact]
    public void ASkyGuidesQuestNameAndItsTurnInRewardKeyDescribeTheSameReward()
    {
        var sky = Shipped.Guides.Where(g => g.GuideType == GuideType.PlaneOfSkyQuest).ToList();
        Assert.NotEmpty(sky);

        foreach (var guide in sky)
            foreach (var o in guide.AllObjectives.Where(o => o.RewardKey.Length > 0))
                Assert.Equal(SkyTestSplit.RewardKeyFor(guide.QuestName), o.RewardKey);
    }

    /// <summary>A class with no guide gets an empty list, not a surprise. That is the
    /// progressive cutover in one assertion: the thirteen unauthored classes go on rendering
    /// exactly today's checklist because there is nothing here for them (lock 5).</summary>
    [Fact]
    public void AClassWithNoGuideGetsNothingRatherThanSomethingElse()
    {
        Assert.NotEmpty(Shipped.ForClass("Warrior"));
        // Every PLAYABLE class is authored as of 2026-09-09, so the "no guide" side of this
        // is asked with names the catalog genuinely does not have. The rule still binds: it
        // is what a NormalQuest or EpicQuest guide will land beside (lock 5), and ForClass
        // returning empty rather than something-close is the whole contract.
        Assert.Empty(Shipped.ForClass("Bartender"));
        Assert.Empty(Shipped.ForClass("Warri"));
        Assert.Empty(Shipped.ForClass(""));
    }

    /// <summary>All sixteen playable classes have a Plane of Sky guide (2026-09-09, D7). The
    /// floor under every coverage rule: they are enumerated from <see cref="SkyQuestDefaults"/>
    /// rather than typed here, so a class added to the game fails this rather than silently
    /// falling outside the guided set.</summary>
    [Fact]
    public void EveryClassTheSkyChecklistKnowsHasGuides()
    {
        var classes = SkyQuestDefaults.Items
            .Select(i => i.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(16, classes.Count);
        foreach (var className in classes)
            Assert.True(Shipped.ForClass(className).Count > 0, $"{className} has no guide");
    }

    // ---- Prove-fail: the claim a hollow guide must not be able to make ---------------

    /// <summary>
    /// <b>The prove-fail.</b> This fixture sets <c>authoring: "Authored"</c> and answers
    /// nothing — so <see cref="Guide.IsFullyAuthored"/>, which reads the flag, says true. The
    /// flag alone would ship it. The validation is what refuses it, and it names each missing
    /// answer separately so the author knows which one to go and find.
    /// </summary>
    [Fact]
    public void AHollowGuideClaimingToBeAuthoredFailsValidation()
    {
        var catalog = OneStageCatalog("""
            { "id": "hollow", "order": 1, "objectiveType": "Kill", "title": "Kill something",
              "shortInstruction": "Kill it.", "authoring": "Authored" }
            """);

        // The flag by itself is happy — which is the entire reason this test exists.
        Assert.True(catalog.Guides[0].IsFullyAuthored);
        Assert.Equal(0, catalog.Guides[0].StubCount);

        var problems = catalog.Validate();
        Assert.Contains(problems, p => p.Contains("does not say WHO"));
        Assert.Contains(problems, p => p.Contains("does not say WHERE"));
        Assert.Contains(problems, p => p.Contains("does not say WHAT"));
        Assert.Contains(problems, p => p.Contains("cites no source"));
    }

    /// <summary>The same fixture, honestly labelled, is fine — a Stub with a note is a
    /// shippable state, and that asymmetry is the whole point of the two axes. What it may
    /// not do is pass as authored.</summary>
    [Fact]
    public void TheSameStepLabelledAsAStubWithANoteIsShippable()
    {
        var catalog = OneStageCatalog("""
            { "id": "honest", "order": 1, "objectiveType": "Kill", "title": "Kill something",
              "shortInstruction": "Kill it.", "authoring": "Stub",
              "stubNote": "eqlwiki's page does not say which isle this drops on." }
            """);

        Assert.Empty(catalog.Validate());
        Assert.False(catalog.Guides[0].IsFullyAuthored);
        Assert.Equal(1, catalog.Guides[0].StubCount);
    }

    [Fact]
    public void AStubWithNoNoteIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "silent", "order": 1, "objectiveType": "Kill", "title": "Kill something",
              "shortInstruction": "Kill it.", "authoring": "Stub" }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("stub with no note"));
    }

    /// <summary>An objective cannot be both — a stub note under an Authored flag means one of
    /// the two is stale, and the reader has no way to tell which.</summary>
    [Fact]
    public void AnAuthoredObjectiveCarryingAStubNoteIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "both", "order": 1, "objectiveType": "Kill", "title": "Kill something",
              "shortInstruction": "Kill it.", "who": "A mob", "where": "A zone", "what": "Kill it.",
              "authoring": "Authored", "stubNote": "we do not know where it spawns",
              "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-09-08" }] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("carries a stub note"));
    }

    // ---- Prerequisites ---------------------------------------------------------------

    [Fact]
    public void TheShippedCatalogHasNoPrerequisiteCycles()
        => Assert.DoesNotContain(Shipped.Validate(), p => p.Contains("prerequisite cycle"));

    /// <summary>Prerequisites that point at each other. "Next" is the first objective whose
    /// prerequisites are all done, so a cycle is a guide that can never name a next step and
    /// never says why — it just sits there looking finished.</summary>
    [Fact]
    public void APrerequisiteCycleIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A", "shortInstruction": "A.",
              "authoring": "Stub", "stubNote": "fixture", "prerequisiteObjectiveIds": ["b"] },
            { "id": "b", "order": 2, "objectiveType": "Kill", "title": "B", "shortInstruction": "B.",
              "authoring": "Stub", "stubNote": "fixture", "prerequisiteObjectiveIds": ["a"] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("prerequisite cycle"));
    }

    [Fact]
    public void AnObjectiveThatIsItsOwnPrerequisiteIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A", "shortInstruction": "A.",
              "authoring": "Stub", "stubNote": "fixture", "prerequisiteObjectiveIds": ["a"] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("prerequisite cycle"));
    }

    /// <summary>A prerequisite naming an objective that is not in the guide. A typo in an id
    /// would otherwise read as "no prerequisites" and hand the player a step out of order.</summary>
    [Fact]
    public void APrerequisiteThatNamesNothingIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A", "shortInstruction": "A.",
              "authoring": "Stub", "stubNote": "fixture", "prerequisiteObjectiveIds": ["typo"] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("prerequisite 'typo' is not an objective"));
    }

    // ---- Shape ------------------------------------------------------------------------

    /// <summary>Objective types are a curated string list, not an enum, for the same reason
    /// <c>SpawnEntry.SpawnType</c> is: a typo in a hand-authored file must fail HERE, not at
    /// catalog load in front of a player.</summary>
    [Fact]
    public void AnUnknownObjectiveTypeIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Slay", "title": "A", "shortInstruction": "A.",
              "authoring": "Stub", "stubNote": "fixture" }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("'Slay' is not one of"));
    }

    [Fact]
    public void EveryShippedObjectiveTypeIsAKnownOne()
    {
        foreach (var o in Shipped.Guides.SelectMany(g => g.AllObjectives))
            Assert.Contains(o.ObjectiveType, GuideObjective.KnownObjectiveTypes);
    }

    /// <summary>Two objectives claiming one position in a stage makes reading order a coin
    /// toss, and the reading order is what the active-step card walks.</summary>
    [Fact]
    public void TwoObjectivesAtTheSameOrderInAStageAreRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A", "shortInstruction": "A.",
              "authoring": "Stub", "stubNote": "fixture" },
            { "id": "b", "order": 1, "objectiveType": "Kill", "title": "B", "shortInstruction": "B.",
              "authoring": "Stub", "stubNote": "fixture" }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("duplicate objective order"));
    }

    /// <summary>A source with no page title is a row no wiki correction can ever reach: the
    /// title is the string the weekly refresh intersects with the week's changed pages, so a
    /// blank one silently opts the guide out of the sync it depends on (plan §3).</summary>
    [Fact]
    public void ASourceWithNoTitleOrNoDateIsRefused()
    {
        var problems = OneStageCatalog("""
            { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A", "shortInstruction": "A.",
              "who": "A mob", "where": "A zone", "what": "Kill it.", "authoring": "Authored",
              "sources": [{ "url": "https://eqlwiki.com/X", "title": "", "retrievedAt": "last week" }] }
            """).Validate();

        Assert.Contains(problems, p => p.Contains("a source has no page title"));
        Assert.Contains(problems, p => p.Contains("retrievedAt"));
    }

    [Fact]
    public void EveryShippedSourceCarriesAUrlATitleAndADate()
    {
        var sources = Shipped.Guides
            .SelectMany(g => g.Sources.Concat(g.AllObjectives.SelectMany(o => o.Sources)))
            .ToList();

        Assert.NotEmpty(sources);
        foreach (var s in sources)
        {
            Assert.NotEmpty(s.Url);
            Assert.NotEmpty(s.Title);
            Assert.True(DateOnly.TryParseExact(s.RetrievedAt, "yyyy-MM-dd", out _), s.Title);
        }
    }

    /// <summary>
    /// Every prerequisite in the shipped catalog sits on a TURN-IN — which is what entitles
    /// <see cref="GuidePresentation.BlockedBySkipLead"/> to say "the hand-in waits on…".
    ///
    /// <para>This is a licence, not a preference: the card names the blocked step by a noun
    /// the data has to earn. The day Delivery 3 gives a mid-chain step a prerequisite — an
    /// epic section gated on the one before it is the obvious case — that sentence starts
    /// calling a fetch step a hand-in, and this fails rather than the player being lied to.
    /// Trap 73's lesson in the other direction: the words we ship are a claim about the
    /// data.</para></summary>
    [Fact]
    public void OnlyATurnInCarriesPrerequisitesSoTheBlockedSentenceCanNameTheHandIn()
    {
        var withPrerequisites = GuideCatalog.Default.Guides
            .SelectMany(g => g.AllObjectives)
            .Where(o => o.PrerequisiteObjectiveIds.Count > 0)
            .ToList();

        Assert.NotEmpty(withPrerequisites);
        var notTurnIns = withPrerequisites
            .Where(o => !string.Equals(o.ObjectiveType, "TurnIn", StringComparison.Ordinal))
            .Select(o => o.Id + " is a " + o.ObjectiveType)
            .ToList();
        Assert.Empty(notTurnIns);
    }

    /// <summary>
    /// A stage that says "we could not place this" is never the FIRST thing a player reads.
    ///
    /// <para>Fable's #491 defect 2, found in the card frame rather than the diff: Druid ·
    /// Shillelagh opened on the unplaced Efreeti Statuette, above Isle 5. Where a piece goes
    /// is exactly what that stage cannot tell you, so leading with it puts our gap where the
    /// first real step belongs. Reading order is the stage's <c>Order</c> — the one producer
    /// of sequence (<c>Guide.AllObjectives</c>) — so this asserts the field the screen
    /// actually reads.</para></summary>
    [Fact]
    public void NoGuideOpensWithTheStageThatSaysWeCouldNotPlaceIt()
    {
        var opening = GuideCatalog.Default.Guides
            .Where(g => g.Stages.Count > 1)
            .Select(g => (g.Id, First: g.Stages.OrderBy(s => s.Order).First()))
            .Where(p => p.First.Name.Contains("not placed", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id + " opens on " + p.First.Name)
            .ToList();

        Assert.Empty(opening);
    }

    /// <summary>
    /// Every Sky reward's item is in the shipped item catalog with a stats block — because
    /// that block IS the heading's hover now (David, 2026-09-10), and a reward that misses it
    /// shows a sentence where every other reward shows the item window.
    ///
    /// <para><b>The two exceptions are named, and they are OUR bugs rather than the wiki's.</b>
    /// A bare count would let a third join them silently; naming them means the day one is
    /// fixed this test says so by failing. Both are known reward-name defects deferred to
    /// Delivery 2 (<c>MigrateSkyRewardRenames</c>):</para>
    /// <list type="bullet">
    /// <item><c>Harmonic Spear</c> — eqlwiki titles the item <c>Spear of Harmony</c>, so the
    ///   lookup misses. Matching the wiki is the standing rule (David, 2026-08-14).</item>
    /// <item><c>Windhowl/Spirit Render</c> — TWO rewards jammed into one string by an old
    ///   import. No item is called that, so no item page can ever match it.</item>
    /// </list>
    /// <para>Until then a Beastlord and a Bard get a sentence where everyone else gets the
    /// item window — which is the first time either bug has cost a player anything
    /// visible.</para></summary>
    [Fact]
    public void EverySkyRewardsItemIsInTheShippedCatalogExceptTheTwoWeMisname()
    {
        string[] knownMisnamed = ["Harmonic Spear", "Windhowl/Spirit Render"];

        var rewards = GuideCatalog.Default.Guides
            .Select(g => g.Name.Split(" - ")[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(95, rewards.Count);

        var missing = rewards
            .Where(r => string.IsNullOrWhiteSpace(ItemCatalog.Default.Find(r)?.StatsText))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(knownMisnamed.OrderBy(r => r, StringComparer.Ordinal), missing);
    }

    // ---- Fixture ----------------------------------------------------------------------

    /// <summary>A one-stage guide around the given objective JSON, valid in every respect the
    /// test under it is not about — so a failing assertion names the rule being exercised and
    /// nothing else.</summary>
    private static GuideCatalog OneStageCatalog(string objectivesJson) =>
        GuideCatalog.FromJson($$"""
            {
              "guides": [
                {
                  "id": "fixture",
                  "name": "Fixture guide",
                  "guideType": "NormalQuest",
                  "zoneNames": ["Somewhere"],
                  "applicableClasses": ["Warrior"],
                  "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-09-08" }],
                  "stages": [
                    { "id": "s1", "name": "The stage", "order": 1, "objectives": [ {{objectivesJson}} ] }
                  ]
                }
              ]
            }
            """);
}
