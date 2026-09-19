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
}
