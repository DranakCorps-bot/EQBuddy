using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// WHILE YOU'RE HERE (DRA-42 D1, requirements §18) — the zone join, the place rules, the three
/// groups and the empty states, asserted over the SHIPPED catalogs.
///
/// <para>Real data on purpose, the <see cref="EpicAndSkyTrackingTests"/> reason: the join
/// between a step and a zone IS the item pages' drop zones and the quests' start zones, and a
/// hand-made fixture would prove a join the product does not have. The committed negatives are
/// real catalog strings for the same reason.</para>
/// </summary>
public sealed class WhileHereTests : IDisposable
{
    private const string Character = "dranak_legends";

    /// <summary>A Paladin quest whose Nightfall Giant's Head drops in West Commonlands ONLY —
    /// the exhibit for "Commonlands is not West Commonlands".</summary>
    private const string ArmorOfRo = "Armor of Ro Quests";
    private const string NightfallStep = "Collect Nightfall Giant's Head";
    private const string SandOfRoStep = "Collect Sand of Ro";

    /// <summary>A Plane of Sky reward, and its first isle's authored loot step.</summary>
    private const string BardSky = "Bard Sky Test: Amulet of the Fae";
    private const string WovenHairId = "amulet-of-woven-hair";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"while-here-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private static readonly QuestCatalog Quests = QuestCatalog.LoadEmbedded();

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        return s;
    }

    private static WhileHereAnswer Ask(string? zone, AppSettings s, QuestLedgerStore ledger,
        IReadOnlyCollection<string>? classes = null) =>
        WhileHere.For(new WhileHereInputs(zone, s, ledger, Character, Quests,
            GuideCatalog.Default, ItemCatalog.Default, classes ?? [], ""));

    // ── The zone join: exact, then IdentityKey, and nothing looser ────────────────────────

    [Fact]
    public void TheLogsSpellingAndTheCatalogsAreOneZone()
    {
        Assert.True(WhileHerePlaces.SameZone("Plane of Sky", "Plane of Sky"));
        Assert.True(WhileHerePlaces.SameZone("The Plane of Sky", "plane of sky"));
        Assert.True(WhileHerePlaces.SameZone("West Commonlands", "west commonlands"));
    }

    [Fact]
    public void CommonlandsIsNotWestCommonlandsThoughTheQuestSearchRuleSaysItTouches()
    {
        Assert.False(WhileHerePlaces.SameZone("Commonlands", "West Commonlands"));
        Assert.False(WhileHerePlaces.SameZone("West Commonlands", "Commonlands"));
        // The looser rule this deliberately does not use: the quest search's containment
        // backstop answers YES for the same pair. That is the bug the exact rule prevents.
        Assert.True(new QuestEntry { Zones = ["West Commonlands"] }.TouchesZone("Commonlands"));
    }

    [Fact]
    public void ANonPlaceIsRefusedAsADropZone()
    {
        var catalog = new ItemCatalog(
        [
            new ItemCatalog.Record
            {
                Name = "Test Pelt",
                DropZones = ["Various Zones", "}}", "Crushbone", "crushbone", "Unknown"],
                DropMobs = new() { ["Crushbone"] = ["orc pawn"] },
            },
        ]);
        var places = WhileHerePlaces.DropPlaces(catalog, "Test Pelt");
        var only = Assert.Single(places);
        Assert.Equal("Crushbone", only.Zone);
        Assert.Equal(["orc pawn"], only.Who);
    }

    [Fact]
    public void ANonPlaceZoneAnswersNotAPlaceRatherThanNothingHere()
    {
        var answer = Ask("Various Zones", Settings(), Store());
        Assert.Equal(WhileHereState.NotAPlace, answer.State);
        Assert.NotNull(WhileHerePresentation.Empty(answer));
    }

    [Fact]
    public void NoZoneYetIsItsOwnStateNotNothingHere()
    {
        var answer = Ask("", Settings(), Store());
        Assert.Equal(WhileHereState.ZoneUnknown, answer.State);
        Assert.Contains("You have entered", WhileHerePresentation.Empty(answer));
    }

    // ── Required: what the player tracks ──────────────────────────────────────────────────

    [Fact]
    public void ATrackedQuestsStepIsRequiredInTheZoneItsItemDropsIn()
    {
        var ledger = Store();
        ledger.SetTracked(Character, ArmorOfRo, true);

        var west = Ask("West Commonlands", Settings(), ledger);
        Assert.Equal(WhileHereState.Answered, west.State);
        Assert.Contains(west.Required, s => s.Quest == ArmorOfRo && s.Step == NightfallStep);
        Assert.Contains(west.Required, s => s.Step == SandOfRoStep);

        var common = Ask("Commonlands", Settings(), ledger);
        Assert.DoesNotContain(common.Required, s => s.Step == NightfallStep);
    }

    [Fact]
    public void AStepAtAPersonIsPlacedAtTheQuestsStartZoneAndNamesTheGiver()
    {
        var ledger = Store();
        ledger.SetTracked(Character, ArmorOfRo, true);
        var start = Quests.Quests.Single(q => q.Name == ArmorOfRo).StartZone;

        var answer = Ask(start, Settings(), ledger);
        var speak = Assert.Single(answer.Required, s => s.StepId == "start-speak");
        Assert.NotEmpty(speak.Who);
    }

    [Fact]
    public void AHandInWhosePiecesAreMissingIsNotActionableHere()
    {
        var ledger = Store();
        ledger.SetTracked(Character, ArmorOfRo, true);
        var start = Quests.Quests.Single(q => q.Name == ArmorOfRo).StartZone;

        var answer = Ask(start, Settings(), ledger);
        Assert.DoesNotContain(answer.Required, s => s.StepId == "turn-in-pieces-hand-in");
    }

    [Fact]
    public void ADoneStepAndASkippedStepAreNeverHere()
    {
        var ledger = Store();
        ledger.SetTracked(Character, ArmorOfRo, true);
        ledger.SetManual(Character, "Nightfall Giant's Head", 1);
        var guide = GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, ArmorOfRo)!;
        var sand = guide.AllObjectives.Single(o => o.Title == SandOfRoStep);
        GuideProgressRouter.SetSkipped(ledger, Character, guide.Id, sand, true);

        var answer = Ask("West Commonlands", Settings(), ledger);
        Assert.DoesNotContain(answer.Required, s => s.Step == NightfallStep);
        Assert.DoesNotContain(answer.Required, s => s.Step == SandOfRoStep);
    }

    [Fact]
    public void ASkyPieceIsRequiredOnThePlaneOfSkyWithItsDropperNotTheGiver()
    {
        var ledger = Store();
        ledger.SetTracked(Character, BardSky, true);

        // The LOG's spelling, with its article — the IdentityKey half of the join.
        var answer = Ask("The Plane of Sky", Settings(), ledger);
        var piece = Assert.Single(answer.Required, s => s.StepId == WovenHairId);
        Assert.Equal(["Bazzt Zzzt"], piece.Who);
    }

    [Fact]
    public void AnAcquiredSkyPieceLeavesTheBlockThroughTheChecklistsOwnBox()
    {
        var s = Settings();
        var ledger = Store();
        ledger.SetTracked(Character, BardSky, true);
        s.SkyQuestChecklist.Single(i => i.QuestItem == "Amulet of Woven Hair"
                                        && i.ClassName == "Bard").Acquired = true;

        var answer = Ask("Plane of Sky", s, ledger);
        Assert.DoesNotContain(answer.Required, x => x.StepId == WovenHairId);
    }

    [Fact]
    public void ATrackedEpicSectionIsCountedAsUnplacedAndNeverGuessedIntoAZone()
    {
        var ledger = Store();
        var epic = GuideCatalog.Default.Guides.First(g => g.GuideType == GuideType.EpicQuest);
        var stage = epic.Stages.OrderBy(x => x.Order).First();
        ledger.SetSectionTracked(Character, EpicSection.Key(epic.Id, stage.Id), true);

        // The guide's own ZoneNames is the tempting wrong answer: one zone for a walk across
        // a dozen of them.
        var answer = Ask(epic.ZoneNames[0], Settings(), ledger);
        Assert.True(answer.UnplacedTracked > 0);
        Assert.DoesNotContain(answer.Required, x => stage.Objectives.Any(o => o.Id == x.StepId));
        Assert.Contains("no place EQBuddy can read", WhileHerePresentation.Unplaced(answer.UnplacedTracked));
    }

    // ── Relevant rewards and Optional ─────────────────────────────────────────────────────

    [Fact]
    public void AStartedUntrackedQuestIsARelevantRewardAndItsHeldPieceIsNot()
    {
        var ledger = Store();
        ledger.SetManual(Character, "Nightfall Giant's Head", 1);

        var answer = Ask("West Commonlands", Settings(), ledger);
        Assert.DoesNotContain(answer.Required, s => s.Quest == ArmorOfRo);
        Assert.Contains(answer.Relevant, s => s.Quest == ArmorOfRo && s.Step == SandOfRoStep);
        Assert.DoesNotContain(answer.Relevant, s => s.Step == NightfallStep);
        Assert.DoesNotContain(ArmorOfRo, answer.Optional);
    }

    [Fact]
    public void AnUntouchedQuestIsOptionalByNameAndTheClassLensNarrowsIt()
    {
        var anyClass = Ask("West Commonlands", Settings(), Store());
        Assert.Contains(ArmorOfRo, anyClass.Optional);

        var paladin = Ask("West Commonlands", Settings(), Store(), ["Paladin"]);
        Assert.Contains(ArmorOfRo, paladin.Optional);

        var warrior = Ask("West Commonlands", Settings(), Store(), ["Warrior"]);
        Assert.DoesNotContain(ArmorOfRo, warrior.Optional);
    }

    [Fact]
    public void AHiddenOrCompletedQuestIsNotOffered()
    {
        var hidden = Store();
        hidden.SetHidden(Character, ArmorOfRo, true);
        Assert.DoesNotContain(ArmorOfRo, Ask("West Commonlands", Settings(), hidden).Optional);

        var done = Store();
        done.SetCompleted(Character, ArmorOfRo, true);
        Assert.DoesNotContain(ArmorOfRo, Ask("West Commonlands", Settings(), done).Optional);
    }

    [Fact]
    public void AZoneNoStepIsPlacedInAnswersNothingOpenHereNamingTheCatalog()
    {
        var answer = WhileHere.For(new WhileHereInputs("Crushbone", Settings(), Store(), Character,
            new QuestCatalog(), GuideCatalog.Default, ItemCatalog.Default, [], ""));
        Assert.Equal(WhileHereState.NothingOpenHere, answer.State);
        Assert.Contains("EQBuddy's catalogs", WhileHerePresentation.Empty(answer));
    }

    // ── Words ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryCapSaysWhatItHeldBack()
    {
        Assert.Contains("3 more", WhileHerePresentation.MoreSteps(3));
        Assert.Contains("1 more step", WhileHerePresentation.MoreSteps(1));
        Assert.Contains("4 more quests", WhileHerePresentation.MoreQuests(4));
        Assert.Equal("a, b, c and 2 more on its page",
            WhileHerePresentation.WhoLine(["a", "b", "c", "d", "e"]));
        Assert.Equal("a, b", WhileHerePresentation.WhoLine(["a", "b"]));
    }

    [Fact]
    public void NoSentenceCallsACampSafeEasyOrSurvivable()
    {
        var sentences = new List<string>
        {
            WhileHerePresentation.HeadingNoZone, WhileHerePresentation.SourceNote,
            WhileHerePresentation.Heading("Crushbone"),
            WhileHerePresentation.MoreSteps(2), WhileHerePresentation.MoreQuests(2),
            WhileHerePresentation.Unplaced(2), WhileHerePresentation.Unplaced(1),
        };
        sentences.AddRange(Enum.GetValues<WhileHereGroup>().Select(WhileHerePresentation.GroupLabel));
        foreach (var state in Enum.GetValues<WhileHereState>())
            if (WhileHerePresentation.Empty(WhileHereAnswer.None with { State = state, Zone = "Crushbone" }) is { } e)
                sentences.Add(e);
        foreach (var s in sentences)
            foreach (var banned in new[] { "safe", "easy", "surviv" })
                Assert.DoesNotContain(banned, s, StringComparison.OrdinalIgnoreCase);
    }
}
