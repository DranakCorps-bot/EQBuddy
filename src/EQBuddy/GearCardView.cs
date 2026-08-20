using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The widget's Gear card: an imported EQ Legends Tools shopping list as a checklist,
/// grouped either by kind (gear vs exaltations) or by the zone you would farm it in.
///
/// Lifted out of <c>MainWindow</c> for the Loot &amp; Items theme (docs/Themes.md), and it
/// is the clean case CLAUDE.md describes: the whole surface touched settings, five named
/// controls and one repaint flag, with the grouping and the rollup already living in
/// UI.Shared where they are tested. Its rendered shape was pinned in
/// <c>tests/EQBuddy.E2E</c> BEFORE the move — the WPF layer has no unit tests, so that
/// assertion is the only thing between this lift and a silent regression.
///
/// The two pivots are not symmetrical, and both halves matter:
/// <list type="bullet">
/// <item>By KIND is everything you imported, acquired or not — the list as a list.</item>
/// <item>By ZONE excludes what you already have and repeats an item under every zone it
/// drops in, because the question it answers is "where do I go next", not "what do I
/// own".</item>
/// </list>
/// </summary>
internal sealed class GearCardView
{
    private readonly AppSettings _settings;
    private readonly ItemsControl _list;
    private readonly TextBlock _listName;
    private readonly CheckBox _byZone;
    private readonly TextBlock _header;
    private readonly Func<string> _currentZone;
    private readonly Func<string, string, int?> _hops;
    private readonly Action _markDirty;
    private readonly Func<string, object> _brush;

    public GearCardView(
        AppSettings settings,
        ItemsControl list, TextBlock listName, CheckBox byZone, TextBlock header,
        Func<string> currentZone, Func<string, string, int?> hops,
        Action markDirty, Func<string, object> brush)
    {
        _settings = settings;
        _list = list;
        _listName = listName;
        _byZone = byZone;
        _header = header;
        _currentZone = currentZone;
        _hops = hops;
        _markDirty = markDirty;
        _brush = brush;
    }

    /// <summary>Just the header's "3/12" — cheap enough for every tick, and the one part
    /// that must stay true while the card is collapsed.</summary>
    public void UpdateHeaderOnly()
    {
        var total = _settings.GearChecklist.Count;
        var acquired = _settings.GearChecklist.Count(i => i.Acquired);
        _header.Text = $"{acquired}/{total}";
    }

    public void Render()
    {
        _list.Items.Clear();
        var total = _settings.GearChecklist.Count;
        // No list, no view to pivot — the toggle would be a silent no-op.
        _byZone.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (total == 0)
        {
            _listName.Text = "Import an EQ Legends Tools shopping-list HTML in Options.";
            _list.Items.Add(Dim("No gear list imported."));
            UpdateHeaderOnly();
            return;
        }

        _listName.Text = GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);

        if (_settings.GearGroupByZone) RenderByZone();
        else RenderByKind();

        UpdateHeaderOnly();
    }

    /// <summary>Gear, then Exaltations. It was called <c>RenderGearBySlot</c> and grouped
    /// by neither slot nor anything like it — the slot rides on the ROW. The name was
    /// wrong for long enough that writing the E2E pin for this lift predicted five rows
    /// where the app draws four, which is how the misnomer surfaced at all.</summary>
    private void RenderByKind()
    {
        foreach (var group in GearChecklistPresentation.BuildGroups(_settings.GearChecklist))
        {
            _list.Items.Add(GroupHeading(group.Heading));
            foreach (var item in group.Items) _list.Items.Add(Row(item));
        }
    }

    /// <summary>The WHERE-TO-GO pivot: grouping and buckets live in UI.Shared
    /// (<see cref="GearFarmRollup"/>) where they are tested; this side only draws.
    /// Nearest-first needs a current zone — before the first zone line of a session the
    /// rollup degrades to alphabetical rather than guessing, which is why the delegate is
    /// null and not a function that answers null.</summary>
    private void RenderByZone()
    {
        var here = _currentZone();
        Func<string, int?>? hopsFromHere = here.Length > 0 ? zone => _hops(here, zone) : null;
        var groups = GearFarmRollup.Build(
            _settings.GearChecklist, ItemCatalog.Default.Find, hopsFromHere);
        if (groups.Count == 0)
        {
            _list.Items.Add(Dim("Everything on the list is acquired — nothing left to farm."));
            return;
        }

        foreach (var group in groups)
        {
            _list.Items.Add(GroupHeading(GearFarmRollup.Heading(group)));
            foreach (var item in group.Items) _list.Items.Add(Row(item));
        }
    }

    private TextBlock Dim(string text) => new()
    {
        Text = text,
        FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
        Foreground = (Brush)_brush("DimBrush"),
        TextWrapping = TextWrapping.Wrap,
    };

    private TextBlock GroupHeading(string heading) => new()
    {
        Text = heading,
        FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)_brush("AccentBrush"),
        Margin = new Thickness(0, Tok.SpaceM, 0, Tok.SpaceXxs),
    };

    private CheckBox Row(GearChecklistItem item)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = item.Slot,
            FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
            Foreground = (Brush)_brush("DimBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var itemName = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            Foreground = (Brush)_brush("TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var itemText = GearChecklistPresentation.TextFor(item);
        itemName.Inlines.Add(itemText.Name);
        if (itemText.EffectSuffix.Length > 0)
        {
            itemName.Inlines.Add(new System.Windows.Documents.Run(itemText.EffectSuffix)
            {
                FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
                Foreground = (Brush)_brush("DimBrush"),
            });
        }
        text.Children.Add(itemName);
        if (item.Source.Length > 0)
        {
            text.Children.Add(new TextBlock
            {
                Text = item.Source,
                FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
                Foreground = (Brush)_brush("DimBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        var check = new CheckBox
        {
            IsChecked = item.Acquired,
            Content = text,
            Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceXxs),
            ToolTip = GearChecklistPresentation.Tooltip(item),
        };
        check.Checked += (_, _) => Toggled(item, true);
        check.Unchecked += (_, _) => Toggled(item, false);
        return check;
    }

    private void Toggled(GearChecklistItem item, bool acquired)
    {
        item.Acquired = acquired;
        _settings.Save();
        UpdateHeaderOnly();
        _listName.Text = GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);
        // The zone view excludes acquired rows and repeats a multi-zone item under each
        // zone it drops in — its checkbox twins must repaint, next tick.
        if (_settings.GearGroupByZone) _markDirty();
    }

    /// <summary>The by-zone toggle. Repaints now when the card is open, and otherwise
    /// leaves a note for the next tick — rebuilding a collapsed list is work nobody can
    /// see.</summary>
    public void SetGroupByZone(bool value, bool cardIsOpen, Action clearDirty)
    {
        if (_settings.GearGroupByZone == value) return;
        _settings.GearGroupByZone = value;
        _settings.Save();
        if (cardIsOpen) { Render(); clearDirty(); }
        else _markDirty();
    }
}
