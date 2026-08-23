using System.Collections.Concurrent;
using System.Security.Cryptography;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>
/// Where the desktop's live state comes from, as callbacks rather than references, so
/// the host can ask for a surface's data ONLY when that surface is offered and a device
/// is actually connected. A null member is a surface this host simply can't serve —
/// the section comes back empty rather than the app growing a second data path.
/// </summary>
public sealed record CompanionSources
{
    /// <summary>The catalog zone timers and the spawn archive live under ("Befallen"),
    /// which is not always the log's zone name ("Befallen 4 (Refined)").</summary>
    public Func<string>? TimerZone { get; init; }
    public SpawnPointLedger? SpawnPoints { get; init; }
    public Func<DateTime, IReadOnlyList<MezState>>? Mezzes { get; init; }
    public Func<DateTime, IReadOnlyList<(string Class, IReadOnlyList<BuffSetEntryState> Entries)>>? BuffSets { get; init; }
    public Func<IReadOnlyList<BuffLossEntry>>? BuffLosses { get; init; }
    /// <summary>Zone → hops from here, for the gear checklist's by-zone view.</summary>
    public Func<string, int?>? HopsFromHere { get; init; }
    /// <summary>The Progress theme's Experience state, as one record rather than a
    /// widening tuple — it went from two members to four the day the next-level preview
    /// reached the phone, and a positional tuple is exactly where a caller silently swaps
    /// two lists of strings.</summary>
    public Func<CompanionProgressState>? Progress { get; init; }

    /// <summary>This character's raid clears — the Progress theme's Raids tab
    /// (docs/Themes.md). Wired in the same change as the desktop fold: a surface that
    /// exists on two screens gets one decision, and the two days EQBuddy Mobile spent
    /// ahead of the desktop on #210 is the reason that rule is written down.</summary>
    public RaidKillLedger? Raids { get; init; }

    /// <summary>Where a running timer's named camps, and whether that came from the
    /// wiki rather than your own /loc at kill time. The desktop owns this because the
    /// wiki fallback runs through its memoized, polite lookup — the map window asks the
    /// same question the same way.</summary>
    public Func<SpawnTimerState, (double Y, double X, bool FromWiki)?>? CampFor { get; init; }

    /// <summary>The quest surface's per-tick bundle: catalog + this character's
    /// ledger slice. Asked only while the surface is offered and a device is paired.</summary>
    public Func<CompanionQuestRequest>? Quests { get; init; }

    /// <summary>The ledger a device's quest taps (pin, class picker) land on — the
    /// same store the desktop quest window writes.</summary>
    public QuestLedgerStore? QuestLedger { get; init; }
    public Func<string>? QuestCharacterKey { get; init; }
}

/// <summary>
/// The desktop-side lifecycle glue, UI-toolkit-free so WPF and Avalonia hosts share
/// it: owns the server per AppSettings (enabled/port/token/surface gate), and turns
/// the app's once-a-second tick into pushes — only when something moved, only for the
/// devices whose own screens moved, and only when a phone is actually connected. With
/// the feature off or nobody paired, Tick is two field reads.
/// </summary>
public sealed class CompanionHost : IDisposable
{
    /// <summary>Belt-and-braces refresh: even with unchanged fingerprints, connected
    /// phones get a full snapshot this often so slow-drifting numbers (xp/hr, session
    /// length) never stagnate through a quiet camp.</summary>
    private static readonly TimeSpan ForcedPushInterval = TimeSpan.FromSeconds(30);

    private readonly AppSettings _settings;
    private readonly string _appVersion;
    private readonly CompanionSources _sources;
    private readonly CompanionMapSource _maps;
    private readonly ConcurrentQueue<CompanionAction> _actions = new();
    private readonly ConcurrentQueue<CompanionMapAction> _mapActions = new();
    private CompanionServer? _server;
    private CompanionThemeSection? _theme;
    /// <summary>The searchable quest index, built once from the immutable catalog on
    /// the first offered tick and handed to every projection by reference.</summary>
    private CompanionQuestCatalog? _questIndex;
    private Dictionary<string, string> _lastSections = [];
    private DateTime _lastPush = DateTime.MinValue;

    public CompanionHost(AppSettings settings, string appVersion, CompanionSources? sources = null)
    {
        _settings = settings;
        _appVersion = appVersion;
        _sources = sources ?? new CompanionSources();
        _maps = new CompanionMapSource(settings);
        // The phone follows the desktop's theme from its very first frame; the WPF app
        // pushes swaps in afterwards via SetTheme (ThemeManager.PaletteApplied).
        SetTheme(settings.Theme, CustomTheme.PaletteFor(settings));
        MigrateQuestGate(settings);
        if (settings.CompanionEnabled) Start();
    }

    /// <summary>The separate Epic/Sky surfaces folded into "quests". An owner who had
    /// gated BOTH off had said quest data stays home, so the merged surface starts
    /// gated too; either way the stale names leave the hidden-list, because entries
    /// matching no known surface are invisible in Options and would sit there forever.</summary>
    private static void MigrateQuestGate(AppSettings settings)
    {
        var hidden = settings.CompanionHiddenSurfaces;
        var hadEpics = hidden.Contains(CompanionSurfaces.Epics, StringComparer.OrdinalIgnoreCase);
        var hadSky = hidden.Contains(CompanionSurfaces.Sky, StringComparer.OrdinalIgnoreCase);
        if (!hadEpics && !hadSky) return;
        if (hadEpics && hadSky && !hidden.Contains(CompanionSurfaces.Quests, StringComparer.OrdinalIgnoreCase))
            hidden.Add(CompanionSurfaces.Quests);
        hidden.RemoveAll(s => s.Equals(CompanionSurfaces.Epics, StringComparison.OrdinalIgnoreCase)
                           || s.Equals(CompanionSurfaces.Sky, StringComparison.OrdinalIgnoreCase));
        settings.Save();
    }

    public bool Running => _server is not null;
    public int ClientCount => _server?.ClientCount ?? 0;

    /// <summary>Is anyone actually looking? The mobile pump asks this before doing any
    /// work at all, so an unpaired EQBuddy pays one field read per pump and nothing
    /// else — the same "zero cost while idle" contract <see cref="Tick"/> keeps.</summary>
    public bool HasClients => _server is { ClientCount: > 0 };

    /// <summary>Why the last Start failed (port in use, no permission), for the
    /// pairing window to show honestly; null when all is well.</summary>
    public string? LastError { get; private set; }

    /// <summary>Something worth SAYING that is not a failure — today, only "the port you
    /// asked for was refused so I took another one". Separate from LastError because the
    /// pairing UI paints that in red and asks the player to act, and this needs neither.</summary>
    public string? Notice { get; private set; }

    /// <summary>Raised (possibly on a worker thread) when a phone connects/drops.</summary>
    public event Action? ClientsChanged;

    /// <summary>Raised on the TICK thread after a device's checklist tap was applied
    /// and saved, naming the surface — the desktop's cue to repaint that card.</summary>
    public event Action<string>? SurfaceEdited;

    /// <summary>The address to pair against: http://ip:port/#token. Null while stopped.
    /// The token travels in the FRAGMENT, so it never appears in an HTTP request line —
    /// only the page's JS reads it and presents it on the WebSocket connect.</summary>
    public string? PairingUrl =>
        _server is { BoundAddresses.Count: > 0 } s
            ? $"http://{s.BoundAddresses[0]}:{s.Port}/#{_settings.CompanionToken}"
            : null;

    /// <summary>The desktop gate: every known surface minus the ones the owner
    /// unticked (persisted as the hidden-list, same idiom as HiddenSections).</summary>
    public IReadOnlyList<string> OfferedSurfaces =>
        CompanionSurfaces.All
            .Where(s => !_settings.CompanionHiddenSurfaces.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <summary>The desktop's palette changed — repaint every paired device without a
    /// reconnect. Cheap and idempotent: an unchanged palette produces the same stamp
    /// and is never re-sent.</summary>
    public void SetTheme(string themeKey, IEnumerable<(string Key, string Hex)> palette)
    {
        try { _theme = CompanionTheme.Project(themeKey, palette); }
        catch (Exception ex) { CoreLog.Error(ex); }   // a bad palette must not stop the app
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _settings.CompanionEnabled && enabled == Running) return;
        _settings.CompanionEnabled = enabled;
        _settings.Save();
        if (enabled) Start(); else Stop();
    }

    /// <summary>New token, old phones revoked: the server restarts under the new
    /// token, which drops every open connection and refuses their reconnects.</summary>
    public void RegenerateToken()
    {
        _settings.CompanionToken = MintToken();
        _settings.Save();
        if (Running) { Stop(); Start(); }
    }

    /// <summary>Flip one surface in the desktop gate; connected phones learn on the
    /// next tick (the offer list is part of the envelope's fingerprint).</summary>
    public void SetSurfaceOffered(string surface, bool offered)
    {
        var hidden = _settings.CompanionHiddenSurfaces;
        var present = hidden.Contains(surface, StringComparer.OrdinalIgnoreCase);
        if (offered && present) hidden.RemoveAll(s => string.Equals(s, surface, StringComparison.OrdinalIgnoreCase));
        else if (!offered && !present) hidden.Add(surface);
        else return;
        _settings.Save();
    }

    private void Start()
    {
        // Reached only when the player has turned EQBuddy Mobile on: the launch gate
        // that used to stand here is gone (2026-08-14), and CompanionEnabled — off in a
        // fresh settings file — is now the only thing between a dormant feature and a
        // listening port.
        LastError = null;
        Notice = null;
        _settings.CompanionToken ??= MintToken();

        var wanted = _settings.CompanionPort;
        if (TryListen(wanted)) { _lastSections = []; return; }

        // A TAKEN PORT IS NOT A DEAD END, since 2026-08-21.
        //
        // David ticked Enable and got "Couldn't listen on port 47998 ... Change the port and
        // try again." Nothing on his machine owned that port in ANY table — not netstat in
        // any state, not an HTTP.sys reservation, not the excluded-port ranges — and yet no
        // process could bind it on any address, 0.0.0.0 included. A kernel-level reservation
        // from a driver or tunnel: nothing EQBuddy can detect, argue with, or fix.
        //
        // Worse, "change the port" named an action the app does not offer: CompanionPort is
        // in no Options screen and no dialog. That is a surface naming a task and handing
        // over no way to do it, which is the defect this project has now paid for three
        // times (the Gear tab, the wiki-pack Step 2 click, and this).
        //
        // So: ask the OS for one it WILL give us. The pairing URL carries the port anyway -
        // a phone scans whatever it is handed — so a fixed number was only ever buying a
        // stable QR between sessions, which is worth much less than the feature working.
        if (TryListen(0))
        {
            var got = _server!.Port;
            _settings.CompanionPort = got;
            _settings.Save();
            Notice = $"Port {wanted} was refused by Windows, so EQBuddy Mobile is on "
                   + $"port {got} instead. Nothing to do — the code below already has it.";
            _lastSections = [];
            return;
        }

        LastError = $"Couldn't listen on port {wanted}, or on any port Windows offered "
                  + $"({_lastListenError}). If you use a VPN or virtual-machine networking, "
                  + "it may be holding the range.";
        _lastSections = [];
    }

    /// <summary>One bind attempt. Returns false and remembers why rather than throwing, so
    /// Start can try again on a port the OS chooses.</summary>
    private bool TryListen(int port)
    {
        try
        {
            var server = new CompanionServer(new CompanionServerOptions
            {
                Token = _settings.CompanionToken,
                Port = port,
            });
            server.ClientsChanged += () => ClientsChanged?.Invoke();
            server.ActionReceived += action => _actions.Enqueue(action);
            server.MapActionReceived += action => _mapActions.Enqueue(action);
            server.Start();
            _server = server;
            return true;
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            _lastListenError = ex.Message;
            return false;
        }
    }

    private string _lastListenError = "";

    private void Stop()
    {
        _server?.Dispose();
        _server = null;
    }

    /// <summary>Once-a-second feed from the app's existing UI tick, handing over the
    /// SAME shared snapshot the desktop cards render from (the perf pass's rule: one
    /// snapshot per tick, no extras built for the companion). Takes SpawnTimers
    /// itself, not a list, so its Snapshot() isn't even taken while nobody's paired,
    /// and every other source is a callback asked only for offered surfaces.</summary>
    public void Tick(StatsSnapshot? stats, SpawnTimers spawnTimers, string character, DateTime now)
    {
        // Taps a device made before it wandered off still land: draining is a field
        // read when the queue is empty, which is every tick but a handful.
        if (!_actions.IsEmpty) DrainActions();
        if (!_mapActions.IsEmpty) DrainMapActions();
        if (_server is not { ClientCount: > 0 } server) return; // zero cost while idle

        var offered = OfferedSurfaces;
        bool On(string surface) => offered.Contains(surface, StringComparer.OrdinalIgnoreCase);

        var wantTimers = On(CompanionSurfaces.Spawns) || On(CompanionSurfaces.Map);
        var timers = wantTimers ? spawnTimers.Snapshot(now) : [];
        var timerZone = _sources.TimerZone?.Invoke() ?? stats?.CurrentZone ?? "";
        var progress = On(CompanionSurfaces.Progress) ? _sources.Progress?.Invoke() : null;
        var quests = On(CompanionSurfaces.Quests) ? _sources.Quests?.Invoke() : null;
        if (quests?.Catalog is { } questCatalog)
            _questIndex ??= CompanionQuestIndex.Build(questCatalog);

        var input = new CompanionInputs
        {
            Character = character,
            AppVersion = _appVersion,
            Offered = offered,
            Stats = stats,
            Theme = _theme,
            Settings = _settings,
            Timers = On(CompanionSurfaces.Spawns) ? timers : [],
            Mezzes = On(CompanionSurfaces.Mez) ? _sources.Mezzes?.Invoke(now) ?? [] : [],
            BuffSets = On(CompanionSurfaces.Buffs) ? _sources.BuffSets?.Invoke(now) ?? [] : [],
            BuffLosses = On(CompanionSurfaces.Buffs) ? _sources.BuffLosses?.Invoke() ?? [] : [],
            HopsFromHere = _sources.HopsFromHere,
            Map = On(CompanionSurfaces.Map)
                ? _maps.Build(new CompanionMapRequest
                {
                    MapZone = stats?.CurrentZone ?? "",
                    TimerZone = timerZone,
                    Points = _sources.SpawnPoints,
                    Timers = timers
                        .Where(t => string.Equals(t.Zone, timerZone, StringComparison.OrdinalIgnoreCase))
                        .ToList(),
                    Location = stats?.LastLocation,
                    Trail = stats?.LocationTrail,
                    CampFor = _sources.CampFor,
                }, now)
                : null,
            Level = progress?.Level,
            Unlocks = progress?.Unlocks,
            UnlockClasses = progress?.Classes ?? [],
            NextUnlocks = progress?.Next,
            Raids = On(CompanionSurfaces.Progress) ? _sources.Raids : null,
            Quests = quests,
            QuestIndex = quests is null ? null : _questIndex,
        };

        var snap = CompanionProjection.Build(input, now);
        var sections = CompanionProjection.SectionFingerprints(snap);
        var changed = Changed(sections);
        var forced = now - _lastPush >= ForcedPushInterval;
        if (changed.Count == 0 && !forced) return;

        _lastSections = sections;
        _lastPush = now;
        server.Publish(snap, forced ? null : changed);
    }

    /// <summary>Which sections moved since the last pass. A section that appeared or
    /// vanished (the owner flipping the gate) counts as moved.</summary>
    private HashSet<string> Changed(Dictionary<string, string> sections)
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, print) in sections)
            if (!_lastSections.TryGetValue(name, out var was) || was != print)
                changed.Add(name);
        foreach (var name in _lastSections.Keys)
            if (!sections.ContainsKey(name))
                changed.Add(name);
        return changed;
    }

    /// <summary>Apply the taps devices made. On the tick thread, so the settings lists
    /// are touched exactly where the desktop's own toggles touch them; one Save for
    /// the whole batch, and one repaint cue per surface.</summary>
    private void DrainActions()
    {
        var edited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settingsTouched = false;
        while (_actions.TryDequeue(out var action))
        {
            try
            {
                // General-quest taps (pin, class picker) write the per-character
                // ledger, which persists itself; everything else writes settings.
                if (string.Equals(action.Surface, CompanionSurfaces.Quests, StringComparison.OrdinalIgnoreCase))
                {
                    if (_sources.QuestLedger is { } ledger
                        && _sources.QuestCharacterKey?.Invoke() is { Length: > 0 } key
                        && CompanionActions.Apply(ledger, key, action))
                        edited.Add(action.Surface);
                }
                else if (CompanionActions.Apply(_settings, action))
                {
                    edited.Add(action.Surface);
                    settingsTouched = true;
                }
            }
            catch (Exception ex) { CoreLog.Error(ex); }
        }
        if (edited.Count == 0) return;
        if (settingsTouched) _settings.Save();
        foreach (var surface in edited) SurfaceEdited?.Invoke(surface);
    }

    /// <summary>Apply the spawn-point curation devices asked for. On the tick thread,
    /// the same place the desktop map's own right-click lands, so the ledger is never
    /// edited underneath a card that is mid-render.
    ///
    /// Every edit answers — including the ones that changed nothing. A tap that goes
    /// quiet is indistinguishable from a tap that didn't work, and the map's own
    /// repaint can't tell the difference between "removed" and "wasn't there".</summary>
    private void DrainMapActions()
    {
        var touched = false;
        while (_mapActions.TryDequeue(out var action))
        {
            try
            {
                if (_sources.SpawnPoints is not { } points)
                {
                    _server?.Notify("This copy of EQBuddy isn't tracking spawn points, so there was nothing to edit.");
                    continue;
                }
                if (CompanionActions.Apply(points, action) is { } said)
                {
                    _server?.Notify(said);
                    touched = true;
                }
            }
            catch (Exception ex)
            {
                CoreLog.Error(ex);
                _server?.Notify("That edit hit an error on the PC and was not applied.");
            }
        }
        // The ledger owns its own persistence; what the desktop needs is the cue to
        // repaint the map card, exactly as a checklist tap gets one.
        if (touched) SurfaceEdited?.Invoke(CompanionSurfaces.Map);
    }

    /// <summary>128 crypto-random bits as lowercase hex — long enough that guessing
    /// races the per-IP rate limiter for longer than the universe cares to wait,
    /// short enough to keep the QR at version ≤ 4.</summary>
    private static string MintToken() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public void Dispose() => Stop();
}
