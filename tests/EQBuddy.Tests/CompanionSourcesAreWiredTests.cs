using System.Reflection;
using System.Text.RegularExpressions;
using EQBuddy.Companion;

namespace EQBuddy.Tests;

/// <summary>
/// **Every source <see cref="CompanionSources"/> declares is wired by the widget that
/// ships EQBuddy Mobile.** The record is ~20 callbacks and a MISSING one is not a compile
/// error — it is a surface that arrives empty on a paired phone, which nobody finds
/// without a phone, a second machine and the right camp.
///
/// That is not hypothetical: <c>Raids</c> and <c>Progress</c> were added five days after
/// the record, and a wiring written from an older mental model of it would have shipped
/// without them and looked complete.
///
/// **Ported here from <c>EQBuddy.Avalonia.Tests/CompanionWiringTests</c> (E-2a), and it
/// changes lanes on the way.** That test constructed the Avalonia widget and asked the
/// live object which properties were still null — a stronger check, on the lane that is
/// being removed. The WPF widget cannot be constructed in a toolkit-free test project
/// (docs/TestPlan.md §5: no test project references it), so the port is a source scan in
/// the shape <see cref="CompanionSnapshotArgumentTests"/> and
/// <c>LegacyPlatformUpdatePolicyTests.NoWidgetDecidesTheLegacyPlatformQuestionForItself</c>
/// already use. **The lane that actually serves phones had NO check of this at all**, so
/// the port is worth more than the assertion it replaces even though it can see less.
///
/// It reads the record by REFLECTION rather than from a list of names, for the reason the
/// original gave: a member added to <see cref="CompanionSources"/> and forgotten fails
/// here, at the moment the gap is created, instead of on a tablet weeks later.
/// </summary>
public sealed class CompanionSourcesAreWiredTests
{
    private const string Widget = "src/EQBuddy/MainWindow.xaml.cs";

    [Fact]
    public void EverySourceTheRecordDeclaresIsWiredByTheWidget()
    {
        var initializer = SourcesInitializer();

        var unwired = typeof(CompanionSources)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => !Regex.IsMatch(initializer, $@"(^|\W){Regex.Escape(name)}\s*="))
            .ToList();

        Assert.True(unwired.Count == 0,
            "EQBuddy Mobile would serve these surfaces empty: " + string.Join(", ", unwired) +
            $" — {Widget} builds CompanionSources without them.");
    }

    /// <summary>The negative that keeps the scan from going vacuous (trap 39). A regex
    /// that found nothing would report a perfectly wired widget, and so would one that
    /// matched the whole file: this pins that the block really is the initializer, and
    /// that a name the record does NOT declare is not being found by accident.</summary>
    [Fact]
    public void TheScanIsReadingTheInitializerAndNotTheWholeFile()
    {
        var initializer = SourcesInitializer();

        Assert.Contains("SpawnPoints", initializer);
        Assert.DoesNotContain("NoSuchCompanionSource", initializer);
        // MainWindow is ~4,300 lines; the initializer is a few dozen. A match against the
        // whole file would make every property above pass forever.
        Assert.True(initializer.Length < 8_000,
            $"the captured initializer is {initializer.Length} characters — that is the " +
            "file, not the object initializer, and the guard above cannot fail like that.");
    }

    /// <summary>The <c>new CompanionSources { … }</c> block, brace-matched from the widget's
    /// source. Nothing here parses C#: the block is found by counting braces from the one
    /// that opens the initializer, which is enough for a member-name scan and does not
    /// pretend to more.</summary>
    private static string SourcesInitializer()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), Widget));
        var start = Regex.Match(source, @"new\s+(?:Companion\.)?CompanionSources\s*\{");
        Assert.True(start.Success,
            $"{Widget} no longer builds a CompanionSources — if the wiring moved, move this guard.");

        var depth = 0;
        for (var i = start.Index + start.Length - 1; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source[start.Index..(i + 1)];
        }
        throw new InvalidOperationException("unbalanced braces after new CompanionSources {");
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
