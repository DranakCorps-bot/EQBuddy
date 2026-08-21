using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Gear Locker (#104, Techsteps): everything wearable you OWN, grouped by slot,
/// each slot's items compared against each other — "⬇ outclassed by X" marks a
/// dominance dump candidate, never a taste call, and nothing here is ever "BiS":
/// the Locker ranks your bags, not the game. Stats come from the wiki item cache;
/// the one button that fetches missing pages is explicit, counted, and rate-limited
/// (the same one-fetch-per-request etiquette every wiki surface follows).
/// </summary>
public sealed class GearLockerWindow : Window
{
    private readonly EqlWikiItemService _wikiItems;
    private readonly Func<bool, InventoryFile.Snapshot?> _latestInventory;
    private readonly Func<IReadOnlyList<string>> _pickedClasses;
    private readonly Func<string?> _inferredClass;
    private readonly StackPanel _panel = new() { Margin = new Thickness(10) };
    private readonly TextBlock _status = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.DimBrush,
    };
    private readonly Button _fetch;
    private bool _fetching;

    /// <summary>WPF's ctor takes MainWindow; the Avalonia MainWindow doesn't expose the
    /// wiki service, inventory, or the quest ledger's class picks yet, so those inputs
    /// arrive as delegates (picked classes from the ledger; inferred class as fallback).</summary>
    public GearLockerWindow(EqlWikiItemService wikiItems,
        Func<bool, InventoryFile.Snapshot?> latestInventory,
        Func<IReadOnlyList<string>> pickedClasses,
        Func<string?> inferredClass)
    {
        _wikiItems = wikiItems;
        _latestInventory = latestInventory;
        _pickedClasses = pickedClasses;
        _inferredClass = inferredClass;
        Title = "Gear Locker";
        Width = 470;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppTheme.BgBrush;
        ToolTip.SetTip(_status, InventoryWindow.OutputFileTip);

        // A DockPanel's FILL child gets whatever the docked children leave, and docked
        // children take what they ask for — three Dock.Right buttons were ~440px of a
        // 470px window, so the status wrapped ONE CHARACTER PER LINE and the buttons,
        // which stretch to the row height, grew into 380px slabs (David's screenshot,
        // 2026-08-20). Trap 14's cousin and invisible in code. WPF twin fixed the same way.
        var bar = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
        var buttons = new WrapPanel();
        var refresh = AppTheme.IconButton("⟳ Refresh", InventoryWindow.OutputFileTip);
        refresh.Click += (_, _) => Render();
        buttons.Children.Add(refresh);
        _fetch = AppTheme.IconButton("",
            "Fetches the wiki page for each owned item that has no cached stats "
            + "yet — one page at a time, politely spaced, cached for a week. Rows fill in "
            + "as pages arrive.");
        _fetch.FontSize = 11;
        _fetch.Margin = new Thickness(0, 0, 6, 0);
        _fetch.Click += async (_, _) => await FetchMissing();
        buttons.Children.Add(_fetch);
        // Same one-click command as the Inventory window and the quest tracker's
        // held tab (David, 2026-08-14): copy, paste in the game's chat, click ⟳.
        var copyCmd = AppTheme.IconButton($"⧉ copy  {GameCommands.OutputfileInventory}",
            "Copies the command — paste it into the game's chat and the game "
            + "writes your inventory file; the Locker reads it. Re-run any time your bags change.");
        copyCmd.FontSize = 11;
        copyCmd.Margin = new Thickness(0, 0, 6, 0);
        copyCmd.Click += async (_, _) =>
        {
            try
            {
                await (Clipboard?.SetTextAsync(GameCommands.OutputfileInventory) ?? Task.CompletedTask);
                copyCmd.Content = "✓ copied — paste in game chat";
            }
            catch (Exception ex) { App.LogError(ex); }   // clipboard momentarily held by another app
        };
        buttons.Children.Add(copyCmd);
        bar.Children.Add(buttons);
        _status.Margin = new Thickness(0, 6, 0, 0);
        bar.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer
        {
            Content = _panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Content = root;
        Render();
    }

    private List<string> _missing = [];
    /// <summary>Items a fetch already tried and could not produce stats for (page
    /// missing, or a knowledge-only page with no stats block): shown honestly, never
    /// re-offered — the fetch button looping over the same unresolvable names while
    /// appearing to work was a silent no-op (2026-08-13 review).</summary>
    private readonly HashSet<string> _unresolvable = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.CancellationTokenSource _closed = new();

    private IReadOnlyList<string> MyClassCodes()
    {
        var picked = _pickedClasses();
        if (picked.Count == 0 && _inferredClass() is { Length: > 0 } inf)
            picked = [inf];
        return picked.Select(GearLocker.Code).ToList();
    }

    internal void Render()
    {
        _panel.Children.Clear();
        var snap = _latestInventory(true);
        if (snap is null)
        {
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  "
                + "and this fills in on its own. (Hover for the full recipe.)";
            _fetch.IsVisible = false;
            return;
        }

        // The embedded catalog answers first with the build tool's own STRUCTURED
        // numbers — no text round-trip, and the catalog≡live-parse guarantee holds
        // (2026-08-13 review). A genuinely fetched live page covers the rest.
        var groups = GearLocker.Build(snap.Entries,
            name => ItemCatalog.Default.Find(name) is { } rec
                    && (rec.Slots.Count > 0 || rec.StatsText.Length > 0)
                ? rec.ToStatsBlock()
                : _wikiItems.CachedInfo(name) is { StatsLines.Count: > 0 } info
                    ? ItemStatsBlock.Parse(info.StatsLines) : null,
            MyClassCodes());

        _missing = groups.Where(g => g.Slot == "STATS NOT FETCHED YET")
            .SelectMany(g => g.Rows).Select(r => r.BaseName)
            .Where(n => !_unresolvable.Contains(n)).ToList();
        _fetch.IsVisible = _missing.Count > 0;
        if (!_fetching)
            _fetch.Content = $"⇣ fetch stats for {_missing.Count} item{(_missing.Count == 1 ? "" : "s")}";

        var age = DateTime.Now - snap.WrittenAt;
        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — "
            + (age.TotalMinutes < 1 ? "just now" : age.TotalHours < 1
                ? $"{(int)age.TotalMinutes}m ago" : $"{(int)age.TotalHours}h ago")
            + ". Comparisons use wiki BASE stats — a +N raises them in-game, so upgrades "
            + "are shown, never folded in.";

        foreach (var group in groups)
        {
            // Small-caps eyebrow in WPF (SectionLabel); same weight/size in accent here.
            var header = AppTheme.Heading(group.Slot is "STATS NOT FETCHED YET"
                ? $"{group.Slot} ({group.Rows.Count})"
                : group.Slot);
            header.FontSize = 10.5;
            header.Margin = new Thickness(0, 9, 0, 2);
            _panel.Children.Add(header);

            foreach (var row in group.Rows)
            {
                var line = new StackPanel { Margin = new Thickness(6, 0, 0, 3) };
                var name = new TextBlock
                {
                    Text = (row.Worn ? "★ " : row.UpgradeOver.Length > 0 ? "⬆ " : "")
                        + (row.Count > 1 ? $"{row.Name} ×{row.Count}" : row.Name),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = row.UpgradeOver.Length > 0 ? AppTheme.GoodBrush
                        : row.OutclassedBy.Length > 0 ? AppTheme.DimBrush : AppTheme.TextBrush,
                };
                if (_wikiItems.CachedStatsText(row.BaseName) is { } tip)
                    ToolTip.SetTip(name, tip);
                line.Children.Add(name);

                var detailParts = new List<string> { row.Where };
                if (row.StatLine.Length > 0) detailParts.Add(row.StatLine);
                if (row.ClassNote.Length > 0) detailParts.Add(row.ClassNote);
                if (row.UpgradeOver.Length > 0) detailParts.Add($"⬆ upgrade over worn {row.UpgradeOver}");
                if (row.OutclassedBy.Length > 0) detailParts.Add($"⬇ outclassed by {row.OutclassedBy}");
                if (_unresolvable.Contains(row.BaseName))
                    detailParts.Add("no stats on its wiki page — nothing to fetch");
                var detail = new TextBlock
                {
                    Text = string.Join("  ·  ", detailParts),
                    FontSize = 10.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = row.UpgradeOver.Length > 0 ? AppTheme.GoodBrush
                        : row.OutclassedBy.Length > 0 ? AppTheme.WarnBrush : AppTheme.DimBrush,
                };
                ToolTip.SetTip(detail, string.Join("\n", detailParts));
                line.Children.Add(detail);
                _panel.Children.Add(line);
            }
        }

        _panel.Children.Add(AppTheme.DimText(
            "★ = what you're wearing. ⬆ = something in your bags beats it on every stat "
            + "both carry — the swap worth making. \"Outclassed\" is the same test among "
            + "everything you own: a dump candidate by arithmetic, not taste. Items "
            + "class-locked away from you never outclass anything of yours, and a plain "
            + "item never claims to beat a worn \"+N\" — the wiki lists base stats, so an "
            + "upgraded item reads lower here than it really is.",
            new Thickness(0, 8, 0, 0)));
    }

    private async Task FetchMissing()
    {
        if (_fetching || _missing.Count == 0) return;
        _fetching = true;
        try
        {
            var total = _missing.Count;
            for (var i = 0; i < _missing.Count; i++)
            {
                // A closed window must not keep fetching invisibly (2026-08-13
                // review) — the loop dies with the window.
                if (_closed.IsCancellationRequested) return;
                var name = _missing[i];
                _fetch.Content = $"⇣ fetching {i + 1}/{total}…";
                var result = await _wikiItems.LookupAsync(name);
                // No stats after a real fetch = this page will never resolve here;
                // remember that instead of offering the same fetch forever.
                if (result.Item is not { StatsLines.Count: > 0 })
                    _unresolvable.Add(name);
                await Task.Delay(400, _closed.Token).ContinueWith(_ => { });   // polite pacing
            }
        }
        finally
        {
            _fetching = false;
            if (!_closed.IsCancellationRequested && IsLoaded) Render();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed.Cancel();
        base.OnClosed(e);
    }
}
