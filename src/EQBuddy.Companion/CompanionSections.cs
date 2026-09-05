namespace EQBuddy.Companion;

// The wire shape of every surface. One file so the protocol reads top to bottom;
// each record is pre-chewed for a phone (numbers already formatted where the phone
// can't do better, seconds rather than absolute times so a clock-skewed device still
// counts down correctly, semantic flags rather than colors so the theme decides).

// ---------------- spawns / session (Phase 1) ----------------

public sealed record CompanionSpawnSection(IReadOnlyList<CompanionSpawnTimer> Timers);

/// <summary>One spawn countdown, pre-chewed for a phone: the page ticks
/// <see cref="RemainingSeconds"/> down locally between pushes, so it is the remaining
/// time AT <see cref="CompanionSnapshot.SentAtUtc"/>. Null remaining = the kill was
/// seen but no respawn duration is known ("killed, duration unknown").</summary>
public sealed record CompanionSpawnTimer(
    string Name,
    string Zone,
    double? RemainingSeconds,
    bool Due,
    bool Imminent,
    double? DurationSeconds);

/// <summary>
/// Session basics for the footer strip — and, since E-3 PR 5, what you CLEARED.
///
/// **<see cref="Raids"/> arrived here from <see cref="CompanionProgressSection"/> in the
/// same commit that moved the desktop's Raids tab from the Progress room to the Live
/// room**, which is Bevel's §3 requirement rather than tidiness: the phone's progress
/// screen follows the ROOM, and <c>CompanionSurfaces.PageFor</c>'s own comment had said so
/// since PR 1 (*"it stays Progress until that PR moves it"*). Session is the screen that
/// <c>PageFor</c> routes to <c>ShellPage.Live</c>, so it is where Raids belongs on this
/// surface. Moving one host and not the other would leave the desktop and the phone
/// disagreeing about what "Progress" contains — trap 33's shape, one level up from data
/// into which room a fact lives in.
/// </summary>
public sealed record CompanionSessionSection(
    int Kills,
    double XpPerHour,
    double SessionSeconds,
    double SessionDps,
    CompanionRaidsBlock? Raids = null);

// ---------------- map ----------------

/// <summary>
/// The zone map. Split deliberately: <see cref="Geometry"/> is the STATIC picture
/// (thousands of segments from the map pack) and is sent once per zone per device —
/// the server withholds it while a device already holds that <see cref="GeometryStamp"/> —
/// while the marker and circles ride every push. The page keeps the last geometry it
/// received and re-attaches it whenever the stamp still matches.
/// </summary>
public sealed record CompanionMapSection(
    string Zone,
    /// <summary>The catalog zone the spawn archive lives under — what a curation edit
    /// must name. Not the same string as <see cref="Zone"/> in tiered zones, and
    /// aiming an edit at the wrong one would quietly curate a different archive.</summary>
    string TimerZone,
    string GeometryStamp,
    CompanionMapGeometry? Geometry,
    string? Missing,
    CompanionMapMarker? You,
    IReadOnlyList<CompanionMapCircle> Circles,
    IReadOnlyList<CompanionMapCrumb> Trail,
    IReadOnlyList<CompanionMapNamed> Named,
    /// <summary>Session camp markers (World PR 4) — "Drop camp marker"'s output, at
    /// last. Desktop-dropped and phone-dropped markers land on the same list, since both
    /// write the same <c>SessionStats.AddMarker</c>. Empty (not null) when nothing has
    /// been dropped, or when a marker exists but carries no location (dropped before the
    /// first /loc) — that marker still shows on the Travels list, it just plants no pin
    /// here, exactly as a named with no camp yet gets a row but no dot.</summary>
    IReadOnlyList<CompanionMapPin> Markers);

/// <summary>One dropped camp marker, plotted. <see cref="AgeSeconds"/> rides the wire
/// rather than a label already worded ("3m ago") for the same reason
/// <see cref="CompanionMapMarker.AgeSeconds"/> does — the page ticks it locally, so a
/// marker sitting on screen for an hour does not need an hourly re-push to stay honest.</summary>
public sealed record CompanionMapPin(double X, double Y, string Text, double AgeSeconds);

/// <summary>Map-space geometry. Coordinates are rounded to whole map units (roughly
/// game feet) — the pack's sub-unit precision is invisible on a phone and doubles the
/// payload.</summary>
public sealed record CompanionMapGeometry(
    string Stamp,
    int MinX, int MinY, int MaxX, int MaxY,
    IReadOnlyList<CompanionMapStroke> Strokes,
    IReadOnlyList<CompanionMapPoi> Pois,
    bool Truncated);

/// <summary>All segments of one color, flattened to x1,y1,x2,y2,… — the page turns
/// each stroke into a single SVG path, exactly as the desktop makes one Path per color.</summary>
public sealed record CompanionMapStroke(string Color, IReadOnlyList<int> Segments);

public sealed record CompanionMapPoi(int X, int Y, string Color, string Label);

/// <summary>Where the player's last /loc put them, in map space.</summary>
public sealed record CompanionMapMarker(double X, double Y, double AgeSeconds);

/// <summary>One breadcrumb of the /loc trail, in map space, oldest first — the same
/// list the desktop map draws its comet tail from (thinned to 25 map units apart by
/// SessionStats, so the tail spans real ground rather than one corridor).
///
/// The AGE rides the wire, not an alpha: the page fades locally against
/// <see cref="EQBuddy.UI.Shared.TrailFade"/>'s curve every second, so the tail keeps
/// burning down smoothly between pushes exactly as it does on the desktop — and a
/// crumb merely fading never counts as a change worth waking a device for.</summary>
public sealed record CompanionMapCrumb(double X, double Y, double AgeSeconds);

/// <summary>One running spawn timer in the shown zone — the row the desktop map's named
/// panel draws, and the camp pin it plants, from a single answer. They are the same
/// question asked twice on the desktop too (UpdateNamedPanel builds both from one
/// resolved list), and splitting them here would let a pin and its row disagree.
///
/// <see cref="X"/>/<see cref="Y"/> are the camp in map space, null when no camp is known
/// yet — those named still get a ROW (with the desktop's "/loc during the fight" nudge),
/// they just get no pin. <see cref="FromWiki"/> is the desktop's "~": approximate,
/// and said out loud rather than passed off as your own observation.
///
/// <see cref="DueSeconds"/> is the countdown at send time; the page ticks it locally like
/// every other clock, so a running timer doesn't wake a device once a second.</summary>
public sealed record CompanionMapNamed(
    string Name,
    double? DueSeconds,
    bool Due,
    double? DurationSeconds,
    double? X, double? Y,
    bool FromWiki);

/// <summary>An archived spawn point. <see cref="Named"/> points wear the accent and
/// carry a <see cref="Label"/>; ordinary ones sit dim. <see cref="DueSeconds"/> is the
/// countdown at send time (negative = already due), <see cref="Projected"/> marks the
/// ordinary-point estimate the desktop prints with a "~".
///
/// <see cref="LocY"/>/<see cref="LocX"/> are the point's own game coordinates, carried
/// so a device curating this circle echoes them back verbatim. The page must never
/// derive them from <see cref="X"/>/<see cref="Y"/>: getting that inversion subtly
/// wrong would aim a removal at the wrong dot, and nothing on screen would say so.</summary>
public sealed record CompanionMapCircle(
    double X, double Y,
    bool Named,
    string? Label,
    bool Confirmed,
    double? DueSeconds,
    bool Imminent,
    bool Projected,
    int Kills,
    string Mobs,
    double LocY, double LocX);

// ---------------- travel ----------------

/// <summary>
/// The Path tab on a phone (World PR 4) — the SAME <see cref="EQBuddy.Core.TravelPlan"/>
/// module the desktop's Path tab reads, so the two cannot compute different routes for
/// one destination (#210's rule). Resent in full every tick rather than riding the map's
/// sticky-payload machinery (trap 38): a route is a few dozen zone names at most, and it
/// must never go stale the way a withheld map geometry silently can.
/// </summary>
public sealed record CompanionTravelSection(
    string From,
    string? Destination,
    /// <summary>Every zone the embedded ZoneGraph knows, for the destination picker.</summary>
    IReadOnlyList<string> Zones,
    /// <summary><see cref="EQBuddy.Core.TravelOutcome"/> lowercased — "route" /
    /// "alreadythere" / "noroute" — the same semantic-flag convention
    /// <see cref="CompanionBuffRow.Status"/> uses, so the page colors it rather than the
    /// server inventing a color here.</summary>
    string Outcome,
    int Hops,
    IReadOnlyList<string> Path,
    string Note);

// ---------------- mez ----------------

public sealed record CompanionMezSection(IReadOnlyList<CompanionMezChip> Chips);

/// <summary>One mez chip, numbered and warned exactly as MezChipsWindow does.
/// <see cref="Fraction"/> is the ELAPSED share (the page draws 1 - fraction, a
/// draining gauge); null when the duration is unknown.</summary>
public sealed record CompanionMezChip(
    string Name,
    double? RemainingSeconds,
    bool Warning,
    double? Fraction,
    string Detail);

// ---------------- buffs ----------------

public sealed record CompanionBuffsSection(
    IReadOnlyList<CompanionBuffGroup> Groups,
    IReadOnlyList<CompanionBuffLoss> Lost);

public sealed record CompanionBuffGroup(string Class, IReadOnlyList<CompanionBuffRow> Rows);

/// <summary><see cref="Status"/> is the BuffSetEvaluator state lowercased
/// ("active"/"expiring"/"missing"/"notSeen") — a semantic the page colors itself, so
/// the desktop's theme decides what "expiring" looks like on the phone too.</summary>
public sealed record CompanionBuffRow(
    string Spell,
    string Status,
    double? RemainingSeconds,
    bool Estimated);

public sealed record CompanionBuffLoss(string Spell, string Cause, double AgoSeconds);

// ---------------- combat ----------------

/// <summary>The three breakout boards in one section; each carries BOTH scopes so the
/// page's fight/session toggle is instant and needs no round trip.</summary>
public sealed record CompanionCombatSection(IReadOnlyList<CompanionCombatBoard> Boards);

public sealed record CompanionCombatBoard(
    string Key,
    string Label,
    string FightHeader,
    string SessionHeader,
    IReadOnlyList<CompanionAbilityRow> Fight,
    IReadOnlyList<CompanionAbilityRow> Session);

/// <summary>One ability row. <see cref="Value"/> is the desktop's own line (total ·
/// ×hits · avg · rate), <see cref="Fraction"/> the bar width against the top row, and
/// <see cref="Percent"/> the share of the board's total.</summary>
public sealed record CompanionAbilityRow(
    string Name,
    string Value,
    double Fraction,
    double Percent,
    long Total,
    int Hits);

// ---------------- loot ----------------

public sealed record CompanionLootSection(
    int Total,
    int CraftedTotal,
    IReadOnlyList<CompanionCountRow> Items,
    IReadOnlyList<CompanionCountRow> Crafted,
    IReadOnlyList<CompanionWatchRow> Watch);

public sealed record CompanionCountRow(string Name, int Count);

/// <summary>A watch rule's running count — the same three numbers the desktop's watch
/// card shows (total · /hr · /active hr).</summary>
public sealed record CompanionWatchRow(
    string Name,
    int Total,
    double PerHour,
    double PerActiveHour,
    string? LastItem);

// ---------------- checklists (epics / sky / gear) ----------------

/// <summary>One checklist, shaped the same for Epics, Sky and Gear so the page has a
/// single renderer and the tick path has a single action.</summary>
public sealed record CompanionChecklistSection(
    int Done,
    int Total,
    IReadOnlyList<CompanionChecklistGroup> Groups,
    /// <summary>The in-game command this checklist depends on, when it depends on one —
    /// Gear auto-ticks from the inventory dump; Epics and Sky don't and send null.
    /// Null is omitted from the JSON, so a checklist with no command costs nothing.</summary>
    CompanionCommandPrompt? Prompt = null,
    /// <summary>This checklist's own empty-state sentence, when the page's generic
    /// "set it up on the PC" would name a task with no route — Gear's list is a website
    /// export behind a particular Options page, and saying so is the other half of the
    /// same defect the ⧉ buttons fix (David, 2026-08-20). Null keeps the generic line.</summary>
    string? Empty = null);

/// <summary>An in-game command shown as SELECTABLE TEXT rather than offered as a ⧉ copy
/// (David, 2026-08-20): the phone's clipboard cannot paste into the game on the PC, so a
/// button there would be a silent no-op wearing a working control's clothes. Comes over
/// the wire rather than being spelled in index.html, for the same reason the desktops
/// read <c>GameCommands</c> — the page holding its own copy of a command is exactly the
/// drift the constant exists to prevent, and trap 32 means a page-side literal can sit on
/// an open phone for weeks after the PC has moved on.</summary>
public sealed record CompanionCommandPrompt(string Lead, string Command, string Note);

/// <summary><see cref="Class"/> is the group's class when it has exactly one (Epic and
/// Sky groups do; Gear's and Sky's cross-class ★ Ready group don't) — the quest
/// surface's class lens filters on it rather than parsing headings.</summary>
/// <param name="Tickable">False for a SUMMARY group whose rows are not items — the Sky
/// ★ Ready band names rewards, and its row ids are reward keys that no tick action
/// accepts. It rendered checkboxes anyway, so every one of them was a silent no-op, and
/// "silent no-ops are broken" is a house rule (#212, bjstrange).</param>
/// <param name="Title">The reward (Sky) or section (Epic) WITHOUT the class — the same
/// split <see cref="EQBuddy.Core.QuestChecklistGroup.Title"/> carries, and for the same
/// reason: the page's item-grouped search (#108) needs the reward on its own, and
/// recovering it by splitting <paramref name="Heading"/> on the separator is one fact
/// stored in one place and read out of another (trap 4) — it breaks on the first reward
/// whose name contains the separator.</param>
public sealed record CompanionChecklistGroup(
    string Heading,
    string? Note,
    IReadOnlyList<CompanionChecklistRow> Rows,
    string? Class = null,
    bool Tickable = true,
    string? Title = null);

/// <summary><see cref="Id"/> is what a tap sends back to tick the row — the stored
/// item's own id for Epics/Sky, slot|item for Gear (which has no id of its own).</summary>
public sealed record CompanionChecklistRow(
    string Id,
    string Text,
    string? Detail,
    bool Done);

// ---------------- quests (General · Epic 1.0 · Plane of Sky) ----------------

/// <summary>
/// The quest surface: the same three tabs as the desktop quest window — the strip is
/// built from Core's QuestSurface, so the two UIs cannot disagree about which tabs
/// exist — plus the general tracker's state and the two checklists the Epic and Sky
/// tabs render.
///
/// The CATALOG is the sticky payload: ~1,200 quests compact to a few hundred KB of
/// search index, shipped once per device and withheld while the device already holds
/// <see cref="CatalogStamp"/> (exactly the map-geometry contract in
/// <see cref="CompanionSnapshot.ForClient"/>). Search then runs on the device —
/// instant, no round trip per keystroke, and identical to the desktop's because the
/// index rows carry the same fields its search reads.
///
/// <see cref="Mine"/> is the desktop's "mine" view by NAME only — membership and
/// order come from Core's QuestMatcher, and the device joins everything else (giver,
/// zone, rewards, items) from the catalog it holds, with progress recomputed from
/// <see cref="Owned"/>. One list on the wire, not two copies of every field.
/// </summary>
public sealed record CompanionQuestsSection(
    IReadOnlyList<CompanionQuestTab> Tabs,
    string CatalogStamp,
    CompanionQuestCatalog? Catalog,
    IReadOnlyList<string> Mine,
    /// <summary>Matches beyond the shipped cap — never a silent cap; the page prints
    /// "+N more".</summary>
    int MineMore,
    /// <summary>Item → owned count (looted + manual − consumed), quest-relevant items
    /// only — what the page computes have/need and "ready" from, for searched cards
    /// exactly as for <see cref="Mine"/> ones.</summary>
    IReadOnlyDictionary<string, int> Owned,
    IReadOnlyList<string> Tracked,
    IReadOnlyList<string> Hidden,
    IReadOnlyDictionary<string, int> Completed,
    /// <summary>The character's picked classes (the ⚙ picker's state, from the
    /// ledger) — the page's chip row narrows within these.</summary>
    IReadOnlyList<CompanionQuestClass> Classes,
    /// <summary>Set only when nothing is picked and the log's evidence suggests a
    /// class — always labeled inferred on screen, exactly as the desktop labels it.</summary>
    string? InferredClass,
    /// <summary>Every class this character has, and the WORDS for where that came from
    /// ("from your achievements" / "inferred from your log" / "your picks") — decided once
    /// desktop-side. Empty when nothing knows yet.</summary>
    IReadOnlyList<string>? CharacterClasses,
    string? ClassSourceLabel,
    CompanionChecklistSection Epics,
    CompanionChecklistSection Sky);

/// <summary>One tab tile: key/label straight from Core's QuestSurface, and the
/// "done / total" badge — null on General, which is a catalog you search rather than
/// a checklist you finish.</summary>
public sealed record CompanionQuestTab(string Key, string Label, string? Badge);

public sealed record CompanionQuestClass(string Name, string Abbrev);

/// <summary>The searchable index of the whole shipped catalog. Single-letter members
/// keep ~1,200 entries small on the wire; <see cref="AllClasses"/> rides here so the
/// page's class picker lists exactly Core's classes (Berserker was once missed by a
/// hand-kept copy — never again).</summary>
public sealed record CompanionQuestCatalog(
    string Stamp,
    IReadOnlyList<CompanionQuestClass> AllClasses,
    IReadOnlyList<CompanionQuestIndexEntry> Quests);

/// <summary>One quest, pre-chewed for search and cards: name, url, giver, start zone,
/// min level, the wiki's class text (displayed as written, never parsed on the page),
/// the abbrevs of classes that can do it (null = any — computed by Core's
/// QuestClassFilter at build, so the page checks membership instead of re-implementing
/// the text rules), turn-in items, rewards, era, repeatable, collection-page.</summary>
public sealed record CompanionQuestIndexEntry(
    string N,
    string U,
    string G,
    string Z,
    int L,
    string C,
    IReadOnlyList<string>? A,
    IReadOnlyList<CompanionQuestNeed> I,
    IReadOnlyList<string> R,
    string E,
    bool P,
    bool O);

public sealed record CompanionQuestNeed(string N, int Q);

// ---------------- progress ----------------

/// <summary>
/// The PROGRESS THEME on a phone (docs/Themes.md): the same tabs the desktop ROOM shows —
/// Experience, Wealth and Faction — from the same
/// <see cref="EQBuddy.Core.ProgressSurface"/> definition and the same
/// <see cref="EQBuddy.UI.Shared.ProgressTheme"/> badges.
///
/// **RAIDS LEFT THIS SECTION IN E-3 PR 5**, in the same commit that moved the desktop's
/// Raids tab out of the shell's Progress room, and it is on
/// <see cref="CompanionSessionSection"/> now — the phone screen
/// <c>CompanionSurfaces.PageFor</c> routes to the Live room. The tab strip loses its fourth
/// chip through <see cref="EQBuddy.Core.ProgressSurface.MovedToLive"/> rather than through
/// a filter typed here, which is what stops the two hosts drifting (trap 55).
///
/// **The v1 desktop <c>ProgressWindow</c> still shows four**, and that is not a
/// disagreement: retiring a tab from a v1 window is a subtraction, gated per item on a
/// screenshot and a later PR, while this section mirrors the room the phone's picker names.
///
/// It grew the three new blocks in the SAME change as the desktop fold, which is the whole
/// lesson of #210: EQBuddy Mobile went on building the cross-class ready list for two days
/// after the desktop had lost it, and restoring the desktop immediately created the mirror
/// risk. Parity by feature list drifts; parity by shared module does not.
///
/// <see cref="Tabs"/> rides the wire rather than being rebuilt on the page, so the phone
/// cannot end up naming different tabs, ordering them differently, or computing a
/// different badge than the window it mirrors.
/// </summary>
public sealed record CompanionProgressSection(
    double XpPercent,
    double XpPerHour,
    double XpPerActiveHour,
    double? HoursToLevel,
    int AaGained,
    int AaTotal,
    double AaPerHour,
    int? Level,
    string? UnlocksLabel,
    IReadOnlyList<CompanionUnlockRow> Unlocks,
    IReadOnlyList<CompanionProgressTab> Tabs,
    CompanionWealthBlock Wealth,
    IReadOnlyList<CompanionCountRow> Faction,
    /// <summary>The NEXT level's preview — "At level 34: 2 new AA abilities, 3 new
    /// spells" — and its per-class groups. Null when no level is known, no class is in
    /// play, or the catalogs have nothing further (Bevel's three empty rules,
    /// Helm-signed 2026-08-23). **Its own heading, never the ding's**: Bevel, on finding
    /// the phone painting only the ding under "New at level" — *"do not steal that
    /// heading."*</summary>
    string? NextLabel = null,
    IReadOnlyList<CompanionUnlockGroup>? NextGroups = null,
    /// <summary>Whether to draw the groups as expanders. **The decision rides the wire.**
    /// It is <c>LevelUnlockGroups.WorthGrouping</c>'s answer, and a page recomputing it
    /// from <c>NextGroups.length</c> would be a fourth copy of a rule that exists to have
    /// one — the #210 shape, and the reason the tab strip rides the wire too.</summary>
    bool NextGrouped = false,
    /// <summary>Which group starts open, by index — <c>LevelUnlockGroups.DefaultOpenIndex</c>,
    /// decided desktop-side like everything else about this split. The first group with
    /// something to SHOW, which is only the first group when the first group is not empty:
    /// a Warrior whose next milestone is an Archetype AA would otherwise open an empty
    /// group above the collapsed one holding the single row.</summary>
    int NextOpenIndex = 0,
    /// <summary>Motes per hour as one summary line, for the Experience tab (David,
    /// 2026-08-23). Null when nothing has dropped. The same string the desktop shows,
    /// from <c>MotesPresentation.RateLine</c> — the phone's Wealth tab carries the Motes
    /// card's own summary and this is the Experience room's line, both off one
    /// formatter.</summary>
    string? MoteLine = null,
    /// <summary>The Level-ups fold's label — "Level-ups (17) · last Aug 23" — or null when
    /// this character has never dinged while EQBuddy watched, which is the phone's "no
    /// heading over nothing" (#240). It is <c>LevelHistory.FoldLabel</c>'s answer rather
    /// than a count the page formats: the count and the last date are the glance the fold
    /// is closed over, and a second formatter is how the phone and the window start
    /// disagreeing about a string a player reads on both.</summary>
    string? LevelUpsLabel = null,
    /// <summary>Every level-up, newest first — the SAME rows the two windows draw, from
    /// <c>LevelHistory</c> (<c>SurfaceParityTests</c> holds them to it).
    ///
    /// **Deliberately not capped at <c>MaxRows</c> like the lists above it.** This one is
    /// bounded by the level cap rather than by how long you played, and it is ordered
    /// newest-first — so a cap would drop the EARLIEST dings, which are the rarest rows and
    /// the ones a player goes looking for (trap 50, #234). The desktop draws all of them;
    /// a phone that quietly showed twenty would be a different answer to the same
    /// question.</summary>
    IReadOnlyList<CompanionLevelUpRow>? LevelUps = null);

/// <summary>One level-up on the phone: "Level 24" · "Aug 23, 8:14 PM", plus the hover.
///
/// A record of its own rather than <see cref="CompanionUnlockRow"/> because of
/// <see cref="Tip"/>, and the tip is Bevel's call (2026-09-02, Helm-signed): the gap since
/// the previous level-up is HOVER text on all three surfaces, never a dim third token and
/// never "x ago" — an age ticks, and a ticking string wakes every paired device on the
/// section fingerprint (trap 8).</summary>
/// <param name="Tip">Null for the oldest row, which has no previous ding to measure from.
/// Carried per ROW rather than looked up by name: dying back a level and re-dinging it
/// writes "Level 24" twice with a different gap each time, so a name-keyed lookup would
/// answer the same thing for both — the same fact the desktop's <c>CardRow.Tip</c>
/// exists for.</param>
public sealed record CompanionLevelUpRow(string Name, string Value, string? Tip);

/// <summary>One class's share of a level's unlocks. <see cref="Empty"/> is the words a
/// class that gains nothing shows — a class row is KEPT rather than dropped (Bevel,
/// Helm-signed), because on screen a missing group is indistinguishable from that class
/// not being one of yours.</summary>
/// <param name="Class">Named <c>Class</c>, not <c>ClassName</c>, and that is load-bearing:
/// <see cref="CompanionSnapshot.JsonOpts"/> is camelCase, so the property name IS the wire
/// key. It shipped once as <c>ClassName</c> — reaching the page as <c>className</c> while
/// every other group record on this wire (<see cref="CompanionBuffGroup"/>, the quest group)
/// says <c>class</c> — so the page's <c>g.class</c> was <c>undefined</c> on a real phone: a
/// heading reading "▾ undefined", and one open/shut state shared by every group because they
/// were all keyed on the same <c>undefined</c>. `CompanionWireKeyTests` is the guard.</param>
public sealed record CompanionUnlockGroup(
    string Class, IReadOnlyList<CompanionUnlockRow> Rows, string? Empty);

/// <summary>One tab, already labelled and badged by Core + UI.Shared. <see cref="Key"/> is
/// the stable wire key, so a device's saved tab survives a rename of the label.</summary>
public sealed record CompanionProgressTab(string Key, string Label, string? Badge);

/// <summary>Coin and motes — the tab that merges two cards, because motes are currency in
/// Legends and "what was the trip worth" should not require knowing which card held which
/// half. Coin values are pre-formatted: the phone cannot do better than the app's own
/// FormatCoin, and two formatters for one number is how they drift.</summary>
public sealed record CompanionWealthBlock(
    string Total,
    string Corpses,
    string Sales,
    string PerHour,
    int CoinDrops,
    int SalesCount,
    IReadOnlyList<CompanionCountRow> Sold,
    string MotesSummary,
    IReadOnlyList<CompanionCountRow> Motes);

/// <summary>Raid targets, per zone. <see cref="CompanionRaidZone.Done"/> over its boss
/// count is the desktop's own zone heading; <see cref="CompanionRaidBoss.Detail"/> is its
/// row text, built desktop-side so a difficulty badge cannot be invented here.</summary>
public sealed record CompanionRaidsBlock(
    int Defeated, int Total, IReadOnlyList<CompanionRaidZone> Zones,
    /// <summary>The achievements dump, for the same reason the desktop Raids card carries
    /// a ⧉ button in both its states — the page named the command in prose and offered
    /// nothing.</summary>
    CompanionCommandPrompt? Prompt = null);

public sealed record CompanionRaidZone(string Zone, int Done, int Total, IReadOnlyList<CompanionRaidBoss> Bosses);

public sealed record CompanionRaidBoss(string Name, bool Cleared, string Detail);

public sealed record CompanionUnlockRow(string Name, string Value);
