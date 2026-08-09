using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// Watch rules as guild-chat-sized strings (queued since the 2026-08-06 EQ Legends
/// Companion review — their share-string idea, our own format and code):
///
///     EQB1.&lt;base64url(deflate(json))&gt;.&lt;fnv1a-32 hex&gt;
///
/// The dot separators are deliberate — base64url's alphabet includes '-' and '_', so a
/// dash-delimited format couldn't be split unambiguously. Decode is paste-friendly: the
/// token is extracted from whatever surrounds it (a Discord message, a forum quote), the
/// checksum catches truncation before json sees it, and every imported rule is REBUILT
/// field by field — fresh id, clamped lengths, machine-local fields (custom sound paths)
/// dropped — so a crafted string can never smuggle state the editor couldn't type.
/// </summary>
public static partial class WatchRuleShare
{
    private const string Prefix = "EQB1";
    public const int MaxRulesPerString = 20;
    private const int MaxTextLength = 200;

    [GeneratedRegex(@"EQB1\.([A-Za-z0-9_-]+)\.([0-9a-f]{8})")]
    private static partial Regex TokenRx();

    /// <summary>Wire shape: short keys keep guild-chat strings short; defaults are
    /// omitted entirely (WhenWritingDefault) for the same reason.</summary>
    private sealed class Wire
    {
        [JsonPropertyName("n")] public string? Name { get; set; }
        [JsonPropertyName("p")] public string? Pattern { get; set; }
        [JsonPropertyName("k")] public int Kind { get; set; }
        [JsonPropertyName("f")] public int Filter { get; set; }
        [JsonPropertyName("b")] public bool Banner { get; set; }
        [JsonPropertyName("s")] public bool Sound { get; set; }
        [JsonPropertyName("sp")] public bool Speech { get; set; }
        [JsonPropertyName("c")] public string? Color { get; set; }
        [JsonPropertyName("sn")] public string? SoundName { get; set; }
        [JsonPropertyName("d")] public double Delay { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static string Encode(IEnumerable<TrackedRule> rules)
    {
        var wire = rules.Take(MaxRulesPerString).Select(r => new Wire
        {
            Name = r.Name.Length > 0 ? r.Name : null,
            Pattern = r.Pattern.Length > 0 ? r.Pattern : null,
            Kind = (int)r.Kind,
            Filter = (int)r.SpellFilter,
            Banner = r.AlertBanner,
            Sound = r.AlertSound,
            Speech = r.AlertSpeech,
            Color = r.AlertColor.Length > 0 ? r.AlertColor : null,
            // Custom sounds are file paths on the sharer's machine — meaningless (and
            // mildly leaky) elsewhere. Only built-in names travel.
            SoundName = IsBuiltInSoundName(r.AlertSoundName) ? r.AlertSoundName : null,
            Delay = r.AlertDelaySeconds,
        }).ToList();

        var json = JsonSerializer.SerializeToUtf8Bytes(wire, JsonOpts);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(json);
        var payload = Base64Url(buffer.ToArray());
        return $"{Prefix}.{payload}.{Fnv1a32(payload):x8}";
    }

    /// <summary>Extracts and decodes the first share token in <paramref name="text"/>.
    /// Returns rebuilt, import-ready rules — or null with a human-readable reason.</summary>
    public static List<TrackedRule>? TryDecode(string text, out string error)
    {
        error = "";
        var m = TokenRx().Match(text ?? "");
        if (!m.Success)
        {
            error = "No EQB1 share string found — paste the whole EQB1.….… token.";
            return null;
        }
        var payload = m.Groups[1].Value;
        if (Fnv1a32(payload) != Convert.ToUInt32(m.Groups[2].Value, 16))
        {
            error = "The string is damaged (checksum mismatch) — it was probably cut off mid-copy.";
            return null;
        }

        List<Wire>? wire;
        try
        {
            using var input = new MemoryStream(FromBase64Url(payload));
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            wire = JsonSerializer.Deserialize<List<Wire>>(deflate, JsonOpts);
        }
        catch
        {
            error = "The string decodes to something that isn't a watch rule.";
            return null;
        }
        if (wire is not { Count: > 0 })
        {
            error = "The string contains no rules.";
            return null;
        }

        var rules = new List<TrackedRule>();
        foreach (var w in wire.Take(MaxRulesPerString))
        {
            if (!Enum.IsDefined((WatchKind)w.Kind) || !Enum.IsDefined((SpellFilter)w.Filter))
            {
                error = "The string uses a rule kind this version doesn't know — update EQBuddy and try again.";
                return null;
            }
            // Rebuilt, never deserialized-into: fresh Id, Enabled/Pinned at their
            // defaults, every text field cleaned and clamped.
            rules.Add(new TrackedRule
            {
                Name = Clean(w.Name),
                Pattern = Clean(w.Pattern),
                Kind = (WatchKind)w.Kind,
                SpellFilter = (SpellFilter)w.Filter,
                AlertBanner = w.Banner,
                AlertSound = w.Sound,
                AlertSpeech = w.Speech,
                AlertColor = Clean(w.Color, 30),
                AlertSoundName = IsBuiltInSoundName(w.SoundName ?? "") ? w.SoundName! : "",
                AlertDelaySeconds = w.Delay,   // property setter clamps
            });
        }
        return rules;
    }

    /// <summary>One line per rule, for the import preview: what will this do to my
    /// widget if I click Add?</summary>
    public static string Describe(TrackedRule r)
    {
        var what = r.Kind is WatchKind.SpellFade && r.SpellFilter != SpellFilter.ByName
            ? $"Spell fade · {r.SpellFilter}"
            : $"{r.Kind} · \"{r.EffectivePattern}\"";
        var cues = new List<string>();
        if (r.AlertBanner) cues.Add("banner");
        if (r.AlertSound) cues.Add(r.AlertSoundName.Length > 0 ? $"sound: {r.AlertSoundName}" : "sound");
        if (r.AlertSpeech) cues.Add("speech");
        if (r.AlertDelaySeconds > 0) cues.Add($"delay {r.AlertDelaySeconds:0.#}s");
        var name = r.Name.Length > 0 ? r.Name : "(unnamed)";
        return $"{name} — {what}" + (cues.Count > 0 ? $" — {string.Join(", ", cues)}" : " — silent");
    }

    /// <summary>Built-in sound names are single words like "Alarm" or "Chime"; anything
    /// with path separators, extensions or exotic characters is a local file reference
    /// and stays home. Core can't see the UI's sound catalog, so shape is the test —
    /// the picker on the receiving end shrugs off an unknown-but-clean name anyway.</summary>
    private static bool IsBuiltInSoundName(string name) =>
        name.Length is > 0 and <= 30 && name.All(c => char.IsLetterOrDigit(c) || c == ' ');

    private static string Clean(string? s, int max = MaxTextLength)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var kept = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return kept.Length <= max ? kept : kept[..max];
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    /// <summary>FNV-1a 32-bit over the payload characters — catches the truncation and
    /// mid-string edits that chat clients love, which is all a share string needs.</summary>
    private static uint Fnv1a32(string payload)
    {
        var hash = 2166136261u;
        foreach (var c in payload)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }
}
