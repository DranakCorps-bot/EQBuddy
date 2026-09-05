using EQBuddy.Companion;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// #202 (bjstrange): EQBuddy Mobile's loot card rebuilding several times a second, on a
/// page that was provably current, for three releases and two wrong diagnoses.
///
/// The cause was not the page and not the change detection. Two call sites pushed to the
/// phone — <c>RefreshUi</c> once a second and the low-latency companion pump every 50 ms —
/// and they built their snapshots with DIFFERENT ARGUMENTS. <c>Snapshot()</c> passes no
/// rules, and a snapshot built without rules comes back with <c>Tracked</c> empty; the
/// loot section is the only surface carrying the watch rows. So the phone was told the
/// watch list had emptied twenty times a second and refilled once a second, and its
/// fingerprint — correctly — said the section had changed every single time.
///
/// bjstrange's two <c>?debug=1</c> captures are the evidence, nine seconds apart and
/// mirror images of each other: "was watch:[] now watch:[{Motes…}]" and
/// "was watch:[{Motes…}] now watch:[]".
///
/// This is CLAUDE.md trap 10 with the knobs being ARGUMENTS rather than settings: a second
/// route that skips what the main path passes is a second product. The lesson the earlier
/// fix missed is that two callers with different arguments do not produce a stale answer
/// and a fresh one — they produce two different answers, both current, and whichever
/// pushed last wins.
/// </summary>
public sealed class CompanionSnapshotArgumentTests
{
    private static SessionStats Seeded()
    {
        var stats = new SessionStats();
        foreach (var line in new[]
        {
            "[Mon Aug 17 21:00:00 2026] --You have looted a Mote of Infinitesimal Potential from a gnoll's corpse.--",
            "[Mon Aug 17 21:00:10 2026] --You have looted a Rusty Dagger from a gnoll's corpse.--",
        })
            stats.Apply(LogParser.Parse(line)!);
        return stats;
    }

    private static readonly IReadOnlyList<TrackedRule> Rules =
    [
        new TrackedRule { Name = "Motes", Pattern = "Mote of", Kind = WatchKind.Loot },
    ];

    /// <summary>The argument-less overload really does come back empty-handed. Stated as a
    /// fact of its own, because every line below depends on it and it is not obvious from
    /// the call site — <c>_stats.Snapshot()</c> reads like "the snapshot".</summary>
    [Fact]
    public void ASnapshotBuiltWithoutRulesHasNoWatchRows()
    {
        var stats = Seeded();

        Assert.Empty(stats.Snapshot().Tracked);
        Assert.NotEmpty(stats.Snapshot(recentWindow: null, rules: Rules).Tracked);
    }

    /// <summary>And the difference reaches the wire: the two snapshots project to loot
    /// sections with DIFFERENT fingerprints, so alternating between them makes the phone
    /// tear the card down and rebuild it on every push. This is the assertion that says
    /// why #202 looked like a phone bug — the page was doing exactly what it should.</summary>
    [Fact]
    public void TheTwoSnapshotsDisagreeAboutTheLootSectionOnTheWire()
    {
        var stats = Seeded();

        var withRules = Fingerprint(stats.Snapshot(recentWindow: null, rules: Rules));
        var without = Fingerprint(stats.Snapshot());

        Assert.NotEqual(withRules, without);
        // Same version, same instant, same everything else — the arguments alone.
        Assert.Equal(withRules, Fingerprint(stats.Snapshot(recentWindow: null, rules: Rules)));
    }

    private static string Fingerprint(StatsSnapshot snap)
    {
        var projection = CompanionProjection.Build(snap, timers: [], character: "Xyrid",
            appVersion: "1.98.0", now: new DateTime(2026, 8, 17, 21, 5, 0),
            offered: [CompanionSurfaces.Loot]);
        return CompanionProjection.SectionFingerprints(projection)
            .TryGetValue(CompanionSurfaces.Loot, out var fp) ? fp : "";
    }

    /// <summary>
    /// THE WIRING, which is the half no unit test could otherwise see: every push to a
    /// paired device must build its snapshot the same way.
    ///
    /// A source scan rather than a behavioural assertion, because the widget has no unit
    /// tests at all (docs/TestPlan.md §5) and this bug lives entirely in which overload a
    /// call site picked.
    ///
    /// **Both lanes were scanned until E-2 (2026-09-04), and the original defect was never
    /// cross-lane: it was TWO PUSH SITES IN ONE WIDGET** — `RefreshUi` once a second and
    /// the 50 ms pump — disagreeing about arguments. Both of those are in the file below.
    /// The Avalonia copy had inherited the same bug, which is why the scan covered it; the
    /// thing it guards against is a third push site here.
    /// </summary>
    [Fact]
    public void EveryCompanionPushBuildsItsSnapshotTheSameWay()
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

        foreach (var relative in new[]
        {
            Path.Combine("EQBuddy", "MainWindow.xaml.cs"),
        })
        {
            var path = Path.Combine(src, relative);
            Assert.True(File.Exists(path), $"{relative} moved — update this test's paths.");

            var pushes = File.ReadLines(path)
                .Select((line, i) => (Line: line.Trim(), Number: i + 1))
                .Where(l => l.Line.Contains("_companion.Tick("))
                .ToList();

            Assert.True(pushes.Count > 0,
                $"{relative} pushes to no paired device at all — that is a bigger bug "
                + "than the one this test guards.");

            foreach (var (line, number) in pushes)
                Assert.False(line.Contains("_stats.Snapshot()"),
                    $"{relative}:{number} pushes a snapshot built WITHOUT rules, so its "
                    + "Tracked list is empty and the phone's loot card will churn against "
                    + "whatever the other push site sends (#202). Use the widget's own "
                    + "snapshot builder — BuildSnapshot() — so every push carries the "
                    + "same arguments.");
        }
    }
}
