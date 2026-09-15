using System.Text.RegularExpressions;
using EQBuddy.Core;

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

    /// <summary>
    /// <b>The committed guides carry the date the committed state claims</b> — the invariant
    /// CI's <c>guides-transform.py --check</c> actually enforces, asserted here directly so it
    /// is a named rule and not an emergent property of two scripts' running order.
    ///
    /// <para><b>The bug this is built on (DRA-84 D3, 2026-09-14).</b> Every guide's
    /// <c>retrievedAt</c> is <c>refresh-state.json</c>'s <c>ranAt</c> date, and
    /// <c>refresh.py</c> stamped that state AFTER the promotion that reads it. So the
    /// transform baked the PREVIOUS run's date, the stamp then advanced past it, and
    /// <c>--check</c> regenerated a date the committed file could not have. The gate went red
    /// with the data byte-for-byte identical and the payload length unchanged — a gate
    /// reddening for a reason that is not about the data, which teaches everyone to re-run
    /// until green (trap 74's own warning, from the other side). The fix is the
    /// <c>--stamp</c> wiring below; this row is what notices if it ever comes undone, by ANY
    /// mechanism, without needing Python to run.</para>
    /// </summary>
    [Fact]
    public void TheCommittedGuidesCarryTheDateTheCommittedRefreshStateClaims()
    {
        var state = Read("scripts/harvests/eqlwiki/refresh-state.json");
        var ranAt = Regex.Match(state, @"""ranAt""\s*:\s*""([^""]+)""").Groups[1].Value;
        Assert.False(ranAt.Length == 0, "refresh-state.json has no ranAt — the stamp every "
            + "harvested guide's retrievedAt is derived from.");
        var expected = ranAt[..10];

        // Guide sources AND objective sources — a partial regeneration would leave two dates
        // in the file, and reading only one level would photograph the half that agreed.
        var guides = GuideCatalog.LoadHarvested().Guides;
        var stamps = guides.SelectMany(g => g.Sources)
            .Concat(guides.SelectMany(g => g.AllObjectives).SelectMany(o => o.Sources))
            .Select(s => s.RetrievedAt)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // One distinct value, and it is the state's. Two would mean a partial regeneration.
        Assert.Equal([expected], stamps);
    }

    /// <summary>
    /// <b>refresh.py hands guides-transform the stamp it is ABOUT to write</b>, and the state
    /// write stays last. Both halves matter: passing the stamp is what stops the gate
    /// reddening on a date nobody changed, and keeping the write last is what stops a promotion
    /// that throws from leaving the window advanced past pages nobody processed.
    ///
    /// <para>And the QUIET path must not advance <c>ranAt</c> at all. No promotion runs there,
    /// so nothing regenerates the guides — advancing the date would date the committed file to
    /// a run that never rebuilt it and redden the gate on a week where, by construction,
    /// nothing happened.</para>
    /// </summary>
    [Fact]
    public void TheRefreshStampsTheGuidesWithTheDateItIsAboutToWriteAndLeavesAQuietWeekAlone()
    {
        var refresh = Read("scripts/harvests/refresh.py");

        // The stamp is passed, and to guides-transform specifically.
        Assert.Matches(@"--stamp""\s*,\s*now\s*\]\s*if\s*script\.name\s*==\s*""guides-transform\.py""",
            refresh);

        // guides-transform accepts it rather than silently ignoring an unknown flag.
        var transform = Read("scripts/harvests/eqlwiki/guides-transform.py");
        Assert.Contains("\"--stamp\"", transform);
        Assert.Contains("harvested_at(args.stamp)", transform);

        // The quiet path keeps the previous ranAt; the full path writes `now`.
        Assert.Contains("\"ranAt\": keep", refresh);
        Assert.Contains("\"ranAt\": now", refresh);

        // The state write stays AFTER the promotions — read off positions, not off the text,
        // because the name appears in the comment that explains the ordering too.
        var promotionsRun = refresh.IndexOf("for script in PROMOTIONS:", StringComparison.Ordinal);
        var finalStamp = refresh.LastIndexOf("\"ranAt\": now", StringComparison.Ordinal);
        Assert.True(promotionsRun >= 0 && finalStamp > promotionsRun,
            "refresh.py must stamp refresh-state.json AFTER the promotions run, so a promotion "
            + "that throws cannot leave the window advanced past pages nobody processed.");
    }

    /// <summary>Every harvester the refresh drives must exist. A renamed script would make
    /// the weekly run fail loudly, which is fine — but a DELETED one silently stops
    /// refreshing whatever it fed.</summary>
    [Fact]
    public void EveryScriptTheRefreshDrivesExists()
    {
        var refresh = Read("scripts/harvests/refresh.py");
        var named = Regex.Matches(refresh,
                @"""([a-z0-9-]+(?:-harvest|-promote|-merge|-transform)\.py)""")
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

    /// <summary>
    /// The guide catalog is on the weekly cadence as CURATED, and the file the refresh looks
    /// for is the file that exists.
    ///
    /// Both halves, and the second is the one that bites: `curated_flags` silently
    /// `continue`s past a path it cannot find, so a renamed or moved catalog produces a
    /// green refresh that flags nothing, forever. A guide is prose about the world — when
    /// eqlwiki's page for a step changes, this flag is the ONLY way that correction reaches
    /// the person who has to re-author it (plan §3; the same shape as trap 20).
    /// </summary>
    [Fact]
    public void TheGuideCatalogIsCuratedAndTheRefreshCanFindIt()
    {
        var refresh = Read("scripts/harvests/refresh.py");
        var promoted = Regex.Match(refresh, @"PROMOTED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;
        var curated = Regex.Match(refresh, @"CURATED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains("GuideCatalog.json", curated);
        Assert.DoesNotContain("GuideCatalog.json", promoted);

        // `DATA / name` in refresh.py — the flag reads the shipped file itself.
        Assert.True(File.Exists(Path.Combine(Root, "src", "EQBuddy.Core", "Data", "GuideCatalog.json")),
            "refresh.py flags Data/GuideCatalog.json; it is not there, so the weekly flag is a no-op.");
    }

    /// <summary>
    /// The HARVESTED half is the mirror image and both halves matter (DRA-45):
    /// `HarvestedGuides.json.gz` is PROMOTED — regenerated every week, diffed for the
    /// report — and never curated, while `GuideCatalog.json` above is curated and never
    /// promoted. Swapping either would break the one rule the two files exist to keep
    /// apart: a machine may write the harvested guide and may never touch the authored one.
    ///
    /// <para>And the transform runs AFTER <c>quests-promote.py</c>, because it reads the
    /// catalog that promotion writes. Listed before it, the week's new quests would be
    /// harvested a week late while the run reported success — the silent-decay shape this
    /// whole file guards.</para>
    /// </summary>
    [Fact]
    public void TheHarvestedGuidesArePromotedAfterTheQuestCatalogTheyRead()
    {
        var refresh = Read("scripts/harvests/refresh.py");
        var promoted = Regex.Match(refresh, @"PROMOTED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;
        var curated = Regex.Match(refresh, @"CURATED = \[(.*?)\]", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains("HarvestedGuides.json.gz", promoted);
        Assert.DoesNotContain("HarvestedGuides.json.gz", curated);

        // The RUN ORDER, read off the entries and not off the text. Searching the block for
        // "quests-promote.py" found it in the COMMENT that explains the ordering — which
        // sits above guides-transform.py, so the guard passed on a tree where the two had
        // been swapped. Proved by doing exactly that (2026-09-11). It is the same mistake
        // `TheClassPageHarvestRunsWeeklyAndItsCacheIsEvicted` documents one test up: a name
        // present in a file is not the call site.
        var promotions = Regex.Match(refresh, @"PROMOTIONS = \[(.*?)\]", RegexOptions.Singleline)
            .Groups[1].Value;
        var order = promotions.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith('#'))
            .SelectMany(line => Regex.Matches(line, @"""([a-z0-9-]+\.py)""")
                .Select(m => m.Groups[1].Value))
            .ToList();
        var quests = order.IndexOf("quests-promote.py");
        var guides = order.IndexOf("guides-transform.py");
        Assert.True(quests >= 0 && guides > quests,
            "guides-transform.py reads QuestCatalog.json and must run after quests-promote.py "
            + $"writes it — the promotion order is [{string.Join(", ", order)}]");

        Assert.True(File.Exists(Path.Combine(Root, "src", "EQBuddy.Core", "Data",
                "HarvestedGuides.json.gz")),
            "refresh.py diffs Data/HarvestedGuides.json.gz; it is not there.");
    }
}
