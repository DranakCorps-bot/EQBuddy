using System.Text;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Every republish reset David's Evolved settings, and this is the mechanism.**
///
/// `File.WriteAllText` truncates the real file and writes into it without waiting for the
/// disk. `install-local.ps1 -Evolved` force-kills the running portable copy when it has not
/// closed within 15 seconds — so a republish lands in that window, and NTFS keeps the new
/// LENGTH while the contents are still zeros. The file then reads back as
/// <c>'0x00' is an invalid start of a value</c>, which is not a hypothesis: David's
/// `%AppData%\EQBuddy Evolved\error.log` threw exactly that from `AppSettings.Load`,
/// `AaLedgerStore`, `StackingLedgerStore`, `QuestLedgerStore` and `SpawnCycleLedger` at
/// **one timestamp**, 2026-09-07 06:59:54.
///
/// `Load` caught it, started from defaults and reported `hadFile: false` — so the theme
/// went back to ParchmentBrass, `ApplyDefaultRules` re-seeded the built-in Watch rule
/// against a `DefaultRulesVersion` that had gone back to 0, and the Watch card came back
/// with an empty `HiddenSections`. Then the migration chain reported work and `Load`
/// **saved those defaults over the corrupt file**, which was the last copy of the profile.
///
/// The reported symptom is three settings at once, so the test asserts all three: it is the
/// combination, not any one of them, that says "this profile was read as brand new".
///
/// Same collection as <see cref="SettingsClobberTests"/>: the throwaway profile is ONE
/// directory for the whole assembly, and these tests write settings.json in it.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class ProfileJsonTornWriteTests : IDisposable
{
    private static string SettingsPath => AppPaths.File("settings.json");
    private static string Scratch => AppPaths.File("profile-json-scratch.json");

    public ProfileJsonTornWriteTests() => Clean();
    public void Dispose() => Clean();

    private static void Clean()
    {
        foreach (var stem in new[] { SettingsPath, Scratch })
            foreach (var p in new[] { stem, stem + ".bak", stem + ".corrupt", stem + ".new" })
                try { File.Delete(p); } catch { /* best effort */ }
    }

    /// <summary>The kill, exactly as NTFS leaves it: right length, all zeros.</summary>
    private static void TearInPlace(string path)
    {
        var length = new FileInfo(path).Length;
        File.WriteAllBytes(path, new byte[length]);
    }

    // ---- the reported bug ---------------------------------------------------------

    /// <summary>
    /// David's three settings, torn the way a republish tears them, and still there on the
    /// next launch. **This is the assertion that fails on the pre-fix tree**, three times
    /// over — theme, rule and card all come back as defaults.
    /// </summary>
    [Fact]
    public void ATornSettingsFileDoesNotResetTheProfile()
    {
        var settings = AppSettings.Load();
        settings.Theme = "BlueGrey";               // not the ParchmentBrass default
        settings.TrackedRules.Clear();             // David deletes the built-in Watch rule
        if (!settings.HiddenSections.Contains("watch")) settings.HiddenSections.Add("watch");
        settings.Save();
        settings.Save();                           // the second save is what leaves a .bak

        Assert.True(File.Exists(ProfileJson.BackupPath(SettingsPath)),
            "a good save must leave the previous copy behind, or there is nothing to recover from");

        TearInPlace(SettingsPath);

        var reloaded = AppSettings.Load();
        Assert.Equal("BlueGrey", reloaded.Theme);
        Assert.Empty(reloaded.TrackedRules);
        Assert.Contains("watch", reloaded.HiddenSections);
    }

    /// <summary>
    /// The recovered profile must be treated as a REAL profile, or the migrations re-seed
    /// over the top of it and the recovery buys nothing. `DefaultRulesVersion` is the
    /// visible edge of that: a fresh profile is 0 and gets the built-in rule.
    /// </summary>
    [Fact]
    public void ARecoveredProfileIsNotTreatedAsBrandNew()
    {
        var settings = AppSettings.Load();
        settings.TrackedRules.Clear();
        settings.Save();
        settings.Save();
        var version = settings.DefaultRulesVersion;
        Assert.True(version > 0, "the profile under test has to have been migrated already");

        TearInPlace(SettingsPath);

        var reloaded = AppSettings.Load();
        Assert.Equal(version, reloaded.DefaultRulesVersion);
        Assert.Empty(reloaded.TrackedRules);
    }

    /// <summary>
    /// The torn file is kept, not silently written over. It is the evidence, and before this
    /// the very next `Save` destroyed it — so nobody could ever tell a torn write from a
    /// player who had reset their own settings.
    /// </summary>
    [Fact]
    public void ATornFileIsSetAsideRatherThanOverwritten()
    {
        var settings = AppSettings.Load();
        settings.Theme = "BlueGrey";
        settings.Save();
        settings.Save();
        TearInPlace(SettingsPath);

        AppSettings.Load();

        var corrupt = ProfileJson.CorruptPath(SettingsPath);
        Assert.True(File.Exists(corrupt), "the unreadable file must be kept");
        Assert.All(File.ReadAllBytes(corrupt), b => Assert.Equal(0, b));
    }

    // ---- ProfileJson itself -------------------------------------------------------

    /// <summary>A write leaves no temp file behind and the file reads back exactly.</summary>
    [Fact]
    public void WriteLeavesNoTemporaryFile()
    {
        ProfileJson.Write(Scratch, "{\"a\":1}");
        Assert.Equal("{\"a\":1}", File.ReadAllText(Scratch));
        Assert.False(File.Exists(Scratch + ".new"));
    }

    /// <summary>A write is UTF-8 with no BOM — the encoding every reader here assumes,
    /// and the one a `File.ReadAllText` round trip would otherwise silently change
    /// (trap 60's write side, one layer down).</summary>
    [Fact]
    public void WriteIsUtf8WithoutABom()
    {
        ProfileJson.Write(Scratch, "{\"name\":\"Cazic—Thule\"}");
        var bytes = File.ReadAllBytes(Scratch);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal("{\"name\":\"Cazic—Thule\"}", Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Each successful write hands the outgoing copy to `.bak`, which is the
    /// thing the recovery reads.</summary>
    [Fact]
    public void EachWriteKeepsThePreviousCopy()
    {
        ProfileJson.Write(Scratch, "{\"v\":1}");
        ProfileJson.Write(Scratch, "{\"v\":2}");
        Assert.Equal("{\"v\":2}", File.ReadAllText(Scratch));
        Assert.Equal("{\"v\":1}", File.ReadAllText(ProfileJson.BackupPath(Scratch)));
    }

    [Fact]
    public void ReadReportsMissingWhenThereIsNothingAtAll()
    {
        Assert.Equal(ProfileReadOutcome.Missing,
            ProfileJson.Read<Dictionary<string, int>>(Scratch, null, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void ReadReportsLoadedForAGoodFile()
    {
        ProfileJson.Write(Scratch, "{\"v\":7}");
        Assert.Equal(ProfileReadOutcome.Loaded,
            ProfileJson.Read<Dictionary<string, int>>(Scratch, null, out var value));
        Assert.Equal(7, value!["v"]);
    }

    [Fact]
    public void ReadFallsBackToTheBackupAndSaysSo()
    {
        ProfileJson.Write(Scratch, "{\"v\":1}");
        ProfileJson.Write(Scratch, "{\"v\":2}");
        TearInPlace(Scratch);

        Assert.Equal(ProfileReadOutcome.RecoveredFromBackup,
            ProfileJson.Read<Dictionary<string, int>>(Scratch, null, out var value));
        Assert.Equal(1, value!["v"]);
    }

    /// <summary>The negative every one of these needs: with no backup there is nothing to
    /// recover, and the outcome has to SAY Unreadable rather than quietly answering with a
    /// default — that difference is what stops the migrations re-seeding.</summary>
    [Fact]
    public void ReadReportsUnreadableWhenTheBackupIsGoneToo()
    {
        ProfileJson.Write(Scratch, "{\"v\":1}");
        TearInPlace(Scratch);
        try { File.Delete(ProfileJson.BackupPath(Scratch)); } catch { /* may not exist */ }

        Assert.Equal(ProfileReadOutcome.Unreadable,
            ProfileJson.Read<Dictionary<string, int>>(Scratch, null, out var value));
        Assert.Null(value);
    }

    /// <summary>A launch loop against a broken file must not fill the profile with copies
    /// of it — one fixed `.corrupt` name, overwritten.</summary>
    [Fact]
    public void QuarantineUsesOneFixedName()
    {
        for (var i = 0; i < 3; i++)
        {
            ProfileJson.Write(Scratch, "{\"v\":1}");
            TearInPlace(Scratch);
            ProfileJson.Read<Dictionary<string, int>>(Scratch, null, out _);
        }

        var dir = Path.GetDirectoryName(Scratch)!;
        var stem = Path.GetFileName(Scratch);
        Assert.Single(Directory.GetFiles(dir, stem + ".corrupt*"));
    }
}
