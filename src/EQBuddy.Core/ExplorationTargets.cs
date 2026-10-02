namespace EQBuddy.Core;

/// <summary>What an exploration place IS, as far as EQBuddy can say (DRA-754 D1).</summary>
public enum ExplorationKind
{
    /// <summary>A world zone the shipped catalogs name — <see cref="ExplorationPlace.Zone"/>
    /// carries the canonical spelling.</summary>
    World,
    /// <summary>A world place no shipped file names as a zone. REPORTED by name, never
    /// guessed at (Dragoncrypt, Freeport Sewers, Shadowrest, The Caverns of Exile today).</summary>
    WorldUnread,
    /// <summary>A house, guild hall, guild lobby or wedding chapel — a place each player has
    /// their own copy of, which no map and no route can point at.</summary>
    PlayerInstanced,
}

/// <summary>What the Helper does with a kind. <see cref="ExplorationTargets.ShapeFor"/> is
/// the must-list.</summary>
public enum ExplorationShape
{
    /// <summary>An open one is a candidate row (D2's engine).</summary>
    Ranked,
    /// <summary>Said once under the rows, by name, capped with "and N more" (trap 50).</summary>
    ReportedByName,
    /// <summary>Counted in one line; there is nothing to route to.</summary>
    CountedOnly,
}

/// <summary>A regional "&lt;Region&gt; Explorer" achievement and where it stands.</summary>
public sealed record ExplorerProgress(string Name, int Done, int Total);

/// <summary>
/// One distinct place the dump's Exploration section names.
/// </summary>
/// <param name="Place">The dump's own spelling, with " Traveler" taken off.</param>
/// <param name="Rows">How many dump rows name it — usually two (an Explorer criterion and its
/// own "&lt;Place&gt; Traveler" achievement). Kept so the unit is visibly the PLACE.</param>
/// <param name="Complete">Every row says complete. A place whose rows disagree is NOT complete.</param>
/// <param name="RowsDisagree">Its rows carry both flags. Counted and reported, left open.</param>
/// <param name="Zone">The universe's canonical spelling, for <see cref="ExplorationKind.World"/>
/// only.</param>
/// <param name="GraphNode">The <see cref="ZoneGraph"/> node it routes to, resolved by this
/// fold and never by <see cref="ZoneGraph.Resolve"/>'s containment. Null = no route.</param>
public sealed record ExplorationPlace(
    string Place,
    ExplorationKind Kind,
    int Rows,
    bool Complete,
    bool RowsDisagree,
    IReadOnlyList<ExplorerProgress> Explorers,
    string? Zone,
    string? GraphNode);

/// <summary>
/// The zone names EQBuddy ships, and the one rule that matches a dump's place onto them
/// (DRA-754 §1).
///
/// <para>The universe is the union of six named sources: <see cref="ZoneLevels"/>,
/// <see cref="ZoneEras"/>, <see cref="ZoneGraph"/>, <see cref="SpawnCatalog"/> (title and the
/// log's spelling), <see cref="QuestCatalog"/> (start zone and zones) and the item catalog's
/// <c>DropZones</c> — <b>the last through <see cref="TradeskillMaterials.IsPlace"/></b>, because
/// 16 of its raw values are <c>}}</c>, <c>:* …</c> and their like, and a wiki fragment that
/// entered the universe would "resolve" a place that happens to share its key.</para>
///
/// <para>Matching is EXACT title, case-insensitive, then <see cref="ZoneMapFiles.IdentityKey"/>,
/// and <b>nothing looser</b> — the <see cref="ZoneLevels"/>/<see cref="ZoneEras"/> rule verbatim.
/// No containment, no distance and no alias table: the four world places this misses are not
/// another zone's spelling (plan §1), and an alias table arrives with its first evidenced row.</para>
///
/// <para><b>The route is resolved SEPARATELY</b>, by the same rule over the graph's own nodes.
/// <see cref="ZoneGraph.Resolve"/> falls back to longest containment, so the canonical name
/// "Commonlands" (what "The Commonlands" resolves to) would come back as one of the East/West
/// pair the graph holds — a route to a place the player was not asked to visit. A key two
/// graph nodes share answers no route rather than a coin toss.</para>
/// </summary>
public sealed class ExplorationUniverse
{
    private readonly Dictionary<string, string> _byTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _graphByTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _graphByKey = new(StringComparer.Ordinal);

    /// <summary>Distinct names, case-insensitive.</summary>
    public int Count => _byTitle.Count;

    public IReadOnlyCollection<string> Names => _byTitle.Keys;

    /// <param name="names">Every admitted zone name, in PRIORITY order: where two spellings
    /// fold to one key, the first one given is the canonical one.</param>
    /// <param name="graphNodes">The nodes a route can be computed between.</param>
    public ExplorationUniverse(IEnumerable<string> names, IEnumerable<string> graphNodes)
    {
        foreach (var raw in names)
        {
            var name = raw.Trim();
            if (name.Length == 0 || _byTitle.ContainsKey(name)) continue;
            _byTitle[name] = name;
            var key = ZoneMapFiles.IdentityKey(name);
            if (key.Length != 0) _byKey.TryAdd(key, name);
        }
        foreach (var raw in graphNodes)
        {
            var node = raw.Trim();
            if (node.Length == 0 || _graphByTitle.ContainsKey(node)) continue;
            _graphByTitle[node] = node;
            var key = ZoneMapFiles.IdentityKey(node);
            if (key.Length == 0) continue;
            _graphByKey[key] = _graphByKey.ContainsKey(key) ? null : node;
        }
    }

    /// <summary>The canonical zone name for a place, or null. Exact, then the identity key.</summary>
    public string? Resolve(string place)
    {
        var name = place.Trim();
        if (name.Length == 0) return null;
        if (_byTitle.TryGetValue(name, out var exact)) return exact;
        var key = ZoneMapFiles.IdentityKey(name);
        return key.Length != 0 && _byKey.TryGetValue(key, out var folded) ? folded : null;
    }

    /// <summary>The graph node a place routes to, or null. Exact, then the identity key, over
    /// the graph's OWN nodes — never <see cref="ZoneGraph.Resolve"/>.</summary>
    public string? GraphNode(string place)
    {
        var name = place.Trim();
        if (name.Length == 0) return null;
        if (_graphByTitle.TryGetValue(name, out var exact)) return exact;
        var key = ZoneMapFiles.IdentityKey(name);
        return key.Length != 0 && _graphByKey.TryGetValue(key, out var node) ? node : null;
    }

    /// <summary>The universe over the shipped catalogs. The order is the canonical-spelling
    /// priority: the graph first (it is what a route is asked in), then the two wiki zone
    /// files, then the spawn, quest and item catalogs.</summary>
    public static ExplorationUniverse FromCatalogs(ZoneGraph graph, ZoneLevels levels, ZoneEras eras,
        SpawnCatalog spawns, QuestCatalog quests, ItemCatalog items)
    {
        IEnumerable<string> Names()
        {
            foreach (var z in graph.Zones) yield return z;
            foreach (var z in levels.Titles) yield return z;
            foreach (var z in eras.Titles) yield return z;
            foreach (var zone in spawns.Zones)
            {
                yield return zone.Zone;
                yield return zone.LogZoneName;
            }
            foreach (var q in quests.Quests)
            {
                yield return q.StartZone;
                foreach (var z in q.Zones) yield return z;
            }
            foreach (var r in items.All)
                foreach (var z in r.DropZones ?? [])
                    if (TradeskillMaterials.IsPlace(z)) yield return z;
        }
        return new ExplorationUniverse(Names(), graph.Zones);
    }

    private static ExplorationUniverse? _default;
    private static readonly object DefaultLock = new();

    /// <summary>Lazy: the item catalog's gunzip happens on first use, not at the first dump read.</summary>
    public static ExplorationUniverse Default
    {
        get
        {
            if (_default is { } d) return d;
            lock (DefaultLock)
                return _default ??= FromCatalogs(ZoneGraph.LoadEmbedded(), ZoneLevels.Default,
                    ZoneEras.Default, SpawnCatalog.LoadEmbedded(), QuestCatalog.LoadEmbedded(),
                    ItemCatalog.Default);
        }
    }
}

/// <summary>
/// The Achievements engine's fold: the dump's "EverQuest: Exploration" section as distinct
/// PLACES (DRA-754 D1). No UI reads it yet — D2's engine does.
///
/// <para><b>The unit is the place, not the row.</b> The section names most places twice: as a
/// "&lt;Place&gt; Traveler" criterion of one of ten regional "&lt;Region&gt; Explorer"
/// achievements, and as its own "&lt;Place&gt; Traveler" achievement with one
/// "Visit &lt;Place&gt;" criterion. 186 rows are 95 places in both committed fixtures; counting
/// rows is DRA-749's 207/146/71% that did not stand.</para>
///
/// <para><b>The dump is the authority on completion</b> and nothing here writes. A place is
/// complete when every row naming it says so. Rows that DISAGREE are counted and reported, and
/// the place stays open — no rule here picks a winner (0 of 95 in both fixtures today).</para>
///
/// <para>Built from the same parsed entries <see cref="UnlockRequirements"/> reads, by
/// <see cref="UnlockSource.Exploration"/> — one producer (trap 4).</para>
/// </summary>
public sealed record ExplorationTargets(IReadOnlyList<ExplorationPlace> Places)
{
    public const string Section = "EverQuest: Exploration";
    private const string Traveler = " Traveler";
    private const string Visit = "Visit ";

    /// <summary>Places each player has their own copy of. CURATED, and each prefix's evidence is
    /// the dump row that names it: "House (Bixie Hive) Traveler", "Guild Hall (Grand) Traveler",
    /// "Guild Lobby Traveler", "Wedding Chapel (Dark) Traveler" (both committed fixtures).
    /// Matched as a whole leading word run — "House" and "House (…)", never "Housefly".</summary>
    public static readonly IReadOnlyList<string> PlayerInstancedPrefixes =
        ["House", "Guild Hall", "Guild Lobby", "Wedding Chapel"];

    public static ExplorationTargets Empty { get; } = new([]);

    /// <summary>What the Helper does with each kind; null = undecided. Every member of
    /// <see cref="ExplorationKind"/> must answer (ExplorationTargetsTests).</summary>
    public static ExplorationShape? ShapeFor(ExplorationKind kind) => kind switch
    {
        ExplorationKind.World => ExplorationShape.Ranked,
        ExplorationKind.WorldUnread => ExplorationShape.ReportedByName,
        ExplorationKind.PlayerInstanced => ExplorationShape.CountedOnly,
        _ => null,
    };

    public int PlayerInstancedCount => Places.Count(p => p.Kind == ExplorationKind.PlayerInstanced);
    public IEnumerable<ExplorationPlace> World => Places.Where(p => p.Kind != ExplorationKind.PlayerInstanced);
    public int WorldCount => World.Count();
    public int ResolvedCount => Places.Count(p => p.Kind == ExplorationKind.World);
    public int RoutableCount => World.Count(p => p.GraphNode is not null);

    /// <summary>World places no shipped file names, by the dump's own spelling.</summary>
    public IReadOnlyList<string> Unread =>
        [.. Places.Where(p => p.Kind == ExplorationKind.WorldUnread).Select(p => p.Place)];

    /// <summary>World places this character has not visited — resolved or not.</summary>
    public IEnumerable<ExplorationPlace> OpenWorld => World.Where(p => !p.Complete);

    /// <summary>Places whose rows disagree on completion, by name.</summary>
    public IReadOnlyList<string> Disagreeing =>
        [.. Places.Where(p => p.RowsDisagree).Select(p => p.Place)];

    public static bool IsPlayerInstanced(string place)
    {
        var p = place.Trim();
        foreach (var prefix in PlayerInstancedPrefixes)
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (p.Length == prefix.Length || p[prefix.Length] is ' ' or '('))
                return true;
        return false;
    }

    public static ExplorationTargets From(IEnumerable<AchievementEntry> achievements, ExplorationUniverse universe)
    {
        var flags = new Dictionary<string, List<bool>>(StringComparer.OrdinalIgnoreCase);
        var spelling = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var explorersOf = new Dictionary<string, List<ExplorerProgress>>(StringComparer.OrdinalIgnoreCase);

        void Row(string place, bool complete)
        {
            place = place.Trim();
            if (place.Length == 0) return;
            if (!flags.TryGetValue(place, out var list))
            {
                flags[place] = list = [];
                spelling[place] = place;
            }
            list.Add(complete);
        }

        foreach (var a in achievements)
        {
            if (!a.Section.Equals(Section, StringComparison.OrdinalIgnoreCase)) continue;

            // A regional Explorer: its criteria are "<Place> Traveler".
            var places = a.Criteria.Where(c => c.Text.EndsWith(Traveler, StringComparison.Ordinal)).ToList();
            if (places.Count > 0)
            {
                var progress = new ExplorerProgress(a.Name,
                    a.Criteria.Count(c => c.Complete), a.Criteria.Count);
                foreach (var (text, complete) in places)
                {
                    var place = text[..^Traveler.Length].Trim();
                    Row(place, complete);
                    if (!explorersOf.TryGetValue(place, out var list)) explorersOf[place] = list = [];
                    list.Add(progress);
                }
            }

            // A standalone "<Place> Traveler": its one "Visit …" criterion is the row. The
            // place is named by the ACHIEVEMENT — the criterion's wording drifts ("Visit a
            // Bixie Hive House", "Visit the Dark Wedding Chapel").
            if (a.Name.EndsWith(Traveler, StringComparison.Ordinal))
                foreach (var (text, complete) in a.Criteria)
                    if (text.StartsWith(Visit, StringComparison.Ordinal))
                        Row(a.Name[..^Traveler.Length], complete);
        }

        var result = new List<ExplorationPlace>(flags.Count);
        foreach (var (key, rows) in flags)
        {
            var place = spelling[key];
            var disagree = rows.Contains(true) && rows.Contains(false);
            var complete = !disagree && rows.All(f => f);
            var explorers = explorersOf.TryGetValue(key, out var e) ? (IReadOnlyList<ExplorerProgress>)e : [];
            if (IsPlayerInstanced(place))
            {
                result.Add(new ExplorationPlace(place, ExplorationKind.PlayerInstanced, rows.Count,
                    complete, disagree, explorers, null, null));
                continue;
            }
            var zone = universe.Resolve(place);
            result.Add(new ExplorationPlace(place,
                zone is null ? ExplorationKind.WorldUnread : ExplorationKind.World,
                rows.Count, complete, disagree, explorers, zone, universe.GraphNode(place)));
        }
        return new ExplorationTargets(result);
    }
}
