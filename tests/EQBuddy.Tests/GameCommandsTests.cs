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
        // A TAB of Gear & Loot since 2026-08-20, not a window. This row is also the list
        // earning itself: the file rename broke the assertion the same day, which is
        // exactly the notice a curated list is for.
        ("EQBuddy/GearLockerView.cs", nameof(GameCommands.OutputfileInventory),
            "the locker IS the dump, rendered by slot"),
        ("EQBuddy/InventoryWindow.cs", nameof(GameCommands.OutputfileInventory),
            "the same dump, raw"),
        ("EQBuddy/QuestsWindow.xaml.cs", nameof(GameCommands.OutputfileInventory),
            "the held and ready views answer what-can-I-turn-in from bags and bank"),
        ("EQBuddy/RaidsCardView.cs", nameof(GameCommands.OutputfileAchievements),
            "clears from before EQBuddy come from the achievements dump — the worked example"),
        ("EQBuddy/QuestChecklistView.cs", nameof(GameCommands.OutputfileAchievements),
            "owns the achievements import, and the menu copy beside it"),
        ("EQBuddy/MapWindow.cs", nameof(GameCommands.LocSocial),
            "the /loc social is the map's whole trick"),

        // ---- Avalonia: the same surfaces, per CLAUDE.md's both-UIs-in-one-change rule.
        // A gap here is how #122 and #152 reached Linux after Windows had already paid.
        ("EQBuddy.Avalonia/MainWindow.cs", nameof(GameCommands.OutputfileInventory),
            "hosts the gear checklist inline — GearCardView's twin until it is lifted out"),
        ("EQBuddy.Avalonia/MainWindow.cs", nameof(GameCommands.OutputfileAchievements),
            "hosts the Raids section inline"),
        ("EQBuddy.Avalonia/GearLockerWindow.cs", nameof(GameCommands.OutputfileInventory), "WPF twin"),
        ("EQBuddy.Avalonia/InventoryWindow.cs", nameof(GameCommands.OutputfileInventory), "WPF twin"),
        ("EQBuddy.Avalonia/QuestsWindow.cs", nameof(GameCommands.OutputfileInventory), "WPF twin"),
        ("EQBuddy.Avalonia/MapWindow.cs", nameof(GameCommands.LocSocial), "WPF twin"),
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
    /// seen: <c>gearCopyCmd</c> in E2E for WPF, <c>WidgetRenderTests</c> for Avalonia.</summary>
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
