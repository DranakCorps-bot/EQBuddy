using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// Session drops grouped by source creature, with export (discussion #55 — LeBigNasty
/// tracking Plane of Sky and feeding corrected drop tables back to eqlwiki for revamped
/// zones). Read-only over <c>StatsSnapshot.Mobs</c>; the filter narrows both the display
/// and what the export buttons emit, so "just the golems" is one filter away.
/// </summary>
/// <remarks>
/// A TAB of the KILLS &amp; DROPS theme since 2026-08-21, not a window buried in the cog
/// menu where nobody found it. Both tabs answer one question — <i>is this camp worth
/// it?</i> — and it was being answered in two places.
///
/// **This lift was a XAML-to-code conversion, not the straight move GearLockerView was.**
/// The body lived in <c>DropsWindow.xaml</c>, so every control here is the hand-written
/// twin of a markup element, and the two things that had to be re-decided rather than
/// copied are the ones a diff cannot show:
/// <list type="bullet">
/// <item>The <c>ScrollViewer</c> did NOT come with it (trap 36). A child scroller inside
/// the host's own is measured with infinite height, so it never overflows, never scrolls,
/// and still swallows the wheel — which cost David a working mouse wheel on the Inventory
/// tab for a day. Scrolling belongs to the host.</item>
/// <item>The header row was a four-column <c>Grid</c> sized for a 560px window. It is a
/// <c>DockPanel</c>-free <c>Grid</c> with the filter in the star column here, for the same
/// reason the Inventory bar became a WrapPanel: docked or auto-sized buttons take what they
/// ask for and starve the flexible child.</item>
/// </list>
/// </remarks>
internal sealed class DropsCardView : IWidgetCard
{
    private readonly MainWindow _main;
    private readonly TextBox _filter;
    private readonly StackPanel _mobs = new();
    private StatsSnapshot _snapshot = new();
    private string _signature = "";

    public string Key => CreatureSurface.KeyFor(CreatureTab.Drops);
    public UIElement Body { get; }

    /// <summary>The tab strip's headline: how many creature types have dropped something.
    /// Nothing at all before the first drop — a "0" on a fresh character reads as a
    /// failure rather than as a session that has not started.</summary>
    public string? Badge { get; private set; }

    /// <summary>The facts <c>DropsWindow</c> reported before this lift, with the SAME
    /// keys, so the E2E assertion written against the old host reads the same numbers out
    /// of the new one. The WPF layer has no unit tests (docs/TestPlan.md §5), so a launched
    /// app asserting these is the only cover the conversion has.</summary>
    internal int DebugMobCount => Filtered().Count;

    internal int DebugRowCount => _mobs.Children.Count;

    internal int DebugItemCount => Filtered().Sum(m => m.Loot.Count);

    internal int DebugFilterLength => _filter.Text.Trim().Length;

    public DropsCardView(MainWindow main)
    {
        _main = main;

        _filter = new TextBox { ToolTip = "Filter by creature or item name" };
        _filter.SetResourceReference(FrameworkElement.StyleProperty, "InputBox");
        _filter.TextChanged += (_, _) => { _signature = ""; Render(); };

        var bar = new Grid { Margin = new Thickness(0, 0, 0, Tok.SpaceS) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++)
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Children.Add(_filter);
        Add(bar, 1, "Copy text",
            "Copy everything shown as readable text — pastes cleanly into Discord or a wiki page",
            () => TryClipboard(DropsReport.ToText(
                Filtered(), _main.Identity.Character, _main.Identity.Server, _snapshot.SessionStart)));
        Add(bar, 2, "Copy CSV", "Copy everything shown as CSV for a spreadsheet",
            () => TryClipboard(DropsReport.ToCsv(Filtered())));
        Add(bar, 3, "Save CSV…", "Write everything shown to a .csv file", SaveCsv);

        // The pack moved to its own window (#217 Ask 1). The marker stays — this view
        // still answers "is this trip worth it" — but the footer says where the paste
        // went, so the button leaving is a signpost rather than a disappearance.
        var footer = new TextBlock
        {
            Text = "Observed personal drop rates this session — the kill count is the "
                + "denominator, so thin data looks thin. Exports include exactly what the "
                + "filter shows. " + WikiPackPresentation.MovedHint,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, Tok.SpaceS),
        };
        footer.SetResourceReference(FrameworkElement.StyleProperty, "Dim");

        // No ScrollViewer: the host owns scrolling (trap 36).
        // ABOVE the rows, not below them. As a window this sat in its own Grid row
        // outside the scroller and was ALWAYS on screen; the host owns scrolling now
        // (trap 36), so a footer at the bottom of the body is a footer under thirteen
        // creatures of rows — which the first screenshot showed and no test could. It
        // carries the only in-app pointer to where the contribution pack went (#217
        // Ask 1), so "scroll to find out" is the wrong place for it.
        var root = new StackPanel();
        root.Children.Add(bar);
        root.Children.Add(footer);
        root.Children.Add(_mobs);
        Body = root;
    }

    private static void Add(Grid bar, int column, string text, string tip, Action click)
    {
        var b = Theming.Button(text);
        b.ToolTip = tip;
        b.Margin = new Thickness(column == 1 ? Tok.SpaceS : Tok.SpaceXs, 0, 0, 0);
        b.Click += (_, _) => click();
        Grid.SetColumn(b, column);
        bar.Children.Add(b);
    }

    public void Render(StatsSnapshot snapshot)
    {
        _snapshot = snapshot;
        Render();
    }

    private List<MobSummary> Filtered()
    {
        var filter = _filter.Text.Trim();
        var mobs = _snapshot.Mobs.Where(m => m.Loot.Count > 0);
        if (filter.Length > 0)
            mobs = mobs.Where(m =>
                m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || m.Loot.Any(l => l.Item.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        return mobs.ToList();
    }

    /// <summary>Wiki knowledge per creature, via MainWindow's target-drops memo (#65):
    /// lookups fire for every creature shown, results land async, and the signature
    /// carries each item's status so rows re-render as answers arrive.</summary>
    private WikiDropStatus Status(MobSummary mob, MobLoot l) =>
        WikiContribution.Classify(_main.WikiMobResult(mob.Name), l.Item);

    public void Render()
    {
        var mobs = Filtered();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        // The badge is computed OUTSIDE the signature gate, deliberately. The signature
        // exists to skip redrawing forty rows that have not changed; a badge the tab strip
        // reads every second must be true whether or not the rows moved.
        Badge = mobs.Count > 0
            ? $"{mobs.Count} creature{(mobs.Count == 1 ? "" : "s")}"
            : null;

        // THE FILTER LEADS THE SIGNATURE, and it is a fix rather than a copy. WPF's
        // DropsWindow hashed only the rows, and reset _signature to "" on a filter change
        // — so filtering to something that matches NOTHING produced sig == "" == the reset
        // sentinel, the early-out fired, and the stale rows stayed on screen instead of
        // "Nothing matches that filter." The Avalonia twin had already fixed this; the
        // conversion is where the two lanes get to agree.
        var sig = _filter.Text.Trim() + "\u0001" + string.Join("|", mobs.Select(m =>
            $"{m.Name}:{m.Kills}:{string.Join(",", m.Loot.Select(l => $"{l.Item}{l.Count}{(int)Status(m, l)}"))}"));
        if (sig == _signature) return;
        _signature = sig;

        _mobs.Children.Clear();
        foreach (var mob in mobs)
        {
            var pageStatus = mob.Loot.Count > 0 ? Status(mob, mob.Loot[0]) : WikiDropStatus.Unknown;
            // CLICKABLE, because the tooltip two rows down has always said so: step 2 of
            // the how-to-sync note is "Click the creature's name to open its wiki page",
            // and until 2026-08-21 this was a plain label (#226, LeBigNasty). An app that
            // names an action and does not offer it is the silent-no-op rule with the
            // switch on the other side — the same defect as the Gear tab naming an import
            // it gave you no way to run.
            var header = new TextBlock
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Open this creature's page on eqlwiki",
                Text = $"{mob.Name} — {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}"
                    + pageStatus switch
                    {
                        WikiDropStatus.PageMissing => "  ·  no wiki page yet",
                        WikiDropStatus.PageHasNoLoot => "  ·  wiki page lists no loot yet",
                        _ => "",
                    },
                FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, Tok.SpaceS, 0, Tok.SpaceXxs),
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            var creature = mob.Name;
            header.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MainWindow.OpenWikiUrl(WikiLinks.Creature(_main.WikiMobResult(creature), creature));
            };
            _mobs.Children.Add(header);

            foreach (var l in mob.Loot)
                _mobs.Children.Add(ItemRow(l, mob.Kills, Status(mob, l)));
        }
        if (mobs.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = _filter.Text.Trim().Length > 0
                    ? "Nothing matches that filter."
                    : "No drops recorded this session yet — loot lines name their corpse,\nso every kill you loot shows up here.",
                FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, Tok.SpaceS, 0, 0),
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            _mobs.Children.Add(empty);
        }
    }

    /// <summary>One drop row, wired like the Loot card (David, 2026-08-07: "in the loot
    /// window that we track drops in"): click the name → the item's wiki page, hover →
    /// its stats fetched live, and quest items carry the map pin that opens the Quest
    /// Tracker filtered to the quests that want them.</summary>
    private StackPanel ItemRow(MobLoot l, int kills, WikiDropStatus wikiStatus)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(Tok.SpaceL, 1, 0, 1),
        };

        var name = new TextBlock
        {
            Text = l.Item,
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var cached = _main.CachedItemStats(l.Item);
        var tipText = new TextBlock
        {
            Text = cached ?? "Looking up on eqlwiki…",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new FontFamily("Consolas"),
        };
        var tip = new System.Windows.Controls.ToolTip { Content = tipText };
        name.ToolTip = tip;
        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            var text = await _main.FetchItemTooltip(l.Item);
            tipText.Text = text ?? (cached ?? "Not on the wiki.");
        };
        name.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            MainWindow.OpenWikiPage(l.Item);
        };
        row.Children.Add(name);

        // THE one quest badge, from EqCardRows — not a fourth hand-drawn one. It was an
        // emoji 🗺 with a click handler here, and a glyph hit-tests across its whole layout
        // rect where a vector only responds where it is PAINTED (#211, n3cr0nk1tt3n). The
        // shared builder already carries that lesson; drawing a badge in four places is
        // how a badge ends up clickable in three of them.
        if (EqCardRows.QuestBadge(_main, l.Item) is { } badge) row.Children.Add(badge);

        // The ✦ David asked for (#65), promoted to RED with a how-to-sync tooltip
        // (David, 2026-08-10): red says "the wiki doesn't know this yet — you're
        // holding new knowledge", and the hover tells you exactly how to hand it in.
        if (wikiStatus is WikiDropStatus.NewToPage or WikiDropStatus.PageHasNoLoot
            or WikiDropStatus.PageMissing)
        {
            var why = wikiStatus switch
            {
                WikiDropStatus.PageMissing => "This creature has no eqlwiki page at all.",
                WikiDropStatus.PageHasNoLoot => "The creature's wiki page lists no loot yet.",
                _ => "This drop isn't in the creature's wiki loot list yet.",
            };
            row.Children.Add(DesignSystem.InlineIconButton("Sparkle",
                why + " You're holding knowledge eqlwiki.com doesn't have.\n" +
                "\n" +
                "To sync it to the wiki (takes about a minute):\n" +
                "  1. Click this ✦ to open the Wiki contribution pack — it lists everything\n" +
                "     the wiki is missing this session and copies paste-ready edits, built\n" +
                "     from your observed drops and rates. (Data & imports opens it too.)\n" +
                "  2. Click the creature's name to open its wiki page, then Edit (create the\n" +
                "     page if it doesn't exist — the export includes the full page skeleton).\n" +
                "  3. Paste, save, done. The whole community's tracker gets smarter.",
                (_, _) => _main.ShowWikiPackWindow(), "BadBrush"));
        }

        var counts = new TextBlock
        {
            Text = $"  ×{l.Count}" + (l.DropRatePct is { } pct ? $"  ·  {pct:0.#}% of {kills}" : ""),
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
        };
        counts.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        row.Children.Add(counts);
        return row;
    }

    private static void TryClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }

    private void SaveCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"eqbuddy-drops-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, DropsReport.ToCsv(Filtered())); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
