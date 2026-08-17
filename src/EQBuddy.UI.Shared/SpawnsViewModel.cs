using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One chicklet in the minimized spawn-countdown stack.</summary>
/// <summary>One row in the chip stack. Icon distinguishes chip kinds sharing the stack:
/// "⏳" spawn countdowns, "💤" active mezzes (Zone is "" for those — they belong to no
/// zone list and their due-click clears nothing).</summary>
public sealed record SpawnChip(string Zone, string Name, string CountdownText, bool IsDue, string Detail,
    string Icon = "⏳")
{
    /// <summary>Elapsed share of the countdown, 0..1 — the chip's bottom progress
    /// track (2026-08-11 modernization): a stack of chips reads as a stack of
    /// gauges. Null when no duration is known (the track hides).</summary>
    public double? Fraction { get; init; }

    /// <summary>Right-click dismisses the chip when set (David, 2026-08-13: a slow
    /// chip that isn't relevant — a bard's haste lapsing on you — must be
    /// dismissible instead of squatting for two minutes). Null = not dismissible.</summary>
    public Action? OnDismiss { get; init; }
}

/// <summary>One row in the Spawns window, ready to render.</summary>
public sealed record SpawnRow(
    string Zone,
    string Name,
    /// <summary>"Frenzied Ghoul — Placeholder (ghoul)" when a PH is known.</summary>
    string DisplayName,
    /// <summary>Editable duration as text ("22m", "3d 12h"); "" when unknown.</summary>
    string DurationText,
    /// <summary>"7:54", "due", "killed 12m ago" (no duration known), or "".</summary>
    string CountdownText,
    bool HasActiveTimer,
    bool IsDue,
    bool Alert,
    /// <summary>This named's own due sound ("" = follow shared, "Off", name, or path).</summary>
    string SoundName,
    bool IsCustom,
    /// <summary>Variance/source/note rolled into tooltip text; "" when there's nothing.</summary>
    string Detail);

/// <summary>
/// Presentation and edit logic for the Spawns window, shared by both UIs. The window
/// itself stays a thin view: rows in, user actions forwarded here, persistence handled
/// by <see cref="SpawnOverrides"/>/<see cref="SpawnTimers"/> underneath.
/// </summary>
public sealed class SpawnsViewModel
{
    private readonly SpawnCatalog _catalog;
    private readonly SpawnOverrides _overrides;
    private readonly SpawnTimers _timers;
    // Keys already seen due (or due at first glance after launch) — an alert fires only
    // on the live transition, so a restart doesn't replay every overdue camp as news.
    private readonly HashSet<string> _alerted = new(StringComparer.OrdinalIgnoreCase);
    private bool _primed;

    public SpawnsViewModel(SpawnCatalog catalog, SpawnOverrides overrides, SpawnTimers timers)
    {
        _catalog = catalog;
        _overrides = overrides;
        _timers = timers;
    }

    public IReadOnlyList<string> ZoneNames { get; private set; } = [];

    /// <summary>The zone the log says the player is in, or null before any zone line.</summary>
    public string? CurrentZoneName => _timers.CurrentZone?.Zone;

    public void RefreshZoneList() =>
        ZoneNames = _catalog.Zones.Select(z => z.Zone).OrderBy(z => z, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Rows for one zone: named with running timers first (soonest due on top),
    /// then the rest alphabetically. Custom (player-added) named merge in.</summary>
    public List<SpawnRow> RowsFor(string zoneName, DateTime now)
    {
        var zone = _catalog.FindZone(zoneName);
        if (zone is null) return [];

        var active = _timers.Snapshot(now)
            .Where(t => string.Equals(t.Zone, zone.Zone, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SpawnRow>();
        foreach (var entry in zone.Named)
            rows.Add(BuildRow(zone.Zone, entry.Name,
                _overrides.Find(zone.Zone, entry.Name), entry, zone, active, now));
        foreach (var (name, o) in _overrides.CustomFor(zone.Zone))
            rows.Add(BuildRow(zone.Zone, name, o, null, zone, active, now));

        return rows
            .OrderByDescending(r => r.HasActiveTimer)
            .ThenBy(r => active.TryGetValue(r.Name, out var t) ? t.DueAt ?? DateTime.MaxValue : DateTime.MaxValue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private SpawnRow BuildRow(string zoneName, string name, SpawnOverride? o, SpawnEntry? entry,
        SpawnZone zone, Dictionary<string, SpawnTimerState> active, DateTime now)
    {
        var placeholder = o?.Placeholder ?? entry?.Placeholder ?? "";
        var duration = o?.RespawnSeconds
            ?? (entry is not null ? SpawnCatalog.EffectiveSeconds(zone, entry) : null);

        var display = placeholder.Length > 0 ? $"{name} — Placeholder ({placeholder})" : name;

        string countdown = "";
        var hasTimer = active.TryGetValue(name, out var timer);
        var isDue = false;
        if (hasTimer && timer is not null)
        {
            if (timer.DueAt is { } due)
            {
                countdown = SpawnDurationText.Countdown(due - now);
                isDue = timer.IsDue(now);
            }
            else
                countdown = $"killed {SpawnDurationText.Format((now - timer.KilledAt).TotalSeconds)} ago";
        }
        // Raid-instance bosses without a player-typed duration run no countdown
        // (#109) — say why in place of one, instead of a blank that reads as broken.
        var suppressedInstance = entry is { RaidInstanced: true }
            && (o?.RespawnSeconds is null || o.Learned);
        if (!hasTimer && suppressedInstance) countdown = "instance";

        // "Added by you" would be a lie for a row EQBuddy discovered itself (#185) —
        // and the player has to be able to tell, because a discovery is a guess from
        // the log's own naming convention and deleting it must feel allowed.
        var detail = entry is null
            ? o is { Discovered: true }
                ? "Found by EQBuddy — the log named this mob without an article, which "
                  + "is how EverQuest writes a named. It isn't in the shipped catalog, so "
                  + "its respawn is measured from your own kills: no countdown until the "
                  + "second one. Type a duration to override it, or remove the row if it "
                  + "isn't a named."
                : "Added by you"
            : string.Join(" · ", new[]
        {
            suppressedInstance
                ? "Raid instance: a fresh instance brings its own copy (daily per "
                  + "difficulty, weekly full reset — the game's Instance Maintenance "
                  + "screen has the real clocks), so there is no kill-to-respawn camp "
                  + "timer to count down. Type a respawn time here if you want your "
                  + "own reminder anyway."
                : "",
            // "Learned" covers both sources of automatic values: re-kill gaps from
            // your own play AND an accepted zone-knowledge import — either way it
            // may keep tightening, and your typed edit always outranks it.
            o is { Learned: true } ? "timer learned automatically (your kills or an import)" : "",
            entry.Variance.Length > 0 ? $"variance {entry.Variance}" : "",
            entry.Note,
            entry.Source.Length > 0 ? $"source: {entry.Source}" : "",
        }.Where(s => s.Length > 0));

        return new SpawnRow(zoneName, name, display,
            SpawnDurationText.Format(duration), countdown,
            hasTimer, isDue, o?.Alert ?? false, o?.SoundName ?? "", o?.Custom ?? false, detail);
    }

    // ---- user actions ----

    /// <summary>Duration edited in a row. Persists as an override (catalog stays
    /// pristine) and re-derives any running countdown from its original kill time.</summary>
    public void SetDuration(string zone, string name, string durationText)
    {
        var seconds = SpawnDurationText.Parse(durationText);
        var o = _overrides.GetOrAdd(zone, name);
        o.RespawnSeconds = seconds;
        o.Learned = false;   // typed by the player — outranks inference, forever
        _overrides.Save();
        _timers.SetDuration(zone, name, seconds);
    }

    public void ToggleAlert(string zone, string name)
    {
        var o = _overrides.GetOrAdd(zone, name);
        o.Alert = !o.Alert;
        _overrides.Save();
    }

    /// <summary>Set this named's own due sound: "" = follow the Options alert sound,
    /// "Off" = silent, else a built-in name or custom file path. Picking a concrete
    /// sound also flips the bell on — choosing "Alarm" for a camp IS opting in to
    /// hearing it, and making someone find the bell separately reads as broken.</summary>
    public void SetSound(string zone, string name, string soundNameOrPath)
    {
        var o = _overrides.GetOrAdd(zone, name);
        o.SoundName = soundNameOrPath;
        if (soundNameOrPath.Length > 0
            && !string.Equals(soundNameOrPath, "Off", StringComparison.OrdinalIgnoreCase))
            o.Alert = true;
        _overrides.Save();
    }

    /// <summary>The sound to actually play when this named comes due, or null for
    /// silence. "Default" maps to <see cref="SpawnOverride.DefaultSound"/> (Alarm) —
    /// spawns deliberately do NOT follow the Options alert sound; a camp popping
    /// deserves a louder default than a loot ding (David's call).</summary>
    public string? SoundFor(string zone, string name)
    {
        var own = _overrides.Find(zone, name)?.SoundName ?? "";
        var choice = own.Length > 0 ? own : SpawnOverride.DefaultSound;
        return string.Equals(choice, "Off", StringComparison.OrdinalIgnoreCase) ? null : choice;
    }

    /// <summary>▶ — the player marks the kill themselves; <paramref name="agoText"/>
    /// covers "died 5m ago" and uses the same syntax as durations.</summary>
    public void StartNow(string zone, string name, string agoText = "")
    {
        var elapsed = SpawnDurationText.Parse(agoText) is { } ago
            ? TimeSpan.FromSeconds(ago) : TimeSpan.Zero;
        var zoneEntry = _catalog.FindZone(zone);
        var entry = zoneEntry?.Named.FirstOrDefault(n =>
            string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
        var duration = _overrides.Find(zone, name)?.RespawnSeconds
            ?? (entry is not null && zoneEntry is not null
                ? SpawnCatalog.EffectiveSeconds(zoneEntry, entry) : null);
        _timers.StartManual(zone, name, duration, elapsed);
    }

    public void ClearTimer(string zone, string name) => _timers.Clear(zone, name);

    /// <summary>Player-added named for zones (or spawns) the catalog doesn't know.</summary>
    public bool AddCustom(string zone, string name, string durationText)
    {
        name = name.Trim();
        if (name.Length == 0) return false;
        var zoneEntry = _catalog.FindZone(zone);
        if (zoneEntry is not null && zoneEntry.Named.Any(n =>
                string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)))
            return false; // already in the catalog — edit that row instead
        var o = _overrides.GetOrAdd(zone, name);
        o.Custom = true;
        o.RespawnSeconds = SpawnDurationText.Parse(durationText);
        _overrides.Save();
        return true;
    }

    public void RemoveCustom(string zone, string name)
    {
        _overrides.Remove(zone, name);
        _overrides.Save();
        _timers.Clear(zone, name);
    }

    // Timer starts the window-popping caller has already reacted to. Unlike _alerted
    // there is no startup priming: countdowns recovered from the log SHOULD pop the
    // window at launch — they are exactly "a named has been killed".
    private readonly HashSet<string> _poppedTimers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Timers not yet reported to the caller — new kills, and recovered
    /// countdowns on the first call after launch. Drives the pop-on-kill window: a
    /// re-kill restarts a timer with a new kill time and pops again; an unchanged
    /// timer never re-pops.</summary>
    public List<SpawnTimerState> ConsumeNewTimers(DateTime now)
    {
        var fresh = new List<SpawnTimerState>();
        foreach (var t in _timers.Snapshot(now))
            if (_poppedTimers.Add($"{t.Zone}|{t.Name}|{t.KilledAt:O}"))
                fresh.Add(t);
        return fresh;
    }

    /// <summary>Any countdown still running (or lingering as due) for this server.</summary>
    public bool HasActiveTimers(DateTime now) => _timers.Snapshot(now).Count > 0;

    /// <summary>One chicklet per running countdown, every zone on this server, soonest
    /// first. The minimized face of the feature: name + countdown, nothing else — the
    /// full zone list is a double-click away.</summary>
    public List<SpawnChip> Chips(DateTime now)
    {
        return _timers.Snapshot(now).Select(t =>
        {
            var text = t.DueAt is { } due
                ? SpawnDurationText.Countdown(due - now)
                : $"killed {SpawnDurationText.Format((now - t.KilledAt).TotalSeconds)} ago";
            var detail = t.DueAt is { } d
                ? $"{t.Zone} — due {d:t}. Double-click for the zone list; click a due chip to dismiss it."
                : $"{t.Zone} — no respawn time known (set one in the zone list). Double-click to open it.";
            return new SpawnChip(t.Zone, t.Name, text, t.IsDue(now), detail)
            {
                Fraction = t is { DueAt: { } dueAt, DurationSeconds: { } dur } && dur > 0
                    ? Math.Clamp(1 - (dueAt - now).TotalSeconds / dur, 0, 1)
                    : null,
            };
        }).ToList();
    }

    /// <summary>Timers that crossed into "due" since the last call, for rows whose alert
    /// toggle is on. The first call after launch primes silently: a camp that came due
    /// while the app was closed shows as due but doesn't re-alert on startup.</summary>
    public List<SpawnTimerState> ConsumeDueAlerts(DateTime now)
    {
        var due = _timers.Snapshot(now).Where(t => t.IsDue(now)).ToList();
        var fresh = new List<SpawnTimerState>();
        foreach (var t in due)
        {
            var key = $"{t.Zone}|{t.Name}|{t.KilledAt:O}";
            if (!_alerted.Add(key)) continue;
            if (_primed && (_overrides.Find(t.Zone, t.Name)?.Alert ?? false))
                fresh.Add(t);
        }
        _primed = true;
        return fresh;
    }
}
