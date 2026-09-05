using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The wiki contribution pack, as its own surface (#217 Ask 1, Frankthetankk).
///
/// It used to be one button — "✦ Copy for wiki" — inside Drops by Creature, and his
/// argument for moving it is the one that decided this: the pack stopped being a loot
/// feature several releases ago. Creature pages, item pages, con-derived level ranges,
/// money and faction ranges with small-sample caveats — that is a contribution pipeline
/// living behind a button in a loot window, findable only by someone already thinking
/// about loot.
///
/// **Why it is a window and not just a relocated menu command.** The button's data source
/// is the session snapshot, and the only thing that made that scope legible was standing
/// in front of the session's drop list while pressing it. Fired from Data &amp; imports it
/// would have copied a silent scope. So the move comes with a surface that says what it
/// pooled — and <see cref="WikiPackPresentation"/> owns every word of that, which is what
/// lets the Avalonia twin be the same feature rather than a re-implementation.
///
/// Drops by Creature keeps its live view untouched: "is this trip worth it" is a different
/// question from "what can I give the wiki", and Frankthetankk asked for both to survive.
/// </summary>
public partial class WikiPackWindow : Window, IFollowingSurface
{
    private readonly MainWindow _main;
    private readonly WikiPackPool _pool;
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;

    public WikiPackWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        // Stored sessions read once per open (this window is built fresh each time);
        // the live session re-folds on top only when its mob set moves.
        _pool = new WikiPackPool(main.StoredMobRows);
        WindowZoom.Attach(this, "wikipack", main.Settings);
        FooterText.Text = WikiPackPresentation.Footer;
    }

    /// <summary>Called on open and from MainWindow's tick while visible.</summary>
    /// <summary>The snapshot VERSION this window last PAINTED — see
    /// <c>CreatureWindow.RenderedVersion</c> for why the dump carries it. This window's
    /// throttle is THREE seconds, the longest of the six.</summary>
    public long RenderedVersion { get; private set; } = -1;

    public void Update(StatsSnapshot s)
    {
        _lastRefresh = DateTime.Now;
        RenderedVersion = s.Version;
        var (character, server) = _main.Identity;
        _pool.Refresh(s, character, server, _main.ActiveSessionRowId);
        Render();
    }

    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 3) Update(_main.CurrentSnapshot());
    }

    void IFollowingSurface.MaybeFollow() => MaybeRefresh();
    // Painting more often does NOT ask eqlwiki more often: EnsureMobLookup keys on the
    // creature name and no-ops once one is in flight or cached, so the request count is
    // the number of distinct creatures either way (the consequence list's rule 7).
    void IFollowingSurface.PaintNow() => Update(_main.CurrentSnapshot());

    /// <summary>The same observations the clipboard export consumes, so what is on screen
    /// is what gets pasted. POOLED across every stored session plus the live one (#217
    /// ask 2) — that is what lets twelve kills over three evenings cross the 10-kill
    /// rarity bar the honesty rules refuse to relax. Lookups are kicked off for
    /// everything shown; the 2-in-flight cap in the host is what keeps a long history
    /// from bursting eqlwiki.</summary>
    private List<WikiContribution.MobObservation> Observations()
    {
        var mobs = _pool.Mobs.Where(m => m.Loot.Count > 0).ToList();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        return mobs
            .Select(m => new WikiContribution.MobObservation(m, _main.WikiMobResult(m.Name),
                _main.RespawnEvidenceFor(m.Zone, m.Name)))
            .ToList();
    }

    private void Render()
    {
        var observations = Observations();
        var pack = WikiPackPresentation.Build(observations);

        // The re-check button's state rides the signature too, so the tick repaints
        // "checking 3 of 9…" as pages land and the label when the last one does.
        var targets = WikiPackPresentation.RecheckTargets(observations);
        var inFlight = targets.Count(_main.IsRechecking);

        // Same memo-signature guard as DropsWindow (#65): lookups land async, so the tick
        // re-renders until they settle. Rebuilding an identical panel every three seconds
        // would fight the scroll position.
        var sig = string.Join("|", pack.Rows.Select(r =>
            $"{r.Creature}:{r.Kind}:{r.Contributions}"))
            + $"|{pack.PendingCreatures}|{targets.Count}|{inFlight}|{_pool.Scope.SessionCount}";
        if (sig == _signature) return;
        _signature = sig;

        // Rows keep their previous kind until the new answer lands — the memo is never
        // nulled by a re-check — so nothing here flickers to "not checked yet" mid-read.
        RecheckBtn.Content = inFlight > 0
            ? WikiPackPresentation.RecheckProgress(inFlight, targets.Count)
            : WikiPackPresentation.RecheckLabel(targets.Count);
        RecheckBtn.IsEnabled = WikiPackPresentation.CanRecheck(targets.Count, inFlight > 0);
        RecheckBtn.Opacity = RecheckBtn.IsEnabled ? 1.0 : 0.5;   // trap 17, as Copy below
        RecheckBtn.ToolTip = WikiPackPresentation.RecheckTip(targets.Count, inFlight > 0);

        HeadlineText.Text = WikiPackPresentation.Headline(pack);
        ScopeText.Text = WikiPackPresentation.ScopeLine(_pool.Scope,
            kills: observations.Sum(o => o.Mob.Kills), creatures: observations.Count);

        var breakdown = WikiPackPresentation.Breakdown(pack);
        BreakdownText.Text = breakdown;
        BreakdownText.Visibility = breakdown.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        CopyBtn.Content = "Copy for wiki";
        CopyBtn.IsEnabled = WikiPackPresentation.CanCopy(pack);
        // Trap 17: IsEnabled alone is invisible — the app's button style carries no
        // disabled visual, so a dead button would look exactly like a live one.
        CopyBtn.Opacity = CopyBtn.IsEnabled ? 1.0 : 0.5;
        CopyBtn.ToolTip = WikiPackPresentation.CopyTip(pack);

        RowsPanel.Children.Clear();
        if (pack.Rows.Count == 0)
        {
            var empty = DesignSystem.Text(DesignTokens.TypeRole.Body,
                WikiPackPresentation.EmptyText(pack));
            empty.TextWrapping = TextWrapping.Wrap;
            empty.Margin = (System.Windows.Thickness)FindResource("LeadXs");
            RowsPanel.Children.Add(empty);
            return;
        }

        foreach (var row in pack.Rows) RowsPanel.Children.Add(BuildRow(row));
    }

    /// <summary>Trap 14: an icon beside wrapping text needs a two-column Grid. In a
    /// horizontal StackPanel the text is measured with infinite width, never reaches a
    /// boundary to wrap at, and is silently CLIPPED — which is what shipped the Gate 2
    /// "pick classes ab" row in both UIs.</summary>
    private UIElement BuildRow(WikiPackPresentation.PackRow row)
    {
        // Two columns, not three. The first cut carried a right-aligned contribution count
        // as well, and the screenshot showed it for what it was: the same "9 items" the
        // note already says, repeated at the far edge of the row where nothing connects
        // it to the creature. One entry, one source (trap 4) applies to what is DRAWN too.
        var grid = new Grid { Margin = (System.Windows.Thickness)FindResource("ListBlock") };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = DesignSystem.Icon(WikiPackPresentation.KindIcon(row.Kind),
            WikiPackPresentation.KindInk(row.Kind), DesignTokens.IconInline);
        icon.Margin = (System.Windows.Thickness)FindResource("GapS");
        icon.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel();
        var name = DesignSystem.Text(DesignTokens.TypeRole.Body, row.Creature);
        name.TextWrapping = TextWrapping.Wrap;
        // The pack's whole job is "go and edit these pages", and the creature name was the
        // one thing on the row that looked like a link and was not (#226). It goes to the
        // page the wiki SERVED when we read it, so the player lands on the article the
        // paste is written against rather than on a search result (trap 3).
        name.Cursor = System.Windows.Input.Cursors.Hand;
        name.ToolTip = "Open this creature's page on eqlwiki — the page this edit is for";
        var creature = row.Creature;
        name.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            MainWindow.OpenWikiUrl(WikiLinks.Creature(_main.WikiMobResult(creature), creature));
        };
        text.Children.Add(name);

        var kills = row.Kills == 1 ? "1 kill" : $"{row.Kills} kills";
        var detail = DesignSystem.Text(DesignTokens.TypeRole.BodySecondary,
            $"{WikiPackPresentation.KindLabel(row.Kind)} · {row.Note} · {kills}");
        detail.TextWrapping = TextWrapping.Wrap;
        detail.ToolTip = WikiPackPresentation.KindTip(row.Kind);
        text.Children.Add(detail);

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        return grid;
    }

    /// <summary>Re-read the flagged creatures' pages (#226). Bounded to
    /// <see cref="WikiPackPresentation.RecheckTargets"/>, run through the host's own
    /// re-check — which forgets both stale layers and obeys the two-in-flight cap — and
    /// never silently: Copy does NOT re-read, because that would change what the player
    /// saw before they pressed it.</summary>
    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        foreach (var creature in WikiPackPresentation.RecheckTargets(Observations()))
            _main.RecheckMobLookup(creature);
        _signature = "";
        Render();
    }

    /// <summary>For the E2E dump. <c>packRecheck</c> is the re-check button's target
    /// count — asserted because an absent control photographs as an unremarkable
    /// button row (trap 29/34).</summary>
    public string DebugFacts() =>
        $"packRows={RowsPanel.Children.Count} " +
        $"packRecheck={WikiPackPresentation.RecheckTargets(Observations()).Count}";

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var (character, server) = _main.Identity;
        var text = WikiContribution.BuildExport(
            Observations(), character, server, _main.CurrentZoneName, DateTime.Now);
        try { Clipboard.SetText(text); }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }
}
