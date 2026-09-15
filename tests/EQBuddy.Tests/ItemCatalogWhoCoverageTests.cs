using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE COVERAGE SURVEY THAT DECIDED WHETHER D4 SHIPPED AT ALL** (DRA-84 D4, Fable plan P3).
///
/// <para>The signed plan made this slice open with a measurement and named the outcome that
/// would stop it: <i>"it OPENS with a coverage survey (what fraction of <c>DropZones</c>-bearing
/// records carry ≥1 creature). If coverage comes back under half, the withhold default is
/// wrong, and the slice STOPS AND ESCALATES to Helm with the number instead of shipping a
/// hollowed room."</i></para>
///
/// <para><b>What the survey found on the post-D3 catalog, and the numbers this file pins:</b></para>
///
/// <code>
///   11,196 records                            shipped
///    5,626 carry a DropZones                   of which 3,740 are wearable-slotted
///    5,591 of those name ≥1 creature           99.4% of records
///   10,497 of 10,637 (item, zone) pairs        98.7% of pairs
///    5,897 of  6,004 wearable pairs            98.2% — the number the rule actually rests on
///    4,712 distinct creature names             over 25,695 mentions (trap 73's tell)
///    3,830 pairs name more than one creature   1,384 name more than three
/// </code>
///
/// <para>So the slice proceeded. The floors below are the plan's own half, not the measurement —
/// a guard that asserted 98.2% would redden on any honest refresh. What they catch is the state
/// the plan legislated for: a catalog that goes back to naming nobody, in which case the withhold
/// rule silently empties the Farm Gear room and somebody has to DECIDE again rather than notice
/// from a support thread.</para>
///
/// <para>The pair count is the denominator that matters, and not the record count. The engine
/// withholds per (item, zone) OFFER — a page naming creatures in one of its two zones is answered
/// in one place and silent in the other, and a record-level survey would call that a hit.</para>
/// </summary>
public class ItemCatalogWhoCoverageTests
{
    /// <summary>The plan's own stop-and-escalate line, as a number. Under this, D4's withhold
    /// default is the wrong default and the slice was to stop rather than ship.</summary>
    private const double PlanFloor = 0.5;

    private readonly record struct Coverage(int Pairs, int Answered, int Records, int WithMobs)
    {
        public double PairShare => Pairs == 0 ? 0 : Answered / (double)Pairs;
    }

    /// <summary>The survey itself — (item, zone) pairs over the shipped catalog, optionally
    /// narrowed to the records the gear sweep can actually anchor on (a slotted record; see
    /// <c>GearUpgrades.SlotIndex</c>, which skips anything with no <c>Slots</c>).</summary>
    private static Coverage Survey(bool wearableOnly)
    {
        int pairs = 0, answered = 0, records = 0, withMobs = 0;
        foreach (var record in ItemCatalog.Default.All)
        {
            if (wearableOnly && record.Slots is not { Count: > 0 }) continue;
            if (record.DropZones is not { Count: > 0 } zones) continue;

            records++;
            var any = false;
            foreach (var zone in zones.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                pairs++;
                if (record.DropMobs is { } mobs
                    && mobs.TryGetValue(zone, out var named) && named.Count > 0)
                {
                    answered++;
                    any = true;
                }
            }
            if (any) withMobs++;
        }
        return new Coverage(pairs, answered, records, withMobs);
    }

    /// <summary>The sweep's own liveness check, first (trap 78). A catalog that stopped carrying
    /// drop zones at all would make every share below 0/0, and a ratio over nothing is green for
    /// no reason.</summary>
    [Fact]
    public void ThereIsACatalogWithDropZonesInItToSurvey()
    {
        var all = Survey(wearableOnly: false);
        var wearable = Survey(wearableOnly: true);

        Assert.True(all.Records >= 4_000,
            $"Only {all.Records:N0} shipped records carry a DropZones. The survey below measures "
            + "a share of them, and a share of almost nothing says nothing.");
        Assert.True(wearable.Pairs >= 3_000,
            $"Only {wearable.Pairs:N0} wearable (item, zone) pairs were found — the gear sweep "
            + "anchors on slotted records, so this is the population the who rule acts on.");
    }

    /// <summary>
    /// **THE PLAN'S STOP-AND-ESCALATE SEAM, LEFT ARMED.**
    ///
    /// <para>It passed at 98.2% and it is committed anyway, because the condition it tests is a
    /// property of the DATA and the data is regenerated weekly by a transform nobody reads the
    /// output of line by line. A refresh that reverted the creature half would leave
    /// <c>Recommendations.WhoRule</c> withholding every drop offer EQBuddy has, and the room
    /// would say so honestly and be empty — which is the worst kind of correct.</para>
    /// </summary>
    [Fact]
    public void MostWearableDropOffersCanNameACreature()
    {
        var wearable = Survey(wearableOnly: true);

        Assert.True(wearable.PairShare >= PlanFloor,
            $"Only {wearable.Answered:N0} of {wearable.Pairs:N0} wearable (item, zone) pairs "
            + $"({wearable.PairShare:P1}) name a creature — under the DRA-84 plan's own "
            + $"{PlanFloor:P0} floor.\n\n"
            + "That floor is a STOP: the plan said that below it the withhold default is wrong "
            + "and the slice escalates to Helm with the number rather than shipping a hollowed "
            + "room. Recommendations.WhoRule is now live, so this is not a cosmetic regression — "
            + "it is the Farm Gear room emptying itself. Do not lower this number; take it to "
            + "Helm with the refresh that caused it.");
    }

    /// <summary>The same measurement over the whole catalog, not just the wearables. Kept
    /// separate because the two populations can drift apart — a refresh that filled in consumables
    /// and dropped armour would look fine on one and not the other.</summary>
    [Fact]
    public void TheWholeCatalogsCoverageIsMeasuredToo()
    {
        var all = Survey(wearableOnly: false);

        Assert.True(all.PairShare >= PlanFloor,
            $"{all.Answered:N0} of {all.Pairs:N0} (item, zone) pairs ({all.PairShare:P1}) name a "
            + $"creature, under the plan's {PlanFloor:P0} floor. See the wearable row beside this "
            + "one — if only this one is red, the gear sweep is still answering and the problem is "
            + "elsewhere in the promoter.");
    }

    /// <summary>
    /// **DISTINCT-COUNT, ON THE THING THE ROWS ACTUALLY SAY** (trap 73's tell).
    ///
    /// <para><c>ItemCatalogDropMobsTests</c> holds the ratio of distinct names to records. This
    /// one holds it against MENTIONS, which is the number a reader meets: 4,712 distinct names
    /// across 25,695 mentions. A promoter that had found one infobox template and copied its
    /// example creature onto everything would pass the ratio-to-records check and fail this one,
    /// because the same handful of names would be repeated thousands of times.</para>
    /// </summary>
    [Fact]
    public void TheNamedCreaturesAreNotAHandfulRepeated()
    {
        var mentions = 0;
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.DropMobs is not { } mobs) continue;
            foreach (var named in mobs.Values)
                foreach (var name in named)
                {
                    mentions++;
                    distinct.Add(name);
                }
        }

        Assert.True(mentions > 0, "No creature is named anywhere in the shipped catalog.");
        Assert.True(distinct.Count >= mentions / 20.0,
            $"{mentions:N0} creature mentions carry only {distinct.Count:N0} distinct names. "
            + "That ratio is what a template looks like rather than what research looks like — "
            + "survey the promoter's output before believing it (trap 73).");
    }

    /// <summary>
    /// **THE CAP IS LOAD-BEARING ON REAL DATA**, which is why it is three and says so.
    ///
    /// <para>Before D4 a row named <c>MobsIn(zone).FirstOrDefault()</c> — one creature, with
    /// nothing saying more existed. If the shipped catalog rarely named more than one, that would
    /// have been a defensible simplification and this slice would be decoration. It names more
    /// than one on 3,830 of 10,637 pairs and more than three on 1,384, so both halves of the
    /// plural clause — the list and the count it holds back — draw on real rows.</para>
    /// </summary>
    [Fact]
    public void PlentyOfPagesNameMoreCreaturesThanOneRowCanShow()
    {
        int plural = 0, overCap = 0;
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.DropMobs is not { } mobs) continue;
            foreach (var named in mobs.Values)
            {
                if (named.Count > 1) plural++;
                if (named.Count > Recommendations.GearMobsPerItem) overCap++;
            }
        }

        Assert.True(plural >= 500,
            $"Only {plural:N0} (item, zone) pairs name more than one creature. The plural WHO "
            + "clause and GearMobsPerItem both exist for pages that name several — if this is "
            + "genuinely near zero now, the cap and the list are decoration and should be "
            + "reconsidered rather than kept as unexercised code.");
        Assert.True(overCap >= 100,
            $"Only {overCap:N0} pairs name more than GearMobsPerItem "
            + $"({Recommendations.GearMobsPerItem}) creatures, so the \"and N more on its page\" "
            + "clause is almost never drawn. A cap that never fires cannot be trusted to say so "
            + "when it does (trap 50).");
    }

    /// <summary>
    /// **THE TWO FOUNDER EXHIBITS, BY NAME.**
    ///
    /// <para>Crushbone was the level class of failure and D2 answered it. Rathe Mountains was the
    /// WHO class — its band is 13–45, which spans most characters, so no level rule was ever
    /// going to refuse it (the D2 ruling said so explicitly and pointed the complaint at this
    /// slice). This pins that the data can now answer it: all 80 of Rathe Mountains' wearable
    /// drop records name a creature, so the row the Founder read as "somewhere in the Rathe" is
    /// a row that now says what to kill rather than a row that got withheld.</para>
    /// </summary>
    [Theory]
    [InlineData("Rathe Mountains")]
    [InlineData("Crushbone")]
    public void TheFounderExhibitZonesCanNameWhatDropsTheirGear(string zone)
    {
        int pairs = 0, answered = 0;
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;
            if (record.DropZones is not { } zones
                || !zones.Contains(zone, StringComparer.OrdinalIgnoreCase)) continue;

            pairs++;
            if (record.DropMobs is { } mobs
                && mobs.TryGetValue(zone, out var named) && named.Count > 0) answered++;
        }

        Assert.True(pairs >= 20,
            $"{zone} carries only {pairs:N0} wearable drop records in the shipped catalog — it "
            + "was one of the Founder's two exhibits precisely because it carries dozens.");
        Assert.Equal(pairs, answered);
    }
}
