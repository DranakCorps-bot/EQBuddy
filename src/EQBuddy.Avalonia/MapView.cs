using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Path = System.IO.Path;

namespace EQBuddy.Avalonia;

/// <summary>
/// The zone map (competitive gap #1, 2026-08-10): classic-format map files —
/// Brewall packs, the game's own /map output — drawn with your last /loc as the
/// player marker. Log-only and honest about it: the marker moves when YOU type
/// /loc, and the window says how old the position is rather than pretending to
/// live-track. Follows the zone the log last saw; pick any map from the dropdown
/// to plan ahead. Wheel zooms around the cursor, drag pans, double-click refits.
///
/// Lifted out of <c>MapWindow</c> for World PR 1 (docs/Themes.md theme 6): the
/// content, not the chrome — <c>MapWindow</c> becomes a thin host. Owner-window
/// lookups (dialogs, clipboard) resolve via <see cref="TopLevel.GetTopLevel"/>
/// instead of a captured Window reference, since this class is no longer one —
/// it works identically once the body is parented, which happens before a player
/// can ever click one of them.
/// </summary>
internal sealed class MapView
{
    /// <summary>Brewall's EverQuest Maps — the canonical home (brewall.com is the
    /// old domain). Linked, never bundled: the pack states no redistribution terms,
    /// so we send players to the source and the credit stays with the cartographer.</summary>
    internal const string MapPackUrl = "https://www.eqmaps.info/eq-map-files/";

    private readonly IZoneHost _host;
    // WPF's transformed Canvas splits into a geometry surface (lines + trail, drawn
    // under the view matrix in Render) and an overlay Canvas for screen-space
    // elements (marker, POIs, circles, pins) repositioned on every view change.
    private readonly Panel _mapHost = new() { ClipToBounds = true, Background = Brushes.Transparent };
    private readonly MapSurface _mapLayer = new();
    private readonly Canvas _overlay = new();
    private readonly Ellipse _marker = new()
    {
        Width = 10, Height = 10, StrokeThickness = 2.5, IsVisible = false,
        Stroke = AppTheme.WarnBrush,
        Fill = new SolidColorBrush(Color.FromArgb(120, 255, 200, 60)),
    };
    private readonly TextBlock _status = new()
    {
        FontSize = 11, Margin = new Thickness(8, 4, 8, 6),
        TextWrapping = TextWrapping.Wrap, Foreground = AppTheme.DimBrush,
    };
    private readonly ComboBox _zonePick = new() { FontSize = 11, MinWidth = 170 };
    private readonly DockPanel _root;
    private Matrix _view = Matrix.Identity;
    private ZoneMap? _map;
    private string _shownFile = "";
    private string _followedZone = "";
    private bool _userPicked;
    private Point _dragStart;
    private bool _dragging;
    private readonly StackPanel _namedPanel = new() { Margin = new Thickness(8, 4, 8, 4) };
    private readonly List<(Control El, double X, double Y, double Dx, double Dy)> _campPins = [];
    private (int Count, long Bucket) _trailStamp = (-1, 0);
    private DateTime _lastRefresh = DateTime.MinValue;

    // ---- Spawn-point circles (David's map brief, 2026-08-13) ----------------
    // Every archived spawn point in the shown zone, drawn as a circle: named
    // points wear the theme ACCENT, ordinary points sit dim — visibly of the UI,
    // never of the map pack. Theme-keyed via AppTheme's live brushes throughout
    // (David: "colors should align with our UI Theme", not hard-coded). Hover
    // answers "what lives here": every mob seen at the point with kill counts,
    // the last kill, and the projected respawn. Circles PULSE when a respawn is
    // imminent — within PulseWindowSeconds of the named timer or the ordinary
    // projection.
    private readonly List<(Control El, double X, double Y, double Dx, double Dy)> _spawnCircles = [];
    private readonly List<SpawnCircle> _circleMeta = [];
    private (string Zone, int Revision, int TimerHash) _circleStamp = ("\0", -1, 0);

    /// <summary>"Imminent" = due within this many seconds (David, 2026-08-13).</summary>
    internal const double PulseWindowSeconds = 10;
    /// <summary>Keep pulsing this long past due — the pop is happening about now —
    /// then settle; the named panel's DUE state carries on from there.</summary>
    internal const double PulseLingerSeconds = 30;

    private sealed class SpawnCircle
    {
        public required Ellipse Ring;
        public required Ellipse Halo;
        /// <summary>The halo's built opacity, restored when a pulse stops.</summary>
        public required double HaloOpacity;
        public required SpawnPointLedger.SpawnPoint Point;
        /// <summary>Canonical catalog name when this is a named point — the SAME
        /// basis the label suppression uses, so a named killed under an alias still
        /// finds its timer and pulses (review 2026-08-13).</summary>
        public required string? NamedName;
        /// <summary>Non-null while the imminence beat runs; cancelling it stops it.</summary>
        public CancellationTokenSource? Pulse;
    }

    /// <summary>Timers live under the CATALOG zone ("Befallen"); the log names the
    /// instance ("Befallen 4 (Refined)"). One resolver for the panel, the circles,
    /// and the share window — they must never disagree about which zone "here" is.</summary>
    private string ResolvedTimerZone() =>
        _host.SpawnTimers.CurrentZone?.Zone
            ?? SpawnCatalog.StripTierVariant(_host.CurrentZoneName);

    /// <summary>How often the trail re-renders just because time passed. Every
    /// shared tick: with a one-minute horizon the fade must read as continuous,
    /// and a rebuild is ~80 small brushes — nothing.</summary>
    private static readonly TimeSpan FadeTick = TimeSpan.FromSeconds(1);

    public MapView(IZoneHost host)
    {
        _host = host;

        _mapHost.Children.Add(_mapLayer);
        _mapHost.Children.Add(_overlay);
        _overlay.Children.Add(_marker);

        var bar = new DockPanel { Margin = new Thickness(8, 6, 8, 0) };
        var zoneLabel = new TextBlock
        {
            Text = "Map: ", FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = AppTheme.TextBrush,
        };
        var follow = ZoneTheming.Button("Follow me");
        follow.Margin = new Thickness(6, 0, 0, 0);
        ToolTip.SetTip(follow,
            "Snap back to the zone you're actually in, and keep following as you zone.\n" +
            "Picking a map from the dropdown pauses following to let you plan ahead —\n" +
            "your marker, trail, and camp pins hide until you're back on your own map.");
        follow.Click += (_, _) => { _userPicked = false; MaybeRefresh(force: true); };
        var chooseFolder = ZoneTheming.Button("Maps folder…");
        chooseFolder.Margin = new Thickness(6, 0, 0, 0);
        chooseFolder.Click += (_, _) => ChooseFolder();
        // Map packs aren't ours to bundle (Brewall's states no redistribution
        // terms, so none are granted) — but the download is one click away and
        // the credit stays where it belongs. Same posture we ask of others.
        var getMaps = ZoneTheming.Button("Get maps…");
        getMaps.Margin = new Thickness(6, 0, 0, 0);
        ToolTip.SetTip(getMaps,
            "Opens Brewall's EverQuest Maps (eqmaps.info) in your browser.\n" +
            "Download the map pack zip and extract the .txt files into the game's \"maps\"\n" +
            "folder (next to Logs) — EQBuddy picks them up from there. Maps by Brewall.");
        getMaps.Click += (_, _) => OpenUrl(MapPackUrl);
        DockPanel.SetDock(zoneLabel, Dock.Left);
        DockPanel.SetDock(chooseFolder, Dock.Right);
        DockPanel.SetDock(getMaps, Dock.Right);
        DockPanel.SetDock(follow, Dock.Right);
        bar.Children.Add(zoneLabel);
        bar.Children.Add(chooseFolder);
        bar.Children.Add(getMaps);
        bar.Children.Add(follow);
        bar.Children.Add(_zonePick);
        _zonePick.SelectionChanged += (_, _) =>
        {
            if (_zonePick.SelectedItem is string stem && MapFolder is { } dir)
            {
                var file = Path.Combine(dir, stem + ".txt");
                if (file != _shownFile) { _userPicked = true; ShowFile(file); }
            }
        };

        // The named side panel — "ShowEQ Lite," minus everything bannable (David,
        // 2026-08-10): current-zone named with their respawn countdowns, camps
        // pinned from YOUR /loc at kill time or the wiki's location field. All of
        // it from the log and public pages; nothing reads or touches the game.
        var scroll = new ScrollViewer
        {
            Content = _namedPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        // Zone knowledge sharing lives with the map because the map is where the
        // knowledge shows (the spawn circles). Docked OUTSIDE the rebuilt panel so
        // the button never churns.
        var share = ZoneTheming.Button("Share zone knowledge…");
        share.FontSize = 10.5;
        share.Margin = new Thickness(8, 4, 8, 8);
        ToolTip.SetTip(share,
            "Export this zone's spawn points and learned timers as one paste-safe string,\n" +
            "import a friend's (with a full preview first — big timer deviations arrive flagged),\n" +
            "or submit yours to EQBuddy for everyone. Nothing is sent unless you click it.");
        share.Click += async (_, _) =>
        {
            var timerZone = ResolvedTimerZone();
            if (timerZone.Length == 0)
            {
                _status.Text = "Share zone knowledge needs to know the zone — it unlocks once the log sees you zone in.";
                return;
            }
            try
            {
                if (OwnerWindow() is { } owner)
                    await new ZoneShareWindow(_host.SpawnPoints, _host.SpawnCatalogData,
                        _host.SpawnOverridesStore, timerZone).ShowDialog(owner);
            }
            catch (Exception ex) { App.LogError(ex); }
        };
        var side = new DockPanel { Width = 190, Background = AppTheme.PanelBrush };
        DockPanel.SetDock(share, Dock.Bottom);
        side.Children.Add(share);
        side.Children.Add(scroll);

        // The /loc social is the map's whole trick, so its two lines are one click
        // (David, 2026-08-14) — the hover recipe stays the teacher, the button
        // skips the typing. Both lines land newline-separated; the social editor
        // takes one per slot, so the paste happens line by line.
        var copyLoc = AppTheme.IconButton("⧉ copy  /loc social",
            "Copies both social lines, newline-separated:\n" +
            "    Line 1:  " + GameCommands.LocSocialLine1 + "\n" +
            "    Line 2:  " + GameCommands.LocSocialLine2 + "\n" +
            "The game's social editor takes one line per slot — paste line by line.\n" +
            "(Hover the status text for the full trick.)");
        copyLoc.FontSize = 11;
        copyLoc.Margin = new Thickness(0, 4, 8, 6);
        copyLoc.VerticalAlignment = VerticalAlignment.Top;
        copyLoc.Click += async (_, _) =>
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(_mapHost)?.Clipboard;
                await (clipboard?.SetTextAsync(GameCommands.LocSocial) ?? Task.CompletedTask);
                copyLoc.Content = "✓ copied — one line per social slot";
            }
            catch (Exception ex) { App.LogError(ex); }   // clipboard momentarily held by another app
        };
        var statusBar = new DockPanel();
        DockPanel.SetDock(copyLoc, Dock.Right);
        statusBar.Children.Add(copyLoc);
        statusBar.Children.Add(_status);

        _root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        DockPanel.SetDock(side, Dock.Right);
        _root.Children.Add(bar);
        _root.Children.Add(statusBar);
        _root.Children.Add(side);
        _root.Children.Add(_mapHost);

        // Right-click on open map space: zone-level actions. Circles carry their
        // own menus, which take precedence when the click lands on one — Avalonia
        // stops the ContextRequested bubble at the innermost control that owns a
        // menu, so the dot's menu wins with no hit-test bookkeeping of ours.
        var mapMenu = new ContextMenu();
        var reset = new MenuItem { Header = "Reset spawn points for this zone…" };
        reset.Click += (_, _) => OnResetZonePoints();
        mapMenu.Items.Add(reset);
        // Opening, not Opened: Avalonia's Opened fires with the popup already
        // realized (the gear menu leans on exactly that), so a relabel there would
        // measure against the old header. Opening still runs per open, which is the
        // point — the zone name in the item must be the zone you're looking at.
        mapMenu.Opening += (_, _) =>
        {
            var z = ResolvedTimerZone();
            reset.Header = z.Length > 0 ? $"Reset spawn points — {z}…" : "Reset spawn points…";
        };
        _mapHost.ContextMenu = mapMenu;

        _mapHost.PointerWheelChanged += OnWheel;
        _mapHost.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(_mapHost);
            if (!point.Properties.IsLeftButtonPressed) return;
            if (e.ClickCount == 2) { FitToView(); return; }
            _dragging = true;
            _dragStart = e.GetPosition(_mapHost);
            e.Pointer.Capture(_mapHost);
        };
        _mapHost.PointerReleased += (_, e) => { _dragging = false; e.Pointer.Capture(null); };
        _mapHost.PointerMoved += OnDrag;
        _mapHost.SizeChanged += (_, _) => { if (!_dragging) FitToView(); };

        // Like the WPF window, this rides MainWindow's shared RefreshUi tick (which
        // calls MaybeRefresh while visible); the throttle guard there keeps any extra
        // callers — follow button, share import — from stacking refreshes on top.
        PopulateZoneList();
        MaybeRefresh(force: true);
    }

    public Control Body => _root;

    /// <summary>The Window this view is currently hosted in — resolved lazily so a
    /// dialog or clipboard call always targets whichever host actually holds this
    /// body right now, never a reference captured before it was parented.</summary>
    private Window? OwnerWindow() => TopLevel.GetTopLevel(_mapHost) as Window;

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private string? MapFolder =>
        _host.Settings.MapFolder is { Length: > 0 } custom && Directory.Exists(custom)
            ? custom
            : ZoneMapFiles.DefaultFolder(_host.Settings.LogFolder);

    private void PopulateZoneList()
    {
        _zonePick.Items.Clear();
        if (MapFolder is not { } folder) return;
        foreach (var f in Directory.EnumerateFiles(folder, "*.txt")
                     .Select(Path.GetFileNameWithoutExtension)
                     .Where(stem => stem is { Length: > 0 } && !stem.Contains('_'))
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            _zonePick.Items.Add(f!);
    }

    /// <summary>Cheap follow tick: reload only when the zone (or the marker)
    /// actually moved.</summary>
    public void MaybeRefresh(bool force = false)
    {
        // The QuestsWindow (2 s) / DropsWindow (3 s) throttle shape, at 1 s: the host
        // tick drives this once a second, and anything faster is wasted repainting.
        if (!force && (DateTime.Now - _lastRefresh).TotalSeconds < 1) return;
        _lastRefresh = DateTime.Now;
        var zone = _host.CurrentZoneName;
        if (MapFolder is not { } folder)
        {
            _status.Text = "No maps folder found. EQBuddy looks for the game's own \"maps\" folder " +
                "beside Logs — click \"Get maps…\" for Brewall's pack (unzip it there), or point " +
                "me at an existing folder with Maps folder…";
            return;
        }
        if (!_userPicked && zone.Length > 0 && zone != _followedZone)
        {
            _followedZone = zone;
            var file = ZoneMapFiles.Resolve(folder, zone);
            if (file is not null) ShowFile(file);
            else
            {
                _mapLayer.LineBatches.Clear();
                _mapLayer.TrailSegments.Clear();
                _mapLayer.InvalidateVisual();
                _map = null;
                _shownFile = "";
                _status.Text = $"No map file matched \"{zone}\" in {folder} — pick one from the dropdown " +
                    "(and tell the discussions board which file it should have been).";
            }
        }
        else if (force && _shownFile.Length > 0)
        {
            ShowFile(_shownFile);
        }
        UpdateMarker();
        UpdateTrail();
        UpdateSpawnCircles();   // before the pins so pins stay on top
        UpdateNamedPanel();
    }

    /// <summary>Rebuild the circles only when the archive actually changed (zone,
    /// point count, or kill total) — a rebuild every tick would restart the pulse
    /// animations mid-beat. The pulse check itself runs every tick regardless,
    /// because imminence is a property of the clock, not the archive.</summary>
    private void UpdateSpawnCircles()
    {
        var now = DateTime.Now;
        var timerZone = ResolvedTimerZone();
        var showing = _map is not null && !_userPicked && timerZone.Length > 0;
        // ONE timer snapshot per tick, shared by the rebuild check and the pulse —
        // and change detection by the ledger's revision counter, not a per-tick
        // deep clone of the archive (review 2026-08-13).
        var timers = showing
            ? _host.SpawnTimers.Snapshot(now)
                .Where(t => string.Equals(t.Zone, timerZone, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];
        // Running timers matter to the rebuild too: a named circle carries its own
        // name label ONLY while no timer pin is labeling that mob (David caught
        // Trainer/Taskmaster going nameless — named points must read as named even
        // with no countdown running).
        var timerHash = timers.Aggregate(17,
            (h, t) => h * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(t.Name));
        var stamp = showing ? (timerZone, _host.SpawnPoints.Revision, timerHash) : ("", 0, 0);
        if (stamp != _circleStamp)
        {
            _circleStamp = stamp;
            foreach (var c in _circleMeta) c.Pulse?.Cancel();
            foreach (var (el, _, _, _, _) in _spawnCircles) _overlay.Children.Remove(el);
            _spawnCircles.Clear();
            _circleMeta.Clear();
            if (showing)
                foreach (var p in _host.SpawnPoints.Snapshot(timerZone).Points)
                    BuildCircle(timerZone, p, timers);
            PlaceSpawnCircles();
        }
        if (_circleMeta.Count > 0) UpdatePulse(now, timerZone, timers);
    }

    private void BuildCircle(string zone, SpawnPointLedger.SpawnPoint p, List<SpawnTimerState> timers)
    {
        var namedName = _host.SpawnPoints.NamedPointName(zone, p);
        var named = namedName is not null;
        var (mx, my) = ZoneMap.FromLoc(p.LocY, p.LocX);
        var d = named ? 13.0 : 9.0;
        var haloOpacity = named ? 0.30 : 0.16;
        var halo = new Ellipse
        {
            Width = d + 8, Height = d + 8,
            Opacity = haloOpacity, IsHitTestVisible = false,
            Fill = named ? AppTheme.AccentBrush : AppTheme.DimBrush,
        };
        var ring = new Ellipse
        {
            Width = d, Height = d,
            StrokeThickness = named ? 2.2 : 1.5,
            Fill = Brushes.Transparent,   // hit-test the interior for the hover
            Stroke = named ? AppTheme.AccentBrush : AppTheme.DimBrush,
        };
        ToolTip.SetShowDelay(ring, 150);
        ToolTip.SetTip(ring, "");
        // Built fresh at hover — the countdown must read the clock, not the rebuild.
        var meta = new SpawnCircle
        {
            Ring = ring, Halo = halo, HaloOpacity = haloOpacity, Point = p, NamedName = namedName,
        };
        ring.PointerEntered += (_, _) => ToolTip.SetTip(ring, CircleTip(zone, meta));
        ring.ContextMenu = CircleMenu(zone, meta);
        _spawnCircles.Add((halo, mx, my, -(d + 8) / 2, -(d + 8) / 2));
        _spawnCircles.Add((ring, mx, my, -d / 2, -d / 2));
        _overlay.Children.Add(halo);
        _overlay.Children.Add(ring);
        if (p.Confirmed)
        {
            // A confirmed spot wears a small filled center dot — "this one is
            // vouched for", visible without hovering.
            var dot = new Ellipse
            {
                Width = 3.5, Height = 3.5, IsHitTestVisible = false,
                Fill = named ? AppTheme.AccentBrush : AppTheme.DimBrush,
            };
            _spawnCircles.Add((dot, mx, my, -1.75, -1.75));
            _overlay.Children.Add(dot);
        }
        // Named points carry their NAME beside the circle — unless a running timer's
        // camp pin is already labeling that mob with name + countdown right there.
        // (NameMatches is symmetric fold-equality; one direction suffices.)
        if (namedName is not null
            && !timers.Any(t => SpawnCatalog.NameMatches(t.Name, namedName)))
        {
            var label = new TextBlock { Text = namedName, FontSize = 9.5, Foreground = AppTheme.AccentBrush };
            _spawnCircles.Add((label, mx, my, d / 2 + 3, -7));
            _overlay.Children.Add(label);
        }
        _circleMeta.Add(meta);
    }

    /// <summary>Reset (clear) the shown zone's whole spawn-point archive — the map's
    /// empty-space right-click (David, 2026-08-13: "a reset"). Confirmed first with
    /// the real count; the wipe is durable, and future kills rebuild honestly.</summary>
    private async void OnResetZonePoints()
    {
        try
        {
            var zone = ResolvedTimerZone();
            if (zone.Length == 0)
            {
                _status.Text = "Reset needs to know the zone — it unlocks once the log sees you zone in.";
                return;
            }
            var count = _host.SpawnPoints.Snapshot(zone).Points.Count;
            if (count == 0)
            {
                _status.Text = $"Nothing to reset — {zone} has no archived spawn points yet.";
                return;
            }
            if (OwnerWindow() is not { } owner) return;
            var ok = await ConfirmDialog.Ask(owner, "Reset spawn points",
                $"Reset {zone}'s spawn-point archive?\n\n" +
                $"All {count} archived point{(count == 1 ? "" : "s")} — including confirmed ones — " +
                "will be removed. The zone starts learning fresh from your next kills; " +
                "nothing already archived comes back on its own.",
                $"Reset {zone}");
            if (!ok) return;
            var cleared = _host.SpawnPoints.ClearZone(zone);
            _status.Text = $"Reset {zone} — {cleared} spawn point{(cleared == 1 ? "" : "s")} cleared; the zone learns fresh from here.";
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Right-click on a circle: remove the point from the zone's archive
    /// (David, 2026-08-13). Honest about the semantics in the item itself — the
    /// removal survives restarts, but fresh kills near the spot re-learn it,
    /// because the log always outranks an edit.</summary>
    private ContextMenu CircleMenu(string zone, SpawnCircle c)
    {
        var menu = new ContextMenu();
        var what = c.NamedName
            ?? c.Point.Mobs.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).First();
        var header = new MenuItem
        {
            Header = c.Point.Mobs.Count > 1 ? $"{what} +{c.Point.Mobs.Count - 1} more" : what,
            IsEnabled = false, FontSize = 11,
        };
        // Built from the circle's snapshot, which is fine because every edit below
        // bumps the ledger's Revision — the next tick rebuilds the circles, and the
        // menus with them, so the label can't go stale behind the archive.
        var confirm = new MenuItem
        {
            Header = c.Point.Confirmed ? "Un-confirm location" : "Confirm location",
        };
        ToolTip.SetTip(confirm,
            "Marks this spot as verified by you: the dot stops drifting toward\n" +
            "new kills and holds exactly here. Kills and timers keep counting.\n" +
            "Confirmations travel in share strings like everything else you set.");
        confirm.Click += (_, _) =>
        {
            var now = _host.SpawnPoints.ConfirmPoint(zone, c.Point.LocY, c.Point.LocX,
                !c.Point.Confirmed);
            if (now is { } state)
                _status.Text = state
                    ? $"Location confirmed ({what}) — the dot holds this spot from now on."
                    : $"Confirmation removed ({what}) — the dot refines with new kills again.";
        };
        var remove = new MenuItem { Header = "Remove this spawn point" };
        ToolTip.SetTip(remove,
            "Takes the circle off the map and out of this zone's archive.\n" +
            "New kills near this spot will honestly re-learn it — the log always wins.");
        remove.Click += (_, _) =>
        {
            if (_host.SpawnPoints.RemovePoint(zone, c.Point.LocY, c.Point.LocX))
                _status.Text = $"Spawn point removed ({what}) — new kills near that spot will re-learn it.";
        };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());
        menu.Items.Add(confirm);
        menu.Items.Add(remove);
        return menu;
    }

    /// <summary>Named circles match their timer on the CANONICAL name — the same
    /// basis the label suppression uses — so a named killed under a catalog alias
    /// still finds its timer and pulses. Callers pass the tick's shared, already
    /// zone-filtered timer list.</summary>
    private DateTime? CircleDue(string zone, SpawnCircle c, List<SpawnTimerState> timers)
    {
        if (c.NamedName is not { } namedName)
            return _host.SpawnPoints.ProjectedRespawn(zone, c.Point);
        return timers.FirstOrDefault(t => SpawnCatalog.NameMatches(t.Name, namedName))?.DueAt;
    }

    private string CircleTip(string zone, SpawnCircle c)
    {
        var now = DateTime.Now;
        var named = c.NamedName is not null;
        var lines = new List<string>();
        foreach (var kv in c.Point.Mobs.OrderByDescending(kv => kv.Value.Kills)
                     .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            lines.Add($"{kv.Key} ×{kv.Value.Kills}");
        var (lastName, lastSeen) = c.Point.LastKilled();
        lines.Add("");
        if (c.Point.Confirmed) lines.Add("Location confirmed — this dot holds its spot");
        lines.Add($"Last kill: {lastName}, {EQBuddy.UI.Shared.Countdown.Format(now - lastSeen.LastKill)} ago");
        var label = named ? "Respawn" : "Projected respawn (~)";
        // Tooltips open rarely — a fresh timer snapshot here is fine.
        var timers = named
            ? _host.SpawnTimers.Snapshot(now)
                .Where(t => string.Equals(t.Zone, zone, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        if (CircleDue(zone, c, timers) is { } due)
            lines.Add(due <= now ? $"{label}: DUE" : $"{label}: {EQBuddy.UI.Shared.Countdown.Format(due - now)}");
        else
            lines.Add(named
                ? "Respawn: no running timer"
                : "Projected respawn: unknown — this zone documents no clock");
        return string.Join("\n", lines);
    }

    /// <summary>Start or stop the imminence pulse per circle. Cancelling the beat
    /// hands Opacity back to the local value each circle was built with.</summary>
    private void UpdatePulse(DateTime now, string zone, List<SpawnTimerState> timers)
    {
        foreach (var c in _circleMeta)
        {
            var due = CircleDue(zone, c, timers);
            var secs = due is { } d ? (d - now).TotalSeconds : double.MaxValue;
            var imminent = secs <= PulseWindowSeconds && secs >= -PulseLingerSeconds;
            if (imminent == c.Pulse is not null) continue;
            if (imminent)
            {
                c.Pulse = new CancellationTokenSource();
                _ = PulseBeat().RunAsync(c.Ring, c.Pulse.Token);
                _ = PulseBeat().RunAsync(c.Halo, c.Pulse.Token);
            }
            else
            {
                c.Pulse!.Cancel();
                c.Pulse = null;
                c.Ring.Opacity = 1.0;
                c.Halo.Opacity = c.HaloOpacity;
            }
        }
    }

    /// <summary>WPF's 550 ms auto-reversing DoubleAnimation as keyframes: one full
    /// beat out and back per 1.1 s cycle, forever until cancelled.</summary>
    private static Animation PulseBeat() => new()
    {
        Duration = TimeSpan.FromMilliseconds(1100),
        IterationCount = IterationCount.Infinite,
        Children =
        {
            new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
            new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(Visual.OpacityProperty, 0.25) } },
            new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
        },
    };

    private void PlaceSpawnCircles()
    {
        foreach (var (el, x, y, dx, dy) in _spawnCircles)
        {
            var s = ToScreen(x, y);
            Canvas.SetLeft(el, s.X + dx);
            Canvas.SetTop(el, s.Y + dy);
        }
    }

    /// <summary>The breadcrumb trail: the last minute of your /locs in this zone,
    /// drawn as a comet tail that fades continuously on the wall clock (TrailFade) —
    /// tap a /loc hotbutton while traveling and the map shows where you just came
    /// from; stop moving and the tail burns down to nothing behind you. Geometry
    /// lives in map space; rebuilt when a new /loc arrives or the fade clock ticks
    /// over — age moves even when the player doesn't (David's field tests,
    /// 2026-08-10).</summary>
    private void UpdateTrail()
    {
        var trail = _host.CurrentSnapshot().LocationTrail;
        var showing = _map is not null && !_userPicked;
        var now = DateTime.Now;
        var stamp = showing ? (trail.Count, now.Ticks / FadeTick.Ticks) : (0, 0L);
        if (stamp == _trailStamp) return;
        _trailStamp = stamp;
        _mapLayer.TrailSegments.Clear();
        if (!showing || trail.Count < 2) { AfterViewChanged(); return; }

        for (var i = 1; i < trail.Count; i++)
        {
            var alpha = EQBuddy.UI.Shared.TrailFade.Alpha(now - trail[i].Time);
            if (alpha == 0) continue;   // aged out — stays in the list, not on the map
            var (x1, y1) = ZoneMap.FromLoc(trail[i - 1].LocY, trail[i - 1].LocX);
            var (x2, y2) = ZoneMap.FromLoc(trail[i].LocY, trail[i].LocX);
            _mapLayer.TrailSegments.Add((new Point(x1, y1), new Point(x2, y2),
                new SolidColorBrush(Color.FromArgb(alpha, 255, 200, 60))));
        }
        AfterViewChanged();
    }

    /// <summary>Side panel + camp pins: every running spawn timer in the shown zone,
    /// its countdown, and a pin when a camp location is known — learned from your
    /// kill-time /loc first, the wiki's location field as fallback.</summary>
    private void UpdateNamedPanel()
    {
        _namedPanel.Children.Clear();
        foreach (var (el, _, _, _, _) in _campPins) _overlay.Children.Remove(el);
        _campPins.Clear();

        var zone = _host.CurrentZoneName;
        // Same mark as the WPF map and the spawn chips: a respawn countdown is the
        // stopwatch, everywhere. Two columns rather than a StackPanel so the zone name
        // trims instead of being clipped (trap 14).
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 2, 0, 4),
        };
        var mark = DesignSystem.Icon("Timer", "AccentBrush", size: DesignTokens.IconInline);
        mark.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        header.Children.Add(mark);
        var headerText = new TextBlock
        {
            Text = zone.Length > 0 ? $"Named — {zone}" : "Named",
            FontSize = 11, FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = AppTheme.AccentBrush,
        };
        Grid.SetColumn(headerText, 1);
        header.Children.Add(headerText);
        _namedPanel.Children.Add(header);

        var now = DateTime.Now;
        // Resolve before comparing — hopping to another instance of the same zone
        // must not empty the panel (David's field test, 2026-08-10; countdowns
        // already span instances by design).
        var timerZone = ResolvedTimerZone();
        var timers = _host.SpawnTimers.Snapshot(now)
            .Where(t => string.Equals(t.Zone, timerZone, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (timers.Count == 0)
        {
            var none = new TextBlock
            {
                Text = "No running timers here — kill a named (or its placeholder) and its countdown appears, pinned to wherever your last /loc put you.",
                FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = AppTheme.DimBrush,
            };
            _namedPanel.Children.Add(none);
            return;
        }

        var pinsAllowed = _map is not null && !_userPicked;
        foreach (var t in timers)
        {
            // Camp: learned kill-time /loc wins; wiki location field is the fallback
            // (fetched through the same cached, polite lookup the Loot card uses).
            (double Y, double X)? camp = t is { CampLocY: { } cy, CampLocX: { } cx } ? (cy, cx) : null;
            var fromWiki = false;
            if (camp is null)
            {
                _host.EnsureMobLookup(t.Name);
                if (_host.WikiMobResult(t.Name)?.Mob?.LocYX is { } wl) { camp = wl; fromWiki = true; }
            }

            var due = t.DueAt;
            var isDue = t.IsDue(now);
            var countdown = due is null ? "?"
                : isDue ? "DUE"
                : EQBuddy.UI.Shared.Countdown.Format(due.Value - now);

            // Named cards (2026-08-11 modernization): name + camp source + a countdown
            // pill with an elapsed track — glanceable from across the room, DUE glows.
            var body = new StackPanel();
            var nameRow = new TextBlock
            {
                Text = $"{(camp is null ? "" : "📍 ")}{t.Name}",
                FontSize = 11.5, FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = isDue ? AppTheme.WarnBrush : AppTheme.TextBrush,
            };
            body.Children.Add(nameRow);
            var meta = new TextBlock
            {
                Text = camp is null
                    ? "no camp yet — /loc during the fight"
                    : fromWiki ? "camp from the wiki (~)" : "camp from your /loc at kill",
                FontSize = 9.5, Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = AppTheme.DimBrush,
            };
            body.Children.Add(meta);

            var gaugeRow = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            gaugeRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            gaugeRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            var pill = new Border
            {
                CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 1, 7, 2),
                Background = isDue ? AppTheme.BadBrush : AppTheme.TrackBrush,
            };
            var pillText = new TextBlock
            {
                Text = countdown, FontSize = 10.5, FontWeight = FontWeight.Bold,
                Foreground = isDue ? Brushes.White : AppTheme.AccentBrush,
            };
            pill.Child = pillText;
            gaugeRow.Children.Add(pill);
            if (t.DurationSeconds is { } dur && dur > 0 && due is not null)
            {
                var frac = isDue ? 1.0 : Math.Clamp(1 - (due.Value - now).TotalSeconds / dur, 0, 1);
                var track = new Grid { Height = 3, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var trackBg = new Border { CornerRadius = new CornerRadius(1.5), Background = AppTheme.TrackBrush };
                track.Children.Add(trackBg);
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(1.5),
                    HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
                    Background = isDue ? AppTheme.BadBrush : AppTheme.AccentBrush,
                };
                track.Children.Add(fill);
                track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * frac);
                Grid.SetColumn(track, 1);
                gaugeRow.Children.Add(track);
            }
            body.Children.Add(gaugeRow);

            var row = new Border
            {
                Child = body, CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 6, 9, 7), Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(1),
                Background = AppTheme.RaisedBrush,
                BorderBrush = isDue ? AppTheme.BadBrush : AppTheme.HairlineBrush,
            };
            ToolTip.SetTip(row, camp is null
                ? $"{t.Name} — no camp location yet: type /loc during the fight and the next kill pins it"
                : $"{t.Name} — camp {(fromWiki ? "from the wiki (~)" : "from your /loc at kill time")}");
            _namedPanel.Children.Add(row);

            if (camp is { } c && pinsAllowed)
            {
                var (mx, my) = ZoneMap.FromLoc(c.Y, c.X);
                var pin = new Polygon
                {
                    Points = new List<Point> { new(0, 0), new(5, -10), new(-5, -10) },
                    StrokeThickness = 1,
                    Fill = t.IsDue(now) ? AppTheme.WarnBrush : AppTheme.BadBrush,
                    Stroke = AppTheme.BgBrush,
                };
                var label = new TextBlock
                {
                    Text = $"{t.Name} {countdown}", FontSize = 10,
                    Foreground = t.IsDue(now) ? AppTheme.WarnBrush : AppTheme.BadBrush,
                };
                _campPins.Add((pin, mx, my, 0, 0));
                _campPins.Add((label, mx, my, 7, -14));
                _overlay.Children.Add(pin);
                _overlay.Children.Add(label);
            }
        }
        PlaceCampPins();
    }

    private void PlaceCampPins()
    {
        foreach (var (el, x, y, dx, dy) in _campPins)
        {
            var s = ToScreen(x, y);
            Canvas.SetLeft(el, s.X + dx);
            Canvas.SetTop(el, s.Y + dy);
        }
        PlaceSpawnCircles();
    }

    private void ShowFile(string file)
    {
        try
        {
            // Bounds re-derive from the merged content in MapBounds(); the merged
            // ZoneMap's own running bounds are bypassed and never read.
            var map = new ZoneMap();
            foreach (var layer in ZoneMapFiles.WithLayers(file))
            {
                var part = ZoneMap.Load(layer);
                map.Lines.AddRange(part.Lines);
                map.Points.AddRange(part.Points);
            }
            _map = map;
            _shownFile = file;
            var stem = Path.GetFileNameWithoutExtension(file);
            if (_zonePick.SelectedItem as string != stem && _zonePick.Items.Contains(stem))
                _zonePick.SelectedItem = stem;
            RenderMap();
            FitToView();
            UpdateMarker();
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            _status.Text = $"Couldn't read {Path.GetFileName(file)} — {ex.Message}";
        }
    }

    private (double MinX, double MinY, double MaxX, double MaxY) MapBounds()
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var l in _map!.Lines)
        {
            minX = Math.Min(minX, Math.Min(l.X1, l.X2)); maxX = Math.Max(maxX, Math.Max(l.X1, l.X2));
            minY = Math.Min(minY, Math.Min(l.Y1, l.Y2)); maxY = Math.Max(maxY, Math.Max(l.Y1, l.Y2));
        }
        foreach (var p in _map.Points)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }
        return (minX, minY, maxX, maxY);
    }

    private readonly List<(Control El, double X, double Y, double Dx, double Dy)> _pois = [];

    private void RenderMap()
    {
        _mapLayer.LineBatches.Clear();
        _mapLayer.TrailSegments.Clear();
        foreach (var (el, _, _, _, _) in _pois) _overlay.Children.Remove(el);
        _pois.Clear();
        if (_map is null || _map.IsEmpty) { _mapLayer.InvalidateVisual(); return; }

        // One geometry per color: a Brewall file holds thousands of segments, and one
        // StreamGeometry per color batch is what keeps that cheap.
        foreach (var group in _map.Lines.GroupBy(l => (l.R, l.G, l.B)))
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                foreach (var l in group)
                {
                    ctx.BeginFigure(new Point(l.X1, l.Y1), false);
                    ctx.LineTo(new Point(l.X2, l.Y2));
                    ctx.EndFigure(false);
                }
            }
            _mapLayer.LineBatches.Add((geo,
                new SolidColorBrush(Readable(group.Key.R, group.Key.G, group.Key.B))));
        }

        // Points and labels live in SCREEN space, repositioned on every view change —
        // inside the scale transform they zoomed with the geometry (David caught the
        // first cut: half-scale fit made labels unreadably small and lines hairline).
        foreach (var p in _map.Points)
        {
            var color = new SolidColorBrush(Readable(p.R, p.G, p.B));
            var dot = new Ellipse { Width = 5, Height = 5, Fill = color };
            var label = new TextBlock { Text = p.Label, FontSize = 11, Foreground = color };
            _pois.Add((dot, p.X, p.Y, -2.5, -2.5));
            _pois.Add((label, p.X, p.Y, 4, 3));
            _overlay.Children.Add(dot);
            _overlay.Children.Add(label);
        }
    }

    /// <summary>Everything screen-sized, after any view change: the surface redraws
    /// (its line strokes divide out the current scale — constant px however far you
    /// zoom), POIs and the player marker re-place at their transformed positions.</summary>
    private void AfterViewChanged()
    {
        _mapLayer.View = _view;
        _mapLayer.InvalidateVisual();
        foreach (var (el, x, y, dx, dy) in _pois)
        {
            var s = ToScreen(x, y);
            Canvas.SetLeft(el, s.X + dx);
            Canvas.SetTop(el, s.Y + dy);
        }
        PlaceCampPins();
        UpdateMarker();
    }

    private Point ToScreen(double x, double y) =>
        new(_view.M11 * x + _view.M21 * y + _view.M31,
            _view.M12 * x + _view.M22 * y + _view.M32);

    /// <summary>Map packs assume the game's parchment map window; a pure-black line
    /// vanishes on our dark theme. Lines darker than the floor are lifted to a
    /// readable gray, everything else keeps its pack color.</summary>
    private static Color Readable(byte r, byte g, byte b) =>
        r * 2 + g * 5 + b < 300 ? Color.FromRgb(170, 170, 170) : Color.FromRgb(r, g, b);

    /// <summary>The old forager's trick, offered wherever the marker is explained:
    /// the game itself makes /loc nearly automatic if you fold it into a social.</summary>
    private const string LocMacroTip =
        "Make /loc automatic-ish — the old forager's trick, no addons involved:\n" +
        "\n" +
        "In game, open Socials and make a macro:\n" +
        "    Line 1:  " + GameCommands.LocSocialLine1 + "\n" +
        "    Line 2:  " + GameCommands.LocSocialLine2 + "   (Forage, Sense Heading, Kick — whatever you already spam)\n" +
        "\n" +
        "Put it on the hotbar key that skill already lives on, and every press drops a\n" +
        "breadcrumb while doing exactly what the key did before.\n" +
        "\n" +
        "Even better: bind that hotbar slot to a movement key you TAP a lot — the turn\n" +
        "keys (A and D) are the sweet spot, because every course adjustment drops a\n" +
        "crumb and the trail draws itself. (W works too, but held keys don't repeat —\n" +
        "it only fires when you start moving.) Either way it's a plain in-game social —\n" +
        "the game runs it, EQBuddy just reads the log (and doesn't mind however many\n" +
        "/locs you produce).";

    private void UpdateMarker()
    {
        var loc = _host.CurrentSnapshot().LastLocation;
        var following = !_userPicked && _shownFile.Length > 0;
        if (_map is null || loc is null || !following)
        {
            _marker.IsVisible = false;
            if (_shownFile.Length > 0)
                _status.Text = $"{Path.GetFileNameWithoutExtension(_shownFile)} — type /loc in game to place " +
                    "your marker (hover here: a macro trick makes it near-automatic).";
            ToolTip.SetTip(_status, LocMacroTip);
            return;
        }
        var (mx, my) = ZoneMap.FromLoc(loc.LocY, loc.LocX);
        var screen = ToScreen(mx, my);
        Canvas.SetLeft(_marker, screen.X - _marker.Width / 2);
        Canvas.SetTop(_marker, screen.Y - _marker.Height / 2);
        _marker.IsVisible = true;
        var age = DateTime.Now - loc.Time;
        _status.Text = $"{Path.GetFileNameWithoutExtension(_shownFile)} — position from /loc " +
            (age.TotalMinutes < 1 ? "just now" : $"{(int)age.TotalMinutes}m ago") +
            " (type /loc to update; EQBuddy reads only the log)";
        ToolTip.SetTip(_status, LocMacroTip);
    }

    private void FitToView()
    {
        if (_map is null || _map.IsEmpty || _mapHost.Bounds.Width < 50) { return; }
        var (minX, minY, maxX, maxY) = MapBounds();
        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        var scale = Math.Min(_mapHost.Bounds.Width / w, _mapHost.Bounds.Height / h) * 0.94;
        _view = Matrix.CreateTranslation(-minX - w / 2, -minY - h / 2)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(_mapHost.Bounds.Width / 2, _mapHost.Bounds.Height / 2);
        AfterViewChanged();
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0) return;
        var factor = e.Delta.Y > 0 ? 1.25 : 0.8;
        var at = e.GetPosition(_mapHost);
        // WPF's Matrix.ScaleAt, spelled out: translate the cursor to the origin,
        // scale, translate it back — appended to the current view.
        _view = _view
            * Matrix.CreateTranslation(-at.X, -at.Y)
            * Matrix.CreateScale(factor, factor)
            * Matrix.CreateTranslation(at.X, at.Y);
        AfterViewChanged();
        e.Handled = true;
    }

    private void OnDrag(object? sender, PointerEventArgs e)
    {
        if (!_dragging || !e.GetCurrentPoint(_mapHost).Properties.IsLeftButtonPressed) return;
        var pos = e.GetPosition(_mapHost);
        _view *= Matrix.CreateTranslation(pos.X - _dragStart.X, pos.Y - _dragStart.Y);
        _dragStart = pos;
        AfterViewChanged();
    }

    private void ChooseFolder()
    {
        _ = ChooseFolderAsync();
    }

    private async Task ChooseFolderAsync()
    {
        try
        {
            var storage = TopLevel.GetTopLevel(_mapHost)?.StorageProvider;
            if (storage is null) return;
            var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Pick the folder holding zone map .txt files",
                AllowMultiple = false,
            });
            if (picked.FirstOrDefault()?.TryGetLocalPath() is not { } dir) return;
            _host.Settings.MapFolder = dir;
            _host.Settings.Save();
            PopulateZoneList();
            _followedZone = "";
            _userPicked = false;
            MaybeRefresh(force: true);
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>The geometry half of the map: line batches and the breadcrumb trail,
    /// drawn under the view matrix. Stroke widths divide out the current scale so
    /// lines stay a constant 1.2 px (trail 2.2 px) however far you zoom.</summary>
    private sealed class MapSurface : Control
    {
        public List<(StreamGeometry Geometry, IBrush Brush)> LineBatches { get; } = [];
        public List<(Point A, Point B, IBrush Brush)> TrailSegments { get; } = [];
        public Matrix View { get; set; } = Matrix.Identity;

        public override void Render(DrawingContext context)
        {
            if (LineBatches.Count == 0 && TrailSegments.Count == 0) return;
            var scale = Math.Max(0.0001, Math.Abs(View.M11));
            using var _ = context.PushTransform(View);
            foreach (var (geometry, brush) in LineBatches)
                context.DrawGeometry(null, new Pen(brush, 1.2 / scale), geometry);
            foreach (var (a, b, brush) in TrailSegments)
                context.DrawLine(new Pen(brush, 2.2 / scale), a, b);
        }
    }

    /// <summary>Facts for a debug/E2E-style dump — the WPF layer's shape, mirrored here
    /// for parity even though Avalonia has no launched-app E2E suite of its own; kept
    /// consistent so a reader of one lane recognises the other's.</summary>
    public string DebugFacts() =>
        $"mapShown={(_shownFile.Length > 0 ? 1 : 0)} mapZones={_zonePick.Items.Count} " +
        $"mapNamedRows={_namedPanel.Children.Count} mapCircles={_spawnCircles.Count} " +
        $"mapCampPins={_campPins.Count} mapMarkerVisible={(_marker.IsVisible ? 1 : 0)}";
}
