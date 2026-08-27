using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>Pins the three outcomes both desktop <c>TravelWindow</c>s hand-roll today, so
/// the Routes tab (PR 1) and the phone's travel surface (PR 4) read the exact same words
/// instead of two copies drifting (#122/#152/#184's lesson, one theme early).</summary>
public class TravelPlanTests
{
    private static ZoneGraph Graph() => new(new Dictionary<string, List<string>>
    {
        ["Crushbone"] = ["Blackburrow"],
        ["Blackburrow"] = ["Crushbone", "Rivervale"],
        ["Rivervale"] = ["Blackburrow"],
        ["Kithicor Forest"] = [],
    });

    [Fact]
    public void ARealRouteCarriesHopsAndTheFullPath()
    {
        var result = TravelPlan.Plan(Graph(), "Crushbone", "Rivervale");

        Assert.Equal(TravelOutcome.Route, result.Outcome);
        Assert.Equal(2, result.Hops);
        Assert.Equal(["Crushbone", "Blackburrow", "Rivervale"], result.Path);
        Assert.Equal("2 zones away:", result.Note);
    }

    /// <summary>Singular wording for exactly one hop — the negative for the plural case
    /// above.</summary>
    [Fact]
    public void ASingleHopIsSingular()
    {
        var result = TravelPlan.Plan(Graph(), "Crushbone", "Blackburrow");

        Assert.Equal(TravelOutcome.Route, result.Outcome);
        Assert.Equal(1, result.Hops);
        Assert.Equal("1 zone away:", result.Note);
    }

    [Fact]
    public void AlreadyThereIsZeroHopsWithTheOwnZoneAsThePath()
    {
        var result = TravelPlan.Plan(Graph(), "Crushbone", "Crushbone");

        Assert.Equal(TravelOutcome.AlreadyThere, result.Outcome);
        Assert.Equal(0, result.Hops);
        Assert.Equal(["Crushbone"], result.Path);
        Assert.Equal(TravelPlan.AlreadyThereNote, result.Note);
        Assert.Equal("You're already there.", result.Note);
    }

    /// <summary>The negative for a real route: an unreachable zone gets the wiki-hint
    /// wording and an empty path, never a hop count.</summary>
    [Fact]
    public void NoConnectionGetsTheWikiHintWordingAndAnEmptyPath()
    {
        var result = TravelPlan.Plan(Graph(), "Crushbone", "Kithicor Forest");

        Assert.Equal(TravelOutcome.NoRoute, result.Outcome);
        Assert.Empty(result.Path);
        Assert.Equal(
            "No known route from Crushbone to \"Kithicor Forest\" — if a connection is missing, " +
            "its wiki zone page probably lacks the adjacency.",
            result.Note);
    }

    /// <summary>An unknown zone name resolves the same way as no connection — the graph
    /// cannot tell "never heard of it" from "heard of it, nothing links to it", and both
    /// desktop windows already collapse the two into one message.</summary>
    [Fact]
    public void AnUnknownDestinationIsAlsoNoRoute()
    {
        var result = TravelPlan.Plan(Graph(), "Crushbone", "Plane of Sky");

        Assert.Equal(TravelOutcome.NoRoute, result.Outcome);
        Assert.Empty(result.Path);
    }
}
