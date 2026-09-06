using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **SR-5's room, guarded from the only side a unit test can reach it.**
///
/// The Evolved shell's Settings room is a WPF control in a project with no test host
/// (docs/TestPlan.md §5), so what can be asserted here is Core's own surface —
/// <see cref="SettingsSurface"/>, whose labels and keys are what the rail, the Ctrl+K palette
/// and the <c>page:room</c> address grammar all read — and the SOURCE of the room and its two
/// hosts. The behaviour that needs a running app (the room paints, the tabs land, the two
/// hosts agree) is `tests/EQBuddy.E2E/ShellHostTests`, and the two halves are deliberately
/// different questions: this file says the wiring exists, that one says it is in effect.
///
/// **The enumeration below is trap 26 written as code**, the shape
/// <see cref="SettingsAlertsBlockTests"/> established for SR-4 and
/// <see cref="SettingsHudBlockTests"/> repeated for SR-3 — with one difference worth naming.
/// Those two guarded a LIFT: controls leaving one file and arriving in another, where the
/// failure is a write path left behind. This guards a COMPOSITION: the room must not
/// re-declare anything, and every row here is about the room reaching for the block rather
/// than building its own copy. A second copy of forty control wirings is #210's mechanism
/// with a bigger surface, and it is invisible in a screenshot — both hosts render, both look
/// right, and they drift from the first commit that touches one of them.
/// </summary>
public class SettingsRoomTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string Read(string project, string relative) =>
        File.ReadAllText(Path.Combine(Src, project, relative));

    private static string Room => Read("EQBuddy", "SettingsRoom.cs");
    private static string Shell => Read("EQBuddy", "ShellWindow.xaml.cs");
    private static string Window => Read("EQBuddy", "OptionsWindow.xaml.cs");

    /// <summary>
    /// The same file with its comments removed — what every NEGATIVE below is asked against.
    ///
    /// **This is not fastidiousness; the first draft failed on its own documentation.** Four
    /// of the assertions here forbid a token (`AppSettings.Load`, `EnterPlacement`,
    /// `HotkeyManager.Parse(`), and this codebase's whole convention is that a room NAMES the
    /// thing it deliberately does not do — trap 26's "list every control and say where it
    /// went", the pattern `GearRoom`'s loot-star note and `WorldRoom`'s deaths-star note both
    /// follow. A scan that cannot tell an explanation from a call can only be satisfied by
    /// deleting the explanation, which is the guard actively making the code worse.
    /// <c>ShellTerminologyTests</c> hit the same wall from the other side and walked
    /// characters for the same reason: `//` inside a string is not a comment.
    /// </summary>
    private static string CodeOnly(string source)
    {
        var kept = new System.Text.StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i += 2;
            }
            else if (c == '"')
            {
                var verbatim = i > 0 && source[i - 1] == '@';
                kept.Append(c);
                i++;
                while (i < source.Length)
                {
                    if (!verbatim && source[i] == '\\') { kept.Append(source[i..(i + 2)]); i += 2; continue; }
                    kept.Append(source[i]);
                    if (source[i] == '"') { i++; break; }
                    i++;
                }
            }
            else { kept.Append(c); i++; }
        }
        return kept.ToString();
    }

    /// <summary>The negative that keeps <see cref="CodeOnly"/> from silently eating
    /// everything — a stripper that returned "" would make every `DoesNotContain` below pass
    /// forever, which is trap 34's shape hiding inside a helper.</summary>
    [Fact]
    public void TheCommentStripperKeepsCodeAndDropsProse()
    {
        const string source = """
            namespace X;
            /// <summary>It never calls Forbidden() and says so.</summary>
            internal class Y
            {
                // Forbidden() would be wrong here
                /* and Forbidden() again */
                void M() { var url = "https://example.com/x"; Allowed(); }
            }
            """;
        var code = CodeOnly(source);
        Assert.DoesNotContain("Forbidden()", code, StringComparison.Ordinal);
        Assert.Contains("Allowed()", code, StringComparison.Ordinal);
        // The `//` inside a URL inside a string is not a comment: a line filter would have
        // swallowed the rest of that line, and with it the call after it.
        Assert.Contains("https://example.com/x", code, StringComparison.Ordinal);
        // And it is applied to something real, not to a fixture alone.
        Assert.Contains("new SettingsHudView(", CodeOnly(Room), StringComparison.Ordinal);
    }

    // ---- the Core surface ------------------------------------------------------

    /// <summary>
    /// Four tabs, in the signed order, with the signed words. Bevel's I-11 §2 table is the
    /// ruling; this is it as an assertion, so an editor who renames a tab has to come here
    /// and say so.
    ///
    /// **"HUD" is the row this file exists to pin.** §4 of `docs/BEVEL-v2-staging-critique.md`
    /// bans "Cards &amp; windows (as a *finder*)" from shell copy and Bevel §3 named the
    /// replacement; SR-3 lifted that tab's whole body while leaving the naming to this PR, and
    /// wrote *"SR-5 still owes the word HUD"* into `FABLE.md` to make sure it was not
    /// forgotten. This is where the debt is paid.
    /// </summary>
    [Fact]
    public void TheRoomHasFourTabsInTheSignedOrderWithTheSignedWords()
    {
        Assert.Equal(
            [("Look", "look"), ("Alerts", "alerts"), ("HUD", "hud"), ("Behavior", "behavior")],
            SettingsSurface.Tabs().Select(t => (t.Label, t.Key)).ToArray());

        // Every key round-trips through the surface's own table — the property
        // `ShellPages.Rooms` mapping rather than translating rests on.
        foreach (var tab in SettingsSurface.Tabs())
            Assert.Equal(tab.Tab, SettingsSurface.TabForKey(tab.Key));
    }

    /// <summary>
    /// **The v1 window keeps "Cards &amp; windows" and the room says "HUD", and both halves
    /// are asserted.** The ban's own scope line exempts `OptionsWindow`, and renaming shipped
    /// copy for no player benefit is the #228 class — so the divergence is deliberate and this
    /// is what stops a well-meaning sweep "fixing" it in either direction.
    /// </summary>
    [Fact]
    public void TheV1TabKeepsItsNameAndTheRoomDoesNotBorrowIt()
    {
        Assert.Contains("Cards &amp; windows",
            Read("EQBuddy", "OptionsWindow.xaml"), StringComparison.Ordinal);
        Assert.DoesNotContain("Cards & windows", Room, StringComparison.Ordinal);
        Assert.Equal("HUD", SettingsSurface.LabelFor(SettingsTab.Hud));
    }

    /// <summary>The room opens on Look when an address names no tab — the v1 window's own
    /// fallback, and the least destructive arrival.</summary>
    [Fact]
    public void TheDefaultTabIsLook() =>
        Assert.Equal(SettingsTab.Look, SettingsSurface.DefaultTab);

    // ---- the composition, enumerated -------------------------------------------

    /// <summary>
    /// Every block the room composes, and the token that proves it COMPOSES rather than
    /// rebuilds. Each row asserts both halves: the room constructs the block type, and it
    /// does not declare a control of its own for that subject.
    /// </summary>
    public static TheoryData<string, string> Blocks() => new()
    {
        { "SettingsLookView", "_look" },
        { "SettingsAlertsView", "_alerts" },
        { "SettingsHudView", "_hud" },
        { "SettingsBehaviorView", "_behavior" },
    };

    [Theory]
    [MemberData(nameof(Blocks))]
    public void TheRoomBuildsItsOwnInstanceOfEveryBlock(string type, string field)
    {
        Assert.Contains($"new {type}(", Room, StringComparison.Ordinal);
        Assert.Contains(field, Room, StringComparison.Ordinal);
        // And the v1 window builds its own too — the trap 45 half. A WPF UIElement has
        // exactly one parent, so a block shared between the two hosts would be torn out of
        // whichever painted it last, silently, with nothing in a diff or a picture to say so.
        Assert.Contains($"new {type}(", Window, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Trap 13 as a constructor contract, asserted on the room as it already is on the
    /// blocks.** Both hosts wrap the ONE `AppSettings` instance the widget holds and the one
    /// persist delegate beside it. A room that called `AppSettings.Load` for itself would
    /// hold a second snapshot and write the whole file back from it on its next save — which
    /// is how "my tick-boxes won't stay ticked" (#169) presents, with nothing on screen to
    /// say so, and it is the one mistake that would make two open settings surfaces actively
    /// destructive rather than merely redundant.
    /// </summary>
    [Fact]
    public void TheRoomLoadsNoSettingsOfItsOwn()
    {
        Assert.DoesNotContain("AppSettings.Load", CodeOnly(Room), StringComparison.Ordinal);
        Assert.Contains("new OptionsViewModel(main.Settings, main.PersistSettings)",
            Room, StringComparison.Ordinal);
        // The same two arguments the v1 window passes, so the two view models are two views
        // of one object rather than two objects.
        Assert.Contains("new OptionsViewModel(main.Settings, main.PersistSettings)",
            Window, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The saved tab is the window's and the room does not touch it.** The v1 keys are
    /// `look/alerts/watch/cards/behavior` and the room's are `look/alerts/hud/behavior`, so a
    /// room that persisted "hud" would send the WINDOW home to Look on its next open — one
    /// host silently editing the other's landing, which is trap 13's shape without the file
    /// corruption. `DeadSettingTests` guards the opposite polarity (a setting read and never
    /// written); this guards a writer that must not appear.
    /// </summary>
    [Fact]
    public void TheRoomNeitherReadsNorWritesTheWindowsSavedTab()
    {
        // The two shapes that would do it, both absent. Asserted as CODE rather than as the
        // bare identifier because the room's own doc comment names the setting it is
        // deliberately not touching, and a scan that could not tell an explanation from a
        // write would have to be answered by deleting the explanation.
        Assert.DoesNotContain("_main.Settings.OptionsTab", Room, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.Settings.OptionsTab", Room, StringComparison.Ordinal);
        // The window still does, which is what makes the absence above a decision rather
        // than a capability nobody has.
        Assert.Contains("_main.Settings.OptionsTab = tab", Window, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The trap-25 strip obligation, discharged.** `SettingsAlertsView.Tabs()` carries a
    /// paragraph addressed to whoever renders it: the four chips carry COUNTS, so their
    /// widths depend on their content, and a horizontal `StackPanel` measures with infinite
    /// width in the stacking direction — the fourth chip is CLIPPED at the panel edge with no
    /// ellipsis and no error. That is exactly how the Progress window shipped three visible
    /// tabs out of four. This room is that host, and both of its strips wrap.
    /// </summary>
    [Fact]
    public void BothStripsWrapRatherThanStack()
    {
        Assert.DoesNotContain("StackPanel { Orientation = Orientation.Horizontal",
            Room, StringComparison.Ordinal);
        // Two WrapPanels, not one: the main strip and the Alerts family sub-strip.
        Assert.Equal(2, Room.Split("new WrapPanel").Length - 1);
    }

    /// <summary>
    /// **The hotkey ROUTE, on the second host** — SR-1's two lines, which it recorded as
    /// SR-5's to add. `BuildHotkeyRows` replaces the button that was clicked, so nothing
    /// inside the block's panel has focus when the gesture lands and the tunnelling route
    /// never reaches it: the press arrives at the WINDOW, which here is the shell. A host
    /// that composed the block and forgot the route gets a recorder that silently never
    /// records — which no screenshot and no build can see.
    ///
    /// **The negative is the half that matters**, and it is asserted on BOTH hosts: a host
    /// parsing gestures itself would be a second copy of the rule the block was created to
    /// hold, and the two copies would answer differently the first time either was edited.
    /// </summary>
    [Fact]
    public void BothHostsRouteAKeyPressToTheBlockThatOwnsTheDecision()
    {
        Assert.Contains("_behavior?.HandleRecordingKey(e)", Window, StringComparison.Ordinal);
        Assert.DoesNotContain("HotkeyManager.Parse(", CodeOnly(Window), StringComparison.Ordinal);

        Assert.Contains("_behavior.HandleRecordingKey(e)", Room, StringComparison.Ordinal);
        Assert.Contains("HandleRecordingKey(e)", Shell, StringComparison.Ordinal);
        Assert.DoesNotContain("HotkeyManager.Parse(", CodeOnly(Room), StringComparison.Ordinal);
        Assert.DoesNotContain("HotkeyManager.Parse(", CodeOnly(Shell), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The dump facts come from the blocks and are re-keyed mechanically, on both hosts**
    /// (trap 58). The dump is one flat namespace, so two live hosts of one block would
    /// otherwise write over each other and every assertion on those keys would quietly start
    /// reading the other window. A hand-written list on either side would also stop covering
    /// a block the day it gains a fifth fact (trap 30) — and, worse, would be a second
    /// producer of a number the first host already reports, which is the failure it is trying
    /// to avoid, one level up (trap 33).
    /// </summary>
    [Fact]
    public void BothHostsReportTheBlocksOwnFactsUnderTheirOwnPrefix()
    {
        Assert.Contains("ShellDumpFacts.Prefixed(\"shellSettings\", _look.DebugFacts())",
            Room, StringComparison.Ordinal);
        Assert.Contains("ShellDumpFacts.Prefixed(\"options\", _look?.DebugFacts()",
            Window, StringComparison.Ordinal);
        // All four blocks on both sides, so a fifth block cannot arrive reported by one host.
        Assert.Equal(4, Room.Split("ShellDumpFacts.Prefixed(\"shellSettings\"").Length - 1);
        Assert.Equal(4, Window.Split("ShellDumpFacts.Prefixed(\"options\"").Length - 1);
    }

    /// <summary>
    /// **The `IShellRoom` contract answered explicitly, which is the interface's own rule:
    /// an empty method with a reason is a decision, and a missing one is a question nobody
    /// asked.**
    ///
    /// `Release()` is empty because the four blocks hold no timer, no token and no watcher —
    /// checked rather than assumed, and the two things that looked like candidates are named
    /// in the room's own doc comment. `ApplyLayout()` is empty because all four tabs are one
    /// column, which is the only thing `ShellLayout.RoomSinglePane` decides.
    /// </summary>
    [Fact]
    public void TheEmptyContractMethodsCarryTheirReasons()
    {
        Assert.Contains("public void Release() { }", Room, StringComparison.Ordinal);
        Assert.Contains("public void ApplyLayout(ShellLayout layout) { }",
            Room, StringComparison.Ordinal);
        // The reasons, pinned by a phrase from each so the bodies cannot be emptied of their
        // explanation while staying empty of code.
        Assert.Contains("no timer, no token, no watcher", Room, StringComparison.Ordinal);
        Assert.Contains("ONE COLUMN", Room, StringComparison.Ordinal);
    }

    /// <summary>
    /// **No room-level empty, stated rather than omitted.** Every other room can be about
    /// nothing — no character, no session, no bags — and collapses to an explanation.
    /// Settings configures the tool rather than the character, so every control on it is
    /// meaningful on a profile that has never seen a log line; a whole-room empty here would
    /// hide the four tabs from precisely the player who has just installed EQBuddy.
    ///
    /// Asserted as an ABSENCE with the reason pinned beside it, because an absence is what
    /// trap 20 is about: the thing you are looking for is what is not there.
    /// </summary>
    [Fact]
    public void TheRoomUsesNoWholeRoomEmptyAndSaysWhy()
    {
        Assert.DoesNotContain("RoomEmptyState.Build", CodeOnly(Room), StringComparison.Ordinal);
        Assert.DoesNotContain("ShellRoomEmpty.", CodeOnly(Room), StringComparison.Ordinal);
        Assert.Contains("Settings is never empty", Room, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The one player-visible thing the window does and the room does not, named in the
    /// room rather than discovered later.** `MainWindow.OnOptions` puts the ★ alert banner
    /// into placement mode while Options is open and takes it out again on `Closed`; a room
    /// is navigated to and away from rather than opened and closed, so copying that would
    /// leave a draggable tile on the desktop for as long as the shell stayed open on any
    /// other room.
    ///
    /// This is the GearRoom loot-star / WorldRoom deaths-star pattern: a subtraction blocker
    /// written at the PR that adds the room, so the commit that retires `OptionsWindow`
    /// cannot take the affordance with it silently (trap 26 — "the data survived the move and
    /// the write path did not").
    /// </summary>
    [Fact]
    public void ThePlacementModeDivergenceIsNamedAndIsAretirementBlocker()
    {
        Assert.DoesNotContain("EnterPlacement", CodeOnly(Room), StringComparison.Ordinal);
        Assert.Contains("AlertTile.EnterPlacement()",
            Read("EQBuddy", "MainWindow.xaml.cs"), StringComparison.Ordinal);
        Assert.Contains("blocker on the commit that retires", Room, StringComparison.Ordinal);
    }

    /// <summary>
    /// **`OptionsWindow` is not retired, not renamed and still fully wired** — I-9's standing
    /// rule, and the out-list of the signed plan in one assertion. Landing a room is separate
    /// from, and earlier than, retiring the surface it replaces.
    /// </summary>
    [Fact]
    public void TheV1WindowIsUnretiredAndStillComposesAllFourBlocks()
    {
        Assert.Contains("public partial class OptionsWindow : Window", Window,
            StringComparison.Ordinal);
        Assert.Contains("_optionsWindow = new OptionsWindow(this)",
            Read("EQBuddy", "MainWindow.xaml.cs"), StringComparison.Ordinal);
        foreach (var type in new[]
                 {
                     "SettingsLookView", "SettingsAlertsView",
                     "SettingsHudView", "SettingsBehaviorView",
                 })
            Assert.Contains($"new {type}(", Window, StringComparison.Ordinal);
    }
}
