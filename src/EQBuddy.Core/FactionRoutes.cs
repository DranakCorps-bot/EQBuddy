using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>What eqlwiki says a faction turn-in is worth: hand in THESE items, THIS many per
/// turn-in, and THESE factions move by THESE integer amounts. Promoted into shipped data by
/// <c>scripts/harvests/eqlwiki/faction-routes-transform.py</c> from the COMMITTED cache — the
/// <i>All Positive Faction Quests</i> table and the <c>facblock</c>s on quest pages, joined
/// to turn-in items through <see cref="QuestCatalog"/>. It fetches nothing (DRA-746,
/// DRA-728 D1).
///
/// <para><b>This is a reference, never a recommendation.</b> Every route names the page(s)
/// it was read from (<see cref="Route.Sources"/>), so a surface can say "eqlwiki lists …"
/// rather than present the wiki's number as the player's own. Negative deltas are kept: a
/// route that tanks an opposing faction is the first thing a grinder needs to know.</para>
///
/// <para><b>Requirement and obtain text are shown, never checked.</b> No faction TIER table
/// exists in this repo ("Apprehensive" and "Amiable" appear nowhere else in <c>src/</c>), so
/// "Apprehensive Dark Bargainers" is a sentence to quote, not a gate.</para>
///
/// <para><b>Absent is still an answer.</b> A faction the wiki names as moved with no route
/// EQBuddy could admit — no amount stated, a quest that could not be joined to one turn-in
/// item, or two sources that disagree — ships in <c>Unrouted</c> with the reason, so
/// <see cref="StatusFor"/> can tell "the wiki names it and we hold no number" from "we have
/// never seen it". Read <c>scripts/harvests/eqlwiki/faction-routes-report.md</c> before
/// relying on the coverage.</para>
///
/// <para><b>One reader:</b> DRA-728 D2's cold-start arm in
/// <see cref="UnlockGuidance.Faction"/>, which speaks only when the player's own log holds no
/// raiser for the faction.</para>
/// </summary>
public sealed class FactionRoutes
{
    /// <summary>One item of a turn-in, and how many of it one turn-in takes.</summary>
    public sealed record TurnInItem(string Item, int Count);

    /// <summary>One faction a turn-in moves, by an integer the wiki states. Negative is a cost.</summary>
    public sealed record FactionDelta(string Faction, int Delta);

    /// <summary>A faction the source names beside the route with no amount — <c>(+?)</c> in
    /// the table, "got better" on a page — carried word for word.</summary>
    public sealed record Unquantified(string Faction, string Verbatim);

    /// <summary>One admitted route. <see cref="Quest"/> is the catalog's quest name, so a
    /// reader joins zone and door through <see cref="QuestCatalog"/> rather than through a
    /// copy here (trap 4). <see cref="Requirement"/> and <see cref="Obtain"/> are the table's
    /// own cells (link markup folded, nothing else) and are empty for a page-only route.</summary>
    public sealed record Route(
        string Quest,
        IReadOnlyList<TurnInItem> Items,
        IReadOnlyList<FactionDelta> Factions,
        IReadOnlyList<Unquantified> Unquantified,
        string Requirement,
        string Obtain,
        IReadOnlyList<string> Sources);

    /// <summary>A quest the wiki names as raising a faction, that EQBuddy holds no route for,
    /// and why.</summary>
    public sealed record UnroutedMention(string Quest, string Why);

    /// <summary>What the shipped data can say about one faction. Three outcomes and no fourth.</summary>
    public enum Status
    {
        /// <summary>Nothing in the cache we read names this faction as raised.</summary>
        None,
        /// <summary>The wiki names a raise, and no route could be admitted: the amount is not
        /// stated, the quest could not be joined to one turn-in item, or the sources disagree.
        /// <see cref="UnroutedFor"/> carries which.</summary>
        DirectionOnly,
        /// <summary>At least one admitted route RAISES this faction by a stated amount.</summary>
        Routed,
    }

    private readonly List<Route> _routes = [];
    private readonly List<(string Faction, IReadOnlyList<UnroutedMention> Mentions)> _unrouted = [];

    public IReadOnlyList<Route> Routes => _routes;

    public FactionRoutes() { }

    public FactionRoutes(IEnumerable<Route> routes,
                         IReadOnlyDictionary<string, IReadOnlyList<UnroutedMention>> unrouted)
    {
        _routes.AddRange(routes);
        foreach (var (faction, mentions) in unrouted) _unrouted.Add((faction, mentions));
    }

    /// <summary>Every route that RAISES this faction, in shipped order. The faction name may
    /// be spelled the way the achievements dump, the faction dump or the log spells it —
    /// <see cref="FactionNames.Same"/> is the one fold that decides "same faction", and it is
    /// not re-implemented here.</summary>
    public IReadOnlyList<Route> Raising(string faction) =>
        [.. _routes.Where(r => r.Factions.Any(f => f.Delta > 0 && FactionNames.Same(f.Faction, faction)))];

    /// <summary>The wiki's mentions of a raise to this faction that EQBuddy could not route.</summary>
    public IReadOnlyList<UnroutedMention> UnroutedFor(string faction) =>
        [.. _unrouted.Where(u => FactionNames.Same(u.Faction, faction)).SelectMany(u => u.Mentions)];

    public Status StatusFor(string faction) =>
        Raising(faction).Count > 0 ? Status.Routed
        : UnroutedFor(faction).Count > 0 ? Status.DirectionOnly
        : Status.None;

    public static FactionRoutes LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.FactionRoutes.json");
        if (stream is null) return new FactionRoutes();
        try
        {
            var root = JsonSerializer.Deserialize<Root>(stream);
            if (root is null) return new FactionRoutes();
            var routes = (root.Routes ?? []).Select(r => new Route(
                r.Quest ?? "",
                [.. (r.Items ?? []).Select(i => new TurnInItem(i.Item ?? "", i.Count))],
                [.. (r.Factions ?? []).Select(f => new FactionDelta(f.Faction ?? "", f.Delta))],
                [.. (r.Unquantified ?? []).Select(u => new Unquantified(u.Faction ?? "", u.Verbatim ?? ""))],
                r.Requirement ?? "",
                r.Obtain ?? "",
                [.. r.Sources ?? []]));
            var unrouted = (root.Unrouted ?? []).ToDictionary(
                p => p.Key,
                p => (IReadOnlyList<UnroutedMention>)[.. p.Value.Select(m => new UnroutedMention(m.Quest ?? "", m.Why ?? ""))]);
            return new FactionRoutes(routes, unrouted);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return new FactionRoutes();
        }
    }

    private sealed class Root
    {
        public string? Source { get; set; }
        public List<RouteDto>? Routes { get; set; }
        public Dictionary<string, List<MentionDto>>? Unrouted { get; set; }
    }

    private sealed class RouteDto
    {
        public string? Quest { get; set; }
        public List<ItemDto>? Items { get; set; }
        public List<DeltaDto>? Factions { get; set; }
        public List<UnquantifiedDto>? Unquantified { get; set; }
        public string? Requirement { get; set; }
        public string? Obtain { get; set; }
        public List<string>? Sources { get; set; }
    }

    private sealed class ItemDto { public string? Item { get; set; } public int Count { get; set; } }
    private sealed class DeltaDto { public string? Faction { get; set; } public int Delta { get; set; } }
    private sealed class UnquantifiedDto { public string? Faction { get; set; } public string? Verbatim { get; set; } }
    private sealed class MentionDto { public string? Quest { get; set; } public string? Why { get; set; } }

    private static FactionRoutes? _default;
    private static readonly object DefaultLock = new();

    /// <summary>Lazy, like every other shipped catalog: the parse happens on first use.</summary>
    public static FactionRoutes Default
    {
        get
        {
            if (_default is { } d) return d;
            lock (DefaultLock) return _default ??= LoadEmbedded();
        }
    }
}
