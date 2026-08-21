using System.Text.RegularExpressions;
using EQBuddy.Companion;

namespace EQBuddy.Tests;

/// <summary>
/// The PC and the phone must answer "has this section changed?" the SAME way.
///
/// They did not, and that is #202 (bjstrange): the PC's
/// <see cref="CompanionProjection.SectionFingerprints"/> deliberately leaves out values
/// that move on a clock (trap 8 — a countdown or a rate in a fingerprint wakes every
/// device every second), while the page's repaint gate stringified the WHOLE payload and
/// so saw a different key on every render. A watch rule's <c>/hr</c> and <c>/active hr</c>
/// have time in the denominator, so they drift while nothing at all happens: the PC
/// correctly said "nothing to report" and the page tore the panel down and rebuilt it
/// anyway, roughly twice a second — its own 1 s timer plus each push.
///
/// This is trap 4 in a new place: one fact ("did it change?"), two sources.
///
/// Loot was the only card affected, and bjstrange's own test is what bounds that — he
/// enabled every card and reported this one misbehaving, which says the watch rates are
/// the only clock-driven numbers reaching a non-TICKING surface. So this pins the pair
/// rather than trying to enumerate every field: if a future payload adds a drifting
/// value, the fingerprint will exclude it and this test is the reminder that the page's
/// gate has to exclude it too.
/// </summary>
public class CompanionRepaintGateTests
{
    private static string PageSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>The page's exclusion list, as the page itself declares it.</summary>
    private static IReadOnlyCollection<string> DriftKeys()
    {
        var m = Regex.Match(PageSource(), @"const DRIFT_KEYS = new Set\(\[(?<body>[^\]]*)\]\)");
        Assert.True(m.Success,
            "index.html no longer declares DRIFT_KEYS — the repaint gate's exclusion list is " +
            "the other half of SectionFingerprints and cannot just disappear (#202).");
        return Regex.Matches(m.Groups["body"].Value, @"""(?<k>[A-Za-z]+)""")
            .Select(x => x.Groups["k"].Value).ToList();
    }

    [Fact]
    public void ThePageIgnoresTheWatchRatesWhenDecidingToRepaint()
    {
        var keys = DriftKeys();
        Assert.Contains("perHour", keys);
        Assert.Contains("perActiveHour", keys);
    }

    /// <summary>The rates are excluded from the PC's side too — the two halves agree.
    /// Asserted against the real projection rather than by reading the source, so a
    /// fingerprint that started including a rate would fail here.</summary>
    [Fact]
    public void TheFingerprintIgnoresTheWatchRatesToo()
    {
        var slow = Snapshot(perHour: 1.0, perActiveHour: 2.0);
        var fast = Snapshot(perHour: 999.5, perActiveHour: 888.25);

        var a = CompanionProjection.SectionFingerprints(slow)[CompanionSurfaces.Loot];
        var b = CompanionProjection.SectionFingerprints(fast)[CompanionSurfaces.Loot];

        Assert.Equal(a, b);
        // And it still notices a real change — a guard that never fires is not a guard.
        var looted = CompanionProjection.SectionFingerprints(
            Snapshot(perHour: 1.0, perActiveHour: 2.0, total: 4))[CompanionSurfaces.Loot];
        Assert.NotEqual(a, looted);
    }

    /// <summary>The rates are still SENT — this is about what triggers a repaint, not
    /// about hiding numbers. A fix that stopped shipping them would "fix" #202 by
    /// deleting the feature he was watching.</summary>
    [Fact]
    public void TheRatesStillReachThePhone()
    {
        var row = Snapshot(perHour: 12.5, perActiveHour: 25.0).Loot!.Watch.Single();
        Assert.Equal(12.5, row.PerHour);
        Assert.Equal(25.0, row.PerActiveHour);
        Assert.Contains("perHour", PageSource());
        Assert.Contains("perActiveHour", PageSource());
    }

    /// <summary>The gate must not stringify the 1,200-quest catalog — that was the
    /// reason it was excluded — but it must still see whether one is THERE.
    ///
    /// Excluding it outright made the gate blind to the only transition that decides
    /// whether the quest surface works: <c>setCatalog</c> is a side effect of the paint,
    /// so a panel painted without a catalog can only be filled BY a repaint, and a
    /// payload that differed only by carrying one produced an identical key. The panel
    /// sat on "Waiting for the quest catalog from the PC…" for the life of the page —
    /// David's phone, 2026-08-21 — with a correct server pushing the catalog at it.
    ///
    /// Asserted against the page's own source: the content stays out, the presence goes
    /// in. Reproduced in <c>scripts/mobile-harness.ps1</c> before and after.</summary>
    [Fact]
    public void TheGateSeesWhetherACatalogArrivedWithoutStringifyingIt()
    {
        var keys = DriftKeys();
        Assert.DoesNotContain("catalog", keys);   // not a drifting value, and not ignored

        var page = PageSource();
        var m = Regex.Match(page, @"const DRIFTS = (?<body>.*?);", RegexOptions.Singleline);
        Assert.True(m.Success, "index.html no longer declares DRIFTS — the repaint gate's key builder.");
        var body = m.Groups["body"].Value;
        Assert.Contains("\"catalog\"", body);
        // Its PRESENCE, never its content: a 1/0 marker, which is what keeps the gate
        // cheap while leaving it able to notice a catalog arriving.
        Assert.Matches(@"catalog""\s*\?\s*\(\s*v\s*\?\s*1\s*:\s*0\s*\)", body);
    }

    private static CompanionSnapshot Snapshot(double perHour, double perActiveHour, int total = 3) =>
        new()
        {
            Identity = new CompanionIdentity("Testchar", "Befallen", "1.94.0"),
            Offered = [CompanionSurfaces.Loot],
            Loot = new CompanionLootSection(
                Total: total, CraftedTotal: 0,
                Items: [new CompanionCountRow("Bone Chips", total)],
                Crafted: [],
                Watch: [new CompanionWatchRow("Bone chips", total, perHour, perActiveHour, "Bone Chips")]),
        };
}
