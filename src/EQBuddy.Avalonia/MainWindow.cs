using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

public sealed class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SessionStats _stats = new();
    // Attached at construction (not in SessionStats itself) so tests never touch disk.
    private void AttachSpellStore() =>
        _stats.Spells.AttachStore(System.IO.Path.Combine(Core.AppPaths.Dir, "spell-categories.json"));
    private readonly LogWatcher _watcher;
    private readonly SpawnTimers _spawnTimers;
    private readonly EQBuddy.UI.Shared.SpawnsViewModel _spawnsVm;
    private SpawnsWindow? _spawnsWindow;
    private SpawnChipsWindow? _spawnChipsWindow;
    private MezChipsWindow? _mezChipsWindow;
    private readonly MezTracker _mezTracker = new();
    private readonly EqlWikiItemService _wikiItems =
        new(System.IO.Path.Combine(AppPaths.Dir, "wiki-cache", "items"));
    private ItemInfoWindow? _itemInfoWindow;
    private readonly EqlWikiMobService _wikiMobs =
        new(System.IO.Path.Combine(AppPaths.Dir, "wiki-cache", "mobs"));
    private readonly Dictionary<string, MobLookupResult?> _targetResults =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<BreakoutKind, BreakoutWindow> _breakouts = new();
    private readonly HashSet<BreakoutKind> _dismissedBreakouts = [];
    private readonly SessionRepository _repo = new(SessionRepository.DefaultDbPath);
    private readonly SessionArchiver _archiver;
    private DateTime _lastCheckpoint = DateTime.MinValue;
    private readonly DispatcherTimer _uiTimer;
    private readonly LayoutTransformControl _scaleRoot = new();
    private readonly Border _root = new();
    private readonly Grid _miniRoot = new();
    private readonly StackPanel _miniChips = new() { Orientation = Orientation.Horizontal };
    private readonly Ellipse _miniDot = Dot();
    private readonly StackPanel _normalRoot = new() { Width = 320 };
    private readonly Ellipse _statusDot = Dot();
    private readonly TextBlock _charLabel = AppTheme.DimText("looking for a character...");
    private readonly ScrollViewer _sectionScroll = new();
    private readonly Border _logBanner = Banner(AppTheme.WarnWashBrush);
    private readonly Border _updateBanner = Banner(AppTheme.GoodWashBrush);
    private readonly TextBlock _updateText = new() { FontSize = 12, Foreground = AppTheme.GoodBrush, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _zoneText = AppTheme.DimText("-");
    private readonly TextBlock _sessionText = AppTheme.DimText("session 0:00");
    private readonly TextBlock _combatHeader = AppTheme.StatValue("0 dps");
    private readonly TextBlock _healingHeader = AppTheme.StatValue("0 hps");
    private readonly TextBlock _killsHeader = AppTheme.StatValue("0");
    private readonly TextBlock _lootHeader = AppTheme.StatValue("0 items");
    private readonly TextBlock _trackedHeader = AppTheme.StatValue("0");
    private readonly TextBlock _moneyHeader = AppTheme.StatValue("0c");
    private readonly TextBlock _progressHeader = AppTheme.StatValue("0% xp");
    private readonly TextBlock _factionHeader = AppTheme.StatValue("-");
    private readonly TextBlock _miscHeader = AppTheme.StatValue("0 deaths");
    private readonly TextBlock _combatSummary = AppTheme.DimText("");
    // The fight in front of you, above the session aggregate — see ShowLastFight. The
    // headings are buttons: each subsection collapses on its own and remembers it.
    private readonly Button _combatFightLabel = AppTheme.IconButton("v Last fight", "Show or hide this fight's breakdown");
    private readonly StackPanel _combatFightBody = new();
    private readonly TextBlock _combatFightText = AppTheme.DimText("");
    private readonly ItemsControl _combatFightList = new();
    private readonly TextBlock _combatFightSplit = AppTheme.DimText("");
    private readonly TextBlock _combatFightOutLabel = AppTheme.Heading("Your damage");
    private readonly TextBlock _combatFightInLabel = AppTheme.Heading("Damage you took");
    private readonly ItemsControl _combatFightInList = new();
    private readonly Button _combatSessionLabel = AppTheme.IconButton("v Session so far", "Show or hide the session totals");
    private readonly StackPanel _combatSessionBody = new();
    private readonly Button _healFightLabel = AppTheme.IconButton("v Last fight", "Show or hide this fight's healing");
    private readonly StackPanel _healFightBody = new();
    private readonly TextBlock _healFightText = AppTheme.DimText("");
    private readonly ItemsControl _healFightList = new();
    private readonly Button _healSessionLabel = AppTheme.IconButton("v Session so far", "Show or hide the session totals");
    private readonly StackPanel _healSessionBody = new();
    private readonly TextBlock _healingSummary = AppTheme.DimText("");
    private readonly TextBlock _killsSummary = AppTheme.DimText("");
    private readonly TextBlock _moneySummary = AppTheme.DimText("");
    private readonly TextBlock _progressSummary = AppTheme.DimText("");
    private readonly ItemsControl _damageSourceList = new();
    private readonly TextBlock _petAbilityLabel = AppTheme.Heading("Pet abilities");
    private readonly ItemsControl _petAbilityList = new();
    private readonly ItemsControl _damageTakenList = new();
    private readonly ItemsControl _healSpellList = new();
    private readonly ItemsControl _healerList = new();
    private readonly ItemsControl _killList = new();
    private readonly ItemsControl _partyKillList = new();
    private readonly ItemsControl _lootList = new();
    private readonly StackPanel _targetDropsBlock = new() { IsVisible = false, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _targetDropsHeader = AppTheme.Heading("", AppTheme.WarnBrush);
    private readonly ItemsControl _targetDropsList = new();
    private readonly StackPanel _trackedPanel = new();
    private readonly ItemsControl _craftedList = new();
    private readonly ItemsControl _soldList = new();
    private readonly ItemsControl _skillList = new();
    private readonly TextBlock _aaAbilitiesLabel = AppTheme.Heading("AA abilities");
    private readonly ItemsControl _aaAbilityList = new();
    private readonly ItemsControl _factionList = new();
    private readonly ItemsControl _deathList = new();
    private readonly ItemsControl _zoneList = new();
    private readonly TextBlock _healSpellsLabel = AppTheme.Heading("Heals cast", AppTheme.GoodBrush);
    private readonly StackPanel _healSortBar = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _healersLabel = AppTheme.Heading("Healed by", AppTheme.GoodBrush);
    private readonly TextBlock _partyKillsLabel = AppTheme.Heading("Group kills");
    private readonly TextBlock _craftedLabel = AppTheme.Heading("Created by merging");
    private readonly TextBlock _soldLabel = AppTheme.Heading("Sold to merchants");
    private readonly TextBlock _recentFightsLabel = AppTheme.Heading("Recent fights");
    private readonly ItemsControl _recentFightsList = new();
    private readonly TextBlock _areaSpellLabel = AppTheme.Heading("Area spells (per cast)");
    private readonly ItemsControl _areaSpellList = new();
    private readonly TextBlock _stanceLabel = AppTheme.Heading("By stance");
    private readonly ItemsControl _stanceList = new();
    private readonly TextBlock _invocationLabel = AppTheme.Heading("By invocation");
    private readonly ItemsControl _invocationList = new();
    private readonly TextBlock _farmingLabel = AppTheme.Heading("Farming (per creature)");
    private readonly ItemsControl _farmingList = new();
    private readonly TextBlock _markersLabel = AppTheme.Heading("Camp markers");
    private readonly ItemsControl _markerList = new();
    private readonly Button _gearBtn = AppTheme.IconButton(AppIcon.Settings, "Settings");
    private readonly Dictionary<string, Button> _stars = new();
    private readonly Dictionary<string, SectionPanel> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel _sectionsPanel = new();
    private TextBlock _dmgOutSortTotal = null!;
    private TextBlock? _dmgOutSortDps;
    private TextBlock _dmgOutSortHits = null!;
    private TextBlock _dmgOutSortAvg = null!;
    private TextBlock _dmgInSortTotal = null!;
    private TextBlock _dmgInSortHits = null!;
    private TextBlock _dmgInSortAvg = null!;
    private TextBlock _healSortTotal = null!;
    private TextBlock? _healSortHps;
    private TextBlock _healSortHits = null!;
    private TextBlock _healSortAvg = null!;
    private DateTime _lastCharScan = DateTime.MinValue;
    private DateTime _lastJanitorRun = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private DateTime _upToDateNoticeUntil = DateTime.MinValue;
    private bool _installingUpdate;
    private bool _clickThrough;
    private HistoryWindow? _historyWindow;
    private OptionsWindow? _optionsWindow;
    private readonly MenuItem _trackSpawnsItem = new()
    {
        Header = "Track spawns (named respawn timers)",
    };
    private readonly MenuItem _clickThroughItem = new()
    {
        Header = "Click-through (game clicks pass through)",
    };
    private ClickThroughChip? _unlockChip;
    private AlertWindow? _alertWindow;
    private IReadOnlyList<WhatsNewEntry> _whatsNewNotes = [];
    private StatSort _dmgOutSort = StatSort.Total;
    private StatSort _dmgInSort = StatSort.Total;
    private StatSort _healSort = StatSort.Total;
    private readonly bool _expandForTesting = Environment.GetEnvironmentVariable("EQBUDDY_EXPAND") == "1";

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "pet", "loot", "money", "xp", "deaths"];

    private enum StatSort { Total, Hits, Avg, Rate }

    public MainWindow()
    {
        // Before the watcher's startup replay, so already-logged charms classify with
        // everything learned in earlier sessions (issue #29).
        AttachSpellStore();
        _stats.AaStore = new AaLedgerStore(AppPaths.File("aa-ledger.json"));
        _mezTracker.AttachStore(AppPaths.File("mez-durations.json"));
        _watcher = new LogWatcher(_stats);
        _watcher.Mez = _mezTracker;
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
        _stats.SessionRolledOver += () => Dispatcher.UIThread.Post(_delayedAlerts.CancelAll);
        _archiver = new SessionArchiver(_repo);
        _stats.SessionEnding += snap => _archiver.FinalizeActive(snap, "IdleTimeout");
        Title = "EQBuddy";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = true;
        CanResize = false;
        Opacity = _settings.Opacity;
        Content = BuildRoot();

        // Migration: any old per-rule pin enables the replacement group pin.
        if (!_settings.PinWatchChips && _settings.TrackedRules.Any(r => r.Pinned))
            _settings.PinWatchChips = true;
        // Chips became per-rule again: someone who had them on was seeing every enabled rule,
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

        if (_settings.LogFolder is { } saved && !Directory.Exists(saved))
            _settings.LogFolder = null;
        _settings.LogFolder ??= LogWatcher.FindDefaultLogFolder();
        RestorePosition();
        ApplyUiScale(_settings.UiScale);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        UpdateStarVisuals();
        ApplySectionLayout();
        SetMode(_settings.Minimized);
        if (_expandForTesting)
            foreach (var section in _sections.Values)
                section.IsExpanded = true;
        FollowActiveCharacter();

        PrepareWhatsNew();

        if (_settings.LogFolder is { } lf)
        {
            // Page one of the launch tour is the log-truncation consent question.
            // Leave existing logs untouched until the user has answered it.
            var prune = _settings.TruncateLogs && !_settings.ShowTutorial;
            var archive = _settings.ArchiveLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(lf);
                if (prune) EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap, archive: archive);
            });
        }

        if (Environment.GetEnvironmentVariable("EQBUDDY_CCLOG") == "1")
            StartCrowdControlCapture();

        // 1.20.0 could turn Follow off on a selection event the user never made.
        // Repair affected profiles once; subsequent user choices are left alone.
        if (!_settings.SpawnFollowRepaired)
        {
            _settings.SpawnFollowZone = true;
            _settings.SpawnFollowRepaired = true;
            _settings.Save();
        }

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();
        Loaded += (_, _) =>
        {
            UpdateWindowHeightLimit();
            if (_settings.ShowTutorial)
                new TutorialWindow(this).Show(this);
            else if (_whatsNewNotes.Count > 0)
                new WhatsNewWindow(_whatsNewNotes).Show(this);
        };
        // A portrait secondary can be much taller than the primary. Recalculate after
        // every move so crossing a monitor boundary updates the available card height.
        PositionChanged += (_, _) => UpdateWindowHeightLimit();
    }

    /// <summary>Records the running version before displaying release notes, so an
    /// interrupted launch cannot show the same popup forever. Fresh installs use the
    /// tutorial instead; installs predating this feature see only the current release.</summary>
    private void PrepareWhatsNew()
    {
        var currentVersion = UpdateChecker.CurrentVersion.ToString();
        if (_settings.ShowTutorial || _settings.LastSeenVersion == currentVersion)
        {
            if (_settings.LastSeenVersion != currentVersion)
            {
                _settings.LastSeenVersion = currentVersion;
                _settings.Save();
            }
            return;
        }

        var lastSeen = _settings.LastSeenVersion.Length > 0
            ? _settings.LastSeenVersion
            : PreviousVersionBaseline(currentVersion);
        _whatsNewNotes = WhatsNewCatalog.EntriesBetween(lastSeen, currentVersion);
        _settings.LastSeenVersion = currentVersion;
        _settings.Save();
    }

    internal static string PreviousVersionBaseline(string current) =>
        Version.TryParse(current, out var version)
            ? new Version(version.Major, Math.Max(0, version.Minor - 1), 0).ToString()
            : current;

    public double UiScale => _settings.UiScale;
    public double WidgetOpacity => Opacity;
    public double BackgroundOpacityValue => _settings.BackgroundOpacity;
    public bool TruncateLogsValue => _settings.TruncateLogs;
    public AppSettings Settings => _settings;
    public void PersistSettings() => _settings.Save();

    /// <summary>
    /// Opt-in capture for CC-looking lines whose EQ Legends wording is not known yet.
    /// Keep only distinct lines and cap the file so diagnostics cannot grow without bound.
    /// </summary>
    private static void StartCrowdControlCapture()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = AppPaths.File("cc-candidates.txt");
        var gate = new object();
        LogParser.UnmatchedCandidateSink = message =>
        {
            lock (gate)
            {
                if (seen.Count >= 500 || !seen.Add(message)) return;
                try { File.AppendAllText(path, message + Environment.NewLine); }
                catch { /* diagnostics must never interrupt log tailing */ }
            }
        };
    }

    internal static readonly (string Key, string Title)[] SectionCatalog =
    [
        ("combat", "Combat"), ("healing", "Healing"), ("kills", "Kills"), ("loot", "Loot"),
        ("tracked", "Tracked"), ("money", "Money"), ("progress", "Progress"),
        ("faction", "Faction"), ("misc", "Travels & Deaths"),
    ];

    public void ApplySectionLayout()
    {
        var order = _settings.SectionOrder.Where(_sections.ContainsKey).ToList();
        foreach (var (key, _) in SectionCatalog)
            if (!order.Contains(key)) order.Add(key);

        _sectionsPanel.Children.Clear();
        foreach (var key in order)
        {
            var section = _sections[key];
            _sectionsPanel.Children.Add(section);
            if (key != "tracked")
                section.IsVisible = !_settings.HiddenSections.Contains(key);
        }
    }

    public void SetTruncateLogs(bool enabled)
    {
        _settings.TruncateLogs = enabled;
        _settings.Save();
    }

    public void SetUiScale(double scale)
    {
        _settings.UiScale = Math.Clamp(scale, 0.5, 2.0);
        ApplyUiScale(_settings.UiScale);
        _settings.Save();
    }

    public void SetWindowOpacity(double opacity)
    {
        _settings.Opacity = Math.Clamp(opacity, 0.3, 1.0);
        Opacity = _settings.Opacity;
        _settings.Save();
    }

    public void SetBackgroundOpacity(double opacity)
    {
        _settings.BackgroundOpacity = Math.Clamp(opacity, 0.15, 1.0);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        _settings.Save();
    }

    private Control BuildRoot()
    {
        _scaleRoot.Child = _root;
        _root.CornerRadius = new CornerRadius(10);
        _root.BorderBrush = AppTheme.BorderBrush;
        _root.BorderThickness = new Thickness(1);
        _root.ContextMenu = BuildContextMenu();
        _root.PointerPressed += OnDrag;
        _root.Child = new StackPanel
        {
            Margin = new Thickness(10),
            Children =
            {
                BuildMiniRoot(),
                BuildNormalRoot(),
            },
        };
        return _scaleRoot;
    }

    private Control BuildMiniRoot()
    {
        _miniRoot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _miniRoot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _miniRoot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _miniRoot.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _miniDot.Margin = new Thickness(2, 0, 8, 0);
        _miniRoot.Children.Add(_miniDot);
        Grid.SetColumn(_miniChips, 1);
        _miniRoot.Children.Add(_miniChips);
        var restore = AppTheme.IconButton(AppIcon.Expand, "Expand");
        restore.Click += (_, _) => SetMode(false);
        restore.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(restore, 2);
        _miniRoot.Children.Add(restore);
        var close = AppTheme.IconButton(AppIcon.Close, "Close");
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 3);
        _miniRoot.Children.Add(close);
        return _miniRoot;
    }

    private Control BuildNormalRoot()
    {
        _normalRoot.Children.Add(BuildTitleBar());
        _logBanner.Child = new TextBlock
        {
            Text = "Logging looks off. Type /log in the game's chat window. EQBuddy enables it automatically for future game launches.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = AppTheme.WarnBrush,
        };
        _logBanner.Margin = new Thickness(0, 8, 0, 0);
        _normalRoot.Children.Add(_logBanner);
        _updateBanner.Child = _updateText;
        _updateBanner.Margin = new Thickness(0, 8, 0, 0);
        _updateBanner.Cursor = new Cursor(StandardCursorType.Hand);
        _updateBanner.PointerPressed += OnUpdateBannerClick;
        _normalRoot.Children.Add(_updateBanner);
        _normalRoot.Children.Add(BuildSessionLine());
        _sectionScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _sectionScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _sectionScroll.Content = BuildSections();
        _normalRoot.Children.Add(_sectionScroll);
        return _normalRoot;
    }

    private Control BuildTitleBar()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _statusDot.Margin = new Thickness(2, 0, 7, 0);
        title.Children.Add(_statusDot);
        title.Children.Add(new TextBlock { Text = "EQBuddy", FontWeight = FontWeight.Bold, FontSize = 14, Foreground = AppTheme.AccentBrush });
        grid.Children.Add(title);
        _charLabel.Margin = new Thickness(10, 0, 6, 0);
        _charLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_charLabel, 1);
        grid.Children.Add(_charLabel);
        _gearBtn.Click += OnGear;
        Grid.SetColumn(_gearBtn, 2);
        grid.Children.Add(_gearBtn);
        var reset = AppTheme.IconButton(AppIcon.Refresh, "Reset session stats");
        reset.Click += (_, _) =>
        {
            // With archiving on, reset also splits the log (#52) — same as WPF.
            if (_settings.ArchiveLogs && _watcher.CurrentPath is { } path)
                Task.Run(() => EqConfig.SplitLog(path));
            _stats.Reset();
        };
        Grid.SetColumn(reset, 3);
        grid.Children.Add(reset);
        var mini = AppTheme.IconButton(AppIcon.Minimize, "Minimize to dashboard");
        mini.Click += (_, _) => SetMode(true);
        Grid.SetColumn(mini, 4);
        grid.Children.Add(mini);
        var close = AppTheme.IconButton(AppIcon.Close, "Close");
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 5);
        grid.Children.Add(close);
        return grid;
    }

    private Control BuildSessionLine()
    {
        var grid = new Grid { Margin = new Thickness(2, 8, 2, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(_zoneText);
        Grid.SetColumn(_sessionText, 1);
        grid.Children.Add(_sessionText);
        return grid;
    }

    private Control BuildSections()
    {
        AddSection("combat", "dps", "Combat", _combatHeader, BuildCombatSection(), "Show DPS in mini dashboard");
        AddSection("healing", "hps", "Healing", _healingHeader, BuildHealingSection(), "Show HPS in mini dashboard");
        AddSection("kills", "kills", "Kills", _killsHeader, BuildKillsSection(), "Show kills in mini dashboard");
        AddSection("loot", "loot", "Loot", _lootHeader, BuildLootSection(), "Show loot count in mini dashboard");
        _sections["tracked"] = AppTheme.Section(Header("Tracked", _trackedHeader), _trackedPanel);
        AddSection("money", "money", "Money", _moneyHeader, BuildMoneySection(), "Show money in mini dashboard");
        AddSection("progress", "xp", "Progress", _progressHeader, BuildProgressSection(), "Show XP in mini dashboard");
        _sections["faction"] = AppTheme.Section(Header("Faction", _factionHeader), _factionList);
        AddSection("misc", "deaths", "Travels & Deaths", _miscHeader, BuildMiscSection(), "Show deaths in mini dashboard");
        return _sectionsPanel;
    }

    private void AddSection(string sectionKey, string starKey, string title, TextBlock value, Control content, string tip)
    {
        var star = AppTheme.StarButton(starKey, tip);
        star.Click += OnStarChanged;
        _stars[starKey] = star;
        _sections[sectionKey] = AppTheme.Section(Header(title, value, star), content);
    }

    private static Grid Header(string title, TextBlock value, Button? star = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        if (star is not null) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(new TextBlock { Text = title, FontSize = 13, Foreground = AppTheme.TextBrush });
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        if (star is not null)
        {
            Grid.SetColumn(star, 2);
            grid.Children.Add(star);
        }
        return grid;
    }

    private Control BuildCombatSection()
    {
        var panel = new StackPanel();
        _combatFightText.Margin = new Thickness(0, 1, 0, 2);
        _combatFightBody.Children.Add(_combatFightText);
        _combatFightBody.Children.Add(_combatFightSplit);
        _combatFightBody.Children.Add(_combatFightOutLabel);
        _combatFightBody.Children.Add(_combatFightList);
        _combatFightInLabel.Margin = new Thickness(0, 2, 0, 0);
        _combatFightBody.Children.Add(_combatFightInLabel);
        _combatFightBody.Children.Add(_combatFightInList);
        _combatFightLabel.Click += (_, _) =>
            ToggleSubsection(v => _settings.ShowCombatFight = v, _settings.ShowCombatFight);
        panel.Children.Add(_combatFightLabel);
        panel.Children.Add(_combatFightBody);

        _combatSessionLabel.Click += (_, _) =>
            ToggleSubsection(v => _settings.ShowCombatSession = v, _settings.ShowCombatSession);
        panel.Children.Add(_combatSessionLabel);

        var body = _combatSessionBody;
        _combatSummary.Margin = new Thickness(0, 2, 0, 4);
        body.Children.Add(_combatSummary);
        body.Children.Add(SortHeader("Damage by attack", out _dmgOutSortTotal, out _dmgOutSortHits,
            out _dmgOutSortAvg, out _dmgOutSortDps, OnSortDmgOut, rateText: "dps"));
        body.Children.Add(_damageSourceList);
        var petHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _petAbilityLabel.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(_petAbilityLabel,
            "What your pet is using, split out of its Pet row above — click to expand");
        _petAbilityLabel.PointerPressed += OnPetAbilitiesToggled;
        petHeader.Children.Add(_petAbilityLabel);
        var petStar = AppTheme.StarButton("pet", "Show pet damage breakout when minimized");
        petStar.Click += OnStarChanged;
        _stars["pet"] = petStar;
        Grid.SetColumn(petStar, 1);
        petHeader.Children.Add(petStar);
        body.Children.Add(petHeader);
        body.Children.Add(_petAbilityList);
        body.Children.Add(SortHeader("Damage taken from", out _dmgInSortTotal, out _dmgInSortHits,
            out _dmgInSortAvg, out _, OnSortDmgIn));
        body.Children.Add(_damageTakenList);
        _recentFightsLabel.Margin = new Thickness(0, 6, 0, 0);
        body.Children.Add(_recentFightsLabel);
        body.Children.Add(_recentFightsList);
        _areaSpellLabel.Margin = new Thickness(0, 6, 0, 0);
        _areaSpellLabel.IsVisible = false;
        body.Children.Add(_areaSpellLabel);
        body.Children.Add(_areaSpellList);
        _stanceLabel.Margin = new Thickness(0, 6, 0, 0);
        body.Children.Add(_stanceLabel);
        body.Children.Add(_stanceList);
        _invocationLabel.Margin = new Thickness(0, 6, 0, 0);
        body.Children.Add(_invocationLabel);
        body.Children.Add(_invocationList);
        panel.Children.Add(body);
        return panel;
    }

    /// <summary>Each subsection remembers its own collapsed state — see AppSettings.</summary>
    private void ToggleSubsection(Action<bool> set, bool current)
    {
        set(!current);
        PersistSettings();
        RefreshUi();
    }

    private void OnPetAbilitiesToggled(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_petAbilityLabel).Properties.IsLeftButtonPressed) return;
        _settings.ShowPetAbilities = !_settings.ShowPetAbilities;
        PersistSettings();
        RefreshUi();
        e.Handled = true;
    }

    private void ApplySessionSubsections()
    {
        _combatSessionLabel.Content = (_settings.ShowCombatSession ? "v" : ">") + " Session so far";
        _combatSessionBody.IsVisible = _settings.ShowCombatSession;
        _healSessionLabel.Content = (_settings.ShowHealSession ? "v" : ">") + " Session so far";
        _healSessionBody.IsVisible = _settings.ShowHealSession;
    }

    private Control BuildHealingSection()
    {
        var panel = new StackPanel();
        _healFightText.Margin = new Thickness(0, 1, 0, 2);
        _healFightBody.Children.Add(_healFightText);
        _healFightBody.Children.Add(_healFightList);
        _healFightLabel.Click += (_, _) =>
            ToggleSubsection(v => _settings.ShowHealFight = v, _settings.ShowHealFight);
        panel.Children.Add(_healFightLabel);
        panel.Children.Add(_healFightBody);

        _healSessionLabel.Click += (_, _) =>
            ToggleSubsection(v => _settings.ShowHealSession = v, _settings.ShowHealSession);
        panel.Children.Add(_healSessionLabel);

        var body = _healSessionBody;
        _healingSummary.Margin = new Thickness(0, 2, 0, 4);
        body.Children.Add(_healingSummary);
        var sort = SortHeader("Heals cast", out _healSortTotal, out _healSortHits, out _healSortAvg,
            out _healSortHps, OnSortHeal, _healSpellsLabel, _healSortBar, "hps");
        body.Children.Add(sort);
        body.Children.Add(_healSpellList);
        body.Children.Add(_healersLabel);
        body.Children.Add(_healerList);
        panel.Children.Add(body);
        return panel;
    }

    private Control BuildKillsSection()
    {
        var panel = new StackPanel();
        _killsSummary.Margin = new Thickness(0, 2, 0, 4);
        panel.Children.Add(_killsSummary);
        panel.Children.Add(_killList);
        _farmingLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_farmingLabel);
        panel.Children.Add(_farmingList);
        _partyKillsLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_partyKillsLabel);
        panel.Children.Add(_partyKillList);
        return panel;
    }

    private Control BuildLootSection()
    {
        var panel = new StackPanel();
        panel.Children.Add(_lootList);
        _craftedLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_craftedLabel);
        panel.Children.Add(_craftedList);
        _targetDropsBlock.Children.Add(_targetDropsHeader);
        _targetDropsBlock.Children.Add(_targetDropsList);
        panel.Children.Add(_targetDropsBlock);
        return panel;
    }

    private Control BuildMoneySection()
    {
        var panel = new StackPanel();
        panel.Children.Add(_moneySummary);
        _soldLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_soldLabel);
        panel.Children.Add(_soldList);
        return panel;
    }

    private Control BuildProgressSection()
    {
        var panel = new StackPanel();
        _progressSummary.Margin = new Thickness(0, 2, 0, 4);
        panel.Children.Add(_progressSummary);
        panel.Children.Add(AppTheme.Heading("Skill-ups"));
        panel.Children.Add(_skillList);
        _aaAbilitiesLabel.Margin = new Thickness(0, 4, 0, 0);
        panel.Children.Add(_aaAbilitiesLabel);
        panel.Children.Add(_aaAbilityList);
        return panel;
    }

    private Control BuildMiscSection()
    {
        var panel = new StackPanel();
        panel.Children.Add(AppTheme.Heading("Deaths", AppTheme.BadBrush));
        panel.Children.Add(_deathList);
        panel.Children.Add(AppTheme.Heading("Zones visited"));
        panel.Children.Add(_zoneList);
        _markersLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_markersLabel);
        panel.Children.Add(_markerList);
        return panel;
    }

    private static Control SortHeader(string title, out TextBlock total, out TextBlock hits, out TextBlock avg,
        out TextBlock? rate, EventHandler<PointerPressedEventArgs> handler, TextBlock? titleBlock = null,
        StackPanel? sortBar = null, string? rateText = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(titleBlock ?? AppTheme.Heading(title));
        sortBar ??= new StackPanel { Orientation = Orientation.Horizontal };
        sortBar.HorizontalAlignment = HorizontalAlignment.Right;
        sortBar.Children.Add(AppTheme.DimText("sort:", new Thickness(0, 0, 4, 0)));
        total = SortLink("total", "total", handler, selected: true);
        var rateSubject = title.Contains("Heal", StringComparison.OrdinalIgnoreCase) ? "spell" : "ability";
        rate = rateText is null ? null : SortLink(rateText, "rate", handler,
            tip: $"Per-{rateSubject} {rateText}: that {rateSubject}'s total divided by total time in combat");
        hits = SortLink(title.Contains("Heal", StringComparison.OrdinalIgnoreCase) ? "casts" : "hits", "hits", handler);
        avg = SortLink("avg", "avg", handler);
        sortBar.Children.Add(total);
        if (rate is not null) sortBar.Children.Add(rate);
        sortBar.Children.Add(hits);
        sortBar.Children.Add(avg);
        Grid.SetColumn(sortBar, 1);
        grid.Children.Add(sortBar);
        return grid;
    }

    private static TextBlock SortLink(string text, string tag, EventHandler<PointerPressedEventArgs> handler,
        bool selected = false, string? tip = null)
    {
        var link = new TextBlock
        {
            Text = text,
            Tag = tag,
            FontSize = 10,
            Foreground = selected ? AppTheme.AccentBrush : AppTheme.DimBrush,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(text == "total" ? 0 : 6, 0, 0, 0),
        };
        if (tip is not null) ToolTip.SetTip(link, tip);
        link.PointerPressed += handler;
        return link;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        // Clickable since 1.48 (#76): downloads, guides, and a shareable link.
        var version = new MenuItem { Header = $"EQBuddy v{UpdateChecker.CurrentVersion}" };
        version.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://github.com/DranakCorps-bot/EQBuddy") { UseShellExecute = true });
        var check = new MenuItem { Header = "Check for updates" };
        check.Click += (_, _) => { _lastUpdateCheck = DateTime.Now; CheckForUpdates(manual: true); };
        var options = new MenuItem { Header = "Options... (size, opacity, watch rules)" };
        options.Click += OnOptions;
        var tutorial = new MenuItem { Header = "Quick tutorial..." };
        tutorial.Click += OnTutorial;
        var marker = new MenuItem { Header = "Drop camp marker" };
        marker.Click += (_, _) => DropCampMarker();
        _clickThroughItem.Click += (_, _) => SetClickThrough(!_clickThrough);
        var history = new MenuItem { Header = "Session history..." };
        history.Click += OnHistory;
        var spawns = new MenuItem { Header = "Spawn timers..." };
        spawns.Click += (_, _) => ShowSpawnsWindow();
        SyncTrackSpawnsMenu();
        _trackSpawnsItem.Click += (_, _) => SetTrackSpawns(!_settings.TrackSpawns);
        var feedback = new MenuItem { Header = "Send feedback..." };
        feedback.Click += (_, _) => new FeedbackWindow().Show(this);
        var choose = new MenuItem { Header = "Choose log folder..." };
        choose.Click += OnChooseLogFolder;
        var detect = new MenuItem { Header = "Auto-detect log folder" };
        detect.Click += (_, _) =>
        {
            _settings.LogFolder = LogWatcher.FindDefaultLogFolder();
            _settings.Save();
            _lastCharScan = DateTime.MinValue;
            FollowActiveCharacter();
        };
        menu.Items.Add(version);
        menu.Items.Add(check);
        menu.Items.Add(options);
        menu.Items.Add(tutorial);
        menu.Items.Add(marker);
        menu.Items.Add(_clickThroughItem);
        menu.Items.Add(history);
        menu.Items.Add(spawns);
        menu.Items.Add(_trackSpawnsItem);
        menu.Items.Add(feedback);
        menu.Items.Add(new Separator());
        menu.Items.Add(choose);
        menu.Items.Add(detect);
        return menu;
    }

    private void RestorePosition()
    {
        // A spot saved on a monitor that's since gone would put the widget in the
        // void; keep the default position instead (parity with the WPF guard).
        if (ScreenGuard.OnScreen(this, _settings.WindowLeft, _settings.WindowTop, Width, Height))
            Position = new PixelPoint((int)_settings.WindowLeft, (int)_settings.WindowTop);
    }

    private void ApplyUiScale(double scale)
    {
        _scaleRoot.LayoutTransform = Math.Abs(scale - 1.0) < 0.001 ? null : new ScaleTransform(scale, scale);
        UpdateWindowHeightLimit();
        _scaleRoot.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void UpdateWindowHeightLimit()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null) return;

        var workingHeight = screen.WorkingArea.Height / screen.Scaling;
        MaxHeight = Math.Max(240, workingHeight - 20);

        // The section list sits inside the scaled widget. Reserve room for the title,
        // status/session lines, borders, and a little work-area breathing room.
        var scale = Math.Max(0.5, _settings.UiScale);
        _sectionScroll.MaxHeight = Math.Max(160, (workingHeight - 160) / scale);
    }

    private void ApplyBackgroundOpacity(double opacity) => _root.Background = AppTheme.BgWithOpacity(opacity);

    /// <summary>Re-applies visual state that AppTheme.Apply's brush mutation can't reach
    /// on its own: BgWithOpacity returns a fresh, non-live brush each call, and stat rows
    /// built from AccentBarBrush() bake in a color snapshot rather than a live reference.
    /// Everything else (borders, banners, headings) repaints on its own because it holds
    /// a reference to the same AppTheme brush instance that just got mutated.</summary>
    public void RefreshTheme()
    {
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        RefreshUi();
    }

    private async void OnChooseLogFolder(object? sender, EventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Pick the EverQuest Legends Logs folder",
            AllowMultiple = false,
        });
        var picked = folders.FirstOrDefault()?.TryGetLocalPath();
        if (picked is null) return;
        var logsSub = System.IO.Path.Combine(picked, "Logs");
        if (!Directory.EnumerateFiles(picked, "eqlog_*.txt").Any() && Directory.Exists(logsSub))
            picked = logsSub;
        _settings.LogFolder = picked;
        _settings.Save();
        _lastCharScan = DateTime.MinValue;
        FollowActiveCharacter();
    }

    private void FollowActiveCharacter()
    {
        if (_settings.LogFolder is null)
        {
            _charLabel.Text = "logs not found - right-click, Choose log folder";
            return;
        }
        var active = LogWatcher.MostRecentlyActive(_settings.LogFolder);
        if (active is null)
        {
            _charLabel.Text = "waiting for a character to log in...";
            return;
        }
        if (!string.Equals(active.FilePath, _watcher.CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            if (_watcher.CurrentPath is not null)
                _archiver.FinalizeActive(CurrentSnapshot(), "CharacterChanged");
            _watcher.Select(active.FilePath);
            _archiver.SetIdentity(_stats.ServerName, _stats.CharacterName);
            _charLabel.Text = active.Display;
        }
    }

    private StatsSnapshot CurrentSnapshot() =>
        _stats.Snapshot(TimeSpan.FromMinutes(Math.Max(1, _settings.RecentWindowMinutes)),
            _settings.TrackedRules);

    private void RefreshUi()
    {
        if (_settings.TrackSpawns)
        {
            // Sound only: the chip changing to DUE is already the visual notification.
            foreach (var due in _spawnsVm.ConsumeDueAlerts(DateTime.Now))
                if (_spawnsVm.SoundFor(due.Zone, due.Name) is { } sound)
                    PlayAlertSound(sound);

            // Chips are the ambient face and stay visible alongside the full browser.
            if (_spawnsVm.HasActiveTimers(DateTime.Now))
            {
                if (_spawnChipsWindow is not { IsVisible: true })
                {
                    var chips = new SpawnChipsWindow(this, _spawnsVm);
                    chips.Closed += (_, _) =>
                    {
                        if (ReferenceEquals(_spawnChipsWindow, chips)) _spawnChipsWindow = null;
                    };
                    _spawnChipsWindow = chips;
                    chips.Show(this);
                }
                _spawnChipsWindow.RefreshChips(DateTime.Now);
            }
            else
                CloseSpawnChips();
        }
        else
            CloseSpawnChips();

        // Combat-urgent mez targets use their own movable stack rather than mixing with
        // ambient spawn timers. The stack exists only while a mez is believed active.
        var mezzes = _mezTracker.Snapshot(DateTime.Now);
        if (mezzes.Count > 0)
        {
            if (_mezChipsWindow is not { IsVisible: true })
            {
                var chips = new MezChipsWindow(_settings, MezChipsWindow.BuildChips);
                chips.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_mezChipsWindow, chips)) _mezChipsWindow = null;
                };
                _mezChipsWindow = chips;
                chips.Show(this);
            }
            _mezChipsWindow.RefreshChips(mezzes, DateTime.Now);
        }
        else
            CloseMezChips();

        if (DateTime.Now - _lastCharScan > TimeSpan.FromSeconds(5))
        {
            _lastCharScan = DateTime.Now;
            FollowActiveCharacter();
        }
        if (DateTime.Now - _lastUpdateCheck > TimeSpan.FromHours(6))
        {
            _lastUpdateCheck = DateTime.Now;
            CheckForUpdates(manual: false);
        }
        if (_settings.LogFolder is { } folder && DateTime.Now - _lastJanitorRun > TimeSpan.FromMinutes(10))
        {
            _lastJanitorRun = DateTime.Now;
            var prune = _settings.TruncateLogs && !_settings.ShowTutorial;
            var archive = _settings.ArchiveLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(folder);
                if (prune) EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap, archive: archive);
            });
        }

        UpdateLoggingStatus();
        if (_upToDateNoticeUntil != DateTime.MinValue && DateTime.Now > _upToDateNoticeUntil && _pendingUpdate is null && !_installingUpdate)
        {
            _updateBanner.IsVisible = false;
            _upToDateNoticeUntil = DateTime.MinValue;
        }
        if (_watcher.LastError is { } err) App.LogError(err);

        var s = CurrentSnapshot();
        ProcessTrackedAlerts(s);
        if (DateTime.Now - _lastCheckpoint > TimeSpan.FromMinutes(5))
        {
            _lastCheckpoint = DateTime.Now;
            _archiver.Checkpoint(s);
        }
        if (_miniRoot.IsVisible) UpdateMiniChips(s);
        UpdateBreakouts(s);
        _zoneText.Text = s.CurrentZone.Length > 0 ? s.CurrentZone : "-";
        var active = TimeSpan.FromSeconds(s.ActiveSeconds);
        _sessionText.Text = s.SessionStart is { } start
            ? $"session {(int)s.Elapsed.TotalHours}:{s.Elapsed.Minutes:D2} - active {(int)active.TotalMinutes}m (since {start:h:mm tt})"
            : "waiting for log activity...";
        _combatHeader.Text = s.CurrentDps > 0 ? $"{s.SessionDps:0} dps (now {s.CurrentDps:0})" : $"{s.SessionDps:0} dps";
        _killsHeader.Text = s.PartyKillCount > 0 ? $"{s.YourKillCount} (+{s.PartyKillCount})" : $"{s.YourKillCount}";
        _lootHeader.Text = s.CraftedTotal > 0 ? $"{s.LootTotal} items (+{s.CraftedTotal} made)" : $"{s.LootTotal} item{(s.LootTotal == 1 ? "" : "s")}";
        _moneyHeader.Text = StatsSnapshot.FormatCoin(s.Copper);
        _progressHeader.Text = $"{s.XpPercent:0.0}% xp" + (s.Levels.Count > 0 ? $", +{s.Levels.Count} lvl" : "") + (s.AaGained > 0 ? $", +{s.AaGained} aa" : "");
        _factionHeader.Text = s.Faction.Count > 0 ? $"{s.Faction.Count} factions" : "-";
        _miscHeader.Text = $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}";
        ApplySessionSubsections();
        RefreshExpandedSections(s);
    }

    /// <summary>Paint a snapshot into the cards, without the timer-driven housekeeping
    /// RefreshUi also does (character rescan, update check, log janitor). Exists so the
    /// headless render tests can exercise the code path every refresh takes — which is where
    /// a card that mis-formats or dereferences null actually breaks — without a log folder,
    /// a network, or a five-second wait.</summary>
    internal void RenderSnapshotForTest(StatsSnapshot s,
        IReadOnlyDictionary<string, DateTime>? dueByRule = null)
    {
        ApplySessionSubsections();
        RefreshExpandedSections(s);
        RenderTracked(s, dueByRule);
    }

    private void RefreshExpandedSections(StatsSnapshot s)
    {
        RefreshOptionalSectionVisibility(s);

        if (_sections["combat"].IsExpanded)
        {
            var acc = s.HitCount + s.MissCount > 0 ? (double)s.HitCount / (s.HitCount + s.MissCount) * 100 : 0;
            var critRate = s.HitCount > 0 ? (double)s.CritCount / s.HitCount * 100 : 0;
            var incomingSwings = s.AvoidedIncoming + s.MeleeHitsTaken;
            var avoidance = incomingSwings > 0 ? (double)s.AvoidedIncoming / incomingSwings * 100 : 0;
            var combatTime = TimeSpan.FromSeconds(s.CombatSeconds);
            ShowLastFight(s, _combatFightLabel, _combatFightBody, _combatFightText,
                _combatFightList, healing: false, _settings.ShowCombatFight);
            _combatSummary.Text =
                $"Dealt {s.DamageDealt:N0} ({s.MeleeDamage:N0} melee / {s.SpellDamage:N0} spell)\n" +
                $"{s.CritCount} crits ({critRate:0.#}% rate) - {acc:0}% accuracy\n" +
                $"In combat {(int)combatTime.TotalMinutes}m {combatTime.Seconds}s this session\n" +
                (s.Recent is { } rc ? $"Last {(int)rc.Window.TotalMinutes}m: {rc.Dps:0.#} dps{(rc.HasFullWindow ? "" : " (partial window)")}\n" : "") +
                $"Biggest hit: {s.MaxHit:N0} ({s.MaxHitDesc})\n" +
                $"Taken {s.DamageTaken:N0} - avoided {s.AvoidedIncoming} of {incomingSwings} melee attacks ({avoidance:0}%)" +
                (s.SpecialHits.Count > 0 ? "\n" + string.Join(" - ", s.SpecialHits.Select(x => $"{x.Name} {x.Count}")) : "") +
                (s.DotDamage + s.DirectSpellDamage > 0
                    ? $"\nYour spells: {s.DotDamage:N0} over time / {s.DirectSpellDamage:N0} direct"
                    : "") +
                (s.CastCompletion is { } completion
                    ? $"\nCasts {s.CastsStarted} · {completion * 100:0}% completed" +
                      $" ({s.CastsInterrupted} interrupted · {s.Fizzles} fizzled · {s.Resists} resisted)"
                    : s.Fizzles + s.Resists > 0 ? $"\nFizzles {s.Fizzles} - resists {s.Resists}" : "") +
                (s.CurrentStance.Length > 0 ? $"\nStance: {s.CurrentStance}" : "");
            FillBreakdown(_damageSourceList, s.DamageBySource, _dmgOutSort, s.CombatSeconds, "dps");
            // Shares the damage sort bar above it — it's the same rows, one level down.
            // The overall Pet row is already visible above, so keep this potentially long
            // per-ability list folded until the player asks for it.
            _petAbilityLabel.IsVisible = s.PetAbilities.Count > 0;
            _petAbilityLabel.Text = _settings.ShowPetAbilities
                ? "▾ Pet abilities"
                : $"▸ Pet abilities ({s.PetAbilities.Count})";
            _petAbilityList.IsVisible = _settings.ShowPetAbilities && s.PetAbilities.Count > 0;
            if (_petAbilityList.IsVisible)
                FillBreakdown(_petAbilityList, s.PetAbilities, _dmgOutSort, s.CombatSeconds, "dps");
            FillStatList(_damageTakenList, s.DamageByAttacker, _dmgInSort, "hit");
            _recentFightsLabel.IsVisible = s.RecentEncounters.Count > 0;
            var topFightDps = Math.Max(0.1, s.RecentEncounters.Count > 0
                ? s.RecentEncounters.Max(f => f.Dps)
                : 0);
            var fightBrush = AccentBarBrush();
            _recentFightsList.ItemsSource = s.RecentEncounters.Select(f => BarRow(f.Name,
                $"{f.DurationSeconds:0}s - {f.Dps:0.#} dps{(f.Outcome == "Timeout" ? " - ?" : "")}",
                f.Dps / topFightDps, fightBrush,
                $"{f.DamageOut:N0} damage over {f.DurationSeconds:0}s")).ToList();
            // Per cast, not per target: one cast's total damage is the useful comparison
            // when deciding whether an area spell is worthwhile for the pull size.
            _areaSpellLabel.IsVisible = s.AreaSpells.Count > 0;
            FillList(_areaSpellList, s.AreaSpells.Select(x =>
                (x.Name, $"{x.DamagePerCast:N0}/cast - x{x.Casts} - {x.AvgTargets:0.#} targets" +
                         (x.MaxTargets > x.AvgTargets + 0.05 ? $" (best {x.MaxTargets})" : ""))));
            _stanceLabel.IsVisible = s.Stances.Count > 0;
            FillList(_stanceList, s.Stances.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg - {(int)x.CombatSeconds}s - {x.Dps:0.#} dps")));
            _invocationLabel.IsVisible = s.Invocations.Count > 0;
            FillList(_invocationList, s.Invocations.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg - {(int)x.CombatSeconds}s - {x.Dps:0.#} dps")));
        }
        _healingHeader.Text = s.Hps > 0 ? $"{s.Hps:0.#} hps" : $"{s.HealingDone:N0} healed";
        if (_sections["healing"].IsExpanded)
        {
            ShowLastFight(s, _healFightLabel, _healFightBody, _healFightText,
                _healFightList, healing: true, _settings.ShowHealFight);
            _healingSummary.Text = $"Done {s.HealingDone:N0} - received {s.HealingReceived:N0}" +
                (s.Recent is { Hps: > 0 } rh ? $"\nLast {(int)rh.Window.TotalMinutes}m: {rh.Hps:0.#} hps" : "") +
                (s.RegenTicks > 0 ? $"\n{s.RegenTicks} regen/hymn ticks (game logs no amounts for these)" : "") +
                (s.RuneBlockCount > 0
                    ? $"\nRune absorbed {s.RuneBlockCount} hit{(s.RuneBlockCount == 1 ? "" : "s")}" +
                      $" (best streak {s.RuneBlockStreakMax}" +
                      (s.RuneBlockStreak > 0 ? $", current {s.RuneBlockStreak}" : "") + ")"
                    : "");
            var showSpells = s.HealsBySpell.Count > 0;
            _healSpellsLabel.IsVisible = showSpells;
            _healSortBar.IsVisible = showSpells;
            FillBreakdown(_healSpellList, s.HealsBySpell, _healSort, s.CombatSeconds, "hps");
            _healersLabel.IsVisible = s.HealsByHealer.Count > 0;
            FillList(_healerList, s.HealsByHealer.Select(h => (h.Name, $"{h.Total:N0} - {h.Hits} heal{(h.Hits == 1 ? "" : "s")}")));
        }
        if (_sections["kills"].IsExpanded)
        {
            _killsSummary.Text = $"{s.KillsPerHour:0.0} kills/hr - {s.KillsPerActiveHour:0.0} active" +
                (s.Recent is { } rk ? $" - last {(int)rk.Window.TotalMinutes}m: {rk.Kills}" : "");
            FillList(_killList, s.YourKills.Select(k => (k.Name, $"x{k.Count}")));
            var farmed = s.Mobs.Where(m => m.Kills > 0).ToList();
            _farmingLabel.IsVisible = farmed.Count > 0;
            var farmRows = new List<(string, string)>();
            foreach (var m in farmed)
            {
                farmRows.Add((m.Name,
                    $"avg {m.AvgFightSeconds:0}s - {StatsSnapshot.FormatCoin(m.Copper)} - {m.XpPercent:0.0}% xp"));
                foreach (var l in m.Loot)
                    farmRows.Add(($"      {l.Item}", l.DropRatePct is { } pct ? $"x{l.Count} - {pct:0}%" : $"x{l.Count}"));
            }
            FillList(_farmingList, farmRows);
            _partyKillsLabel.IsVisible = s.PartyKillsByKiller.Count > 0;
            FillList(_partyKillList, s.PartyKillsByKiller.Select(k => (k.Name, $"x{k.Count}")));
        }
        if (_sections["loot"].IsExpanded)
        {
            FillList(_lootList, s.Loot.Select(l => (l.Item, $"x{l.Count}")),
                onNameClick: ShowItemInfo);
            _craftedLabel.IsVisible = s.Crafted.Count > 0;
            FillList(_craftedList, s.Crafted.Select(c => (c.Name, $"x{c.Count}")));
            RenderTargetDrops(s);
        }
        RenderTracked(s);
        if (_sections["money"].IsExpanded)
        {
            _moneySummary.Text = $"Corpses {StatsSnapshot.FormatCoin(s.CorpseCopper)} ({s.CoinDrops} drops, biggest {StatsSnapshot.FormatCoin(s.BiggestDrop)})\n" +
                $"Merchant sales {StatsSnapshot.FormatCoin(s.VendorCopper)} ({s.SalesCount} sales)\n" +
                $"{StatsSnapshot.FormatCoin(s.CopperPerHour)} per hour - {StatsSnapshot.FormatCoin(s.CopperPerActiveHour)} per active hour" +
                (s.Recent is { } rm ? $"\nLast {(int)rm.Window.TotalMinutes}m: {StatsSnapshot.FormatCoin(rm.Copper)}" : "");
            _soldLabel.IsVisible = s.SoldItems.Count > 0;
            FillList(_soldList, s.SoldItems.Select(i => ($"{i.Item}{(i.Count > 1 ? $" x{i.Count}" : "")}", StatsSnapshot.FormatCoin(i.Copper))));
        }
        if (_sections["progress"].IsExpanded)
        {
            _progressSummary.Text = $"{s.XpTicks} xp gains - {s.XpPerHour:0.0}%/hr - {s.XpPerActiveHour:0.0}% active - {s.SkillUpTotal} skill-ups" +
                (s.Recent is { } rx ? $"\nLast {(int)rx.Window.TotalMinutes}m: {rx.XpPerHour:0.0}%/hr" : "") +
                (s.AaGained > 0 ? $"\n{s.AaGained} AA point{(s.AaGained == 1 ? "" : "s")} - {s.AaPerHour:0.0} AA/hr (now {s.AaTotal} unspent)" : "") +
                (s.HoursToLevel is { } eta ? $"\nNext level in {FormatEta(eta)} at this pace" : "") +
                (s.Levels.Count > 0
                    ? "\n" + string.Join(", ", s.Levels.Select((l, i) =>
                    {
                        var from = i == 0 ? s.SessionStart : s.Levels[i - 1].Time;
                        var mins = from is { } f ? (int)(l.Time - f).TotalMinutes : 0;
                        return $"{l.Text} at {l.Time:h:mm tt} ({mins}m)";
                    }))
                    : "");
            FillList(_skillList, s.SkillUps.Select(k => (k.Skill, $"{k.Value} (+{k.Ups})")));
            _aaAbilitiesLabel.IsVisible = s.AaAbilities.Count > 0;
            FillList(_aaAbilityList, s.AaAbilities.Select(ability =>
                    (ability.Name, $"rank {ability.Rank}")),
                tooltip: name => AaCatalog.Find(name)?.Effect);
        }
        if (_sections["faction"].IsExpanded)
            FillList(_factionList, s.Faction.Select(f => (f.Faction, EQBuddy.UI.Shared.FactionFormat.Net(f))),
                valueBrush: f => f.StartsWith('-') ? AppTheme.BadBrush : AppTheme.GoodBrush);
        if (_sections["misc"].IsExpanded)
        {
            FillList(_deathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
            FillList(_zoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
            _markersLabel.IsVisible = s.Markers.Count > 0;
            FillList(_markerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
        }

        if (_expandForTesting)
        {
            try
            {
                var dump = $"dmgSrc={_damageSourceList.Items.Count} dmgTaken={_damageTakenList.Items.Count} " +
                    $"kills={_killList.Items.Count} party={_partyKillList.Items.Count} loot={_lootList.Items.Count} " +
                    $"crafted={_craftedList.Items.Count} skills={_skillList.Items.Count} faction={_factionList.Items.Count} " +
                    $"zones={_zoneList.Items.Count} deaths={_deathList.Items.Count} " +
                    $"actualH={Bounds.Height:0} actualW={Bounds.Width:0}";
                File.WriteAllText(AppPaths.File("debug.txt"), dump);
            }
            catch { }
        }
    }

    private void RefreshOptionalSectionVisibility(StatsSnapshot s)
    {
        _recentFightsLabel.IsVisible = s.RecentEncounters.Count > 0;
        _petAbilityLabel.IsVisible = s.PetAbilities.Count > 0;
        _stanceLabel.IsVisible = s.Stances.Count > 0;
        _invocationLabel.IsVisible = s.Invocations.Count > 0;
        _farmingLabel.IsVisible = s.Mobs.Any(m => m.Kills > 0);
        _partyKillsLabel.IsVisible = s.PartyKillsByKiller.Count > 0;
        _craftedLabel.IsVisible = s.Crafted.Count > 0;
        _soldLabel.IsVisible = s.SoldItems.Count > 0;
        _healSpellsLabel.IsVisible = s.HealsBySpell.Count > 0;
        _healSortBar.IsVisible = s.HealsBySpell.Count > 0;
        _healersLabel.IsVisible = s.HealsByHealer.Count > 0;
        _markersLabel.IsVisible = s.Markers.Count > 0;
    }

    // Keyed by TrackedRule.Id — a display name can be shared by two rules, and keying
    // on it made same-named rules share baselines and cooldowns.
    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.Ordinal);
    private readonly HashSet<string> _watchExpandedRules = new(StringComparer.Ordinal);
    private readonly EQBuddy.UI.Shared.AlertCooldowns _ruleCooldowns = new();
    private readonly EQBuddy.UI.Shared.SoundGate _soundGate = new();
    private string? _alertBaselinePath;

    /// <summary>The floating alert tile, created on first use and owned by the widget.</summary>
    internal AlertWindow AlertTile => _alertWindow ??= new AlertWindow(_settings, this);

    private void RenderTracked(StatsSnapshot s,
        IReadOnlyDictionary<string, DateTime>? dueOverride = null)
    {
        var haveRules = _settings.TrackedRules.Count > 0 && !_settings.HiddenSections.Contains("tracked");
        if (_sections.TryGetValue("tracked", out var section))
            section.IsVisible = haveRules;
        if (!haveRules) return;

        _trackedHeader.Text = s.Tracked.Sum(t => t.TotalQuantity).ToString();
        if (!_sections["tracked"].IsExpanded) return;

        _trackedPanel.Children.Clear();
        var now = DateTime.Now;
        var dueByRule = dueOverride ?? _delayedAlerts.NextDueByRule(now);
        foreach (var r in s.Tracked)
        {
            var head = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var counting = dueByRule.TryGetValue(r.Id, out var dueAt);
            head.Children.Add(new TextBlock
            {
                Text = counting
                    ? $"{r.Name.ToUpperInvariant()} ⏳ {EQBuddy.UI.Shared.Countdown.Format(dueAt - now)}"
                    : r.Name.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = counting ? AppTheme.WarnBrush : AppTheme.AccentBrush,
            });
            var rate = AppTheme.DimText($"{r.TotalQuantity} total - {r.PerHour:0.#}/hr - {r.PerActiveHour:0.#}/active hr");
            Grid.SetColumn(rate, 1);
            head.Children.Add(rate);
            _trackedPanel.Children.Add(head);

            _trackedPanel.Children.Add(AppTheme.DimText(
                r.LastMatch is { } lm && !string.IsNullOrWhiteSpace(r.LastItem)
                    ? $"last: {r.LastItem} · {FormatAge(now - lm)} ago"
                    : "no matches yet",
                new Thickness(6, 1, 0, 2)));

            if (r.Items.Count > 1)
            {
                var expanded = _watchExpandedRules.Contains(r.Id);
                if (expanded)
                    foreach (var item in r.Items)
                        _trackedPanel.Children.Add(new TextBlock
                        {
                            Text = $"{item.Name}   x{item.Count}",
                            FontSize = 12,
                            Foreground = AppTheme.TextBrush,
                            Margin = new Thickness(12, 1, 0, 0),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        });

                var ruleId = r.Id;
                var toggle = AppTheme.DimText(
                    expanded ? "▾ less" : $"▸ all {r.Items.Count} kinds",
                    new Thickness(6, 1, 0, 2));
                toggle.Cursor = new Cursor(StandardCursorType.Hand);
                toggle.PointerPressed += (_, e) =>
                {
                    if (!_watchExpandedRules.Remove(ruleId))
                        _watchExpandedRules.Add(ruleId);
                    RenderTracked(CurrentSnapshot());
                    e.Handled = true;
                };
                _trackedPanel.Children.Add(toggle);
            }
        }
    }

    private static string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"{Math.Max(0, (int)age.TotalSeconds)}s"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m" : $"{(int)age.TotalHours}h {age.Minutes}m";

    /// <summary>Per-rule alert cooldown for text rules. Shorter than the 5 s used elsewhere
    /// (ALERT-008): a heal rotation announces every few seconds by design, and swallowing
    /// those repeats would silence exactly the case this rule kind exists for.</summary>
    private static readonly TimeSpan TextAlertCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A Text watch rule matched, straight off the ingest thread. Alerting here rather than
    /// from the next snapshot removes a whole refresh interval of lag from the one rule
    /// kind that's about reacting in time. Suppressed during initial ingest, like every
    /// other alert, so replaying today's log at startup fires nothing.
    /// </summary>
    private void OnTextMatched(RawLineEvent raw)
    {
        // Immediate alerts stay suppressed during the startup re-read, but a delayed cue
        // whose due time is still ahead is recovered with the time it has left — losing a
        // running respawn timer to an app restart is exactly when you needed it.
        var ingesting = !_watcher.InitialIngestDone;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var rule in _settings.TrackedRules)
            {
                if (!rule.Enabled || rule.Kind != WatchKind.Text) continue;
                if (!rule.Matches(raw.Line)) continue;
                if (ingesting && rule.AlertDelaySeconds <= 0) continue;

                var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
                var line = raw.Line.Length <= 80 ? raw.Line : raw.Line[..79].TrimEnd() + "…";
                AlertOrCue(rule, name, line, TextAlertCooldown, raw.Time);
            }
        });
    }

    private readonly EQBuddy.UI.Shared.DelayedAlerts _delayedAlerts = new();

    /// <summary>
    /// Alert now, or set a cue for later when the rule asks for a delay
    /// (<see cref="TrackedRule.AlertDelaySeconds"/>) — a complete-heal chain wants the sound
    /// a couple of seconds *after* the call, and a mez wants it before the spell breaks.
    ///
    /// One dispatcher timer per cue rather than the periodic refresh, so a 2.5 s cue lands
    /// at 2.5 s. The cooldown applies when the alert fires, not when it was scheduled: with
    /// a delay set, what matters is how long since you last heard something.
    /// </summary>
    private void AlertOrCue(TrackedRule rule, string ruleName, string label, TimeSpan cooldown,
        DateTime? matchTime = null)
    {
        if (rule.AlertDelaySeconds <= 0)
        {
            FireAlert(rule, ruleName, label, cooldown);
            return;
        }
        // Scheduled from when the line was written, not when we read it.
        var from = matchTime ?? DateTime.Now;
        var remaining = from.AddSeconds(rule.AlertDelaySeconds) - DateTime.Now;
        if (remaining <= TimeSpan.Zero) return;
        if (_delayedAlerts.Schedule(rule, ruleName, label, from) is not { } pending) return;

        DispatcherTimer? timer = null;
        timer = new DispatcherTimer { Interval = remaining };
        timer.Tick += (_, _) =>
        {
            timer!.Stop();
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

    /// <summary>Deaths seen last refresh, so a new one can cancel pending cues — a reminder
    /// to recast something is noise once you're dead.</summary>
    private int _knownDeaths;

    /// <summary>The "Last fight" line above a card's session totals, and the "Session so far"
    /// heading that separates the two. Hidden until there's been a fight.</summary>
    private void ShowLastFight(StatsSnapshot s, Button label, StackPanel body, TextBlock text,
        ItemsControl list, bool healing, bool open)
    {
        if (s.LastFight is not { } f)
        {
            label.IsVisible = body.IsVisible = false;
            return;
        }
        label.IsVisible = true;
        body.IsVisible = open;
        label.Content = $"{(open ? "v" : ">")} {(f.InProgress ? "Current fight" : "Last fight")}";
        if (!open) return;

        // Rates within the fight use the fight's own length, not session combat time.
        FillBreakdown(list, healing ? f.HealsBySpell : f.ByAbility,
            healing ? _healSort : _dmgOutSort, f.DurationSeconds, healing ? "hps" : "dps");
        if (!healing)
        {
            // Same treatment as the WPF card: split line, "Your damage", "Damage you took".
            _combatFightSplit.IsVisible = f.Fights.Count > 1;
            if (f.Fights.Count > 1)
                _combatFightSplit.Text = string.Join(" - ",
                    f.Fights.Select(x => $"{x.Name} {x.DamageOut:N0}"));
            _combatFightOutLabel.IsVisible = f.ByAbility.Count > 0;
            _combatFightInLabel.IsVisible = f.ByIncoming.Count > 0;
            FillList(_combatFightInList, f.ByIncoming.Select(x =>
                (x.Name, $"{x.Total:N0} - x{x.Hits} - avg {(double)x.Total / Math.Max(1, x.Hits):0.#}")));
        }
        text.Text = healing
            ? $"{f.Name} - {f.Healed:N0} healed - {f.Hps:0.#} hps over {f.DurationSeconds:0}s"
              + (f.InProgress ? " (fighting)" : "")
            : $"{f.Name} - {f.DamageOut:N0} dmg - {f.Dps:0.#} dps over {f.DurationSeconds:0}s"
              + $" - took {f.DamageIn:N0}"
              + (f.InProgress ? " (fighting)" : f.Outcome == "Killed" ? "" : $" - {f.Outcome}");
    }

    private void ProcessTrackedAlerts(StatsSnapshot s)
    {
        if (!_watcher.InitialIngestDone) return;
        if (_alertBaselinePath != _watcher.CurrentPath)
        {
            // First run isn't a character switch — cancelling here wiped cues recovered from
            // the log seconds earlier, which is the restart case they exist for.
            var switchedCharacter = _alertBaselinePath is not null;
            _alertBaselinePath = _watcher.CurrentPath;
            _ruleBaseline.Clear();
            foreach (var r in s.Tracked) _ruleBaseline[r.Id] = r.TotalQuantity;
            if (switchedCharacter) _delayedAlerts.CancelAll();
            _knownDeaths = s.Deaths.Count;
            return;
        }
        // Combat cues only: a respawn timer doesn't care that you died.
        if (s.Deaths.Count > _knownDeaths) _delayedAlerts.CancelCombatCues();
        _knownDeaths = s.Deaths.Count;

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
                TimeSpan.FromSeconds(5));
        }
    }

    private void UpdateLoggingStatus()
    {
        DateTime? lastActivity = _watcher.LastGrowth;
        if (lastActivity is null && _watcher.CurrentPath is { } p && File.Exists(p))
            lastActivity = File.GetLastWriteTime(p);
        var age = lastActivity is { } t ? DateTime.Now - t : TimeSpan.MaxValue;
        var brush = age < TimeSpan.FromSeconds(30) ? AppTheme.GoodBrush : age < TimeSpan.FromMinutes(2) ? AppTheme.WarnBrush : AppTheme.BadBrush;
        var tip = lastActivity is { } la ? $"Last log activity: {la:h:mm:ss tt}" : "No log file activity yet";
        _statusDot.Fill = brush;
        _miniDot.Fill = brush;
        ToolTip.SetTip(_statusDot, tip);
        ToolTip.SetTip(_miniDot, tip);
        _logBanner.IsVisible = age > TimeSpan.FromMinutes(2);
    }

    private void SetMode(bool mini)
    {
        _settings.Minimized = mini;
        _miniRoot.IsVisible = mini;
        _normalRoot.IsVisible = !mini;
        Topmost = true;
        _settings.Save();
        if (!mini) _dismissedBreakouts.Clear();
        var snapshot = CurrentSnapshot();
        if (mini) UpdateMiniChips(snapshot);
        UpdateBreakouts(snapshot);
    }

    private static readonly (BreakoutKind Kind, string Star)[] BreakoutStars =
        [(BreakoutKind.Damage, "dps"), (BreakoutKind.Healing, "hps"), (BreakoutKind.Pet, "pet")];

    private void UpdateBreakouts(StatsSnapshot snapshot)
    {
        // Avalonia refuses Show(owner) while the owner itself isn't visible — and the
        // ctor's SetMode lands here before the main window ever opens. A profile saved
        // minimized then CRASHED ON EVERY LAUNCH, unrecoverably (issue #82, Bazzite/KDE:
        // "can't reopen"). The 1-second tick calls back the moment we're actually up.
        if (!IsVisible) return;
        foreach (var (kind, star) in BreakoutStars)
        {
            var wanted = _settings.Minimized && _settings.MiniStats.Contains(star)
                && !_dismissedBreakouts.Contains(kind);
            _breakouts.TryGetValue(kind, out var window);
            if (wanted)
            {
                if (window is null)
                {
                    window = new BreakoutWindow(_settings, kind);
                    window.Dismissed += dismissed => _dismissedBreakouts.Add(dismissed);
                    _breakouts[kind] = window;
                }
                try
                {
                    if (!window.IsVisible) window.Show(this);
                    window.Update(snapshot);
                }
                catch (Exception ex)
                {
                    // A breakout must never take the whole widget down with it (#82).
                    App.LogError(ex);
                }
            }
            else if (window is { IsVisible: true }) window.HideAndSave();
        }
    }

    private void UpdateMiniChips(StatsSnapshot s)
    {
        _miniChips.Children.Clear();
        var selected = MiniStatOrder.Where(_settings.MiniStats.Contains).ToList();
        foreach (var key in selected)
        {
            var text = key switch
            {
                "kills" => $"Kills {s.YourKillCount}",
                "dps" => s.CurrentDps > 0 ? $"{s.CurrentDps:0} dps" : $"{s.SessionDps:0} dps",
                "hps" => $"{s.Hps:0.#} hps",
                "pet" => $"Pet {s.PetAbilities.Sum(row => row.Total) / Math.Max(1, s.CombatSeconds):0.#} dps",
                "loot" => $"Loot {s.LootTotal}",
                "money" => StatsSnapshot.FormatCoin(s.Copper),
                "xp" => $"{s.XpPercent:0.0}%" + (s.HoursToLevel is { } eta ? $" - lvl {FormatEta(eta)}" : ""),
                "deaths" => $"Deaths {s.Deaths.Count}",
                _ => "",
            };
            _miniChips.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
        }
        // Per-rule pins, not every enabled rule: a mini bar with eight chips isn't a mini bar.
        var due = _delayedAlerts.NextDueByRule(DateTime.Now);
        foreach (var rule in _settings.PinWatchChips
                     ? _settings.TrackedRules.Where(r => r.Enabled && r.Pinned)
                     : [])
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t => t.Id == rule.Id);
            // While a cue is counting down, when it fires is the only thing worth the space.
            var counting = due.TryGetValue(rule.Id, out var at);
            _miniChips.Children.Add(new TextBlock
            {
                Text = counting
                    ? $"{name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}"
                    : $"Target {name} {result?.TotalQuantity ?? 0}",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = counting ? AppTheme.WarnBrush : AppTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
        }

        // Only when there is genuinely nothing to show — it used to return early when no
        // stats were starred, hiding pinned watch chips behind the hint.
        if (_miniChips.Children.Count == 0)
            _miniChips.Children.Add(AppTheme.DimText("* star stats in full view"));
    }

    private static string FormatEta(double hours) => hours >= 1
        ? $"~{(int)hours}h {(int)((hours - (int)hours) * 60)}m"
        : $"~{Math.Max(1, (int)(hours * 60))}m";

    private void OnOptions(object? sender, EventArgs e)
    {
        if (_optionsWindow is { IsVisible: true })
        {
            _optionsWindow.Activate();
            return;
        }
        _optionsWindow = new OptionsWindow(this);
        _optionsWindow.Closed += (_, _) => _alertWindow?.ExitPlacement();
        _optionsWindow.Show(this);
        AlertTile.EnterPlacement();
    }

    internal void RegisterOptionsWindow(OptionsWindow window) => _optionsWindow = window;

    private void OnTutorial(object? sender, EventArgs e) => new TutorialWindow(this).Show(this);

    /// <summary>One switch keeps settings, menu, Options, and tracker window in sync.</summary>
    internal void SetTrackSpawns(bool on)
    {
        _settings.TrackSpawns = on;
        _settings.Save();
        SyncTrackSpawnsMenu();
        if (_optionsWindow is { IsVisible: true } options)
            options.SyncTrackSpawns(on);
        if (!on)
        {
            CloseSpawnChips();
            if (_spawnsWindow is { } window)
            {
                _spawnsWindow = null;
                window.Close();
            }
        }
    }

    internal void ShowSpawnsWindow(string? zone = null)
    {
        if (_spawnsWindow is { IsVisible: true })
        {
            _spawnsWindow.Activate();
            return;
        }
        var window = new SpawnsWindow(this, _spawnsVm, zone);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_spawnsWindow, window)) _spawnsWindow = null;
        };
        _spawnsWindow = window;
        window.Show(this);
    }

    private void SyncTrackSpawnsMenu() => _trackSpawnsItem.Header =
        (_settings.TrackSpawns ? "✓ " : "") + "Track spawns (named respawn timers)";

    private void CloseSpawnChips()
    {
        if (_spawnChipsWindow is not { } chips) return;
        _spawnChipsWindow = null;
        chips.Close();
    }

    private void CloseMezChips()
    {
        if (_mezChipsWindow is not { } chips) return;
        _mezChipsWindow = null;
        chips.Close();
    }

    private void OnHistory(object? sender, EventArgs e)
    {
        _archiver.CheckpointSync(CurrentSnapshot());
        if (_historyWindow is { IsVisible: true })
        {
            _historyWindow.Activate();
            return;
        }
        _historyWindow = new HistoryWindow(_repo);
        _historyWindow.Show();
    }

    private void DropCampMarker()
    {
        var s = CurrentSnapshot();
        _stats.AddMarker($"Marker {s.Markers.Count + 1}" +
            (s.CurrentZone.Length > 0 ? $" - {s.CurrentZone}" : ""));
    }

    // Global hotkeys removed 2026-08-06 (Reddit: system-wide registration ate common
    // browser shortcuts like Ctrl+Shift+T). Click-through's trigger is now the context
    // menu + the amber 🔒 unlock chip, mirroring WPF (#7): a menu can't be reached
    // through a transparent window, so the chip is the one solid thing left to click.

    /// <summary>Menu toggle for click-through. Engages only if the platform call actually
    /// succeeds — on Wayland, a missing XFixes, or a backend with no implementation the
    /// state must not flip, or the menu would lie about what clicks do (the backend
    /// logs why).</summary>
    private void SetClickThrough(bool on)
    {
        if (on && !ClickThrough.Set(this, enabled: true)) return;
        if (!on) ClickThrough.Set(this, enabled: false);
        _clickThrough = on;
        _root.BorderBrush = on ? AppTheme.WarnBrush : AppTheme.BorderBrush;
        ToolTip.SetTip(_root, on ? "Click-through ON — click the \U0001F512 chip to interact again" : null);
        _clickThroughItem.Header = (on ? "✓ " : "") + "Click-through (game clicks pass through)";
        if (on)
        {
            _unlockChip ??= new ClickThroughChip(() => SetClickThrough(false));
            _unlockChip.ShowNear(this);
        }
        else
        {
            _unlockChip?.Hide();
        }
    }

    private void OnGear(object? sender, EventArgs e) => _root.ContextMenu?.Open(_root);

    private void OnStarChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var btn = (Button)sender!;
        var key = (string)btn.Tag!;
        if (_settings.MiniStats.Contains(key))
        {
            _settings.MiniStats.Remove(key);
        }
        else
        {
            _settings.MiniStats.Add(key);
        }
        UpdateStarVisuals();
        _settings.Save();
    }

    private void UpdateStarVisuals()
    {
        foreach (var star in _stars.Values)
        {
            var isSelected = _settings.MiniStats.Contains((string)star.Tag!);
            star.Content = AppTheme.Icon(isSelected ? AppIcon.StarFilled : AppIcon.Star, isSelected ? AppTheme.AccentBrush : AppTheme.DimBrush, 13);
        }
    }

    private void CheckForUpdates(bool manual)
    {
        Task.Run(async () =>
        {
            var folder = UpdateChecker.FindUpdateFolder(_settings.UpdateFolder);
            var info = await UpdateChecker.FindBestAsync(_settings.UpdateFolder);
            Dispatcher.UIThread.Post(() =>
            {
                if (_installingUpdate) return;
                if (info is not null && UpdateChecker.IsNewer(info))
                {
                    _pendingUpdate = info;
                    _updateText.Text = UpdateOffer.OfferText(info, OperatingSystem.IsWindows());
                    _updateBanner.IsVisible = true;
                }
                else if (manual)
                {
                    _pendingUpdate = null;
                    _updateText.Text = info is null && folder is null
                        ? "Couldn't check for updates (no update folder, GitHub unreachable)."
                        : $"You're up to date (v{UpdateChecker.CurrentVersion}).";
                    _updateBanner.IsVisible = true;
                    _upToDateNoticeUntil = DateTime.Now.AddSeconds(6);
                }
            });
        });
    }

    internal static readonly (string Name, string File)[] AlertSounds =
    [
        ("Ding", "bell.oga"),
        ("Notify", "message-new-instant.oga"),
        ("Chimes", "service-login.oga"),
        ("Chord", "device-added.oga"),
        ("Tada", "complete.oga"),
        ("Exclamation", "dialog-warning.oga"),
        ("Alarm", "alarm-clock-elapsed.oga"),
    ];

    internal void PlayAlertSound() => PlayAlertSound(_settings.AlertSound);

    /// <summary>
    /// Play a specific sound: a built-in name, or the full path of a custom file. The
    /// argument exists so per-rule sounds work — the point of giving each rule its own sound
    /// is telling them apart by ear, which a single shared sound can't do.
    /// With <paramref name="coalesce"/> on, sounds within <see cref="EQBuddy.UI.Shared.SoundGate.Window"/>
    /// of the last are dropped — several rules firing together are one audio alert (here they
    /// would literally overlap, one player process per sound). Previews keep coalesce off.
    /// </summary>
    internal void PlayAlertSound(string choiceOrPath, bool coalesce = false)
    {
        if (coalesce && !_soundGate.TryClaim(DateTime.Now)) return;
        try
        {
            var choice = choiceOrPath switch
            {
                "Asterisk" or "" => "Ding",
                "Beep" => "Chord",
                "Hand" => "Chimes",
                "Question" => "Notify",
                { } other => other,
            };
            var named = Array.Find(AlertSounds, x => x.Name == choice);
            var file = named.File is { } systemFile
                ? FindDesktopSound(systemFile)
                : choice;
            // Sound themes are not required to carry every freedesktop event. A named
            // built-in should still make noise when its preferred clip is absent.
            if (file.Length == 0 && named.File is not null)
                file = FindDesktopSound("bell.oga");
            if (file.Length > 0 && File.Exists(file))
            {
                var volume = Math.Clamp(_settings.AlertVolume, 0.0, 1.0);
                _ = Task.Run(() => PlaySoundFile(file, volume));
                return;
            }
            App.LogError($"Alert sound file was not found: {choiceOrPath}");
            Console.Beep();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private static string FindDesktopSound(string fileName)
    {
        var dataDirs = new List<string>();
        var userData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(userData))
            dataDirs.Add(userData);
        else
            dataDirs.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"));

        var systemData = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        dataDirs.AddRange(string.IsNullOrWhiteSpace(systemData)
            ? ["/usr/local/share", "/usr/share"]
            : systemData.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var dataDir in dataDirs)
        {
            var path = System.IO.Path.Combine(dataDir, "sounds", "freedesktop", "stereo", fileName);
            if (File.Exists(path)) return path;
        }

        // Ubuntu, Fedora, and desktop environments often install the clip only in the
        // active theme (Yaru, Oxygen, etc.). Prefer freedesktop above for consistency,
        // then accept the same event from any installed theme.
        foreach (var dataDir in dataDirs)
        {
            var sounds = System.IO.Path.Combine(dataDir, "sounds");
            if (!Directory.Exists(sounds)) continue;
            try
            {
                var match = Directory.EnumerateFiles(sounds, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match is not null) return match;
            }
            catch { /* an unreadable theme must not prevent the remaining locations */ }
        }
        return "";
    }

    /// <summary>Try Linux audio backends in order and verify their exit status. Merely
    /// starting pw-play is not success: it can launch and immediately fail to connect or
    /// decode an .oga file, which used to swallow the alert without trying paplay.</summary>
    private static void PlaySoundFile(string file, double volume)
    {
        // Each Linux backend expresses volume differently. Canberra uses decibels,
        // PipeWire uses a 0..1 scalar, and PulseAudio uses 0..65536. ALSA's aplay has
        // no per-stream volume, so it remains the last-resort fallback.
        var decibels = volume <= 0 ? -100 : 20 * Math.Log10(volume);
        var players = new (string Command, string[] Args)[]
        {
            ("canberra-gtk-play", ["--volume", $"{decibels:0.##}", "--file", file]),
            ("pw-play", ["--volume", $"{volume:0.###}", file]),
            ("paplay", ["--volume", $"{(int)Math.Round(volume * 65536)}", file]),
            ("aplay", [file]),
        };
        foreach (var (command, args) in players)
            if (TryPlay(command, args)) return;

        try { Console.Beep(); }
        catch { }
        App.LogError($"Alert sound could not be played by any available backend: {file}");
    }

    private static bool TryPlay(string command, IReadOnlyList<string> args)
    {
        try
        {
            var start = new ProcessStartInfo(command)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return false;
            _ = process.StandardError.ReadToEndAsync(); // drain it so a noisy failure cannot block WaitForExit
            if (!process.WaitForExit(10_000))
            {
                try { process.Kill(entireProcessTree: true); }
                catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private void OnUpdateBannerClick(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (_pendingUpdate is not { } info || _installingUpdate) return;

        if (!UpdateOffer.CanAutoInstall(info, OperatingSystem.IsWindows()))
        {
            var target = UpdateOffer.BrowserTarget(info, OperatingSystem.IsWindows());
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                _pendingUpdate = null;
                _updateText.Text = UpdateOffer.OpenedText(info, OperatingSystem.IsWindows());
                _upToDateNoticeUntil = DateTime.Now.AddSeconds(10);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                // A URL the user must retype should be the short release page, even when
                // the click would have gone straight to the tarball asset.
                _updateText.Text = $"Couldn't open browser - visit {UpdateChecker.GitHubLatestPage}";
            }
            return;
        }
        _installingUpdate = true;
        _updateText.Text = info.DownloadUrl is not null
            ? "Downloading update - EQBuddy will restart itself..."
            : "Installing update - EQBuddy will restart itself...";
        Task.Run(async () =>
        {
            try
            {
                var staged = await UpdateChecker.StageForInstall(info);
                Process.Start(staged, "/SILENT");
                Dispatcher.UIThread.Post(Shutdown);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                Dispatcher.UIThread.Post(() =>
                {
                    _installingUpdate = false;
                    _updateText.Text = "Update failed to start - see error.log.";
                });
            }
        });
    }

    /// <summary>Details-style breakdown whose displayed rate follows parser convention:
    /// source total divided by total combat time. The source's active-time burst rate
    /// remains available in the row tooltip.</summary>
    private void FillBreakdown(ItemsControl list, IEnumerable<SourceDamage> stats,
        StatSort sort, double combatSeconds, string rateLabel)
    {
        var secs = Math.Max(1, combatSeconds);
        static double Avg(SourceDamage d) => (double)d.Total / Math.Max(1, d.Hits);
        double Rate(SourceDamage d) => d.Total / secs;
        var sorted = (sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(Avg),
            StatSort.Rate => stats.OrderByDescending(Rate),
            _ => stats.OrderByDescending(d => d.Total),
        }).ToList();
        if (sorted.Count == 0)
        {
            list.ItemsSource = Array.Empty<Control>();
            return;
        }

        var grand = Math.Max(1, sorted.Sum(d => d.Total));
        Func<SourceDamage, double> metric = sort switch
        {
            StatSort.Hits => d => d.Hits,
            StatSort.Avg => Avg,
            StatSort.Rate => Rate,
            _ => d => d.Total,
        };
        var topMetric = Math.Max(1e-9, sorted.Max(metric));
        var barBrush = AccentBarBrush();
        list.ItemsSource = sorted.Select(d =>
        {
            var critPart = d.Crits > 0
                ? $" - {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit"
                : "";
            var value = $"{d.Total:N0} - ×{d.Hits} - avg {Avg(d):0.#} - {Rate(d):0.#} {rateLabel}{critPart}";
            var tooltip = $"{100.0 * d.Total / grand:0.#}% of total - {rateLabel} = total / {secs:0}s in combat" +
                (d.ActiveSeconds > 0
                    ? $" - burst {d.Total / Math.Max(1, d.ActiveSeconds):0.#}/s over the ~{d.ActiveSeconds:0}s it was in use"
                    : "");
            return BarRow(d.Name, value, metric(d) / topMetric, barBrush, tooltip);
        }).ToList();
    }

    private static Grid BarRow(string name, string value, double fraction, IBrush barBrush, string? tooltip)
    {
        fraction = Math.Clamp(fraction, 0.004, 1.0);
        var row = new Grid
        {
            Margin = new Thickness(0, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var bar = new Border
        {
            Background = barBrush,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
        };
        row.SizeChanged += (_, args) => bar.Width = Math.Max(0, args.NewSize.Width * fraction);
        row.Children.Add(bar);

        var content = new Grid { Margin = new Thickness(4, 1, 0, 1) };
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        content.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        content.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = AppTheme.TextBrush,
        });
        var right = new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = AppTheme.DimBrush,
            Margin = new Thickness(8, 1, 2, 0),
        };
        Grid.SetColumn(right, 1);
        content.Children.Add(right);
        row.Children.Add(content);
        if (tooltip is not null) ToolTip.SetTip(row, tooltip);
        return row;
    }

    private static SolidColorBrush AccentBarBrush()
    {
        var accent = ((SolidColorBrush)AppTheme.AccentBrush).Color;
        return new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B));
    }

    private void FillStatList(ItemsControl list, IEnumerable<SourceDamage> stats, StatSort sort, string unit)
    {
        var sorted = sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(d => (double)d.Total / d.Hits),
            _ => stats.OrderByDescending(d => d.Total),
        };
        FillList(list, sorted.Select(d => (d.Name, $"{d.Total:N0} - {d.Hits} {unit}{(d.Hits == 1 ? "" : "s")} - avg {(double)d.Total / d.Hits:0.#}")));
    }

    private static StatSort ParseSort(object sender) => (string)((TextBlock)sender).Tag! switch
    {
        "hits" => StatSort.Hits,
        "avg" => StatSort.Avg,
        "rate" => StatSort.Rate,
        _ => StatSort.Total,
    };

    private static void SetSortVisual(StatSort mode, TextBlock total, TextBlock hits, TextBlock avg,
        TextBlock? rate = null)
    {
        total.Foreground = mode == StatSort.Total ? AppTheme.AccentBrush : AppTheme.DimBrush;
        hits.Foreground = mode == StatSort.Hits ? AppTheme.AccentBrush : AppTheme.DimBrush;
        avg.Foreground = mode == StatSort.Avg ? AppTheme.AccentBrush : AppTheme.DimBrush;
        if (rate is not null)
            rate.Foreground = mode == StatSort.Rate ? AppTheme.AccentBrush : AppTheme.DimBrush;
    }

    private void OnSortDmgOut(object? sender, PointerPressedEventArgs e)
    {
        _dmgOutSort = ParseSort(sender!);
        SetSortVisual(_dmgOutSort, _dmgOutSortTotal, _dmgOutSortHits, _dmgOutSortAvg, _dmgOutSortDps);
        RefreshUi();
    }

    private void OnSortDmgIn(object? sender, PointerPressedEventArgs e)
    {
        _dmgInSort = ParseSort(sender!);
        SetSortVisual(_dmgInSort, _dmgInSortTotal, _dmgInSortHits, _dmgInSortAvg);
        RefreshUi();
    }

    private void OnSortHeal(object? sender, PointerPressedEventArgs e)
    {
        _healSort = ParseSort(sender!);
        SetSortVisual(_healSort, _healSortTotal, _healSortHits, _healSortAvg, _healSortHps);
        RefreshUi();
    }

    private static void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows,
        Func<string, IBrush>? valueBrush = null, Action<string>? onNameClick = null,
        Func<string, string?>? tooltip = null)
    {
        list.ItemsSource = rows.Select(row =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var left = new TextBlock
            {
                Text = row.Name,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = AppTheme.TextBrush,
                Margin = new Thickness(0, 1, 8, 1),
            };
            if (tooltip?.Invoke(row.Name) is { Length: > 0 } tip)
                ToolTip.SetTip(left, new TextBlock
                {
                    Text = tip,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 340,
                    Foreground = AppTheme.TextBrush,
                });
            if (onNameClick is not null)
            {
                var itemName = row.Name;
                left.Cursor = new Cursor(StandardCursorType.Hand);
                ToolTip.SetTip(left, "Click for item info (eqlwiki)");
                left.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(left).Properties.IsLeftButtonPressed) return;
                    onNameClick(itemName);
                    e.Handled = true;
                };
            }
            grid.Children.Add(left);
            var right = new TextBlock
            {
                Text = row.Value,
                FontSize = 12,
                Foreground = valueBrush?.Invoke(row.Value) ?? AppTheme.DimBrush,
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return grid;
        }).ToList();
    }

    internal void ShowItemInfo(string itemName)
    {
        if (_itemInfoWindow is not { IsVisible: true })
        {
            _itemInfoWindow = new ItemInfoWindow(_wikiItems);
            _itemInfoWindow.Closed += (_, _) => _itemInfoWindow = null;
            _itemInfoWindow.Show(this);
        }
        _itemInfoWindow.Activate();
        _itemInfoWindow.Lookup(itemName);
    }

    private void RenderTargetDrops(StatsSnapshot snapshot)
    {
        var targets = _settings.ShowTargetDrops ? snapshot.CurrentTargets : [];
        if (targets.Count == 0)
        {
            _targetDropsBlock.IsVisible = false;
            return;
        }
        _targetDropsBlock.IsVisible = true;
        foreach (var target in targets)
        {
            if (_targetResults.ContainsKey(target)) continue;
            _targetResults[target] = null;
            _ = LookupTargetAsync(target, snapshot.CurrentZone);
        }

        var observed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var kills = 0;
        foreach (var target in targets)
        {
            var mob = snapshot.Mobs.FirstOrDefault(m =>
                m.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (mob is null) continue;
            kills += mob.Kills;
            foreach (var loot in mob.Loot)
            {
                var name = EqlWikiItemService.NormalizeTitle(loot.Item);
                observed[name] = observed.GetValueOrDefault(name) + loot.Count;
            }
        }

        var rows = new List<(string Name, string Value)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, count) in observed.OrderByDescending(pair => pair.Value))
        {
            var rate = targets.Count == 1 && kills > 0 ? $" · {100.0 * count / kills:0}%" : "";
            rows.Add((item, $"{count} this session{rate}"));
            seen.Add(item);
        }
        foreach (var target in targets)
        {
            if (_targetResults.GetValueOrDefault(target)?.Mob is not { } mob) continue;
            foreach (var (item, rarity) in mob.Drops)
                if (seen.Add(EqlWikiItemService.NormalizeTitle(item)))
                    rows.Add((item, rarity.Length > 0 ? rarity : "listed"));
        }

        var extra = Math.Max(0, rows.Count - 14);
        var names = string.Join(" + ", targets.Take(3)) +
            (targets.Count > 3 ? $" +{targets.Count - 3}" : "");
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
            : targets.Any(target => _targetResults.GetValueOrDefault(target) is null)
                ? "looking up…" : "merged pull";
        _targetDropsHeader.Text = $"🎯 Fighting: {names}" +
            (kills > 0 ? $" — {kills} kill{(kills == 1 ? "" : "s")} this session" : "") +
            $" · drops (eqlwiki · {state}{(extra > 0 ? $" · +{extra} more" : "")})";
        FillList(_targetDropsList, rows.Take(14), onNameClick: ShowItemInfo);
    }

    private async Task LookupTargetAsync(string target, string zone)
    {
        try
        {
            var result = await _wikiMobs.LookupAsync(target, zone);
            _targetResults[target] = result;
            Dispatcher.UIThread.Post(() => RenderTargetDrops(CurrentSnapshot()));
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && _miniRoot.IsVisible)
        {
            SetMode(false);
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
        foreach (var breakout in _breakouts.Values) breakout.Close();
        _settings.WindowLeft = Position.X;
        _settings.WindowTop = Position.Y;
        _settings.Save();
        if (_clickThrough)
            ClickThrough.Set(this, enabled: false);
        _alertWindow?.Close();
        _stats.QuestStore?.Flush();   // debounced writers get their last word (audit #3)
        _stats.AaStore?.Flush();
        _archiver.FinalizeActiveSync(CurrentSnapshot(), "ApplicationExit");
        _watcher.Dispose();
        _repo.Dispose();
        base.OnClosed(e);
        Shutdown();
    }

    private static void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private static Ellipse Dot() => new()
    {
        Width = 9,
        Height = 9,
        Fill = AppTheme.BadBrush,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // Takes an already-translucent wash brush (AppTheme.GoodWashBrush/WarnWashBrush)
    // directly rather than deriving one, so a live theme switch repaints it — the brush
    // reference is the same instance AppTheme.Apply mutates in place.
    private static Border Banner(IBrush brush) => new()
    {
        Background = brush,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 6),
        IsVisible = false,
    };
}
