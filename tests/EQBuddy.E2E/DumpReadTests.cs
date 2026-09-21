using System.Text;

namespace EQBuddy.E2E;

/// <summary>
/// **The prove-fail for DRA-225's read rules, and it launches nothing.**
///
/// <para>Every other test in this project asserts about a LIVE app. These assert about the
/// way this harness READS that app, which is where four `ShellHostTests` reds actually
/// lived — `Actual: -1`, `AppHarness.DumpValue`'s missing-key sentinel, on a second read
/// taken after a wait had already succeeded. A fix for a read that is green most of the
/// time is vacuous unless the absent read is BUILT (trap 34), so each test below seeds the
/// exact state and asserts the OLD pattern's answer beside the NEW one's.</para>
///
/// <para><b>The mechanism, measured with two processes over one file at the dump's real
/// size (~6.4 KB) before any of this was written:</b> `File.ReadAllText` opens with
/// `FileShare.Read`, which DENIES a concurrent writer — and the writer is the app.
/// `WidgetDump.MaybeWrite` writes the dump inside a try whose catch replaces the whole
/// file with `tick=… dumpError=…`, every other key absent. With `FileShare.Read` the
/// writer was denied <b>103 of 378</b> writes (27%); with the shared mask the harness uses
/// now it was denied <b>0 of 377</b>, at the cost of the reader seeing a partial file 223
/// times in 102,503 (0.22%). So the harness was breaking the thing it observed, and the
/// breakage surfaced as a flake in whatever assertion came next.</para>
///
/// <para>These construct an <see cref="AppHarness"/> without calling `Launch()`: no
/// process, no screen lock (trap 61), no fixture replay. The constructor still requires
/// `EQBuddy.exe` to be built, which the suite requires anyway (trap 64).</para>
/// </summary>
public sealed class DumpReadTests
{
    /// <summary>A dump shaped like the one the app writes: space-separated `key=value`.</summary>
    private const string FullDump = "tick=42 shellPage=gear shellGearTab=gear shellRail=8 "
        + "spawnsZones=119 shellWorldSpawnsZones=119 altTabTaskbar=1";

    /// <summary>What `WidgetDump`'s CATCH writes when its `File.WriteAllText` throws — and
    /// the state this whole card is about. `shellGearTab` is present in the full dump above
    /// and absent here, which is exactly how `gear:gear` failed on 2026-09-20 while its
    /// sibling `gear:inventory` passed 11 s later.</summary>
    private const string DumpErrorDump = "tick=43 dumpError=IOException";

    private static void Seed(AppHarness app, string text) =>
        File.WriteAllText(app.DebugDumpPath, text);

    // ---- The root cause: a read that denies the app its write -------------------

    /// <summary>
    /// **The harness must be able to read the dump WHILE the app holds it open for
    /// writing** — the DRA-225 root cause, from the side that can be made deterministic.
    ///
    /// <para>`File.WriteAllText` opens `FileMode.Create` / `FileAccess.Write` /
    /// `FileShare.Read`. Two handles on one file must each permit what the other holds, and
    /// under the harness's old `File.ReadAllText` (which requests `FileShare.Read`, i.e.
    /// "others may read") the app's WRITE access is not permitted — so the two collide. The
    /// collision is symmetric, which is why this test can arm it from the handle that can
    /// be held still: in the wild the harness's read came first and the APP was the one
    /// refused.</para>
    ///
    /// <para><b>Prove-fail:</b> restoring `File.ReadAllText` in `AppHarness.ReadDump`
    /// reddens this — the read throws `IOException`, the catch answers -1, and the assert
    /// reads `Expected: 8 / Actual: -1`, the ledger's exact signature.</para>
    /// </summary>
    [Fact]
    public void TheHarnessReadsTheDumpWhileTheAppHoldsItOpenForWriting()
    {
        using var app = new AppHarness();

        // The app's own handle, opened exactly as File.WriteAllText opens it, and HELD.
        using var appHandle = new FileStream(app.DebugDumpPath,
            FileMode.Create, FileAccess.Write, FileShare.Read);
        appHandle.Write(Encoding.UTF8.GetBytes(FullDump));
        appHandle.Flush();

        Assert.Equal(8, app.DumpValue("shellRail"));
        Assert.Equal("gear", app.DumpText("shellGearTab"));
        Assert.Equal(new[] { 119, 119 }, app.DumpValues("spawnsZones", "shellWorldSpawnsZones"));
    }

    // ---- The sentinel is real, and the OLD pattern asserts on it ----------------

    /// <summary>
    /// **The committed negative that stops every test below going vacuous** (trap 78: a
    /// guard aimed at nothing is green). It states what the OLD pattern actually did on the
    /// seeded `dumpError` dump: answer the sentinel, and hand it straight to an assert.
    ///
    /// <para>The three numbers are the three ledger rows this card closes — `7`/`8` for the
    /// rail and `119` for `spawnsZones` — each becoming `Expected: N / Actual: -1`.</para>
    /// </summary>
    [Fact]
    public void ABareReadOfADumpErrorDumpAnswersTheSentinelForEveryKey()
    {
        using var app = new AppHarness();
        Seed(app, DumpErrorDump);

        // The tick still advances, which is the whole reason this reads as a live app
        // rather than a dead one — `WhyTheAppCannotAnswer` is right not to abort here.
        Assert.Equal(43, app.DumpValue("tick"));

        Assert.Equal(-1, app.DumpValue("shellRail"));
        Assert.Equal(-1, app.DumpValue("spawnsZones"));
        Assert.Equal(-1, app.DumpValue("shellWorldSpawnsZones"));
        Assert.Equal("", app.DumpText("shellGearTab"));

        // **And ONE READ is not enough on its own.** `DumpValues` gives one moment, which
        // is the fix for the `4 / 13` row — but on this dump it answers -1 for every key at
        // once. That is why `WaitForDumpValues` waits on PRESENCE and `DumpValues` alone
        // was never going to close the `-1` rows.
        Assert.Equal(new[] { -1, -1 }, app.DumpValues("spawnsZones", "shellWorldSpawnsZones"));
    }

    // ---- Absent at the first read, present at a later one -----------------------

    /// <summary>
    /// **The card's bar, for the equality arm:** a key missing at the first read and
    /// present at a second. `EveryLandedRoomIsReachableByItsOwnAddress` now asks
    /// `WaitForDump`, which re-reads; the old bare `DumpValue` had no second read to take.
    ///
    /// <para>`rewritten` is what makes this a proof rather than a hope: the wait can only
    /// have returned AFTER the rewrite, because `shellRail` did not exist before it. If the
    /// wait had answered off the first read it would have answered -1 and thrown.</para>
    /// </summary>
    [Fact]
    public void AnEqualityWaitSurvivesAKeyThatIsAbsentOnTheFirstRead()
    {
        using var app = new AppHarness();
        Seed(app, DumpErrorDump);

        // OLD pattern, at the instant the test would have taken it: red.
        Assert.Equal(-1, app.DumpValue("shellRail"));

        var rewritten = false;
        using var landing = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), landing.Token);
            Seed(app, FullDump);
            Volatile.Write(ref rewritten, true);
        }, landing.Token);

        // NEW pattern: green, and it had to wait for the dump to carry the key.
        app.WaitForDump("shellRail", 8, "the rail count to arrive in a later dump");
        Assert.True(Volatile.Read(ref rewritten),
            "the wait returned before the key existed, so it proved nothing");
        writer.Wait();
    }

    /// <summary>
    /// **The card's bar, for the agreement arm** — and the one `WaitForDump` could not
    /// have covered, because neither number is known before the app reports it.
    ///
    /// <para>The seeded first state is the real shape of the 2026-09-19 red: one side of a
    /// pair present, the other absent, which asserts as `Expected: 119 / Actual: -1`.</para>
    /// </summary>
    [Fact]
    public void AnAgreementWaitRefusesAReadThatCarriedOnlyHalfThePair()
    {
        using var app = new AppHarness();
        // `spawnsZones` present, `shellWorldSpawnsZones` absent: the 2026-09-19 signature.
        Seed(app, "tick=43 spawnsZones=119");

        // OLD pattern: one moment, and still the sentinel on one side. The assert this fed
        // read `Expected: 119 / Actual: -1`.
        Assert.Equal(new[] { 119, -1 }, app.DumpValues("spawnsZones", "shellWorldSpawnsZones"));

        var rewritten = false;
        using var landing = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), landing.Token);
            Seed(app, FullDump);
            Volatile.Write(ref rewritten, true);
        }, landing.Token);

        var pair = app.WaitForDumpValues("both hosts to report the zone count in one dump",
            "spawnsZones", "shellWorldSpawnsZones");

        Assert.True(Volatile.Read(ref rewritten),
            "the wait returned before both keys existed, so it proved nothing");
        Assert.Equal(new[] { 119, 119 }, pair);
        writer.Wait();
    }

    /// <summary>
    /// The reachable negative for the wait itself (trap 34's other half): it must answer
    /// off a read that carried BOTH keys, not off two reads that each carried one.
    ///
    /// <para>The dump alternates between two half-states that never agree, and each half is
    /// individually readable — so a wait built out of two `DumpValue` calls would be
    /// satisfied by them while no single dump ever was. This asserts the values come back
    /// together, from the one read that carried both.</para>
    /// </summary>
    [Fact]
    public void TheAgreementWaitAnswersOffOneReadRatherThanTwoHalves()
    {
        using var app = new AppHarness();
        Seed(app, "tick=1 spawnsZones=119");

        using var landing = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            // Alternate the two halves for a while — neither read can ever see both — then
            // let the complete dump land.
            for (var i = 0; i < 6 && !landing.IsCancellationRequested; i++)
            {
                Seed(app, i % 2 == 0
                    ? "tick=1 spawnsZones=119"
                    : "tick=1 shellWorldSpawnsZones=119");
                await Task.Delay(TimeSpan.FromMilliseconds(120), landing.Token);
            }
            Seed(app, FullDump);
        }, landing.Token);

        var pair = app.WaitForDumpValues("one dump carrying both halves",
            "spawnsZones", "shellWorldSpawnsZones");

        Assert.Equal(new[] { 119, 119 }, pair);
        writer.Wait();
    }
}
