using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The WORLD theme's window (docs/Themes.md theme 6; FABLE.md World plan PR 2) — Map,
/// Camps, Path and Travels become one window with four tabs, replacing three standalone
/// windows (<c>MapWindow</c>, <c>SpawnsWindow</c>, <c>TravelWindow</c> — deleted this PR)
/// plus the buried "Drop camp marker" cog entry. <see cref="CreatureWindow"/> is the
/// template.
///
/// Each surface is this window's OWN instance — <see cref="MapView"/> and
/// <see cref="TravelView"/> are built directly against <see cref="IZoneHost"/> (the shape
/// <c>MapWindow</c>/<c>TravelWindow</c> already used, kept rather than "fixed" because
/// <c>ZoneWindowsRenderTests</c> depends on it), and <see cref="SpawnsView"/> /
/// <see cref="TravelsView"/> come from <c>MainWindow</c>'s World PR 1 factories — never
/// borrowed from anywhere else (trap 45: a control has one visual parent on this toolkit).
///
/// <c>ZoneShareWindow</c> is untouched — it still opens from inside <see cref="MapView"/>
/// on the Map tab, exactly as before this window existed (Bevel-signed; it is not ported
/// to the phone either).
///
/// This window still tracks its own selected tab directly rather than through a
/// <c>ThemeHost&lt;WorldTab&gt;</c>; <c>MainWindow</c>'s own <c>_worldHost</c> (World PR 3)
/// is wired to this window's <see cref="TabChanged"/> event and <see cref="SetTab(WorldTab)"/>.
/// </summary>
internal sealed class WorldWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _restored;
    private PixelPoint _placed;

    /// <summary>The last on-screen position, so Closed never persists a torn-down
    /// window's 0,0 (#169).</summary>
    private LastVisiblePosition _seen;

    private WorldTab _tab = WorldSurface.DefaultInlineTab;

    private readonly WrapPanel _tabStrip = new();
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();
    private readonly ScrollViewer _bodyScroll = new();
    private readonly StackPanel _actionRow = new() { Orientation = Orientation.Horizontal };

    private readonly MapView _map;
    private readonly SpawnsView _spawns;
    private readonly TravelView _travel;
    private readonly TravelsView _travels;

    /// <summary>The Travels tab's own star (trap 20/26 — see <c>MainWindow.BuildDeathsStar</c>).
    /// Visibility toggles with the tab rather than living in a separate row.</summary>
    private Button _deathsStar = null!;
    private TextBlock _deathsStarLabel = null!;

    /// <paramref name="initialZone"/>: the zone whose kill popped a spawn window before
    /// this fold — carried through so <c>EQBUDDY_SPAWNS=&lt;zone&gt;</c> still opens on it.
    public WorldWindow(MainWindow main, string? initialZone = null)
    {
        _main = main;
        _settings = main.Settings;
        Title = "EQBuddy World";
        Width = 640;
        MinHeight = 220;
        Height = 500;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        // Not resizable yet — same as every other Avalonia theme window (issue #50);
        // WPF's twin got AllowResize because that lane already has the mechanism.
        CanResize = false;

        _map = new MapView(main);
        _spawns = main.NewSpawnsView(initialZone);
        // The Spawns view carries its own title row and close button — leftover chrome
        // from when it was a borderless standalone window. Redundant now that this
        // window supplies both.
        _spawns.HideOwnTitleBar();
        _travel = new TravelView(main);
        _travels = main.NewTravelsView();

        _tabs = new EqSegmentedStrip(_tabStrip);
        Content = BuildContent();
        WindowZoom.Attach(this, "world", _settings);

        PointerPressed += OnDrag;
        Opened += (_, _) =>
        {
            UpdateHeightLimit();
            _restored = ScreenGuard.OnScreen(this, _settings.WorldLeft, _settings.WorldTop, Width, 200);
            if (_restored)
                Position = new PixelPoint((int)_settings.WorldLeft, (int)_settings.WorldTop);
            else if (Screens.Primary is { } screen)
                Position = new PixelPoint(
                    screen.WorkingArea.X
                        + (screen.WorkingArea.Width - (int)(Width * screen.Scaling)) / 2,
                    screen.WorkingArea.Y + 80);
            _placed = Position;
        };
        PositionChanged += (_, _) =>
        {
            UpdateHeightLimit();
            _seen.Observe(Position.X, Position.Y, IsVisible);
        };
        Closed += (_, _) =>
        {
            _spawns.StopTicking();
            var (curX, curY) = _seen.Or(_settings.WorldLeft, _settings.WorldTop);
            (_settings.WorldLeft, _settings.WorldTop) = WindowPlacement.PositionToPersist(
                _restored, _placed.X, _placed.Y, curX, curY,
                _settings.WorldLeft, _settings.WorldTop);
            _settings.Save();
        };
        Refresh();
    }

    /// <summary>Which tab is showing — read by <c>MainWindow</c>'s tick to apply the
    /// Bevel-signed chip hide-rule: overlay spawn chips hide only while this window is
    /// visible AND on Camps, never on Map/Path/Travels and never while this window is
    /// closed.</summary>
    public WorldTab CurrentTab => _tab;

    private Control BuildContent()
    {
        var title = new Grid { Margin = new Thickness(DesignTokens.SpaceM) };
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleRow.Children.Add(DesignSystem.Icon("Location", "AccentBrush", 15));
        var titleText = DesignSystem.Text(DesignTokens.TypeRole.TitleWindow, "World");
        titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        titleText.Foreground = AppTheme.AccentBrush;
        titleRow.Children.Add(titleText);
        title.Children.Add(titleRow);
        var close = AppTheme.IconButton(AppIcon.Close, "Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        title.Children.Add(close);

        _tabStrip.Margin = new Thickness(DesignTokens.SpaceM, 0, DesignTokens.SpaceM, 0);
        _bodyScroll.Content = _body;
        _bodyScroll.Margin = new Thickness(DesignTokens.SpaceM);

        // The Drop-marker action (Helm-signed World pre-design amendment, question 6):
        // window chrome on every tab, so it survives the cog entry's retirement without
        // losing its home for even one release.
        _actionRow.Margin = new Thickness(DesignTokens.SpaceM);
        _actionRow.VerticalAlignment = VerticalAlignment.Center;
        var marker = DesignSystem.IconButton("Location",
            "Drop a marker at your current zone — see it on the Travels tab and on your phone's map",
            () => { _main.DropCampMarker(); Refresh(); }, "AccentBrush");
        _actionRow.Children.Add(marker);
        var markerLabel = DesignSystem.Text(DesignTokens.TypeRole.Caption, "Drop camp marker");
        markerLabel.Foreground = AppTheme.DimBrush;
        markerLabel.VerticalAlignment = VerticalAlignment.Center;
        markerLabel.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        _actionRow.Children.Add(markerLabel);

        // The Travels tab's own star — visibility toggled in Refresh() based on _tab.
        _deathsStar = _main.BuildDeathsStar();
        _deathsStar.Margin = new Thickness(DesignTokens.SpaceL, 0, 0, 0);
        _actionRow.Children.Add(_deathsStar);
        _deathsStarLabel = DesignSystem.Text(DesignTokens.TypeRole.Caption, "Show in mini dashboard");
        _deathsStarLabel.Foreground = AppTheme.DimBrush;
        _deathsStarLabel.VerticalAlignment = VerticalAlignment.Center;
        _deathsStarLabel.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        _actionRow.Children.Add(_deathsStarLabel);

        var stack = new StackPanel();
        stack.Children.Add(title);
        stack.Children.Add(_tabStrip);
        stack.Children.Add(_bodyScroll);
        stack.Children.Add(_actionRow);
        return new Border
        {
            Background = AppTheme.BgBrush,
            CornerRadius = new CornerRadius(DesignTokens.RadiusPanel),
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    private void UpdateHeightLimit()
    {
        var work = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var height = work is null ? 900 : work.WorkingArea.Height / work.Scaling;
        MaxHeight = Math.Max(220, height * 0.85);
        _bodyScroll.MaxHeight = WindowSizing.BodyCap(MaxHeight, 160, taken: null);
    }

    /// <summary>Open on a tab by its wire key. An unknown key is left alone rather than
    /// snapped to Travels; silently showing the wrong tab is worse than showing the one
    /// already open.</summary>
    public void SetTab(string key)
    {
        if (WorldSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Refresh();
    }

    public void SetTab(WorldTab tab) => SetTab(WorldSurface.KeyFor(tab));

    public void MaybeRefresh(bool force = false)
    {
        if (force || (DateTime.Now - _lastRefresh).TotalSeconds >= 1) Refresh();
    }

    public void Refresh()
    {
        _lastRefresh = DateTime.Now;
        var s = _main.CurrentSnapshot();
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
                Refresh();
            });
        }
        _tabs.Select(_tab);
        var onTravels = _tab == WorldTab.Travels;
        _deathsStar.IsVisible = onTravels;
        _deathsStarLabel.IsVisible = onTravels;
        _body.Content = _tab switch
        {
            WorldTab.Map => _map.Body,
            WorldTab.Camps => _spawns.Body,
            WorldTab.Routes => _travel.Body,
            _ => _travels.Body,
        };
        // Trap 46: the VISIBLE tab keeps per-tick paint; the others cost nothing this
        // tick. Camps (SpawnsView) is the one exception — it owns its own 1-second
        // DispatcherTimer and always ran regardless of window visibility, same as it did
        // as a standalone window.
        switch (_tab)
        {
            case WorldTab.Map: _map.MaybeRefresh(); break;
            case WorldTab.Routes: _travel.Render(); break;
            case WorldTab.Travels: _travels.Render(s); break;
        }
    }

    /// <summary>Raised when the PLAYER switches tabs here — same shape every other theme
    /// window's event takes, for the card the theme gains in PR 3.</summary>
    public event Action<WorldTab>? TabChanged;

    /// <summary>The window's facts for the debug dump, matching the WPF twin's shape.</summary>
    public string DebugFacts() =>
        $"worldTab={WorldSurface.KeyFor(_tab)} worldTabs={_tabs.Count} " +
        $"{_map.DebugFacts()} {_spawns.DebugFacts()} {_travel.DebugFacts()} {_travels.DebugFacts()}";

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
