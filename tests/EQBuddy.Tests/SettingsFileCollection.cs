using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Every test that writes the profile's <c>settings.json</c> runs in THIS collection, and
/// therefore never at the same time as another one that does.
///
/// <see cref="TestProfileIsolation"/> gives the whole assembly ONE throwaway profile
/// directory, which is what keeps the suite off a real install (it overwrote David's own
/// settings.json in 2026-08-14). The cost of one directory is that every test writing
/// <c>settings.json</c> is writing the SAME file, and xUnit runs collections in parallel —
/// so two of them racing is not a hypothetical.
///
/// It stopped being hypothetical on 2026-08-22: <c>SettingsClobberTests</c> failed about
/// one run in three because <c>CompanionHost.Save()</c> landed a default settings.json on
/// top of the two-byte file it had just written. The test was RIGHT — something else had
/// written the file — and the something else was another test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SettingsFileCollection
{
    public const string Name = "settings.json";
}

/// <summary>
/// The half that keeps the collection true as the suite grows.
///
/// A collection attribute on four files is a fact about today. CLAUDE.md's rule after
/// trap 34 is to pair every "no X may do Y" with a curated list of "these must do Y", each
/// row carrying its reason — because the failure that matters is a file that SHOULD be in
/// the collection and is not, and nothing about that shows in a diff, a build or a green
/// run. It shows as a flake in a different file, weeks later, which is exactly how this
/// one presented.
/// </summary>
public class SettingsFileCollectionTests
{
    /// <summary>What reaches <c>AppSettings.Save()</c> on the shared profile path. A test
    /// file naming any of these writes settings.json, whether or not it means to.</summary>
    private static readonly string[] Writers =
    [
        // Loads persist migrations and generated rule ids at the bottom (Fable 5, v1.99.3
        // release review): a Load IS a write on a profile that has not migrated.
        "AppSettings.Load(",
        // Pairing/enable/theme all persist immediately — CompanionHost.cs:104,159,168,181.
        "CompanionHost",
        // Writes the settings back after an import it accepted — OutputfileAutoImport.cs:108,132.
        "OutputfileAutoImport",
        // The #253 one-time watch-pin migration ends in settings.Save() —
        // WatchPinMigration.cs:40. **This row is the one that proves the list is the weak
        // half of this guard, and it was added on 2026-09-05 after the flake it was built
        // to prevent came back.** `WatchPinMigration` arrived in v1.99.16, months after
        // the three rows above, and nothing asked whether it belonged here — so
        // `WatchPinMigrationTests` sat outside the collection landing a full default
        // settings.json (5,125 bytes) on top of the two-byte `{}` that
        // `LoadCanBeAskedNotToPersistMigrations` had just written. Same test, same
        // mechanism, same one-in-several rate as 2026-08-22; only the writer was new.
        // Trap 30 to the letter: a hand-maintained list stops covering the set the day the
        // set grows, and it cannot be type-checked, so the set has to be re-derived by hand.
        // → **When you add a `settings.Save()` anywhere in Core or UI.Shared, add its row
        //   here in the same commit.** The cost of forgetting is not a red build; it is a
        //   flake in somebody else's file, weeks later, that reads as their bug.
        "WatchPinMigration",
    ];

    [Fact]
    public void EveryTestThatWritesSettingsJsonIsInTheSerialCollection()
    {
        // The same walk up out of bin/ that ArchitectureTests' ratchet uses.
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "EQBuddy.Tests"));
        Assert.True(Directory.Exists(dir),
            $"the test sources moved: {dir}. This guard scans them, so a wrong path here "
            + "is a guard that silently passes (trap 34).");
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var name = Path.GetFileName(path);
            // This file names the writers in order to look for them.
            if (name == "SettingsFileCollection.cs") continue;

            var text = File.ReadAllText(path);
            // Comments mention these names constantly; only real code counts.
            var code = Regex.Replace(text, @"^\s*(///|//).*$", "", RegexOptions.Multiline);
            if (!Writers.Any(w => code.Contains(w, StringComparison.Ordinal))) continue;
            if (code.Contains($"[Collection(SettingsFileCollection.Name)]", StringComparison.Ordinal)
                || code.Contains("[Collection(\"settings.json\")]", StringComparison.Ordinal))
                continue;

            offenders.Add(name);
        }

        Assert.True(offenders.Count == 0,
            "These test files write the shared profile's settings.json but are not in the "
            + $"'{SettingsFileCollection.Name}' collection, so they run in parallel with the "
            + "tests that assert on that file — the 1-in-3 flake of 2026-08-22. Add "
            + "[Collection(SettingsFileCollection.Name)] to each, or stop it writing "
            + "settings.json: " + string.Join(", ", offenders));
    }
}
