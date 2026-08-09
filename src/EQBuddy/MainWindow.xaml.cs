using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;
using SpawnChip = EQBuddy.UI.Shared.SpawnChip;

namespace EQBuddy;

public partial class MainWindow : Window
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
    private DateTime _lastCharScan = DateTime.MinValue;
    private DateTime _lastJanitorRun = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private DateTime _upToDateNoticeUntil = DateTime.MinValue;
    private bool _installingUpdate;

    private readonly SpawnTimers _spawnTimers;
    private readonly EQBuddy.UI.Shared.SpawnsViewModel _spawnsVm;
    private SpawnsWindow? _spawnsWindow;
    private readonly Dictionary<string, int> _skyQuestLootSeen = new(StringComparer.OrdinalIgnoreCase);
    // Rebuilding 200+ checkboxes every UI tick is the one thing this overlay never
    // does elsewhere — the checklist re-renders only when a box actually changed.
    private bool _skyQuestDirty = true;

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "pet", "loot", "motes", "money", "xp", "deaths"];

    // StatSort moved to BreakdownRows.cs (internal) when the breakout windows grew
    // their own sort bars — one enum, every surface.
    private StatSort _dmgOutSort = StatSort.Total;
    private StatSort _dmgInSort = StatSort.Total;
    private StatSort _healSort = StatSort.Total;

    public MainWindow()
    {
        InitializeComponent();
        // Before the watcher's startup replay, so already-logged charms classify with
        // everything learned in earlier sessions (issue #29).
        AttachSpellStore();
        _mezTracker.AttachStore(System.IO.Path.Combine(Core.AppPaths.Dir, "mez-durations.json"));
        _stats.AaStore = new AaLedgerStore(AppPaths.File("aa-ledger.json"));
        // Quest ledger rides the same replay: the catalog decides what's worth keeping,
        // the store's time high-water mark keeps the replay from double-counting.
        QuestCatalog = QuestCatalog.LoadEmbedded();
        ZoneGraph = ZoneGraph.LoadEmbedded();
        QuestLedger = new QuestLedgerStore(AppPaths.File("quest-ledger.json"))
        { TrackFilter = QuestCatalog.IsTurnInItem, Normalize = QuestCatalog.BaseItemName };
        _stats.QuestStore = QuestLedger;
        _watcher = new LogWatcher(_stats);
        _watcher.Mez = _mezTracker;
        // Spawn timers ride the watcher's event stream — wired before the first Select so
        // the startup replay re-derives countdowns from kills already in the log.
        var spawnCatalog = SpawnCatalog.LoadEmbedded();
        var spawnOverrides = SpawnOverrides.Load(AppPaths.File("spawn-overrides.json"));
        _spawnTimers = new SpawnTimers(spawnCatalog, spawnOverrides, AppPaths.File("spawn-timers.json"));
        _watcher.Spawns = _spawnTimers;
        _spawnsVm = new EQBuddy.UI.Shared.SpawnsViewModel(spawnCatalog, spawnOverrides, _spawnTimers);
        // Before any tailing: the initial full-log ingest has to know which text rules to
        // watch for, or a Text rule would miss everything already in today's log.
        _stats.RefreshTextPatterns(_settings.TrackedRules);
        _stats.TextMatched += OnTextMatched;
        // An idle gap ended the session: anything still cued belongs to a fight that is
        // long over.
        _stats.SessionRolledOver += () => Dispatcher.BeginInvoke(_delayedAlerts.CancelAll);
        _archiver = new SessionArchiver(_repo);
        // A 60-minute quiet gap ends a session — persist its final state to history.
        _stats.SessionEnding += snap => _archiver.FinalizeActive(snap, "IdleTimeout");

        // Height caps follow the monitor the widget is ON (a portrait secondary screen
        // is taller than the primary — discussion #31); primary work area is only the
        // pre-handle starting value.
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        SectionScroll.MaxHeight = SystemParameters.WorkArea.Height - 160;
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
        if (ScreenGuard.OnScreen(_settings.WindowLeft, _settings.WindowTop, Width, Height))
        { Left = _settings.WindowLeft; Top = _settings.WindowTop; }
        else { Left = SystemParameters.WorkArea.Right - 360; Top = 40; }
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
                if (prune) EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap, archive: archive);
            });
        }

        if (Environment.GetEnvironmentVariable("EQBUDDY_EXPAND") == "1")
            foreach (var ex in new[] { CombatSection, HealingSection, KillsSection, LootSection,
                         MotesSection, SkyQuestSection, TrackedSection, MoneySection,
                         ProgressSection, FactionSection, MiscSection })
                ex.IsExpanded = true;

        if (Environment.GetEnvironmentVariable("EQBUDDY_CCLOG") == "1")
            StartCrowdControlCapture();

        // Screenshot/debug hook, same family as EQBUDDY_OPTIONS: open the Quest Tracker
        // after the startup replay has fed the ledger. "1" opens the default view;
        // "zone"/"all" open that mode directly.
        if (Environment.GetEnvironmentVariable("EQBUDDY_DROPS") == "1")
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => OnDropsWindow(this, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_QUESTS") is { Length: > 0 } questsMode)
            Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                ShowQuestsWindow();
                if (questsMode is "zone" or "all") _questsWindow?.SetMode(questsMode);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_OPTIONS") == "1")
            Loaded += (_, _) => OnOptions(this, new RoutedEventArgs());

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

        TrackSpawnsItem.IsChecked = _settings.TrackSpawns;
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

        SkyQuestTabs.SelectionChanged += OnSkyQuestTabChanged;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();
    }

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
        ["loot"] = LootSection, ["motes"] = MotesSection, ["sky"] = SkyQuestSection,
        ["tracked"] = TrackedSection,
        ["money"] = MoneySection,
        ["progress"] = ProgressSection, ["faction"] = FactionSection, ["misc"] = MiscSection,
    };

    /// <summary>Apply saved card order + hidden set (OVERLAY-001..003). Hidden cards keep collecting.</summary>
    public void ApplySectionLayout()
    {
        var map = SectionMap();
        var order = _settings.SectionOrder.Where(map.ContainsKey).ToList();
        foreach (var (key, _) in SectionCatalog)
            if (!order.Contains(key)) order.Add(key);

        SectionsPanel.Children.Clear();
        foreach (var key in order)
        {
            var el = map[key];
            SectionsPanel.Children.Add(el);
            if (key != "tracked")   // tracked manages its own visibility (no rules = hidden)
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

    /// <summary>A fresh stats snapshot, for windows that refresh on their own cadence.</summary>
    internal StatsSnapshot CurrentSnapshot() => _stats.Snapshot();

    /// <summary>The 🗺 badge signal: a known quest's turn-in OR a member of the wiki's
    /// Quest Items category (back to the broad set once the loud green retired — a
    /// quiet glyph can afford the coverage; David's Crushbone pass, 2026-08-07). When
    /// known quests want the item and ALL are dismissed, the badge goes too.</summary>
    internal bool IsActiveQuestItem(string name)
    {
        var wanting = QuestCatalog.QuestsWanting(name);
        if (wanting.Count == 0) return QuestCatalog.IsQuestItem(name);
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
        const string marker = "🗺 Part of a quest — click the 🗺 to see its quests in the Quest Tracker.";
        return baseTip is { Length: > 0 } ? marker + "\n" + baseTip : marker;
    }


    public double UiScale => _settings.UiScale;

    public void SetUiScale(double scale)
    {
        _settings.UiScale = Math.Clamp(scale, 0.5, 2.0);
        ApplyUiScale(_settings.UiScale);
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
        RootBorder().BorderBrush = (Brush)FindResource(_clickThrough ? "WarnBrush" : "BorderBrush");
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

    private readonly EqlWikiItemService _wikiItems =
        new(System.IO.Path.Combine(Core.AppPaths.Dir, "wiki-cache", "items"));
    private ItemInfoWindow? _itemWindow;

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

    internal (string Header, List<(string Name, string Value)> Rows) TargetDropsContent(StatsSnapshot s)
    {
        var targets = _settings.ShowTargetDrops ? s.CurrentTargets : [];
        if (targets.Count == 0) return ("", []);
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
        var header = $"🎯 Fighting: {names}" +
            (kills > 0 ? $" — {kills} kill{(kills == 1 ? "" : "s")} this session" : "") +
            $" · drops (eqlwiki · {state}{(extra > 0 ? $" · +{extra} more" : "")})";
        return (header, rows);
    }

    private void RenderTargetDrops(StatsSnapshot s)
    {
        var (header, rows) = TargetDropsContent(s);
        if (header.Length == 0)
        {
            TargetBlock.Visibility = Visibility.Collapsed;
            return;
        }
        TargetBlock.Visibility = Visibility.Visible;
        TargetHeader.Text = header;
        FillList(TargetDropsList, rows, onNameClick: ShowItemInfo,
            tooltip: n => QuestAwareTooltip(n, ItemHoverStats(n)), questBadges: true);
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
        SectionScroll.MaxHeight = Math.Max(120, work.Height - 160);
    }

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
                Icon: "💤");
        }).ToList();
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

    private void OnLootSort(object sender, MouseButtonEventArgs e)
    {
        _settings.LootSort = (string)((FrameworkElement)sender).Tag;
        _settings.Save();
        RefreshUi();
        e.Handled = true;
    }

    private void OnPetAbilitiesToggled(object sender, MouseButtonEventArgs e)
    {
        _settings.ShowPetAbilities = !_settings.ShowPetAbilities;
        _settings.Save();
        RefreshUi();
        e.Handled = true;
    }

    private void OnTrackSpawns(object sender, RoutedEventArgs e) =>
        SetTrackSpawns(TrackSpawnsItem.IsChecked);

    private void OnSpawnsWindow(object sender, RoutedEventArgs e) => ShowSpawnsWindow();

    private QuestsWindow? _questsWindow;
    private DropsWindow? _dropsWindow;

    private void OnDropsWindow(object sender, RoutedEventArgs e)
    {
        if (_dropsWindow is not { IsLoaded: true })
            _dropsWindow = new DropsWindow(this);
        _dropsWindow.Update(_stats.Snapshot());
        _dropsWindow.Show();
        _dropsWindow.Activate();
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
        TrackSpawnsItem.IsChecked = on;
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

    /// <summary>Switch to whoever is actively playing: the most recently written log.</summary>
    private void FollowActiveCharacter()
    {
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
            _watcher.Select(active.FilePath);
            _archiver.SetIdentity(_stats.ServerName, _stats.CharacterName);
            CharLabel.Text = active.Display;
        }
    }

    private void RefreshUi()
    {
        UpdateFocusHide();
        _stats.RegenPerTickOverride = _settings.RegenPerTickOverride;

        // Spawn timers crossing zero: banner always, sound only if one is chosen. Runs
        // off the shared tick so a hidden window can't silence a camp.
        if (_questsWindow is { IsLoaded: true, IsVisible: true } qw) qw.MaybeRefresh();
        if (_dropsWindow is { IsLoaded: true, IsVisible: true } dw) dw.MaybeRefresh();

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
        // mez chips park next to the fight, spawn chips are ambient).
        if (!_hiddenForFocus && _mezTracker.Snapshot(DateTime.Now).Count > 0)
        {
            if (_mezWindow is not { IsLoaded: true })
            {
                _mezWindow = new MezChipsWindow(_settings, MezChips, SetChipScale);
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
                if (prune) EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap, archive: archive);
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

        ProcessTrackedAlerts(s);

        // Every 5 min: checkpoint the active session so a crash loses little (RECOVERY-001).
        if (DateTime.Now - _lastCheckpoint > TimeSpan.FromMinutes(5))
        {
            _lastCheckpoint = DateTime.Now;
            _archiver.Checkpoint(s);
        }

        if (MiniRoot.Visibility == Visibility.Visible)
            UpdateMiniChips(s);
        UpdateBreakouts(s);

        ZoneText.Text = s.CurrentZone.Length > 0 ? s.CurrentZone : "—";
        CurrentZoneName = s.CurrentZone;
        var active = TimeSpan.FromSeconds(s.ActiveSeconds);
        SessionText.Text = s.SessionStart is { } start
            ? $"session {(int)s.Elapsed.TotalHours}:{s.Elapsed.Minutes:D2} · active {(int)active.TotalMinutes}m (since {start:h:mm tt})"
            : "waiting for log activity…";

        CombatHeader.Text = s.CurrentDps > 0
            ? $"{s.SessionDps:0} dps (now {s.CurrentDps:0})"
            : $"{s.SessionDps:0} dps";
        KillsHeader.Text = s.PartyKillCount > 0 ? $"{s.YourKillCount} (+{s.PartyKillCount})" : $"{s.YourKillCount}";
        LootHeader.Text = s.CraftedTotal > 0
            ? $"{s.LootTotal} items (+{s.CraftedTotal} made)"
            : $"{s.LootTotal} item{(s.LootTotal == 1 ? "" : "s")}";
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        MotesHeader.Text = motes.Total > 0 ? $"{motes.Total} · {motes.PerHour:0.#}/hr" : "0";
        UpdateSkyQuestChecklist(s);
        MoneyHeader.Text = StatsSnapshot.FormatCoin(s.Copper);
        ProgressHeader.Text = $"{s.XpPercent:0.0}% xp"
            + (s.Levels.Count > 0 ? $", +{s.Levels.Count} lvl" : "")
            + (s.AaGained > 0 ? $", +{s.AaGained} aa" : "");
        FactionHeader.Text = s.Faction.Count > 0 ? $"{s.Faction.Count} factions" : "—";
        MiscHeader.Text = $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}";
        ApplySessionSubsections();

        if (CombatSection.IsExpanded)
        {
            var acc = s.HitCount + s.MissCount > 0
                ? (double)s.HitCount / (s.HitCount + s.MissCount) * 100 : 0;
            var critRate = s.HitCount > 0 ? (double)s.CritCount / s.HitCount * 100 : 0;
            var incomingSwings = s.AvoidedIncoming + s.MeleeHitsTaken;
            var avoidance = incomingSwings > 0
                ? (double)s.AvoidedIncoming / incomingSwings * 100 : 0;
            var combatTime = TimeSpan.FromSeconds(s.CombatSeconds);
            ShowLastFight(s, CombatFightLabel, CombatFightBody, CombatFightText, CombatFightList,
                healing: false, _settings.ShowCombatFight);
            CombatSummary.Text =
                $"Dealt {s.DamageDealt:N0} ({s.MeleeDamage:N0} melee / {s.SpellDamage:N0} spell)\n" +
                $"{s.CritCount} crits ({critRate:0.#}% rate) · {acc:0}% accuracy\n" +
                $"In combat {(int)combatTime.TotalMinutes}m {combatTime.Seconds}s this session\n" +
                (s.Recent is { } rc
                    ? $"Last {(int)rc.Window.TotalMinutes}m: {rc.Dps:0.#} dps{(rc.HasFullWindow ? "" : " (partial window)")}\n"
                    : "") +
                $"Biggest hit: {s.MaxHit:N0} ({s.MaxHitDesc})\n" +
                $"Taken {s.DamageTaken:N0} · avoided {s.AvoidedIncoming} of {incomingSwings} melee attacks ({avoidance:0}%)" +
                (s.SpecialHits.Count > 0
                    ? "\n" + string.Join(" · ", s.SpecialHits.Select(x => $"{x.Name} {x.Count}"))
                    : "") +
                (s.DotDamage + s.DirectSpellDamage > 0
                    ? $"\nYour spells: {s.DotDamage:N0} over time / {s.DirectSpellDamage:N0} direct"
                    : "") +
                // Cast completion subsumes the fizzle count, so only show the old
                // fizzle/resist line for logs with no cast lines in them.
                (s.CastCompletion is { } completion
                    ? $"\nCasts {s.CastsStarted} · {completion * 100:0}% completed" +
                      $" ({s.CastsInterrupted} interrupted · {s.Fizzles} fizzled · {s.Resists} resisted)"
                    : s.Fizzles + s.Resists > 0 ? $"\nFizzles {s.Fizzles} · resists {s.Resists}" : "") +
                (s.CurrentStance.Length > 0 ? $"\nStance: {s.CurrentStance}" : "");
            FillBreakdown(DamageSourceList, s.DamageBySource, _dmgOutSort, s.CombatSeconds, "dps");
            // Shares the damage sort bar above it — it's the same rows, one level down.
            // Collapsed to one line by default (asked for in discussion #28 by a pet
            // class drowning in rows): the pet's overall damage is already a row in the
            // list above; the per-ability split is a click away.
            PetAbilityLabel.Visibility = s.PetAbilities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PetAbilityLabel.Text = _settings.ShowPetAbilities
                ? "▾ Pet abilities"
                : $"▸ Pet abilities ({s.PetAbilities.Count})";
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
            HealingSummary.Text =
                $"Done {s.HealingDone:N0} · received {s.HealingReceived:N0}" +
                (s.Recent is { Hps: > 0 } rh
                    ? $"\nLast {(int)rh.Window.TotalMinutes}m: {rh.Hps:0.#} hps"
                    : "") +
                (s.RegenTicks > 0 ? "\n" + RegenLine(s) : "") +
                (s.RuneBlockCount > 0
                    ? $"\nRune absorbed {s.RuneBlockCount} hit{(s.RuneBlockCount == 1 ? "" : "s")}" +
                      $" (best streak {s.RuneBlockStreakMax}" +
                      (s.RuneBlockStreak > 0 ? $", current {s.RuneBlockStreak}" : "") + ")"
                    : "");
            var showSpells = s.HealsBySpell.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            HealSpellsLabel.Visibility = showSpells;
            HealSortBar.Visibility = showSpells;
            FillBreakdown(HealSpellList, s.HealsBySpell, _healSort, s.CombatSeconds, "hps");
            HealersLabel.Visibility = s.HealsByHealer.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(HealerList, s.HealsByHealer.Select(h =>
                (h.Name, $"{h.Total:N0} · {h.Hits} heal{(h.Hits == 1 ? "" : "s")}")));
        }

        if (KillsSection.IsExpanded)
        {
            KillsSummary.Text = $"{s.KillsPerHour:0.0} kills/hr · {s.KillsPerActiveHour:0.0} active" +
                (s.Recent is { } rk ? $" · last {(int)rk.Window.TotalMinutes}m: {rk.Kills}" : "");
            FillList(KillList, s.YourKills.Select(k => (k.Name, $"×{k.Count}")));
            var farmed = s.Mobs.Where(m => m.Kills > 0).ToList();
            FarmingLabel.Visibility = farmed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var farmRows = new List<(string, string)>();
            foreach (var m in farmed)
            {
                farmRows.Add((m.Name,
                    $"avg {m.AvgFightSeconds:0}s · {StatsSnapshot.FormatCoin(m.Copper)} · {m.XpPercent:0.0}% xp"));
                foreach (var l in m.Loot)
                    farmRows.Add(($"      {l.Item}",
                        l.DropRatePct is { } pct ? $"×{l.Count} · {pct:0}%" : $"×{l.Count}"));
            }
            FillList(FarmingList, farmRows);
            var showParty = s.PartyKillsByKiller.Count > 0;
            PartyKillsLabel.Visibility = showParty ? Visibility.Visible : Visibility.Collapsed;
            FillList(PartyKillList, s.PartyKillsByKiller.Select(k => (k.Name, $"×{k.Count}")));
        }

        if (LootSection.IsExpanded)
        {
            var byName = _settings.LootSort == "name";
            LootSortBar.Visibility = s.Loot.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            LootSortName.Foreground = (Brush)FindResource(byName ? "AccentBrush" : "DimBrush");
            LootSortCount.Foreground = (Brush)FindResource(byName ? "DimBrush" : "AccentBrush");
            var loot = byName
                ? s.Loot.OrderBy(l => l.Item, StringComparer.OrdinalIgnoreCase).AsEnumerable()
                : s.Loot;
            FillList(LootList, loot.Select(l => (l.Item, $"×{l.Count}")), onNameClick: ShowItemInfo,
                tooltip: n => QuestAwareTooltip(n, ItemHoverStats(n)), questBadges: true);
            CraftedLabel.Visibility = s.Crafted.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(CraftedList, s.Crafted.Select(c => (c.Name, $"×{c.Count}")));
            RenderTargetDrops(s);
        }

        if (MotesSection.IsExpanded)
        {
            MotesSummaryText.Text = motes.Total > 0
                ? $"{motes.PerHour:0.#} motes/hr this session"
                : "No motes yet this session — every Mote of … Potential you loot " +
                  "(or store as currency) lands here.";
            FillList(MotesList, motes.Tiers.Select(t => (t.Item, $"×{t.Count}")),
                onNameClick: ShowItemInfo, tooltip: ItemHoverStats);
        }

        if (SkyQuestSection.IsExpanded && _skyQuestDirty)
        {
            RenderSkyQuestChecklist();
            _skyQuestDirty = false;
        }

        if (MoneySection.IsExpanded)
        {
            MoneySummary.Text =
                $"Corpses {StatsSnapshot.FormatCoin(s.CorpseCopper)} ({s.CoinDrops} drops, biggest {StatsSnapshot.FormatCoin(s.BiggestDrop)})\n" +
                $"Merchant sales {StatsSnapshot.FormatCoin(s.VendorCopper)} ({s.SalesCount} sales)\n" +
                $"{StatsSnapshot.FormatCoin(s.CopperPerHour)} per hour · {StatsSnapshot.FormatCoin(s.CopperPerActiveHour)} per active hour" +
                (s.Recent is { } rm ? $"\nLast {(int)rm.Window.TotalMinutes}m: {StatsSnapshot.FormatCoin(rm.Copper)}" : "");
            SoldLabel.Visibility = s.SoldItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(SoldList, s.SoldItems.Select(i =>
                ($"{i.Item}{(i.Count > 1 ? $" ×{i.Count}" : "")}", StatsSnapshot.FormatCoin(i.Copper))));
        }

        if (ProgressSection.IsExpanded)
        {
            ProgressSummary.Text =
                $"{s.XpTicks} xp gains · {s.XpPerHour:0.0}%/hr · {s.XpPerActiveHour:0.0}% active · {s.SkillUpTotal} skill-ups" +
                (s.Recent is { } rx ? $"\nLast {(int)rx.Window.TotalMinutes}m: {rx.XpPerHour:0.0}%/hr" : "") +
                (s.AaGained > 0
                    ? $"\n{s.AaGained} AA point{(s.AaGained == 1 ? "" : "s")} · {s.AaPerHour:0.0} AA/hr (now {s.AaTotal} unspent)"
                    : "") +
                (s.HoursToLevel is { } eta ? $"\nNext level in {FormatEta(eta)} at this pace" : "") +
                (s.Levels.Count > 0
                    ? "\n" + string.Join(", ", s.Levels.Select((l, i) =>
                    {
                        var from = i == 0 ? s.SessionStart : s.Levels[i - 1].Time;
                        var mins = from is { } f ? (int)(l.Time - f).TotalMinutes : 0;
                        return $"{l.Text} at {l.Time:h:mm tt} ({mins}m)";
                    }))
                    : "");
            FillList(SkillList, s.SkillUps.Select(k => (k.Skill, $"{k.Value} (+{k.Ups})")));
            AaAbilitiesLabel.Visibility = s.AaAbilities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(AaAbilityList, s.AaAbilities.Select(a =>
                    (a.Name, a.Rank > 1 ? $"rank {a.Rank}" : "")),
                tooltip: name => AaCatalog.Find(name)?.Effect);
        }

        if (FactionSection.IsExpanded)
            FillList(FactionList, s.Faction.Select(f =>
                (f.Faction, EQBuddy.UI.Shared.FactionFormat.Net(f))),
                valueBrush: f => f.StartsWith('-') ? (Brush)FindResource("BadBrush") : (Brush)FindResource("GoodBrush"));

        RenderTracked(s);

        if (MiscSection.IsExpanded)
        {
            FillList(DeathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
            FillList(ZoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
            MarkersLabel.Visibility = s.Markers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(MarkerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
        }

        if (Environment.GetEnvironmentVariable("EQBUDDY_EXPAND") == "1")
        {
            try
            {
                var dump = $"dmgSrc={DamageSourceList.Items.Count} dmgTaken={DamageTakenList.Items.Count} " +
                    $"kills={KillList.Items.Count} party={PartyKillList.Items.Count} loot={LootList.Items.Count} " +
                    $"crafted={CraftedList.Items.Count} skills={SkillList.Items.Count} faction={FactionList.Items.Count} " +
                    $"zones={ZoneList.Items.Count} deaths={DeathList.Items.Count} " +
                    $"actualH={ActualHeight:0} actualW={ActualWidth:0}";
                System.IO.File.WriteAllText(Core.AppPaths.File("debug.txt"), dump);
            }
            catch { }
        }
    }

    // ---- watch rules: rendering + alerts ----

    // Keyed by TrackedRule.Id — a display name can be shared by two rules, and keying
    // on it made same-named rules share baselines and cooldowns.
    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.Ordinal);
    private readonly EQBuddy.UI.Shared.AlertCooldowns _ruleCooldowns = new();
    private readonly EQBuddy.UI.Shared.SoundGate _soundGate = new();
    private string? _alertBaselinePath;
    private AlertWindow? _alertWindow;

    /// <summary>The floating alert tile — created on first use, owned by the widget.</summary>
    internal AlertWindow AlertTile => _alertWindow ??= new AlertWindow(_settings) { Owner = this };

    private void RenderTracked(StatsSnapshot s)
    {
        var haveRules = _settings.TrackedRules.Count > 0 &&
                        !_settings.HiddenSections.Contains("tracked");
        TrackedSection.Visibility = haveRules ? Visibility.Visible : Visibility.Collapsed;
        if (!haveRules) return;

        TrackedHeader.Text = s.Tracked.Sum(t => t.TotalQuantity).ToString();
        if (!TrackedSection.IsExpanded) return;

        TrackedPanel.Children.Clear();
        var dueByRule = _delayedAlerts.NextDueByRule(DateTime.Now);
        foreach (var r in s.Tracked)
        {
            var head = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // A rule with a cue counting down says so in its heading, so you can watch the
            // respawn timer you set without opening Options to remember what it was.
            var counting = dueByRule.TryGetValue(r.Id, out var dueAt);
            head.Children.Add(new TextBlock
            {
                Text = counting
                    ? $"{r.Name.ToUpperInvariant()} ⏳ {EQBuddy.UI.Shared.Countdown.Format(dueAt - DateTime.Now)}"
                    : r.Name.ToUpperInvariant(),
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource(counting ? "WarnBrush" : "AccentBrush"),
            });
            var rate = new TextBlock
            {
                Text = $"{r.TotalQuantity} total · {r.PerHour:0.#}/hr · {r.PerActiveHour:0.#}/active hr",
                FontSize = 11, Foreground = (Brush)FindResource("DimBrush"),
            };
            Grid.SetColumn(rate, 1);
            head.Children.Add(rate);
            TrackedPanel.Children.Add(head);

            // The card leads with what just happened, not with everything that ever did
            // (asked for by an enchanter drowning in an hour of mez targets): one
            // "last:" line per rule, the full per-item breakdown behind a toggle.
            if (r.LastMatch is { } lm && r.LastItem is { } li)
                TrackedPanel.Children.Add(new TextBlock
                {
                    Text = $"last: {li} · {FormatAge(DateTime.Now - lm)} ago", FontSize = 12,
                    Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(6, 1, 0, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            else
                TrackedPanel.Children.Add(new TextBlock
                {
                    Text = "no matches yet", FontSize = 11,
                    Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(6, 1, 0, 2),
                });

            if (r.Items.Count > 1)
            {
                var expanded = _watchExpandedRules.Contains(r.Id);
                if (expanded)
                    foreach (var item in r.Items)
                        TrackedPanel.Children.Add(new TextBlock
                        {
                            Text = $"{item.Name}   ×{item.Count}", FontSize = 12,
                            Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(12, 1, 0, 0),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        });
                var toggle = new TextBlock
                {
                    Text = expanded ? "▾ less" : $"▸ all {r.Items.Count} kinds",
                    FontSize = 11, Cursor = System.Windows.Input.Cursors.Hand,
                    Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(6, 0, 0, 2),
                };
                var id = r.Id;
                toggle.MouseLeftButtonDown += (_, e) =>
                {
                    if (!_watchExpandedRules.Remove(id)) _watchExpandedRules.Add(id);
                    RefreshUi();
                    e.Handled = true;
                };
                TrackedPanel.Children.Add(toggle);
            }
        }
    }

    /// <summary>Rules whose full per-item breakdown is open on the Watch card.
    /// Session-scoped on purpose: the collapsed "last:" view is the designed default.</summary>
    private readonly HashSet<string> _watchExpandedRules = new(StringComparer.Ordinal);

    private static string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"{Math.Max(0, (int)age.TotalSeconds)}s"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m" : $"{(int)age.TotalHours}h {age.Minutes}m";

    private void UpdateSkyQuestChecklist(StatsSnapshot s)
    {
        var changed = AutoCheckSkyQuestLoot(s);
        UpdateSkyQuestHeaderOnly();
        if (changed)
        {
            _skyQuestDirty = true;
            _settings.Save();
        }
    }

    private bool AutoCheckSkyQuestLoot(StatsSnapshot s)
    {
        var changed = false;
        // Quest item names repeat across classes (five classes need a Wind Rune
        // Azia); only tick boxes for the class whose tab the player works in.
        var cls = _settings.SkyQuestClass;
        var lootByName = s.Loot
            .GroupBy(l => l.Item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Count), StringComparer.OrdinalIgnoreCase);

        foreach (var key in _skyQuestLootSeen.Keys.ToList())
            if (!lootByName.ContainsKey(key))
                _skyQuestLootSeen[key] = 0;

        foreach (var (name, count) in lootByName)
        {
            _skyQuestLootSeen.TryGetValue(name, out var seen);
            if (count <= seen)
            {
                _skyQuestLootSeen[name] = count;
                continue;
            }

            var newlyLooted = count - seen;
            _skyQuestLootSeen[name] = count;
            foreach (var item in _settings.SkyQuestChecklist
                         .Where(i => !i.Acquired
                             && (cls.Length == 0 || string.Equals(i.ClassName, cls, StringComparison.Ordinal))
                             && string.Equals(i.QuestItem, name, StringComparison.OrdinalIgnoreCase))
                         .Take(newlyLooted))
            {
                item.Acquired = true;
                changed = true;
            }
        }

        return changed;
    }

    private void RenderSkyQuestChecklist()
    {
        // Live selection wins; the persisted class restores the tab across restarts.
        var selectedClass = (SkyQuestTabs.SelectedItem as TabItem)?.Tag as string
            ?? (_settings.SkyQuestClass.Length > 0 ? _settings.SkyQuestClass : null);
        SkyQuestTabs.Items.Clear();

        foreach (var classGroup in _settings.SkyQuestChecklist.GroupBy(i => i.ClassName).OrderBy(g => g.Key))
        {
            var classTotal = classGroup.Count();
            var classDone = classGroup.Count(i => i.Acquired);
            var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

            foreach (var rewardGroup in classGroup.GroupBy(i => i.Reward).OrderBy(g => g.Key))
            {
                // The reward line is itself a checkbox: "I turned this in" (#73).
                // Manual only — the log shows nothing reliable at the NPC hand-over.
                var completed = IsSkyRewardCompleted(classGroup.Key, rewardGroup.Key);
                var rewardItems = rewardGroup.ToList();
                var rewardCheck = new CheckBox
                {
                    IsChecked = completed,
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 6, 0, 1),
                    ToolTip = $"{rewardGroup.Key} - {rewardGroup.First().Npc}\n" +
                              "Check when you've turned everything in — quest complete.",
                    Content = new TextBlock
                    {
                        Text = completed ? $"✔ {rewardGroup.Key}" : rewardGroup.Key,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("AccentBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                };
                rewardCheck.Checked += (_, _) =>
                    OnSkyRewardToggled(classGroup.Key, rewardGroup.Key, rewardItems, true);
                rewardCheck.Unchecked += (_, _) =>
                    OnSkyRewardToggled(classGroup.Key, rewardGroup.Key, rewardItems, false);
                panel.Children.Add(rewardCheck);

                foreach (var item in rewardGroup.OrderBy(i => i.QuestItem))
                {
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock
                    {
                        Text = item.QuestItem,
                        FontSize = 12,
                        Foreground = (Brush)FindResource("TextBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                    text.Children.Add(new TextBlock
                    {
                        Text = item.Source,
                        FontSize = 10,
                        Foreground = (Brush)FindResource("DimBrush"),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });

                    var check = new CheckBox
                    {
                        IsChecked = item.Acquired,
                        Content = text,
                        Margin = new Thickness(0, 1, 0, 1),
                        ToolTip = $"{item.Reward}: {item.QuestItem} ({item.Source})",
                        // A completed quest's items are history, not a to-do list.
                        IsEnabled = !completed,
                        Opacity = completed ? 0.55 : 1.0,
                    };
                    check.Checked += (_, _) => OnSkyQuestToggled(item, true);
                    check.Unchecked += (_, _) => OnSkyQuestToggled(item, false);
                    panel.Children.Add(check);
                }
            }

            var tab = new TabItem
            {
                Header = $"{ClassAbbrev(classGroup.Key)} {classDone}/{classTotal}",
                Tag = classGroup.Key,
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = SkyQuestListMaxHeight(),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    PanningMode = PanningMode.VerticalOnly,
                    Padding = new Thickness(0, 0, 4, 0),
                },
                ToolTip = classGroup.Key,
            };
            SkyQuestTabs.Items.Add(tab);
            if (string.Equals(selectedClass, classGroup.Key, StringComparison.Ordinal))
                SkyQuestTabs.SelectedItem = tab;
        }

        if (SkyQuestTabs.SelectedIndex < 0 && SkyQuestTabs.Items.Count > 0)
            SkyQuestTabs.SelectedIndex = 0;
    }

    private double SkyQuestListMaxHeight()
    {
        var available = SectionScroll.MaxHeight > 0 ? SectionScroll.MaxHeight - 220 : 260;
        return Math.Clamp(available, 180, 320);
    }

    private static string SkyRewardKey(string className, string reward) => className + "|" + reward;

    private bool IsSkyRewardCompleted(string className, string reward) =>
        _settings.SkyQuestCompleted.Contains(SkyRewardKey(className, reward));

    /// <summary>Reward turned in (#73): completing checks the reward's items too —
    /// they were acquired and then handed over. Unchecking reopens the quest but
    /// leaves the item boxes as they were; the player knows what they still hold.</summary>
    private void OnSkyRewardToggled(string className, string reward,
        List<SkyQuestChecklistItem> items, bool done)
    {
        var key = SkyRewardKey(className, reward);
        if (done)
        {
            if (!_settings.SkyQuestCompleted.Contains(key)) _settings.SkyQuestCompleted.Add(key);
            foreach (var i in items) i.Acquired = true;
        }
        else
        {
            _settings.SkyQuestCompleted.Remove(key);
        }
        _settings.Save();
        UpdateSkyQuestHeaderOnly();
        _skyQuestDirty = true;   // rebuild next tick: ✔ label, dimmed items, counts
    }

    /// <summary>Manual toggle: the box itself is already right, so only the counts
    /// need refreshing — no rebuild, the control under the cursor stays put.</summary>
    private void OnSkyQuestToggled(SkyQuestChecklistItem item, bool acquired)
    {
        item.Acquired = acquired;
        _settings.Save();
        UpdateSkyQuestHeaderOnly();
        UpdateSkyQuestTabHeader(item.ClassName);
    }

    /// <summary>Persist the class tab the player works in — it scopes loot auto-check
    /// and picks the tab shown after a restart.</summary>
    private void OnSkyQuestTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Items.Clear() during a rebuild fires this with no selection — ignore.
        if ((SkyQuestTabs.SelectedItem as TabItem)?.Tag is string cls &&
            !string.Equals(_settings.SkyQuestClass, cls, StringComparison.Ordinal))
        {
            _settings.SkyQuestClass = cls;
            _settings.Save();
        }
    }

    private void UpdateSkyQuestTabHeader(string className)
    {
        foreach (var tab in SkyQuestTabs.Items.OfType<TabItem>())
            if (string.Equals(tab.Tag as string, className, StringComparison.Ordinal))
            {
                var done = _settings.SkyQuestChecklist.Count(i =>
                    string.Equals(i.ClassName, className, StringComparison.Ordinal) && i.Acquired);
                var total = _settings.SkyQuestChecklist.Count(i =>
                    string.Equals(i.ClassName, className, StringComparison.Ordinal));
                tab.Header = $"{ClassAbbrev(className)} {done}/{total}";
            }
    }

    private void UpdateSkyQuestHeaderOnly()
    {
        var total = _settings.SkyQuestChecklist.Count;
        var acquired = _settings.SkyQuestChecklist.Count(i => i.Acquired);
        SkyQuestHeader.Text = $"{acquired}/{total}";
    }

    private static string ClassAbbrev(string className) => className switch
    {
        "Bard" => "BRD",
        "Beastlord" => "BST",
        "Berserker" => "BER",
        "Cleric" => "CLR",
        "Druid" => "DRU",
        "Enchanter" => "ENC",
        "Magician" => "MAG",
        "Monk" => "MNK",
        "Necromancer" => "NEC",
        "Paladin" => "PAL",
        "Ranger" => "RNG",
        "Rogue" => "ROG",
        "Shadow Knight" => "SHD",
        "Shaman" => "SHM",
        "Warrior" => "WAR",
        "Wizard" => "WIZ",
        _ => className,
    };

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
            AlertTile.ShowAlert($"★ {ruleName}: {label}",
                EQBuddy.UI.Shared.AlertColors.Hex(rule.AlertColor));
        if (EQBuddy.UI.Shared.AlertSoundCatalog.Resolve(rule, _settings.AlertSound) is { } sound)
            PlayAlertSound(sound, coalesce: true);
        if (rule.AlertSpeech)
            EQBuddy.UI.Shared.SpokenAlerts.Speak(label);
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
        label.Content = $"{(open ? "▾" : "▸")} {(f.InProgress ? "Current fight" : "Last fight")}";
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
        CombatSessionLabel.Content = (_settings.ShowCombatSession ? "▾" : "▸") + " Session so far";
        CombatSessionBody.Visibility = _settings.ShowCombatSession ? Visibility.Visible : Visibility.Collapsed;
        HealSessionLabel.Content = (_settings.ShowHealSession ? "▾" : "▸") + " Session so far";
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
            foreach (var r in s.Tracked) _ruleBaseline[r.Id] = r.TotalQuantity;
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
                continue;
            }
            var delta = r.TotalQuantity - baseline;
            _ruleBaseline[r.Id] = r.TotalQuantity;

            var rule = _settings.TrackedRules.FirstOrDefault(x => x.Id == r.Id);
            if (rule is null) continue;
            // Text rules already alerted from the ingest thread the moment the line
            // arrived (OnTextMatched). The baseline above still had to move so this rule
            // doesn't look like a fresh burst later.
            if (rule.Kind == WatchKind.Text) continue;

            AlertOrCue(rule, r.Name, EQBuddy.UI.Shared.WatchAlertText.MatchLabel(rule, r, delta),
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
            // Legacy SystemSounds names from earlier settings map onto the palette.
            var choice = EQBuddy.UI.Shared.AlertSoundCatalog.Normalize(choiceOrPath);
            var named = Array.Find(AlertSounds, x => x.Name == choice);
            var file = named.File is { } f
                ? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", f)
                : choice;
            if (System.IO.File.Exists(file))
            {
                _alertPlayer ??= new System.Windows.Media.MediaPlayer();
                // MediaPlayer defaults to HALF volume; this line was the whole
                // "alerts are very quiet" report.
                _alertPlayer.Volume = Math.Clamp(_settings.AlertVolume, 0.0, 1.0);
                _alertPlayer.Open(new Uri(file));
                _alertPlayer.Play();
                return;
            }
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception ex) { App.LogError(ex); }
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
        yield return ("kills", StarKills);
        yield return ("loot", StarLoot);
        yield return ("motes", StarMotes);
        yield return ("money", StarMoney);
        yield return ("xp", StarXp);
        yield return ("deaths", StarDeaths);
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
                        // The ✕ is a small target floating over a game screen, and until
                        // now the only trace of hitting it was a window that quietly never
                        // came back — David lost his DPS breakout to exactly that
                        // (2026-08-08) with no way to reconstruct when or how. A permanent
                        // state change must announce itself, and leave a timestamp behind.
                        AlertTile.ShowAlert($"{k} breakout hidden — re-enable in ⚙ Options → Breakout windows");
                        CoreLog.Error($"{k} breakout hidden via its ✕ (re-enable: Options → Breakout windows)");
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

    private void UpdateMiniChips(StatsSnapshot s)
    {
        MiniChips.Children.Clear();
        var selected = MiniStatOrder.Where(_settings.MiniStats.Contains).ToList();
        foreach (var key in selected)
        {
            var text = key switch
            {
                "kills" => $"\U0001F480 {s.YourKillCount}",
                "dps" => s.CurrentDps > 0 ? $"⚔ {s.CurrentDps:0} dps" : $"⚔ {s.SessionDps:0} dps",
                "hps" => $"✚ {s.Hps:0.#} hps",
                "pet" => $"🐾 {s.PetAbilities.Sum(p => p.Total) / Math.Max(1, s.CombatSeconds):0.#} dps",
                "loot" => $"\U0001F392 {s.LootTotal}",
                "motes" => Motes.Summarize(s.Loot, s.Elapsed) is { Total: > 0 } mo
                    ? $"\U0001F52E {mo.Total} · {mo.PerHour:0.#}/hr" : "\U0001F52E 0",
                "money" => $"\U0001F4B0 {StatsSnapshot.FormatCoin(s.Copper)}",
                // Rate, not total: minimized is farming mode, and "how fast am I
                // gaining" is the number a farmer watches (MorrolanTV, discussion #63).
                "xp" => $"\U0001F4C8 {s.XpPerHour:0.#}%/hr" +
                        (s.HoursToLevel is { } eta ? $" · lvl {FormatEta(eta)}" : ""),
                "deaths" => $"☠ {s.Deaths.Count}",
                _ => "",
            };
            MiniChips.Children.Add(new TextBlock
            {
                Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
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
            var text = counting
                ? $"⏳ {name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}"
                : $"🎯 {name} {result?.TotalQuantity ?? 0}";
            MiniChips.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource(counting ? "WarnBrush" : "AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
        }

        // The hint belongs at the end, and only when there's genuinely nothing to show. It
        // used to return early when no stats were starred, which meant someone who pinned
        // watch rules but starred nothing got the hint instead of their chips.
        if (MiniChips.Children.Count == 0)
            MiniChips.Children.Add(new TextBlock
            {
                Text = "☆ star stats in full view", FontSize = 12,
                Foreground = (Brush)FindResource("DimBrush"), VerticalAlignment = VerticalAlignment.Center,
            });
    }


    private static string FormatEta(double hours) => hours >= 1
        ? $"~{(int)hours}h {(int)((hours - (int)hours) * 60)}m"
        : $"~{Math.Max(1, (int)(hours * 60))}m";

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
                    UpdateText.Text = info.SetupPath is not null || info.DownloadUrl is not null
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

        if (info.SetupPath is null && info.DownloadUrl is null)
        {
            // No installer to fetch (e.g. a release that shipped without one) — send the
            // user to the GitHub release page instead.
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    UpdateChecker.GitHubLatestPage) { UseShellExecute = true });
                _pendingUpdate = null;
                UpdateText.Text = "Download page opened — run the new EQBuddySetup.exe to update.";
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

    /// <summary>Details!-style breakdown: proportional bar behind each row with the full
    /// "total · ×hits · avg · rate (· crit%)" columns inline. The rate (dps/hps) uses the
    /// parser convention: ability damage ÷ total time in combat, so an ability's dps
    /// falls the longer you go without using it. The burst rate (total ÷ the ability's
    /// own active time) lives in the tooltip. The bar follows the sorted column.</summary>
    private void FillBreakdown(ItemsControl list, IEnumerable<SourceDamage> stats,
        StatSort sort, double combatSeconds, string rateLabel) =>
        BreakdownRows.FillAbilityRowsSorted(this, list, stats, sort, combatSeconds, rateLabel);

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

    private static StatSort ParseSort(object sender) => (string)((FrameworkElement)sender).Tag switch
    {
        "hits" => StatSort.Hits,
        "avg" => StatSort.Avg,
        "rate" => StatSort.Rate,
        _ => StatSort.Total,
    };

    private void SetSortVisual(StatSort mode, TextBlock total, TextBlock hits, TextBlock avg,
        TextBlock? rate = null)
    {
        total.Foreground = (Brush)FindResource(mode == StatSort.Total ? "AccentBrush" : "DimBrush");
        hits.Foreground = (Brush)FindResource(mode == StatSort.Hits ? "AccentBrush" : "DimBrush");
        avg.Foreground = (Brush)FindResource(mode == StatSort.Avg ? "AccentBrush" : "DimBrush");
        if (rate is not null)
            rate.Foreground = (Brush)FindResource(mode == StatSort.Rate ? "AccentBrush" : "DimBrush");
    }

    private void OnSortDmgOut(object sender, MouseButtonEventArgs e)
    {
        _dmgOutSort = ParseSort(sender);
        SetSortVisual(_dmgOutSort, DmgOutSortTotal, DmgOutSortHits, DmgOutSortAvg, DmgOutSortDps);
        RefreshUi();
    }

    private void OnSortDmgIn(object sender, MouseButtonEventArgs e)
    {
        _dmgInSort = ParseSort(sender);
        SetSortVisual(_dmgInSort, DmgInSortTotal, DmgInSortHits, DmgInSortAvg);
        RefreshUi();
    }

    private void OnSortHeal(object sender, MouseButtonEventArgs e)
    {
        _healSort = ParseSort(sender);
        SetSortVisual(_healSort, HealSortTotal, HealSortHits, HealSortAvg, HealSortHps);
        RefreshUi();
    }

    private void OnLootQuestMap(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowQuestsWindow();
    }

    private void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows,
        Func<string, Brush>? valueBrush = null, Action<string>? onNameClick = null,
        Func<string, string?>? tooltip = null, Func<string, Brush?>? nameBrush = null,
        bool questBadges = false)
    {
        var items = rows.ToList();
        list.Items.Clear();
        foreach (var (name, value) in items)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new TextBlock
            {
                Text = name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = nameBrush?.Invoke(name) ?? (Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 1, 8, 1),
            };
            if (tooltip?.Invoke(name) is { Length: > 0 } tip)
            {
                var tipText = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
                // Multi-line tips are stat blocks — monospace keeps their columns readable.
                if (tip.Contains('\n')) tipText.FontFamily = new FontFamily("Consolas");
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
            if (questBadges && IsActiveQuestItem(name))
            {
                // 🗺 next to quest loot → the Quest Tracker, filtered to this item's
                // quests; each card's name opens the wiki walkthrough from there
                // (David's final shape, 2026-08-07: item click = item page, 🗺 = tracker).
                var badgeName = name;
                var badge = new TextBlock
                {
                    Text = "🗺", FontSize = 11, Margin = new Thickness(0, 1, 6, 1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Part of a quest — click for its quest info",
                };
                badge.SetResourceReference(TextBlock.ForegroundProperty, "GoodBrush");
                badge.MouseLeftButtonDown += (_, ev) => ev.Handled = true;
                badge.MouseLeftButtonUp += (_, ev) =>
                {
                    ev.Handled = true;
                    OpenQuestInfoForItem(badgeName);
                };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }
            var right = new TextBlock
            {
                Text = value, FontSize = 12,
                Foreground = valueBrush?.Invoke(value) ?? (Brush)FindResource("DimBrush"),
            };
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(right);
            list.Items.Add(grid);
        }
    }

    // ---- hide while the game is unfocused (FOCUS-*, discussion #41) ----

    private bool _hiddenForFocus;

    /// <summary>When enabled, the widget hides while the game runs WITHOUT being the
    /// foreground app — alt-tab to a browser and the corner it lives in is the browser's
    /// again. Never hides when the game isn't running (configuring the widget outside the
    /// game must stay possible) or when EQBuddy itself is what has focus (clicking the
    /// widget must not vanish it). Satellite windows follow via their own tick gates.</summary>
    private void UpdateFocusHide()
    {
        var hide = ShouldHideForFocus();
        if (hide == _hiddenForFocus) return;
        _hiddenForFocus = hide;
        Visibility = hide ? Visibility.Hidden : Visibility.Visible;
    }

    private bool ShouldHideForFocus()
    {
        if (!_settings.HideWhenGameUnfocused) return false;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(fg, out var fgPid);
        if (fgPid == (uint)Environment.ProcessId) return false;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)fgPid);
            if (p.ProcessName.Equals("eqgame", StringComparison.OrdinalIgnoreCase)) return false;
        }
        catch { return false; }   // foreground process already gone — don't flicker

        // Foreground is some third app: hide only if the game is actually running.
        var game = System.Diagnostics.Process.GetProcessesByName("eqgame");
        try { return game.Length > 0; }
        finally { foreach (var g in game) g.Dispose(); }
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
    }

    /// <summary>
    /// Someone launched a second EQBuddy. Surface this one instead — which is almost
    /// certainly what they wanted, since the usual reason to relaunch is that the widget
    /// is hidden or buried behind a fullscreen game.
    /// </summary>
    internal void RestoreFromAnotherInstance()
    {
        try
        {
            if (Visibility != Visibility.Visible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Topmost = true;
            Activate();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this)!;
    }

    private ClickThroughChip? _unlockChip;

    // ---- the alignment grid (discussion #34) ----

    private GridOverlayWindow? _gridOverlay;

    private void OnGridOverlay(object sender, RoutedEventArgs e) =>
        SetGridOverlay(!_settings.ShowGridOverlay);

    /// <summary>Menu toggle and Options checkbox both land here, so they stay in
    /// lockstep (the SetTrackSpawns pattern). The overlay window exists only while
    /// the grid is on — nothing invisible lingers.</summary>
    internal void SetGridOverlay(bool on)
    {
        _settings.ShowGridOverlay = on;
        _settings.Save();
        GridOverlayItem.IsChecked = on;
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
        RootBorder().BorderBrush = (Brush)FindResource(_clickThrough ? "WarnBrush" : "BorderBrush");
        RootBorder().ToolTip = _clickThrough
            ? "Click-through ON — click the 🔒 chip to interact again"
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

    private void OnReset(object sender, RoutedEventArgs e)
    {
        // With archiving on, reset also splits the log: what's parsed so far moves to
        // Logs\archive and a fresh file begins — the second half of #52's ask.
        if (_settings.ArchiveLogs && _watcher.CurrentPath is { } path)
            Task.Run(() => EqConfig.SplitLog(path));
        _stats.Reset();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _unlockChip?.Close();
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
        foreach (var w in _breakouts.Values) w.Close();   // each persists its spot on Closed
        _archiver.FinalizeActiveSync(_stats.Snapshot(), "ApplicationExit");
        _watcher.Dispose();
        _repo.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
