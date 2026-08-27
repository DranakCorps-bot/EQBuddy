using EQBuddy.Companion;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The map surface: the pack's picture is loaded once per zone, cached, and
/// stamped so a device holding it is never sent it again; the spawn circles and the
/// /loc marker ride every push.</summary>
public class CompanionMapSourceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 8, 14, 20, 0, 0);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "eqb-map-" + Guid.NewGuid().ToString("N"));

    public CompanionMapSourceTests()
    {
        Directory.CreateDirectory(_dir);
        // A tiny Brewall-shaped map: two segments (one near-black, which the shared
        // readability rule lifts) and one labeled point.
        File.WriteAllLines(Path.Combine(_dir, "befallen.txt"),
        [
            "L 0.0, 0.0, 0.0, 100.0, 50.0, 0.0, 200, 200, 200",
            "L 100.0, 50.0, 0.0, 100.0, 150.0, 0.0, 0, 0, 0",
            "P -20.0, -30.0, 0.0, 240, 200, 60, 3, Camp_One",
        ]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp dir */ }
        GC.SuppressFinalize(this);
    }

    private CompanionMapSource Source() =>
        new(new AppSettings { MapFolder = _dir });

    /// <summary>A request with just the zone filled in — every test names only the
    /// layer it is about, which is the point of the bundle.</summary>
    private static CompanionMapRequest In(string mapZone = "Befallen", string? timerZone = null) =>
        new() { MapZone = mapZone, TimerZone = timerZone ?? mapZone };

    [Fact]
    public void LoadsTheZonesPictureAndStampsIt()
    {
        var map = Source().Build(In(), Now);

        Assert.Equal("Befallen", map.Zone);
        Assert.Null(map.Missing);
        var geo = Assert.IsType<CompanionMapGeometry>(map.Geometry);
        Assert.NotEmpty(geo.Stamp);
        Assert.Equal(map.GeometryStamp, geo.Stamp);
        Assert.False(geo.Truncated);

        // One stroke per colour, segments flattened to x1,y1,x2,y2 — and the black
        // line arrives lifted to the shared readable grey, not invisible on a dark UI.
        Assert.Equal(2, geo.Strokes.Count);
        Assert.Contains(geo.Strokes, s => s.Color == "#C8C8C8" && s.Segments.SequenceEqual([0, 0, 100, 50]));
        Assert.Contains(geo.Strokes, s => s.Color == "#AAAAAA");
        Assert.Equal("Camp One", geo.Pois[0].Label);   // underscores are spaces
        Assert.Equal(-30, geo.MinY);
        Assert.Equal(150, geo.MaxY);
    }

    [Fact]
    public void GeometryIsParsedOncePerZoneAndTheStampHoldsStill()
    {
        var source = Source();
        var first = source.Build(In(), Now);
        var second = source.Build(In(), Now.AddSeconds(1));

        // The same object, not a re-parse — this is the "never re-serialized per tick"
        // promise the wire's sticky-geometry rule depends on.
        Assert.Same(first.Geometry, second.Geometry);
        Assert.Equal(first.GeometryStamp, second.GeometryStamp);
    }

    [Fact]
    public void AMissingMapNamesTheFileItWanted()
    {
        var map = Source().Build(In("Plane of Sky"), Now);
        Assert.Null(map.Geometry);
        Assert.NotNull(map.Missing);
        Assert.Contains("airplane.txt", map.Missing);
        Assert.Contains(_dir, map.Missing);
    }

    [Fact]
    public void TheMarkerIsTheLastLocInMapSpace()
    {
        // The game prints /loc as (Y, X); a position plots at map (-X, -Y).
        var loc = new LocationEvent(Now.AddSeconds(-45), 30, 20, 0);
        var map = Source().Build(In() with { Location = loc }, Now);

        Assert.NotNull(map.You);
        Assert.Equal(-20, map.You!.X);
        Assert.Equal(-30, map.You.Y);
        Assert.Equal(45, map.You.AgeSeconds, 1);
    }

    /// <summary>Session camp markers plot at the /loc they were dropped at (World PR 4)
    /// — same plotting rule as the "you are here" marker above, same file.</summary>
    [Fact]
    public void ACampMarkerWithALocationPlotsAPin()
    {
        var markers = new List<MarkerDetail>
        {
            new(Now.AddMinutes(-2), "Marker 1 — Befallen", LocY: 30, LocX: 20),
        };
        var map = Source().Build(In() with { Markers = markers }, Now);

        var pin = Assert.Single(map.Markers);
        Assert.Equal(-20, pin.X);
        Assert.Equal(-30, pin.Y);
        Assert.Equal("Marker 1 — Befallen", pin.Text);
        Assert.Equal(120, pin.AgeSeconds, 1);
    }

    /// <summary>A marker dropped before the first /loc has nowhere to plot — it still
    /// belongs on the Travels list (unaffected by this surface), it just gets no pin,
    /// the same "row but no dot" shape a named with no camp yet gets.</summary>
    [Fact]
    public void ACampMarkerWithNoLocationPlotsNoPin()
    {
        var markers = new List<MarkerDetail> { new(Now, "Marker 1 — Befallen") };
        var map = Source().Build(In() with { Markers = markers }, Now);

        Assert.Empty(map.Markers);
    }

    [Fact]
    public void CirclesCarryTheirCountdownAndTheirImminence()
    {
        var catalog = SpawnCatalog.LoadEmbedded();
        var zone = catalog.Zones.FirstOrDefault(z => z.Named.Count > 0);
        Assert.NotNull(zone);
        var named = zone!.Named[0].Name;

        var ledgerDir = Path.Combine(_dir, "ledger");
        var ledger = new SpawnPointLedger(ledgerDir, catalog);
        ledger.Apply(new ZoneEvent(Now.AddMinutes(-10), zone.Zone));
        ledger.Apply(new LocationEvent(Now.AddMinutes(-9), 100, 200, 0));
        ledger.Apply(new KillEvent(Now.AddMinutes(-9), named, "Dranak"));

        // A running timer with 5 seconds left: inside the pulse window.
        var timers = new List<SpawnTimerState>
        {
            new("legends", zone.Zone, named, Now.AddSeconds(-55), 60),
        };
        var map = Source().Build(In(timerZone: zone.Zone) with { Points = ledger, Timers = timers }, Now);

        var circle = Assert.Single(map.Circles);
        Assert.True(circle.Named);
        Assert.Equal(named, circle.Label);
        Assert.False(circle.Projected);
        Assert.Equal(5, circle.DueSeconds!.Value, 1);
        Assert.True(circle.Imminent);
        Assert.Equal(1, circle.Kills);
        Assert.Contains(named, circle.Mobs, StringComparison.OrdinalIgnoreCase);
        // Map space again: /loc (100, 200) plots at (-200, -100).
        Assert.Equal(-200, circle.X);
        Assert.Equal(-100, circle.Y);
    }

    // ---- the breadcrumb trail ----
    // David, 2026-08-14: "the breadcrumbs don't render at all now. This should work and
    // display the same as it would on the PC." They had never been projected at all —
    // StatsSnapshot.LocationTrail reached MapWindow and nothing else.

    private static LocationEvent Crumb(double ageSeconds, double locY, double locX) =>
        new(Now.AddSeconds(-ageSeconds), locY, locX, 0);

    [Fact]
    public void TheTrailRidesTheWireInMapSpaceOldestFirst()
    {
        var map = Source().Build(In() with { Trail = [Crumb(30, 10, 20), Crumb(20, 30, 40), Crumb(5, 50, 60)] }, Now);

        Assert.Equal(3, map.Trail.Count);
        // Same (Y, X) → (-X, -Y) plot the marker and the circles use.
        Assert.Equal(-20, map.Trail[0].X);
        Assert.Equal(-10, map.Trail[0].Y);
        // Oldest first, so ages descend along the list — the page draws each segment
        // at the alpha of its newer end.
        Assert.Equal([30d, 20d, 5d], map.Trail.Select(c => Math.Round(c.AgeSeconds)));
    }

    [Fact]
    public void ASingleCrumbIsNotATail()
    {
        var one = Source().Build(In() with { Trail = [Crumb(5, 10, 20)] }, Now);
        Assert.Empty(one.Trail);

        var none = Source().Build(In(), Now);
        Assert.Empty(none.Trail);
    }

    [Fact]
    public void CrumbsPastTheHorizonAreDroppedButOneAnchorSurvives()
    {
        // Two crumbs well past the fade horizon, two inside it. The desktop still draws
        // the segment INTO the oldest live crumb, so its predecessor has to ride along
        // as an anchor — otherwise the phone's tail is a segment shorter than the PC's.
        var map = Source().Build(In() with { Trail = [
            Crumb(300, 10, 10), Crumb(120, 20, 20), Crumb(40, 30, 30), Crumb(2, 40, 40),
        ] }, Now);

        Assert.Equal(3, map.Trail.Count);
        Assert.Equal([120d, 40d, 2d], map.Trail.Select(c => Math.Round(c.AgeSeconds)));
    }

    [Fact]
    public void AWhollyFadedTrailShipsNothing()
    {
        var map = Source().Build(In() with { Trail = [Crumb(600, 10, 10), Crumb(300, 20, 20)] }, Now);
        Assert.Empty(map.Trail);
    }

    [Fact]
    public void TheTrailDoesNotWakeADeviceMerelyForFading()
    {
        // Section fingerprints exist so a phone only wakes when ITS surfaces MOVE.
        // Ages drift every tick by definition; only a new crumb is news.
        static string Print(CompanionMapSection map) =>
            CompanionProjection.SectionFingerprints(
                CompanionProjection.Build(new CompanionInputs
                {
                    Offered = [CompanionSurfaces.Map],
                    Map = map,
                }, Now))[CompanionSurfaces.Map];

        var source = Source();
        var trail = new List<LocationEvent> { Crumb(20, 10, 20), Crumb(5, 30, 40) };
        var first = source.Build(In() with { Trail = trail }, Now);
        var older = source.Build(In() with { Trail = trail }, Now.AddSeconds(10));
        Assert.Equal(Print(first), Print(older));

        trail.Add(Crumb(-15, 50, 60));   // a fresh /loc, 15s after "now"
        var moved = source.Build(In() with { Trail = trail }, Now.AddSeconds(15));
        Assert.NotEqual(Print(first), Print(moved));
    }

    [Fact]
    public void ThePageFadesOnTheSameCurveTheDesktopDoes()
    {
        // The phone can't call TrailFade, so it carries the two numbers itself. This is
        // the lock: change the C# curve without changing the page and the tail silently
        // stops matching the PC — which is exactly the class of bug that lost the
        // breadcrumbs in the first place.
        var declared = System.Text.RegularExpressions.Regex.Match(
            PhonePage.Html, @"TRAIL_FULL_ALPHA\s*=\s*(\d+),\s*TRAIL_HORIZON\s*=\s*(\d+)");
        Assert.True(declared.Success, "The phone page no longer declares its trail-fade constants.");
        Assert.Equal(EQBuddy.UI.Shared.TrailFade.FullAlpha, byte.Parse(declared.Groups[1].Value));
        Assert.Equal(EQBuddy.UI.Shared.TrailFade.Horizon.TotalSeconds, double.Parse(declared.Groups[2].Value));
    }

    // ---- camp pins ----
    // The desktop's named panel pins every running timer whose camp it can resolve;
    // the tablet draws the same pins from the same resolution.

    private static SpawnTimerState Timer(string zone, string name, double ageSeconds, double duration) =>
        new("legends", zone, name, Now.AddSeconds(-ageSeconds), duration);

    [Fact]
    public void CampPinsCarryTheCountdownAndOwnUpToAWikiCamp()
    {
        var yours = Timer("Befallen", "Ghoul Assassin", 30, 100);
        var wiki = Timer("Befallen", "Sir Rufus", 10, 100);
        var unknown = Timer("Befallen", "Nobody", 10, 100);

        var map = Source().Build(In() with
        {
            Timers = [yours, wiki, unknown],
            // /loc at kill for the first, the wiki's location field for the second,
            // and nothing at all for the third.
            CampFor = t => t.Name switch
            {
                "Ghoul Assassin" => (100.0, 200.0, false),
                "Sir Rufus" => (-50.0, 25.0, true),
                _ => null,
            },
        }, Now);

        // EVERY running timer gets a row; only the ones with a camp get coordinates.
        // The desktop's named panel lists all three too — a named with no camp is
        // precisely the one asking you to /loc during the fight.
        Assert.Equal(3, map.Named.Count);
        var mine = map.Named.Single(n => n.Name == "Ghoul Assassin");
        Assert.Equal(-200, mine.X);        // (Y, X) → (-X, -Y), same as everything else
        Assert.Equal(-100, mine.Y);
        Assert.False(mine.FromWiki);
        Assert.Equal(70, mine.DueSeconds!.Value, 1);
        Assert.False(mine.Due);
        Assert.Equal(100, mine.DurationSeconds);   // the row's elapsed track needs it

        var theirs = map.Named.Single(n => n.Name == "Sir Rufus");
        Assert.True(theirs.FromWiki);      // the desktop's "~": approximate, and says so

        var campless = map.Named.Single(n => n.Name == "Nobody");
        Assert.Null(campless.X);           // a row, but no pin
        Assert.Null(campless.Y);
    }

    [Fact]
    public void NamedAreSoonestFirstSoTheSidePanelReadsInOrder()
    {
        var map = Source().Build(In() with
        {
            Timers =
            [
                Timer("Befallen", "Late", 10, 600),      // ~590s left
                Timer("Befallen", "Soon", 90, 100),      // ~10s left
                Timer("Befallen", "Middle", 30, 200),    // ~170s left
            ],
        }, Now);

        Assert.Equal(["Soon", "Middle", "Late"], map.Named.Select(n => n.Name));
    }

    [Fact]
    public void ADueNamedSaysSo()
    {
        var map = Source().Build(In() with
        {
            Timers = [Timer("Befallen", "Ghoul Assassin", 120, 60)],
            CampFor = _ => (10.0, 10.0, false),
        }, Now);

        var named = Assert.Single(map.Named);
        Assert.True(named.Due);
        Assert.True(named.DueSeconds < 0);   // already overdue; the page shows DUE
    }

    [Fact]
    public void AHostThatCannotResolveCampsStillGetsItsRows()
    {
        // Avalonia, tests, any host that hasn't wired CampFor: rows and countdowns
        // still arrive, they just carry no camp — and above all no second wiki lookup
        // is started from inside the companion.
        var map = Source().Build(In() with { Timers = [Timer("Befallen", "Ghoul Assassin", 5, 60)] }, Now);
        var named = Assert.Single(map.Named);
        Assert.Null(named.X);
        Assert.Equal(55, named.DueSeconds!.Value, 1);
    }
}
