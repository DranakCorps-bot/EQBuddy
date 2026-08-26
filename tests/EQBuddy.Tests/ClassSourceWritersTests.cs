using System.Text.RegularExpressions;

namespace EQBuddy.Tests;

/// <summary>
/// Every route that reads an achievements dump must record the classes it names.
///
/// **This is trap 20's shape, and it is the one that keeps happening.** `SkyQuestCompleted`
/// (#204/#209), `EpicQuestCompleted` (#210 — a helper with passing tests and NO CALLER) and
/// `SkyQuestClass` (#212) were all the same event: the data survived, the WRITE path did
/// not, and nothing could see it — not the compiler, not the unit suite, not a screenshot.
///
/// `UnlockedClasses` is the newest thing with that shape. It is read by
/// <c>CharacterClasses.Resolve</c> on both desktops and it decides what every class-aware
/// surface shows, so a route that imports a dump and does not record it leaves that player
/// permanently on inference alone — and there are THREE such routes, two of them
/// hand-written per lane.
///
/// So this is a curated MUST-list (the `GameCommandsTests.SurfacesNeedingACommand` idiom),
/// not a "nobody may do X" scan: a rule that forbids the wrong thing cannot see a missing
/// thing, which is trap 34 and is exactly why the Gear tab shipped with no ⧉ button.
/// </summary>
/// <remarks>
/// **In the settings.json collection despite writing nothing.** `SettingsFileCollection`'s
/// guard flags any test file whose CODE names `OutputfileAutoImport`, on the reasoning that
/// naming it means calling it and calling it persists settings. This file only names it as
/// a PATH STRING in the list below, so that is a false positive — and joining the
/// collection is the right answer anyway: it costs four file reads of serialisation, where
/// teaching the detector to tell a path from a call would weaken a guard that exists
/// because of a real 1-in-3 flake. A guard with an exception carved into it for
/// convenience is how the exception becomes the rule.
/// </remarks>
[Collection(SettingsFileCollection.Name)]
public class ClassSourceWritersTests
{
    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    /// <summary>Each row: the file that reads a dump, and why it is a writer. Adding a
    /// fourth import route means adding a row — which is the point of the list.</summary>
    public static TheoryData<string, string> Writers => new()
    {
        {
            "src/EQBuddy.Core/OutputfileAutoImport.cs",
            "the AUTOMATIC path — it fires unprompted when the game announces a dump "
            + "(1.98.1), so for most players it is the only route that ever runs"
        },
        {
            "src/EQBuddy/QuestChecklistView.cs",
            "the Windows manual path — ⚙ → Import achievements"
        },
        {
            "src/EQBuddy.Avalonia/MainWindow.cs",
            "the Linux/macOS manual path, hand-written separately from its WPF twin, "
            + "which is precisely how a lane gets missed (#210)"
        },
    };

    /// <summary>
    /// Routes that READ a dump and deliberately record nothing, each with the reason.
    ///
    /// An exemption nobody can see is a blind spot rather than an exemption
    /// (`SurfaceOwnershipTests` says the same thing about its two lanes), so this is a
    /// list and not a hole in the scan below.
    /// </summary>
    public static readonly (string File, string Why)[] ReadsButDoesNotRecord =
    [
        ("src/EQBuddy.Core/UnlockSource.cs",
            "It re-reads both dumps on a RENDER path, when their timestamps move, so that "
            + "the Unlocks tab cannot show what was true last week. Recording from there "
            + "would make a getter write to the per-character ledger on a UI tick — a "
            + "second writer racing the import path for one fact (trap 4), and a ledger "
            + "save per repaint. The import routes above are the writers, and they run on "
            + "the same dump the moment the game announces it."),
    ];

    [Theory]
    [MemberData(nameof(Writers))]
    public void EveryAchievementsImportRouteRecordsTheClassesItRead(string file, string why)
    {
        var path = Path.Combine(Root, file);
        Assert.True(File.Exists(path), $"{file} has moved — update this list, it is {why}");
        var source = File.ReadAllText(path);

        Assert.Contains("AchievementsImport.UnlockedClasses", source);
        Assert.Contains("SetUnlockedClasses", source);
    }

    /// <summary>The negative that keeps the list honest: if a FOURTH file learns to parse a
    /// dump, this fails until it is either given the write or added here with a reason.
    /// Without it the list above is a rule that only covers what someone remembered to put
    /// in it — coverage by luck, which is what trap 34 names.</summary>
    [Fact]
    public void NoOtherFileParsesAnAchievementsDumpUnnoticed()
    {
        var known = Writers.Select(row => (string)row[0]!)
            .Concat(ReadsButDoesNotRecord.Select(r => r.File))
            .Select(f => f.Replace('/', Path.DirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs",
                     SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            var rel = Path.GetRelativePath(Root, path);
            if (known.Contains(rel)) continue;
            // The definition site itself is not a caller.
            if (rel.EndsWith("AchievementsImport.cs", StringComparison.OrdinalIgnoreCase)) continue;
            if (Regex.IsMatch(File.ReadAllText(path), @"AchievementsImport\s*\.\s*Parse\s*\("))
                offenders.Add(rel);
        }

        Assert.True(offenders.Count == 0,
            "these files read an achievements dump and are not on the writers list — either "
            + "record UnlockedClasses there or add a row to ReadsButDoesNotRecord saying "
            + "why not: " + string.Join(", ", offenders));
    }

    /// <summary>An exemption needs a REASON, and the file it names has to still exist —
    /// otherwise the list decays into a set of names nobody can evaluate.</summary>
    [Fact]
    public void EveryExemptionNamesARealFileAndSaysWhy()
    {
        Assert.All(ReadsButDoesNotRecord, row =>
        {
            Assert.True(File.Exists(Path.Combine(Root, row.File)),
                $"{row.File} has moved — update this exemption");
            Assert.True(row.Why.Length > 40, $"{row.File}: an exemption needs a real reason");
        });
    }
}
