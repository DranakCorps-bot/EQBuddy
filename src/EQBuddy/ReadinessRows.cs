using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The readiness rows, and the one control that draws one** — read by
/// <see cref="HomeRoom"/>'s Readiness block and by the first-run <see cref="SetupView"/>.
///
/// It came out of <c>HomeRoom</c> in OE-6, when Setup became a SECOND host of the same
/// list. Bevel's ruling is explicit that Setup renders *"the same <c>ReadinessRow</c> list
/// with the same <c>ReadinessRowView</c>-shaped copy/open treatment ... reusing
/// <c>CommandFor(OutputfileKind)</c> rather than a second switch"*, and the alternative was
/// never a style question: a hand-rolled second copy of "Inventory / Achievements /
/// Factions" is trap 33's shape with the two producers being two HOSTS, and it stops
/// agreeing the day <see cref="HomeReadout.Readiness"/> gains a fourth row — which is a
/// change already on the board (OE-5 PR-1's spellbook row). Adding it there gives it to
/// both surfaces and to neither's author.
///
/// **The <c>/outputfile</c> command switch lives HERE, and so does its must-list row.**
/// <c>GameCommandsTests.SurfacesNeedingACommand</c> asserts that a surface which asks the
/// player for an output file NAMES the command off <see cref="GameCommands"/> — the rule
/// David reported the absence of on 2026-08-20 — and the surface that does the asking is
/// now this file, for both hosts. The rows followed it here the same way they followed
/// <c>MapView</c> in World PR 1 and <c>QuestsView</c> in E-3 PR 3. What a source scan can
/// never prove is that the CONTROL is on screen (trap 29: an absent control photographs as
/// an unremarkable panel), so <c>shellHomeCopyCmd</c> and <c>shellSetupCopyCmd</c> count
/// them from a launched app — two hosts, two counts, one builder.
/// </summary>
internal static class ReadinessRows
{
    /// <summary>
    /// The rows themselves, for whichever host is asking.
    ///
    /// **One read, two hosts** — the identity pair and the dump timestamps are the inputs,
    /// and a second host that assembled them itself could disagree with the first about
    /// which character it was even looking at. <see cref="ShellRoomIdentity.Of"/> is the
    /// destructure that has to stay a destructure (see its note in <see cref="HomeRoom"/>):
    /// the two identity pairs in this codebase are spelled in opposite orders and a tuple
    /// conversion is positional.
    ///
    /// It is NOT throttled here. Home caches it behind its own five-second clock because it
    /// re-reads on every paint; Setup asks once when it opens and again when a dump lands.
    /// Putting a throttle in here would hand Setup a cache it has no use for and hide
    /// Home's own cost decision from the file that makes it.
    /// </summary>
    public static IReadOnlyList<ReadinessRow> Read(MainWindow main)
    {
        var identity = ShellRoomIdentity.Of(main);
        var logFolder = main.Settings.LogFolder;
        return HomeReadout.Readiness(identity,
            kind => OutputfileAutoImport.WrittenAt(logFolder, identity.Character, kind));
    }

    /// <summary>
    /// One readiness row: what the dump feeds, when it last landed, the ⧉ copy of the
    /// command that produces it, and — once it has landed — the way into the surface that
    /// uses it.
    ///
    /// **The ⧉ is on EVERY row in BOTH states as of DRA-63** (Founder smoke 2026-09-11:
    /// *"always show copy/paste catch-up for Bags, Achievements, Factions, Spellbooks from
    /// Home"*). It used to be drawn only for a never-scanned row, which made it an
    /// onboarding prompt that switched itself off the moment it worked — and left the player
    /// who wanted to re-run the command hunting for it, in the state they are in every time
    /// after the first. A surface that asks the player for an output file and hands them no
    /// way to run it is the defect David reported on 2026-08-20; a surface that hands it
    /// over once and then takes it back is the same defect with a delay on it. The WORDS
    /// still differ by state — <see cref="HomeReadout.CatchUpTooltip"/> — because asking and
    /// offering are different jobs.
    /// </summary>
    /// <param name="navigate">Where an already-landed row's "Open" goes. Home hands in the
    /// shell's own <c>Navigate</c>; Setup hands in one that closes itself first. Never a
    /// second dispatch of its own (trap 33 lifted into navigation).</param>
    /// <returns>The row, how many ⧉ copies it carried, and how many "Open" links — all
    /// counted off the BUILT controls rather than re-derived from the state, so the numbers
    /// in the dump are facts about the visual tree and not a restatement of the conditions
    /// above them (trap 29: an absent control photographs as an unremarkable panel).</returns>
    public static (FrameworkElement View, int CopyCommands, int Opens) Row(
        ReadinessRow row, Action<string> navigate)
    {
        var stack = new StackPanel { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };

        // A GRID and never a horizontal StackPanel: a stack measures its children with
        // infinite width, so the answer would be pushed off the edge with no ellipsis and
        // the row would simply be cut (trap 14, and trap 25 with chips).
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = DesignSystem.Text(Role.Body, row.Name);
        name.TextWrapping = TextWrapping.Wrap;
        head.Children.Add(name);

        var answer = DesignSystem.Text(Role.Caption, HomeReadout.ReadinessAnswer(row));
        answer.Ink(row.State == ReadinessState.NeverScanned ? "AccentBrush" : "DimBrush");
        answer.Margin = new Thickness(Tok.SpaceM, 0, 0, 0);
        Grid.SetColumn(answer, 1);
        head.Children.Add(answer);
        stack.Children.Add(head);

        stack.Children.Add(Line(row.Feeds));

        // UNCONDITIONAL since DRA-63. There is no state in which a row shows the command's
        // name and no way to take it: see the summary above, and HomeReadout.CatchUpTooltip
        // for why the sentence on it is not the same sentence in both states.
        var copy = Theming.WireCopyCommand(Theming.Button(""), CommandFor(row.Kind));
        copy.FontSize = Tok.Spec(Role.Caption).Size;
        copy.HorizontalAlignment = HorizontalAlignment.Left;
        copy.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        copy.ToolTip = HomeReadout.CatchUpTooltip(row);
        stack.Children.Add(copy);

        var opens = 0;
        if (row.State == ReadinessState.Scanned && row.Address.Length > 0)
        {
            // A row whose dump HAS landed is a way into the surface that uses it — through
            // the same Navigate the rail calls, never a second dispatch. Filtered by Landed
            // in HomeReadout, so this cannot offer a room that does not exist.
            //
            // **Since DRA-63 this is the only navigation in Home's body**, the "Go to" block
            // having gone with the rail that already draws those doors. `shellHomeDeadLinks`
            // asks its question of these rows now.
            var link = DesignSystem.Text(Role.Caption, "Open");
            link.Ink("AccentBrush");
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
            var address = row.Address;
            DesignSystem.WireClick(link, () => navigate(address));
            stack.Children.Add(link);
            opens = 1;
        }

        return (stack, 1, opens);
    }

    /// <summary>
    /// The command a dump needs, named as the constant rather than as a literal — the whole
    /// point of centralising them, and what
    /// <c>GameCommandsTests.NoCopySurfaceCarriesItsOwnCommandLiteral</c> forbids the other
    /// way round.
    ///
    /// The switch is HERE rather than on <c>GameCommands</c> on purpose: the must-list scan
    /// asserts that a surface which needs a command NAMES it, and a helper in UI.Shared
    /// would satisfy the compiler while making these readiness rows unverifiable.
    /// </summary>
    public static string CommandFor(OutputfileKind kind) => kind switch
    {
        OutputfileKind.Achievements => GameCommands.OutputfileAchievements,
        OutputfileKind.Factions => GameCommands.OutputfileFaction,
        OutputfileKind.Spellbook => GameCommands.OutputfileSpellbook,
        _ => GameCommands.OutputfileInventory,
    };

    private static TextBlock Line(string text)
    {
        var block = DesignSystem.Text(Role.Metadata, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink("DimBrush");
        return block;
    }
}
