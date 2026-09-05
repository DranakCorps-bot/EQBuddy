using System.Text;

namespace EQBuddy.E2E;

/// <summary>
/// The SCREEN, taken as a mutex — the same one <c>scripts/shoot.ps1</c> takes, in the same
/// file, with the same contract.
///
/// **This is the second half of trap 61, and until now the guard was one-sided.**
/// `shoot.ps1` grew a lock file on 2026-09-05 after a cross-seat collision failed a random
/// row of three separate batches; its own comment names the hole this closes:
/// *"`tests/EQBuddy.E2E` launches the same exe and takes no lock, so the lock alone cannot
/// see it."* It compensated with a second guard — refuse when any EQBuddy runs out of a
/// `bin\Release` / `bin\Debug` path — which catches this suite once its app is UP and
/// cannot catch the window between `dotnet test` starting and the first `Process.Start`.
/// A guard that only one of two parties honours is a convention with extra steps.
///
/// **What the collision actually looks like, from either side.** Both harnesses drive
/// always-on-top windows on one desktop and both find their target by title; `shoot.ps1`
/// additionally stands the "running EQBuddy" down by process NAME. So a second seat
/// starting up closes the first seat's in-flight fixture app mid-settle, and the row that
/// fails is whichever one happened to be on screen. Nothing about that row is defective,
/// which is why it reads as a flake and why three sessions chased it.
///
/// **Held for the whole RUN, not for one harness — that is the mirror of `shoot.ps1`
/// holding it for the whole BATCH.** Releasing between tests would leave a gap a shoot
/// batch could take, and the E2E suite would then fail at whichever test came next: the
/// same "random row fails" pathology, arriving from the other direction. So it is acquired
/// lazily on the first <see cref="AppHarness.Launch"/> and held until the test host exits.
/// It cannot go stale — the handle dies with the process — which is the property that lets
/// a refusal be unconditional rather than a heuristic about age.
///
/// **It REFUSES rather than waits**, matching `shoot.ps1` and `DECISIONS.md` (2026-09-05):
/// a batch is ~45 minutes, and a silent 45-minute block is worse than a message naming the
/// holder's pid. <c>EQBUDDY_SCREEN_FORCE=1</c> is this side's <c>-Force</c>.
///
/// **`shoot.ps1`'s SECOND guard has no counterpart here, deliberately.** It also refuses
/// when an EQBuddy is already running out of a `bin\Release` / `bin\Debug` path — a guard
/// that exists *because* this suite took no lock. The symmetric check from this side would
/// refuse on our own straggler (a previous test's app in the seconds between `Kill` and the
/// OS reaping it) and turn a tidy teardown race into a red suite. The lock covers the
/// collision that check was standing in for.
///
/// Nothing here is product code. The contract is duplicated in C# rather than lifted into
/// `src/` on purpose: it is thirty lines describing a file path and a share mode, and a
/// shared helper would put a test-harness concern into a shipping assembly to save nothing.
/// </summary>
internal static class ScreenLock
{
    /// <summary>The same path `scripts/shoot.ps1` opens:
    /// <c>Join-Path ([IO.Path]::GetTempPath()) 'eqbuddy-screen.lock'</c>. Both sides resolve
    /// %TEMP% the same way, so the two agree without either naming a literal directory.</summary>
    internal static string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eqbuddy-screen.lock");

    /// <summary>Set <c>EQBUDDY_SCREEN_FORCE=1</c> to run anyway when the holder is known
    /// dead. It is the counterpart of `shoot.ps1 -Force`, and like that switch it overrides
    /// the refusal only — it never makes this suite touch another harness's app.</summary>
    private const string ForceVariable = "EQBUDDY_SCREEN_FORCE";

    private static readonly object Gate = new();
    private static FileStream? _held;

    /// <summary>
    /// Takes the screen for this test host, once. Subsequent calls are no-ops, so every
    /// harness may ask and only the first one pays.
    /// </summary>
    /// <exception cref="IOException">Another screen job holds it and
    /// <c>EQBUDDY_SCREEN_FORCE</c> is not set.</exception>
    internal static void Acquire()
    {
        lock (Gate)
        {
            if (_held is not null) return;
            try
            {
                _held = Take(Path);
            }
            catch (IOException refused)
            {
                if (Environment.GetEnvironmentVariable(ForceVariable) != "1") throw;
                Console.Error.WriteLine(refused.Message + Environment.NewLine +
                    $"{ForceVariable}=1 given; continuing.");
                return;
            }

            // The handle dies with the process regardless — this only makes an orderly exit
            // orderly. There is no path where a crashed run leaves the desktop claimed.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Release();
        }
    }

    /// <summary>
    /// The take itself, against a named path — the whole contract in one method so
    /// <c>ScreenLockTests</c> can exercise it on a throwaway file rather than on the real
    /// desktop's lock, which by then is held by this very run. **A guard whose refusal path
    /// nobody has ever executed is a guard nobody knows the shape of** (trap 34), and the
    /// refusal is the only half a green suite never reaches.
    /// </summary>
    internal static FileStream Take(string path)
    {
        FileStream stream;
        try
        {
            // FileShare.Read, exactly as shoot.ps1 opens it, so a refused seat can read
            // back WHO holds it instead of reporting an anonymous collision.
            stream = new FileStream(path, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.Read);
        }
        catch (IOException)
        {
            throw new IOException(Refusal(path));
        }

        stream.SetLength(0);
        // ASCII by construction, and written in explicit UTF-8 bytes the way shoot.ps1
        // writes it: this line's whole job is to be read back by a stranger, and under
        // Windows PowerShell 5.1 `Get-Content` decodes as the ANSI code page (trap 54).
        // A holder line nobody can read is worse than no holder line.
        var stamp = Encoding.UTF8.GetBytes(
            $"pid {Environment.ProcessId} | {DateTime.Now:o} | tests/EQBuddy.E2E");
        stream.Write(stamp, 0, stamp.Length);
        stream.Flush();
        return stream;
    }

    /// <summary>Gives the screen back. Called at process exit; safe to call twice.</summary>
    internal static void Release()
    {
        lock (Gate)
        {
            _held?.Dispose();
            _held = null;
        }
    }

    /// <summary>The refusal, naming the holder — pid, time and which harness — because
    /// "the screen is busy" is not something the reader can act on and "pid 21804,
    /// scripts/shoot.ps1, four minutes ago" is.</summary>
    private static string Refusal(string path) =>
        $"Another screen job holds {path} — {Holder(path)}. " +
        "tests/EQBuddy.E2E and scripts/shoot.ps1 own the desktop exclusively (FABLE.md §4); " +
        "running anyway closes that job's fixture app and fails a random row of BOTH runs " +
        "(CLAUDE.md trap 61). Wait for it, or set " + ForceVariable + "=1 if you know the " +
        "holder is gone.";

    private static string Holder(string path)
    {
        try
        {
            // FileShare.ReadWrite: the holder opened for Write, so a reader that does not
            // permit writing is refused by the very lock it is trying to describe.
            using var read = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            using var text = new StreamReader(read, Encoding.UTF8);
            var line = text.ReadToEnd().Trim();
            return line.Length > 0 ? line : "(the holder wrote no identity)";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "(unreadable)";
        }
    }
}
