using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public static partial class SpokenAlerts
{
    private const int SpeakAsync = 1;
    private const int MacQueueLimit = 4;
    private static readonly object Sync = new();
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static object? _voice;
    private static string _lastText = "";
    private static DateTime _lastAt = DateTime.MinValue;
    private static Task _macSpeech = Task.CompletedTask;
    private static int _macQueueDepth;

    public static bool Speak(string text) => Speak(text, DateTime.Now);

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

    /// <summary>The one place that decides which platforms have a voice; false means this
    /// one has none, and the caller must not record the utterance as spoken.</summary>
    private static bool SpeakOnPlatform(string text)
    {
        if (OperatingSystem.IsWindows()) { SpeakWindows(text); return true; }
        if (OperatingSystem.IsMacOS()) { SpeakMac(text); return true; }
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static void SpeakWindows(string text)
    {
        if (_voice is null)
        {
            var voiceType = Type.GetTypeFromProgID("SAPI.SpVoice")
                ?? throw new InvalidOperationException("Windows speech voice is not available.");
            _voice = Activator.CreateInstance(voiceType)
                ?? throw new InvalidOperationException("Windows speech voice could not be created.");
        }
        var voice = _voice;
        voice.GetType().InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice,
            [text, SpeakAsync]);
    }

    /// <summary>
    /// macOS speaks through <c>/usr/bin/say</c>, one process per utterance. SAPI's
    /// SpeakAsync owns a queue and plays its backlog in order; concurrent `say` processes
    /// would instead talk over each other, so utterances are chained onto a single task
    /// tail to reproduce that ordering.
    /// </summary>
    [SupportedOSPlatform("macos")]
    private static void SpeakMac(string text)
    {
        // A stale alert read out a minute late is worse than silence, so an alert storm
        // that outruns the voice drops the overflow rather than queueing it forever.
        if (Interlocked.Increment(ref _macQueueDepth) > MacQueueLimit)
        {
            Interlocked.Decrement(ref _macQueueDepth);
            return;
        }

        _macSpeech = _macSpeech.ContinueWith(_ => RunSay(text), CancellationToken.None,
            TaskContinuationOptions.None, TaskScheduler.Default);
    }

    [SupportedOSPlatform("macos")]
    private static void RunSay(string text)
    {
        try
        {
            // No shell, and `--` ends option parsing: alert labels are built from log text
            // that other players control, and a label starting with "-v" must be spoken,
            // not read as a flag.
            var start = new ProcessStartInfo("/usr/bin/say")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(text);

            using var say = Process.Start(start);
            say?.WaitForExit();
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
        }
        finally
        {
            Interlocked.Decrement(ref _macQueueDepth);
        }
    }
}
