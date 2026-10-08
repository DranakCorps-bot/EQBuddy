using System.Diagnostics;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE CHARACTER CARD WRITER, D2 ACCEPTANCE** (DRA-1288, <c>docs/plans/DRA-1288.md</c>;
/// card DRA-1472). <see cref="CharacterCardWriter"/> is where every decision the app's runtime
/// makes about the file lives, so it is driven here with a STATED clock and real stores — a
/// ledger file, a SQLite session store, a Sky/Epic binding — inside a temp directory.
///
/// <para>One test per acceptance line: off means off, no churn, switch, delete, isolation; plus
/// the interval (decision 2), the relaunch baseline and "Write it now". The torn-read line is
/// <see cref="CharacterCardTornReadTests"/>, in a collection of its own because it pins the
/// whole test process to two cores.</para>
/// </summary>
public sealed class CharacterCardWriterTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 14, 30, 0, TimeSpan.FromHours(-5));

    private readonly string _dir;
    private readonly string _folder;
    private readonly QuestLedgerStore _ledger;
    private readonly AppSettings _settings = new();
    private readonly SessionRepository _repo;
    private int _persisted;

    public CharacterCardWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "eqb-cardw-" + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_dir, CharacterCardWriter.FolderName);
        Directory.CreateDirectory(_dir);
        _ledger = new QuestLedgerStore(Path.Combine(_dir, "quest-ledger.json"));
        _repo = new SessionRepository(Path.Combine(_dir, "history.db"));
    }

    public void Dispose()
    {
        _repo.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private CharacterCardWriter Writer() => new(_folder, _settings, () => _persisted++);

    private CharacterCardSources Sources(string name, string server, GuideCatalog? guides = null,
        long activeRow = 0, IReadOnlyList<string>? inferred = null) =>
        new(name, server, _ledger, _settings, (s, c) => _repo.Query(s, c), Path.Combine(_dir, "Logs"),
            InferredClasses: inferred, Guides: guides, AppVersion: "2.0.0-test", ActiveSessionRowId: activeRow);

    private string CardPath(string name, string server) =>
        Path.Combine(_folder, CharacterCardWriter.FileNameFor(name, server)!);

    private static string Key(string name, string server) => QuestLedgerStore.KeyFor(name, server);

    private static string LevelLine(string card) =>
        card.Split('\n').Single(l => l.StartsWith("- Level: ", StringComparison.Ordinal));

    // ---- off means off ------------------------------------------------------------------

    /// <summary>
    /// **Off means off.** With the switch off, a level change moves neither the file's bytes nor
    /// its timestamp, however many ticks go by. Turned back on, the next tick — inside the
    /// interval — writes the new level.
    /// Prove-fail: drop the <c>CharacterCardEnabled</c> check from <c>Tick</c> and the file moves.
    /// </summary>
    [Fact]
    public void OffMeansOffAndOnWritesTheChangeAtTheNextTick()
    {
        var key = Key("Dranak", "freeport");
        _ledger.SetLevel(key, 40, new DateTime(2026, 10, 1));
        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), ready: true, T0));
        var path = CardPath("Dranak", "freeport");
        Assert.Contains("Level 40", LevelLine(File.ReadAllText(path)), StringComparison.Ordinal);

        _settings.CharacterCardEnabled = false;
        var bytes = File.ReadAllBytes(path);
        var stamp = File.GetLastWriteTimeUtc(path);
        _ledger.SetLevel(key, 41, new DateTime(2026, 10, 2));
        for (var s = 10; s <= 120; s += 10)
            Assert.False(writer.Tick(Sources("Dranak", "freeport"), ready: true, T0.AddSeconds(s)));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(1, writer.Writes);

        _settings.CharacterCardEnabled = true;
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), ready: true, T0.AddSeconds(121)));
        Assert.Contains("Level 41", LevelLine(File.ReadAllText(path)), StringComparison.Ordinal);
    }

    /// <summary>The host is not ready (an archived log under review, or the live log still
    /// replaying): nothing is written, switch on or not.</summary>
    [Fact]
    public void NothingIsWrittenWhileTheHostIsNotReady()
    {
        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        Assert.False(writer.Tick(Sources("Dranak", "freeport"), ready: false, T0));
        Assert.False(writer.Switching(Sources("Dranak", "freeport"), ready: false, T0));
        Assert.False(File.Exists(CardPath("Dranak", "freeport")));
        Assert.Equal(CharacterCardWriter.NowOutcome.Reviewing,
            writer.WriteNow(Sources("Dranak", "freeport"), reviewing: true, T0));
        Assert.False(File.Exists(CardPath("Dranak", "freeport")));
    }

    // ---- the interval (decision 2) --------------------------------------------------------

    /// <summary>
    /// **Never more than once per five seconds, and a change is written within five.** A change
    /// one second after a write waits for the interval, then lands on the first tick it allows.
    /// And an unchanged character is not rewritten however long it sits (decision 3: only the
    /// written-at line moved).
    /// </summary>
    [Fact]
    public void AChangeWaitsForTheIntervalAndAnUnchangedCardIsNeverRewritten()
    {
        var key = Key("Dranak", "freeport");
        _ledger.SetLevel(key, 40, new DateTime(2026, 10, 1));
        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), true, T0));

        _ledger.SetLevel(key, 41, new DateTime(2026, 10, 2));
        for (var s = 1; s < 5; s++)
            Assert.False(writer.Tick(Sources("Dranak", "freeport"), true, T0.AddSeconds(s)));
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), true, T0.AddSeconds(5)));
        Assert.Equal(2, writer.Writes);

        for (var s = 6; s < 600; s += 7)
            Assert.False(writer.Tick(Sources("Dranak", "freeport"), true, T0.AddSeconds(s)));
        Assert.Equal(2, writer.Writes);
    }

    /// <summary>A relaunch over an unchanged character writes nothing: what is on disk is the
    /// baseline, not "never written".</summary>
    [Fact]
    public void ARelaunchOverAnUnchangedCharacterWritesNothing()
    {
        _settings.CharacterCardEnabled = true;
        Assert.True(Writer().Tick(Sources("Dranak", "freeport"), true, T0));
        var relaunched = Writer();
        Assert.False(relaunched.Tick(Sources("Dranak", "freeport"), true, T0.AddHours(3)));
        Assert.Equal(0, relaunched.Writes);
    }

    // ---- no churn -------------------------------------------------------------------------

    /// <summary>
    /// **No churn: fifty combat lines with no kill, no loot and no level write nothing** —
    /// DRA-1472 binding condition 3, measured rather than argued. The live session is
    /// checkpointed into the store as an <c>Active</c> row every ten lines, exactly the row the
    /// archiver keeps moving, and the clock advances past the interval on every tick. The
    /// character has a FINISHED session too, so "Your evidence" is a populated section the
    /// active row could leak into.
    /// <para>Prove-fail: fold "Your evidence" over every stored row (the active one included, as
    /// <c>HelperSources.Read</c> does) and the hours move at each checkpoint — measured at 5
    /// writes in place of 0.</para>
    /// </summary>
    [Fact]
    public void FiftyCombatLinesWithNoKillLootOrLevelWriteNothing()
    {
        // A finished session in the zone the live one is in.
        var earlier = new SessionStats { CharacterName = "Dranak", ServerName = "freeport" };
        Apply(earlier, $"{Stamp(10, 0, 0)} You have entered Befallen.");
        for (var i = 0; i < 30; i++)
        {
            Apply(earlier, $"{Stamp(10, i, 0)} You hit a skeleton for 10 points of damage.");
            Apply(earlier, $"{Stamp(10, i, 30)} You have slain a skeleton!");
        }
        _repo.Checkpoint(0, earlier.Snapshot(), "freeport", "Dranak", "Exit");

        var live = new SessionStats { CharacterName = "Dranak", ServerName = "freeport" };
        Apply(live, $"{Stamp(12, 0, 0)} You have entered Befallen.");
        Apply(live, $"{Stamp(12, 0, 5)} You hit a skeleton for 10 points of damage.");
        Apply(live, $"{Stamp(12, 0, 10)} You have slain a skeleton!");
        var active = _repo.Checkpoint(0, live.Snapshot(), "freeport", "Dranak", SessionRepository.ActiveEndReason);

        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        var now = T0;
        Assert.True(writer.Tick(Sources("Dranak", "freeport", activeRow: active, inferred: live.Snapshot().InferredClasses), true, now));
        var first = File.ReadAllText(CardPath("Dranak", "freeport"));
        Assert.Contains("| Befallen |", first, StringComparison.Ordinal);   // the evidence row exists

        for (var line = 1; line <= 50; line++)
        {
            Apply(live, $"{Stamp(12, 1 + line / 6, line % 6 * 10)} You hit a skeleton for 10 points of damage.");
            if (line % 10 == 0)
                active = _repo.Checkpoint(active, live.Snapshot(), "freeport", "Dranak", SessionRepository.ActiveEndReason);
            now = now.AddSeconds(6);
            writer.Tick(Sources("Dranak", "freeport", activeRow: active, inferred: live.Snapshot().InferredClasses), true, now);
        }

        Assert.True(writer.Writes == 1,
            $"{writer.Writes - 1} writes from combat lines that changed nothing about the character");
    }

    private static void Apply(SessionStats stats, string line)
    {
        if (LogParser.Parse(line) is { } e) stats.Apply(e);
    }

    private static string Stamp(int day, int minute, int second) =>
        new DateTime(2026, 7, day, 18, minute, second).ToString(
            "[ddd MMM dd HH:mm:ss yyyy]", System.Globalization.CultureInfo.InvariantCulture);

    // ---- switch ---------------------------------------------------------------------------

    /// <summary>
    /// **Switch: the outgoing card is written with the OUTGOING character's guide progress,
    /// then stops changing** — DRA-1472 binding condition 2. Guide progress counts the BOUND
    /// character's Sky/Epic working copy (<see cref="QuestTickBinding"/>), so this runs the host's
    /// order with a real binding: Dranak turns a Sky reward in, the switch fires inside the
    /// interval, THEN the binding moves to Hugzee.
    /// <para>Prove-fail (both measured): <c>Switching</c> honouring the interval leaves Dranak's
    /// file at "1 of 2"; and moving the binding BEFORE <c>Switching</c> — the order the host must
    /// not take — writes Hugzee's working copy, "1 of 2", into Dranak's file.</para>
    /// </summary>
    [Fact]
    public void ASwitchWritesTheOutgoingCardFromItsOwnBindingAndThenLeavesItAlone()
    {
        const string Reward = "Wind Rune Reward";
        var guides = new GuideCatalog
        {
            Guides =
            [
                new Guide
                {
                    Id = "sky", Name = "Sky Guide",
                    Stages =
                    [
                        new GuideStage
                        {
                            Id = "s",
                            Objectives = [new GuideObjective { Id = "turnin", RewardKey = Reward }, new GuideObjective { Id = "o2" }],
                        },
                    ],
                },
            ],
        };
        var dranak = Key("Dranak", "freeport");
        var hugzee = Key("Hugzee", "freeport");
        _ledger.SetObjectiveDone(dranak, "sky", "o2", true);
        _ledger.SetObjectiveDone(hugzee, "sky", "o2", true);
        var binding = new QuestTickBinding(_settings, _ledger, Path.Combine(_dir, "no-sidecar.json"));
        binding.Bind(dranak);

        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        Assert.True(writer.Tick(Sources("Dranak", "freeport", guides), true, T0));
        Assert.Contains("| Sky Guide | 1 | 2 |", File.ReadAllText(CardPath("Dranak", "freeport")), StringComparison.Ordinal);

        // Dranak turns the reward in, one second after the last write — inside the interval.
        _settings.SkyQuestCompleted.Add(Reward);

        // The host's order: the card first, then the binding.
        Assert.True(writer.Switching(Sources("Dranak", "freeport", guides), true, T0.AddSeconds(1)));
        binding.Bind(hugzee);
        Assert.True(writer.Tick(Sources("Hugzee", "freeport", guides), true, T0.AddSeconds(2)));

        var outgoing = CardPath("Dranak", "freeport");
        Assert.Contains("| Sky Guide | 2 | 2 |", File.ReadAllText(outgoing), StringComparison.Ordinal);
        Assert.Contains("| Sky Guide | 1 | 2 |", File.ReadAllText(CardPath("Hugzee", "freeport")), StringComparison.Ordinal);

        // The old file stops changing: Dranak's stores move, ticks keep coming for Hugzee.
        var bytes = File.ReadAllBytes(outgoing);
        _ledger.SetLevel(dranak, 60, new DateTime(2026, 10, 5));
        for (var s = 10; s <= 60; s += 10)
            writer.Tick(Sources("Hugzee", "freeport", guides), true, T0.AddSeconds(s));
        Assert.Equal(bytes, File.ReadAllBytes(outgoing));
        Assert.Equal(CardPath("Hugzee", "freeport"), writer.CurrentPath);
    }

    /// <summary>A switch while the card is off writes nothing.</summary>
    [Fact]
    public void ASwitchWhileOffWritesNothing()
    {
        var writer = Writer();
        Assert.False(writer.Switching(Sources("Dranak", "freeport"), true, T0));
        Assert.False(Directory.Exists(_folder));
    }

    // ---- write it now + delete -----------------------------------------------------------

    /// <summary>"Write it now" writes while the switch is off, and again when nothing changed:
    /// the press is the reason. It says where, through <see cref="CharacterCardWriter.CurrentPath"/>.</summary>
    [Fact]
    public void WriteItNowWritesWhileOffAndWhenNothingChanged()
    {
        var writer = Writer();
        Assert.Equal(CharacterCardWriter.NowOutcome.NoCharacter, writer.WriteNow(Sources("", "freeport"), false, T0));
        Assert.Equal(CharacterCardWriter.NowOutcome.Written, writer.WriteNow(Sources("Dranak", "freeport"), false, T0));
        Assert.Equal(CharacterCardWriter.NowOutcome.Written, writer.WriteNow(Sources("Dranak", "freeport"), false, T0.AddSeconds(1)));
        Assert.Equal(2, writer.Writes);
        Assert.Equal(CardPath("Dranak", "freeport"), writer.CurrentPath);
        Assert.Equal(["Dranak (freeport).md"], _settings.CharacterCardFiles);
        Assert.Equal(1, _persisted);   // recorded once, saved once
    }

    /// <summary>
    /// **Delete removes only the names the writer recorded** (decision 4; DRA-1472 binding
    /// condition 1). Beside two written cards the folder holds a player's own <c>notes.md</c>,
    /// a file that LOOKS like a card (<c>Zed (freeport).md</c>) but was never written by
    /// EQBuddy, and settings.json carries a hand-edited name pointing out of the folder. Only
    /// the two cards go.
    /// <para>Prove-fail: delete by the writer's name pattern (<c>* (*).md</c>) and
    /// <c>Zed (freeport).md</c> is gone; drop the bare-name rule and the file outside the
    /// folder is gone.</para>
    /// </summary>
    [Fact]
    public void DeleteRemovesOnlyTheFilesTheWriterRecorded()
    {
        var writer = Writer();
        writer.WriteNow(Sources("Dranak", "freeport"), false, T0);
        writer.WriteNow(Sources("Hugzee", "qeynos"), false, T0);
        var notes = Path.Combine(_folder, "notes.md");
        var lookalike = Path.Combine(_folder, "Zed (freeport).md");
        var outside = Path.Combine(_dir, "outside.md");
        File.WriteAllText(notes, "# my own notes");
        File.WriteAllText(lookalike, "# not EQBuddy's");
        File.WriteAllText(outside, "# above the folder");
        _settings.CharacterCardFiles.Add(Path.Combine("..", "outside.md"));

        var deleted = writer.Delete();

        Assert.Equal(2, deleted);
        Assert.False(File.Exists(CardPath("Dranak", "freeport")));
        Assert.False(File.Exists(CardPath("Hugzee", "qeynos")));
        Assert.True(File.Exists(notes), "the player's own file was deleted");
        Assert.True(File.Exists(lookalike), "a file EQBuddy never wrote was deleted for looking like a card");
        Assert.True(File.Exists(outside), "a recorded name reached outside the cards folder");
        Assert.Empty(_settings.CharacterCardFiles);
    }

    /// <summary>After a delete with the switch on, the current character's card comes back on
    /// the next tick — deleting is not turning off, and the words say so.</summary>
    [Fact]
    public void AfterADeleteTheSwitchOnWritesTheCardAgain()
    {
        _settings.CharacterCardEnabled = true;
        var writer = Writer();
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), true, T0));
        Assert.Equal(1, writer.Delete());
        Assert.True(writer.Tick(Sources("Dranak", "freeport"), true, T0.AddSeconds(1)));
        Assert.True(File.Exists(CardPath("Dranak", "freeport")));
        Assert.EndsWith("will be written again.", CharacterCardCopy.Deleted(1, stillOn: true), StringComparison.Ordinal);
        Assert.DoesNotContain("again", CharacterCardCopy.Deleted(1, stillOn: false), StringComparison.Ordinal);
    }

    // ---- where -----------------------------------------------------------------------------

    /// <summary>The file name is decision 1's, and a character a file name cannot hold is
    /// replaced rather than trusted.</summary>
    [Fact]
    public void TheFileIsNamedForTheCharacterAndServer()
    {
        Assert.Equal("Dranak (freeport).md", CharacterCardWriter.FileNameFor("Dranak", "freeport"));
        Assert.Equal("a_b (c_d).md", CharacterCardWriter.FileNameFor("a/b", "c:d"));
        Assert.Null(CharacterCardWriter.FileNameFor("", "freeport"));
        Assert.Null(CharacterCardWriter.FileNameFor("Dranak", ""));
    }

    /// <summary>
    /// **Isolation: under tests the cards folder is inside the redirected profile** (trap 68).
    /// The folder is a child of <c>AppPaths.Dir</c>, which <c>TestProfileIsolation</c> points
    /// at a throwaway — so a test, or a capture, can never write a card into a player's
    /// profile.
    /// </summary>
    [Fact]
    public void TheCardsFolderIsInsideTheRedirectedProfile()
    {
        var folder = Path.GetFullPath(CharacterCardWriter.DefaultFolder);
        var profile = Path.GetFullPath(AppPaths.Dir);
        Assert.Equal(Path.Combine(profile, CharacterCardWriter.FolderName), folder, StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "eqbuddy-tests")), folder,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(folder.StartsWith(Path.GetFullPath(AppPaths.ProductDir), StringComparison.OrdinalIgnoreCase),
            $"the cards folder {folder} is inside the live Evolved profile");
    }
}

/// <summary>Serial: the pinned run sets the WHOLE test process's processor affinity, which
/// would slow every test running beside it.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CharacterCardTornReadCollection
{
    public const string Name = "character card torn read";
}

/// <summary>
/// **Torn read: an app reading the card while the writer rewrites it sees a whole card or the
/// previous whole card, never half of one** (DRA-1288 D2). A reader thread loops on the file
/// while <see cref="CharacterCardWriter.Tick"/> republishes it with a different version line each
/// time. Asserted on the FACT the atomic path leaves — <c>AtomicRename.Renames</c> advanced once
/// per write after the first — never on a returned outcome (trap 84). Run pinned to two cores,
/// the CI box's shape and the only one where the window shows (trap 77), and unpinned.
/// <para>Prove-fail: publish with <c>File.WriteAllText</c> in place of
/// <c>WholeFilePublish.Write</c> and the rename count stays put and partial reads appear.</para>
/// </summary>
[Collection(CharacterCardTornReadCollection.Name)]
public sealed class CharacterCardTornReadTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("eqb-cardtorn-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReaderNeverSeesHalfACard(bool pinnedToTwoCores)
    {
        var process = Process.GetCurrentProcess();
        var affinity = OperatingSystem.IsWindows() ? process.ProcessorAffinity : IntPtr.Zero;
        var pinned = pinnedToTwoCores && OperatingSystem.IsWindows() && Environment.ProcessorCount > 2;
        if (pinned)
        {
            process.ProcessorAffinity = 0b11;
            Thread.Sleep(250);   // let the scheduler move the threads before measuring
        }
        try { Measure(); }
        finally
        {
            if (pinned) process.ProcessorAffinity = affinity;
        }
    }

    private void Measure()
    {
        const int Publishes = 1500;
        var settings = new AppSettings { CharacterCardEnabled = true };
        var folder = Path.Combine(_dir, CharacterCardWriter.FolderName);
        var writer = new CharacterCardWriter(folder, settings, () => { });
        CharacterCardSources Card(int i) => new("Dranak", "freeport", AppVersion: $"2.0.{i}");
        var t0 = new DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.Zero);

        // The first publish creates the file; every later one replaces a live name.
        Assert.True(writer.Tick(Card(0), true, t0));
        var path = writer.CurrentPath!;
        var renames = AtomicRename.Renames;

        var published = new HashSet<string>(StringComparer.Ordinal) { File.ReadAllText(path) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reads = 0;
        var refused = 0;
        using var done = new CancellationTokenSource();
        using var running = new ManualResetEventSlim(false);
        var reader = Task.Factory.StartNew(() =>
        {
            void ReadOnce()
            {
                var text = WholeFilePublish.Read(path, out var outcome);
                reads++;
                // Every distinct text is kept and checked against the published set AFTER the
                // run, so the order the two threads happen to take cannot make a read look whole.
                if (outcome != WholeFilePublish.ReadOutcome.Read) refused++;
                else seen.Add(text);
            }
            ReadOnce();
            running.Set();
            while (!done.IsCancellationRequested) ReadOnce();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(running.Wait(TimeSpan.FromSeconds(30)), "the reader never completed a read");

        for (var i = 1; i <= Publishes; i++)
            if (writer.Tick(Card(i), true, t0.AddSeconds(5 * i)))
                published.Add(File.ReadAllText(path));
        done.Cancel();
        reader.Wait();

        var notWhole = seen.Where(r => !published.Contains(r)).ToList();
        Assert.True(reads > 0, "the reader never ran, so this proved nothing");
        Assert.True(refused == 0 && notWhole.Count == 0,
            $"{reads} reads: {refused} found no file to read, {notWhole.Count} distinct texts were not a "
            + "whole published card" + (notWhole.Count > 0 ? $" (first is {notWhole[0].Length} chars)" : ""));
        // The fact the atomic path leaves (trap 84): one rename over the live name per write
        // after the first. A writer that went back to truncate-and-fill renames nothing.
        Assert.Equal(writer.Writes - 1, AtomicRename.Renames - renames);
        Assert.True(writer.Writes > Publishes * 99 / 100,
            $"only {writer.Writes} of {Publishes + 1} publishes landed — the writer bought its clean reads by going quiet");
    }
}
