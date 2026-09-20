using EQBuddy.Core;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// **The <c>My Classes</c> quick-select in the quest class lens** (DRA-216 D1, S4.3;
/// acceptance S22 AC 5/6).
///
/// <para>One action selects the classes the character actually plays, instead of three or four
/// trips through a sixteen-row popup re-entering something the app already resolved.</para>
///
/// <para><b>Only a launched app can say this control exists.</b> It lives inside a WPF
/// <c>Popup</c> — its own top-level HWND, which <c>PrintWindow</c> does not render at all, so
/// <c>shoot.ps1</c> photographs the shut button and nothing behind it (trap 79). An absent
/// control also photographs as an unremarkable panel (trap 29). <c>QuestClassLensTests</c>
/// proves the DECISION; these rows prove the button reached the screen and that pressing it
/// moves the real selection.</para>
///
/// <para><b>The expected strings are DERIVED from what the test seeds</b>, through the same
/// <see cref="QuestClassFilter.Abbrev"/> the dump reads — a typed "CLR+PAL+WAR" would still
/// pass the day the fact started naming something else abbreviating the same way (trap 23).</para>
///
/// <para><b>What is NOT here, and why.</b> The empty arm — no dump, no log evidence, no
/// statement, so no button at all — cannot be staged in this suite: the fixture log resolves a
/// class on its own, so every launched character has an identity. It is covered at the unit
/// level by <c>NothingResolvedMeansNoQuickSelectRatherThanAnEmptyOne</c>, and the dump carries
/// <c>questsMyClassesBtn</c> so the day a class-silent fixture exists the arm is one row.</para>
/// </summary>
public class QuestMyClassesTests
{
    /// <summary>What the dump should read for a strip offering these classes: the Any chip,
    /// then one per class, abbreviated as the chip abbreviates them.</summary>
    private static string Strip(params string[] classes) =>
        string.Join("+", new[] { "Any" }.Concat(classes.Select(QuestClassFilter.Abbrev)));

    /// <summary>What <c>questsMyClasses</c> should read: the quick-select's own answer, in the
    /// picker's ROW order, abbreviated because the dump is space-separated and
    /// "Shadow Knight" carries a space.</summary>
    private static string Mine(params string[] classes) =>
        string.Join("+", classes.Select(QuestClassFilter.Abbrev));

    private static AppHarness ProbeWindow() =>
        new(null, new Dictionary<string, string>
        {
            ["EQBUDDY_QUESTS"] = "1",
            ["EQBUDDY_LENSPROBE"] = "1",
        });

    private static void WaitForTheWindow(AppHarness app) =>
        Wait.Until(() => app.DumpValue("questsTabs") > 0, TimeSpan.FromSeconds(45),
            "the Quest Tracker window to open", app.Artifacts);

    /// <summary>
    /// **The button is offered, and it names the character's own classes** (S22 AC 5).
    ///
    /// <para>Two facts from ONE dump, because they are two claims (trap 56): the producer
    /// answered, and the control reached the popup. A build whose producer worked while
    /// <c>SetActions</c> was never called would pass the first and fail the second — which is
    /// the failure a screenshot of this surface can never see.</para>
    ///
    /// <para><b>ROW order, not dump order.</b> The seeded unlock list leads with Warrior (the
    /// primary class, which the dump names first); the rows are alphabetical. Asserting the row
    /// order is what pins that the answer is in the spelling and sequence the picker paints
    /// ticks by — the same selection represented two ways is trap 4's shape.</para>
    /// </summary>
    [Fact]
    public void TheQuickSelectIsOfferedAndNamesTheCharactersOwnClasses()
    {
        using var app = ProbeWindow();
        app.SeedQuestLedger(classes: [], unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);

        var facts = app.DumpTexts("questsMyClasses", "questsMyClassesBtn");
        Assert.Equal(Mine("Cleric", "Paladin", "Warrior"), facts[0]);
        // The control itself, counted off the REAL picker rather than off the list handed to
        // it (trap 29) — "the answer exists" and "the button exists" are different claims.
        Assert.Equal("1", facts[1]);
    }

    /// <summary>
    /// **THE POINT OF THE SLICE: pressing it selects the character's classes** (S22 AC 5), and
    /// the whole surface follows.
    ///
    /// <para>The player starts lensed to a friend's Bard — reachable and ordinary, the picker
    /// offers all sixteen on purpose ("we may be helping a friend", David 2026-08-15) — while
    /// their own character is a Warrior/Paladin/Cleric. One pick collapses the chip strip to
    /// nothing ("Any · BRD" chooses nothing), so the BEFORE state is the <c>-</c> sentinel and
    /// the AFTER state is three chips: a transition neither end of which is the dump's
    /// start-up value, so neither can pass on an app that never drew.</para>
    ///
    /// <para><b>The strip is the assertion because the strip is what the picks drive.</b> It is
    /// built from <c>QuestClassLens.Offered</c>, which is picks-first — so three chips here say
    /// the PICKS became the three classes, through <c>OnClassCheckChanged</c>, the store's one
    /// writer. A quick-select that only painted ticks without writing would leave the strip on
    /// the Bard.</para>
    ///
    /// <para><b>And the rows survive</b> (S22 AC 6): all sixteen are still in the popup
    /// afterwards, so every class stays one click from being added or removed. Read from the
    /// same dump as the strip — a rebuilt or narrowed list is exactly what "you can still
    /// add/remove afterward" forbids, and nothing else in this dump would move if it happened.</para>
    /// </summary>
    [Fact]
    public void PressingItSelectsTheCharactersClassesAndLeavesEveryRowStillPickable()
    {
        using var app = ProbeWindow();
        app.SeedQuestLedger(
            // A friend's class, and not one of the character's own.
            classes: ["Bard"],
            unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);
        // The BEFORE, load-bearing rather than decorative: without it the assertion below
        // would pass on a build where the picks had been the identity all along (trap 11 —
        // a verdict only one side can produce).
        var before = app.DumpTexts("questsClassStrip", "questsClassRows");
        Assert.Equal("-", before[0]);
        Assert.Equal(QuestClassFilter.Classes.Length.ToString(), before[1]);

        app.PressMyClasses();

        // On the STRIP, which is downstream of the store write — the probe returns on the far
        // side of the click, but the dump is written on its own timer (trap 62).
        Wait.Until(
            () => app.DumpText("questsClassStrip") == Strip("Cleric", "Paladin", "Warrior"),
            TimeSpan.FromSeconds(30),
            "the class strip to offer the character's own three classes after My Classes",
            app.Artifacts);

        var after = app.DumpTexts("questsClassStrip", "questsClassRows", "questsMyClasses");
        Assert.Equal(Strip("Cleric", "Paladin", "Warrior"), after[0]);
        // S22 AC 6 — every class is still one click away, in the same moment the strip moved.
        Assert.Equal(QuestClassFilter.Classes.Length.ToString(), after[1]);
        // The friend's class is gone from the LENS, which is what "select my classes" means.
        Assert.DoesNotContain(QuestClassFilter.Abbrev("Bard"), after[0], StringComparison.Ordinal);
        // And the quick-select still answers identity, unmoved by having been pressed: picks
        // widen identity and never remove from it (S3.3), so a build that had let the action
        // write identity would answer the picks back here.
        Assert.Equal(Mine("Cleric", "Paladin", "Warrior"), after[2]);
    }

    /// <summary>
    /// **It answers IDENTITY, not the picks** (S4.3) — the requirement, on the screen.
    ///
    /// <para>The character's dump names three classes, so <c>CharacterClasses.Resolve</c> is at
    /// <c>Max</c> before it ever reaches the picks and identity holds none of them. The picks
    /// are two entirely different classes. A quick-select reading "whatever the lens is showing"
    /// would answer <c>BRD+DRU</c> here and be a button that re-selects what is already
    /// selected; identity answers Warrior/Paladin/Cleric.</para>
    ///
    /// <para>This is the arm that reddens on the one wrong implementation a reviewer cannot see
    /// in a screenshot — the strip, the face and the rows are all identical either way.</para>
    /// </summary>
    [Fact]
    public void TheQuickSelectAnswersIdentityEvenWhileTheLensIsOnOtherClasses()
    {
        using var app = ProbeWindow();
        app.SeedQuestLedger(
            classes: ["Bard", "Druid"],
            unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);

        var facts = app.DumpTexts("questsClassStrip", "questsMyClasses");
        // The lens really is on the friend's classes — the anchor, so the claim below is about
        // a disagreement that exists rather than about two lists that happen to match.
        Assert.Equal(Strip("Bard", "Druid"), facts[0]);
        Assert.Equal(Mine("Cleric", "Paladin", "Warrior"), facts[1]);
    }
}
