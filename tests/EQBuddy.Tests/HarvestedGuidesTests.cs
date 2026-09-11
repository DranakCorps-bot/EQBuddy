using System.Text.Json;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The AUTO-WRITTEN half of the guide catalog (DRA-45, Delivery 2 N1).
///
/// <para><b>The rule this suite exists to keep.</b> <c>GuideCatalog.json</c> is curated and a
/// machine may never write it; <c>HarvestedGuides.json.gz</c> is the opposite — regenerated
/// by <c>scripts/harvests/eqlwiki/guides-transform.py</c> inside the weekly refresh, never
/// hand-edited, and admitted at load only where no authored guide claims the quest. What
/// keeps that safe is not the merge rule on its own but WHAT THE TRANSFORMER IS ALLOWED TO
/// SAY: one line of the page's own prose, or a row built from the catalog's structured item
/// list. It never answers who or where from prose, which is trap 73 at 11,000×.</para>
///
/// <para>So the assertions below are a MUST-LIST (trap 34): every harvested objective is one
/// of four enumerated shapes and nothing else. A "no harvested guide may…" rule cannot see
/// the shape nobody thought of.</para>
///
/// <para><b>Byte-reproducibility is a CI step, not a test here</b> —
/// <c>guides-transform.py --check</c> in <c>.github/workflows/ci.yml</c>. Spawning Python
/// from xunit would make `dotnet test` fail on a machine that has none, and a test that
/// skips instead is the vacuous coverage the rest of this file is written against. What this
/// suite can do without Python is hold the committed FILE to its shape, which it does.</para>
/// </summary>
public class HarvestedGuidesTests
{
    private static readonly GuideCatalog Harvested = GuideCatalog.LoadHarvested();
    private static readonly GuideCatalog Curated = GuideCatalog.LoadCurated();
    private static readonly GuideCatalog Shipped = GuideCatalog.Default;
    private static readonly QuestCatalog Quests = QuestCatalog.LoadEmbedded();

    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static Guide ForQuest(string questName) =>
        Harvested.Guides.Single(g =>
            string.Equals(g.QuestName, questName, StringComparison.OrdinalIgnoreCase));

    // ---- The file --------------------------------------------------------------------

    /// <summary>
    /// One guide per catalog quest — <b>except the fourteen that have nothing to say at all</b>,
    /// which are named rather than counted.
    ///
    /// <para>Fable's §3 predicted 1,178, one per catalog row. Fourteen of those rows are index
    /// or collection PAGES: no walkthrough, no checklist, no turn-in items, and no quest giver
    /// and start zone to open with. A guide with no objectives is refused by lock 4a, and
    /// inventing a step so the count reads 1,178 would be the fabrication this whole file is
    /// built to avoid. Naming them means the day one of them gains content on the wiki, this
    /// fails and says which — a bare 1,164 would not.</para>
    /// </summary>
    [Fact]
    public void EveryCatalogQuestGetsAGuideExceptTheFourteenWithNothingToSay()
    {
        string[] nothingToSay =
        [
            "Bone Chips Quests", "Burning Soul of the Pestilent", "Burning Soul of the Pious",
            "Burning Soul of the Virtuous", "Cougarskin Sleeves Quest", "Dozekar Tear Quests",
            "Faction Quests", "Guild Summons", "Monk Quests", "Orc Belt Quests",
            "Popular Quests by Level", "Scroll of G'han", "Velious Class Armor",
            "Velious Class Armor Comparisons",
        ];

        // The RAW catalog: the transformer reads the file, so its universe is every row in
        // it, not the smaller set QuestCatalog.LoadEmbedded hands the app.
        var raw = ReadRawQuestNames();
        Assert.Equal(1178, raw.Count);

        var guided = new HashSet<string>(
            Harvested.Guides.Select(g => g.QuestName), StringComparer.OrdinalIgnoreCase);
        var without = raw.Where(n => !guided.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(nothingToSay.OrderBy(n => n, StringComparer.Ordinal), without);
        Assert.Equal(1164, Harvested.Guides.Count);
    }

    [Fact]
    public void EveryHarvestedGuideIsANormalQuestGuideThatNamesItsQuest()
    {
        Assert.All(Harvested.Guides, g =>
        {
            Assert.Equal(GuideType.NormalQuest, g.GuideType);
            Assert.NotEmpty(g.QuestName);
            Assert.Equal(g.QuestName, g.Name);
            Assert.NotEmpty(g.Sources);
            Assert.NotEmpty(g.Stages);
            Assert.NotEmpty(g.AllObjectives);
        });

        // Ids are unique and marked as machine-written, so no authoring PR can collide with
        // one by accident and no reviewer can mistake one for a curated guide.
        Assert.All(Harvested.Guides, g => Assert.StartsWith("harvested-", g.Id, StringComparison.Ordinal));
        Assert.Equal(Harvested.Guides.Count,
            Harvested.Guides.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>The whole rule set, over the harvested half on its own. It also runs as part
    /// of <c>GuideCatalogTests.TheShippedCatalogValidates</c>, but that one sees only the
    /// guides the merge ADMITS — the ones a curated guide displaces, or whose quest the app
    /// drops, would never be checked at all.</summary>
    [Fact]
    public void TheHarvestedFileValidates()
    {
        var problems = Harvested.Validate();
        Assert.True(problems.Count == 0,
            "Data/HarvestedGuides.json.gz is not shippable:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems.Take(40)));
    }

    /// <summary>
    /// Prove-fail for the validation rule this slice swapped in: a <c>NormalQuest</c> guide
    /// that names no quest is REFUSED.
    ///
    /// <para>The old rule asked every guide for classes and zones. A harvested guide carries
    /// neither on purpose — eqlwiki's free-text <c>Classes</c> cell is "All" on 318 pages,
    /// "?" on 87 and blank on 40, and copying it would be a second producer of a fact
    /// <c>QuestCatalog</c> already owns — so the requirement swapped rather than relaxed.
    /// If the swap had quietly dropped both halves, a guide reachable from nowhere would
    /// validate clean, which is the silent version of this whole failure.</para>
    /// </summary>
    [Fact]
    public void ANormalQuestGuideThatNamesNoQuestIsRefused()
    {
        var orphan = OneGuide("harvested-orphan", "", GuideType.NormalQuest);
        orphan.Name = "An orphan";
        orphan.Stages[0].Objectives.Add(new GuideObjective
        {
            Id = "o", Order = 1, ObjectiveType = "Custom", What = "Do the thing.",
            Authoring = GuideAuthoring.Transcribed,
            Sources = [new GuideSource
            {
                Url = "https://eqlwiki.com/X", Title = "X", RetrievedAt = "2026-09-11",
            }],
        });

        var problems = new GuideCatalog { Guides = [orphan] }.Validate();
        Assert.Contains(problems, p => p.Contains("names no quest", StringComparison.Ordinal));

        // ...and the same guide WITH a quest name validates, so the failure above is the
        // rule firing rather than the fixture being broken in some other way.
        orphan.QuestName = "A Quest";
        Assert.Empty(new GuideCatalog { Guides = [orphan] }.Validate());
    }

    /// <summary>And the other half of the swap: a class-keyed guide still has to name its
    /// classes and zones. Relaxing that for everybody would have been the easy way to make
    /// the harvested file validate, and it would have let a Sky guide no screen can open
    /// ship green.</summary>
    [Fact]
    public void ASkyGuideStillHasToNameItsClassesAndZones()
    {
        var sky = OneGuide("authored-sky", "A Sky Test", GuideType.PlaneOfSkyQuest);
        sky.ApplicableClasses.Clear();
        sky.Stages[0].Objectives.Add(new GuideObjective
        {
            Id = "o", Order = 1, ObjectiveType = "Custom", What = "Do the thing.",
            Authoring = GuideAuthoring.Transcribed,
            Sources = [new GuideSource
            {
                Url = "https://eqlwiki.com/X", Title = "X", RetrievedAt = "2026-09-11",
            }],
        });

        Assert.Contains(new GuideCatalog { Guides = [sky] }.Validate(),
            p => p.Contains("no applicable classes", StringComparison.Ordinal));
    }

    // ---- What a harvested objective may be ---------------------------------------------

    /// <summary>
    /// THE MUST-LIST: every harvested objective is one of exactly four shapes.
    ///
    /// <list type="number">
    /// <item>A <c>Custom</c> / <c>Transcribed</c> row — one line of the page's prose in WHAT,
    ///   every other question empty, the page cited.</item>
    /// <item>A <c>Collect</c> / <c>Stub</c> row — one catalog turn-in item, with the note
    ///   that says we do not know where it drops.</item>
    /// <item>A <c>TurnIn</c> row — <c>Authored</c> when the infobox names both the giver and
    ///   the start zone, <c>Stub</c> when it does not.</item>
    /// <item>A <c>TalkToNpc</c> / <c>Authored</c> row — the one step a skeleton-only guide
    ///   can honestly open with.</item>
    /// </list>
    ///
    /// <para>Written positively and enumerated, because the failure to catch is a FIFTH shape
    /// nobody predicted — a transformer that started filling WHERE from a "(found in
    /// Crushbone)" parenthesis would still pass every "may not" rule anyone thought to write.</para>
    /// </summary>
    [Fact]
    public void EveryHarvestedObjectiveIsOneOfFourShapesAndNothingElse()
    {
        var odd = new List<string>();
        foreach (var guide in Harvested.Guides)
            foreach (var stage in guide.Stages)
                foreach (var o in stage.Objectives)
                {
                    var shape = (o.ObjectiveType, o.Authoring) switch
                    {
                        ("Custom", GuideAuthoring.Transcribed) => Prose(o),
                        ("Collect", GuideAuthoring.Stub) => Collect(o),
                        ("TurnIn", GuideAuthoring.Authored) => HandIn(o, answered: true),
                        ("TurnIn", GuideAuthoring.Stub) => HandIn(o, answered: false),
                        ("TalkToNpc", GuideAuthoring.Authored) => Speak(o),
                        _ => $"is a {o.ObjectiveType}/{o.Authoring}, which is not one of the four",
                    };
                    if (shape.Length > 0) odd.Add($"{guide.Id}/{o.Id}: {shape}");
                }

        Assert.True(odd.Count == 0, string.Join(Environment.NewLine, odd.Take(30)));

        static string Prose(GuideObjective o) =>
            o.What.Length == 0 ? "transcribed with no sentence"
            : (o.Who + o.Where + o.When + o.How).Length > 0
                ? "transcribed and answers a question the page states as prose (trap 73)"
            : o.Sources.Count == 0 ? "transcribed and cites nothing"
            : o.ItemNames.Count > 0 ? "transcribed and names items — nothing parses items out of prose"
            : "";

        static string Collect(GuideObjective o) =>
            o.ItemNames.Count != 1 ? $"a Collect naming {o.ItemNames.Count} items, not one"
            : o.StubNote.Length == 0 ? "a Collect stub with no note"
            : (o.Who + o.Where).Length > 0
                ? "a Collect that says who or where — the infobox answers neither for a drop"
            : "";

        static string HandIn(GuideObjective o, bool answered) =>
            answered && (o.Who.Length == 0 || o.Where.Length == 0)
                ? "an Authored hand-in missing WHO or WHERE"
            : !answered && o.StubNote.Length == 0 ? "a hand-in stub with no note"
            : o.PrerequisiteObjectiveIds.Count == 0 && o.Order > 1
                ? "a hand-in that waits on none of its own Collect rows"
            : "";

        static string Speak(GuideObjective o) =>
            o.Who.Length == 0 || o.Where.Length == 0 ? "a TalkToNpc missing WHO or WHERE" : "";
    }

    /// <summary>
    /// No harvested guide reaches into a store a CURATED guide owns.
    ///
    /// <para>A <c>RewardKey</c> would put a machine-written row on the Sky turn-in the
    /// checklist, the phone and the achievements import all share; an objective id matching
    /// an Epic checklist row would route it to that row's box (<c>GuideProgressHome.EpicItem</c>
    /// matches on the id alone). Either is one fact with two writers, and the machine would
    /// be the one nobody reviewed.</para>
    /// </summary>
    [Fact]
    public void NoHarvestedGuideClaimsASkyRewardKeyOrAnEpicChecklistRow()
    {
        var epicRowIds = new HashSet<string>(
            EpicQuestDefaults.Items().Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(epicRowIds);
        Assert.NotEmpty(GuideCatalog.SkyRewardKeys);

        var claims = Harvested.Guides
            .SelectMany(g => g.AllObjectives.Select(o => (g.Id, Objective: o)))
            .Where(x => x.Objective.RewardKey.Length > 0 || epicRowIds.Contains(x.Objective.Id))
            .Select(x => $"{x.Id}/{x.Objective.Id} claims "
                + (x.Objective.RewardKey.Length > 0
                    ? $"Sky reward key '{x.Objective.RewardKey}'" : "an Epic checklist row"))
            .ToList();

        Assert.Empty(claims);
    }

    /// <summary>
    /// Prove-fail for the rule above, all the way through to the CONSEQUENCE: a harvested row
    /// wearing an epic checklist row's id really would write that row's box.
    ///
    /// <para>Asserting only "the shipped file has none" would pass on an empty id set or on
    /// ids that can never collide, and would say nothing about why the collision matters.
    /// This runs the router over the collision and shows it routing to
    /// <see cref="GuideProgressHome.EpicItem"/> — the machine's row and the curated tab's row
    /// writing one boolean.</para>
    /// </summary>
    [Fact]
    public void AHarvestedRowWearingAnEpicRowsIdWouldWriteThatRowsBox()
    {
        var epicRow = EpicQuestDefaults.Items()[0];
        var impostor = new GuideObjective
        {
            Id = epicRow.Id, Order = 1, ObjectiveType = "Custom", What = "A harvested line.",
            Authoring = GuideAuthoring.Transcribed,
        };

        Assert.Equal(GuideProgressHome.EpicItem,
            GuideProgressRouter.HomeFor(impostor, new GuideStores([], [epicRow], null), out var backing));
        Assert.Same(epicRow, backing.EpicRow);

        // And a real harvested row does not, which is what the shipped-file assertion above
        // is protecting.
        var real = Harvested.Guides.SelectMany(g => g.AllObjectives)
            .First(o => o.Authoring == GuideAuthoring.Transcribed);
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.HomeFor(real, new GuideStores([], [epicRow], null), out _));
    }

    // ---- The merge ---------------------------------------------------------------------

    /// <summary>
    /// A curated guide WINS on quest name — the promise that lets a machine write guides at
    /// all. The fourteen Epic 1.0 quests are the live proof: the harvested file carries a row
    /// for every one of them, and not one reaches the shipped catalog.
    /// </summary>
    [Fact]
    public void ACuratedGuideWinsOnQuestNameAndTheFourteenEpicsProveIt()
    {
        var epicQuestNames = Curated.Guides
            .Where(g => g.GuideType == GuideType.EpicQuest)
            .Select(g => g.QuestName).ToList();
        Assert.Equal(14, epicQuestNames.Count);

        foreach (var questName in epicQuestNames)
        {
            // The harvested file HAS one — otherwise this passes for the wrong reason.
            Assert.Contains(Harvested.Guides, g =>
                string.Equals(g.QuestName, questName, StringComparison.OrdinalIgnoreCase));

            var shipped = Shipped.Guides
                .Where(g => string.Equals(g.QuestName, questName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Single(shipped);
            Assert.Equal(GuideType.EpicQuest, shipped[0].GuideType);
        }
    }

    /// <summary>The merge rule, on a fixture, both ways round — curated displaces harvested
    /// for the same quest, and an unclaimed harvested guide is admitted.</summary>
    [Fact]
    public void MergeKeepsTheCuratedGuideAndAdmitsTheUnclaimedOne()
    {
        var curated = OneGuide("authored-thing", "The Thing", GuideType.PlaneOfSkyQuest);
        var harvestedSame = OneGuide("harvested-the-thing", "The Thing", GuideType.NormalQuest);
        var harvestedOther = OneGuide("harvested-other", "The Other", GuideType.NormalQuest);

        var merged = GuideCatalog.Merge(
            new GuideCatalog { Guides = [curated] },
            new GuideCatalog { Guides = [harvestedSame, harvestedOther] },
            ["The Thing", "The Other"]);

        Assert.Equal(["authored-thing", "harvested-other"], merged.Guides.Select(g => g.Id));
    }

    /// <summary>And a harvested guide for a quest the APP does not load is not merged —
    /// <c>QuestCatalog.LoadEmbedded</c> drops the five index pages and replaces the Sky
    /// aggregates, and the transformer reading the raw file cannot know either.</summary>
    [Fact]
    public void AHarvestedGuideForAQuestTheAppDoesNotLoadIsNotMerged()
    {
        var merged = GuideCatalog.Merge(
            new GuideCatalog(),
            new GuideCatalog { Guides = [OneGuide("harvested-ghost", "A Page Nobody Loads",
                GuideType.NormalQuest)] },
            ["Something Else"]);

        Assert.Empty(merged.Guides);
    }

    /// <summary>
    /// The shipped catalog is the curated file plus every harvested guide the two rules
    /// admit, and the ones they turn away are NAMED.
    ///
    /// <para>A bare count would pass on the wrong set. These are the two reasons a harvested
    /// guide can fail to ship — a curated guide claims the quest, or the app does not load
    /// the quest at all — and the day a third appears, this says so.</para>
    /// </summary>
    [Fact]
    public void TheShippedCatalogIsTheCuratedFilePlusTheAdmittedHarvestedGuides()
    {
        var known = new HashSet<string>(Quests.Quests.Select(q => q.Name),
            StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(
            Curated.Guides.Select(g => g.QuestName).Where(n => n.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var turnedAway = Harvested.Guides
            .Where(g => !known.Contains(g.QuestName) || claimed.Contains(g.QuestName))
            .Select(g => g.QuestName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Two pure index pages CatalogHygiene removes (the other three have no harvested
        // guide to turn away — they are in the fourteen with nothing to say), the sixteen
        // "{Class} Plane of Sky Tests" aggregates SkyTestSplit replaces with one quest per
        // reward, and the fourteen epics the curated catalog owns. 2 + 16 + 14 = 32.
        Assert.Equal(
            new[]
            {
                "All Positive Faction Quests", "Bard Epic Quest", "Bard Plane of Sky Tests",
                "Beastlord Plane of Sky Tests", "Berserker Plane of Sky Tests",
                "Class Race Quest List", "Cleric Epic Quest",
                "Cleric Plane of Sky Tests", "Druid Epic Quest", "Druid Plane of Sky Tests",
                "Enchanter Epic Quest", "Enchanter Plane of Sky Tests", "Magician Epic Quest",
                "Magician Plane of Sky Tests", "Monk Epic Quest", "Monk Plane of Sky Tests",
                "Necromancer Epic Quest", "Necromancer Plane of Sky Tests", "Paladin Epic Quest",
                "Paladin Plane of Sky Tests", "Ranger Epic Quest", "Ranger Plane of Sky Tests",
                "Rogue Epic Quest", "Rogue Plane of Sky Tests", "Shadow Knight Epic Quest",
                "Shadow Knight Plane of Sky Tests", "Shaman Epic Quest",
                "Shaman Plane of Sky Tests",
                "Warrior Epic Quest", "Warrior Plane of Sky Tests", "Wizard Epic Quest",
                "Wizard Plane of Sky Tests",
            }.OrderBy(n => n, StringComparer.Ordinal),
            turnedAway);

        Assert.Equal(Curated.Guides.Count + Harvested.Guides.Count - turnedAway.Count,
            Shipped.Guides.Count);
    }

    // ---- One fixture page per shape ----------------------------------------------------

    /// <summary>
    /// A <c>== Checklist ==</c> page: four <c>{{CheckboxList}}</c> bullets, one stage, then
    /// the skeleton. The intro sentence above the bullets ("Obtain the following items and
    /// return them to Vilissia:") is NOT a step and is not carried — bullets, whole-line
    /// bolds and <c>You say</c> lines are the three shapes, and a preamble is none of them.
    /// </summary>
    [Fact]
    public void AChecklistPageBecomesOneStageOfItsBullets()
    {
        var guide = ForQuest("Acumen Mask Quest");

        Assert.Equal(["Checklist", "Turn-in pieces"], guide.Stages.Select(s => s.Name));
        var prose = guide.Stages[0].Objectives;
        Assert.Equal(4, prose.Count);
        Assert.All(prose, o => Assert.Equal(GuideAuthoring.Transcribed, o.Authoring));
        Assert.Equal("Glowing Mask from a skeleton monk or A Froglok Scryer in Upper Guk",
            prose[0].What);
        Assert.DoesNotContain(prose, o => o.What.Contains("Obtain the following",
            StringComparison.Ordinal));

        // Four items in the catalog, so four Collect rows and one hand-in.
        Assert.Equal(5, guide.Stages[1].Objectives.Count);
    }

    /// <summary>A SUBSECTIONED page: the lead text before the first <c>===</c> is its own
    /// stage, then one per heading, in document order and with the heading's own words.</summary>
    [Fact]
    public void ASubsectionedPageBecomesOneStagePerHeading()
    {
        var guide = ForQuest("Scaled Mystic Armor Quests");

        Assert.Equal(
            ["Walkthrough", "Greaves", "Gauntlets", "Cloak", "Boots", "Bracers", "Helm",
             "Vambraces", "Turn-in pieces"],
            guide.Stages.Select(s => s.Name));
        // Eight armour pieces, four bullets each.
        Assert.All(guide.Stages.Take(8), s => Assert.Equal(4, s.Objectives.Count));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], guide.Stages.Select(s => s.Order));
    }

    /// <summary>
    /// A page whose walkthrough has no bullets at all: two <c>You say</c> lines and one
    /// whole-line bold become three rows, and everything between them — the italic location
    /// note, three NPC speeches, the faction block, the closing prose — becomes nothing.
    /// </summary>
    [Fact]
    public void ABoldAndDialoguePageCarriesOnlyThePlayersOwnLines()
    {
        var guide = ForQuest("A Job for Nanrum");

        Assert.Equal(["Walkthrough", "Turn-in pieces"], guide.Stages.Select(s => s.Name));
        Assert.Equal(
            ["You say, 'Hail, Basher Nanrum'", "You say, 'What job?'",
             "Hand in three Fire Beetle Eyes."],
            guide.Stages[0].Objectives.Select(o => o.What));

        // Not one of Basher Nanrum's three speeches, and not the faction adjustments.
        Assert.DoesNotContain(guide.AllObjectives, o =>
            o.What.Contains("Nanrum says", StringComparison.Ordinal)
            || o.What.Contains("faction standing", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A page whose walkthrough is NPC speech and nothing else yields no prose rows,
    /// so the guide is the skeleton alone — here just the opening step, because the quest has
    /// no turn-in items either.</summary>
    [Fact]
    public void ADialogueOnlyPageBecomesTheSkeletonAlone()
    {
        var guide = ForQuest("Blood Ink");

        Assert.Equal(["Start the quest"], guide.Stages.Select(s => s.Name));
        var speak = Assert.Single(guide.Stages[0].Objectives);
        Assert.Equal("TalkToNpc", speak.ObjectiveType);
        Assert.Equal("Speak to War Historian Kobl in Cabilis.", speak.What);
        Assert.Equal("War Historian Kobl", speak.Who);
        Assert.Equal("Cabilis", speak.Where);
    }

    /// <summary>
    /// A page with NEITHER section: the skeleton alone, and the hand-in SHRINKS rather than
    /// inventing. "A sealed letter" names no quest giver on eqlwiki, so the row says "Hand in
    /// the pieces." and is a stub that says why — never "Hand the pieces to ." and never a
    /// giver borrowed from somewhere else.
    /// </summary>
    [Fact]
    public void APageWithNoWalkthroughShrinksTheHandInRatherThanInventingAGiver()
    {
        var guide = ForQuest("A sealed letter");

        Assert.Equal(["Turn-in pieces"], guide.Stages.Select(s => s.Name));
        var handIn = guide.AllObjectives.Single(o => o.ObjectiveType == "TurnIn");
        Assert.Equal("Hand in the pieces.", handIn.What);
        Assert.Equal(GuideAuthoring.Stub, handIn.Authoring);
        Assert.Empty(handIn.Who);
        Assert.NotEmpty(handIn.StubNote);
    }

    // ---- Where a skeleton row's tick lives ----------------------------------------------

    /// <summary>
    /// A Collect row routes to the QUEST LEDGER's owned count and the hand-in to the quest's
    /// completion record — the two stores the General tab has always drawn — rather than to a
    /// tick of the guide's own.
    /// </summary>
    [Fact]
    public void TheSkeletonRowsRouteToTheLedgerAndTheCompletionRecord()
    {
        var guide = ForQuest("The Falchion");
        var quest = Quests.Quests.Single(q => q.Name == "The Falchion");
        var stores = new GuideStores([], [], quest);

        var pieces = guide.Stages.Single(s => s.Name == "Turn-in pieces");
        foreach (var collect in pieces.Objectives.Where(o => o.ObjectiveType == "Collect"))
        {
            Assert.Equal(GuideProgressHome.LedgerItem,
                GuideProgressRouter.HomeFor(collect, stores, out var backing));
            Assert.Equal(collect.ItemNames[0], backing.QuestItem!.Name);
        }

        var handIn = pieces.Objectives.Single(o => o.ObjectiveType == "TurnIn");
        Assert.Equal(GuideProgressHome.QuestCompletion,
            GuideProgressRouter.HomeFor(handIn, stores, out _));

        // And the transcribed prose rows keep their own tick — nothing else has an opinion
        // about "have I read this line".
        foreach (var prose in guide.Stages[0].Objectives)
            Assert.Equal(GuideProgressHome.GuideLedger,
                GuideProgressRouter.HomeFor(prose, stores, out _));
    }

    /// <summary>The Collect row lights when the BAGS reach the need, and it cannot be ticked
    /// by hand: the count is the answer, and a click that could disagree with it would be the
    /// guide and the quest card saying different things about one fact (trap 4).</summary>
    [Fact]
    public void ACollectRowFollowsTheBagsAndRefusesAManualTick()
    {
        var guide = ForQuest("The Falchion");
        var quest = Quests.Quests.Single(q => q.Name == "The Falchion");
        var stores = new GuideStores([], [], quest);
        var collect = guide.AllObjectives.First(o => o.ObjectiveType == "Collect");
        var itemName = collect.ItemNames[0];

        var settings = new AppSettings();
        var ledger = new QuestLedgerStore(Path.Combine(Path.GetTempPath(),
            $"eqb-harvest-{Guid.NewGuid():N}.json")) { TrackFilter = _ => true };

        Assert.False(GuideProgressRouter.IsDone(settings, ledger, "Dranak", guide.Id, collect, stores));

        // A click writes nothing...
        GuideProgressRouter.SetDone(settings, ledger, "Dranak", guide.Id, collect, stores, true);
        Assert.False(GuideProgressRouter.IsDone(settings, ledger, "Dranak", guide.Id, collect, stores));
        Assert.DoesNotContain(collect.Id,
            ledger.GuideProgressFor("Dranak", guide.Id).DoneObjectiveIds);

        // ...and the loot does.
        ledger.RecordLoot("Dranak", itemName, 1, DateTime.UtcNow);
        Assert.True(GuideProgressRouter.IsDone(settings, ledger, "Dranak", guide.Id, collect, stores));

        // Skipping still works: "I am not doing this" is a different fact from "I have it".
        GuideProgressRouter.SetSkipped(ledger, "Dranak", guide.Id, collect, true);
        Assert.True(GuideProgressRouter.IsSkipped(ledger, "Dranak", guide.Id, collect));
    }

    /// <summary>Ticking the hand-in writes the same completion record the General tab's own
    /// tick writes — <c>SetCompleted</c>, the catch-up marking that consumes no items.</summary>
    [Fact]
    public void TheHandInWritesTheQuestsCompletionRecord()
    {
        var guide = ForQuest("The Falchion");
        var quest = Quests.Quests.Single(q => q.Name == "The Falchion");
        var stores = new GuideStores([], [], quest);
        var handIn = guide.AllObjectives.Single(o => o.ObjectiveType == "TurnIn");

        var settings = new AppSettings();
        var ledger = new QuestLedgerStore(Path.Combine(Path.GetTempPath(),
            $"eqb-harvest-{Guid.NewGuid():N}.json"));

        GuideProgressRouter.SetDone(settings, ledger, "Dranak", guide.Id, handIn, stores, true);
        Assert.Equal(1, ledger.CompletedFor("Dranak")["The Falchion"]);
        Assert.True(GuideProgressRouter.IsDone(settings, ledger, "Dranak", guide.Id, handIn, stores));

        // The pieces were NOT consumed — that is what the General tab's hand-in button does,
        // and two callers of a destructive write is trap 47's shape.
        Assert.Empty(ledger.For("Dranak"));

        GuideProgressRouter.SetDone(settings, ledger, "Dranak", guide.Id, handIn, stores, false);
        Assert.False(GuideProgressRouter.IsDone(settings, ledger, "Dranak", guide.Id, handIn, stores));
    }

    /// <summary>Without the quest in hand the same rows fall to the guide ledger, which is
    /// what every existing Sky and Epic caller does. A new home must not change an old
    /// caller's answer.</summary>
    [Fact]
    public void WithNoQuestInHandTheSkeletonRowsFallToTheGuideLedger()
    {
        var guide = ForQuest("The Falchion");
        foreach (var o in guide.Stages.Single(s => s.Name == "Turn-in pieces").Objectives)
            Assert.Equal(GuideProgressHome.GuideLedger,
                GuideProgressRouter.HomeFor(o, GuideStores.None, out _));
    }

    // ---- The report ---------------------------------------------------------------------

    /// <summary>
    /// The weekly PR's reviewable artefact exists and AGREES with the file beside it.
    ///
    /// <para>A report regenerated from a different run than the data is worse than none: a
    /// reviewer reads the counts and believes them. So this checks the two numbers a reviewer
    /// acts on — guides and objectives — against the committed catalog, which is the only
    /// thing that makes the rest of the report worth reading.</para>
    /// </summary>
    [Fact]
    public void TheReportIsThereAndItsCountsMatchTheCommittedFile()
    {
        var path = Path.Combine(Root, "scripts", "harvests", "eqlwiki", "guides-report.md");
        Assert.True(File.Exists(path), "guides-transform.py writes guides-report.md; it is not there.");
        var report = File.ReadAllText(path);

        Assert.Contains($"- Guides written: {Harvested.Guides.Count}", report, StringComparison.Ordinal);
        Assert.Contains($"- Objectives: {Harvested.Guides.Sum(g => g.AllObjectives.Count())}",
            report, StringComparison.Ordinal);
        Assert.Contains("## Per shape", report, StringComparison.Ordinal);
        Assert.Contains("## Skeleton-only guides", report, StringComparison.Ordinal);
        Assert.Contains("## Quests with no guide", report, StringComparison.Ordinal);

        // The skeleton-only set is the number a reviewer watches week to week: it is the
        // quests we can say nothing about beyond their item list.
        var skeletonOnly = Harvested.Guides.Count(g =>
            g.Stages.All(s => s.Id is "turn-in-pieces" or "start"));
        Assert.Contains($"## Skeleton-only guides ({skeletonOnly})", report, StringComparison.Ordinal);
    }

    // ---- Fixture -------------------------------------------------------------------------

    private static Guide OneGuide(string id, string questName, GuideType type) => new()
    {
        Id = id,
        Name = questName,
        GuideType = type,
        QuestName = questName,
        ApplicableClasses = ["Warrior"],
        ZoneNames = ["Somewhere"],
        Sources = [new GuideSource
        {
            Url = "https://eqlwiki.com/X", Title = "X", RetrievedAt = "2026-09-11",
        }],
        Stages = [new GuideStage { Id = "s", Name = "S", Order = 1, Objectives = [] }],
    };

    /// <summary>Quest names as the FILE holds them, before <c>SkyTestSplit</c> and
    /// <c>CatalogHygiene</c> — the transformer's universe, which is wider than the app's.</summary>
    private static List<string> ReadRawQuestNames()
    {
        var path = Path.Combine(Root, "src", "EQBuddy.Core", "Data", "QuestCatalog.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return [.. doc.RootElement.GetProperty("quests").EnumerateArray()
            .Select(q => q.GetProperty("name").GetString() ?? "")];
    }
}
