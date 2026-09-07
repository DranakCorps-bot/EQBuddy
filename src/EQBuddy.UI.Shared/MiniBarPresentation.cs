using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One cell of the minimized bar: which icon, and what it currently reads.</summary>
/// <param name="Key">The settings key (<see cref="AppSettings.MiniStats"/>).</param>
/// <param name="Icon">A name from <see cref="IconPaths"/> — never a glyph.</param>
/// <param name="Text">The formatted value.</param>
public sealed record MiniBarCell(string Key, string Icon, string Text);

/// <summary>
/// The minimized bar's contents, decided once (Gate 5c).
///
/// Both widgets carried this table by hand, identically, right down to the comments —
/// which stat gets which glyph, how each value is formatted, and what order they sit in.
/// Two copies of one decision is the shape every drift in this codebase has started from
/// (#184, #122, #152), and it was the densest remaining cluster of glyphs on the surface
/// that is on screen the ENTIRE time a player is farming.
///
/// The glyphs are gone. Every icon here already existed in <see cref="IconPaths"/>, so
/// this cost no new geometry — a fair sign the vectors were being ignored rather than
/// missing. On the Linux and macOS builds a glyph can fail to render altogether (#148,
/// #166), and the minimized bar is precisely where a player is not looking closely enough
/// to notice a box where a skull should be.
///
/// **Deliberately not a size decision.** This says what a cell CONTAINS, never how wide it
/// is. Both widgets are <c>SizeToContent</c>, so a value that changes width on a timer
/// asks the window manager to resize an always-on-top window over a fullscreen game —
/// which cost #173 its keyboard. Reserved widths belong to the bar that draws these, and
/// arrive with #191 (TheMegaSage) when its contents become configurable.
/// </summary>
public static class MiniBarPresentation
{
    /// <summary>The order cells appear in, whichever subset is switched on. Not the
    /// order the player picked them in: a bar that reshuffles as you toggle stats is a
    /// bar you have to re-read every time.
    ///
    /// "buffs" is deliberately absent — it is a valid <see cref="AppSettings.MiniStats"/>
    /// entry that gates the Buffs breakout window and never draws a cell here.
    ///
    /// **"xp", "dps" and "hps" are absent for the opposite reason since Surface A / SA-1:
    /// they are always ON.** They were promoted to the collapsed HUD's fixed trio (name,
    /// DPS, XP%/hr — HPS taking the third slot while healing dominates), which is drawn by
    /// <see cref="HudGlance"/> ahead of every cell in this list. A key that is drawn
    /// unconditionally has no business in a table whose whole job is "which subset did the
    /// player switch on", and leaving one here would have drawn it twice.</summary>
    public static readonly IReadOnlyList<string> Order =
        ["kills", "pet", "procs", "loot", "motes", "money", "deaths"];

    /// <summary>The key the buff set's chip draws under. A <see cref="AppSettings.MiniStats"/>
    /// member since long before it drew anything, and deliberately absent from
    /// <see cref="Order"/> — see <see cref="CanonicalOrder"/> for why it has a PLACE here
    /// without having a row in any table above.</summary>
    public const string BuffsKey = "buffs";

    /// <summary>
    /// Every key that can sit on the bar, in the order an untouched profile draws them —
    /// the floor <see cref="AppSettings.MiniBarOrder"/> means by "empty".
    ///
    /// **It is <see cref="Order"/> plus "buffs", and the difference between the two lists is
    /// the point.** <see cref="Order"/> is a FORMATTING table: which stats this class can
    /// turn into an icon and a string. "buffs" is not one of them and cannot be — there is
    /// no buff state on <see cref="StatsSnapshot"/> at all, so <c>HudBarView</c> builds that
    /// chip's face from the buff tracker's own count (OE-7). But it is a chip on the bar
    /// like any other, so it has a PLACE, and a place is what an order is about. Its
    /// canonical slot is where it has always drawn: after "deaths".
    ///
    /// The trio (name, DPS, XP%/HPS) is absent for the reason SA-1 promoted it: those three
    /// are drawn unconditionally ahead of every cell here, and the third slot swaps identity
    /// mid-session (<see cref="HudGlance"/>), so a drag target there would change meaning
    /// under the cursor. Pinned watch chips are absent too — they are a BLOCK after the
    /// cells, one per rule, and per-rule placement would widen this list by rule id rather
    /// than by stat key. Both seams are named rather than built.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalOrder = [.. Order, BuffsKey];

    /// <summary>
    /// The player's chip order — every key of <see cref="CanonicalOrder"/>, exactly once.
    ///
    /// A key the setting omits is APPENDED in its canonical position rather than dropped, so
    /// a later release's new stat lands ON the bar instead of in a hole, and a stale file
    /// cannot silently lose a cell (trap 20's shape). Unknown names are skipped and
    /// duplicates collapse to their first appearance, so a hand-edited file cannot produce a
    /// bar that draws one chip twice. This is <c>HudChipRow.ResolveOrder</c>'s rule, stated
    /// once more for a list of stat keys rather than of families.
    /// </summary>
    public static IReadOnlyList<string> ResolveOrder(AppSettings settings)
    {
        var order = new List<string>();
        foreach (var name in settings.MiniBarOrder)
        {
            var key = CanonicalOrder.FirstOrDefault(
                k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key is not null && !order.Contains(key)) order.Add(key);
        }
        foreach (var key in CanonicalOrder)
            if (!order.Contains(key)) order.Add(key);
        return order;
    }

    /// <summary>Writes a new chip order into the profile. The WRITER half of
    /// <see cref="AppSettings.MiniBarOrder"/>, shipping in the same PR as its reader — the
    /// <c>DeadSettingTests</c> posture, which exists because three player-facing bugs came
    /// from data that survived a move and a write path that did not.
    ///
    /// The one caller is the mini bar's DROP. There is deliberately no second writer: two
    /// surfaces editing a brand-new setting on day one is the shape that produced #252.</summary>
    public static void SetOrder(AppSettings settings, IEnumerable<string> order) =>
        settings.MiniBarOrder = [.. order];

    /// <summary>
    /// The keys the bar actually DRAWS, in the player's order: <see cref="ResolveOrder"/>
    /// minus the stats with no ★, minus anything this class cannot put a face on.
    ///
    /// **The one membership decision, so the bar cannot draw a chip the order does not
    /// know about** (trap 4). <c>HudBarView</c> walks this list and asks <see cref="Cell"/>
    /// for each face, except <see cref="BuffsKey"/>, whose face it builds itself.
    /// </summary>
    public static IReadOnlyList<string> DrawnKeys(AppSettings settings)
    {
        var on = new HashSet<string>(settings.MiniStats, StringComparer.Ordinal);
        return
        [
            .. ResolveOrder(settings)
                .Where(on.Contains)
                .Where(key => key == BuffsKey || Icons.ContainsKey(key)),
        ];
    }

    /// <summary>A key list as one space-free token for the <c>EQBUDDY_EXPAND</c> dump
    /// ("money,kills,loot"), or "-" when the bar has no chips at all. The dump is
    /// space-separated key=value, so a value with a space in it would silently become two
    /// keys; "-" rather than "" because a key with an empty value cannot be waited on.</summary>
    public static string OrderKey(IEnumerable<string> keys) =>
        string.Join(",", keys) is { Length: > 0 } key ? key : "-";

    /// <summary>What each cell is CALLED, for the one screen that lists them.
    ///
    /// It had no such screen until 2026-08-21, and that was the hole. A stat's only switch
    /// was the star on its card header, so when the themes folded five cards into windows
    /// the switches went with them — and Options could only reach a star through its
    /// BREAKOUT checkbox, which exists for six kinds. Motes, money and kills have no
    /// breakout, so their stars became reachable only by opening the very window a player
    /// was complaining about ("hidden behind too much other junk I don't care about" -
    /// #228, daetien-lab). Same family as trap 20: the writers survived the fold, the ROUTE
    /// to them did not.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kills"] = "Kills",
            ["pet"] = "Pet damage",
            ["procs"] = "Weapon procs",
            ["loot"] = "Loot",
            ["motes"] = "Motes",
            ["money"] = "Coin",
            ["deaths"] = "Deaths",
        };

    /// <summary>Stat key → <see cref="IconPaths"/> name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Icons =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kills"] = "Skull",
            ["pet"] = "Paw",
            ["procs"] = "Bolt",
            ["loot"] = "Bag",
            ["motes"] = "Sparkle",
            ["money"] = "Coin",
            ["deaths"] = "Skull",
        };

    /// <summary>One cell's face — its icon and what it currently reads — or null when this
    /// table cannot format the key.
    ///
    /// Null is a real answer and has two readings, both of which the caller handles the same
    /// way: a settings file from a LATER version naming a stat this release has never heard
    /// of (which must be skipped rather than drawn blank — a hole in the bar), and
    /// <see cref="BuffsKey"/>, which is a chip the bar builds for itself.</summary>
    public static MiniBarCell? Cell(StatsSnapshot s, string key) =>
        Icons.TryGetValue(key, out var icon) ? new MiniBarCell(key, icon, Text(s, key)) : null;

    /// <summary>What one cell reads. Every format here was already agreed by both
    /// widgets; the point is that it is now agreed in one place.</summary>
    public static string Text(StatsSnapshot s, string key) => key switch
    {
        "kills" => $"{s.YourKillCount}",
        // No "dps"/"hps"/"xp" rows: those three are the always-on HUD trio since SA-1 and
        // HudGlance formats them. Leaving a second formatter here would be two sources for
        // one number (trap 4), and the day one of them gained a decimal only the other
        // would move.
        "pet" => $"{s.PetAbilities.Sum(p => p.Total) / Math.Max(1, s.CombatSeconds):0.#} dps",
        // Same denominator as the Procs card: combat minutes, so downtime doesn't
        // flatter the weapon.
        "procs" => $"{s.Procs.Sum(p => p.Count) / Math.Max(1.0 / 60, s.CombatSeconds / 60.0):0.#}/min",
        "loot" => $"{s.LootTotal}",
        "motes" => Motes.Summarize(s.Loot, s.Elapsed) is { Total: > 0 } mo
            ? $"{mo.Total} · {mo.PerHour:0.#}/hr" : "0",
        "money" => StatsSnapshot.FormatCoin(s.Copper),
        "deaths" => $"{s.Deaths.Count}",
        _ => "",
    };
}
