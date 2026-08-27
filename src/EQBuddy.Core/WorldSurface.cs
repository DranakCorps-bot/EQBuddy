namespace EQBuddy.Core;

/// <summary>
/// The WORLD theme's tabs, in the order every UI shows them (docs/Themes.md theme 6) —
/// the fifth sibling of <see cref="ProgressSurface"/>, <see cref="QuestSurface"/>,
/// <see cref="CreatureSurface"/> and <see cref="LootSurface"/>. One definition of what
/// the tabs ARE, or the desktop, the Linux widget and the phone drift (#122, #152, #184).
///
/// **This is the heaviest fold of the six**: the zone map, the spawn/camp timer list, the
/// travel router and zone-knowledge sharing become one theme, absorbing the Travels &amp;
/// Deaths card, <c>MapWindow</c>, <c>SpawnsWindow</c>, <c>TravelWindow</c> and
/// <c>ZoneShareWindow</c> (the last stays a desktop dialog opened from the Map tab — its
/// door moves, the window does not).
/// </summary>
public enum WorldTab
{
    /// <summary>The zone map: your last /loc, the trail, camp pins, spawn circles.</summary>
    Map,
    /// <summary>Named/ordinary spawn timers and the per-named bell configuration, unchanged
    /// (Alerts will own "alert me, at this volume, with this sound"; this owns the LIST).</summary>
    Camps,
    /// <summary>"How do I get there from here?" — the hop list from wherever the log last
    /// saw you, over <see cref="TravelPlan"/>.</summary>
    Routes,
    /// <summary>The old Travels &amp; Deaths card body: deaths, zones visited, markers —
    /// small, player-driven lists, which is why this is the one Full-inline tab.</summary>
    Travels,
}

/// <summary>A tab as a UI should draw it. <see cref="Value"/> is the tab's headline, kept
/// so the tab strip answers at a glance what a separate card header used to.</summary>
public sealed record WorldTabHeader(WorldTab Tab, string Label, string Key, string? Value);

/// <summary>
/// Builds the tab strip shared by the desktop World window and EQBuddy Mobile. Pure:
/// takes the already-computed headlines, returns headers.
/// </summary>
public static class WorldSurface
{
    /// <summary>The canonical label for each tab — docs/Themes.md theme 6's own wording,
    /// spelled here rather than re-typed in every UI.</summary>
    public static string LabelFor(WorldTab tab) => tab switch
    {
        WorldTab.Map => "Map",
        WorldTab.Camps => "Camps",
        // "Path" (Bevel-signed pre-design, question 4) — "Routes" sat one word from
        // "Travels" while meaning something different (a route you plan vs the zones you
        // visited). The enum member and wire key stay Routes/"travel"; only the label moved.
        WorldTab.Routes => "Path",
        WorldTab.Travels => "Travels",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label. Every key here is a name one of the four absorbed
    /// windows already answered to, not a new invention:
    /// <c>map</c> (MapWindow), <c>spawns</c> (SpawnsWindow), <c>travel</c> (TravelWindow),
    /// <c>misc</c> (the Travels &amp; Deaths card's own settings key).</summary>
    public static string KeyFor(WorldTab tab) => tab switch
    {
        WorldTab.Map => "map",
        WorldTab.Camps => "spawns",
        WorldTab.Routes => "travel",
        WorldTab.Travels => "misc",
        _ => tab.ToString().ToLowerInvariant(),
    };

    /// <summary>Every word these four surfaces have been called, so an old habit and an old
    /// doc line both still land. <c>camps</c>/<c>timers</c> are the Themes.md label read
    /// back; <c>routes</c> is the tab label for what <c>TravelWindow</c> answered;
    /// <c>travels</c>/<c>deaths</c> are the absorbed card's own names for itself.</summary>
    public static WorldTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "map" => WorldTab.Map,
        "spawns" or "camps" or "timers" => WorldTab.Camps,
        "travel" or "routes" => WorldTab.Routes,
        "misc" or "travels" or "deaths" => WorldTab.Travels,
        _ => null,
    };

    /// <summary>Initial table (Bevel may move any row, per the plan — PR 2-4 wait for its
    /// pre-design answers). Travels is the current card body: small, player-driven lists
    /// (deaths, zones, markers), which fits a widget the way the other themes' Full tabs
    /// do. Map, Camps and Routes are Glance — a map canvas, a timer list with its own bell
    /// pickers, and a destination picker all carry their own chrome (Bevel's host rule:
    /// "do not shrink-wrap the full window onto a SizeToContent always-on-top panel"), and
    /// conservative-glance is the ratified posture: a Glance understates and never lies,
    /// and promoting one later costs no migration.</summary>
    public static InlineMode InlineModeFor(WorldTab tab) => tab switch
    {
        WorldTab.Travels => InlineMode.Full,
        _ => InlineMode.Glance,
    };

    /// <summary>The tab an expanded World card opens on: the room that already lived on
    /// the widget as its own card.</summary>
    public const WorldTab DefaultInlineTab = WorldTab.Travels;

    /// <summary>The card keys this theme absorbs — exactly one, unlike every other theme so
    /// far. Read by the fold so the list of what disappears lives in ONE place rather than
    /// being spelled again in each UI's settings migration.</summary>
    public static readonly IReadOnlyList<string> AbsorbedCardKeys = ["misc"];

    /// <summary>
    /// The key the folded theme takes — <c>misc</c>, deliberately, and this is the thing
    /// the ask flagged as needing a doc comment so nobody "fixes" it later.
    ///
    /// Every theme so far kept an absorbed card's key precisely so nobody's card slot
    /// moved (see <see cref="CreatureSurface.ThemeCardKey"/> = <c>kills</c>). This theme
    /// absorbs exactly ONE card — the old Travels &amp; Deaths card, whose settings key has
    /// always been <c>misc</c> (a name from before that card had a proper one) — so keeping
    /// <c>misc</c> means there is **no settings migration at all**: <c>SectionOrder</c>,
    /// <c>HiddenSections</c> and <c>MiniStats</c> all keep pointing at the same string, and
    /// the step Themes.md calls "where silent data loss lives" simply does not run.
    /// Renaming the key to <c>world</c> would buy an aesthetic and cost a migration for
    /// zero player benefit — the card's TITLE becomes "World" (PR 3); the KEY stays
    /// <c>misc</c> forever, the same way <c>kills</c> stayed <c>kills</c>.
    /// </summary>
    public const string ThemeCardKey = "misc";

    public static IReadOnlyList<WorldTabHeader> Tabs(
        string? map = null, string? camps = null, string? routes = null, string? travels = null)
    {
        return
        [
            Header(WorldTab.Map, map),
            Header(WorldTab.Camps, camps),
            Header(WorldTab.Routes, routes),
            Header(WorldTab.Travels, travels),
        ];

        static WorldTabHeader Header(WorldTab tab, string? value) =>
            new(tab, LabelFor(tab), KeyFor(tab), string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>
    /// The launcher card's one-line summary — the line that has to justify replacing the
    /// Travels &amp; Deaths card's own header.
    ///
    /// **Counts, never countdowns — in the launcher AND the tab badges.** A countdown
    /// changes measured size every second (trap 12, the #173 keyboard-killer over a
    /// fullscreen game) and would wake every phone every second (trap 8). Deadlines belong
    /// to the spawn-due chips, which this theme does not touch. So the line says how many
    /// timers are running, never how soon one is due.
    ///
    /// A part with nothing to say is omitted rather than printed as a zero — the line a
    /// brand new character sees, exactly who is looking at a fresh widget.
    /// </summary>
    public static string LauncherSummary(
        string? zone = null, int zonesVisited = 0, int deaths = 0, int runningTimers = 0)
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(zone)) parts.Add(zone!);
        if (zonesVisited > 0) parts.Add($"{zonesVisited} zone{(zonesVisited == 1 ? "" : "s")}");
        if (deaths > 0) parts.Add($"{deaths} death{(deaths == 1 ? "" : "s")}");
        if (runningTimers > 0) parts.Add($"{runningTimers} timer{(runningTimers == 1 ? "" : "s")}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "no travels yet";
    }
}
