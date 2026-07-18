using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace EQBuddy.Core;

public sealed record UpdateInfo(Version Latest, string SetupPath);

/// <summary>
/// Local-first update checker: looks for a newer EQBuddySetup.exe in the family's
/// synced OneDrive folder (no internet API involved — OneDrive does the distribution).
/// </summary>
public static class UpdateChecker
{
    private const string FolderName = "EQBuddyDownload";
    private const string SetupName = "EQBuddySetup.exe";

    public static Version CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return Normalize(v);
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>
    /// Locate the shared download folder: an explicit setting wins, then the known family
    /// path, then a shallow scan of this PC's OneDrive roots for "EQBuddyDownload"
    /// (shared folders sync under different paths on each family member's account).
    /// </summary>
    public static string? FindUpdateFolder(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var known = @"C:\Users\david\OneDrive\EQBuddyDownload";
        if (Directory.Exists(known)) return known;

        foreach (var env in (string[])["OneDrive", "OneDriveConsumer", "OneDriveCommercial"])
        {
            var root = Environment.GetEnvironmentVariable(env);
            if (root is null || !Directory.Exists(root)) continue;
            var direct = Path.Combine(root, FolderName);
            if (Directory.Exists(direct)) return direct;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(root))
                {
                    var nested = Path.Combine(sub, FolderName);
                    if (Directory.Exists(nested)) return nested;
                }
            }
            catch { /* ignore inaccessible roots */ }
        }
        return null;
    }

    /// <summary>Read the version stamped into the shared setup exe. Null if absent/unreadable.</summary>
    public static UpdateInfo? Check(string folder)
    {
        try
        {
            var setup = Path.Combine(folder, SetupName);
            if (!File.Exists(setup)) return null;
            var vi = FileVersionInfo.GetVersionInfo(setup);
            if (!Version.TryParse(vi.FileVersion ?? "", out var v)) return null;
            return new UpdateInfo(Normalize(v), setup);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(UpdateInfo info) => info.Latest > CurrentVersion;

    /// <summary>
    /// Copy the setup out of OneDrive into %TEMP% (forces hydration of cloud-only files,
    /// and survives OneDrive sync touching the original), then return the staged path.
    /// </summary>
    public static string StageForInstall(UpdateInfo info)
    {
        var staged = Path.Combine(Path.GetTempPath(), SetupName);
        File.Copy(info.SetupPath, staged, overwrite: true);
        return staged;
    }
}
