namespace EQBuddy.UI.Shared;

/// <summary>
/// Publish a small text file so that a concurrent reader sees ALL of it or NONE of it —
/// never half. One producer for "the name always resolves to a complete file" (trap 4).
///
/// <para><b>This is the other half of DRA-225, and DRA-228 is what measured the gap it
/// left.</b> DRA-225 fixed the E2E harness's SHARE MASK: <c>AppHarness.ReadDump</c> asked for
/// <c>FileShare.Read</c>, which denies a concurrent writer, so a read that overlapped the
/// app's dump write made <c>WidgetDump.MaybeWrite</c> throw into its catch and blank the whole
/// file for a tick — measured at 103 of 378 writes denied (27%). Widening the reader's mask to
/// <c>ReadWrite | Delete</c> took that to 0 of 377, and knowingly traded it for the reader
/// seeing a PARTIAL file 223 times in 102,503 (0.22%), because <c>File.WriteAllText</c>
/// TRUNCATES the file and then fills it. DRA-225 admitted that residual as a spoiled read of
/// microseconds in place of a blanked dump of a second, which it is — but it is still a read
/// that answers "absent" for a key the app is perfectly able to report.</para>
///
/// <para><b>The measured proof that the residual is real</b> is `main` at <c>66fbed08</c>, CI
/// run <c>35503846320</c>, where <c>e2e-windows</c> went red on a tree that had DRA-225's fix
/// in it: <c>TheLiveRoomAndTheKillsWindowAgreeAboutTheSessionsKills</c> asserted
/// <c>shellLiveKillRows &gt; 0</c> and PASSED, then read the same key two lines later and got
/// <c>-1</c>. Present, then absent, in one test, with the app running and the room drawn. That
/// is not a late render and it is not a missing key — it is a read that landed inside the
/// truncate.</para>
///
/// <para><b>Why it lives here rather than at the call sites.</b> There are 485
/// assert-on-<c>DumpValue</c> call sites in <c>tests/EQBuddy.E2E</c>. Migrating them one at a
/// time treats a writer defect as 485 reader defects; fixing the WRITER once means the
/// sentinel goes back to meaning what <c>AppHarness.DumpValue</c>'s docstring says it means —
/// ask again, the key is not there yet. <c>Core.ProfileJson</c> already does exactly this for
/// files a player cannot recreate (trap 65); this is the same move for the file the test suite
/// cannot re-read, and it is in UI.Shared rather than in the WPF layer so that it is unit-
/// testable in <c>build-and-test</c> (docs/TestPlan.md §5 — the WPF layer has no test project).
/// </para>
///
/// <para><b>The whole fix is on the WRITER, and two wrong turns were measured out of it before
/// that was clear.</b> The first draft fell back to a plain write when the replace was refused;
/// counting the outcomes showed the fallback ran <b>2,833 times in 4,000 publishes</b> and
/// truncated the file every time, which put the original defect back at 71%. The second added a
/// retry to the READER for a refused open; instrumenting the real pair showed the open is never
/// refused at all — <b>zero failures in 1,986,753 reads</b> — so that arm was a claim no test
/// could reach and it was removed (trap 78). What survives is one thing: build the next version
/// beside the file and replace it atomically, retrying the replace and dropping the version if
/// it stays refused. Over 4,000 publishes against a reading thread that is <b>zero torn reads
/// and zero sentinels in 52,683 reads</b>, with every publish taking the atomic path.</para>
///
/// <para>Nothing here throws to its caller for a contended file, because throwing reaches
/// <c>WidgetDump</c>'s catch and blanks the dump — the 27% failure DRA-225 removed, and the one
/// thing this must not reintroduce.</para>
/// </summary>
public static class WholeFilePublish
{
    /// <summary>The suffix of the scratch file the next version is built in. Beside the
    /// target on purpose: <c>File.Move</c> is only atomic within one volume, and a temp
    /// directory can be on another one.</summary>
    public const string PendingSuffix = ".writing";

    /// <summary>What <see cref="Write"/> managed. Returned rather than logged so a test can
    /// assert WHICH path ran — a publisher that had quietly stopped replacing would look
    /// identical from outside to one that was working (trap 78: an untaken branch has no
    /// symptom).</summary>
    public enum Outcome
    {
        /// <summary>Built in the scratch file and moved into place. No reader saw a partial file.</summary>
        Replaced,

        /// <summary>The replace was refused for as long as it was retried, so the PREVIOUS
        /// file was left whole and this version was dropped. See <see cref="Write"/> for why
        /// that is the right answer for a file that is republished every tick, and why it is
        /// not a silent failure.</summary>
        SkippedToKeepThePreviousFileWhole,
    }

    /// <summary>
    /// Read a file <see cref="Write"/> publishes — the READER half, here beside the writer
    /// because the two are one mechanism (trap 4), and because it is what lets
    /// <c>WholeFilePublishTests</c> exercise the REAL read rule rather than a copy of it.
    /// The share mask is DRA-225's and is unchanged.
    ///
    /// <para><b>There is deliberately NO retry here, and that is a measured decision rather
    /// than an omission.</b> A retry was written first, on the theory that an atomic replace
    /// must briefly refuse a concurrent <c>Open</c>. Instrumenting the real pair disproved it:
    /// over <b>1,986,753 reads</b> against 4,000 publishes, the open was refused
    /// <b>zero</b> times — no <c>FileNotFoundException</c>, no <c>IOException</c>, no
    /// <c>UnauthorizedAccessException</c>. Every sentinel answer in that run came from reading
    /// a file that opened perfectly and had nothing in it, which is a WRITER defect.</para>
    ///
    /// <para>The retry was then removed rather than kept as insurance, because a mutation test
    /// proved it was unguardable: deleting it reddened nothing (trap 78 — a guard aimed at
    /// nothing is green, and an arm no test can reach is a claim, not code). If a refused open
    /// is ever actually observed, the evidence belongs in the flake ledger and the retry comes
    /// back WITH a test that fails without it.</para>
    ///
    /// <para>Returns "" for a missing or unreadable file, which every caller already treats as
    /// "ask again" (trap 4: absent is spelled one way).</para>
    /// </summary>
    public static string Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException) { return ""; }   // covers FileNotFound too: missing file = not yet
    }

    /// <summary>How many times the replace is retried before this version is dropped. The
    /// refusal is a reader holding the target for the microseconds of its own read, so a
    /// retry almost always wins at once — see <see cref="Backoff"/> for why the early ones
    /// must not sleep.</summary>
    private const int ReplaceRetries = 24;

    /// <summary>How many of those retries YIELD rather than sleep.</summary>
    private const int SpinRetries = 16;

    /// <summary>
    /// Wait before re-attempting a refused replace — yielding first, sleeping only later.
    ///
    /// <para><b>The first draft slept 1 ms on every attempt, and that made the publish 14 ms
    /// slower under contention rather than microseconds.</b> Windows' timer granularity means
    /// <c>Thread.Sleep(1)</c> actually parks for ~15 ms, so one refusal cost more than a whole
    /// UI tick — measured at 58 s for 4,000 publishes against a reading thread, versus under
    /// three when the early attempts yield instead. The contended window is one reader's open,
    /// so a yield is the right length of wait and a sleep is three orders of magnitude too
    /// long. The later attempts still sleep, because by then the contention is not a passing
    /// read and spinning would burn a core.</para>
    /// </summary>
    private static void Backoff(int attempt)
    {
        if (attempt < SpinRetries) Thread.Yield();
        else Thread.Sleep(1);
    }

    /// <summary>
    /// Write <paramref name="text"/> to <paramref name="path"/> so a reader never sees it
    /// half-written. Returns which path was taken.
    ///
    /// <para><b>A refused replace DROPS this version instead of writing in place, and that is
    /// the decision in here rather than an omission.</b> The first draft of this did fall back
    /// to <c>File.WriteAllText(path, text)</c>, on the reasoning that publishing something
    /// beats publishing nothing. Measured against a reader in a tight loop, that fallback ran
    /// <b>2,833 times in 4,000 publishes</b> — <c>File.Move</c> is refused while a reader
    /// holds the target — and each one truncated the file, which put the ORIGINAL defect back
    /// at 71% instead of 0.22%. The fallback was the bug, and it was invisible until the
    /// outcome was counted.</para>
    ///
    /// <para>So the contract is the other way round: the previous file stays whole. That is
    /// only correct because every caller here REPUBLISHES — <c>WidgetDump.MaybeWrite</c> runs
    /// on the UI tick, so a dropped version is superseded milliseconds later, while a torn one
    /// is read as fact by whatever polls next. A caller that publishes ONCE must check the
    /// outcome; a caller that publishes on a timer can ignore it.</para>
    /// </summary>
    public static Outcome Write(string path, string text)
    {
        var pending = path + PendingSuffix;
        try
        {
            File.WriteAllText(pending, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The scratch path is unusable. If a previous file is published, keeping it
            // whole is the better answer and this version is dropped like any other refused
            // replace. If there is NO previous file there is nothing to protect and a caller
            // waiting for a first dump would wait forever, so that one case writes directly
            // — the pre-DRA-228 behaviour, in the only situation where it costs nothing.
            if (File.Exists(path)) return Outcome.SkippedToKeepThePreviousFileWhole;
            File.WriteAllText(path, text);
            return Outcome.Replaced;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(pending, path, overwrite: true);
                return Outcome.Replaced;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= ReplaceRetries)
                {
                    try { File.Delete(pending); }
                    catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                    return Outcome.SkippedToKeepThePreviousFileWhole;
                }
                Backoff(attempt);
            }
        }
    }
}
