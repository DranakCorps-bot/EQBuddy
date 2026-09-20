using EQBuddy.Core;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// **The class-lens chip strip tracks the class multi-select** (DRA-181 D4, plan P5).
///
/// <para>The Founder's 2026-09-17 Desktop smoke: island+class grouping PASS, and then
/// "leftover bubble chips under the class multi-select (Warrior/Paladin/Cleric) do not track
/// the selected classes". The render narrowed picks-first; the strip was built from the
/// RESOLVED identity list, where picks widen and never remove — two producers of one answer
/// (trap 33). A leftover chip is also a DEAD control: clicking it sets a lens to a class the
/// render has narrowed away, and the next render silently clears it.</para>
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so <c>QuestClassLensTests</c>
/// proves the DECISION and only a launched app can say the strip on screen is built from it.
/// <c>questsClassStrip</c> folds the real strip's chip KEYS — a count would be unmoved by a
/// swap (trap 72), and "the right number of wrong chips" is exactly what the Founder saw.</para>
///
/// <para><b>The expected strings are DERIVED from what the test seeds</b>, through the same
/// <see cref="QuestClassFilter.Abbrev"/> the chip reads — a typed "Any+WAR+PAL" would still
/// pass the day the strip started listing something else with the same abbreviation
/// (trap 23).</para>
/// </summary>
public class QuestClassStripTests
{
    /// <summary>What the dump should read for a strip offering these classes: the Any chip,
    /// then one per class, abbreviated as the chip itself abbreviates them.</summary>
    private static string Strip(params string[] classes) =>
        string.Join("+", new[] { "Any" }.Concat(classes.Select(QuestClassFilter.Abbrev)));

    private static AppHarness Window() =>
        new(null, new Dictionary<string, string> { ["EQBUDDY_QUESTS"] = "1" });

    /// <summary>The same window with the DRA-199 lens rendezvous armed — the only way this
    /// suite can set a lens or rewrite picks mid-run. Separate from <see cref="Window"/> so
    /// the four rows above go on proving the strip with no probe in the process at all.</summary>
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
    /// **KEEP: with nothing picked, the strip is the character's identity** — unchanged, and
    /// this is the row that says so.
    ///
    /// <para>Without it every assertion below would pass on a build that had made the picker
    /// mandatory, or that had simply stopped drawing chips. The three classes come from the
    /// seeded unlock dump, because the fixture LOG resolves to one class and a one-class
    /// strip is collapsed on purpose ("Any · WAR" chooses nothing).</para>
    /// </summary>
    [Fact]
    public void WithNothingPickedTheStripOffersTheClassesIdentityHolds()
    {
        using var app = Window();
        app.SeedQuestLedger(classes: [], unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);

        Assert.Equal(Strip("Warrior", "Paladin", "Cleric"), app.DumpText("questsClassStrip"));
    }

    /// <summary>
    /// **THE FOUNDER'S SCREEN.** Same identity, two of the three picked — so the strip offers
    /// those two and the third has no chip at all.
    ///
    /// <para>Prove-failed against the pre-fix build, which answers
    /// <c>Any+WAR+PAL+CLR</c> here: the deselected class kept its chip, and clicking it lensed
    /// the view to a class the render had already dropped.</para>
    /// </summary>
    [Fact]
    public void PickingTwoOfThreeClassesLeavesChipsForThoseTwoAndNoOthers()
    {
        using var app = Window();
        app.SeedQuestLedger(
            classes: ["Warrior", "Paladin"],
            unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);

        Assert.Equal(Strip("Warrior", "Paladin"), app.DumpText("questsClassStrip"));
        // Named, because it is the chip the Founder was looking at.
        Assert.DoesNotContain(QuestClassFilter.Abbrev("Cleric"),
            app.DumpText("questsClassStrip"), StringComparison.Ordinal);
    }

    /// <summary>
    /// **ONE offered class collapses the strip to nothing, and that is the arm that regresses
    /// silently** (DRA-181 D4's done bar, landed in DRA-196).
    ///
    /// <para>"Any · WAR" chooses nothing, so <c>QuestsView.BuildClassStrip</c> clears the
    /// strip and returns before adding a single chip. That is what "the chips scale to the
    /// multi-select" buys at the bottom end — and every other row in this file asserts a
    /// NON-empty strip, so a build that started padding the strip back out to the identity list
    /// would leave all of them green.</para>
    ///
    /// <para><b>The pick is what narrows it here, not the identity</b>, which is why this row
    /// also prove-fails against the pre-D4 build: the dump names three classes, so the strip
    /// built from <c>resolved</c> held three chips and never reached the collapse at all. The
    /// decision under test is that the collapse reads the OFFERED list.</para>
    ///
    /// <para><b>The negative claim is anchored to a positive event</b> (trap 62). <c>-</c> is
    /// also what the dump reads before a strip has ever been built, so asserting it alone would
    /// pass on an app that had not drawn yet. <c>questsTabs</c> is the anchor and it is exact:
    /// <c>BuildClassStrip</c> is called from <c>BuildTabs</c>, in the same synchronous pass that
    /// fills <c>_tabs</c>, past the signature gate and after the offered list is assigned. So a
    /// dump carrying tabs is a dump whose strip was built from these picks — and both facts are
    /// read from ONE dump because two reads are two moments (trap 56).</para>
    /// </summary>
    [Fact]
    public void PickingOneClassCollapsesTheStripToNoChipsAtAll()
    {
        using var app = Window();
        app.SeedQuestLedger(
            classes: ["Warrior"],
            unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);

        var facts = app.DumpTexts("questsTabs", "questsClassStrip");
        // The anchor first: without it the sentinel below is satisfied by an app that has not
        // built a strip yet, which is every app for the first few hundred milliseconds.
        Assert.NotEqual("0", facts[0]);
        Assert.NotEqual("", facts[0]);
        // "-" is the dump's "no chips at all" sentinel — asserted rather than a count, because
        // a count is what a strip padded back to the identity list would also answer wrongly
        // and the sentinel is the only value that says the control left the screen.
        Assert.Equal("-", facts[1]);
    }

    /// <summary>
    /// **A pick identity does not carry still gets its chip**, which is the other half of one
    /// producer and is not reachable by accident: the dump names three classes, so
    /// <c>CharacterClasses.Resolve</c> is at <c>Max</c> before it ever looks at the picks, and
    /// the resolved list holds none of them.
    ///
    /// <para>The render has always narrowed to such a pick — the picker offers all sixteen on
    /// purpose, "we may be helping a friend" (David, 2026-08-15). Before this slice the strip
    /// was built from identity, so the classes the player had picked were the ones they could
    /// not lens to, and the three they could lens to did nothing.</para>
    /// </summary>
    [Fact]
    public void APickTheResolvedIdentityDoesNotCarryStillGetsItsChip()
    {
        using var app = Window();
        app.SeedQuestLedger(
            classes: ["Warrior", "Paladin"],
            unlockedClasses: ["Bard", "Druid", "Enchanter"]);
        app.Launch();

        WaitForTheWindow(app);

        Assert.Equal(Strip("Warrior", "Paladin"), app.DumpText("questsClassStrip"));
    }

    /// <summary>
    /// **THE LIVE TRANSITION** (DRA-181 D4's done bar, arm (c); DRA-199, AUTHORIZED by Helm
    /// <c>d15c1369</c>) — un-ticking the class the lens is currently ON drops its chip AND
    /// lands the selection on Any.
    ///
    /// <para><b>Every row above this one photographs a strip that was built once, at
    /// startup.</b> They prove the chips are built from the offered list; not one of them can
    /// say what happens when that list SHRINKS UNDER a live lens, which is the only moment the
    /// two halves can disagree — and it is a moment the app reaches on its own, because
    /// EQBuddy Mobile writes these picks (<c>CompanionActions.SetClasses</c>) while this
    /// window sits open.</para>
    ///
    /// <para><b>The lens is set by the probe, and the picks are rewritten by the probe, and
    /// neither is a path built for the test.</b> <c>lens</c> drives the chip's own click body;
    /// <c>picks</c> drives the ledger writer the phone uses, forcing no refresh — so the
    /// repaint asserted here is the one the <c>off:</c> signature term earns, not one the
    /// suite asked for.</para>
    ///
    /// <para><b>Both halves are read from ONE dump</b> (trap 56): "the chip went away" and
    /// "the selection landed somewhere real" are two claims, and two reads would be two
    /// renders. The wait in between is on the STRIP — the first half — so the assertion about
    /// the selection is anchored to a positive event that can only happen after the repaint
    /// (trap 62).</para>
    ///
    /// <para><b>The mid-test assertion is load-bearing, not a comment.</b> <c>Any</c> is also
    /// what the lens fact reads before anything has ever been lensed, so without proving the
    /// view really is on <c>CLR</c> first, the final assertion would pass on a build where the
    /// lens never engaged at all — trap 11's shape, a verdict only one side can produce.</para>
    ///
    /// <para><b>Prove-failed</b> by deleting the stale-lens reset in <c>QuestsView.Refresh</c>
    /// (<c>else if (_classLens is not null &amp;&amp; !classes.Contains(...)) _classLens =
    /// null;</c>): the strip drops the chip either way, the lens stays on Cleric, nothing on
    /// the strip is painted selected, and <c>questsClassLens</c> reads the <c>-</c> sentinel.
    /// With that mutant in place a lens fact read off <c>_classLens</c> instead of off the
    /// strip answers <c>CLR</c> — a class with no chip on screen — which is why the fact is
    /// read off <see cref="EQBuddy.EqSegmentedStrip.Selected"/>.</para>
    /// </summary>
    [Fact]
    public void UntickingTheLensedClassDropsItsChipAndLandsTheSelectionOnAny()
    {
        using var app = ProbeWindow();
        app.SeedQuestLedger(
            classes: ["Warrior", "Paladin", "Cleric"],
            unlockedClasses: ["Warrior", "Paladin", "Cleric"]);
        app.Launch();

        WaitForTheWindow(app);
        Assert.Equal(Strip("Warrior", "Paladin", "Cleric"), app.DumpText("questsClassStrip"));

        // ON the class we are about to take away. The probe returns on the far side of the
        // write, but the dump is written on its own timer, so the wait is on the FACT.
        app.SetClassLens("Cleric");
        Wait.Until(() => app.DumpText("questsClassLens") == QuestClassFilter.Abbrev("Cleric"),
            TimeSpan.FromSeconds(30),
            $"the class strip to light {QuestClassFilter.Abbrev("Cleric")} after lensing to it",
            app.Artifacts);

        // The phone's own writer, with no forced refresh behind it.
        app.SetClassPicks("Warrior", "Paladin");

        Wait.Until(() => app.DumpText("questsClassStrip") == Strip("Warrior", "Paladin"),
            TimeSpan.FromSeconds(30),
            "the un-picked class's chip to leave the strip", app.Artifacts);

        var facts = app.DumpTexts("questsClassStrip", "questsClassLens");
        Assert.Equal(Strip("Warrior", "Paladin"), facts[0]);
        // "Any", not "-": the chip that is LIT, from the same moment the strip above was read.
        // "-" is the sentinel for "nothing on this strip is painted selected", which is what a
        // stranded lens looks like on screen.
        Assert.Equal("Any", facts[1]);
    }
}
