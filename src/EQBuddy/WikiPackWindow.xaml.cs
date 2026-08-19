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
public partial class WikiPackWindow : Window
{
    private readonly MainWindow _main;
    private StatsSnapshot _snapshot = new();
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;

    public WikiPackWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        WindowZoom.Attach(this, "wikipack", main.Settings);
        FooterText.Text = WikiPackPresentation.Footer;
    }

    /// <summary>Called on open and from MainWindow's tick while visible.</summary>
    public void Update(StatsSnapshot s)
    {
        _lastRefresh = DateTime.Now;
        _snapshot = s;
        Render();
    }

    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 3) Update(_main.CurrentSnapshot());
    }

    /// <summary>The same observations the clipboard export consumes, so what is on screen
    /// is what gets pasted. Lookups are kicked off for everything shown — a creature whose
    /// page has not been read yet is reported as exactly that, never as "nothing new".</summary>
    private List<WikiContribution.MobObservation> Observations()
    {
        var mobs = _snapshot.Mobs.Where(m => m.Loot.Count > 0).ToList();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        return mobs
            .Select(m => new WikiContribution.MobObservation(m, _main.WikiMobResult(m.Name)))
            .ToList();
    }

    private void Render()
    {
        var observations = Observations();
        var pack = WikiPackPresentation.Build(observations);

        // Same memo-signature guard as DropsWindow (#65): lookups land async, so the tick
        // re-renders until they settle. Rebuilding an identical panel every three seconds
        // would fight the scroll position.
        var sig = string.Join("|", pack.Rows.Select(r =>
            $"{r.Creature}:{r.Kind}:{r.Contributions}")) + $"|{pack.PendingCreatures}";
        if (sig == _signature) return;
        _signature = sig;

        var (character, server) = _main.Identity;
        HeadlineText.Text = WikiPackPresentation.Headline(pack);
        ScopeText.Text = WikiPackPresentation.ScopeLine(character, server, _snapshot.SessionStart);

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

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var (character, server) = _main.Identity;
        var text = WikiContribution.BuildExport(
            Observations(), character, server, _main.CurrentZoneName, DateTime.Now);
        try { Clipboard.SetText(text); }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }
}
