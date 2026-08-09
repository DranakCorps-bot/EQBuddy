using System.Reflection;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public static class SpokenAlerts
{
    private const int SpeakAsync = 1;
    private static readonly object Sync = new();
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private static object? _voice;
    private static string _lastText = "";
    private static DateTime _lastAt = DateTime.MinValue;

    public static bool Speak(string text) => Speak(text, DateTime.Now);

    internal static bool Speak(string text, DateTime now)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            lock (Sync)
            {
                if (string.Equals(text, _lastText, StringComparison.OrdinalIgnoreCase)
                    && now - _lastAt < DuplicateWindow)
                    return false;

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
}
