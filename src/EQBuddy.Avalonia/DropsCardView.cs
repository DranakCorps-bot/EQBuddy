using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>What the Drops surface needs from the widget — the interface that let it be
/// a window, and now lets it be a tab AND a unit-testable view. <c>WikiPackWindow</c>
/// reuses it, which is why it outlived the window it was written for.</summary>
public interface IDropsHost
{
    AppSettings Settings { get; }
    (string Character, string Server) Identity { get; }
    string CurrentZoneName { get; }
    StatsSnapshot CurrentSnapshot();
    string? CachedItemStats(string itemName);
    Task<string?> FetchItemTooltip(string itemName);
    MobLookupResult? WikiMobResult(string name);
    void EnsureMobLookup(string name);
    /// <summary>Read the creature's page again now, past the cache (#226). The host owns
    /// both stale layers — the disk cache and its session memo — so it is the host's call.</summary>
    void RecheckMobLookup(string name);
    bool IsRechecking(string name);
    bool IsActiveQuestItem(string name);
    void OpenQuestInfoForItem(string itemName);
    /// <summary>#217 Ask 1: the contribution pack has its own window under Data &amp;
    /// imports now, and the row marker opens it instead of copying silently.</summary>
    void ShowWikiPack();
}

/// <summary>
/// Session drops grouped by source creature, with export (discussion #55 — LeBigNasty
/// tracking Plane of Sky and feeding corrected drop tables back to eqlwiki for revamped
/// zones). Read-only over <c>StatsSnapshot.Mobs</c>; the filter narrows both the display
/// and what the export buttons emit, so "just the golems" is one filter away.
/// </summary>
/// <remarks>
/// A TAB of the KILLS &amp; DROPS theme since 2026-08-21, not a window buried in the cog
/// menu where nobody found it — the same fold Windows took in the same change, because the
/// theme switches on in shared Core vocabulary and a fold that lands on one build only
/// takes the Kills card off the other with nowhere for it to go.
///
/// It keeps <see cref="IDropsHost"/> rather than taking the widget: the interface already
/// existed for the window, <c>DropsRenderTests</c> already drives it with a stub, and this
/// build has no E2E suite (docs/TestPlan.md) — so a surface that could only be built from a
/// live widget would be a surface with no cover at all.
///
/// **No ScrollViewer.** The window had one; carrying it into a tab would put a child
/// scroller inside the host's own, where it is measured with infinite height, never
/// overflows, never scrolls, and still swallows the wheel (trap 36).
/// </remarks>
internal sealed class DropsCardView
{
    private readonly IDropsHost _main;
    private StatsSnapshot _snapshot = new();
    private string _signature = "";

    private readonly TextBox _filterBox = new()
    {
        FontSize = 12,
        Padding = new Thickness(5, 3),
        Background = AppTheme.ComboBoxBrush,
        Foreground = AppTheme.TextBrush,
        CaretBrush = AppTheme.TextBrush,
        BorderBrush = AppTheme.BorderBrush,
        BorderThickness = new Thickness(1),
        VerticalContentAlignment = VerticalAlignment.Center,
    };
    private readonly StackPanel _mobsPanel = new();

    /// <summary>What the Kills &amp; Drops window hangs in its Drops tab.</summary>
    public Control Body { get; }

    /// <summary>Rendered creature count and row count, for the headless render tests —
    /// the only cover this build has for the surface.</summary>
    public int MobCount => Filtered().Count;
    public int RowCount => _mobsPanel.Children.Count;

    public DropsCardView(IDropsHost main)
    {
        _main = main;

        var topRow = new Grid
        {
            Margin = new Thickness(0, 0, 0, 8),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
        };
        ToolTip.SetTip(_filterBox, "Filter by creature or item name");
        _filterBox.TextChanged += (_, _) => { _signature = ""; Render(); };
        topRow.Children.Add(_filterBox);
        var buttons = new (string Text, string? Tip, Action Click)[]
        {
            ("Copy text",
                "Copy everything shown as readable text — pastes cleanly into Discord or a wiki page",
                OnCopyText),
            ("Copy CSV", "Copy everything shown as CSV for a spreadsheet", OnCopyCsv),
            ("Save CSV…", null, OnSaveCsv),
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            var (text, tip, click) = buttons[i];
            var button = new Button
            {
                Content = text,
                FontSize = 12,
                Padding = new Thickness(8, 3),
                Margin = new Thickness(i == 0 ? 8 : 6, 0, 0, 0),
                Background = AppTheme.PanelBrush,
                Foreground = AppTheme.TextBrush,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            if (tip is not null) ToolTip.SetTip(button, tip);
            button.Click += (_, _) => click();
            Grid.SetColumn(button, i + 1);
            topRow.Children.Add(button);
        }

        // The pack moved to its own window (#217 Ask 1). The marker stays — this view
        // still answers "is this trip worth it" — but the footer says where the paste
        // went, so the button leaving is a signpost rather than a disappearance.
        var footer = AppTheme.DimText(
            "Observed personal drop rates this session — the kill count is the denominator, " +
            "so thin data looks thin. Exports include exactly what the filter shows. " +
            WikiPackPresentation.MovedHint,
            new Thickness(0, 0, 0, 8));

        // ABOVE the rows, not below them. As a window this sat in its own Grid row
        // outside the scroller and was ALWAYS on screen; the host owns scrolling now
        // (trap 36), so a footer at the bottom of the body is a footer under thirteen
        // creatures of rows — which the first screenshot showed and no test could. It
        // carries the only in-app pointer to where the contribution pack went (#217
        // Ask 1), so "scroll to find out" is the wrong place for it.
        var root = new StackPanel();
        root.Children.Add(topRow);
        root.Children.Add(footer);
        root.Children.Add(_mobsPanel);
        Body = root;
    }

    /// <summary>Paint a snapshot. Called by the window's once-a-second tick and on every
    /// tab change — both cheap, because this is arithmetic over a snapshot already in
    /// memory and the signature below skips the redraw when nothing moved.</summary>
    public void Render(StatsSnapshot s)
    {
        _snapshot = s;
        Render();
    }

    private List<MobSummary> Filtered()
    {
        var filter = (_filterBox.Text ?? "").Trim();
        var mobs = _snapshot.Mobs.Where(m => m.Loot.Count > 0);
        if (filter.Length > 0)
            mobs = mobs.Where(m =>
                m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || m.Loot.Any(l => l.Item.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        return mobs.ToList();
    }

    /// <summary>Wiki knowledge per creature, via the widget's target-drops memo (#65):
    /// lookups fire for every creature shown, results land async, and the signature
    /// carries each item's status so rows re-render as answers arrive.</summary>
    private WikiDropStatus Status(MobSummary mob, MobLoot l) =>
        WikiContribution.Classify(_main.WikiMobResult(mob.Name), l.Item);

    private void Render()
    {
        var mobs = Filtered();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        // The filter LEADS the signature: an empty result list hashes to "", which collides
        // with the reset sentinel — the pane kept its stale rows instead of saying "Nothing
        // matches that filter." Windows carried that bug until the same fold, where its
        // conversion took this line rather than the other way round.
        // The freshness caption rides the signature in BUCKETS (WikiFreshness), so a
        // re-check that returns the same status still repaints its caption and a second
        // passing does not (trap 8).
        var now = DateTime.UtcNow;
        var sig = (_filterBox.Text ?? "").Trim() + "\u0001" + string.Join("|", mobs.Select(m =>
            $"{m.Name}:{m.Kills}:{WikiFreshness.SignatureToken(_main.WikiMobResult(m.Name), _main.IsRechecking(m.Name), now)}:"
            + string.Join(",", m.Loot.Select(l => $"{l.Item}{l.Count}{(int)Status(m, l)}"))));
        if (sig == _signature) return;
        _signature = sig;

        _mobsPanel.Children.Clear();
        foreach (var mob in mobs)
        {
            var pageStatus = mob.Loot.Count > 0 ? Status(mob, mob.Loot[0]) : WikiDropStatus.Unknown;
            var header = new TextBlock
            {
                Text = $"{mob.Name} — {mob.Kills} kill{(mob.Kills == 1 ? "" : "s")}"
                    + pageStatus switch
                    {
                        WikiDropStatus.PageMissing => "  ·  no wiki page yet",
                        WikiDropStatus.PageHasNoLoot => "  ·  wiki page lists no loot yet",
                        // Not "no loot yet": the wiki answered with the wrong ARTICLE, and
                        // saying the creature's page is empty would be a false statement
                        // about a page nobody has looked at (#226, Innoruk's Lore page).
                        WikiDropStatus.PageIsNotACreature => "  ·  that wiki page isn't the creature",
                        _ => "",
                    },
                FontSize = 13, FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 8, 0, 2),
                Foreground = AppTheme.AccentBrush,
            };
            // CLICKABLE, because the tooltip two rows down has always said so: step 2 of
            // the how-to-sync note is "Click the creature's name to open its wiki page",
            // and until 2026-08-21 this was a plain label (#226, LeBigNasty).
            // Bevel (Helm-signed): the wrong-article case names the way OUT here too.
            ToolTip.SetTip(header, "Open this creature's page on eqlwiki" + (pageStatus == WikiDropStatus.PageIsNotACreature ? " — this one is not the creature's page. Open it, then find the creature's own page." : ""));
            var creature = mob.Name;
            OnClick(header, () => MainWindow.OpenWikiUrl(
                WikiLinks.Creature(_main.WikiMobResult(creature), creature)));
            _mobsPanel.Children.Add(HeaderRow(header, creature, now));

            foreach (var l in mob.Loot)
                _mobsPanel.Children.Add(ItemRow(l, mob.Kills, Status(mob, l)));
        }
        if (mobs.Count == 0)
        {
            _mobsPanel.Children.Add(new TextBlock
            {
                Text = (_filterBox.Text ?? "").Trim().Length > 0
                    ? "Nothing matches that filter."
                    : "No drops recorded this session yet — loot lines name their corpse,\nso every kill you loot shows up here.",
                FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0), Foreground = AppTheme.DimBrush,
            });
        }
    }

    /// <summary>One drop row, wired like the Loot card (David, 2026-08-07: "in the loot
    /// window that we track drops in"): click the name → the item's wiki page, hover →
    /// its stats fetched live, and quest items carry a map badge that opens the Quest
    /// Tracker filtered to the quests that want them.</summary>
    private StackPanel ItemRow(MobLoot l, int kills, WikiDropStatus wikiStatus)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(14, 1, 0, 1),
        };

        var name = new TextBlock
        {
            Text = l.Item, FontSize = 12, Foreground = AppTheme.TextBrush,
        };
        AttachWikiTip(name, l.Item);
        OnClick(name, () => MainWindow.OpenWikiPage(l.Item));
        row.Children.Add(name);

        // Both badges were CLICK-HANDLED EMOJI. Two rules land on that at once: a glyph
        // boxes outright under Wine (#148, #166), and a vector only hit-tests where it is
        // PAINTED, so converting one to a bare Path leaves holes you can click straight
        // through (#211, n3cr0nk1tt3n — on this exact map badge, on the loot rows). An
        // InlineIconButton is the answer to both: drawn at IconInline, hit at
        // IconInlineHit, and keyboard-reachable, which the TextBlocks never were.
        if (_main.IsActiveQuestItem(l.Item))
            row.Children.Add(DesignSystem.InlineIconButton("Map",
                "Part of a quest — click for its quest info",
                () => _main.OpenQuestInfoForItem(l.Item), "GoodBrush"));

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
                _main.ShowWikiPack, "BadBrush"));
        }

        row.Children.Add(new TextBlock
        {
            Text = $"  ×{l.Count}" + (l.DropRatePct is { } pct ? $"  ·  {pct:0.#}% of {kills}" : ""),
            FontSize = 12, Foreground = AppTheme.DimBrush,
        });
        return row;
    }

    private void OnCopyText()
    {
        var (character, server) = _main.Identity;
        TryClipboard(DropsReport.ToText(Filtered(), character, server, _snapshot.SessionStart));
    }

    private void OnCopyCsv() => TryClipboard(DropsReport.ToCsv(Filtered()));

    private async void TryClipboard(string text)
    {
        try
        {
            // The view's OWN TopLevel, not the widget's: it lives in a satellite window
            // now, and a clipboard taken off the wrong top level is a button that does
            // nothing on the one surface a player is actually looking at.
            if (TopLevel.GetTopLevel(Body)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }

    private async void OnSaveCsv()
    {
        try
        {
            if (TopLevel.GetTopLevel(Body) is not { } top) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save drops CSV",
                SuggestedFileName = $"eqbuddy-drops-{DateTime.Now:yyyyMMdd-HHmm}.csv",
                FileTypeChoices =
                [
                    new FilePickerFileType("CSV files") { Patterns = ["*.csv"] },
                    FilePickerFileTypes.All,
                ],
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            await File.WriteAllTextAsync(path, DropsReport.ToCsv(Filtered()));
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    /// <summary>Same live wiki-stats hover as the Quest Tracker's rows: cached block (or
    /// "Looking up…") immediately, one real fetch on first hover rewrites it in place.</summary>
    private void AttachWikiTip(Control target, string itemName)
    {
        var cached = _main.CachedItemStats(itemName);
        var tipText = new TextBlock
        {
            Text = cached ?? "Looking up on eqlwiki…",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new FontFamily("monospace"),
            Foreground = AppTheme.TextBrush,
        };
        ToolTip.SetTip(target, tipText);
        var fetched = false;
        target.AddHandler(ToolTip.ToolTipOpeningEvent, async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            try
            {
                var text = await _main.FetchItemTooltip(itemName);
                tipText.Text = text ?? cached ?? "Not on the wiki.";
            }
            catch (Exception ex) { App.LogError(ex); }
        });
    }

    /// <summary>The creature heading as one row: the clickable name, then the wiki
    /// re-check ↻ and its freshness caption (#226). A Grid, not a StackPanel: a
    /// horizontal stack measures with infinite width and clips the caption silently
    /// (trap 14). The button is an InlineIconButton — never a bare icon with a handler,
    /// which hit-tests only where it is painted (trap 16). Disabled inside the 30 s rule
    /// and visibly dim when it is (trap 17).</summary>
    private Grid HeaderRow(TextBlock name, string creature, DateTime now)
    {
        var lookup = _main.WikiMobResult(creature);
        var inFlight = _main.IsRechecking(creature);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        row.Children.Add(name);

        var recheck = DesignSystem.InlineIconButton("Refresh",
            WikiFreshness.RecheckTip(lookup, inFlight, now),
            () => _main.RecheckMobLookup(creature));
        // LIVE, always — see the WPF twin (Bevel, 2026-08-22). The debounce is the
        // wiki's, not the button's, and the tooltip carries it.
        recheck.IsEnabled = true;
        recheck.VerticalAlignment = VerticalAlignment.Center;
        recheck.Margin = new Thickness(6, 8, 0, 2);
        Grid.SetColumn(recheck, 1);
        row.Children.Add(recheck);

        var caption = new TextBlock
        {
            Text = WikiFreshness.Caption(lookup, inFlight, now),
            FontSize = 11, Foreground = AppTheme.DimBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 8, 0, 2),
        };
        Grid.SetColumn(caption, 2);
        row.Children.Add(caption);
        return row;
    }

    private static void OnClick(TextBlock block, Action action)
    {
        block.Cursor = new Cursor(StandardCursorType.Hand);
        block.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            e.Handled = true;
            action();
        };
    }
}
