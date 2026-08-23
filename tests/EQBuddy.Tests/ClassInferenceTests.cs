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

    /// <summary>
    /// Two classes with comparable recent evidence is a **two-class character**, and both
    /// are named.
    ///
    /// **This test asserted the opposite until 2026-08-23**, and it was the bug rather
    /// than the guard: it expected `""`, documented as *"the reading drives the quest lens,
    /// so 'don't know' beats a coin toss"*. That is right for a game where you have one
    /// class. EverQuest Legends gives you up to three (David: *"you seem to think EQ
    /// Legends just lets you have 1 class when in fact you can be 3 at a time"*), so a
    /// player backstabbing and laying on hands is not an ambiguous log — he is a
    /// Rogue/Paladin, and the honest answer is to say so.
    ///
    /// What "don't know" still means is <see cref="OneSightingNeverNamesAClassAndThreeDo"/>'s
    /// case: not enough evidence, rather than too much.
    /// </summary>
    [Fact]
    public void TwoClassesPlayedTogetherAreBothNamed()
    {
        var inf = new ClassInference();
        for (var i = 0; i < 4; i++)
        {
            inf.RecordAbilityUse("Backstab", At(i));
            inf.RecordAbilityUse("Lay on Hands", At(i + 0.5));
        }

        Assert.Equal(["Paladin", "Rogue"], inf.CurrentClasses().OrderBy(c => c));
        // Current() still answers, and now means "the one you are playing MOST" rather
        // than "the one I am sure enough to name" — Lay on Hands is the later of the two
        // each round, so Paladin carries marginally less decay.
        Assert.Equal("Paladin", inf.Current());
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
        // Mid-handover BOTH are named — half an hour of rogue does not stop being true
        // because a wizard sat down five minutes ago, and on a real character it might
        // never have been someone else. What used to happen here was `""`: the app
        // withdrew the reading entirely, which is the behaviour that left a genuine
        // multi-class player with no classes at all.
        Assert.Contains("Rogue", inf.CurrentClasses());
        Assert.Contains("Wizard", inf.CurrentClasses());

        for (var i = 30; i < 120; i++) Cast(i / 6.0);          // 20 min in
        // Recency still decides the ORDER, which is the half of #120 that mattered: what
        // you are playing now leads.
        Assert.Equal("Wizard", inf.Current());
        // And the stale class eventually falls under MemberFraction and drops out — two
        // more half-lives of silence.
        for (var i = 120; i < 240; i++) Cast(i / 6.0);         // 40 min in
        Assert.Equal(["Wizard"], inf.CurrentClasses());
    }

    /// <summary>
    /// #120 (Frankthetankk, 2026-08-21): the ALT-SWAP case — *"someone plays two classes
    /// roughly equally rather than a one-time dabble… does the half-life decay handle that
    /// gracefully, or is there a real risk of visible flip-flopping session to session?"*
    ///
    /// **Re-expressed 2026-08-23, and the honest reading is that his question had a false
    /// premise we supplied.** The old answer was "there is no flip-flop because there are
    /// only two outcomes: the class in hand, or the honest `""` during a handover" — the
    /// reading went QUIET between stretches. That is defensible for two alts sharing a log
    /// and wrong for the case it cannot tell apart: one character who is both classes.
    /// Legends allows three at once, so going quiet is not caution, it is losing the
    /// answer.
    ///
    /// What replaces it: **both are named while both are recent, and the one being played
    /// leads.** The flicker he was actually worried about — the LEAD swapping back and
    /// forth mid-stretch — is still asserted, and is still absent.
    /// </summary>
    [Fact]
    public void AlternatingTwoClassesNamesBothAndLeadsWithTheOneInHand()
    {
        var inf = new ClassInference();
        var leads = new List<string>();

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
                leads.Add(inf.Current());
            }
            // The class being PLAYED leads at the end of its stretch...
            Assert.Equal(rogue ? "Rogue" : "Wizard", inf.Current());
            // ...and the class NOT being played never leads, whether or not it is still
            // named. Both outcomes are legitimate and which one you get depends on signal
            // DENSITY rather than on elapsed time — a fact worth stating because it looks
            // like an inconsistency until you see why:
            //
            //   after a Rogue stretch  -> ["Rogue", "Wizard"]   Wizard is still named
            //   after a Wizard stretch -> ["Wizard"]            Rogue has dropped
            //
            // The wizard casts TWO class-unique spells per tick and the rogue backstabs
            // once, so wizard weight accrues twice as fast and survives two half-lives of
            // silence where rogue weight does not. Weight measures how loudly a class
            // announces itself, not how long it was played. For an ALT SWAP either answer
            // is fine — the lead is right in both. For a real MULTI-CLASS character the
            // question never arises: a Warrior/Druid/Monk rotates inside a single fight,
            // so all three stay recent (see PlayNowOutweighsPlayAnHourAgo's handover).
            Assert.Equal(rogue ? "Rogue" : "Wizard", inf.CurrentClasses()[0]);
            Assert.InRange(inf.CurrentClasses().Count, 1, 2);
        }

        // It only ever leads with a class it has evidence for — never a third one, and
        // never nothing once the first stretch has earned an answer.
        Assert.All(leads, a => Assert.Contains(a, new[] { "", "Rogue", "Wizard" }));

        // The flicker #120 asked about: the lead changing hands more than once per
        // stretch. Four stretches means at most four handovers, and anything more is the
        // reading oscillating under a player who is doing one thing.
        var changes = leads.Where((a, i) => i > 0 && a != leads[i - 1]).Count();
        Assert.True(changes <= 4,
            $"the leading class changed {changes} times across four stretches — that is the "
            + "flip-flop #120 asked about");
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
        // Three each, and neither is INFLATED — which is the property under test. Replaying
        // the older Paladin lines cannot decay the banked Rogue weight (the clock never runs
        // backwards), so the two sit at exactly equal weight and both are named.
        //
        // The tie then breaks on the class NAME, not on dictionary order: that is the half
        // of the old assertion worth keeping. It used to expect `""` under the "two close
        // classes name nobody" rule, with the comment that "whichever way the dictionary
        // happened to enumerate must not decide what the quest tracker filters by" — still
        // true, and now guaranteed by an explicit ThenBy rather than by refusing to answer.
        Assert.Equal(["Paladin", "Rogue"], inf.CurrentClasses());
        Assert.Equal("Paladin", inf.Current());
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
