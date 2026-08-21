using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the KILLS &amp; DROPS theme's tabs say in their badges, and what its launcher card
/// says on the widget (docs/Themes.md, step 3).
///
/// <see cref="CreatureSurface"/> in Core owns which tabs exist, their order, their labels
/// and their keys. This owns the NUMBERS beside those labels, which need a
/// <see cref="StatsSnapshot"/> — so they belong here with the rest of the presentation
/// rather than in Core. Exactly the split <see cref="LootTheme"/> and
/// <see cref="ProgressTheme"/> already make.
///
/// #210's rule is why it exists at all: the moment a fold puts a card header into a tab
/// strip is the moment a third copy of those strings gets hand-rolled somewhere and the
/// phone starts reporting a different number than the window. Decided once, here.
/// </summary>
public static class CreatureTheme
{
    /// <summary>The Kills badge — exactly what the Kills card's own header carried, so the
    /// glance a player already reads survives the fold verbatim: your kills, and the
    /// party's in parentheses when anyone else has been killing beside you.</summary>
    public static string Kills(StatsSnapshot s) =>
        s.PartyKillCount > 0 ? $"{s.YourKillCount} (+{s.PartyKillCount})" : $"{s.YourKillCount}";

    /// <summary>The Drops badge — how many creature types have actually dropped something,
    /// which is the denominator of every rate on that tab.
    ///
    /// **Blank until there is one**, the rule <see cref="LootTheme.Gear"/> was given by
    /// David on 2026-08-20: "0 creatures" on a fresh character reads as a failure rather
    /// than as a session that has not started, and the body underneath already says so in
    /// words.</summary>
    public static string Drops(StatsSnapshot s)
    {
        var mobs = s.Mobs.Count(m => m.Loot.Count > 0);
        return mobs == 0 ? "" : $"{mobs} creature{(mobs == 1 ? "" : "s")}";
    }

    /// <summary>The full strip, badges included — what the window's tab row and the mobile
    /// page's tab row both build from.</summary>
    public static IReadOnlyList<CreatureTabHeader> Tabs(StatsSnapshot s) =>
        CreatureSurface.Tabs(kills: Kills(s), drops: Drops(s));

    /// <summary>
    /// The launcher card's one line — the line that has to justify replacing the Kills
    /// card's own header with a door.
    /// </summary>
    /// <remarks>
    /// **What moves while you play leads**, which is #219's lesson taken in advance rather
    /// than after: the Progress launcher was trimmed to fit by dropping the mote RATE, and
    /// the player who used that number arrived within the hour of the release. Kills and
    /// the rate both move constantly, so both stay; the count of creature TYPES is the part
    /// that would be dropped if the line ever had to give, and it rides last for that
    /// reason.
    ///
    /// The assembly and the "omit a part with nothing to say" rule live in
    /// <see cref="CreatureSurface.LauncherSummary"/>, which is unit-tested in Core; this
    /// only decides which numbers go in.
    /// </remarks>
    public static string LauncherSummary(StatsSnapshot s) =>
        CreatureSurface.LauncherSummary(
            kills: s.YourKillCount,
            killsPerHour: s.KillsPerHour,
            creatureTypes: s.Mobs.Count(m => m.Loot.Count > 0));
}
