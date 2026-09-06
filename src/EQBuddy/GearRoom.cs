using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The GEAR room as the Evolved shell hosts it — Loot · Wishlist · Inventory, the third
/// surface moved in (E-3 Phase 2 PR 2).
///
/// **Why Gear was the other room PR 2 could take.** Bevel's signed IA asks for a Gear room
/// that is *"bags, wishlist, item lookup, what you picked up"*, and three of those four are
/// what <see cref="GearLootWindow"/> already is. The fourth — item lookup — is Search's
/// job in the Evolved IA, not a tab, so hosting this window's content is the whole verdict
/// rather than most of it. As with World, that makes this a MOVE and not a redesign.
///
/// **It builds its own three surfaces and hands none out** (trap 45): a fresh
/// <see cref="LootCardView"/>, a fresh gear card through <c>MainWindow.NewGearCard</c>, and
/// a fresh <see cref="InventoryView"/>. A view shared with <see cref="GearLootWindow"/>
/// would be torn out of whichever host painted it last — on WPF silently.
///
/// **What the WINDOW does that this room does NOT** (trap 26: name every control and say
/// where it went):
///
///  * **The chrome** — hand-drawn title bar, drag, close, <c>WindowZoom</c>,
///    <c>ScreenGuard</c>, position persistence, the monitor-derived <c>MaxHeight</c>. The
///    shell supplies all of it natively, once, for every room.
///  * **The Loot mini-dashboard star.** It is the ONLY writer <c>MiniStats</c> has for
///    <c>"loot"</c> — and it also gates the Loot breakout window, so losing it would take
///    that away too (trap 20/26). It is not lost: <see cref="GearLootWindow"/> is not
///    retired by this PR and still carries it. It is deliberately not copied here either,
///    for the same reason the World room refuses the deaths star: Bevel's IA puts HUD
///    configuration in the HUD's Edit mode and in Settings, never in a room, and two
///    writers of one settings key is trap 13's shape. **The retirement commit is blocked
///    on rehoming it.**
///
/// **The nested scroller stays, and that is trap 37 rather than an oversight.** Trap 36
/// says scrolling belongs to the host, and the room's body scroller is the host's. But the
/// gear card keeps its OWN inner scroller so that the ⧉ copy of
/// <c>/outputfile inventory</c>, the auto-tick note and the import report sit outside it
/// and cannot be pushed below the fold by a forty-row list — which is the only in-app
/// route to the command that makes the ticks happen, and the row this very surface has in
/// <c>GameCommandsTests.SurfacesNeedingACommand</c>. Its cap comes from this room's body,
/// exactly as it comes from the window's body in the other host.
/// </summary>
internal sealed class GearRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();
    private readonly ScrollViewer _scroll;

    /// <summary>Everything this room draws when it has something to draw — the tab strip and
    /// the scrolling body — in their own Grid, so the whole page is ONE thing to collapse
    /// when the room-level empty takes over.</summary>
    private readonly Grid _page = new();

    /// <summary>The whole-room empty, built on the first render that needs one and kept
    /// afterwards. A SIBLING of <see cref="_page"/> rather than something dropped into the
    /// body, because a whole-room empty has to take the TAB STRIP with it: an empty room
    /// under a live strip of tabs is four invitations to open something that is not there.
    /// <c>LiveRoom</c> set this shape and the other three follow it.</summary>
    private FrameworkElement? _emptyRoom;

    private bool _empty;

    /// <summary>Loot by default, the same call <c>LootSurface.DefaultInlineTab</c> makes
    /// and for its stated reason: it is the tab that moves while you play, where gear
    /// changes on the day you finally get the drop.</summary>
    private LootTab _tab = LootSurface.DefaultInlineTab;

    private readonly LootCardView _loot;
    private readonly GearCardView _gear;
    private readonly InventoryView _inventory;

    public UIElement Body => this;

    public GearRoom(MainWindow main)
    {
        _main = main;
        _settings = main.Settings;

        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_page);

        // WRAPS (trap 25): "Loot — 41 items" and "Inventory — 6 swaps" are tab labels, and
        // a horizontal StackPanel would clip the last chip with no ellipsis and no error.
        var strip = new WrapPanel { Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0) };
        SetRow(strip, 0);
        _page.Children.Add(strip);
        _tabs = new EqSegmentedStrip(strip);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, Tok.SpaceM),
            Content = _body,
        };
        SetRow(_scroll, 1);
        _page.Children.Add(_scroll);

        _loot = new LootCardView(main, _settings);
        // The ROOM's own scroller decides the gear list's cap, not a card-sized constant —
        // the same move #250 PR 2 made for the window. Before the first layout pass the
        // room has no height, and NaN is the answer that means "not sized yet":
        // NestedBodyCap turns it into the design default rather than into a 120-unit
        // sliver, which is what a literal 0 would have produced on the first paint.
        _gear = main.NewGearCard(pinned => WindowSizing.NestedBodyCap(
            _scroll.ActualHeight > 0 ? _scroll.ActualHeight : double.NaN, pinned));
        _inventory = new InventoryView(main);
    }

    public void SetTab(string key)
    {
        if (LootSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Render(_main.CurrentSnapshot());
    }

    public void Render(StatsSnapshot s) => Render(s, force: false);

    private void Render(StatsSnapshot s, bool force)
    {
        // **The whole-room empty, and the only state that gets one.** Loot comes from the
        // log and the bags come from a dump the game names after the character, so with no
        // character there is nothing for Loot or Inventory to be about — and three separate
        // "nothing yet" panels would be three ways of saying the one thing that matters.
        //
        // **The wishlist is why this is a call and not a `NoCharacter` test.** It is a
        // SETTINGS list a player can type before they ever play; collapsing the room over
        // one would take away something they made. `ShellRoomEmpty.GearIsEmpty` carries
        // that clause, and a unit test fails on it.
        _empty = ShellRoomEmpty.GearIsEmpty(
            ShellRoomIdentity.Of(_main), s, _settings.GearChecklist.Count);
        if (_empty)
        {
            _emptyRoom ??= AddEmptyRoom();
            _emptyRoom.Visibility = Visibility.Visible;
            _page.Visibility = Visibility.Collapsed;
            return;
        }
        if (_emptyRoom is not null) _emptyRoom.Visibility = Visibility.Collapsed;
        _page.Visibility = Visibility.Visible;

        BuildTabs(s);
        _body.Content = _tab switch
        {
            LootTab.Gear => _gear.Body,
            LootTab.Inventory => _inventory.Body,
            _ => _loot.Body,
        };
        // Both of these render every tick even when hidden, and that is the window's
        // behaviour carried across rather than an oversight (trap 46): the INACTIVE tab's
        // badge has to stay true, because it is the number the player uses to decide
        // whether to switch.
        _loot.Render(s);
        _gear.Render();
        // Inventory paints on ARRIVAL, never on the tick. It re-scans the game folder and
        // rebuilds every row, so on a once-a-second tick it was re-reading the dump from
        // disk and clearing a StackPanel out from under the player's cursor — the scroll
        // position goes with the children, so a long inventory could not be read past its
        // first screen. That is why `force` exists here at all.
        if (_tab == LootTab.Inventory && force) _inventory.Render();
    }

    /// <summary>A new inventory dump landed. The Inventory room paints on arrival rather
    /// than on the tick, so the auto-import has to say so — otherwise a player watching it
    /// while the game writes the file sees the OLD bags, which is exactly the "EQBuddy did
    /// nothing" reading the auto-import exists to prevent. <c>GearLootWindow</c> has the
    /// same method for the same reason, and <c>FollowingSurfaces.InventoryChanged</c> is
    /// what makes sure both hosts hear it.</summary>
    public void InventoryChanged()
    {
        if (_tab == LootTab.Inventory) _inventory.Render();
    }

    /// <summary>
    /// The room-level empty, positioned by the shared wrapper Home built and worded for THIS
    /// room.
    ///
    /// **No ⧉ copy on it, and that is an ORDER rather than an omission** (trap 34 is about a
    /// room-level empty SWALLOWING an affordance the surface under it was offering). The
    /// Inventory tab's own empty state ships the ⧉ copy of <c>/outputfile inventory</c> and
    /// keeps it — but that command needs a character logged in, so on a profile with no
    /// character it is not the next thing to do. <c>/log on</c> is, and the explanation says
    /// so. Home's readiness row is where the ⧉ for this dump lives on a profile that HAS a
    /// character, which is the room the shell opens on.
    /// </summary>
    private FrameworkElement AddEmptyRoom()
    {
        var built = RoomEmptyState.Build(ShellRoomEmpty.Gear);
        Children.Add(built);
        return built;
    }

    /// <summary>Build the strip from Core's <see cref="LootSurface"/> and UI.Shared's
    /// <see cref="LootTheme"/> — the same two sources <see cref="GearLootWindow"/> and
    /// EQBuddy Mobile read.</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        foreach (var header in LootTheme.Tabs(s, _settings.GearChecklist, _inventory.Badge))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                // force: the player has just ARRIVED at this room, which is the one moment
                // the Inventory tab is supposed to re-read the dump.
                Render(_main.CurrentSnapshot(), force: true);
            });
        }
        // Chips first, THEN the selection.
        _tabs.Select(_tab);
    }

    /// <summary>**The token this room borrowed, given back.** <see cref="InventoryView"/>
    /// holds a <c>CancellationTokenSource</c> that <c>GearLootWindow.Closed</c> disposes;
    /// a shell that closed without doing the same would leak one per open, invisibly. Trap
    /// 46's rule covers close as well as tick.</summary>
    public void Release() => _inventory.Dispose();

    /// <summary>Nothing to arrange. Loot, Wishlist and Inventory are each one column of
    /// rows that reflow — `shell-gear-narrow` shoots the widest of them at the floor for
    /// exactly that claim. There is no list beside a detail pane to collapse, which is the
    /// only thing <see cref="ShellLayout.RoomSinglePane"/> decides. Empty with a reason
    /// rather than absent, per the interface's own contract.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>The room's facts, under <c>shellGear*</c> — the same numbers
    /// <see cref="GearLootWindow"/> reports from the same surfaces, re-keyed mechanically
    /// so two open hosts cannot write over each other in the dump's one flat namespace
    /// (see <see cref="ShellDumpFacts"/>). What is asserted is that the two agree.</summary>
    public string DebugFacts() =>
        // Zero on any profile with a character. A predicate that fired while the room had
        // content would collapse the tab strip, the three tabs AND the ⧉ copy this dump
        // already counts below — so this key and `shellGearCopyCmd` fail together, which
        // is what makes the second one able to say something.
        $"shellGearEmpty={(_empty ? 1 : 0)} " +
        $"shellGearTab={LootSurface.KeyFor(_tab)} " +
        $"shellGearTabs={_tabs.Count} " +
        $"shellGearLootRows={_loot.RowCount} " +
        $"shellGearRows={_gear.DebugRowCount} " +
        $"shellGearPivotShown={(_gear.DebugPivotShown ? 1 : 0)} " +
        // The ⧉ copy of /outputfile inventory, counted rather than assumed: an ABSENT
        // control photographs as an unremarkable panel (trap 29), so a picture of this
        // room could never say whether the command survived the host change. This is the
        // only thing that can.
        $"shellGearCopyCmd={(_gear.DebugCopyCommandShown ? 1 : 0)} " +
        // And the import block (SR-2), for the same reason and re-keyed the same way: two
        // hosts of one surface writing one flat namespace is trap 58, and the assertion
        // worth having is that the two agree.
        $"shellGearImport={(_gear.DebugImportShown ? 1 : 0)} " +
        // The list cap beside the body it is derived FROM, so E2E can assert the
        // relationship rather than a number off the desk it was written on.
        $"shellGearListCap={_gear.DebugListCap:0} " +
        $"shellGearBodyCap={(_scroll.ActualHeight > 0 ? _scroll.ActualHeight : 0):0}";
}
