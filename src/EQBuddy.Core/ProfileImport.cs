using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>Why the one-time v1 import is not on offer — or <see cref="None"/>, which is
/// the only value that permits a copy.</summary>
public enum ProfileImportBlock
{
    /// <summary>Nothing in the way. The player has still not been asked.</summary>
    None,
    /// <summary>This is a 1.x build, or the profile has been redirected somewhere that is
    /// not the product's own (a test, <c>shoot.ps1</c>, <c>tests/EQBuddy.E2E</c>).</summary>
    NotTheProductProfile,
    /// <summary>Nothing at <c>%AppData%\EQBuddy</c> that looks like a profile.</summary>
    NoLegacyProfile,
    /// <summary>The marker is there: this profile has already been offered the import and
    /// answered, either way. The offer never auto-repeats.</summary>
    AlreadyAnswered,
    /// <summary>The Evolved profile holds something. There is no merge path, ever.</summary>
    TargetNotEmpty,
    /// <summary>EQBuddy 1.x is holding its own profile open. A live copy is a corrupt
    /// import wearing a progress bar — and this is the ONE block worth saying out loud,
    /// because the player can fix it and come back.</summary>
    LegacyIsRunning,
}

/// <summary>One top-level thing the import found in the v1 profile: a file, or a directory
/// with its contents rolled up. The manifest is per top-level entry rather than per file
/// because a profile's wiki cache runs to thousands of files and a truncated list is a
/// list that lies about being complete (trap 50).</summary>
/// <param name="Name">The entry's name, relative to the profile root.</param>
/// <param name="IsDirectory">Whether <paramref name="Files"/> rolls up a subtree.</param>
/// <param name="Files">Files this entry contributes to the copy.</param>
/// <param name="Bytes">Their total size.</param>
public readonly record struct ProfileImportEntry(
    string Name, bool IsDirectory, int Files, long Bytes);

/// <summary>What <see cref="ProfileImport.Inspect"/> found: the answer to "may we offer
/// this player their v1 profile, and what would come across".</summary>
/// <param name="Block">The reason there is nothing to offer, or <see cref="ProfileImportBlock.None"/>.</param>
/// <param name="Source">The v1 profile directory this was asked about.</param>
/// <param name="Target">The Evolved profile directory.</param>
/// <param name="Entries">The manifest, top level only. Empty unless the import can run.</param>
/// <param name="LegacyVersion">The version v1 last recorded in its own settings, or null.</param>
/// <param name="TargetContents">What is already in the Evolved profile, named — the thing a
/// <see cref="ProfileImportBlock.TargetNotEmpty"/> refusal has to be able to say.</param>
public sealed record ProfileImportOffer(
    ProfileImportBlock Block,
    string Source,
    string Target,
    IReadOnlyList<ProfileImportEntry> Entries,
    string? LegacyVersion,
    IReadOnlyList<string> TargetContents)
{
    /// <summary>The gate. Nothing copies on any other value, and
    /// <see cref="ProfileImport.Run"/> asks again rather than trusting the caller.</summary>
    public bool CanImport => Block == ProfileImportBlock.None;

    /// <summary>Whether the player should be told anything at all. A profile that is not
    /// the product's, has no v1 beside it, or has already answered gets SILENCE — the offer
    /// is a first-run question, not a nag. "1.x is running" is said, because it is the one
    /// block the player can clear.</summary>
    public bool WorthSaying =>
        Block is ProfileImportBlock.None or ProfileImportBlock.LegacyIsRunning;

    public int FileCount => Entries.Sum(e => e.Files);
    public long ByteCount => Entries.Sum(e => e.Bytes);
}

/// <summary>What actually happened when the copy ran.</summary>
/// <param name="Imported">True only when every file landed and the marker was written.</param>
/// <param name="Files">How many files were copied.</param>
/// <param name="Bytes">Their total size.</param>
/// <param name="Error">Why not, when <paramref name="Imported"/> is false.</param>
public sealed record ProfileImportResult(bool Imported, int Files, long Bytes, string? Error);

/// <summary>The record left in the Evolved profile — written LAST, and the idempotence
/// key. Public and settable because it round-trips through <c>System.Text.Json</c>.</summary>
public sealed class ProfileImportMarker
{
    /// <summary>"imported" or "declined". A decline is recorded too: the offer is a
    /// first-run question and asking it again every launch is trap 55's shape — a one-time
    /// step that was never marked one-time, undoing the player on every start.</summary>
    public string Decision { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>What v1 last recorded as its own version, read out of the source profile's
    /// settings rather than guessed.</summary>
    public string? SourceVersion { get; set; }
    public DateTime WhenUtc { get; set; }
    public int Files { get; set; }
    public long Bytes { get; set; }
    /// <summary>The manifest, top level only — see <see cref="ProfileImportEntry"/>.</summary>
    public List<ProfileImportEntry> Entries { get; set; } = [];
}

/// <summary>
/// The one-time import of a player's EQBuddy 1.x profile into EQBuddy Evolved's own —
/// <c>%AppData%\EQBuddy</c> → <c>%AppData%\EQBuddy Evolved</c>, Fable's transition plan §2,
/// Helm-signed (TR-1).
///
/// **It COPIES and never moves, and that is a public promise made mechanical.**
/// <c>LEGACY-V1.md</c> says "your profile is yours" and "we will not force a migration"; the
/// v1 profile is untouched, byte for byte, so undo is structural — start fresh means clear
/// the Evolved profile, and v1 still has everything. Nothing in this file opens a file in
/// the source for writing.
///
/// **Five rules, each of them a trap this repo has already paid for:**
///
/// 1. **Whole-directory copy with a short transient EXCLUSION list**, never a hand-written
///    include list (trap 30 — a staging list is code a compiler cannot check, and it stops
///    covering the profile the day the profile grows a ledger). A stale exclusion
///    over-copies harmlessly; a stale include silently drops a player's quest ledger.
/// 2. **Into an EMPTY profile only.** There is no merge path, ever: merging two
///    settings.json files is trap 13 with extra steps.
/// 3. **Refuse while v1 is RUNNING.** v1 writes settings.json whole-file and unflushed —
///    the exact torn shape trap 65 describes, on a binary we cannot fix — and holds
///    history.db open. Named, so the player can clear it.
/// 4. **Stage, verify, then commit.** Everything lands in a staging directory INSIDE the
///    target (same volume, so the moves are atomic), the copied settings.json is parsed
///    before anything is committed, and the marker is written LAST. A kill at any point
///    leaves a profile that is still empty and an offer that will come back.
/// 5. **Consent BEFORE any copy** — trap 47, the rule that a consent gate is only as good
///    as its slowest path. Nothing in this class copies without an offer, and nothing in
///    the product asks <see cref="Run"/> without having asked the player first.
///
/// **The moment it runs is part of the design and is NOT negotiable:** before
/// <c>AppSettings.Load</c>, at startup, and therefore before the first save. The widget's
/// <c>_settings</c> is a readonly field handed by reference to every view it builds, so an
/// import that landed after the load would be a settings.json the app's own next save
/// reverts — trap 13 with our hand on the trigger, presenting to the player as "the import
/// did nothing". <c>App.OnStartup</c> is where it is wired for that reason.
/// </summary>
public static class ProfileImport
{
    /// <summary>The marker's file name. Written last; its presence is what makes the offer
    /// one-time.</summary>
    public const string MarkerFileName = "migrated-from.json";

    /// <summary>Where a copy is assembled before it is committed. Inside the target profile
    /// rather than in <c>%TEMP%</c>, for <see cref="ProfileJson"/>'s reason: a move is only
    /// atomic within a volume, and a player's profile can sit on a different drive.</summary>
    public const string StagingDirName = ".import-staging";

    /// <summary>
    /// The transient files that do NOT come across, each with the reason it is here. Trap
    /// 30 says a curated list stops covering its subject; this one is deliberately about
    /// files that are *about a running process* rather than about the player, which is a
    /// closed set, and everything else copies by default.
    /// </summary>
    public static readonly (string Name, string Why)[] Excluded =
    [
        ("debug.txt", "The EQBUDDY_EXPAND state dump — a picture of one tick of a process "
            + "that has exited."),
        ("error.log", "v1's error log. Carrying it across would put v1's crashes in "
            + "Evolved's log, where the next person reading it would date them wrongly."),
        ("door.trigger", "The E2E door-probe rendezvous. A stale one would fire a click."),
        ("instance.lock", "The single-instance claim, and the one exclusion that is "
            + "MANDATORY rather than tidy: the importing copy holds the TARGET's lock open "
            + "with FileShare.None for its whole lifetime, so copying a file over it fails "
            + "the import outright."),
        ("show.request", "The other half of that claim — a stale one surfaces the window."),
    ];

    /// <summary>Suffixes that do not come across. <c>.bak</c> is deliberately absent: it is
    /// <see cref="ProfileJson"/>'s net, and a profile that arrives without one arrives
    /// without the thing that repairs it.</summary>
    public static readonly (string Suffix, string Why)[] ExcludedSuffixes =
    [
        (".corrupt", "A file ProfileJson set aside because it would not parse. Evidence, "
            + "kept where it happened."),
        (".new", "ProfileJson's staging file. Present only mid-write, which is the state "
            + "rule 3 refuses to copy in anyway."),
    ];

    /// <summary>Whether a profile-relative path is transient rather than the player's.
    /// Matches on the top-level NAME as well as the leaf, so a directory of that name is
    /// excluded with its contents.</summary>
    public static bool IsExcluded(string relativePath)
    {
        var name = Path.GetFileName(relativePath.TrimEnd('/', '\\'));
        var top = relativePath.Replace('\\', '/').Split('/')[0];
        if (top is MarkerFileName or StagingDirName) return true;
        foreach (var (excluded, _) in Excluded)
            if (name.Equals(excluded, StringComparison.OrdinalIgnoreCase)
                || top.Equals(excluded, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var (suffix, _) in ExcludedSuffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// May we offer this player their v1 profile, and what would come across?
    ///
    /// Filesystem reads only — it copies nothing and writes nothing, so a caller may ask it
    /// before it has anything to show the player.
    /// </summary>
    /// <param name="source">The v1 profile directory.</param>
    /// <param name="target">The Evolved profile directory.</param>
    /// <param name="productOwnedProfile">
    /// <see cref="AppPaths.IsProductOwnedProfile"/>, passed in rather than read, so this
    /// rule is testable and so the one harness path that overrides it has to say so out
    /// loud at the call site rather than by setting an environment variable this file
    /// happens to read.
    /// </param>
    /// <param name="sourceIsLocked">
    /// Whether a live EQBuddy holds <paramref name="source"/>. Injected because the lock's
    /// file NAME belongs to <c>UI.Shared.SingleInstance</c> and there must be exactly one
    /// spelling of it (trap 4); the app passes
    /// <c>SingleInstance.IsHeldByAnotherCopy</c>.
    /// </param>
    public static ProfileImportOffer Inspect(string source, string target,
        bool productOwnedProfile, Func<string, bool> sourceIsLocked)
    {
        ProfileImportOffer Blocked(ProfileImportBlock block, IReadOnlyList<string>? contents = null) =>
            new(block, source, target, [], null, contents ?? []);

        if (!productOwnedProfile) return Blocked(ProfileImportBlock.NotTheProductProfile);
        if (SameDirectory(source, target)) return Blocked(ProfileImportBlock.NotTheProductProfile);

        // "Looks like a v1 profile" is settings.json being there. A directory with no
        // settings is a folder somebody made, and offering to import it would be an offer
        // that copies nothing.
        if (!File.Exists(Path.Combine(source, "settings.json")))
            return Blocked(ProfileImportBlock.NoLegacyProfile);

        if (File.Exists(Path.Combine(target, MarkerFileName)))
            return Blocked(ProfileImportBlock.AlreadyAnswered);

        var already = LivingContents(target);
        if (already.Count > 0) return Blocked(ProfileImportBlock.TargetNotEmpty, already);

        // LAST of the cheap checks, because it is the only one that can change while the
        // player is looking at the answer — and the only one worth saying out loud.
        if (sourceIsLocked(source)) return Blocked(ProfileImportBlock.LegacyIsRunning);

        return new(ProfileImportBlock.None, source, target, Manifest(source),
            LegacyVersionOf(source), []);
    }

    /// <summary>
    /// Everything in a profile that is the PLAYER'S — i.e. what makes a target non-empty.
    ///
    /// **The exclusions are load-bearing here, not just in the copy.** By the time the
    /// player is asked, the running EQBuddy has already created its own
    /// <c>instance.lock</c> in the target and may have written <c>error.log</c> and
    /// <c>debug.txt</c>; counting those would make "empty" a state no live app can ever be
    /// in, and the offer would be blocked on every machine by the very process making the
    /// offer.
    /// </summary>
    public static IReadOnlyList<string> LivingContents(string target)
    {
        try
        {
            if (!Directory.Exists(target)) return [];
            return Directory.EnumerateFileSystemEntries(target)
                .Select(Path.GetFileName)
                .Where(name => name is { Length: > 0 } && !IsExcluded(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            // A target we cannot read is a target we must not copy into. Say it is
            // occupied: the failure direction that does nothing beats the one that
            // overwrites something we could not see.
            return ["(this folder could not be read)"];
        }
    }

    /// <summary>The top-level manifest of what would come across.</summary>
    private static IReadOnlyList<ProfileImportEntry> Manifest(string source)
    {
        var entries = new List<ProfileImportEntry>();
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(source)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(path);
                if (name is not { Length: > 0 } || IsExcluded(name)) continue;
                if (Directory.Exists(path))
                {
                    var (files, bytes) = RollUp(path);
                    if (files > 0) entries.Add(new(name, true, files, bytes));
                }
                else
                {
                    entries.Add(new(name, false, 1, Size(path)));
                }
            }
        }
        catch (Exception ex) { CoreLog.Error(ex); }
        return entries;
    }

    private static (int Files, long Bytes) RollUp(string dir)
    {
        var files = 0;
        var bytes = 0L;
        foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            if (IsExcluded(Path.GetFileName(path))) continue;
            files++;
            bytes += Size(path);
        }
        return (files, bytes);
    }

    private static long Size(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception) { return 0; }
    }

    /// <summary>What v1 last recorded as its own version — read out of the source's
    /// settings.json as a raw document rather than by deserializing
    /// <see cref="AppSettings"/>, which would run this build's shape over a file written by
    /// a frozen one. Null when it is not there or will not parse; the marker says "unknown"
    /// rather than inventing a number.</summary>
    private static string? LegacyVersionOf(string source) =>
        LegacySetting(source, nameof(AppSettings.LastSeenVersion));

    /// <summary>
    /// The palette the player's EQBuddy 1.x is wearing, so the consent question can be drawn
    /// in it.
    ///
    /// **The dialog is asked before this product has any settings to read a theme out of** —
    /// that ordering is the whole point of the import (see the class remarks) — so without
    /// this it draws in whatever <see cref="AppSettings"/>'s default happens to be, on a
    /// screen whose entire job is to say "you already have EQBuddy 1.x". Asking the SOURCE
    /// is the only honest answer available at that moment, and it is the player's own.
    ///
    /// Null when there is no theme to read; the caller keeps the default rather than
    /// guessing, and an unknown name falls through the theme manager's own fallback.
    /// </summary>
    public static string? LegacyTheme(string source) =>
        LegacySetting(source, nameof(AppSettings.Theme));

    /// <summary>One string out of the source's settings.json, read as a raw document rather
    /// than by deserializing <see cref="AppSettings"/> — which would run this build's shape
    /// over a file written by a frozen one.</summary>
    private static string? LegacySetting(string source, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "settings.json")));
            return doc.RootElement.TryGetProperty(property, out var v)
                && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (Exception)
        {
            return null;   // an unreadable source settings.json is rule 3's business, not this one's
        }
    }

    /// <summary>
    /// Copy the v1 profile in. **Only ever called after the player has said yes.**
    ///
    /// The offer is re-inspected first rather than trusted: a caller holds it across a
    /// dialog the player can leave open, and "1.x is running" is a fact that changes in
    /// that window. Trap 47's rule with the polarity that matters here — the slowest path
    /// must not be the one that skips the check.
    /// </summary>
    public static ProfileImportResult Run(ProfileImportOffer offer,
        bool productOwnedProfile, Func<string, bool> sourceIsLocked)
    {
        var now = Inspect(offer.Source, offer.Target, productOwnedProfile, sourceIsLocked);
        if (!now.CanImport)
            return new(false, 0, 0, $"the import was refused at the last check: {now.Block}");

        var staging = Path.Combine(now.Target, StagingDirName);
        try
        {
            // A staging directory left by a killed run is not evidence of anything — the
            // commit is what matters and it never happened. Clear it rather than refusing.
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);

            var (files, bytes) = CopyTree(now.Source, staging);

            // VERIFY before committing: the one file whose loss resets a player to defaults
            // has to be readable on the far side of the copy. This is the check that would
            // have caught #385's zero-filled settings.json being carried across as-is.
            var staged = Path.Combine(staging, "settings.json");
            if (!File.Exists(staged))
                return Failed(staging, "the copy did not produce a settings.json");
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(staged));
                _ = doc.RootElement.ValueKind;
            }
            catch (Exception ex)
            {
                return Failed(staging,
                    "the copied settings.json would not parse, so nothing was committed "
                    + $"({ex.GetType().Name}). Your EQBuddy 1.x profile is untouched.");
            }

            Commit(staging, now.Target);

            // LAST, and the ordering is the whole idempotence story: a kill before this
            // line leaves a profile that has files in it and no marker, which the next
            // launch reads as TargetNotEmpty and refuses — loudly, naming what is there —
            // rather than silently importing twice.
            WriteMarker(now.Target, new ProfileImportMarker
            {
                Decision = "imported",
                Source = now.Source,
                SourceVersion = now.LegacyVersion,
                WhenUtc = DateTime.UtcNow,
                Files = files,
                Bytes = bytes,
                Entries = [.. now.Entries],
            });

            TryDelete(staging);
            return new(true, files, bytes, null);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return Failed(staging, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ProfileImportResult Failed(string staging, string why)
    {
        TryDelete(staging);
        return new(false, 0, 0, why);
    }

    /// <summary>Records that the player said no, so the question is asked once. Nothing is
    /// copied and the v1 profile is not touched — clearing the Evolved profile removes this
    /// marker along with everything else, which is what "start fresh" already means.
    /// </summary>
    public static void Decline(ProfileImportOffer offer)
    {
        try
        {
            WriteMarker(offer.Target, new ProfileImportMarker
            {
                Decision = "declined",
                Source = offer.Source,
                SourceVersion = offer.LegacyVersion,
                WhenUtc = DateTime.UtcNow,
            });
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    /// <summary>The record of what happened, for the surface that reports it. Null when
    /// this profile has never been offered an import.</summary>
    public static ProfileImportMarker? ReadMarker(string target)
    {
        var path = Path.Combine(target, MarkerFileName);
        return ProfileJson.Read<ProfileImportMarker>(path, null, out var marker)
                is ProfileReadOutcome.Loaded or ProfileReadOutcome.RecoveredFromBackup
            ? marker
            : null;
    }

    private static void WriteMarker(string target, ProfileImportMarker marker) =>
        ProfileJson.Write(Path.Combine(target, MarkerFileName),
            JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>The copy itself: every file under <paramref name="source"/> that is not
    /// excluded, into the same relative place under <paramref name="into"/>. Read-only on
    /// the source side — <see cref="File.Copy(string,string)"/> opens it for reading, which
    /// is the only access this whole class ever asks of a v1 profile.</summary>
    private static (int Files, long Bytes) CopyTree(string source, string into)
    {
        var files = 0;
        var bytes = 0L;
        foreach (var path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path);
            if (IsExcluded(relative) || IsExcluded(Path.GetFileName(path))) continue;
            var destination = Path.Combine(into, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(path, destination, overwrite: true);
            files++;
            bytes += Size(destination);
        }
        return (files, bytes);
    }

    /// <summary>Move the staged copy into the (empty) profile, one top-level entry at a
    /// time. Same volume by construction, so each move is a directory operation rather than
    /// a second copy.</summary>
    private static void Commit(string staging, string target)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(staging))
        {
            var destination = Path.Combine(target, Path.GetFileName(path));
            if (Directory.Exists(path)) Directory.Move(path, destination);
            else File.Move(path, destination, overwrite: true);
        }
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    private static bool SameDirectory(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
