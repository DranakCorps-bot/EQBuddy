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

    private static readonly string[] MiniStatOrder = ["kills", "dps", "hps", "loot", "money", "xp", "deaths"];

    private enum StatSort { Total, Hits, Avg, Rate }
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
        _watcher = new LogWatcher(_stats);
        _watcher.Mez = _mezTracker;
        _watcher.PartyDps = _partyDpsTracker;
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

        if (Environment.GetEnvironmentVariable("EQBUDDY_CCLOG") == "1")
            StartCrowdControlCapture();

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
    private readonly PartyDpsTracker _partyDpsTracker = new();
    private PartyDpsWindow? _partyDpsWindow;

    private readonly EqlWikiItemService _wikiItems =
        new(System.IO.Path.Combine(Core.AppPaths.Dir, "wiki-cache", "items"));
    private ItemInfoWindow? _itemWindow;

    /// <summary>Loot rows and the search box route here: one shared popup, re-driven
    /// per lookup.</summary>
    public void ShowItemInfo(string itemName)
    {
        if (_itemWindow is not { IsLoaded: true })
            _itemWindow = new ItemInfoWindow(_wikiItems) { Owner = this };
        _itemWindow.Show();
        _itemWindow.Activate();
        _itemWindow.Lookup(itemName);
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
        // Spawn timers crossing zero: banner always, sound only if one is chosen. Runs
        // off the shared tick so a hidden window can't silence a camp.
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
            var hasTimers = _spawnsVm.HasActiveTimers(DateTime.Now);
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
        if (_mezTracker.Snapshot(DateTime.Now).Count > 0)
        {
            if (_mezWindow is not { IsLoaded: true })
            {
                _mezWindow = new MezChipsWindow(_settings, MezChips);
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
            FillList(LootList, s.Loot.Select(l => (l.Item, $"×{l.Count}")), onNameClick: ShowItemInfo);
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

    // Both keyed by TrackedRule.Id — a display name can be shared by two rules, and keying
    // on it made same-named rules share baselines and cooldowns.
    private readonly Dictionary<string, int> _ruleBaseline = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _ruleLastAlert = new(StringComparer.Ordinal);
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
        var last = _ruleLastAlert.TryGetValue(rule.Id, out var la) ? la : DateTime.MinValue;
        if (DateTime.Now - last < cooldown) return;
        _ruleLastAlert[rule.Id] = DateTime.Now;

        if (rule.AlertBanner) AlertTile.ShowAlert($"★ {ruleName}: {label}");
        if (EQBuddy.UI.Shared.AlertSoundCatalog.Resolve(rule, _settings.AlertSound) is { } sound)
            PlayAlertSound(sound);
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

            AlertOrCue(rule, r.Name,
                $"{r.LastItem ?? "match"}{(delta > 1 ? $" ×{delta}" : "")}",
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
    /// .wav/.mp3 path. Unknown/missing values fall back to the system Asterisk.</summary>
    internal void PlayAlertSound(string choiceOrPath)
    {
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

    private void OnPartyDps(object sender, RoutedEventArgs e)
    {
        if (_partyDpsWindow is { IsLoaded: true })
        {
            _partyDpsWindow.Activate();
            return;
        }
        _partyDpsWindow = new PartyDpsWindow(this, _partyDpsTracker);
        _partyDpsWindow.Show();
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
        ResizeGrip.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        _settings.Save();
        if (mini) UpdateMiniChips(_stats.Snapshot());
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
        Func<string, Brush>? valueBrush = null, Action<string>? onNameClick = null)
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
            if (onNameClick is not null)
            {
                var clickName = name;
                left.Cursor = System.Windows.Input.Cursors.Hand;
                left.ToolTip = "Click for item info (eqlwiki)";
                left.MouseLeftButtonUp += (_, _) => onNameClick(clickName);
            }
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Point { public int X, Y; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);
    }

    /// <summary>
    /// Someone launched a second EQBuddy. Surface this one instead — which is almost
    /// certainly what they wanted, since the usual reason to relaunch is that the widget
    /// is hidden by the hotkey or buried behind a fullscreen game.
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
