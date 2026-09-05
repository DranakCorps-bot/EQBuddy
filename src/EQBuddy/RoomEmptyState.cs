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
/// </summary>
internal static class RoomEmptyState
{
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
