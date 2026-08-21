using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The Gear Locker (#104, Techsteps): everything wearable you OWN, grouped by slot,
/// each slot's items compared against each other — "⬇ outclassed by X" marks a
/// dominance dump candidate, never a taste call, and nothing here is ever "BiS":
/// the Locker ranks your bags, not the game. Stats come from the wiki item cache;
/// the one button that fetches missing pages is explicit, counted, and rate-limited
/// (the same one-fetch-per-request etiquette every wiki surface follows).
/// </summary>
/// <remarks>
/// A TAB of the Gear &amp; Loot theme since 2026-08-20, not a window. David opened the
/// theme expecting his gear and found only a wishlist: <i>"We should at least put our gear
/// locker (what we're wearing) into this window so Gear and Loot can complete a theme."</i>
/// The roadmap always said so — that theme row lists GearLocker among what it absorbs.
///
/// Lifted the way <c>GearCardView</c> was: it builds its own body, so the same class can
/// hang in a tab without a window around it. The comparison logic was already
/// framework-free in <c>UI.Shared/GearLocker.cs</c>, which is why this was a lift and not
/// a rewrite.
/// </remarks>
internal sealed class InventoryView : IWidgetCard
{
    public string Key => LootSurface.KeyFor(LootTab.Inventory);
    public UIElement Body { get; }

    /// <summary>The tab strip's headline: how many swaps the arithmetic found. Nothing at
    /// all when there is no dump yet — a "0" would read as "you have no upgrades" when the
    /// truth is "EQBuddy has not been told what you own".</summary>
    public string? Badge { get; private set; }

    public void Render(StatsSnapshot snapshot) => Render();

    private readonly MainWindow _main;
    private readonly StackPanel _panel = new();
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly Button _fetch;
    private bool _fetching;
    private readonly CheckBox _byContainer = new();

    /// <summary>The recipe, in one place: it was a const on the old Inventory window and
    /// three surfaces read it from there.</summary>
    internal const string OutputFileTip =
        "In game, type:   " + GameCommands.OutputfileInventory + "\n" +
        "The game writes it beside its own folders, and EQBuddy reads it on its own.";

    private void SetByContainer(bool value)
    {
        if (_main.Settings.InventoryByContainer == value) return;
        _main.Settings.InventoryByContainer = value;
        _main.Settings.Save();
        Render();
    }


    public InventoryView(MainWindow main)
    {
        _main = main;
        _status.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        _status.ToolTip = OutputFileTip;

        // A DockPanel's FILL child gets whatever the docked children leave, and docked
        // children take whatever they ask for. Three Dock.Right buttons here are ~440px
        // of a 470px window, so _status got ~30 and wrapped ONE CHARACTER PER LINE — and
        // because a docked child stretches to the row's height, the buttons then grew
        // into 380px-tall slabs (David's screenshot, 2026-08-20). Trap 14's cousin: a
        // panel whose measurement rule starves a text child, invisible in the code.
        // It only appeared when the fetch button was visible, which is why it survived.
        // Buttons in a WrapPanel, status on its OWN row at full width: no starvation
        // possible at any window size, and it survives the lift into a tab.
        var bar = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        var buttons = new WrapPanel();
        var refresh = Theming.Button("⟳ Refresh");
        refresh.ToolTip = OutputFileTip;
        refresh.Click += (_, _) => Render();
        buttons.Children.Add(refresh);
        _fetch = Theming.Button("");
        _fetch.FontSize = 11;
        _fetch.Margin = new Thickness(0, 0, 6, 0);
        _fetch.ToolTip = "Fetches the wiki page for each owned item that has no cached stats "
            + "yet — one page at a time, politely spaced, cached for a week. Rows fill in "
            + "as pages arrive.";
        _fetch.Click += async (_, _) => await FetchMissing();
        buttons.Children.Add(_fetch);
        // Same one-click command as the Inventory window and the quest tracker's
        // held tab (David, 2026-08-14): copy, paste in the game's chat, click ⟳.
        var copyCmd = Theming.WireCopyCommand(Theming.Button(""), GameCommands.OutputfileInventory);
        copyCmd.FontSize = 11;
        copyCmd.Margin = new Thickness(0, 0, 6, 0);
        copyCmd.ToolTip = "Copies the command — paste it into the game's chat and the game "
            + "writes your inventory file; the Locker reads it. Re-run any time your bags change.";
        buttons.Children.Add(copyCmd);
        // One tab, two lenses — the same shape the Wishlist tab's "Group by farm zone"
        // already uses, and the reason this is a pivot rather than two stacked sections:
        // concatenating them buries the comparison advice, which is the clever part.
        _byContainer.Content = "Group by bag (where things are)";
        _byContainer.FontSize = 11;
        _byContainer.Margin = new Thickness(0, 6, 0, 0);
        _byContainer.SetResourceReference(Control.ForegroundProperty, "DimBrush");
        _byContainer.ToolTip = "Off: everything wearable you own, ranked within each slot — "
            + "what to swap and what to vendor. On: where each item physically is, bag by bag.";
        _byContainer.Checked += (_, _) => SetByContainer(true);
        _byContainer.Unchecked += (_, _) => SetByContainer(false);
        bar.Children.Add(buttons);
        _status.Margin = new Thickness(0, 6, 0, 0);
        bar.Children.Add(_status);
        bar.Children.Add(_byContainer);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer
        {
            Content = _panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        Body = root;
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
        var picked = _main.QuestLedger?.ClassesFor(_main.QuestCharacterKey) ?? [];
        if (picked.Count == 0 && _main.CurrentSnapshot().InferredClass is { Length: > 0 } inf)
            picked = [inf];
        return picked.Select(GearLocker.Code).ToList();
    }

    public void Render()
    {
        _byContainer.IsChecked = _main.Settings.InventoryByContainer;
        if (_main.Settings.InventoryByContainer) RenderByContainer();
        else RenderBySlot();
    }

    /// <summary>The BY-SLOT pivot — what to WEAR. Ranked within each slot, with the
    /// upgrade and outclassed marks; the default, because "what should I swap" is the
    /// actionable question and "where is my stuff" is the occasional lookup.</summary>
    private void RenderBySlot()
    {
        _panel.Children.Clear();
        var snap = _main.LatestInventory(refresh: true);
        if (snap is null)
        {
            // No "and click ⟳" any more: the dump imports itself the moment the game
            // announces it (OutputfileAutoImport). Telling someone to press a button that
            // is no longer required is the same defect as not telling them anything.
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  "
                + "and this fills in on its own. (Hover for the full recipe.)";
            _fetch.Visibility = Visibility.Collapsed;
            Badge = null;
            return;
        }

        // The embedded catalog answers first with the build tool's own STRUCTURED
        // numbers — no text round-trip, and the catalog≡live-parse guarantee holds
        // (2026-08-13 review). A genuinely fetched live page covers the rest.
        var groups = GearLocker.Build(snap.Entries,
            name => ItemCatalog.Default.Find(name) is { } rec
                    && (rec.Slots.Count > 0 || rec.StatsText.Length > 0)
                ? rec.ToStatsBlock()
                : _main.WikiItems.CachedInfo(name) is { StatsLines.Count: > 0 } info
                    ? ItemStatsBlock.Parse(info.StatsLines) : null,
            MyClassCodes());

        _missing = groups.Where(g => g.Slot == "STATS NOT FETCHED YET")
            .SelectMany(g => g.Rows).Select(r => r.BaseName)
            .Where(n => !_unresolvable.Contains(n)).ToList();
        _fetch.Visibility = _missing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!_fetching)
            _fetch.Content = $"⇣ fetch stats for {_missing.Count} item{(_missing.Count == 1 ? "" : "s")}";

        var upgrades = groups.SelectMany(g => g.Rows).Count(r => r.UpgradeOver.Length > 0);
        Badge = upgrades > 0 ? $"{upgrades} upgrade{(upgrades == 1 ? "" : "s")}" : null;

        var age = DateTime.Now - snap.WrittenAt;
        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — "
            + (age.TotalMinutes < 1 ? "just now" : age.TotalHours < 1
                ? $"{(int)age.TotalMinutes}m ago" : $"{(int)age.TotalHours}h ago")
            + ". Comparisons use wiki BASE stats — a +N raises them in-game, so upgrades "
            + "are shown, never folded in.";

        foreach (var group in groups)
        {
            var header = new TextBlock
            {
                Text = group.Slot is "STATS NOT FETCHED YET"
                    ? $"{group.Slot} ({group.Rows.Count})"
                    : group.Slot,
                Margin = new Thickness(0, 9, 0, 2),
            };
            // SetResourceReference, not FindResource: a view has no window to look up
            // through, and a lookup that silently returns nothing renders a heading as
            // body text with no error anywhere (trap 19).
            header.SetResourceReference(FrameworkElement.StyleProperty, "SectionLabel");
            header.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
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
                };
                name.SetResourceReference(TextBlock.ForegroundProperty,
                    row.UpgradeOver.Length > 0 ? "GoodBrush"
                    : row.OutclassedBy.Length > 0 ? "DimBrush" : "TextBrush");
                if (_main.WikiItems.CachedStatsText(row.BaseName) is { } tip)
                    name.ToolTip = tip;
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
                    ToolTip = string.Join("\n", detailParts),
                };
                detail.SetResourceReference(TextBlock.ForegroundProperty,
                    row.UpgradeOver.Length > 0 ? "GoodBrush"
                    : row.OutclassedBy.Length > 0 ? "WarnBrush" : "DimBrush");
                line.Children.Add(detail);
                _panel.Children.Add(line);
            }
        }

        var foot = new TextBlock
        {
            Text = "★ = what you're wearing. ⬆ = something in your bags beats it on every "
                + "stat both carry — the swap worth making. \"Outclassed\" is the same test "
                + "among everything you own: a dump candidate by arithmetic, not taste. "
                + "Items class-locked away from you never outclass anything of yours, and a "
                + "plain item never claims to beat a worn \"+N\" — the wiki lists base stats, "
                + "so an upgraded item reads lower here than it really is.",
            FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        };
        foot.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        _panel.Children.Add(foot);
    }

    /// <summary>The BY-BAG pivot — where a thing physically is. Lifted from the old
    /// Inventory window when the two surfaces became one tab (David, 2026-08-20:
    /// "maybe we can merge Locker and Inventory into the tab Inventory"). Both read
    /// the SAME dump, which is the argument for one tab: two tabs off one file makes
    /// people wonder which is the real one.</summary>
    private void RenderByContainer()
    {
        var snap = _main.LatestInventory(refresh: true);
        if (snap is null)
        {
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  " +
                "and this fills in on its own. (Hover for the full recipe.)";
            return;
        }
        var age = DateTime.Now - snap.WrittenAt;
        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — written " +
            (age.TotalMinutes < 1 ? "just now" : age.TotalHours < 1
                ? $"{(int)age.TotalMinutes}m ago" : $"{(int)age.TotalHours}h ago") +
            $" — re-type {GameCommands.OutputfileInventory} in game any time; "
            + "EQBuddy picks the new file up by itself.";

        void Header(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 9, 0, 2),
            };
            // SetResourceReference, not FindResource: a view has no window to look
            // up through, and a silent miss renders a heading as body text (trap 19).
            tb.SetResourceReference(FrameworkElement.StyleProperty, "SectionLabel");
            // Small-caps eyebrow, but in accent: these headers carry real names
            // (which bag), not just structure.
            tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            _panel.Children.Add(tb);
        }
        void Row(string left, string right, bool dim = false)
        {
            var g = new Grid { Margin = new Thickness(6, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = new TextBlock { Text = left, FontSize = 11.5 };
            l.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            var r = new TextBlock
            {
                Text = right, FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = right,
            };
            r.SetResourceReference(TextBlock.ForegroundProperty, dim ? "DimBrush" : "TextBrush");
            Grid.SetColumn(r, 1);
            g.Children.Add(l);
            g.Children.Add(r);
            _panel.Children.Add(g);
        }

        // What the log saw AFTER the dump was written: these counts already feed the
        // quest tracker's held tab, but the bag structure below is the dump's — the
        // log knows what you gained, not which bag you put it in.
        var gained = snap.SinceDump.Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
        if (gained.Count > 0)
        {
            Header($"Looted since this dump ({gained.Count})");
            foreach (var (item, n) in gained)
                Row("", n > 1 ? $"{item} ×{n}" : item);
        }

        var containers = snap.Entries.Where(e => e.InContainer)
            .GroupBy(e => e.ContainerSlot, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var topLevel = snap.Entries.Where(e => !e.InContainer).ToList();

        // Worn gear: top-level slots that aren't bags-with-contents or bank rows.
        Header("Worn");
        foreach (var e in topLevel.Where(e =>
                     !containers.ContainsKey(e.Location)
                     && !e.Location.StartsWith("Bank", StringComparison.OrdinalIgnoreCase)
                     && !e.Location.StartsWith("General", StringComparison.OrdinalIgnoreCase)))
            Row(e.Location, e.Count > 1 ? $"{e.Name} ×{e.Count}" : e.Name);

        // Bags (and any other container), each with its contents.
        foreach (var e in topLevel.Where(e => containers.ContainsKey(e.Location)))
        {
            var contents = containers[e.Location];
            Header($"{e.Name}  ({e.Location} — {contents.Count} item{(contents.Count == 1 ? "" : "s")})");
            foreach (var c in contents)
                Row("", c.Count > 1 ? $"{c.Name} ×{c.Count}" : c.Name);
        }

        // Anything else top-level (bank slots, loose General rows without children).
        var rest = topLevel.Where(e =>
            !containers.ContainsKey(e.Location)
            && (e.Location.StartsWith("Bank", StringComparison.OrdinalIgnoreCase)
                || e.Location.StartsWith("General", StringComparison.OrdinalIgnoreCase))).ToList();
        if (rest.Count > 0)
        {
            Header("Elsewhere");
            foreach (var e in rest)
                Row(e.Location, e.Count > 1 ? $"{e.Name} ×{e.Count}" : e.Name, dim: true);
        }
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
                var result = await _main.WikiItems.LookupAsync(name);
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
            if (!_closed.IsCancellationRequested) Render();
        }
    }

    /// <summary>The host tears the view down when its window closes: a fetch loop must not
    /// keep running invisibly (2026-08-13 review), and the view outlives no window now.</summary>
    public void Dispose() => _closed.Cancel();
}
