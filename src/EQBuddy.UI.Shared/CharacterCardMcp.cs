using System.Reflection;
using System.Text;
using System.Text.Json;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **THE READ-ONLY MCP SERVER BEHIND <c>EQBuddy.exe --mcp</c>** (DRA-1446 D1,
/// <c>docs/plans/DRA-1446.md</c>, option 2b). Newline-delimited JSON-RPC 2.0 over two streams
/// — the stdio transport — answering <c>initialize</c>, <c>ping</c>, <c>tools/list</c> and
/// <c>tools/call</c> for two tools, <see cref="CharacterCardMcpCopy.ListTool"/> and
/// <see cref="CharacterCardMcpCopy.ReadTool"/>. Hand-written, no SDK (decision 3).
///
/// <list type="bullet">
/// <item><b>It never writes a file.</b> It enumerates the cards folder and opens files for
/// READ with <see cref="FileAccess.Read"/>; <c>CharacterCardMcpTests</c> forbid-scans this file
/// for every write API and pairs the scan with a must-list of the reads it does use (trap 34),
/// plus a committed negative that proves the scan fires (trap 78).</item>
/// <item><b>The argument never becomes a path</b> (decision 5). A file is served only when its
/// name parses through <see cref="CharacterCardWriter.TryParseFileName"/> AND its first lines
/// carry the renderer's own <see cref="CharacterCardPresentation.Title"/> and
/// <see cref="CharacterCardPresentation.Provenance"/>. The argument is compared against the
/// parsed names, whole-string and case-insensitive; it is never joined to the folder.</item>
/// <item><b>The card is returned as labelled data</b> (decision 6): one text block, a fixed
/// lead line, then the card inside a fence longer than any backtick run in it.</item>
/// <item><b>Over <see cref="MaxCardBytes"/> is refused, never truncated</b> (decision 7).</item>
/// </list>
///
/// <para>Every response is ONE line on the output: <see cref="Utf8JsonWriter"/> escapes a
/// card's newlines, so nothing inside a card can split the stream.</para>
/// </summary>
public sealed class CharacterCardMcp
{
    /// <summary>
    /// The MCP revision this server speaks — the current dated one (decision 3). The earlier
    /// revisions in <see cref="CompatibleProtocolVersions"/> carry the same shapes for every
    /// message this server sends (two tools, text content, no resources, prompts or sampling),
    /// so a client that asks for one of them is answered in its own version rather than
    /// refused; anything else is answered with this one, which is the spec's negotiation.
    /// </summary>
    public const string ProtocolVersion = "2025-11-25";

    public static readonly IReadOnlyList<string> CompatibleProtocolVersions =
        [ProtocolVersion, "2025-06-18", "2025-03-26", "2024-11-05"];

    /// <summary>Decision 7. A slice-1 card is a few KiB.</summary>
    public const long MaxCardBytes = 256 * 1024;

    /// <summary>How many lines of a candidate are read to decide whether EQBuddy wrote it
    /// (decision 5): the title is line 1 and the provenance line 3.</summary>
    public const int HeaderLines = 4;

    /// <summary>A bound on the header read in characters, so a file with no newline in it is
    /// not read whole to find its "first four lines".</summary>
    private const int HeaderChars = 4096;

    /// <summary>Relaxed escaping keeps a card's quotes and backticks readable in a transcript;
    /// it still escapes every control character, so a newline is <c>\n</c> and a response
    /// stays one line. Nothing here is ever embedded in HTML, which is all "unsafe" refers to.</summary>
    private static readonly JsonWriterOptions Options =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // JSON-RPC 2.0 error codes.
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;

    public CharacterCardMcp(string cardsFolder) => Folder = cardsFolder;

    public string Folder { get; }

    /// <summary>The folder <c>EQBuddy.exe --mcp</c> serves: the writer's own, so it follows a
    /// redirected <c>EQBUDDY_APPDATA</c> exactly as the writer does (traps 68/69).</summary>
    public static string DefaultFolder => CharacterCardWriter.DefaultFolder;

    /// <summary>
    /// Every file this instance has opened, with how far: <c>header</c> (at most
    /// <see cref="HeaderLines"/> lines) or <c>whole</c>. Recorded inside the two read methods
    /// themselves, so a test asserts what was READ rather than what was answered (trap 84).
    /// </summary>
    public IReadOnlyList<(string File, string Extent)> Opened => _opened;

    private readonly List<(string File, string Extent)> _opened = [];

    /// <summary>Serves until <paramref name="input"/> ends or the token is cancelled.</summary>
    public static void Serve(TextReader input, TextWriter output, string cardsFolder, CancellationToken cancel) =>
        new CharacterCardMcp(cardsFolder).Serve(input, output, cancel);

    public void Serve(TextReader input, TextWriter output, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested && input.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (Handle(line) is { } response)
            {
                output.Write(response);
                output.Write('\n');
                output.Flush();
            }
        }
    }

    /// <summary>One incoming line → the one response line, or null for a notification or a
    /// client's response (neither is answered).</summary>
    public string? Handle(string line)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); }
        catch (JsonException) { return Error(null, ParseError, "Parse error"); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Error(null, InvalidRequest, "Invalid Request");

            JsonElement? id = root.TryGetProperty("id", out var idEl)
                              && idEl.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? idEl.Clone()
                : null;
            if (!root.TryGetProperty("method", out var methodEl) || methodEl.ValueKind != JsonValueKind.String)
                return root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _)
                    ? null   // a response from the client: nothing here ever asks it anything
                    : Error(id, InvalidRequest, "Invalid Request");

            if (id is null) return null;   // a notification: initialized, cancelled, …

            var method = methodEl.GetString();
            var hasParams = root.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object;
            return method switch
            {
                "initialize" => Result(id, w => Initialize(w, hasParams ? p : null)),
                "ping" => Result(id, w => { w.WriteStartObject(); w.WriteEndObject(); }),
                "tools/list" => Result(id, ToolsList),
                "tools/call" => ToolsCall(id.Value, hasParams ? p : null),
                _ => Error(id, MethodNotFound, "Method not found"),
            };
        }
    }

    // ---- methods --------------------------------------------------------------------------

    private static void Initialize(Utf8JsonWriter w, JsonElement? p)
    {
        var asked = p is { } pp && pp.TryGetProperty("protocolVersion", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
        w.WriteStartObject();
        w.WriteString("protocolVersion",
            asked is not null && CompatibleProtocolVersions.Contains(asked) ? asked : ProtocolVersion);
        w.WriteStartObject("capabilities");
        w.WriteStartObject("tools");
        w.WriteBoolean("listChanged", false);
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteStartObject("serverInfo");
        w.WriteString("name", CharacterCardMcpCopy.ServerName);
        w.WriteString("version", ServerVersion);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static string ServerVersion =>
        typeof(CharacterCardMcp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? typeof(CharacterCardMcp).Assembly.GetName().Version?.ToString()
        ?? "0";

    /// <summary>Fixed literals only (decision 6) — no card string can reach this.</summary>
    private static void ToolsList(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteStartArray("tools");

        Tool(w, CharacterCardMcpCopy.ListTool, CharacterCardMcpCopy.ListTitle, CharacterCardMcpCopy.ListDescription,
            schema => { schema.WriteStartObject("properties"); schema.WriteEndObject(); });

        Tool(w, CharacterCardMcpCopy.ReadTool, CharacterCardMcpCopy.ReadTitle, CharacterCardMcpCopy.ReadDescription,
            schema =>
            {
                schema.WriteStartObject("properties");
                schema.WriteStartObject("character");
                schema.WriteString("type", "string");
                schema.WriteString("description", CharacterCardMcpCopy.CharacterArgument);
                schema.WriteEndObject();
                schema.WriteStartObject("server");
                schema.WriteString("type", "string");
                schema.WriteString("description", CharacterCardMcpCopy.ServerArgument);
                schema.WriteEndObject();
                schema.WriteEndObject();
                schema.WriteStartArray("required");
                schema.WriteStringValue("character");
                schema.WriteEndArray();
            });

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Tool(Utf8JsonWriter w, string name, string title, string description, Action<Utf8JsonWriter> properties)
    {
        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteString("title", title);
        w.WriteString("description", description);
        w.WriteStartObject("inputSchema");
        w.WriteString("type", "object");
        properties(w);
        w.WriteBoolean("additionalProperties", false);
        w.WriteEndObject();
        w.WriteStartObject("annotations");
        w.WriteString("title", title);
        w.WriteBoolean("readOnlyHint", true);
        w.WriteBoolean("destructiveHint", false);
        w.WriteBoolean("idempotentHint", true);
        w.WriteBoolean("openWorldHint", false);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private string ToolsCall(JsonElement id, JsonElement? p)
    {
        if (p is not { } pp || !pp.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return Error(id, InvalidParams, "Missing tool name");
        var args = pp.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;

        return nameEl.GetString() switch
        {
            CharacterCardMcpCopy.ListTool => ToolText(id, ListCards(), isError: false),
            CharacterCardMcpCopy.ReadTool => ReadCard(id, args),
            _ => Error(id, InvalidParams, "Unknown tool"),
        };
    }

    private string ListCards()
    {
        var cards = Cards();
        if (cards.Count == 0) return CharacterCardMcpCopy.NoCards;
        var sb = new StringBuilder(CharacterCardMcpCopy.ListLead).Append('\n');
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartArray();
            foreach (var c in cards)
            {
                w.WriteStartObject();
                w.WriteString("character", c.Name);
                w.WriteString("server", c.Server);
                w.WriteString("lastWritten", c.LastWritten);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        return sb.Append(Encoding.UTF8.GetString(buffer.ToArray())).ToString();
    }

    private string ReadCard(JsonElement id, JsonElement? args)
    {
        string? Arg(string key) =>
            args is { } a && a.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var character = Arg("character");
        var server = Arg("server");
        if (string.IsNullOrWhiteSpace(character)) return ToolText(id, CharacterCardMcpCopy.MissingCharacter, isError: true);

        var cards = Cards();
        var named = cards.Where(c => string.Equals(c.Name, character.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(server))
            named = named.Where(c => string.Equals(c.Server, server.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        if (named.Count == 0)
            return ToolText(id, CharacterCardMcpCopy.NoSuchCard(cards.Select(c => c.Label)), isError: true);
        if (named.Count > 1)
            return ToolText(id, CharacterCardMcpCopy.Ambiguous(named.Select(c => c.Label)), isError: true);

        var card = named[0];
        var text = ReadWhole(card, out var length);
        if (text is null)
            return ToolText(id, length > MaxCardBytes
                ? CharacterCardMcpCopy.TooLarge(card.Label, length, MaxCardBytes)
                : CharacterCardMcpCopy.Unreadable(card.Label), isError: true);
        return ToolText(id, Labelled(text), isError: false);
    }

    /// <summary>
    /// Decision 6: the fixed lead line, then the card inside a fence whose backtick run is
    /// longer than any run inside the card, closed after the card's last byte.
    /// </summary>
    public static string Labelled(string card)
    {
        var fence = new string('`', Math.Max(3, LongestBacktickRun(card) + 1));
        var sb = new StringBuilder(CharacterCardMcpCopy.ReadLead).Append("\n\n").Append(fence).Append("markdown\n").Append(card);
        if (!card.EndsWith('\n')) sb.Append('\n');
        return sb.Append(fence).ToString();
    }

    public static int LongestBacktickRun(string text)
    {
        int longest = 0, run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            if (run > longest) longest = run;
        }
        return longest;
    }

    // ---- the folder (decisions 5 and 7) -----------------------------------------------------

    private sealed record Card(string FileName, string Path, string Name, string Server, string LastWritten)
    {
        public string Label => CharacterCardMcpCopy.CardLabel(Name, Server);
    }

    /// <summary>The cards that exist: a name that parses AND a header EQBuddy wrote. Sorted by
    /// name, then server, so the answer does not move with the file system's order.</summary>
    private List<Card> Cards()
    {
        var cards = new List<Card>();
        if (!Directory.Exists(Folder)) return cards;
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(Folder, "*", SearchOption.TopDirectoryOnly).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return cards; }

        foreach (var path in files)
        {
            var fileName = System.IO.Path.GetFileName(path);
            if (!CharacterCardWriter.TryParseFileName(fileName, out var name, out var server)) continue;
            if (!HasOurHeader(path)) continue;
            string written;
            try { written = new FileInfo(path).LastWriteTime.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            cards.Add(new Card(fileName, path, name, server, written));
        }
        return [.. cards
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Server, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Decision 5's content half: within the first <see cref="HeaderLines"/> lines,
    /// the renderer's own title and provenance line, verbatim. Reads no further.</summary>
    private bool HasOurHeader(string path)
    {
        _opened.Add((System.IO.Path.GetFileName(path), "header"));
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[HeaderChars];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            var lines = new string(buffer, 0, read).Split('\n').Take(HeaderLines).Select(l => l.TrimEnd('\r')).ToList();
            return lines.Contains(CharacterCardPresentation.Title, StringComparer.Ordinal)
                   && lines.Contains(CharacterCardPresentation.Provenance, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The whole card, or null when it is over the cap (<paramref name="length"/> says
    /// by how much) or could not be read. The cap is checked on the length BEFORE the read and
    /// again on what the read returned, so a file that grew in between is refused too.</summary>
    private string? ReadWhole(Card card, out long length)
    {
        length = 0;
        try
        {
            using var stream = new FileStream(card.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            length = stream.Length;
            if (length > MaxCardBytes) return null;
            _opened.Add((card.FileName, "whole"));
            var bytes = new byte[MaxCardBytes + 1];
            var total = 0;
            int n;
            while (total < bytes.Length && (n = stream.Read(bytes, total, bytes.Length - total)) > 0) total += n;
            if (total > MaxCardBytes) { length = total; return null; }
            using var reader = new StreamReader(new MemoryStream(bytes, 0, total), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    // ---- JSON-RPC envelopes ---------------------------------------------------------------

    private static string ToolText(JsonElement id, string text, bool isError) =>
        Result(id, w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("content");
            w.WriteStartObject();
            w.WriteString("type", "text");
            w.WriteString("text", text);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteBoolean("isError", isError);
            w.WriteEndObject();
        });

    private static string Result(JsonElement? id, Action<Utf8JsonWriter> result) =>
        Envelope(w =>
        {
            WriteId(w, id);
            w.WritePropertyName("result");
            result(w);
        });

    private static string Error(JsonElement? id, int code, string message) =>
        Envelope(w =>
        {
            WriteId(w, id);
            w.WriteStartObject("error");
            w.WriteNumber("code", code);
            w.WriteString("message", message);
            w.WriteEndObject();
        });

    private static void WriteId(Utf8JsonWriter w, JsonElement? id)
    {
        w.WritePropertyName("id");
        if (id is { } i) i.WriteTo(w);
        else w.WriteNullValue();
    }

    private static string Envelope(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            body(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
