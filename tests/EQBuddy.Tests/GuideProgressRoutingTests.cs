using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// One-writer routing for guide objectives (Fable plan §4, P1b).
///
/// A guide row that carries a <c>RewardKey</c> is not a new fact — it is the Plane of Sky
/// turn-in the classic checklist, the phone, the achievements import and the inventory-driven
/// auto-complete have all shared since 1.x. Keeping a second tick for it in the guide ledger
/// would be trap 4 with a visible price: turn the reward in on the phone, open the guide on
/// the desktop, and the guide still asks you to do it. So the reward rows read FROM and write
/// THROUGH <see cref="SkyCompleteToggle"/>, and the guide ledger never holds their done id.
///
/// Both halves are asserted, because a guard that only forbids cannot see a missing thing
/// (trap 34): the ledger must not gain the reward tick, AND the router must be the code that
/// calls the store's setter.
/// </summary>
public sealed class GuideProgressRoutingTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string GuideId = "ench-pos";
    private const string Reward = "Ivory Mask";
    private const string ClassName = "Enchanter";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static string RewardKey => QuestChecklistLayout.RewardKey(ClassName, Reward);

    /// <summary>A Sky reward with two ingredient rows, the same shape the checklist builds.</summary>
    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "a", ClassName = ClassName, Reward = Reward,
                Npc = "Enchanter Jolas", QuestItem = "Silken Mask",
            },
            new SkyQuestChecklistItem
            {
                Id = "b", ClassName = ClassName, Reward = Reward,
                Npc = "Enchanter Jolas", QuestItem = "Wind Rune Beza",
            },
        ]);
        return s;
    }

    private static GuideObjective Step(string id) => new()
    {
        Id = id, Order = 1, ObjectiveType = "Kill", Title = id,
        ShortInstruction = "do the thing",
    };

    private static GuideObjective TurnIn(string id) => new()
    {
        Id = id, Order = 2, ObjectiveType = "TurnIn", Title = id,
        ShortInstruction = "hand it over", RewardKey = RewardKey,
    };

    // ---- the routing decision itself ------------------------------------------------

    [Fact]
    public void ARewardObjectiveIsOwnedByTheSkyStoreAndEverythingElseByTheLedger()
    {
        Assert.Equal(GuideProgressHome.SkyTurnIn, GuideProgressRouter.HomeFor(TurnIn("t")));
        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(Step("s")));
    }

    // ---- the reward path ------------------------------------------------------------

    /// <summary>The point of the whole class: ticking a reward objective writes the Sky
    /// store and leaves NOTHING in the guide ledger. The negative is the assertion — a
    /// passing "it is done" would be green with the duplicate tick happily written.</summary>
    [Fact]
    public void TickingARewardObjectiveWritesTheSkyStoreAndNotTheGuideLedger()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);

        Assert.True(SkyCompleteToggle.IsTurnedIn(settings, RewardKey));
        Assert.True(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
        Assert.DoesNotContain("isle4-turnin",
            ledger.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A reward turned in on ANOTHER screen reads as done inside the guide the same
    /// tick — the whole reason the guide does not keep its own copy.</summary>
    [Fact]
    public void ARewardTurnedInOnTheClassicChecklistIsDoneInsideTheGuide()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");
        Assert.False(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));

        // Exactly the call the classic checklist's own button makes.
        SkyCompleteToggle.MarkTurnedIn(settings, RewardKey,
            SkyCompleteToggle.ItemsFor(settings.SkyQuestChecklist, RewardKey), ledger, Dranak);

        Assert.True(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
    }

    /// <summary>Writing THROUGH the existing path means the turn-in's side effects happen —
    /// the reward's items are acquired and the quest ledger records the completion — because
    /// the guide makes the same call, not a lookalike of it.</summary>
    [Fact]
    public void TheRewardPathKeepsTheTurnInsSideEffects()
    {
        var settings = Settings();
        var ledger = Store();

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, TurnIn("isle4-turnin"), true);

        Assert.All(settings.SkyQuestChecklist, i => Assert.True(i.Acquired));
        Assert.Equal(1, ledger.CompletedFor(Dranak)[SkyTestSplit.QuestName(ClassName, Reward)]);
    }

    /// <summary>Un-ticking a reward reopens it. The classic checklist's asymmetry is kept
    /// verbatim: the item boxes are NOT unticked, because the player knows what they still
    /// hold and correcting one mis-click must not clear six ticks.</summary>
    [Fact]
    public void UntickingARewardObjectiveReopensItAndLeavesTheItemBoxesAlone()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");
        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, false);

        Assert.False(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
        Assert.All(settings.SkyQuestChecklist, i => Assert.True(i.Acquired));
    }

    /// <summary>A turn-in arriving from another screen must not grow the guide's ledger row.
    /// "Which guides has this character touched" stays a true answer.</summary>
    [Fact]
    public void TheRewardPathDoesNotCreateAGuideRowAtAll()
    {
        var settings = Settings();
        var ledger = Store();

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, TurnIn("isle4-turnin"), true);

        Assert.Empty(ledger.GuidesTouchedBy(Dranak));
    }

    // ---- skip: the deliberate asymmetry ----------------------------------------------

    /// <summary>Skip has no home in the Sky store and is a different statement from "I turned
    /// this in", so it lands in the guide ledger even for a reward objective. That is not a
    /// duplicate of the turn-in — it is the one fact the Sky store cannot hold.</summary>
    [Fact]
    public void ARewardObjectiveCanBeSkippedInTheGuideLedgerWithoutTouchingTheSkyStore()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");

        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, objective, true);

        Assert.True(GuideProgressRouter.IsSkipped(ledger, Dranak, GuideId, objective));
        Assert.False(SkyCompleteToggle.IsTurnedIn(settings, RewardKey));
        Assert.False(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
    }

    /// <summary>Turning a skipped reward in clears the strike-out: the row cannot render
    /// struck out and done at once, and the done half lives in the other store, so this is
    /// the one place the router has to reach across.</summary>
    [Fact]
    public void TurningInASkippedRewardClearsTheSkip()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");
        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, objective, true);

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);

        Assert.False(GuideProgressRouter.IsSkipped(ledger, Dranak, GuideId, objective));
        Assert.True(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
    }

    /// <summary>Skipping a reward objective the player already turned in leaves the turn-in
    /// standing: a hand-in that happened is a fact about the past, and a skip is a statement
    /// about the future.</summary>
    [Fact]
    public void SkippingDoesNotUndoATurnInThatAlreadyHappened()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = TurnIn("isle4-turnin");
        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);

        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, objective, true);

        Assert.True(SkyCompleteToggle.IsTurnedIn(settings, RewardKey));
    }

    // ---- the plain path and the counts ------------------------------------------------

    [Fact]
    public void APlainObjectiveRoundTripsThroughTheGuideLedger()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Step("isle1-key");

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);
        Assert.True(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
        Assert.False(SkyCompleteToggle.IsTurnedIn(settings, RewardKey));

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, false);
        Assert.False(GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective));
    }

    /// <summary>The counts read each row through its OWN store, so a reward turned in
    /// elsewhere moves the guide's number without anything copying a tick.</summary>
    [Fact]
    public void CountsReadEachRowThroughItsOwnStore()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = new Guide
        {
            Id = GuideId, Name = "Enchanter — Plane of Sky",
            Stages =
            [
                new GuideStage
                {
                    Id = "isle1", Name = "Isle 1", Order = 1,
                    Objectives = [Step("isle1-key"), Step("isle1-drop"), TurnIn("isle4-turnin")],
                },
            ],
        };
        Assert.Equal(new GuideProgressCounts(0, 0, 3),
            GuideProgressRouter.Counts(settings, ledger, Dranak, guide));

        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, Step("isle1-key"), true);
        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, Step("isle1-drop"), true);
        SkyCompleteToggle.MarkTurnedIn(settings, RewardKey,
            SkyCompleteToggle.ItemsFor(settings.SkyQuestChecklist, RewardKey), ledger, Dranak);

        var counts = GuideProgressRouter.Counts(settings, ledger, Dranak, guide);
        Assert.Equal(new GuideProgressCounts(2, 1, 3), counts);
        Assert.Equal(0, counts.Remaining);
    }

    /// <summary>A done row is never also counted as skipped, so Done + Skipped can never
    /// exceed Total — a progress readout that could show 4 of 3 is a readout nobody trusts
    /// again. The store's setters refuse to produce the contradiction, so it is seeded on
    /// disk: a hand-edited profile, or a file written by a future build with different
    /// rules, is exactly how one would arrive.</summary>
    [Fact]
    public void ARowIsCountedOnceEvenWhenBothFlagsAreSetOnDisk()
    {
        var settings = Settings();
        File.WriteAllText(_path, $$"""
            {
              "{{Dranak}}": {
                "Guides": {
                  "{{GuideId}}": {
                    "DoneObjectiveIds": [ "isle1-key" ],
                    "SkippedObjectiveIds": [ "isle1-key" ],
                    "LastUpdated": "2026-09-09T12:00:00Z"
                  }
                }
              }
            }
            """);
        var ledger = Store();
        var guide = new Guide
        {
            Id = GuideId, Name = "g",
            Stages =
            [
                new GuideStage
                {
                    Id = "isle1", Name = "Isle 1", Order = 1, Objectives = [Step("isle1-key")],
                },
            ],
        };

        var counts = GuideProgressRouter.Counts(settings, ledger, Dranak, guide);

        Assert.Equal(new GuideProgressCounts(1, 0, 1), counts);
        Assert.Equal(0, counts.Remaining);
    }

    // ---- the one-writer guard, both halves --------------------------------------------

    /// <summary>
    /// <c>QuestLedgerStore.SetObjectiveDone</c> has exactly one caller in shipping code, and
    /// it is the router. A surface that reached past it would put a reward objective's tick
    /// in two stores, and the screen the player used second would be the one telling the
    /// truth — the failure this whole class exists to prevent, and one no unit test of a
    /// window can reach (that is why it is a source scan, the shape
    /// <c>QuestLedgerClearCountTests</c> already uses for the same reason).
    ///
    /// <para><c>SetObjectiveSkipped</c> is deliberately NOT restricted: skip has one store,
    /// so a direct call cannot split a fact. Guarding it would be a rule with no failure
    /// behind it.</para>
    /// </summary>
    [Fact]
    public void TheRouterIsTheOnlyWriterOfGuideObjectiveTicks()
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

        var definition = Path.Combine(src, "EQBuddy.Core", "QuestLedgerStore.cs");
        var router = Path.Combine(src, "EQBuddy.UI.Shared", "GuideProgressRouter.cs");

        // The positive half: the one writer really does write.
        Assert.Contains("ledger.SetObjectiveDone(", File.ReadAllText(router));

        var offenders = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !string.Equals(f, definition, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(f, router, StringComparison.OrdinalIgnoreCase))
            .Where(f => File.ReadAllText(f).Contains("SetObjectiveDone(", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(src, f))
            .ToList();

        Assert.Empty(offenders);
    }
}
