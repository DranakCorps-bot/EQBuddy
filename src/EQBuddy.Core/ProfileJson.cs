using System.Text;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>How a profile file came back, which is a different question from what it said.</summary>
public enum ProfileReadOutcome
{
    /// <summary>No file and no backup — a brand-new profile.</summary>
    Missing,
    /// <summary>The file itself parsed.</summary>
    Loaded,
    /// <summary>The file was unreadable and the previous good copy answered instead.</summary>
    RecoveredFromBackup,
    /// <summary>Neither the file nor a backup could be read. The caller starts fresh.</summary>
    Unreadable,
}

/// <summary>
/// Every JSON file EQBuddy keeps in the player's profile is written through here.
///
/// **`File.WriteAllText` truncates the real file and then writes into it, and it does not
/// wait for the disk.** Kill the process in that window — or lose power — and NTFS keeps
/// the new LENGTH while the contents are still zeros, which is why a corrupted profile file
/// reads back as <c>'0x00' is an invalid start of a value</c> rather than as a truncated
/// document. That is not a theory: on 2026-09-07 at 06:59:54 David's Evolved profile threw
/// exactly that exception from <c>AppSettings.Load</c>, <c>AaLedgerStore</c>,
/// <c>StackingLedgerStore</c>, <c>QuestLedgerStore</c> and <c>SpawnCycleLedger</c> — five
/// files, one timestamp, one abrupt termination.
///
/// **What it cost is the whole reported bug.** <c>AppSettings.Load</c> catches that
/// exception, starts from defaults and reports <c>hadFile: false</c> — so the theme went
/// back to ParchmentBrass, <c>ApplyDefaultRules</c> re-seeded the built-in Watch rule
/// against a <c>DefaultRulesVersion</c> that had gone back to 0, and the Watch card came
/// back because <c>HiddenSections</c> was empty. The migration chain then reported work, so
/// <c>Load</c> SAVED those defaults over the corrupt file — the last copy of the player's
/// settings, gone. `install-local.ps1 -Evolved` force-kills the running portable copy when
/// it does not close within 15 seconds, so a republish is precisely the event that lands in
/// the window, which is why this reads to the player as "publishing resets my settings".
///
/// **The fix is that a reader can never see a half-written file.** Write to a temp file,
/// flush it to the DISK (not merely to the OS — <c>Flush(flushToDisk: true)</c> is the
/// whole point, and a plain <c>WriteAllText</c>-then-rename would still order the rename
/// ahead of the data), then swap it in with <see cref="File.Replace(string,string,string)"/>,
/// which is atomic on NTFS and hands the outgoing file to <c>.bak</c> in the same operation.
/// A kill at any instant leaves either the old file or the new one, never zeros.
///
/// **And <see cref="Read{T}"/> is the other half, because the profiles already damaged do
/// not repair themselves.** An unreadable file falls back to <c>.bak</c>, and the bad copy
/// is moved aside to <c>.corrupt</c> rather than being silently overwritten — so the
/// evidence survives, and so does the backup (a <see cref="File.Replace(string,string,string)"/>
/// over a corrupt primary would have pushed the corruption INTO the backup).
/// </summary>
public static class ProfileJson
{
    /// <summary>The previous good copy, kept by every successful <see cref="Write"/>.</summary>
    public static string BackupPath(string path) => path + ".bak";

    /// <summary>Where an unreadable file is set aside, so it is never silently destroyed.</summary>
    public static string CorruptPath(string path) => path + ".corrupt";

    /// <summary>
    /// Write <paramref name="json"/> to <paramref name="path"/> so that no reader — and no
    /// kill — can ever observe a partial file. Throws on failure; call sites keep their own
    /// try/catch, because "could not save" is their decision to report, not this one's.
    /// </summary>
    public static void Write(string path, string json)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Beside the target, never in %TEMP%: File.Replace and File.Move are only atomic
        // within a volume, and a player's profile can sit on a different drive from TEMP.
        var tmp = path + ".new";
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            // The line this class exists for. Without it the rename below can reach the
            // disk before the bytes do, and the crash window is back exactly as it was.
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            try
            {
                File.Replace(tmp, path, BackupPath(path), ignoreMetadataErrors: true);
                return;
            }
            catch (IOException)
            {
                // File.Replace refuses across volumes and on some non-NTFS mounts. A plain
                // overwriting move is still atomic-enough (the old file is replaced in one
                // directory operation) and the flush above has already happened, so the
                // failure mode this class exists to prevent is still prevented.
            }
            catch (PlatformNotSupportedException) { }
        }

        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Read a profile file, falling back to the backup when the file itself is unreadable.
    /// <paramref name="value"/> is null unless the outcome is
    /// <see cref="ProfileReadOutcome.Loaded"/> or
    /// <see cref="ProfileReadOutcome.RecoveredFromBackup"/>.
    /// </summary>
    public static ProfileReadOutcome Read<T>(string path, JsonSerializerOptions? options, out T? value)
    {
        value = default;

        // **A file that is not there is Missing, and the backup is not consulted.** The
        // backup answers exactly one question — "this file is present and will not parse"
        // — and widening it to "present or absent" would turn a DELETED profile file into
        // a resurrected one. Nothing here ever deletes a profile file, so an absent primary
        // is somebody's intent; and <see cref="Write"/> is atomic, so a torn write can no
        // longer produce an absent primary either. Recovery below restores the file in the
        // same breath, which is what keeps that narrow reading safe.
        if (!File.Exists(path)) return ProfileReadOutcome.Missing;

        if (TryParse(path, options, out value)) return ProfileReadOutcome.Loaded;

        // Present and unreadable. Say so, keep it, and try the copy the last good save left.
        CoreLog.Error(new InvalidDataException(
            $"{Path.GetFileName(path)} could not be read; it has been kept as " +
            $"{Path.GetFileName(CorruptPath(path))}. This is what an EQBuddy killed " +
            "mid-write used to leave behind."));

        var backup = BackupPath(path);
        if (File.Exists(backup) && TryParse(backup, options, out value))
        {
            Quarantine(path);
            // Put the profile back together NOW rather than trusting the caller to save.
            // Otherwise a kill between here and the next write leaves no primary at all,
            // which the Missing rule above would then read as a brand-new profile — the
            // very reset this class exists to stop, arriving one launch later.
            try { File.Copy(backup, path, overwrite: true); }
            catch (Exception ex) { CoreLog.Error(ex); }
            return ProfileReadOutcome.RecoveredFromBackup;
        }

        Quarantine(path);
        value = default;
        return ProfileReadOutcome.Unreadable;
    }

    private static bool TryParse<T>(string path, JsonSerializerOptions? options, out T? value)
    {
        value = default;
        try
        {
            var text = File.ReadAllText(path);
            if (text.Length == 0) return false;
            value = JsonSerializer.Deserialize<T>(text, options);
            return value is not null;
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return false;
        }
    }

    /// <summary>
    /// Move the unreadable file aside. One fixed name rather than a timestamp: a launch loop
    /// against a broken file must not fill the profile with copies of it.
    /// </summary>
    private static void Quarantine(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, CorruptPath(path), overwrite: true);
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
