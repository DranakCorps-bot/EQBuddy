namespace EQBuddy.Core;

/// <summary>
/// The LIVE room's tabs — the sixth sibling of <see cref="ProgressSurface"/>,
/// <see cref="QuestSurface"/>, <see cref="CreatureSurface"/>, <see cref="LootSurface"/>
/// and <see cref="WorldSurface"/>. One definition of what the tabs ARE, read by the shell
/// room and by the address grammar, so a room cannot be spelled twice (#122, #152, #184).
///
/// **It is the first of these that is NOT a v1 theme, and the difference is the whole
/// shape of the room.** Every other surface in this family describes a fold that already
/// happened on the widget: a set of cards collapsed into one window, with an
/// <c>AbsorbedCardKeys</c> list and a <c>ThemeCardKey</c> naming the slot they took. Live
/// folds nothing. Its five sources are five separate places that all answer *"what is
/// happening in this sitting"* — two inline <c>MainWindow</c> sections (Combat, Healing),
/// three tabs of one floating breakout, a standalone pop-out, and one tab of the Kills
/// &amp; Drops window — and every one of them **stays exactly where it is** while this room
/// exists beside it. Subtracting a surface from the widget is gated per item on its shell
/// room existing, its HUD chip shipping and a screenshot proving the replacement does the
/// job (Bevel, Helm-signed 2026-09-04 ~11:15 PM CT), and that is a LATER PR by
/// construction. So there is no card key here to absorb, no settings migration, and
/// nothing for a fold to be idempotent about — which is why <see cref="InlineMode"/> does
/// not appear either. **Do not add an <c>AbsorbedCardKeys</c> list to this file
/// speculatively**: trap 55 is what a fold naming a key that is still a card costs, and a
/// fold that has not happened naming any key at all would be that bug with nothing on the
/// other side of it.
/// </summary>
public enum LiveTab
{
    /// <summary>What you dealt: the Combat card's summary and its by-ability breakdown,
    /// with the Damage breakout's Fight/Session axis over the top.</summary>
    Damage,

    /// <summary>What you healed — and the regen/hymn ticks the game logs without amounts,
    /// which is why this tab can be non-empty while every HPS row is missing.</summary>
    Healing,

    /// <summary>The pet's damage, by ability. Its own room rather than a fold inside
    /// <see cref="Damage"/>: a pet class drowning in rows is what asked for the split in
    /// the first place (#28).</summary>
    Pet,

    /// <summary>The whole pull on one canvas — a lane per skill over a DPS graph.</summary>
    Timeline,

    /// <summary>What died: this session's kills by creature, the farming rollup and the
    /// party-kill split. The <em>other</em> half of Kills &amp; Drops — drops-by-creature
    /// is camp research and belongs to World, per the disposition table's own Why column,
    /// and it is deliberately not here.</summary>
    Kills,

    /// <summary>What you cleared: raid targets witnessed or imported. **Moved here from
    /// Progress**, which is a move between two rooms rather than a subtraction from the
    /// widget — see <see cref="ProgressSurface.MovedToLive"/>.</summary>
    Raids,
}

/// <summary>A tab as a UI should draw it. <see cref="Value"/> is the tab's headline — the
/// number a glance wants without opening the room.</summary>
public sealed record LiveTabHeader(LiveTab Tab, string Label, string Key, string? Value);

/// <summary>
/// Builds the Live room's tab strip. Pure: takes the already-computed headlines, returns
/// headers — the same contract the other five surfaces keep, so the arithmetic stays where
/// a unit test can reach it and the room only draws.
/// </summary>
public static class LiveSurface
{
    /// <summary>The canonical label for each tab.
    ///
    /// **"Damage" and not "Combat".** The v1 card is called Combat and carries both what
    /// you dealt and what you took; on a strip that already says Healing and Pet beside it,
    /// "Combat" would be the only label naming a category rather than a number, and a
    /// player looking for their DPS would have to know that Combat is where it lives. The
    /// breakout window has called this "Your damage" since it shipped.</summary>
    public static string LabelFor(LiveTab tab) => tab switch
    {
        LiveTab.Damage => "Damage",
        LiveTab.Healing => "Healing",
        LiveTab.Pet => "Pet",
        LiveTab.Timeline => "Timeline",
        LiveTab.Kills => "Kills",
        LiveTab.Raids => "Raids",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so an address in a script or a
    /// doc survives a rename of the human-facing label.
    ///
    /// **Every key is a name one of the five sources already answered to**, never a new
    /// invention: <c>damage</c>/<c>healing</c>/<c>pet</c> are <c>BreakoutPresentation</c>'s
    /// own constants, <c>kills</c> is the Kills card's settings key, and <c>raids</c> is
    /// the key it carried under Progress — so <c>live:raids</c> and the old
    /// <c>progress:raids</c> differ only in the room, which is exactly what moved.</summary>
    public static string KeyFor(LiveTab tab) => tab switch
    {
        LiveTab.Damage => "damage",
        LiveTab.Healing => "healing",
        LiveTab.Pet => "pet",
        LiveTab.Timeline => "timeline",
        LiveTab.Kills => "kills",
        LiveTab.Raids => "raids",
        _ => tab.ToString().ToLowerInvariant(),
    };

    /// <summary>Every word these surfaces have been called, so an old habit and an old doc
    /// line both still land. <c>combat</c> is the v1 card's name and the phone screen's;
    /// <c>dps</c>/<c>hps</c>/<c>loot</c>-adjacent stars are the <c>MiniStats</c> keys the
    /// breakouts are gated on; <c>fight</c> is what the Combat card's ⧗ button opens.
    /// Unknown keys answer null — snapping to a default lands a caller somewhere it did not
    /// ask for, which is the refusal every sibling here makes.</summary>
    public static LiveTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "damage" or "combat" or "dps" => LiveTab.Damage,
        "healing" or "heals" or "hps" => LiveTab.Healing,
        "pet" => LiveTab.Pet,
        "timeline" or "fight" => LiveTab.Timeline,
        "kills" or "creatures" => LiveTab.Kills,
        "raids" => LiveTab.Raids,
        _ => null,
    };

    /// <summary>The tab the room opens on: the one the room's own name is about. Damage is
    /// the number a player opens a session meter to see, and it is the only tab that is
    /// non-empty from the first swing.</summary>
    public const LiveTab DefaultTab = LiveTab.Damage;

    public static IReadOnlyList<LiveTabHeader> Tabs(
        string? damage = null, string? healing = null, string? pet = null,
        string? timeline = null, string? kills = null, string? raids = null)
    {
        return
        [
            Header(LiveTab.Damage, damage),
            Header(LiveTab.Healing, healing),
            Header(LiveTab.Pet, pet),
            Header(LiveTab.Timeline, timeline),
            Header(LiveTab.Kills, kills),
            Header(LiveTab.Raids, raids),
        ];

        static LiveTabHeader Header(LiveTab tab, string? value) =>
            new(tab, LabelFor(tab), KeyFor(tab), string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
