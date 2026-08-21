using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

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

    private readonly MainWindow _main;
    private readonly StackPanel _panel = new() { Margin = new Thickness(10) };
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };

    public InventoryWindow(MainWindow main)
    {
        _main = main;
        Title = "Inventory";
        Width = 420;
        Height = 620;
        Owner = main;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BgBrush");
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
        var bar = new StackPanel { Margin = new Thickness(10, 8, 10, 0) };
        var buttons = new WrapPanel();
        var refresh = Theming.Button("⟳ Refresh");
        refresh.ToolTip = OutputFileTip;
        refresh.Click += (_, _) => Render();
        buttons.Children.Add(refresh);
        // Same one-click command as the quest tracker's held tab (David's ask,
        // 2026-08-11): copy here, paste in the game's chat, click ⟳.
        var copyCmd = Theming.WireCopyCommand(Theming.Button(""), GameCommands.OutputfileInventory);
        copyCmd.FontSize = 11;
        copyCmd.Margin = new Thickness(0, 0, 6, 0);
        copyCmd.ToolTip = "Copies the command — paste it into the game's chat and the game " +
            "writes your inventory file; this window reads it. Re-run any time your bags change.";
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

    private void Render()
    {
        _panel.Children.Clear();
        var snap = _main.LatestInventory(refresh: true);
        if (snap is null)
        {
            _status.Text = $"No inventory dump found yet — in game, type  {GameCommands.OutputfileInventory}  " +
                "and click ⟳. (Hover for the full recipe.)";
            return;
        }
        var age = DateTime.Now - snap.WrittenAt;
        _status.Text = $"{System.IO.Path.GetFileName(snap.Path)} — written " +
            (age.TotalMinutes < 1 ? "just now" : age.TotalHours < 1
                ? $"{(int)age.TotalMinutes}m ago" : $"{(int)age.TotalHours}h ago") +
            $" (re-type {GameCommands.OutputfileInventory} in game, then ⟳)";

        void Header(string text)
        {
            var tb = new TextBlock
            {
                Text = text, Style = (Style)FindResource("SectionLabel"),
                Margin = new Thickness(0, 9, 0, 2),
            };
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
}
