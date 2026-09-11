using System.Diagnostics;

using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The auto-written half of the guide catalog (DRA-45, Delivery 2 N1):
/// <c>HarvestedGuides.json.gz</c>, one guide per quest, produced by
/// <c>scripts/harvests/eqlwiki/guides-transform.py</c> from wikitext already in the cache.
///
/// <para><b>Nobody reads 1,178 guides, so these tests are the review.</b> The transformer is
/// deliberately boring — it carries a page's own line and never answers a question the page
/// does not — and every assertion here is a way of catching it stopping being boring. The one
/// that matters most is the byte-for-byte re-run: a diff a person cannot read is not a review,
/// and re-running the script and finding the file unchanged is.</para>
///
/// <para>The five shape fixtures are hand-verified against the wikitext, not read back off the
/// output: a count you did not predict has not been checked (trap 23).</para>
/// </summary>
public class HarvestedGuidesTests
{
    private static readonly GuideCatalog Harvested = GuideCatalog.LoadHarvested();
    private static readonly GuideCatalog Curated = GuideCatalog.Curated;
    private static readonly GuideCatalog Merged = GuideCatalog.Default;

    /// <summary>Stage names the transformer mints itself. Every objective outside one of these
    /// came off a wiki page and must be Transcribed.</summary>
    private const string SkeletonStage = "Turn-in pieces";
    private const string UnwrittenStage = "Not written up yet";

    private static IEnumerable<(Guide Guide, GuideStage Stage, GuideObjective Objective)> AllRows(
        GuideCatalog catalog) =>
        from guide in catalog.Guides
        from stage in guide.Stages
        from objective in stage.Objectives
        select (guide, stage, objective);

    // ---- The file itself -------------------------------------------------------------

    /// <summary>One guide per quest in <c>QuestCatalog.json</c> AS THE FILE HAS IT — before
    /// <c>CatalogHygiene</c> drops the navigation pages and before <c>SkyTestSplit</c> rewrites
    /// the Sky aggregates, both of which are load-time rules the transformer cannot see from
    /// Python. What the app draws is the merge, and that is asserted separately below.</summary>
    [Fact]
    public void TheHarvestCarriesOneGuidePerQuestInTheCatalogFile()
    {
        Assert.Equal(1178, Harvested.Guides.Count);

        var questNames = HarvestFixtures.QuestNamesInTheFile();
        Assert.Equal(1178, questNames.Count);
        Assert.Equal(
            questNames,
            [.. Harvested.Guides.Select(g => g.QuestName)]);
    }

    [Fact]
    public void EveryHarvestedGuideHasAUniqueIdInItsOwnNamespace()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in Harvested.Guides)
        {
            Assert.StartsWith("hq-", guide.Id, StringComparison.Ordinal);
            Assert.True(ids.Add(guide.Id), $"duplicate harvested guide id '{guide.Id}'");
            Assert.Equal(GuideType.NormalQuest, guide.GuideType);
            Assert.NotEmpty(guide.Stages);
            Assert.NotEmpty(guide.AllObjectives);
        }
    }

    /// <summary>The rule set, over the harvested file on its own — so a failure names the
    /// transformer rather than arriving mixed in with the curated catalog's.</summary>
    [Fact]
    public void TheHarvestedFileValidates()
    {
        var problems = Harvested.Validate();
        Assert.True(problems.Count == 0,
            "HarvestedGuides.json.gz is not shippable:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems.Take(40)));
    }

    // ---- What states a harvested row may be in ---------------------------------------

    /// <summary>
    /// Three states and no others, each only where it belongs: the page's own sentence is
    /// <see cref="GuideAuthoring.Transcribed"/>, and the two skeleton stages the transformer
    /// mints are the ONLY place an <see cref="GuideAuthoring.Authored"/> or a
    /// <see cref="GuideAuthoring.Stub"/> row may appear.
    ///
    /// <para>Written as the positive claim and keyed on the STAGE, because the failure this
    /// guards is a transformer that starts answering who and where off prose — and that row
    /// would look exactly like a skeleton row except for where it sits (trap 34: forbidding
    /// the wrong thing cannot see a missing thing).</para>
    /// </summary>
    [Fact]
    public void EveryHarvestedRowIsTranscribedOrLivesInAStageTheTransformerMinted()
    {
        foreach (var (guide, stage, objective) in AllRows(Harvested))
        {
            var where = $"{guide.Id}/{stage.Id}/{objective.Id}";
            var minted = stage.Name is SkeletonStage or UnwrittenStage;

            if (!minted)
            {
                Assert.True(objective.Authoring == GuideAuthoring.Transcribed,
                    $"{where}: a row off a wiki page is {objective.Authoring}, not Transcribed "
                    + "— only the stages the transformer mints may answer a question");
                Assert.Equal("Custom", objective.ObjectiveType);
                continue;
            }

            Assert.True(objective.Authoring is GuideAuthoring.Authored or GuideAuthoring.Stub,
                $"{where}: a minted row is {objective.Authoring}");
        }
    }

    /// <summary>A transcribed row's one claim is the sentence. Validation refuses a filled
    /// Who/Where/When/How already; this counts them over all 1,178 so the number is in the
    /// record rather than implied by a rule passing.</summary>
    [Fact]
    public void NoTranscribedRowAnswersAQuestionThePageDidNot()
    {
        var transcribed = AllRows(Harvested)
            .Where(r => r.Objective.Authoring == GuideAuthoring.Transcribed)
            .ToList();

        Assert.True(transcribed.Count > 5_000,
            $"only {transcribed.Count} transcribed rows — the corpus lost its pages");
        Assert.Empty(transcribed.Where(r => r.Objective.Who.Length > 0
                                            || r.Objective.Where.Length > 0
                                            || r.Objective.When.Length > 0
                                            || r.Objective.How.Length > 0
                                            || r.Objective.Why.Length > 0
                                            || r.Objective.Title.Length > 0
                                            || r.Objective.ShortInstruction.Length > 0));
        Assert.Empty(transcribed.Where(r => r.Objective.What.Length == 0));
        Assert.Empty(transcribed.Where(r => r.Objective.Sources.Count == 0));
    }

    /// <summary>
    /// The DISTINCT-COUNT tell (trap 73). Forty-eight rows carrying ten distinct values for a
    /// per-row fact is a template, not research — that is how the invented prose was caught,
    /// by counting rather than by reading. A transformer that started synthesising sentences
    /// would fail here long before anyone read the file.
    /// </summary>
    [Fact]
    public void TranscribedSentencesAreTheCorpusAndNotATemplate()
    {
        var sentences = AllRows(Harvested)
            .Where(r => r.Objective.Authoring == GuideAuthoring.Transcribed)
            .Select(r => r.Objective.What)
            .ToList();

        var distinct = sentences.Distinct(StringComparer.Ordinal).Count();
        Assert.True(distinct * 10 >= sentences.Count * 7,
            $"{distinct} distinct sentences across {sentences.Count} transcribed rows — "
            + "a per-row fact that repeats that hard is a template");

        foreach (var banned in GuideCatalog.FabricatedProse)
            Assert.DoesNotContain(sentences,
                s => s.Contains(banned, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A skeleton row is <see cref="GuideAuthoring.Authored"/> only when the page's
    /// infobox states BOTH a quest giver and a start zone, and a <see cref="GuideAuthoring.Stub"/>
    /// naming the missing field otherwise. That asymmetry is the whole reason the skeleton is
    /// allowed to answer who and where at all: the fields are labelled rows on the page, not
    /// the parser's reading of prose.</summary>
    [Fact]
    public void ASkeletonRowIsAuthoredOnlyWhereTheInfoboxAnsweredBothQuestions()
    {
        foreach (var (guide, stage, objective) in AllRows(Harvested))
        {
            if (stage.Name != SkeletonStage) continue;
            var where = $"{guide.Id}/{objective.Id}";

            if (objective.Authoring == GuideAuthoring.Authored)
            {
                Assert.True(objective.Who.Length > 0, $"{where}: authored with no WHO");
                Assert.True(objective.Where.Length > 0, $"{where}: authored with no WHERE");
                Assert.Empty(objective.StubNote);
            }
            else
            {
                Assert.Equal(GuideAuthoring.Stub, objective.Authoring);
                Assert.Contains("infobox states no", objective.StubNote, StringComparison.Ordinal);
            }

            Assert.Contains(objective.ObjectiveType, (string[])["Collect", "TurnIn", "TalkToNpc"]);
            Assert.Equal(HarvestFixtures.SkeletonCaption, stage.ArrivalNote);
        }
    }

    // ---- What a harvested guide may never claim --------------------------------------

    /// <summary>
    /// Nothing harvested may reach into the two stores a curated guide owns. A reward key is
    /// the Plane of Sky turn-in the classic checklist has written since 1.x; an epic row id is
    /// the box <c>EpicLootAutoCheck</c> writes. A transformer that minted either would put a
    /// machine-read sentence in charge of a tick a human curated — trap 4 with the blast radius
    /// pointing at the two surfaces that already work.
    /// </summary>
    [Fact]
    public void NoHarvestedRowClaimsASkyRewardKeyOrAnEpicRow()
    {
        var epicIds = new HashSet<string>(
            EpicQuestChecklistCatalog.LoadEmbedded().Classes.SelectMany(c => c.Rows).Select(r => r.Id),
            StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(epicIds);

        foreach (var (guide, _, objective) in AllRows(Harvested))
        {
            Assert.True(objective.RewardKey.Length == 0,
                $"{guide.Id}/{objective.Id}: claims Sky reward key '{objective.RewardKey}'");
            Assert.False(epicIds.Contains(objective.Id),
                $"{guide.Id}/{objective.Id}: collides with an Epic checklist row id");
        }
    }

    /// <summary>Empty, and that is the honest state until the system that would fill it exists
    /// — the same claim <c>EpicGuideTests</c> holds open over the curated file. A transformer
    /// is the last thing that should be recommending gear.</summary>
    [Fact]
    public void NoHarvestedGuideCarriesAnAttachmentOrAnInventedPrerequisite()
    {
        foreach (var (_, stage, objective) in AllRows(Harvested))
        {
            Assert.Empty(objective.Attachments);
            Assert.Empty(stage.Attachments);
            // Sequence is DOCUMENT ORDER and nothing else. A prerequisite is a claim about
            // which step blocks which, and no page states one in a form a parser can read.
            Assert.Empty(objective.PrerequisiteObjectiveIds);
        }
    }

    // ---- The merge --------------------------------------------------------------------

    /// <summary>The fourteen class epic pages are the live collision, and the curated guide
    /// has to win every one of them: a person transcribed 486 rows into sections a flat
    /// re-read of the same page cannot see.</summary>
    [Fact]
    public void CuratedWinsOnQuestNameOverTheRealCollisionSet()
    {
        var curatedQuests = new HashSet<string>(
            Curated.Guides.Select(g => g.QuestName), StringComparer.OrdinalIgnoreCase);
        var collisions = Harvested.Guides
            .Where(g => curatedQuests.Contains(g.QuestName))
            .Select(g => g.QuestName)
            .ToList();

        Assert.Equal(14, collisions.Count);
        Assert.All(collisions, n => Assert.EndsWith("Epic Quest", n, StringComparison.Ordinal));

        foreach (var name in collisions)
        {
            var drawn = Merged.Guides
                .Where(g => string.Equals(g.QuestName, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var only = Assert.Single(drawn);
            Assert.Equal(GuideType.EpicQuest, only.GuideType);
            Assert.DoesNotContain("hq-", only.Id, StringComparison.Ordinal);
        }
    }

    /// <summary>The same rule against a FIXTURE, so the win is tested by its shape and not
    /// only by today's fourteen — the day a curated guide is written for an ordinary quest,
    /// this is what says the harvested one steps aside (trap 34's must-list, from both ends:
    /// the curated guide arrives AND the harvested one does not).</summary>
    [Fact]
    public void CuratedWinsOnQuestNameOverAFixture()
    {
        var curated = GuideCatalog.FromJson(HarvestFixtures.CuratedRivalJson);
        var merged = GuideCatalog.Merge(curated, Harvested, ["A Job for Nanrum", "Acumen Mask Quest"]);

        var forNanrum = merged.Guides
            .Where(g => string.Equals(g.QuestName, "A Job for Nanrum", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var only = Assert.Single(forNanrum);
        Assert.Equal("fixture-nanrum", only.Id);

        // …and the quests the fixture does NOT claim still arrive from the harvest.
        Assert.Contains(merged.Guides, g => g.Id == "hq-acumen-mask-quest");
    }

    /// <summary>
    /// A harvested guide only arrives if its quest survives the load. <c>CatalogHygiene</c>
    /// drops five navigation pages outright and <c>SkyTestSplit</c> replaces each class's
    /// aggregate Sky page with one quest per reward — so a guide keyed to either is a guide no
    /// surface can reach. The exact set is named here rather than counted, because a silent
    /// drop is how a catalog quietly stops covering what it says it covers.
    /// </summary>
    [Fact]
    public void AHarvestedGuideWhoseQuestTheAppDropsNeverReachesASurface()
    {
        var loaded = new HashSet<string>(
            QuestCatalog.LoadEmbedded().Quests.Select(q => q.Name), StringComparer.OrdinalIgnoreCase);
        var unreachable = Harvested.Guides
            .Where(g => !loaded.Contains(g.QuestName))
            .Select(g => g.QuestName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(HarvestFixtures.QuestsTheAppDrops, unreachable);
        foreach (var name in unreachable)
            Assert.DoesNotContain(Merged.Guides,
                g => string.Equals(g.QuestName, name, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(Curated.Guides.Count + Harvested.Guides.Count - 14 - unreachable.Count,
            Merged.Guides.Count);
    }

    // ---- One fixture page per shape --------------------------------------------------
    //
    // Every count below was read off the WIKITEXT by hand before the file was consulted.
    // They are the review of the extraction rule: which lines become rows, which are dropped,
    // and where a stage begins.

    /// <summary>`== Checklist ==` wins over `== Walkthrough ==` where a page has both, and its
    /// four `*[[Item]] from …` bullets become four rows. The page's Dialogue section is not
    /// the walkthrough and contributes nothing.</summary>
    [Fact]
    public void ChecklistShape_AcumenMaskQuest()
    {
        var guide = Find("hq-acumen-mask-quest");
        Assert.Equal(2, guide.Stages.Count);

        var checklist = guide.Stages[0];
        Assert.Equal("Checklist", checklist.Name);
        Assert.Equal(4, checklist.Objectives.Count);
        Assert.Equal("Glowing Mask from a skeleton monk or A Froglok Scryer in Upper Guk",
            checklist.Objectives[0].What);
        Assert.Equal("Bonechipped Mask from A Goblin Headmaster in Ocean of Tears",
            checklist.Objectives[3].What);

        // Four turn-in items plus the hand-in. The `{{CheckboxList}}` and `{{End}}` templates
        // and the "Obtain the following items…" prose line all drop.
        var turnIn = guide.Stages[1];
        Assert.Equal(SkeletonStage, turnIn.Name);
        Assert.Equal(5, turnIn.Objectives.Count);
        Assert.Equal(4, turnIn.Objectives.Count(o => o.ObjectiveType == "Collect"));
        Assert.Equal("Hand the pieces to Vilissia.", turnIn.Objectives[^1].What);
    }

    /// <summary>Two `=== … ===` headings inside the Walkthrough are two stages, and the lead
    /// bold line before the first heading keeps the section's own name. The five-bullet
    /// `<div class="facblock">` under each version is the page's faction RESULT block and
    /// contributes nothing — those bullets are not steps.</summary>
    [Fact]
    public void SubsectionedShape_DeathfistSlashedBelts()
    {
        var guide = Find("hq-deathfist-slashed-belts");
        Assert.Equal(4, guide.Stages.Count);

        Assert.Equal("Walkthrough", guide.Stages[0].Name);
        Assert.Equal(1, guide.Stages[0].Objectives.Count);
        Assert.StartsWith("There are two versions of this quest",
            guide.Stages[0].Objectives[0].What, StringComparison.Ordinal);

        Assert.Equal("Good Version", guide.Stages[1].Name);
        Assert.Equal(3, guide.Stages[1].Objectives.Count);

        Assert.Equal("Evil Version (Freeport Militia)", guide.Stages[2].Name);
        Assert.Equal(2, guide.Stages[2].Objectives.Count);

        Assert.Equal(SkeletonStage, guide.Stages[3].Name);

        Assert.DoesNotContain(guide.AllObjectives,
            o => o.What.Contains("faction standing", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Three wholly-bold lines and nothing else: the page's prose paragraphs, its NPC
    /// speech and its two faction blocks all drop, and the inline bold inside a sentence
    /// ("performed at '''dubious''' and higher") is not a bold LINE.</summary>
    [Fact]
    public void BoldOnlyShape_NoteForKonem()
    {
        var guide = Find("hq-note-for-konem");
        var stage = Assert.Single(guide.Stages);
        Assert.Equal("Walkthrough", stage.Name);
        Assert.Equal(3, stage.Objectives.Count);
        Assert.StartsWith("Konem Matse is in Qeynos Hills", stage.Objectives[0].What,
            StringComparison.Ordinal);
        Assert.Equal("You receive: Grathin's Invoice.", stage.Objectives[1].What);
        Assert.Equal("Give it to Phin.", stage.Objectives[2].What);
    }

    /// <summary>The player's own lines are rows; the NPC's replies are not. Both are quoted
    /// speech one line apart, and telling them apart is the whole of the rule — "You say" is a
    /// thing to do and "Dzan Amo says" is a thing that happens.</summary>
    [Fact]
    public void DialogueShape_BatFurAndBeetleLegs()
    {
        var guide = Find("hq-bat-fur-and-beetle-legs");
        var stage = Assert.Single(guide.Stages);
        Assert.Equal(2, stage.Objectives.Count);
        Assert.Equal("You say, 'Hail, Dzan Amo'", stage.Objectives[0].What);
        Assert.Equal("You say, 'What service?'", stage.Objectives[1].What);
        Assert.DoesNotContain(guide.AllObjectives,
            o => o.What.Contains("Dzan Amo says", StringComparison.Ordinal));
    }

    /// <summary>No section we carry and no turn-in items: one Stub that says which of the two
    /// reasons it is. An empty guide would be a silent no-op wearing a guide's clothes.</summary>
    [Fact]
    public void NothingToCarryShape_GuildSummons()
    {
        var guide = Find("hq-guild-summons");
        var stage = Assert.Single(guide.Stages);
        Assert.Equal(UnwrittenStage, stage.Name);
        var only = Assert.Single(stage.Objectives);
        Assert.Equal(GuideAuthoring.Stub, only.Authoring);
        Assert.Contains("no Checklist and no Walkthrough", only.StubNote, StringComparison.Ordinal);
        Assert.NotEmpty(only.Title);
    }

    /// <summary>The 250 collection-split steps live inside their parent's page and have no
    /// cache file of their own, so the item list is the whole guide — led by a TalkToNpc only
    /// because this page's infobox states both a giver and a start zone.</summary>
    [Fact]
    public void SkeletonOnlyShape_BootsOfTheReliant()
    {
        var guide = Find("hq-boots-of-the-reliant");
        var stage = Assert.Single(guide.Stages);
        Assert.Equal(SkeletonStage, stage.Name);
        Assert.Equal("TalkToNpc", stage.Objectives[0].ObjectiveType);
        Assert.Equal("TurnIn", stage.Objectives[^1].ObjectiveType);
        Assert.All(stage.Objectives, o => Assert.Equal(GuideAuthoring.Authored, o.Authoring));
        Assert.Equal(HarvestFixtures.SkeletonCaption, stage.ArrivalNote);

        // The URL is the PARENT page's, because that is where the words are — and the source
        // title is read back off it rather than from the step's own synthetic name (trap 3).
        var source = Assert.Single(guide.Sources);
        Assert.Equal("Armor of the Priest Quests", source.Title);
    }

    // ---- The check that a diff cannot give you ---------------------------------------

    /// <summary>
    /// Re-run the transformer and the committed DATA must not move. Everything above reads
    /// the OUTPUT; this is the only assertion that holds the SCRIPT to it, and it is the reason
    /// a reviewer can believe 1,178 guides they will never read.
    ///
    /// <para><b>The data, decompressed — not the gzip file.</b> A gzip container is not
    /// reproducible across environments: two zlib builds compress identical input to different
    /// bytes, and the first CI run of this gate proved it by failing on a file whose contents
    /// were identical (runner Python 3.12 against a 3.14 developer box). Comparing the
    /// compressed file is a gate that fails on a Python version rather than on a data change,
    /// which is worse than no gate — it teaches the next person to re-run and commit until it
    /// goes green.</para>
    ///
    /// <para>Shells out because the transformer is Python and re-implementing it in C# would be
    /// a second producer of the very rule under test (trap 4). <c>build-and-test</c> installs
    /// Python for this step; a developer box without it gets a NAMED skip rather than a silent
    /// pass, because a gate that reads as coverage without running is trap 34.</para>
    /// </summary>
    [Fact]
    public void TheTransformerReproducesTheCommittedFileByteForByte()
    {
        var python = HarvestFixtures.FindPython();
        if (python is null)
        {
            Assert.True(Environment.GetEnvironmentVariable("CI") is null,
                "CI must have Python on PATH — build-and-test installs it for this gate");
            return;
        }

        var script = Path.Combine(HarvestFixtures.RepoRoot, "scripts", "harvests", "eqlwiki",
            "guides-transform.py");
        Assert.True(File.Exists(script), script);

        var psi = new ProcessStartInfo(python)
        {
            WorkingDirectory = HarvestFixtures.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("--check");

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        Assert.True(process.ExitCode == 0,
            "guides-transform.py --check says the committed file is not what it would write. "
            + "Re-run `python scripts/harvests/eqlwiki/guides-transform.py` and commit the "
            + $"result.{Environment.NewLine}{stdout}{stderr}");
    }

    private static Guide Find(string id) =>
        Harvested.Find(id) ?? throw new Xunit.Sdk.XunitException(
            $"no harvested guide '{id}' — the fixture page moved or its name changed");
}

/// <summary>Fixture data and the small bits of plumbing the assertions above lean on. Kept out
/// of the test class so each test reads as its claim.</summary>
internal static class HarvestFixtures
{
    /// <summary>The caption every skeleton stage carries, verbatim — the sentence that keeps a
    /// list of ITEMS from reading as a list of PLACES.</summary>
    public const string SkeletonCaption = "from the quest's item list";

    /// <summary>Quests the transformer writes a guide for and the app then drops: five
    /// navigation pages (<c>CatalogHygiene</c>) and the per-class Sky aggregates
    /// (<c>SkyTestSplit</c>, which replaces each with one quest per reward). Named rather than
    /// counted, so adding one is a decision somebody makes on purpose.</summary>
    public static readonly string[] QuestsTheAppDrops =
    [
        "All Positive Faction Quests",
        "Bard Plane of Sky Tests",
        "Beastlord Plane of Sky Tests",
        "Berserker Plane of Sky Tests",
        "Class Race Quest List",
        "Cleric Plane of Sky Tests",
        "Druid Plane of Sky Tests",
        "Enchanter Plane of Sky Tests",
        "Faction Quests",
        "Magician Plane of Sky Tests",
        "Monk Plane of Sky Tests",
        "Necromancer Plane of Sky Tests",
        "Paladin Plane of Sky Tests",
        "Popular Quests by Level",
        "Ranger Plane of Sky Tests",
        "Rogue Plane of Sky Tests",
        "Shadow Knight Plane of Sky Tests",
        "Shaman Plane of Sky Tests",
        "Velious Class Armor Comparisons",
        "Warrior Plane of Sky Tests",
        "Wizard Plane of Sky Tests",
    ];

    /// <summary>A curated guide for an ORDINARY quest — the shape that does not exist yet, so
    /// that "curated wins" is tested as a rule rather than as today's fourteen epics.</summary>
    public const string CuratedRivalJson = """
    {
      "note": "fixture",
      "guides": [{
        "id": "fixture-nanrum",
        "name": "A Job for Nanrum (curated)",
        "guideType": "NormalQuest",
        "questName": "A Job for Nanrum",
        "zoneNames": ["Grobb"],
        "applicableClasses": ["Warrior"],
        "sources": [{"url": "https://eqlwiki.com/A_Job_for_Nanrum",
                     "title": "A Job for Nanrum", "retrievedAt": "2026-09-11"}],
        "stages": [{
          "id": "fixture-nanrum-s1", "name": "Eyeballs", "order": 1,
          "objectives": [{
            "id": "fixture-nanrum-s1-o1", "order": 1, "objectiveType": "Farm",
            "title": "Three fire beetle eyes", "shortInstruction": "Kill fire beetles",
            "who": "a fire beetle", "where": "Innothule Swamp",
            "what": "Loot three Fire Beetle Eyes.",
            "authoring": "Authored",
            "sources": [{"url": "https://eqlwiki.com/A_Job_for_Nanrum",
                         "title": "A Job for Nanrum", "retrievedAt": "2026-09-11"}]
          }]
        }]
      }]
    }
    """;

    /// <summary>The quest names in <c>QuestCatalog.json</c> AS THE FILE HAS THEM — read through
    /// the embedded resource rather than off disk, so the assertion holds against what shipped
    /// and not against a working copy.</summary>
    public static IReadOnlyList<string> QuestNamesInTheFile()
    {
        using var stream = typeof(QuestCatalog).Assembly
            .GetManifestResourceStream("EQBuddy.Core.Data.QuestCatalog.json")!;
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        return [.. document.RootElement.GetProperty("quests").EnumerateArray()
            .Select(q => q.GetProperty("name").GetString()!)];
    }

    /// <summary>The repo root, found by walking up for the solution file — the test host's
    /// working directory is its own bin folder and says nothing about where the scripts
    /// are.</summary>
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EQBuddy.slnx")))
                dir = dir.Parent;
            return dir?.FullName
                   ?? throw new InvalidOperationException("EQBuddy.slnx not found above "
                                                          + AppContext.BaseDirectory);
        }
    }

    /// <summary>Python, or null. `python` first because that is what the refresh workflow's
    /// `setup-python` puts on PATH; `py` is the Windows launcher a developer box usually
    /// has.</summary>
    public static string? FindPython()
    {
        foreach (var candidate in new[] { "python", "py" })
        {
            try
            {
                var psi = new ProcessStartInfo(candidate)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("--version");
                using var probe = Process.Start(psi);
                if (probe is null) continue;
                probe.WaitForExit(20_000);
                if (probe.HasExited && probe.ExitCode == 0) return candidate;
            }
            catch
            {
                // Not on PATH; try the next spelling.
            }
        }
        return null;
    }
}
