using EQBuddy.Companion;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Path tab on a phone (World PR 4) — reads the SAME <see cref="TravelPlan"/> module
/// the desktop Path tab reads (#210's rule), and is never withheld from a device the way
/// the map's geometry is (trap 38): a route is small enough to send every tick.
/// </summary>
public class CompanionTravelTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0);

    private static ZoneGraph Graph() => new(new Dictionary<string, List<string>>
    {
        ["Crushbone"] = ["Blackburrow"],
        ["Blackburrow"] = ["Crushbone", "Rivervale"],
        ["Rivervale"] = ["Blackburrow"],
    });

    private static CompanionSnapshot Build(ZoneGraph? graph, string zone, string? destination) =>
        CompanionProjection.Build(new CompanionInputs
        {
            Stats = new StatsSnapshot { CurrentZone = zone },
            ZoneGraph = graph,
            TravelDestination = destination,
            Offered = [CompanionSurfaces.Travel],
        }, Now);

    [Fact]
    public void NoDestinationPickedAsksForOneAndListsEveryZone()
    {
        var snap = Build(Graph(), "Crushbone", destination: null);

        Assert.NotNull(snap.Travel);
        var travel = snap.Travel!;
        Assert.Equal("Crushbone", travel.From);
        Assert.Null(travel.Destination);
        Assert.Equal("noroute", travel.Outcome);
        Assert.Empty(travel.Path);
        Assert.Equal("Pick a destination.", travel.Note);
        Assert.Contains("Blackburrow", travel.Zones);
        Assert.Contains("Rivervale", travel.Zones);
    }

    [Fact]
    public void APickedDestinationReadsTheSameTravelPlanTheDesktopDoes()
    {
        var snap = Build(Graph(), "Crushbone", "Rivervale");
        var expected = TravelPlan.Plan(Graph(), "Crushbone", "Rivervale");

        var travel = snap.Travel!;
        Assert.Equal("route", travel.Outcome);
        Assert.Equal(expected.Hops, travel.Hops);
        Assert.Equal(expected.Path, travel.Path);
        Assert.Equal(expected.Note, travel.Note);
    }

    [Fact]
    public void AlreadyThereReportsTheOutcomeLowercased()
    {
        var snap = Build(Graph(), "Crushbone", "Crushbone");
        Assert.Equal("alreadythere", snap.Travel!.Outcome);
    }

    /// <summary>No zone graph wired (a test host, or a build before one exists) answers
    /// rather than throwing — the same "a host that hasn't wired it still works" rule
    /// the map's CampFor delegate follows.</summary>
    [Fact]
    public void NoZoneGraphAnswersRatherThanThrowing()
    {
        var snap = Build(null, "Crushbone", "Rivervale");

        var travel = snap.Travel!;
        Assert.Equal("noroute", travel.Outcome);
        Assert.Empty(travel.Zones);
    }

    [Fact]
    public void NotOfferedMeansNoTravelSection()
    {
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Stats = new StatsSnapshot { CurrentZone = "Crushbone" },
            ZoneGraph = Graph(),
            TravelDestination = "Rivervale",
            Offered = [CompanionSurfaces.Map],
        }, Now);

        Assert.Null(snap.Travel);
    }

    // ---- the destination pick, applied ----

    [Fact]
    public void PickingADestinationWritesTheSetting()
    {
        var settings = new AppSettings();
        var changed = CompanionActions.Apply(settings, new CompanionTravelAction("Rivervale"));

        Assert.True(changed);
        Assert.Equal("Rivervale", settings.CompanionTravelDestination);
    }

    [Fact]
    public void ClearingTheDestinationRemovesTheSetting()
    {
        var settings = new AppSettings { CompanionTravelDestination = "Rivervale" };
        var changed = CompanionActions.Apply(settings, new CompanionTravelAction(null));

        Assert.True(changed);
        Assert.Null(settings.CompanionTravelDestination);
    }

    /// <summary>Re-picking the same destination is not a change — no save, no
    /// re-fingerprint, the same "nothing moved" contract every other Apply overload has.</summary>
    [Fact]
    public void PickingTheSameDestinationTwiceIsNotAChange()
    {
        var settings = new AppSettings { CompanionTravelDestination = "Rivervale" };
        Assert.False(CompanionActions.Apply(settings, new CompanionTravelAction("Rivervale")));
    }
}
