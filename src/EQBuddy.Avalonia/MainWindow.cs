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
    private readonly LogWatcher _watcher;
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
    private readonly Border _logBanner = Banner(AppTheme.WarnBrush);
    private readonly Border _alertBanner = Banner(AppTheme.AccentBrush);
    private readonly TextBlock _alertText = new() { FontSize = 12, Foreground = AppTheme.AccentBrush, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly Border _updateBanner = Banner(AppTheme.GoodBrush);
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
    private readonly TextBlock _healingSummary = AppTheme.DimText("");
    private readonly TextBlock _killsSummary = AppTheme.DimText("");
    private readonly TextBlock _moneySummary = AppTheme.DimText("");
    private readonly TextBlock _progressSummary = AppTheme.DimText("");
    private readonly ItemsControl _damageSourceList = new();
    private readonly ItemsControl _damageTakenList = new();
    private readonly ItemsControl _healSpellList = new();
    private readonly ItemsControl _healerList = new();
    private readonly ItemsControl _killList = new();
    private readonly ItemsControl _partyKillList = new();
    private readonly ItemsControl _lootList = new();
    private readonly StackPanel _trackedPanel = new();
    private readonly ItemsControl _craftedList = new();
    private readonly ItemsControl _soldList = new();
    private readonly ItemsControl _skillList = new();
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
    private readonly TextBlock _stanceLabel = AppTheme.Heading("By stance");
    private readonly ItemsControl _stanceList = new();
    private readonly TextBlock _farmingLabel = AppTheme.Heading("Farming (per creature)");
    private readonly ItemsControl _farmingList = new();
    private readonly TextBlock _markersLabel = AppTheme.Heading("Camp markers");
    private readonly ItemsControl _markerList = new();
    private readonly Button _gearBtn = AppTheme.IconButton(AppIcon.Settings, "Settings");
    private readonly Dictionary<string, Button> _stars = new();
    private readonly Dictionary<string, SectionPanel> _sections = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel _sectionsPanel = new();
    private TextBlock _dmgOutSortTotal = null!;
    private TextBlock _dmgOutSortHits = null!;
    private TextBlock _dmgOutSortAvg = null!;
    private TextBlock _dmgInSortTotal = null!;
    private TextBlock _dmgInSortHits = null!;
    private TextBlock _dmgInSortAvg = null!;
    private TextBlock _healSortTotal = null!;
    private TextBlock _healSortHits = null!;
    private TextBlock _healSortAvg = null!;
    private DateTime _lastCharScan = DateTime.MinValue;
    private DateTime _lastJanitorRun = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private DateTime _upToDateNoticeUntil = DateTime.MinValue;
    private bool _installingUpdate;
    private bool _clickThrough;
    private X11HotkeyService? _hotkeys;
    private HistoryWindow? _historyWindow;
    private OptionsWindow? _optionsWindow;
    private StatSort _dmgOutSort = StatSort.Total;
    private StatSort _dmgInSort = StatSort.Total;
    private StatSort _healSort = StatSort.Total;

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "loot", "money", "xp", "deaths"];

    private enum StatSort { Total, Hits, Avg }

    public MainWindow()
    {
        _watcher = new LogWatcher(_stats);
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

        if (_settings.LogFolder is { } saved && !Directory.Exists(saved))
            _settings.LogFolder = null;
        _settings.LogFolder ??= LogWatcher.FindDefaultLogFolder();
        RestorePosition();
        ApplyUiScale(_settings.UiScale);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);
        UpdateStarVisuals();
        ApplySectionLayout();
        SetMode(_settings.Minimized);
        FollowActiveCharacter();

        if (_settings.LogFolder is { } lf)
        {
            var prune = _settings.TruncateLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(lf);
                if (prune) EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap);
            });
        }

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();
        Loaded += (_, _) => RegisterGlobalHotkeys();
    }

    public double UiScale => _settings.UiScale;
    public double WidgetOpacity => Opacity;
    public double BackgroundOpacityValue => _settings.BackgroundOpacity;
    public bool TruncateLogsValue => _settings.TruncateLogs;
    public AppSettings Settings => _settings;
    public void PersistSettings() => _settings.Save();

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
                _alertBanner,
                BuildMiniRoot(),
                BuildNormalRoot(),
            },
        };
        _alertBanner.Child = _alertText;
        _alertBanner.Margin = new Thickness(0, 0, 0, 8);
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
        reset.Click += (_, _) => _stats.Reset();
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
        _combatSummary.Margin = new Thickness(0, 2, 0, 4);
        panel.Children.Add(_combatSummary);
        panel.Children.Add(SortHeader("Damage by attack", out _dmgOutSortTotal, out _dmgOutSortHits, out _dmgOutSortAvg, OnSortDmgOut));
        panel.Children.Add(_damageSourceList);
        panel.Children.Add(SortHeader("Damage taken from", out _dmgInSortTotal, out _dmgInSortHits, out _dmgInSortAvg, OnSortDmgIn));
        panel.Children.Add(_damageTakenList);
        _recentFightsLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_recentFightsLabel);
        panel.Children.Add(_recentFightsList);
        _stanceLabel.Margin = new Thickness(0, 6, 0, 0);
        panel.Children.Add(_stanceLabel);
        panel.Children.Add(_stanceList);
        return panel;
    }

    private Control BuildHealingSection()
    {
        var panel = new StackPanel();
        _healingSummary.Margin = new Thickness(0, 2, 0, 4);
        panel.Children.Add(_healingSummary);
        var sort = SortHeader("Heals cast", out _healSortTotal, out _healSortHits, out _healSortAvg, OnSortHeal, _healSpellsLabel, _healSortBar);
        panel.Children.Add(sort);
        panel.Children.Add(_healSpellList);
        panel.Children.Add(_healersLabel);
        panel.Children.Add(_healerList);
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
        EventHandler<PointerPressedEventArgs> handler, TextBlock? titleBlock = null, StackPanel? sortBar = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(titleBlock ?? AppTheme.Heading(title));
        sortBar ??= new StackPanel { Orientation = Orientation.Horizontal };
        sortBar.HorizontalAlignment = HorizontalAlignment.Right;
        sortBar.Children.Add(AppTheme.DimText("sort:", new Thickness(0, 0, 4, 0)));
        total = SortLink("total", "total", handler, selected: true);
        hits = SortLink(title.Contains("Heal", StringComparison.OrdinalIgnoreCase) ? "casts" : "hits", "hits", handler);
        avg = SortLink("avg", "avg", handler);
        sortBar.Children.Add(total);
        sortBar.Children.Add(hits);
        sortBar.Children.Add(avg);
        Grid.SetColumn(sortBar, 1);
        grid.Children.Add(sortBar);
        return grid;
    }

    private static TextBlock SortLink(string text, string tag, EventHandler<PointerPressedEventArgs> handler, bool selected = false)
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
        link.PointerPressed += handler;
        return link;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var version = new MenuItem { Header = $"EQBuddy v{UpdateChecker.CurrentVersion}", IsEnabled = false };
        var check = new MenuItem { Header = "Check for updates" };
        check.Click += (_, _) => { _lastUpdateCheck = DateTime.Now; CheckForUpdates(manual: true); };
        var options = new MenuItem { Header = "Options... (size, opacity, tracked loot)" };
        options.Click += OnOptions;
        var marker = new MenuItem { Header = "Drop camp marker" };
        marker.Click += (_, _) => DropCampMarker();
        var history = new MenuItem { Header = "Session history..." };
        history.Click += OnHistory;
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
        menu.Items.Add(marker);
        menu.Items.Add(history);
        menu.Items.Add(new Separator());
        menu.Items.Add(choose);
        menu.Items.Add(detect);
        return menu;
    }

    private void RestorePosition()
    {
        if (!double.IsNaN(_settings.WindowLeft))
            Position = new PixelPoint((int)_settings.WindowLeft, (int)_settings.WindowTop);
    }

    private void ApplyUiScale(double scale)
    {
        _scaleRoot.LayoutTransform = Math.Abs(scale - 1.0) < 0.001 ? null : new ScaleTransform(scale, scale);
        _scaleRoot.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void ApplyBackgroundOpacity(double opacity) => _root.Background = AppTheme.BgWithOpacity(opacity);

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
            var prune = _settings.TruncateLogs;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(folder);
                if (prune) EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap);
            });
        }

        UpdateLoggingStatus();
        if (_upToDateNoticeUntil != DateTime.MinValue && DateTime.Now > _upToDateNoticeUntil && _pendingUpdate is null && !_installingUpdate)
        {
            _updateBanner.IsVisible = false;
            _upToDateNoticeUntil = DateTime.MinValue;
        }
        if (_alertUntil != DateTime.MinValue && DateTime.Now > _alertUntil)
        {
            _alertBanner.IsVisible = false;
            _alertUntil = DateTime.MinValue;
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
        RefreshExpandedSections(s);
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
            _combatSummary.Text =
                $"Dealt {s.DamageDealt:N0} ({s.MeleeDamage:N0} melee / {s.SpellDamage:N0} spell)\n" +
                $"{s.CritCount} crits ({critRate:0.#}% rate) - {acc:0}% accuracy\n" +
                $"In combat {(int)combatTime.TotalMinutes}m {combatTime.Seconds}s this session\n" +
                (s.Recent is { } rc ? $"Last {(int)rc.Window.TotalMinutes}m: {rc.Dps:0.#} dps{(rc.HasFullWindow ? "" : " (partial window)")}\n" : "") +
                $"Biggest hit: {s.MaxHit:N0} ({s.MaxHitDesc})\n" +
                $"Taken {s.DamageTaken:N0} - avoided {s.AvoidedIncoming} of {incomingSwings} melee attacks ({avoidance:0}%)" +
                (s.SpecialHits.Count > 0 ? "\n" + string.Join(" - ", s.SpecialHits.Select(x => $"{x.Name} {x.Count}")) : "") +
                (s.Fizzles + s.Resists > 0 ? $"\nFizzles {s.Fizzles} - resists {s.Resists}" : "") +
                (s.CurrentStance.Length > 0 ? $"\nStance: {s.CurrentStance}" : "");
            FillStatList(_damageSourceList, s.DamageBySource, _dmgOutSort, "hit");
            FillStatList(_damageTakenList, s.DamageByAttacker, _dmgInSort, "hit");
            _recentFightsLabel.IsVisible = s.RecentEncounters.Count > 0;
            FillList(_recentFightsList, s.RecentEncounters.Select(f =>
                (f.Name, $"{f.DurationSeconds:0}s - {f.Dps:0.#} dps{(f.Outcome == "Timeout" ? " - ?" : "")}")));
            _stanceLabel.IsVisible = s.Stances.Count > 0;
            FillList(_stanceList, s.Stances.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg - {(int)x.CombatSeconds}s - {x.Dps:0.#} dps")));
        }
        _healingHeader.Text = s.Hps > 0 ? $"{s.Hps:0.#} hps" : $"{s.HealingDone:N0} healed";
        if (_sections["healing"].IsExpanded)
        {
            _healingSummary.Text = $"Done {s.HealingDone:N0} - received {s.HealingReceived:N0}" +
                (s.Recent is { Hps: > 0 } rh ? $"\nLast {(int)rh.Window.TotalMinutes}m: {rh.Hps:0.#} hps" : "") +
                (s.RegenTicks > 0 ? $"\n{s.RegenTicks} regen/hymn ticks (game logs no amounts for these)" : "");
            var showSpells = s.HealsBySpell.Count > 0;
            _healSpellsLabel.IsVisible = showSpells;
            _healSortBar.IsVisible = showSpells;
            FillStatList(_healSpellList, s.HealsBySpell, _healSort, "cast");
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
            FillList(_lootList, s.Loot.Select(l => (l.Item, $"x{l.Count}")));
            _craftedLabel.IsVisible = s.Crafted.Count > 0;
            FillList(_craftedList, s.Crafted.Select(c => (c.Name, $"x{c.Count}")));
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
        }
        if (_sections["faction"].IsExpanded)
            FillList(_factionList, s.Faction.Select(f => (f.Faction, $"{(f.Net >= 0 ? "+" : "")}{f.Net}")),
                valueBrush: f => f.StartsWith('-') ? AppTheme.BadBrush : AppTheme.GoodBrush);
        if (_sections["misc"].IsExpanded)
        {
            FillList(_deathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
            FillList(_zoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
            _markersLabel.IsVisible = s.Markers.Count > 0;
            FillList(_markerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
        }
    }

    private void RefreshOptionalSectionVisibility(StatsSnapshot s)
    {
        _recentFightsLabel.IsVisible = s.RecentEncounters.Count > 0;
        _stanceLabel.IsVisible = s.Stances.Count > 0;
        _farmingLabel.IsVisible = s.Mobs.Any(m => m.Kills > 0);
        _partyKillsLabel.IsVisible = s.PartyKillsByKiller.Count > 0;
        _craftedLabel.IsVisible = s.Crafted.Count > 0;
        _soldLabel.IsVisible = s.SoldItems.Count > 0;
        _healSpellsLabel.IsVisible = s.HealsBySpell.Count > 0;
        _healSortBar.IsVisible = s.HealsBySpell.Count > 0;
        _healersLabel.IsVisible = s.HealsByHealer.Count > 0;
        _markersLabel.IsVisible = s.Markers.Count > 0;
    }

    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _ruleLastAlert = new(StringComparer.OrdinalIgnoreCase);
    private string? _alertBaselinePath;
    private DateTime _alertUntil = DateTime.MinValue;

    private void RenderTracked(StatsSnapshot s)
    {
        var haveRules = _settings.TrackedRules.Count > 0 && !_settings.HiddenSections.Contains("tracked");
        if (_sections.TryGetValue("tracked", out var section))
            section.IsVisible = haveRules;
        if (!haveRules) return;

        _trackedHeader.Text = s.Tracked.Sum(t => t.TotalQuantity).ToString();
        if (!_sections["tracked"].IsExpanded) return;

        _trackedPanel.Children.Clear();
        foreach (var r in s.Tracked)
        {
            var head = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            head.Children.Add(new TextBlock
            {
                Text = r.Name.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.AccentBrush,
            });
            var rate = AppTheme.DimText($"{r.TotalQuantity} total - {r.PerHour:0.#}/hr - {r.PerActiveHour:0.#}/active hr");
            Grid.SetColumn(rate, 1);
            head.Children.Add(rate);
            _trackedPanel.Children.Add(head);

            foreach (var item in r.Items)
                _trackedPanel.Children.Add(new TextBlock
                {
                    Text = $"{item.Name}   x{item.Count}",
                    FontSize = 12,
                    Foreground = AppTheme.TextBrush,
                    Margin = new Thickness(6, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            _trackedPanel.Children.Add(AppTheme.DimText(
                r.LastMatch is { } lm ? $"last match {FormatAge(DateTime.Now - lm)} ago" : "no matches yet",
                new Thickness(6, 1, 0, 2)));
        }
    }

    private static string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"{Math.Max(0, (int)age.TotalSeconds)}s"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m" : $"{(int)age.TotalHours}h {age.Minutes}m";

    private void ProcessTrackedAlerts(StatsSnapshot s)
    {
        if (!_watcher.InitialIngestDone) return;
        if (_alertBaselinePath != _watcher.CurrentPath)
        {
            _alertBaselinePath = _watcher.CurrentPath;
            _ruleBaseline.Clear();
            foreach (var r in s.Tracked) _ruleBaseline[r.Name] = r.TotalQuantity;
            return;
        }

        foreach (var r in s.Tracked)
        {
            var baseline = _ruleBaseline.TryGetValue(r.Name, out var b) ? b : 0;
            if (r.TotalQuantity <= baseline)
            {
                _ruleBaseline[r.Name] = r.TotalQuantity;
                continue;
            }
            var delta = r.TotalQuantity - baseline;
            _ruleBaseline[r.Name] = r.TotalQuantity;
            var rule = _settings.TrackedRules.FirstOrDefault(x =>
                string.Equals(x.Name.Length > 0 ? x.Name : x.Pattern, r.Name, StringComparison.OrdinalIgnoreCase));
            if (rule is null) continue;
            var last = _ruleLastAlert.TryGetValue(r.Name, out var la) ? la : DateTime.MinValue;
            if (DateTime.Now - last < TimeSpan.FromSeconds(5)) continue;
            _ruleLastAlert[r.Name] = DateTime.Now;
            if (rule.AlertBanner)
            {
                _alertText.Text = $"* {r.Name}: {r.LastItem ?? "match"}{(delta > 1 ? $" x{delta}" : "")}";
                _alertBanner.IsVisible = true;
                _alertUntil = DateTime.Now.AddSeconds(6);
            }
            if (rule.AlertSound) PlayAlertSound();
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
        if (mini) UpdateMiniChips(CurrentSnapshot());
    }

    private void UpdateMiniChips(StatsSnapshot s)
    {
        _miniChips.Children.Clear();
        var selected = MiniStatOrder.Where(_settings.MiniStats.Contains).ToList();
        if (selected.Count == 0)
        {
            _miniChips.Children.Add(AppTheme.DimText("* star stats in full view"));
            return;
        }
        foreach (var key in selected)
        {
            var text = key switch
            {
                "kills" => $"Kills {s.YourKillCount}",
                "dps" => s.CurrentDps > 0 ? $"{s.CurrentDps:0} dps" : $"{s.SessionDps:0} dps",
                "hps" => $"{s.Hps:0.#} hps",
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
        foreach (var rule in _settings.TrackedRules.Where(r => r.Enabled && r.Pinned))
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            _miniChips.Children.Add(new TextBlock
            {
                Text = $"Target {name} {result?.TotalQuantity ?? 0}",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
        }
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
        _optionsWindow.Show(this);
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

    private void RegisterGlobalHotkeys()
    {
        if (_hotkeys is not null) return;
        try
        {
            _hotkeys = new X11HotkeyService(
            [
                (_settings.HotkeyToggleOverlay, ToggleOverlayVisibility),
                (_settings.HotkeyClickThrough, ToggleClickThrough),
                (_settings.HotkeyMiniMode, () => SetMode(!_settings.Minimized)),
                (_settings.HotkeyCampMarker, DropCampMarker),
            ]);
        }
        catch (Exception ex)
        {
            App.LogError($"Global hotkeys disabled: {ex.Message}");
        }
    }

    private void ToggleOverlayVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Topmost = true;
            Activate();
        }
    }

    private void ToggleClickThrough()
    {
        var next = !_clickThrough;
        if (!X11ClickThrough.Set(this, next)) return;
        _clickThrough = next;
        _root.BorderBrush = _clickThrough ? AppTheme.WarnBrush : AppTheme.BorderBrush;
        Topmost = true;
        ToolTip.SetTip(_root, _clickThrough
            ? $"Click-through ON - press {_settings.HotkeyClickThrough} to interact again"
            : null);
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
            var info = folder is null ? null : UpdateChecker.Check(folder);
            if (info is null && await UpdateChecker.CheckGitHubAsync() is { } webLatest)
                info = new UpdateInfo(webLatest, SetupPath: null);
            Dispatcher.UIThread.Post(() =>
            {
                if (_installingUpdate) return;
                if (info is not null && UpdateChecker.IsNewer(info))
                {
                    _pendingUpdate = info;
                    _updateText.Text = info.SetupPath is not null
                        ? $"Update v{info.Latest} is ready - click here to install."
                        : $"Update v{info.Latest} is available - click to open the download page.";
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
        ("Ding", "Windows Ding.wav"),
        ("Notify", "Windows Notify.wav"),
        ("Chimes", "chimes.wav"),
        ("Chord", "chord.wav"),
        ("Tada", "tada.wav"),
        ("Exclamation", "Windows Exclamation.wav"),
        ("Alarm", "Alarm01.wav"),
    ];

    internal void PlayAlertSound()
    {
        try
        {
            var choice = _settings.AlertSound switch
            {
                "Asterisk" or "" => "Ding",
                "Beep" => "Chord",
                "Hand" => "Chimes",
                "Question" => "Notify",
                { } other => other,
            };
            var named = Array.Find(AlertSounds, x => x.Name == choice);
            var file = named.File is { } ? "" : choice;
            if (file.Length > 0 && File.Exists(file))
            {
                if (TryStart("paplay", file) || TryStart("aplay", file))
                    return;
            }
            Console.Beep();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private static bool TryStart(string command, string file)
    {
        try
        {
            Process.Start(new ProcessStartInfo(command, file) { UseShellExecute = false });
            return true;
        }
        catch { return false; }
    }

    private void OnUpdateBannerClick(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (_pendingUpdate is not { } info || _installingUpdate) return;
        if (info.SetupPath is null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(UpdateChecker.GitHubLatestPage) { UseShellExecute = true });
                _pendingUpdate = null;
                _updateText.Text = "Download page opened - run the new EQBuddySetup.exe to update.";
                _upToDateNoticeUntil = DateTime.Now.AddSeconds(10);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                _updateText.Text = $"Couldn't open browser - visit {UpdateChecker.GitHubLatestPage}";
            }
            return;
        }
        _installingUpdate = true;
        _updateText.Text = "Installing update - EQBuddy will restart itself...";
        Task.Run(() =>
        {
            try
            {
                var staged = UpdateChecker.StageForInstall(info);
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
        _ => StatSort.Total,
    };

    private static void SetSortVisual(StatSort mode, TextBlock total, TextBlock hits, TextBlock avg)
    {
        total.Foreground = mode == StatSort.Total ? AppTheme.AccentBrush : AppTheme.DimBrush;
        hits.Foreground = mode == StatSort.Hits ? AppTheme.AccentBrush : AppTheme.DimBrush;
        avg.Foreground = mode == StatSort.Avg ? AppTheme.AccentBrush : AppTheme.DimBrush;
    }

    private void OnSortDmgOut(object? sender, PointerPressedEventArgs e)
    {
        _dmgOutSort = ParseSort(sender!);
        SetSortVisual(_dmgOutSort, _dmgOutSortTotal, _dmgOutSortHits, _dmgOutSortAvg);
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
        SetSortVisual(_healSort, _healSortTotal, _healSortHits, _healSortAvg);
        RefreshUi();
    }

    private static void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows, Func<string, IBrush>? valueBrush = null)
    {
        list.ItemsSource = rows.Select(row =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.Children.Add(new TextBlock
            {
                Text = row.Name,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = AppTheme.TextBrush,
                Margin = new Thickness(0, 1, 8, 1),
            });
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
        _settings.WindowLeft = Position.X;
        _settings.WindowTop = Position.Y;
        _settings.Save();
        if (_clickThrough)
            X11ClickThrough.Set(this, enabled: false);
        _hotkeys?.Dispose();
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

    private static Border Banner(IBrush brush) => new()
    {
        Background = new SolidColorBrush(((SolidColorBrush)brush).Color, 0.20),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 6),
        IsVisible = false,
    };
}
