namespace EQBuddy.Core;

/// <summary>What kind of answer a route request got — the three cases both
/// <c>TravelWindow</c>s already hand-roll, named so a caller can switch on the outcome
/// rather than re-deriving it from <see cref="TravelPlanResult.Hops"/>.</summary>
public enum TravelOutcome
{
    /// <summary><see cref="ZoneGraph.Distance"/> found nothing connecting the two zones.</summary>
    NoRoute,
    /// <summary>Zero hops — you are already where you asked to go.</summary>
    AlreadyThere,
    /// <summary>A real route: one or more hops, with the full path.</summary>
    Route,
}

/// <summary>A planned route, or the reason there isn't one. <see cref="Path"/> is the full
/// stop list including the starting zone (empty for <see cref="TravelOutcome.NoRoute"/>).
/// <see cref="Note"/> is the sentence a UI shows above the step list — one of the two
/// wordings both desktop <c>TravelWindow</c>s already carry, moved here so the phone's
/// travel surface (PR 4) reads the same words rather than inventing its own.</summary>
public sealed record TravelPlanResult(TravelOutcome Outcome, string From, string Destination, int Hops, IReadOnlyList<string> Path, string Note);

/// <summary>
/// Pure module over <see cref="ZoneGraph.Distance"/> — from, destination, hops, the step
/// list, and the two wordings both <c>TravelWindow</c>s currently hand-roll. Desktop
/// Routes tab and the phone travel surface (PR 4) read the SAME module; that is the
/// parity mechanism, per the standing rule ("when a surface exists on both, the decision
/// goes in Core/UI.Shared and all three call it").
/// </summary>
public static class TravelPlan
{
    public const string AlreadyThereNote = "You're already there.";

    /// <summary>Plan a route from <paramref name="from"/> to <paramref name="destination"/>.
    /// Both must be non-empty zone names; an empty either side has nothing to plan and is
    /// the caller's job to gate (both desktop windows do — "no zone seen in the log yet",
    /// no destination typed).</summary>
    public static TravelPlanResult Plan(ZoneGraph graph, string from, string destination)
    {
        if (graph.Distance(from, destination) is not { } route)
        {
            return new TravelPlanResult(TravelOutcome.NoRoute, from, destination, 0, [],
                $"No known route from {from} to \"{destination}\" — if a connection is missing, " +
                "its wiki zone page probably lacks the adjacency.");
        }

        if (route.Hops == 0)
            return new TravelPlanResult(TravelOutcome.AlreadyThere, from, destination, 0, route.Path, AlreadyThereNote);

        return new TravelPlanResult(TravelOutcome.Route, from, destination, route.Hops, route.Path,
            $"{route.Hops} zone{(route.Hops == 1 ? "" : "s")} away:");
    }
}
