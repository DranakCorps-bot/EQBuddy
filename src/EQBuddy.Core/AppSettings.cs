using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

public sealed class AppSettings
{
    public string? LogFolder { get; set; }
    /// <summary>Folder holding EQBuddySetup.exe for updates; null = auto-detect OneDrive.</summary>
    public string? UpdateFolder { get; set; }
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
    /// <summary>Quest Tracker era ceiling ("" = any): quests after this era are hidden
    /// (discussion #62). Persisted app-wide — the world's era isn't per character.</summary>
    public string QuestEraFilter { get; set; } = "";
    /// <summary>Per-window Ctrl+wheel zoom factors, keyed by window kind ("drops",
    /// "breakout:Damage", …) — the universal text-scaling answer (discussion #59;
    /// David: "a more permanent scaling solution").</summary>
    public Dictionary<string, double> WindowZooms { get; set; } = new();
    /// <summary>Opacity of the widget's background panel only — text stays fully opaque.</summary>
    public double BackgroundOpacity { get; set; } = 0.95;
    /// <summary>Empty finished-session logs automatically. Off = logs grow forever
    /// (for players who upload their logs elsewhere).</summary>
    public bool TruncateLogs { get; set; } = true;
    /// <summary>Copy a log's content to Logs\archive\eqlog_name_server_STAMP.txt before
    /// the janitor empties it — for players who want the raw history kept (discussion
    /// #52, joeymavity). Off by default: most players run EQBuddy precisely so logs
    /// stop accumulating.</summary>
    public bool ArchiveLogs { get; set; }
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
    /// Exclamation, Alarm) or the full path of a custom .wav/.mp3 file.</summary>
    public string AlertSound { get; set; } = "Ding";
    /// <summary>Alert playback volume, 0..1. Defaults to FULL — WPF's MediaPlayer
    /// default is 0.5 and nothing ever set it, so alerts played at half loudness
    /// for everyone (Reddit report: "very quiet, needs a booster").</summary>
    public double AlertVolume { get; set; } = 1.0;
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
    /// <summary>Color theme key (see EQBuddy.UI.Shared.ThemeCatalog); defaults to the
    /// original parchment-and-brass look so existing installs don't change on upgrade.</summary>
    public string Theme { get; set; } = "ParchmentBrass";

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

    /// <summary>Position of the mez-chip stack — its own window, deliberately separate
    /// from the spawn chips (mez chips are combat-urgent, spawn chips are ambient).</summary>
    public double MezChipsLeft { get; set; } = double.NaN;
    public double MezChipsTop { get; set; } = double.NaN;

    /// <summary>Target-drops block in the Loot card (wiki drops for the creature being
    /// fought). Default on; the toggle exists for lean-card people.</summary>
    public bool ShowTargetDrops { get; set; } = true;

    /// <summary>Loot list order: "count" (biggest stacks first, the original behavior) or
    /// "name" (alphabetical — Klona11's ask, discussion #43).</summary>
    public string LootSort { get; set; } = "count";

    /// <summary>Player-supplied hp-per-tick for the regen healing estimate (0 = use the
    /// wiki base value). The log can't see instrument resonance or spell ranks; the
    /// player's own health bar can — their number wins (David, 2026-08-06).</summary>
    public int RegenPerTickOverride { get; set; }

    /// <summary>Hide the widget (and its satellite windows) while the game is running but
    /// NOT the foreground app — alt-tabbing to a browser shouldn't leave the widget over
    /// its buttons (sicliffe-cloud, discussion #41). Off by default; when the game isn't
    /// running at all the widget always shows (people configure it outside the game).</summary>
    public bool HideWhenGameUnfocused { get; set; }

    // Breakout stat windows (BREAKOUT-*): one position + Fight/Session scope per kind.
    // They open while the widget is minimized with the matching star set.

    /// <summary>Breakout kinds the player ✕-closed for good ("Damage", "Loot", …): the
    /// star keeps its mini-pill chip, the window stays away until re-enabled in Options
    /// (Frankthetankk, discussion #45 — ✕-until-next-minimize made the window a
    /// whack-a-mole).</summary>
    public List<string> DisabledBreakouts { get; set; } = [];
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
    // Per-breakout row sort for the stat kinds: "total" | "hits" | "avg" | "rate".
    public string BreakoutDamageSort { get; set; } = "total";
    public string BreakoutHealingSort { get; set; } = "total";
    public string BreakoutPetSort { get; set; } = "total";

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

    public static AppSettings Load()
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
        // Non-short-circuiting on purpose: rules saved before ids existed get theirs
        // assigned at construction, and persisting them NOW is what makes the id stable
        // across restarts rather than re-rolled every launch until some unrelated edit
        // happens to save settings.
        var changed = settings.ApplyDefaultRules();
        changed |= settings.ApplyDefaultSectionLayout();
        changed |= settings.ApplyDefaultSkyQuestChecklist();
        if (changed | settings.TrackedRules.Any(r => r.IdWasGenerated))
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

    private static readonly string[] DefaultSectionOrder =
    [
        "combat", "healing", "kills", "loot", "motes", "sky", "tracked",
        "money", "progress", "faction", "misc",
    ];

    public bool ApplyDefaultSectionLayout()
    {
        var changed = false;
        var order = SectionOrder
            .Where(k => DefaultSectionOrder.Contains(k, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        changed |= order.Count != SectionOrder.Count || !order.SequenceEqual(SectionOrder);

        foreach (var key in DefaultSectionOrder)
        {
            if (order.Contains(key, StringComparer.Ordinal))
                continue;

            var defaultIndex = Array.IndexOf(DefaultSectionOrder, key);
            var insertAt = order.Count;

            for (var i = defaultIndex - 1; i >= 0; i--)
            {
                var previous = order.IndexOf(DefaultSectionOrder[i]);
                if (previous >= 0)
                {
                    insertAt = previous + 1;
                    break;
                }
            }

            order.Insert(insertAt, key);
            changed = true;
        }

        var hidden = HiddenSections
            .Where(k => DefaultSectionOrder.Contains(k, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        changed |= hidden.Count != HiddenSections.Count || !hidden.SequenceEqual(HiddenSections);

        SectionOrder = order;
        HiddenSections = hidden;
        return changed;
    }

    public bool ApplyDefaultSkyQuestChecklist()
    {
        SkyQuestChecklist ??= [];
        var changed = false;
        foreach (var item in SkyQuestDefaults.Items)
        {
            if (SkyQuestChecklist.Any(i => string.Equals(i.Id, item.Id, StringComparison.Ordinal)))
                continue;

            SkyQuestChecklist.Add(item.Clone());
            changed = true;
        }

        return changed;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex); // non-fatal, but visible
        }
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

    public SkyQuestChecklistItem Clone() => new()
    {
        Id = Id,
        ClassName = ClassName,
        Npc = Npc,
        Reward = Reward,
        QuestItem = QuestItem,
        Source = Source,
        Acquired = Acquired,
    };
}
