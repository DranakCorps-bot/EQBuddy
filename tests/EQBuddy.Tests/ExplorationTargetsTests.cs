using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-754 D1: the dump's Exploration section as distinct PLACES, matched onto the shipped
/// zone names by exact title then <see cref="ZoneMapFiles.IdentityKey"/> and nothing looser.
///
/// The census is pinned on both committed fixtures to the numbers the plan's survey
/// (<c>scripts/dra754-exploration-survey.py</c>) measured, so the C# rule and the instrument
/// the plan was signed on cannot drift apart. The plan's stop-and-escalate seam is a floor
/// of 70 resolved world places; a refresh that moves the pin below it is a new plan ask.
/// </summary>
public class ExplorationTargetsTests
{
    private static List<AchievementEntry> Dump(string who) =>
        AchievementsImport.Parse(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "achievements", $"{who}.txt")));

    private static ExplorationTargets Fold(string who) =>
        ExplorationTargets.From(Dump(who), ExplorationUniverse.Default);

    [Theory]
    [InlineData("averaj", 109, 24)]
    [InlineData("hateborne", 87, 35)]
    public void TheCensusIsTheOneThePlanWasSignedOn(string who, int completeRows, int openWorld)
    {
        var t = Fold(who);

        // 186 rows name 95 places: the unit is the PLACE (the rows-not-places mutant reads 186).
        Assert.Equal(186, t.Places.Sum(p => p.Rows));
        Assert.Equal(95, t.Places.Count);
        Assert.Equal(16, t.PlayerInstancedCount);
        Assert.Equal(79, t.WorldCount);
        Assert.Equal(75, t.ResolvedCount);
        Assert.Equal(73, t.RoutableCount);
        Assert.Equal(["Dragoncrypt", "Freeport Sewers", "Shadowrest", "The Caverns of Exile"],
            t.Unread.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(openWorld, t.OpenWorld.Count());
        Assert.Empty(t.Disagreeing);

        // The survey's row-level complete count, read back through the places' own rows.
        Assert.Equal(completeRows, t.Places.Where(p => p.Complete).Sum(p => p.Rows));

        // The plan's stop-and-escalate seam, stated where a refresh would trip it.
        Assert.True(t.ResolvedCount >= 70, $"{who}: {t.ResolvedCount} world places resolved — below the plan's floor of 70; STOP and escalate");
    }

    [Fact]
    public void FreeportSewersResolvesToNothingAndGetsNoRoute()
    {
        var u = ExplorationUniverse.Default;
        Assert.Null(u.Resolve("Freeport Sewers"));
        Assert.Null(u.GraphNode("Freeport Sewers"));

        // MEASURED, against the plan's §4 wording: the travel resolver's containment does NOT
        // reach this one as spelled — no graph node contains "Freeport Sewers" or is contained
        // by it. The containment it guards against lives in the universe matcher instead: a
        // looser Resolve hands it a Freeport (the containment mutant reddens the census).
        Assert.Null(ZoneGraph.LoadEmbedded().Resolve("Freeport Sewers"));

        var place = Assert.Single(Fold("averaj").Places, p => p.Place == "Freeport Sewers");
        Assert.Equal(ExplorationKind.WorldUnread, place.Kind);
        Assert.Null(place.Zone);
        Assert.Null(place.GraphNode);
        Assert.False(place.Complete);
    }

    [Fact]
    public void TheCommonlandsResolvesByItsKeyButGetsNoRoute()
    {
        var u = ExplorationUniverse.Default;
        var graph = ZoneGraph.LoadEmbedded();

        // Resolves — through the identity key, since no shipped file spells it with "The".
        Assert.DoesNotContain("The Commonlands", u.Names, StringComparer.OrdinalIgnoreCase);
        var zone = u.Resolve("The Commonlands");
        Assert.Equal("Commonlands", zone);

        // No route: the graph holds East and West Commonlands only. The trap is the RESOLVED
        // name: handed to the travel resolver, "Commonlands" is contained in both and comes
        // back as one of them — the ZoneLevels rule's own Commonlands case.
        Assert.DoesNotContain(graph.Zones, z => ZoneMapFiles.IdentityKey(z) == "commonlands");
        Assert.Null(u.GraphNode("The Commonlands"));
        Assert.Null(u.GraphNode(zone!));
        Assert.Null(graph.Resolve("The Commonlands"));   // as spelled, containment misses too
        Assert.EndsWith("Commonlands", graph.Resolve(zone!));

        var place = Assert.Single(Fold("hateborne").Places, p => p.Place == "The Commonlands");
        Assert.Equal(ExplorationKind.World, place.Kind);
        Assert.Null(place.GraphNode);
    }

    [Fact]
    public void AWikiFragmentInDropZonesNeverEntersTheUniverse()
    {
        var items = new ItemCatalog([
            new ItemCatalog.Record { Name = "Shard", DropZones = ["}}", ":* Dread", "Wyrmwatch"] },
        ]);
        var u = ExplorationUniverse.FromCatalogs(new ZoneGraph(), new ZoneLevels(), new ZoneEras(),
            new SpawnCatalog(), new QuestCatalog(), items);

        Assert.Equal(["Wyrmwatch"], u.Names.ToArray());
        Assert.Null(u.Resolve("}}"));
        Assert.Null(u.Resolve(":* Dread"));

        // And the shipped universe: the 16 raw values IsPlace refuses are not in it.
        var shipped = ExplorationUniverse.Default.Names;
        Assert.DoesNotContain(shipped, n => !TradeskillMaterials.IsPlace(n));
    }

    [Fact]
    public void TheMatcherIsExactThenTheIdentityKeyAndNothingLooser()
    {
        var u = new ExplorationUniverse(["West Commonlands", "Estate of Unrest"], ["West Commonlands"]);
        Assert.Equal("Estate of Unrest", u.Resolve("The Estate of Unrest"));   // key
        Assert.Equal("West Commonlands", u.Resolve("west commonlands"));       // exact, any case
        Assert.Null(u.Resolve("Commonlands"));                                 // containment: refused
        Assert.Null(u.Resolve("West Commonlands Sewers"));                     // containment: refused
        Assert.Null(u.GraphNode("Commonlands"));
    }

    [Fact]
    public void AKeyTwoGraphNodesShareAnswersNoRouteRatherThanACoinToss()
    {
        var u = new ExplorationUniverse(["Chardok"], ["Chardok (Pre-Revamp)", "Chardok (Post-Revamp)"]);
        Assert.Equal("Chardok", u.Resolve("Chardok"));
        Assert.Null(u.GraphNode("Chardok"));
        Assert.Equal("Chardok (Pre-Revamp)", u.GraphNode("chardok (pre-revamp)"));
    }

    [Fact]
    public void EveryKindHasADecidedShapeAndAnUndecidedOneAnswersNull()
    {
        foreach (var kind in Enum.GetValues<ExplorationKind>())
            Assert.NotNull(ExplorationTargets.ShapeFor(kind));
        Assert.Equal(ExplorationShape.Ranked, ExplorationTargets.ShapeFor(ExplorationKind.World));
        Assert.Equal(ExplorationShape.ReportedByName, ExplorationTargets.ShapeFor(ExplorationKind.WorldUnread));
        Assert.Equal(ExplorationShape.CountedOnly, ExplorationTargets.ShapeFor(ExplorationKind.PlayerInstanced));
        Assert.Null(ExplorationTargets.ShapeFor((ExplorationKind)99));
    }

    [Fact]
    public void EachInstancedPrefixIsEvidencedByADumpRowAndMatchesOnlyAWholeWord()
    {
        var places = Fold("averaj").Places;
        foreach (var prefix in ExplorationTargets.PlayerInstancedPrefixes)
            Assert.Contains(places, p => p.Kind == ExplorationKind.PlayerInstanced
                                         && p.Place.StartsWith(prefix, StringComparison.Ordinal));

        Assert.True(ExplorationTargets.IsPlayerInstanced("Guild Lobby"));
        Assert.True(ExplorationTargets.IsPlayerInstanced("House (Bixie Hive)"));
        Assert.False(ExplorationTargets.IsPlayerInstanced("Housefly Hollow"));
        Assert.False(ExplorationTargets.IsPlayerInstanced("High Keep"));
    }

    [Fact]
    public void APlaceCarriesTheExplorersItAdvancesAndWhereTheyStand()
    {
        var t = Fold("averaj");
        var sewers = Assert.Single(t.Places, p => p.Place == "Freeport Sewers");
        var explorer = Assert.Single(sewers.Explorers);
        Assert.Equal("Northeast Antonica Explorer", explorer.Name);
        Assert.True(explorer.Total > explorer.Done);

        // The Oasis of Marr has its own Traveler achievement and no regional Explorer.
        var oasis = Assert.Single(t.Places, p => p.Place == "The Oasis of Marr");
        Assert.Empty(oasis.Explorers);
        Assert.Equal(1, oasis.Rows);
    }

    [Fact]
    public void RowsThatDisagreeAreReportedAndThePlaceStaysOpen()
    {
        List<AchievementEntry> dump =
        [
            new(ExplorationTargets.Section, "Odus Explorer", false,
                [("Paineel Traveler", true), ("Toxxulia Forest Traveler", false)]),
            new(ExplorationTargets.Section, "Paineel Traveler", false, [("Visit Paineel", false)]),
            new(ExplorationTargets.Section, "Toxxulia Forest Traveler", false, [("Visit Toxxulia Forest", false)]),
            new("EverQuest: Hunter", "Paineel Traveler", true, [("Visit Paineel", true)]),
        ];
        var t = ExplorationTargets.From(dump, new ExplorationUniverse(["Paineel", "Toxxulia Forest"], []));

        Assert.Equal(["Paineel"], t.Disagreeing.ToArray());
        var paineel = Assert.Single(t.Places, p => p.Place == "Paineel");
        Assert.False(paineel.Complete);
        Assert.Equal(2, paineel.Rows);   // the Hunter-section row is not an exploration row
        Assert.Equal(2, t.OpenWorld.Count());
    }

    [Fact]
    public void UnlockSourceBuildsExplorationFromTheSameDumpAndRebuildsWhenItMoves()
    {
        var game = Directory.CreateTempSubdirectory("eqb-explore");
        try
        {
            var logs = Directory.CreateDirectory(Path.Combine(game.FullName, "Logs")).FullName;
            var dump = Path.Combine(game.FullName, "Dranak_freeport-Achievements.txt");
            var source = new UnlockSource();

            Assert.Empty(source.Exploration.Places);   // no dump: empty, never a guess

            File.Copy(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "fixtures", "achievements", "averaj.txt"), dump);
            Assert.True(source.Refresh(logs, "Dranak"));
            Assert.Equal(24, source.Exploration.OpenWorld.Count());

            File.Copy(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "fixtures", "achievements", "hateborne.txt"), dump, overwrite: true);
            File.SetLastWriteTime(dump, DateTime.Now.AddMinutes(1));
            Assert.True(source.Refresh(logs, "Dranak"));
            Assert.Equal(35, source.Exploration.OpenWorld.Count());
        }
        finally { game.Delete(true); }
    }
}
