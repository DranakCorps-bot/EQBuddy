using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **SR-2's move, guarded from the only side a test can reach it.**
///
/// The gear checklist import — the website link, the file picker, Clear and the status line —
/// left `OptionsWindow.xaml(.cs)` for <c>GearCardView</c>, the surface its result appears on.
/// It is NOT a lift into a shared block like SR-1 and SR-4: an import workflow is a domain
/// action rather than a setting, and this one lived two windows away from every list it ever
/// produced (`docs/v2/v1-feature-disposition.md`'s own disposition, and Fable's signed item 3).
///
/// **The enumeration is trap 26 written as code**, the shape
/// <see cref="SettingsAlertsBlockTests"/> established and <see cref="SettingsLookBehaviorBlockTests"/>
/// followed: "when you fold a surface, list every control on it and say where each one went".
/// Three player-facing bugs (#204, #210, #212) came from one event — a surface moved, the data
/// survived and the write path did not — and none of them was visible to a compiler, a test or
/// a screenshot. A list in a commit message is read once; this one fails the build when a row
/// stops being true.
///
/// **Why this file is also the must-list, and what the check of the two existing ones found.**
/// The plan asked whether the checklist import owes a row to
/// <see cref="ImportReportReachesASurfaceTests"/> or <see cref="GameCommandsTests"/>. Checked by
/// subject, it owes neither, and the reasons are worth writing down rather than leaving as
/// silence:
/// <list type="bullet">
/// <item><b>`ImportReportReachesASurfaceTests` is about <c>AutoImportOutcome</c>s</b> — dumps
/// EQBuddy reads BY ITSELF and could therefore act on without saying so. This import is a file
/// the player picked in a dialog; it records no outcome, and it reports in the line beside the
/// button they just pressed. Adding a row would have meant inventing a property to satisfy the
/// guard, which is the premise-first mistake of trap 52.</item>
/// <item><b>`GameCommandsTests.SurfacesNeedingACommand` is about IN-GAME commands.</b> Its Gear
/// row (`GearCardView` → `/outputfile inventory`) is untouched and this change never goes near
/// it — an EQ Legends Tools export is a website file, not something you type in chat.</item>
/// </list>
/// So the affordance the move puts at risk has no existing must-list, and gets one here: the
/// import block is now the ONLY route into the feature, and trap 34's whole lesson is that a
/// missing thing is invisible to everything except a must-list or an assertion. `gearImport` /
/// `shellGearImport` in the E2E dump are the runtime half.
/// </summary>
public class GearImportBlockMoveTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string Read(string project, string relative) =>
        File.ReadAllText(Path.Combine(Src, project, relative));

    private static string Card => Read("EQBuddy", "GearCardView.cs");

    /// <summary>The card with its commentary removed, for the assertions about text that must
    /// NOT be on this surface. The file explains at length what the empty state used to say and
    /// why it stopped saying it; a raw scan would be matching the history rather than the
    /// product.</summary>
    private static string CardCode => StripComments(Card);

    /// <summary>
    /// Options with its comments removed. The XAML and the code-behind both keep a note saying
    /// what left and where it went (trap 26 is a duty to say so, and the next reader of that
    /// file is someone hunting the block) — so an "it is gone" assertion that matched raw text
    /// would either fail on the note or force the note to be vague. What matters is that
    /// nothing LIVE references the block, which is what this strips down to.
    /// </summary>
    private static string OptionsWithoutComments(string file) => StripComments(Read("EQBuddy", file));

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, "<!--.*?-->", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(text, @"(?m)^\s*//.*$", "");
    }

    /// <summary>
    /// Every control the Options block declared, and the token on the destination surface that
    /// proves it landed. A row asserts BOTH halves — gone from Options, built in
    /// <c>GearCardView</c> — because a control that left one surface and arrived at none is
    /// exactly the shape of the three bugs above.
    ///
    /// A row that cannot fail is not a guard (trap 39): every token here was checked against
    /// the pre-move tree, where all of the `Gone` halves fail.
    /// </summary>
    public static readonly (string WasNamed, string Went, string NowBuiltAs)[] Enumeration =
    [
        // ---- the three buttons
        ("GearToolsBtn", "the website link, above the list on the Wishlist surface",
            "GearChecklistPresentation.OpenToolsButton"),
        ("GearImportBtn", "the file picker, beside it", "GearChecklistPresentation.ImportButton"),
        ("GearClearBtn", "Clear, beside those", "GearChecklistPresentation.ClearButton"),

        // ---- what each button DOES. The handlers went with their controls, and the two
        // mutations went further than that — into Core, where the rule that a re-import keeps
        // your ticks can be tested rather than asserted about a window.
        ("OnOpenGearTools", "the browser launch, off one shared address",
            "GearChecklistPresentation.ToolsUrl"),
        ("OnImportGearChecklist", "the file dialog", "Microsoft.Win32.OpenFileDialog"),
        ("OnImportGearChecklist", "the dialog's own title and filter",
            "GearChecklistPresentation.ImportDialogTitle"),
        ("OnImportGearChecklist", "preserve-ticks-then-replace, now in Core",
            "GearChecklistImporter.Apply"),
        ("OnClearGearChecklist", "forget the list, now in Core", "GearChecklistImporter.Clear"),

        // ---- the status line, and the three things only it could ever say
        ("GearImportStatus", "a line under the buttons that speaks when something happened",
            "_importStatus"),
        ("GearImportStatus", "an empty file, named as the wrong file rather than as a fault",
            "GearChecklistPresentation.NoItemsFound"),
        ("GearImportStatus", "an import that threw", "GearChecklistPresentation.ImportFailed"),
        ("GearImportStatus", "a browser that would not open",
            "GearChecklistPresentation.CouldNotOpenTools"),
        ("GearImportStatus", "an import that WORKED — the half a silent success loses",
            "GearChecklistPresentation.Imported"),
    ];

    public static TheoryData<string, string, string> EnumerationRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var (was, went, now) in Enumeration) rows.Add(was, went, now);
        return rows;
    }

    [Theory]
    [MemberData(nameof(EnumerationRows))]
    public void EveryMovedControlLandedOnTheGearSurfaceAndLeftOptions(
        string wasNamed, string went, string nowBuiltAs)
    {
        Assert.DoesNotContain(wasNamed, OptionsWithoutComments("OptionsWindow.xaml"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(wasNamed, OptionsWithoutComments("OptionsWindow.xaml.cs"),
            StringComparison.Ordinal);

        Assert.True(Card.Contains(nowBuiltAs, StringComparison.Ordinal),
            $"{wasNamed} moved to {went} and GearCardView.cs does not build it (looked for "
            + $"\"{nowBuiltAs}\"). A control that left one surface and arrived at none is the "
            + "shape of #204, #210 and #212 — the data survives the move and the write path "
            + "does not, and nothing else in this repo can see it.");
    }

    /// <summary>
    /// **The two pieces that deliberately did NOT travel, and they are the trap-26 half people
    /// skip.** Enumerating what moved is easy; saying what was dropped, and why dropping it was
    /// not a loss, is the part that keeps a "we shipped one fact twice" from riding along.
    ///
    /// The Options block opened with a heading ("Gear checklist") and an explanation ("Import
    /// the exported shopping-list HTML from EQ Legends Tools, then show it as a checklist in the
    /// Gear overlay card"). On the destination both are already there and said better: the tab
    /// IS the gear checklist, and <see cref="GearChecklistPresentation.EmptyRoute"/> is the
    /// explanation — the one David made us rewrite twice on 2026-08-20 until it defined the term
    /// and named the website. Carrying the pair across would have put the same fact on the same
    /// surface twice, one paragraph under a better version of itself, which is exactly the
    /// duplicate SR-1 found on the Behavior tab.
    /// </summary>
    [Fact]
    public void TheHeadingAndTheBlurbWereNotCarriedBecauseTheSurfaceAlreadySaysBoth()
    {
        Assert.DoesNotContain("Import the exported shopping-list HTML", Card, StringComparison.Ordinal);
        Assert.DoesNotContain("Gear overlay card", Card, StringComparison.Ordinal);

        // And the half that makes the omission legitimate rather than a deletion: the surface's
        // own explanation is there, it defines what a gear list is, and it names the website.
        Assert.Contains("eqlegends.tools", GearChecklistPresentation.EmptyRoute, StringComparison.Ordinal);
        Assert.Contains("wishlist you build", GearChecklistPresentation.EmptyRoute, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The status line's STEADY state did not travel either, and this is the one row that
    /// could have shipped a duplicate.** Options printed "{name}: {done}/{total} checked." under
    /// the buttons — which is what <c>_listName</c> has always said two lines above it on the
    /// destination (`GearChecklistPresentation.ListName`). One fact, one place. What the line
    /// keeps is what only it can say: the outcome of the last action.
    /// </summary>
    [Fact]
    public void TheStatusLineSaysOutcomesAndNotTheCountTheListNameAlreadyCarries()
    {
        Assert.DoesNotContain("checked.\"", CardCode, StringComparison.Ordinal);
        Assert.DoesNotContain("No gear list imported", CardCode, StringComparison.Ordinal);

        // ListName is still the steady state, and still the only one.
        Assert.Contains("GearChecklistPresentation.ListName", Card, StringComparison.Ordinal);
    }

    /// <summary>
    /// **`IsEnabled = false` became hidden, on purpose (trap 17).** Options greyed Clear out
    /// when there was nothing to clear — except that this app's button style carries no disabled
    /// visual, so the control rendered exactly like a live one and silently swallowed the click.
    /// That is "silent no-ops are broken" with the switch on the other side, and it is invisible
    /// to a screenshot. On the destination the button is simply not there when there is nothing
    /// to remove, which is the same fact said in a way a player can see.
    /// </summary>
    [Fact]
    public void ClearIsHiddenRatherThanDisabledWhenThereIsNothingToClear()
    {
        Assert.Contains("_clear.Visibility = total > 0", Card, StringComparison.Ordinal);
        Assert.DoesNotContain("_clear.IsEnabled", Card, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Clear asks first, and the confirmation is a consequence of the move rather than a
    /// change of mind.** In Options this button sat two clicks deep on a screen nobody keeps
    /// open; it now sits beside a list a player reads every session. What a mis-click destroys
    /// is not the export — that is still on the website — but every box ticked since, which is
    /// the part EQBuddy holds and the website has never heard of. An irreversible operation that
    /// got easier to reach gets a net (trap 48's closing lesson).
    /// </summary>
    [Fact]
    public void ClearingAnImportedListAsksFirstAndSaysWhatIsActuallyLost()
    {
        Assert.Contains("MessageBox.Show", Card, StringComparison.Ordinal);
        Assert.Contains("GearChecklistPresentation.ClearConfirm", Card, StringComparison.Ordinal);

        var prompt = GearChecklistPresentation.ClearConfirm("Harness list", 12);
        Assert.Contains("Harness list", prompt, StringComparison.Ordinal);
        Assert.Contains("12 rows", prompt, StringComparison.Ordinal);
        // The reassurance and the warning are both load-bearing: the rows come back, the ticks
        // do not, and a prompt that said only "are you sure?" would leave a player guessing at
        // which of those two they were about to lose.
        Assert.Contains("still on the website", prompt, StringComparison.Ordinal);
        Assert.Contains("not the ticks", prompt, StringComparison.Ordinal);
        Assert.Contains("1 row", GearChecklistPresentation.ClearConfirm("", 1), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The route line stopped pointing at Options the moment the block left it.** This is the
    /// rule that is not up for renegotiation, one level below a What's-new entry: "you should
    /// never have to hunt for something EQBuddy relocated". The empty state's whole job is to
    /// tell a player where to go, and a sentence naming a screen the buttons are no longer on is
    /// #219's mechanism inside one string — worse than no sentence, because it is followed.
    /// </summary>
    [Fact]
    public void TheEmptyStateRouteNamesTheButtonsBeneathItAndNotOptions()
    {
        var route = GearChecklistPresentation.EmptyRoute;

        Assert.DoesNotContain("Options", route, StringComparison.Ordinal);
        Assert.DoesNotContain("Cards & windows", route, StringComparison.Ordinal);

        // It names them off the SAME consts the buttons are labelled from — a route line that
        // spelled the label itself is one rename away from pointing at nothing.
        Assert.Contains(GearChecklistPresentation.ImportButton, route, StringComparison.Ordinal);
        Assert.Contains(GearChecklistPresentation.OpenToolsButton, route, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Both hosts got the block, and both report it — which is the only claim a screenshot of
    /// either one cannot make** (trap 29: an absent control photographs as an unremarkable
    /// panel). `GearLootWindow` and the shell's `GearRoom` each build their own
    /// <c>GearCardView</c>, so a single `DebugImportShown` on the card is enough to cover both;
    /// what has to be checked here is that each host actually WRITES it, under its own key
    /// (trap 58 — two hosts, one flat dump namespace).
    /// </summary>
    [Fact]
    public void BothHostsReportTheImportBlockToTheDump()
    {
        Assert.Contains("DebugImportShown", Card, StringComparison.Ordinal);
        Assert.Contains("gearImport={", Read("EQBuddy", "GearLootWindow.xaml.cs"), StringComparison.Ordinal);
        Assert.Contains("shellGearImport={", Read("EQBuddy", "GearRoom.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The ⧉ copy of `/outputfile inventory` is untouched, and the row that pins it is too.**
    /// The import block arriving above the list must not have pushed the auto-tick note and the
    /// command copy anywhere — they are outside the scroller precisely so a forty-row list
    /// cannot bury them (trap 37), and `GameCommandsTests.SurfacesNeedingACommand` carries this
    /// file's row for the command itself.
    /// </summary>
    [Fact]
    public void TheInGameCommandAffordanceIsUnchangedByTheMove()
    {
        Assert.Contains("GameCommands.OutputfileInventory", Card, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(_copyCmd)", Card, StringComparison.Ordinal);
        Assert.Contains(("EQBuddy/GearCardView.cs", nameof(GameCommands.OutputfileInventory)),
            GameCommandsTests.SurfacesNeedingACommand.Select(r => (r.File, r.Command)));
    }

    // ---------------------------------------------------------------------------------
    // The mutations, now that they are in Core and can actually be run
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The clause that is the whole difference between a re-import and a wipe: boxes ticked in
    /// the app survive, because the fresh export only knows what the website was told. It lived
    /// in `MainWindow` until this move and could only be asserted ABOUT; here it is executed.
    /// </summary>
    [Fact]
    public void ApplyingAnImportKeepsTheBoxesTheWebsiteNeverHeardAbout()
    {
        var settings = new AppSettings
        {
            GearChecklistName = "Old list",
            GearChecklist =
            [
                new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi", Acquired = true },
                new GearChecklistItem { Slot = "HANDS", Item = "Gloves of Dark Embers" },
            ],
        };

        GearChecklistImporter.Apply(settings, new GearChecklistImportResult
        {
            Name = "New list",
            Items =
            [
                new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi" },
                new GearChecklistItem { Slot = "FEET", Item = "Boots of the Storm" },
            ],
        });

        Assert.Equal("New list", settings.GearChecklistName);
        Assert.Equal(2, settings.GearChecklist.Count);
        // The tick survived the replacement...
        Assert.True(settings.GearChecklist.Single(i => i.Slot == "HEAD").Acquired);
        // ...and a row the new export brought is not ticked for having been near one.
        Assert.False(settings.GearChecklist.Single(i => i.Slot == "FEET").Acquired);
        // The row that is no longer on the website is gone, ticked or not — a re-import
        // REPLACES, which is what the button has always promised.
        Assert.DoesNotContain(settings.GearChecklist, i => i.Slot == "HANDS");
    }

    [Fact]
    public void ClearingForgetsTheListAndItsName()
    {
        var settings = new AppSettings
        {
            GearChecklistName = "Old list",
            GearChecklist = [new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi" }],
        };

        GearChecklistImporter.Clear(settings);

        Assert.Empty(settings.GearChecklist);
        Assert.Equal("", settings.GearChecklistName);
    }

    /// <summary>An import that arrived and changed nothing has to say so, or it is
    /// indistinguishable from a file EQBuddy never read — the confusion the whole auto-import
    /// report exists to end (David, 2026-08-20), arriving here through a dialog instead.</summary>
    [Fact]
    public void TheSuccessLineNamesTheListAndCountsItsRows()
    {
        Assert.Equal("Imported Harness list — 4 rows.",
            GearChecklistPresentation.Imported("Harness list", 4));
        Assert.Equal("Imported the gear list — 1 row.", GearChecklistPresentation.Imported("", 1));
    }
}
