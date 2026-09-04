using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The right-click "clear this count" operation (#241 PR 3's provenance sentence points
/// straight at it), found silently dead during the v1.99.14 release review: both windows
/// hand-rolled it as <c>SetManual(-Looted)</c>, and PR 1's reconcile moves the whole count
/// into <c>Verified</c> and zeroes <c>Looted</c> — so on every row an inventory dump had
/// verified, the documented affordance did nothing. Trap 20's family: a new field, an old
/// writer not updated; PR 1 caught the same shape in <c>For()</c> and this second site
/// survived because the arithmetic lived in the windows, where no unit test can reach it.
/// The arithmetic is <c>QuestLedgerStore.ClearCount</c> now; the scan at the bottom keeps
/// it there.
/// </summary>
public sealed class QuestLedgerClearCountTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    /// <summary>The exact regression: a dump-verified count must clear to zero. This is
    /// the row the Turn-ins sentence tells the player to right-click.</summary>
    [Fact]
    public void ClearZeroesADumpVerifiedCount()
    {
        var store = Store();
        store.RecordLoot("dranak_freeport", "Wind Rune Izah", 4, new DateTime(2026, 8, 20, 18, 0, 0));
        store.ReconcileInventory("dranak_freeport",
            new Dictionary<string, int> { ["Wind Rune Izah"] = 4 },
            new DateTime(2026, 8, 20, 18, 47, 36));
        Assert.Equal(4, store.For("dranak_freeport")["Wind Rune Izah"].Total);

        store.ClearCount("dranak_freeport", "Wind Rune Izah");

        Assert.Equal(0, store.For("dranak_freeport")["Wind Rune Izah"].Total);
    }

    /// <summary>Verified plus loot logged after the dump clears as one count, and loot
    /// after the clear counts up from zero — "future loot counts up from there" is the
    /// operation's contract and must survive the Verified bucket.</summary>
    [Fact]
    public void ClearCoversLootSinceTheDumpAndFutureLootCountsUpFromZero()
    {
        var store = Store();
        store.ReconcileInventory("dranak_freeport",
            new Dictionary<string, int> { ["Sphinx Claw"] = 3 },
            new DateTime(2026, 8, 20, 18, 0, 0));
        store.RecordLoot("dranak_freeport", "Sphinx Claw", 2, new DateTime(2026, 8, 20, 19, 0, 0));
        Assert.Equal(5, store.For("dranak_freeport")["Sphinx Claw"].Total);

        store.ClearCount("dranak_freeport", "Sphinx Claw");
        Assert.Equal(0, store.For("dranak_freeport")["Sphinx Claw"].Total);

        store.RecordLoot("dranak_freeport", "Sphinx Claw", 1, new DateTime(2026, 8, 20, 20, 0, 0));
        Assert.Equal(1, store.For("dranak_freeport")["Sphinx Claw"].Total);
    }

    /// <summary>The pre-#241 shape keeps working: a log-tally-only count still clears.</summary>
    [Fact]
    public void ClearStillZeroesAPlainLootedCount()
    {
        var store = Store();
        store.RecordLoot("dranak_freeport", "Mithril Bands", 2, new DateTime(2026, 8, 20, 18, 0, 0));

        store.ClearCount("dranak_freeport", "Mithril Bands");

        Assert.Equal(0, store.For("dranak_freeport").GetValueOrDefault("Mithril Bands")?.Total ?? 0);
    }

    /// <summary>An item the ledger does not hold is a no-op — clearing must never CREATE
    /// an entry.</summary>
    [Fact]
    public void ClearingAnUnknownItemCreatesNothing()
    {
        var store = Store();

        store.ClearCount("dranak_freeport", "Bone Chips");

        Assert.Empty(store.For("dranak_freeport"));
    }

    /// <summary>The quest window must CALL the store's ClearCount and may not hand-roll the
    /// offset again — the hand-rolled copy is exactly what went stale (trap 34's positive
    /// half beside the fix). Scanned both lanes until E-2 (2026-09-04); the row that
    /// remains is the one that ships, and the hand-rolled subtraction it forbids is a
    /// within-lane regression, not a cross-lane one.</summary>
    [Theory]
    [InlineData("EQBuddy", "QuestsWindow.xaml.cs")]
    public void TheQuestWindowUsesTheStoresClearCount(string project, string file)
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var text = File.ReadAllText(Path.Combine(src, project, file));

        Assert.Contains("ledger.ClearCount(", text);
        Assert.DoesNotContain("-entry.Looted", text);
    }
}
