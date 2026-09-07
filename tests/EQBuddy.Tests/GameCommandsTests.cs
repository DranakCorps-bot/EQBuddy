using System.Text.RegularExpressions;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>David, 2026-08-14: every surface that names an in-game command offers
/// a one-click ⧉ copy of the EXACT text. The commands live once in GameCommands;
/// these pin the text the game expects and forbid any copy source from carrying
/// its own literal — a future command change can't drift between surfaces.</summary>
public class GameCommandsTests
{
    [Fact]
    public void CommandsAreExactlyWhatTheGameExpects()
    {
        Assert.Equal("/outputfile inventory", GameCommands.OutputfileInventory);
        Assert.Equal("/outputfile achievements", GameCommands.OutputfileAchievements);
        Assert.Equal("/loc", GameCommands.LocSocialLine1);
        Assert.Equal("/doability 1", GameCommands.LocSocialLine2);
    }

    [Fact]
    public void LocSocialIsTwoLinesOnePerSocialSlot()
    {
        var lines = GameCommands.LocSocial.Split(Environment.NewLine);
        Assert.Equal([GameCommands.LocSocialLine1, GameCommands.LocSocialLine2], lines);
        // Each pastes into one social-editor slot: single line, slash-led.
        Assert.All(lines, l => Assert.StartsWith("/", l));
        Assert.All(lines, l => Assert.DoesNotContain('\n', l));
    }

    /// <summary>No clipboard call anywhere in src may copy a slash-command literal —
    /// copy sources must reference GameCommands, the whole point of centralizing.</summary>
    [Fact]
    public void NoCopySurfaceCarriesItsOwnCommandLiteral()
    {
        var offenders = Directory.EnumerateFiles(SrcRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), """SetText(?:Async)?\(\s*@?"/"""))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>
    /// Every surface that NEEDS the player to run an in-game command, and which command.
    /// Curated by hand with a reason per entry, exactly like <c>DeadSettingTests.Known</c>:
    /// a list is code no compiler can check, so the list and the assertion below are
    /// written and reviewed together.
    ///
    /// It exists because the negative test above could not catch the bug David reported on
    /// 2026-08-20 — the Gear tab told him to import something and gave him no way to do it.
    /// Forbidding a copy source from carrying its own literal says nothing about a surface
    /// carrying NO copy source at all, which is exactly the hole the gear checklist fell
    /// through, on both widgets, for as long as the surface has existed.
    ///
    /// Adding a row is the deliberate act. A new surface that asks for an output file
    /// belongs here; a listed one that stops needing the command loses its row, and the
    /// commit says why.
    /// </summary>
    public static readonly (string File, string Command, string Why)[] SurfacesNeedingACommand =
    [
        // ---- WPF ----
        ("EQBuddy/GearCardView.cs", nameof(GameCommands.OutputfileInventory),
            "the checklist auto-ticks from the inventory dump — David 2026-08-20, the one that was missing"),
        // ONE row where there were two: the Gear Locker and the Inventory window read
        // the same dump and merged into one tab with two pivots (David, 2026-08-20).
        // This list has now caught that surface moving twice in a day, which is exactly
        // the notice a curated list exists to give.
        ("EQBuddy/InventoryView.cs", nameof(GameCommands.OutputfileInventory),
            "the tab IS the dump — ranked by slot, or listed by bag"),
        // All three Quests rows moved from EQBuddy/QuestsWindow.xaml.cs to QuestsView.xaml.cs
        // in E-3 PR 3 (the content lift into the Evolved shell) — the same move MapView's
        // row made in World PR 1, and this list following the surface is exactly the notice
        // it exists to give. The commands did not change; their host did, and there are two
        // of them now.
        ("EQBuddy/QuestsView.xaml.cs", nameof(GameCommands.OutputfileInventory),
            "the held and ready views answer what-can-I-turn-in from bags and bank"),
        // The Sky tab is FED by the achievements dump — a hand-in never appears in the
        // log, so that dump is the only thing that can say a reward was turned in before
        // EQBuddy existed — and it named no way to produce one. The command lived on the
        // widget menu and the Raids card, neither of which is where someone looking at
        // Sky rewards is looking. Same absence as the Gear tab in 2026-08-20, found the
        // same way: by asking what a surface needs rather than what it must not carry.
        ("EQBuddy/QuestsView.xaml.cs", nameof(GameCommands.OutputfileAchievements),
            "the Sky tab's turn-in state comes from the achievements dump"),
        // The Unlocks tab is built from TWO dumps and neither is a one-off — a race unlock
        // moves every time you grind faction — so both buttons are on the populated
        // surface, not only in an empty state (#217's rule, and the reason the Gear tab's
        // copy is not empty-state-only either).
        ("EQBuddy/QuestsView.xaml.cs", nameof(GameCommands.OutputfileFaction),
            "the Unlocks tab's race progress is faction standings, which the log never sees"),
        // E-3 PR 4's Home room. Its READINESS block exists to answer "what has EQBuddy not
        // been told yet", and every row of it that says "Not run yet" is a surface naming a
        // command — four of them, which is why there are four rows here rather than one.
        // This is the room a brand-new player's shell OPENS on, so it is the surface where
        // an ask with no ⧉ beside it costs the most: the empty state is the only state a new
        // player sees, and it is the whole state of this block on a fresh profile.
        //
        // **All three moved from EQBuddy/HomeRoom.cs to ReadinessRows.cs in OE-6**, when the
        // first-run Setup screen became a SECOND host of the same rows — the same move
        // MapView's row made in World PR 1 and QuestsView's three made in E-3 PR 3, and this
        // list following the surface is exactly the notice it exists to give. The surface
        // that ASKS is the shared row builder now: Bevel's signed ruling has Setup reuse
        // `CommandFor(OutputfileKind)` rather than grow a second switch (trap 33 with the
        // two producers being two hosts), so a copy of these rows keyed on `SetupView.cs`
        // would be asking a file that correctly names no command to name all of them.
        //
        // **What the move costs, said out loud: the count of HOSTS is no longer in this
        // list.** It never was the thing this rule guards — trap 34 is about a surface with
        // NO copy source at all, and a host that renders these rows structurally cannot have
        // one. The per-host claim is where controls can be seen: `shellHomeCopyCmd` and
        // `shellSetupCopyCmd` in `tests/EQBuddy.E2E`, counted off two launched surfaces.
        ("EQBuddy/ReadinessRows.cs", nameof(GameCommands.OutputfileInventory),
            "the Bags readiness row asks for the dump the wishlist and quest turn-ins read"),
        ("EQBuddy/ReadinessRows.cs", nameof(GameCommands.OutputfileAchievements),
            "the Achievements readiness row asks for what Sky turn-ins and raid clears read"),
        ("EQBuddy/ReadinessRows.cs", nameof(GameCommands.OutputfileFaction),
            "the Factions readiness row asks for standings the log can never see"),
        // FOUR rows now, since OE-5 added the optional spellbook dump — and the fourth one
        // is keyed on `ReadinessRows.cs` like the three above it rather than on the room,
        // which is the whole promise OE-6's shared builder was written to keep: a fourth
        // dump joins both hosts in one place and neither host's file learns a command.
        // (It was written against `HomeRoom.cs` while OE-5 was based pre-OE-6, and rebasing
        // over the lift is exactly the move this list exists to notice.)
        //
        // Its row is the one most in need of a ⧉: the other three name a command a player
        // may have met on another surface, and this one appears nowhere else in the app at
        // all — an ask with no way to answer it would be the whole of what a player could
        // do about it.
        ("EQBuddy/ReadinessRows.cs", nameof(GameCommands.OutputfileSpellbook),
            "the Spellbook readiness row asks for the optional dump that sharpens buff countdowns"),
        ("EQBuddy/RaidsCardView.cs", nameof(GameCommands.OutputfileAchievements),
            "clears from before EQBuddy come from the achievements dump — the worked example"),
        ("EQBuddy/QuestChecklistView.cs", nameof(GameCommands.OutputfileAchievements),
            "owns the achievements import, and the menu copy beside it"),
        // Moved from EQBuddy/MapWindow.cs to MapView.cs in World PR 1 (the content lift) —
        // this row following the surface is exactly the notice this list exists to give.
        ("EQBuddy/MapView.cs", nameof(GameCommands.LocSocial),
            "the /loc social is the map's whole trick"),

        // ---- The seven Avalonia rows that used to sit here went with the platform in E-2
        // (2026-09-04). They were the same surfaces, per the both-UIs-in-one-change rule,
        // and the list following them across two folds is the notice this list exists to
        // give — including on the way out.
        //
        // **What must not be inferred from their removal: the RULE is unchanged.** It was
        // never about parity between lanes. A surface that names an /outputfile command
        // and hands the player no way to run it is the same defect as a silent no-op, and
        // it is worse in the empty state, which is the only state a new player sees
        // (trap 34: the ban this list sits beside cannot see a surface with no copy source
        // at all). Every row above still earns its place on its own, and E-3's shell adds
        // rows here as it takes these surfaces over.
    ];

    public static TheoryData<string, string, string> SurfaceRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var (file, command, why) in SurfacesNeedingACommand) rows.Add(file, command, why);
        return rows;
    }

    /// <summary>The positive half of the rule: a listed surface must name the exact command
    /// it needs, off <see cref="GameCommands"/>. Naming the constant is what makes the
    /// affordance possible — that the CONTROL is on screen is pinned where controls can be
    /// seen: <c>gearCopyCmd</c> in E2E, against the real exe. (Its Avalonia twin was
    /// <c>WidgetRenderTests</c>, deleted with that lane in E-2c.)</summary>
    [Theory]
    [MemberData(nameof(SurfaceRows))]
    public void EverySurfaceThatNeedsACommandHandsItOver(string file, string command, string why)
    {
        var path = Path.Combine(SrcRoot(), file.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"{file} is on the list and does not exist — {why}");
        Assert.True(
            File.ReadAllText(path).Contains("GameCommands." + command, StringComparison.Ordinal),
            $"{file} needs {command} ({why}) and never names it. A surface that asks the "
            + "player for an output file has to hand the command over — David, 2026-08-20: "
            + "it was \"telling me to import it but not telling me how or giving me the tool "
            + "with which to do it\". Copy RaidsCardView.CopyAchievementsCmd; do not invent a "
            + "second shape. If the surface genuinely no longer needs the command, delete its "
            + "row from GameCommandsTests.SurfacesNeedingACommand and say why.");
    }

    /// <summary>The PHONE's version of the rule, and why it is a different affordance
    /// (David, 2026-08-20, answering the question directly): a phone's clipboard cannot
    /// paste into the game running on the PC, so EQBuddy Mobile shows the command as
    /// selectable text rather than a ⧉ button that would do nothing useful. The text still
    /// comes off GameCommands and still travels over the wire — index.html spelling a
    /// command itself is the drift the constant exists to prevent, and trap 32 means such a
    /// literal can sit on an open phone for weeks after the PC has moved on.</summary>
    [Fact]
    public void ThePhoneShowsTheCommandAndNeverSpellsItItself()
    {
        Assert.All(new[] { CommandPrompts.GearInventory, CommandPrompts.RaidsAchievements }, p =>
        {
            Assert.Contains(p.Command, new[]
            {
                GameCommands.OutputfileInventory, GameCommands.OutputfileAchievements,
            });
            Assert.NotEmpty(p.Note);
            // The lead has to say WHERE: the player is holding the one device in the room
            // that cannot run it, and "type this" without "on your PC" is the same defect
            // one level down.
            Assert.Contains("PC", p.Lead, StringComparison.OrdinalIgnoreCase);
        });

        var html = File.ReadAllText(Path.Combine(SrcRoot(), "EQBuddy.Companion", "Web", "index.html"));
        Assert.DoesNotContain("/outputfile", html, StringComparison.OrdinalIgnoreCase);
        // And it has to draw what it is sent, or the wire field is decoration.
        Assert.Contains("cmdPrompt", html, StringComparison.Ordinal);
    }

    private static string SrcRoot() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");
}
