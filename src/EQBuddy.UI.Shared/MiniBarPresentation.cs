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

    /// <summary>The cells to draw, in <see cref="Order"/>, for the stats switched on.
    /// An unknown key is skipped rather than drawn blank — a settings file from a later
    /// version must not leave a hole in the bar.</summary>
    public static IReadOnlyList<MiniBarCell> Cells(StatsSnapshot s, IEnumerable<string> enabled)
    {
        var on = new HashSet<string>(enabled, StringComparer.Ordinal);
        return
        [
            .. Order.Where(on.Contains)
                .Where(Icons.ContainsKey)
                .Select(key => new MiniBarCell(key, Icons[key], Text(s, key))),
        ];
    }

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
