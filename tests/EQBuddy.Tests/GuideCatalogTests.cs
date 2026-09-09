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
    /// The must-list, stated positively (trap 34): every Authored objective ANSWERS all six
    /// questions and says where that came from. Asserted here as well as inside
    /// <c>Validate</c> because the rule is the product promise — if the validation is ever
    /// loosened, this is the test that has to be argued with.
    ///
    /// <para><b>Six, since 2026-09-09</b> (David: *"who, what, where, when, why, how should be
    /// the maximal number of things. We don't want to be redundant, but all must be
    /// addressed."*). A step whose WHEN is genuinely "any time you are in the zone" says that
    /// — the field is never left blank as a way of not answering, which is exactly the
    /// difference between an authored step and a hollow one wearing its clothes (lock 4a).</para>
    /// </summary>
    [Fact]
    public void EveryAuthoredObjectiveAnswersAllSixQuestionsAndCitesASource()
    {
        var authored = Shipped.Guides.SelectMany(g => g.AllObjectives)
            .Where(o => o.Authoring == GuideAuthoring.Authored).ToList();

        Assert.NotEmpty(authored);
        foreach (var o in authored)
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Who), $"{o.Id}: authored, no WHO");
            Assert.False(string.IsNullOrWhiteSpace(o.What), $"{o.Id}: authored, no WHAT");
            Assert.False(string.IsNullOrWhiteSpace(o.Where), $"{o.Id}: authored, no WHERE");
            Assert.False(string.IsNullOrWhiteSpace(o.When), $"{o.Id}: authored, no WHEN");
            Assert.False(string.IsNullOrWhiteSpace(o.Why), $"{o.Id}: authored, no WHY");
            Assert.False(string.IsNullOrWhiteSpace(o.How), $"{o.Id}: authored, no HOW");
            Assert.NotEmpty(o.Sources);
            foreach (var s in o.Sources)
            {
                Assert.NotEmpty(s.Url);
                Assert.NotEmpty(s.Title);
            }
        }
    }

    /// <summary>Prove-fail for the three questions added on 2026-09-09: a step answering the
    /// original who/where/what and NOTHING ELSE is refused. Green-only coverage of a widened
    /// rule is vacuous — the rule has to be shown rejecting what it newly forbids.</summary>
    [Theory]
    [InlineData("when")]
    [InlineData("why")]
    [InlineData("how")]
    public void AnAuthoredStepMissingAnyOneOfTheSixIsRefused(string missing)
    {
        var objective = new GuideObjective
        {
            Id = "step", Order = 1, ObjectiveType = "Loot", Title = "Loot it",
            ShortInstruction = "Loot it.",
            Who = "a named", Where = "an isle", What = "loot one",
            When = "when it is up", Why = "it is a turn-in piece", How = "kill and loot",
            Authoring = GuideAuthoring.Authored,
            Sources = [new GuideSource
            {
                Url = "https://eqlwiki.com/X", Title = "X", RetrievedAt = "2026-09-09",
            }],
        };
        switch (missing)
        {
            case "when": objective.When = ""; break;
            case "why": objective.Why = ""; break;
            case "how": objective.How = ""; break;
        }

        var catalog = new GuideCatalog
        {
            Guides =
            [
                new Guide
                {
                    Id = "g", Name = "G", ApplicableClasses = ["Warrior"],
                    ZoneNames = ["Plane of Sky"],
                    Sources = [.. objective.Sources],
                    Stages =
                    [
                        new GuideStage
                        {
                            Id = "s", Name = "S", Order = 1, Objectives = [objective],
                        },
                    ],
                },
            ],
        };

        var problems = catalog.Validate([]);

        Assert.Contains(problems, p => p.Contains(missing.ToUpperInvariant(), StringComparison.Ordinal));
        // And the same step with every field filled is shippable, so the refusal above is
        // about the missing one and not about the fixture.
        objective.When = "when it is up";
        objective.Why = "it is a turn-in piece";
        objective.How = "kill and loot";
        Assert.Empty(catalog.Validate([]));
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
        Assert.Empty(Shipped.ForClass("Necromancer"));
        Assert.Empty(Shipped.ForClass(""));
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
