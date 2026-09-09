using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Guided progression P1b — the per-character MANUAL progress store, and the two rules that
/// make it safe to layer a guide on top of the Sky checklist.
///
/// <para><b>1. Two axes, never conflated.</b> Authoring completeness (Authored/Stub) is a fact
/// about OUR DATA; progress (done/skipped) is a fact about the CHARACTER. A guide can be
/// finished while half of it is stubs, and fully authored at zero. P1a's validation owns the
/// first axis; this suite owns the second and asserts they cannot be read off each other.</para>
///
/// <para><b>2. One fact, one store.</b> A Sky turn-in already has a store and four writers
/// (checklist, shell, phone, achievements import). The guide reads it from there and writes it
/// back through there; the ledger REFUSES a copy, and the read side ignores one that reaches it
/// anyway. Both halves are asserted — a refusal nobody can bypass and a read that a hand edit
/// cannot fool are different guarantees, and only the second survives a caller who ignores the
/// first.</para>
///
/// <para>The stateful assertions go through a real file and a SECOND store over it, because
/// "manual progress survives" is a claim about a restart, not about a dictionary. Two ways this
/// store has historically lost data are both re-run here against the new field: the
/// counting-rules reset (which is allowed to clear log-derived counters and nothing else) and
/// the pre-tracking-shape reparse in <c>Load</c>, whose emptiness heuristic has to know about
/// every field or it silently swallows the one it does not.</para>
/// </summary>
public sealed class GuideProgressStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-guide-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private const string Dranak = "dranak_freeport";
    private const string GuideId = "pos-warrior-runed-wind-amulet";

    /// <summary>The shipped seed guide — the store is exercised against the real catalog's
    /// objectives, not invented ones, so a schema change that breaks the coupling shows up
    /// here too.</summary>
    private static Guide Seed => GuideCatalog.Default.Find(GuideId)
        ?? throw new InvalidOperationException($"seed guide '{GuideId}' is gone from the catalog");

    private static GuideObjective Objective(string id) =>
        Seed.AllObjectives.FirstOrDefault(o => o.Id == id)
        ?? throw new InvalidOperationException($"seed guide has no objective '{id}'");

    /// <summary>The two shapes the routing splits on, taken from the seed guide: a plain step
    /// the ledger owns, and the turn-in the Sky store owns.</summary>
    private static GuideObjective PlainStep => Objective("stone-amulet");
    private static GuideObjective TurnIn => Objective("turn-in-runed-wind-amulet");

    private static readonly Func<string, bool> NothingTurnedIn = _ => false;

    // ---- The player's own statement, and its durability ------------------------------

    [Fact]
    public void AnUntouchedGuideReadsAsNoProgressRatherThanNull()
    {
        var progress = Store().GuideProgressFor(Dranak, GuideId);

        Assert.Empty(progress.DoneObjectiveIds);
        Assert.Empty(progress.SkippedObjectiveIds);
        Assert.False(GuideProgressRouting.IsDone(PlainStep, progress, NothingTurnedIn));
    }

    [Fact]
    public void ATickIsTakenUnTickedAndIsIdempotentBothWays()
    {
        var store = Store();

        Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true));
        Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true));
        Assert.Equal(["stone-amulet"], store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);

        Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, false));
        Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, false));
        Assert.Empty(store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    /// <summary>The whole point of a store: the tick is still there next launch. Through the
    /// real file and a second instance — an in-memory assertion would pass on a field that
    /// never reaches <c>Load</c>'s reader.</summary>
    [Fact]
    public void ATickSurvivesARestart()
    {
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);
        store.SetGuideObjectiveSkipped(Dranak, GuideId, Objective("wind-rune-azia"), true);
        store.Flush();

        var reloaded = Store().GuideProgressFor(Dranak, GuideId);

        Assert.Equal(["stone-amulet"], reloaded.DoneObjectiveIds);
        Assert.Equal(["wind-rune-azia"], reloaded.SkippedObjectiveIds);
    }

    /// <summary>
    /// The counting-rules reset clears LOG-DERIVED counters so the next replay rebuilds them.
    /// Manual progress was never derived from a log line, so it must come through untouched —
    /// the same reasoning that already protects manual counts, pins and completions.
    ///
    /// <para>The stale marker is written by hand: that is exactly what an install carrying an
    /// older rules version looks like on the next launch.</para>
    /// </summary>
    [Fact]
    public void ATickSurvivesTheCountingRulesReset()
    {
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);
        store.RecordLoot(Dranak, "Stone Amulet", 1, new DateTime(2026, 9, 1, 20, 0, 0));
        store.Flush();
        File.WriteAllText(_path + ".rules", "1");

        var reloaded = Store();

        // The reset really did run — otherwise this test proves nothing about surviving it.
        Assert.Equal(0, reloaded.For(Dranak)["Stone Amulet"].Looted);
        Assert.Equal(["stone-amulet"], reloaded.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    /// <summary>
    /// <b>The silent-swallow guard.</b> <c>Load</c> detects the pre-tracking file shape by
    /// asking whether every character parsed empty — and a character whose only content is
    /// guide progress looks empty to any list that has not been told about the field. The
    /// reparse then reads <c>Guides</c> as an ITEM NAME (an <c>Entry</c> ignores unknown
    /// properties exactly as a <c>CharacterLedger</c> does), decides the file is the old
    /// shape, and returns a phantom item in place of the player's ticks — silently.
    ///
    /// <para><b>Written as a file, not through the store, because that is the shape that can
    /// fail.</b> A ledger the app itself wrote also carries <c>"Tracked": []</c>, and an array
    /// where the old shape wants an <c>Entry</c> throws, which drops the reparse and saves the
    /// data by accident. A trimmed or hand-edited ledger — a player restoring one file, a
    /// future writer that omits empty collections — carries no such accident. Prove-failed by
    /// dropping <c>c.Guides.Count == 0</c> from the heuristic: the progress comes back empty
    /// and an item called "Guides" appears in the ledger, which is what the second assertion
    /// names.</para>
    /// </summary>
    [Fact]
    public void AGuideOnlyLedgerFileReloadsAsGuideProgressAndNotAsAnItemCalledGuides()
    {
        File.WriteAllText(_path, """
            {
              "dranak_freeport": {
                "Guides": {
                  "pos-warrior-runed-wind-amulet": {
                    "DoneObjectiveIds": [ "stone-amulet" ],
                    "SkippedObjectiveIds": [],
                    "LastUpdated": "2026-09-09T15:00:00Z"
                  }
                }
              }
            }
            """);

        var reloaded = Store();

        Assert.Equal(["stone-amulet"], reloaded.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
        Assert.DoesNotContain("Guides", reloaded.For(Dranak).Keys);
    }

    /// <summary>The other half of the same reader: a genuine pre-tracking file (char → item →
    /// entry, the shape that predates every field above) still migrates. The emptiness
    /// heuristic has to keep letting THAT through — a guard that fixes one file shape by
    /// breaking the other has moved the bug, not removed it.</summary>
    [Fact]
    public void ThePreTrackingLedgerShapeStillMigrates()
    {
        File.WriteAllText(_path, """
            { "dranak_freeport": { "Bone Chips": { "Looted": 9, "Manual": 4 } } }
            """);

        // Manual, not Looted: the first load under a newer rules version resets the
        // log-derived counters by design, and this test is about the file SHAPE.
        Assert.Equal(4, Store().For(Dranak)["Bone Chips"].Manual);
    }

    [Fact]
    public void ProgressIsPerCharacter()
    {
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);

        Assert.Empty(store.GuideProgressFor("bexley_qeynos", GuideId).DoneObjectiveIds);
        Assert.Equal(["stone-amulet"], store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    /// <summary>Ids are matched the way every other key in this store is — a guide id spelled
    /// out of a saved address and one spelled out of the catalog must be the same guide.</summary>
    [Fact]
    public void GuideAndObjectiveIdsMatchCaseInsensitively()
    {
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId.ToUpperInvariant(), PlainStep, true);

        var progress = store.GuideProgressFor(Dranak, GuideId);
        Assert.True(GuideProgressRouting.IsDone(PlainStep, progress, NothingTurnedIn));

        store.SetGuideObjectiveDone(Dranak, GuideId,
            new GuideObjective { Id = PlainStep.Id.ToUpperInvariant() }, false);
        Assert.Empty(store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    [Fact]
    public void AGuideBackAtZeroKeepsNoRow()
    {
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, false);
        store.Flush();

        // Nothing to say about a guide nobody has started — and an empty record carrying a
        // LastUpdated would claim progress that was undone.
        Assert.Equal(default, Store().GuideProgressFor(Dranak, GuideId).LastUpdated);
    }

    [Fact]
    public void ATickStampsWhenThePlayerSaidIt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var store = Store();
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);

        var stamped = store.GuideProgressFor(Dranak, GuideId).LastUpdated;
        Assert.InRange(stamped, before, DateTime.UtcNow.AddSeconds(1));
    }

    // ---- Done and skipped are different statements -----------------------------------

    [Fact]
    public void MarkingDoneClearsASkipAndMarkingSkippedClearsADone()
    {
        var store = Store();

        store.SetGuideObjectiveSkipped(Dranak, GuideId, PlainStep, true);
        store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, true);

        var afterDone = store.GuideProgressFor(Dranak, GuideId);
        Assert.Equal(["stone-amulet"], afterDone.DoneObjectiveIds);
        Assert.Empty(afterDone.SkippedObjectiveIds);

        store.SetGuideObjectiveSkipped(Dranak, GuideId, PlainStep, true);

        var afterSkip = store.GuideProgressFor(Dranak, GuideId);
        Assert.Empty(afterSkip.DoneObjectiveIds);
        Assert.Equal(["stone-amulet"], afterSkip.SkippedObjectiveIds);
    }

    /// <summary>A skip is not a done: it counts toward neither the done tally nor a claim the
    /// step happened.</summary>
    [Fact]
    public void ASkippedStepIsNotADoneOne()
    {
        var store = Store();
        store.SetGuideObjectiveSkipped(Dranak, GuideId, PlainStep, true);

        var progress = store.GuideProgressFor(Dranak, GuideId);
        Assert.True(GuideProgressRouting.IsSkipped(PlainStep, progress));
        Assert.False(GuideProgressRouting.IsDone(PlainStep, progress, NothingTurnedIn));
        Assert.Equal(0, GuideProgressRouting.DoneCount(Seed, progress, NothingTurnedIn));
    }

    // ---- One fact, one store ---------------------------------------------------------

    [Fact]
    public void TheRoutingSendsARewardKeyedObjectiveToTheSkyStoreAndEverythingElseToTheLedger()
    {
        Assert.Equal(GuideProgressWriter.SkyTurnIn, GuideProgressRouting.WriterFor(TurnIn));
        Assert.Equal(GuideProgressWriter.GuideLedger, GuideProgressRouting.WriterFor(PlainStep));
    }

    /// <summary>The write half: the ledger refuses the turn-in and writes NOTHING. A store that
    /// accepted it would be a second answer to a question four surfaces already answer.</summary>
    [Fact]
    public void TheLedgerRefusesATurnInAndKeepsNothing()
    {
        var store = Store();

        Assert.False(store.SetGuideObjectiveDone(Dranak, GuideId, TurnIn, true));

        var progress = store.GuideProgressFor(Dranak, GuideId);
        Assert.Empty(progress.DoneObjectiveIds);
        Assert.Equal(default, progress.LastUpdated);
    }

    /// <summary>The read half, and the one that survives a caller who ignored the refusal: a
    /// done id sitting in a reward objective's list — a hand-edited <c>quest-ledger.json</c> —
    /// still does not make the turn-in read as done, because the read routes on the reward key
    /// and never looks at the ledger for it.</summary>
    [Fact]
    public void AHandEditedTurnInTickInTheLedgerIsUnreadable()
    {
        var forged = new QuestLedgerStore.GuideProgress
        {
            DoneObjectiveIds = [TurnIn.Id],
        };

        Assert.False(GuideProgressRouting.IsDone(TurnIn, forged, NothingTurnedIn));
        Assert.True(GuideProgressRouting.IsDone(TurnIn, forged, key => key == TurnIn.RewardKey));
    }

    /// <summary>The positive half: the turn-in reads done from the Sky store alone, with an
    /// empty ledger — which is what a reward turned in on the classic checklist, the phone or
    /// an achievements import looks like from inside the guide.</summary>
    [Fact]
    public void ATurnInMarkedOnTheChecklistReadsAsDoneInsideTheGuideWithNoLedgerEntry()
    {
        var store = Store();
        var turnedIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var progress = store.GuideProgressFor(Dranak, GuideId);
        Assert.False(GuideProgressRouting.IsDone(TurnIn, progress, turnedIn.Contains));

        // Exactly what SkyCompleteToggle.MarkTurnedIn leaves behind: the key in the Sky list.
        turnedIn.Add(TurnIn.RewardKey);

        Assert.True(GuideProgressRouting.IsDone(TurnIn, progress, turnedIn.Contains));
        Assert.Empty(store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    /// <summary>A skip IS allowed on a turn-in — it has no second home, so refusing it would
    /// leave the step unfoldable — and it does not touch the turn-in itself.</summary>
    [Fact]
    public void ATurnInCanBeSkippedWithoutTouchingTheTurnInStore()
    {
        var store = Store();
        var turnedIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.True(store.SetGuideObjectiveSkipped(Dranak, GuideId, TurnIn, true));

        var progress = store.GuideProgressFor(Dranak, GuideId);
        Assert.True(GuideProgressRouting.IsSkipped(TurnIn, progress));
        Assert.False(GuideProgressRouting.IsDone(TurnIn, progress, turnedIn.Contains));
        Assert.Empty(turnedIn);
    }

    // ---- The two axes ----------------------------------------------------------------

    /// <summary>
    /// <b>Progress is not authoring.</b> The seed guide is walked to 100% — every ledger step
    /// ticked, the turn-in marked in the Sky store — while it is still one third stubs. A
    /// finished guide does not become an authored one, and nothing in the store may let a
    /// caller infer otherwise.
    /// </summary>
    [Fact]
    public void AGuideCanBeFullyDoneWhileItIsStillPartlyStubbed()
    {
        var store = Store();
        var turnedIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var objective in Seed.AllObjectives)
            if (GuideProgressRouting.WriterFor(objective) == GuideProgressWriter.SkyTurnIn)
                turnedIn.Add(objective.RewardKey);
            else
                Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, objective, true));

        var progress = store.GuideProgressFor(Dranak, GuideId);

        Assert.Equal(Seed.AllObjectives.Count(),
            GuideProgressRouting.DoneCount(Seed, progress, turnedIn.Contains));
        Assert.True(Seed.StubCount > 0, "the seed guide is supposed to ship a real stub");
        Assert.False(Seed.IsFullyAuthored);
    }

    /// <summary>And the other way round: a guide with no stub in it at all is at zero until the
    /// player says otherwise. Authoring completeness is not a head start.</summary>
    [Fact]
    public void AFullyAuthoredGuideStartsAtZeroProgress()
    {
        var authored = GuideCatalog.FromJson("""
            {
              "guides": [{
                "id": "fixture", "name": "Fixture guide", "guideType": "NormalQuest",
                "zoneNames": ["Somewhere"], "applicableClasses": ["Warrior"],
                "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-09-08" }],
                "stages": [{ "id": "s1", "name": "The stage", "order": 1, "objectives": [
                  { "id": "a", "order": 1, "objectiveType": "Kill", "title": "A",
                    "shortInstruction": "A.", "who": "A mob", "where": "A zone", "what": "Kill it.",
                    "authoring": "Authored",
                    "sources": [{ "url": "https://eqlwiki.com/X", "title": "X", "retrievedAt": "2026-09-08" }] }
                ]}]
              }]
            }
            """).Guides[0];

        Assert.True(authored.IsFullyAuthored);
        Assert.Equal(0, GuideProgressRouting.DoneCount(
            authored, Store().GuideProgressFor(Dranak, authored.Id), NothingTurnedIn));
    }

    // ---- Refusals that write nothing --------------------------------------------------

    [Theory]
    [InlineData("", GuideId, "stone-amulet")]
    [InlineData(Dranak, "", "stone-amulet")]
    [InlineData(Dranak, GuideId, "")]
    public void AnIncompleteAddressIsRefusedRatherThanStored(
        string characterKey, string guideId, string objectiveId)
    {
        var store = Store();

        Assert.False(store.SetGuideObjectiveDone(
            characterKey, guideId, new GuideObjective { Id = objectiveId }, true));
        Assert.Empty(store.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    /// <summary>Unticking a guide nobody started must not create a row for it.</summary>
    [Fact]
    public void UnTickingAGuideNobodyStartedCreatesNothing()
    {
        var store = Store();

        Assert.True(store.SetGuideObjectiveDone(Dranak, GuideId, PlainStep, false));
        store.Flush();

        Assert.Equal(default, Store().GuideProgressFor(Dranak, GuideId).LastUpdated);
    }
}
