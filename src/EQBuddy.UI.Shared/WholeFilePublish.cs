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
/// <para><b>DRA-257 is the third mechanism, and it is why "replace it atomically" above now
/// names a syscall.</b> The paragraph before this one is true about the SHAPE and was wrong
/// about one call: <c>File.Move(pending, path, overwrite: true)</c> is
/// <c>MoveFileEx(MOVEFILE_REPLACE_EXISTING | MOVEFILE_COPY_ALLOWED)</c>, and that is not
/// atomic — there is an instant in which the destination name resolves to NOTHING. So
/// <c>debug.txt</c> was never half-written, and it was briefly ABSENT; a reader in that window
/// gets <c>FileNotFoundException</c>, <see cref="Read"/> answers "", and
/// <c>AppHarness.DumpValue</c> prints <b><c>-1</c></b> — the same sentinel the whole
/// DRA-225 → DRA-228 family exists to remove, arriving by a third route after both of theirs
/// were closed. The first half of this file's own one-line summary was the false one.</para>
///
/// <para><b>The fix is a rename over a live name with POSIX semantics, and the two obvious
/// candidates were both measured and both are wrong</b> — <c>File.Replace</c> (Win32
/// <c>ReplaceFile</c>, which the card named) is the WORST of the four. The arithmetic and the
/// numbers are in <c>AtomicRename</c>, which exists so that the decision has one home rather
/// than a comment beside a call (trap 4). <c>File.Move</c> is kept for the first publish onto a
/// FREE name, where a rename opens no window and <c>Outcome.Created</c> says which arm
/// ran.</para>
///
/// <para><b>Why the writer and not a retry in the reader.</b> A reader retry masks the window
/// for OUR readers and leaves it open for every other reader of the same name — the E2E
/// harness is one consumer of <c>debug.txt</c> and nothing stops a second, and a file whose
/// name intermittently does not resolve is a defect wherever it is read from. Fixing the
/// producer means the guarantee travels with the file instead of with each caller (trap 4),
/// which is the same argument that put this class in front of 485 call sites. <see cref="Read"/>
/// did end up with a bounded retry, and its docstring is careful about what that retry is for:
/// the OS's own mid-unlink transient, and NOT an absent name — which is left visible on
/// purpose, because it is the defect this card was opened for.</para>
///
/// <para><b>Measured, on the box shape where it is visible.</b> 2-core
/// <c>ProcessorAffinity</c> mask, <c>TheRealPublisherIsNeverCaughtHalfway</c>: the
/// <c>File.Move</c> form is red <b>3 runs in 8</b> with bursts of 259–951 absent-name reads in
/// ~75,000, and <c>partialContent=0 zeroByteFile=0 openRefused=0</c> in every one of them —
/// nothing half-written, every publish atomic, the name briefly gone. A 32-core desk is GREEN
/// on that same broken code, so the desk is the measurement that cannot tell the hypothesis
/// from its negation (trap 77) and the mask is the one that can.</para>
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
        /// <summary>Built in the scratch file and swapped over a file that was already
        /// published, through <c>ReplaceFile</c> — the name resolved to the OLD complete file
        /// until the instant it resolved to the new one, and never to neither.</summary>
        Replaced,

        /// <summary>Built in the scratch file and renamed onto a name that was FREE — the
        /// app's first tick, where there is nothing to replace and nothing to protect.
        ///
        /// <para><b>It is a separate value because the two take different syscalls and only
        /// one of them is safe over a live file</b> (DRA-257). A plain rename onto an unused
        /// name cannot expose a window, because the name resolved to nothing before it too;
        /// <c>MoveFileEx(REPLACE_EXISTING)</c> over a live one can, and did. Folding them into
        /// one value is what made the difference untestable: a publisher that had silently
        /// gone back to replace-by-move would look identical from outside to one taking the
        /// atomic path (trap 78), and the only guard left would be a 2-core race that is green
        /// on a 32-core box (trap 77).</para></summary>
        Created,

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
    ///
    /// <para><b>The refused open WAS observed, and the retry is back on exactly the terms the
    /// paragraph above set</b> — with the ledger row and with a test that fails without it
    /// (DRA-257). What the 1,986,753-read run could not see is that it ran on a many-core desk
    /// where the two threads rarely phase-lock; on a 2-core <c>ProcessorAffinity</c> mask —
    /// the CI box's shape — the window is hit in BURSTS (259, 379, 951 in a single run) rather
    /// than as a trickle. "Zero refused opens" was a true count of an unrepresentative sample,
    /// which is trap 77: a measurement that cannot distinguish the hypothesis from its
    /// negation.</para>
    ///
    /// <para><b>THE WRITER FIX CAME FIRST AND IS NOT REPLACED BY THIS.</b> A reader retry
    /// masks a window for OUR readers and leaves it open for anyone else's, so it is the wrong
    /// shape for a producer defect and the card said so. <see cref="Write"/> closes the
    /// absent-name window outright (see <c>AtomicRename</c> — three primitives measured,
    /// one works), and what is left afterwards is 1–2 reads per ~33,000 catching the replaced
    /// directory entry mid-unlink. That residual is not a producer defect anyone can remove;
    /// it is the OS's own transient, and a transient is what a bounded retry is for.</para>
    ///
    /// <para><b>The retry DOES cover an absent name, and that was argued the other way first
    /// — the measurement is what settled it.</b> The first cut returned
    /// <see cref="ReadOutcome.NameDidNotResolve"/> unretried, on the reasoning that absent-name
    /// is DRA-257's own defect and masking it would hide the next writer that stops replacing
    /// atomically. That reasoning is sound and it is answered by a different guard now, because
    /// the premise turned out to be false: the window is not a writer defect anyone can remove.
    /// Four primitives were measured on the mask, and the best of them still leaves it. With
    /// <c>created=0 skipped=0</c> — every publish took the atomic arm, nothing fell back — the
    /// residual is bursts of 314 and 323 CONSECUTIVE absent reads out of ~35,000, which is one
    /// scheduling quantum: a preemption inside the kernel's rename, making an intermediate
    /// state observable. No user-mode call removes that.</para>
    ///
    /// <para><b>What makes masking it safe is that the writer now has a guard that does not
    /// depend on this read at all.</b> <c>APublishOverALiveNameAndAPublishOntoAFreeOneAreDifferentSyscalls</c>
    /// asserts <c>AtomicRename.Renames</c> — the fact, incremented inside the rename that
    /// succeeded — so a revert to <c>File.Move(overwrite: true)</c> reddens deterministically
    /// on ANY box. That is strictly better than what the race test could ever do (it is green
    /// on 32 cores against the broken publisher, trap 77), so the evidence this retry hides is
    /// evidence something else now produces. <b>Do not delete that test and leave this
    /// retry.</b></para>
    ///
    /// <para>Guard: <c>ARefusedOpenIsRetriedRatherThanAnsweredAsAbsent</c> fails without the
    /// loop, deterministically and on any box — it asserts the read does not ANSWER while the
    /// file is locked, so "it returned the right thing" cannot pass it by luck. The budgets are
    /// separate and the reason is in each of them.</para>
    /// </summary>
    public static string Read(string path) => Read(path, out _);

    /// <summary>
    /// <see cref="Read(string)"/>, plus WHY it answered nothing — because "" meant three
    /// different worlds and a caller that has to guess between them writes silence for the
    /// other two (trap 75, and trap 81's "a dashboard may report an absence, never freeze
    /// one"). An absent NAME, a file that opened and held nothing, and a file that opened and
    /// held the wrong thing are three different defects with three different fixes, and the
    /// counter that conflated them is what let DRA-257 hide inside DRA-225's residual for two
    /// cards. Nothing in <c>src/</c> branches on this yet; it is what
    /// <c>WholeFilePublishTests</c> reports its failures in, which is the one place the
    /// distinction has ever had to be drawn.
    /// </summary>
    public static string Read(string path, out ReadOutcome outcome)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        for (var attempt = 0; ; attempt++)
        {
            var text = OpenAndRead(path, out outcome);
            if (outcome == ReadOutcome.Read) return text;

            var budget = outcome == ReadOutcome.NameDidNotResolve ? AbsentNameBudget : RefusedBudget;
            if (waited.Elapsed >= budget) return "";
            Backoff(attempt);
        }
    }

    /// <summary>
    /// How long a read waits out an absent NAME before answering "".
    ///
    /// <para><b>Short, and the number is the measurement.</b> The window is one scheduling
    /// quantum — the observed bursts are 314 and 323 CONSECUTIVE reads out of ~35,000, which
    /// is what a preemption inside the kernel's rename looks like from out here, and why it
    /// never appears on a 32-core desk. 50 ms covers that with several times the margin.</para>
    ///
    /// <para><b>It is deliberately much shorter than <see cref="RefusedBudget"/>, because
    /// absent is also the ORDINARY state of a file that has not been written yet</b> — the
    /// E2E harness polls this name while the app is still starting. That poll is 200 ms
    /// (<c>Wait.Interval</c>), so at 50 ms a file that genuinely is not there yet costs the
    /// harness a quarter of one poll and nothing else.</para>
    /// </summary>
    private static readonly TimeSpan AbsentNameBudget = TimeSpan.FromMilliseconds(50);

    /// <summary>How long a read waits out a REFUSED or DENIED open. Longer than
    /// <see cref="AbsentNameBudget"/> because it means another party is holding the file
    /// rather than that the file is not there, it is rare (1–2 reads in ~33,000 against the
    /// publisher that ships), and no caller polls through it.</summary>
    private static readonly TimeSpan RefusedBudget = TimeSpan.FromMilliseconds(250);

    private static string OpenAndRead(string path, out ReadOutcome outcome)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            outcome = ReadOutcome.Read;
            return reader.ReadToEnd();
        }
        catch (FileNotFoundException) { outcome = ReadOutcome.NameDidNotResolve; return ""; }
        catch (DirectoryNotFoundException) { outcome = ReadOutcome.NameDidNotResolve; return ""; }
        catch (IOException) { outcome = ReadOutcome.OpenRefused; return ""; }
        // UnauthorizedAccessException is NOT an IOException, so until DRA-257 it was not
        // caught at all — it went straight through AppHarness.ReadDump and out of whatever
        // E2E assertion was reading. That was survivable only because nothing produced it:
        // ERROR_ACCESS_DENIED on this open means the name resolves to a file in DELETE-PENDING
        // state, and the writer never left one. DRA-257 measured a rename primitive that does
        // (POSIX semantics — see AtomicRename), which is how the hole was found, and the
        // publisher that shipped is not that one. It is caught anyway: "" is what every other
        // unreadable state answers, and a reader thread dying on a transient is strictly worse
        // than a sentinel the caller already knows how to retry.
        catch (UnauthorizedAccessException) { outcome = ReadOutcome.OpenDenied; return ""; }
    }

    /// <summary>What <see cref="Read(string, out ReadOutcome)"/> managed. The whole point of
    /// the type is that <see cref="Read"/> is <b>not</b> the one that tells them apart —
    /// <see cref="Read"/> answers "" for all three, deliberately, because every caller treats
    /// them the same way. This is for whoever has to say WHICH.</summary>
    public enum ReadOutcome
    {
        /// <summary>The name resolved and the bytes are the answer. An empty answer here is a
        /// file that really is empty — the truncate-then-fill defect DRA-225 measured, and NOT
        /// DRA-257's window.</summary>
        Read,

        /// <summary>The name did not resolve at all. That is DRA-257's window, and it is the
        /// one an <c>IOException</c>-shaped counter cannot tell from a refused open.</summary>
        NameDidNotResolve,

        /// <summary>The name resolved and the open was refused — a share-mask conflict, which
        /// is DRA-225's defect and nobody has observed since it widened the mask.</summary>
        OpenRefused,

        /// <summary>The name resolved to a file the OS would not let us open —
        /// <c>ERROR_ACCESS_DENIED</c>, which for this file means a DELETE-PENDING entry. A
        /// writer can produce it by superseding the name with POSIX semantics; the one that
        /// ships does not, and a non-zero count here is that changing.</summary>
        OpenDenied,
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
            return Outcome.Created;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                // **Two syscalls, and which one is correct depends on whether the name is
                // live** (DRA-257). Over a published file this MUST be ReplaceFile: the whole
                // loop below used to be File.Move(overwrite: true), which is
                // MoveFileEx(REPLACE_EXISTING | COPY_ALLOWED), and that is not atomic — there
                // is an instant in which the destination name resolves to NOTHING. A reader
                // in it gets FileNotFoundException, Read answers "", and AppHarness.DumpValue
                // prints -1: the exact sentinel DRA-225 and DRA-228 exist to remove, arriving
                // by a third mechanism after both of theirs were fixed.
                //
                // Measured on a 2-core ProcessorAffinity mask, which is the CI box's shape and
                // the only one where the two threads phase-lock often enough to see it: the
                // Move form was red 3 runs in 8 with bursts of 259-951 absent-name reads in
                // ~75,000, partialContent 0 and zeroByteFile 0 every time — nothing was ever
                // half-written, the name was briefly gone. On a 32-core desk the same code is
                // green, which is why the desk could not be the evidence (trap 77).
                //
                // Onto a FREE name a plain rename is already safe — the name resolved to
                // nothing beforehand, so there is no window to open — and ReplaceFile refuses
                // an absent destination outright, so the first publish keeps File.Move.
                if (File.Exists(path))
                {
                    AtomicRename.Over(pending, path);
                    return Outcome.Replaced;
                }

                File.Move(pending, path, overwrite: true);
                return Outcome.Created;
            }
            // A destination that vanished between the Exists check and the Replace throws
            // FileNotFoundException, which IS an IOException, so the clause below already
            // catches it and the retry re-reads Exists and takes the Move arm. It gets no
            // clause of its own on purpose: only something OUTSIDE this process can unpublish
            // the name, so a dedicated arm would be one no test could reach — which is the
            // argument that deleted the reader retry from Read, applied to the writer.
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
