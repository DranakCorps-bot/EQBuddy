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
/// Lifted out of <c>MainWindow</c> for the Gear &amp; Loot theme (docs/Themes.md), and it
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
internal sealed class GearCardView : IWidgetCard
{
    private readonly AppSettings _settings;
    private readonly Func<string> _currentZone;
    private readonly Func<string, string, int?> _hops;
    private readonly Action _markDirty;
    private readonly Func<string, object> _brush;

    // Built here, not handed in. A card that takes its host's controls can only ever
    // live in one host — and this one has to live in two, because the Gear & Loot
    // theme puts it in a window as well as on the widget. A UIElement has one parent,
    // so each host gets its OWN instance (MainWindow.NewProgressSurfaces' rule).
    private readonly ItemsControl _list = new();
    private readonly TextBlock _listName;
    private readonly CheckBox _byZone;
    private readonly Button _copyCmd;

    public string Key => LootSurface.KeyFor(LootTab.Gear);
    public UIElement Body { get; }

    public GearCardView(
        AppSettings settings,
        Func<string> currentZone, Func<string, string, int?> hops,
        Action markDirty, Func<string, object> brush)
    {
        _settings = settings;
        _currentZone = currentZone;
        _hops = hops;
        _markDirty = markDirty;
        _brush = brush;

        _byZone = new CheckBox
        {
            Content = "Group by farm zone",
            Margin = (Thickness)_brush("ListBlock"),
            FontSize = Tok.Spec(Tok.TypeRole.Metadata).Size,
            Foreground = (Brush)_brush("DimBrush"),
            IsChecked = settings.GearGroupByZone,
        };
        _byZone.Checked += (_, _) => SetGroupByZone(true);
        _byZone.Unchecked += (_, _) => SetGroupByZone(false);

        _listName = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            Foreground = (Brush)_brush("DimBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = (Thickness)_brush("ListBlock"),
        };

        // David, 2026-08-20: the tab told him to import something and handed him no way
        // to do it. The checklist auto-ticks from the game's own inventory dump
        // (MainWindow.AutoCheckGearFromInventory), so this surface REQUIRES an in-game
        // command — and every surface that names one offers a one-click ⧉ copy of the
        // exact text (David, 2026-08-14). Shape copied from RaidsCardView, deliberately
        // not reinvented; GameCommandsTests now asserts this file references
        // GameCommands so the affordance cannot go missing again unnoticed.
        _copyCmd = Theming.WireCopyCommand(
            Theming.Button(""), GameCommands.OutputfileInventory);
        _copyCmd.FontSize = Tok.Spec(Tok.TypeRole.Caption).Size;
        _copyCmd.HorizontalAlignment = HorizontalAlignment.Left;
        _copyCmd.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        _copyCmd.ToolTip = GearChecklistPresentation.AutoTickTip;

        var panel = new StackPanel();
        panel.Children.Add(_byZone);
        panel.Children.Add(_listName);
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.VerticalOnly,
            Padding = new Thickness(Tok.SpaceXs),
            Content = _list,
        });
        // OUTSIDE the ScrollViewer and outside Render(), which is the whole point: the
        // note and the button belong to the SURFACE, not to a state of it, so neither
        // can be scrolled away and neither has a branch that could forget to draw it.
        var note = Dim(GearChecklistPresentation.AutoTickNote);
        note.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        panel.Children.Add(note);
        panel.Children.Add(_copyCmd);
        Body = panel;
    }

    // The E2E dump's window into this card. The WPF layer has no unit tests, so a
    // launched app asserting these is the only coverage the surface has — and they were
    // written against the widget's own controls before the lift, which is exactly why
    // they still read the same numbers now that the controls live here.
    internal int DebugRowCount => _list.Items.Count;
    internal bool DebugPivotShown => _byZone.Visibility == Visibility.Visible;
    internal int DebugListNameLength => _listName.Text.Length;
    /// <summary>1 when the ⧉ copy of /outputfile inventory is on screen. Pinned in E2E
    /// for BOTH the empty and the populated state — an absent control photographs as an
    /// unremarkable panel (trap 29), so a picture could never have caught this one.</summary>
    internal bool DebugCopyCommandShown =>
        _copyCmd.Visibility == Visibility.Visible && _copyCmd.Parent is not null;

    /// <summary>The card's header badge — "3/12", or an em dash with nothing imported.
    /// The STRING comes from <see cref="LootTheme"/>, because the widget's card header and
    /// the theme window's tab badge must not be two different answers (#210).</summary>
    public string Badge => LootTheme.Gear(_settings.GearChecklist);

    public void Render(StatsSnapshot snapshot) => Render();

    public void Render()
    {
        _list.Items.Clear();
        var total = _settings.GearChecklist.Count;
        // No list, no view to pivot — the toggle would be a silent no-op.
        _byZone.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (total == 0)
        {
            // The route line above IS the empty state now, so the list stays empty. It
            // used to add "No gear list imported." underneath, which said the same thing
            // a second time and in less useful words — and the second sentence is exactly
            // the space the two real routes needed (David, 2026-08-20). E2E's gearRows
            // pin moved from 1 to 0 with this, deliberately.
            _listName.Text = GearChecklistPresentation.EmptyRoute;
            return;
        }

        _listName.Text = GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);

        if (_settings.GearGroupByZone) RenderByZone();
        else RenderByKind();
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
        _markDirty();          // the host repaints its own header from Badge
        _listName.Text = GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);
        // The zone view excludes acquired rows and repeats a multi-zone item under each
        // zone it drops in — its checkbox twins must repaint, next tick.
        if (_settings.GearGroupByZone) _markDirty();
    }

    /// <summary>The by-zone toggle, owned by the card now that the checkbox is its own.
    /// Repaints immediately: the control that changed is inside this body, so the body is
    /// on screen by definition — the old "is the card open" question belonged to the
    /// widget, which was asking on behalf of a checkbox it did not own.</summary>
    private void SetGroupByZone(bool value)
    {
        if (_settings.GearGroupByZone == value) return;
        _settings.GearGroupByZone = value;
        _settings.Save();
        Render();
        _markDirty();
    }
}
