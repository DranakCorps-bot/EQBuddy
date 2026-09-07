using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The first-run Setup screen** (OE-6 — owner LOCK B through Helm on #355, Bevel's
/// pre-design #356, Fable's seat #358).
///
/// **It is a SECOND HOST of Home's readiness list, not a second checklist.** Every row it
/// draws comes from <see cref="ReadinessRows"/>, which is where Home's own block gets them
/// — so a fourth dump joins both surfaces at once, and the ⧉ command each row hands over
/// is resolved by one switch rather than two (Bevel's source-of-truth ruling; traps
/// 20/30/33). Nothing about <c>OutputfileKind</c> is enumerated in this file.
///
/// **A MODAL over the active room, not a room of its own** — and that is the signed shape
/// rather than a shortcut. The lock asked for Setup "in Evolved Settings / Setup room";
/// Bevel read that as *hosted from Settings*, not *a fifth tab of it*, because
/// <see cref="SettingsSurface"/>'s count is FOUR by a Helm-signed decision (I-11/#331) that
/// <c>SettingsRoomTests</c> pins. A navigable address was the plausible other reading and
/// is the one that would have put a permanent rail-reachable room in front of a screen
/// whose whole job is to stop being needed.
///
/// **What it is NOT:** it does not gate anything, it takes nothing away, and it has exactly
/// one way out (see <see cref="SetupReadout.ReopenNote"/> for why one and not two). A
/// player who closes it still has Home's Readiness block, which asks the same question
/// every time they open the shell, and Settings → Behavior, which opens this again.
///
/// Scrolling belongs to the HOST (trap 36) — <c>ShellWindow.SetupLayer</c> wraps this body
/// in the scroller, so nothing here brings one and nothing here carries a
/// <c>Visibility</c> or an outer <c>Margin</c> (trap 15).
/// </summary>
internal sealed class SetupView
{
    private readonly MainWindow _main;
    private readonly Action<string> _navigate;
    private readonly Action _dismiss;

    /// <summary>The body, built once and REFILLED rather than replaced — the host holds
    /// this object in its content cell, so handing out a new panel on a refresh would leave
    /// the old one on screen (trap 45's shape: a long-lived UI object is not a value).
    /// </summary>
    private readonly StackPanel _body = new();

    private IReadOnlyList<ReadinessRow> _rows = [];
    private int _copyCommands;

    public UIElement Body => _body;

    public SetupView(MainWindow main, Action<string> navigate, Action dismiss)
    {
        _main = main;
        _navigate = navigate;
        _dismiss = dismiss;
        Build();
    }

    /// <summary>A dump landed while this was open. Re-read and redraw: a row still reading
    /// "Not run yet" seconds after the game wrote the file is the "EQBuddy did nothing"
    /// reading the auto-import exists to prevent, and it is worst HERE — this is the screen
    /// that just told the player to run the command, so it is the screen they are looking
    /// at when it works. Same reason <c>HomeRoom.Refreshed</c> exists.</summary>
    public void Refreshed() => Build();

    private void Build()
    {
        _rows = ReadinessRows.Read(_main);
        _copyCommands = 0;
        _body.Children.Clear();

        // The same column cap and left pin Home's blocks take, and for the same measured
        // reason: at a wide window an answer sitting 600 units right of the row it belongs
        // to stops reading as one row. LEFT, not stretched — WPF centres a MaxWidth child in
        // the slack it did not use, and a column drifting toward the middle as the window
        // widens is the same defect with better manners.
        _body.MaxWidth = ShellLayoutPolicy.MinRoomWidth;
        _body.HorizontalAlignment = HorizontalAlignment.Left;

        var head = DesignSystem.Text(Role.TitleWindow, SetupReadout.Headline);
        head.Ink("AccentBrush");
        _body.Children.Add(head);
        _body.Children.Add(Line(SetupReadout.Lead, Role.Body, Tok.SpaceS));

        _body.Children.Add(CardParts.BlockLabel(SetupReadout.RowsHeadline, hidden: false));
        foreach (var row in _rows)
        {
            var (view, copies) = ReadinessRows.Row(row, _navigate);
            _copyCommands += copies;
            _body.Children.Add(view);
        }

        var done = Theming.Button(SetupReadout.Done);
        done.HorizontalAlignment = HorizontalAlignment.Left;
        done.Margin = new Thickness(0, Tok.SpaceL, 0, 0);
        done.Click += (_, _) => _dismiss();
        _body.Children.Add(done);

        // UNDER the button and not in a tooltip: what the one close does is the thing a
        // player wants to know BEFORE they press it, and a tooltip is read by nobody who is
        // already reaching for the mouse.
        _body.Children.Add(Line(SetupReadout.ReopenNote, Role.Metadata, Tok.SpaceXs));
    }

    private static TextBlock Line(string text, Role role, double top)
    {
        var block = DesignSystem.Text(role, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(role == Role.Body ? "TextBrush" : "DimBrush");
        block.Margin = new Thickness(0, top, 0, 0);
        return block;
    }

    /// <summary>
    /// This screen's facts, in its own vocabulary — the host adds the prefix (trap 58).
    ///
    /// <c>setupCopyCmd</c> is the row with teeth, for the same reason
    /// <c>shellHomeCopyCmd</c> is: a source scan can prove this surface NAMES the three
    /// commands and only a launched app can say the buttons exist, because an absent
    /// control photographs as an unremarkable panel (trap 29). <c>setupRows</c> beside it is
    /// the floor that stops the count going vacuous — a screen that drew nothing at all
    /// would report zero buttons and agree with a "no missing affordance" reading perfectly
    /// (trap 39).
    /// </summary>
    public string DebugFacts() =>
        $"setupRows={_rows.Count} setupCopyCmd={_copyCommands}";
}
