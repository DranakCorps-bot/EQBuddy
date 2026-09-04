using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

public sealed class AppSettings
{
    public string? LogFolder { get; set; }
    /// <summary>Folder holding EQBuddySetup.exe for updates; null = auto-detect OneDrive.</summary>
    public string? UpdateFolder { get; set; }
    /// <summary>This copy has been told that EQBuddy v2 is Windows-only and that it is
    /// staying on the final v1 build (charter LEGACY-002 / #275). Set the first time the
    /// notice is shown, so the automatic 6-hourly check says it ONCE — the Help menu's
    /// "Check for updates" always answers, whatever this says. Read and written in exactly
    /// one place per lane, both of them through
    /// <c>EQBuddy.UI.Shared.LegacyPlatformUpdatePolicy</c>; nothing on Windows ever touches
    /// it.</summary>
    public bool LegacyFinalNoticeAcknowledged { get; set; }
    public bool Minimized { get; set; }
    public List<string> MiniStats { get; set; } = ["kills", "dps"];
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double Opacity { get; set; } = 0.96;
    public double UiScale { get; set; } = 1.0;
    /// <summary>Scale for the small floating windows — spawn/mez chips and the alert
    /// banner — independent of UiScale so 4K players can grow just those (discussion #47).</summary>
    public double ChipScale { get; set; } = 1.0;
    public double QuestsLeft { get; set; } = double.NaN;
    public double QuestsTop { get; set; } = double.NaN;
    /// <summary>The Progress window's saved spot (the PROGRESS THEME, docs/Themes.md).
    /// NaN until it has been opened and moved, like the Quest Tracker's pair above —
    /// WindowPlacement.PositionToPersist is what stops an unmoved fallback overwriting
    /// a real saved position (#117).</summary>
    public double ProgressLeft { get; set; } = double.NaN;
    public double ProgressTop { get; set; } = double.NaN;

    /// <summary>The Gear &amp; Loot window's spot. NaN until it has been placed once —
    /// WindowPlacement.PositionToPersist keeps an unmoved fallback from overwriting a
    /// real saved position (#117).</summary>
    public double GearLootLeft { get; set; } = double.NaN;
    public double GearLootTop { get; set; } = double.NaN;

    /// <summary>The Kills &amp; Drops window's spot. NaN until it has been placed once -
    /// WindowPlacement.PositionToPersist keeps an unmoved fallback from overwriting a
    /// real saved position (#117).</summary>
    public double CreatureLeft { get; set; } = double.NaN;
    public double CreatureTop { get; set; } = double.NaN;

    /// <summary>The WORLD theme window's spot (World PR 2 — Map · Camps · Path · Travels,
    /// replacing the three standalone windows below). NaN until placed once -
    /// WindowPlacement.PositionToPersist keeps an unmoved fallback from overwriting a
    /// real saved position (#117).</summary>
    public double WorldLeft { get; set; } = double.NaN;
    public double WorldTop { get; set; } = double.NaN;
    /// <summary>Quest Tracker era ceiling ("" = any): quests after this era are hidden
    /// (discussion #62). Persisted app-wide — the world's era isn't per character.</summary>
    public string QuestEraFilter { get; set; } = "";
    /// <summary>Per-window Ctrl+wheel zoom factors, keyed by window kind ("drops",
    /// "breakout:Damage", …) — the universal text-scaling answer (discussion #59;
    /// David: "a more permanent scaling solution").</summary>
    public Dictionary<string, double> WindowZooms { get; set; } = new();

    /// <summary>Per-window BASE width, keyed the same way as <see cref="WindowZooms"/> -
    /// the width a theme window opens at before the zoom multiplies it. Written when a
    /// player drags the window's edge, read on the next open.
    ///
    /// A base rather than the actual width, deliberately: the zoom already owns the final
    /// number (Width = base x zoom), and storing the multiplied value would compound every
    /// session until the window walked off the screen. See UI.Shared/WindowSizing.</summary>
    public Dictionary<string, double> WindowBaseWidths { get; set; } = new();

    /// <summary>Per-window height, same keys. Only the theme windows use it: they size to
    /// their content until a player resizes one, after which their choice is the height.
    /// The zoom never touches height, so this is its only writer.</summary>
    public Dictionary<string, double> WindowHeights { get; set; } = new();
    /// <summary>Opacity of the widget's background panel only — text stays fully opaque.</summary>
    public double BackgroundOpacity { get; set; } = 0.95;
    /// <summary>Re-lift EQBuddy's windows above later-created topmost overlays every few
    /// seconds (#91: Lossless Scaling's upscale surface buried the widget). Off = the old
    /// behavior, for screen-capture setups where the re-lift makes a visible double.</summary>
    public bool KeepAboveOverlays { get; set; } = true;

    /// <summary>macOS/Wine only (CrossOver &amp; friends): float the widget over the game
    /// even when it runs fullscreen, and stop a click on a widget from pulling the game
    /// out of the foreground (no Mac menu bar flash, widget stays on top). Needs the
    /// patched winemac.drv described in docs/CrossOver-macOS-overlay.md; on Windows —
    /// or on Wine without that patch — it does nothing. Off by default: opt-in for the
    /// Wine-on-Mac overlay setup, discoverable from the guide.</summary>
    public bool WineFloatOverFullscreen { get; set; }

    /// <summary>macOS/Wine only, immersive-only: keep the game visually fullscreen (Mac
    /// menu bar hidden) even when it loses focus. Off by default and usually best left
    /// off — the menu bar sits above normal windows, so keeping the game above it also
    /// keeps it above every other window: with this on you can't pull another app onto
    /// the game's monitor or alt-tab a window over it. Companion to WineFloatOverFullscreen;
    /// both need the patched winemac.drv. See docs/CrossOver-macOS-overlay.md.</summary>
    public bool WineKeepGameFullscreen { get; set; }

    /// <summary>Wine/CrossOver only: place every letter on a whole pixel. ON by default,
    /// and on Windows it is read but never acted on (see UI.Shared/TextRenderingPolicy).
    ///
    /// It is a real trade and that is why it is a switch rather than a constant. Wine
    /// truncates the fractional glyph advances WPF's default text mode relies on, so
    /// words break apart mid-letter — "bun dles", "an d th is" — in text whose font
    /// metrics are exactly right. Whole-pixel placement is the only mode Wine renders
    /// correctly, so ON is the right default. But it snaps BEFORE the widget's UI-scale
    /// transform, so above 100% the snapped text is resampled and goes soft (reported
    /// from CrossOver on macOS, 2026-08-21, once the fix landed). A player who runs the
    /// widget large may well prefer the sharper text and can turn it off here.</summary>
    public bool WineWholePixelText { get; set; } = true;
    /// <summary>Global hotkeys, opt-in only (#100): action key → gesture text
    /// ("Ctrl+Alt+M"). EMPTY BY DEFAULT and stays that way unless the player binds
    /// keys in Options — the 1.12–1.34 era's default binds ate other apps' shortcuts
    /// and the feature was removed; it returns only in this bind-it-yourself form.</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = new();

    /// <summary>The mez chip stack, off-switchable (Reddit ask, 2026-08-11): a class
    /// that never mezzes never wants the window popping mid-fight.</summary>
    public bool MezChipsEnabled { get; set; } = true;

    /// <summary>The slow alert (#94): a chip + optional voice when an attack-speed
    /// debuff lands on you — a silent 40% slow quietly doubles a fight.</summary>
    public bool SlowAlertEnabled { get; set; } = true;
    /// <summary>Speak the slow when it lands ("Slowed 40 percent") — the chip alone
    /// is easy to miss in exactly the busy fights slows matter most in.</summary>
    public bool SlowAlertSpoken { get; set; } = true;
    /// <summary>Alert only while raiding (#94's toggle) — detected from raid-channel
    /// chat, the log's only raid signal. Off = alert everywhere.</summary>
    public bool SlowAlertRaidOnly { get; set; }

    /// <summary>How the Tracked card orders its rules (#105, wizen): "manual" (the
    /// Options list order, rearrangeable there), "alpha", "total", or "recent".</summary>
    public string WatchSortMode { get; set; } = "manual";

    /// <summary>The recent-lines rule picker's chat filter (David's field note: General
    /// chat drowns the combat lines). Off by default — a "WTS" watch is a real rule.</summary>
    public bool RecentLinesHideChat { get; set; }

    /// <summary>Buff card display (David): false = every running buff with its full
    /// countdown; true = quiet until a buff is within <see cref="BuffWarnSeconds"/> of
    /// fading — the "tell me when it matters" mode.</summary>
    public bool BuffTimersExpiringOnly { get; set; }
    public double BuffWarnSeconds { get; set; } = 60;

    /// <summary>Buff sets (#120, Frankthetankk): the buffs a character never wants to
    /// camp without, keyed per character by the same "name_server" key the AA ledger
    /// uses. Player-built only — never auto-populated — and evaluated by
    /// BuffSetEvaluator into the Buffs card's missing line. Names stored as picked;
    /// rank suffixes fold at match time, so "Temperance" covers "Temperance II".
    /// STAGE-1 SHAPE, kept only so older settings files deserialize: Load migrates it
    /// into <see cref="BuffSetsByClass"/>'s "(any class)" bucket — never dropped —
    /// and empties it. Nothing writes here anymore.</summary>
    public Dictionary<string, List<string>> BuffSets { get; set; } = new();

    /// <summary>Buff sets stage 2 (#120, Frankthetankk — his design): stored PER CLASS
    /// underneath and assembled by the active class combination, so swapping Warrior
    /// for Rogue keeps the other classes' picks. Character "name_server" key → class →
    /// buff names; the "(any class)" bucket (<see cref="BuffSetStore.AnyClass"/>) is
    /// always part of the assembled set. Edited through <see cref="BuffSetStore"/>
    /// only — it owns the case-insensitive identity and the empty-entry pruning.</summary>
    public Dictionary<string, Dictionary<string, List<string>>> BuffSetsByClass { get; set; } = new();

    /// <summary>Stage 3 (#120, Frankthetankk): new-buff-unlock suggestions the player
    /// ✕-dismissed — character "name_server" → rank-folded base spell names, edited
    /// through <see cref="BuffSuggestions"/>. Dismissed = never asked again for that
    /// character; accepting needs no memory here (the spell joins a bucket and is
    /// covered from then on).</summary>
    public Dictionary<string, List<string>> BuffSuggestionDismissed { get; set; } = new();

    /// <summary>The Options tab last used — iterating on watch rules shouldn't cost a
    /// click per visit. "look" / "alerts" / "watch" / "cards" / "behavior".</summary>
    public string OptionsTab { get; set; } = "look";

    /// <summary>#112 (Frankthetankk): show EQBuddy's own CPU/memory in the title bar.
    /// Off by default — diagnostic info, not furniture.</summary>
    public bool ShowPerfStats { get; set; }

    /// <summary>Fight-timeline window placement; 0 width = never opened, defaults apply.</summary>
    public double TimelineLeft { get; set; }
    public double TimelineTop { get; set; }
    public double TimelineWidth { get; set; }
    public double TimelineHeight { get; set; }
    /// <summary>The Progress card's full AA ledger, folded by default (same Reddit
    /// report): session-new AAs show always; the complete list is a click away.</summary>
    public bool ShowAllAAs { get; set; }

    /// <summary>The Progress card's next-milestone AA preview, folded by default: the
    /// label always names the level and count; the rows are a click away.</summary>
    public bool ShowNextUnlocks { get; set; }

    /// <summary>The Progress card's skill-up list (David, 2026-08-28). **Defaults to TRUE,
    /// unlike its two neighbours above**, and the difference is deliberate: those two were
    /// born folded, while skill-ups has always drawn its rows outright. Shipping this
    /// `false` would hide a list every existing profile can see today — the #227/#228 class
    /// of change, and #240/#250/#251 are three players in one week saying they cannot find
    /// something a fold moved. A new fold may take something AWAY from nobody.
    ///
    /// It is also a RESTORATION rather than a new idea: the retired Progress breakout gave
    /// ding, session AAs and skill-ups their own open/closed state, and folding that float
    /// into the Progress window (1.99.11) dropped all three. `BreakoutWindow`'s
    /// `_skillUpsOpen`/`_dingOpen`/`_sessionAasOpen` survived as write-only fossils and are
    /// deleted with this change — trap 43's polarity, and the fossil is what proved the
    /// capability had existed.</summary>
    public bool ShowSkillUps { get; set; } = true;

    /// <summary>Whether the Experience surface's Level-ups list is unfolded (#240,
    /// joeymavity). **Default FOLDED**, unlike <see cref="ShowSkillUps"/> beside it: a
    /// veteran's list is every ding EQBuddy has ever seen, and the theme body's floor is
    /// 320 units — so the folded label carries the count and the last ding's date and the
    /// rows come out on a click. The fold label is this setting's only writer, which is
    /// the reader-and-writer pair trap 20 exists to check for.</summary>
    public bool ShowLevelUps { get; set; }

    /// <summary>Chip-stack growth direction (#95): anchored at the bottom edge, new
    /// chips push the stack upward — so boss timers can sit above mez timers with
    /// each growing away from the other.</summary>
    public bool SpawnChipsGrowUp { get; set; }
    public bool MezChipsGrowUp { get; set; }
    /// <summary>Section-list height chosen by dragging the widget's bottom edge, in
    /// pre-scale units so it survives UiScale changes (Reddit ask, 2026-08-09: grow the
    /// window without growing the text). NaN = automatic, fit the monitor.</summary>
    public double ContentHeight { get; set; } = double.NaN;
    /// <summary>Empty finished-session logs automatically. Off = logs grow forever
    /// (for players who upload their logs elsewhere).</summary>
    public bool TruncateLogs { get; set; } = true;
    /// <summary>Copy a log's content to Logs\archive\eqlog_name_server_STAMP.txt before
    /// the janitor empties it (discussion #52, joeymavity), and split rather than
    /// continue the log on a manual reset.
    ///
    /// **On by default since 1.84.0** (discussion #146, wizen). It shipped off, which
    /// meant EQBuddy's out-of-the-box behaviour was to destroy a file the player never
    /// asked it to destroy — and as wizen put it, these are text files. Keeping a dated
    /// copy is the answer that costs a few megabytes; wanting the space back is the
    /// preference worth making people opt into, not the other way round.</summary>
    public bool ArchiveLogs { get; set; } = true;
    /// <summary>Whether the one-time "archiving is on now" pass has run. Existing
    /// profiles carry an explicit <c>false</c> from when that was the default, so a
    /// changed default alone would never reach them — and they are exactly the players
    /// whose logs are being emptied without a copy. A flag rather than inferring it,
    /// so someone who turns archiving back off keeps it off.</summary>
    public bool ArchiveDefaultMigrated { get; set; }
    /// <summary>User-defined tracked-loot rules (TRACK-018: persisted).</summary>
    public List<TrackedRule> TrackedRules { get; set; } = [];
    /// <summary>Highest version of the built-in default watch rules already applied.
    /// Bumping <see cref="CurrentDefaultRulesVersion"/> hands new defaults to existing
    /// installs exactly once, and never re-adds a rule the user deleted on purpose.</summary>
    public int DefaultRulesVersion { get; set; }
    /// <summary>Options window width, dragged by its right edge. Wide enough by default
    /// that the watch-rule row (kind + name + spell class + match text + toggles) fits
    /// without clipping.</summary>
    public double OptionsWidth { get; set; } = 420;
    /// <summary>Default rolling window for "recent" rates, in minutes (5/15/30).</summary>
    public int RecentWindowMinutes { get; set; } = 15;
    /// <summary>Alert sound: a built-in name (Ding, Notify, Chimes, Chord, Tada,
    /// Exclamation, Alarm) or the full path of a custom sound file — any format the OS
    /// can play, which is more than the picker used to offer (#197).</summary>
    public string AlertSound { get; set; } = "Ding";
    /// <summary>Alert playback volume, 0..1. Defaults to FULL — WPF's MediaPlayer
    /// default is 0.5 and nothing ever set it, so alerts played at half loudness
    /// for everyone (Reddit report: "very quiet, needs a booster").</summary>
    public double AlertVolume { get; set; } = 1.0;
    /// <summary>Spoken-alert voice: an installed SAPI voice's description ("Microsoft Zira
    /// Desktop"), or "" for the system default — the only behavior before the picker
    /// existed. A voice that's gone missing (settings copied between machines) falls back
    /// to the default at speak time rather than silencing alerts. Windows-only effect;
    /// macOS `say` and the Linux no-op ignore it.</summary>
    public string SpeechVoice { get; set; } = "";
    /// <summary>Spoken-alert rate in SAPI units. SAPI accepts -10..10 but the app clamps
    /// to ±5 (UI.Shared SpokenAlerts.MinRate/MaxRate — past that speech stops being
    /// speech); 0 = the voice's normal pace, the pre-slider behavior.</summary>
    public int SpeechRate { get; set; }
    /// <summary>Spoken-alert volume 0..100, SAPI's own scale. Separate from
    /// <see cref="AlertVolume"/> on purpose: that slider drives only the MediaPlayer that
    /// plays sound files — SAPI never saw it, so one slider claiming both would be a lie
    /// in whichever direction it didn't reach.</summary>
    public int SpeechVolume { get; set; } = 100;
    /// <summary>Position of the floating alert tile; NaN = above the widget.</summary>
    public double AlertLeft { get; set; } = double.NaN;
    public double AlertTop { get; set; } = double.NaN;
    /// <summary>Master switch for watch chips in the mini dashboard. Which rules appear is
    /// then per-rule (<see cref="TrackedRule.Pinned"/>): showing every enabled rule was
    /// all-or-nothing, and a mini bar with eight chips on it isn't a mini bar.</summary>
    public bool PinWatchChips { get; set; }
    /// <summary>Whether the one-time "pin everything you were already seeing" pass has run.
    /// A flag rather than inferring it from "nothing is pinned", so deliberately unpinning
    /// every rule isn't undone at the next launch.</summary>
    public bool WatchPinsMigrated { get; set; }
    /// <summary>Has the one-time <see cref="MigrateWindowHeights"/> clear run? See there
    /// for why every stored window height written before 2026-08-25 is discarded.</summary>
    public bool WindowHeightsReset { get; set; }
    /// <summary>Whether the watch-rule examples panel in Options is expanded. Remembered so
    /// someone still learning the feature doesn't have to reopen it every time, and someone
    /// who doesn't need it never sees it again.</summary>
    public bool ShowWatchGuide { get; set; }
    /// <summary>Which of the Combat/Healing subsections are expanded. Separate per card and
    /// per section, because the reason to collapse one isn't the reason to collapse another:
    /// a melee player may want the fight breakdown open and the session one shut, and a
    /// healer the reverse. Default open — a new subsection nobody can see is a wasted one.</summary>
    public bool ShowCombatFight { get; set; } = true;
    public bool ShowCombatSession { get; set; } = true;
    /// <summary>Pet abilities breakdown expanded on the Combat card. Default collapsed
    /// (discussion #28): the pet's overall damage is already a row in the main list,
    /// and a pet class fighting all session got a wall of ability rows for free.</summary>
    public bool ShowPetAbilities { get; set; }
    public bool ShowHealFight { get; set; } = true;
    public bool ShowHealSession { get; set; } = true;
    /// <summary>Show the quick tour at every launch. Turned off by the tutorial's
    /// "Never show again" button or the Options checkbox. While on, the startup
    /// janitor defers log truncation — the tour's first page is its consent question.</summary>
    public bool ShowTutorial { get; set; } = true;
    /// <summary>Overlay card order (section keys); missing keys append in default order.</summary>
    public List<string> SectionOrder { get; set; } = [];
    /// <summary>Hidden overlay cards (still collect data — OVERLAY acceptance).</summary>
    public List<string> HiddenSections { get; set; } = [];
    // Global hotkeys were REMOVED 2026-08-06 (Reddit report: RegisterHotKey is
    // system-wide, so EQBuddy ate Ctrl+Shift+T — reopen browser tab — from every app on
    // the machine). Old settings.json files still carrying Hotkey* keys deserialize fine;
    // unknown properties are ignored and dropped on the next save.
    /// <summary>Persistent Plane of Sky quest turn-in checklist shown in the overlay.</summary>
    public List<SkyQuestChecklistItem> SkyQuestChecklist { get; set; } = [];
    /// <summary>The class tab last selected in the Sky Quest card. Quest item names
    /// repeat across classes (five classes each need a Wind Rune Azia), so loot
    /// auto-check only ticks boxes for this class; empty = no tab picked yet, first
    /// unacquired match wins.</summary>
    public string SkyQuestClass { get; set; } = "";
    /// <summary>Sky quest rewards marked turned-in, as "ClassName|Reward" keys
    /// (discussion #73, chrstahl). Manual only: the log shows nothing reliable when
    /// items change hands at an NPC, so the player is the source of truth — including
    /// for quests finished before this feature existed. Marking one complete also
    /// checks its items (they were acquired and then handed over).</summary>
    public List<string> SkyQuestCompleted { get; set; } = [];
    /// <summary>Imported equipment shopping list from EQ Legends Tools, shown as a
    /// lightweight in-game checklist. Manual checkboxes: imports replace the list,
    /// toggles persist until the next import or clear.</summary>
    public List<GearChecklistItem> GearChecklist { get; set; } = [];
    public string GearChecklistName { get; set; } = "";
    /// <summary>Gear card grouped by farm zone (the "where to go" pivot) instead of
    /// by slot. Persisted like the Epics classic-only lens — a view choice survives
    /// a restart.</summary>
    public bool GearGroupByZone { get; set; }
    /// <summary>The Inventory tab's pivot: false ranks everything wearable within each
    /// slot (what to swap, what to vendor — the old Gear Locker), true lists where each
    /// item physically is (the old Inventory window). One tab, two lenses, because both
    /// read the same dump (David, 2026-08-20). By-slot is the default: "what should I be
    /// wearing" is the actionable question and the lookup is the occasional one.</summary>
    public bool InventoryByContainer { get; set; }
    /// <summary>Path|timestamp of the last inventory dump the gear auto-done pass
    /// consumed. Persisted so a box the player deliberately unchecked is not
    /// re-fought on restart by the SAME dump; a new dump re-opens the question.</summary>
    public string GearInventoryAppliedStamp { get; set; } = "";
    /// <summary>Persistent Epic 1.0 checklist shown in the overlay. Seeded from the
    /// shipped quest catalog; manual checkboxes for now, with room for log/inventory
    /// auto-checking later.</summary>
    public List<EpicQuestChecklistItem> EpicQuestChecklist { get; set; } = [];
    public string EpicQuestClass { get; set; } = "";
    public List<string> EpicQuestCompleted { get; set; } = [];
    /// <summary>Per-class snapshot of which epic rows were already acquired when the
    /// "Epic complete" master check bulk-flipped the rest (#138, aodgizmo): unchecking
    /// the master restores this instead of leaving every row checked. Persisted so the
    /// undo survives a restart; a class completed before the snapshot existed has no
    /// key here and unchecking falls back to clearing just the completed flag.</summary>
    public Dictionary<string, List<string>> EpicQuestPreCompleteAcquired { get; set; } = [];
    public bool EpicQuestClassicOnly { get; set; }

    /// <summary>How a Plane of Sky step that can be found on SEVERAL islands is placed
    /// (David, 2026-08-23, asked as its own question — his answer was to let the player
    /// choose rather than pick one).
    ///
    /// <c>false</c> (default): it appears once, under "Several islands", after the numbered
    /// groups. One step, one row, one tick — and a numbered island list that is literally
    /// true about what is on that island.
    ///
    /// <c>true</c>: it appears under every island it names, so "what can I do on Island 4
    /// today" is answered completely. The same step then renders more than once; progress
    /// counts distinct steps regardless (<see cref="QuestChecklistGroup.Total"/>).
    ///
    /// Defaulted to the conservative one because it is the shape the ask described — *"where
    /// we know a step is on a specific island"* — and a step on three islands is not on a
    /// specific one.</summary>
    public bool SkyStepsUnderEveryIsland { get; set; }
    /// <summary>Color theme key (see EQBuddy.UI.Shared.ThemeCatalog); defaults to the
    /// original parchment-and-brass look so existing installs don't change on upgrade.</summary>
    public string Theme { get; set; } = "ParchmentBrass";

    /// <summary>The click-through alignment grid (discussion #34). Persisted so a grid
    /// left on comes back after a restart — turning it off is the same one menu click
    /// that turned it on.</summary>
    public bool ShowGridOverlay { get; set; }
    /// <summary>Minor grid line spacing in pixels; strong lines land every fourth.</summary>
    public double GridSpacing { get; set; } = 32;

    /// <summary>The cursor-finder ring (issue #81 — "I often lose my tiny cursor").
    /// Same persistence contract as the grid: left on, it comes back at launch.</summary>
    public bool ShowCursorRing { get; set; }
    /// <summary>Folder of classic-format zone map files (Brewall packs and kin).
    /// Null = auto-detect the game's own maps folder beside Logs.</summary>
    public string? MapFolder { get; set; }
    /// <summary>Ring diameter in DIPs.</summary>
    public double CursorRingSize { get; set; } = 46;

    /// <summary>The three colors behind the "Custom" theme (#RRGGBB); the rest of its
    /// palette is derived in EQBuddy.UI.Shared.CustomTheme. Null until first edited —
    /// the seed colors apply.</summary>
    public string? CustomThemeBg { get; set; }
    public string? CustomThemeText { get; set; }
    public string? CustomThemeAccent { get; set; }

    /// <summary>The newest version whose "What's new" notes this install has shown.
    /// Empty on installs from before the feature: those get just the current version's
    /// notes once (if the tutorial was already done — a fresh install skips notes
    /// entirely; onboarding belongs to the tutorial).</summary>
    public string LastSeenVersion { get; set; } = "";

    // ---- spawn timers (the Spawns window) ----
    /// <summary>Track named-mob spawn timers; the Spawns window opens whenever this is on.
    /// Default ON (David's call): the window is the feature's front door, and a default-off
    /// window behind a right-click menu is a feature nobody's family finds. Closing the
    /// window opts out, and that sticks.</summary>
    public bool TrackSpawns { get; set; } = true;
    public double SpawnLeft { get; set; } = double.NaN;
    public double SpawnTop { get; set; } = double.NaN;
    /// <summary>Follow the zone the log says the player is in; off = stay on the zone
    /// picked in the window's dropdown.</summary>
    public bool SpawnFollowZone { get; set; } = true;
    /// <summary>One-time repair (1.20.1): 1.20.0 could untick SpawnFollowZone on a
    /// selection event the user never made, so following silently died. The auto-untick
    /// is gone; this restores the default once for anyone the bug touched.</summary>
    public bool SpawnFollowRepaired { get; set; }
    /// <summary>Last manually-picked zone, for when SpawnFollowZone is off.</summary>
    public string SpawnZone { get; set; } = "";
    /// <summary>UNUSED since 1.23.0 (kept so older settings.json round-trips): spawn
    /// "Default" now follows <see cref="AlertSound"/>, the same default watch rules use —
    /// a second spawn-specific default made "Default" mean silence, which read as broken.</summary>
    public string SpawnSound { get; set; } = "Off";
    /// <summary>Position of the spawn-chicklet stack; NaN = a default spot near the
    /// top-left, clear of the widget's home edge.</summary>
    public double SpawnChipsLeft { get; set; } = double.NaN;
    public double SpawnChipsTop { get; set; } = double.NaN;
    /// <summary>Bottom edge of the spawn-chip stack at last close. Grow-up stacks
    /// anchor their BOTTOM, and the top edge depends on chip count at close — so the
    /// bottom is what restores when growing upward (#122). NaN = never saved.</summary>
    public double SpawnChipsBottom { get; set; } = double.NaN;

    /// <summary>Position of the mez-chip stack — its own window, deliberately separate
    /// from the spawn chips (mez chips are combat-urgent, spawn chips are ambient).</summary>
    public double MezChipsLeft { get; set; } = double.NaN;
    public double MezChipsTop { get; set; } = double.NaN;
    /// <summary>See <see cref="SpawnChipsBottom"/> — same rule, mez stack.</summary>
    public double MezChipsBottom { get; set; } = double.NaN;

    /// <summary>Target-drops block in the Loot card (wiki drops for the creature being
    /// fought). Default on; the toggle exists for lean-card people.</summary>
    public bool ShowTargetDrops { get; set; } = true;

    /// <summary>Loot list order: "count" (biggest stacks first, the original behavior) or
    /// "name" (alphabetical — Klona11's ask, discussion #43).</summary>
    public string LootSort { get; set; } = "count";

    /// <summary>Which slice of the loot card to show: "all", "looted" (corpse drops only),
    /// or "other" (everything else acquired — foraged, crafted, merged, parcel). Shared by
    /// the Loot card and its breakout. Legacy "made" is read as "other".</summary>
    public string LootView { get; set; } = "all";

    /// <summary>Player-supplied hp-per-tick for the regen healing estimate (0 = use the
    /// wiki base value). The log can't see instrument resonance or spell ranks; the
    /// player's own health bar can — their number wins (David, 2026-08-06).</summary>
    public int RegenPerTickOverride { get; set; }

    /// <summary>Hide the widget (and its satellite windows) while the game is running but
    /// NOT the foreground app — alt-tabbing to a browser shouldn't leave the widget over
    /// its buttons (sicliffe-cloud, discussion #41). Off by default; when the game isn't
    /// running at all the widget always shows (people configure it outside the game).</summary>
    public bool HideWhenGameUnfocused { get; set; }
    /// <summary>Hide the widget (and every satellite) while EverQuest Legends isn't
    /// RUNNING at all (#114) — the complement of <see cref="HideWhenGameUnfocused"/>,
    /// which deliberately keeps the widget visible in that case. Both off by default;
    /// they compose. EQBuddy's own windows having focus always overrides the hide.</summary>
    public bool HideWhenGameNotRunning { get; set; }
    /// <summary>Keep EQBuddy out of the Alt+Tab switcher (Hateborne, 2026-08-25). Off by
    /// default, and Windows-only — Alt+Tab is a Windows concept, so the box says so
    /// rather than persisting a choice that does nothing (the rule
    /// <see cref="UI.Shared.FocusHide.UnavailableNote"/> already sets one row above).
    ///
    /// **It takes the taskbar button with it, and that is not separable**: WS_EX_TOOLWINDOW
    /// is one flag with both effects. The tray icon is then the only way back to a hidden
    /// widget, so the Options row names it — a setting that can strand a player without
    /// saying so is worse than no setting.</summary>
    public bool HideFromAltTab { get; set; }

    // Breakout stat windows (BREAKOUT-*): one position + Fight/Session scope per kind.
    // They open while the widget is minimized with the matching star set.

    /// <summary>Breakout kinds the player ✕-closed for good ("Damage", "Loot", …): the
    /// star keeps its mini-pill chip, the window stays away until re-enabled in Options
    /// (Frankthetankk, discussion #45 — ✕-until-next-minimize made the window a
    /// whack-a-mole).</summary>
    public List<string> DisabledBreakouts { get; set; } = [];

    /// <summary>Double-click a mini-pill chip (dps, hps, pet, loot, watch) to open or
    /// close its breakout window on demand. Opt-in, off by default. While it's on, a
    /// breakout closed with its ✕ stays silent — no "hidden, re-enable in Options" alert —
    /// because a double-click brings it right back (asked for: pop the Loot or DPS window
    /// up only when you want it, without the nag).</summary>
    public bool DoubleClickChipsToggleBreakouts { get; set; }

    public double BreakoutDamageLeft { get; set; } = double.NaN;
    public double BreakoutDamageTop { get; set; } = double.NaN;
    public string BreakoutDamageScope { get; set; } = "fight";
    public double BreakoutHealingLeft { get; set; } = double.NaN;
    public double BreakoutHealingTop { get; set; } = double.NaN;
    public string BreakoutHealingScope { get; set; } = "fight";
    public double BreakoutPetLeft { get; set; } = double.NaN;
    public double BreakoutPetTop { get; set; } = double.NaN;
    public string BreakoutPetScope { get; set; } = "fight";
    /// <summary>The Watch breakout (CrispyPigeon131, discussion #44): pinned watch rules
    /// as a floating window while minimized. No scope — rules are session counters.</summary>
    public double BreakoutWatchLeft { get; set; } = double.NaN;
    public double BreakoutWatchTop { get; set; } = double.NaN;
    /// <summary>The Loot breakout (David's live report 2026-08-06): target drops while
    /// fighting, session loot between fights, opened by the 🎒 star while minimized.</summary>
    public double BreakoutLootLeft { get; set; } = double.NaN;
    public double BreakoutLootTop { get; set; } = double.NaN;
    // The Buff Set breakout (#120 stage 2) has no Fight/Session scope — its axis is
    // the class combination, shown in its own header.
    public double BreakoutBuffsLeft { get; set; } = double.NaN;
    public double BreakoutBuffsTop { get; set; } = double.NaN;
    // BreakoutProgressLeft/Top/Width/Height were deleted 2026-08-25 with the Progress
    // breakout itself (Bevel's fold): the xp chip opens the Progress WINDOW now. They were
    // ORPHANS for a few minutes — neither read nor written — and nothing would have caught
    // them: DeadSettingTests scans for settings READ but never written, so a setting with
    // no reader AND no writer is its blind spot. Removing the properties is safe for
    // existing profiles because AppSettings' JsonSerializerOptions leaves
    // UnmappedMemberHandling at its default, so the leftover keys are simply skipped.
    /// <summary>"target" (drops for the creature you're fighting or last /considered) or
    /// "session" (what you've looted).</summary>
    public string BreakoutLootScope { get; set; } = "target";
    // Per-breakout manual size (NaN = auto-size to content). Set the moment the resize
    // grip is dragged; cleared by double-clicking it (David: let me resize the loot
    // window and scroll, 2026-08-06).
    public double BreakoutDamageWidth { get; set; } = double.NaN;
    public double BreakoutDamageHeight { get; set; } = double.NaN;
    public double BreakoutHealingWidth { get; set; } = double.NaN;
    public double BreakoutHealingHeight { get; set; } = double.NaN;
    public double BreakoutPetWidth { get; set; } = double.NaN;
    public double BreakoutPetHeight { get; set; } = double.NaN;
    public double BreakoutWatchWidth { get; set; } = double.NaN;
    public double BreakoutWatchHeight { get; set; } = double.NaN;
    public double BreakoutLootWidth { get; set; } = double.NaN;
    public double BreakoutLootHeight { get; set; } = double.NaN;
    public double BreakoutBuffsWidth { get; set; } = double.NaN;
    public double BreakoutBuffsHeight { get; set; } = double.NaN;
    // Per-breakout row sort for the stat kinds: "total" | "hits" | "avg" | "rate".
    public string BreakoutDamageSort { get; set; } = "total";
    public string BreakoutHealingSort { get; set; } = "total";
    public string BreakoutPetSort { get; set; } = "total";

    // ---- EQBuddy Mobile (the LAN companion server; see SECURITY.md) ----
    /// <summary>The phone companion listener. OFF by default and stays off until the
    /// player flips it — a network listener is opt-in, never a surprise.</summary>
    public bool CompanionEnabled { get; set; }
    /// <summary>TCP port the companion listens on. One fixed default (so firewall
    /// rules and muscle memory stick) but editable for the rare collision.</summary>
    public int CompanionPort { get; set; } = 47859;
    /// <summary>The pairing token, minted (crypto-random) the first time the feature
    /// is enabled. Regenerating revokes every previously paired device.</summary>
    public string? CompanionToken { get; set; }
    /// <summary>Which of this PC's LAN addresses the pairing QR and URL print, when the
    /// machine has more than one (#264, brhanson2-cyber: "how do I force it to give me a
    /// link using the wifi ip"). Null or empty means "whatever
    /// <see cref="LanAddressRank"/> ranks first", which is the default and prefers Wi-Fi
    /// over ethernet when both are real. Written by the pairing window's address picker;
    /// a value naming an address this machine no longer has is ignored in favour of the
    /// ranked first, so a pin can never leave the QR pointing at nothing.</summary>
    public string? CompanionPairingAddress { get; set; }
    /// <summary>Surfaces the owner does NOT want leaving the PC (the desktop gate in
    /// the pairing window). Hidden-list idiom like <see cref="HiddenSections"/>:
    /// empty = everything offered, which is the default.</summary>
    public List<string> CompanionHiddenSurfaces { get; set; } = [];
    /// <summary>The Travel surface's picked destination (World PR 4) — persisted so a
    /// reconnecting device sees the same route rather than an empty picker. Session-only
    /// in spirit (nobody needs yesterday's destination), but there is no cheaper place to
    /// hold "what did the phone last ask for" than the settings file every other piece of
    /// companion state already lives in.</summary>
    public string? CompanionTravelDestination { get; set; }

    private static string FilePath => AppPaths.File("settings.json");

    // NaN is a legitimate value here ("not placed yet" window positions), and the
    // default serializer refuses it — which made Save() throw and silently drop
    // every settings change on profiles with an unplaced window.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Bump when adding a built-in rule; see <see cref="DefaultRulesVersion"/>.</summary>
    private const int CurrentDefaultRulesVersion = 1;

    public static AppSettings Load(bool persistMigrations = true)
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new()
                : new AppSettings();
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex); // corrupted settings — start fresh, but say so
            settings = new AppSettings();
        }
        // Whether this profile had a settings.json at all. Only one migration cares, and
        // it cares a lot: see MigrateMotesCard.
        var hadFile = settings._fileStamp is not null;
        settings._fileStamp = StampOf(FilePath);
        // Non-short-circuiting on purpose: rules saved before ids existed get theirs
        // assigned at construction, and persisting them NOW is what makes the id stable
        // across restarts rather than re-rolled every launch until some unrelated edit
        // happens to save settings.
        var changed = settings.ApplyDefaultRules();
        // Fold the two old quest cards onto one BEFORE the gear default runs — gear
        // anchors itself to the quests slot.
        changed |= settings.MigrateQuestSections();
        // And the five Progress-theme cards onto one, the same way and for the same
        // reason (docs/Themes.md step 5). After the gear default, because gear anchors
        // itself to the quests slot and must not be pushed around by a fold that happens
        // to land nearby; before nothing else, because nothing else reads SectionOrder.
        changed |= settings.ApplyDefaultGearSection();
        changed |= settings.MigrateProgressSections();
        // The window exists now, so the fold runs (docs/Themes.md step 5). It was written
        // and held back deliberately for two commits — a migration that rearranges a
        // player's widget before the surface it folds into exists buys them nothing.
        changed |= settings.MigrateLootSections();
        // The Motes card came back on 2026-08-21 and must arrive HIDDEN, or every player
        // who never asked for it gets a taller widget on update. Runs after the folds
        // because it reads what they left behind.
        changed |= settings.MigrateMotesCard(hadFile);
        changed |= settings.MigrateSkyRewardRenames();
        changed |= settings.ApplyDefaultSkyQuestChecklist();
        changed |= settings.ApplyDefaultEpicQuestChecklist();
        changed |= settings.MigrateBuffSetsToClassBuckets();
        changed |= settings.MigrateArchiveDefault();
        changed |= settings.MigrateWindowHeights();
        // A READ that writes, and the reason is good: an id assigned at construction is
        // only stable across restarts if it is persisted now. But it means Load() is a
        // writer, and a caller that has not taken the single-instance lock must be able to
        // say no — see persistMigrations. Found by Fable 5 in the v1.99.3 release review,
        // against an executor claim that Load "never saves". It does.
        if (persistMigrations && (changed | settings.TrackedRules.Any(r => r.IdWasGenerated)))
            settings.Save();
        return settings;
    }

    /// <summary>
    /// Adds built-in watch rules that ship enabled. A charm or mez breaking is the one
    /// event where finding out late is expensive — and you are looking at the game, not
    /// the widget — so both the banner and the sound are on out of the box rather than
    /// waiting for the player to discover watch rules and configure one.
    ///
    /// Everything about it stays editable: 🔔 and 🔊 toggle per rule, the class filter and
    /// name are editable, the whole rule can be deleted (and stays deleted), and the sound
    /// itself is the shared <see cref="AlertSound"/> choice.
    ///
    /// Runs once per version — deleting the rule makes it stay deleted.
    /// Returns true when something changed and the settings need saving.
    /// </summary>
    /// <summary>Stage-1 → stage-2 buff-set migration (#120): flat per-character sets
    /// move to the "(any class)" bucket the assembled set always includes, so nothing
    /// anyone configured is lost or demoted. Idempotent — see BuffSetStore.Migrate.</summary>
    public bool MigrateBuffSetsToClassBuckets() => BuffSetStore.Migrate(BuffSets, BuffSetsByClass);

    /// <summary>Turn log archiving on once for profiles that predate it becoming the
    /// default (discussion #146). Runs exactly once and records that it did, so this is
    /// a changed default reaching existing players — not a preference being overruled
    /// every launch.</summary>
    public bool MigrateArchiveDefault()
    {
        if (ArchiveDefaultMigrated) return false;
        ArchiveDefaultMigrated = true;
        if (ArchiveLogs) return true;    // already on; just record that we've been here
        ArchiveLogs = true;
        return true;
    }

    public bool ApplyDefaultRules()
    {
        if (DefaultRulesVersion >= CurrentDefaultRulesVersion) return false;
        if (DefaultRulesVersion < 1 &&
            !TrackedRules.Any(r => r.Kind == WatchKind.SpellFade &&
                                   r.SpellFilter == SpellFilter.AnyCrowdControl))
        {
            TrackedRules.Add(new TrackedRule
            {
                Name = "CC broke",
                Kind = WatchKind.SpellFade,
                SpellFilter = SpellFilter.AnyCrowdControl,
                AlertBanner = true,
                AlertSound = true,
            });
        }
        DefaultRulesVersion = CurrentDefaultRulesVersion;
        return true;
    }

    /// <summary>
    /// Discard every stored window height, once.
    ///
    /// **Not one of them was a choice.** `WindowZoom.AllowResize` persisted `ActualHeight`
    /// on close unconditionally, and until 2026-08-25 no frameless pop-out had a border a
    /// player could grab — so every entry in `WindowHeights` records whatever the window
    /// happened to measure when it was closed. Hateborne's profile carried four: `drops`
    /// 1224 (a window filling the screen), `gearloot` 200 (the minimum floor, sampled from
    /// a frame with nothing in it yet), `quests` 425, `progress` 493.
    ///
    /// They cannot be repaired, only distinguished from real ones by WHEN they were
    /// written — so they go, once. From here a height is only stored when the player has
    /// actually dragged the border, which the app can now tell exactly.
    ///
    /// A player who had dragged a window before today loses that one size and sets it
    /// again in a second. A player who had not — everyone, since it was impossible — gets
    /// their pop-outs back at a sensible height instead of whatever an empty first frame
    /// measured.
    /// </summary>
    public bool MigrateWindowHeights()
    {
        if (WindowHeightsReset) return false;
        WindowHeightsReset = true;
        if (WindowHeights.Count == 0) return true;
        WindowHeights.Clear();
        return true;
    }

    /// <summary>Reward names corrected in the catalog, so a turn-in already recorded
    /// against the old name is not orphaned.
    ///
    /// <see cref="SkyQuestCompleted"/> is keyed by class + REWARD NAME, so renaming a
    /// reward silently un-completes it: the item ticks survive (they key on stable ids)
    /// but the "I handed this in" does not, and the player has no way to tell what
    /// happened. Any future rename belongs in this list rather than in the catalog alone.
    ///
    /// The first entry is #206 (bjstrange), whose achievements export named "Shimmering
    /// Bracer of Protection" while our catalog carried "Scintillating". eqlwiki serves the
    /// SHIMMERING page and redirects Scintillating to it — trap 3, an alias recorded as
    /// the title — and the game's own export agrees with the wiki, so the catalog was
    /// uniquely wrong, which is the case CLAUDE.md says costs the most trust.</summary>
    public bool MigrateSkyRewardRenames()
    {
        var renames = new (string Class, string From, string To)[]
        {
            ("Rogue", "Scintillating Bracer of Protection", "Shimmering Bracer of Protection"),
            // #216 (Snagglefern): the wiki page is Staff_of_The_Magister with a capital
            // T, and eqlwiki does NOT redirect the lower-case form — it 404s (verified
            // both spellings, 200 vs 404). So the link off three Magician Sky rows was
            // dead. Our own harvested QuestCatalog.json already had the capital; only
            // SkyQuestDefaults disagreed, which made it uniquely wrong.
            //
            // A case-only rename should no longer be able to strand a turn-in — the two
            // readers that used a case-SENSITIVE List.Contains were fixed with it — but
            // this entry stays anyway, because it also normalises what is already
            // written in settings.json rather than relying on every future reader
            // remembering the comparer.
            ("Magician", "Staff of the Magister", "Staff of The Magister"),
        };

        var changed = false;
        foreach (var (cls, from, to) in renames)
        {
            var oldKey = QuestChecklistLayout.RewardKey(cls, from);
            var newKey = QuestChecklistLayout.RewardKey(cls, to);
            var at = SkyQuestCompleted.FindIndex(k =>
                k.Equals(oldKey, StringComparison.OrdinalIgnoreCase));
            if (at < 0) continue;
            SkyQuestCompleted.RemoveAt(at);
            if (!SkyQuestCompleted.Contains(newKey, StringComparer.OrdinalIgnoreCase))
                SkyQuestCompleted.Add(newKey);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The "Sky Quest" and "Epics" cards became ONE "Quests" card (David, 2026-08-16).
    /// Both used to carry a full tabbed checklist on the widget — a review surface, not
    /// a glance one — and the Quest Tracker window now owns that on its own three tabs,
    /// which the new card opens.
    ///
    /// The surviving key takes the EARLIER of the two old slots, so the card appears
    /// where the player already looked for quests instead of arriving at the bottom of
    /// the list, where a new card reads as missing (the 1.66 lesson recorded in
    /// NormalizeSectionOrder). It is hidden only when BOTH old cards were hidden:
    /// keeping either one visible was a statement that quests belong on the widget.
    ///
    /// Idempotent — once neither old key is present there is nothing left to fold.
    /// </summary>
    public bool MigrateQuestSections()
    {
        var firstSlot = -1;
        for (var i = 0; i < SectionOrder.Count && firstSlot < 0; i++)
            if (SectionOrder[i] is "sky" or "epic") firstSlot = i;

        var hidSky = HiddenSections.Remove("sky");
        var hidEpic = HiddenSections.Remove("epic");
        var changed = hidSky || hidEpic;

        if (firstSlot >= 0)
        {
            SectionOrder.RemoveAll(k => k is "sky" or "epic");
            if (!SectionOrder.Contains("quests"))
                SectionOrder.Insert(Math.Min(firstSlot, SectionOrder.Count), "quests");
            changed = true;
        }
        if (hidSky && hidEpic && !HiddenSections.Contains("quests"))
            HiddenSections.Add("quests");
        return changed;
    }

    /// <summary>Fold the five Progress-theme cards into one, preserving position and
    /// hidden state — step 5 of docs/Themes.md's recipe, and the step the plan names as
    /// where silent data loss lives.
    ///
    /// Generalised from <see cref="MigrateQuestSections"/>, which did the same for
    /// sky+epic → quests; the card list and the surviving key come from
    /// <see cref="ProgressSurface"/> so they are not spelled twice.
    ///
    /// Two rules worth stating, both conservative on purpose:
    /// <list type="bullet">
    /// <item>The theme lands in the FIRST slot any of its cards occupied, so a player who
    /// dragged Money to the top still finds the theme at the top rather than appended to
    /// the bottom.</item>
    /// <item>It is hidden only if EVERY absorbed card was hidden. Showing a card that was
    /// hidden is one click to undo; hiding one the player wanted is invisible, and they
    /// would have to suspect the update to find it.</item>
    /// </list></summary>
    public bool MigrateProgressSections() => FoldThemeSections(
        ProgressSurface.AbsorbedCardKeys, ProgressSurface.ThemeCardKey);

    /// <summary>
    /// The Gear &amp; Loot fold: the Loot and Gear cards become one launcher (step 5 of
    /// docs/Themes.md's recipe). Same two conservative rules as
    /// <see cref="MigrateProgressSections"/>, and the same idempotence trap guarded the
    /// same way — the theme key is itself one of the absorbed keys, so without the stale
    /// check this would report a change on every load and force a settings SAVE each
    /// launch, and a save rewrites the whole file from the startup snapshot (trap 13).
    ///
    /// The two rules, restated because they are the ones that decide whether a fold loses
    /// something: the theme lands in the FIRST slot either card occupied, so a player who
    /// dragged Loot to the top still finds it at the top; and it is hidden only if BOTH
    /// cards were hidden, because showing a card someone hid is one click to undo while
    /// hiding one they wanted is invisible.
    /// </summary>
    public bool MigrateLootSections() => FoldThemeSections(
        LootSurface.AbsorbedCardKeys, LootSurface.ThemeCardKey);

    /// <summary>Whether the reinstated Motes card has been offered to this profile yet.
    /// A one-shot flag, not a preference: it exists so the hide below happens exactly once
    /// and a player who then SHOWS the card is never quietly re-hidden on the next launch.
    /// Without it the migration would fire on every load, which also forces a settings
    /// SAVE each launch — and a save rewrites the whole file from the startup snapshot
    /// (trap 13).</summary>
    public bool MotesCardOffered { get; set; }

    /// <summary>Whether the one-shot RESTORE pass below has run on this profile.
    ///
    /// A second flag rather than resetting the first, because the two answer different
    /// questions and a profile can need the second having already had the first: everyone
    /// who launched between 2026-08-21 and this fix took the blanket hide, and
    /// <see cref="MotesCardOffered"/> being true is exactly what stops them being looked at
    /// again.</summary>
    public bool MotesCardRestored { get; set; }

    /// <summary>The only surviving evidence that this player was watching motes before the
    /// Progress theme absorbed the card.
    ///
    /// It has to be an odd signal because the obvious one was DESTROYED: the 2026-08-19
    /// fold removes every absorbed key from <c>SectionOrder</c> AND from
    /// <c>HiddenSections</c> (see <c>FoldThemeSections</c>), so "did they have the Motes
    /// card showing" is a question no profile can answer any more. The mini-dashboard star
    /// survived, because nothing but a player's own click has ever written
    /// <see cref="MiniStats"/> — and it is an affirmative choice rather than a default, the
    /// shipped list being just kills and dps.
    ///
    /// **It under-restores on purpose.** A player who watched the card without ever
    /// starring the cell leaves no trace at all, and inventing one would mean showing the
    /// card to everybody — which is the taller-widget-on-update this whole migration exists
    /// to avoid. Restoring what can be PROVEN beats guessing in either direction.</summary>
    private bool WasWatchingMotes => MiniStats.Contains("motes");

    /// <summary>
    /// Motes is a top-level card again (David, 2026-08-21), and it starts HIDDEN.
    ///
    /// The Progress theme absorbed it on 2026-08-19 and two separate reports followed:
    /// #219 wanted the RATE back on the widget (fixed in 1.96.1 — it is on the Progress
    /// launcher line), and #228 plus Scribe's item wanted the card itself back, "behind a
    /// setting if needed". This is that setting, and it is the one the app already has:
    /// HiddenSections plus the eye in Options → Cards & windows. No bespoke toggle, no
    /// second mechanism for one piece of state.
    ///
    /// Hidden by default because the fold happened for a reason — the widget shares the
    /// monitor with the game, and a card nobody asked for is a row nobody asked for. The
    /// player who wants it ticks it once.
    ///
    /// **Existing profiles only get hidden ONCE.** The flag is what makes showing it
    /// stick; see <see cref="MotesCardOffered"/>.
    /// </summary>
    public bool MigrateMotesCard(bool hadFile)
    {
        // THE RESTORE (#228, Helm's ruling 2026-08-22: *"Default-off still hides existing
        // motes... The fix is a restore change, not a reply."*). Runs even on a profile
        // that has already been offered the card, because those are precisely the profiles
        // that took the blanket hide — and it runs FIRST so a single launch both offers and
        // corrects rather than needing two.
        var ran = false;
        if (!MotesCardRestored)
        {
            MotesCardRestored = true;
            ran = true;
            // Only ever UN-hides, and only with evidence — it can never hide anything.
            //
            // **It does NOT respect a deliberate hide, and saying it did was wrong**
            // (Fable 5, v1.99.4 release review; the same false-safety-claim shape as the
            // `Load` one earlier the same day). `HiddenSections` carries no provenance:
            // the entry the blanket pass wrote and the entry Options writes when a player
            // unticks the eye are the same string in the same list. So a starred player
            // who found the card, switched it on, and switched it off again is un-hidden
            // once here, and there is no way at this layer to tell them apart.
            //
            // Left as is on purpose rather than given a "player touched it" flag: the
            // exposure is the one day between 1.99.0 and this, the cost is one toggle, and
            // a setting that exists to remember a single day is a setting forever.
            if (WasWatchingMotes) HiddenSections.Remove("motes");
        }
        if (!MotesCardOffered)
        {
            MotesCardOffered = true;
            ran = true;
            // The blanket hide skips anyone the restore just spoke for; otherwise this
            // line would take the card back off them in the same call.
            if (!WasWatchingMotes && !HiddenSections.Contains("motes"))
                HiddenSections.Add("motes");
        }
        // Both passes are ONE-SHOT, and the flag has to be written for that to be true —
        // so the answer is "something changed" whenever a pass ran, not only when it moved
        // a card. A restore that decided "no evidence" and did not persist saying so would
        // re-decide on every launch, and would then un-hide the card under a player who
        // stars motes next week. That is the "never quietly re-shown" rule with the switch
        // on the other side.
        //
        // Only the WRITE is conditional on hadFile: a profile with no settings.json yet has
        // nothing to preserve, and forcing a save here made every fresh Load() a file
        // writer — which is what made SettingsClobberTests flaky. The in-memory state is
        // already correct either way, and the flags persist with the next real save.
        return ran && hadFile;
    }

    /// <summary>The fold itself, shared by the themes. Extracted when the second one
    /// arrived: two copies of a settings migration is two chances to lose a card
    /// slot, and the Progress version had already been through one round of bug-fixing
    /// that a hand-copy would not have inherited.</summary>
    private bool FoldThemeSections(IReadOnlyList<string> absorbed, string theme)
    {
        var stale = absorbed.Where(k => !k.Equals(theme, StringComparison.OrdinalIgnoreCase))
            .Any(k => SectionOrder.Contains(k, StringComparer.OrdinalIgnoreCase)
                   || HiddenSections.Contains(k));
        if (!stale) return false;

        var firstSlot = -1;
        for (var i = 0; i < SectionOrder.Count && firstSlot < 0; i++)
            if (absorbed.Contains(SectionOrder[i], StringComparer.OrdinalIgnoreCase)) firstSlot = i;

        // Count BEFORE removing: "were they all hidden" is a question about the old state.
        var present = absorbed.Count(k => SectionOrder.Contains(k, StringComparer.OrdinalIgnoreCase));
        var hidden = absorbed.Count(k => HiddenSections.Contains(k));
        var changed = false;
        foreach (var key in absorbed) changed |= HiddenSections.Remove(key);

        if (firstSlot >= 0)
        {
            SectionOrder.RemoveAll(k => absorbed.Contains(k, StringComparer.OrdinalIgnoreCase));
            if (!SectionOrder.Contains(theme))
                SectionOrder.Insert(Math.Min(firstSlot, SectionOrder.Count), theme);
            changed = true;
        }
        // Every card this theme owns was hidden, so the theme is too. `present > 0` keeps a
        // profile that never had these cards at all from acquiring a hidden one.
        if (present > 0 && hidden >= present && !HiddenSections.Contains(theme))
            HiddenSections.Add(theme);
        return changed;
    }

    public bool ApplyDefaultGearSection()
    {
        if (SectionOrder.Count == 0 || SectionOrder.Contains("gear")) return false;
        var quests = SectionOrder.IndexOf("quests");
        var motes = SectionOrder.IndexOf("motes");
        var anchor = quests >= 0 ? quests : motes;
        SectionOrder.Insert(anchor < 0 ? SectionOrder.Count : anchor + 1, "gear");
        return true;
    }

    public bool ApplyDefaultSkyQuestChecklist()
    {
        SkyQuestChecklist ??= [];
        var changed = false;
        foreach (var item in SkyQuestDefaults.Items)
        {
            var existing = SkyQuestChecklist.FirstOrDefault(i => string.Equals(i.Id, item.Id, StringComparison.Ordinal));
            if (existing is not null)
            {
                // Refresh quest metadata by Id so curated corrections reach installs
                // already carrying the row (#139's mask/mantle swap). Acquired and
                // AcquiredUnassigned are the player's record and are never touched —
                // a tick placed on the old text stays on the corrected row.
                // ClassName is refreshed with the rest, and it is the one that MATTERS
                // most: every surface groups and filters by it, so a row whose class does
                // not match the catalog is invisible in all of them — the tick survives in
                // settings.json and the player never sees it again. Found on 2026-08-18 by
                // seeding a checklist for a screenshot and watching the ticked rows vanish.
                if (existing.ClassName == item.ClassName &&
                    existing.Npc == item.Npc &&
                    existing.Reward == item.Reward &&
                    existing.QuestItem == item.QuestItem &&
                    existing.Source == item.Source)
                    continue;

                existing.ClassName = item.ClassName;
                existing.Npc = item.Npc;
                existing.Reward = item.Reward;
                existing.QuestItem = item.QuestItem;
                existing.Source = item.Source;
                changed = true;
                continue;
            }

            SkyQuestChecklist.Add(item.Clone());
            changed = true;
        }

        return changed;
    }

    public bool ApplyDefaultEpicQuestChecklist()
    {
        EpicQuestChecklist ??= [];
        var changed = false;
        foreach (var item in EpicQuestDefaults.Items())
        {
            var existing = EpicQuestChecklist.FirstOrDefault(i => string.Equals(i.Id, item.Id, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (existing.QuestName == item.QuestName &&
                    existing.Reward == item.Reward &&
                    existing.Section == item.Section &&
                    existing.QuestItem == item.QuestItem &&
                    existing.Qty == item.Qty &&
                    existing.Order == item.Order &&
                    existing.Source == item.Source &&
                    existing.AvailableInClassic == item.AvailableInClassic &&
                    existing.ItemNames.SequenceEqual(item.ItemNames, StringComparer.Ordinal))
                    continue;

                existing.QuestName = item.QuestName;
                existing.Reward = item.Reward;
                existing.Section = item.Section;
                existing.QuestItem = item.QuestItem;
                existing.Qty = item.Qty;
                existing.Order = item.Order;
                existing.Source = item.Source;
                existing.AvailableInClassic = item.AvailableInClassic;
                existing.ItemNames = [.. item.ItemNames];
                changed = true;
                continue;
            }

            EpicQuestChecklist.Add(item.Clone());
            changed = true;
        }

        return changed;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            WarnIfClobberingAnotherWriter();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
            _fileStamp = StampOf(FilePath);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex); // non-fatal, but visible
        }
    }

    // ---- "who else is writing this file?" (#169) ----
    //
    // A save writes the WHOLE object from a snapshot taken at load, so anything that
    // changed the file since then is reverted — every setting at once, with no error
    // and nothing on screen. That is the exact shape of "my tick-boxes won't stay
    // ticked", and until now it left no trace at all to distinguish from a bug in the
    // saving. It cannot be repaired here (this object has no idea which of its
    // properties the user meant to change), but it can stop being invisible.
    //
    // Not serialized: System.Text.Json only touches public members.

    private (DateTime WriteUtc, long Length)? _fileStamp;
    private bool _clobberLogged;

    private static (DateTime WriteUtc, long Length)? StampOf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Logs once per process when the file changed between our last read or
    /// write and this one — a second EQBuddy sharing the profile, or the file being
    /// hand-edited while EQBuddy runs.</summary>
    private void WarnIfClobberingAnotherWriter()
    {
        if (_clobberLogged || _fileStamp is not { } known) return;
        var current = StampOf(FilePath);
        if (current is null || current == known) return;
        _clobberLogged = true;
        CoreLog.Error(
            $"settings.json changed underneath this EQBuddy (was {known.Length} bytes at " +
            $"{known.WriteUtc:O}, now {current.Value.Length} bytes at {current.Value.WriteUtc:O}) " +
            "and is about to be overwritten with this copy's values. Another EQBuddy sharing " +
            "this profile, or the file edited by hand while EQBuddy was running, would both " +
            "look like this — and either one silently reverts settings changed elsewhere.");
    }
}

public sealed class SkyQuestChecklistItem
{
    public string Id { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Npc { get; set; } = "";
    public string Reward { get; set; } = "";
    public string QuestItem { get; set; } = "";
    public string Source { get; set; } = "";
    public bool Acquired { get; set; }
    /// <summary>True when the loot auto-tick PLACED this check itself because the
    /// item is wanted by several classes and none of them passed the class lens
    /// (#106, bjstrange's two-quest staff: "check one of them off, doesn't matter
    /// which, and let me decide"). Shown as a * so the player can move the tick;
    /// any manual toggle clears it — the player deciding IS the resolution.</summary>
    public bool AcquiredUnassigned { get; set; }

    public SkyQuestChecklistItem Clone() => new()
    {
        Id = Id,
        ClassName = ClassName,
        Npc = Npc,
        Reward = Reward,
        QuestItem = QuestItem,
        Source = Source,
        Acquired = Acquired,
        AcquiredUnassigned = AcquiredUnassigned,
    };
}

public sealed class GearChecklistItem
{
    public string Slot { get; set; } = "";
    /// <summary>True when this is a socketed exaltation rather than equipped gear.</summary>
    public bool IsExaltation { get; set; }
    public string Item { get; set; } = "";
    /// <summary>The effect granted by a socketed exaltation, when supplied by the export.</summary>
    public string ExaltationEffect { get; set; } = "";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Acquired { get; set; }
}

public sealed class EpicQuestChecklistItem
{
    public string Id { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string QuestName { get; set; } = "";
    public string Reward { get; set; } = "";
    public string Section { get; set; } = "";
    public string QuestItem { get; set; } = "";
    public int Qty { get; set; } = 1;
    public int Order { get; set; }
    public string Source { get; set; } = "";
    public bool AvailableInClassic { get; set; } = true;
    public bool Acquired { get; set; }
    /// <summary>The catalog turn-in items this prose step mentions — the loot auto-tick's
    /// match key (#121), resolved in EpicQuestDefaults from the class's epic quest items.
    /// Empty when no loot line can prove the step (hails, dialogue, kill-only steps) —
    /// those rows simply never auto-tick.</summary>
    public List<string> ItemNames { get; set; } = [];
    /// <summary>True when the loot auto-tick PLACED this check itself because the
    /// item is wanted by several classes' epics and none of them passed the class
    /// lens — same contract as SkyQuestChecklistItem.AcquiredUnassigned (#106).
    /// Shown as a * so the player can move the tick; any manual toggle clears it —
    /// the player deciding IS the resolution.</summary>
    public bool AcquiredUnassigned { get; set; }

    public EpicQuestChecklistItem Clone() => new()
    {
        Id = Id,
        ClassName = ClassName,
        QuestName = QuestName,
        Reward = Reward,
        Section = Section,
        QuestItem = QuestItem,
        Qty = Qty,
        Order = Order,
        Source = Source,
        AvailableInClassic = AvailableInClassic,
        Acquired = Acquired,
        ItemNames = [.. ItemNames],
        AcquiredUnassigned = AcquiredUnassigned,
    };
}
