namespace EQBuddy.UI.Shared;

/// <summary>
/// One EQBuddy per profile, on every platform.
///
/// The WPF app has said for a long time why this matters: a second copy "would tail the
/// same logs twice, fight over the global hotkeys, and race on settings.json". It
/// enforces it with a named mutex — a Windows facility — and the Avalonia build's guard
/// simply returned "you're the first" everywhere else. So on Linux and macOS every
/// launch started another full copy.
///
/// The settings race is the expensive half, and it is silent. Each copy loads the whole
/// of settings.json at startup and writes the whole of it back on any change, so the
/// copy that saves last reverts every setting the other one changed since it started —
/// no error, no log line, nothing on screen. Two undecorated always-on-top widgets
/// restore to the same saved position and sit exactly on top of each other, so there is
/// not even a visual clue that a second one exists. Suspected cause of the tick-boxes
/// that would not stay ticked in discussion #169 (joma65).
///
/// Keyed on the profile directory, like the Windows guard, so an isolated
/// EQBUDDY_APPDATA instance still runs alongside a normal one — that is how the app
/// gets tested.
///
/// The claim is deliberately unable to stop EQBuddy from starting. A held lock only
/// counts if a live copy actually answers: the second launch leaves a request behind
/// and waits to see it picked up. A lock file left by a filesystem that will not honour
/// locking, or by a copy that is no longer listening, times out and the new copy starts
/// normally. A widget that will not launch is a far worse bug than two of them.
/// </summary>
public static class SingleInstance
{
    public const string LockFileName = "instance.lock";
    public const string ShowRequestFileName = "show.request";

    /// <summary>Claims the profile. Returns the handle to hold for the process's
    /// lifetime, or null when another copy holds it. Anything unexpected (a read-only
    /// profile, a missing directory we cannot create) claims successfully — see the
    /// class remarks.</summary>
    public static IDisposable? TryClaim(string profileDir)
    {
        try
        {
            Directory.CreateDirectory(profileDir);
            return new FileStream(Path.Combine(profileDir, LockFileName),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;   // held by another copy, or the filesystem refuses to lock
        }
        catch (Exception)
        {
            return AlwaysClaimed;   // never let instance coordination stop the app
        }
    }

    /// <summary>
    /// Is a live EQBuddy holding <paramref name="profileDir"/> right now?
    ///
    /// **Asked by the v1 profile IMPORT, and the reason it lives here is trap 4.** The lock
    /// is a file with a name, and <c>Core.ProfileImport</c> needs to know whether v1 is
    /// running before it copies a profile that v1 writes whole-file and unflushed (trap 65,
    /// on a binary that is frozen and cannot be fixed). Core cannot reference this
    /// assembly, so the choice was a second spelling of <see cref="LockFileName"/> over
    /// there or this method here — and two spellings of one filename is one entry with two
    /// sources for one fact, which would agree until the day one of them moved.
    ///
    /// **It answers NO on anything unexpected, and that is the safe direction for its ONE
    /// caller and would be the wrong one for a different caller.** A profile we cannot
    /// probe is reported as free, which for the import means the copy goes ahead and its
    /// own file reads fail loudly; if this ever gains a caller for which "held" is the safe
    /// answer, it needs its own method rather than a flag. Note the asymmetry with
    /// <see cref="TryClaim"/>, which treats the same unexpected failure as "claimed" —
    /// there, never blocking a launch is what matters.
    /// </summary>
    public static bool IsHeldByAnotherCopy(string profileDir)
    {
        var path = Path.Combine(profileDir, LockFileName);
        try
        {
            if (!File.Exists(path)) return false;
            // The same share mode the holder took. Opening succeeds iff nobody holds it.
            using var probe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;    // held, or a filesystem that refuses to lock
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static readonly IDisposable AlwaysClaimed = new NoLock();

    private sealed class NoLock : IDisposable
    {
        public void Dispose() { }
    }

    /// <summary>Asks the running copy to surface, and reports whether one actually did.
    /// False means nobody consumed the request inside <paramref name="timeout"/> — the
    /// lock is stale or unhonoured and the caller should start normally. The request is
    /// withdrawn in that case so it cannot fire at some later launch.</summary>
    public static bool AskRunningCopyToShow(string profileDir, TimeSpan timeout,
        Action<TimeSpan>? sleep = null)
    {
        var path = Path.Combine(profileDir, ShowRequestFileName);
        try
        {
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        }
        catch (Exception)
        {
            return false;   // could not even ask — start, rather than vanish silently
        }

        sleep ??= System.Threading.Thread.Sleep;
        var step = TimeSpan.FromMilliseconds(50);
        for (var waited = TimeSpan.Zero; waited < timeout; waited += step)
        {
            if (!File.Exists(path)) return true;   // a live copy took it
            sleep(step);
        }
        if (!File.Exists(path)) return true;
        try { File.Delete(path); } catch (Exception) { /* best effort */ }
        return false;
    }

    /// <summary>True once per request: the running copy calls this from its tick and
    /// surfaces itself when it comes back true. Consuming is what tells a waiting second
    /// launch that somebody is home, so it deletes the file even if the caller ignores
    /// the answer.</summary>
    public static bool ConsumeShowRequest(string profileDir)
    {
        var path = Path.Combine(profileDir, ShowRequestFileName);
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
