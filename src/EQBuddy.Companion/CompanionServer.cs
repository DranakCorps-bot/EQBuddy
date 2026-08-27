using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using EQBuddy.Core;

namespace EQBuddy.Companion;

public sealed class CompanionServerOptions
{
    /// <summary>The pairing token every WebSocket connect must present (?token=…).
    /// Minted by CompanionHost; regenerating revokes every paired phone.</summary>
    public required string Token { get; init; }

    /// <summary>0 = ephemeral (tests); the app uses AppSettings.CompanionPort.</summary>
    public int Port { get; init; }

    /// <summary>Explicit bind addresses (tests bind loopback); null = the machine's
    /// LAN IPv4s via <see cref="CompanionServer.LanAddresses"/>.</summary>
    public IReadOnlyList<IPAddress>? Addresses { get; init; }

    /// <summary>Application-level keepalive cadence. The page treats ANY message as
    /// freshness (browsers can't observe WS ping frames), so this bounds how stale a
    /// healthy connection ever looks. Tests shrink it.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// The EQBuddy Mobile listener: a deliberately tiny HTTP/1.1 + RFC 6455 WebSocket
/// server over raw TcpListener. Two routes only — GET / serves the embedded phone
/// page (which shows nothing but pairing instructions until its JS presents the
/// token), and GET /ws?token=… upgrades to a WebSocket that streams
/// CompanionSnapshot JSON. Everything else is 404.
///
/// Trust model (see SECURITY.md "EQBuddy Mobile"): binds LAN addresses only, never
/// 0.0.0.0 unless the caller says so; every WS connect must carry the pairing token
/// (constant-time compare); failed attempts are rate-limited per IP; the unauthed
/// HTTP surface contains no player data at all.
///
/// FIREWALL REALITY (spike finding, documented for the UI): binding a LAN address
/// needs no elevation, but Windows Defender Firewall blocks INBOUND connections to
/// EQBuddy.exe until the user allows it — normally via the "Windows has blocked some
/// features" prompt that appears on the first non-loopback listen. If that prompt is
/// dismissed/canceled, the phone's connect just times out with no error on the PC
/// side, so the pairing UI must say this out loud ("if the page never loads, check
/// the firewall / try re-enabling"). Router/AP client isolation (common on guest
/// Wi-Fi) fails the same silent way. Phase 2: the installer can add the firewall
/// rule; the spike deliberately makes no netsh/elevation calls.
/// </summary>
public sealed class CompanionServer : IDisposable
{
    private const int MaxRequestHeaderBytes = 8192;
    private const int MaxConcurrentConnections = 32;
    private const int AuthFailureLimit = 5;
    private static readonly TimeSpan AuthFailureWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HeaderReadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    private readonly CompanionServerOptions _options;
    private readonly byte[] _tokenUtf8;
    private readonly List<TcpListener> _listeners = [];
    private readonly ConcurrentDictionary<Guid, WsClient> _clients = new();
    private readonly ConcurrentDictionary<IPAddress, (int Count, DateTime WindowStart)> _authFailures = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _connectionGate = new(MaxConcurrentConnections);
    private CompanionSnapshot? _latest;
    private bool _started;

    public CompanionServer(CompanionServerOptions options)
    {
        _options = options;
        _tokenUtf8 = Encoding.UTF8.GetBytes(options.Token);
    }

    /// <summary>Actual bound port (meaningful after Start; resolves Port 0).</summary>
    public int Port { get; private set; }

    /// <summary>Addresses actually listening — the pairing UI picks its URL from these.</summary>
    public IReadOnlyList<IPAddress> BoundAddresses { get; private set; } = [];

    public int ClientCount => _clients.Count;

    /// <summary>Raised (on a worker thread) when a phone connects or drops.</summary>
    public event Action? ClientsChanged;

    /// <summary>Raised (on a socket thread) when a device ticks a checklist row. The
    /// host queues it and applies it on the desktop's own tick — nothing here touches
    /// settings while the UI is reading them.</summary>
    public event Action<CompanionAction>? ActionReceived;

    /// <summary>Raised (on a socket thread) when a device curates a spawn point. Same
    /// contract as <see cref="ActionReceived"/>: the host queues it and applies it on
    /// the desktop's own tick, never on this thread.</summary>
    public event Action<CompanionMapAction>? MapActionReceived;

    /// <summary>Raised when a device picks (or clears) a Path-tab destination (World
    /// PR 4). Same queue-and-apply-on-tick contract as the two above.</summary>
    public event Action<CompanionTravelAction>? TravelActionReceived;

    /// <summary>Raised when a device taps "Drop camp marker" (World PR 4) — the same
    /// action the desktop's World window chrome offers on every tab. No payload: the
    /// marker is dropped at wherever the log currently says the player is, exactly as
    /// the desktop button does.</summary>
    public event Action? MarkerDropReceived;

    /// <summary>The machine's LAN IPv4s: up interfaces, skipping loopback and
    /// link-local (169.254 — an address that means "no network"). Ordered by
    /// <see cref="LanAddressRank"/>, so BoundAddresses[0] is the one to print on the QR.
    ///
    /// <para>Ordering used to be "private first", which ranked a Hyper-V/VirtualBox host
    /// adapter equal to the real LAN and let NIC enumeration order decide the QR — a
    /// scan that silently reaches nothing (David, 2026-08-15). A default gateway is what
    /// separates them, so the ranking asks for one.</para></summary>
    public static IReadOnlyList<IPAddress> LanAddresses()
    {
        var result = new List<(IPAddress Address, int Score)>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var props = nic.GetIPProperties();
                // A gateway of 0.0.0.0 is a placeholder some adapters report; it routes
                // nowhere, so it must not count as one.
                var hasGateway = props.GatewayAddresses
                    .Any(g => g.Address is { } a
                        && a.AddressFamily == AddressFamily.InterNetwork
                        && !a.Equals(IPAddress.Any));
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var bytes = addr.Address.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254) continue;
                    result.Add((addr.Address,
                        LanAddressRank.Score(addr.Address.ToString(), hasGateway, nic.Description)));
                }
            }
        }
        catch (Exception ex) { CoreLog.Error(ex); }
        // OrderBy is stable, so equally-ranked addresses keep enumeration order.
        return [.. result.OrderBy(r => r.Score).Select(r => r.Address)];
    }

    /// <summary>Bind and begin accepting. Throws SocketException if the port is taken
    /// (the host catches it and surfaces the failure in the pairing UI).</summary>
    public void Start()
    {
        if (_started) throw new InvalidOperationException("Already started.");
        _started = true;

        var addresses = _options.Addresses ?? LanAddresses();
        // No LAN at all (airplane mode, cable out): bind loopback so the feature is
        // at least testable on the PC's own browser; the UI explains why no QR works.
        if (addresses.Count == 0) addresses = [IPAddress.Loopback];

        var port = _options.Port;
        var bound = new List<IPAddress>();
        foreach (var addr in addresses)
        {
            var listener = new TcpListener(addr, port);
            try { listener.Start(); }
            catch (SocketException ex)
            {
                if (bound.Count > 0) { CoreLog.Error(ex); continue; } // partial bind is livable
                foreach (var l in _listeners) l.Stop();
                throw;
            }
            if (port == 0) port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _listeners.Add(listener);
            bound.Add(addr);
            _ = AcceptLoopAsync(listener, _cts.Token);
        }
        Port = port;
        BoundAddresses = bound;
        _ = HeartbeatLoopAsync(_cts.Token);
    }

    /// <summary>Feed the latest full offered snapshot. Cheap when idle: the host
    /// already skips the projection when no client is connected; here we remember it
    /// for the next connect and send each connected phone ONLY the sections it
    /// subscribed to (see <see cref="CompanionSnapshot.ForSubscription"/>) — a phone
    /// showing just spawn timers never receives session payloads.
    ///
    /// <paramref name="changed"/> is the per-section change set from the host: a
    /// device is woken only when one of ITS surfaces moved, so a mez-only phone sleeps
    /// through a loot line and a loot phone sleeps through a mez landing. Null means
    /// "tell everyone" — the periodic full refresh, and the first push after a
    /// connect.</summary>
    public void Publish(CompanionSnapshot snapshot, IReadOnlySet<string>? changed = null)
    {
        _latest = snapshot;
        foreach (var client in _clients.Values)
        {
            if (!Wants(client.Subscriptions, changed)) continue;
            _ = SendTextAsync(client, ForClient(client, snapshot));
        }
    }

    /// <summary>Tell every connected device what an edit did. Broadcast rather than
    /// answered to the sender alone, and deliberately: two devices can be showing the
    /// same map, and the one that didn't do the removing still deserves to know why a
    /// dot vanished under it. Text only — a notice carries no player data the device
    /// wasn't already being sent.</summary>
    public void Notify(string text)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new { kind = "notice", text }, CompanionSnapshot.JsonOpts);
        foreach (var client in _clients.Values) _ = SendTextAsync(client, json);
    }

    /// <summary>Does this device care about anything in the change set? A device that
    /// hasn't picked yet (null subscription) takes everything.</summary>
    private static bool Wants(IReadOnlyList<string>? subscriptions, IReadOnlySet<string>? changed)
    {
        if (changed is null) return true;
        if (changed.Count == 0) return false;
        if (changed.Contains(CompanionProjection.EnvelopeSection)) return true;
        if (subscriptions is null) return true;
        foreach (var surface in subscriptions)
            if (changed.Contains(surface)) return true;
        return false;
    }

    /// <summary>Filter for one device and advance what it now holds. Serialized on the
    /// client's own lock: the tick thread and the read loop (a fresh subscribe) can
    /// both land here, and the sticky-payload bookkeeping must not interleave.</summary>
    private static string ForClient(WsClient client, CompanionSnapshot snapshot)
    {
        lock (client.StateLock)
            return snapshot.ForClient(client.Subscriptions, client.State).ToJson();
    }

    public void Dispose()
    {
        _cts.Cancel();
        foreach (var l in _listeners) { try { l.Stop(); } catch { /* closing */ } }
        foreach (var c in _clients.Values) c.Tcp.Close();
        _clients.Clear();
        _cts.Dispose();
    }

    // ================= accept / HTTP =================

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try { tcp = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            _ = HandleConnectionAsync(tcp, ct);
        }
    }

    private async Task HandleConnectionAsync(TcpClient tcp, CancellationToken ct)
    {
        if (!await _connectionGate.WaitAsync(0, ct).ConfigureAwait(false)) { tcp.Close(); return; }
        try
        {
            tcp.NoDelay = true;
            var stream = tcp.GetStream();
            var request = await ReadRequestAsync(stream, ct).ConfigureAwait(false);
            if (request is null) { tcp.Close(); return; }
            var (method, path, query, headers) = request.Value;

            if (method != "GET")
            {
                await WriteSimpleAsync(stream, "405 Method Not Allowed", "text/plain", "GET only.", ct).ConfigureAwait(false);
                tcp.Close(); return;
            }

            if (path is "/" or "/index.html")
            {
                await WriteSimpleAsync(stream, "200 OK", "text/html; charset=utf-8", PhonePage.Html, ct).ConfigureAwait(false);
                tcp.Close(); return;
            }

            // The two static crumbs "Add to Home Screen" needs — a chrome-free window
            // is the durable answer to "make it full screen" (and the only reliable
            // one on iOS). Neither carries player data or the pairing token.
            if (path == "/manifest.webmanifest")
            {
                await WriteSimpleAsync(stream, "200 OK", "application/manifest+json; charset=utf-8",
                    PhonePage.Manifest, ct).ConfigureAwait(false);
                tcp.Close(); return;
            }

            if (path == "/icon.png")
            {
                await WriteBytesAsync(stream, "200 OK", "image/png", PhonePage.Icon, ct).ConfigureAwait(false);
                tcp.Close(); return;
            }

            if (path == "/ws")
            {
                await HandleWebSocketAsync(tcp, stream, query, headers, ct).ConfigureAwait(false);
                return;
            }

            await WriteSimpleAsync(stream, "404 Not Found", "text/plain", "Not here.", ct).ConfigureAwait(false);
            tcp.Close();
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            tcp.Close(); // phones sleep mid-request; that's weather, not an error
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            tcp.Close();
        }
        finally { _connectionGate.Release(); }
    }

    private static async Task<(string Method, string Path, string Query, Dictionary<string, string> Headers)?>
        ReadRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HeaderReadTimeout);

        var buffer = new byte[MaxRequestHeaderBytes];
        var used = 0;
        int headerEnd;
        while (true)
        {
            headerEnd = FindHeaderEnd(buffer, used);
            if (headerEnd >= 0) break;
            if (used == buffer.Length) return null; // header too big — drop it
            var n = await stream.ReadAsync(buffer.AsMemory(used), timeout.Token).ConfigureAwait(false);
            if (n == 0) return null;
            used += n;
        }

        var text = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        var lines = text.Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length != 3) return null;

        var target = parts[1];
        var qIndex = target.IndexOf('?');
        var path = qIndex < 0 ? target : target[..qIndex];
        var query = qIndex < 0 ? "" : target[(qIndex + 1)..];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return (parts[0], path, query, headers);
    }

    private static int FindHeaderEnd(byte[] buffer, int used)
    {
        for (var i = 3; i < used; i++)
            if (buffer[i] == '\n' && buffer[i - 1] == '\r' && buffer[i - 2] == '\n' && buffer[i - 3] == '\r')
                return i + 1;
        return -1;
    }

    private static Task WriteSimpleAsync(Stream stream, string status, string contentType, string body, CancellationToken ct) =>
        WriteBytesAsync(stream, status, contentType, Encoding.UTF8.GetBytes(body), ct);

    private static async Task WriteBytesAsync(Stream stream, string status, string contentType, byte[] payload, CancellationToken ct)
    {
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(head, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    // ================= auth =================

    private bool TokenMatches(string query)
    {
        string? presented = null;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq] == "token") presented = Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        if (presented is null) return false;
        var presentedUtf8 = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(presentedUtf8, _tokenUtf8);
    }

    /// <summary>True when this IP has burned its auth-failure budget for the window.
    /// Keyed per remote IP so one guessing device can't lock the family out.</summary>
    private bool IsRateLimited(IPAddress ip)
    {
        if (!_authFailures.TryGetValue(ip, out var entry)) return false;
        if (DateTime.UtcNow - entry.WindowStart > AuthFailureWindow)
        {
            _authFailures.TryRemove(ip, out _);
            return false;
        }
        return entry.Count >= AuthFailureLimit;
    }

    private void RecordAuthFailure(IPAddress ip) =>
        _authFailures.AddOrUpdate(ip,
            _ => (1, DateTime.UtcNow),
            (_, e) => DateTime.UtcNow - e.WindowStart > AuthFailureWindow
                ? (1, DateTime.UtcNow)
                : (e.Count + 1, e.WindowStart));

    // ================= WebSocket =================

    private sealed class WsClient
    {
        public required TcpClient Tcp { get; init; }
        public required NetworkStream Stream { get; init; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        /// <summary>Surfaces this device asked for via a "subscribe" message;
        /// null = everything the desktop offers. Written by the read loop, read by
        /// Publish on another thread — a volatile reference swap, never mutated.</summary>
        public volatile IReadOnlyList<string>? Subscriptions;
        /// <summary>The sticky payloads this device already holds (theme, map
        /// geometry), guarded by <see cref="StateLock"/>.</summary>
        public CompanionClientState State { get; } = new();
        public object StateLock { get; } = new();
    }

    private async Task HandleWebSocketAsync(
        TcpClient tcp, NetworkStream stream, string query,
        Dictionary<string, string> headers, CancellationToken ct)
    {
        var remoteIp = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;

        if (IsRateLimited(remoteIp))
        {
            await WriteSimpleAsync(stream, "429 Too Many Requests", "text/plain",
                "Too many failed pairing attempts; wait a minute.", ct).ConfigureAwait(false);
            tcp.Close(); return;
        }
        if (!TokenMatches(query))
        {
            RecordAuthFailure(remoteIp);
            await WriteSimpleAsync(stream, "403 Forbidden", "text/plain",
                "Pairing token missing or wrong — rescan the QR code in EQBuddy.", ct).ConfigureAwait(false);
            tcp.Close(); return;
        }
        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key) ||
            !headers.TryGetValue("Upgrade", out var upgrade) ||
            !upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase))
        {
            await WriteSimpleAsync(stream, "400 Bad Request", "text/plain",
                "Expected a WebSocket upgrade.", ct).ConfigureAwait(false);
            tcp.Close(); return;
        }

        var accept = Convert.ToBase64String(SHA1.HashData(
            Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\nConnection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n\r\n");
        await stream.WriteAsync(response, ct).ConfigureAwait(false);

        var client = new WsClient { Tcp = tcp, Stream = stream };
        var id = Guid.NewGuid();
        _clients[id] = client;
        ClientsChanged?.Invoke();
        try
        {
            if (_latest is { } snap)
                await SendTextAsync(client, ForClient(client, snap)).ConfigureAwait(false);
            await ReadLoopAsync(client, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Abrupt phone sleep / Wi-Fi drop: normal weather, the page reconnects.
        }
        catch (Exception ex) { CoreLog.Error(ex); }
        finally
        {
            _clients.TryRemove(id, out _);
            tcp.Close();
            ClientsChanged?.Invoke();
        }
    }

    /// <summary>Consume client frames: answer pings, honor close, ignore payloads
    /// (the phone never has anything to say in protocol v1 — data flows one way).</summary>
    private async Task ReadLoopAsync(WsClient client, CancellationToken ct)
    {
        var header = new byte[14];
        while (!ct.IsCancellationRequested)
        {
            if (!await ReadExactAsync(client.Stream, header.AsMemory(0, 2), ct).ConfigureAwait(false)) return;
            var opcode = header[0] & 0x0F;
            var masked = (header[1] & 0x80) != 0;
            long length = header[1] & 0x7F;
            if (!masked) return; // RFC 6455: client frames MUST be masked — drop the link
            if (length == 126)
            {
                if (!await ReadExactAsync(client.Stream, header.AsMemory(2, 2), ct).ConfigureAwait(false)) return;
                length = (header[2] << 8) | header[3];
            }
            else if (length == 127)
            {
                if (!await ReadExactAsync(client.Stream, header.AsMemory(2, 8), ct).ConfigureAwait(false)) return;
                length = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(header.AsSpan(2, 8));
            }
            if (length is < 0 or > 65536) return; // nothing legitimate is that chatty

            var mask = new byte[4];
            if (!await ReadExactAsync(client.Stream, mask, ct).ConfigureAwait(false)) return;
            var payload = new byte[length];
            if (length > 0 && !await ReadExactAsync(client.Stream, payload, ct).ConfigureAwait(false)) return;
            for (var i = 0; i < payload.Length; i++) payload[i] ^= mask[i % 4];

            switch (opcode)
            {
                case 0x8: // close: echo and finish
                    await SendFrameAsync(client, 0x8, payload.Length >= 2 ? payload.AsMemory(0, 2) : default).ConfigureAwait(false);
                    return;
                case 0x9: // ping → pong with same payload
                    await SendFrameAsync(client, 0xA, payload).ConfigureAwait(false);
                    break;
                case 0x1: // text: the one client→server message is "subscribe"
                    await HandleClientMessageAsync(client, payload).ConfigureAwait(false);
                    break;
                default:
                    break; // binary/pong/continuation: drained and ignored
            }
        }
    }

    /// <summary>The things a device may say.
    ///
    /// {"kind":"subscribe","surfaces":["spawns", …]} — the ⚙ Screens choice arriving
    /// mid-connection: swaps the subscription and immediately re-projects the latest
    /// snapshot for it, no reconnect. The choice itself lives on the device
    /// (localStorage), never on this server.
    ///
    /// {"kind":"tick","surface":"epics","id":"…","done":true} — a checklist row
    /// tapped. Handed to the host as an event; it applies it on the desktop tick.
    ///
    /// {"kind":"curate","edit":"confirm|unconfirm|remove|resetZone","zone":"…",
    /// "locY":…,"locX":…} — the desktop map's right-click, arriving from a tablet.
    /// Same deal: an event for the host, applied on the tick, never on this thread.
    ///
    /// {"kind":"travel","destination":"…"} — the Path tab's destination picker
    /// (World PR 4); an absent/empty destination clears the pick.
    ///
    /// {"kind":"dropMarker"} — the Path/Travels tab's "Drop camp marker" button
    /// (World PR 4); no payload, same action the desktop's World window chrome offers.
    ///
    /// Anything else, or anything unparseable, is ignored: a companion page bug must
    /// not kill the link.</summary>
    private async Task HandleClientMessageAsync(WsClient client, byte[] payload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("kind", out var kind)) return;
            switch (kind.GetString())
            {
                case "subscribe":
                {
                    if (!doc.RootElement.TryGetProperty("surfaces", out var surfaces) ||
                        surfaces.ValueKind != System.Text.Json.JsonValueKind.Array)
                        return;
                    var subs = surfaces.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .Take(32)
                        .ToList();
                    client.Subscriptions = subs;
                    if (_latest is { } snap)
                        await SendTextAsync(client, ForClient(client, snap)).ConfigureAwait(false);
                    return;
                }
                case "tick":
                {
                    if (!doc.RootElement.TryGetProperty("surface", out var surface) ||
                        !doc.RootElement.TryGetProperty("id", out var id) ||
                        surface.GetString() is not { Length: > 0 and < 200 } surfaceName ||
                        id.GetString() is not { Length: > 0 and < 400 } rowId)
                        return;
                    var done = doc.RootElement.TryGetProperty("done", out var d)
                        && d.ValueKind == System.Text.Json.JsonValueKind.True;
                    ActionReceived?.Invoke(new CompanionAction(surfaceName, rowId, done));
                    return;
                }
                case "curate":
                {
                    if (!doc.RootElement.TryGetProperty("edit", out var editEl) ||
                        !Enum.TryParse<CompanionMapEdit>(editEl.GetString(), ignoreCase: true, out var edit) ||
                        !doc.RootElement.TryGetProperty("zone", out var zoneEl) ||
                        zoneEl.GetString() is not { Length: > 0 and < 200 } zone)
                        return;
                    // A zone reset names no point; the others must, and a coordinate
                    // that isn't a finite number is not a point.
                    double locY = 0, locX = 0;
                    if (edit != CompanionMapEdit.ResetZone)
                    {
                        if (!doc.RootElement.TryGetProperty("locY", out var y) ||
                            !doc.RootElement.TryGetProperty("locX", out var x) ||
                            !y.TryGetDouble(out locY) || !x.TryGetDouble(out locX) ||
                            !double.IsFinite(locY) || !double.IsFinite(locX))
                            return;
                    }
                    MapActionReceived?.Invoke(new CompanionMapAction(edit, zone, locY, locX));
                    return;
                }
                case "travel":
                {
                    // destination omitted or "" clears the pick — the picker's own
                    // "clear" affordance, not a malformed message.
                    var destination = doc.RootElement.TryGetProperty("destination", out var destEl)
                        && destEl.ValueKind == System.Text.Json.JsonValueKind.String
                        && destEl.GetString() is { Length: < 200 } d
                        ? d : null;
                    TravelActionReceived?.Invoke(new CompanionTravelAction(destination));
                    return;
                }
                case "dropMarker":
                    MarkerDropReceived?.Invoke();
                    return;
            }
        }
        catch (System.Text.Json.JsonException) { /* garbage in, nothing out */ }
        catch (InvalidOperationException) { /* wrong JSON kind for a field — same */ }
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, Memory<byte> target, CancellationToken ct)
    {
        var read = 0;
        while (read < target.Length)
        {
            var n = await stream.ReadAsync(target[read..], ct).ConfigureAwait(false);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    private Task SendTextAsync(WsClient client, string text) =>
        SendFrameAsync(client, 0x1, Encoding.UTF8.GetBytes(text));

    private async Task SendFrameAsync(WsClient client, int opcode, ReadOnlyMemory<byte> payload)
    {
        // Serialize writes per client; a stuck phone (asleep with a full TCP window)
        // gets the send timeout and is dropped rather than wedging the broadcast.
        if (!await client.SendLock.WaitAsync(SendTimeout).ConfigureAwait(false)) { client.Tcp.Close(); return; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeout.CancelAfter(SendTimeout);
            var head = BuildFrameHeader(opcode, payload.Length);
            await client.Stream.WriteAsync(head, timeout.Token).ConfigureAwait(false);
            await client.Stream.WriteAsync(payload, timeout.Token).ConfigureAwait(false);
            await client.Stream.FlushAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            client.Tcp.Close(); // read loop notices and unregisters
        }
        finally { client.SendLock.Release(); }
    }

    private static byte[] BuildFrameHeader(int opcode, int length)
    {
        // Server-to-client frames are FIN + unmasked (RFC 6455 §5.1).
        if (length < 126) return [(byte)(0x80 | opcode), (byte)length];
        if (length < 65536)
            return [(byte)(0x80 | opcode), 126, (byte)(length >> 8), (byte)length];
        var head = new byte[10];
        head[0] = (byte)(0x80 | opcode);
        head[1] = 127;
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(head.AsSpan(2), length);
        return head;
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(_options.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (_clients.IsEmpty) continue;
                var beat = $"{{\"kind\":\"heartbeat\",\"protocol\":{CompanionSnapshot.CurrentProtocol}," +
                           $"\"sentAtUtc\":\"{DateTime.UtcNow:O}\"}}";
                foreach (var client in _clients.Values)
                    await SendTextAsync(client, beat).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        finally { timer.Dispose(); }
    }
}
