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
    /// One readiness row: what the dump feeds, when it last landed, and — only when it
    /// never has — the ⧉ copy of the command that produces it.
    ///
    /// **The copy button is the row's whole point in its empty state.** A surface that asks
    /// the player for an output file and hands them no way to run it is the defect David
    /// reported on 2026-08-20, and it is worse in the empty state, which is the only state a
    /// new player sees — and the only state the first-run screen is ever drawn in.
    /// </summary>
    /// <param name="navigate">Where an already-landed row's "Open" goes. Home hands in the
    /// shell's own <c>Navigate</c>; Setup hands in one that closes itself first. Never a
    /// second dispatch of its own (trap 33 lifted into navigation).</param>
    /// <returns>The row, and whether it carried a ⧉ — counted off the BUILT control rather
    /// than re-derived from the state, so the number in the dump is a fact about the visual
    /// tree and not a restatement of the condition above it.</returns>
    public static (FrameworkElement View, int CopyCommands) Row(
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

        if (row.State == ReadinessState.NeverScanned)
        {
            var copy = Theming.WireCopyCommand(Theming.Button(""), CommandFor(row.Kind));
            copy.FontSize = Tok.Spec(Role.Caption).Size;
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            copy.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
            copy.ToolTip = "Copies the command — paste it into the game's chat. The game "
                + "writes the file beside its own folders and EQBuddy reads it by itself.";
            stack.Children.Add(copy);
            return (stack, 1);
        }

        if (row.Address.Length > 0)
        {
            // A row whose dump HAS landed is a way into the surface that uses it — through
            // the same Navigate the rail calls, never a second dispatch. Filtered by Landed
            // in HomeReadout, so this cannot offer a room that does not exist.
            var link = DesignSystem.Text(Role.Caption, "Open");
            link.Ink("AccentBrush");
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
            var address = row.Address;
            DesignSystem.WireClick(link, () => navigate(address));
            stack.Children.Add(link);
        }

        return (stack, 0);
    }

    /// <summary>
    /// The command a dump needs, named as the constant rather than as a literal — the whole
    /// point of centralising them, and what
    /// <c>GameCommandsTests.NoCopySurfaceCarriesItsOwnCommandLiteral</c> forbids the other
    /// way round.
    ///
    /// The switch is HERE rather than on <c>GameCommands</c> on purpose: the must-list scan
    /// asserts that a surface which needs a command NAMES it, and a helper in UI.Shared
    /// would satisfy the compiler while making these three rows unverifiable.
    /// </summary>
    public static string CommandFor(OutputfileKind kind) => kind switch
    {
        OutputfileKind.Achievements => GameCommands.OutputfileAchievements,
        OutputfileKind.Factions => GameCommands.OutputfileFaction,
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
