using System.Text.Json;
using System.Text.Json.Nodes;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-47 (Delivery 2 N3): the Sky and Epic ticks leave the per-PROFILE settings file for the
/// per-CHARACTER quest ledger. Three claims, each with its own test group:
///
/// <list type="number">
/// <item><b>The drain</b> (<see cref="QuestTickMigration.Drain"/>) — the section is copied to
/// its <c>.bak</c> BEFORE it leaves settings.json, the settings file shrinks by exactly that
/// section and nothing else, and draining twice is draining once.</item>
/// <item><b>The adoption</b> — every character, on its first bind, sees exactly what it saw
/// before the upgrade; and never again after that, or a box the player cleared would come
/// back.</item>
/// <item><b>The binding</b> (<see cref="QuestTickBinding"/>) — a tick lands on the character
/// it was made on, reaches the ledger on the <c>Save()</c> every writer already calls, and
/// survives a restart.</item>
/// </list>
///
/// [Collection] because the drain test writes settings.json on the assembly's throwaway
/// profile, and <c>AppSettings.Load(</c> is one of the writers that guard names.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public sealed class QuestTickMigrationTests : IDisposable
{
    private const string Main = "dranak_legends";
    private const string Alt = "altoid_legends";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"eqb-ticks-{Guid.NewGuid():N}");
    private string LedgerPath => Path.Combine(_dir, "quest-ledger.json");
    private string Sidecar => Path.Combine(_dir, QuestTickMigration.FileName);
    private static string SettingsPath => AppPaths.File("settings.json");

    public QuestTickMigrationTests()
    {
        Directory.CreateDirectory(_dir);
        DeleteSettings();
    }

    public void Dispose()
    {
        DeleteSettings();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static void DeleteSettings()
    {
        foreach (var f in new[] { SettingsPath, SettingsPath + ".bak" })
            try { File.Delete(f); } catch { /* best effort */ }
    }

    /// <summary>A pre-DRA-47 profile's tick section, in the shape settings.json carried it:
    /// one Sky piece ticked, one parked *, one reward turned in, one Epic row ticked and one
    /// class marked complete with its undo snapshot.</summary>
    private static AppSettings LegacyProfile()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);   // the rows, from the shipped defaults
        var sky = settings.SkyQuestChecklist.Select(i => i.Clone()).ToList();
        sky.Single(i => i.Id == "sky-198").Acquired = true;
        var guessed = sky.Single(i => i.Id == "sky-007");
        guessed.Acquired = true;
        guessed.AcquiredUnassigned = true;
        var epic = settings.EpicQuestChecklist.Select(i => i.Clone()).ToList();
        epic[0].Acquired = true;
        settings.LegacySkyQuestChecklist = sky;
        settings.LegacySkyQuestCompleted = ["Warrior|Azure Ruby Ring"];
        settings.LegacyEpicQuestChecklist = epic;
        settings.LegacyEpicQuestCompleted = [epic[0].ClassName];
        settings.LegacyEpicQuestPreCompleteAcquired = new() { [epic[0].ClassName] = [] };
        return settings;
    }

    private static string FirstEpicId(AppSettings s) => s.EpicQuestChecklist[0].Id;

    private QuestLedgerStore Ledger() => new(LedgerPath);

    private static bool Ticked(AppSettings s, string skyId) =>
        s.SkyQuestChecklist.Single(i => i.Id == skyId).Acquired;

    // ---- 1. the drain ----

    /// <summary>The plan's own words: "the profile file is smaller by exactly the drained
    /// section". Through the REAL Load and Save, against a file on disk — every other key in
    /// settings.json is byte-for-byte what it was, and the five tick keys are gone.</summary>
    [Fact]
    public void TheSettingsFileLosesExactlyTheDrainedSectionAndNothingElse()
    {
        var stored = LegacyProfile();
        stored.Theme = "Solarized";   // any other setting — it must survive untouched
        stored.Save();

        var settings = AppSettings.Load();
        Assert.True(settings.HasLegacyQuestTicks);
        var before = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

        Assert.True(QuestTickMigration.Drain(settings, Sidecar));

        var after = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        string[] drained = ["SkyQuestChecklist", "SkyQuestCompleted", "EpicQuestChecklist",
            "EpicQuestCompleted", "EpicQuestPreCompleteAcquired"];
        foreach (var key in drained)
        {
            Assert.True(before.ContainsKey(key), $"the stored profile should have carried {key}");
            Assert.False(after.ContainsKey(key), $"{key} should have left settings.json");
        }
        foreach (var drop in drained) before.Remove(drop);
        Assert.Equal(before.ToJsonString(), after.ToJsonString());
        Assert.Equal("Solarized", AppSettings.Load().Theme);
        Assert.False(AppSettings.Load().HasLegacyQuestTicks);
    }

    /// <summary>The .bak carries the section VERBATIM — the names and shapes settings.json
    /// used — and it is written before the section is removed.</summary>
    [Fact]
    public void TheBackupHoldsTheSectionVerbatim()
    {
        var settings = LegacyProfile();
        var epicClass = settings.LegacyEpicQuestChecklist![0].ClassName;

        QuestTickMigration.Drain(settings, Sidecar, save: false);

        var bak = QuestTickMigration.Read(Sidecar)!;
        Assert.True(bak.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired);
        Assert.True(bak.SkyQuestChecklist.Single(i => i.Id == "sky-007").AcquiredUnassigned);
        Assert.Equal(["Warrior|Azure Ruby Ring"], bak.SkyQuestCompleted);
        Assert.Equal([epicClass], bak.EpicQuestCompleted);
        Assert.True(bak.EpicQuestPreCompleteAcquired.ContainsKey(epicClass));
        Assert.False(settings.HasLegacyQuestTicks);
    }

    /// <summary>Trap 65: a backup that cannot be written leaves the section where it is. A
    /// directory standing where the file should go makes the write fail for real.</summary>
    [Fact]
    public void ABackupThatCannotBeWrittenLeavesTheSectionInPlace()
    {
        var settings = LegacyProfile();
        Directory.CreateDirectory(Sidecar);   // the path is a directory: the write throws

        Assert.False(QuestTickMigration.Drain(settings, Sidecar, save: false));
        Assert.True(settings.HasLegacyQuestTicks);
    }

    /// <summary>A kill between the two writes leaves the section in BOTH files; the next
    /// launch drains it again. A later build writing the section back (a downgrade) does the
    /// same with different ticks. Either way the backup is a union: nothing ticked in either
    /// drain is lost, and draining twice is draining once.</summary>
    [Fact]
    public void DrainingTwiceUnionsAndLosesNothing()
    {
        var first = LegacyProfile();
        QuestTickMigration.Drain(first, Sidecar, save: false);
        var once = File.ReadAllText(Sidecar);

        var again = LegacyProfile();   // the same section, still in settings after a kill
        QuestTickMigration.Drain(again, Sidecar, save: false);
        var bak = QuestTickMigration.Read(Sidecar)!;
        Assert.Equal(
            JsonSerializer.Deserialize<QuestTickMigration.Section>(once)!.SkyQuestChecklist.Count(i => i.Acquired),
            bak.SkyQuestChecklist.Count(i => i.Acquired));

        var downgraded = LegacyProfile();
        downgraded.LegacySkyQuestChecklist!.Single(i => i.Id == "sky-198").Acquired = false;
        downgraded.LegacySkyQuestChecklist!.Single(i => i.Id == "sky-001").Acquired = true;
        downgraded.LegacySkyQuestCompleted = ["Bard|Amulet of the Fae"];
        QuestTickMigration.Drain(downgraded, Sidecar, save: false);

        bak = QuestTickMigration.Read(Sidecar)!;
        Assert.True(bak.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired);   // kept
        Assert.True(bak.SkyQuestChecklist.Single(i => i.Id == "sky-001").Acquired);   // added
        Assert.Equal(["Warrior|Azure Ruby Ring", "Bard|Amulet of the Fae"], bak.SkyQuestCompleted);
    }

    [Fact]
    public void AProfileWithNoSectionDrainsNothingAndWritesNoBackup()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);

        Assert.False(QuestTickMigration.Drain(settings, Sidecar, save: false));
        Assert.False(File.Exists(Sidecar));
    }

    // ---- 2. the adoption ----

    /// <summary>The E2E claim in unit form: a tick made before the migration is visible
    /// after it — for EVERY character, because before the upgrade every character was shown
    /// it. And once each: a box one character clears does not come back on its next bind,
    /// nor does clearing it reach the other character.</summary>
    [Fact]
    public void EveryCharacterAdoptsTheOldTicksOnceAndThenTheyDiverge()
    {
        var settings = LegacyProfile();
        QuestTickMigration.Drain(settings, Sidecar, save: false);
        var ledger = Ledger();
        var binding = new QuestTickBinding(settings, ledger, Sidecar);

        binding.Bind(Main);
        Assert.True(Ticked(settings, "sky-198"));
        Assert.True(settings.SkyQuestChecklist.Single(i => i.Id == "sky-007").AcquiredUnassigned);
        Assert.Contains("Warrior|Azure Ruby Ring", settings.SkyQuestCompleted);
        Assert.True(settings.EpicQuestChecklist.Single(i => i.Id == FirstEpicId(settings)).Acquired);
        Assert.Single(settings.EpicQuestCompleted);
        Assert.Single(settings.EpicQuestPreCompleteAcquired);

        // Main clears the box.
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = false;
        binding.Commit();

        binding.Bind(Alt);
        Assert.True(Ticked(settings, "sky-198"));   // the alt adopted it too

        binding.Bind(Main);
        Assert.False(Ticked(settings, "sky-198"));  // and Main is NOT re-adopted
    }

    /// <summary>A profile that never had a section still stamps each character adopted, so
    /// a backup that appears later (a downgrade and re-upgrade) cannot re-tick boxes a
    /// character has lived with since.</summary>
    [Fact]
    public void ACharacterBoundWithNoBackupIsStampedAdoptedAnyway()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var ledger = Ledger();
        new QuestTickBinding(settings, ledger, Sidecar).Bind(Main);

        Assert.True(ledger.TicksFor(Main).Adopted);
    }

    // ---- 3. the binding ----

    /// <summary>The per-character claim itself: a tick on one character is not a tick on
    /// another. This is the wart the whole move exists to remove.</summary>
    [Fact]
    public void ATickOnOneCharacterIsNotATickOnAnother()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var binding = new QuestTickBinding(settings, Ledger(), Sidecar);

        binding.Bind(Main);
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;
        settings.SkyQuestCompleted.Add("Warrior|Azure Ruby Ring");

        binding.Bind(Alt);
        Assert.False(Ticked(settings, "sky-198"));
        Assert.Empty(settings.SkyQuestCompleted);

        binding.Bind(Main);
        Assert.True(Ticked(settings, "sky-198"));
        Assert.Equal(["Warrior|Azure Ruby Ring"], settings.SkyQuestCompleted);
    }

    /// <summary>Every writer already ends in <c>AppSettings.Save()</c>; that is what commits
    /// the tick to the ledger. And it survives a restart: a fresh ledger read from disk, a
    /// fresh settings object, the same character — the box is ticked.</summary>
    [Fact]
    public void ASaveCommitsTheTickAndItSurvivesARestart()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var ledger = Ledger();
        var binding = new QuestTickBinding(settings, ledger, Sidecar);
        binding.Bind(Main);

        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;
        settings.EpicQuestCompleted.Add("Warrior");
        settings.Save();
        Assert.Contains("sky-198", ledger.TicksFor(Main).SkyAcquired);
        ledger.Flush();

        var restarted = new AppSettings();
        restarted.ApplyMigrations(hadFile: true);
        new QuestTickBinding(restarted, new QuestLedgerStore(LedgerPath), Sidecar).Bind(Main);
        Assert.True(Ticked(restarted, "sky-198"));
        Assert.Equal(["Warrior"], restarted.EpicQuestCompleted);
    }

    /// <summary>A reward renamed after a character's ticks were written reaches them on
    /// bind, through the same table the profile's migration uses — so the Bard's turn-in
    /// recorded under "Harmonic Spear" is still turned in.</summary>
    [Fact]
    public void ARewardRenameReachesACharactersOwnTicksOnBind()
    {
        var ledger = Ledger();
        ledger.SetTicks(Main, new QuestLedgerStore.QuestTicks
        {
            SkyCompleted = ["Bard|Harmonic Spear"],
            Adopted = true,
        });
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);

        new QuestTickBinding(settings, ledger, Sidecar).Bind(Main);

        Assert.Equal(["Bard|Spear of Harmony"], settings.SkyQuestCompleted);
        Assert.Equal(["Bard|Spear of Harmony"], ledger.TicksFor(Main).SkyCompleted);
    }

    /// <summary>A tick on a row the shipped catalog no longer has cannot be drawn, but a
    /// commit derived from the rows alone would erase it — so it is carried through.</summary>
    [Fact]
    public void ATickOnARowTheCatalogNoLongerHasIsCarriedNotErased()
    {
        var ledger = Ledger();
        ledger.SetTicks(Main, new QuestLedgerStore.QuestTicks
        {
            SkyAcquired = ["sky-999-retired", "sky-198"],
            Adopted = true,
        });
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var binding = new QuestTickBinding(settings, ledger, Sidecar);
        binding.Bind(Main);

        settings.Save();

        Assert.Contains("sky-999-retired", ledger.TicksFor(Main).SkyAcquired);
        Assert.Contains("sky-198", ledger.TicksFor(Main).SkyAcquired);
    }

    /// <summary>Ticks placed in the seconds before any character is known go to the first
    /// character bound, not nowhere.</summary>
    [Fact]
    public void TicksMadeBeforeAnyCharacterIsKnownGoToTheFirstOneBound()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var ledger = Ledger();
        var binding = new QuestTickBinding(settings, ledger, Sidecar);
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        binding.Bind(Main);

        Assert.True(Ticked(settings, "sky-198"));
        Assert.Contains("sky-198", ledger.TicksFor(Main).SkyAcquired);
    }

    /// <summary>Binding the character already bound is free and changes nothing, which is
    /// what lets the host call it every UI tick.</summary>
    [Fact]
    public void RebindingTheSameCharacterIsANoOp()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        var binding = new QuestTickBinding(settings, Ledger(), Sidecar);
        Assert.True(binding.Bind(Main));
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        Assert.False(binding.Bind(Main.ToUpperInvariant()));
        Assert.True(Ticked(settings, "sky-198"));
    }

    /// <summary>The seeded rows are rebuilt on every load and never written: counting that as
    /// a change would save settings.json on every launch of every profile.</summary>
    [Fact]
    public void SeedingTheChecklistRowsIsNotAChangeTheSettingsFileOwes()
    {
        var settings = new AppSettings();
        settings.ApplyMigrations(hadFile: true);
        Assert.NotEmpty(settings.SkyQuestChecklist);
        Assert.NotEmpty(settings.EpicQuestChecklist);

        var fresh = new AppSettings();
        fresh.ApplyMigrations(hadFile: true);
        Assert.False(fresh.ApplyMigrations(hadFile: true));
        var json = JsonSerializer.Serialize(fresh, new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        });
        Assert.DoesNotContain("\"SkyQuestChecklist\"", json);
        Assert.DoesNotContain("\"EpicQuestChecklist\"", json);
    }
}
