using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The character's inventory, from the game's own `/outputfile inventory` dump
/// (David, 2026-08-11): worn gear by slot first, then each bag with its contents,
/// then everything else top-level (bank and kin). Log-only principles hold — the
/// GAME writes the file, EQBuddy just reads it; the header is honest about how old
/// the dump is and how to refresh it (type the command, click ⟳).
/// </summary>
public sealed class InventoryWindow : Window
{
    /// <summary>The trick, spelled out wherever inventory appears (same treatment as
    /// the map's /loc social tip).</summary>
    internal const string OutputFileTip =
        "In game, type:   " + GameCommands.OutputfileInventory + "\n" +
        "\n" +
        "The game writes <name>_<server>-Inventory.txt beside its own folders and\n" +
        "EQBuddy reads it — nothing is scanned or injected. Re-type the command any\n" +
        "time your bags change, then click ⟳ here (the quest tracker's \"held\" tab\n" +
        "uses the same file to spot quests you can already turn in).";

    private readonly Func<bool, InventoryFile.Snapshot?> _latestInventory;
    private readonly StackPanel _panel = new() { Margin = new Thickness(10) };
    private readonly TextBlock _status = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.DimBrush,
    };

    /// <summary>WPF's ctor takes MainWindow and calls main.LatestInventory(refresh);
    /// the Avalonia MainWindow doesn't expose that yet, so the delegate is the input.</summary>
    public InventoryWindow(Func<bool, InventoryFile.Snapshot?> latestInventory)
    {
        _latestInventory = latestInventory;
        Title = "Inventory";
        Width = 420;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppTheme.BgBrush;
        ToolTip.SetTip(_status, OutputFileTip);

        // A DockPanel's FILL child gets whatever the docked children leave, and docked
        // children take what they ask for — three Dock.Right buttons were ~440px of a
        // 470px window, so the status wrapped ONE CHARACTER PER LINE and the buttons,
        // which stretch to the row height, grew into 380px slabs (David's screenshot,
        // 2026-08-20). Trap 14's cousin and invisible in code. WPF twin fixed the same way.
        var bar = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
        var buttons = new WrapPanel();
        var refresh = AppTheme.IconButton("⟳ Refresh", OutputFileTip);
        refresh.Click += (_, _) => Render();
        buttons.Children.Add(refresh);
        // Same one-click command as the quest tracker's held tab (David's ask,
        // 2026-08-11): copy here, paste in the game's chat, click ⟳.
        var copyCmd = AppTheme.IconButton($"⧉ copy  {GameCommands.OutputfileInventory}",
            "Copies the command — paste it into the game's chat and the game " +
            "writes your inventory file; this window reads it. Re-run any time your bags change.");
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

    internal void Render()
    {
        _panel.Children.Clear();
        var snap = _latestInventory(true);
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
            $" (re-type {GameCommands.OutputfileInventory} in game, then ⟳)";

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
}
