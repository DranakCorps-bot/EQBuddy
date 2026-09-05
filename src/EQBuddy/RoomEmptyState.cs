using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **THE ROOM-LEVEL EMPTY WRAPPER** — where a room's whole-room empty explanation sits,
/// as opposed to what it says.
///
/// Bevel's E-3 rooms pre-design (Helm-signed 2026-09-04 ~11:15 PM CT) split the empty-state
/// question in two and ruled on both halves separately: **the ROOM decides POSITION, and
/// each surface decides its own canvas treatment.** This is the first half, and until now
/// it was a ruling with no code. Three rooms have shipped since it was signed and none of
/// them built it — not through neglect, but because World and Gear both carry populated
/// fixture data in every committed shot and have therefore never been SEEN empty. Home is
/// the first room whose default, most-likely-seen state is a room-level empty (Bevel's Home
/// pre-design §2), which makes it the first thing that could consume the rule, the same way
/// <c>RoomSinglePane</c> sat unconsumed for two PRs until Quests arrived.
///
/// **Why centring is the room's business and not the surface's.** A room gets the whole
/// content cell — 520 units at the floor, most of a monitor at the top end — and an
/// explanation pinned to its top-left in a cell that size does not read as an explanation,
/// it reads as a page that failed to load. Every room's cell is the same shape, so the
/// answer is the same for all of them, which is exactly the kind of decision that must not
/// be re-made per room: four rooms centring four slightly different ways is the drift this
/// codebase keeps paying for.
///
/// **The width cap is the other half of the position answer.** A sentence measured against
/// a 1,400-unit room is a sentence nobody reads to the end of;
/// <see cref="DesignTokens.TipWidth"/> is the measure this codebase already trusts for a
/// paragraph the player is meant to act on, so it is reused rather than re-picked.
///
/// **What this deliberately does NOT do: decide the words, or hide anything.** The words
/// live in <c>UI.Shared</c> where a unit test can read them (<see cref="HomeReadout"/> is
/// the first set), and a room with nothing in it still SHOWS — "silent no-ops are broken",
/// and a room that drew nothing at all would be the same defect as a blank card.
///
/// **ALL SIX ROOMS CONSUME IT SINCE E-3 S1**, which is what the paragraphs above were
/// waiting for: Home and Live built it for themselves, and Progress, Gear, World and
/// Quests reach it through <see cref="ShellRoomEmpty"/>.
///
/// **THE CENTRING ITSELF WAS MEASURED AT THAT PASS RATHER THAN REASONED ABOUT, and two
/// confident hypotheses about it were both WRONG.** They are written down because each one
/// is a plausible-looking change somebody will propose again:
///
///  * *"A bare <c>ContentControl</c> aligns its content top-left, so <c>RoomHost</c> needs
///    <c>Stretch</c>."* Its <c>HorizontalContentAlignment</c>/<c>VerticalContentAlignment</c>
///    defaults ARE <c>Left</c>/<c>Top</c> — and setting them to <c>Stretch</c> changes
///    nothing, because the default template's <c>ContentPresenter</c> does not alias them.
///    The room already gets the whole cell.
///  * *"A room must not put this inside its own <c>ScrollViewer</c>, because a scroller
///    measures its content with infinite height."* It measures with infinity and then
///    ARRANGES content smaller than the viewport at the VIEWPORT's size, so the centring has
///    real slack. Home keeps its empty inside its scroller deliberately, so a window too
///    short to hold the explanation can still scroll to it.
///
/// So the one thing the centring depends on is that the room is given the whole content
/// cell, and <c>shellRoomFills</c> is the assertion for exactly that — against the CELL, not
/// against the host, which is the form that can actually fail (see
/// <c>ShellWindow.RoomFills</c>). A room-level empty is still a SIBLING of a room's page
/// wherever the room has a tab strip, but that is about taking the strip away with it, not
/// about layout.
/// </summary>
internal static class RoomEmptyState
{
    /// <summary>The same thing, handed the heading and the explanation as the PAIR they
    /// are. <see cref="RoomEmptyMessage"/> is what the four data rooms carry, so a room
    /// cannot ship one half of its own empty state — and the pair is one value, so a
    /// caller cannot swap the two strings and get a plausible-looking panel.</summary>
    public static FrameworkElement Build(RoomEmptyMessage message, FrameworkElement? action = null) =>
        Build(message.Heading, message.Explanation, action);

    /// <summary>
    /// A room's whole-room empty: a heading, an explanation, and optionally something to
    /// do about it — centred in whatever cell the shell gives the room.
    ///
    /// <paramref name="action"/> is the "and here is how to change that" half. An empty
    /// state without one is a statement; with one it is an answer, which is what the
    /// inventory-dump voice asks for and what separates this from a blank panel.
    /// </summary>
    public static FrameworkElement Build(string heading, string explanation,
        FrameworkElement? action = null)
    {
        var stack = new StackPanel
        {
            MaxWidth = Tok.TipWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Tok.SpaceXl),
        };

        var title = DesignSystem.Text(Role.TitleSection, heading);
        title.TextWrapping = TextWrapping.Wrap;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextAlignment = TextAlignment.Center;
        stack.Children.Add(title);

        var body = DesignSystem.Text(Role.Body, explanation);
        body.TextWrapping = TextWrapping.Wrap;
        body.TextAlignment = TextAlignment.Center;
        body.Margin = new Thickness(0, Tok.SpaceM, 0, 0);
        body.Ink("DimBrush");
        stack.Children.Add(body);

        if (action is not null)
        {
            action.HorizontalAlignment = HorizontalAlignment.Center;
            action.Margin = new Thickness(0, Tok.SpaceL, 0, 0);
            stack.Children.Add(action);
        }

        // A Grid rather than the stack alone: VerticalAlignment.Center inside a StackPanel
        // parent does nothing (a stack measures its children with infinite height in the
        // stacking direction, so there is no slack to centre within — trap 14's arithmetic
        // on the other axis). The room hands this straight into its own `*` cell.
        var host = new Grid();
        host.Children.Add(stack);
        return host;
    }
}
