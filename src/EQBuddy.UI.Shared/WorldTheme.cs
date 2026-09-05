using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The WORLD THEME's tab badges — the words behind the strip the World window and the
/// Evolved shell's World room both build from.
///
/// <see cref="WorldSurface"/> in Core owns which tabs exist, their order, their labels and
/// their keys. This owns the WORDS — the same parity-by-shared-module reason
/// <see cref="ProgressTheme"/> exists, so a window, a room and (later) the phone cannot end
/// up saying three different things about one tab (#210).
///
/// **Counts, never countdowns** (Helm-signed amendment): a ticking value would resize an
/// always-on-top widget every second (trap 12) and wake every phone every second (trap 8) —
/// deadlines belong to the spawn-due chips, which this theme does not touch. It is why
/// Camps and Path carry no badge at all, and `WorldThemeTests` is where that is asserted.
///
/// **THE GLANCE FAMILY LEFT THIS FILE ON 2026-09-05** with the widget's World card (HUD
/// subtraction cut 2). `MapGlance`, `CampsGlance`, `PathGlance` and `GlanceFor` answered a
/// question only an INLINE CARD asks — what one sentence stands in for a tab whose full
/// view is too big for a panel over the game — and `WorldThemeCard` was their only caller.
/// The two hosts that remain draw every tab's real body, so there is no glance to write.
/// Same reasoning, same day, as `QuestSurface`'s three inline members in cut 1.
/// </summary>
public static class WorldTheme
{
    /// <summary>
    /// **The v1 strip: the World window.** (It was the window AND the widget's inline card
    /// until 2026-09-05, when the card was cut.) Badges included — Map gets the current
    /// zone, Travels gets the death count, which are the two facts the card's launcher line
    /// also carried and the two that survive it. Camps/Path carry no badge: a live timer
    /// count on the tab strip is a countdown by another name the moment a player watches it
    /// tick, and that is why the cut's lost composite is not rebuilt here.
    ///
    /// **Shell-only tabs are filtered out here, and this is the ONLY place that does it.**
    /// Neither v1 host can draw <see cref="WorldTab.Drops"/> — both fall through
    /// <c>_ =&gt; _travels.Body</c> — so an unfiltered header would put a chip on a shipped
    /// window that answers with the wrong list. Both hosts call this method rather than
    /// <see cref="WorldSurface.Tabs"/> so that fact lives once
    /// (<see cref="WorldSurface.ShellOnly"/> is the predicate; this is its one v1 reader).
    /// The window built its own copy of this line until S2 and the comment above already
    /// claimed otherwise — two sources for one strip, which is trap 4 in the small.
    /// </summary>
    public static IReadOnlyList<WorldTabHeader> Tabs(string? zone, int deaths) =>
        [.. AllTabs(zone, deaths, drops: null).Where(h => !WorldSurface.ShellOnly(h.Tab))];

    /// <summary>
    /// **The Evolved shell's strip: everything, Drops included.** The room's own tab list,
    /// built from the same words the v1 strip uses so the two cannot describe one tab
    /// differently — the parity-by-shared-module rule this file exists for (#210).
    ///
    /// <paramref name="drops"/> is the Drops view's OWN badge, threaded through rather than
    /// recomputed here: the card counts the mobs its filter leaves, so a count derived from
    /// the snapshot beside it would be a second producer of one number and would disagree
    /// with the body the moment a player typed in the filter box (trap 33). Same shape as
    /// the Gear room handing <c>LootTheme.Tabs</c> its Inventory view's badge.
    /// </summary>
    public static IReadOnlyList<WorldTabHeader> ShellTabs(string? zone, int deaths, string? drops) =>
        AllTabs(zone, deaths, drops);

    private static IReadOnlyList<WorldTabHeader> AllTabs(string? zone, int deaths, string? drops) =>
        WorldSurface.Tabs(
            map: zone,
            travels: deaths > 0 ? $"{deaths} death{(deaths == 1 ? "" : "s")}" : null,
            drops: drops);
}
