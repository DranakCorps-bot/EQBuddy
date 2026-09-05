using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The rooms of the Evolved shell — ONE definition, read by the desktop rail, by the
/// navigation address grammar, and (through <c>CompanionSurfaces.PageFor</c>) by the
/// phone's screen registry.
///
/// **Why an enum and not two lists.** Bevel's shell nav pre-design (Helm-signed
/// 2026-09-04 ~9:25 PM CT) asked for exactly this and named the failure it prevents:
/// *"If the rail's seven-room list and the phone's ⚙ Screens picker are two
/// hand-maintained lists rather than one Core enumeration, they will drift the way
/// AbsorbedTitles and AbsorbedCardKeys drifted (trap 55) — same shape, new surface."*
/// A hand-maintained list is code that cannot be type-checked (trap 30); an enum is one
/// the compiler walks for you.
///
/// Order is the RAIL order, top to bottom, from the same pre-design: the six rooms that
/// answer "show me something about my character", then <see cref="ShellPage.Settings"/>
/// below a visual gap because it answers a different question. That split does not
/// change at any window width (see <see cref="ShellLayout"/>): collapsing the rail to
/// icons must not also reorder or drop a room, which would turn a resize into a silent
/// capability loss — the #219/#233 shape triggered by a window edge instead of a release.
/// </summary>
public enum ShellPage
{
    Home,
    Live,
    Progress,
    Gear,
    Quests,
    World,
    Settings,
}

/// <summary>
/// The registry around <see cref="ShellPage"/>: rail order, labels, icons, which rooms
/// have actually landed, and the <c>page:room</c> address grammar every navigation path
/// resolves through.
/// </summary>
public static class ShellPages
{
    /// <summary>Rail order, top to bottom. <see cref="ShellPage.Settings"/> is last and
    /// is drawn below a gap — see <see cref="BelowTheGap"/>.</summary>
    public static readonly IReadOnlyList<ShellPage> RailOrder =
    [
        ShellPage.Home, ShellPage.Live, ShellPage.Progress,
        ShellPage.Gear, ShellPage.Quests, ShellPage.World, ShellPage.Settings,
    ];

    /// <summary>
    /// The rooms that have a room to show TODAY, and therefore the only rows the rail
    /// draws.
    ///
    /// **This is not a feature flag, it is the refusal to draw a dead affordance.** The
    /// signed pre-design is explicit: the honest options for a half-built shell are a
    /// rail with one row or seven rows where six are disabled, and this codebase already
    /// ruled on the second shape — the Experience next-level lock says, verbatim, *"an
    /// empty class row gets no chevron — an affordance that opens nothing is a trap."*
    /// A disabled rail row is that chevron wearing a different control.
    ///
    /// **A room joins this list in the same PR that lands it**, which is what stops the
    /// list and the rooms drifting apart in either direction: a room with no row is
    /// unreachable, and a row with no room is a trap.
    ///
    /// **PR 2 added Gear and World, and it added exactly those two for a reason worth
    /// writing down.** Both are surfaces whose Evolved IA verdict — Bevel §2, *"Keep →
    /// unify"* — was already SATISFIED by a v1 fold: <c>GearLootWindow</c> is bags plus
    /// wishlist plus what you picked up, and <c>WorldWindow</c> is the map, the camps, the
    /// route and the travels. So hosting them is a move rather than a redesign, which is
    /// the only shape that keeps a half-built shell coherent at every commit. The rooms
    /// that are NOT here are held back by a decision and not by effort.
    ///
    /// **PR 3 added Quests, and it took its own diff to do it.** Bevel's signed pre-design
    /// sorts rooms by which IA verdict is already PAID FOR: World and Gear were a MOVE
    /// because a v1 fold had already made each of them one window of exactly the tabs a
    /// room needs. Quests had the tabs and no view — 2,481 lines of window-owned rendering
    /// with nothing an <c>IShellRoom</c> could be handed — so it was a LIFT, which is the
    /// shape that has cost this repo real bugs, and it landed alone for that reason rather
    /// than for its size. <c>QuestsView</c> is that lift; <c>QuestsWindow</c> is now a thin
    /// host beside it.
    ///
    /// **PR 4 added Home, and it is the first room that is NEITHER a move nor a lift.**
    /// There was no v1 window to host and no view to extract: the four blocks its signed
    /// door 1 locks — Identity, Readiness, Recent session, Deep links — are a new surface
    /// composed from facts the app already had and had never put on one screen. That is also
    /// why it is the room the shell now OPENS on: <c>ShellWindow._page</c> had been
    /// <see cref="ShellPage.Progress"/> since PR 1 as an explicit placeholder for the room
    /// designed to answer "where do I stand", and the placeholder outlived its excuse the
    /// moment that room existed.
    ///
    /// **PR 5 added Live, and it is the first room that is a MERGE.** World and Gear were a
    /// MOVE, Quests a LIFT, Home a BUILD; Live is five separate v1 places that all answer
    /// "what is happening in this sitting" — two inline widget sections, three breakout
    /// kinds, a pop-out and one tab of another window — brought under one room while every
    /// one of them goes on shipping unchanged. It is also what finally let Raids leave
    /// Progress, which had been waiting on a room to move INTO since PR 1
    /// (<see cref="ProgressSurface.MovedToLive"/>).
    ///
    /// **The one still missing is held back by a decision**: Settings is a room whose whole
    /// job is not being a launcher.
    /// </summary>
    public static readonly IReadOnlyList<ShellPage> Landed =
    [
        ShellPage.Home, ShellPage.Live, ShellPage.Progress,
        ShellPage.Gear, ShellPage.Quests, ShellPage.World,
    ];

    /// <summary>
    /// The rooms INSIDE a room — the second half of a <c>page:room</c> address — read from
    /// the SAME Core surface definition the room's own tab strip is built from.
    ///
    /// **The shell must never re-spell a room.** <c>ProgressSurface.KeyFor(Experience)</c>
    /// is <c>"progress"</c>, deliberately, because it is the card key the five folded
    /// surfaces collapsed into; so the Experience room's address is
    /// <c>progress:progress</c>, which reads oddly and is correct. <c>WorldTab.Travels</c>
    /// is <c>"misc"</c> for the same kind of reason (the old card's settings key, kept so
    /// the fold needed no migration at all), and <c>WorldTab.Routes</c> is <c>"travel"</c>
    /// while its label says "Path". Every one of those looks like a typo and every one of
    /// them is load-bearing history. A shell that invented <c>world:path</c> beside the
    /// surface's own <c>world:travel</c> would be a second name for one destination, which
    /// is trap 33 lifted from data into navigation — the exact thing the one-address rule
    /// exists to prevent.
    ///
    /// So this table maps and does not translate, and <c>ShellNavigationTests</c> asserts
    /// that every key it hands out round-trips through the surface's own
    /// <c>TabForKey</c> — a mapping that quietly stopped matching would otherwise read as
    /// a working palette that lands nowhere.
    /// </summary>
    public static IReadOnlyList<(string Label, string Key)> Rooms(ShellPage page) => page switch
    {
        ShellPage.Live => [.. LiveSurface.Tabs().Select(h => (h.Label, h.Key))],
        // **FILTERED, and it is the one row here that is not a straight read.** The Evolved
        // IA moves Raids to Live, so the Progress ROOM has three tabs while
        // `ProgressSurface.Tabs()` still returns four — the v1 `ProgressWindow` and the
        // widget's inline card both still draw it, and taking it off them would be a v1
        // subtraction, which is gated separately and is a later PR. The predicate lives on
        // the surface rather than here so the phone's Progress screen reads the same answer
        // (Bevel's §3: the two hosts of "what's in Progress" move in one commit or they
        // disagree), and it is a predicate rather than a second list so a fifth Progress tab
        // appears in the room automatically — trap 55's lesson about two hand-maintained
        // lists describing one arrangement.
        ShellPage.Progress =>
            [.. ProgressSurface.Tabs()
                .Where(h => !ProgressSurface.MovedToLive(h.Tab))
                .Select(h => (h.Label, h.Key))],
        ShellPage.Gear => [.. LootSurface.Tabs().Select(h => (h.Label, h.Key))],
        // Counts omitted deliberately: this table names the rooms, and a badge is a
        // reading of the player's progress that belongs to the strip the room draws for
        // itself. `QuestSurface.Tabs()` returns all four either way — an empty checklist
        // still gets its tab, because a Sky room that vanished when nothing was ticked
        // would be unreachable to exactly the player who most needs to find it.
        ShellPage.Quests => [.. QuestSurface.Tabs().Select(h => (h.Label, h.Key))],
        ShellPage.World => [.. WorldSurface.Tabs().Select(h => (h.Label, h.Key))],
        _ => [],
    };

    /// <summary>True for the pages drawn under the rail's visual gap. Settings sits
    /// there because it configures the tool rather than describing the character — the
    /// same separation Windows' own Settings app and VS Code both draw.</summary>
    public static bool BelowTheGap(ShellPage page) => page == ShellPage.Settings;

    /// <summary>The wire spelling of a page: the first half of a <c>page:room</c>
    /// address. Lower-case and stable — these appear in <c>EQBUDDY_SHELL</c>, in the
    /// <c>EQBUDDY_EXPAND</c> dump, and in every navigation call.</summary>
    public static string Key(ShellPage page) => page switch
    {
        ShellPage.Home => "home",
        ShellPage.Live => "live",
        ShellPage.Progress => "progress",
        ShellPage.Gear => "gear",
        ShellPage.Quests => "quests",
        ShellPage.World => "world",
        ShellPage.Settings => "settings",
        _ => "",
    };

    /// <summary>The rail's label. Short nouns on purpose: the rail grows DOWN, but a
    /// long label is what forces the collapsed state early.</summary>
    public static string Label(ShellPage page) => page switch
    {
        ShellPage.Home => "Home",
        ShellPage.Live => "Live",
        ShellPage.Progress => "Progress",
        ShellPage.Gear => "Gear",
        ShellPage.Quests => "Quests",
        ShellPage.World => "World",
        ShellPage.Settings => "Settings",
        _ => "",
    };

    /// <summary>The rail's icon, by name in <see cref="IconPaths"/> — never a glyph
    /// (#148, #166), and never a name the table does not hold, which
    /// <c>ShellNavigationTests</c> asserts rather than trusts.</summary>
    public static string IconName(ShellPage page) => page switch
    {
        ShellPage.Home => "Tray",
        ShellPage.Live => "Bolt",
        ShellPage.Progress => "Chart",
        ShellPage.Gear => "Bag",
        ShellPage.Quests => "Quest",
        ShellPage.World => "Map",
        ShellPage.Settings => "Settings",
        _ => "Info",
    };

    /// <summary>One line naming what the room is for — the rail's tooltip, and the only
    /// thing carrying the room's name when the rail is collapsed to icons.</summary>
    public static string Describe(ShellPage page) => page switch
    {
        ShellPage.Home => "Who you are playing, what is ready, and where you left off.",
        ShellPage.Live => "This sitting: damage, healing, pet, kills and what you cleared.",
        ShellPage.Progress => "Experience, wealth, faction and raid targets.",
        ShellPage.Gear => "Your bags, your wishlist, and what dropped for you.",
        ShellPage.Quests => "Your quest tracker, Epic 1.0 and Plane of Sky.",
        ShellPage.World => "The zone's map, your camps, spawn timers and how to get there.",
        ShellPage.Settings => "Configure EQBuddy.",
        _ => "",
    };

    /// <summary>Resolve a page key. Unknown keys answer null rather than falling back to
    /// a default — silently landing somewhere the caller did not ask for is the shape
    /// <c>ProgressWindow.SetTab</c> already refuses.</summary>
    public static ShellPage? ForKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        foreach (var page in RailOrder)
            if (string.Equals(Key(page), key.Trim(), StringComparison.OrdinalIgnoreCase))
                return page;
        return null;
    }

    /// <summary>
    /// THE ONE NAVIGATION ADDRESS, parsed. <c>progress</c> or <c>progress:raids</c> —
    /// the grammar <c>EQBUDDY_EXPAND</c> has taken since 2026-08-26, reused rather than
    /// reinvented so the rail, the Ctrl+K palette, a future HUD "Open EQBuddy" button and
    /// "Guide me there" all resolve to one destination spelling.
    ///
    /// **Two ways to land on a room is trap 33 one level up from data into navigation**:
    /// two callers with different arguments do not produce a stale answer and a fresh
    /// one, they produce two answers that a later change has to be taught twice.
    ///
    /// Returns null for an unrecognised page. The room half is handed back verbatim for
    /// the page to resolve — <see cref="ShellPages"/> knows the rooms exist, not what
    /// their tabs are called.
    /// </summary>
    public static (ShellPage Page, string? Room)? ParseAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var parts = address.Trim().Split(':', 2);
        if (ForKey(parts[0]) is not { } page) return null;
        var room = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
        return (page, room);
    }

    /// <summary>The inverse: the address string for a page and optional room.</summary>
    public static string Address(ShellPage page, string? room = null) =>
        string.IsNullOrEmpty(room) ? Key(page) : $"{Key(page)}:{room}";
}
