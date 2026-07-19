using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

public sealed class AppSettings
{
    public string? LogFolder { get; set; }
    /// <summary>Folder holding EQBuddySetup.exe for updates; null = auto-detect OneDrive.</summary>
    public string? UpdateFolder { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Minimized { get; set; }
    public List<string> MiniStats { get; set; } = ["kills", "dps"];
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double Opacity { get; set; } = 0.96;
    public double UiScale { get; set; } = 1.0;
    /// <summary>Opacity of the widget's background panel only — text stays fully opaque.</summary>
    public double BackgroundOpacity { get; set; } = 0.95;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EQBuddy", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* corrupted settings — start fresh */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
    }
}
