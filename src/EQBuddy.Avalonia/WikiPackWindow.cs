using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The wiki contribution pack, as its own surface (#217 Ask 1, Frankthetankk) — the
/// Linux/macOS twin of <c>EQBuddy/WikiPackWindow.xaml</c>.
///
/// Both windows compose <see cref="WikiPackPresentation"/> and neither one decides
/// anything itself: the headline, the scope line, the row order, the status words, the
/// icons and the three different empty states all come from the shared module. That is
/// #210's lesson applied before the drift rather than after it — parity by shared module,
/// not by feature list. The Loot card is the counter-example worth remembering: the
/// Avalonia side had never called the shared row builder and was a whole feature behind.
///
/// It reuses <see cref="IDropsHost"/> rather than inventing a host interface: the pack
/// needs exactly the members Drops by Creature already asks for.
/// </summary>
public sealed class WikiPackWindow : Window
{
    private readonly IDropsHost _main;
    private readonly WikiPackPool _pool;
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;

    private readonly TextBlock _headline = DesignSystem.Text(DesignTokens.TypeRole.TitleSection);
    private readonly TextBlock _breakdown = DesignSystem.Text(DesignTokens.TypeRole.BodySecondary);
    private readonly TextBlock _scope = DesignSystem.Text(DesignTokens.TypeRole.BodySecondary);
    private readonly Button _copyBtn = new();
    /// <summary>The re-check (#226): read the flagged pages again, past the 7-day cache.
    /// Explicit, never on open — re-reading on open is the burst, paid by every user on
    /// every open to serve the few who edit.</summary>
    private readonly Button _recheckBtn = new();
    private readonly StackPanel _rowsPanel = new();

    public WikiPackWindow(IDropsHost main)
    {
        _main = main;
        // Stored sessions read once per open (this window is built fresh each time);
        // the live session re-folds on top only when its mob set moves.
        _pool = new WikiPackPool(main.StoredMobRows);
        Title = "EQBuddy — Wiki contribution pack";
        Width = 560;
        Height = 520;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = AppTheme.BgBrush;
        Content = BuildContent();
        WindowZoom.Attach(this, "wikipack", main.Settings);
    }

    private Control BuildContent()
    {
        _headline.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        _breakdown.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        _breakdown.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, 0);
        _scope.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        _scope.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        header.Children.Add(_headline);
        header.Children.Add(_breakdown);
        header.Children.Add(_scope);

        _copyBtn.Content = "Copy for wiki";
        _copyBtn.FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Body).Size;
        _copyBtn.Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs);
        _copyBtn.Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS);
        _copyBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _copyBtn.Background = AppTheme.PanelBrush;
        _copyBtn.Foreground = AppTheme.TextBrush;
        _copyBtn.BorderThickness = new Thickness(0);
        _copyBtn.Cursor = new Cursor(StandardCursorType.Hand);
        _copyBtn.Click += (_, _) => OnCopy();

        _recheckBtn.FontSize = _copyBtn.FontSize;
        _recheckBtn.Padding = _copyBtn.Padding;
        _recheckBtn.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, DesignTokens.SpaceS);
        _recheckBtn.Background = AppTheme.PanelBrush;
        _recheckBtn.Foreground = AppTheme.TextBrush;
        _recheckBtn.BorderThickness = new Thickness(0);
        _recheckBtn.Cursor = new Cursor(StandardCursorType.Hand);
        _recheckBtn.Click += (_, _) => OnRecheck();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_copyBtn);
        buttons.Children.Add(_recheckBtn);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _rowsPanel,
        };

        var footer = AppTheme.DimText(WikiPackPresentation.Footer,
            new Thickness(0, DesignTokens.SpaceM, 0, 0));

        var layout = new Grid { Margin = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceM) };
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(header, 0);
        Grid.SetRow(buttons, 1);
        Grid.SetRow(scroll, 2);
        Grid.SetRow(footer, 3);
        layout.Children.Add(header);
        layout.Children.Add(buttons);
        layout.Children.Add(scroll);
        layout.Children.Add(footer);
        return layout;
    }

    /// <summary>Called on open and from MainWindow's tick while visible.</summary>
    public void Update(StatsSnapshot s)
    {
        _lastRefresh = DateTime.Now;
        var (character, server) = _main.Identity;
        _pool.Refresh(s, character, server, _main.ActiveSessionRowId);
        Render();
    }

    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 3) Update(_main.CurrentSnapshot());
    }

    /// <summary>The same observations the clipboard export consumes, so what is on screen
    /// is what gets pasted. POOLED across every stored session plus the live one (#217
    /// ask 2) — twelve kills over three evenings now cross the 10-kill rarity bar the
    /// honesty rules refuse to relax.</summary>
    private List<WikiContribution.MobObservation> Observations()
    {
        var mobs = _pool.Mobs.Where(m => m.Loot.Count > 0).ToList();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        return mobs
            .Select(m => new WikiContribution.MobObservation(m, _main.WikiMobResult(m.Name)))
            .ToList();
    }

    private void Render()
    {
        var observations = Observations();
        var pack = WikiPackPresentation.Build(observations);
        var targets = WikiPackPresentation.RecheckTargets(observations);
        var inFlight = targets.Count(_main.IsRechecking);

        // Lookups land async, so the tick re-renders until they settle; rebuilding an
        // identical panel every three seconds would fight the scroll position. The
        // re-check's state rides along so "checking 3 of 9…" advances as pages land.
        var sig = string.Join("|", pack.Rows.Select(r =>
            $"{r.Creature}:{r.Kind}:{r.Contributions}"))
            + $"|{pack.PendingCreatures}|{targets.Count}|{inFlight}|{_pool.Scope.SessionCount}";
        if (sig == _signature) return;
        _signature = sig;

        _recheckBtn.Content = inFlight > 0
            ? WikiPackPresentation.RecheckProgress(inFlight, targets.Count)
            : WikiPackPresentation.RecheckLabel(targets.Count);
        _recheckBtn.IsEnabled = WikiPackPresentation.CanRecheck(targets.Count, inFlight > 0);
        _recheckBtn.Opacity = _recheckBtn.IsEnabled ? 1.0 : 0.5;   // trap 17, as Copy below
        ToolTip.SetTip(_recheckBtn, WikiPackPresentation.RecheckTip(targets.Count, inFlight > 0));

        _headline.Text = WikiPackPresentation.Headline(pack);
        _scope.Text = WikiPackPresentation.ScopeLine(_pool.Scope,
            kills: observations.Sum(o => o.Mob.Kills), creatures: observations.Count);

        var breakdown = WikiPackPresentation.Breakdown(pack);
        _breakdown.Text = breakdown;
        _breakdown.IsVisible = breakdown.Length > 0;

        _copyBtn.IsEnabled = WikiPackPresentation.CanCopy(pack);
        // Trap 17: IsEnabled alone is invisible — the button style carries no disabled
        // visual, so a dead button would look exactly like a live one.
        _copyBtn.Opacity = _copyBtn.IsEnabled ? 1.0 : 0.5;
        ToolTip.SetTip(_copyBtn, WikiPackPresentation.CopyTip(pack));

        _rowsPanel.Children.Clear();
        if (pack.Rows.Count == 0)
        {
            var empty = DesignSystem.Text(DesignTokens.TypeRole.Body,
                WikiPackPresentation.EmptyText(pack));
            empty.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
            empty.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
            _rowsPanel.Children.Add(empty);
            return;
        }

        foreach (var row in pack.Rows) _rowsPanel.Children.Add(BuildRow(row));
    }

    /// <summary>Trap 14: an icon beside wrapping text needs a two-column Grid. A stack
    /// measures its children with infinite width in the stacking direction, so the text
    /// never reaches a boundary to wrap at and is silently CLIPPED instead.</summary>
    private Control BuildRow(WikiPackPresentation.PackRow row)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs),
            // Two columns, not three — see the WPF twin: the right-aligned count the first
            // cut carried just repeated the note at the far edge of the row.
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        var icon = DesignSystem.Icon(WikiPackPresentation.KindIcon(row.Kind),
            WikiPackPresentation.KindInk(row.Kind), DesignTokens.IconInline);
        icon.Margin = new Thickness(0, 0, DesignTokens.SpaceS, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel();
        var name = DesignSystem.Text(DesignTokens.TypeRole.Body, row.Creature);
        name.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        // The pack's whole job is "go and edit these pages", and the creature name was the
        // one thing on the row that looked like a link and was not (#226, LeBigNasty). It
        // goes to the page the wiki SERVED when we read it, so the player lands on the
        // article the paste is written against rather than on a search result (trap 3).
        name.Cursor = new global::Avalonia.Input.Cursor(
            global::Avalonia.Input.StandardCursorType.Hand);
        ToolTip.SetTip(name, "Open this creature's page on eqlwiki — the page this edit is for");
        var creature = row.Creature;
        name.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != global::Avalonia.Input.MouseButton.Left) return;
            e.Handled = true;
            MainWindow.OpenWikiUrl(WikiLinks.Creature(_main.WikiMobResult(creature), creature));
        };
        text.Children.Add(name);

        var kills = row.Kills == 1 ? "1 kill" : $"{row.Kills} kills";
        var detail = DesignSystem.Text(DesignTokens.TypeRole.BodySecondary,
            $"{WikiPackPresentation.KindLabel(row.Kind)} · {row.Note} · {kills}");
        detail.TextWrapping = global::Avalonia.Media.TextWrapping.Wrap;
        ToolTip.SetTip(detail, WikiPackPresentation.KindTip(row.Kind));
        text.Children.Add(detail);

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        return grid;
    }

    /// <summary>Re-read the flagged creatures' pages (#226), through the host's own
    /// re-check (both stale layers, two-in-flight cap). Copy does NOT re-read: that would
    /// change what the player saw before they pressed it.</summary>
    private void OnRecheck()
    {
        foreach (var creature in WikiPackPresentation.RecheckTargets(Observations()))
            _main.RecheckMobLookup(creature);
        _signature = "";
        Render();
    }

    private async void OnCopy()
    {
        var (character, server) = _main.Identity;
        var text = WikiContribution.BuildExport(
            Observations(), character, server, _main.CurrentZoneName, DateTime.Now);
        try
        {
            if (GetTopLevel(this)?.Clipboard is { } clip)
                await clip.SetTextAsync(text).ConfigureAwait(false);
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }
}
