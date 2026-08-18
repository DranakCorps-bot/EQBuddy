using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The Loot breakout's contents, on the design system (Gate 4, docs/DesignSystem.md §11.5)
/// — the minimized window David asked for on 2026-08-06, Target | Session toggled.
///
/// Lifted out of <c>BreakoutWindow</c> for the same reason the card was lifted out of the
/// widget: that window serves six kinds and cannot join the ratchet until the last of them
/// is migrated (Gate 8). Here, the two filter strips are held to the scale now.
///
/// It does NOT own the window chrome — the title row, the Target|Session toggle, the
/// subheader, the empty line, the scroller and the size grip are shared by all six kinds
/// and change together, later. What it owns is everything Loot-specific: the strips, the
/// row list, and the decision of what to show, which comes from
/// <see cref="LootPresentation"/> — the same call the card makes, so the two surfaces
/// cannot answer the question differently.
/// </summary>
internal sealed class LootBreakoutView
{
    private readonly BreakoutWindow _w;
    private readonly AppSettings _settings;

    private readonly StackPanel _viewGroup = Group();
    private readonly StackPanel _sortGroup = Group();
    private readonly EqSegmentedStrip _views;
    private readonly EqSegmentedStrip _sorts;

    /// <summary>The filter row — what <c>BreakoutWindow.xaml</c> hangs above the list.
    /// Hidden wholesale in Target scope, which is a different axis entirely.</summary>
    public UIElement Strips { get; }

    public LootBreakoutView(BreakoutWindow w, AppSettings settings)
    {
        _w = w;
        _settings = settings;

        // The visibility and the spacing both belong to THIS panel, not to the host it
        // hangs in: a host that hides itself is a second switch, and the first screenshot
        // of this window found exactly that — the strips were built, selected and never
        // once shown, because the ContentControl around them stayed collapsed.
        var strips = new WrapPanel
        {
            Margin = new Thickness(0, 0, DesignTokens.SpaceXxs, DesignTokens.SpaceXxs),
        };
        // SpaceM, not the card's SpaceL: horizontal room is this window's scarcest
        // resource, and the group frames already separate the two strips.
        _viewGroup.Margin = new Thickness(0, 0, DesignTokens.SpaceM, 0);
        strips.Children.Add(_viewGroup);
        strips.Children.Add(_sortGroup);
        Strips = strips;

        _views = BuildStrip(_viewGroup, "show:", LootPresentation.Views, key =>
        {
            _settings.LootView = key;
            _settings.Save();
            Repaint();
        });
        _sorts = BuildStrip(_sortGroup, "sort:", LootPresentation.Sorts, key =>
        {
            _settings.LootSort = key;
            _settings.Save();
            Repaint();
        });
    }

    private static StackPanel Group() => new() { Orientation = Orientation.Horizontal };

    private static EqSegmentedStrip BuildStrip(Panel group, string label,
        IReadOnlyList<LootPresentation.Option> options, Action<string> onPick)
    {
        // Compact segments in the Target|Session toggle's own vocabulary (LW,
        // 2026-08-18): the card-scale pill crowded this ~270px window, and the window's
        // chrome already established what a dense toggle looks like here — flush
        // segments inside one hairline frame, the selected one washed and accented.
        var caption = DesignSystem.Text(ChipStyle.CompactLabelRole, label);
        // Semibold: "show:" and "sort:" are labels, and at metadata size the weight is
        // what separates them from the options they govern (LW, 2026-08-18).
        caption.FontWeight = FontWeights.SemiBold;
        caption.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;
        group.Children.Add(caption);

        var host = new StackPanel { Orientation = Orientation.Horizontal };
        var frame = new Border
        {
            Child = host,
            CornerRadius = new CornerRadius(ChipStyle.CompactRadius),
            BorderThickness = new Thickness(ChipStyle.BorderThickness),
            VerticalAlignment = VerticalAlignment.Center,
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        group.Children.Add(frame);

        var strip = new EqSegmentedStrip(host, compact: true);
        foreach (var option in options)
        {
            var key = option.Key;
            strip.Add(option.Label, key, tip: option.Tip, onClick: () => onPick(key));
        }
        return strip;
    }

    /// <summary>Both settings are shared with the Loot card, so a click here moves that
    /// card too — on its next tick. This window repaints NOW rather than waiting up to a
    /// second: a reorder the player asked for should feel instant.</summary>
    private void Repaint()
    {
        _w.Signature = "";
        if (_w.LastSnapshot is { } s) Render(s);
    }

    /// <summary>Target = what the creature you're fighting (or last /considered) can drop,
    /// your observed counts leading and the wiki's behind. Session = what you've looted,
    /// through the same filters as the card. Hovering a row fetches the eqlwiki item info
    /// on the spot; clicking opens the page.</summary>
    public void Render(StatsSnapshot s)
    {
        List<LootRow> rows;
        string emptyText;

        if (_w.TargetScope)
        {
            // The show/sort strips belong to the Session view; Target is a different axis.
            Strips.Visibility = Visibility.Collapsed;
            var (names, detail, targetRows) = _w.Main?.TargetDropsContent(s) ?? ("", "", []);
            var hasTarget = names.Length > 0;
            _w.SubText.Text = hasTarget
                ? LootPresentation.TargetSubtitle(names, detail)
                : "No target";
            rows = targetRows.Select(t => new LootRow(t.Name, t.Value, null)).ToList();
            emptyText = hasTarget
                ? _w.Main?.TargetEmptyNote(s) ?? "Nothing known for this creature yet."
                : "Swing at something — or /consider it — and its\npossible drops appear here.";
        }
        else
        {
            // "+N made" echoes the card header (merges + crafts) from the same composer,
            // so the two windows cannot report different totals (#131).
            _w.SubText.Text = LootPresentation.BreakoutSubtitle(
                s.LootTotal, s.CraftedTotal + s.FashionedTotal);

            var plan = LootPresentation.Build(s.Loot, s.Crafted, s.Fashioned, s.RecentLoot,
                _settings.LootView, _settings.LootSort);
            Strips.Visibility = Visibility.Visible;
            _viewGroup.Visibility = plan.ShowViewStrip ? Visibility.Visible : Visibility.Collapsed;
            _sortGroup.Visibility = plan.ShowSortStrip ? Visibility.Visible : Visibility.Collapsed;
            _views.Select(plan.View);
            _sorts.Select(plan.Sort);
            if (_sorts.Chip(LootPresentation.SortRecent) is { } recent)
                recent.Visibility = plan.ShowRecent ? Visibility.Visible : Visibility.Collapsed;

            rows = [.. plan.Rows];
            emptyText = plan.EmptyNote ?? "";
        }

        _w.EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
        {
            _w.EmptyText.Text = emptyText;
            _w.Rows.Items.Clear();
            _w.Signature = "";
            return;
        }

        var sig = $"loot|{_w.TargetScope}|{_settings.LootView}|{_settings.LootSort}|{_w.SubText.Text}|"
            + string.Join(",", rows.Select(r => r.Item + r.Value + r.Tag));
        if (sig == _w.Signature) return;
        _w.Signature = sig;

        _w.Rows.Items.Clear();
        foreach (var r in rows) _w.Rows.Items.Add(BuildRow(r));
    }

    /// <summary>An item row wired the way David specced this window: hover = the eqlwiki
    /// item info, fetched on the spot when the cache is empty (the tooltip live-updates
    /// from "Looking up…"); click = the page in the browser. The row itself IS the
    /// card's (LootCardView.ItemRow — LW, 2026-08-18): flat, badge against the value,
    /// no underline track, so one item cannot look like two different things in two
    /// windows. Only what hover and click DO differs.</summary>
    private Grid BuildRow(LootRow r)
    {
        var cachedTip = _w.Main?.CachedItemStats(r.Item);
        var tipText = new TextBlock
        {
            Text = cachedTip ?? "Looking up on eqlwiki…",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = DesignTokens.TipWidth,
            FontFamily = MainWindow.MonoFamily,
        };
        var tip = new ToolTip { Content = tipText };

        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            // Fetch once per row lifetime; a cache hit inside FetchItemTooltip is free.
            if (fetched || _w.Main is not { } m) return;
            fetched = true;
            var text = await m.FetchItemTooltip(r.Item);
            tipText.Text = text ?? (cachedTip ?? "Not on the wiki.");
        };

        var row = LootCardView.ItemRow(_w.Main, r.Item, r.Value, LootPresentation.Note(r.Tag),
            tip, item => MainWindow.OpenWikiPage(item));
        // A whisper of right inset the card never needs: these rows sit against the
        // window's own scrollbar when the list overflows.
        row.Margin = new Thickness(0, 0, DesignTokens.SpaceXxs, 0);
        return row;
    }

}
