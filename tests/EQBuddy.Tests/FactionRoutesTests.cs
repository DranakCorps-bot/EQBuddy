using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Faction routes (DRA-746, DRA-728 D1) — eqlwiki's turn-in item x count -> faction x delta,
/// promoted from the COMMITTED cache by <c>scripts/harvests/eqlwiki/faction-routes-transform.py</c>.
///
/// <para>Three guards, each paired the trap-34 way:</para>
/// <list type="bullet">
/// <item><b>The must-list</b>: every race-unlock faction the committed achievements fixtures
/// name, with its status (routed / direction-only / none). It is CURATED here and read back
/// against the fixtures both ways, so a fixture faction nobody decided about reddens, and so
/// does a must-list row the fixtures no longer carry.</item>
/// <item><b>The survey floor</b>: at least half the race-unlock factions get a numeric route.
/// Under it DRA-728 §6 S1 says STOP and escalate to Planner — that is the point Alanna's
/// guide would earn a harvest ask. Measured at 26 of 40 when the slice landed.</item>
/// <item><b>The refusals</b>: committed NEGATIVES — the two conflicts, the (+?) rows, a
/// message delivery, a direction-only page — that must never ship a route, beside committed
/// POSITIVES that read back exactly, negative deltas included.</item>
/// </list>
///
/// <para>Byte-reproducibility is the Python <c>--check</c> in <c>check.ps1</c> and CI; the
/// parser's arms are its <c>--selftest</c>. Not spawned from xunit, for the reason
/// <c>ZoneErasTests</c> gives.</para>
/// </summary>
public class FactionRoutesTests
{
    private static readonly FactionRoutes Shipped = FactionRoutes.LoadEmbedded();

    private static IEnumerable<string> FixtureLines(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "achievements", name));

    /// <summary>Every race-unlock faction in the committed dumps, spelled as the GAME spells it
    /// in the achievements text, and what the shipped routes can say about it. Measured
    /// 2026-10-02: 26 routed, 11 direction-only, 3 none (the three Gukta factions, which
    /// no cached page names at all).</summary>
    private static readonly Dictionary<string, FactionRoutes.Status> MustList = new()
    {
        ["Clerics of Tunare"] = FactionRoutes.Status.Routed,
        ["Clurg"] = FactionRoutes.Status.Routed,
        ["Coalition of Tradesfolk"] = FactionRoutes.Status.Routed,
        ["Corrupt Qeynos Guard"] = FactionRoutes.Status.Routed,
        ["Da Bashers"] = FactionRoutes.Status.Routed,
        ["Dark Bargainers"] = FactionRoutes.Status.Routed,
        ["Dark Ones"] = FactionRoutes.Status.DirectionOnly,
        ["Deepwater Knights"] = FactionRoutes.Status.DirectionOnly,
        ["Dreadguard Inner"] = FactionRoutes.Status.Routed,
        ["Dreadguard Outer"] = FactionRoutes.Status.Routed,
        ["Eldritch Collective"] = FactionRoutes.Status.Routed,
        ["Emerald Warriors"] = FactionRoutes.Status.DirectionOnly,
        ["Freeport Militia"] = FactionRoutes.Status.Routed,
        ["Gem Choppers"] = FactionRoutes.Status.Routed,
        ["Grobb Merchants"] = FactionRoutes.Status.Routed,
        ["Guardians of the Vale"] = FactionRoutes.Status.Routed,
        ["Guards of Qeynos"] = FactionRoutes.Status.Routed,
        ["Guktan Elders"] = FactionRoutes.Status.None,
        ["Guktan Suppliers"] = FactionRoutes.Status.None,
        ["Heretics"] = FactionRoutes.Status.DirectionOnly,
        ["High Council of Erudin"] = FactionRoutes.Status.Routed,
        ["Kazon Stormhammer"] = FactionRoutes.Status.Routed,
        ["Keepers of the Art"] = FactionRoutes.Status.Routed,
        ["Kelethin Merchants"] = FactionRoutes.Status.DirectionOnly,
        ["King Ak`Anon"] = FactionRoutes.Status.Routed,
        ["Knights of Truth"] = FactionRoutes.Status.Routed,
        ["Merchants of Felwithe"] = FactionRoutes.Status.DirectionOnly,
        ["Merchants of Halas"] = FactionRoutes.Status.Routed,
        ["Merchants of Kaladim"] = FactionRoutes.Status.DirectionOnly,
        ["Merchants of Oggok"] = FactionRoutes.Status.DirectionOnly,
        ["Merchants of Qeynos"] = FactionRoutes.Status.DirectionOnly,
        ["Merchants of Rivervale"] = FactionRoutes.Status.Routed,
        ["New Sebilisian Expedition"] = FactionRoutes.Status.Routed,
        ["Oggok Guards"] = FactionRoutes.Status.Routed,
        ["Priests of Mischief"] = FactionRoutes.Status.DirectionOnly,
        ["Protectors of Gukta"] = FactionRoutes.Status.None,
        ["Rogues of the White Rose"] = FactionRoutes.Status.Routed,
        ["Soldiers of Tunare"] = FactionRoutes.Status.Routed,
        ["Storm Guard"] = FactionRoutes.Status.DirectionOnly,
        ["Wolves of the North"] = FactionRoutes.Status.Routed,
    };

    private static HashSet<string> RaceUnlockFactionsInFixtures()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "hateborne.txt", "averaj.txt" })
            foreach (var race in UnlockRequirements.Races(AchievementsImport.Parse(FixtureLines(file))))
                foreach (var c in race.Criteria.Where(c => c.Need == UnlockNeed.MaxFaction))
                    names.Add(c.Subject);
        return names;
    }

    // ---- The file is really there --------------------------------------------------------

    [Fact]
    public void TheShippedCatalogLoadsAndIsNotEmpty()
    {
        Assert.True(Shipped.Routes.Count > 0,
            "FactionRoutes.json did not load — an embedded resource that silently answers " +
            "nothing is a guard aimed at nothing (trap 78).");
        Assert.Equal(51, Shipped.Routes.Count);
    }

    // ---- The must-list ------------------------------------------------------------------

    /// <summary>The curated list is the fixtures' list, both ways: a race-unlock faction the
    /// game names that nobody decided a status for reddens, and so does a stale row.</summary>
    [Fact]
    public void TheMustListIsEveryRaceUnlockFactionTheFixturesName()
    {
        var fixtures = RaceUnlockFactionsInFixtures();
        Assert.True(fixtures.Count >= 30, $"only {fixtures.Count} race-unlock factions parsed");
        Assert.Empty(fixtures.Except(MustList.Keys));
        Assert.Empty(MustList.Keys.Except(fixtures));
    }

    [Fact]
    public void EveryRaceUnlockFactionHasItsCommittedStatus()
    {
        var wrong = MustList
            .Where(p => Shipped.StatusFor(p.Key) != p.Value)
            .Select(p => $"{p.Key}: committed {p.Value}, shipped {Shipped.StatusFor(p.Key)}")
            .ToList();
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>DRA-728 §6 S1's stop rule, executable: under half and the slice stops and
    /// escalates to Planner rather than reaching for a harvest.</summary>
    [Fact]
    public void AtLeastHalfTheRaceUnlockFactionsHaveANumericRoute()
    {
        var routed = MustList.Keys.Count(f => Shipped.StatusFor(f) == FactionRoutes.Status.Routed);
        Assert.True(routed * 2 >= MustList.Count,
            $"{routed} of {MustList.Count} race-unlock factions have a numeric route — under " +
            "half. STOP and escalate to Planner (DRA-728 §6 S1); do not harvest.");
    }

    /// <summary>The fold is <see cref="FactionNames.Same"/>'s: the four spellings the game and
    /// the wiki disagree on still find their route, and a near-miss does not.</summary>
    [Fact]
    public void TheGameSpellingFindsTheWikiSpellingAndNothingLooser()
    {
        Assert.NotEmpty(Shipped.Raising("Coalition of Tradesfolk"));   // wiki: Tradefolk
        Assert.NotEmpty(Shipped.Raising("Freeport Militia"));           // wiki: The Freeport Militia
        Assert.NotEmpty(Shipped.Raising("Corrupt Qeynos Guard"));       // wiki: Corrupt Qeynos Guards
        Assert.NotEmpty(Shipped.Raising("King Ak`Anon"));               // wiki: King AkAnon
        // "Coalition of Tradefolk Underground" is a different faction, routed in its own right;
        // it must not answer for the race-unlock one.
        Assert.DoesNotContain(Shipped.Raising("Coalition of Tradesfolk"),
            r => r.Quest == "Bandages for Honeybugger");
        Assert.Equal(FactionRoutes.Status.None, Shipped.StatusFor("Not A Faction Anyone Has"));
    }

    // ---- Committed positives: real rows read back exactly --------------------------------

    [Fact]
    public void TheBottleOfRedWineRowCarriesItsCellsVerbatim()
    {
        var r = Assert.Single(Shipped.Routes, r => r.Quest == "Bottle of Red Wine");
        Assert.Equal([new FactionRoutes.TurnInItem("Red Wine", 1)], r.Items);
        Assert.Equal(
            [new("Dreadguard Outer", 5), new("Dreadguard Inner", 5), new FactionRoutes.FactionDelta("Dark Bargainers", 10)],
            r.Factions);
        Assert.Equal("Apprehensive Dark Bargainers (or Stealth)", r.Requirement);
        Assert.Equal("Purchased Item\nStackable Items\nStacked Turn-Ins", r.Obtain);
        Assert.Equal(["All Positive Faction Quests"], r.Sources);
    }

    /// <summary>Negative deltas are KEPT — the cost is the first thing a grinder needs.</summary>
    [Fact]
    public void NegativeDeltasShipBesideThePositiveOnes()
    {
        var sashes = Assert.Single(Shipped.Routes, r => r.Quest == "Bandit Sashes");
        Assert.Contains(new FactionRoutes.FactionDelta("Bloodsabers", -20), sashes.Factions);
        Assert.Contains(new FactionRoutes.FactionDelta("Knights of Thunder", 20), sashes.Factions);

        var revenge = Assert.Single(Shipped.Routes, r => r.Quest == "Clurg's Revenge");
        Assert.Contains(new FactionRoutes.FactionDelta("Kazon Stormhammer", -15), revenge.Factions);
        Assert.Contains(new FactionRoutes.FactionDelta("Clurg", 15), revenge.Factions);

        Assert.True(Shipped.Routes.SelectMany(r => r.Factions).Count(f => f.Delta < 0) >= 20);
    }

    /// <summary>A page route takes its item from the CATALOG — the join the plan names — and
    /// where the table agrees the route ships once with both pages cited (trap 4).</summary>
    [Fact]
    public void APageRouteTakesTheCatalogsItemAndAnAgreeingTableMergesIntoIt()
    {
        var lizard = Assert.Single(Shipped.Routes, r => r.Quest == "Lizard Tails");
        Assert.Equal([new FactionRoutes.TurnInItem("Lizard Tail", 1)], lizard.Items);
        Assert.Equal(["Lizard Tails"], lizard.Sources);

        var shondo = Assert.Single(Shipped.Routes, r => r.Quest == "Shondo and the Tonic");
        // The table spells it "Vastly"; the catalog's spelling is what ships.
        Assert.Equal([new FactionRoutes.TurnInItem("Vasty Deep Ale", 1)], shondo.Items);
        Assert.Equal(["All Positive Faction Quests", "Shondo and the Tonic"], shondo.Sources);
    }

    /// <summary>Trap 73's telltale. +5 is the game's ordinary step, so most amounts agreeing
    /// is expected — but a parse that latched onto one shared number would ship ONE value.</summary>
    [Fact]
    public void TheAmountsAreNotATemplate()
    {
        var amounts = Shipped.Routes.SelectMany(r => r.Factions).Select(f => f.Delta).ToList();
        Assert.True(amounts.Distinct().Count() >= 8,
            $"{amounts.Distinct().Count()} distinct amounts across {amounts.Count} — a template, not a reading.");
        Assert.DoesNotContain(0, amounts);
        Assert.All(Shipped.Routes, r =>
        {
            Assert.NotEmpty(r.Items);
            Assert.All(r.Items, i => Assert.True(i.Count > 0 && i.Item.Length > 0, r.Quest));
            Assert.NotEmpty(r.Factions);
            Assert.NotEmpty(r.Sources);
        });
    }

    // ---- Committed negatives: what must never ship as a route ----------------------------

    /// <summary>Each is a real quest, refused for its own named reason by the transform (and
    /// listed in <c>faction-routes-report.md</c>). A route for any of them is a guess.</summary>
    [Theory]
    [InlineData("Gnoll Bounty")]            // table 3x vs catalog 1x — sources disagree
    [InlineData("Red Wine to Lady Shae")]   // table 4x vs catalog 1x — sources disagree
    [InlineData("Bladed Weapons")]          // every amount (+?)
    [InlineData("Evil Research")]           // OR alternatives, and (+?)
    [InlineData("Hogcaller's Inn")]         // (+?) and (-??)
    [InlineData("Fabian's Strings")]        // coin in the turn-in
    [InlineData("Note for Janam")]          // a message delivery
    [InlineData("Orc Vest")]                // OR, and coin
    [InlineData("Lion Meat Shipment Quest")] // no turn-in item in the catalog
    [InlineData("Muffin Quests")]           // direction-only facblock
    [InlineData("Innoruuk Recommendation")] // two different numeric blocks (+200 and +10)
    public void ARefusedQuestShipsNoRoute(string quest) =>
        Assert.DoesNotContain(Shipped.Routes, r => r.Quest == quest);

    /// <summary>A refused raise is still NAMED — "the wiki says it moves, we hold no number"
    /// is a different sentence from silence.</summary>
    [Fact]
    public void ARefusedRaiseIsReportedWithItsReason()
    {
        var emerald = Shipped.UnroutedFor("Emerald Warriors");
        Assert.Contains(emerald, m => m.Quest == "Red Wine to Lady Shae" && m.Why == "sources disagree");
        Assert.Contains(emerald, m => m.Quest == "Hogcaller's Inn"
                                      && m.Why == "several turn-in items, one faction block");
        Assert.Contains(Shipped.UnroutedFor("Merchants of Oggok"),
            m => m.Quest == "Muffin Quests" && m.Why == "amount not stated");
    }
}
