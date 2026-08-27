using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Path = System.IO.Path;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The zone/map cluster rendered against a fake IZoneHost — real Core collaborators, no
/// MainWindow. World PR 2 retired the standalone <c>MapWindow</c>/<c>TravelWindow</c>
/// wrappers into <c>WorldWindow</c> (which needs a full <c>MainWindow</c>, not a fake
/// host), so these tests render <see cref="MapView"/>/<see cref="TravelView"/> directly
/// in a plain test <see cref="Window"/> — exactly what the retired wrappers did, minus
/// the chrome this file never asserted on. <c>ZoneShareWindow</c>/<c>SessionPickerWindow</c>
/// are untouched by that fold and still construct directly.
/// </summary>
[Collection("avalonia")]
public sealed class ZoneWindowsRenderTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("eqbuddy-zone-render-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_profile, recursive: true); }
        catch (Exception ex) { Console.Error.WriteLine($"profile cleanup failed: {ex.Message}"); }
    }

    private sealed class FakeZoneHost : IZoneHost
    {
        public AppSettings Settings { get; } = new();
        public string CurrentZoneName { get; set; } = "";
        public SpawnCatalog SpawnCatalogData { get; } = SpawnCatalog.LoadEmbedded();
        public SpawnOverrides SpawnOverridesStore { get; } = new();
        public SpawnPointLedger SpawnPoints { get; }
        public SpawnTimers SpawnTimers { get; }
        public ZoneGraph ZoneGraph { get; } = ZoneGraph.LoadEmbedded();
        public StatsSnapshot Snapshot { get; set; } = new();

        public FakeZoneHost(string profileDir)
        {
            SpawnPoints = new SpawnPointLedger(Path.Combine(profileDir, "spawnpoints"), SpawnCatalogData);
            SpawnTimers = new SpawnTimers(SpawnCatalogData, SpawnOverridesStore);
        }

        public StatsSnapshot CurrentSnapshot() => Snapshot;
        public MobLookupResult? WikiMobResult(string name) => null;
        public void EnsureMobLookup(string name) { }
        public void PlayAlertSound(string choiceOrPath, bool coalesce = false) { }
        public void DropCampMarker() { }
    }

    /// <summary>One archived kill in Befallen — enough for a spawn point.</summary>
    private static void ObserveOneKill(FakeZoneHost host)
    {
        var t = new DateTime(2026, 8, 13, 20, 0, 0);
        host.SpawnPoints.Apply(new ZoneEvent(t, "Befallen"));
        host.SpawnPoints.Apply(new LocationEvent(t, 100, 200, 0));
        host.SpawnPoints.Apply(new KillEvent(t.AddSeconds(30), "a decaying skeleton", "You"));
    }

    [AvaloniaFact]
    public void MapWindowDrawsTheMapAndArchivedSpawnCircles()
    {
        var host = new FakeZoneHost(_profile);
        var maps = Directory.CreateDirectory(Path.Combine(_profile, "maps")).FullName;
        File.WriteAllText(Path.Combine(maps, "befallen.txt"),
            "L 0, 0, 0, 100, 100, 0, 0, 0, 0\n" +
            "L 100, 100, 0, 200, 0, 0, 255, 0, 0\n" +
            "P 50, 50, 0, 255, 255, 255, 2, Test_Label\n");
        host.Settings.MapFolder = maps;
        host.CurrentZoneName = "Befallen";
        ObserveOneKill(host);

        var view = new MapView(host);
        var window = new Window { Content = view.Body };
        window.Show();
        view.MaybeRefresh(force: true);

        var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains(text, t => t?.StartsWith("befallen — type /loc in game") == true);
        Assert.Contains("Test Label", text);                 // POI label, underscores unfolded
        // The side panel follows the zone. The heading's mark is the STOPWATCH vector the
        // spawn chips wear — a respawn countdown is one picture everywhere, and it used
        // to be an emoji hourglass here while the chips had moved on (2026-08-19).
        Assert.Contains("Named — Befallen", text);
        var stopwatch = StreamGeometry.Parse(IconPaths.Path("Timer")).ToString();
        Assert.Contains(window.GetVisualDescendants().OfType<PathIcon>(),
            icon => icon.Data?.ToString() == stopwatch);
        // Marker + spawn-circle halo/ring + POI dot at minimum.
        Assert.True(window.GetVisualDescendants().OfType<Ellipse>().Count() >= 4);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The map's right-click menus (ported from WPF): a circle's menu edits
    /// the archive through the shared ledger, and the map-level menu names the zone
    /// it would reset. The reset itself is behind a dialog, so what's exercised here
    /// is the guard that fires before one ever opens.</summary>
    [AvaloniaFact]
    public void MapCircleMenuConfirmsThenRemovesTheSpawnPoint()
    {
        var host = new FakeZoneHost(_profile);
        var maps = Directory.CreateDirectory(Path.Combine(_profile, "maps")).FullName;
        File.WriteAllText(Path.Combine(maps, "befallen.txt"), "L 0, 0, 0, 100, 100, 0, 0, 0, 0\n");
        host.Settings.MapFolder = maps;
        host.CurrentZoneName = "Befallen";
        ObserveOneKill(host);

        var view = new MapView(host);
        var window = new Window { Content = view.Body };
        window.Show();
        view.MaybeRefresh(force: true);

        // Circles win over the map's own menu when the right-click lands on a dot:
        // the ring owns the ContextRequested bubble and stops it there.
        var ring = window.GetVisualDescendants().OfType<Ellipse>()
            .First(e => e.ContextMenu is not null);
        var mapHost = window.GetVisualDescendants().OfType<Panel>()
            .First(p => p.ContextMenu is not null);
        ring.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        Assert.True(ring.ContextMenu!.IsOpen);
        Assert.False(mapHost.ContextMenu!.IsOpen);
        ring.ContextMenu.Close();

        var said = ClickAndReadTexts(window, CircleMenuItem(window, "Confirm location"));
        Assert.True(host.SpawnPoints.Snapshot("Befallen").Points.Single().Confirmed);
        Assert.Contains(said, t => t?.StartsWith("Location confirmed (Decaying skeleton)") == true);

        // The edit bumped Revision, so the next tick rebuilds the circles — and the
        // rebuilt menu offers the other direction.
        view.MaybeRefresh(force: true);
        said = ClickAndReadTexts(window, CircleMenuItem(window, "Remove this spawn point"));
        Assert.Empty(host.SpawnPoints.Snapshot("Befallen").Points);
        Assert.Contains(said, t => t?.StartsWith("Spawn point removed (Decaying skeleton)") == true);

        // Opening the map menu relabels its one item with the zone it would wipe…
        var reset = mapHost.ContextMenu.Items.OfType<MenuItem>().Single();
        mapHost.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Reset spawn points — Befallen…", reset.Header);
        mapHost.ContextMenu.Close();
        // …and with nothing archived any more, it says so instead of asking.
        Assert.Contains("Nothing to reset — Befallen has no archived spawn points yet.",
            ClickAndReadTexts(window, reset));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The circle rings are the only overlay ellipses carrying a menu.</summary>
    private static MenuItem CircleMenuItem(Window window, string header) =>
        window.GetVisualDescendants().OfType<Ellipse>()
            .First(e => e.ContextMenu is not null)
            .ContextMenu!.Items.OfType<MenuItem>()
            .Single(i => (i.Header as string) == header);

    /// <summary>Click a menu item and read the window's text back before anything else runs.
    /// The reply a click writes is transient: the status bar it lands in is shared with the
    /// map's standing caption, and any layout pass that resizes the map host refits the view,
    /// which rewrites the caption over the reply (as does the app's own refresh tick a second
    /// later — the same reason the WPF window behaves this way). Whether a pass resizes
    /// anything depends on how the text measures, so pumping the dispatcher here made this
    /// test pass on one platform's fonts and fail on another's. Handlers run synchronously on
    /// RaiseEvent, so the snapshot below is exactly what the click put on screen.</summary>
    private static List<string?> ClickAndReadTexts(Window window, MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        return window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
    }

    [AvaloniaFact]
    public void TravelWindowAnswersForTheCurrentZone()
    {
        var host = new FakeZoneHost(_profile);
        var zone = host.ZoneGraph.Zones.First();
        host.CurrentZoneName = zone;

        var view = new TravelView(host);
        var window = new Window { Content = view.Body };
        window.Show();
        var dest = window.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
        dest.Text = zone;
        view.Render();

        var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains($"From: {zone}", text);
        Assert.Contains("You're already there.", text);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ZoneShareWindowPreviewsItsOwnExportAsNoChange()
    {
        var host = new FakeZoneHost(_profile);
        ObserveOneKill(host);

        var window = new ZoneShareWindow(host.SpawnPoints, host.SpawnCatalogData,
            host.SpawnOverridesStore, "Befallen");
        window.Show();
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text?.StartsWith("Your Befallen archive: 1 spawn point, 1 kill observed") == true);

        var export = ZoneShare.Export(host.SpawnPoints.Snapshot("Befallen"),
            host.SpawnCatalogData.FindZone("Befallen"), host.SpawnOverridesStore);
        window.GetVisualDescendants().OfType<TextBox>().Single().Text = export;
        var preview = window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content as string == "Preview…");
        preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Its own point re-finds its own cluster: nothing new, one "refined", no
        // fresh observations — the add-only merge contract, visible in the preview.
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text?.StartsWith("Befallen: +0 new points, 1 refined, +0 observations") == true);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void SessionPickerListsNewestFirstAndPreselectsIt()
    {
        var older = new LogSessionInfo(0, 10,
            new DateTime(2026, 8, 1, 10, 0, 0), new DateTime(2026, 8, 1, 11, 0, 0));
        var newer = new LogSessionInfo(10, 20,
            new DateTime(2026, 8, 2, 10, 0, 0), new DateTime(2026, 8, 2, 10, 30, 0));

        var window = new SessionPickerWindow("eqlog_Test.txt", [older, newer]);
        window.Show();

        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text?.Contains("holds 2 sessions") == true);
        var items = window.GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Equal(2, items.Count);
        Assert.Same(newer, items[0].Tag);
        Assert.True(items[0].IsSelected);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
