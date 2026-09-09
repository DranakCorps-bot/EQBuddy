using System.Text.Json;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The guided-progression progress store (Fable plan §4, P1b): per-character manual state
/// for the steps of a <see cref="Guide"/>.
///
/// The two claims worth a test are both about SURVIVAL, because this is the only kind of
/// progress in EQBuddy that nothing can rebuild. A loot tally is replay-derived — lose it
/// and the next full-log replay puts it back. "I walked to the isle and did the thing" is a
/// player's statement, and the log has never known it. So a tick has to survive a restart,
/// and it has to survive a <c>CountingRulesVersion</c> bump, whose whole job is to throw
/// away the counters that ARE replay-derived.
/// </summary>
public sealed class GuideProgressStoreTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string Guide = "war-pos";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    /// <summary>Reopen the same file the way a relaunch does: flush the debounced write,
    /// then construct a second store over the same path.</summary>
    private QuestLedgerStore Relaunch(QuestLedgerStore store)
    {
        store.Flush();
        return Store();
    }

    [Fact]
    public void AnUntouchedGuideReadsAsEmptyRatherThanMissing()
    {
        var progress = Store().GuideProgressFor(Dranak, Guide);

        Assert.Empty(progress.DoneObjectiveIds);
        Assert.Empty(progress.SkippedObjectiveIds);
        Assert.Equal(default, progress.LastUpdated);
    }

    [Fact]
    public void TickingAnObjectiveRecordsItAndStampsTheTime()
    {
        var before = DateTime.UtcNow;
        var store = Store();

        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        var progress = store.GuideProgressFor(Dranak, Guide);
        Assert.Equal(["isle1-key"], progress.DoneObjectiveIds);
        Assert.InRange(progress.LastUpdated, before, DateTime.UtcNow);
    }

    /// <summary>THE claim of P1b: a tick is still there after a relaunch. It is the only
    /// progress in the app that a replay cannot rebuild.</summary>
    [Fact]
    public void ATickSurvivesARestart()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);
        store.SetObjectiveSkipped(Dranak, Guide, "isle3-camp", true);

        var reopened = Relaunch(store);

        var progress = reopened.GuideProgressFor(Dranak, Guide);
        Assert.Equal(["isle1-key"], progress.DoneObjectiveIds);
        Assert.Equal(["isle3-camp"], progress.SkippedObjectiveIds);
    }

    /// <summary>Progress is per CHARACTER — the per-profile Sky ticks are the known wart this
    /// deliberately does not copy, so a mule's guide must not read as the main's.</summary>
    [Fact]
    public void ProgressIsPerCharacterAndSurvivesTheRestartThatWay()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);
        store.SetObjectiveDone("mule_qeynos", Guide, "isle2-drop", true);

        var reopened = Relaunch(store);

        Assert.Equal(["isle1-key"], reopened.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
        Assert.Equal(["isle2-drop"], reopened.GuideProgressFor("mule_qeynos", Guide).DoneObjectiveIds);
    }

    /// <summary>The second survival claim. A counting-rules bump exists to throw away the
    /// LOG-DERIVED counters so the next replay rebuilds them under the new rules; a guide
    /// tick is not one of those, and erasing it would be a silent loss with no way back.
    /// Asserted next to the loot counter it DOES clear, so the test proves the reset ran
    /// rather than merely finding nothing to do (a green that means nothing happened is the
    /// vacuous half of trap 34).</summary>
    [Fact]
    public void ATickSurvivesTheCountingRulesReset()
    {
        var store = Store();
        store.RecordLoot(Dranak, "Wind Rune Izah", 4, new DateTime(2026, 8, 20, 18, 0, 0));
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);
        store.SetObjectiveSkipped(Dranak, Guide, "isle3-camp", true);
        store.Flush();

        // How a rules bump looks on disk: the marker is behind (or, on the very first run
        // under a new rule, absent), so the constructor resets the replayable counters.
        File.Delete(_path + ".rules");
        var afterBump = Store();

        Assert.Equal(0, afterBump.For(Dranak)["Wind Rune Izah"].Looted);
        var progress = afterBump.GuideProgressFor(Dranak, Guide);
        Assert.Equal(["isle1-key"], progress.DoneObjectiveIds);
        Assert.Equal(["isle3-camp"], progress.SkippedObjectiveIds);
    }

    [Fact]
    public void UntickingRemovesTheObjective()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        store.SetObjectiveDone(Dranak, Guide, "isle1-key", false);

        Assert.Empty(store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
    }

    /// <summary>Done and skipped are answers to the same question, so each clears the other —
    /// the Tracked/Hidden rule, one layer down.</summary>
    [Fact]
    public void SkippingClearsTheTickAndTickingClearsTheSkip()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        store.SetObjectiveSkipped(Dranak, Guide, "isle1-key", true);
        Assert.Empty(store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
        Assert.Equal(["isle1-key"], store.GuideProgressFor(Dranak, Guide).SkippedObjectiveIds);

        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);
        Assert.Equal(["isle1-key"], store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
        Assert.Empty(store.GuideProgressFor(Dranak, Guide).SkippedObjectiveIds);
    }

    /// <summary>Ticking twice is one entry, not two — the rows re-render and the phone and
    /// the desktop can both send the same verb.</summary>
    [Fact]
    public void TickingTwiceIsIdempotent()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        Assert.Equal(["isle1-key"], store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
    }

    /// <summary>Un-ticking something never ticked must not CREATE the guide's row. A render
    /// pass that reasserts state for every guide on screen would otherwise grow the ledger a
    /// row per guide, and "which guides has this character touched" would stop being true.</summary>
    [Fact]
    public void UntickingSomethingNeverTickedWritesNothing()
    {
        var store = Store();

        store.SetObjectiveDone(Dranak, Guide, "isle1-key", false);
        store.SetObjectiveSkipped(Dranak, Guide, "isle3-camp", false);

        Assert.Empty(store.GuidesTouchedBy(Dranak));
    }

    [Theory]
    [InlineData("", Guide, "isle1-key")]
    [InlineData(Dranak, "", "isle1-key")]
    [InlineData(Dranak, Guide, "")]
    public void EmptyKeysAreNoOps(string character, string guide, string objective)
    {
        var store = Store();

        store.SetObjectiveDone(character, guide, objective, true);

        Assert.Empty(store.GuidesTouchedBy(character));
        Assert.Empty(store.GuidesTouchedBy(Dranak));
    }

    /// <summary>Guide ids and objective ids are hand-authored strings in a curated file; the
    /// rest of this store matches names case-insensitively and a guide row that lost its tick
    /// to a capital letter would be a bug nobody could see.</summary>
    [Fact]
    public void IdsMatchCaseInsensitivelyAcrossARestart()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        var reopened = Relaunch(store);

        Assert.Contains("ISLE1-KEY",
            reopened.GuideProgressFor(Dranak, "WAR-POS").DoneObjectiveIds,
            StringComparer.OrdinalIgnoreCase);
        reopened.SetObjectiveDone(Dranak, "WAR-POS", "ISLE1-KEY", false);
        Assert.Empty(reopened.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
    }

    /// <summary>The reader hands out a copy: a caller that mutates what it got back must not
    /// be editing the ledger behind the store's lock.</summary>
    [Fact]
    public void TheReaderHandsOutACopy()
    {
        var store = Store();
        store.SetObjectiveDone(Dranak, Guide, "isle1-key", true);

        store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds.Add("isle9-invented");

        Assert.Equal(["isle1-key"], store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
    }

    /// <summary>A ledger written by an EQBuddy that predates guides loads with no guide
    /// progress and no complaint — and, crucially, is not mistaken for the pre-tracking
    /// shape the loader also has to recognise.</summary>
    [Fact]
    public void ALedgerWrittenBeforeGuidesExistedStillLoads()
    {
        File.WriteAllText(_path, """
            {
              "dranak_freeport": {
                "Items": { "Bone Chips": { "Looted": 4, "LastTime": "2026-08-20T18:00:00" } },
                "Tracked": [ "Rogue Epic" ],
                "Hidden": [],
                "Completed": {},
                "Classes": [ "Warrior" ],
                "UnlockedClasses": [],
                "Level": 29
              }
            }
            """);
        File.WriteAllText(_path + ".rules", "999");   // not a rules bump: keep the counters

        var store = Store();

        Assert.Equal(4, store.For(Dranak)["Bone Chips"].Looted);
        Assert.Equal(29, store.LevelFor(Dranak));
        Assert.Empty(store.GuidesTouchedBy(Dranak));
        Assert.Empty(store.GuideProgressFor(Dranak, Guide).DoneObjectiveIds);
    }

    /// <summary>
    /// A ledger whose ONLY content is guide progress must not trip the loader's
    /// pre-tracking-shape heuristic ("every character is empty, so this must be the old
    /// char → item → entry file"). The heuristic fires on emptiness, and a new field it does
    /// not count looks exactly like emptiness — so without <c>Guides</c> in that condition,
    /// this file goes down the legacy reparse, where <c>"Guides": { "war-pos": {…} }</c>
    /// deserializes cheerfully as one item named "Guides" and every tick is gone.
    ///
    /// <para>Seeded as BYTES rather than through the store, and that is the whole point: the
    /// store's own writer always emits all nine properties, so a round trip can never produce
    /// the file shape that triggers this. The bytes below are what a hand-edited profile, or
    /// a leaner future writer, actually looks like.</para>
    /// </summary>
    [Fact]
    public void ALedgerHoldingONLYGuideProgressIsNotMistakenForTheOldShape()
    {
        File.WriteAllText(_path, """
            {
              "dranak_freeport": {
                "Guides": {
                  "war-pos": {
                    "DoneObjectiveIds": [ "isle1-key" ],
                    "SkippedObjectiveIds": [ "isle3-camp" ],
                    "LastUpdated": "2026-09-09T12:00:00Z"
                  }
                }
              }
            }
            """);

        var progress = Store().GuideProgressFor(Dranak, Guide);

        Assert.Equal(["isle1-key"], progress.DoneObjectiveIds);
        Assert.Equal(["isle3-camp"], progress.SkippedObjectiveIds);
    }

    /// <summary>And the shape the heuristic really is for still works: a genuine pre-tracking
    /// file (char → item → entry, no CharacterLedger wrapper) carries its items over. The
    /// guard above narrows that path; it must not close it.</summary>
    [Fact]
    public void TheGenuinePreTrackingShapeStillCarriesItsItemsOver()
    {
        File.WriteAllText(_path, """
            { "dranak_freeport": { "Bone Chips": { "Looted": 4, "LastTime": "2026-07-01T18:00:00" } } }
            """);
        File.WriteAllText(_path + ".rules", "999");   // not a rules bump: keep the counters

        var store = Store();

        Assert.Equal(4, store.For(Dranak)["Bone Chips"].Looted);
    }

    /// <summary>
    /// Every field of a character's ledger survives the save/load round trip.
    ///
    /// <para><see cref="QuestLedgerStore"/>'s loader rebuilds each <c>CharacterLedger</c> by
    /// HAND so the name-keyed dictionaries come back case-insensitive. A property added to
    /// the type and forgotten in that copy is dropped on every launch, silently — the
    /// player's tick simply is not there after a restart, with nothing logged and nothing
    /// failing. This is trap 26's shape one layer below the UI: a new field, an old writer
    /// not updated.</para>
    ///
    /// <para>The property list below is the must-list (trap 34): forbidding the wrong thing
    /// cannot see a missing thing, so the test asserts the type's shape FIRST and fails when
    /// a field is added, pointing the author at the sample they now have to populate.</para>
    /// </summary>
    [Fact]
    public void EveryCharacterLedgerFieldSurvivesTheRoundTrip()
    {
        string[] populated =
        [
            "Items", "Tracked", "Hidden", "Completed", "Classes",
            "UnlockedClasses", "Level", "Guides", "LastInventoryReconcile",
        ];
        Assert.Equal(
            populated.OrderBy(n => n, StringComparer.Ordinal),
            typeof(QuestLedgerStore.CharacterLedger).GetProperties()
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        var sample = new QuestLedgerStore.CharacterLedger
        {
            Items = { ["Bone Chips"] = new QuestLedgerStore.Entry { Looted = 4, Verified = 1 } },
            Tracked = { "Rogue Epic" },
            Hidden = { "Bone Chip Turn-ins" },
            Completed = { ["Rogue Epic"] = 2 },
            Classes = { "Warrior" },
            UnlockedClasses = { "Warrior", "Monk" },
            Level = 29,
            Guides =
            {
                [Guide] = new QuestLedgerStore.GuideProgress
                {
                    DoneObjectiveIds = { "isle1-key" },
                    SkippedObjectiveIds = { "isle3-camp" },
                    LastUpdated = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc),
                },
            },
            LastInventoryReconcile = new DateTime(2026, 8, 20, 18, 47, 36),
        };
        var written = JsonSerializer.Serialize(
            new Dictionary<string, QuestLedgerStore.CharacterLedger> { [Dranak] = sample });
        File.WriteAllText(_path, written);
        File.WriteAllText(_path + ".rules", "999");   // not a rules bump: keep the counters

        Store().Flush();

        using var before = JsonDocument.Parse(written);
        using var after = JsonDocument.Parse(File.ReadAllText(_path));
        var beforeCharacter = before.RootElement.GetProperty(Dranak);
        var afterCharacter = after.RootElement.GetProperty(Dranak);
        foreach (var name in populated)
            Assert.Equal(
                beforeCharacter.GetProperty(name).GetRawText().Replace(" ", "").Replace("\n", "").Replace("\r", ""),
                afterCharacter.GetProperty(name).GetRawText().Replace(" ", "").Replace("\n", "").Replace("\r", ""));
    }
}
