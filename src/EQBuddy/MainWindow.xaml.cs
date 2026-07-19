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
    private readonly DispatcherTimer _uiTimer;
    private DateTime _lastCharScan = DateTime.MinValue;
    private DateTime _lastJanitorRun = DateTime.MinValue;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private DateTime _upToDateNoticeUntil = DateTime.MinValue;
    private bool _installingUpdate;

    private static readonly string[] MiniStatOrder = ["kills", "dps", "loot", "money", "xp", "deaths"];

    public MainWindow()
    {
        InitializeComponent();
        _watcher = new LogWatcher(_stats);

        MaxHeight = SystemParameters.WorkArea.Height - 20;
        SectionScroll.MaxHeight = SystemParameters.WorkArea.Height - 160;

        if (_settings.LogFolder is { } saved && !System.IO.Directory.Exists(saved))
            _settings.LogFolder = null; // stale saved path (game moved) — re-detect
        _settings.LogFolder ??= LogWatcher.FindDefaultLogFolder();
        if (!double.IsNaN(_settings.WindowLeft)) { Left = _settings.WindowLeft; Top = _settings.WindowTop; }
        else { Left = SystemParameters.WorkArea.Right - 360; Top = 40; }
        Opacity = _settings.Opacity;
        Topmost = _settings.AlwaysOnTop;
        PinBtn.IsChecked = _settings.AlwaysOnTop;
        ApplyUiScale(_settings.UiScale);
        ApplyBackgroundOpacity(_settings.BackgroundOpacity);

        VersionMenuItem.Header = $"EQBuddy v{UpdateChecker.CurrentVersion}";

        foreach (var (key, star) in StarButtons())
            star.IsChecked = _settings.MiniStats.Contains(key);
        SetMode(_settings.Minimized);

        FollowActiveCharacter();

        // Log hygiene at startup: force Log=1 and wipe finished-session logs
        // (both no-ops while the game is running).
        if (_settings.LogFolder is { } lf)
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(lf);
                EqConfig.TruncateStaleLogs(lf, SessionStats.SessionGap);
            });

        if (Environment.GetEnvironmentVariable("EQBUDDY_EXPAND") == "1")
            foreach (var ex in new[] { CombatSection, KillsSection, LootSection, MoneySection,
                         ProgressSection, FactionSection, MiscSection })
                ex.IsExpanded = true;

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
        _optionsWindow.Show();
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
            _watcher.Select(active.FilePath);
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
            Task.Run(() =>
            {
                EqConfig.EnsureLoggingEnabled(folder);
                EqConfig.TruncateStaleLogs(folder, SessionStats.SessionGap);
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

        var s = _stats.Snapshot();

        if (MiniRoot.Visibility == Visibility.Visible)
            UpdateMiniChips(s);

        ZoneText.Text = s.CurrentZone.Length > 0 ? s.CurrentZone : "—";
        SessionText.Text = s.SessionStart is { } start
            ? $"session {(int)s.Elapsed.TotalHours}:{s.Elapsed.Minutes:D2} (since {start:h:mm tt})"
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
            var combatTime = TimeSpan.FromSeconds(s.CombatSeconds);
            CombatSummary.Text =
                $"Dealt {s.DamageDealt:N0} ({s.MeleeDamage:N0} melee / {s.SpellDamage:N0} spell) · " +
                $"{s.CritCount} crits · {acc:0}% accuracy\n" +
                $"In combat {(int)combatTime.TotalMinutes}m {combatTime.Seconds}s this session\n" +
                $"Biggest hit: {s.MaxHit:N0} ({s.MaxHitDesc})\n" +
                $"Taken {s.DamageTaken:N0} · avoided {s.AvoidedIncoming} attacks\n" +
                $"Healing done {s.HealingDone:N0} · received {s.HealingReceived:N0}" +
                (s.SpecialHits.Count > 0
                    ? "\n" + string.Join(" · ", s.SpecialHits.Select(x => $"{x.Name} {x.Count}"))
                    : "") +
                (s.Fizzles + s.Resists > 0 ? $"\nFizzles {s.Fizzles} · resists {s.Resists}" : "");
            FillList(DamageSourceList, s.DamageBySource.Select(d =>
                (d.Name, $"{d.Total:N0} · {d.Hits} hit{(d.Hits == 1 ? "" : "s")} · avg {(double)d.Total / d.Hits:0.#}")));
            FillList(DamageTakenList, s.DamageByAttacker.Select(d =>
                (d.Name, $"{d.Total:N0} · {d.Hits} hit{(d.Hits == 1 ? "" : "s")} · avg {(double)d.Total / d.Hits:0.#}")));
            HealersLabel.Visibility = s.HealsByHealer.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(HealerList, s.HealsByHealer.Select(h =>
                (h.Name, $"{h.Total:N0} · {h.Hits} heal{(h.Hits == 1 ? "" : "s")}")));
        }

        if (KillsSection.IsExpanded)
        {
            KillsSummary.Text = $"{s.KillsPerHour:0.0} kills/hr";
            FillList(KillList, s.YourKills.Select(k => (k.Name, $"×{k.Count}")));
            var showParty = s.PartyKillsByKiller.Count > 0;
            PartyKillsLabel.Visibility = showParty ? Visibility.Visible : Visibility.Collapsed;
            FillList(PartyKillList, s.PartyKillsByKiller.Select(k => (k.Name, $"×{k.Count}")));
        }

        if (LootSection.IsExpanded)
        {
            FillList(LootList, s.Loot.Select(l => (l.Item, $"×{l.Count}")), max: 20);
            CraftedLabel.Visibility = s.Crafted.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(CraftedList, s.Crafted.Select(c => (c.Name, $"×{c.Count}")));
        }

        if (MoneySection.IsExpanded)
        {
            MoneySummary.Text =
                $"Corpses {StatsSnapshot.FormatCoin(s.CorpseCopper)} ({s.CoinDrops} drops, biggest {StatsSnapshot.FormatCoin(s.BiggestDrop)})\n" +
                $"Merchant sales {StatsSnapshot.FormatCoin(s.VendorCopper)} ({s.SalesCount} sales)\n" +
                $"{StatsSnapshot.FormatCoin(s.CopperPerHour)} per hour";
            SoldLabel.Visibility = s.SoldItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FillList(SoldList, s.SoldItems.Select(i =>
                ($"{i.Item}{(i.Count > 1 ? $" ×{i.Count}" : "")}", StatsSnapshot.FormatCoin(i.Copper))));
        }

        if (ProgressSection.IsExpanded)
        {
            ProgressSummary.Text =
                $"{s.XpTicks} xp gains · {s.XpPerHour:0.0}%/hr · {s.SkillUpTotal} skill-ups" +
                (s.AaGained > 0
                    ? $"\n{s.AaGained} AA point{(s.AaGained == 1 ? "" : "s")} · {s.AaPerHour:0.0} AA/hr (now {s.AaTotal} unspent)"
                    : "") +
                (s.HoursToLevel is { } eta ? $"\nNext level in {FormatEta(eta)} at this pace" : "") +
                (s.Levels.Count > 0
                    ? "\n" + string.Join(", ", s.Levels.Select(l => $"{l.Text} at {l.Time:h:mm tt}"))
                    : "");
            FillList(SkillList, s.SkillUps.Select(k => (k.Skill, $"{k.Value} (+{k.Ups})")));
        }

        if (FactionSection.IsExpanded)
            FillList(FactionList, s.Faction.Select(f =>
                (f.Faction, $"{(f.Net >= 0 ? "+" : "")}{f.Net}")), max: 15,
                valueBrush: f => f.StartsWith('-') ? (Brush)FindResource("BadBrush") : (Brush)FindResource("GoodBrush"));

        if (MiscSection.IsExpanded)
        {
            FillList(DeathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
            FillList(ZoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
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
                System.IO.File.WriteAllText(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EQBuddy", "debug.txt"), dump);
            }
            catch { }
        }
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

    private void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows,
        int max = 12, Func<string, Brush>? valueBrush = null)
    {
        var items = rows.ToList();
        list.Items.Clear();
        foreach (var (name, value) in items.Take(max))
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
        if (items.Count > max)
            list.Items.Add(new TextBlock
            {
                Text = $"…and {items.Count - max} more", FontSize = 11,
                Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(0, 2, 0, 0),
            });
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

    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        Topmost = PinBtn.IsChecked == true;
        _settings.AlwaysOnTop = Topmost;
        _settings.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e) => _stats.Reset();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
        _watcher.Dispose();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
