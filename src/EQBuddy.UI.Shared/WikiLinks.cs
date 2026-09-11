using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Where a click on a NAME should take the player on eqlwiki.
///
/// It exists because a surface told people to click something that could not be clicked.
/// The Drops tab's ✦ tooltip has always said <i>"Click the creature's name to open its
/// wiki page"</i> — step 2 of the how-to-sync instructions — and the creature heading was a
/// plain label with no handler on it, in both UIs, on both the Drops surface and the wiki
/// contribution pack. LeBigNasty reported it as exactly what it is (#226): <i>"Step 2 says
/// click on creatures name to open wiki. It doesn't seem to be doing that for me."</i>
///
/// That is the "silent no-op" rule with the switch on the other side, and the same shape as
/// the Gear tab naming an import it gave you no way to run (David, 2026-08-20): the app
/// names an action and the affordance is not there.
///
/// **The creature case is not the item case, which is why this is a class and not a
/// method.** An item is opened by SEARCH, because the log's name and the wiki's title
/// rarely match. A creature we have already looked up carries the URL the wiki actually
/// SERVED — after redirects — and that is the page whose loot list the pack is quoting.
/// Sending the player to a search when we are holding the exact URL would hand them a
/// different page from the one the numbers came from, which is trap 3 in reverse.
/// </summary>
public static class WikiLinks
{
    /// <summary>Search eqlwiki for a name. The right call for ITEMS, and the fallback for a
    /// creature whose lookup has not landed (or failed): a search finds a near-miss, where a
    /// guessed URL just 404s.</summary>
    public static string Search(string name) =>
        "https://eqlwiki.com/index.php?search="
        + Uri.EscapeDataString(EqlWikiItemService.NormalizeTitle(name));

    /// <summary>The creature's own page when the lookup landed, the search otherwise.
    ///
    /// <paramref name="lookup"/> is whatever the widget's target-drops memo holds for this
    /// creature right now — null while it is still in flight, which is a normal state and
    /// not an error. <c>MobInfo.WikiUrl</c> is built from the SERVED title, so a redirect
    /// has already been followed by the time it exists (trap 3).</summary>
    public static string Creature(MobLookupResult? lookup, string name) =>
        lookup?.Mob?.WikiUrl is { Length: > 0 } url ? url : Search(name);

    /// <summary>
    /// A FACTION's page — the door under every "Get maximum faction with X" row on the
    /// Unlocks tab (DRA-65). Player-clicked, like the creature one: EQBuddy asks eqlwiki for
    /// nothing here, so the request policy toward the wiki is untouched.
    ///
    /// <para><b>The SEARCH endpoint rather than a built page title, and that is not the
    /// item rule arriving by accident.</b> MediaWiki's search jumps straight to an exact
    /// title match and shows results otherwise, so a name the wiki spells our way lands on
    /// the page and a name it does not lands on candidates instead of a 404. That matters
    /// here specifically: the achievements text and the faction dump disagree about four of
    /// these names (<see cref="FactionNames"/>) and nobody has checked which spelling the
    /// wiki chose. A guessed URL would be confidently wrong on the four we know about and
    /// silently wrong on any we do not.</para>
    ///
    /// <para>It does NOT go through <see cref="Search"/>, whose normalisation is
    /// item-shaped — it strips a trailing " +N" tier suffix, which is meaningless on a
    /// faction and would quietly edit a name the player is reading on the row above.</para>
    /// </summary>
    public static string Faction(string factionName) =>
        "https://eqlwiki.com/index.php?search="
        + Uri.EscapeDataString((factionName ?? "").Trim());
}
