using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;

namespace EQBuddy;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SessionStats _stats = new();
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

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "loot", "money", "xp", "deaths"];

    private enum StatSort { Total, Hits, Avg, Rate }
    private StatSort _dmgOutSort = StatSort.Total;
    private StatSort _dmgInSort = StatSort.Total;
    private StatSort _healSort = StatSort.Total;

    public MainWindow()
    {
        InitializeComponent();
        _watcher = new LogWatcher(_stats);
        _archiver = new SessionArchiver(_repo);
        // A 60-minute quiet gap ends a session — persist its final state to history.
        _stats.SessionEnding += snap => _archiver.FinalizeActive(snap, "IdleTimeout");

        MaxHeight = SystemParameters.WorkArea.Height - 20;
        SectionScroll.MaxHeight = SystemParameters.WorkArea.Height - 160;

        // Migration: any per-rule pin from older versions turns on the group pin.
        if (!_settings.PinWatchChips && _settings.TrackedRules.Any(r => r.Pinned))
            _settings.PinWatchChips = true;

        if (_settings.LogFolder is { } saved && !System.IO.Directory.Exists(saved))
            _settings.LogFolder = null; // stale saved path (game moved) — re-detect
        _settings.LogFolder ??= LogWatcher.FindDefaultLogFolder();
        if (!double.IsNaN(_settings.WindowLeft)) { Left = _settings.WindowLeft; Top = _settings.WindowTop; }
        else { Left = SystemParameters.WorkArea.Right - 360; Top = 40; }
        Opacity = _settings.Opacity;
        Topmost = true;
        ApplyUiScale(_settings.UiScale);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);

        VersionMenuItem.Header = $"EQBuddy v{UpdateChecker.CurrentVersion}";

        foreach (var (key, star) in StarButtons())
            star.IsChecked = _settings.MiniStats.Contains(key);
        ApplySectionLayout();
        SetMode(_settings.Minimized);

        FollowActiveCharacter();

        // The quick tour shows at every launch until disabled ("Never show again"
        // in the tour, or the Options checkbox).
        if (_settings.ShowTutorial)
            Loaded += (_, _) => new TutorialWindow(this).Show();

        // Log hygiene at startup: force Log=1 and wipe finished-session logs
        // (both no-ops while the game is running). Truncation waits while the tour
        // is enabled — its first page is the consent question; the 10-minute
        // periodic janitor handles it afterwards.
        if (_settings.LogFolder is { } lf)
        {
            var prune = _settings.TruncateLogs && !_settings.ShowTutorial;
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(lf);
                if (prune) EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap);
            });
        }

        if (Environment.GetEnvironmentVariable("EQBUDDY_EXPAND") == "1")
            foreach (var ex in new[] { CombatSection, HealingSection, KillsSection, LootSection,
                         TrackedSection, MoneySection, ProgressSection, FactionSection, MiscSection })
                ex.IsExpanded = true;

        if (Environment.GetEnvironmentVariable("EQBUDDY_OPTIONS") == "1")
            Loaded += (_, _) => OnOptions(this, new RoutedEventArgs());

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

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshUi();
        _uiTimer.Start();
    }

    public AppSettings Settings => _settings;
    public void PersistSettings() => _settings.Save();

    internal static readonly (string Key, string Title)[] SectionCatalog =
        EQBuddy.UI.Shared.OverlaySections.Catalog;

    private Dictionary<string, UIElement> SectionMap() => new()
    {
        ["combat"] = CombatSection, ["healing"] = HealingSection, ["kills"] = KillsSection,
        ["loot"] = LootSection, ["tracked"] = TrackedSection, ["money"] = MoneySection,
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

    public double UiScale => _settings.UiScale;

    public void SetUiScale(double scale)
    {
        _settings.UiScale = Math.Clamp(scale, 0.5, 2.0);
        ApplyUiScale(_settings.UiScale);
        _settings.Save();
    }

    private void ApplyUiScale(double scale) =>
        RootBorder().LayoutTransform = Math.Abs(scale - 1.0) < 0.001
            ? null
            : new System.Windows.Media.ScaleTransform(scale, scale);

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

    private void ApplyBackgroundOpacity(double opacity) =>
        RootBorder().Background = new SolidColorBrush(
            Color.FromArgb((byte)(opacity * 255), 0x1C, 0x19, 0x17));

    private OptionsWindow? _optionsWindow;

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

    private System.Windows.Controls.Border RootBorder() =>
        (System.Windows.Controls.Border)Content;

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
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(folder);
                if (prune) EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap);
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

        ZoneText.Text = s.CurrentZone.Length > 0 ? s.CurrentZone : "—";
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
        MoneyHeader.Text = StatsSnapshot.FormatCoin(s.Copper);
        ProgressHeader.Text = $"{s.XpPercent:0.0}% xp"
            + (s.Levels.Count > 0 ? $", +{s.Levels.Count} lvl" : "")
            + (s.AaGained > 0 ? $", +{s.AaGained} aa" : "");
        FactionHeader.Text = s.Faction.Count > 0 ? $"{s.Faction.Count} factions" : "—";
        MiscHeader.Text = $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}";

        if (CombatSection.IsExpanded)
        {
            var acc = s.HitCount + s.MissCount > 0
                ? (double)s.HitCount / (s.HitCount + s.MissCount) * 100 : 0;
            var critRate = s.HitCount > 0 ? (double)s.CritCount / s.HitCount * 100 : 0;
            var incomingSwings = s.AvoidedIncoming + s.MeleeHitsTaken;
            var avoidance = incomingSwings > 0
                ? (double)s.AvoidedIncoming / incomingSwings * 100 : 0;
            var combatTime = TimeSpan.FromSeconds(s.CombatSeconds);
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
                (s.Fizzles + s.Resists > 0 ? $"\nFizzles {s.Fizzles} · resists {s.Resists}" : "") +
                (s.CurrentStance.Length > 0 ? $"\nStance: {s.CurrentStance}" : "");
            FillBreakdown(DamageSourceList, s.DamageBySource, _dmgOutSort, s.CombatSeconds, "dps");
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
            StanceLabel.Visibility = s.Stances.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(StanceList, s.Stances.Select(x =>
                (x.Name, $"{x.Damage:N0} dmg · {(int)x.CombatSeconds}s · {x.Dps:0.#} dps")));
        }

        HealingHeader.Text = s.Hps > 0 ? $"{s.Hps:0.#} hps" : $"{s.HealingDone:N0} healed";
        if (HealingSection.IsExpanded)
        {
            HealingSummary.Text =
                $"Done {s.HealingDone:N0} · received {s.HealingReceived:N0}" +
                (s.Recent is { Hps: > 0 } rh
                    ? $"\nLast {(int)rh.Window.TotalMinutes}m: {rh.Hps:0.#} hps"
                    : "") +
                (s.RegenTicks > 0 ? $"\n{s.RegenTicks} regen/hymn ticks (game logs no amounts for these)" : "");
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
            FillList(LootList, s.Loot.Select(l => (l.Item, $"×{l.Count}")));
            CraftedLabel.Visibility = s.Crafted.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(CraftedList, s.Crafted.Select(c => (c.Name, $"×{c.Count}")));
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
        }

        if (FactionSection.IsExpanded)
            FillList(FactionList, s.Faction.Select(f =>
                (f.Faction, $"{(f.Net >= 0 ? "+" : "")}{f.Net}")),
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

    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _ruleLastAlert = new(StringComparer.OrdinalIgnoreCase);
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
        foreach (var r in s.Tracked)
        {
            var head = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock
            {
                Text = r.Name.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
            });
            var rate = new TextBlock
            {
                Text = $"{r.TotalQuantity} total · {r.PerHour:0.#}/hr · {r.PerActiveHour:0.#}/active hr",
                FontSize = 11, Foreground = (Brush)FindResource("DimBrush"),
            };
            Grid.SetColumn(rate, 1);
            head.Children.Add(rate);
            TrackedPanel.Children.Add(head);

            foreach (var item in r.Items)
                TrackedPanel.Children.Add(new TextBlock
                {
                    Text = $"{item.Name}   ×{item.Count}", FontSize = 12,
                    Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(6, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            if (r.LastMatch is { } lm)
                TrackedPanel.Children.Add(new TextBlock
                {
                    Text = $"last drop {FormatAge(DateTime.Now - lm)} ago", FontSize = 11,
                    Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(6, 1, 0, 2),
                });
            else
                TrackedPanel.Children.Add(new TextBlock
                {
                    Text = "no matches yet", FontSize = 11,
                    Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(6, 1, 0, 2),
                });
        }
    }

    private static string FormatAge(TimeSpan age) => age.TotalMinutes < 1
        ? $"{Math.Max(0, (int)age.TotalSeconds)}s"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m" : $"{(int)age.TotalHours}h {age.Minutes}m";

    /// <summary>
    /// Fire banner/sound alerts when a tracked rule's total grows. Baselines are reset
    /// (without alerting) whenever the watched log changes, so startup ingest and
    /// character switches never replay old drops (ALERT-007, RECOVERY-006).
    /// </summary>
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
            if (DateTime.Now - last < TimeSpan.FromSeconds(5)) continue;   // ALERT-008 cooldown
            _ruleLastAlert[r.Name] = DateTime.Now;

            if (rule.AlertBanner)
                AlertTile.ShowAlert($"★ {r.Name}: {r.LastItem ?? "match"}{(delta > 1 ? $" ×{delta}" : "")}");
            if (rule.AlertSound)
                PlayAlertSound();
        }
    }

    private System.Windows.Media.MediaPlayer? _alertPlayer;

    /// <summary>Named alert sounds → distinct files in C:\Windows\Media (shared
    /// catalog). SystemSounds is useless here: most of its entries share one "ding"
    /// in the default scheme and Question is typically unassigned (silent).</summary>
    internal static readonly (string Name, string File)[] AlertSounds =
        EQBuddy.UI.Shared.AlertSoundCatalog.Sounds;

    /// <summary>Play the configured alert sound: a named built-in, or a custom
    /// .wav/.mp3 path. Unknown/missing values fall back to the system Asterisk.</summary>
    internal void PlayAlertSound()
    {
        try
        {
            // Legacy SystemSounds names from earlier settings map onto the palette.
            var choice = EQBuddy.UI.Shared.AlertSoundCatalog.Normalize(_settings.AlertSound);
            var named = Array.Find(AlertSounds, x => x.Name == choice);
            var file = named.File is { } f
                ? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", f)
                : choice;
            if (System.IO.File.Exists(file))
            {
                _alertPlayer ??= new System.Windows.Media.MediaPlayer();
                _alertPlayer.Open(new Uri(file));
                _alertPlayer.Play();
                return;
            }
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private void OnTutorial(object sender, RoutedEventArgs e) => new TutorialWindow(this).Show();

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
        _historyWindow = new HistoryWindow(_repo);
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
        yield return ("kills", StarKills);
        yield return ("loot", StarLoot);
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
        _settings.Save();
        if (mini) UpdateMiniChips(_stats.Snapshot());
    }

    private void UpdateMiniChips(StatsSnapshot s)
    {
        MiniChips.Children.Clear();
        var selected = MiniStatOrder.Where(_settings.MiniStats.Contains).ToList();
        if (selected.Count == 0)
        {
            MiniChips.Children.Add(new TextBlock
            {
                Text = "☆ star stats in full view", FontSize = 12,
                Foreground = (Brush)FindResource("DimBrush"), VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }
        foreach (var key in selected)
        {
            var text = key switch
            {
                "kills" => $"\U0001F480 {s.YourKillCount}",
                "dps" => s.CurrentDps > 0 ? $"⚔ {s.CurrentDps:0} dps" : $"⚔ {s.SessionDps:0} dps",
                "hps" => $"✚ {s.Hps:0.#} hps",
                "loot" => $"\U0001F392 {s.LootTotal}",
                "money" => $"\U0001F4B0 {StatsSnapshot.FormatCoin(s.Copper)}",
                "xp" => $"\U0001F4C8 {s.XpPercent:0.0}%" +
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

        // One pin for the whole watch group: chips for every enabled rule (TRACK-006).
        foreach (var rule in _settings.PinWatchChips
                     ? _settings.TrackedRules.Where(r => r.Enabled)
                     : [])
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            MiniChips.Children.Add(new TextBlock
            {
                Text = $"🎯 {name} {result?.TotalQuantity ?? 0}",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            });
        }
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
            // Local (OneDrive) update folder is authoritative when present; otherwise
            // fall back to the GitHub Releases feed for public installs.
            var folder = UpdateChecker.FindUpdateFolder(_settings.UpdateFolder);
            var info = folder is null ? null : UpdateChecker.Check(folder);
            if (info is null && await UpdateChecker.CheckGitHubAsync() is { } webLatest)
                info = new UpdateInfo(webLatest, SetupPath: null);

            Dispatcher.Invoke(() =>
            {
                if (_installingUpdate) return;
                if (info is not null && UpdateChecker.IsNewer(info))
                {
                    _pendingUpdate = info;
                    UpdateText.Text = info.SetupPath is not null
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

        if (info.SetupPath is null)
        {
            // Web update: send the user to the GitHub release page.
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
        UpdateText.Text = "Installing update — EQBuddy will restart itself…";
        Task.Run(() =>
        {
            try
            {
                var staged = UpdateChecker.StageForInstall(info);
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
        StatSort sort, double combatSeconds, string rateLabel)
    {
        var secs = Math.Max(1, combatSeconds);
        double Rate(SourceDamage d) => d.Total / secs;
        static double Avg(SourceDamage d) => (double)d.Total / Math.Max(1, d.Hits);
        var sorted = (sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(Avg),
            StatSort.Rate => stats.OrderByDescending(Rate),
            _ => stats.OrderByDescending(d => d.Total),
        }).ToList();
        list.Items.Clear();
        if (sorted.Count == 0) return;
        var grand = Math.Max(1, sorted.Sum(d => d.Total));
        Func<SourceDamage, double> metric = sort switch
        {
            StatSort.Hits => d => d.Hits,
            StatSort.Avg => Avg,
            StatSort.Rate => Rate,
            _ => d => d.Total,
        };
        var topMetric = Math.Max(1e-9, sorted.Max(metric));
        var barBrush = BreakdownRows.BarBrush(this);

        foreach (var d in sorted)
        {
            var critPart = d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : "";
            var value = $"{d.Total:N0} · ×{d.Hits} · avg {Avg(d):0.#} · {Rate(d):0.#} {rateLabel}{critPart}";
            var tooltip = $"{100.0 * d.Total / grand:0.#}% of total · {rateLabel} = total ÷ {secs:0}s in combat" +
                (d.ActiveSeconds > 0
                    ? $" · burst {d.Total / Math.Max(1, d.ActiveSeconds):0.#}/s over the ~{d.ActiveSeconds:0}s it was in use"
                    : "");
            list.Items.Add(BreakdownRows.Row(this, d.Name, value, metric(d) / topMetric, barBrush, tooltip));
        }
    }

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

    private void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows,
        Func<string, Brush>? valueBrush = null)
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
                Text = name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(0, 1, 8, 1),
            };
            var right = new TextBlock
            {
                Text = value, FontSize = 12,
                Foreground = valueBrush?.Invoke(value) ?? (Brush)FindResource("DimBrush"),
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            list.Items.Add(grid);
        }
    }

    // ---- global hotkeys + click-through (INPUT-*) ----

    private System.Windows.Interop.HwndSource? _hwndSource;
    private bool _clickThrough;
    private const int WmHotkey = 0x0312;

    private static class Native
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        public const int GwlExstyle = -20;
        public const int WsExTransparent = 0x20;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this)!;
        _hwndSource.AddHook(WndProc);
        RegisterHotkey(1, _settings.HotkeyToggleOverlay);
        RegisterHotkey(2, _settings.HotkeyClickThrough);
        RegisterHotkey(3, _settings.HotkeyMiniMode);
        RegisterHotkey(4, _settings.HotkeyCampMarker);
    }

    private void RegisterHotkey(int id, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec) || _hwndSource is null) return;
        uint mods = 0, vk = 0;
        foreach (var part in spec.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": mods |= 0x2; break;
                case "SHIFT": mods |= 0x4; break;
                case "ALT": mods |= 0x1; break;
                case "WIN": mods |= 0x8; break;
                default:
                    if (Enum.TryParse<System.Windows.Input.Key>(part, ignoreCase: true, out var key))
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        if (vk == 0 || !Native.RegisterHotKey(_hwndSource.Handle, id, mods, vk))
            App.LogError($"Hotkey '{spec}' could not be registered (invalid or already in use).");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;
        handled = true;
        switch (wParam.ToInt32())
        {
            case 1: // show/hide overlay
                if (Visibility == Visibility.Visible) Hide(); else { Show(); Topmost = true; }
                break;
            case 2:
                ToggleClickThrough();
                break;
            case 3:
                SetMode(!_settings.Minimized);
                break;
            case 4:
                DropCampMarker();
                break;
        }
        return IntPtr.Zero;
    }

    private void ToggleClickThrough()
    {
        if (_hwndSource is null) return;
        _clickThrough = !_clickThrough;
        var style = Native.GetWindowLong(_hwndSource.Handle, Native.GwlExstyle);
        Native.SetWindowLong(_hwndSource.Handle, Native.GwlExstyle,
            _clickThrough ? style | Native.WsExTransparent : style & ~Native.WsExTransparent);
        // Visible but unobtrusive state indicator (INPUT-012).
        RootBorder().BorderBrush = (Brush)FindResource(_clickThrough ? "WarnBrush" : "BorderBrush");
        RootBorder().ToolTip = _clickThrough
            ? $"Click-through ON — press {_settings.HotkeyClickThrough} to interact again"
            : null;
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

    private void OnReset(object sender, RoutedEventArgs e) => _stats.Reset();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource is not null)
            for (var id = 1; id <= 4; id++) Native.UnregisterHotKey(_hwndSource.Handle, id);
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
        _archiver.FinalizeActiveSync(_stats.Snapshot(), "ApplicationExit");
        _watcher.Dispose();
        _repo.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
