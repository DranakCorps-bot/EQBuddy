namespace EQBuddy.Core;

/// <summary>
/// The KILLS &amp; DROPS theme's tabs, in the order every UI shows them (docs/Themes.md).
///
/// **David's grouping, 2026-08-20**, and it corrected mine. I had put Kills into Live
/// Meters with Combat and Healing; he answered: <i>"Kills isn't a meter though. we don't
/// track kills per second but we track damage per second, healing per second. Kills and
/// Drops should be … Kills and Drops ;)"</i> A meter is a per-second board. Kills/hour is
/// a rate, which is not the same thing.
///
/// **Both tabs are about the CREATURE.** What died, and what it dropped at what rate.
/// That is one question — <i>is this camp worth it?</i> — and it was answered in two
/// places, one of them (Drops by creature) buried in the cog menu where nobody found it.
///
/// **This also takes Drops OUT of Gear &amp; Loot**, where <c>LootTab</c> had it named and
/// waiting since that theme shipped. It never really belonged: Gear &amp; Loot is about
/// your bags — what you picked up, what you want, what you have — and Drops is about the
/// mob. The grouping made the mistake obvious.
/// </summary>
public enum CreatureTab
{
    /// <summary>What died this session, and what you are farming.</summary>
    Kills,

    /// <summary>Drops by creature — your own observed rates, per mob, with kill counts.
    /// The evidence behind "is this camp worth it".</summary>
    Drops,
}

/// <summary>A tab as a UI should draw it — the same shape <see cref="LootTabHeader"/>
/// carries, because the tab strip is one shared control.</summary>
public sealed record CreatureTabHeader(CreatureTab Tab, string Label, string Key, string? Value);

/// <summary>
/// Ordering, labels and keys for the Kills &amp; Drops theme, in Core for the reason
/// <see cref="LootSurface"/> and <see cref="QuestSurface"/> are: one definition of what
/// the tabs ARE, or the desktop, the Linux widget and the phone drift (#122, #152, #184).
///
/// Step 1 of the recipe, deliberately landing before any window exists — the same way
/// <see cref="LootSurface"/> did. The vocabulary is settled once, so a key renamed later
/// is not a saved tab choice broken later.
/// </summary>
public static class CreatureSurface
{
    /// <summary>Single words, parallel with the Progress and Gear &amp; Loot tabs.</summary>
    public static string LabelFor(CreatureTab tab) => tab switch
    {
        CreatureTab.Kills => "Kills",
        CreatureTab.Drops => "Drops",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label.
    ///
    /// Kills keeps <c>kills</c>, the key that card has always used in
    /// <c>SectionOrder</c>, <c>HiddenSections</c>, <c>MiniStats</c> and
    /// <c>EQBUDDY_EXPAND</c>. Step 5 of the recipe is "fold the old keys, PRESERVING
    /// position and hidden state": the theme inherits the slot a player already put that
    /// card in rather than appearing at the bottom of their list.</summary>
    public static string KeyFor(CreatureTab tab) => tab switch
    {
        CreatureTab.Kills => "kills",
        CreatureTab.Drops => "drops",
        _ => tab.ToString().ToLowerInvariant(),
    };

    /// <summary>Every word these two surfaces have been called, so an old habit, an old
    /// doc line and the <c>EQBUDDY_DROPS</c> hook all still land.</summary>
    public static CreatureTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "kills" or "kill" => CreatureTab.Kills,
        "drops" or "targetdrops" or "dropsbycreature" => CreatureTab.Drops,
        _ => null,
    };

    /// <summary>Both tabs are real from the start — unlike Gear &amp; Loot, which had to
    /// name four and host two. Kills is already an <c>IWidgetCard</c> and Drops is an
    /// existing window, so there is no tab here with nothing behind it.</summary>
    public static readonly IReadOnlyList<CreatureTab> Hosted = [CreatureTab.Kills, CreatureTab.Drops];

    /// <summary>The card keys this theme absorbs, in the widget's own vocabulary. The fold
    /// reads this so the list of what disappears lives in ONE place rather than being
    /// spelled again in each UI's settings migration.</summary>
    public static readonly IReadOnlyList<string> AbsorbedCardKeys = ["kills"];

    /// <summary>The key the folded theme takes. <c>kills</c> because it is the card that
    /// exists on the widget today and the one a player may have positioned; Drops has
    /// never been a card at all, only a menu entry.</summary>
    public const string ThemeCardKey = "kills";

    /// <summary>The hosted tabs, with whatever headline each was given.</summary>
    public static IReadOnlyList<CreatureTabHeader> Tabs(string? kills = null, string? drops = null)
    {
        var values = new Dictionary<CreatureTab, string?>
        {
            [CreatureTab.Kills] = kills,
            [CreatureTab.Drops] = drops,
        };
        return [.. Hosted.Select(tab => new CreatureTabHeader(
            tab, LabelFor(tab), KeyFor(tab),
            values.TryGetValue(tab, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null))];
    }

    /// <summary>
    /// The launcher card's one-line summary — the line that has to justify replacing the
    /// Kills card's own header.
    ///
    /// **What moves while you play leads**, which is #219's lesson: the Progress launcher
    /// dropped the mote RATE to fit and the player who used that number turned up within
    /// the hour. Kills and the rate both change constantly, so both stay; the number of
    /// creature TYPES seen is the part that would be dropped if the line ever has to give.
    ///
    /// A part with nothing to say is omitted rather than printed as a zero — which is what
    /// keeps this short on a fresh character, exactly who is looking at a fresh widget.
    /// </summary>
    public static string LauncherSummary(int kills = 0, double killsPerHour = 0, int creatureTypes = 0)
    {
        var parts = new List<string>(3);
        if (kills > 0) parts.Add($"{kills} kill{(kills == 1 ? "" : "s")}");
        if (killsPerHour > 0) parts.Add($"{killsPerHour:0.#}/hr");
        if (creatureTypes > 0) parts.Add($"{creatureTypes} type{(creatureTypes == 1 ? "" : "s")}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "nothing killed yet";
    }
}
