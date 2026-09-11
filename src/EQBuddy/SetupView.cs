using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
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
    private bool _importReported;

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

        AddImportReport();

        _body.Children.Add(CardParts.BlockLabel(SetupReadout.RowsHeadline, hidden: false));
        foreach (var row in _rows)
        {
            // The third element is the row's "Open" link, which Home counts for its
            // dead-affordance guard. Setup does not: this screen is drawn over a profile
            // that has no dumps, so the link is not what it is about, and a count nothing
            // asserts is a fact with no reader (trap 20's shape, pointed at a dump key).
            var (view, copies, _) = ReadinessRows.Row(row, _navigate);
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

    /// <summary>
    /// **What the v1 profile import brought over, and the start-fresh alternative beside
    /// it** — Fable's transition plan §2 (TR-1). Setup is where the plan puts it, and the
    /// reason is trap 43: the copy happens at startup, before <c>AppSettings.Load</c> and
    /// before any window exists, so an import that reported nowhere would be
    /// indistinguishable from an import that never ran — which is the exact defect the
    /// achievements dump shipped with for two days.
    ///
    /// **Read off the MARKER, not off a variable the importer kept.** The marker is the
    /// record and the idempotence key; reading it here is what lets the report survive the
    /// launches after the one that did the copying, and it is the same file
    /// <c>ProfileImport</c> writes last.
    ///
    /// **ABOVE the rows** (trap 44): a report about something that just happened, appended
    /// after a list, is below the fold — and the Raids import report proved that by
    /// rendering correctly behind a scrollbar on a surface nobody scrolls.
    /// </summary>
    private void AddImportReport()
    {
        _importReported = false;
        if (ProfileImport.ReadMarker(AppPaths.Dir) is not { } marker) return;
        _importReported = true;

        _body.Children.Add(CardParts.BlockLabel(
            ProfileImportReadout.ReportHeadline(marker), hidden: false));
        var line = Line(ProfileImportReadout.Report(marker), Role.Body, Tok.SpaceXxs);
        // Ink by what it SAYS: an import that landed is good news, a start-fresh is a
        // statement of fact rather than a success.
        line.Ink(marker.Decision == "imported" ? "GoodBrush" : "TextBrush");
        _body.Children.Add(line);
        _body.Children.Add(Line(ProfileImportReadout.ReportUndo, Role.Metadata, Tok.SpaceXxs));
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
    /// <remarks><c>setupImportReport</c> is the same kind of row as <c>setupCopyCmd</c> and
    /// for the same reason: a source scan can prove this file MENTIONS the import marker,
    /// and only a launched app can say the block was drawn — an absent control photographs
    /// as an unremarkable panel (trap 29), and this one is the only place a player is ever
    /// told a copy of their 1.x profile happened.</remarks>
    public string DebugFacts() =>
        $"setupRows={_rows.Count} setupCopyCmd={_copyCommands} " +
        $"setupImportReport={(_importReported ? 1 : 0)}";
}
