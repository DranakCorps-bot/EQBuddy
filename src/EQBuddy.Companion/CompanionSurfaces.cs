namespace EQBuddy.Companion;

/// <summary>
/// The surface registry: every screen a phone can be shown, by wire name. ONE list —
/// the desktop's offer checkboxes, the per-device ⚙ picker, the per-section change
/// detection and the subscription filter all read it, so a surface added here appears
/// everywhere without a second edit.
///
/// Order is the page's default display order: the things you glance at while camping
/// first, the reference lists after.
/// </summary>
public static class CompanionSurfaces
{
    public const string Map = "map";
    public const string Spawns = "spawns";
    public const string Mez = "mez";
    public const string Buffs = "buffs";
    public const string Combat = "combat";
    public const string Session = "session";
    public const string Loot = "loot";
    /// <summary>The four-tab progress surface (Experience · Wealth · Faction · Raids) —
    /// the PROGRESS THEME (docs/Themes.md, 2026-08-19). It was XP and AA alone until the
    /// desktop folded five cards into one window; the phone grew the other three tabs in
    /// the SAME change, which is #210's whole lesson.</summary>
    public const string Progress = "progress";
    /// <summary>The three-tab quest surface (General · Epic 1.0 · Plane of Sky) —
    /// the consolidation David asked for (2026-08-15). It REPLACED the separate
    /// <see cref="Epics"/> and <see cref="Sky"/> surfaces in <see cref="All"/>.</summary>
    public const string Quests = "quests";
    /// <summary>No longer offered surfaces — the quest surface's Epic and Sky tabs
    /// absorbed them — but still live as tick ROUTES: rows on those tabs send their
    /// taps under the old names, so one Apply path serves both eras.</summary>
    public const string Epics = "epics";
    public const string Sky = "sky";
    public const string Gear = "gear";
    /// <summary>The WORLD theme's Path tab, on the phone (World PR 4). Deliberately a
    /// SEPARATE surface from <see cref="Map"/> — the desktop folds Map/Camps/Path/Travels
    /// into one window, but a tablet showing the map AND timers at once is the product's
    /// uncontested ground, so the phone does NOT fold to match the desktop.</summary>
    public const string Travel = "travel";

    /// <summary>All surfaces this build knows, in default display order.</summary>
    public static readonly IReadOnlyList<string> All =
        [Map, Spawns, Travel, Mez, Buffs, Combat, Session, Loot, Progress, Quests, Gear];

    /// <summary>Human label for the desktop gate checkboxes (both UIs share it;
    /// the phone page carries its own copy in its SURFACE_META table).</summary>
    public static string Label(string surface) => surface switch
    {
        Map => "Zone map",
        Spawns => "Spawn timers",
        Travel => "Travel route",
        Mez => "Mez chips",
        Buffs => "Buffs",
        Combat => "Damage, healing & pet",
        Session => "Session stats",
        Loot => "Loot & watches",
        Progress => "Progress",
        Quests => "Quest tracker",
        Gear => "Gear checklist",
        _ => surface,
    };

    /// <summary>One line for the gate's tooltip — what leaves the PC if this stays
    /// ticked. Untickers deserve to know exactly what they're refusing.</summary>
    public static string Describe(string surface) => surface switch
    {
        Map => "The zone's map picture, your last /loc marker, and your archived spawn points.",
        Spawns => "Named spawn countdowns for the zone you're in.",
        Travel => "The destination you pick, and the hop-by-hop route there.",
        Mez => "Who's mezzed and how long is left.",
        Buffs => "Your buff set's state, and what you've lost this session.",
        Combat => "Ability breakdowns for damage, healing and your pet, last fight and session.",
        Session => "Kills, xp/hr, session length, dps.",
        Loot => "Session loot, what you've made, and your watch counters.",
        Progress => "XP and AA rates and what unlocked at your level, coin and motes, " +
                    "faction standing, and the raid targets you have cleared.",
        Quests => "Your quest tracker — the searchable catalog with your progress and pins, " +
                  "plus the Epic and Plane of Sky checklists, tappable from EQBuddy Mobile.",
        Gear => "Your gear checklist, by slot and by farm zone.",
        _ => "",
    };

    /// <summary>Surfaces whose rows a phone may TICK. Everything else is read-only —
    /// the phone gets no write the desktop doesn't already offer. Epics and Sky stay
    /// listed even though they left <see cref="All"/>: the quest surface's Epic and
    /// Sky tabs still send their row taps under those names.</summary>
    public static bool AcceptsTicks(string surface) =>
        surface is Quests or Epics or Sky or Gear;
}
