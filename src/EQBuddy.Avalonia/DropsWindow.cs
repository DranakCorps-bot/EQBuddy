using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>What the Drops-by-Creature view needs from the shell. Mirrors the WPF
/// MainWindow's surface member-for-member so the integration pass can implement it on
/// the Avalonia MainWindow verbatim and construct the window with <c>this</c>.</summary>
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
    bool IsActiveQuestItem(string name);
    void OpenQuestInfoForItem(string itemName);
    /// <summary>#217 Ask 1: the contribution pack has its own window under Data &amp;
    /// imports now, and the row marker opens it instead of copying silently.</summary>
    void ShowWikiPack();
}

/// <summary>
/// Session drops grouped by source creature, with export (discussion #55 — LeBigNasty
/// tracking Plane of Sky and feeding corrected drop tables back to eqlwiki for revamped
/// zones). Read-only view over StatsSnapshot.Mobs; the filter narrows both the display
/// and what the export buttons emit, so "just the golems" is one filter away.
/// HistoryWindow family: a normal resizable window, not overlay chrome — this is a
/// sit-down review surface, not something that floats over the game.
/// </summary>
public sealed class DropsWindow : Window
{
    private readonly IDropsHost _main;
    private StatsSnapshot _snapshot = new();
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;

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

    public DropsWindow(IDropsHost main)
    {
        _main = main;
        Title = "EQBuddy — Drops by Creature";
        Width = 560;
        Height = 520;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = AppTheme.BgBrush;
        Content = BuildContent();
        WindowZoom.Attach(this, "drops", main.Settings);
    }

    private Control BuildContent()
    {
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

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _mobsPanel,
        };

        // The pack moved to its own window (#217 Ask 1). The marker stays — this view
        // still answers "is this trip worth it" — but the footer says where the paste
        // went, so the button leaving is a signpost rather than a disappearance.
        var footer = AppTheme.DimText(
            "Observed personal drop rates this session — the kill count is the denominator, " +
            "so thin data looks thin. Exports include exactly what the filter shows. " +
            WikiPackPresentation.MovedHint,
            new Thickness(0, 8, 0, 0));

        var layout = new Grid { Margin = new Thickness(10) };
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Children.Add(topRow);
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        return layout;
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

    /// <summary>Wiki knowledge per creature, via MainWindow's target-drops memo (#65):
    /// lookups fire for every creature shown, results land async, and the signature
    /// carries each item's status so rows re-render as answers arrive.</summary>
    private WikiDropStatus Status(MobSummary mob, MobLoot l) =>
        WikiContribution.Classify(_main.WikiMobResult(mob.Name), l.Item);

    private void Render()
    {
        var mobs = Filtered();
        foreach (var m in mobs) _main.EnsureMobLookup(m.Name);
        // The filter leads the signature (small WPF fix): an empty result list used to
        // hash to "", colliding with the reset sentinel — the pane kept its stale rows
        // instead of saying "Nothing matches that filter."
        var sig = (_filterBox.Text ?? "").Trim() + "\u0001" + string.Join("|", mobs.Select(m =>
            $"{m.Name}:{m.Kills}:{string.Join(",", m.Loot.Select(l => $"{l.Item}{l.Count}{(int)Status(m, l)}"))}"));
        if (sig == _signature) return;
        _signature = sig;

        _mobsPanel.Children.Clear();
        var (character, _) = _main.Identity;
        Title = character.Length > 0
            ? $"EQBuddy — Drops by Creature — {character}"
            : "EQBuddy — Drops by Creature";
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
                        _ => "",
                    },
                FontSize = 13, FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 8, 0, 2),
                Foreground = AppTheme.AccentBrush,
            };
            _mobsPanel.Children.Add(header);

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
    /// its stats fetched live, and quest items carry the 🗺 that opens the Quest
    /// Tracker filtered to the quests that want them.</summary>
    private StackPanel ItemRow(MobLoot l, int kills, WikiDropStatus wikiStatus)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(14, 1, 0, 1),
        };
        var isQuest = _main.IsActiveQuestItem(l.Item);

        var name = new TextBlock
        {
            Text = l.Item, FontSize = 12, Foreground = AppTheme.TextBrush,
        };
        AttachWikiTip(name, l.Item);
        OnClick(name, () => MainWindow.OpenWikiPage(l.Item));
        row.Children.Add(name);

        if (isQuest)
        {
            var badge = new TextBlock
            {
                Text = " 🗺", FontSize = 11, Foreground = AppTheme.GoodBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(badge, "Part of a quest — click for its quest info");
            OnClick(badge, () => _main.OpenQuestInfoForItem(l.Item));
            row.Children.Add(badge);
        }

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
            var star = new TextBlock
            {
                Text = " ✦", FontSize = 11, Foreground = AppTheme.BadBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(star, why + " You're holding knowledge eqlwiki.com doesn't have.\n" +
                "\n" +
                "To sync it to the wiki (takes about a minute):\n" +
                "  1. Click this ✦ to open the Wiki contribution pack — it lists everything\n" +
                "     the wiki is missing this session and copies paste-ready edits, built\n" +
                "     from your observed drops and rates. (Data & imports opens it too.)\n" +
                "  2. Click the creature's name to open its wiki page, then Edit (create the\n" +
                "     page if it doesn't exist — the export includes the full page skeleton).\n" +
                "  3. Paste, save, done. The whole community's tracker gets smarter.");
            OnClick(star, _main.ShowWikiPack);
            row.Children.Add(star);
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
            if (Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // clipboard contention: rare, retry by hand
    }

    private async void OnSaveCsv()
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
