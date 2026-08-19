using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using LevelUnlockText = EQBuddy.UI.Shared.LevelUnlockText;
using ProgressText = EQBuddy.UI.Shared.ProgressText;
using SpawnChip = EQBuddy.UI.Shared.SpawnChip;
// An alias rather than a `using EQBuddy.UI.Shared` — this file already aliases two types
// out of that namespace one at a time, because importing the whole of it collides with
// Core here. Tok reads at the call site the way DesignTokens does everywhere else.
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

public partial class MainWindow : Window, ICardContext
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SessionStats _stats = new();
    // Attached at construction (not in SessionStats itself) so tests never touch disk.
    private void AttachSpellStore() =>
        _stats.Spells.AttachStore(System.IO.Path.Combine(Core.AppPaths.Dir, "spell-categories.json"));
    private readonly LogWatcher _watcher;
    private readonly SessionRepository _repo = new(SessionRepository.DefaultDbPath);
    private readonly SessionArchiver _archiver;
    private DateTime _lastCheckpoint = DateTime.MinValue;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _companionPump;
    // Window-level double-click state for the mini-bar breakout chips (see MiniChip): the
    // chips are rebuilt every tick, so per-element ClickCount can't be trusted. Threshold
    // reads the user's own Windows double-click speed; add a floor for a stray zero.
    private BreakoutKind? _lastChipClickKind;
    private DateTime _lastChipClickAt = DateTime.MinValue;
    private static readonly TimeSpan DoubleClickWindow =
        TimeSpan.FromMilliseconds(Math.Max(200, GetDoubleClickTime()));

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
    private DateTime _lastCharScan = DateTime.MinValue;
    private DateTime _lastJanitorRun = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private DateTime _upToDateNoticeUntil = DateTime.MinValue;
    private bool _installingUpdate;

    private readonly SpawnTimers _spawnTimers;
    internal SpawnTimers SpawnTimers => _spawnTimers;
    private readonly Companion.CompanionHost _companion;
    internal Companion.CompanionHost Companion => _companion;
    private CompanionWindow? _companionWindow;
    private readonly EQBuddy.UI.Shared.SpawnsViewModel _spawnsVm;
    private SpawnsWindow? _spawnsWindow;
    // Gear auto-done high-water marks, one per snapshot stream: loot drops, manual
    // merges (Crafted — how exaltations arrive), and loot-merge results (Upgraded —
    // how a wished "+N" tier is usually reached). Same delta contract as Sky's.
    private readonly Dictionary<string, int> _gearLootSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _gearCraftSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _gearUpgradeSeen = new(StringComparer.OrdinalIgnoreCase);
    // Rebuilding 200+ checkboxes every UI tick is the one thing this overlay never
    // does elsewhere — the checklist re-renders only when a box actually changed.
    private bool _gearChecklistDirty = true;
    /// <summary>The Epic and Sky checklists, and their own dirty flags, live here
    /// (extracted 2026-08-15). Assigned after InitializeComponent, since it caches
    /// the controls.</summary>
    private QuestChecklistView _quests = null!;

    // The Loot card, lifted out for Gate 4 the way QuestChecklistView was lifted out
    // before it: a surface migrated INSIDE MainWindow is guarded by no ratchet.
    private LootCardView _loot = null!;

    // Cards on the IWidgetCard seam (Gate 5b). Kills takes no context at all.
    //
    // The PROGRESS THEME's five — Progress, Money, Motes, Faction, Raids — are no longer
    // fields here at all: they are not on the widget any more, and ProgressWindow builds
    // its own instances through NewProgressSurfaces() below. A UIElement has one parent,
    // so a shared instance would be torn out of whichever host drew it last.
    private readonly KillsCardView _kills = new();
    /// <summary>What opened up at this level and the next. The Progress card used to hold
    /// this memo, and three callers still need the answer with no card in sight — the
    /// launcher summary, the buff suggestions and the Progress breakout. Shared with the
    /// Avalonia widget, which had a hand-copied twin of it (UI.Shared/LevelUnlockMemo).</summary>
    private LevelUnlockMemo _unlocks = null!;
    // Watch takes the seam plus one thing no snapshot can answer: when each rule's cue
    // is due. That is scheduled by the alert path, not by the session, so it is handed
    // in rather than reached for — and ICardContext stays six methods wide.
    private WatchCardView _watch = null!;

    // ---- ICardContext ----
    //
    // Implemented EXPLICITLY, so the six methods a card may use become an interface
    // without any of them becoming public API on this class. That is the whole point of
    // the seam: a card depends on these six, not on the other fifty-five internals this
    // window carries, and can therefore be exercised against a fake in a unit test —
    // which is the hole docs/TestPlan.md §5 has recorded since the WPF layer was written.
    StatsSnapshot ICardContext.CurrentSnapshot() => CurrentSnapshot();
    void ICardContext.ShowItemInfo(string itemName) => ShowItemInfo(itemName);
    bool ICardContext.IsActiveQuestItem(string itemName) => IsActiveQuestItem(itemName);
    string? ICardContext.QuestAwareTooltip(string itemName, string? baseTip) =>
        QuestAwareTooltip(itemName, baseTip);
    string ICardContext.ItemHoverStats(string itemName) => ItemHoverStats(itemName);
    void ICardContext.OpenQuestInfoForItem(string itemName) => OpenQuestInfoForItem(itemName);
    // Perf audit #1: the version last painted into the expanded sections, and the
    // last time a full paint happened (10 s heartbeat keeps time-derived rates live).
    private long _lastRenderedVersion = -1;
    private DateTime _lastFullRender = DateTime.MinValue;

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "pet", "procs", "loot", "motes", "money", "xp", "deaths"];

    // StatSort moved to BreakdownRows.cs (internal) when the breakout windows grew
    // their own sort bars — one enum, every surface.
    private StatSort _dmgOutSort = StatSort.Total;
    private StatSort _dmgInSort = StatSort.Total;
    private StatSort _healSort = StatSort.Total;

    public MainWindow()
    {
        InitializeComponent();
        // FIRST, before any control is restored. Restoring a control raises its
        // handler, and the quest handlers forward here — so a player with
        // "classic-doable only" already ticked crashed the constructor outright in
        // 1.84.0, leaving a process with no window (#158, twidget76). Anything the
        // XAML can call must exist before the XAML is touched.
        _quests = new QuestChecklistView(this, _settings, () => _raidLedger);
        _loot = new LootCardView(this, _settings);
        LootBody.Content = _loot.Body;
        KillsBody.Content = _kills.Body;
        _unlocks = new LevelUnlockMemo(
            s => BuffSetClassSource(s).Classes,
            () => QuestLedger?.LevelFor(QuestCharacterKey) is > 0 and var lv ? lv : null);
        _watch = new WatchCardView(this, _settings, _delayedAlerts.NextDueByRule);
        TrackedBody.Content = _watch.Body;

        BuildSortStrips();
        GearByZoneCheck.IsChecked = _settings.GearGroupByZone;
        // Before the watcher's startup replay, so already-logged charms classify with
        // everything learned in earlier sessions (issue #29).
        AttachSpellStore();
        _mezTracker.AttachStore(System.IO.Path.Combine(Core.AppPaths.Dir, "mez-durations.json"));
        _stats.AaStore = new AaLedgerStore(AppPaths.File("aa-ledger.json"));
        // Measured stacking conflicts ("did not take hold") — per character, replay-safe.
        _stats.StackingStore = new StackingLedgerStore(AppPaths.File("stacking-ledger.json"));
        // Quest ledger rides the same replay: the catalog decides what's worth keeping,
        // the store's time high-water mark keeps the replay from double-counting.
        QuestCatalog = QuestCatalog.LoadEmbedded();
        ZoneGraph = ZoneGraph.LoadEmbedded();
        QuestLedger = new QuestLedgerStore(AppPaths.File("quest-ledger.json"))
        { TrackFilter = QuestCatalog.IsTurnInItem, Normalize = QuestCatalog.BaseItemName };
        _stats.QuestStore = QuestLedger;
        _watcher = new LogWatcher(_stats);
        _watcher.Mez = _mezTracker;
        _watcher.Slow = _slowTracker;
        _slowTracker.Landed += OnSlowLanded;
        _buffTracker.AttachStore(System.IO.Path.Combine(Core.AppPaths.Dir, "buff-durations.json"));
        // Your Spell Casting Reinforcement rank stretches your own casts' estimates
        // (+5/15/30/50%); learned durations already carry it and are never re-scaled.
        _buffTracker.ReinforcementRank = () => _stats.AaRank("Spell Casting Reinforcement");
        _watcher.Buffs = _buffTracker;
        // The lost-buff history's evidence intake (#120 stage 3) rides the same
        // stream; its transition detection runs off the UI tick (ObserveBuffLosses).
        _watcher.BuffLosses = _buffLossLog;
        // Configure BEFORE Warmup: the warmup instance applies the stored voice/rate/
        // volume at creation, so even the very first alert speaks with them.
        EQBuddy.UI.Shared.SpokenAlerts.Configure(
            _settings.SpeechVoice, _settings.SpeechRate, _settings.SpeechVolume);
        EQBuddy.UI.Shared.SpokenAlerts.Warmup();   // first alert must not pay SAPI's init
        _raidLedger = new RaidKillLedger(AppPaths.File("raid-kills.json"))
        { CharacterKey = () => _stats.LedgerCharacterKey };
        _watcher.Raids = _raidLedger;
        // Spawn timers ride the watcher's event stream — wired before the first Select so
        // the startup replay re-derives countdowns from kills already in the log.
        var spawnCatalog = SpawnCatalog.LoadEmbedded();
        var spawnOverrides = SpawnOverrides.Load(AppPaths.File("spawn-overrides.json"));
        _spawnTimers = new SpawnTimers(spawnCatalog, spawnOverrides, AppPaths.File("spawn-timers.json"));
        _watcher.Spawns = _spawnTimers;
        _spawnsVm = new EQBuddy.UI.Shared.SpawnsViewModel(spawnCatalog, spawnOverrides, _spawnTimers);
        // EQBuddy Mobile — the title-bar 📱 and the menu's first window entry are always
        // there now; the host below is constructed with its data sources but stays silent,
        // opening no socket at all, until the player turns CompanionEnabled on.
        // The map's spawn-point circles: kills near a fresh /loc accrete into
        // per-zone archives that only refine over time (David's map brief).
        _spawnPoints = new SpawnPointLedger(
            System.IO.Path.Combine(AppPaths.Dir, "zone-spawns"), spawnCatalog);
        _watcher.SpawnPoints = _spawnPoints;
        // Every phone surface reads through these callbacks, which the host invokes
        // only for surfaces the owner offers and only while a device is paired — the
        // point of handing over lambdas rather than a pile of references.
        _companion = new Companion.CompanionHost(_settings, UpdateChecker.CurrentVersion.ToString(),
            new Companion.CompanionSources
            {
                TimerZone = () => _spawnTimers.CurrentZone?.Zone ?? SpawnCatalog.StripTierVariant(CurrentZoneName),
                SpawnPoints = _spawnPoints,
                Mezzes = now => _mezTracker.Snapshot(now),
                BuffSets = now => [.. BuffSetSectionStates(CurrentSnapshot(), now)
                    .Select(sec => (sec.Class, (IReadOnlyList<BuffSetEntryState>)sec.Entries))],
                BuffLosses = () => _buffLossLog.Snapshot(),
                HopsFromHere = zone => ZoneGraph.Distance(CurrentZoneName, zone)?.Hops,
                Progress = () => (CurrentSnapshot().LastLevel, ProgressDingUnlocks(CurrentSnapshot())),
                // The Progress theme's Raids tab, wired in the same change as the desktop
                // fold — both surfaces or neither (#210).
                Raids = _raidLedger,
                CampFor = t => UI.Shared.CampLocations.Resolve(
                    t, EnsureMobLookup, n => WikiMobResult(n)?.Mob?.LocYX),
                Quests = () => new Companion.CompanionQuestRequest
                {
                    Catalog = QuestCatalog,
                    Owned = QuestLedger?.For(QuestCharacterKey)
                        ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase),
                    Tracked = QuestLedger?.TrackedFor(QuestCharacterKey)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Hidden = QuestLedger?.HiddenFor(QuestCharacterKey)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    Completed = QuestLedger?.CompletedFor(QuestCharacterKey)
                        ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    Classes = QuestLedger?.ClassesFor(QuestCharacterKey) ?? [],
                    InferredClass = CurrentSnapshot().InferredClass,
                },
                QuestLedger = QuestLedger,
                QuestCharacterKey = () => QuestCharacterKey,
            });
        ThemeManager.PaletteApplied += _companion.SetTheme;
        _companion.SurfaceEdited += OnCompanionSurfaceEdited;
        _spawnOverrides = spawnOverrides;
        _spawnCatalog = spawnCatalog;
        // Before any tailing: the initial full-log ingest has to know which text rules to
        // watch for, or a Text rule would miss everything already in today's log.
        _stats.RefreshTextPatterns(_settings.TrackedRules);
        _stats.TextMatched += OnTextMatched;
        // An idle gap ended the session: anything still cued belongs to a fight that is
        // long over.
        _stats.SessionRolledOver += () => Dispatcher.BeginInvoke(_delayedAlerts.CancelAll);
        _archiver = new SessionArchiver(_repo);
        // A 60-minute quiet gap ends a session — persist its final state to history.
        // Not while reviewing an archived log (#74): those sessions were archived when
        // they were live; replay must not mint duplicates.
        _stats.SessionEnding += snap =>
        {
            if (_reviewPath is null) _archiver.FinalizeActive(snap, "IdleTimeout");
        };

        // Height caps follow the monitor the widget is ON (a portrait secondary screen
        // is taller than the primary — discussion #31); primary work area is only the
        // pre-handle starting value.
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        ApplySectionMaxHeight(SystemParameters.WorkArea.Height - 160);
        SourceInitialized += (_, _) => UpdateHeightCaps();
        LocationChanged += (_, _) => UpdateHeightCaps();

        // Migration: any per-rule pin from older versions turns on the group pin.
        if (!_settings.PinWatchChips && _settings.TrackedRules.Any(r => r.Pinned))
            _settings.PinWatchChips = true;
        // Chips became per-rule again. Someone who had them on was seeing every enabled rule,
        // so pin what they already had rather than silently emptying their mini bar. Once
        // only — gated on a flag so deliberately unpinning every rule isn't undone next launch.
        if (!_settings.WatchPinsMigrated)
        {
            // Not conditioned on "nothing is pinned": AppSettings.Load may already have
            // added the built-in CC-broke rule, which is pinned by default, and that made
            // this pass skip itself and leave the user's own rules invisible.
            if (_settings.PinWatchChips)
                foreach (var rule in _settings.TrackedRules.Where(r => r.Enabled))
                    rule.Pinned = true;
            _settings.WatchPinsMigrated = true;
            _settings.Save();
        }

        if (_settings.LogFolder is { } saved && !System.IO.Directory.Exists(saved))
            _settings.LogFolder = null; // stale saved path (game moved) — re-detect
        _settings.LogFolder ??= LogWatcher.FindDefaultLogFolder();
        // A saved spot on a monitor that's gone (undocked, TV unplugged) would put the
        // widget in the void — and settings.json survives reinstalls, so it stays there.
        _restoredSavedPosition = ScreenGuard.OnScreen(_settings.WindowLeft, _settings.WindowTop, Width, Height);
        if (_restoredSavedPosition) { Left = _settings.WindowLeft; Top = _settings.WindowTop; }
        else { Left = SystemParameters.WorkArea.Right - 360; Top = 40; }
        (_placedLeft, _placedTop) = (Left, Top);
        Opacity = _settings.Opacity;
        Topmost = true;
        ApplyUiScale(_settings.UiScale);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);

        VersionMenuItem.Header = $"EQBuddy v{UpdateChecker.CurrentVersion}";

        WindowZoom.Route(this, () => _settings.UiScale, SetUiScale);
        foreach (var (key, star) in StarButtons())
            star.IsChecked = _settings.MiniStats.Contains(key);
        ApplySectionLayout();
        SetMode(_settings.Minimized);

        FollowActiveCharacter();

        // The quick tour shows at every launch until disabled ("Never show again"
        // in the tour, or the Options checkbox).
        if (_settings.ShowTutorial)
            Loaded += (_, _) => new TutorialWindow(this).Show();

        // A grid left on comes back — turning it off is the same menu click (#34).
        if (_settings.ShowGridOverlay)
            Loaded += (_, _) => SetGridOverlay(true);
        if (_settings.ShowCursorRing)
            Loaded += (_, _) => SetCursorRing(true);

        // Log hygiene at startup: force Log=1 and wipe finished-session logs
        // (both no-ops while the game is running). Truncation waits while the tour
        // is enabled — its first page is the consent question; the 10-minute
        // periodic janitor handles it afterwards.
        if (_settings.LogFolder is { } lf)
        {
            var prune = _settings.TruncateLogs && !_settings.ShowTutorial;
            var archive = _settings.ArchiveLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(lf);
                if (prune) EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap,
                    archive: archive, archived: AnnounceArchive);
            });
        }

        // EQBUDDY_EXPAND=1 opens the review set (and turns on the E2E dump below);
        // EQBUDDY_EXPAND=loot,kills opens just those cards. The named form exists for
        // scripts/shoot.ps1: a card's expanded state is not persisted, so without it the
        // only way to photograph a card BODY was to open every card at once and crop —
        // and the review is an acceptance criterion, not a nicety (Gate 3's §11.6).
        var expand = Environment.GetEnvironmentVariable("EQBUDDY_EXPAND");
        if (expand == "1")
            foreach (var ex in new[] { CombatSection, HealingSection, KillsSection, LootSection,
                         GearSection, TrackedSection, MiscSection })
                ex.IsExpanded = true;
        else if (!string.IsNullOrWhiteSpace(expand))
        {
            var wanted = expand.Split(',', StringSplitOptions.RemoveEmptyEntries
                                           | StringSplitOptions.TrimEntries);
            foreach (var (key, element) in SectionMap())
                if (element is Expander card
                    && wanted.Contains(key, StringComparer.OrdinalIgnoreCase))
                    card.IsExpanded = true;
        }

        // Expanding a card renders it NOW (David's field report: sections only fill
        // inside the fullRender gate, so a click during a quiet moment stared at an
        // empty body until the next event or the 10 s heartbeat — up to "multiple
        // seconds to open"). Background priority lets the expander's own layout land
        // first, so the click still feels mechanical.
        foreach (var el in SectionMap().Values)
            if (el is Expander section)
                section.Expanded += (_, _) =>
                {
                    _lastRenderedVersion = -1;
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                        RefreshUi);
                };

        if (Environment.GetEnvironmentVariable("EQBUDDY_CCLOG") == "1")
            StartCrowdControlCapture();

        // Tray icon: EQBuddy's always-there presence (#114 follow-up) — when a hide
        // takes the widget AND its taskbar entry, this is how you know it's running
        // and how you get it back.
        _trayIcon = new TrayIcon(this);

        // Warm the embedded item catalog off-thread: its one-time gunzip+parse
        // (~11k records) must never land on the UI thread mid-fight via the first
        // loot-row tooltip (2026-08-13 review) — after this, first UI touch is a
        // dictionary probe.
        Task.Run(() => Core.ItemCatalog.Default);

        // Screenshot/debug hook, same family as EQBUDDY_OPTIONS: open the Quest Tracker
        // after the startup replay has fed the ledger. "1" opens the default view;
        // "zone"/"all" open that mode directly.
        if (Environment.GetEnvironmentVariable("EQBUDDY_DROPS") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnDropsWindow(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_WIKIPACK") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(ShowWikiPackWindow,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_QUESTS") is { Length: > 0 } questsMode)
            Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                ShowQuestsWindow();
                if (questsMode.Split(':')[0] is "sky" or "epic") _questsWindow?.SetTab(questsMode);
                else if (questsMode is "zone" or "all") _questsWindow?.SetMode(questsMode);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Same family. The Progress window is where five card BODIES went, and a card
        // body has never been photographable except through a hook — so without this the
        // theme's four tabs could not be reviewed, and a surface nobody can review reads
        // as "reviewed" anyway (trap 22). "1" opens it on Experience; a tab key
        // (wealth / faction / raids) opens it there.
        if (Environment.GetEnvironmentVariable("EQBUDDY_PROGRESS") is { Length: > 0 } progressTab)
            Loaded += (_, _) => Dispatcher.BeginInvoke(
                () => ShowProgressWindow(progressTab == "1" ? null : progressTab),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Same family, and the one that was missing: the Spawns window deliberately stays
        // hidden until a countdown exists, so scripts/shoot.ps1 could never capture it and
        // Gate 3 shipped without a screenshot review. "1" opens it on the current zone.
        if (Environment.GetEnvironmentVariable("EQBUDDY_SPAWNS") is { Length: > 0 } spawnZone)
            Loaded += (_, _) => Dispatcher.BeginInvoke(
                () => ShowSpawnsWindow(spawnZone == "1" ? null : spawnZone),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_OPTIONS") == "1")
            Loaded += (_, _) => OnOptions(this, new RoutedEventArgs());

        if (Environment.GetEnvironmentVariable("EQBUDDY_MAP") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnZoneMap(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_TRAVEL") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnTravelRoute(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_INVENTORY") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnInventoryWindow(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_GEARLOCKER") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnGearLocker(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_TIMELINE") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(OpenFightTimeline,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Screenshot/debug hook, same family as EQBUDDY_QUESTS: open straight into
        // archive review of the given file (#74), skipping the file dialog.
        if (Environment.GetEnvironmentVariable("EQBUDDY_REVIEW") is { Length: > 0 } reviewPath)
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => EnterReview(reviewPath),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_FEEDBACK") == "1")
            Loaded += (_, _) => OnFeedback(this, new RoutedEventArgs());


        if (Environment.GetEnvironmentVariable("EQBUDDY_HISTORY") == "1")
            Loaded += async (_, _) =>
            {
                await Task.Delay(4000); // let initial ingest finish
                OnHistory(this, new RoutedEventArgs());
            };

        if (Environment.GetEnvironmentVariable("EQBUDDY_MENU") == "1")
            Loaded += (_, _) =>
            {
                if (RootBorder().ContextMenu is not { } m) return;
                m.StaysOpen = true;
                m.PlacementTarget = RootBorder();
                m.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                m.IsOpen = true;
            };

        // What's-new notes, once per update. A fresh install (tutorial still pending)
        // skips them and just records the baseline — onboarding is the tutorial's job.
        // Installs from before the feature have no baseline; they get only the current
        // version's notes rather than the whole history.
        var currentVersion = UpdateChecker.CurrentVersion.ToString();
        if (_settings.ShowTutorial || _settings.LastSeenVersion == currentVersion)
        {
            if (_settings.LastSeenVersion != currentVersion)
            {
                _settings.LastSeenVersion = currentVersion;
                _settings.Save();
            }
        }
        else
        {
            var lastSeen = _settings.LastSeenVersion.Length > 0
                ? _settings.LastSeenVersion
                : PreviousVersionBaseline(currentVersion);
            var notes = WhatsNewCatalog.EntriesBetween(lastSeen, currentVersion);
            _settings.LastSeenVersion = currentVersion;
            _settings.Save();
            if (notes.Count > 0)
                Loaded += (_, _) => new WhatsNewWindow(this, notes).Show();
        }

        // No auto-open here: the window pops from RefreshUi when a countdown exists —
        // including ones recovered from the log during startup ingest. A tracker parked
        // on screen with nothing to say was the 1.20.0 behaviour, and it was noise.

        // One-time repair (1.20.1): 1.20.0 could untick zone-following on a selection
        // event the user never made. The auto-untick is gone; restore the default once.
        if (!_settings.SpawnFollowRepaired)
        {
            _settings.SpawnFollowZone = true;
            _settings.SpawnFollowRepaired = true;
            _settings.Save();
        }

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();

        // EQBuddy Mobile's own cadence. The desktop redraws once a second because that
        // is how often a human wants a card to change under their eyes; a phone showing
        // a mez breaking wants to hear about it as soon as the log does. Riding the 1 Hz
        // redraw put up to a second between the two for no reason but shared plumbing.
        _companionPump = new DispatcherTimer(DispatcherPriority.Send)
        { Interval = CompanionPumpInterval };
        _companionPump.Tick += (_, _) => PumpCompanion();
        _companionPump.Start();
    }

    /// <summary>The mobile coalescing window. 50 ms caps pushes at 20/s however fast the
    /// log arrives, so a raid's event storm cannot turn into a message storm — and it is
    /// affordable: a snapshot REBUILD measures 0.081 ms (`IngestBenchmark`), so 20 Hz of
    /// continuous change costs ~1.6 ms/s of one core, and nothing at all while the state
    /// is still or nobody is paired.</summary>
    private static readonly TimeSpan CompanionPumpInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Whether a pump tick has anything to do. The decision lives in UI.Shared
    /// so the "free when idle" claim is unit-tested rather than trusted.</summary>
    private readonly EQBuddy.UI.Shared.CompanionPumpGate _companionGate = new();

    /// <summary>
    /// Push to paired devices as soon as the session actually moves, instead of waiting
    /// for the next desktop redraw.
    ///
    /// This does NOT replace the tick inside <see cref="RefreshUi"/>. That one still runs
    /// every second and is what drives <c>ForcedPushInterval</c> reconciliation, so the
    /// correctness path is untouched and this is purely a latency path. Countdowns are
    /// unaffected either way: they are computed on the device from authoritative
    /// timestamps, and are deliberately excluded from the section fingerprints — a
    /// ticking clock is not news, and including one would wake every phone every pump.
    /// </summary>
    private void PumpCompanion()
    {
        _companionPumpTicks++;
        if (!_companionGate.ShouldPush(_companion.HasClients, _stats.CurrentVersion)) return;
        _companionPushes++;
        _companion.Tick(_stats.Snapshot(), _spawnTimers, _stats.CharacterName ?? "", DateTime.Now);
    }

    // For the EQBUDDY_EXPAND dump: how many times the pump ran, and how many of those
    // did any work. E2E asserts the second is zero while no device is paired — the
    // "free when idle" claim is the one that costs a core if it's wrong, and a unit
    // test of the gate can't show that the real timer is wired to the real gate.
    private long _companionPumpTicks;
    private long _companionPushes;

    public AppSettings Settings => _settings;
    /// <summary>
    /// EQBUDDY_CCLOG=1: append log lines we suspect are meaningful but couldn't match, to
    /// %AppData%\EQBuddy\cc-candidates.txt — crowd-control landing lines and pet chatter.
    /// Both have unconfirmed EQ Legends wording, so rather than ship guessed regexes that
    /// silently never fire, we capture the real text during play and turn it into proper
    /// patterns — with fixtures — in a later release. Distinct lines only, capped, so a
    /// long session can't fill the disk.
    /// </summary>
    private static void StartCrowdControlCapture()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Core.AppPaths.File("cc-candidates.txt");
        var gate = new object();
        LogParser.UnmatchedCandidateSink = msg =>
        {
            lock (gate)
            {
                if (seen.Count >= 500 || !seen.Add(msg)) return;
                try { System.IO.File.AppendAllText(path, msg + Environment.NewLine); }
                catch { /* diagnostics must never break tailing */ }
            }
        };
    }

    public void PersistSettings() => _settings.Save();

    internal static readonly (string Key, string Title)[] SectionCatalog =
        EQBuddy.UI.Shared.OverlaySections.Catalog;

    private Dictionary<string, UIElement> SectionMap() => new()
    {
        ["combat"] = CombatSection, ["healing"] = HealingSection, ["kills"] = KillsSection,
        ["loot"] = LootSection, ["quests"] = QuestsSection,
        ["gear"] = GearSection, ["tracked"] = TrackedSection,
        ["buffs"] = BuffsSection,
        // One key for the whole Progress theme, and it is deliberately "progress" — one
        // OF the five it absorbs rather than a new name, so a player who had dragged that
        // card somewhere keeps the slot instead of finding the theme appended to the
        // bottom of their list (AppSettings.MigrateProgressSections).
        ["progress"] = ProgressSection, ["misc"] = MiscSection,
    };

    /// <summary>Apply saved card order + hidden set (OVERLAY-001..003). Hidden cards keep collecting.</summary>
    public void ApplySectionLayout()
    {
        var map = SectionMap();
        var order = _settings.SectionOrder.Where(map.ContainsKey).ToList();
        foreach (var (key, _) in SectionCatalog)
            if (!order.Contains(key)) order.Add(key);

        // Options is the whole truth (David's 1.66.2 verdict): a card the user hasn't
        // hidden SHOWS, empty or not — self-hiding cards read as missing features. The
        // renders fill an honest one-line empty state instead of vanishing the card.
        SectionsPanel.Children.Clear();
        foreach (var key in order)
        {
            var el = map[key];
            SectionsPanel.Children.Add(el);
            ((FrameworkElement)el).Visibility = _settings.HiddenSections.Contains(key)
                ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    internal QuestCatalog QuestCatalog { get; private set; } = new();
    internal ZoneGraph ZoneGraph { get; private set; } = new();
    internal QuestLedgerStore? QuestLedger { get; private set; }
    internal string QuestCharacterKey => _stats.LedgerCharacterKey;

    /// <summary>The zone the log last put us in — the Quest Tracker measures distances
    /// from here.</summary>
    internal string CurrentZoneName { get; private set; } = "";

    /// <summary>Followed character identity for window titles and exports.</summary>
    internal (string Character, string Server) Identity =>
        (_stats.CharacterName ?? "", _stats.ServerName ?? "");

    /// <summary>The snapshot RefreshUi built this tick, reused by every satellite
    /// window on its own cadence (perf audit #12: the map's marker/trail, Drops'
    /// signature and Quests' inferred-class checks each built their own full
    /// snapshot per tick). Snapshots are immutable, so sharing one instance is
    /// safe; it is at most one tick (~1 s) old, which is already the cadence the
    /// satellites polled at. Null only before the first tick.</summary>
    private StatsSnapshot? _latestSnapshot;

    /// <summary>The current stats snapshot for windows that refresh on their own
    /// cadence: this tick's shared instance, or a fresh build when a window opens
    /// before RefreshUi has ever ticked.</summary>
    internal StatsSnapshot CurrentSnapshot() => _latestSnapshot ?? _stats.Snapshot();

    /// <summary>The 🗺 badge signal: a known quest's turn-in OR a member of the wiki's
    /// Quest Items category (back to the broad set once the loud green retired — a
    /// quiet glyph can afford the coverage; David's Crushbone pass, 2026-08-07). When
    /// known quests want the item and ALL are dismissed, the badge goes too.
    /// Third source, from #75: the item page's own "QUEST ITEM" stats flag — some
    /// pages carry the flag but miss the category (Phosphorous Powder), and the
    /// cached page knows better than the harvest. Cache-only on purpose: the badge
    /// appears once you've looked the item up, and costs nothing before that.</summary>
    internal bool IsActiveQuestItem(string name)
    {
        var wanting = QuestCatalog.QuestsWanting(name);
        if (wanting.Count == 0)
            return QuestCatalog.IsQuestItem(name)
                || _wikiItems.CachedInfo(name) is { QuestFlagged: true };
        var hidden = QuestLedger?.HiddenFor(QuestCharacterKey);
        return hidden is not { Count: > 0 } || wanting.Any(q => !hidden.Contains(q.Name));
    }

    /// <summary>Badge click, one behavior everywhere: quests we can name open in the
    /// Quest Tracker; a category-only item opens its own wiki page, where the quest
    /// that wants it is documented.</summary>
    internal void OpenQuestInfoForItem(string itemName)
    {
        var baseName = QuestCatalog.BaseItemName(itemName);
        if (QuestCatalog.QuestsWanting(baseName).Count > 0) ShowQuestsWindow(baseName);
        else OpenWikiPage(baseName);
    }

    /// <summary>Prefix an item tooltip with the quest marker so the green explains itself.</summary>
    internal string? QuestAwareTooltip(string name, string? baseTip)
    {
        if (!IsActiveQuestItem(name)) return baseTip;
        // Names the badge in WORDS rather than reproducing it. A tooltip that draws the
        // glyph it points at is drawing it on the Wine prefixes where it renders as a
        // box — twice, in the same sentence, in what is supposed to be the explanation.
        const string marker =
            "Part of a quest — click the green map pin to see its quests in the Quest Tracker.";
        return baseTip is { Length: > 0 } ? marker + "\n" + baseTip : marker;
    }


    public double UiScale => _settings.UiScale;

    public void SetUiScale(double scale)
    {
        _settings.UiScale = Math.Clamp(scale, 0.5, 2.0);
        ApplyUiScale(_settings.UiScale);
        // The section cap is expressed in pre-scale units, so it has to be re-derived
        // whenever the scale moves — otherwise the list keeps the previous scale's
        // allowance and the bottom card is clipped or short until something else
        // happens to recompute it (discussion #144).
        ApplySectionMaxHeight();
        _settings.Save();
    }

    /// <summary>Live-apply the chips/alerts scale to whichever family windows exist right
    /// now; windows created later pick it up in their constructors.</summary>
    public void SetChipScale(double scale)
    {
        _settings.ChipScale = Math.Clamp(scale, 0.5, 2.0);
        foreach (var w in new Window?[] { _chipsWindow, _mezWindow, _alertWindow })
            if (w is not null) ChipScale.Apply(w, _settings.ChipScale);
        _settings.Save();
    }

    private void ApplyUiScale(double scale) =>
        RootBorder().LayoutTransform = Math.Abs(scale - 1.0) < 0.001
            ? null
            : new System.Windows.Media.ScaleTransform(scale, scale);

    // Resize-grip state captured at drag start. The window has no native resize border
    // (WindowStyle=None), and SizeToContent="WidthAndHeight" means setting Width/Height
    // directly wouldn't stick anyway — so the grip drives UiScale instead, and the window
    // grows or shrinks to fit as SizeToContent re-measures the rescaled content. Deriving
    // the drag distance from the cursor's absolute position each frame, rather than
    // accumulating DragDelta, avoids feedback jitter as the window resizes under the
    // cursor mid-drag.
    private double _resizeCursorX, _resizeCursorY, _resizeStartScale, _resizeStartWidth, _resizeStartHeight;

    private void OnResizeGripStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _resizeCursorX = CursorX();
        _resizeCursorY = CursorY();
        _resizeStartScale = _settings.UiScale;
        _resizeStartWidth = ActualWidth;
        _resizeStartHeight = ActualHeight;
    }

    private void OnResizeGripDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (_resizeStartWidth < 1 || _resizeStartHeight < 1) return;
        // Average the two axes so a diagonal drag from the corner feels like one motion
        // rather than the width or height alone dominating.
        var widthFactor = 1 + (CursorX() - _resizeCursorX) / _resizeStartWidth;
        var heightFactor = 1 + (CursorY() - _resizeCursorY) / _resizeStartHeight;
        SetUiScale(_resizeStartScale * (widthFactor + heightFactor) / 2);
    }

    /// <summary>Cursor position in device-independent units (the space Width/Height live in).</summary>
    private double CursorX()
    {
        Native.GetCursorPos(out var p);
        return p.X * DipScale().X;
    }

    private double CursorY()
    {
        Native.GetCursorPos(out var p);
        return p.Y * DipScale().Y;
    }

    private (double X, double Y) DipScale()
    {
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        return m is { } t ? (t.M11, t.M22) : (1.0, 1.0);
    }

    public void SetWindowOpacity(double opacity)
    {
        _settings.Opacity = Math.Clamp(opacity, 0.3, 1.0);
        Opacity = _settings.Opacity;
        _settings.Save();
    }

    public double BackgroundOpacityValue => _settings.BackgroundOpacity;

    public bool TruncateLogsValue => _settings.TruncateLogs;

    public void SetTruncateLogs(bool enabled)
    {
        _settings.TruncateLogs = enabled;
        _settings.Save();
    }

    public void SetBackgroundOpacity(double opacity)
    {
        _settings.BackgroundOpacity = Math.Clamp(opacity, 0.15, 1.0);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        _settings.Save();
    }

    private void ApplyBackgroundOpacity(double opacity)
    {
        // Tint comes from the current theme's BgBrush rather than a fixed color, so this
        // still reads right after a theme switch — only the alpha is opacity's to control.
        var tint = ((SolidColorBrush)FindResource("BgBrush")).Color;
        RootBorder().Background = new SolidColorBrush(
            Color.FromArgb((byte)(opacity * 255), tint.R, tint.G, tint.B));
    }

    /// <summary>Re-applies visual state that was baked in via FindResource at construction
    /// time rather than DynamicResource, so a live theme switch reaches it too.</summary>
    public void RefreshTheme()
    {
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        RootBorder().BorderBrush = (Brush)FindResource(_clickThrough ? "WarnBrush" : "HairlineBrush");
        // Most stat rows bake their brush in via FindResource when built rather than a
        // binding, and only get rebuilt on the next data change — force one now so an idle
        // widget still repaints immediately when the theme switches.
        RefreshUi();
    }

    private OptionsWindow? _optionsWindow;

    /// <summary>For pre-feature installs with no baseline: pretend they saw everything
    /// before the running version, so they get exactly one version's worth of notes.</summary>
    private static string PreviousVersionBaseline(string current) =>
        Version.TryParse(current, out var v)
            ? new Version(v.Major, Math.Max(0, v.Minor - 1), 0).ToString()
            : current;

    private SpawnChipsWindow? _chipsWindow;
    private MezChipsWindow? _mezWindow;
    private readonly MezTracker _mezTracker = new();
    private readonly SlowTracker _slowTracker = new();
    private readonly BuffTracker _buffTracker = new();
    private readonly BuffLossLog _buffLossLog = new();
    private readonly RaidKillLedger _raidLedger;

    private readonly EqlWikiItemService _wikiItems =
        new(System.IO.Path.Combine(Core.AppPaths.Dir, "wiki-cache", "items"));
    internal EqlWikiItemService WikiItems => _wikiItems;
    private ItemInfoWindow? _itemWindow;
    private GearLockerWindow? _gearLockerWindow;

    private void OnCompanion(object sender, RoutedEventArgs e) => OpenCompanionWindow();

    internal void OpenCompanionWindow()
    {
        if (_companionWindow is { IsLoaded: true } open) { open.Activate(); return; }
        _companionWindow = new CompanionWindow(_companion) { Owner = this };
        _companionWindow.Closed += (_, _) => _companionWindow = null;
        _companionWindow.Show();
    }

    /// <summary>A paired device ticked a checklist row. The host already wrote it to
    /// the same settings list a click on the card writes to, and raised this on the
    /// tick thread — so all that's left is the repaint cue the card's own toggle sets.</summary>
    private void OnCompanionSurfaceEdited(string surface)
    {
        switch (surface)
        {
            // A tablet ticking an Epic or Sky row edits the same settings list the Quest
            // Tracker window is drawing from, so the window is what needs the nudge —
            // the widget's Quests card only carries counts, and those refresh on the tick.
            case EQBuddy.Companion.CompanionSurfaces.Quests:
            case EQBuddy.Companion.CompanionSurfaces.Epics:
            case EQBuddy.Companion.CompanionSurfaces.Sky:
                _questsWindow?.MaybeRefresh();
                break;
            case EQBuddy.Companion.CompanionSurfaces.Gear: _gearChecklistDirty = true; break;
        }
    }

    private void OnGearLocker(object sender, RoutedEventArgs e)
    {
        if (_gearLockerWindow is { IsLoaded: true } open) { open.Activate(); return; }
        _gearLockerWindow = new GearLockerWindow(this);
        _gearLockerWindow.Closed += (_, _) => _gearLockerWindow = null;
        _gearLockerWindow.Show();
    }

    /// <summary>Loot rows and the search box route here: one shared popup, re-driven
    /// per lookup.</summary>
    public void ShowItemInfo(string itemName)
    {
        if (_itemWindow is not { IsLoaded: true })
            _itemWindow = new ItemInfoWindow(_wikiItems, _settings) { Owner = this };
        _itemWindow.Show();
        _itemWindow.Activate();
        _itemWindow.Lookup(itemName);
    }

    /// <summary>Hover stats for an item row: the cached wiki stat block when we have one
    /// (any age — a hover is a peek, not a lookup), else a hint that clicking fetches.
    /// Internal: the Loot breakout borrows it for its own rows.</summary>
    internal string ItemHoverStats(string itemName) =>
        _wikiItems.CachedStatsText(itemName) ?? "Click for item info (eqlwiki)";

    /// <summary>Raw cached stats (null when the cache is empty) — the Loot breakout's
    /// tooltip wants the real distinction so it knows to fetch.</summary>
    internal string? CachedItemStats(string itemName) => _wikiItems.CachedStatsText(itemName);

    // ---- target drops (TARGET-*): the Loot card's "what can this drop" block ----

    private readonly EqlWikiMobService _wikiMobs =
        new(System.IO.Path.Combine(Core.AppPaths.Dir, "wiki-cache", "mobs"));

    /// <summary>Session-lifetime per-creature results, so a multi-mob pull never re-looks
    /// anything up and the drops list can't flicker as different creatures swing
    /// (David's live report, 2026-08-06). null value = lookup in flight.</summary>
    private readonly Dictionary<string, MobLookupResult?> _targetResults =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>DropsWindow's window into the target-drops memo (WIKI-NEW, #65): the
    /// Drops view flags observations the wiki doesn't know, reusing the same lookups
    /// and cache the Loot card fires — no extra wiki traffic for creatures already
    /// seen, and anything it does request benefits the Loot card too.</summary>
    internal MobLookupResult? WikiMobResult(string name) =>
        _targetResults.GetValueOrDefault(name);

    internal void EnsureMobLookup(string name)
    {
        if (_targetResults.ContainsKey(name)) return;
        _targetResults[name] = null;
        _ = LookupTargetAsync(name);
    }

    private async Task LookupTargetAsync(string name)
    {
        try
        {
            var result = await _wikiMobs.LookupAsync(name, CurrentZoneName);
            _targetResults[name] = result;
            RefreshUi();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Target-drops content shared by the Loot card's 🎯 block and the Loot
    /// breakout — one builder, so the two can never disagree, and the wiki lookups fire
    /// from HERE so a minimized session (where the card never renders) still resolves
    /// targets. The pool is EVERY creature in the current pull (the log can't say which
    /// is targeted; picking one made the list cycle — David's live report), and items
    /// fold to their base names so "Leather Whip +2" and the wiki's "Leather Whip"
    /// are one row (David's screenshot, same session). "" header = no target.</summary>
    /// <summary>Why the target-drops list is empty, in words that say what we actually
    /// know: a wiki page with no drops recorded is an invitation, not a failure
    /// (David vs the orc thaumaturgist, 2026-08-07 — page exists, loot fields blank).</summary>
    internal string TargetEmptyNote(StatsSnapshot s)
    {
        var targets = s.CurrentTargets;
        if (targets.Count != 1) return "Nothing known for these creatures yet.";
        return _targetResults.GetValueOrDefault(targets[0]) switch
        {
            null => "Looking up on eqlwiki…",
            { State: ItemLookupState.Offline } => "Wiki unreachable — drops will fill in when it's back.",
            { State: ItemLookupState.NotFound } =>
                $"{targets[0]} has no eqlwiki page yet.",
            { Mob.Drops.Count: 0 } =>
                $"The wiki page for {targets[0]} lists no drops yet — nothing you loot\n" +
                "is wasted though: Drops by creature… (right-click menu) exports your\n" +
                "observations, and the wiki takes edits.",
            _ => "Nothing known for this creature yet.",
        };
    }

    internal (string Names, string Detail, List<(string Name, string Value)> Rows)
        TargetDropsContent(StatsSnapshot s)
    {
        var targets = _settings.ShowTargetDrops ? s.CurrentTargets : [];
        if (targets.Count == 0) return ("", "", []);
        foreach (var t in targets)
            if (!_targetResults.ContainsKey(t))
            {
                _targetResults[t] = null;
                _ = LookupTargetAsync(t);
            }

        // Observed drops lead (your data outranks the wiki), folded to base names with
        // counts summed across tiers and creatures. Percent only for a single-creature
        // pool — mixed kill denominators would make it a lie.
        var kills = 0;
        var observed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targets)
        {
            var mob = s.Mobs.FirstOrDefault(m => m.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
            if (mob is null) continue;
            kills += mob.Kills;
            foreach (var l in mob.Loot)
            {
                var baseName = EqlWikiItemService.NormalizeTitle(l.Item);
                observed[baseName] = observed.GetValueOrDefault(baseName) + l.Count;
            }
        }
        var rows = new List<(string Name, string Value)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, count) in observed.OrderByDescending(kv => kv.Value))
        {
            var pct = targets.Count == 1 && kills > 0 ? $" · {100.0 * count / kills:0}%" : "";
            rows.Add((item, $"{count} this session{pct}"));
            seen.Add(item);
        }

        var pending = false;
        foreach (var t in targets)
        {
            var r = _targetResults.GetValueOrDefault(t);
            if (r is null) { pending = true; continue; }
            foreach (var (item, rarity) in r.Mob?.Drops ?? [])
                if (seen.Add(EqlWikiItemService.NormalizeTitle(item)))
                    rows.Add((item, rarity));
        }
        var extra = Math.Max(0, rows.Count - 14);
        if (extra > 0) rows = rows.Take(14).ToList();

        var state = targets.Count == 1
            ? _targetResults.GetValueOrDefault(targets[0]) switch
            {
                null => "looking up…",
                { State: ItemLookupState.Live } => "LIVE",
                { State: ItemLookupState.Cached, FetchedAt: { } at } => $"CACHED {at:M/d}",
                { State: ItemLookupState.StaleCache, FetchedAt: { } at } => $"STALE {at:M/d}",
                { State: ItemLookupState.Offline } => "OFFLINE",
                _ => "NOT ON WIKI",
            }
            : pending ? "looking up…" : "merged pull";
        var names = string.Join(" + ", targets.Take(3)) +
            (targets.Count > 3 ? $" +{targets.Count - 3}" : "");
        // The NAMES and everything ABOUT them, apart: the card writes "Fighting: <names>"
        // under a target icon and the breakout writes just the names, and each composes
        // its own line through LootPresentation rather than string-replacing the other's.
        var detail = (kills > 0 ? $" — {kills} kill{(kills == 1 ? "" : "s")} this session" : "") +
            $" · drops (eqlwiki · {state}{(extra > 0 ? $" · +{extra} more" : "")})";
        return (names, detail, rows);
    }

    /// <summary>Full tooltip text for an item, FETCHING from the wiki when the cache is
    /// empty — the Loot breakout's hover asks for this deliberately (David: mouse-over
    /// should just show the item info). One bounded lookup, cached for a week.</summary>
    internal async Task<string?> FetchItemTooltip(string name)
    {
        var r = await _wikiItems.LookupAsync(name);
        return r.Item is { StatsLines.Count: > 0 } info
            ? string.Join("\n", info.StatsLines)
            : null;
    }

    /// <summary>Open an item's eqlwiki page in the default browser — the search URL
    /// lands on the page itself on an exact title match (MediaWiki "Go"), and on
    /// search results otherwise, so a rename never strands the user on a 404.</summary>
    internal static void OpenWikiPage(string itemName)
    {
        var url = "https://eqlwiki.com/index.php?search="
            + Uri.EscapeDataString(EqlWikiItemService.NormalizeTitle(itemName));
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Re-derives the height caps from the monitor the widget currently
    /// occupies (see MonitorMetrics — primary-only caps halved the widget on portrait
    /// secondary screens, discussion #31).</summary>
    private void UpdateHeightCaps()
    {
        if (MonitorMetrics.WorkAreaFor(this) is not { } work) return;
        MaxHeight = Math.Max(200, work.Height - 20);
        ApplySectionMaxHeight(Math.Max(120, work.Height - 160));
    }

    /// <summary>The section list's height: automatic (fit the monitor) unless the
    /// bottom-edge grip chose one (Reddit ask, 2026-08-09 — taller or shorter without
    /// rescaling text). The choice lives in pre-scale units so it survives scale
    /// changes; the monitor's cap always wins.</summary>
    private double _sectionAutoCap = double.MaxValue;

    /// <summary>SectionScroll sits under the UI-scale LayoutTransform, so its MaxHeight
    /// is in pre-scale units while the monitor cap arrives in screen pixels. The
    /// conversion lives in WidgetMetrics, where it is unit-tested (#144).</summary>
    private void ApplySectionMaxHeight(double? autoCap = null)
    {
        if (autoCap is { } cap) _sectionAutoCap = cap;
        SectionScroll.MaxHeight = EQBuddy.UI.Shared.WidgetMetrics.SectionMaxHeight(
            _sectionAutoCap, _settings.ContentHeight, _settings.UiScale);
    }

    // Same absolute-cursor discipline as the scale grip: the window resizes under the
    // cursor mid-drag, so accumulating DragDelta would feed back and jitter.
    private double _heightDragCursorY, _heightDragStart;

    private void OnHeightGripStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _heightDragCursorY = CursorY();
        _heightDragStart = SectionScroll.ActualHeight;
    }

    // ---- #112 (Frankthetankk): the widget's own footprint, on the record ----
    private readonly System.Diagnostics.Process _self = System.Diagnostics.Process.GetCurrentProcess();
    private DateTime _perfSampledAt;
    private TimeSpan _perfCpuAt;

    /// <summary>CPU% (share of ALL cores, so 100% = the whole machine) and working
    /// set, sampled every 3 s from the process's own counters — cheap enough that
    /// measuring the app doesn't meaningfully show up in the measurement. Off by
    /// default; the label collapses without leaving a gap.</summary>
    private void UpdatePerfStats()
    {
        if (!_settings.ShowPerfStats)
        {
            if (PerfLabel.Visibility != Visibility.Collapsed)
                PerfLabel.Visibility = Visibility.Collapsed;
            return;
        }
        var now = DateTime.UtcNow;
        if ((now - _perfSampledAt).TotalSeconds < 3) return;
        try
        {
            _self.Refresh();
            var cpu = _self.TotalProcessorTime;
            if (_perfSampledAt != default)
            {
                // Through UI.Shared, and fixed-width, for the reason #173 found on the
                // Avalonia side: this string used to grow a character at 9→10% or
                // 999→1000 MB, and the widget sizes itself to its contents, so a
                // diagnostic readout resized a real always-on-top window every three
                // seconds forever. Harmless on Windows — but this is the hand-copied
                // inline arithmetic that carried #122 and #152 to Linux, so it goes
                // through the same tested helper rather than staying a near-copy.
                PerfLabel.Text = EQBuddy.UI.Shared.PerfReadout.Format(
                    EQBuddy.UI.Shared.PerfReadout.CpuPercent(
                        cpu - _perfCpuAt, now - _perfSampledAt, Environment.ProcessorCount),
                    _self.WorkingSet64);
                PerfLabel.Visibility = Visibility.Visible;
            }
            _perfSampledAt = now;
            _perfCpuAt = cpu;
        }
        catch (Exception ex) { App.LogError(ex); _settings.ShowPerfStats = false; }
    }

    /// <summary>The one-line body of a card that has nothing yet — the card stays
    /// where Options put it (David's verdict: "show what I've selected to see"),
    /// and the line says what will fill it.</summary>
    private static TextBlock EmptyCardLine(string text)
    {
        var line = DesignSystem.Text(Tok.TypeRole.Caption, text);
        line.TextWrapping = TextWrapping.Wrap;
        line.Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceXxs);
        return line;
    }

    /// <summary>The grip's tooltip states what a drag can actually do RIGHT NOW —
    /// with every card already visible, dragging down is a no-op, and a control
    /// that silently does nothing reads as broken (David's 1.66.1 retest). The
    /// cards themselves aren't hidden height: Buffs/Raids appear with content.</summary>
    private void OnHeightGripEnter(object sender, MouseEventArgs e)
    {
        var scrolling = SectionsPanel.ActualHeight > SectionScroll.ActualHeight + 1;
        HeightGrip.ToolTip = scrolling
            ? "Drag down to show more cards (the list is scrolling); drag up to shorten. Double-click: back to automatic."
            : "The widget is sizing itself automatically — everything you've selected in Options is shown. " +
              "Drag up if you'd rather have it shorter (the list scrolls); double-click returns to automatic.";
    }

    private void OnHeightGripDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        // Cursor moves in screen units; the list lives under the scale transform.
        _settings.ContentHeight = EQBuddy.UI.Shared.WidgetMetrics.ContentHeightFromDrag(
            _heightDragStart, CursorY() - _heightDragCursorY, _settings.UiScale);
        ApplySectionMaxHeight();
    }

    private void OnHeightGripCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        _settings.Save();

    private void OnHeightGripReset(object sender, MouseButtonEventArgs e)
    {
        _settings.ContentHeight = double.NaN;
        ApplySectionMaxHeight();
        _settings.Save();
    }

    /// <summary>The Combat card's session-pace sparkline (2026-08-11): damage per
    /// calendar minute across the last 30, zeros filled in — quiet minutes stay flat
    /// instead of being edited out, because pacing is the honest story. Repainted on
    /// the shared tick while the card is expanded; ~30 points of Polyline is free.</summary>
    private void PaintCombatSpark(StatsSnapshot s)
    {
        var pts = s.DamageTimeline;
        if (pts.Count < 2) { CombatSparkHost.Visibility = Visibility.Collapsed; return; }
        var end = pts[^1].Time;
        var perMinute = pts.ToDictionary(p => p.Time, p => p.Damage);
        var series = new List<long>();
        for (var m = 29; m >= 0; m--)
            series.Add(perMinute.GetValueOrDefault(end.AddMinutes(-m)));
        if (series.Count(v => v > 0) < 2) { CombatSparkHost.Visibility = Visibility.Collapsed; return; }
        CombatSparkHost.Visibility = Visibility.Visible;

        var w = CombatSparkHost.ActualWidth > 20 ? CombatSparkHost.ActualWidth : 300;
        const double h = 30;
        var max = Math.Max(1, series.Max());
        var xs = new double[series.Count];
        var ys = new double[series.Count];
        var peakIdx = 0;
        for (var i = 0; i < series.Count; i++)
        {
            xs[i] = w * i / (series.Count - 1);
            ys[i] = h - h * series[i] / max;
            if (series[i] > series[peakIdx]) peakIdx = i;
        }
        // Same monotone-cubic smoothing as the fight timeline (the approved chart
        // pass): curved, never overshooting — sampled densely into the Polyline so
        // the XAML stays a Polyline.
        // 3 samples/segment: visually identical at sparkline size, half the points
        // pushed through the Freezable collections each combat second.
        var line = new PointCollection();
        foreach (var (px, py) in EQBuddy.UI.Shared.MonotoneCurve.Sample(xs, ys, samplesPerSegment: 3))
            line.Add(new Point(px, py));
        CombatSpark.Points = line;
        var fill = new PointCollection(line) { new(w, h + 2), new(0, h + 2) };
        CombatSparkFill.Points = fill;
        if (CombatSparkFill.Fill is null)
        {
            var you = ((SolidColorBrush)FindResource("ChartYouBrush")).Color;
            CombatSparkFill.Fill = new LinearGradientBrush(
                Color.FromArgb(0x40, you.R, you.G, you.B),
                Color.FromArgb(0x00, you.R, you.G, you.B), 90);
        }
        CombatSparkPeak.Visibility = Visibility.Visible;
        CombatSparkPeak.Margin = new Thickness(
            Math.Max(0, xs[peakIdx] - 3), Math.Max(0, ys[peakIdx] - 3), 0, 0);
        CombatSparkHost.ToolTip =
            $"Damage per minute, last 30 — hottest minute {max:N0} at {end.AddMinutes(peakIdx - 29):HH:mm}";
    }

    /// <summary>#89 (jeremycranfill): the fight as a Discord-ready code block on the
    /// clipboard — the official Discord bans image sharing, so parses travel as text.</summary>
    private void OnCopyFight(object sender, RoutedEventArgs e)
    {
        var s = CurrentSnapshot();
        if (s.LastFight is not { } f) return;
        try
        {
            Clipboard.SetText(EQBuddy.UI.Shared.FightExport.ToText(
                f, Identity.Character, $"v{UpdateChecker.CurrentVersion}",
                EQBuddy.UI.Shared.FightExport.DeathsDuring(f.Start, f.DurationSeconds, s.Deaths)));
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Recent raw log messages for the Options "rule from a recent line" picker.</summary>
    internal List<(DateTime Time, string Message)> RecentLogLines() => _stats.RecentLines();

    private FightTimelineWindow? _timelineWindow;

    private void OnOpenTimeline(object sender, RoutedEventArgs e) => OpenFightTimeline();

    /// <summary>The fight timeline (⧗): one window, re-fronted if already open —
    /// reachable from the Combat card and the Damage breakout alike.</summary>
    internal void OpenFightTimeline()
    {
        if (_timelineWindow is { IsLoaded: true } open) { open.Activate(); return; }
        _timelineWindow = new FightTimelineWindow(_settings, TimelineSource)
        { SourceVersion = () => _stats.CurrentVersion };
        _timelineWindow.Show();
    }

    /// <summary>The timeline's data pull, called on its 1 s tick: the current/last
    /// pull plus the journal slice under it. A live fight's window extends a couple
    /// of seconds so the newest events aren't clipped by the running duration.</summary>
    private (LastFightInfo?, List<GameEvent>, string) TimelineSource()
    {
        var s = CurrentSnapshot();
        if (s.LastFight is not { } f || f.Start == default) return (null, [], "");
        var end = f.Start.AddSeconds(f.DurationSeconds + (f.InProgress ? 2 : 0));
        return (f, _stats.JournalWindow(f.Start, end), s.PetName);
    }

    private void OnAaAllToggled(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _settings.ShowAllAAs = !_settings.ShowAllAAs;
        _settings.Save();
    }

    private void OnNextUnlocksToggled(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _settings.ShowNextUnlocks = !_settings.ShowNextUnlocks;
        _settings.Save();
        RefreshUi();
    }

    /// <summary>Classes for level-unlock filtering: the Quest Tracker's picked classes,
    /// falling back to the combat-inferred class — the Gear Locker rule (#104). Buff-set
    /// assembly (#120 stage 2) reads the same source via BuffSetClassSource.</summary>
    private IReadOnlyList<string> UnlockClasses(StatsSnapshot s) => BuffSetClassSource(s).Classes;

    /// <summary>The Progress breakout's ding pull, and the header cue's. Both go through
    /// the CARD now: the memo moved with the surface that computes it (Gate 5b), so this
    /// is a forward rather than a second copy — two memos over one catalog scan is the
    /// "one entry, two sources" trap with a performance bill attached.</summary>
    internal LevelUnlockSet ProgressDingUnlocks(StatsSnapshot s) => _unlocks.Ding(s);

    /// <summary>The next-milestone preview, likewise forwarded. The card and the breakout
    /// must agree on where "At N:" starts (trap 4).</summary>
    internal (int Level, LevelUnlockSet Unlocks)? NextUnlockPreview(StatsSnapshot s) =>
        _unlocks.Next(s);

    // The three static unlock helpers that used to live here moved to
    // UI.Shared/LevelUnlockRows.cs. They were pure functions on a WINDOW class, which is
    // why the Progress breakout had to call MainWindow.UnlockRows to draw its own list —
    // §11.9's "lifting files without moving dependencies" in miniature. They are
    // unit-tested now (LevelUnlockRowsTests), which nothing here could be.

    /// <summary>Click handler for a merged unlock list (the Loot rows' click = wiki
    /// idiom). Only the NAVIGATION stays in the window; which page to open is
    /// <see cref="LevelUnlockRows.WikiPageFor"/>'s decision.</summary>
    internal static Action<string> UnlockClick(LevelUnlockSet set) =>
        name => OpenWikiPage(LevelUnlockRows.WikiPageFor(set, name));

    private void OnOpenWebsite(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://github.com/DranakCorps-bot/EQBuddy") { UseShellExecute = true });

    /// <summary>Mez chips: who's asleep, wake-up countdown ("?" until the spell's
    /// duration is known), warning tint inside the last tick. Same-named entries are
    /// numbered — "orc pawn (2)" — since the log can't tell the creatures apart
    /// (issue #32 asked for separate timers rather than one merged chip).</summary>
    private List<SpawnChip> MezChips(DateTime now)
    {
        var states = _mezTracker.Snapshot(now);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return states.Select(m =>
        {
            var n = seen[m.Target] = seen.GetValueOrDefault(m.Target) + 1;
            var dupe = states.Count(x => x.Target.Equals(m.Target, StringComparison.OrdinalIgnoreCase)) > 1;
            var remaining = m.RemainingSeconds(now);
            var text = remaining is { } r
                ? $"{(int)r / 60}:{(int)r % 60:00}"
                : "?";
            return new SpawnChip(
                Zone: "", Name: dupe ? $"{m.Target} ({n})" : m.Target, CountdownText: text,
                IsDue: remaining is <= 6,
                Detail: $"{m.Spell} by {m.Caster} · landed {m.LandedAt:h:mm:ss tt}",
                Icon: "Moon")
            {
                // Elapsed share for the gauge; the mez view draws the REMAINING side
                // (a draining bar, like a buff), so 1 - this.
                Fraction = m.ExpiresAt is { } exp && (exp - m.LandedAt).TotalSeconds is > 0 and var dur
                    ? Math.Clamp((now - m.LandedAt).TotalSeconds / dur, 0, 1)
                    : null,
            };
        }).ToList();
    }

    /// <summary>
    /// The Buffs card: every buff believed active on you, soonest-fading first, with
    /// a countdown. "est" marks a wiki-base duration (ranks and AAs lengthen buffs;
    /// a natural fade teaches the real number and the label drops). Unresolved
    /// landings ("You feel different." with nobody seen casting) show the line
    /// itself and the longest candidate duration — honest range, never a guess.
    /// </summary>
    /// <summary>Per-tick clock TextBlocks + their buff labels, so a tick with an
    /// unchanged buff SET updates text in place instead of rebuilding rows (the mez
    /// window's signature idiom — 2026-08-12 tuning pass).</summary>
    private readonly List<(TextBlock Clock, string Label)> _buffClocks = [];
    private string _buffsSignature = "";

    private void RenderBuffs(StatsSnapshot snap)
    {
        if (_settings.HiddenSections.Contains("buffs")) return;   // layout collapsed it
        BuffsSection.Visibility = Visibility.Visible;
        var count = _buffTracker.ActiveCount;   // header needs a number, not a list
        BuffsHeader.Text = count.ToString();
        if (!BuffsSection.IsExpanded) return;
        var now = DateTime.Now;
        var buffs = count > 0 ? _buffTracker.Snapshot(now) : [];

        // The buff set's honesty line (#120): evaluated against the FULL active list,
        // before the expiring-only filter — the set cares what's up, not what's shown.
        // Stage 2: the set is ASSEMBLED per class combination; the line itself is
        // unchanged in look.
        var set = AssembledBuffSet(BuffSetClassSource(snap).Classes);
        List<BuffSetEntryState> setStates = set.Count > 0 ? EvaluateBuffSet(set, buffs, now) : [];
        var setMissing = setStates.Where(s => s.Status == BuffSetStatus.Missing).Select(s => s.Spell).ToList();
        var setNotSeen = setStates.Where(s => s.Status == BuffSetStatus.NotSeen).Select(s => s.Spell).ToList();
        var setExpiring = setStates.Where(s => s.Status == BuffSetStatus.Expiring).Select(s => s.Spell).ToList();
        // Stage 3 (#120): new-buff-unlock suggestions ride the same card — rows only
        // while suggestions exist, never a popup (David's UX rules).
        var suggestions = BuffSuggestionsFor(snap, set);

        // Expiring-only mode (David): the card stays quiet until a buff is inside the
        // warning window — "tell me when it matters", with the rest counted honestly.
        var quiet = 0;
        if (_settings.BuffTimersExpiringOnly && buffs.Count > 0)
        {
            var warn = Math.Max(10, _settings.BuffWarnSeconds);
            var urgent = buffs.Where(b => b.RemainingSeconds(now) is { } r && r <= warn).ToList();
            quiet = buffs.Count - urgent.Count;
            buffs = urgent;
        }

        var signature = string.Join("|", buffs.Select(b => b.Label + (b.Estimated ? "~" : ""))) + "·" + quiet
            + "§" + string.Join(",", setMissing) + "§" + string.Join(",", setNotSeen)
            + "§" + string.Join(",", setExpiring)
            + "§" + string.Join(",", suggestions.Select(x => x.Spell + "@" + x.Class));
        if (signature == _buffsSignature)
        {
            // Same rows, newer clocks: update text and urgency tint in place.
            for (var i = 0; i < _buffClocks.Count && i < buffs.Count; i++)
            {
                var remaining = buffs[i].RemainingSeconds(now);
                _buffClocks[i].Clock.Text = ClockText(remaining, buffs[i].Estimated);
                _buffClocks[i].Clock.SetResourceReference(TextBlock.ForegroundProperty,
                    remaining is < 60 ? "WarnBrush" : "DimBrush");
            }
            return;
        }
        _buffsSignature = signature;
        _buffClocks.Clear();

        BuffsPanel.Children.Clear();
        if (buffs.Count == 0)
        {
            BuffsPanel.Children.Add(EmptyCardLine(_settings.BuffTimersExpiringOnly && quiet > 0
                ? $"{quiet} running quietly — timers appear at {Math.Max(10, _settings.BuffWarnSeconds):0}s left."
                : "Nothing running — a buff landing on you starts its countdown here."));
            AddBuffSetLine(setMissing, setNotSeen, setExpiring);
            AddBuffSuggestionRows(suggestions);
            return;
        }
        foreach (var b in buffs)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock
            {
                Text = b.Label, FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                Foreground = (Brush)FindResource("TextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = (b.Candidates.Length > 1
                              ? "One of: " + string.Join(", ", b.Candidates) + " · "
                              : "")
                          + (b.Caster.Length > 0 ? $"cast by {b.Caster} · " : "")
                          + $"landed {b.LandedAt:h:mm:ss tt}"
                          + (b.Estimated ? " · est = wiki base; a natural fade teaches your real duration" : ""),
            };
            row.Children.Add(name);
            var remaining = b.RemainingSeconds(now);
            var clock = new TextBlock
            {
                Text = ClockText(remaining, b.Estimated),
                FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            };
            clock.SetResourceReference(TextBlock.ForegroundProperty,
                remaining is < 60 ? "WarnBrush" : "DimBrush");
            Grid.SetColumn(clock, 1);
            row.Children.Add(clock);
            BuffsPanel.Children.Add(row);
            _buffClocks.Add((clock, b.Label));
        }
        AddBuffSetLine(setMissing, setNotSeen, setExpiring);
        AddBuffSuggestionRows(suggestions);

        static string ClockText(double? remaining, bool estimated) => remaining is { } r
            ? $"{(int)r / 60}:{(int)r % 60:00}{(estimated ? " est" : "")}"
            : "?";
    }

    // ---- buff set (#120, Frankthetankk; see BuffSetEvaluator for the honesty rules) ----

    /// <summary>Buff sets are per character — the same "name_server" key the AA ledger
    /// uses. Empty until the log names the character; the Options editor says so
    /// instead of silently saving into nowhere.</summary>
    internal string BuffSetKey => _stats.LedgerCharacterKey;
    internal string BuffSetCharacterName => _stats.CharacterName ?? "";

    /// <summary>The active class combination for buff-set assembly (#120 stage 2), and
    /// whether it was picked or read: the Quest Tracker's picked classes, falling back
    /// to the combat-inferred class — the Gear Locker rule (#104). No /who parsing
    /// exists in the log pipeline (the #120 thread's open question stays open), so
    /// this is the honest signal the app already has, and every surface that shows
    /// the combination says which source it came from.</summary>
    internal (IReadOnlyList<string> Classes, bool Picked) BuffSetClassSource(StatsSnapshot s)
    {
        var picked = QuestLedger?.ClassesFor(QuestCharacterKey) ?? [];
        if (picked.Count > 0) return (picked, true);
        return s.InferredClass is { Length: > 0 } inf ? ([inf], false) : ([], false);
    }

    /// <summary>The assembled set (#120 stage 2, Frankthetankk): the "(any class)"
    /// bucket plus every active class's picks — swap one class and the others' picks
    /// survive, exactly the requester's design.</summary>
    internal List<string> AssembledBuffSet(IReadOnlyList<string> classes) =>
        BuffSetKey is { Length: > 0 } key
            ? BuffSetStore.Assemble(_settings.BuffSetsByClass.GetValueOrDefault(key), classes)
            : [];

    /// <summary>Per-class sections with each entry's live honesty state — the Buff Set
    /// breakout's content (#120 stage 2). Sections come from the active combination
    /// (empty ones included: they're where the breakout's editor adds) PLUS any parked
    /// bucket with stored picks, because the class picker offers every class and a
    /// pick you cannot see is a pick you cannot remove (#120, Frankthetankk).</summary>
    internal List<(string Class, List<BuffSetEntryState> Entries)> BuffSetSectionStates(
        StatsSnapshot s, DateTime now)
    {
        if (BuffSetKey is not { Length: > 0 } key) return [];
        var active = _buffTracker.Snapshot(now);
        return BuffSetStore.EditableSections(
                _settings.BuffSetsByClass.GetValueOrDefault(key), BuffSetClassSource(s).Classes)
            .Select(r => (r.Section.Class, EvaluateBuffSet([.. r.Section.Spells], active, now)))
            .ToList();
    }

    /// <summary>Set edits repaint the card immediately — a change that waits for the
    /// next tick reads as a silent no-op, and silent no-ops read as broken.</summary>
    internal void RepaintBuffs()
    {
        _buffsSignature = "";
        RenderBuffs(CurrentSnapshot());
    }

    /// <summary>Stage 2 grew a second editor (the breakout); both write the same
    /// per-class storage, so an edit from either repaints the card AND the other
    /// editor at once — David's rule, same as above.</summary>
    internal void OnBuffSetEdited()
    {
        RepaintBuffs();
        if (_optionsWindow is { IsLoaded: true } ow) ow.RefreshBuffSetEditor();
        if (_breakouts.TryGetValue(BreakoutKind.Buffs, out var b) && b.IsVisible)
            b.RefreshBuffSet(CurrentSnapshot());
    }

    /// <summary>The set editor's seen-first ranking: buffs YOU were seen casting this
    /// session, plus buffs whose real duration was ever learned (evidence of use from
    /// past sessions on this install).</summary>
    internal IReadOnlyCollection<string> SeenBuffCasts()
    {
        var seen = new HashSet<string>(_buffTracker.SetSights().OwnCasts, StringComparer.OrdinalIgnoreCase);
        foreach (var spell in _buffTracker.LearnedDurations.Keys) seen.Add(spell);
        return seen;
    }

    private List<BuffSetEntryState> EvaluateBuffSet(List<string> set, List<BuffState> active, DateTime now)
    {
        var sights = _buffTracker.SetSights();
        // Reuses the Buffs card's existing warn threshold — stage 1 adds no second knob.
        return BuffSetEvaluator.Evaluate(set, active, sights.Landings, sights.Fades,
            now, Math.Max(10, _settings.BuffWarnSeconds));
    }

    // ---- stage 3 (#120, Frankthetankk): suggestions + the lost-buff history ----

    /// <summary>The lost-buff history — the Buffs breakout's fold reads it.</summary>
    internal BuffLossLog BuffLosses => _buffLossLog;

    /// <summary>Per-tick loss detection (#120 stage 3): the assembled set's evaluated
    /// states go to the loss log, which records transitions to Missing with their
    /// cause. Waits for the initial ingest — mid-replay, an "expired" would be
    /// stamped with wall-clock time hours after the fact; replayed fades carry their
    /// own log times and the log's first look picks them up instead.</summary>
    private void ObserveBuffLosses(StatsSnapshot s)
    {
        if (!_watcher.InitialIngestDone) return;
        var now = DateTime.Now;
        var set = AssembledBuffSet(BuffSetClassSource(s).Classes);
        _buffLossLog.Observe(
            set.Count > 0 ? EvaluateBuffSet(set, _buffTracker.Snapshot(now), now) : [], now);
    }

    /// <summary>New-buff-unlock suggestions for the session's latest ding (#120
    /// stage 3): buff-shaped unlocks the assembled set doesn't cover, minus this
    /// character's dismissals. A new RANK of a set spell folds into the same slot
    /// (rank-folded identity everywhere) and never appears here.</summary>
    internal List<BuffSuggestion> BuffSuggestionsFor(StatsSnapshot s, List<string> assembled) =>
        BuffSetKey is { Length: > 0 } key
            ? BuffSuggestions.Compute(ProgressDingUnlocks(s).Spells, assembled,
                BuffSuggestions.DismissedFor(_settings.BuffSuggestionDismissed, key))
            : [];

    /// <summary>✓ on a suggestion: the spell joins the gaining class's bucket — the
    /// same storage either editor writes — and every surface repaints at once. The
    /// suggestion row disappears because the set now covers it, not by memory.</summary>
    internal void AcceptBuffSuggestion(BuffSuggestion sug)
    {
        if (BuffSetKey is not { Length: > 0 } key) return;
        BuffSetStore.Add(_settings.BuffSetsByClass, key, sug.Class, sug.Spell);
        _settings.Save();
        OnBuffSetEdited();
    }

    /// <summary>✕ on a suggestion: remembered per character per base spell name,
    /// never re-asked. Repaints card and breakout mirror immediately — a dismissal
    /// that waits for the next tick reads as a silent no-op.</summary>
    internal void DismissBuffSuggestion(BuffSuggestion sug)
    {
        if (BuffSetKey is not { Length: > 0 } key) return;
        if (BuffSuggestions.Dismiss(_settings.BuffSuggestionDismissed, key, sug.Spell))
            _settings.Save();
        OnBuffSetEdited();
    }

    /// <summary>The "missing:" line (#120): appears ONLY when a set buff isn't cleanly
    /// up, and disappears entirely when everything is. Three visibly different claims:
    /// missing (seen fading, or timer ran out), expiring (inside the warn window), and
    /// not seen (no landing line this session — it may be up from before the log was
    /// watched; we can't know, and never pretend to).</summary>
    private void AddBuffSetLine(List<string> missing, List<string> notSeen, List<string> expiring)
    {
        if (missing.Count == 0 && notSeen.Count == 0 && expiring.Count == 0) return;
        var line = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, Tok.SpaceXs, 0, 0),
            ToolTip = "Your buff set. missing = EQBuddy saw it fade this session (or its timer ran out). "
                + "expiring = still up, inside the warn window. "
                + "not seen = no landing line this session — it may still be up from before "
                + "EQBuddy was watching; the log can't tell, so this stays a separate state. "
                + "The set is assembled from your active classes' picks plus (any class). "
                + "Edit it in Options → Alerts & chips, or in the Buff set breakout.",
        };
        void Add(string label, List<string> names, string brush, bool italic = false)
        {
            if (names.Count == 0) return;
            if (line.Inlines.Count > 0)
            {
                var sep = new Run(" · ");
                sep.SetResourceReference(TextElement.ForegroundProperty, "DimBrush");
                line.Inlines.Add(sep);
            }
            var run = new Run(label + string.Join(", ", names));
            if (italic) run.FontStyle = FontStyles.Italic;
            run.SetResourceReference(TextElement.ForegroundProperty, brush);
            line.Inlines.Add(run);
        }
        // No warning sign in front of "missing". The three labels are parallel states of
        // one line and only this one wore a glyph, so it read as a fourth channel that
        // said nothing the word and the warn ink did not already say — and it is a box on
        // a Wine prefix. An InlineUIContainer could carry a vector here, but not one that
        // stays aligned through a wrap.
        Add("missing: ", missing, "WarnBrush");
        Add("expiring: ", expiring, "AccentBrush");
        Add("not seen: ", notSeen, "DimBrush", italic: true);
        BuffsPanel.Children.Add(line);
    }

    /// <summary>New-buff-unlock suggestion rows (#120 stage 3, Frankthetankk): one dim
    /// row per genuinely new buff line the ding made available — ✓ adds it to the
    /// gaining class's bucket, ✕ dismisses for good (per character). Present only
    /// while suggestions exist; never auto-added — the player decides everything.</summary>
    private void AddBuffSuggestionRows(List<BuffSuggestion> suggestions)
    {
        foreach (var sug in suggestions)
        {
            var row = new Grid { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new TextBlock
            {
                Text = $"new buff at your level — add {sug.Spell} to {sug.Class}?",
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Your level-up made this buff available (the Progress card's "
                    + "\"New at level\" list). The tick adds it to that class's set bucket; "
                    + "the cross never asks again for this character. A new RANK of a buff "
                    + "already in your set folds into the same slot and is never "
                    + "suggested — only genuinely new lines appear here.",
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            row.Children.Add(text);
            row.Children.Add(SuggestionTick("Check", "GoodBrush",
                $"Add {sug.Spell} to your {sug.Class} set", 1, () => AcceptBuffSuggestion(sug)));
            row.Children.Add(SuggestionTick("Close", "DimBrush",
                "Dismiss — never suggest this buff for this character again", 2,
                () => DismissBuffSuggestion(sug)));
            BuffsPanel.Children.Add(row);
        }
    }

    /// <summary>Accept / dismiss on a buff-suggestion row.
    ///
    /// A real <see cref="DesignSystem.InlineIconButton"/> rather than a click-handled
    /// glyph: the tick and the cross were TextBlocks, which hit-test across their whole
    /// layout rect, and the drawn strokes of a vector do not — that is #211 exactly, on a
    /// pair of controls where a missed click either adds a buff you didn't want or fails
    /// to silence a suggestion you're tired of. The button also makes them
    /// keyboard-reachable, which the TextBlocks never were.</summary>
    private static Button SuggestionTick(string icon, string brush, string tip, int column, Action act)
    {
        var button = DesignSystem.InlineIconButton(icon, tip, (_, _) => act(), brush);
        Grid.SetColumn(button, column);
        return button;
    }

    /// <summary>Everything the fight-side chip stack shows: mez chips and slow chips,
    /// each behind its own Options toggle, sharing one window and saved position.</summary>
    private List<SpawnChip> FightChips(DateTime now)
    {
        var chips = _settings.MezChipsEnabled ? MezChips(now) : [];
        if (SlowChipsVisible(now)) chips.AddRange(SlowChips(now));
        // Placement preview (#94 follow-up): the stack only exists while a mez or
        // slow is live, so there was no way to park it BEFORE the first mid-fight
        // debuff. While Options is open, an empty stack shows one draggable
        // placeholder — same idea as the alert tile's placement mode.
        if (chips.Count == 0 && _optionsWindow is { IsLoaded: true })
            chips.Add(new SpawnChip(Zone: "", Name: "drag me — chips appear here",
                CountdownText: "", IsDue: false,
                Detail: "Placement preview: mez and slow chips will stack at this "
                    + "spot. Drag it where you'll notice them; it disappears when "
                    + "Options closes.",
                Icon: "ChevronsDown"));
        return chips;
    }

    private bool SlowChipsVisible(DateTime now) =>
        _settings.SlowAlertEnabled
        && (!_settings.SlowAlertRaidOnly || _slowTracker.InRaid(now));

    /// <summary>Slow chips (#94): the debuff's honest % (a range when several slows
    /// share the landing line), time left when the wiki documents a duration, and the
    /// cure line in the tooltip — "how do I get rid of this" attached to the alert.</summary>
    private List<SpawnChip> SlowChips(DateTime now) =>
        _slowTracker.Snapshot(now).Select(s =>
        {
            var remaining = s.RemainingSeconds(now);
            var detail = string.Join(" · ", new[]
            {
                s.Spells.Length == 1 ? s.Spells[0] : "One of: " + string.Join(", ", s.Spells),
                s.CounterText,
                _slowTracker.CureLine(s),
                $"landed {s.LandedAt:h:mm:ss tt}",
            }.Where(part => part.Length > 0));
            return new SpawnChip(
                Zone: "", Name: EQBuddy.UI.Shared.SlowChipText.Label(s),
                CountdownText: remaining is { } r ? $"{(int)r / 60}:{(int)r % 60:00}" : "?",
                IsDue: false, Detail: detail + " · right-click to dismiss", Icon: "ChevronsDown")
            {
                Fraction = s.ExpiresAt is { } exp && (exp - s.LandedAt).TotalSeconds is > 0 and var dur
                    ? Math.Clamp((now - s.LandedAt).TotalSeconds / dur, 0, 1)
                    : null,
                OnDismiss = () => _slowTracker.Dismiss(s.Message),
            };
        }).ToList();

    /// <summary>
    /// A slow landed on the player, straight off the ingest thread. Speaks once per
    /// landing (never on the refresh of one already showing — a chain-slowing NPC must
    /// not turn the voice into a metronome); the chip itself appears via the 1 s tick.
    /// Suppressed during the startup replay like every other alert — a slow from an
    /// hour ago is history, not news.
    /// </summary>
    private void OnSlowLanded(SlowState state, bool isNew)
    {
        if (!isNew || !_watcher.InitialIngestDone) return;
        if (!_settings.SlowAlertEnabled || !_settings.SlowAlertSpoken) return;
        if (_settings.SlowAlertRaidOnly && !_slowTracker.InRaid(state.LandedAt)) return;
        var pct = state.PctMin == state.PctMax
            ? $"{state.PctMax} percent"
            : $"up to {state.PctMax} percent";
        EQBuddy.UI.Shared.SpokenAlerts.Speak($"Slowed {pct}");
    }

    private void CloseChips()
    {
        if (_chipsWindow is not { IsLoaded: true } cw) { _chipsWindow = null; return; }
        _chipsWindow = null;
        cw.Close();   // saves the stack position on the way out
    }

    /// <summary>The regen-tick line for healing surfaces: count always; estimate when a
    /// cast attributed the ticks (× the player's Options value when set, else wiki base —
    /// the log itself never carries an amount, so this stays labeled est.).</summary>
    internal string RegenLine(StatsSnapshot s)
    {
        if (s.RegenEstimatedHealed <= 0 || s.RegenSpell.Length == 0)
            return $"{s.RegenTicks} regen/hymn ticks (game logs no amounts for these)";
        var basis = _settings.RegenPerTickOverride > 0
            ? "your hp/tick from Options"
            : "wiki base — set your real hp/tick in Options";
        return $"{s.RegenSpell}: est. ~{s.RegenEstimatedHealed:N0} healed over {s.RegenTicks} ticks ({basis})";
    }

    /// <summary>Repaint the Loot card alone, from the snapshot we already have. Its two
    /// filter chips call this: a full RefreshUi recomputes nothing new (the memo hands
    /// back the same snapshot) and repaints every card, both breakouts and the whole
    /// mobile projection.</summary>
    internal void RepaintLootCard()
    {
        if (_latestSnapshot is { } s) _loot.Render(s);
    }

    private void OnPetAbilitiesToggled(object sender, MouseButtonEventArgs e)
    {
        _settings.ShowPetAbilities = !_settings.ShowPetAbilities;
        _settings.Save();
        RefreshUi();
        e.Handled = true;
    }

    private void OnSpawnsWindow(object sender, RoutedEventArgs e) => ShowSpawnsWindow();

    private QuestsWindow? _questsWindow;
    private DropsWindow? _dropsWindow;
    private WikiPackWindow? _wikiPackWindow;

    private void OnDropsWindow(object sender, RoutedEventArgs e)
    {
        if (_dropsWindow is not { IsLoaded: true })
            _dropsWindow = new DropsWindow(this);
        _dropsWindow.Update(_stats.Snapshot());
        _dropsWindow.Show();
        _dropsWindow.Activate();
    }

    /// <summary>#217 Ask 1 (Frankthetankk): the contribution pack is its own surface under
    /// Data &amp; imports now, not a button inside Drops by Creature. Drops keeps its live
    /// view — "is this trip worth it" is a different question from "what can I give the
    /// wiki".</summary>
    private void OnWikiPackWindow(object sender, RoutedEventArgs e) => ShowWikiPackWindow();

    /// <summary>Also reached from the ✦ marker on a Drops by Creature row, which used to
    /// copy the pack straight to the clipboard. Opening the surface instead is the point
    /// of the move: the marker says "there is something here", and the window says what.</summary>
    internal void ShowWikiPackWindow()
    {
        if (_wikiPackWindow is not { IsLoaded: true })
            _wikiPackWindow = new WikiPackWindow(this);
        _wikiPackWindow.Update(_stats.Snapshot());
        _wikiPackWindow.Show();
        _wikiPackWindow.Activate();
    }

    private ProgressWindow? _progressWindow;

    private void OnProgressWindow(object sender, RoutedEventArgs e) => ShowProgressWindow();

    /// <summary>Open (or front) the Progress window — the PROGRESS THEME's four tabs, and
    /// the only way to reach five surfaces that used to be five cards.</summary>
    internal void ShowProgressWindow(string? tab = null)
    {
        if (_progressWindow is not { IsLoaded: true })
        {
            _progressWindow = new ProgressWindow(this);
            _progressWindow.Show();
        }
        if (tab is { Length: > 0 }) _progressWindow.SetTab(tab);
        _progressWindow.Activate();
    }

    /// <summary>The five surfaces the Progress window hosts, freshly built for IT.
    ///
    /// The window cannot borrow the widget's — the widget has none any more — and even
    /// when it did, a UIElement has one parent, so a shared instance would be torn out of
    /// whichever host painted it last. This is the payoff for putting them on the
    /// <see cref="IWidgetCard"/> seam: "paint yourself from this snapshot" is a contract
    /// a second host can satisfy, and a view that took MainWindow could not have moved.
    ///
    /// One method rather than five, so this window's whole reach into the widget is one
    /// name. The seam's own warning applies (see <see cref="ICardContext"/>): if THIS
    /// starts growing, something is being pushed through the window that belongs in
    /// UI.Shared.</summary>
    internal ProgressSurfaceSet NewProgressSurfaces()
    {
        var motes = new MotesCardView(this); motes.Attach();
        var money = new MoneyCardView(this); money.Attach();
        var faction = new FactionCardView(); faction.Attach();
        return new ProgressSurfaceSet(
            Experience: new ProgressCardView(this, _settings,
                s => BuffSetClassSource(s).Classes,
                () => QuestLedger?.LevelFor(QuestCharacterKey) is > 0 and var lv ? lv : null),
            Money: money,
            Motes: motes,
            Faction: faction,
            Raids: new RaidsCardView(() => _raidLedger));
    }

    /// <summary>How many unlocks this session's ding opened — the Experience tab's badge
    /// needs the count, and the memo that answers it lives here.</summary>
    internal int ProgressDingUnlockCount(StatsSnapshot s) => ProgressDingUnlocks(s).Count;

    /// <summary>Turn a mini-dashboard stat on or off from somewhere that is not a star on
    /// a card header. The Progress theme took the xp, money and motes stars off the
    /// widget with their cards, and this is what the window's own stars call — the same
    /// two lines <see cref="OnStarChanged"/> runs, so there is one rule for what a star
    /// does and the breakouts still follow it.</summary>
    internal void SetMiniStat(string key, bool on)
    {
        if (on)
        {
            if (!_settings.MiniStats.Contains(key)) _settings.MiniStats.Add(key);
        }
        else
        {
            _settings.MiniStats.Remove(key);
        }
        _settings.Save();
        UpdateBreakouts(CurrentSnapshot());
    }

    private void OnQuestsWindow(object sender, RoutedEventArgs e) => ShowQuestsWindow();

    /// <summary>Open (or front) the Quest Tracker; with an item, jump straight to that
    /// item's quests — the 🗺 badge path from the Loot views.</summary>
    internal void ShowQuestsWindow(string? filterItem = null)
    {
        if (_questsWindow is not { IsLoaded: true })
        {
            _questsWindow = new QuestsWindow(this);
            _questsWindow.Show();
        }
        if (filterItem is { Length: > 0 }) _questsWindow.FilterToItem(filterItem);
        _questsWindow.Activate();
    }

    /// <summary>Single switch for the spawn-timer feature: the setting, the menu check,
    /// and the Options checkbox stay in lockstep whichever of them the user touched.
    /// Arming opens nothing — the chicklet stack appears from the next tick if timers
    /// are running; the full window only ever opens on demand.</summary>
    internal void SetTrackSpawns(bool on)
    {
        _settings.TrackSpawns = on;
        _settings.Save();
        if (_optionsWindow is { IsLoaded: true } ow) ow.SyncTrackSpawns(on);
        if (!on)
        {
            CloseChips();
            if (_spawnsWindow is { } w)
            {
                _spawnsWindow = null;   // cleared first so Closed handling can't loop
                if (w.IsLoaded) w.Close();
            }
        }
    }

    internal void ShowSpawnsWindow(string? zone = null)
    {
        if (_spawnsWindow is { IsLoaded: true })
        {
            _spawnsWindow.Activate();
            return;
        }
        var w = new SpawnsWindow(this, _spawnsVm, zone);
        w.Closed += (_, _) => { if (ReferenceEquals(_spawnsWindow, w)) _spawnsWindow = null; };
        _spawnsWindow = w;
        w.Show();
    }

    private void OnOptions(object sender, RoutedEventArgs e)
    {
        if (_optionsWindow is { IsLoaded: true })
        {
            _optionsWindow.Activate();
            return;
        }
        _optionsWindow = new OptionsWindow(this);
        // While Options is open, the alert tile shows in placement mode (draggable,
        // click-through off) so the user can position where alerts appear.
        _optionsWindow.Closed += (_, _) => _alertWindow?.ExitPlacement();
        _optionsWindow.Show();
        AlertTile.EnterPlacement();
    }

    /// <summary>Options via a non-event caller (the tray icon's menu).</summary>
    internal void ShowOptions() => OnOptions(this, new RoutedEventArgs());

    private void OnGear(object sender, RoutedEventArgs e)
    {
        if (RootBorder().ContextMenu is { } menu)
        {
            menu.PlacementTarget = GearBtn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private System.Windows.Controls.Border RootBorder() => RootBorderElement;

    private void OnChooseLogFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pick the EverQuest Legends Logs folder (contains eqlog_*.txt files)",
            InitialDirectory = _settings.LogFolder is { } cur && System.IO.Directory.Exists(cur)
                ? cur : Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
        };
        if (dlg.ShowDialog(this) != true) return;

        var picked = dlg.FolderName;
        // Accept the install root too — quietly step down into its Logs subfolder.
        var logsSub = System.IO.Path.Combine(picked, "Logs");
        if (!System.IO.Directory.EnumerateFiles(picked, "eqlog_*.txt").Any() &&
            System.IO.Directory.Exists(logsSub))
            picked = logsSub;

        _settings.LogFolder = picked;
        _settings.Save();
        _lastCharScan = DateTime.MinValue;
        FollowActiveCharacter();
    }

    private void OnAutoDetectLogFolder(object sender, RoutedEventArgs e)
    {
        _settings.LogFolder = LogWatcher.FindDefaultLogFolder();
        _settings.Save();
        _lastCharScan = DateTime.MinValue;
        FollowActiveCharacter();
    }

    // ---- archived-log review (#74, Snagglefern: "see what I can contribute") ----

    /// <summary>Path of the archive being replayed; null = live. While set, character
    /// follow stands down and nothing writes to session history — the review is a
    /// window onto the past, not a new session.</summary>
    private string? _reviewPath;

    private void OnReviewLog(object sender, RoutedEventArgs e)
    {
        if (_reviewPath is not null) { ExitReview(); return; }
        var archive = _settings.LogFolder is { } lf ? Path.Combine(lf, "archive") : null;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Review an archived log",
            Filter = "EQ logs (eqlog_*.txt)|eqlog_*.txt|All files (*.*)|*.*",
            InitialDirectory = archive is not null && Directory.Exists(archive)
                ? archive : _settings.LogFolder ?? "",
        };
        if (dlg.ShowDialog(this) == true) EnterReview(dlg.FileName);
    }

    private void EnterReview(string path)
    {
        // A pre-splitter log holds days of sessions; ask which one (#74 round two —
        // Snagglefern's 10 MB archive replayed as a 10-minute evening). Splitter
        // archives are one session each, so they skip the dialog entirely.
        List<LogSessionInfo> sessions;
        try { sessions = LogSessions.Scan(path); }
        catch (Exception ex) { App.LogError(ex); sessions = []; }
        LogSessionInfo? pick = null;
        if (sessions.Count > 1)
        {
            // Debug/screenshot hook: 1-based chronological index skips the dialog.
            pick = int.TryParse(Environment.GetEnvironmentVariable("EQBUDDY_REVIEW_SESSION"),
                    out var idx) && idx >= 1 && idx <= sessions.Count
                ? sessions[idx - 1]
                : SessionPickerWindow.Choose(this, Path.GetFileName(path), sessions);
            if (pick is null) return;   // cancelled
        }

        // The live session goes to history first, same as a character switch —
        // then the archiver stands down until we're back.
        _archiver.FinalizeActive(_stats.Snapshot(), "ReviewingArchive");
        _reviewPath = path;
        // Review is read-only (finding 4): the per-character ledgers stand down and
        // the two persisting watcher consumers detach — an archive replay must not
        // mint spawn timers or raid kills. The spawn-point ledger stays attached:
        // its per-zone archive carries no character identity and is replay-gated.
        _stats.StoresSuppressed = true;
        _watcher.Spawns = null;
        _watcher.Raids = null;
        _targetResults.Clear();
        _quests.ResetLootSeen();
        ClearGearAutoCheckSeen();
        if (pick is not null) _watcher.Select(path, pick.StartOffset, pick.EndOffset);
        else _watcher.Select(path);
        // A MenuItem has an Icon slot; the tick belongs in it rather than typed onto the
        // front of the header, where it is a glyph in a string and reads as part of the
        // words on a screen reader.
        ReviewLogItem.Header = "Reviewing an archive — return to live log";
        ReviewLogItem.Icon = DesignSystem.Icon("Check", "GoodBrush", size: Tok.IconInline);
        var when = pick is not null ? $" ({pick.Start:MMM d HH:mm})" : "";
        CharLabel.Text = $"REVIEWING {Path.GetFileName(path)}{when} — click here to go live";
        CharLabel.Foreground = (Brush)FindResource("WarnBrush");
        CharLabel.Cursor = Cursors.Hand;
        CharLabel.ToolTip = "Replaying a saved log. Drops by Creature and the wiki pack " +
            "show the reviewed session. Click to return to the live log.";
    }

    private void ExitReview()
    {
        ReviewLogItem.Header = "Review an archived log…";
        ReviewLogItem.Icon = null;
        CharLabel.Foreground = (Brush)FindResource("DimBrush");
        CharLabel.Cursor = null;
        CharLabel.ToolTip = "Follows whoever is actively playing (log file growth)";
        // Reattach the persistent consumers BEFORE the live Select, so its replay
        // rebuilds their state; their own high-water marks keep it idempotent.
        _stats.StoresSuppressed = false;
        _watcher.Spawns = _spawnTimers;
        _watcher.Raids = _raidLedger;
        // No finalize here: the reviewed session is already history. Follow just
        // re-selects whoever is live; the switch path sees review's CurrentPath but
        // _reviewPath clears below, so guard by handing follow a clean slate.
        _lastCharScan = DateTime.MinValue;
        if (_settings.LogFolder is { } lf && LogWatcher.MostRecentlyActive(lf) is { } active)
        {
            // Identity before Select, same as the switch path (audit finding 7).
            _archiver.SetIdentity(active.Server, active.Character);
            _watcher.Select(active.FilePath);
            CharLabel.Text = active.Display;
        }
        else
        {
            CharLabel.Text = "waiting for a character to log in…";
        }
        // Cleared only AFTER Select returns (finding 5): the SessionEnding guard
        // must stay armed while the archive replay can still roll a session over —
        // clearing first let those rollovers mint duplicate history rows.
        _reviewPath = null;
    }

    // Mouse DOWN, and handled: the title bar's OnDrag starts a DragMove on the same
    // press, which captures the mouse and eats any up-event this label would get.
    private void OnCharLabelClick(object sender, MouseButtonEventArgs e)
    {
        if (_reviewPath is null) return;
        ExitReview();
        e.Handled = true;
    }

    /// <summary>Switch to whoever is actively playing: the most recently written log.</summary>
    private void FollowActiveCharacter()
    {
        if (_reviewPath is not null) return;   // reviewing an archive — stay put (#74)
        ChooseLogFolderItem.ToolTip = _settings.LogFolder ?? "(no folder found)";
        if (_settings.LogFolder is null)
        {
            CharLabel.Text = "logs not found — right-click, Choose log folder";
            return;
        }
        var active = LogWatcher.MostRecentlyActive(_settings.LogFolder);
        if (active is null)
        {
            CharLabel.Text = "waiting for a character to log in…";
            return;
        }
        if (!string.Equals(active.FilePath, _watcher.CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            // Character switch: the outgoing character's session goes to history first
            // (SESSION-004: switches never merge data).
            if (_watcher.CurrentPath is not null)
                _archiver.FinalizeActive(_stats.Snapshot(), "CharacterChanged");
            // Identity BEFORE Select (audit finding 7): Select's background ingest can
            // hit a 60-minute-gap rollover before control returns here, and that
            // finalize must already carry the NEW character's name — the old ordering
            // archived the new character's first session under the old identity.
            _archiver.SetIdentity(active.Server, active.Character);
            _watcher.Select(active.FilePath);
            CharLabel.Text = active.Display;
            // Perf audit #9: these were session-lifetime by intent but PROCESS-lifetime
            // in fact — with review mode switching logs freely now, clear them with the
            // rest of the character state.
            _targetResults.Clear();
            _quests.ResetLootSeen();
            ClearGearAutoCheckSeen();
        }
    }

    private DateTime? _autoCheckSessionStart;

    private void ClearGearAutoCheckSeen()
    {
        _gearLootSeen.Clear();
        _gearCraftSeen.Clear();
        _gearUpgradeSeen.Clear();
    }

    /// <summary>Every EQBuddy surface is Topmost, but Windows keeps topmost windows
    /// in the order they claimed the band — an overlay created AFTER ours (Lossless
    /// Scaling's upscale surface was the field case, discussion #91) sits above the
    /// widget and WPF never re-asserts on its own. A periodic no-activate re-place
    /// lifts every visible EQBuddy window back to the top of the band; the overlay
    /// doesn't re-assert, so the widget stays visible. A handful of SetWindowPos
    /// calls every few seconds — free.</summary>
    private const int TopmostReassertSeconds = 5;
    private int _topmostTick;

    private void ReassertTopmost()
    {
        if (!_settings.KeepAboveOverlays) return;   // #91: opt out for capture setups
        if (++_topmostTick < TopmostReassertSeconds) return;
        _topmostTick = 0;
        foreach (Window w in Application.Current.Windows)
        {
            if (!w.Topmost || !w.IsVisible) continue;
            if (PresentationSource.FromVisual(w) is not System.Windows.Interop.HwndSource src) continue;
            Native.SetWindowPos(src.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }

    private void RefreshUi()
    {
        AnswerSecondLaunch();
        UpdateFocusHide();
        ReassertTopmost();
        _stats.RegenPerTickOverride = _settings.RegenPerTickOverride;

        // Spawn timers crossing zero: banner always, sound only if one is chosen. Runs
        // off the shared tick so a hidden window can't silence a camp.
        if (_questsWindow is { IsLoaded: true, IsVisible: true } qw) qw.MaybeRefresh();
        if (_progressWindow is { IsLoaded: true, IsVisible: true } pw) pw.MaybeRefresh();
        if (_dropsWindow is { IsLoaded: true, IsVisible: true } dw) dw.MaybeRefresh();
        if (_wikiPackWindow is { IsLoaded: true, IsVisible: true } wp) wp.MaybeRefresh();
        if (_mapWindow is { IsLoaded: true, IsVisible: true } mapw) mapw.MaybeRefresh();

        if (_settings.TrackSpawns)
        {
            // Sound only — no banner. The chip flipping to DUE is the visual, and a
            // banner on top of it was double notification (David's call). Each named
            // can carry its own sound; "Default" maps to Alarm — a camp popping
            // deserves a louder default than a loot ding (also David's call).
            foreach (var due in _spawnsVm.ConsumeDueAlerts(DateTime.Now))
                if (_spawnsVm.SoundFor(due.Zone, due.Name) is { } sound)
                    PlayAlertSound(sound);

            // Chicklets are the ambient face of spawn tracking: the stack exists exactly
            // while timers do — including alongside the full window, which is a browser,
            // not a replacement. No pop-open of the full window, ever (David's design).
            var hasTimers = !_hiddenForFocus && _spawnsVm.HasActiveTimers(DateTime.Now);
            if (hasTimers)
            {
                if (_chipsWindow is not { IsLoaded: true })
                {
                    _chipsWindow = new SpawnChipsWindow(this, _spawnsVm);
                    _chipsWindow.Show();
                }
                _chipsWindow.RefreshChips(DateTime.Now);
            }
            else
            {
                CloseChips();
            }
        }
        else
        {
            CloseChips();
        }

        // The mez stack lives its own life, independent of spawn tracking: it exists
        // exactly while a mez is believed active, in its own window (David's call —
        // mez chips park next to the fight, spawn chips are ambient). Optional since
        // the 2026-08-11 Reddit ask — a non-CC class never wants the stack.
        // Slow chips (#94) ride the same stack: both are "active effect, counting
        // down, parked next to the fight", and one window means one saved position.
        // Emptiness is probed cheaply first (2026-08-12 tuning pass): building the
        // full chip list twice a second to learn it was empty was pure churn.
        var chipsNow = DateTime.Now;
        // Options open = placement preview: the stack exists (with a placeholder if
        // empty) so it can be parked before the first real debuff (#94 follow-up).
        var chipPlacement = _optionsWindow is { IsLoaded: true }
            && (_settings.MezChipsEnabled || _settings.SlowAlertEnabled);
        var haveFightChips = !_hiddenForFocus
            && (chipPlacement
                || (_settings.MezChipsEnabled && _mezTracker.Any(chipsNow))
                || (SlowChipsVisible(chipsNow) && _slowTracker.Any(chipsNow)));
        if (haveFightChips)
        {
            if (_mezWindow is not { IsLoaded: true })
            {
                _mezWindow = new MezChipsWindow(_settings, FightChips, SetChipScale);
                _mezWindow.Show();
            }
            _mezWindow.RefreshChips(DateTime.Now);
        }
        else if (_mezWindow is { IsLoaded: true } mw)
        {
            _mezWindow = null;
            mw.Close();   // saves the stack position on the way out
        }

        // Every 5s: re-check which character's log is growing and follow them.
        if (DateTime.Now - _lastCharScan > TimeSpan.FromSeconds(5))
        {
            _lastCharScan = DateTime.Now;
            FollowActiveCharacter();
        }

        // Every 6 h (and shortly after startup): look for a newer installer in OneDrive.
        if (DateTime.Now - _lastUpdateCheck > TimeSpan.FromHours(6))
        {
            _lastUpdateCheck = DateTime.Now;
            CheckForUpdates(manual: false);
        }

        // Every 10 min: sweep stale logs and re-assert Log=1 (skipped while game runs).
        if (_settings.LogFolder is { } folder && DateTime.Now - _lastJanitorRun > TimeSpan.FromMinutes(10))
        {
            _lastJanitorRun = DateTime.Now;
            var prune = _settings.TruncateLogs;
            var archive = _settings.ArchiveLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(folder);
                if (prune) EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap,
                    archive: archive, archived: AnnounceArchive);
            });
        }

        UpdateLoggingStatus();

        if (_upToDateNoticeUntil != DateTime.MinValue && DateTime.Now > _upToDateNoticeUntil &&
            _pendingUpdate is null && !_installingUpdate)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            _upToDateNoticeUntil = DateTime.MinValue;
        }

        if (_watcher.LastError is { } err)
            App.LogError(err);

        var s = _stats.Snapshot(TimeSpan.FromMinutes(Math.Max(1, _settings.RecentWindowMinutes)),
            _settings.TrackedRules);
        _latestSnapshot = s;   // satellites reuse this tick's snapshot (perf audit #12)

        ProcessTrackedAlerts(s);

        // Every 5 min: checkpoint the active session so a crash loses little (RECOVERY-001).
        // Review replays are read-only — their sessions are already history (#74).
        if (_reviewPath is null && DateTime.Now - _lastCheckpoint > TimeSpan.FromMinutes(5))
        {
            _lastCheckpoint = DateTime.Now;
            _archiver.Checkpoint(s);
        }

        if (MiniRoot.Visibility == Visibility.Visible)
            UpdateMiniChips(s);
        // BEFORE the breakouts and the focus-hide gate: loss transitions must be
        // detected every tick, whatever's visible — a hidden Buffs card must not
        // mean a blind history (#120 stage 3) — and the Buffs breakout should show
        // this tick's losses, not last tick's.
        ObserveBuffLosses(s);
        UpdateBreakouts(s);

        // EQBuddy Mobile rides the same shared snapshot as every desktop card, and
        // must keep flowing while the widget hides for focus (the phone is exactly the
        // screen you look at then). Free unless a device is actually connected.
        //
        // The latency path is PumpCompanion, which pushes as soon as the session moves.
        // This call remains the reconciliation one: it is what keeps ForcedPushInterval
        // running through a camp so quiet that nothing bumps the version at all. Record
        // the version it covered, so the pump doesn't immediately repeat this push.
        _companionGate.Observe(s.Version);
        _companion.Tick(s, _spawnTimers, _stats.CharacterName ?? "", DateTime.Now);

        // Hidden while the game is unfocused: everything the player can't see stops
        // here — alerts, chips, timers, and checkpoints above already ran (perf
        // audit #1b: the full element rebuild used to run every second into a
        // window that wasn't even shown).
        if (_hiddenForFocus) return;

        ZoneText.Text = s.CurrentZone.Length > 0 ? s.CurrentZone : "—";
        // The by-zone gear view bakes "you're here"/hop counts into its headings —
        // zoning must repaint it or the card keeps claiming the old zone.
        if (_settings.GearGroupByZone && CurrentZoneName != s.CurrentZone)
            _gearChecklistDirty = true;
        CurrentZoneName = s.CurrentZone;
        var active = TimeSpan.FromSeconds(s.ActiveSeconds);
        SessionText.Text = s.SessionStart is { } start
            ? $"session {(int)s.Elapsed.TotalHours}:{s.Elapsed.Minutes:D2} · active {(int)active.TotalMinutes}m (since {start:h:mm tt})"
            : "waiting for log activity…";

        CombatHeader.Text = s.CurrentDps > 0
            ? $"{s.SessionDps:0} dps (now {s.CurrentDps:0})"
            : $"{s.SessionDps:0} dps";
        // KPI strip (2026-08-11): the headline numbers, always painted — current DPS
        // while fighting, session DPS between fights.
        KpiDps.Text = s.CurrentDps > 0 ? $"{s.CurrentDps:0}" : $"{s.SessionDps:0}";
        KpiKills.Text = $"{s.YourKillCount}";
        KpiLoot.Text = $"{s.LootTotal}";
        KpiXp.Text = $"{s.XpPerHour:0.#}%";
        KillsHeader.Text = s.PartyKillCount > 0 ? $"{s.YourKillCount} (+{s.PartyKillCount})" : $"{s.YourKillCount}";
        // Merges + crafts counted apart from drops (#131); the breakout's subheader
        // reports the same two numbers from the same place, so they cannot disagree.
        LootHeader.Text = EQBuddy.UI.Shared.LootPresentation.Header(
            s.LootTotal, s.CraftedTotal + s.FashionedTotal);
        // A session rollover empties the loot lists lazily, inside the same batch
        // that may carry the new session's first loot — inferring the reset from
        // emptied lists can miss that first same-name drop. The session identity
        // is the honest reset signal for every auto-check high-water mark.
        if (s.SessionStart != _autoCheckSessionStart)
        {
            _autoCheckSessionStart = s.SessionStart;
            _quests.ResetLootSeen();
            ClearGearAutoCheckSeen();
        }
        UpdateSkyQuestChecklist(s);
        UpdateGearChecklist(s);
        UpdateEpicQuestChecklist(s);
        // Remember the announced level per character — the "At N:" preview must survive
        // restarts and log truncation, and the log only says the number at the ding.
        if (s.LastLevel is { } announced && QuestLedger is { } lg && QuestCharacterKey.Length > 0
            && lg.LevelFor(QuestCharacterKey) != announced)
            lg.SetLevel(QuestCharacterKey, announced);
        // The ding's cue rides the header, visible while the card is closed: the header
        // is the only Progress surface that always shows, and clicking it opens the
        // card where the "New at level N" list waits (never a popup). Text built in
        // UI.Shared so the Progress breakout says exactly the same thing.
        // ONE line for five folded cards. Every number those five headers carried is in
        // it, which is the whole bargain of the fold: the glance survives, the five slots
        // do not. Assembled in UI.Shared so the Avalonia widget and EQBuddy Mobile say the
        // same thing (#210 — parity by shared module, never by feature list).
        ProgressHeader.Text = ProgressTheme.LauncherSummary(
            s, ProgressDingUnlocks(s).Count, _raidLedger.DefeatedCount());
        MiscHeader.Text = $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}";
        ApplySessionSubsections();

        // Perf audit #1: identical content was re-rendered every tick — hundreds of
        // fresh WPF elements per second during idle, the app's main steady-state
        // cost. Expanded sections now rebuild only when an event actually arrived;
        // a 10 s heartbeat keeps time-derived rates (xp/hr, coin/hr, recent-window
        // dps) honest during long AFKs. Everything above stays per-tick (the clock,
        // headers, chips, alerts); RenderTracked below does too — it draws live cue
        // countdowns. The braces add a scope, not an indent — the region is 200
        // lines and re-indenting it would bury this change in noise.
        var fullRender = s.Version != _lastRenderedVersion ||
                         DateTime.Now - _lastFullRender > TimeSpan.FromSeconds(10);
        if (fullRender)
        {
        _lastRenderedVersion = s.Version;
        _lastFullRender = DateTime.Now;

        if (CombatSection.IsExpanded)
        {
            ShowLastFight(s, CombatFightLabel, CombatFightBody, CombatFightText, CombatFightList,
                healing: false, _settings.ShowCombatFight);
            CombatFightCopy.Visibility = s.LastFight is not null
                ? Visibility.Visible : Visibility.Collapsed;
            CombatFightTimeline.Visibility = CombatFightCopy.Visibility;
            CombatSummary.Text = string.Join(Environment.NewLine,
                EQBuddy.UI.Shared.CombatPresentation.SummaryLines(s));
            PaintCombatSpark(s);
            FillBreakdown(DamageSourceList, s.DamageBySource, _dmgOutSort, s.CombatSeconds, "dps",
                SpellResistLookup(s), BlockedByLookup(s));
            // Shares the damage sort bar above it — it's the same rows, one level down.
            // Collapsed to one line by default (asked for in discussion #28 by a pet
            // class drowning in rows): the pet's overall damage is already a row in the
            // list above; the per-ability split is a click away.
            PetAbilityLabel.Visibility = s.PetAbilities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PetAbilityLabel.Set(_settings.ShowPetAbilities, _settings.ShowPetAbilities
                ? "Pet abilities"
                : $"Pet abilities ({s.PetAbilities.Count})");
            PetAbilityList.Visibility = _settings.ShowPetAbilities ? Visibility.Visible : Visibility.Collapsed;
            if (_settings.ShowPetAbilities)
                FillBreakdown(PetAbilityList, s.PetAbilities, _dmgOutSort, s.CombatSeconds, "dps");
            FillStatList(DamageTakenList, s.DamageByAttacker, _dmgInSort, "hit");
            RecentFightsLabel.Visibility = s.RecentEncounters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RecentFightsList.Items.Clear();
            if (s.RecentEncounters.Count > 0)
            {
                // Bars compare per-fight DPS against the hottest recent fight.
                var topFightDps = Math.Max(0.1, s.RecentEncounters.Max(f => f.Dps));
                var fightBrush = BreakdownRows.BarBrush(this);
                foreach (var f in s.RecentEncounters)
                    RecentFightsList.Items.Add(BreakdownRows.Row(this, f.Name,
                        $"{f.DurationSeconds:0}s · {f.Dps:0.#} dps{(f.Outcome == "Timeout" ? " · ?" : "")}",
                        f.Dps / topFightDps, fightBrush,
                        $"{f.DamageOut:N0} damage over {f.DurationSeconds:0}s"));
            }
            // Per cast, not per target — an AoE's whole value is what one cast produces.
            AreaSpellLabel.Visibility = s.AreaSpells.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(AreaSpellList, s.AreaSpells.Select(x =>
                (x.Name, $"{x.DamagePerCast:N0}/cast · ×{x.Casts} · {x.AvgTargets:0.#} targets" +
                         (x.MaxTargets > x.AvgTargets + 0.05 ? $" (best {x.MaxTargets})" : ""))));
            // Procs per combat-minute (#85, Kerdude): same denominator as DPS, so
            // downtime doesn't flatter the weapon.
            ProcLabel.Visibility = s.Procs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var combatMinutes = Math.Max(1.0 / 60, s.CombatSeconds / 60.0);
            FillList(ProcList, s.Procs.Select(x =>
                (x.Name, $"×{x.Count} · {x.Damage:N0} dmg · {x.Count / combatMinutes:0.#}/min")));
            StanceLabel.Visibility = s.Stances.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(StanceList, s.Stances.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg · {(int)x.CombatSeconds}s · {x.Dps:0.#} dps")));
            InvocationLabel.Visibility = s.Invocations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(InvocationList, s.Invocations.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg · {(int)x.CombatSeconds}s · {x.Dps:0.#} dps")));
        }

        HealingHeader.Text = s.Hps > 0 ? $"{s.Hps:0.#} hps" : $"{s.HealingDone:N0} healed";
        if (HealingSection.IsExpanded)
        {
            ShowLastFight(s, HealFightLabel, HealFightBody, HealFightText, HealFightList,
                healing: true, _settings.ShowHealFight);
            HealingSummary.Text = string.Join(Environment.NewLine,
                EQBuddy.UI.Shared.CombatPresentation.HealingLines(
                    s, s.RegenTicks > 0 ? RegenLine(s) : null));
            var showSpells = s.HealsBySpell.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            HealSpellsLabel.Visibility = showSpells;
            HealSortBar.Visibility = showSpells;
            // The resist/block lookup rides along: a blocked HoT or buff that has
            // landed at least once this session gets its "N blocked" here — the only
            // per-spell row a non-damage spell ever has.
            FillBreakdown(HealSpellList, s.HealsBySpell, _healSort, s.CombatSeconds, "hps",
                SpellResistLookup(s), BlockedByLookup(s));
            HealersLabel.Visibility = s.HealsByHealer.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EqCardRows.Fill(HealerList, EQBuddy.UI.Shared.CombatPresentation.HealerRows(s));
        }

        if (KillsSection.IsExpanded) _kills.Render(s);

        if (LootSection.IsExpanded)
            _loot.Render(s);

        // The Quests card is a launcher, not a checklist: its one line reports both
        // checklists so the glance survives, and the work happens in the window.
        QuestsHeader.Text = _quests.SummaryLine();

        if (GearSection.IsExpanded && _gearChecklistDirty)
        {
            RenderGearChecklist();
            _gearChecklistDirty = false;
        }

        if (MiscSection.IsExpanded)
        {
            FillList(DeathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
            FillList(ZoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
            MarkersLabel.Visibility = s.Markers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(MarkerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
        }
        }   // end fullRender gate

        RenderTracked(s);   // per-tick: live cue countdowns and "last: … ago" ages
        RenderBuffs(s);     // per-tick: the countdowns ARE the content
        UpdatePerfStats();  // #112: self-measurement, every few seconds, off by default

        // Any non-empty EQBUDDY_EXPAND writes the dump, not just "1". The named form
        // (EQBUDDY_EXPAND=raids) is the ONLY way to open a card the review set leaves
        // closed — Raids and Buffs both default to Collapsed — so while the gate read
        // "1" those two cards were the two the E2E suite could not assert on at all.
        // That is exactly backwards: Raids is the surface about to be lifted, and the
        // WPF layer has no unit tests (docs/TestPlan.md §5). A shot's throwaway profile
        // getting a debug.txt it does not read costs nothing.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EQBUDDY_EXPAND")))
        {
            try
            {
                // Row counts say "a new name appeared"; the snapshot totals say "the
                // session moved" — the E2E suite (tests/EQBuddy.E2E) asserts on both.
                var dump = $"dmgSrc={DamageSourceList.Items.Count} dmgTaken={DamageTakenList.Items.Count} " +
                    $"kills={_kills.KillRowCount} party={_kills.PartyRowCount} loot={_loot.RowCount} " +

                    // The PROGRESS THEME's launcher card (docs/Themes.md). It replaced
                    // five cards whose row counts used to be asserted here; what a reader
                    // sees now is one line, so that is what this pins — the card is
                    // present, and folding five cards into it kept their numbers on
                    // screen rather than quietly losing the glance. Exactly the shape
                    // questsCard/questsSummaryLen took when the quest cards folded.
                    //
                    // The ROWS moved with the surfaces, into ProgressWindow.DebugFacts()
                    // below, where the same E2E assertions read them.
                    $"progressCard={(ProgressSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"progressSummaryLen={ProgressHeader.Text.Length} " +
                    $"raidsDefeated={_raidLedger.DefeatedCount()} " +
                    $"zones={ZoneList.Items.Count} deaths={DeathList.Items.Count} " +
                    $"killsTotal={s.YourKillCount} lootTotal={s.LootTotal} " +
                    $"tracked={s.Tracked.Sum(t => t.TotalQuantity)} " +
                    // The Watch card's RENDERED shape, not just its total. The total
                    // above proves the data arrived; these prove the card drew it, and
                    // they exist because this surface is about to be lifted into a file
                    // of its own — the WPF layer has no unit tests (docs/TestPlan.md §5),
                    // so an assertion from a launched app is the only thing standing
                    // between that move and a silent regression. Row count, whether the
                    // sort strip is up (it appears only above two or more rules), and
                    // which sort is lit.
                    // The PROGRESS card's rendered shape, for the same reason and in the
                    // same week: it is the next surface being lifted out. "skills" above
                    // proves the data arrived; these prove the card drew the three lists
                    // that are easy to lose in a move — the ding unlocks (shown only when
                    // a level was announced this session), the next-milestone preview
                    // (hidden until a level is known at all, and folded behind a setting)
                    // and the AA split into session-new vs the full ledger.
                    $"watchRows={_watch.RowCount} " +
                    $"watchStrip={(_watch.SortStripShown ? 1 : 0)} " +
                    $"watchSort={_settings.WatchSortMode} " +
                    $"actualH={ActualHeight:0} actualW={ActualWidth:0} " +
                    // Geometry, for the E2E wiring check. WidgetMetrics is unit-tested,
                    // but only a launched app can show that its answer actually reaches
                    // the control — which is the half of #144 a unit test cannot see.
                    // uiScale is ×100 because the dump carries integers.
                    $"uiScale100={_settings.UiScale * 100:0} " +
                    $"sectionCapScreen={_sectionAutoCap:0} " +
                    $"sectionMaxH={SectionScroll.MaxHeight:0} " +
                    // The Quests card (2026-08-16). It replaced the Epic and Sky cards,
                    // whose tab and row counts used to be asserted here. What a reader
                    // sees now is one launcher line, so that is what E2E pins: the card
                    // is present, and folding two cards into it kept BOTH checklists'
                    // counts on screen rather than quietly losing the glance.
                    $"questsCard={(QuestsSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"questsEpicTotal={_settings.EpicQuestChecklist.Count} " +
                    $"questsSkyTotal={_settings.SkyQuestChecklist.Count} " +
                    $"questsSummaryLen={QuestsHeader.Text.Length} " +
                    // The Quest Tracker WINDOW, when EQBUDDY_QUESTS opened one. The WPF
                    // layer has no unit tests (docs/TestPlan.md §5), so the Gate 2
                    // rebuild's structure — list rows, a selection, a populated detail
                    // pane — is only assertable from a launched app. The window formats
                    // its own facts; this just carries them.
                    (_questsWindow is { IsLoaded: true } qwin ? qwin.DebugFacts() + " " : "") +
                    // The Progress WINDOW, when EQBUDDY_PROGRESS opened one. The five
                    // surfaces it hosts were pinned on the widget before the fold; this
                    // is where those same numbers come out now, and the point of the
                    // assertion is that they are the SAME numbers.
                    (_progressWindow is { IsLoaded: true } pwin ? pwin.DebugFacts() + " " : "") +
                    // EQBuddy Mobile's pump: it should be running, and it should be
                    // doing nothing, because this profile has no paired device.
                    $"companionPumpTicks={_companionPumpTicks} " +
                    $"companionPushes={_companionPushes}";
                System.IO.File.WriteAllText(Core.AppPaths.File("debug.txt"), dump);
            }
            catch { }
        }
    }

    /// <summary>
    /// Say so, out loud, whenever a log is archived — with the file it went to.
    ///
    /// The janitor runs unattended on a background thread, so until 1.85.0 the only
    /// evidence archiving worked was going and looking. Frankthetankk lost a session to
    /// the idle cleanup (#159) and had no way to tell whether the copy he was relying on
    /// had ever been made. A destructive-adjacent action that leaves no trace is the same
    /// silent no-op the rest of this app refuses to ship.
    ///
    /// Both the toast and the log line, deliberately: the banner is seen now, the
    /// error.log entry is what answers "did it archive that session last Tuesday".
    /// </summary>
    private void AnnounceArchive(string destination)
    {
        var line = $"Log archived → {destination}";
        CoreLog.Error(line);
        Dispatcher.BeginInvoke(() => AlertTile.ShowAlert(
            $"Log archived — {System.IO.Path.GetFileName(destination)} (Logs\\archive)"));
    }

    // ---- watch rules: rendering + alerts ----

    // Keyed by TrackedRule.Id — a display name can be shared by two rules, and keying
    // on it made same-named rules share baselines and cooldowns.
    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.Ordinal);
    // #137 (bjstrange): last-seen per-item counts per rule, so a burst catching several
    // distinct items names each one instead of "{last} ×N". Written and reset in
    // lock-step with _ruleBaseline — the two must never disagree about "last seen".
    private readonly Dictionary<string, Dictionary<string, int>> _ruleItemBaseline = new(StringComparer.Ordinal);
    private readonly EQBuddy.UI.Shared.AlertCooldowns _ruleCooldowns = new();
    private readonly EQBuddy.UI.Shared.SoundGate _soundGate = new();
    private string? _alertBaselinePath;
    private AlertWindow? _alertWindow;

    /// <summary>The floating alert tile — created on first use, owned by the widget.</summary>
    internal AlertWindow AlertTile => _alertWindow ??= new AlertWindow(_settings) { Owner = this };

    /// <summary>The Watch card, per tick. It draws live cue countdowns and "last: … ago"
    /// ages, so it runs outside the fullRender gate like the clock and the chips do.
    ///
    /// The surface itself is <see cref="WatchCardView"/> (lifted 2026-08-19 for ratchet
    /// room). What stays here is what the HOST owns for every card: the hidden check,
    /// the header count, and not painting a collapsed body.</summary>
    private void RenderTracked(StatsSnapshot s)
    {
        if (_settings.HiddenSections.Contains("tracked")) return;   // layout collapsed it
        TrackedSection.Visibility = Visibility.Visible;
        TrackedHeader.Text = s.Tracked.Sum(t => t.TotalQuantity).ToString();
        if (!TrackedSection.IsExpanded) return;
        _watch.Render(s);
    }

    internal void ImportGearChecklist(GearChecklistImportResult import)
    {
        // Boxes ticked in the app (by hand or auto-done) survive a re-import — the
        // fresh export only knows what the website was told.
        GearChecklistImporter.PreserveAcquired(import.Items, _settings.GearChecklist);
        _settings.GearChecklist = import.Items;
        _settings.GearChecklistName = import.Name;
        _settings.Save();
        _gearChecklistDirty = true;
        UpdateGearHeaderOnly();
        RefreshUi();
    }

    internal void ClearGearChecklist()
    {
        _settings.GearChecklist.Clear();
        _settings.GearChecklistName = "";
        _settings.Save();
        _gearChecklistDirty = true;
        UpdateGearHeaderOnly();
        RefreshUi();
    }

    private void RenderGearChecklist()
    {
        GearChecklistList.Items.Clear();
        var total = _settings.GearChecklist.Count;
        // No list, no view to pivot — the toggle would be a silent no-op.
        GearByZoneCheck.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (total == 0)
        {
            GearListName.Text = "Import an EQ Legends Tools shopping-list HTML in Options.";
            GearChecklistList.Items.Add(new TextBlock
            {
                Text = "No gear list imported.",
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                Foreground = (Brush)FindResource("DimBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
            UpdateGearHeaderOnly();
            return;
        }

        GearListName.Text = EQBuddy.UI.Shared.GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);

        if (_settings.GearGroupByZone) RenderGearByZone();
        else RenderGearBySlot();

        UpdateGearHeaderOnly();
    }

    private void RenderGearBySlot()
    {
        foreach (var group in EQBuddy.UI.Shared.GearChecklistPresentation.BuildGroups(_settings.GearChecklist))
        {
            GearChecklistList.Items.Add(GearGroupHeading(group.Heading));
            foreach (var item in group.Items)
                GearChecklistList.Items.Add(GearRow(item));
        }
    }

    private void RenderGearByZone()
    {
        // The WHERE-TO-GO pivot: grouping and buckets live in UI.Shared
        // (GearFarmRollup) where they are tested; this side only draws. Nearest-first
        // needs a current zone — before the first zone line of a session the rollup
        // degrades to alphabetical rather than guessing.
        Func<string, int?>? hopsFromHere = CurrentZoneName.Length > 0
            ? zone => ZoneGraph.Distance(CurrentZoneName, zone)?.Hops
            : null;
        var groups = EQBuddy.UI.Shared.GearFarmRollup.Build(
            _settings.GearChecklist, ItemCatalog.Default.Find, hopsFromHere);
        if (groups.Count == 0)
        {
            GearChecklistList.Items.Add(new TextBlock
            {
                Text = "Everything on the list is acquired — nothing left to farm.",
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                Foreground = (Brush)FindResource("DimBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var group in groups)
        {
            GearChecklistList.Items.Add(GearGroupHeading(EQBuddy.UI.Shared.GearFarmRollup.Heading(group)));
            foreach (var item in group.Items)
                GearChecklistList.Items.Add(GearRow(item));
        }
    }

    private TextBlock GearGroupHeading(string heading) => new()
    {
        Text = heading,
        FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)FindResource("AccentBrush"),
        Margin = new Thickness(0, Tok.SpaceM, 0, Tok.SpaceXxs),
    };

    private CheckBox GearRow(GearChecklistItem item)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = item.Slot,
            FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
            Foreground = (Brush)FindResource("DimBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var itemName = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            Foreground = (Brush)FindResource("TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var itemText = EQBuddy.UI.Shared.GearChecklistPresentation.TextFor(item);
        itemName.Inlines.Add(itemText.Name);
        if (itemText.EffectSuffix.Length > 0)
        {
            itemName.Inlines.Add(new System.Windows.Documents.Run(itemText.EffectSuffix)
            {
                FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
                Foreground = (Brush)FindResource("DimBrush"),
            });
        }
        text.Children.Add(itemName);
        if (item.Source.Length > 0)
        {
            text.Children.Add(new TextBlock
            {
                Text = item.Source,
                FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
                Foreground = (Brush)FindResource("DimBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        var check = new CheckBox
        {
            IsChecked = item.Acquired,
            Content = text,
            Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceXxs),
            ToolTip = EQBuddy.UI.Shared.GearChecklistPresentation.Tooltip(item),
        };
        check.Checked += (_, _) => OnGearToggled(item, true);
        check.Unchecked += (_, _) => OnGearToggled(item, false);
        return check;
    }

    private void OnGearToggled(GearChecklistItem item, bool acquired)
    {
        item.Acquired = acquired;
        _settings.Save();
        UpdateGearHeaderOnly();
        GearListName.Text = EQBuddy.UI.Shared.GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);
        // The zone view excludes acquired rows and repeats a multi-zone item under
        // each zone it drops in — its checkbox twins must repaint, next tick.
        if (_settings.GearGroupByZone) _gearChecklistDirty = true;
    }

    private void OnGearByZoneToggled(object sender, RoutedEventArgs e)
    {
        var value = GearByZoneCheck.IsChecked == true;
        if (_settings.GearGroupByZone == value)
            return;

        _settings.GearGroupByZone = value;
        _settings.Save();
        if (GearSection.IsExpanded)
        {
            RenderGearChecklist();
            _gearChecklistDirty = false;
        }
        else
        {
            _gearChecklistDirty = true;
        }
    }

    private void UpdateGearHeaderOnly()
    {
        var total = _settings.GearChecklist.Count;
        var acquired = _settings.GearChecklist.Count(i => i.Acquired);
        GearHeader.Text = $"{acquired}/{total}";
    }

    private void UpdateGearChecklist(StatsSnapshot s)
    {
        var changed = AutoCheckGearLoot(s);
        UpdateGearHeaderOnly();
        if (changed)
        {
            _gearChecklistDirty = true;   // rebuild next tick: checked box, list-name count
            _settings.Save();
        }
    }

    private bool AutoCheckGearLoot(StatsSnapshot s)
    {
        // Most installs have no imported list; skip the per-tick grouping for them.
        if (_settings.GearChecklist.Count == 0) return false;
        // The matching rules live in Core (GearLootAutoCheck) where they are tested:
        // single-owner list, so a name match ticks — no class lens, no * machinery
        // (an auto-ticked row is indistinguishable from a hand-ticked one, by design).
        // Three streams feed it: drops, manual merges (exaltations), and loot-merge
        // results (the "+N" tier a wish usually names). Same high-water diff as
        // Sky/Epic — only the newly-obtained delta ticks, so a re-render never
        // double-counts.
        var changed = ApplyGearHighWater(_gearLootSeen,
            s.Loot.Select(l => new NameCount(l.Item, l.Count)));
        changed |= ApplyGearHighWater(_gearCraftSeen, s.Crafted);
        changed |= ApplyGearHighWater(_gearUpgradeSeen, s.Upgraded);
        return changed;
    }

    private bool ApplyGearHighWater(Dictionary<string, int> seen, IEnumerable<NameCount> counts)
    {
        var byName = counts
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);

        // A session reset empties the snapshot's lists; drop the marks with it so the
        // next session's first loot still reads as new (the Sky perf-audit #9 contract).
        foreach (var key in seen.Keys.ToList())
            if (!byName.ContainsKey(key))
                seen[key] = 0;

        var changed = false;
        foreach (var (name, count) in byName)
        {
            seen.TryGetValue(name, out var prior);
            seen[name] = count;
            if (count <= prior) continue;
            changed |= GearLootAutoCheck.Apply(_settings.GearChecklist, name, count - prior);
        }
        return changed;
    }

    // ---- quest checklists ----
    //
    // The widget's Epic and Sky cards became one Quests launcher on 2026-08-16, so the
    // tabbed rendering that used to live behind these went with them. What remains in
    // QuestChecklistView is the part that was never about the cards: auto-ticking the
    // checklists from loot, and the achievements import — both of which feed the Quest
    // Tracker window and EQBuddy Mobile, neither of which needs a card to be open.
    // These two forward because XAML binds the ⚙ menu handlers by name.

    private void UpdateEpicQuestChecklist(StatsSnapshot s) => _quests.UpdateEpicQuestChecklist(s);
    private void UpdateSkyQuestChecklist(StatsSnapshot s) => _quests.UpdateSkyQuestChecklist(s);

    private void OnImportAchievements(object sender, RoutedEventArgs e) =>
        _quests.OnImportAchievements(sender, e);
    private void OnCopyAchievementsCommand(object sender, RoutedEventArgs e) =>
        _quests.OnCopyAchievementsCommand(sender, e);


    /// <summary>
    /// Fire banner/sound alerts when a tracked rule's total grows. Baselines are reset
    /// (without alerting) whenever the watched log changes, so startup ingest and
    /// character switches never replay old drops (ALERT-007, RECOVERY-006).
    /// </summary>
    /// <summary>Per-rule alert cooldown for text rules. Shorter than the 5 s used elsewhere
    /// (ALERT-008): a heal rotation announces every few seconds by design, and swallowing
    /// those repeats would silence exactly the case this rule kind exists for.</summary>
    private static readonly TimeSpan TextAlertCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A Text watch rule matched, straight off the ingest thread. Alerting here rather than
    /// from the next snapshot removes a whole refresh interval of lag from the one rule
    /// kind that's about reacting in time.
    ///
    /// Suppressed during initial ingest, like every other alert — replaying today's log at
    /// startup must not fire a burst of banners for calls that happened an hour ago.
    /// </summary>
    private void OnTextMatched(RawLineEvent raw)
    {
        // During the startup re-read of the log, immediate alerts stay suppressed — nobody
        // wants a burst of banners for things that happened an hour ago. Delayed cues are
        // different: a respawn timer set four minutes ago is still running, and losing it
        // because the app restarted is exactly when you needed it. So a cue whose due time
        // is still in the future gets scheduled for the time it has left.
        var ingesting = !_watcher.InitialIngestDone;
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var rule in _settings.TrackedRules)
            {
                if (!rule.Enabled || rule.Kind != WatchKind.Text) continue;
                if (!rule.Matches(raw.Line)) continue;
                if (ingesting && rule.AlertDelaySeconds <= 0) continue;
                var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
                AlertOrCue(rule, name, Trim(raw.Line), TextAlertCooldown, raw.Time);
            }
        });

        static string Trim(string line) => line.Length <= 80 ? line : line[..79].TrimEnd() + "…";
    }

    private readonly EQBuddy.UI.Shared.DelayedAlerts _delayedAlerts = new();

    /// <summary>
    /// Alert now, or set a cue for later when the rule asks for a delay
    /// (<see cref="TrackedRule.AlertDelaySeconds"/>) — a complete-heal chain wants the sound
    /// a couple of seconds *after* the call, and a mez wants it before the spell breaks.
    ///
    /// The wait uses a one-shot dispatcher timer per cue rather than the 1 s UI refresh, so
    /// a 2.5 s cue lands at 2.5 s and not somewhere in the following second. The cooldown is
    /// applied when the alert actually fires, not when it was scheduled: with a delay set,
    /// what matters is how long since you last *heard* something.
    /// </summary>
    /// <param name="matchTime">When the line was written, not when we read it. Cues are
    /// scheduled from this, so one recovered from the log at startup fires with the time it
    /// has left rather than restarting its whole delay.</param>
    private void AlertOrCue(TrackedRule rule, string ruleName, string label, TimeSpan cooldown,
        DateTime? matchTime = null)
    {
        if (rule.AlertDelaySeconds <= 0)
        {
            FireAlert(rule, ruleName, label, cooldown);
            return;
        }
        var from = matchTime ?? DateTime.Now;
        var remaining = from.AddSeconds(rule.AlertDelaySeconds) - DateTime.Now;
        if (remaining <= TimeSpan.Zero) return;   // already due — the moment has passed
        if (_delayedAlerts.Schedule(rule, ruleName, label, from) is not { } pending) return;

        var timer = new DispatcherTimer { Interval = remaining };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_delayedAlerts.Claim(pending))
                FireAlert(pending.Rule, pending.RuleName, pending.Label, cooldown);
        };
        timer.Start();
    }

    private void FireAlert(TrackedRule rule, string ruleName, string label, TimeSpan cooldown)
    {
        if (!_ruleCooldowns.ShouldFire(rule, label, cooldown, DateTime.Now)) return;

        if (rule.AlertBanner)
            // No leading star: the banner is a bare TextBlock, so a glyph in front of the
            // rule name is drawn text with nothing controlling its size or weight — and
            // it is a box on a Wine prefix. The tile already identifies itself by the
            // rule's own colour (Chaosrah, 2026-08-06) and by naming the rule.
            AlertTile.ShowAlert($"{ruleName}: {label}",
                EQBuddy.UI.Shared.AlertColors.Hex(rule.AlertColor));
        if (EQBuddy.UI.Shared.AlertSoundCatalog.Resolve(rule, _settings.AlertSound) is { } sound)
            PlayAlertSound(sound, coalesce: true);
        if (rule.AlertSpeech)
            EQBuddy.UI.Shared.SpokenAlerts.Speak(
                EQBuddy.UI.Shared.SpokenAlerts.ResolvePhrase(rule.SpokenPhrase, label));
    }

    /// <summary>
    /// The "Last fight" line above a card's session totals, and the "Session so far" heading
    /// that then separates the two. Both stay hidden until there's been a fight — a heading
    /// over nothing is worse than no heading.
    /// </summary>
    private void ShowLastFight(StatsSnapshot s, System.Windows.Controls.Button label,
        System.Windows.Controls.Panel body, System.Windows.Controls.TextBlock text,
        System.Windows.Controls.ItemsControl list, bool healing, bool open)
    {
        if (s.LastFight is not { } f)
        {
            label.Visibility = body.Visibility = Visibility.Collapsed;
            return;
        }
        label.Visibility = Visibility.Visible;
        body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        // "Current" while it's still running, so a duration that keeps growing reads as
        // in-progress rather than as a fight that took a suspiciously long time.
        if (label.Content is EqFoldLabel fold)
            fold.Set(open, f.InProgress ? "Current fight" : "Last fight");
        if (!open) return;

        // Rates within the fight use the fight's own length, not session combat time —
        // "what did this pull actually do" is the whole point of the section.
        var rows = healing ? f.HealsBySpell : f.ByAbility;
        FillBreakdown(list, rows, healing ? _healSort : _dmgOutSort,
            f.DurationSeconds, healing ? "hps" : "dps");
        if (!healing)
        {
            // Same treatment as the History encounter review: per-creature split when
            // the pull has several, then "Your damage" and "Damage you took".
            CombatFightSplit.Visibility = f.Fights.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            if (f.Fights.Count > 1)
                CombatFightSplit.Text = string.Join(" · ",
                    f.Fights.Select(x => $"{x.Name} {x.DamageOut:N0}"));
            CombatFightOutLabel.Visibility =
                f.ByAbility.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            CombatFightInLabel.Visibility =
                f.ByIncoming.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(CombatFightInList, f.ByIncoming.Select(x =>
                (x.Name, $"{x.Total:N0} · ×{x.Hits} · avg {(double)x.Total / Math.Max(1, x.Hits):0.#}")));
        }
        text.Text = healing
            ? $"{f.Name} — {f.Healed:N0} healed · {f.Hps:0.#} hps over {f.DurationSeconds:0}s"
              + (f.InProgress ? " (fighting)" : "")
            : $"{f.Name} — {f.DamageOut:N0} dmg · {f.Dps:0.#} dps over {f.DurationSeconds:0}s"
              + $" · took {f.DamageIn:N0}"
              + (f.InProgress ? " (fighting)" : f.Outcome == "Killed" ? "" : $" · {f.Outcome}");
    }

    /// <summary>Collapse handlers for the Combat/Healing subsections. Each remembers its own
    /// state: the reason to shut the fight breakdown isn't the reason to shut the session
    /// one, and a card that reopens everything on restart isn't really collapsible.</summary>
    private void OnToggleCombatFight(object sender, RoutedEventArgs e) =>
        ToggleSubsection(v => _settings.ShowCombatFight = v, _settings.ShowCombatFight);

    private void OnToggleCombatSession(object sender, RoutedEventArgs e) =>
        ToggleSubsection(v => _settings.ShowCombatSession = v, _settings.ShowCombatSession);

    private void OnToggleHealFight(object sender, RoutedEventArgs e) =>
        ToggleSubsection(v => _settings.ShowHealFight = v, _settings.ShowHealFight);

    private void OnToggleHealSession(object sender, RoutedEventArgs e) =>
        ToggleSubsection(v => _settings.ShowHealSession = v, _settings.ShowHealSession);

    private void ToggleSubsection(Action<bool> set, bool current)
    {
        set(!current);
        _settings.Save();
        RefreshUi();   // the next refresh applies visibility and rebuilds only what's shown
    }

    /// <summary>Session bodies are plain show/hide — their content is filled elsewhere.</summary>
    private void ApplySessionSubsections()
    {
        if (CombatSessionLabel.Content is EqFoldLabel cs) cs.Open = _settings.ShowCombatSession;
        CombatSessionBody.Visibility = _settings.ShowCombatSession ? Visibility.Visible : Visibility.Collapsed;
        if (HealSessionLabel.Content is EqFoldLabel hs) hs.Open = _settings.ShowHealSession;
        HealSessionBody.Visibility = _settings.ShowHealSession ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProcessTrackedAlerts(StatsSnapshot s)
    {
        if (!_watcher.InitialIngestDone) return;
        if (_alertBaselinePath != _watcher.CurrentPath)
        {
            // First run isn't a character switch — it's the baseline being set for the first
            // time. Cancelling here wiped cues recovered from the log seconds earlier, which
            // is precisely the restart case they exist for.
            var switchedCharacter = _alertBaselinePath is not null;
            _alertBaselinePath = _watcher.CurrentPath;
            _ruleBaseline.Clear();
            _ruleItemBaseline.Clear();
            foreach (var r in s.Tracked)
            {
                _ruleBaseline[r.Id] = r.TotalQuantity;
                _ruleItemBaseline[r.Id] = EQBuddy.UI.Shared.WatchAlertText.ItemCounts(r);
            }
            if (switchedCharacter) _delayedAlerts.CancelAll();   // cues belonged to who we left
            _knownDeaths = s.Deaths.Count;
            return;
        }
        CancelStaleCues(s);

        foreach (var r in s.Tracked)
        {
            var baseline = _ruleBaseline.TryGetValue(r.Id, out var b) ? b : 0;
            if (r.TotalQuantity <= baseline)
            {
                _ruleBaseline[r.Id] = r.TotalQuantity;
                _ruleItemBaseline[r.Id] = EQBuddy.UI.Shared.WatchAlertText.ItemCounts(r);
                continue;
            }
            var delta = r.TotalQuantity - baseline;
            var previousItems = _ruleItemBaseline.TryGetValue(r.Id, out var prevItems) ? prevItems : null;
            _ruleBaseline[r.Id] = r.TotalQuantity;
            _ruleItemBaseline[r.Id] = EQBuddy.UI.Shared.WatchAlertText.ItemCounts(r);

            var rule = _settings.TrackedRules.FirstOrDefault(x => x.Id == r.Id);
            if (rule is null) continue;
            // Text rules already alerted from the ingest thread the moment the line
            // arrived (OnTextMatched). The baseline above still had to move so this rule
            // doesn't look like a fresh burst later.
            if (rule.Kind == WatchKind.Text) continue;

            AlertOrCue(rule, r.Name,
                EQBuddy.UI.Shared.WatchAlertText.MatchLabel(rule, r, delta, previousItems),
                TimeSpan.FromSeconds(5));   // ALERT-008 cooldown
        }
    }

    /// <summary>Deaths seen last refresh, so a new one can cancel pending cues — a reminder
    /// to recast something is noise once you're dead.</summary>
    private int _knownDeaths;

    /// <summary>Drop cues that have outlived the situation that scheduled them: the session
    /// rolled over on an idle gap, the widget followed a different character, or you died.</summary>
    private void CancelStaleCues(StatsSnapshot s)
    {
        if (s.Deaths.Count != _knownDeaths)
        {
            var died = s.Deaths.Count > _knownDeaths;
            _knownDeaths = s.Deaths.Count;
            // Combat cues only: a respawn timer doesn't care that you died.
            if (died) _delayedAlerts.CancelCombatCues();
        }
    }

    private System.Windows.Media.MediaPlayer? _alertPlayer;

    /// <summary>Named alert sounds → distinct files in C:\Windows\Media (shared
    /// catalog). SystemSounds is useless here: most of its entries share one "ding"
    /// in the default scheme and Question is typically unassigned (silent).</summary>
    internal static readonly (string Name, string File)[] AlertSounds =
        EQBuddy.UI.Shared.AlertSoundCatalog.Sounds;

    /// <summary>Play the shared alert sound (Options preview, and rules with no sound
    /// of their own).</summary>
    internal void PlayAlertSound() => PlayAlertSound(_settings.AlertSound);

    /// <summary>Play a specific alert sound: a named built-in, or a custom
    /// .wav/.mp3 path. Unknown/missing values fall back to the system Asterisk.
    /// With <paramref name="coalesce"/> on, sounds inside <see cref="EQBuddy.UI.Shared.SoundGate.Window"/>
    /// of the last one are dropped — several rules firing together are one audio alert, and
    /// the first clip plays to the end instead of being cut off by the next Open(). Manual
    /// previews and spawn-due chimes keep coalesce off: the user asked for that exact sound.</summary>
    internal void PlayAlertSound(string choiceOrPath, bool coalesce = false)
    {
        if (coalesce && !_soundGate.TryClaim(DateTime.Now)) return;
        try
        {
            // Every decision — legacy name mapping, where a built-in lives, what happens
            // when the player's own file is gone — belongs to UI.Shared, where it is unit
            // tested without an audio device (#153). This method only obeys the plan.
            var plan = EQBuddy.UI.Shared.AlertSoundPlanner.Plan(
                choiceOrPath, _settings.AlertVolume, BuiltInSoundPath, System.IO.File.Exists);
            if (plan.ShouldReportMissingFile) ReportMissingAlertSound(plan.MissingFile);
            if (plan.FilePath.Length == 0)
            {
                if (plan.Source != EQBuddy.UI.Shared.AlertSoundSource.Silent)
                    App.LogError($"No alert sound could be played for: {choiceOrPath}");
                return;
            }

            _alertPlayer ??= NewAlertPlayer();
            // MediaPlayer defaults to HALF volume; this line was the whole "alerts are
            // very quiet" report. Assigned before Open AND re-asserted from MediaOpened:
            // the documented-safe order is the handler, and doing only the handler would
            // leave the very first frames of a clip at the old level.
            var level = plan.Volume;
            _alertPlayer.Volume = level;
            _alertPlayerVolume = level;
            _alertPlayer.Open(new Uri(plan.FilePath));
            _alertPlayer.Play();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Volume the pending Open() is meant to play at, re-asserted once the media
    /// is actually open (see PlayAlertSound).</summary>
    private double _alertPlayerVolume = 1.0;

    private System.Windows.Media.MediaPlayer NewAlertPlayer()
    {
        var player = new System.Windows.Media.MediaPlayer();
        player.MediaOpened += (_, _) => player.Volume = _alertPlayerVolume;
        // A clip the player picked that MediaFoundation cannot decode is a real answer
        // too — and an unhandled MediaFailed on a MediaPlayer surfaces on the dispatcher.
        player.MediaFailed += (_, e) =>
            App.LogError($"Alert sound could not be played: {e.ErrorException?.Message}");
        return player;
    }

    /// <summary>Where a built-in alert sound lives on Windows: the shared Media folder.
    /// Anything not in the palette has no built-in file, which is how a custom path is
    /// told apart from a name.</summary>
    private static string BuiltInSoundPath(string name)
    {
        var named = Array.Find(AlertSounds, x => x.Name == name);
        return named.File is { } f
            ? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", f)
            : "";
    }

    /// <summary>A picked alert sound has gone missing. Say so once per file rather than
    /// on every alert — but say it: substituting in silence is the no-op that made the
    /// volume slider look broken (#153, adndmike).</summary>
    private readonly HashSet<string> _reportedMissingSounds = new(StringComparer.OrdinalIgnoreCase);

    private void ReportMissingAlertSound(string missingFile)
    {
        var message = EQBuddy.UI.Shared.AlertSoundPlanner.MissingFileMessage(missingFile);
        App.LogError(message);
        if (!_reportedMissingSounds.Add(missingFile)) return;
        AlertTile.ShowAlert(message);
    }

    private void OnTutorial(object sender, RoutedEventArgs e) => new TutorialWindow(this).Show();

    private void OnFeedback(object sender, RoutedEventArgs e) =>
        new FeedbackWindow { Owner = this }.Show();

    private void OnCampMarker(object sender, RoutedEventArgs e) => DropCampMarker();

    private HistoryWindow? _historyWindow;

    private void OnHistory(object sender, RoutedEventArgs e)
    {
        // Flush the live session so it appears in the list as "(in progress)".
        _archiver.CheckpointSync(_stats.Snapshot());
        if (_historyWindow is { IsLoaded: true })
        {
            _historyWindow.Activate();
            return;
        }
        _historyWindow = new HistoryWindow(_repo, _settings);
        _historyWindow.Show();
    }

    private void DropCampMarker()
    {
        var s = _stats.Snapshot();
        _stats.AddMarker($"Marker {s.Markers.Count + 1}" +
            (s.CurrentZone.Length > 0 ? $" — {s.CurrentZone}" : ""));
    }

    private void UpdateLoggingStatus()
    {
        DateTime? lastActivity = _watcher.LastGrowth;
        if (lastActivity is null && _watcher.CurrentPath is { } p && File.Exists(p))
            lastActivity = File.GetLastWriteTime(p);

        var age = lastActivity is { } t ? DateTime.Now - t : TimeSpan.MaxValue;
        var brush = age < TimeSpan.FromSeconds(30) ? (Brush)FindResource("GoodBrush")
            : age < TimeSpan.FromMinutes(2) ? (Brush)FindResource("WarnBrush")
            : (Brush)FindResource("BadBrush");
        var tip = lastActivity is { } la
            ? $"Last log activity: {la:h:mm:ss tt}"
            : "No log file activity yet";
        StatusDot.Fill = brush; StatusDot.ToolTip = tip;
        MiniDot.Fill = brush; MiniDot.ToolTip = tip;
        LogBanner.Visibility = age > TimeSpan.FromMinutes(2) ? Visibility.Visible : Visibility.Collapsed;
    }

    private IEnumerable<(string Key, System.Windows.Controls.Primitives.ToggleButton Star)> StarButtons()
    {
        yield return ("dps", StarDps);
        yield return ("hps", StarHps);
        yield return ("pet", StarPet);
        yield return ("procs", StarProcs);
        yield return ("kills", StarKills);
        yield return ("loot", StarLoot);
        yield return ("deaths", StarDeaths);
        yield return ("buffs", StarBuffs);
    }

    /// <summary>The 🐾/⚡ glyphs beside their stars: clicking the glyph must toggle the
    /// star, not fall through to the section expander (David's live catch, 1.59.0).</summary>
    private void OnStarGlyphClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        System.Windows.Controls.Primitives.ToggleButton? star =
            (string)((FrameworkElement)sender).Tag switch
            {
                "pet" => StarPet, "procs" => StarProcs, _ => null,
            };
        if (star is null) return;
        star.IsChecked = star.IsChecked != true;
        OnStarChanged(star, new RoutedEventArgs());
    }

    private void OnStarChanged(object sender, RoutedEventArgs e)
    {
        var btn = (System.Windows.Controls.Primitives.ToggleButton)sender;
        var key = (string)btn.Tag;
        if (btn.IsChecked == true)
        {
            if (!_settings.MiniStats.Contains(key)) _settings.MiniStats.Add(key);
        }
        else
        {
            _settings.MiniStats.Remove(key);
        }
        _settings.Save();
    }

    private void SetMode(bool mini)
    {
        _settings.Minimized = mini;
        MiniRoot.Visibility = mini ? Visibility.Visible : Visibility.Collapsed;
        NormalRoot.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        ResizeGrip.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        HeightGrip.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        _settings.Save();
        var snap = _stats.Snapshot();
        if (mini) UpdateMiniChips(snap);
        UpdateBreakouts(snap);
    }

    // ---- breakout stat windows (BREAKOUT-*) ----

    private readonly Dictionary<BreakoutKind, BreakoutWindow> _breakouts = new();

    /// <summary>Open/refresh/hide the breakout windows: each shows while the widget is
    /// minimized and its condition holds — a star for the stat kinds, any 📌-pinned rule
    /// for the Watch list — unless ✕-disabled (persistent, re-enable in Options: the old
    /// until-next-minimize dismissal made the window whack-a-mole, discussion #45) or
    /// hidden with the game unfocused.</summary>
    private void UpdateBreakouts(StatsSnapshot s)
    {
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            var want = _settings.Minimized && !_hiddenForFocus &&
                       !_settings.DisabledBreakouts.Contains(kind.ToString()) && kind switch
                       {
                           BreakoutKind.Damage => _settings.MiniStats.Contains("dps"),
                           BreakoutKind.Healing => _settings.MiniStats.Contains("hps"),
                           BreakoutKind.Pet => _settings.MiniStats.Contains("pet"),
                           BreakoutKind.Loot => _settings.MiniStats.Contains("loot"),
                           // The Progress card's star is the xp one — same key gates
                           // its chip, so star + minimize opens this like the others.
                           BreakoutKind.Progress => _settings.MiniStats.Contains("xp"),
                           // "buffs" never renders a mini chip (MiniStatOrder skips
                           // it) — the Buffs card's star gates this window alone.
                           BreakoutKind.Buffs => _settings.MiniStats.Contains("buffs"),
                           _ => _settings.PinWatchChips &&
                                _settings.TrackedRules.Any(r => r.Enabled && r.Pinned),
                       };
            _breakouts.TryGetValue(kind, out var w);
            if (want)
            {
                if (w is not { IsLoaded: true })
                {
                    _breakouts[kind] = w = new BreakoutWindow(_settings, kind) { Main = this };
                    w.Dismissed += k =>
                    {
                        if (!_settings.DisabledBreakouts.Contains(k.ToString()))
                            _settings.DisabledBreakouts.Add(k.ToString());
                        _settings.Save();
                        // With double-click-toggle on, the ✕ is no longer a one-way trap —
                        // a double-click on the chip brings the window straight back — so the
                        // nag and its log entry would just be noise. Off, they stay: the ✕ is
                        // a small target over a game screen, and until the alert existed the
                        // only trace of hitting it was a window that quietly never came back
                        // (David lost his DPS breakout to exactly that, 2026-08-08). A
                        // permanent, hard-to-reverse state change must announce itself.
                        if (_settings.DoubleClickChipsToggleBreakouts) return;
                        AlertTile.ShowAlert($"{k} breakout hidden — re-enable in Options → Breakout windows");
                        CoreLog.Error($"{k} breakout hidden via its close button (re-enable: Options → Breakout windows)");
                    };
                }
                if (!w.IsVisible) w.Show();
                w.Update(s);
            }
            else if (w is { IsVisible: true })
            {
                w.SavePosition();
                w.Hide();
            }
        }
    }

    /// <summary>Toggle a stat's breakout window from its mini chip: show it if hidden,
    /// hide it if showing. Rides the same persistent DisabledBreakouts flag the ✕ uses,
    /// so the choice sticks and UpdateBreakouts applies it on the spot — a
    /// double-click is just a friendlier reach for it than the ✕ or Options
    /// (asked for: "let me pop the DPS or Loot window up only when I want it").</summary>
    private void ToggleBreakout(BreakoutKind kind)
    {
        var name = kind.ToString();
        if (!_settings.DisabledBreakouts.Remove(name))
            _settings.DisabledBreakouts.Add(name);
        _settings.Save();
        if (_latestSnapshot is { } snap) UpdateBreakouts(snap);
    }

    /// <summary>One mini-dashboard stat (2026-08-11, take two — David: no ovals):
    /// glyph + semibold tabular value as clean text, separated from its neighbor by
    /// a thin hairline divider rather than any chip chrome. A counting-down watch
    /// rule still announces itself by color alone. A chip whose stat has a breakout
    /// window takes a double-click to toggle it.</summary>
    private StackPanel MiniChip(string iconName, string value, string valueBrush, string? edgeBrush = null,
        BreakoutKind? breakout = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, Tok.SpaceL, 0),
        };
        if (breakout is { } kind && _settings.DoubleClickChipsToggleBreakouts)
        {
            // Transparent (not null) so the gaps between glyph and value are hit-testable
            // too. Two things conspired against WPF's own double-click here, so we detect it
            // ourselves at the window level:
            //   1. The bar's OnDrag starts a modal window DragMove on the FIRST left-click
            //      anywhere on the bar; that capture disrupted the click sequence and the
            //      cursor flickered into drag mode (the tell). Eating the click stops it.
            //   2. UpdateMiniChips rebuilds these panels every 1s tick, so a rebuild landing
            //      between the two clicks left the second click on a brand-new element and
            //      reset ClickCount to 1 — an intermittent miss.
            // Keying the double-click on (kind, time) on the window survives both: the panel
            // can be replaced mid-gesture and the second click still lands. The widget is
            // still dragged from any non-chip part of the bar; when the opt-in is off the
            // chip stays inert and a double-click expands the widget as before.
            panel.Background = System.Windows.Media.Brushes.Transparent;
            panel.Cursor = System.Windows.Input.Cursors.Hand;
            panel.ToolTip = $"Double-click to show or hide the {kind} breakout";
            panel.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                var now = DateTime.Now;
                if (_lastChipClickKind == kind && now - _lastChipClickAt <= DoubleClickWindow)
                {
                    _lastChipClickKind = null;   // consume, so a third click starts fresh
                    ToggleBreakout(kind);
                }
                else
                {
                    _lastChipClickKind = kind;
                    _lastChipClickAt = now;
                }
            };
        }
        // A vector, not a glyph (#148, #166): the minimized bar is on screen the whole
        // time a player farms, and it is exactly where a box instead of a skull would go
        // unnoticed on a Wine prefix.
        var icon = DesignSystem.Icon(iconName, "AccentBrush", size: UI.Shared.DesignTokens.IconInline);
        icon.Opacity = 0.9;
        icon.Margin = new Thickness(0, 0, UI.Shared.DesignTokens.SpaceS, 0);
        panel.Children.Add(icon);
        var v = new TextBlock
        {
            Text = value, FontSize = Tok.Spec(Tok.TypeRole.TitleSection).Size,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        v.SetResourceReference(TextBlock.ForegroundProperty, edgeBrush ?? valueBrush);
        panel.Children.Add(v);
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXxs, 0, Tok.SpaceXxs),
        };
        divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
        panel.Children.Add(divider);
        return panel;
    }

    /// <summary>The last chip's divider has nothing to divide — trim it.</summary>
    private static void TrimLastMiniDivider(Panel chips)
    {
        if (chips.Children.Count > 0 && chips.Children[^1] is StackPanel { Children.Count: > 0 } last
            && last.Children[^1] is Border divider)
            divider.Visibility = Visibility.Collapsed;
    }

    private void UpdateMiniChips(StatsSnapshot s)
    {
        MiniChips.Children.Clear();
        // Which cells, in which order, with which icon and what each reads: all from
        // UI.Shared. Both widgets carried this table by hand, identically, comments and
        // all — and the Avalonia one is the lane that historically drifts.
        foreach (var cell in UI.Shared.MiniBarPresentation.Cells(s, _settings.MiniStats))
        {
            BreakoutKind? breakout = cell.Key switch
            {
                "dps" => BreakoutKind.Damage,
                "hps" => BreakoutKind.Healing,
                "pet" => BreakoutKind.Pet,
                "loot" => BreakoutKind.Loot,
                "xp" => BreakoutKind.Progress,
                _ => null,   // kills/procs/motes/money/deaths have no breakout
            };
            MiniChips.Children.Add(
                MiniChip(cell.Icon, cell.Text, "AccentBrush", breakout: breakout));
        }

        // Per-rule pins: only the rules you picked (📌 in Options), not every enabled one.
        // The master toggle still gates the lot, so turning chips off is one click.
        var due = _delayedAlerts.NextDueByRule(DateTime.Now);
        foreach (var rule in _settings.PinWatchChips
                     ? _settings.TrackedRules.Where(r => r.Enabled && r.Pinned)
                     : [])
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t => t.Id == rule.Id);
            // A rule with a cue in flight shows time remaining instead of its count: while
            // something is counting down, when it fires is the only thing you want to know.
            var counting = due.TryGetValue(rule.Id, out var at);
            // A counting-down chip wears the warn edge too — state has a shape.
            MiniChips.Children.Add(counting
                ? MiniChip("Timer", $"{name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}",
                    "WarnBrush", edgeBrush: "WarnBrush", breakout: BreakoutKind.Watch)
                : MiniChip("Target", $"{name} {result?.TotalQuantity ?? 0}", "AccentBrush",
                    breakout: BreakoutKind.Watch));
        }

        TrimLastMiniDivider(MiniChips);
        // The hint belongs at the end, and only when there's genuinely nothing to show. It
        // used to return early when no stats were starred, which meant someone who pinned
        // watch rules but starred nothing got the hint instead of their chips.
        if (MiniChips.Children.Count == 0)
        {
            // The hollow star is the CONTROL it points at — the one beside every card
            // header — so it is the same vector those wear rather than a lookalike glyph.
            // Safe as a StackPanel: one short line that never wraps (trap 14).
            var hint = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var star = DesignSystem.Icon("Star", "DimBrush", size: Tok.IconInline);
            star.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
            hint.Children.Add(star);
            hint.Children.Add(DesignSystem.Text(Tok.TypeRole.Body, "star stats in full view")
                .Ink("DimBrush"));
            MiniChips.Children.Add(hint);
        }
    }


    private void OnMinimize(object sender, RoutedEventArgs e) => SetMode(true);
    private void OnRestore(object sender, RoutedEventArgs e) => SetMode(false);

    private void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        _lastUpdateCheck = DateTime.Now;
        CheckForUpdates(manual: true);
    }

    private void CheckForUpdates(bool manual)
    {
        Task.Run(async () =>
        {
            // Best of the shared folder and the GitHub feed. A local folder with a genuine
            // update short-circuits the network; a stale one no longer hides a release.
            var folder = UpdateChecker.FindUpdateFolder(_settings.UpdateFolder);
            var info = await UpdateChecker.FindBestAsync(_settings.UpdateFolder);

            Dispatcher.Invoke(() =>
            {
                if (_installingUpdate) return;
                if (info is not null && UpdateChecker.IsNewer(info))
                {
                    _pendingUpdate = info;
                    // Portable copies never get the silent-install path (#119): the
                    // installer lands elsewhere and the portable exe stays old, which
                    // reads as the update "reverting" on every relaunch.
                    UpdateText.Text = !UpdateChecker.IsInstalledCopy
                        ? $"Update v{info.Latest} is out. You're running the portable copy — click to open the download page, then replace this folder with the new EQBuddy-portable.zip."
                        : info.SetupPath is not null || info.DownloadUrl is not null
                            ? $"Update v{info.Latest} is ready — click here to install."
                            : $"Update v{info.Latest} is available — click to open the download page.";
                    UpdateBanner.Visibility = Visibility.Visible;
                }
                else if (manual)
                {
                    _pendingUpdate = null;
                    UpdateText.Text = info is null && folder is null
                        ? "Couldn't check for updates (no update folder, GitHub unreachable)."
                        : $"You're up to date (v{UpdateChecker.CurrentVersion}).";
                    UpdateBanner.Visibility = Visibility.Visible;
                    _upToDateNoticeUntil = DateTime.Now.AddSeconds(6);
                }
            });
        });
    }

    private void OnUpdateBannerClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pendingUpdate is not { } info || _installingUpdate) return;

        if ((info.SetupPath is null && info.DownloadUrl is null) || !UpdateChecker.IsInstalledCopy)
        {
            // No installer to fetch (a release without one), or a PORTABLE copy (#119) —
            // running Setup.exe from a portable copy installs elsewhere and the portable
            // exe stays old. Send the user to the release page for the right asset.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    UpdateChecker.GitHubLatestPage) { UseShellExecute = true });
                _pendingUpdate = null;
                UpdateText.Text = UpdateChecker.IsInstalledCopy
                    ? "Download page opened — run the new EQBuddySetup.exe to update."
                    : "Download page opened — grab EQBuddy-portable.zip, close EQBuddy, and replace this folder's files with the zip's.";
                _upToDateNoticeUntil = DateTime.Now.AddSeconds(10);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                UpdateText.Text = $"Couldn't open browser — visit {UpdateChecker.GitHubLatestPage}";
            }
            return;
        }

        _installingUpdate = true;
        UpdateText.Text = info.DownloadUrl is not null
            ? "Downloading update — EQBuddy will restart itself…"
            : "Installing update — EQBuddy will restart itself…";
        Task.Run(async () =>
        {
            try
            {
                var staged = await UpdateChecker.StageForInstall(info);
                System.Diagnostics.Process.Start(staged, "/SILENT");
                Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                Dispatcher.Invoke(() =>
                {
                    _installingUpdate = false;
                    UpdateText.Text = "Update failed to start — see error.log.";
                });
            }
        });
    }

    /// <summary>Per-spell resist/block tallies as a row-lookup dict (session-scoped;
    /// empty → null so rows skip the lookup entirely).</summary>
    internal static IReadOnlyDictionary<string, (int Casts, int Resists, int Blocked)>? SpellResistLookup(
        StatsSnapshot s) =>
        s.SpellResists.Count == 0
            ? null
            : s.SpellResists.ToDictionary(x => x.Spell, x => (x.Casts, x.Resists, x.Blocked),
                StringComparer.OrdinalIgnoreCase);

    /// <summary>Tooltip text per blocked spell ("Blocked by: Chloroplast ×3") from the
    /// per-character stacking ledger — only for spells the session actually saw blocked,
    /// so the ledger read stays proportional to what's on screen. Null when nothing was.</summary>
    internal IReadOnlyDictionary<string, string>? BlockedByLookup(StatsSnapshot s)
    {
        if (_stats.StackingStore is not { } store) return null;
        var blockedSpells = s.SpellResists.Where(x => x.Blocked > 0).Select(x => x.Spell).ToList();
        if (blockedSpells.Count == 0) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in blockedSpells)
        {
            var blockers = store.BlockersFor(_stats.LedgerCharacterKey, spell);
            if (blockers.Count == 0) continue;   // blocker-less lines only — no names to show
            result[spell] = "Blocked by: " + string.Join(", ",
                blockers.Select(b => $"{b.BlockedBy} ×{b.Count}"));
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>Details!-style breakdown: proportional bar behind each row with the full
    /// "total · ×hits · avg · rate (· crit%)" columns inline. The rate (dps/hps) uses the
    /// parser convention: ability damage ÷ total time in combat, so an ability's dps
    /// falls the longer you go without using it. The burst rate (total ÷ the ability's
    /// own active time) lives in the tooltip. The bar follows the sorted column.</summary>
    /// <summary>Card lists cap at 30 rows with a spoken overflow line (David's field
    /// report: a long session's Combat card built EVERY ability row ever seen — procs
    /// and clickies included — and first-expand paid seconds of layout for rows below
    /// the fold). Sorting still surfaces anything; breakouts and History stay uncapped.</summary>
    private const int CardRowCap = 30;

    // Internal: the lifted Loot card formats the same stat-block tooltips.
    internal static readonly FontFamily MonoFamily = new("Consolas");

    private void FillBreakdown(ItemsControl list, IEnumerable<SourceDamage> stats,
        StatSort sort, double combatSeconds, string rateLabel,
        IReadOnlyDictionary<string, (int Casts, int Resists, int Blocked)>? resists = null,
        IReadOnlyDictionary<string, string>? blockedBy = null) =>
        BreakdownRows.FillAbilityRowsSorted(this, list, stats, sort, combatSeconds, rateLabel,
            CardRowCap, resists: resists, blockedBy: blockedBy);

    /// <summary>Render a Total/Count/Avg stat list in the chosen sort order.</summary>
    private void FillStatList(ItemsControl list, IEnumerable<SourceDamage> stats, StatSort sort, string unit)
    {
        var sorted = sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(d => (double)d.Total / d.Hits),
            _ => stats.OrderByDescending(d => d.Total),
        };
        FillList(list, sorted.Select(d =>
            (d.Name, $"{d.Total:N0} · {d.Hits} {unit}{(d.Hits == 1 ? "" : "s")} · avg {(double)d.Total / d.Hits:0.#}")));
    }

    // The three sort strips, on the chip primitive (Gate 5). They were bare TextBlocks
    // with a Tag, a click handler and a hand-written ApplyVisual — the exact pattern the
    // whole rework exists to remove, and the one #198 rebuilt by hand because the
    // primitive was private to one window. WHAT each strip offers, and what healing calls
    // its columns, comes from UI.Shared.EQBuddy.UI.Shared.SortStrip.
    private EqSegmentedStrip _dmgOutStrip = null!, _dmgInStrip = null!, _healStrip = null!;

    private void BuildSortStrips()
    {
        _dmgOutStrip = SortStripInto(DmgOutSortBar, EQBuddy.UI.Shared.SortStrip.ForDamage,
            m => { _dmgOutSort = Sort(m); _dmgOutStrip.Select(m); RefreshUi(); });
        _dmgInStrip = SortStripInto(DmgInSortBar, EQBuddy.UI.Shared.SortStrip.ForDamageTaken,
            m => { _dmgInSort = Sort(m); _dmgInStrip.Select(m); RefreshUi(); });
        _healStrip = SortStripInto(HealSortBar, EQBuddy.UI.Shared.SortStrip.ForHealing,
            m => { _healSort = Sort(m); _healStrip.Select(m); RefreshUi(); });
        _dmgOutStrip.Select(Metric(_dmgOutSort));
        _dmgInStrip.Select(Metric(_dmgInSort));
        _healStrip.Select(Metric(_healSort));
    }

    // Mapped by NAME, never by cast: StatSort is {Total,Hits,Avg,Rate} and the shared
    // Metric is {Total,Rate,Hits,Avg}, so the ordinals disagree and a cast would silently
    // sort by the wrong column.
    private static StatSort Sort(EQBuddy.UI.Shared.SortStrip.Metric m) => m switch
    {
        EQBuddy.UI.Shared.SortStrip.Metric.Hits => StatSort.Hits,
        EQBuddy.UI.Shared.SortStrip.Metric.Avg => StatSort.Avg,
        EQBuddy.UI.Shared.SortStrip.Metric.Rate => StatSort.Rate,
        _ => StatSort.Total,
    };

    private static EQBuddy.UI.Shared.SortStrip.Metric Metric(StatSort s) => s switch
    {
        StatSort.Hits => EQBuddy.UI.Shared.SortStrip.Metric.Hits,
        StatSort.Avg => EQBuddy.UI.Shared.SortStrip.Metric.Avg,
        StatSort.Rate => EQBuddy.UI.Shared.SortStrip.Metric.Rate,
        _ => EQBuddy.UI.Shared.SortStrip.Metric.Total,
    };

    /// <summary>One labelled sort strip built into an empty host from the XAML.</summary>
    private static EqSegmentedStrip SortStripInto(Panel host,
        IReadOnlyList<EQBuddy.UI.Shared.SortStrip.Option> options, Action<EQBuddy.UI.Shared.SortStrip.Metric> onPick)
    {
        // No "sort:" caption: this strip sits on the same row as the heading that names
        // the list, and the caption cost that heading its last words in a 342px window.
        // See SortStrip.Caption for when one is worth its width.
        var strip = new EqSegmentedStrip(host);
        foreach (var option in options)
        {
            var metric = option.Metric;
            strip.Add(option.Label, metric, tip: option.Tip, onClick: () => onPick(metric));
        }
        return strip;
    }

    private void OnLootQuestMap(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowQuestsWindow();
    }

    /// <summary>The plain name/value list the un-migrated card BODIES still draw with —
    /// the breakdown lists, the ding unlocks, deaths and zones. <c>EqCardRows</c> is what
    /// replaced it for every card that has been through the seam; the rest is a later
    /// batch (docs/DesignSystem.md §11.9).
    ///
    /// It used to take a <c>questBadges</c> flag that hung a quest map-pin on each item
    /// row. Nothing has passed it since the Loot card moved onto <c>EqCardRows</c>, which
    /// draws that badge itself — so the branch was unreachable, and it was unreachable in
    /// the #211 shape: a bare clickable vector with holes you could click through. Dead
    /// code carrying a bug already paid for is worse than no code.</summary>
    private void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows,
        Func<string, Brush>? valueBrush = null, Action<string>? onNameClick = null,
        Func<string, string?>? tooltip = null, Func<string, Brush?>? nameBrush = null,
        Func<string, string?>? noteFor = null)
    {
        var items = rows.ToList();
        list.Items.Clear();
        foreach (var (name, value) in items)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new TextBlock
            {
                FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = nameBrush?.Invoke(name) ?? (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 1, Tok.SpaceM, 1),
            };
            // Provenance rides inline as a muted "(Foraged)"/"(Crafted)"/… after the name —
            // a separate run, not part of the name, so the click still looks up the base item.
            if (noteFor?.Invoke(name) is { Length: > 0 } note)
            {
                left.Inlines.Add(new System.Windows.Documents.Run(name));
                left.Inlines.Add(new System.Windows.Documents.Run($" {note}")
                {
                    FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                    Foreground = (Brush)FindResource("DimBrush"),
                });
            }
            else left.Text = name;
            if (tooltip?.Invoke(name) is { Length: > 0 } tip)
            {
                var tipText = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
                // Multi-line tips are stat blocks — monospace keeps their columns readable.
                // (Static family: the item catalog made this branch always-taken, and a
                // fresh FontFamily per row per render second was churn — 2026-08-13 review.)
                if (tip.Contains('\n')) tipText.FontFamily = MonoFamily;
                left.ToolTip = new System.Windows.Controls.ToolTip { Content = tipText };
            }
            if (onNameClick is not null)
            {
                var clickName = name;
                left.Cursor = System.Windows.Input.Cursors.Hand;
                left.ToolTip ??= "Click for item info (eqlwiki)";
                // Swallow the down so it can't start a window DragMove and eat the Up
                // (the discussion #46 failure mode, same fix as the breakout rows).
                left.MouseLeftButtonDown += (_, ev) => ev.Handled = true;
                left.MouseLeftButtonUp += (_, _) => onNameClick(clickName);
            }
            var right = new TextBlock
            {
                Text = value, FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                Foreground = valueBrush?.Invoke(value) ?? (Brush)FindResource("DimBrush"),
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            list.Items.Add(grid);
        }
    }

    // ---- hide while the game is unfocused (FOCUS-*, discussion #41) ----

    private bool _hiddenForFocus;

    // Where the constructor placed the window and whether that was the SAVED spot —
    // OnClosed's PositionToPersist call needs both (#117).
    private bool _restoredSavedPosition;
    private double _placedLeft, _placedTop;

    private TrayIcon? _trayIcon;

    private readonly SpawnPointLedger _spawnPoints;
    private readonly SpawnOverrides _spawnOverrides;
    private readonly SpawnCatalog _spawnCatalog;
    internal SpawnPointLedger SpawnPoints => _spawnPoints;
    internal SpawnOverrides SpawnOverridesStore => _spawnOverrides;
    internal SpawnCatalog SpawnCatalogData => _spawnCatalog;

    // What the focus hide took down, so the same windows — and only those — come back.
    // Not "everything that is closed now": a window the player shut while alt-tabbed
    // must stay shut, and one they opened must not be re-hidden on the way back.
    private readonly List<Window> _focusHiddenWindows = [];

    /// <summary>When enabled, the widget hides while the game runs WITHOUT being the
    /// foreground app — alt-tab to a browser and the corner it lives in is the browser's
    /// again. Never hides when the game isn't running (configuring the widget outside the
    /// game must stay possible) or when EQBuddy itself is what has focus (clicking the
    /// widget must not vanish it).
    ///
    /// **Its satellites go with it** (#189, wizen: the Quest Tracker stayed on screen
    /// after the widget vanished). They used to be described as following "via their own
    /// tick gates", which was only ever true of the chips and the breakouts — a window
    /// opened from the ⚙ menu had no gate at all and nothing hid it.
    /// <see cref="EQBuddy.UI.Shared.FocusHide.FollowsWidgetHide"/> names the exceptions;
    /// everything else follows, including windows written after this was.</summary>
    private void UpdateFocusHide()
    {
        var hide = ShouldHideForFocus();
        if (hide == _hiddenForFocus) return;
        _hiddenForFocus = hide;
        Visibility = hide ? Visibility.Hidden : Visibility.Visible;

        if (hide)
        {
            foreach (Window w in Application.Current.Windows)
                if (!ReferenceEquals(w, this) && w.IsVisible
                    && EQBuddy.UI.Shared.FocusHide.FollowsWidgetHide(w.GetType().Name))
                {
                    _focusHiddenWindows.Add(w);
                    w.Hide();
                }
        }
        else
        {
            foreach (var w in _focusHiddenWindows) if (w.IsLoaded) w.Show();
            _focusHiddenWindows.Clear();
        }
    }

    // Perf audit #6: this runs every tick, and both process calls are system-wide
    // walks. The foreground answer is memoized per HWND (same window in front →
    // same verdict), and "is the game running" is refreshed at most every 5 s —
    // a game launch can't matter faster than that.
    private (IntPtr Fg, bool IsGame) _lastFgProbe = (IntPtr.Zero, false);
    private (DateTime At, bool Running) _lastGameProbe = (DateTime.MinValue, false);

    private bool ShouldHideForFocus()
    {
        // Two opt-ins share this gate (#41 unfocused / #114 not running); the actual
        // decision lives in UI.Shared.FocusHide where tests can reach it. Everything
        // up to that call is probe plumbing and early-outs that skip process walks.
        if (!_settings.HideWhenGameUnfocused && !_settings.HideWhenGameNotRunning) return false;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(fg, out var fgPid);
        if (fgPid == (uint)Environment.ProcessId) return false;
        if (fg != _lastFgProbe.Fg)
        {
            bool isGame;
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById((int)fgPid);
                isGame = p.ProcessName.Equals("eqgame", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }   // foreground process already gone — don't flicker
            _lastFgProbe = (fg, isGame);
        }
        if (_lastFgProbe.IsGame) return false;

        // Foreground is some third app: what happens next depends on whether the
        // game is running and which of the two hides is on.
        if (DateTime.Now - _lastGameProbe.At > TimeSpan.FromSeconds(5))
            _lastGameProbe = (DateTime.Now, EqConfig.IsGameRunning());
        return EQBuddy.UI.Shared.FocusHide.Decide(
            _settings.HideWhenGameUnfocused, _settings.HideWhenGameNotRunning,
            foregroundIsSelf: false, foregroundIsGame: false, _lastGameProbe.Running);
    }

    // ---- click-through (INPUT-*) ----
    // Global hotkeys are GONE (Reddit report, 2026-08-06): RegisterHotKey is system-wide,
    // so EQBuddy was eating Ctrl+Shift+T (reopen browser tab) and friends from every app
    // on the machine. Click-through — the one feature that lived only on a hotkey — moved
    // to the right-click menu, with a small clickable 🔒 chip as the way back out (the
    // widget itself can't be clicked while transparent, by definition).

    private System.Windows.Interop.HwndSource? _hwndSource;
    private bool _clickThrough;

    private static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        public const int GwlExstyle = -20;
        public const int WsExTransparent = 0x20;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Point { public int X, Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);
    }

    /// <summary>
    /// Someone launched a second EQBuddy. Surface this one instead — which is almost
    /// certainly what they wanted, since the usual reason to relaunch is that the widget
    /// is hidden or buried behind a fullscreen game.
    /// </summary>
    /// <summary>A second launch left a request in the profile directory rather than
    /// starting a twin (see <see cref="EQBuddy.UI.Shared.SingleInstance"/>). Answering it
    /// is also what TELLS that launch somebody is home — a request nobody consumes times
    /// out and the second copy starts normally, so this must run on every tick and not
    /// only while the widget is visible.
    ///
    /// This replaces a background thread parked on a named EventWaitHandle. The handle
    /// was a Windows facility and the Avalonia build guards the same profile with a lock
    /// file, so the two builds could not see each other and both ran (2026-08-19).</summary>
    private void AnswerSecondLaunch()
    {
        if (EQBuddy.UI.Shared.SingleInstance.ConsumeShowRequest(AppPaths.Dir))
            RestoreFromAnotherInstance();
    }

    internal void RestoreFromAnotherInstance()
    {
        try
        {
            if (Visibility != Visibility.Visible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            // Clear the hide state DIRECTLY — relying on Activate() winning foreground
            // left a visible-but-frozen widget when Windows refused the focus switch
            // (2026-08-13 review): RefreshUi gates on this flag, so a stale true froze
            // stats and kept satellites hidden. An explicit show IS the user's choice.
            _hiddenForFocus = false;
            foreach (var w in _focusHiddenWindows) if (w.IsLoaded) w.Show();
            _focusHiddenWindows.Clear();
            Topmost = true;
            Activate();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this)!;
        _hwndSource.AddHook(_hotkeys.Hook);
        ApplyHotkeys();
        // Under Wine + opt-in only: don't steal focus from a fullscreen game when clicked.
        WineOverlay.MakeNonActivating(this);
    }

    // ---- global hotkeys, opt-in only (#100 — see HotkeyManager) ----

    private readonly HotkeyManager _hotkeys = new();
    private bool _hotkeyHidden;
    private readonly List<Window> _hotkeyHiddenWindows = [];

    /// <summary>Registers whatever the player bound in Options; called at startup
    /// and again after any Options edit.</summary>
    internal void ApplyHotkeys()
    {
        if (_hwndSource is null) return;
        _hotkeys.Apply(_hwndSource.Handle, _settings.Hotkeys, action => Dispatcher.BeginInvoke(() =>
        {
            switch (action)
            {
                case "toggleAll":
                    // The get-out-of-my-way key: everything hides as one, comes back
                    // as it was. Same idea as focus-hide, but on demand.
                    if (_hotkeyHidden)
                    {
                        foreach (var w in _hotkeyHiddenWindows) if (w.IsLoaded) w.Show();
                        _hotkeyHiddenWindows.Clear();
                        _hotkeyHidden = false;
                    }
                    else
                    {
                        foreach (Window w in Application.Current.Windows)
                            if (w.IsVisible) { _hotkeyHiddenWindows.Add(w); w.Hide(); }
                        _hotkeyHidden = _hotkeyHiddenWindows.Count > 0;
                    }
                    break;
                case "toggleMap":
                    if (_mapWindow is { IsLoaded: true, IsVisible: true }) _mapWindow.Hide();
                    else if (_mapWindow is { IsLoaded: true }) _mapWindow.Show();
                    else OnZoneMap(this, new RoutedEventArgs());
                    break;
                case "toggleQuests":
                    if (_questsWindow is { IsLoaded: true, IsVisible: true }) _questsWindow.Hide();
                    else if (_questsWindow is { IsLoaded: true }) _questsWindow.Show();
                    else OnQuestsWindow(this, new RoutedEventArgs());
                    break;
                case "toggleSpawns":
                    if (_spawnsWindow is { IsLoaded: true, IsVisible: true }) _spawnsWindow.Hide();
                    else if (_spawnsWindow is { IsLoaded: true }) _spawnsWindow.Show();
                    else ShowSpawnsWindow(null);
                    break;
                case "toggleClickThrough":
                    OnClickThrough(this, new RoutedEventArgs());
                    break;
                // #100 round two (jlcrisp): the pill/dashboard flip, from the keyboard.
                case "toggleMinimize":
                    SetMode(!_settings.Minimized);
                    break;
            }
        }));
    }

    private ClickThroughChip? _unlockChip;

    // ---- the alignment grid (discussion #34) ----

    private GridOverlayWindow? _gridOverlay;

    /// <summary>Menu toggle and Options checkbox both land here, so they stay in
    /// lockstep (the SetTrackSpawns pattern). The overlay window exists only while
    /// the grid is on — nothing invisible lingers.</summary>
    internal void SetGridOverlay(bool on)
    {
        _settings.ShowGridOverlay = on;
        _settings.Save();
        if (on)
        {
            if (_gridOverlay is not { IsLoaded: true })
                _gridOverlay = new GridOverlayWindow(_settings);
            _gridOverlay.Show();
            _gridOverlay.ApplySpacing();
        }
        else
        {
            _gridOverlay?.Close();
            _gridOverlay = null;
        }
    }

    /// <summary>Live spacing updates from the Options slider.</summary>
    internal void RefreshGridSpacing() => _gridOverlay?.ApplySpacing();

    // ---- inventory (/outputfile inventory, David 2026-08-11) ----

    private InventoryFile.Snapshot? _inventory;
    private InventoryWindow? _inventoryWindow;

    /// <summary>The newest inventory dump for the followed character, adjusted by what
    /// the log has seen since it was written (loot in, sells out — David, 2026-08-11);
    /// the dump itself is memoized, the log overlay is always current. Pass refresh to
    /// re-scan the game folder (the ⟳ button, the held tab).</summary>
    internal InventoryFile.Snapshot? LatestInventory(bool refresh = false)
    {
        if (refresh || _inventory is null)
        {
            _inventory = InventoryFile.FindLatest(_settings.LogFolder, Identity.Character);
            // This is where dumps enter the app (the ⟳ buttons, the quests held tab)
            // — the Gear card's auto-done rides the same load.
            AutoCheckGearFromInventory(_inventory);
        }
        return _inventory?.WithChanges(_stats.ItemsGainedSince(_inventory.WrittenAt));
    }

    /// <summary>One dump, one pass: what the character verifiably OWNS ticks gear
    /// wishes — the raw Entries carry "+N" tiers (Counts folds them off), so the
    /// at-or-above rule holds here too. The stamp keeps a re-scan of the same file
    /// from re-fighting a box the player deliberately unchecked — PERSISTED, so the
    /// truce survives a restart; only a genuinely new dump re-opens the question.</summary>
    private void AutoCheckGearFromInventory(InventoryFile.Snapshot? dump)
    {
        if (dump is null) return;
        var stamp = $"{dump.Path}|{dump.WrittenAt:O}";
        if (stamp == _settings.GearInventoryAppliedStamp) return;
        _settings.GearInventoryAppliedStamp = stamp;
        if (GearLootAutoCheck.ApplyInventory(_settings.GearChecklist, dump.Entries))
        {
            _gearChecklistDirty = true;
            _settings.Save();
            UpdateGearHeaderOnly();
        }
    }

    private void OnInventoryWindow(object sender, RoutedEventArgs e)
    {
        if (_inventoryWindow is { IsLoaded: true } w) { w.Activate(); return; }
        _inventoryWindow = new InventoryWindow(this);
        _inventoryWindow.Show();
    }

    // ---- travel routing + zone maps (competitive gaps #1/#2, 2026-08-10) ----

    private TravelWindow? _travelWindow;
    private MapWindow? _mapWindow;

    private void OnTravelRoute(object sender, RoutedEventArgs e)
    {
        if (_travelWindow is { IsLoaded: true } t) { t.RenderRoute(); t.Activate(); return; }
        _travelWindow = new TravelWindow(this) { Owner = this };
        _travelWindow.Show();
    }

    private void OnZoneMap(object sender, RoutedEventArgs e)
    {
        // Re-opening re-probes: a zone whose map wasn't found (pack unzipped since?)
        // must not stay a cached failure just because the window object lived on.
        if (_mapWindow is { IsLoaded: true } m) { m.MaybeRefresh(force: true); m.Activate(); return; }
        _mapWindow = new MapWindow(this);
        _mapWindow.Show();
    }

    // ---- the cursor-finder ring (issue #81) ----

    private CursorRingWindow? _cursorRing;

    /// <summary>Same lockstep shape as SetGridOverlay: the window exists only while
    /// the ring is on, and the menu check always tells the truth.</summary>
    internal void SetCursorRing(bool on)
    {
        _settings.ShowCursorRing = on;
        _settings.Save();
        if (on)
        {
            if (_cursorRing is not { IsLoaded: true })
                _cursorRing = new CursorRingWindow(_settings);
            _cursorRing.ApplySize();
            _cursorRing.Show();
        }
        else if (_cursorRing is { } ring)
        {
            _cursorRing = null;
            ring.Close();
        }
    }

    private void OnClickThrough(object sender, RoutedEventArgs e) =>
        SetClickThrough(!_clickThrough);

    private void SetClickThrough(bool on)
    {
        if (_hwndSource is null) return;
        _clickThrough = on;
        var style = Native.GetWindowLong(_hwndSource.Handle, Native.GwlExstyle);
        Native.SetWindowLong(_hwndSource.Handle, Native.GwlExstyle,
            _clickThrough ? style | Native.WsExTransparent : style & ~Native.WsExTransparent);
        // Visible but unobtrusive state indicator (INPUT-012).
        RootBorder().BorderBrush = (Brush)FindResource(_clickThrough ? "WarnBrush" : "HairlineBrush");
        RootBorder().ToolTip = _clickThrough
            ? "Click-through ON — click the padlock chip beside the widget to interact again"
            : null;
        ClickThroughItem.IsChecked = _clickThrough;
        // The way back: a transparent widget can't be clicked, so a tiny normal-hit-test
        // chip parks beside it while click-through is on.
        if (_clickThrough)
        {
            _unlockChip ??= new ClickThroughChip(() => SetClickThrough(false));
            _unlockChip.ShowNear(this);
        }
        else
        {
            _unlockChip?.Hide();
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && MiniRoot.Visibility == Visibility.Visible)
        {
            SetMode(false);
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    /// <summary>The tooltip is built on hover rather than fixed in XAML, so it always
    /// describes the archiving setting as it is right now (#159).</summary>
    private void OnResetToolTipOpening(object sender, ToolTipEventArgs e) =>
        ResetButton.ToolTip = EQBuddy.UI.Shared.ResetPrompt.Tooltip(_settings.ArchiveLogs);

    private void OnReset(object sender, RoutedEventArgs e)
    {
        // Ask first when the click will move a file (#159, Frankthetankk — same
        // treatment Epic Complete got after #138). With archiving off nothing but the
        // on-screen numbers change, and ResetPrompt returns null rather than put a
        // dialog in front of an action that cannot lose anything.
        if (EQBuddy.UI.Shared.ResetPrompt.Confirmation(_settings.ArchiveLogs) is { } ask
            && MessageBox.Show(this, ask, EQBuddy.UI.Shared.ResetPrompt.ConfirmationTitle,
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        // History first (audit finding 1): without the finalize, the archiver's row id
        // survived the reset and later checkpoints overwrote the pre-reset session's
        // row with post-reset numbers. Snapshot before the split task and the reset,
        // so what goes to history is exactly what the click saw.
        _archiver.FinalizeActive(_stats.Snapshot(), "ManualReset");
        // With archiving on, reset also splits the log: what's parsed so far moves to
        // Logs\archive and a fresh file begins — the second half of #52's ask.
        if (_settings.ArchiveLogs && _watcher.CurrentPath is { } path)
            Task.Run(() =>
            {
                if (EqConfig.SplitLog(path) is { } dest) AnnounceArchive(dest);
            });
        _stats.Reset();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon?.Dispose();   // a ghost tray icon outliving its process reads as a crash
        _unlockChip?.Close();
        _spawnPoints.Flush();   // debounced archives; anything missed replays from the log
        // Never let an unmoved fallback overwrite a real saved spot (#117).
        (_settings.WindowLeft, _settings.WindowTop) = WindowPlacement.PositionToPersist(
            _restoredSavedPosition, _placedLeft, _placedTop, Left, Top,
            _settings.WindowLeft, _settings.WindowTop);
        _settings.Save();
        foreach (var w in _breakouts.Values) w.Close();   // each persists its spot on Closed
        _stats.QuestStore?.Flush();   // debounced writers get their last word (audit #3)
        _stats.AaStore?.Flush();
        _stats.StackingStore?.Flush();
        _stats.Spells.Flush();        // learned spell categories (audit #13, same idiom)
        _buffTracker.Flush();         // learned buff durations
        if (_reviewPath is null)   // a review session is already history (#74)
            _archiver.FinalizeActiveSync(_stats.Snapshot(), "ApplicationExit");
        _watcher.Dispose();
        _uiTimer.Stop();
        _companionPump.Stop();   // before the host: no pump into a disposed listener
        ThemeManager.PaletteApplied -= _companion.SetTheme;
        _companion.Dispose();   // stop the LAN listener with the app, not after it
        _repo.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
