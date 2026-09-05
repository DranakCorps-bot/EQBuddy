using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the WORLD THEME's Glance tabs say when the inline card draws them instead of their
/// full view (Bevel-signed pre-design, World PR 3), and the tab badges the card and the
/// window both build from.
///
/// <see cref="WorldSurface"/> in Core owns which tabs exist, their order, their labels and
/// their keys. This owns the WORDS — the same parity-by-shared-module reason
/// <see cref="ProgressTheme"/> exists, so the desktop card, the desktop window, the
/// Avalonia twin and (later) the phone cannot end up saying three different things about
/// one tab (#210).
///
/// **Counts, never countdowns, and never a canvas** (Helm-signed amendment): a Glance line
/// is one sentence, not a rendered map or a live timer. A ticking value would resize the
/// widget every second (trap 12) and wake every phone every second (trap 8) — deadlines
/// belong to the spawn-due chips, which this theme does not touch.
/// </summary>
public static class WorldTheme
{
    /// <summary>Map — the current zone, or that none is known yet. No canvas, no marker,
    /// no countdown: just whether there is a zone to show at all.</summary>
    public static string MapGlance(string? zone) =>
        !string.IsNullOrWhiteSpace(zone) ? $"Map — {zone}" : "Map — no zone yet";

    /// <summary>Camps — how many timers are running, never how soon one is due (that is
    /// the spawn-due chips' job).</summary>
    public static string CampsGlance(int runningTimers) =>
        runningTimers > 0
            ? $"Camps — {runningTimers} timer{(runningTimers == 1 ? "" : "s")}"
            : "Camps — no timers";

    /// <summary>Path — the route the player last picked, or that none has been. Today
    /// nothing persists a picked destination outside the Path tab's own combo box, so the
    /// inline card (which never builds that tab's body) honestly reports "no route" —
    /// this is written to accept one anyway, for the day a destination is remembered.</summary>
    public static string PathGlance(string? from, string? destination) =>
        !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(destination)
            ? $"Path — {from} to {destination}"
            : "Path — no route";

    /// <summary>The Glance line for whichever tab is selected — the card's one call, so it
    /// never has to know which of the three Glance tabs it is drawing.</summary>
    public static string GlanceFor(WorldTab tab, string? zone, int runningTimers,
        string? from, string? destination) => tab switch
    {
        WorldTab.Map => MapGlance(zone),
        WorldTab.Camps => CampsGlance(runningTimers),
        WorldTab.Routes => PathGlance(from, destination),
        // Travels is Full, so a Glance line is never drawn for it; Drops never reaches the
        // inline card at all (WorldSurface.ShellOnly keeps it off the v1 strip).
        _ => "",
    };

    /// <summary>
    /// **The v1 strip: the World window and the widget's inline card.** Badges included —
    /// Map gets the current zone, Travels gets the death count (the same two facts the
    /// card's launcher line already carries). Camps/Path carry no badge: a live timer count
    /// on the tab strip is a countdown by another name the moment a player watches it tick.
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
