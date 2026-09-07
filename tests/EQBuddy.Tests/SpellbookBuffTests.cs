using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The optional `/outputfile spellbook` half of the buff-timer data path (OE-5 LOCK A).
///
/// **Two rules carry this whole file and both are product locks, not preferences:**
/// the spellbook is never a timer source (it resolves WHICH spell a landing was and never
/// starts, ends or times a countdown), and **the log outranks the dump** (a stale
/// spellbook must never erase or shrink something the log observed). Every assertion below
/// that looks like a nothing-happened test is one of those two, and each was run against
/// the un-guarded shape before it landed.
///
/// The catalog rows used here are the shipped `BuffDurations.json`'s own — "A coat of
/// shimmering runes surround you" really does carry Rune IV at 5,400s and Rune V at
/// 6,600s, and "A cool breeze slips through your mind" really does carry two spells with
/// DIFFERENT base names. Those two shapes are the two tiers of the match, so the test data
/// is the product's data rather than a fixture that agrees with the code.
/// </summary>
public class SpellbookBuffTests : IDisposable
{
    private static readonly DateTime T0 = DateTime.Parse("2026-09-07T02:00:00");

    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "eqbuddy-spellbook-" + Guid.NewGuid().ToString("N"));

    private string LogFolder
    {
        get
        {
            var logs = Path.Combine(_dir, "Logs");
            Directory.CreateDirectory(logs);
            return logs;
        }
    }

    /// <summary>Writes the dump where the game writes it — the Logs folder's PARENT — so
    /// the test goes through the real finder rather than around it (trap 23).</summary>
    private void WriteSpellbook(string character, params string[] lines)
    {
        Directory.CreateDirectory(LogFolder);
        File.WriteAllLines(Path.Combine(_dir, $"{character}_test-Spellbook.txt"), lines);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp */ }
        GC.SuppressFinalize(this);
    }

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    private BuffTracker Tracker(string character = "Testchar")
    {
        var t = new BuffTracker();
        t.AttachSpellbook(LogFolder, character);
        return t;
    }

    // ---- 1. the reader ---------------------------------------------------------

    /// <summary>
    /// **The reader does not guess a column, and this is why.** The dump's layout is
    /// unverified — nobody on this project has a sample — and the two dumps that HAVE been
    /// seen put their name column in different places (inventory second, factions second,
    /// but behind a Location and an ID respectively). So every field is collected and the
    /// consumer intersects against a known spell-name space.
    ///
    /// The three shapes below are the three the game plausibly writes, and the reader has
    /// to survive all of them because we cannot say which one it is.
    /// </summary>
    [Fact]
    public void TheReaderCollectsSpellNamesWhateverColumnTheyAreIn()
    {
        // Inventory-shaped: a location, a name, an id.
        Assert.Contains("Rune V", SpellbookFile.Parse(
            ["Location\tName\tID", "Spell Book Slot 3\tRune V\t1234"]));
        // Faction-shaped: an id first.
        Assert.Contains("Rune V", SpellbookFile.Parse(["ID\tName", "1234\tRune V"]));
        // And a file with no tabs at all degrades to one field per line rather than to
        // nothing — the format is unverified in both directions.
        Assert.Contains("Rune V", SpellbookFile.Parse(["Rune V", "Clarity"]));
    }

    /// <summary>Pure numbers are ids, slots and levels in every dump the game writes, and
    /// no spell is called "1234". Everything else stays: over-collecting is harmless
    /// because the consumer intersects, and a wrong column guess would not have been.</summary>
    [Fact]
    public void NumbersAreDroppedAndTheAttunedMarkerIsCosmetic()
    {
        var names = SpellbookFile.Parse(["1\tRune V*\t\t 1234 "]);
        Assert.Contains("Rune V", names);
        Assert.DoesNotContain("1234", names);
        Assert.DoesNotContain("1", names);
    }

    /// <summary>Both tiers of the match, off one parse. Exact is what buys a per-rank
    /// duration; the rank-folded line is the fallback for a dump that spells its ranks
    /// some way we have not seen.</summary>
    [Fact]
    public void ASnapshotAnswersBothExactSpellAndRankFoldedLine()
    {
        WriteSpellbook("Testchar", "Rune V");
        var book = SpellbookFile.FindLatest(LogFolder, "Testchar");
        Assert.NotNull(book);
        Assert.True(book!.Knows("Rune V"));
        Assert.False(book.Knows("Rune IV"));
        // Same LINE, so the rank-folded question answers yes for both.
        Assert.True(book.KnowsLine("Rune IV"));
        Assert.False(book.KnowsLine("Clarity"));
    }

    /// <summary>No dump is the ordinary case and not a fault — the player has simply never
    /// run an optional command.</summary>
    [Fact]
    public void NoDumpIsNullRatherThanAnEmptyBook()
    {
        Directory.CreateDirectory(LogFolder);
        Assert.Null(SpellbookFile.FindLatest(LogFolder, "Testchar"));
        Assert.Null(SpellbookFile.FindLatest(null, "Testchar"));
        Assert.Null(SpellbookFile.FindLatest(LogFolder, ""));
    }

    // ---- 2. narrowing, which is the whole feature ------------------------------

    /// <summary>
    /// **The accuracy win, stated as a number.** An unexplained landing of the Rune line
    /// used to estimate at the longest rank in the game — Rune V, 6,600s — for a character
    /// who has only ever scribed Rune IV. With the dump attached the candidate set is the
    /// one rank they own and the countdown is that rank's 5,400s.
    /// </summary>
    [Fact]
    public void TheDumpNarrowsAnUnexplainedLandingToTheRankYouActuallyKnow()
    {
        WriteSpellbook("Testchar", "Rune IV");
        var t = Tracker();
        t.Apply(Ev(0, "A coat of shimmering runes surround you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.Equal(["Rune IV"], b.Candidates);
        Assert.Equal(5400 - 1, b.RemainingSeconds(T0.AddSeconds(1))!.Value, 0);
        // Still an estimate: a dump-resolved base is a base, and only a natural fade
        // teaches this character's real number.
        Assert.True(b.Estimated);
    }

    /// <summary>Without the dump the same landing is the pre-fix answer — the longest
    /// candidate, every candidate standing. This is the baseline the assertion above is
    /// measured against, in the same file, so neither number can drift alone.</summary>
    [Fact]
    public void WithoutADumpTheLongestCandidateStillWins()
    {
        var t = new BuffTracker();
        t.Apply(Ev(0, "A coat of shimmering runes surround you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.True(b.Candidates.Length > 1);
        Assert.Equal(6600 - 1, b.RemainingSeconds(T0.AddSeconds(1))!.Value, 0);
    }

    /// <summary>The second tier: two spells that share one landing message and do NOT
    /// share a base name. A dump that names one of them separates them even though no rank
    /// is involved at all.</summary>
    [Fact]
    public void TheDumpAlsoSeparatesTwoDifferentSpellsSharingOneLandingLine()
    {
        WriteSpellbook("Testchar", "Clarity");
        var t = Tracker();
        t.Apply(Ev(0, "A cool breeze slips through your mind."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.Equal(["Clarity"], b.Candidates);
        Assert.DoesNotContain("Boon of the Clear Mind", b.Candidates);
    }

    /// <summary>Running the command mid-session sharpens the NEXT landing rather than the
    /// next launch: the tracker re-reads on the dump's own announcement, which is the same
    /// line the quest reconcile rides (#241).</summary>
    [Fact]
    public void RunningTheCommandMidSessionIsPickedUpFromItsOwnAnnouncement()
    {
        var t = Tracker();                       // nothing to attach yet
        t.Apply(Ev(0, "A coat of shimmering runes surround you."));
        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(1))).Candidates.Length > 1);

        WriteSpellbook("Testchar", "Rune IV");
        t.Apply(Ev(10, "Outputfile Complete: Testchar_test-Spellbook.txt"));
        t.Apply(Ev(20, "A coat of shimmering runes surround you."));

        Assert.Equal(["Rune IV"], Assert.Single(t.Snapshot(T0.AddSeconds(21))).Candidates);
    }

    // ---- 3. the log outranks the dump ------------------------------------------

    /// <summary>
    /// **A spellbook that recognises NONE of the candidates narrows nothing.** A dump
    /// written before the spell was learned, a clicky item, a rank spelled some way this
    /// dump does not use — every one of those is a reason the log is right and the dump is
    /// behind, and none is a reason to shrink a landing the log observed.
    ///
    /// Prove-fail: dropping the `line.Length &gt; 0` / `exact.Length &gt; 0` guards in
    /// `BuffTracker.NarrowBySpellbook` empties the candidate set here, and the `Max` over
    /// the remaining spells then throws on an empty sequence — the landing does not merely
    /// shrink, it takes the ingest thread's event with it.
    /// </summary>
    [Fact]
    public void AStaleDumpThatKnowsNoCandidateLeavesTheLandingExactlyAsItWas()
    {
        WriteSpellbook("Testchar", "Bind Affinity", "Gate");
        var t = Tracker();
        t.Apply(Ev(0, "A coat of shimmering runes surround you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.True(b.Candidates.Length > 1);
        Assert.False(b.DumpNarrowed);
        Assert.Equal(6600 - 1, b.RemainingSeconds(T0.AddSeconds(1))!.Value, 0);
    }

    /// <summary>A cast line names the spell and its caster outright, so the dump has
    /// nothing to say — including when the dump does not list that spell at all, which is a
    /// stale dump and not a wrong log.</summary>
    [Fact]
    public void ACastResolvedLandingIsUntouchedEvenWhenTheDumpDisagrees()
    {
        WriteSpellbook("Testchar", "Clarity");
        var t = Tracker();
        t.Apply(Ev(0, "You begin casting Boon of the Clear Mind."));
        t.Apply(Ev(3, "A cool breeze slips through your mind."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Boon of the Clear Mind", b.Label);
        Assert.Equal("You", b.Caster);
        Assert.Equal(["Boon of the Clear Mind"], b.Candidates);
        Assert.False(b.DumpNarrowed);
    }

    /// <summary>
    /// **The dump must never cost us a fade**, and the shape that proves it is the
    /// SINGLE-SPELL wear-off line rather than a shared fade message. "The cool breeze
    /// fades." names the whole Clarity line, so a narrowed set is still in it; *"Your Boon
    /// of the Clear Mind spell has worn off."* names one spell, and it is the one the dump
    /// ruled out. The log said this buff ended and a dump-shrunk candidate set is not
    /// allowed to disagree.
    ///
    /// Prove-fail: matching `OnFade` on `Candidates` instead of `FadeNames` leaves this
    /// chip on screen until its five-minute linger expires.
    /// </summary>
    [Fact]
    public void AWearOffNamingARuledOutCandidateStillEndsTheBuff()
    {
        WriteSpellbook("Testchar", "Clarity");
        var t = Tracker();
        t.Apply(Ev(0, "A cool breeze slips through your mind."));
        var landed = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.Equal(["Clarity"], landed.Candidates);          // the premise, checked

        t.Apply(Ev(600, "Your Boon of the Clear Mind spell has worn off."));

        Assert.Empty(t.Snapshot(T0.AddSeconds(601)));
    }

    /// <summary>
    /// **The back door the owner lock closes.** Fade-teaching used to be gated on "one
    /// candidate", which meant "the LOG named this spell" — a dump can now produce a set of
    /// one too, and learning a real per-character duration off a dump-guessed identity
    /// would make the spellbook a timer source by another name.
    ///
    /// Prove-fail: dropping `|| b.DumpNarrowed` from `OnFade`'s gate writes a learned
    /// duration for "Rune IV" here.
    /// </summary>
    [Fact]
    public void ADumpNarrowedFadeNeverTeachesADuration()
    {
        WriteSpellbook("Testchar", "Rune IV");
        var t = Tracker();
        t.Apply(Ev(0, "A coat of shimmering runes surround you."));
        var fade = FadeMessageCatalog.Default.FindBySpell("Rune IV")!;
        t.Apply(Ev(5400, fade.Message));

        Assert.Empty(t.Snapshot(T0.AddSeconds(5401)));
        Assert.Empty(t.LearnedDurations);
    }

    /// <summary>And the log-resolved path still teaches, unchanged — the gate above must
    /// close one door without closing the one beside it.</summary>
    [Fact]
    public void ALogResolvedFadeStillTeachesTheRealDuration()
    {
        WriteSpellbook("Testchar", "Clarity");
        var t = Tracker();
        t.Apply(Ev(0, "You begin casting Boon of the Clear Mind."));
        t.Apply(Ev(3, "A cool breeze slips through your mind."));
        var fade = FadeMessageCatalog.Default.FindBySpell("Boon of the Clear Mind")!;
        t.Apply(Ev(1803, fade.Message));

        var learned = Assert.Single(t.LearnedDurations);
        Assert.Equal("Boon of the Clear Mind", learned.Key);
        Assert.Equal(1800, learned.Value, 0);
    }

    // ---- 4. attaching -----------------------------------------------------------

    /// <summary>Selecting another character cannot leave the previous one's book behind:
    /// the attach always runs, and no dump for the new character sets it back to null.
    /// Same isolation `ResetSession` gives the session sights (#120 stage 2).</summary>
    [Fact]
    public void AttachingACharacterWithNoDumpClearsThePreviousOnesBook()
    {
        WriteSpellbook("Testchar", "Rune IV");
        var t = Tracker();
        Assert.NotNull(t.Spellbook);

        t.AttachSpellbook(LogFolder, "Otherchar");
        Assert.Null(t.Spellbook);

        t.Apply(Ev(0, "A coat of shimmering runes surround you."));
        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(1))).Candidates.Length > 1);
    }

    /// <summary>Everything about this is optional, so every way it can be absent is
    /// silence rather than a throw: no folder, no character, no file.</summary>
    [Fact]
    public void AnAbsentSpellbookIsSilenceInEveryDirection()
    {
        var t = new BuffTracker();
        t.AttachSpellbook(null, "Testchar");
        t.AttachSpellbook(LogFolder, "");
        t.AttachSpellbook(Path.Combine(_dir, "nope"), "Testchar");
        Assert.Null(t.Spellbook);

        t.Apply(Ev(0, "A coat of shimmering runes surround you."));
        Assert.Single(t.Snapshot(T0.AddSeconds(1)));
    }
}
