using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Perf audit #13: SpellCatalog and BuffTracker learned-store writes are debounced
/// (the StackingLedgerStore idiom — flag now, one write ~2 s later, Flush at exit)
/// instead of a synchronous file write on the ingest path. What must not change:
/// everything learned still reaches disk — via the debounce or the exit flush — and
/// a reload round-trips it.
/// </summary>
public class LearnedStoreDebounceTests
{
    private static string TempStore(string name) =>
        Path.Combine(Path.GetTempPath(), $"eqbuddy-test-{name}-{Guid.NewGuid():N}.json");

    [Fact]
    public void SpellCatalogLearnPersistsViaFlushAndRoundTrips()
    {
        var path = TempStore("spells");
        try
        {
            var cat = new SpellCatalog();
            cat.AttachStore(path);
            Assert.True(cat.Learn("Fizzlewick's Torment", SpellCategory.DamageOverTime));
            // The write is debounced — Flush is the deterministic "now".
            cat.Flush();
            Assert.True(File.Exists(path));

            var reloaded = new SpellCatalog();
            reloaded.AttachStore(path);
            Assert.Equal(SpellCategory.DamageOverTime, reloaded.Classify("Fizzlewick's Torment"));
        }
        finally { TryDelete(path); }
    }

    /// <summary>
    /// **WAIT ON THE STORE'S LANDED SIGNAL, NEVER ON `File.Exists`** (DRA-240).
    ///
    /// <para><see cref="SpellCatalog.Flush"/> writes with <c>File.WriteAllText</c>, which
    /// CREATES the file before it has written or closed it. So existence goes true in the
    /// middle of the write, and a poll that asks it can exit onto a file that is there and
    /// says nothing. That one window produced all three ways this test has reddened CI:
    /// the 10 s poll timing out, the <c>finally</c> delete hitting a still-open handle, and
    /// — the mode that proved the mechanism — a reload classifying <c>Unknown</c> because it
    /// read an empty file.</para>
    ///
    /// <para>So the wait asks the question the test is actually about: <b>can the spell be
    /// read back?</b> A probe inside the write window is safe because
    /// <see cref="SpellCatalog.AttachStore"/> already swallows a torn or locked read — it
    /// simply learns nothing and we poll again. <c>AnExistingButUnwrittenStoreIsNotALandedSave</c>
    /// pins the discriminator: the state the old wait accepted, this one rejects.</para>
    ///
    /// <para><b>No product change.</b> A learned spell store is re-derivable from the log,
    /// which is why it is best-effort rather than on <c>ProfileJson</c>'s atomic path
    /// (trap 65). The flake was in the asking, not in the writing.</para>
    /// </summary>
    [Fact]
    public void SpellCatalogDebouncedSaveLandsWithoutAFlush()
    {
        var path = TempStore("spells-debounce");
        try
        {
            var cat = new SpellCatalog();
            cat.AttachStore(path);
            Assert.True(cat.Learn("Fizzlewick's Lament", SpellCategory.Heal));

            // ~2 s debounce. Poll the LANDED signal — a reload that reads the spell back.
            var deadline = DateTime.UtcNow.AddSeconds(20);
            SpellCatalog? reloaded = null;
            while (DateTime.UtcNow < deadline)
            {
                var probe = new SpellCatalog();
                probe.AttachStore(path);
                if (probe.Classify("Fizzlewick's Lament") == SpellCategory.Heal)
                {
                    reloaded = probe;
                    break;
                }
                Thread.Sleep(100);
            }

            Assert.True(reloaded is not null,
                "debounced background save never landed (no reload could read the spell back)");
            Assert.Equal(SpellCategory.Heal, reloaded!.Classify("Fizzlewick's Lament"));
        }
        finally { TryDelete(path); }
    }

    /// <summary>
    /// The flake's mechanism, pinned deterministically: a store that EXISTS and says nothing.
    ///
    /// <para>This is the exact state <c>File.WriteAllText</c> leaves behind for the length of
    /// a write, and the exact state CI caught on 2026-09-20 (<i>Expected: Heal, Actual:
    /// Unknown</i>). The old wait ACCEPTS it — <c>File.Exists</c> is true — and the wait above
    /// REJECTS it. Without this row, restoring the old poll would go green.</para>
    /// </summary>
    [Fact]
    public void AnExistingButUnwrittenStoreIsNotALandedSave()
    {
        var path = TempStore("spells-halfwritten");
        try
        {
            File.WriteAllText(path, "");

            Assert.True(File.Exists(path));          // what the old wait asked
            var probe = new SpellCatalog();
            probe.AttachStore(path);
            Assert.Equal(                            // what it asks now
                SpellCategory.Unknown, probe.Classify("Fizzlewick's Lament"));
        }
        finally { TryDelete(path); }
    }

    /// <summary>A cleanup delete must never be the thing that fails a green test. The
    /// debounce writer can still hold the handle when the asserts are done, so this retries
    /// briefly instead of throwing out of a <c>finally</c> — the second of the two modes
    /// DRA-240 names.</summary>
    private static void TryDelete(string path)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }

    [Fact]
    public void BuffTrackerLearnedDurationPersistsViaFlushAndRoundTrips()
    {
        var t0 = DateTime.Parse("2026-08-12T21:00:00");
        GameEvent Ev(int seconds, string message) =>
            LogParser.Parse($"[{t0.AddSeconds(seconds).ToString("ddd MMM d HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture)}] {message}")!;

        var path = TempStore("buffs");
        try
        {
            var fadeLine = FadeMessageCatalog.Default.FindBySpell("Armor of Faith")!;
            var t = new BuffTracker();
            t.AttachStore(path);
            // The natural-fade learning recipe from BuffTrackerTests: rank-lengthened
            // fade at 4200 s teaches 4200 (tick-floored).
            t.Apply(Ev(0, "You begin casting Armor of Faith."));
            t.Apply(Ev(2, "You feel the favor of the gods upon you."));
            t.Apply(Ev(2 + 4201, fadeLine.Message));
            Assert.Equal(4200, t.LearnedDurations["Armor of Faith"], 0);

            t.Flush();
            Assert.True(File.Exists(path));

            var reloaded = new BuffTracker();
            reloaded.AttachStore(path);
            Assert.Equal(4200, reloaded.LearnedDurations["Armor of Faith"], 0);
        }
        finally { TryDelete(path); }
    }
}
