using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The WORLD theme's window (docs/Themes.md theme 6; FABLE.md World plan PR 2) — Map,
/// Camps, Path and Travels become one window with four tabs, replacing three standalone
/// windows (<c>MapWindow</c>, <c>SpawnsWindow</c>, <c>TravelWindow</c> — deleted this PR)
/// plus the buried "Drop camp marker" cog entry.
///
/// Each surface is this window's OWN instance, built through <c>MainWindow</c>'s World
/// PR 1 factories — never borrowed from anywhere else (trap 45: a UIElement has one
/// parent). <see cref="CreatureWindow"/> is the template.
///
/// <c>ZoneShareWindow</c> is untouched — it still opens from inside <see cref="MapView"/>
/// on the Map tab, exactly as before this window existed (Bevel-signed; it is not ported
/// to the phone either).
///
/// This window still tracks its own selected tab directly (World PR 2) rather than
/// through a <c>ThemeHost&lt;WorldTab&gt;</c> — the same shape every theme window took
/// before its card existed. World PR 3 wired <c>MainWindow</c>'s own <c>_worldHost</c> to
/// this window's <see cref="TabChanged"/> event and <see cref="SetTab(WorldTab)"/>,
/// exactly as it does for the other three theme windows.
/// </summary>
public partial class WorldWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;

    private WorldTab _tab = WorldSurface.DefaultInlineTab;

    private readonly EqSegmentedStrip _tabs;
    private readonly MapView _map;
    private readonly SpawnsView _spawns;
    private readonly TravelView _travel;
    private readonly TravelsView _travels;

    /// <summary>The mini-dashboard star the old Travels &amp; Deaths card header carried
    /// (trap 20/26 — the ONLY writer <c>MiniStats</c> has for "deaths"). Lives beside the
    /// Travels tab specifically, per the plan, rather than on every tab like the drop
    /// marker: it stars the ROOM it sits next to, the same way Kills' star sits beside
    /// only the Kills room in <see cref="CreatureWindow"/>.</summary>
    private System.Windows.Controls.Primitives.ToggleButton _deathsStar = null!;
    private System.Windows.Controls.TextBlock _deathsStarLabel = null!;

    /// <paramref name="initialZone"/>: the zone whose kill popped a spawn window before
    /// this fold — carried through so <c>EQBUDDY_SPAWNS=&lt;zone&gt;</c> still opens on it.
    public WorldWindow(MainWindow main, string? initialZone = null)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        // "world", not "map"/"spawns"/"travel" — its own size key (FABLE.md PR 2). The
        // three orphaned entries are left inert rather than migrated (the
        // BreakoutKind.Progress ruling: a migration would be code to delete a harmless
        // token).
        WindowZoom.Attach(this, "world", _settings, baseWidth: Width);
        WindowZoom.AllowResize(this, "world", _settings);
        SizeChanged += (_, _) => UpdateHeightCap();

        _map = main.NewMapView();
        _spawns = main.NewSpawnsView(initialZone);
        // The Spawns view carries its own title row and close button — leftover chrome
        // from when it was a borderless standalone window. Redundant now that this
        // window supplies both; hidden rather than removed so a future standalone use
        // (there is none planned) would only need to un-hide it.
        _spawns.HideOwnTitleBar();
        _travel = main.NewTravelView();
        _travels = main.NewTravelsView();

        _tabs = new EqSegmentedStrip(TabStrip);
        BuildStaticChrome();
        BuildActionRow();

        var restored = ScreenGuard.OnScreen(_settings.WorldLeft, _settings.WorldTop, Width, 200);
        if (restored) { Left = _settings.WorldLeft; Top = _settings.WorldTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        UpdateHeightCap();
        SourceInitialized += (_, _) => UpdateHeightCap();
        LocationChanged += (_, _) => UpdateHeightCap();
        Closed += (_, _) =>
        {
            _spawns.StopTicking();
            (_settings.WorldLeft, _settings.WorldTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.WorldLeft, _settings.WorldTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Location", "AccentBrush", size: 15));
        var title = DesignSystem.Text(Role.TitleWindow, "World");
        title.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        title.Ink("AccentBrush");
        TitleRow.Children.Add(title);
        CloseBtn.Content = DesignSystem.Icon("Close");
    }

    /// <summary>
    /// The "Drop camp marker" action — a cog menu entry before this PR (Helm-signed World
    /// pre-design amendment, question 6: "window chrome on every tab plus inline Full
    /// Travels; cog retires in that same PR"). Chrome, not a tab body, so it is visible
    /// whichever of the four tabs is open — the capability must not lose its home for
    /// even one release.
    /// </summary>
    private void BuildActionRow()
    {
        var button = DesignSystem.IconButton("Location",
            "Drop a marker at your current zone — see it on the Travels tab and on your phone's map",
            (_, _) => { _main.DropCampMarker(); Refresh(force: true); }, "AccentBrush");
        ActionRow.Children.Add(button);
        var label = DesignSystem.Text(Role.Caption, "Drop camp marker");
        label.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        ActionRow.Children.Add(label);

        // The Travels tab's own star — visibility toggled in Refresh() rather than being
        // a separate row, so switching tabs costs no new chrome.
        _deathsStar = new System.Windows.Controls.Primitives.ToggleButton
        {
            Style = (Style)FindResource("StarToggle"),
            Tag = "deaths",
            IsChecked = _settings.MiniStats.Contains("deaths"),
            ToolTip = "Show deaths in mini dashboard",
            Margin = new Thickness(Tok.SpaceL, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _deathsStar.Click += (_, _) =>
        {
            if (_deathsStar.IsChecked == true)
            {
                if (!_settings.MiniStats.Contains("deaths")) _settings.MiniStats.Add("deaths");
            }
            else _settings.MiniStats.Remove("deaths");
            _settings.Save();
        };
        ActionRow.Children.Add(_deathsStar);
        _deathsStarLabel = DesignSystem.Text(Role.Caption, "Show in mini dashboard");
        _deathsStarLabel.Margin = new Thickness(Tok.SpaceXs, 0, 0, 0);
        _deathsStarLabel.VerticalAlignment = VerticalAlignment.Center;
        ActionRow.Children.Add(_deathsStarLabel);
    }

    private void UpdateHeightCap()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;
        MaxHeight = Math.Max(220, height * 0.85);
        BodyScroll.MaxHeight = WindowSizing.BodyCap(MaxHeight, 120, FramelessResize.ManualHeight(this));
    }

    /// <summary>Cheap follow tick from <c>MainWindow.RefreshUi</c>: throttled to once a
    /// second, same cadence every other theme window uses.</summary>
    public void MaybeRefresh(bool force = false)
    {
        if (!force && DateTime.Now - _lastRefresh < TimeSpan.FromSeconds(1)) return;
        Refresh(force);
    }

    /// <summary>The snapshot VERSION this window last PAINTED — see
    /// <c>CreatureWindow.RenderedVersion</c>. It answers for the WINDOW; the Map tab's
    /// own view keeps a throttle of its own, so this is not a claim about the map.</summary>
    public long RenderedVersion { get; private set; } = -1;

    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        var s = _main.CurrentSnapshot();
        RenderedVersion = s.Version;
        BuildTabs(s);
        var onTravels = _tab == WorldTab.Travels ? Visibility.Visible : Visibility.Collapsed;
        _deathsStar.Visibility = onTravels;
        _deathsStarLabel.Visibility = onTravels;
        TabBody.Content = _tab switch
        {
            WorldTab.Map => _map.Body,
            WorldTab.Camps => _spawns.Body,
            WorldTab.Routes => _travel.Body,
            _ => _travels.Body,
        };
        // Trap 46: the VISIBLE tab keeps per-tick paint; the others cost nothing this
        // tick. Camps (SpawnsView) is the one exception — it owns its own 1-second
        // DispatcherTimer and always ran regardless of window visibility, the same as it
        // did as a standalone window; pausing it on a tab switch would risk an inaccurate
        // countdown for no measurable saving.
        switch (_tab)
        {
            case WorldTab.Map: _map.MaybeRefresh(force); break;
            case WorldTab.Routes: _travel.Render(); break;
            case WorldTab.Travels: _travels.Render(s); break;
        }
        _ = force;
    }

    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        var deaths = s.Deaths.Count > 0 ? $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}" : null;
        var zone = _main.CurrentZoneName is { Length: > 0 } z ? z : null;
        foreach (var header in WorldSurface.Tabs(map: zone, travels: deaths))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                TabChanged?.Invoke(tab);
                Refresh(force: true);
            });
        }
        _tabs.Select(_tab);
    }

    /// <summary>Raised when the PLAYER switches tabs here — the same shape every other
    /// theme window's event takes, for the card the theme gains in PR 3.</summary>
    internal event Action<WorldTab>? TabChanged;

    internal void SetTab(WorldTab tab)
    {
        _tab = tab;
        Refresh(force: true);
    }

    /// <summary>The currently visible tab — read by <c>MainWindow</c>'s tick to apply the
    /// Bevel-signed chip hide-rule: overlay spawn chips hide only while this window is
    /// visible AND on Camps, never on Map/Path/Travels and never while this window is
    /// closed.</summary>
    internal WorldTab CurrentTab => _tab;

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump. Same keys the
    /// three standalone windows always reported (World PR 1's pinned baseline), plus
    /// <c>worldTab</c>/<c>worldTabs</c> mirroring the other themes' window facts.</summary>
    public string DebugFacts() =>
        $"worldTab={WorldSurface.KeyFor(_tab)} worldTabs={_tabs.Count} " +
        $"{_map.DebugFacts()} {_spawns.DebugFacts()} {_travel.DebugFacts()} {_travels.DebugFacts()}";

    private void OnDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
