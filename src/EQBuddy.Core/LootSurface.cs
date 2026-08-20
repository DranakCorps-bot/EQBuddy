namespace EQBuddy.Core;

/// <summary>
/// The Loot &amp; Items theme's tabs, in the order every UI shows them (docs/Themes.md).
///
/// **The chain, in one window.** This is the theme that carries what CLAUDE.md calls the
/// differentiator: loot → quest → item → mob → camp → route. What did I just get, what
/// drops it, what is it actually for, and what am I still missing. Those were four
/// separate places to look, which is precisely why nobody followed the chain.
///
/// **Surface: phone and desktop, never the overlay.** Every question here is one you look
/// AWAY for — reading an item's stats, deciding whether a drop is an upgrade, planning
/// where to farm next. None of it is a deadline. The one loot fact that IS time-critical
/// (a watch rule matching a drop you are hunting) is a chip and stays one.
///
/// Ordering and labels live here for the reason <see cref="QuestSurface"/> and
/// <see cref="ProgressSurface"/> exist: one definition of what the tabs ARE, or the three
/// surfaces drift (#122, #152, #184).
/// </summary>
public enum LootTab
{
    /// <summary>What this session has picked up, made and sold.</summary>
    Loot,

    /// <summary>Drops by creature — your own observed rates, per mob.</summary>
    Drops,

    /// <summary>Look an item up: stats, what wants it, where it comes from.</summary>
    Items,

    /// <summary>The imported shopping list, and what is left to farm.</summary>
    Gear,
}

/// <summary>A tab as a UI should draw it. <see cref="Value"/> is the tab's headline —
/// "39 items", "1/12" — the number its card used to carry in its header, kept so the tab
/// strip answers at a glance what the separate card headers used to.</summary>
public sealed record LootTabHeader(LootTab Tab, string Label, string Key, string? Value);

/// <summary>
/// Builds the tab strip shared by the desktop window and EQBuddy Mobile. Pure: takes the
/// already-computed headlines, returns headers.
/// </summary>
public static class LootSurface
{
    /// <summary>Single words, parallel with the Progress tabs. The theme is called "Loot
    /// &amp; Items" in the plan because that is what it COVERS; the tabs are named for
    /// what a player is trying to do when they reach for one.</summary>
    public static string LabelFor(LootTab tab) => tab switch
    {
        LootTab.Loot => "Loot",
        LootTab.Drops => "Drops",
        LootTab.Items => "Items",
        LootTab.Gear => "Gear",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label.
    ///
    /// Loot and Gear keep <c>loot</c> and <c>gear</c>, the keys those cards have always
    /// used in <c>SectionOrder</c>, <c>HiddenSections</c> and <c>EQBUDDY_EXPAND</c>. Step 5
    /// of the recipe is "fold the old keys, PRESERVING position and hidden state": the
    /// theme inherits the card slot a player already placed rather than appearing at the
    /// bottom of their list, and a player who had Loot hidden still has it hidden.</summary>
    public static string KeyFor(LootTab tab) => tab switch
    {
        LootTab.Loot => "loot",
        LootTab.Drops => "drops",
        LootTab.Items => "items",
        LootTab.Gear => "gear",
        _ => tab.ToString().ToLowerInvariant(),
    };

    public static LootTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "loot" or "session" => LootTab.Loot,
        "drops" or "targetdrops" => LootTab.Drops,
        "items" or "item" or "lookup" => LootTab.Items,
        "gear" => LootTab.Gear,
        _ => null,
    };

    /// <summary>
    /// The tabs this theme actually HOSTS today.
    ///
    /// The enum above names all four because the vocabulary should be settled once — a
    /// key renamed later is a saved tab choice broken later. What ships in the first pass
    /// is Loot and Gear, the two CARDS that fold; Drops and Items are existing windows
    /// (`DropsWindow`, `ItemInfoWindow`) that become tabs in a second pass, so that each
    /// fold is reviewable on its own rather than four surfaces moving at once.
    ///
    /// A tab is listed here only when there is something behind it. An empty tab is worse
    /// than an absent one: it reads as a feature that is broken rather than one that has
    /// not arrived.
    /// </summary>
    public static readonly IReadOnlyList<LootTab> Hosted = [LootTab.Loot, LootTab.Gear];

    /// <summary>The card keys this theme absorbs, in the widget's own vocabulary. The fold
    /// reads this so the list of what disappears lives in ONE place rather than being
    /// spelled again in each UI's settings migration.</summary>
    public static readonly IReadOnlyList<string> AbsorbedCardKeys = ["loot", "gear"];

    /// <summary>The key the folded theme takes — the one card slot the two collapse into.
    /// Deliberately one OF the absorbed keys rather than a new one; see
    /// <see cref="KeyFor"/>. Loot rather than gear because it is the card that moves while
    /// you play, so it is the slot a player is likelier to have positioned deliberately.</summary>
    public const string ThemeCardKey = "loot";

    /// <summary>The hosted tabs, with whatever headline each was given.</summary>
    public static IReadOnlyList<LootTabHeader> Tabs(string? loot = null, string? gear = null)
    {
        var values = new Dictionary<LootTab, string?>
        {
            [LootTab.Loot] = loot,
            [LootTab.Gear] = gear,
        };
        return [.. Hosted.Select(tab => new LootTabHeader(
            tab, LabelFor(tab), KeyFor(tab),
            values.TryGetValue(tab, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null))];
    }

    /// <summary>
    /// The launcher card's one-line summary — step 3 of the recipe, and the line that has
    /// to justify replacing two card headers with one.
    ///
    /// **It carries what MOVES WHILE YOU PLAY**, which is the rule #219 taught: the
    /// Progress launcher dropped the mote RATE to stop the line truncating, and the player
    /// who used that number arrived within the hour to say so. Loot count and what you
    /// have crafted change every few minutes; a gear list changes when you finally get the
    /// drop. So loot leads, and gear badges its own tab — one click, no scrolling.
    ///
    /// A part with nothing to say is omitted rather than printed as a zero, which is what
    /// keeps this short on a fresh character — exactly who is looking at a fresh widget.
    /// </summary>
    public static string LauncherSummary(int items = 0, int crafted = 0, int gearTotal = 0, int gearAcquired = 0)
    {
        var parts = new List<string>(3);
        if (items > 0) parts.Add($"{items} item{(items == 1 ? "" : "s")}");
        if (crafted > 0) parts.Add($"+{crafted} made");
        // Gear only speaks up once there is a list — and then only as a fraction, because
        // "3/12" is the whole question that card answered.
        if (gearTotal > 0) parts.Add($"gear {gearAcquired}/{gearTotal}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "nothing looted yet";
    }
}
