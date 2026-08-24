using System.Text.RegularExpressions;

namespace EQBuddy.Tests;

/// <summary>
/// The weekly refresh actually re-reads what it claims to re-read.
///
/// **`refresh.py` says its cache schemes are "kept in sync with each script" — by hand.**
/// That is the whole risk: a harvester whose cache scheme drifts from the copy in
/// `refresh.py` still RUNS every week, still reports success, and quietly serves its own
/// stale cache forever. The catalog freezes at the day it was first parsed and nothing
/// says so — the same silent-decay shape as a setting with no writer (trap 20), one layer
/// out in the pipeline.
///
/// It became worth guarding when `class-spells-harvest.py` joined the cadence
/// (2026-08-23): its whole point is that eqlwiki's CLASS pages now decide the spell
/// catalog, so an eviction rule that misses them means the class pages are read once,
/// ever, while the refresh reports green every week.
///
/// These are TEXT assertions over the scripts, deliberately — there is no Python to run
/// here, and the failure being guarded is two files disagreeing rather than either one
/// being wrong on its own.
/// </summary>
public class WeeklyRefreshWiringTests
{
    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative)
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"{relative} has moved — this guard scans it, so a wrong "
            + "path here is a guard that silently passes (trap 34).");
        return File.ReadAllText(path);
    }

    /// <summary>Every harvester the refresh drives must exist. A renamed script would make
    /// the weekly run fail loudly, which is fine — but a DELETED one silently stops
    /// refreshing whatever it fed.</summary>
    [Fact]
    public void EveryScriptTheRefreshDrivesExists()
    {
        var refresh = Read("scripts/harvests/refresh.py");
        var named = Regex.Matches(refresh, @"""([a-z0-9-]+(?:-harvest|-promote|-merge)\.py)""")
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.NotEmpty(named);
        foreach (var script in named)
        {
            var inWiki = File.Exists(Path.Combine(Root, "scripts", "harvests", "eqlwiki", script));
            var inTools = File.Exists(Path.Combine(Root, "scripts", "harvests", "eqltools", script));
            Assert.True(inWiki || inTools, $"refresh.py drives {script} and it does not exist");
        }
    }

    /// <summary>
    /// The class-page harvest is ON the weekly cadence, and its cache is EVICTED there.
    ///
    /// Both halves, because either alone is useless: running it weekly without evicting
    /// re-reads a cache and reports success, and evicting without running it changes
    /// nothing. eqlwiki's class pages decide the spell catalog since 2026-08-23, so this
    /// is the path by which a class-page edit reaches players at all.
    /// </summary>
    [Fact]
    public void TheClassPageHarvestRunsWeeklyAndItsCacheIsEvicted()
    {
        var refresh = Read("scripts/harvests/refresh.py");

        Assert.Contains("class-spells-harvest.py", refresh);
        // The EVICTION CALL SITE, not the mere presence of the name. Asserting
        // `Contains("class_cache(title)")` passes on `def class_cache(title):` alone — so
        // deleting the eviction and keeping the dead helper left this test green. Caught by
        // running it against a tree with the eviction removed, which is the only thing that
        // separates a guard from a comment (trap 34, in a test written to prevent trap 34).
        var candidates = Regex.Match(refresh, @"candidates = \[(.*?)\]", RegexOptions.Singleline)
            .Groups[1].Value;
        Assert.Contains("class_cache(title)", candidates);
        Assert.Contains("class_meta_cache(title)", candidates);
        // It has to run BEFORE the promote that reads its output, which the refresh
        // guarantees structurally by putting harvesters ahead of promotions.
        Assert.True(refresh.IndexOf("HARVESTERS", StringComparison.Ordinal)
            < refresh.IndexOf("PROMOTIONS", StringComparison.Ordinal));
        Assert.Contains("spell-levels-promote.py", refresh);
    }

    /// <summary>
    /// `refresh.py`'s copy of the class cache scheme matches the harvest's own.
    ///
    /// This is the assertion the whole file exists for. The two are separate literals in
    /// separate languages; nothing but this compares them, and a drift is invisible
    /// precisely because both sides keep working alone.
    /// </summary>
    [Fact]
    public void TheClassCacheSchemeMatchesTheHarvestsOwn()
    {
        var harvest = Read("scripts/harvests/eqlwiki/class-spells-harvest.py");
        var refresh = Read("scripts/harvests/refresh.py");

        // The harvest builds `class-{stem}.wikitext` / `.json` from a title with spaces
        // replaced by underscores.
        Assert.Contains("stem = title.replace(\" \", \"_\")", harvest);
        Assert.Contains("f\"class-{stem}.wikitext\"", harvest);
        Assert.Contains("f\"class-{stem}.json\"", harvest);

        Assert.Contains("stem = title.replace(\" \", \"_\")", refresh);
        Assert.Contains("f\"class-{stem}.wikitext\"", refresh);
        Assert.Contains("class-{title.replace(' ', '_')}.json", refresh);
    }

    /// <summary>The catalog the class pages now decide is a PROMOTED file — generated and
    /// diffed for the refresh report — and must never drift into the curated list, which is
    /// never auto-written. Getting that backwards would either freeze the catalog or
    /// auto-write something a human is supposed to review.</summary>
    [Fact]
    public void TheSpellCatalogIsPromotedAndNotCurated()
    {
        var refresh = Read("scripts/harvests/refresh.py");
        var promoted = Regex.Match(refresh, @"PROMOTED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;
        var curated = Regex.Match(refresh, @"CURATED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains("SpellLevels.json", promoted);
        Assert.DoesNotContain("SpellLevels.json", curated);
        // And the AA catalog stays CURATED — a wrong AA level is worse than a stale one,
        // which is why the refresh only ever flags it (CLAUDE.md).
        Assert.Contains("AaCatalog.json", curated);
    }
}
