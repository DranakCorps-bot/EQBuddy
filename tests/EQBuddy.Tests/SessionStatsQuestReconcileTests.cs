using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The #241 reconcile has to run ON THE INGEST, at <c>SessionStats</c>' own
/// <c>OutputfileEvent</c> case, in log order — never on a UI-thread hop, or a loot line
/// seconds after the dump could race ahead of the reconcile and get zeroed. These pin the
/// wiring one layer below the widget-source-scan in <see cref="QuestReconcileWiringTests"/>:
/// given a resolver and a store, does applying the announcement line actually reconcile.
/// </summary>
// Not itself a settings.json writer — but it names OutputfileAutoImport (for
// OutputfileAutoImport.KindOf), which is the collection's coarse trigger string
// (SettingsFileCollection.cs), so it joins the serial collection rather than being an
// undetectable false-negative in that guard's own scan.
[Collection(SettingsFileCollection.Name)]
public sealed class SessionStatsQuestReconcileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private const string AnnounceLine =
        "[Thu Aug 20 18:47:36 2026] Outputfile Complete: Dranak_freeport-Inventory.txt";

    private static SessionStats Stats(QuestLedgerStore store, InventoryFile.Snapshot? snap) => new()
    {
        CharacterName = "Dranak", ServerName = "freeport",
        QuestStore = store,
        InventoryDumpResolver = fileName =>
            OutputfileAutoImport.KindOf(fileName) == OutputfileKind.Inventory ? snap : null,
    };

    [Fact]
    public void ApplyingTheAnnouncementReconcilesTheLedger()
    {
        var store = new QuestLedgerStore(_path) { TrackFilter = _ => true };
        store.RecordLoot("dranak_freeport", "Wind Rune Izah", 5,
            new DateTime(2026, 8, 20, 18, 0, 0));
        var snap = new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt",
            new DateTime(2026, 8, 20, 18, 47, 36),
            new Dictionary<string, int> { ["Wind Rune Izah"] = 2 });
        var stats = Stats(store, snap);

        stats.Apply(LogParser.Parse(AnnounceLine)!);

        Assert.Equal(2, store.For("dranak_freeport")["Wind Rune Izah"].Total);
        Assert.NotNull(stats.LastQuestReconcile);
        Assert.Equal(1, stats.LastQuestReconcile!.Value.Trued);
    }

    /// <summary>Review replay (#74): the store must not be touched while a session is being
    /// re-lived as history, exactly like the loot/consumed sites right above the ingest
    /// case this reads.</summary>
    [Fact]
    public void StoresSuppressedStopsTheReconcile()
    {
        var store = new QuestLedgerStore(_path) { TrackFilter = _ => true };
        var snap = new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt",
            new DateTime(2026, 8, 20, 18, 47, 36),
            new Dictionary<string, int> { ["Wind Rune Izah"] = 9 });
        var stats = Stats(store, snap);
        stats.StoresSuppressed = true;

        stats.Apply(LogParser.Parse(AnnounceLine)!);

        Assert.False(store.For("dranak_freeport").ContainsKey("Wind Rune Izah"));
        Assert.Null(stats.LastQuestReconcile);
    }

    /// <summary>A dump that isn't an inventory dump (or that the resolver can't read)
    /// leaves the ledger untouched and still forwards the announcement, so the gear/
    /// achievements/factions handlers on the UI thread keep working exactly as before
    /// this feature existed.</summary>
    [Fact]
    public void AResolverThatReturnsNothingLeavesTheLedgerAloneAndStillForwardsTheEvent()
    {
        var store = new QuestLedgerStore(_path) { TrackFilter = _ => true };
        var stats = Stats(store, snap: null);
        OutputfileEvent? forwarded = null;
        stats.OutputfileWritten += ev => forwarded = ev;

        stats.Apply(LogParser.Parse(AnnounceLine)!);

        Assert.Null(stats.LastQuestReconcile);
        Assert.Equal("Dranak_freeport-Inventory.txt", forwarded?.FileName);
    }
}
