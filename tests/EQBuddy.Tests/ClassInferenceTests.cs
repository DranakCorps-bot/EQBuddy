using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Class inference (#120, Frankthetankk). The first version knew ten melee skills and
/// nothing else, so a caster who once produced a melee-ish line was that class for the
/// rest of the session: there was no line in the table a caster could ever produce to
/// argue back. These tests pin the two halves of the fix — casters can produce evidence,
/// and recent evidence outweighs old — and, just as importantly, the cases where the
/// right answer is no answer at all.
/// </summary>
public class ClassInferenceTests
{
    private static readonly DateTime T0 = new(2026, 8, 16, 20, 0, 0, DateTimeKind.Unspecified);
    private static DateTime At(double minutes) => T0.AddMinutes(minutes);

    // ---- the catalog ----

    /// <summary>The bug itself, as an assertion. Every class the app offers must have
    /// SOMETHING it can say for itself, or the vote is rigged before it starts.</summary>
    [Fact]
    public void EveryClassCanProduceEvidence()
    {
        var speak = ClassSignalCatalog.ClassesWithSignals;
        var mute = QuestClassFilter.Classes.Where(c => !speak.Contains(c)).ToList();
        Assert.True(mute.Count == 0,
            "Classes with no way to produce class evidence — the #120 one-sidedness is "
            + "back: " + string.Join(", ", mute));
    }

    [Fact]
    public void ClassUniqueNamesResolveAndSharedOnesNeverDo()
    {
        // Curated melee skills, which the log names through its own verbs.
        Assert.Equal(new ClassSignal("Rogue", ClassSignalKind.Ability), ClassSignalCatalog.Find("Backstab"));
        // An AA: no item lends you a touch, so it stands alone as evidence.
        Assert.Equal(new ClassSignal("Shadow Knight", ClassSignalKind.Ability), ClassSignalCatalog.Find("Harm Touch"));
        Assert.Equal(new ClassSignal("Beastlord", ClassSignalKind.Ability), ClassSignalCatalog.Find("Frenzy of Spirit"));
        // Spells: the caster half that #120 was missing entirely.
        Assert.Equal(new ClassSignal("Wizard", ClassSignalKind.Spell), ClassSignalCatalog.Find("Ice Comet"));
        Assert.Equal(new ClassSignal("Enchanter", ClassSignalKind.Spell), ClassSignalCatalog.Find("Clarity"));

        // Shared lines are not evidence, however class-flavoured they read.
        Assert.Null(ClassSignalCatalog.Find("Gate"));                // seven classes
        Assert.Null(ClassSignalCatalog.Find("Superior Healing"));    // four
        Assert.Null(ClassSignalCatalog.Find("Lifespike"));           // necro + shadow knight
        // Cleric-only in the AA harvest, Cleric AND Paladin in the spell harvest: the
        // union across catalogs is what decides, so this one is out.
        Assert.Null(ClassSignalCatalog.Find("Divine Aura"));
        Assert.Null(ClassSignalCatalog.Find("orc centurion"));
        Assert.Null(ClassSignalCatalog.Find(""));

        // A harvest that broke would still build a catalog — an empty one, which would
        // quietly stop inferring anything rather than fail. Roughly 1,100 names today.
        Assert.True(ClassSignalCatalog.Count > 800,
            $"Only {ClassSignalCatalog.Count} class-unique names — did a harvest lose its "
            + "class column?");
    }

    // ---- the ledger ----

    [Fact]
    public void OneSightingNeverNamesAClassAndThreeDo()
    {
        var inf = new ClassInference();
        inf.RecordAbilityUse("Backstab", At(0));
        Assert.Equal("", inf.Current());
        inf.RecordAbilityUse("Backstab", At(0.1));
        Assert.Equal("", inf.Current());       // still under the floor
        inf.RecordAbilityUse("Backstab", At(0.2));
        Assert.Equal("Rogue", inf.Current());  // a genuine melee character is still found
    }

    /// <summary>The clicky guard, and the reason spell evidence is graded differently
    /// from ability evidence: one item cast a hundred times is still one spell.</summary>
    [Fact]
    public void OneClassUniqueSpellIsAClickyTwoAreASpellbook()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 10; i++) inf.RecordCast("Ice Comet", At(i * 0.2));
        Assert.Equal("", inf.Current());

        inf.RecordCast("Draught of Fire", At(2.2));
        Assert.Equal("Wizard", inf.Current());
    }

    /// <summary>A damage line is also what an item proc looks like, so spell-grade names
    /// arriving that way are ignored outright — but the touches, which are only ever
    /// printed as damage, must still count (they never did before this change).</summary>
    [Fact]
    public void DamageSourcesCountAbilitiesAndIgnoreProcSpells()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 6; i++) inf.RecordAbilityUse("Ice Comet", At(i * 0.2));
        Assert.Equal("", inf.Current());

        for (var i = 0; i < 3; i++) inf.RecordAbilityUse("Harm Touch", At(2 + i * 20));
        Assert.Equal("Shadow Knight", inf.Current());
    }

    /// <summary>Two classes with comparable recent evidence is an ambiguous log. The
    /// reading drives the quest lens, so "don't know" beats a coin toss.</summary>
    [Fact]
    public void AnAmbiguousLogNamesNoClassAtAll()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 4; i++)
        {
            inf.RecordAbilityUse("Backstab", At(i));
            inf.RecordAbilityUse("Lay on Hands", At(i + 0.5));
        }
        Assert.Equal("", inf.Current());
    }

    /// <summary>Recency, the second half of the #120 fix. A rogue plays for half an hour,
    /// then a wizard is at the keyboard: the wrong answer is withdrawn first, and the
    /// right one arrives once it has genuinely out-weighed what came before.</summary>
    [Fact]
    public void PlayNowOutweighsPlayAnHourAgo()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 180; i++) inf.RecordAbilityUse("Backstab", At(i / 6.0));   // 30 min
        Assert.Equal("Rogue", inf.Current());

        // The swap. Two distinct wizard spells, cast at the same cadence.
        void Cast(double minute)
        {
            inf.RecordCast("Ice Comet", At(30 + minute));
            inf.RecordCast("Draught of Fire", At(30 + minute + 0.08));
        }

        for (var i = 0; i < 30; i++) Cast(i / 6.0);            // 5 min in
        Assert.Equal("", inf.Current());                       // no longer Rogue, not yet Wizard

        for (var i = 30; i < 120; i++) Cast(i / 6.0);          // 20 min in
        Assert.Equal("Wizard", inf.Current());
    }

    /// <summary>#120 (Frankthetankk, 2026-08-21): the ALT-SWAP case — *"someone plays two
    /// classes roughly equally rather than a one-time dabble… does the half-life decay
    /// handle that gracefully, or is there a real risk of visible flip-flopping session to
    /// session?"* He asked whether the lead margin already covers it or it needs testing
    /// separately. It needed testing separately; this is it.
    ///
    /// The answer is that there is no flip-flop, because there are only two outcomes and
    /// neither one flickers: while you are actually playing a class, recency carries it
    /// past the 2× lead margin and it is named; in the handover between them the margin is
    /// not met and the honest "" comes back. A player who splits evenly does not see the
    /// label alternate under him — he sees it go quiet and then name whatever he is
    /// actually casting. That is the designed behaviour and it is the right one, but it
    /// was a reading of the constant until this test drove the sequence.</summary>
    [Fact]
    public void AlternatingTwoClassesNamesTheOneBeingPlayedAndNeverFlickers()
    {
        var inf = new ClassInference();
        var answers = new List<string>();

        // Four stretches, alternating. Twenty minutes each — two half-lives, which is what
        // an alt-swapper actually does in a night, not a one-line dabble.
        for (var stretch = 0; stretch < 4; stretch++)
        {
            var rogue = stretch % 2 == 0;
            var start = stretch * 20.0;
            for (var i = 0; i < 120; i++)
            {
                var at = start + i / 6.0;
                if (rogue) inf.RecordAbilityUse("Backstab", At(at));
                else
                {
                    inf.RecordCast("Ice Comet", At(at));
                    inf.RecordCast("Draught of Fire", At(at + 0.08));
                }
                answers.Add(inf.Current());
            }
            // By the end of each stretch the class being PLAYED is the one named.
            Assert.Equal(rogue ? "Rogue" : "Wizard", inf.Current());
        }

        // The only two things it ever says are the class in hand and "don't know" — it
        // never names the class you are NOT playing, which is what flip-flopping would be.
        Assert.All(answers, a => Assert.Contains(a, new[] { "", "Rogue", "Wizard" }));

        // And every change of mind passes through "" rather than swapping directly: a
        // direct Rogue→Wizard flip would be the visible flicker he was worried about.
        var changes = answers.Where((a, i) => i == 0 || a != answers[i - 1]).ToList();
        for (var i = 1; i < changes.Count; i++)
            Assert.True(changes[i - 1] == "" || changes[i] == "",
                $"the reading went straight from '{changes[i - 1]}' to '{changes[i]}' without "
                + "passing through \"don't know\" — that is the flip-flop #120 asked about");
    }

    /// <summary>Decay is uniform, so it can never flip a reading on its own. A player who
    /// earned an inference and then stood in the bazaar for an hour still has it — that
    /// is why the sighting floor counts raw sightings and not weight.</summary>
    [Fact]
    public void SilenceDoesNotErodeAnEarnedInference()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 3; i++) inf.RecordAbilityUse("Backstab", At(i * 0.2));
        Assert.Equal("Rogue", inf.Current());

        inf.RecordAbilityUse("Backstab", At(120));   // two hours later, one more swing
        Assert.Equal("Rogue", inf.Current());
    }

    /// <summary>Log lines share a second constantly, and a re-ingest can replay an older
    /// one. Neither may run the clock backwards and inflate what is already banked.</summary>
    [Fact]
    public void OutOfOrderTimestampsDoNotInflateWeights()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 3; i++) inf.RecordAbilityUse("Backstab", At(30));
        inf.RecordAbilityUse("Lay on Hands", At(0));
        inf.RecordAbilityUse("Lay on Hands", At(0));
        inf.RecordAbilityUse("Lay on Hands", At(0));
        Assert.Equal("", inf.Current());   // three each, same instant: ambiguous, not Paladin
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 3; i++) inf.RecordAbilityUse("Backstab", At(i * 0.2));
        inf.Clear();
        Assert.Equal("", inf.Current());
    }

    // ---- through the log ----

    private static SessionStats Ingest(params string[] lines)
    {
        var stats = new SessionStats();
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } ev) stats.Apply(ev);
        return stats;
    }

    private static string Stamp(int minute, int second) =>
        $"[Sun Aug 16 20:{minute:D2}:{second:D2} 2026] ";

    /// <summary>#120 end to end. A caster whose weapon lands a stray melee-ish burst early
    /// used to wear that class for the whole session, because nothing he did afterwards
    /// was in the table. Now his own spellbook answers it.</summary>
    [Fact]
    public void ACasterIsNotStuckWithAStrayMeleeBurst()
    {
        var lines = new List<string>();
        for (var i = 0; i < 3; i++)
            lines.Add(Stamp(0, i) + "You backstab a froglok tad for 12 points of damage.");
        for (var m = 1; m < 25; m++)
        {
            lines.Add(Stamp(m, 0) + "You begin casting Ice Comet.");
            lines.Add(Stamp(m, 20) + "You begin casting Draught of Fire.");
            lines.Add(Stamp(m, 40) + "You begin casting Lure of Frost.");
        }

        Assert.Equal("Wizard", Ingest([.. lines]).Snapshot().InferredClass);
    }

    /// <summary>The touches arrive as SPELL damage ("… points of magic damage by Harm
    /// Touch"), which the old melee-only guard filtered out — the Shadow Knight and
    /// Paladin rows of the table could never fire at all.</summary>
    [Fact]
    public void AShadowKnightIsFoundByHisTouch()
    {
        var lines = new List<string>();
        for (var m = 0; m < 3; m++)
            lines.Add(Stamp(m * 20, 0) + "You hit a froglok tad for 640 points of magic damage by Harm Touch.");

        Assert.Equal("Shadow Knight", Ingest([.. lines]).Snapshot().InferredClass);
    }

    /// <summary>Only bards sing, whatever the song is called — the parser separates
    /// "You begin to sing" from "You begin casting", so this needs no name table.</summary>
    [Fact]
    public void ABardIsFoundByHisSongs()
    {
        var lines = new List<string>();
        for (var i = 0; i < 3; i++)
            lines.Add(Stamp(0, i * 10) + "You begin to sing Selo's Song of Travel.");

        Assert.Equal("Bard", Ingest([.. lines]).Snapshot().InferredClass);
    }

    /// <summary>Beastlord is the sixteenth class (jlcrisp, #175) and reaches the ballot
    /// through the spell harvest like every other caster.</summary>
    [Fact]
    public void ABeastlordIsFoundByHisSpells()
    {
        var lines = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            lines.Add(Stamp(0, i * 10) + "You begin casting Spirit of Khaliz.");
            lines.Add(Stamp(0, i * 10 + 5) + "You begin casting Sha's Lethargy.");
        }

        Assert.Equal("Beastlord", Ingest([.. lines]).Snapshot().InferredClass);
    }

    /// <summary>A log with nothing class-unique in it names nobody. Plain swings are
    /// every melee class at once, so they are not evidence about any of them.</summary>
    [Fact]
    public void APlainMeleeLogInfersNothing()
    {
        var lines = new List<string>();
        for (var i = 0; i < 20; i++)
            lines.Add(Stamp(0, i) + "You slash a froglok tad for 42 points of damage.");

        Assert.Equal("", Ingest([.. lines]).Snapshot().InferredClass);
    }
}
