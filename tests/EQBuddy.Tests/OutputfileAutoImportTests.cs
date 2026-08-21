using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The game announces every dump it writes, in the log EQBuddy already tails, and names the
/// file. Nothing parsed that line until 2026-08-20, so three surfaces asked the player to go
/// and find the file by hand — David ran the command, the file appeared, the window sat
/// there, and the instructions then sent him to a menu and a folder he did not know.
///
/// These pin the whole path: the line is recognised, the filename decides the meaning, the
/// dump is located without help, the import applies, and the undo puts back exactly what
/// that import changed and nothing else.
/// </summary>
public class OutputfileAutoImportTests
{
    /// <summary>VERBATIM from David's own log, 2026-08-20 18:47:36 — not a guess at what
    /// the game prints. The whole feature rests on this string, so it is quoted rather
    /// than described.</summary>
    private const string RealLine =
        "[Thu Aug 20 18:47:36 2026] Outputfile Complete: Dranak_freeport-Inventory.txt";

    [Fact]
    public void TheGameAnnouncesTheDumpAndTheParserHearsIt()
    {
        var ev = Assert.IsType<OutputfileEvent>(LogParser.Parse(RealLine));
        Assert.Equal("Dranak_freeport-Inventory.txt", ev.FileName);
        Assert.Equal(new DateTime(2026, 8, 20, 18, 47, 36), ev.Time);
    }

    [Theory]
    [InlineData("Dranak_freeport-Inventory.txt", OutputfileKind.Inventory)]
    [InlineData("Hugzee_qeynos-Inventory.txt", OutputfileKind.Inventory)]
    [InlineData("Dranak_freeport-Achievements.txt", OutputfileKind.Achievements)]
    // Case varies across servers in the filenames we HAVE seen, so the match is
    // case-insensitive rather than trusting one capitalisation.
    [InlineData("dranak_freeport-inventory.txt", OutputfileKind.Inventory)]
    // A dump EQBuddy has no reader for is named as such, not silently treated as one of
    // the two it does read — guessing here would apply the wrong importer to a real file.
    [InlineData("Dranak_freeport-Spellbook.txt", OutputfileKind.Unknown)]
    [InlineData("", OutputfileKind.Unknown)]
    public void TheFilenameDecidesWhichImporterRuns(string file, OutputfileKind expected) =>
        Assert.Equal(expected, OutputfileAutoImport.KindOf(file));

    /// <summary>The dump lives beside the game's own folders — the Logs folder's PARENT.
    /// This is the fact the UI used to make the player supply by hand.</summary>
    [Fact]
    public void TheDumpIsFoundFromTheLogFolderAlone()
    {
        var game = Directory.CreateTempSubdirectory("eqb-outputfile");
        try
        {
            var logs = Directory.CreateDirectory(Path.Combine(game.FullName, "Logs"));
            var dump = Path.Combine(game.FullName, "Dranak_freeport-Inventory.txt");
            File.WriteAllText(dump, "");

            Assert.Equal(dump, OutputfileAutoImport.ResolvePath(logs.FullName, "Dranak_freeport-Inventory.txt"));
            // A name for a file that is not there resolves to nothing rather than to a
            // path that will throw on open one frame later.
            Assert.Null(OutputfileAutoImport.ResolvePath(logs.FullName, "Nobody_nowhere-Inventory.txt"));
            // The log prints a bare name. Anything with a path in it was not what we were
            // told about, and must not be able to walk out of the game folder.
            Assert.Null(OutputfileAutoImport.ResolvePath(logs.FullName, @"..\..\Windows\System32\config\SAM"));
            Assert.Null(OutputfileAutoImport.ResolvePath(null, "Dranak_freeport-Inventory.txt"));
        }
        finally { game.Delete(true); }
    }

    [Fact]
    public void AnInventoryDumpTicksWhatYouOwnAndTheUndoPutsBackOnlyThat()
    {
        var settings = new AppSettings
        {
            GearChecklist =
            [
                new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi" },
                new GearChecklistItem { Slot = "HANDS", Item = "Gloves of Dark Embers" },
                // Ticked by the PLAYER before the import. The undo must not touch it —
                // an undo that restores a whole snapshot would silently revert this.
                new GearChecklistItem { Slot = "NECK", Item = "Silver Chain of Dread", Acquired = true },
            ],
        };
        var dump = new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt", new DateTime(2026, 8, 20, 18, 47, 0), [])
        {
            Entries =
            [
                new InventoryFile.Entry("General1", "Crown of Narandi", 1),
                new InventoryFile.Entry("Bank1", "Gloves of Dark Embers", 1),
            ],
        };

        var outcome = OutputfileAutoImport.ImportInventory(dump, settings);

        Assert.Equal(2, outcome.GearTicked);
        Assert.All(settings.GearChecklist, i => Assert.True(i.Acquired));
        Assert.Contains("2 items ticked", outcome.Summary);
        Assert.Contains("18:47", outcome.Summary);

        Assert.NotNull(outcome.Undo);
        outcome.Undo!();
        Assert.False(settings.GearChecklist[0].Acquired);
        Assert.False(settings.GearChecklist[1].Acquired);
        Assert.True(settings.GearChecklist[2].Acquired);   // the player's own tick survives
    }

    /// <summary>"EQBuddy did nothing" and "EQBuddy never saw your file" look identical from
    /// the outside, and only one of them is a bug. David hit exactly that: he ran the
    /// command with an EMPTY checklist, so even a working import would have had nothing to
    /// show — and the surface said nothing either way. The report always says the dump was
    /// READ, and offers no undo when there is nothing to undo.</summary>
    [Fact]
    public void ADumpWithNothingToTickStillSaysItWasRead()
    {
        var settings = new AppSettings { GearChecklist = [] };
        var dump = new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt", new DateTime(2026, 8, 20, 18, 47, 0), [])
        { Entries = [new InventoryFile.Entry("General1", "Crown of Narandi", 1)] };

        var outcome = OutputfileAutoImport.ImportInventory(dump, settings);

        Assert.Equal(0, outcome.Applied);
        Assert.Contains("Read your inventory dump", outcome.Summary);
        Assert.Contains("nothing new to tick", outcome.Summary);
        Assert.Null(outcome.Undo);
    }

    [Fact]
    public void AnAchievementsDumpMarksClearsAndTheUndoLeavesWitnessedKillsAlone()
    {
        var dir = Directory.CreateTempSubdirectory("eqb-ach");
        try
        {
            var path = Path.Combine(dir.FullName, "Dranak_freeport-Achievements.txt");
            // The dump's REAL shape, read off AchievementsImport.Parse rather than
            // invented: a bare section line, then "C<tab>achievement", then
            // "C<tab><tab>criterion". The first draft of this fixture put the flag at the
            // END of the row, which parses as three section headings and marks nothing —
            // a real state, so the test failed for the right reason and not the obvious
            // one (trap 23: a fixture in the wrong shape renders something real).
            File.WriteAllLines(path,
            [
                "EverQuest: Raids",
                "C\tConqueror of Nagafen's Lair",
                "C\t\tLord Nagafen",
            ]);
            var ledger = new RaidKillLedger(Path.Combine(dir.FullName, "raid-kills.json"))
            { CharacterKey = () => "dranak_freeport" };
            // A kill the LOG witnessed. Nothing an import does may take this away.
            ledger.Apply(new KillEvent(new DateTime(2026, 8, 20, 12, 0, 0), "Phinigel Autropos", "you"));

            var settings = new AppSettings();
            var outcome = OutputfileAutoImport.ImportAchievements(path, settings, ledger);

            Assert.Equal(1, outcome.RaidsMarked);
            Assert.True(ledger.For("Lord Nagafen")!.AchievementComplete);
            Assert.Contains("1 raid clear", outcome.Summary);

            outcome.Undo!();
            Assert.False(ledger.For("Lord Nagafen")?.AchievementComplete ?? false);
            Assert.True(ledger.For("Phinigel Autropos")!.Kills > 0);   // the witnessed kill stands
        }
        finally { dir.Delete(true); }
    }

    /// <summary>Re-reading the SAME dump twice must not double-count or hand back a second
    /// undo — the log announces a write, and a player who types the command twice gets two
    /// announcements for what may be the same content.</summary>
    [Fact]
    public void ImportingTheSameDumpAgainAppliesNothingFurther()
    {
        var settings = new AppSettings
        {
            GearChecklist = [new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi" }],
        };
        var dump = new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt", new DateTime(2026, 8, 20, 18, 47, 0), [])
        { Entries = [new InventoryFile.Entry("General1", "Crown of Narandi", 1)] };

        Assert.Equal(1, OutputfileAutoImport.ImportInventory(dump, settings).GearTicked);
        var again = OutputfileAutoImport.ImportInventory(dump, settings);
        Assert.Equal(0, again.GearTicked);
        Assert.Null(again.Undo);
    }
}
