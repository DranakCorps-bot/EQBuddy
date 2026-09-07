using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the Loot surface SHOWS — the decisions, once, for the card and the breakout and
/// both desktops (docs/DesignSystem.md §11.3, Gate 4).
///
/// <see cref="LootRows"/> already owned row ORDER, and it was shared. Everything around
/// the rows was not: which strips are up, which chip is lit, whether "recent" is offered
/// at all, and what an empty slice says were written twice — once in the widget's
/// RenderLoot and once in the breakout's UpdateLoot — from the same four inputs, in code
/// that had already drifted (the breakout's chips carried no tooltips, and its "made"
/// alias was spelled a third way in its own comment). That is trap 4 in CLAUDE.md: one
/// entry, two sources for one fact.
///
/// It is framework-free like the rest of UI.Shared, so it is unit-tested with no window,
/// and so the Avalonia card — which never received #198's filter at all — composes the
/// SAME rules rather than a hand-copied approximation. The alternative is what already
/// happened to the chip stacks: #122 and #152 reached Linux after Windows had paid.
/// </summary>
public static class LootPresentation
{
    /// <param name="Key">Written to settings; matched by the strip.</param>
    /// <param name="Label">The chip's word. Lower case: a strip is navigation.</param>
    /// <param name="Tip">Hover copy, or null. These exist because "other" is not
    /// self-explanatory — it is everything you acquired that a corpse did not hand you.</param>
    public readonly record struct Option(string Key, string Label, string? Tip);

    /// <summary>The provenance slice. "all" is first because it is the default and the
    /// one a player returns to.</summary>
    public static readonly IReadOnlyList<Option> Views =
    [
        new("all", "all", "Everything you acquired this session."),
        new("looted", "looted", "Items you looted from corpses."),
        new("other", "other", "Everything else: foraged, crafted, merged, or parcel."),
    ];

    /// <summary>Row order. "recent" is offered only when something carries a timestamp —
    /// see <see cref="Plan.ShowRecent"/>.</summary>
    public static readonly IReadOnlyList<Option> Sorts =
    [
        new("count", "count", "Biggest stacks first — the farming view."),
        new("name", "name", "Alphabetical."),
        new("recent", "recent",
            "Every drop as it happened, newest first — how you catch the one unusual " +
            "thing in a long farm. Runs of the same item collapse into a single row."),
    ];

    public const string ViewAll = "all";
    public const string ViewLooted = "looted";
    public const string ViewOther = "other";

    public const string SortCount = "count";
    public const string SortName = "name";
    public const string SortRecent = "recent";

    /// <summary>Settings' <c>LootView</c> as one of the three the strip knows.
    ///
    /// "made" was the pre-#198 spelling of the third slice and is still in profiles on
    /// disk; both surfaces mapped it inline, which is two chances to forget. Anything
    /// unrecognised falls back to "all" rather than showing an empty card, because a
    /// hand-edited settings file must not be able to hide a player's loot.</summary>
    public static string NormalizeView(string? view) => view switch
    {
        ViewLooted => ViewLooted,
        ViewOther or "made" => ViewOther,
        _ => ViewAll,
    };

    /// <summary>Settings' <c>LootSort</c> as one of the three the strip knows.</summary>
    public static string NormalizeSort(string? sort) => sort switch
    {
        SortName => SortName,
        SortRecent => SortRecent,
        _ => SortCount,
    };

    /// <summary>Corpse drops are "looted"; everything else you acquired is "other".
    /// Crafts and merges arrive as their own snapshot lists rather than in
    /// <see cref="LootDetail"/>, so this only has to name the two that ride the loot
    /// list.</summary>
    public static bool IsOther(string source) =>
        source is LootRows.ForageSource or LootRows.ParcelSource;

    /// <summary>Everything a Loot surface needs to paint itself, from one call.</summary>
    /// <param name="Rows">In order, from <see cref="LootRows"/>.</param>
    /// <param name="View">The normalized slice — which chip is lit.</param>
    /// <param name="Sort">The normalized order — which chip is lit.</param>
    /// <param name="ShowViewStrip">The show strip stays up whenever the card holds ANY
    /// loot, even when the slice the player picked is empty: a filter nobody can see is
    /// a filter nobody knows they applied (LW, 2026-08-17).</param>
    /// <param name="ShowSortStrip">One row cannot be sorted, so the strip is noise.</param>
    /// <param name="ShowRecent">Whether "recent" is worth offering — the slice on screen
    /// has to contain something timestamped.</param>
    /// <param name="EmptyNote">Null when there are rows. Otherwise the sentence to draw
    /// instead: it NAMES the empty slice rather than blanking, so an empty "looted" view
    /// never reads as a broken card.</param>
    public readonly record struct Plan(
        IReadOnlyList<LootRow> Rows,
        string View,
        string Sort,
        bool ShowViewStrip,
        bool ShowSortStrip,
        bool ShowRecent,
        string? EmptyNote);

    /// <summary>The whole Loot surface, decided. Both widgets and both breakouts call
    /// this and then only paint.</summary>
    public static Plan Build(
        IReadOnlyList<LootDetail> loot,
        IReadOnlyList<NameCount> merged,
        IReadOnlyList<NameCount> fashioned,
        IReadOnlyList<LootPickup> recentLoot,
        string? viewSetting,
        string? sortSetting)
    {
        var view = NormalizeView(viewSetting);
        var sort = NormalizeSort(sortSetting);

        var hasLooted = loot.Any(l => !IsOther(l.LastSource));
        var hasOther = loot.Any(l => IsOther(l.LastSource))
                       || merged.Count > 0 || fashioned.Count > 0;
        var hasAny = hasLooted || hasOther;

        var rows = LootRows.Build(loot, merged, fashioned, recentLoot, view, sort);

        // Every acquisition carries a timestamp now (crafts and merges included, via
        // RecentLoot), so "recent" is meaningful for any slice that isn't empty.
        var hasTimeline = view switch
        {
            ViewLooted => hasLooted,
            ViewOther => hasOther,
            _ => hasAny,
        };

        return new Plan(
            rows,
            view,
            sort,
            ShowViewStrip: hasAny,
            ShowSortStrip: rows.Count > 1,
            ShowRecent: hasTimeline,
            EmptyNote: rows.Count > 0 ? null : EmptyNoteFor(view, hasAny));
    }

    /// <summary>What an empty list says. "No loot seen yet" is a session that has not
    /// started; the other two are a slice that is empty inside a session that is not —
    /// a different fact, and the one a player needs to see the filter is on.</summary>
    public static string EmptyNoteFor(string view, bool hasAnyLoot) => !hasAnyLoot
        ? "No loot seen yet."
        : NormalizeView(view) == ViewLooted ? "No looted items yet." : "Nothing else yet.";

    /// <summary>The card header's count. Merges and crafts are counted apart from drops
    /// because they are not drops — "+N made" is the whole of #131's ask.</summary>
    public static string Header(int lootTotal, int madeTotal) => madeTotal > 0
        ? $"{lootTotal} items (+{madeTotal} made)"
        : $"{lootTotal} item{(lootTotal == 1 ? "" : "s")}";

    /// <summary>The breakout's subheader in Session scope. Same two numbers as
    /// <see cref="Header"/> so the minimized window and the card cannot disagree (#131).</summary>
    public static string BreakoutSubtitle(int lootTotal, int madeTotal) =>
        $"Session · {lootTotal} item{(lootTotal == 1 ? "" : "s")} looted"
        + (madeTotal > 0 ? $" · +{madeTotal} made" : "");

    // ---- the target-drops block ----
    //
    // Its heading used to be composed as one string with a leading emoji, in the widget,
    // and the breakout then string-REPLACED the emoji back out to get a shorter version
    // of the same line. Two surfaces, one sentence, and the seam was a Replace on a
    // literal that either of them could have changed. The
    // content builder now hands back the two parts and these compose them.

    /// <summary>The Loot card's heading over the target-drops list. The marker beside it
    /// is an ICON, drawn by the card — a glyph in the sentence is a control smuggled into
    /// text, and it cannot take the palette or a size we choose.</summary>
    public static string TargetHeading(string names, string detail) => $"Fighting: {names}{detail}";

    /// <summary>The Loot breakout's subheader in Target scope. Shorter because the window
    /// title already says what it is looking at.</summary>
    public static string TargetSubtitle(string names, string detail) => names + detail;

    /// <summary>Target scope's empty note when nothing is targeted at all — one sentence,
    /// shared so the float and the mini-bar peek (OE-9) can never describe "no target" two
    /// different ways (trap 4). The line break is deliberate: <c>BreakoutWindow</c>'s
    /// <c>EmptyText</c> does not wrap, and the peek's own empty line does, so the same
    /// string reads correctly on both without either host reformatting it.</summary>
    public const string NoTargetNote =
        "Swing at something — or /consider it — and its\npossible drops appear here.";

    /// <summary>Provenance as it rides after the name — muted, parenthesised, and NOT
    /// part of the name, so a click still looks the base item up (LW, 2026-08-17).</summary>
    public static string? Note(string? tag) => tag is { Length: > 0 } ? $"({tag})" : null;
}
