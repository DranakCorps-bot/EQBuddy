using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Inventory tab on Linux and macOS — what you actually HAVE, from the game's own
/// <c>/outputfile inventory</c> dump, with two pivots:
///
/// <list type="bullet">
/// <item>BY SLOT is the Gear Locker (#104, Techsteps): everything wearable you own,
/// ranked within each slot, "⬆" for a swap worth making and "⬇ outclassed by X" for a
/// dominance dump candidate — never a taste call, and nothing here is ever "BiS": it
/// ranks YOUR BAGS, not the game. The default, because "what should I swap" is the
/// actionable question.</item>
/// <item>BY BAG is the old Inventory window: where a thing physically is.</item>
/// </list>
///
/// **One tab, because they read the SAME FILE** (David, 2026-08-20: <i>"maybe we can merge
/// Locker and Inventory into the tab Inventory"</i>). Two tabs off one dump made people
/// wonder which one was real. Windows folded them on 2026-08-20 and this build did not, so
/// for one release <see cref="LootSurface.Hosted"/> — SHARED Core vocabulary — named a tab
/// this widget had no body for, and selecting the chip threw. That was guarded rather than
/// fixed; this is the fix.
///
/// **It brings NO ScrollViewer.** The two windows each had one, and carrying it into a tab
/// puts a child scroller inside the host's own — where it is measured with INFINITE height,
/// so it never overflows, never scrolls, and still handles the wheel. On Windows that cost
/// David a working mouse wheel for a day (trap 36). Scrolling belongs to the host.
/// </summary>
internal sealed class InventoryView
{
    private readonly AppSettings _settings;
    private readonly EqlWikiItemService _wikiItems;
    private readonly Func<bool, InventoryFile.Snapshot?> _latestInventory;
    private readonly Func<IReadOnlyList<string>> _pickedClasses;
    private readonly Func<string?> _inferredClass;
    private readonly StackPanel _panel = new();
    private readonly TextBlock _status = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.DimBrush,
    };
    private readonly Button _fetch;
    private readonly CheckBox _byContainer = new();
    private bool _fetching;

    /// <summary>The recipe, spelled out wherever inventory appears — it was a const on the
    /// old Inventory window and three surfaces read it from there.</summary>
    internal const string OutputFileTip =
        "In game, type:   " + GameCommands.OutputfileInventory + "\n" +
        "\n" +
        "The game writes <name>_<server>-Inventory.txt beside its own folders and\n" +
        "EQBuddy reads it — nothing is scanned or injected. Re-type the command any\n" +
        "time your bags change; EQBuddy picks the new file up by itself.";

    /// <summary>What the tab strip shows beside the label: how many swaps the arithmetic
    /// found. Nothing at all before there is a dump — a "0" would read as "you have no
    /// upgrades" when the truth is "EQBuddy has not been told what you own".</summary>
    public string? Badge { get; private set; }

    /// <summary>What the Gear &amp; Loot window hangs in its Inventory tab.</summary>
    public Control Body { get; }

    /// <summary>Its inputs arrive as delegates, where WPF's twin takes the whole
    /// <c>MainWindow</c>. That is deliberate and it is not a style choice: this build has
    /// NO E2E suite (docs/TestPlan.md), so a surface that can only be built from a live
    /// widget is a surface with no cover at all — and the two windows this replaces were
    /// already shaped this way, which is why their render tests survived the fold intact.
    /// The by-slot comparison itself is framework-free in <see cref="GearLocker"/>.</summary>
    public InventoryView(
        AppSettings settings,
        EqlWikiItemService wikiItems,
        Func<bool, InventoryFile.Snapshot?> latestInventory,
        Func<IReadOnlyList<string>> pickedClasses,
        Func<string?> inferredClass)
    {
        _settings = settings;
        _wikiItems = wikiItems;
        _latestInventory = latestInventory;
        _pickedClasses = pickedClasses;
        _inferredClass = inferredClass;
        ToolTip.SetTip(_status, OutputFileTip);

        // A DockPanel's FILL child gets whatever the docked children leave, and docked
        // children take what they ask for — three Dock.Right buttons were ~440px of a
        // 470px window, so the status wrapped ONE CHARACTER PER LINE and the buttons,
        // which stretch to the row height, grew into 380px slabs (David's screenshot,
        // 2026-08-20). Buttons in a WrapPanel, status on its OWN row at full width: no
        // starvation possible at any width, and it survives the lift into a tab.
        var bar = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        var buttons = new WrapPanel();
        var refresh = AppTheme.IconButton("⟳ Refresh", OutputFileTip);
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
        // Every surface that names a command hands it over (David, 2026-08-14) — through
        // the one builder, off GameCommands, never a literal.
        buttons.Children.Add(DesignSystem.CopyCommandButton(
            GameCommands.OutputfileInventory,
            "Copies the command — paste it into the game's chat and the game writes your "
            + "inventory file; EQBuddy reads it on its own. Re-run any time your bags change."));
        bar.Children.Add(buttons);
        _status.Margin = new Thickness(0, 6, 0, 0);
        bar.Children.Add(_status);
        // One tab, two lenses — the same shape the Wishlist tab's "Group by farm zone"
        // already uses, and the reason this is a pivot rather than two stacked sections:
        // concatenating them buries the comparison advice, which is the clever part.
        _byContainer.Content = "Group by bag (where things are)";
        _byContainer.FontSize = 11;
        _byContainer.Margin = new Thickness(0, 6, 0, 0);
        _byContainer.Foreground = AppTheme.DimBrush;
        ToolTip.SetTip(_byContainer,
            "Off: everything wearable you own, ranked within each slot — what to swap and "
            + "what to vendor. On: where each item physically is, bag by bag.");
        _byContainer.IsCheckedChanged += (_, _) => SetByContainer(_byContainer.IsChecked == true);
        bar.Children.Add(_byContainer);

        var root = new StackPanel();
        root.Children.Add(bar);
        root.Children.Add(_panel);
        Body = root;
    }

    private void SetByContainer(bool value)
    {
        if (_settings.InventoryByContainer == value) return;
        _settings.InventoryByContainer = value;
        _settings.Save();
        Render();
    }

    private List<string> _missing = [];

    /// <summary>Items a fetch already tried and could not produce stats for (page missing,
    /// or a knowledge-only page with no stats block): shown honestly, never re-offered —
    /// the fetch button looping over the same unresolvable names while appearing to work
    /// was a silent no-op (2026-08-13 review).</summary>
    private readonly HashSet<string> _unresolvable = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.CancellationTokenSource _closed = new();

    private IReadOnlyList<string> MyClassCodes()
    {
        var picked = _pickedClasses();
        if (picked.Count == 0 && _inferredClass() is { Length: > 0 } inf)
            picked = [inf];
        return picked.Select(GearLocker.Code).ToList();
    }

    public void Render()
    {
        _byContainer.IsChecked = _settings.InventoryByContainer;
        _panel.Children.Clear();
        if (_settings.InventoryByContainer) RenderByContainer();
        else RenderBySlot();
    }

    /// <summary>The BY-SLOT pivot — what to WEAR.</summary>
    private void RenderBySlot()
    {
        var snap = _latestInventory(true);
        if (snap is null)
        {
            // No "and click ⟳" any more: the dump imports itself the moment the game
            // announces it (OutputfileAutoImport). Telling someone to press a button that
            // is no longer required is the same defect as not telling them anything.
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  "
                + "and this fills in on its own. (Hover for the full recipe.)";
            _fetch.IsVisible = false;
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
                : _wikiItems.CachedInfo(name) is { StatsLines.Count: > 0 } info
                    ? ItemStatsBlock.Parse(info.StatsLines) : null,
            MyClassCodes());

        _missing = groups.Where(g => g.Slot == "STATS NOT FETCHED YET")
            .SelectMany(g => g.Rows).Select(r => r.BaseName)
            .Where(n => !_unresolvable.Contains(n)).ToList();
        _fetch.IsVisible = _missing.Count > 0;
        if (!_fetching)
            _fetch.Content = $"⇣ fetch stats for {_missing.Count} item{(_missing.Count == 1 ? "" : "s")}";

        var upgrades = groups.SelectMany(g => g.Rows).Count(r => r.UpgradeOver.Length > 0);
        Badge = upgrades > 0 ? $"{upgrades} upgrade{(upgrades == 1 ? "" : "s")}" : null;

        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — {Age(snap)}"
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

    /// <summary>The BY-BAG pivot — where a thing physically is. Both pivots read the SAME
    /// dump, which is the argument for one tab.</summary>
    private void RenderByContainer()
    {
        var snap = _latestInventory(true);
        _fetch.IsVisible = false;   // the fetch is the by-slot comparison's, not this view's
        if (snap is null)
        {
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  " +
                "and this fills in on its own. (Hover for the full recipe.)";
            return;
        }
        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — written {Age(snap)}"
            + $" — re-type {GameCommands.OutputfileInventory} in game any time; "
            + "EQBuddy picks the new file up by itself.";

        void Header(string text)
        {
            // Small-caps eyebrow in WPF; here the same weight/size in accent — these
            // headers carry real names (which bag), not just structure.
            var tb = AppTheme.Heading(text);
            tb.FontSize = 10.5;
            tb.Margin = new Thickness(0, 9, 0, 2);
            _panel.Children.Add(tb);
        }
        void Row(string left, string right, bool dim = false)
        {
            var g = new Grid { Margin = new Thickness(6, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(110)));
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var l = new TextBlock { Text = left, FontSize = 11.5, Foreground = AppTheme.DimBrush };
            var r = new TextBlock
            {
                Text = right,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = dim ? AppTheme.DimBrush : AppTheme.TextBrush,
            };
            ToolTip.SetTip(r, right);
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

    private static string Age(InventoryFile.Snapshot snap)
    {
        var age = DateTime.Now - snap.WrittenAt;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes}m ago"
            : $"{(int)age.TotalHours}h ago";
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
                // A torn-down surface must not keep fetching invisibly (2026-08-13 review).
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
            if (!_closed.IsCancellationRequested) Render();
        }
    }

    /// <summary>The widget tears this down when it closes. Unlike WPF — where the theme
    /// window owns its own instance and disposes it on close — this view is owned by the
    /// widget and outlives every opening of the window, so the app's exit is the only
    /// moment a running fetch has to stop.</summary>
    public void Dispose() => _closed.Cancel();
}
