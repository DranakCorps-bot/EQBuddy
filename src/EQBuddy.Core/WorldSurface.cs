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
    /// <summary>What dropped, by creature — <i>"is this camp worth it?"</i>, which is a
    /// question about the WORLD rather than about your bags. The Evolved shell's World room
    /// only; the v1 lane still ships it as <c>CreatureWindow</c>'s Drops tab. See
    /// <see cref="WorldSurface.ShellOnly"/>, which is what keeps those two facts from
    /// becoming a fifth tab on a window that cannot draw it.</summary>
    Drops,
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
        WorldTab.Drops => "Drops",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label. Every key here is a name one of the four absorbed
    /// windows already answered to, not a new invention:
    /// <c>map</c> (MapWindow), <c>spawns</c> (SpawnsWindow), <c>travel</c> (TravelWindow),
    /// <c>misc</c> (the Travels &amp; Deaths card's own settings key — the card is gone as
    /// of 2026-09-05, HUD subtraction cut 2, and the key stays: this is the address a
    /// shell room and a saved tab choice resolve through, not a claim that a card exists),
    /// <c>drops</c>
    /// (<c>DropsWindow</c>, and <see cref="CreatureSurface"/>'s key for the same tab today —
    /// one surface answering to one name in both lanes).</summary>
    public static string KeyFor(WorldTab tab) => tab switch
    {
        WorldTab.Map => "map",
        WorldTab.Camps => "spawns",
        WorldTab.Routes => "travel",
        WorldTab.Travels => "misc",
        WorldTab.Drops => "drops",
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
        // The name DropsWindow answered to, and the one CreatureSurface still answers to.
        // It resolves here whether or not the host asking can DRAW the tab — the same rule
        // ProgressSurface.TabForKey keeps for "raids" after the move to Live: this method is
        // about an old address landing somewhere true, not about who may show it.
        "drops" => WorldTab.Drops,
        _ => null,
    };

    // `InlineModeFor` LEFT THIS FILE ON 2026-09-05, with the widget's World card (HUD
    // subtraction cut 2). It answered a question only a CARD asks — which rooms draw a
    // body inline (Travels, alone) and which answer with a glance line — and
    // `WorldThemeCard` was its one caller in the repo. Keeping it would have left a
    // contract asserted for a surface nobody draws, which is trap 34's shape: a guard
    // that cannot fail reads as coverage. The tabs, the labels, the wire keys and
    // ShellOnly below are untouched — the World window, the shell's World room and
    // EQBuddy Mobile all still read them.

    /// <summary>
    /// **The tabs the Evolved shell's World room has and the v1 lane does not** — today,
    /// exactly <see cref="WorldTab.Drops"/>.
    ///
    /// It is the mirror of <see cref="ProgressSurface.MovedToLive"/> and it exists for the
    /// same reason, from the opposite direction: that one hides a tab the ROOM has moved
    /// away, this one hides a tab the room has GAINED. Drops arrives in World because the
    /// Evolved IA sends camp research here — but the v1 lane still ships it as
    /// <c>CreatureWindow</c>'s Drops tab, and <c>WorldWindow</c>/the inline card cannot
    /// draw it at all: both map <see cref="WorldTab"/> to a body with a
    /// <c>_ =&gt; _travels.Body</c> default, so an unfiltered fifth header would put a
    /// "Drops" chip on a shipped window that answers it with the Travels list. That is a
    /// player-visible defect reachable with no code change to either host, which is exactly
    /// why the predicate is here rather than in one of them.
    ///
    /// **A predicate rather than a second list** (trap 30/55): a sixth World tab is drawn by
    /// the room automatically, and adding one to the v1 lane means deleting a row here
    /// rather than remembering to edit two strips. <see cref="TabForKey"/> deliberately does
    /// NOT consult it — an address resolving is a different question from a host being able
    /// to draw it.
    /// </summary>
    public static bool ShellOnly(WorldTab tab) => tab == WorldTab.Drops;

    /// <summary>The room an opener lands on when it does not name one: Travels, the room
    /// that lived on the widget as its own card until 2026-09-05. Was
    /// <c>DefaultInlineTab</c> until that cut, and the rename is the point — there is no
    /// inline card for it to be the default OF, only three hosts that open somewhere.</summary>
    public const WorldTab DefaultTab = WorldTab.Travels;

    // `AbsorbedCardKeys` (["misc"]) AND `ThemeCardKey` ("misc") LEFT THIS FILE ON
    // 2026-09-05, with the World card (HUD subtraction cut 2).
    //
    // **They were the fold's statement about a card, and there is no card.** This theme
    // absorbed exactly one card and KEPT its key, so no `FoldThemeSections` call ever read
    // either constant — `AppSettings` calls it for Progress and Loot only, and the sole
    // reader of these two was `SectionFoldIdempotenceTests.Folds`. Leaving them would have
    // left a fold naming a key with no catalog row, which is the premise check that guard
    // exists to make (trap 55, and #252 is what it cost when Motes came back and nobody
    // edited the list). The row went out of `Folds()` in the same commit.
    //
    // `CreatureSurface`'s pair has the identical shape (one card, key kept) and stays,
    // because `kills` IS still a card. That is the whole distinction.
    //
    // What replaces them is a REMOVAL, not a fold: `AppSettings.MigrateWorldSections`
    // strips "misc" out of `SectionOrder` and `HiddenSections`, because every 1.x profile
    // in the world carries that key and a key with no card can never draw anything.
    // `KeyFor(WorldTab.Travels)` is still "misc" and always will be — that is the WIRE
    // key for the Travels room (`world:misc` in the shell's address grammar), which is a
    // different question from whether a card by that name exists.

    /// <summary>Every tab this theme defines, in order — including the ones only the Evolved
    /// shell can draw. Callers that are a v1 host filter with <see cref="ShellOnly"/>;
    /// <c>UI.Shared</c>'s <c>WorldTheme</c> is where both strips are actually built, so no
    /// UI has to remember. Kept unfiltered here on purpose: <c>ShellPages.Rooms</c> reads
    /// this for the <c>page:room</c> grammar, and an address the shell can land on must
    /// exist in the definition the shell reads.</summary>
    public static IReadOnlyList<WorldTabHeader> Tabs(
        string? map = null, string? camps = null, string? routes = null, string? travels = null,
        string? drops = null)
    {
        return
        [
            Header(WorldTab.Map, map),
            Header(WorldTab.Camps, camps),
            Header(WorldTab.Routes, routes),
            Header(WorldTab.Travels, travels),
            Header(WorldTab.Drops, drops),
        ];

        static WorldTabHeader Header(WorldTab tab, string? value) =>
            new(tab, LabelFor(tab), KeyFor(tab), string.IsNullOrWhiteSpace(value) ? null : value);
    }

    // `LauncherSummary` LEFT THIS FILE ON 2026-09-05, with the World card (HUD subtraction
    // cut 2). It built the collapsed card's one line — "Befallen · 2 zones · 1 death ·
    // 3 timers" — and `MainWindow.RefreshUi` was its only caller. Bevel's I-5 check named
    // this composite as the one thing the cut actually costs: the window's tab strip
    // carries the zone and the death count as badges, and Camps/Path deliberately carry
    // none, so nothing puts all four numbers on one line any more.
    //
    // **The rule it enforced outlives it and is not optional: COUNTS, NEVER COUNTDOWNS.**
    // A countdown changes measured size every second (trap 12, the #173 keyboard-killer
    // over a fullscreen game) and would wake every phone every second (trap 8). It is
    // still written down, in `UI.Shared/WorldTheme`'s own comment on why Camps and Path
    // have no badge, and `LivePresentation` cites the same rule for its rates.
}
