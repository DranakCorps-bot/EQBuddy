using System.IO;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>
/// One tick's map inputs. A bundle rather than a parameter list, for the same reason
/// <see cref="CompanionInputs"/> is one: the map is the surface still growing towards
/// the desktop's, and every new layer would otherwise rewrite every call site.
/// </summary>
public sealed record CompanionMapRequest
{
    /// <summary>The log's zone name, which is what names the map FILE.</summary>
    public string MapZone { get; init; } = "";

    /// <summary>The catalog zone the spawn archive and timers live under. Kept apart
    /// from <see cref="MapZone"/> exactly as the desktop keeps them apart — "Befallen 4
    /// (Refined)" has no map file of its own.</summary>
    public string TimerZone { get; init; } = "";

    public SpawnPointLedger? Points { get; init; }
    public IReadOnlyList<SpawnTimerState> Timers { get; init; } = [];

    /// <summary>Your last /loc, and the crumbs behind it.</summary>
    public LocationEvent? Location { get; init; }
    public IReadOnlyList<LocationEvent>? Trail { get; init; }

    /// <summary>Session camp markers (World PR 4) — "Drop camp marker"'s output, the
    /// same list the desktop's Travels tab reads.</summary>
    public IReadOnlyList<MarkerDetail>? Markers { get; init; }

    /// <summary>Camp resolution, owned by the desktop — see CompanionSources.CampFor.
    /// Null simply means no pins, which is what a host that can't answer should get.</summary>
    public Func<SpawnTimerState, (double Y, double X, bool FromWiki)?>? CampFor { get; init; }
}

/// <summary>
/// The map surface's builder, and its cache. The zone's PICTURE is static — thousands
/// of segments parsed off disk — so it is loaded once per zone and then handed out by
/// reference; only the marker and the spawn-point circles are rebuilt per tick, and
/// the circles only when the ledger's revision moves. Nothing here runs unless the map
/// surface is offered AND a device is connected.
///
/// The geometry is serialized ONCE per zone too: <see cref="CompanionSnapshot.ForClient"/>
/// withholds it from any device already holding the stamp, so a phone parked in Lower
/// Guk for an hour receives the picture exactly once.
/// </summary>
public sealed class CompanionMapSource
{
    /// <summary>"Imminent" = due within this many seconds, and it keeps pulsing this
    /// long past due — the same window the desktop map pulses its circles on.</summary>
    public const double PulseWindowSeconds = 10;
    public const double PulseLingerSeconds = 30;

    /// <summary>A hard ceiling on segments shipped for one zone. Brewall's biggest maps
    /// run into the tens of thousands; past this the phone is drawing hair, not a map,
    /// and the page says so rather than pretending it drew everything.</summary>
    public const int MaxSegments = 24000;

    private readonly AppSettings _settings;
    private string _geometryZone = "\0";
    private CompanionMapGeometry? _geometry;
    private string? _missing;

    private (string Zone, int Revision) _pointsStamp = ("\0", -1);
    private SpawnPointLedger.ZoneArchive? _points;

    public CompanionMapSource(AppSettings settings) => _settings = settings;

    /// <summary>The user's pack first, the game's own maps folder as the fallback —
    /// the desktop map window's precedence, so both windows show the same file.</summary>
    public IReadOnlyList<string> Folders()
    {
        var folders = new List<string>(2);
        try
        {
            if (_settings.MapFolder is { Length: > 0 } custom && Directory.Exists(custom))
                folders.Add(custom);
            if (ZoneMapFiles.DefaultFolder(_settings.LogFolder) is { } game
                && !folders.Contains(game, StringComparer.OrdinalIgnoreCase))
                folders.Add(game);
        }
        catch (Exception ex) { CoreLog.Error(ex); }
        return folders;
    }

    /// <summary>Build the section from one tick's worth of map inputs. Everything the
    /// map layer needs arrives in <see cref="CompanionMapRequest"/> rather than as a
    /// parameter list — the same reason <see cref="CompanionInputs"/> exists, learned
    /// the hard way when the trail and the camp pins each rewrote every call site.</summary>
    public CompanionMapSection Build(CompanionMapRequest request, DateTime now)
    {
        EnsureGeometry(request.MapZone);

        var circles = request.Points is null || request.TimerZone.Length == 0
            ? []
            : BuildCircles(request.Points, request.TimerZone, request.Timers, now);

        CompanionMapMarker? you = request.Location is { } loc
            ? Marker(loc, now)
            : null;

        return new CompanionMapSection(
            Zone: request.MapZone,
            TimerZone: request.TimerZone,
            GeometryStamp: _geometry?.Stamp ?? "",
            Geometry: _geometry,
            Missing: _missing,
            You: you,
            Circles: circles,
            Trail: BuildTrail(request.Trail, now),
            Named: BuildNamed(request.Timers, request.CampFor, now),
            Markers: BuildMarkers(request.Markers, now));
    }

    /// <summary>Session camp markers, plotted where a /loc was known at drop time — a
    /// marker with no location gets no pin, the same "row but no dot" treatment a named
    /// with no camp yet gets in <see cref="BuildNamed"/>.</summary>
    private static List<CompanionMapPin> BuildMarkers(IReadOnlyList<MarkerDetail>? markers, DateTime now)
    {
        if (markers is null || markers.Count == 0) return [];
        var pins = new List<CompanionMapPin>(markers.Count);
        foreach (var m in markers)
        {
            if (m.LocY is not { } locY || m.LocX is not { } locX) continue;
            var (x, y) = ZoneMap.FromLoc(locY, locX);
            pins.Add(new CompanionMapPin(x, y, m.Text, Math.Max(0, (now - m.Time).TotalSeconds)));
        }
        return pins;
    }

    /// <summary>The zone's running named: the desktop map's side panel in list form,
    /// with the camp coordinates that also plant its pins. EVERY running timer gets a
    /// row — a named with no camp yet is exactly the one you want to see, because it is
    /// the one asking you to /loc during the fight.
    ///
    /// Camp resolution stays on the desktop side of the delegate: the wiki fallback is a
    /// memoized, rate-limited lookup the app already owns, and the companion has no
    /// business starting a second one. A host that hasn't wired it still gets rows and
    /// countdowns, just no camps.</summary>
    private static List<CompanionMapNamed> BuildNamed(
        IReadOnlyList<SpawnTimerState> timers,
        Func<SpawnTimerState, (double Y, double X, bool FromWiki)?>? campFor,
        DateTime now)
    {
        var rows = new List<CompanionMapNamed>(timers.Count);
        foreach (var t in timers)
        {
            var camp = campFor?.Invoke(t);
            var plotted = camp is { } c ? ZoneMap.FromLoc(c.Y, c.X) : ((double X, double Y)?)null;
            rows.Add(new CompanionMapNamed(
                t.Name,
                DueSeconds: t.DueAt is { } due ? (due - now).TotalSeconds : null,
                Due: t.IsDue(now),
                DurationSeconds: t.DurationSeconds,
                X: plotted?.X, Y: plotted?.Y,
                FromWiki: camp?.FromWiki ?? false));
        }
        // Soonest first, unknown durations last — the desktop's own reading order, and
        // the order a side panel has to be in to be glanceable.
        rows.Sort((a, b) => (a.DueSeconds ?? double.MaxValue).CompareTo(b.DueSeconds ?? double.MaxValue));
        return rows;
    }

    private static CompanionMapMarker Marker(LocationEvent loc, DateTime now)
    {
        var (x, y) = ZoneMap.FromLoc(loc.LocY, loc.LocX);
        return new CompanionMapMarker(x, y, Math.Max(0, (now - loc.Time).TotalSeconds));
    }

    /// <summary>The breadcrumb trail, oldest first — the wire form of what the desktop
    /// map draws in UpdateTrail. A segment takes the alpha of its NEWER end, so the
    /// oldest crumb still inside the horizon needs its predecessor to anchor the
    /// leading segment: shipping only unfaded crumbs would drop that segment and make
    /// the phone's tail one crumb shorter than the PC's. Ages decrease along the list,
    /// so the shipped run is always a suffix.</summary>
    private static List<CompanionMapCrumb> BuildTrail(IReadOnlyList<LocationEvent>? trail, DateTime now)
    {
        if (trail is null || trail.Count < 2) return [];
        var horizon = TrailFade.Horizon.TotalSeconds;
        var first = -1;
        for (var i = 0; i < trail.Count; i++)
        {
            if ((now - trail[i].Time).TotalSeconds >= horizon) continue;
            first = i;
            break;
        }
        if (first < 0) return [];   // every crumb has faded out — no tail, same as the desktop

        // The anchor is the crumb before the oldest unfaded one; a wholly fresh trail
        // (first == 0) has none and needs none.
        var start = Math.Max(0, first - 1);
        var crumbs = new List<CompanionMapCrumb>(trail.Count - start);
        for (var i = start; i < trail.Count; i++)
        {
            var (x, y) = ZoneMap.FromLoc(trail[i].LocY, trail[i].LocX);
            crumbs.Add(new CompanionMapCrumb(x, y, Math.Max(0, (now - trail[i].Time).TotalSeconds)));
        }
        return crumbs;
    }

    private void EnsureGeometry(string zone)
    {
        if (zone == _geometryZone) return;
        _geometryZone = zone;
        _geometry = null;
        _missing = null;
        if (zone.Length == 0) { _missing = "Waiting for the log to say where you are."; return; }

        var folders = Folders();
        if (folders.Count == 0)
        {
            _missing = "No maps folder found — EQBuddy looks for the game's own \"maps\" folder " +
                "beside Logs (the desktop's map window has a Get maps… button).";
            return;
        }
        var file = ZoneMapFiles.Resolve(folders, zone);
        if (file is null)
        {
            // Name the exact file that would have filled this, same as the desktop:
            // a blank map must always say what's missing.
            _missing = $"No map for \"{zone}\" — {ZoneMapFiles.ExpectedShortname(zone)}.txt " +
                $"not found in {string.Join(" or ", folders)}.";
            return;
        }
        try { _geometry = Load(file); }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            _missing = $"Couldn't read {Path.GetFileName(file)}.";
        }
    }

    private static CompanionMapGeometry? Load(string file)
    {
        // Layer files ("crushbone_1.txt") carry detail the base map leaves out.
        var merged = new ZoneMap();
        foreach (var layer in ZoneMapFiles.WithLayers(file))
        {
            var part = ZoneMap.Load(layer);
            merged.Lines.AddRange(part.Lines);
            merged.Points.AddRange(part.Points);
        }
        if (merged.IsEmpty) return null;

        // ZoneMap tracks its bounds during Load; merging the lists bypassed that, so
        // they are re-derived here (the same trap the desktop map documents).
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var l in merged.Lines)
        {
            minX = Math.Min(minX, Math.Min(l.X1, l.X2)); maxX = Math.Max(maxX, Math.Max(l.X1, l.X2));
            minY = Math.Min(minY, Math.Min(l.Y1, l.Y2)); maxY = Math.Max(maxY, Math.Max(l.Y1, l.Y2));
        }
        foreach (var p in merged.Points)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }

        var truncated = false;
        var strokes = new List<CompanionMapStroke>();
        var budget = MaxSegments;
        // One stroke per color, exactly as the desktop makes one Path per color —
        // and coordinates rounded to whole map units (roughly game feet), which is
        // invisible at any zoom a tablet offers and halves the payload.
        foreach (var group in merged.Lines.GroupBy(l => MapColors.Readable(l.R, l.G, l.B)))
        {
            if (budget <= 0) { truncated = true; break; }
            var take = group.Take(budget).ToList();
            if (take.Count < group.Count()) truncated = true;
            budget -= take.Count;
            var flat = new List<int>(take.Count * 4);
            foreach (var l in take)
            {
                flat.Add((int)Math.Round(l.X1)); flat.Add((int)Math.Round(l.Y1));
                flat.Add((int)Math.Round(l.X2)); flat.Add((int)Math.Round(l.Y2));
            }
            var (r, g, b) = group.Key;
            strokes.Add(new CompanionMapStroke($"#{r:X2}{g:X2}{b:X2}", flat));
        }

        var pois = merged.Points.Select(p =>
        {
            var (r, g, b) = MapColors.Readable(p.R, p.G, p.B);
            return new CompanionMapPoi((int)Math.Round(p.X), (int)Math.Round(p.Y), $"#{r:X2}{g:X2}{b:X2}", p.Label);
        }).ToList();

        var stamp = CompanionHash.Of(
            $"{file}|{merged.Lines.Count}|{merged.Points.Count}|{minX:0}|{minY:0}|{maxX:0}|{maxY:0}");

        return new CompanionMapGeometry(
            stamp,
            (int)Math.Floor(minX), (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY),
            strokes, pois, truncated);
    }

    private List<CompanionMapCircle> BuildCircles(
        SpawnPointLedger points, string zone, IReadOnlyList<SpawnTimerState> timers, DateTime now)
    {
        // Change detection by the ledger's revision counter, not a deep clone per
        // tick — the same trick the desktop map uses.
        if (_pointsStamp != (zone, points.Revision))
        {
            _pointsStamp = (zone, points.Revision);
            _points = points.Snapshot(zone);
        }

        var circles = new List<CompanionMapCircle>(_points?.Points.Count ?? 0);
        foreach (var p in _points?.Points ?? [])
        {
            var named = points.NamedPointName(zone, p);
            // Named circles match their timer on the CANONICAL name, so a named
            // killed under a catalog alias still finds its countdown.
            var due = named is { } n
                ? timers.FirstOrDefault(t => SpawnCatalog.NameMatches(t.Name, n))?.DueAt
                : points.ProjectedRespawn(zone, p);
            var secs = due is { } d ? (d - now).TotalSeconds : (double?)null;
            var (x, y) = ZoneMap.FromLoc(p.LocY, p.LocX);
            circles.Add(new CompanionMapCircle(
                x, y,
                Named: named is not null,
                Label: named,
                Confirmed: p.Confirmed,
                DueSeconds: secs,
                Imminent: secs is { } s && s <= PulseWindowSeconds && s >= -PulseLingerSeconds,
                Projected: named is null,
                Kills: p.TotalKills(),
                Mobs: string.Join(", ", p.Mobs
                    .OrderByDescending(kv => kv.Value.Kills)
                    .Take(4)
                    .Select(kv => $"{kv.Key} ×{kv.Value.Kills}")),
                LocY: p.LocY, LocX: p.LocX));
        }
        return circles;
    }
}
