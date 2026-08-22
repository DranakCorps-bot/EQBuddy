using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A settings save writes the WHOLE object from a snapshot taken at load. Anything that
/// changed the file since then is reverted — every setting at once, no error, nothing on
/// screen. That is precisely how joma65's hide tick-boxes behaved in #169, and until now
/// it was indistinguishable from a bug in the saving itself.
///
/// It cannot be repaired at this layer (an AppSettings has no idea which of its two
/// hundred properties the user meant to change), so the goal here is narrower and
/// sufficient: it must stop being invisible, so the next report says which of the two it
/// is instead of leaving us guessing.
///
/// These tests write settings.json inside the throwaway profile TestProfileIsolation
/// hands every test — and that profile is ONE directory for the whole assembly, which is
/// why the collection below exists.
///
/// **This file used to claim "no other test in the suite touches that file". It was not
/// true, and saying it instead of enforcing it cost a 1-in-3 flake** in
/// <c>LoadCanBeAskedNotToPersistMigrations</c> — the guard Fable 5 asked for in the
/// v1.99.3 release review, flaky from the day it shipped. `CompanionHost` and
/// `OutputfileAutoImport` both call `settings.Save()`, their tests run in a different
/// xUnit collection, and collections run in PARALLEL: so a default settings.json landed
/// on top of the two-byte file this test had just written, and the assertion that nothing
/// wrote it failed against a real write by a real writer. Trap 34's shape exactly — a
/// comment standing in for a guard reads as coverage.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class SettingsClobberTests : IDisposable
{
    private readonly Action<object?>? _sink = CoreLog.Sink;
    private readonly List<string> _logged = [];
    private static string SettingsPath => AppPaths.File("settings.json");

    public SettingsClobberTests()
    {
        CoreLog.Sink = message => _logged.Add(message?.ToString() ?? "");
        Delete();
    }

    public void Dispose()
    {
        CoreLog.Sink = _sink;
        Delete();
    }

    private static void Delete()
    {
        try { File.Delete(SettingsPath); } catch { /* best effort */ }
    }

    /// <summary>The ordinary case: one writer, many saves, and never a word about it.</summary>
    [Fact]
    public void OurOwnRepeatedSavesAreNotAClobber()
    {
        var settings = AppSettings.Load();
        settings.HideWhenGameUnfocused = true;
        settings.Save();
        settings.HideWhenGameNotRunning = true;
        settings.Save();
        settings.Save();

        Assert.DoesNotContain(_logged, m => m.Contains("changed underneath"));
        var reloaded = AppSettings.Load();
        Assert.True(reloaded.HideWhenGameUnfocused);
        Assert.True(reloaded.HideWhenGameNotRunning);
    }

    /// <summary>Two copies of EQBuddy on one profile, in miniature: the second reads the
    /// file, the first writes it, and the second's next save takes the settings back.
    /// The revert still happens — but now it says so.</summary>
    [Fact]
    public void AForeignWriteBetweenLoadAndSaveIsReported()
    {
        var mine = AppSettings.Load();

        var theirs = AppSettings.Load();
        theirs.HideWhenGameUnfocused = true;
        theirs.HideWhenGameNotRunning = true;
        theirs.Save();

        mine.UiScale = 1.15;   // any change at all is enough to trigger the save
        mine.Save();

        Assert.Contains(_logged, m => m.Contains("changed underneath"));
        // And the report is true: the other copy's tick-boxes are gone.
        Assert.False(AppSettings.Load().HideWhenGameUnfocused);
    }

    /// <summary>Once per process. A widget saves on card toggles, drags, auto-ticks and
    /// exit; a line per save would bury the one that matters.</summary>
    [Fact]
    public void TheReportIsLoggedOnce()
    {
        var mine = AppSettings.Load();
        for (var i = 0; i < 3; i++)
        {
            var theirs = AppSettings.Load();
            theirs.WindowLeft = 100 + i;
            theirs.Save();
            mine.UiScale = 1.0 + i * 0.05;
            mine.Save();
        }

        Assert.Single(_logged, m => m.Contains("changed underneath"));
    }

    /// <summary>A profile with no settings.json yet has nothing to clobber.</summary>
    [Fact]
    public void TheVeryFirstSaveIsSilent()
    {
        var fresh = AppSettings.Load();
        fresh.UiScale = 1.2;
        fresh.Save();

        Assert.DoesNotContain(_logged, m => m.Contains("changed underneath"));
    }

    /// <summary>`Load` is a READ that WRITES — it persists migrations and generated rule
    /// ids at the bottom — and a caller which has not taken the single-instance lock has
    /// to be able to say no.
    ///
    /// That caller is `--textprobe`, a diagnostic you run WITH the widget already up, so
    /// it deliberately skips the lock. The executor told Fable 5 it was safe "because Load
    /// never saves"; Fable read the code and it does. Narrow — it needs a profile with a
    /// pending migration, i.e. an upgrade in progress — and it is trap 13 to the letter:
    /// a second writer saving a whole-file snapshot under a live widget.</summary>
    [Fact]
    public void LoadCanBeAskedNotToPersistMigrations()
    {
        // A settings file old enough to need a migration on read.
        File.WriteAllText(SettingsPath, "{}");
        var before = File.GetLastWriteTimeUtc(SettingsPath);
        var bytesBefore = File.ReadAllBytes(SettingsPath).Length;

        var probe = AppSettings.Load(persistMigrations: false);
        Assert.NotNull(probe);
        Assert.Equal(bytesBefore, File.ReadAllBytes(SettingsPath).Length);
        Assert.Equal(before, File.GetLastWriteTimeUtc(SettingsPath));

        // …and the ordinary path still persists, or ids would be re-rolled every launch.
        AppSettings.Load();
        Assert.True(File.ReadAllBytes(SettingsPath).Length > bytesBefore,
            "the normal Load must still write its migrations");
    }
}
