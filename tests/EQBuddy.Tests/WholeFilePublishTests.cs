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
    private int ReadKeyAsTheHarnessWould(string key)
    {
        var text = WholeFilePublish.Read(Target);

        foreach (var pair in text.Split(' '))
            if (pair.StartsWith(key + "=", StringComparison.Ordinal) &&
                int.TryParse(pair.AsSpan(key.Length + 1), out var value))
                return value;
        return -1;
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
        using var done = new CancellationTokenSource();
        using var readerIsRunning = new ManualResetEventSlim(false);

        void ReadOnce()
        {
            var kills = ReadKeyAsTheHarnessWould("shellLiveKillRows");
            reads++;
            // -1 here can only mean a file with no such key. The key is in BOTH dumps,
            // and the file exists throughout, so any sentinel is a torn read.
            if (kills < 0) torn++;
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
        for (var i = 0; i < Publishes; i++)
            if (WholeFilePublish.Write(Target, i % 2 == 0 ? FullDump : NextDump)
                != WholeFilePublish.Outcome.Replaced) skipped++;

        done.Cancel();
        reader.Wait();

        Assert.True(reads > 0, "the reader never ran, so this proved nothing");
        Assert.True(torn == 0, $"torn reads: {torn} of {reads}");

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
    /// every launch.</summary>
    [Fact]
    public void TheFirstPublishOntoAnAbsentNameTakesTheAtomicPathToo()
    {
        Assert.False(File.Exists(Target));

        Assert.Equal(WholeFilePublish.Outcome.Replaced, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(FullDump, File.ReadAllText(Target));
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

        Assert.Equal(WholeFilePublish.Outcome.Replaced, WholeFilePublish.Write(Target, FullDump));
        Assert.Equal(FullDump, File.ReadAllText(Target));
    }
}
