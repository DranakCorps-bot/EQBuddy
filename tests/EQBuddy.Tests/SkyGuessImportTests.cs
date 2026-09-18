using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The inventory dump is where a stale Sky guess gets caught (Hateborne, 2026-09-18): the
/// ledger was squared to the dump on the ingest thread, so its total is "the dump, then
/// the log since" - and the import takes back every * tick that total no longer covers,
/// names them in the report, and offers them back with Undo.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public sealed class SkyGuessImportTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private const string Key = "hateborne_neriak";

    private static AppSettings WithGuesses(params string[] ids)
    {
        var settings = new AppSettings
        {
            GearChecklist = [],
            SkyQuestChecklist = [.. SkyQuestDefaults.Items.Select(i => i.Clone())],
        };
        foreach (var row in settings.SkyQuestChecklist.Where(r => ids.Contains(r.Id)))
        {
            row.Acquired = true;
            row.AcquiredUnassigned = true;
        }
        return settings;
    }

    private static InventoryFile.Snapshot Dump(Dictionary<string, int> counts) =>
        new("Hateborne_neriak-Inventory.txt", new DateTime(2026, 9, 18, 11, 45, 38), counts);

    private QuestLedgerStore ReconciledTo(Dictionary<string, int> counts)
    {
        var store = new QuestLedgerStore(_path) { TrackFilter = _ => true, Normalize = QuestCatalog.BaseItemName };
        store.RecordLoot(Key, "High Quality Raiment +1", 1, new DateTime(2026, 9, 3, 8, 31, 20));
        store.RecordLoot(Key, "Wind Rune Meda", 1, new DateTime(2026, 8, 25, 11, 26, 45));
        store.ReconcileInventory(Key, counts, new DateTime(2026, 9, 18, 11, 45, 38));
        return store;
    }

    [Fact]
    public void TheDumpTakesBackGuessesItNoLongerCoversAndSaysWhich()
    {
        var settings = WithGuesses("sky-034", "sky-163");
        var counts = new Dictionary<string, int> { ["Leather Cord"] = 1 };

        var outcome = OutputfileAutoImport.ImportInventory(Dump(counts), settings, null, ReconciledTo(counts), Key);

        Assert.Equal(1, outcome.SkyGuessesCleared);
        Assert.Contains("1 guessed Sky tick cleared", outcome.Summary);
        Assert.Contains("Berserker · High Quality Raiment", outcome.Detail);
        Assert.True(settings.SkyQuestChecklist.Single(r => r.Id == "sky-163").Acquired);   // the banked cord

        Assert.NotNull(outcome.Undo);
        outcome.Undo!();
        Assert.True(settings.SkyQuestChecklist.Single(r => r.Id == "sky-034").AcquiredUnassigned);
    }

    /// <summary>Hateborne's call (2026-09-18), because this ships to every player: a scan
    /// never judges a Wind Rune. The dump cannot see currency, and scans made before EQBuddy
    /// knew that recorded every rune as zero - so most players hold runes their ledger says
    /// they don't. Only a logged hand-in takes a rune guess back.</summary>
    [Fact]
    public void AScanNeverTakesBackARuneGuess()
    {
        var settings = WithGuesses("sky-149", "sky-164", "sky-056");   // Meda: Ranger, Shaman, Druid
        var counts = new Dictionary<string, int> { ["Leather Cord"] = 1 };

        var outcome = OutputfileAutoImport.ImportInventory(Dump(counts), settings, null, ReconciledTo(counts), Key);

        Assert.Equal(0, outcome.SkyGuessesCleared);
        Assert.All(new[] { "sky-149", "sky-164", "sky-056" }, id =>
            Assert.True(settings.SkyQuestChecklist.Single(r => r.Id == id).Acquired));
    }

    [Fact]
    public void WithoutALedgerNothingIsTakenBack()
    {
        var settings = WithGuesses("sky-149");

        var outcome = OutputfileAutoImport.ImportInventory(Dump([]), settings);

        Assert.Equal(0, outcome.SkyGuessesCleared);
        Assert.Contains("nothing new to tick", outcome.Summary);
        Assert.True(settings.SkyQuestChecklist.Single(r => r.Id == "sky-149").Acquired);
    }
}
