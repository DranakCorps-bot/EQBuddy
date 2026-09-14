using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE SURVEY THAT REFUSED THE MOTE CATALOG ARM** (DRA-71 D7, Fable plan P10).
///
/// <para>The signed plan let this slice CHECK whether the shipped catalog names where motes
/// drop, and said what to do either way: <i>"if yes, those are labeled Catalog fallback rows
/// (match-the-wiki); if no, silence and an honest empty state."</i> The check came back as
/// neither of the two answers anybody expected. All eleven mote records carry a
/// <c>DropZones</c> — the field is PRESENT — and not one of its values is a place:</para>
///
/// <code>
///   Mote of Infinitesimal … Superior … Grand … Ascendant   DropZones = ["Various Zones"]
///   Mote of Potential                                      DropZones = ["D3+ Zones"]
///   Mote of Infinite Potential                             DropZones = ["Unknown"]
///   Void-Touched Potential                                 DropZones = null
/// </code>
///
/// <para><b>A field that is present and says nothing is worse than a field that is absent</b>,
/// because a reader that checked only for presence would have shipped rows telling players to
/// travel to "Various Zones". So the engine has no catalog arm at all, and this file holds the
/// finding against the real file so the day it changes somebody DECIDES rather than nobody
/// noticing.</para>
///
/// <para>The "D3+ Zones" row is the one worth keeping in view for its own reason: it is the
/// wiki tying mote quality to instance tier in its own words, which is the only corroboration
/// anywhere for reading the Founder's "difficulty 2–4" as the game's D0–D4 scale. One string is
/// not a model, and nothing derives a per-tier mote value from it.</para>
/// </summary>
public class MoteCatalogSurveyTests
{
    /// <summary>The values the survey found. They are PLACEHOLDERS rather than zones, and the
    /// list is curated deliberately: a heuristic for "does this look like a zone" would be a
    /// guess, and there are eleven rows.</summary>
    private static readonly string[] Placeholders = ["Various Zones", "Unknown", "D3+ Zones"];

    private static List<ItemCatalog.Record> MoteRecords() =>
        [.. ItemCatalog.Default.All.Where(r => Motes.IsMote(r.Name))];

    /// <summary>The sweep's own liveness check. A fold or a rename that stopped finding mote
    /// records would make every row below vacuously green (trap 78: a guard aimed at nothing is
    /// green for no reason).</summary>
    [Fact]
    public void TheShippedCatalogStillHasMoteRecordsToSurvey()
    {
        var motes = MoteRecords();
        Assert.True(motes.Count >= 10,
            $"Only {motes.Count} mote records were found in the shipped catalog — the sweep has "
            + "stopped finding them. Check Motes.IsMote against the catalog's spelling, not the "
            + "assertion.");
        Assert.Contains(motes, r => r.Name.Equals(Motes.VoidTouched, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// **NO MOTE RECORD NAMES A PLACE, AND THAT IS WHY THERE IS NO CATALOG ARM.**
    ///
    /// <para>The day a refresh puts a real zone on one of these pages, this fails — on purpose.
    /// It is the reopen condition for the plan's "Catalog fallback rows" arm, and it should be
    /// somebody reading a diff rather than a player reading a recommendation to go to Various
    /// Zones.</para>
    /// </summary>
    [Fact]
    public void NoMoteRecordNamesAZoneAPlayerCouldTravelTo()
    {
        var named = MoteRecords()
            .SelectMany(r => (r.DropZones ?? []).Select(z => $"{r.Name} → {z}"))
            .Where(pair => !Placeholders.Any(p =>
                pair.EndsWith("→ " + p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(named.Count == 0,
            "A mote record now names a drop zone that is not one of the survey's placeholders:\n"
            + string.Join("\n", named)
            + "\n\nThat is the reopen condition for the plan's catalog-fallback arm (DRA-71 D7, "
            + "P10). Decide whether the Farm Motes engine should draw Catalog rows for it, then "
            + "update this row — do not simply widen the placeholder list.");
    }

    /// <summary>The engine reads no catalog for motes at all, asserted from the other side: a
    /// fixture with a mote record that DOES name a real zone, and a player who has never looted
    /// one, still answers nothing. Without this, the row above would be the only thing standing
    /// between a placeholder and a recommendation.</summary>
    [Fact]
    public void AMoteRecordWithARealZoneStillProducesNoRecommendation()
    {
        var catalog = new ItemCatalog([
            new ItemCatalog.Record
            {
                Name = "Mote of Major Potential", DropZones = ["Lower Guk"],
            },
        ]);
        var set = Recommendations.Rank(
            HelperInputs.Nothing with { Items = catalog }, [HelperGoal.FarmMotes]);

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoPlayHistory, Assert.Single(set.Gaps).Reason);
    }

    /// <summary>The one corroborating datum, pinned so the claim in <c>DECISIONS.md</c> can be
    /// checked rather than taken on trust: the wiki itself ties a mote to an instance tier
    /// band, which is the evidence behind reading "difficulty 2–4" as D0–D4.</summary>
    [Fact]
    public void TheWikiTiesTheBareMoteToAnInstanceTierBand()
    {
        var record = ItemCatalog.Default.Find("Mote of Potential");
        Assert.NotNull(record);
        Assert.Contains(record!.DropZones ?? [],
            z => z.Contains("D3", StringComparison.OrdinalIgnoreCase));
    }
}
