using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The prove-fail for DRA-228's half of the dump-read flake.**
///
/// <para>DRA-225 fixed the E2E harness's share mask and closed three ledger rows with it. It
/// left one residual, admitted on the card by number: <c>File.WriteAllText</c> TRUNCATES the
/// file and then fills it, so a reader that lands in the gap sees a PARTIAL file — measured
/// there at 223 reads in 102,503 (0.22%). <c>e2e-windows</c> then went red on `main` at
/// <c>66fbed08</c> (run <c>35503846320</c>) with DRA-225's fix already in, and the shape of
/// that red is what makes the residual more than a statistic:
/// <c>TheLiveRoomAndTheKillsWindowAgreeAboutTheSessionsKills</c> asserted
/// <c>shellLiveKillRows &gt; 0</c>, PASSED, and read <c>-1</c> for the SAME key two lines
/// later.</para>
///
/// <para>These tests live in <c>EQBuddy.Tests</c> — inside `build-and-test` — rather than in
/// the E2E suite on purpose. The defect they cover is what makes `e2e-windows` flaky, and
/// DRA-228 is the card that makes `e2e-windows` a REQUIRED check; a guard for it that could
/// only run in the lane it is meant to stabilise would be the wrong way round.</para>
///
/// <para>Nothing here is timing-dependent. Each state is BUILT — a truncated file, a
/// half-published one, an unusable scratch path — because a fix for a read that is green
/// 99.78% of the time is vacuous unless the bad instant is constructed (trap 34).</para>
/// </summary>
public sealed class WholeFilePublishTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("eqb-dra228-").FullName;

    private string Target => Path.Combine(_dir, "debug.txt");
    private string Pending => Target + WholeFilePublish.PendingSuffix;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A dump shaped like the one the app writes: space-separated `key=value`.</summary>
    private const string FullDump = "tick=42 kills=10 shellLiveKillRows=10 party=3 shellLivePartyRows=3";

    private const string NextDump = "tick=43 kills=11 shellLiveKillRows=11 party=3 shellLivePartyRows=3";

    /// <summary>The E2E harness's read, through the SAME <see cref="WholeFilePublish.Read"/>
    /// that <c>AppHarness.ReadDump</c> calls — not a second copy of the rule (trap 4) — plus
    /// the "absent key answers -1" sentinel <c>AppHarness.DumpValue</c> applies on top.</summary>
    private int ReadKeyAsTheHarnessWould(string key) => ReadKeyAsTheHarnessWould(key, out _);

    private int ReadKeyAsTheHarnessWould(string key, out TornMode mode)
    {
        var text = WholeFilePublish.Read(Target, out var outcome);

        foreach (var pair in text.Split(' '))
            if (pair.StartsWith(key + "=", StringComparison.Ordinal) &&
                int.TryParse(pair.AsSpan(key.Length + 1), out var value))
            {
                mode = TornMode.NotTorn;
                return value;
            }

        mode = outcome switch
        {
            WholeFilePublish.ReadOutcome.NameDidNotResolve => TornMode.AbsentName,
            WholeFilePublish.ReadOutcome.OpenRefused => TornMode.OpenRefused,
            WholeFilePublish.ReadOutcome.OpenDenied => TornMode.OpenDenied,
            _ when text.Length == 0 => TornMode.ZeroByteFile,
            _ => TornMode.PartialContent,
        };
        return -1;
    }

    /// <summary>
    /// **Why the sentinel is counted BY MODE and not as one number** (done bar 3 of DRA-257).
    /// <c>torn reads: N of M</c> said the same sentence for FIVE different defects with five
    /// different fixes, and that is precisely how DRA-257 stayed hidden inside DRA-225's
    /// admitted residual for two cards: the failure text a seat greps is the only thing
    /// telling them which mechanism fired, and five rows of <c>docs/ops/flake-ledger.md</c>
    /// already say "assert text NOT captured" because that text did not repay capturing.
    ///
    /// <para>The discriminator is not cosmetic and it is not the test's own guess — it comes
    /// off <see cref="WholeFilePublish.ReadOutcome"/>, which is the reader's own answer at the
    /// instant of the read, so a classification cannot drift from the read it describes
    /// (trap 4). <see cref="ZeroByteFile"/> is DRA-225's truncate-then-fill;
    /// <see cref="AbsentName"/> is DRA-257's rename window; <see cref="OpenRefused"/> is
    /// DRA-225's share mask; <see cref="OpenDenied"/> is a delete-pending entry, whose
    /// exception `Read` was not catching at all. Each of the five is produced on demand by
    /// <c>TheTornReadCounterTellsTheFiveModesApart</c> — a counter whose buckets have never
    /// been shown to fire is a detector aimed at nothing (trap 78).</para>
    /// </summary>
    private enum TornMode
    {
        NotTorn,

        /// <summary>The open threw <c>FileNotFoundException</c>: <c>debug.txt</c> named
        /// nothing for that instant. **DRA-257** — the kernel's rename caught mid-step by a
        /// preemption. Retried by `Read` since DRA-257; the WRITER's guard is
        /// <c>APublishOverALiveNameAndAPublishOntoAFreeOneAreDifferentSyscalls</c>.</summary>
        AbsentName,

        /// <summary>The open was refused while the name resolved. **DRA-225's share
        /// mask**, which is fixed; a non-zero count here is that regressing.</summary>
        OpenRefused,

        /// <summary>The name resolved to a DELETE-PENDING entry — <c>ERROR_ACCESS_DENIED</c>.
        /// **A writer that supersedes with POSIX semantics produces this** — 1-2 reads in
        /// ~33,000, which is how DRA-257 found that `Read` was not catching
        /// `UnauthorizedAccessException` at all. Retried since.</summary>
        OpenDenied,

        /// <summary>The file opened and had nothing in it. **DRA-225's truncate-then-fill
        /// writer**, which is what <c>TheOldTruncateThenFillWriterExposesAFileWithNoKeysInIt</c>
        /// reproduces on purpose.</summary>
        ZeroByteFile,

        /// <summary>The file opened, held bytes, and the key was not among them — a genuinely
        /// half-written file, which no run has yet produced and which neither fix above
        /// addresses.</summary>
        PartialContent,
    }

    /// <summary>Every mode with its count, including the zeroes — a breakdown that printed
    /// only what fired would make "this run saw no zero-byte reads" and "this build no longer
    /// counts zero-byte reads" the same screen (trap 78).</summary>
    private static string TornBreakdown(int[] byMode) =>
        string.Join(" ", Enum.GetValues<TornMode>()
            .Where(m => m != TornMode.NotTorn)
            .Select(m => $"{char.ToLowerInvariant(m.ToString()[0])}{m.ToString()[1..]}={byMode[(int)m]}"));

    /// <summary>
    /// **The counter's five buckets, each fired on demand** — trap 78, and the reason this
    /// test exists at all: DRA-257's evidence is a breakdown printed by a failing assertion,
    /// so a bucket that cannot fire would make the next seat's diagnosis confidently wrong
    /// rather than merely absent. Each state is BUILT, not waited for.
    ///
    /// <para><see cref="TornMode.PartialContent"/> is the one no run has ever produced by
    /// race, which is exactly why it is constructed here: it is the bucket a reader would
    /// otherwise have no reason to believe in.</para>
    /// </summary>
    [Fact]
    public void TheTornReadCounterTellsTheFiveModesApart()
    {
        File.WriteAllText(Target, FullDump);
        Assert.Equal(10, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var whole));
        Assert.Equal(TornMode.NotTorn, whole);

        // DRA-257: the name resolves to nothing. This is the state MoveFileEx opens a window on.
        File.Delete(Target);
        Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var absent));
        Assert.Equal(TornMode.AbsentName, absent);

        // DRA-225's truncate-then-fill: the name resolves, to nothing.
        File.WriteAllText(Target, "");
        Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var empty));
        Assert.Equal(TornMode.ZeroByteFile, empty);

        // A genuinely half-written file: bytes, no key.
        File.WriteAllText(Target, "tick=42 kills=10 shellLive");
        Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var partial));
        Assert.Equal(TornMode.PartialContent, partial);

        // DRA-225's share mask: the name resolves and the open is refused.
        File.WriteAllText(Target, FullDump);
        using (new FileStream(Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var refused));
            Assert.Equal(TornMode.OpenRefused, refused);
        }

        // ERROR_ACCESS_DENIED: the name resolves and the OS refuses the open outright. This
        // is the mode whose exception `Read` did not catch AT ALL before DRA-257 —
        // UnauthorizedAccessException is not an IOException, so it went through
        // AppHarness.ReadDump and out of whatever assertion was reading.
        //
        // It was OBSERVED for real, once, and that is why the clause exists: a rename that
        // supersedes with POSIX semantics leaves the old entry delete-pending, and a reader
        // landing there killed the reader thread outright in a measured run. That producer is
        // a race and cannot be constructed, so the arm is fired here the deterministic way —
        // a DIRECTORY standing at the name, which denies the open for a different reason and
        // reaches the same clause. What is being pinned is the CLASSIFICATION, not the
        // mechanism (trap 78: the bucket must be shown to fire).
        File.Delete(Target);
        Directory.CreateDirectory(Target);
        Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows", out var denied));
        Assert.Equal(TornMode.OpenDenied, denied);
        Directory.Delete(Target);

        // And the breakdown names every mode even at zero, so a bucket cannot go missing quietly.
        var text = TornBreakdown(new int[Enum.GetValues<TornMode>().Length]);
        Assert.Equal("absentName=0 openRefused=0 openDenied=0 zeroByteFile=0 partialContent=0", text);
    }

    // ---- The committed negative: the old writer really does expose a torn file ----

    /// <summary>
    /// **The state that produced the CI red, built rather than waited for** (trap 78: a guard
    /// aimed at nothing is green). <c>File.WriteAllText</c> opens <c>FileMode.Create</c>,
    /// which truncates on OPEN — so between the open and the fill, the file that
    /// <c>debug.txt</c> names is zero bytes and every key in it reads absent.
    ///
    /// <para>This is the whole defect in three lines, and it is asserted on the exact pair
    /// from the failing test: a key that was present a moment ago answers the sentinel now,
    /// with no app fault and no missing key. If this test ever goes green, the mechanism
    /// below has stopped being worth guarding against and the guard should go, not be
    /// loosened.</para>
    /// </summary>
    [Fact]
    public void TheOldTruncateThenFillWriterExposesAFileWithNoKeysInIt()
    {
        File.WriteAllText(Target, FullDump);
        Assert.Equal(10, ReadKeyAsTheHarnessWould("shellLiveKillRows"));

        // File.WriteAllText's own open, HELD at the instant after the truncate. The fill has
        // not happened; the name still resolves, and resolves to nothing.
        using (new FileStream(Target, FileMode.Create, FileAccess.Write,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal(-1, ReadKeyAsTheHarnessWould("shellLiveKillRows"));
            Assert.Equal(-1, ReadKeyAsTheHarnessWould("tick"));
        }
    }

    // ---- The fix: the name never resolves to a half-written file -----------------

    /// <summary>
    /// **The measurement DRA-225 made, re-run against the real publisher — and this is the
    /// test a mutation reddens.** It drives <see cref="WholeFilePublish.Write"/> itself in a
    /// loop on one thread while another reads the published name exactly as
    /// <c>AppHarness</c> does, and asserts the reader NEVER sees a file without the key in
    /// it.
    ///
    /// <para>It is a hammer, but its assertion is not statistical: under an atomic replace
    /// the count of torn reads is ZERO by construction, not merely small. DRA-225 measured
    /// 223 partial reads in 102,503 against the truncate-then-fill writer and accepted them;
    /// the bar here is none at all.</para>
    ///
    /// <para><b>Prove-fail, run before this was committed:</b> replacing the body of
    /// <see cref="WholeFilePublish.Write"/> with a plain
    /// <c>File.WriteAllText(path, text); return Outcome.Replaced;</c> reddens this with
    /// <c>torn reads: N of 4000</c> — the CI red's mechanism, reproduced on demand. The
    /// reader also counts how many reads SAW something, so a reader that silently stopped
    /// reading cannot pass this by observing nothing (trap 34).</para>
    ///
    /// <para><b>The reader is HANDED OFF before the publish loop starts, and that is
    /// DRA-255</b> (run <c>35515932572</c>, PR #737). The reader used to go onto
    /// <c>Task.Run</c> and the publishes began immediately; on a starved CI pool the task was
    /// scheduled zero times before <c>done.Cancel()</c>, so its loop condition was false on
    /// first evaluation, <c>reads</c> stayed 0, and the anti-vacuity line below correctly
    /// refused to claim anything — blocking a merge for three and a half hours. The race was
    /// in the TEST's setup and not in <see cref="WholeFilePublish"/>; the PR's diff was two
    /// markdown appends and <c>e2e-windows</c> was green beside it. Two changes, and the
    /// second does not replace the first: the reader is a dedicated thread
    /// (<see cref="TaskCreationOptions.LongRunning"/>) so a saturated pool cannot starve a
    /// loop that by design spins for the whole publish run, AND it SIGNALS after its first
    /// completed read, waited on with a bounded timeout, so "the reader is running" is a fact
    /// this test establishes rather than assumes. A timeout fails with its own sentence
    /// naming what did not happen — it is not the torn-read assertion, and the anti-vacuity
    /// line stays exactly as it was (trap 34: it is the only thing between this test and
    /// vacuous coverage, and it is what CAUGHT this).</para>
    ///
    /// <para><b>THIS TEST IS RED UNTIL DRA-257 LANDS, AND THAT IS THE POINT OF DRA-257.</b>
    /// Handing the reader off before the publish loop means it overlaps the publisher for the
    /// whole run instead of by luck — and the REAL publisher then fails on a 2-core
    /// `ProcessorAffinity` mask, 2 runs in 8:
    /// <c>torn reads: 2566 of 77844 [empty=2566 partial=0 skipped=0 | notFound=2566 io=0
    /// zeroByte=2 unauth=0]</c>. Every torn read is a <c>FileNotFoundException</c> — nothing was
    /// half-written (<c>partial=0</c>, <c>zeroByte=0</c>) and every publish took the atomic path
    /// (<c>skipped=0</c>). The target NAME briefly does not resolve, because
    /// <c>File.Move(overwrite: true)</c> is <c>MoveFileEx(REPLACE_EXISTING)</c> and that is not
    /// atomic. <c>Read</c> catches it under <c>catch (IOException)</c>, answers "", and
    /// <c>DumpValue</c> prints <c>-1</c> — the same sentinel DRA-225 and DRA-228 chased.
    /// **That defect is not new and this change did not cause it; it stopped hiding it** — the
    /// pre-DRA-255 shape measured 44k–58k reads over 8 mask-3 runs with <c>notFound=0</c>
    /// because its reader started at an arbitrary phase. <b>Do not merge this ahead of DRA-257
    /// and do not "fix" it by loosening either assertion.</b> A 32-core run is green on the
    /// broken publisher, so it is the measurement that cannot tell the hypothesis from its
    /// negation (trap 77) — prove it on the mask.</para>
    /// </summary>
    [Fact]
    public void TheRealPublisherIsNeverCaughtHalfway()
    {
        File.WriteAllText(Target, FullDump);

        const int Publishes = 4000;
        var torn = 0;
        var reads = 0;
        var byMode = new int[Enum.GetValues<TornMode>().Length];
        using var done = new CancellationTokenSource();
        using var readerIsRunning = new ManualResetEventSlim(false);

        void ReadOnce()
        {
            var kills = ReadKeyAsTheHarnessWould("shellLiveKillRows", out var mode);
            reads++;
            // -1 here can only mean a file with no such key. The key is in BOTH dumps,
            // and the file exists throughout, so any sentinel is a torn read — and WHICH
            // of the four ways it was torn is the only thing that names the mechanism, so
            // it is counted rather than summed away (see TornMode).
            if (kills < 0) { torn++; byMode[(int)mode]++; }
        }

        var reader = Task.Factory.StartNew(() =>
        {
            // One read BEFORE the loop, so the signal means "a read has COMPLETED" rather
            // than "a thread reached this line" — and set ONCE, off the hot loop.
            ReadOnce();
            readerIsRunning.Set();

            while (!done.IsCancellationRequested) ReadOnce();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Assert.True(readerIsRunning.Wait(TimeSpan.FromSeconds(30)),
            "the reader did not complete a single read in 30 s, so the publish loop below "
            + "would have measured nothing — that is this test's own setup failing to start, "
            + "not a fault in WholeFilePublish");

        var skipped = 0;
        var created = 0;
        for (var i = 0; i < Publishes; i++)
            switch (WholeFilePublish.Write(Target, i % 2 == 0 ? FullDump : NextDump))
            {
                case WholeFilePublish.Outcome.SkippedToKeepThePreviousFileWhole: skipped++; break;
                case WholeFilePublish.Outcome.Created: created++; break;
            }

        done.Cancel();
        reader.Wait();

        Assert.True(reads > 0, "the reader never ran, so this proved nothing");
        Assert.True(torn == 0,
            $"torn reads: {torn} of {reads} — {TornBreakdown(byMode)} "
            + $"[created={created} skipped={skipped}]. "
            + "Read the MODE, not the total: absentName is DRA-257's MoveFileEx window "
            + "(the name resolved to nothing), zeroByte is DRA-225's truncate-then-fill, "
            + "openRefused is DRA-225's share mask, partial is a genuinely half-written "
            + "file and no run has ever produced one. They have different fixes.");

        // **And the publisher did not buy that by going quiet.** Keeping the previous file
        // whole is the right answer to a refused replace, but a publisher that took it
        // routinely would be a dump that stops advancing — the frozen-debug.txt picture
        // WidgetDump's own catch exists to distinguish, and it would read as zero torn reads
        // here (trap 11: a measurement only one side can produce is a verdict, not a vote).
        // Dropping the replace retry reddens this line and not the one above it.
        //
        // **The bar is a RATE and not zero, and the first version of it was zero.** That
        // passed on this desk and on the pull request, then failed on `main`'s own hosted
        // runner with `1 of 4000` (run `35507162594`) — a two-core box, a reader thread
        // spinning against the publisher, and one publish that exhausted a deliberately
        // finite retry budget. **Dropping that version is the CONTRACT working, not a
        // defect:** the previous dump stayed whole, which is the whole point of the skip
        // arm. A guard that reddens for the scheduler rather than for the change is the one
        // trap 74 warns about — it teaches the next person to re-run until green, and then
        // nobody believes it. So the assertion measures what it actually cares about: that
        // skipping is RARE rather than routine. Observed worst case 0.025%, ceiling 1%,
        // mutant (no retry at all) 51% — two orders of margin on each side.
        Assert.True(skipped * 100 <= Publishes,
            $"publishes dropped to keep the previous file: {skipped} of {Publishes} "
            + $"(ceiling is 1%, {Publishes / 100}); dropping the replace retry gives about half of them");
    }

    /// <summary>
    /// **The prove-fail for the reader retry DRA-228 deleted and DRA-257 brought back.** It
    /// was deleted because it reddened nothing — an arm no test can reach is a claim, not code
    /// (trap 78) — and the instruction left behind was that it comes back "WITH a test that
    /// fails without it". This is that test.
    ///
    /// <para><b>What it asserts is that the read does not ANSWER, not that it answers
    /// correctly</b>, and the difference is the whole guard. A read with no retry returns ""
    /// the instant the open is refused; a read with one is still going. So the lock is
    /// released by a thread started BEFORE the read, and the assertion is that the release had
    /// already happened by the time the read came back. Delete the retry loop and the first
    /// line reddens on any box, with no race to lose — whereas "it eventually read the right
    /// bytes" would pass on the broken version the moment the lock was released early.</para>
    ///
    /// <para>The holder is released after 25 ms against a retry budget of roughly 240 ms, so
    /// the margin is an order of magnitude, and the releasing thread is dedicated rather than
    /// pooled — a starved pool is what made DRA-255's own setup misreport (and the reason that
    /// card exists at all).</para>
    /// </summary>
    [Fact]
    public void ARefusedOpenIsRetriedRatherThanAnsweredAsAbsent()
    {
        File.WriteAllText(Target, FullDump);

        var holder = new FileStream(Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var released = new ManualResetEventSlim(false);

        var releaser = new Thread(() =>
        {
            Thread.Sleep(25);
            holder.Dispose();
            released.Set();
        }) { IsBackground = true };
        releaser.Start();

        var text = WholeFilePublish.Read(Target, out var outcome);

        Assert.True(released.IsSet,
            "Read answered while the file was still locked, so it did not retry a refused "
            + "open — that is the DRA-228 behaviour, and it is what turns the OS's own "
            + "mid-unlink transient into a -1 sentinel in the E2E harness");
        Assert.Equal(WholeFilePublish.ReadOutcome.Read, outcome);
        Assert.Equal(FullDump, text);

        releaser.Join();
    }

    /// <summary>
    /// **A name that is absent for good still answers, quickly, and still says so.** Both
    /// halves are load-bearing and neither is implied by the retry existing.
    ///
    /// <para><b>The TIME is the half that protects the E2E harness.</b> Absent is the
    /// ORDINARY state of this file while the app is starting, and <c>AppHarness</c> polls it
    /// through <c>Wait.Until</c> every 200 ms. A retry ladder sized for a locked file — 250 ms
    /// — would have more than doubled the cost of every one of those polls, so the absent-name
    /// budget is its own, shorter number. This asserts the budget is being applied rather than
    /// taken from the other one; swapping the two constants reddens it.</para>
    ///
    /// <para><b>The OUTCOME is the half that keeps the mask honest.</b> The retry hides the
    /// window from a caller that only reads the string, but <see cref="WholeFilePublish.Read"/>
    /// still REPORTS what the final attempt saw — so "" after a full ladder is
    /// distinguishable from "" out of an empty file, and the mode counter in the race test
    /// stays able to name a regression (trap 75).</para>
    /// </summary>
    [Fact]
    public void ANameThatIsAbsentForGoodAnswersInsideTheShorterBudgetAndSaysWhy()
    {
        Assert.False(File.Exists(Target));

        var started = System.Diagnostics.Stopwatch.StartNew();
        var text = WholeFilePublish.Read(Target, out var outcome);
        started.Stop();

        Assert.Equal("", text);
        Assert.Equal(WholeFilePublish.ReadOutcome.NameDidNotResolve, outcome);
        Assert.True(started.ElapsedMilliseconds < 200,
            $"an absent name took {started.ElapsedMilliseconds} ms to answer. That is the "
            + "REFUSED budget, not the absent-name one — and absent is the ordinary state of "
            + "this file while the app is starting, so the harness would pay it on every "
            + "200 ms poll");
    }

    /// <summary>
    /// **The older rename class works, which is what makes the fallback code rather than a
    /// claim** (trap 78). `AtomicRename` asks for `FileRenameInfoEx` — Windows 10 1709 and
    /// later — and this project pins no `SupportedOSPlatformVersion`, so an older kernel is
    /// reachable and would answer `ERROR_INVALID_PARAMETER` to every publish. Without a
    /// fallback that is not a degraded dump, it is `WholeFilePublish.Write` spinning and then
    /// SLEEPING through 24 retries on the UI thread, every tick, and then dropping the
    /// version — forever, silently.
    ///
    /// <para><b>The SELECTION cannot be tested here and this does not pretend to test it</b>:
    /// no box this repo runs on refuses the newer class, so the branch that latches
    /// `_useLegacyClass` is unreachable in the suite. What IS tested is the thing that would
    /// actually be broken — the legacy call, which shares one buffer builder with the primary
    /// path (trap 4), so a layout change cannot fix one and break the other. That is the
    /// difference between an untested branch and untested CODE.</para>
    /// </summary>
    [Fact]
    public void TheLegacyRenameClassStillPublishes()
    {
        File.WriteAllText(Target, FullDump);
        File.WriteAllText(Pending, NextDump);
        var renames = AtomicRename.Renames;

        AtomicRename.RenameLegacy(Pending, Target);

        Assert.Equal(NextDump, File.ReadAllText(Target));
        Assert.False(File.Exists(Pending), "the scratch file outlived the rename");
        Assert.Equal(renames + 1, AtomicRename.Renames);
        Assert.Equal(11, ReadKeyAsTheHarnessWould("shellLiveKillRows"));
    }

    /// <summary>The publish lands, atomically: after it returns the reader sees the NEXT
    /// dump whole, and the scratch file is gone rather than left beside the target where the
    /// next run would find it.</summary>
    [Fact]
    public void ThePublishReplacesTheWholeFileAndLeavesNoScratchBehind()
    {
        File.WriteAllText(Target, FullDump);

        Assert.Equal(WholeFilePublish.Outcome.Replaced, WholeFilePublish.Write(Target, NextDump));

        Assert.Equal(NextDump, File.ReadAllText(Target));
        Assert.Equal(11, ReadKeyAsTheHarnessWould("shellLiveKillRows"));
        Assert.False(File.Exists(Pending), "the scratch file outlived the publish");
    }

    /// <summary>A first publish, onto a name that does not exist yet — the app's very first
    /// tick. <c>File.Move(overwrite: true)</c> is specified for a missing destination, and
    /// asserting it here is what stops the fallback silently becoming the normal path on
    /// every launch.
    ///
    /// <para>It answers <see cref="WholeFilePublish.Outcome.Created"/> since DRA-257, not
    /// because the rename got less atomic but because the two arms now take different
    /// syscalls and the enum is where that is visible. Onto a free name a rename opens no
    /// window — the name resolved to nothing before it as well — so "atomic path too" still
    /// holds; see <c>APublishOverALiveNameAndAPublishOntoAFreeOneAreDifferentSyscalls</c> for
    /// the arm this one is the pair of.</para></summary>
    [Fact]
    public void TheFirstPublishOntoAnAbsentNameTakesTheAtomicPathToo()
    {
        Assert.False(File.Exists(Target));

        Assert.Equal(WholeFilePublish.Outcome.Created, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(FullDump, File.ReadAllText(Target));
    }

    /// <summary>
    /// **The DETERMINISTIC guard for DRA-257, and the reason the enum grew a member.** The
    /// race test above reddens on a 2-core mask and is GREEN on a 32-core desk against the
    /// very same broken publisher (trap 77), so a revert to `File.Move(overwrite: true)` over
    /// a live name would pass `build-and-test` on every hosted runner with enough cores. That
    /// is a guard whose firing depends on the scheduler — exactly the shape trap 74 says
    /// teaches people to re-run until green.
    ///
    /// <para><b>Asserting the OUTCOME here was the first attempt and it did not work</b>,
    /// which is worth keeping because it is the whole trap: reverting the live-name arm to
    /// <c>File.Move(overwrite: true)</c> — the DRA-257 defect exactly — left every test green,
    /// because the mutant still returned <see cref="WholeFilePublish.Outcome.Replaced"/>. The
    /// enum's own docstring claims it lets a test "assert WHICH path ran", and it does not: it
    /// is a label the publisher writes about itself, and reading it as evidence of a syscall is
    /// trap 64b. So this asserts the FACT — <c>AtomicRename.Renames</c>, incremented inside the
    /// rename that succeeded — and the outcome values are checked beside it as the contract
    /// they are.</para>
    ///
    /// <para>The sequence matters: the same target is published onto twice, so the second
    /// publish differs from the first in nothing except that a file is now there. The counter
    /// is process-wide and this is the only class in the suite that publishes, which is what
    /// makes the exact deltas readable rather than a floor.</para>
    /// </summary>
    [Fact]
    public void APublishOverALiveNameAndAPublishOntoAFreeOneAreDifferentSyscalls()
    {
        var renames = AtomicRename.Renames;

        // A free name: renamed onto, never renamed OVER, so the atomic renamer is not asked.
        Assert.False(File.Exists(Target));
        Assert.Equal(WholeFilePublish.Outcome.Created, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(renames, AtomicRename.Renames);

        // A live name: this is the publish that had the window, and it must go through the
        // renamer. `Outcome.Replaced` alone does NOT establish that — a File.Move revert
        // returns it too.
        Assert.Equal(WholeFilePublish.Outcome.Replaced, WholeFilePublish.Write(Target, NextDump));
        Assert.Equal(renames + 1, AtomicRename.Renames);
        Assert.Equal(NextDump, File.ReadAllText(Target));

        // And it keeps going through it — a publisher that took the atomic arm once and then
        // drifted back onto the move would be invisible to a single-publish assertion.
        Assert.Equal(WholeFilePublish.Outcome.Replaced, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(renames + 2, AtomicRename.Renames);

        // Unpublished again, the free-name arm comes back: the decision is read off the name
        // every publish, not latched at the first one.
        File.Delete(Target);
        Assert.Equal(WholeFilePublish.Outcome.Created, WholeFilePublish.Write(Target, NextDump));
        Assert.Equal(renames + 2, AtomicRename.Renames);
    }

    // ---- The fallback arm, which has no symptom until it is asked for ------------

    /// <summary>
    /// **An unusable scratch path keeps the PREVIOUS dump whole rather than throwing**
    /// (trap 78: the arm has no symptom until it is asked for). A directory standing where
    /// the scratch file goes makes the atomic path impossible.
    ///
    /// <para>Two things are asserted and the second is the one that matters: it does not
    /// throw — throwing reaches <c>WidgetDump.MaybeWrite</c>'s catch and blanks the dump,
    /// which is the 27% failure DRA-225 removed and the one thing this change must not
    /// reintroduce — and the file still reads as the COMPLETE older dump rather than as a
    /// torn newer one. A reader gets a stale answer, never a wrong one.</para>
    /// </summary>
    [Fact]
    public void AnUnusableScratchPathKeepsThePreviousDumpWholeInsteadOfThrowing()
    {
        File.WriteAllText(Target, FullDump);
        Directory.CreateDirectory(Pending);

        Assert.Equal(WholeFilePublish.Outcome.SkippedToKeepThePreviousFileWhole,
            WholeFilePublish.Write(Target, NextDump));

        Assert.Equal(FullDump, File.ReadAllText(Target));
        Assert.Equal(10, ReadKeyAsTheHarnessWould("shellLiveKillRows"));
    }

    /// <summary>
    /// **The one case where skipping would be worse than a torn read: there is no previous
    /// file to keep.** A caller waiting for a first dump would wait forever, so an unusable
    /// scratch path on a name that does not exist yet writes directly instead.
    ///
    /// <para>This is the asymmetry stated out loud rather than left as a gap — the skip
    /// contract is "keep what is published", and when nothing is published it has nothing to
    /// say.</para>
    /// </summary>
    [Fact]
    public void WithNoPreviousFileToProtectAnUnusableScratchPathStillPublishes()
    {
        Directory.CreateDirectory(Pending);
        Assert.False(File.Exists(Target));

        Assert.Equal(WholeFilePublish.Outcome.Created, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(FullDump, File.ReadAllText(Target));
    }
}
