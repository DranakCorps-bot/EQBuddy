using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

public sealed class AppSettings
{
    public string? LogFolder { get; set; }
    /// <summary>Folder holding EQBuddySetup.exe for updates; null = auto-detect OneDrive.</summary>
    public string? UpdateFolder { get; set; }
    public bool Minimized { get; set; }
    public List<string> MiniStats { get; set; } = ["kills", "dps"];
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double Opacity { get; set; } = 0.96;
    public double UiScale { get; set; } = 1.0;
    /// <summary>Opacity of the widget's background panel only — text stays fully opaque.</summary>
    public double BackgroundOpacity { get; set; } = 0.95;
    /// <summary>Empty finished-session logs automatically. Off = logs grow forever
    /// (for players who upload their logs elsewhere).</summary>
    public bool TruncateLogs { get; set; } = true;
    /// <summary>User-defined tracked-loot rules (TRACK-018: persisted).</summary>
    public List<TrackedRule> TrackedRules { get; set; } = [];
    /// <summary>Default rolling window for "recent" rates, in minutes (5/15/30).</summary>
    public int RecentWindowMinutes { get; set; } = 15;
    /// <summary>Alert sound: a built-in name (Ding, Notify, Chimes, Chord, Tada,
    /// Exclamation, Alarm) or the full path of a custom .wav/.mp3 file.</summary>
    public string AlertSound { get; set; } = "Ding";
    /// <summary>Position of the floating alert tile; NaN = above the widget.</summary>
    public double AlertLeft { get; set; } = double.NaN;
    public double AlertTop { get; set; } = double.NaN;
    /// <summary>One pin for the whole watch group: show all enabled rules as mini
    /// chips (replaces per-rule Pinned, which is kept for settings compatibility
    /// and migrated on load).</summary>
    public bool PinWatchChips { get; set; }
    /// <summary>Show the quick tour at every launch. Turned off by the tutorial's
    /// "Never show again" button or the Options checkbox. While on, the startup
    /// janitor defers log truncation — the tour's first page is its consent question.</summary>
    public bool ShowTutorial { get; set; } = true;
    /// <summary>Overlay card order (section keys); missing keys append in default order.</summary>
    public List<string> SectionOrder { get; set; } = [];
    /// <summary>Hidden overlay cards (still collect data — OVERLAY acceptance).</summary>
    public List<string> HiddenSections { get; set; } = [];
    /// <summary>Global hotkeys ("Ctrl+Shift+H" style; empty disables one).</summary>
    public string HotkeyToggleOverlay { get; set; } = "Ctrl+Shift+H";
    public string HotkeyClickThrough { get; set; } = "Ctrl+Shift+T";
    public string HotkeyMiniMode { get; set; } = "Ctrl+Shift+M";
    public string HotkeyCampMarker { get; set; } = "Ctrl+Shift+K";

    private static string FilePath => AppPaths.File("settings.json");

    // NaN is a legitimate value here ("not placed yet" window positions), and the
    // default serializer refuses it — which made Save() throw and silently drop
    // every settings change on profiles with an unplaced window.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new();
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex); // corrupted settings — start fresh, but say so
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex); // non-fatal, but visible
        }
    }
}
