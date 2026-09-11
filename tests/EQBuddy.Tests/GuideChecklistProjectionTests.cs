using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A guide, rendered as the checklist rows every surface already draws (Fable plan §2 D1).
///
/// <para>The claim under test is the CUTOVER: a reward a guide claims becomes the
/// walkthrough, and a reward no guide claims is handed back untouched — Founder lock 5, and
/// the reason a class nobody has authored still works exactly as it did in 1.x. Reference
/// equality is asserted for the untouched case, because "looks the same" and "is the same
/// object" are different promises and only the second one cannot drift.</para>
/// </summary>
public sealed class GuideChecklistProjectionTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string ClassName = "Warrior";
    private const string Reward = "Runed Wind Amulet";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"guide-proj-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static string RewardKey => QuestChecklistLayout.RewardKey(ClassName, Reward);

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "sky-198", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Stone Amulet",
                Source = "Isle 4: Keeper of Souls",
            },
            new SkyQuestChecklistItem
            {
                Id = "sky-199", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Wind Rune Azia",
                Source = "Trash mobs",
            },
            // A second class, deliberately: nothing about the Warrior's guide may reach it.
            new SkyQuestChecklistItem
            {
                Id = "sky-007", ClassName = "Bard", Reward = "Mask of Song",
                Npc = "Bardmaster", QuestItem = "Wind Rune Azia",
            },
        ]);
        return s;
    }

    private static IReadOnlyList<QuestChecklistGroup> Groups(AppSettings s) =>
        QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted);

    /// <summary>The shipped Warrior seed's shape, written the way an author writes one.</summary>
    private static GuideCatalog Catalog() => new()
    {
        Guides =
        [
            new Guide
            {
                Id = "pos-warrior-runed-wind-amulet",
                Name = "Runed Wind Amulet - Warrior, Plane of Sky",
                GuideType = GuideType.PlaneOfSkyQuest,
                QuestName = "Warrior Sky Test: Runed Wind Amulet",
                ZoneNames = ["Plane of Sky"],
                ApplicableClasses = [ClassName],
                Stages =
                [
                    new GuideStage
                    {
                        Id = "isle-4", Name = "Isle 4: Keeper of Souls", Order = 1,
                        Objectives =
                        [
                            new GuideObjective
                            {
                                Id = "stone-amulet", Order = 1, ObjectiveType = "Loot",
                                Title = "Loot the Stone Amulet from the Keeper of Souls",
                                ShortInstruction = "Kill the Keeper of Souls and loot the Stone Amulet.",
                                Who = "Keeper of Souls", Where = "Plane of Sky - Isle 4.",
                                What = "Loot Stone Amulet (1).",
                                ItemNames = ["Stone Amulet"],
                                Authoring = GuideAuthoring.Authored,
                            },
                        ],
                    },
                    new GuideStage
                    {
                        Id = "wind-runes", Name = "The wind rune", Order = 2,
                        Objectives =
                        [
                            new GuideObjective
                            {
                                Id = "wind-rune-azia", Order = 1, ObjectiveType = "Farm",
                                Title = "Collect Wind Rune Azia",
                                ShortInstruction = "Loot a Wind Rune Azia from Plane of Sky trash.",
                                ItemNames = ["Wind Rune Azia"],
                                Authoring = GuideAuthoring.Stub,
                                StubNote = "eqlwiki does not say which isle drops it.",
                            },
                        ],
                    },
                    new GuideStage
                    {
                        Id = "turn-in", Name = "Turn in to Torgon Blademaster", Order = 3,
                        Objectives =
                        [
                            new GuideObjective
                            {
                                Id = "turn-in-runed-wind-amulet", Order = 1, ObjectiveType = "TurnIn",
                                Title = "Hand in the Stone Amulet and Wind Rune Azia",
                                ShortInstruction = "Give Torgon Blademaster both pieces.",
                                Who = "Torgon Blademaster", Where = "Plane of Sky.",
                                What = "Hand over both.",
                                PrerequisiteObjectiveIds = ["stone-amulet", "wind-rune-azia"],
                                RewardKey = RewardKey,
                                Authoring = GuideAuthoring.Authored,
                            },
                        ],
                    },
                ],
            },
        ],
    };

    private QuestChecklistGroup Guided(AppSettings settings) =>
        GuideChecklistProjection
            .Apply(Groups(settings), Catalog(), settings, Store(), Dranak)
            .Single(g => g.CompletionKey == RewardKey);

    // ---- the cutover -----------------------------------------------------------------

    [Fact]
    public void AGuidedGroupsRowsAreTheGuidesObjectivesInReadingOrder()
    {
        var group = Guided(Settings());

        Assert.Equal(
            ["Kill the Keeper of Souls and loot the Stone Amulet.",
             "Loot a Wind Rune Azia from Plane of Sky trash.",
             "Give Torgon Blademaster both pieces."],
            group.Rows.Select(r => r.Title));
        Assert.Equal(
            ["Isle 4: Keeper of Souls", "The wind rune", "Turn in to Torgon Blademaster"],
            group.Rows.Select(r => r.IslandHeading));
    }

    [Fact]
    public void AGroupNoGuideClaimsIsHandedBackTheSameObject()
    {
        var settings = Settings();
        var before = Groups(settings);

        var after = GuideChecklistProjection.Apply(before, Catalog(), settings, Store(), Dranak);

        var bard = before.Single(g => g.ClassName == "Bard");
        // The same instance, not merely an equal one: a class nobody has authored is not
        // rebuilt, re-sorted or re-worded on its way through.
        Assert.Same(bard, after.Single(g => g.ClassName == "Bard"));
    }

    [Fact]
    public void AnEmptyCatalogChangesNothingAtAll()
    {
        var settings = Settings();
        var before = Groups(settings);

        var after = GuideChecklistProjection.Apply(
            before, new GuideCatalog(), settings, Store(), Dranak);

        Assert.All(after, g => Assert.Same(before[after.ToList().IndexOf(g)], g));
    }

    /// <summary>Prove-fail: a catalog whose turn-in key matches no group attaches nothing.
    /// Without this the suite could pass on a projection that guided every group it saw.</summary>
    [Fact]
    public void ACatalogWhoseRewardKeyMatchesNoGroupAttachesNothing()
    {
        var settings = Settings();
        var catalog = Catalog();
        catalog.Guides[0].Stages[2].Objectives[0].RewardKey =
            QuestChecklistLayout.RewardKey("Warrior", "A Reward Nobody Has");

        var after = GuideChecklistProjection.Apply(
            Groups(settings), catalog, settings, Store(), Dranak);

        Assert.All(after, g => Assert.Equal("", g.GuideId));
        Assert.All(after, g => Assert.Equal("", g.GuideCaption));
    }

    // ---- ids, stubs, state -----------------------------------------------------------

    [Fact]
    public void GuideRowIdsCanNeverCollideWithASkyCatalogId()
    {
        var group = Guided(Settings());

        Assert.All(group.Rows, r => Assert.StartsWith("guide:", r.Id, StringComparison.Ordinal));
        Assert.All(group.Rows, r => Assert.DoesNotContain("sky-", r.Id, StringComparison.Ordinal));
        // DistinctBy(Id) is what Done/Total count on; a collision would miscount silently.
        Assert.Equal(group.Rows.Count, group.Rows.Select(r => r.Id).Distinct().Count());
        Assert.All(group.Rows, r => Assert.Equal("pos-warrior-runed-wind-amulet", r.GuideRowKey));
    }

    [Fact]
    public void AStubRowCarriesItsNoteAndAnAuthoredRowCarriesNone()
    {
        var group = Guided(Settings());

        var stub = group.Rows.Single(r => r.Id.EndsWith("wind-rune-azia", StringComparison.Ordinal));
        Assert.Equal("eqlwiki does not say which isle drops it.", stub.StubNote);
        Assert.All(group.Rows.Where(r => r != stub), r => Assert.Equal("", r.StubNote));
    }

    /// <summary>The caption says how hollow the data is and NOTHING the heading already
    /// says. Progress left it when folding turned "Guide · 0 of 3" into a second copy of the
    /// heading's own "0/3" with nothing between them (Bevel SIGNED; Fable #491 defect 3).</summary>
    [Fact]
    public void TheCaptionCountsTheStepsAndSaysHowManyAreStubs()
    {
        var group = Guided(Settings());

        Assert.Equal("Guide · 1 stub", group.GuideCaption);
        // The count the caption gave up is still ON the group — it moved home, it did not go.
        Assert.Equal(0, group.Done);
        Assert.Equal(3, group.Total);
    }

    /// <summary>
    /// A tick moves the HEADING's count, and leaves the caption exactly where it was.
    ///
    /// <para>That split is the fix, stated as behaviour: progress belongs to the heading, and
    /// the caption is only there to say what the heading has no room for. Both still read one
    /// <c>IsDone</c> — this asserts the ROW moved too, so "the caption did not change" can
    /// never pass because nothing happened at all.</para></summary>
    [Fact]
    public void ATickMovesTheHeadingsCountAndLeavesTheCaptionAlone()
    {
        var settings = Settings();
        var before = Guided(settings);
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        var group = Guided(settings);

        Assert.Equal(0, before.Done);
        Assert.Equal(1, group.Done);
        Assert.Equal(before.GuideCaption, group.GuideCaption);
        // And the ROW agrees — heading and ticks are two readers of one IsDone.
        Assert.True(group.Rows.Single(r => r.Id.EndsWith("stone-amulet", StringComparison.Ordinal)).Acquired);
    }

    [Fact]
    public void OnlyTheTurnInRowIsMarkedAsTheHandIn()
    {
        var group = Guided(Settings());

        Assert.Equal(
            ["guide:pos-warrior-runed-wind-amulet/turn-in-runed-wind-amulet"],
            group.Rows.Where(r => r.IsTurnIn).Select(r => r.Id));
    }

    /// <summary>The regression the <c>IsTurnIn</c> flag exists to prevent: with both pieces
    /// in hand the reward is READY — the turn-in row is the thing being gated, never one of
    /// the things gating it. Counting it among the pieces would have made "ready"
    /// unreachable, silently dropping the reward out of the cross-class Ready band and
    /// taking the "Mark turned in" button off its own heading.</summary>
    [Fact]
    public void AGuidedRewardWithEveryPieceInHandStillReadsReady()
    {
        var settings = Settings();
        foreach (var item in settings.SkyQuestChecklist.Where(i => i.ClassName == ClassName))
            item.Acquired = true;

        var group = Guided(settings);

        Assert.True(group.ReadyToTurnIn);
        Assert.Equal(QuestChecklistLayout.StateReady, group.State);
        Assert.Equal("ready", group.Note);
    }

    [Fact]
    public void ATurnedInGuidedRewardReadsDoneAndItsTurnInRowIsTicked()
    {
        var settings = Settings();
        settings.SkyQuestCompleted.Add(RewardKey);

        var group = Guided(settings);

        Assert.Equal(QuestChecklistLayout.StateDone, group.State);
        Assert.True(group.Rows.Single(r => r.IsTurnIn).Acquired);
    }

    // ---- what the rows SAY -----------------------------------------------------------

    [Fact]
    public void AnOrdinaryStepsDetailIsWhoThenWhere()
    {
        var row = Guided(Settings()).Rows
            .Single(r => r.Id.EndsWith("stone-amulet", StringComparison.Ordinal));

        Assert.Equal("Keeper of Souls · Plane of Sky - Isle 4.", row.Detail);
    }

    [Fact]
    public void ALockedTurnInNamesTheStepsThatUnlockIt()
    {
        var row = Guided(Settings()).Rows.Single(r => r.IsTurnIn);

        Assert.Equal(
            "after: Loot the Stone Amulet from the Keeper of Souls, Collect Wind Rune Azia",
            row.Detail);
    }

    [Fact]
    public void AnUnlockedTurnInGoesBackToSayingWhoAndWhere()
    {
        var settings = Settings();
        foreach (var item in settings.SkyQuestChecklist.Where(i => i.ClassName == ClassName))
            item.Acquired = true;

        var row = Guided(settings).Rows.Single(r => r.IsTurnIn);

        Assert.Equal("Torgon Blademaster · Plane of Sky.", row.Detail);
    }

    [Fact]
    public void AnItemBackedRowInheritsTheGuessedMarkFromTheBoxItReads()
    {
        var settings = Settings();
        var box = settings.SkyQuestChecklist.Single(i => i.Id == "sky-198");
        box.Acquired = true;
        box.AcquiredUnassigned = true;

        var row = Guided(settings).Rows
            .Single(r => r.Id.EndsWith("stone-amulet", StringComparison.Ordinal));

        Assert.True(row.Unassigned);
    }

    // ---- resolving back --------------------------------------------------------------

    [Fact]
    public void ARowIdResolvesBackToItsOwnObjective()
    {
        var catalog = Catalog();
        var id = GuideChecklistProjection.RowId("pos-warrior-runed-wind-amulet", "wind-rune-azia");

        var resolved = GuideChecklistProjection.Resolve(catalog, id);

        Assert.Equal("wind-rune-azia", resolved!.Value.Objective.Id);
        Assert.Equal("pos-warrior-runed-wind-amulet", resolved.Value.Guide.Id);
    }

    [Fact]
    public void AClassicRowIdResolvesToNothing()
    {
        Assert.Null(GuideChecklistProjection.Resolve(Catalog(), "sky-198"));
        Assert.False(GuideChecklistProjection.IsGuideRowId("sky-198"));
    }

    [Fact]
    public void TheShippedCatalogsSkyGuidesAllClaimARewardTheChecklistKnows()
    {
        // RewardKeyOf is what the phone's tick path uses to find a guide's own rows; a guide
        // it cannot answer for would silently route every step to the ledger.
        var sky = GuideCatalog.Default.Guides
            .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest).ToList();
        Assert.Equal(95, sky.Count);
        foreach (var guide in sky)
            Assert.Contains(GuideChecklistProjection.RewardKeyOf(guide),
                GuideCatalog.SkyRewardKeys);
    }
}
