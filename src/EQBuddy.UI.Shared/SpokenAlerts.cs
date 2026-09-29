using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public static partial class SpokenAlerts
{
    private const int SpeakAsync = 1;
    private static readonly object Sync = new();
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static object? _voice;
    /// <summary>The token SAPI came up with on its own, captured before the first
    /// override — "" (system default) means restoring THIS, not re-creating the voice.</summary>
    private static object? _defaultVoiceToken;
    private static string _voiceName = "";
    private static int _rate;
    private static int _volume = 100;
    private static string _lastText = "";
    private static DateTime _lastAt = DateTime.MinValue;

    /// <summary>SAPI accepts -10..10, but past ±5 speech stops being speech; the Options
    /// slider exposes this band and hand-edited settings clamp into it too.</summary>
    public const int MinRate = -5;
    public const int MaxRate = 5;

    public static bool Speak(string text) => Speak(text, DateTime.Now);

    /// <summary>Startup wiring for the persisted voice/rate/volume. Values are stored
    /// first (so a later-created voice picks them up) and then reach SAPI.</summary>
    public static void Configure(string voiceName, int rate, int volume)
    {
        lock (Sync)
        {
            _voiceName = voiceName ?? "";
            _rate = Math.Clamp(rate, MinRate, MaxRate);
            _volume = Math.Clamp(volume, 0, 100);
            ApplyConfigIfLive();
        }
    }

    /// <summary>"" = the system default voice (the pre-picker behavior). Takes effect on
    /// the next utterance — SAPI reads the token per Speak call, no restart needed.</summary>
    public static void SetVoice(string name)
    {
        lock (Sync)
        {
            _voiceName = name ?? "";
            ApplyConfigIfLive();
        }
    }

    public static void SetRate(int rate)
    {
        lock (Sync)
        {
            _rate = Math.Clamp(rate, MinRate, MaxRate);
            ApplyConfigIfLive();
        }
    }

    public static void SetVolume(int volume)
    {
        lock (Sync)
        {
            _volume = Math.Clamp(volume, 0, 100);
            ApplyConfigIfLive();
        }
    }

    /// <summary>What the voice says for a matched rule: the player's own phrase when one
    /// is set, the auto-generated label otherwise. Pure — both UIs and the tests share it.</summary>
    public static string ResolvePhrase(string customPhrase, string autoLabel) =>
        string.IsNullOrWhiteSpace(customPhrase) ? autoLabel : customPhrase.Trim();

    /// <summary>The Options ▶ button: speaks with the current voice/rate/volume, skipping
    /// the duplicate window — hearing the same sample twice in a row IS the test.</summary>
    public static void SpeakSample(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            lock (Sync) SpeakOnPlatform(Speakable(text));
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    /// <summary>Voice descriptions SAPI can actually switch to — enumerated through the
    /// same SpVoice the alerts speak with, so the picker never offers a voice Speak can't
    /// use (System.Speech and OneCore keep slightly different registries). Empty
    /// off-Windows and when SAPI is unavailable; instantiates SAPI, so tests that must
    /// stay hardware-free pass a fake list to the view model instead of calling this.</summary>
    public static IReadOnlyList<string> InstalledVoiceNames()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return InstalledVoiceNamesWindows();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> InstalledVoiceNamesWindows()
    {
        try
        {
            lock (Sync)
            {
                if (EnsureVoiceWindows() is not { } voice) return [];
                var names = new List<string>();
                ForEachVoiceToken(voice, (_, description) => names.Add(description));
                return names;
            }
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return [];
        }
    }

    /// <summary>Must hold <see cref="Sync"/>. No-op until the platform voice exists —
    /// creation applies the stored config itself, so nothing is lost either way.</summary>
    private static void ApplyConfigIfLive()
    {
        if (!OperatingSystem.IsWindows() || _voice is null) return;
        try { ApplyConfigWindows(_voice); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyConfigWindows(object voice)
    {
        var type = voice.GetType();
        _defaultVoiceToken ??= type.InvokeMember("Voice", BindingFlags.GetProperty, null, voice, null);
        // A voice that's gone missing (settings copied between machines, an uninstalled
        // language pack) falls back to the default rather than silencing alerts.
        var token = _voiceName.Length > 0 ? FindVoiceToken(voice, _voiceName) : _defaultVoiceToken;
        if (token is not null)
            type.InvokeMember("Voice", BindingFlags.SetProperty, null, voice, [token]);
        type.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, [_rate]);
        type.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, [_volume]);
    }

    [SupportedOSPlatform("windows")]
    private static object? FindVoiceToken(object voice, string name)
    {
        object? found = null;
        ForEachVoiceToken(voice, (token, description) =>
        {
            if (found is null && string.Equals(description, name, StringComparison.OrdinalIgnoreCase))
                found = token;
        });
        return found;
    }

    [SupportedOSPlatform("windows")]
    private static void ForEachVoiceToken(object voice, Action<object, string> visit)
    {
        var tokens = voice.GetType().InvokeMember("GetVoices", BindingFlags.InvokeMethod, null, voice, ["", ""]);
        if (tokens is null) return;
        var count = (int)tokens.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, tokens, null)!;
        for (var i = 0; i < count; i++)
        {
            var token = tokens.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, tokens, [i]);
            if (token is null) continue;
            if (token.GetType().InvokeMember("GetDescription", BindingFlags.InvokeMethod, null, token, [0]) is string description)
                visit(token, description);
        }
    }

    /// <summary>Pre-creates the platform voice off the caller's thread. SAPI's first
    /// SpVoice instantiation costs a noticeable beat (David's bench test: the slow
    /// chip popped instantly, the voice arrived fashionably late) — paying it at
    /// startup means the first real alert speaks on time. No-op on repeat calls.</summary>
    public static void Warmup()
    {
        if (!OperatingSystem.IsWindows()) return;
        Task.Run(WarmupWindows);
    }

    [SupportedOSPlatform("windows")]
    private static void WarmupWindows()
    {
        try
        {
            lock (Sync) EnsureVoiceWindows();
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    /// <summary>Must hold <see cref="Sync"/>. Creates the SpVoice on first use and applies
    /// the stored voice/rate/volume right there — a Configure that ran before warmup (the
    /// startup order) still shapes the very first utterance.</summary>
    [SupportedOSPlatform("windows")]
    private static object? EnsureVoiceWindows()
    {
        if (_voice is null)
        {
            var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (voiceType is null) return null;
            var voice = Activator.CreateInstance(voiceType);
            if (voice is null) return null;
            // Keep the voice even if applying stored config throws (a stale voice
            // token, a COM hiccup) — losing the config must not cost every future
            // alert its speech.
            try { ApplyConfigWindows(voice); }
            catch (Exception ex) { CoreLog.Error(ex); }
            _voice = voice;
        }
        return _voice;
    }

    /// <summary>Banner text carries the app's × counts ("Rusty Sword ×3"); the voice
    /// gets plain English ("Rusty Sword 3 times") instead of a multiplication sign.</summary>
    [GeneratedRegex(@"\s*×\s*(\d+)")]
    private static partial Regex CountSuffixRx();

    public static string Speakable(string text) =>
        CountSuffixRx().Replace(text, " $1 times");

    internal static bool Speak(string text, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = Speakable(text);

        try
        {
            lock (Sync)
            {
                if (string.Equals(text, _lastText, StringComparison.OrdinalIgnoreCase)
                    && now - _lastAt < DuplicateWindow)
                    return false;

                if (!SpeakOnPlatform(text)) return false;

                _lastText = text;
                _lastAt = now;
            }
            return true;
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return false;
        }
    }

    /// <summary>The one place that decides whether there is a voice; false means none,
    /// and the caller must not record the utterance as spoken. Windows only since E-2c —
    /// the macOS `say` path served the v1 Avalonia build and stays on `legacy-v1`. The
    /// guard remains because this project targets plain net10.0.</summary>
    private static bool SpeakOnPlatform(string text)
    {
        if (!OperatingSystem.IsWindows()) return false;
        SpeakWindows(text);
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void SpeakWindows(string text)
    {
        var voice = EnsureVoiceWindows()
            ?? throw new InvalidOperationException("Windows speech voice is not available.");
        voice.GetType().InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice,
            [text, SpeakAsync]);
    }
}
