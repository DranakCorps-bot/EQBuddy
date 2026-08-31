using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Wishlist surface on Linux and macOS — an imported EQ Legends Tools shopping list as
/// a checklist, grouped either by kind (gear vs exaltations) or by the zone you would farm
/// it in. The twin of WPF's <c>GearCardView</c>, built from the same
/// <see cref="GearChecklistPresentation"/> and the same <see cref="GearFarmRollup"/>.
///
/// **Why it is a file and not more of <c>MainWindow.cs</c>:** that file is the largest in
/// the repo and its ratchet had THREE lines left. CLAUDE.md's rule when a hotspot runs out
/// is to lift a surface, and this was the named candidate — ~275 contiguous lines that only
/// ever touched settings, three named controls and one repaint flag, with the grouping and
/// the rollup already living in UI.Shared where they are tested. The Inventory tab that
/// closes the 1.98.1 parity gap has to land in this file's neighbourhood, so the room had
/// to be freed before it, not after.
///
/// **This build has no E2E suite**, so CLAUDE.md's "pin the behaviour before the move" was
/// paid in <c>WidgetRenderTests</c> instead: the populated draw, the tick, and the by-zone
/// pivot actually redrawing were written first and verified to FAIL against a MainWindow
/// that had stopped rendering rows. Three older pins (the pivot's existence, the command
/// hand-over, the empty state) already existed and moved across unchanged, which is the
/// point of having had them.
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
    private readonly Func<string> _currentZone;
    private readonly Func<string, string, int?> _hops;
    private readonly Action _markDirty;
    private readonly ImportReportView _importReport;

    // Built here, not handed in. A view that takes its host's controls can only ever live
    // in one host, and a Control has one parent — so a second host builds its own instance
    // rather than tearing this one out of whichever drew it last (the same rule WPF's twin
    // records for NewGearCard).
    private readonly TextBlock _listName = AppTheme.DimText("");
    private readonly CheckBox _byZone = new() { Content = "Group by farm zone" };
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _listScroll;

    /// <summary>Asked for the list's cap, given what this surface keeps PINNED beside it.
    /// The HOST answers — the WPF twin's contract verbatim.</summary>
    private readonly Func<double, double> _listCap;

    /// <summary>What the Gear &amp; Loot window hangs in its Wishlist tab.</summary>
    public Control Body { get; }

    public GearCardView(AppSettings settings, Func<string> currentZone,
        Func<string, string, int?> hops, Action markDirty, Func<AutoImportOutcome?> lastImport,
        Func<double, double> listCap)
    {
        _listCap = listCap;
        _settings = settings;
        _currentZone = currentZone;
        _hops = hops;
        _markDirty = markDirty;
        // What the auto-import DID. Without it the feature is invisible and reads exactly
        // like the bug it replaces: David ran the command, the file appeared, and the
        // window sat there saying nothing (2026-08-20).
        _importReport = new ImportReportView(lastImport, markDirty);

        var panel = new StackPanel();
        _listName.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        _listName.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_listName);
        // The checklist says WHAT; this says WHERE (#122abd6). Off by default — the
        // slot view is the one people import for.
        _byZone.IsChecked = settings.GearGroupByZone;
        _byZone.FontSize = DesignSystem.Size(Role.Caption);
        _byZone.Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs);
        ToolTip.SetTip(_byZone,
            "Pivot the same wishes to where you'd farm them — nearest zone first once "
            + "the log has seen you zone in. An item that drops in several zones is listed "
            + "under each, and one tick clears it everywhere.");
        _byZone.IsCheckedChanged += OnByZoneToggled;
        panel.Children.Add(_byZone);
        panel.Children.Add(_listScroll = new ScrollViewer
        {
            // Starts at the design opening height and follows the HOST from the first
            // render (ApplyListCap) — the WPF twin's note carries the reasoning.
            MaxHeight = WindowSizing.DefaultBodyHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, DesignTokens.SpaceXs, 0),
            Content = _rows,
        });
        // The checklist auto-ticks from the game's own inventory dump and never said so,
        // and offered no way to produce one (David, 2026-08-20). Built in the constructor
        // rather than in Render, and outside the scroller: the note and the button belong
        // to the SURFACE, not to one of its states, so neither can be scrolled away and no
        // render branch can forget to draw them.
        panel.Children.Add(new TextBlock
        {
            Text = GearChecklistPresentation.AutoTickNote,
            FontSize = DesignSystem.Size(Role.Caption),
            TextWrapping = TextWrapping.Wrap,
            Foreground = AppTheme.DimBrush,
            Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0),
        });
        panel.Children.Add(DesignSystem.CopyCommandButton(
            GameCommands.OutputfileInventory, GearChecklistPresentation.AutoTickTip));
        panel.Children.Add(_importReport.Body);
        Body = panel;
        // Re-measured on LAYOUT, not only on Render — the WPF twin's note carries the
        // reasoning. Layout-driven, never on a clock (trap 12), and idempotent.
        panel.LayoutUpdated += (_, _) => ApplyListCap();
    }

    /// <summary>Point the list's cap at whatever is capping the HOST right now — the WPF
    /// twin's <c>ApplyListCap</c> line for line. What is handed over is everything this
    /// surface keeps OUTSIDE the scroller (the auto-tick note, the ⧉ copy of the in-game
    /// command, the import report), which is out there so a long list cannot push it below
    /// the fold (trap 37) — and is why the scroller is capped rather than deleted.</summary>
    private void ApplyListCap()
    {
        if (Body is not Control panel) return;
        var cap = Math.Floor(_listCap(panel.Bounds.Height - _listScroll.Bounds.Height));
        if (Math.Abs(_listScroll.MaxHeight - cap) > 0.5) _listScroll.MaxHeight = cap;
    }

    /// <summary>The list's cap right now, for the render tests.</summary>
    internal double ListCap => _listScroll.MaxHeight;

    public void Render()
    {
        ApplyListCap();
        _importReport.Render();
        _rows.Children.Clear();
        var total = _settings.GearChecklist.Count;
        // No list, no view to pivot — the toggle would be a silent no-op.
        _byZone.IsVisible = total > 0;
        if (total == 0)
        {
            // The route line IS the empty state; a second "No gear list imported."
            // underneath said the same thing in less useful words. WPF's twin.
            _listName.Text = GearChecklistPresentation.EmptyRoute;
            return;
        }

        UpdateListName();
        if (_settings.GearGroupByZone) RenderByZone();
        else RenderByKind();
    }

    /// <summary>Gear, then Exaltations. It was called <c>RenderGearBySlot</c> and grouped
    /// by neither slot nor anything like it — the slot rides on the ROW.</summary>
    private void RenderByKind()
    {
        foreach (var group in GearChecklistPresentation.BuildGroups(_settings.GearChecklist))
        {
            _rows.Children.Add(GroupHeading(group.Heading));
            foreach (var item in group.Items) _rows.Children.Add(Row(item));
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
            _rows.Children.Add(
                EmptyLine("Everything on the list is acquired — nothing left to farm."));
            return;
        }

        foreach (var group in groups)
        {
            _rows.Children.Add(GroupHeading(GearFarmRollup.Heading(group)));
            foreach (var item in group.Items) _rows.Children.Add(Row(item));
        }
    }

    private static TextBlock EmptyLine(string text) => new()
    {
        Text = text,
        FontSize = DesignSystem.Size(Role.Caption),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXxs),
        Foreground = AppTheme.DimBrush,
    };

    private static TextBlock GroupHeading(string heading) => new()
    {
        Text = heading,
        FontSize = DesignSystem.Size(Role.Caption),
        FontWeight = FontWeight.SemiBold,
        Foreground = AppTheme.AccentBrush,
        Margin = new Thickness(0, DesignTokens.SpaceM, 0, DesignTokens.SpaceXxs),
    };

    private CheckBox Row(GearChecklistItem item)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = item.Slot,
            FontSize = DesignSystem.Size(Role.Metadata),
            Foreground = AppTheme.DimBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var itemName = new TextBlock
        {
            FontSize = DesignSystem.Size(Role.Body),
            Foreground = AppTheme.TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        // An exaltation's effect rides the name as a dim run, so the row reads as one
        // item rather than two — same treatment as WPF's.
        var itemText = GearChecklistPresentation.TextFor(item);
        itemName.Inlines?.Add(new Run(itemText.Name));
        if (itemText.EffectSuffix.Length > 0)
            itemName.Inlines?.Add(new Run(itemText.EffectSuffix)
            {
                FontSize = DesignSystem.Size(Role.Metadata),
                Foreground = AppTheme.DimBrush,
            });
        text.Children.Add(itemName);
        if (item.Source.Length > 0)
            text.Children.Add(new TextBlock
            {
                Text = item.Source,
                FontSize = DesignSystem.Size(Role.Metadata),
                Foreground = AppTheme.DimBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

        var check = new CheckBox
        {
            IsChecked = item.Acquired,
            Content = text,
            Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXxs),
        };
        ToolTip.SetTip(check, GearChecklistPresentation.Tooltip(item));
        check.IsCheckedChanged += (box, _) => Toggled(item, ((CheckBox)box!).IsChecked == true);
        return check;
    }

    private void Toggled(GearChecklistItem item, bool acquired)
    {
        item.Acquired = acquired;
        _settings.Save();
        UpdateListName();
        // The zone view excludes acquired rows and repeats a multi-zone item under
        // each zone it drops in — its checkbox twins must repaint, next tick. The BY-KIND
        // view deliberately does not ask for a rebuild: the box the player just clicked is
        // already drawn correctly, and the tab badge is recomputed from settings by
        // GearLootWindow.MaybeRefresh once a second either way. WPF's twin marks dirty here
        // and can afford to; this is the widget whose one rule is never to rebuild 200
        // checkboxes it did not have to.
        if (_settings.GearGroupByZone) _markDirty();
    }

    /// <summary>The by-zone toggle, owned by the view now that the checkbox is its own.
    /// Repaints immediately: the control that changed is inside this body, so the body is
    /// on screen by definition — the old "is this tab showing" question belonged to the
    /// widget, which was asking on behalf of a checkbox it did not own.</summary>
    private void OnByZoneToggled(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var value = _byZone.IsChecked == true;
        if (_settings.GearGroupByZone == value) return;

        _settings.GearGroupByZone = value;
        _settings.Save();
        Render();
        _markDirty();
    }

    private void UpdateListName() =>
        _listName.Text = GearChecklistPresentation.ListName(
            _settings.GearChecklistName, _settings.GearChecklist);
}
