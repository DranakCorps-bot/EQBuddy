using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The WORLD room as the Evolved shell hosts it — Map · Camps · Path · Travels, the
/// second surface moved in (E-3 Phase 2 PR 2).
///
/// **Why World was one of the two rooms PR 2 could take.** Bevel's signed IA verdict for
/// it is *"Keep → unify"*, and the unify already happened: the World fold (1.99.13)
/// collapsed <c>MapWindow</c>, <c>SpawnsWindow</c>, <c>TravelWindow</c> and a buried cog
/// entry into one four-tab window. So this is a MOVE, not a redesign — the same thing PR 1
/// could say about Progress, and the only shape that keeps a half-built shell coherent at
/// every commit.
///
/// **It builds its own four views and hands none out** (trap 45). A <c>UIElement</c> has
/// exactly one parent, so a view shared with <see cref="WorldWindow"/> would be torn out
/// of whichever host painted it last — on WPF silently, with no exception to point at.
/// <c>MainWindow</c>'s four separate World factories are what make that cheap, and they are
/// four rather than one set precisely because <see cref="MapView"/> and
/// <see cref="SpawnsView"/> do real construction-time work (file I/O, ledger reads) that a
/// combined factory would fire for every sibling.
///
/// **What the WINDOW was doing that this room does NOT do, listed rather than left to be
/// noticed** (trap 26's rule: when you fold a surface, name every control and say where it
/// went):
///
///  * **The chrome** — hand-drawn title bar, drag handler, close button, <c>WindowZoom</c>,
///    <c>ScreenGuard</c> placement, the <c>Left</c>/<c>Top</c> persistence and the
///    monitor-derived <c>MaxHeight</c>. The shell supplies all of it once, natively, for
///    every room. Deleted rather than ported.
///  * **The Travels tab's mini-dashboard star.** It is the ONLY writer <c>MiniStats</c> has
///    for <c>"deaths"</c>, and a fold that drops the last writer of a setting is exactly
///    #204/#209, #210 and #212 (trap 20/26). It is NOT lost: this PR does not retire
///    <see cref="WorldWindow"/>, which still carries it. It is also not COPIED here, which
///    is the deliberate half — Bevel's IA puts HUD configuration in the HUD's Edit mode and
///    in Settings, *never in a room*, and two writers of one setting is the shape trap 13
///    is about. **When <see cref="WorldWindow"/> is retired the star must be rehomed**, and
///    the retirement commit is blocked on it.
///
/// **What it DOES carry, and the line between the two.** "Drop camp marker" is here. It is
/// a capability the room performs, not a statement about the HUD — the same distinction
/// that sends the star away and keeps the button. It sits in chrome below the body,
/// visible on every tab, exactly as the Helm-signed World pre-design amendment required,
/// so it never loses its home for even one release.
///
/// **Scrolling belongs to the host** (trap 36), and the host here is the room's own
/// bounded <c>*</c> cell in the shell — a real overflow, not the infinite-height measure
/// that makes a child scroller swallow the wheel and scroll nothing. The tab strip stays
/// pinned above it and the action row pinned below it rather than being concatenated into
/// the scrolling body, which is what trap 37 cost the Drops tab's footer.
/// </summary>
internal sealed class WorldRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();

    /// <summary>Everything this room draws when it has something to draw — the tab strip,
    /// the scrolling body and the pinned "Drop camp marker" row — in their own Grid, so the
    /// whole page is ONE thing to collapse when the room-level empty takes over.</summary>
    private readonly Grid _page = new();

    /// <summary>The whole-room empty, built on the first render that needs one and kept
    /// afterwards. A SIBLING of <see cref="_page"/> rather than something dropped into the
    /// body, because a whole-room empty has to take the TAB STRIP with it: an empty room
    /// under a live strip of tabs is four invitations to open something that is not there.
    /// <c>LiveRoom</c> set this shape and the other three follow it.</summary>
    private FrameworkElement? _emptyRoom;

    private bool _empty;

    private WorldTab _tab = WorldSurface.DefaultInlineTab;

    private readonly MapView _map;
    private readonly SpawnsView _spawns;
    private readonly TravelView _travel;
    private readonly TravelsView _travels;

    public UIElement Body => this;

    public WorldRoom(MainWindow main)
    {
        _main = main;

        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Children.Add(_page);

        // WRAPS, and it has to: a horizontal StackPanel measures its children with INFINITE
        // width, so a chip never reaches a boundary to wrap at and the last one is simply
        // clipped at the panel's edge — silently, with no ellipsis (trap 25, itself trap 14
        // with chips). "Map — Everfrost Peaks" and "Travels — 3 deaths" are tab labels.
        var strip = new WrapPanel { Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0) };
        SetRow(strip, 0);
        _page.Children.Add(strip);
        _tabs = new EqSegmentedStrip(strip);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, Tok.SpaceM),
            Content = _body,
        };
        SetRow(scroll, 1);
        _page.Children.Add(scroll);

        _map = main.NewMapView();
        _spawns = main.NewSpawnsView();
        // The Camps view still carries the title row and close button it had as a
        // standalone window. Redundant inside any host that supplies its own chrome —
        // WorldWindow hides it for the same reason, and a room that forgot to would draw
        // a second title bar under the shell's real one.
        _spawns.HideOwnTitleBar();
        _travel = main.NewTravelView();
        _travels = main.NewTravelsView();

        BuildActionRow();
    }

    /// <summary>The "Drop camp marker" action — room chrome, not a tab body, so it is
    /// there whichever of the four rooms is open. Carried across from
    /// <see cref="WorldWindow"/> because it is something the player DOES here; the deaths
    /// star beside it in that window is a statement about the HUD and is deliberately not
    /// carried (see the type's summary).</summary>
    private void BuildActionRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Tok.SpaceL, 0, Tok.SpaceL, Tok.SpaceM),
        };
        row.Children.Add(DesignSystem.IconButton("Location",
            "Drop a marker at your current zone — see it on the Travels tab and on your phone's map",
            (_, _) => { _main.DropCampMarker(); Render(_main.CurrentSnapshot()); }, "AccentBrush"));
        var label = DesignSystem.Text(Role.Caption, "Drop camp marker");
        label.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        SetRow(row, 2);
        _page.Children.Add(row);
    }

    /// <summary>
    /// The room-level empty, positioned by the shared wrapper Home built and worded for THIS
    /// room.
    ///
    /// **The "Drop camp marker" button goes with the page it is pinned to, and that is
    /// safe because of a clause rather than because of luck.** The button works whether or
    /// not a log has ever been read, so hiding it would be trap 34's shape — an affordance
    /// removed by something that reads as polish. <c>ShellRoomEmpty.WorldIsEmpty</c> refuses
    /// to fire on a profile that already has markers, and a unit test fails on that clause;
    /// a profile with no character AND no markers has nothing to drop a marker relative to,
    /// since the marker is labelled with the zone the log says you are in.
    /// </summary>
    private FrameworkElement AddEmptyRoom()
    {
        var built = RoomEmptyState.Build(ShellRoomEmpty.World);
        Children.Add(built);
        return built;
    }

    public void SetTab(string key)
    {
        if (WorldSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Render(_main.CurrentSnapshot());
    }

    public void Render(StatsSnapshot s)
    {
        // **The whole-room empty, and the only state that gets one.** All four tabs start
        // from the zone the log says you are standing in — including Path, which looks
        // character-independent and is not (`TravelView` plans FROM the current zone and
        // says so when there is none). With no zone, no travel history and no markers there
        // is nothing for any of them to be about; the clauses are in `ShellRoomEmpty` where
        // a unit test can reach them.
        _empty = ShellRoomEmpty.WorldIsEmpty(ShellRoomIdentity.Of(_main), s);
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
            WorldTab.Map => _map.Body,
            WorldTab.Camps => _spawns.Body,
            WorldTab.Routes => _travel.Body,
            _ => _travels.Body,
        };
        // Trap 46: the VISIBLE room keeps its per-tick paint and the others cost nothing
        // this tick — the same split WorldWindow makes, and the same reason. Camps is the
        // one exception: SpawnsView owns a one-second DispatcherTimer that runs regardless
        // of what is on screen, exactly as it did when it was a standalone window, because
        // pausing a countdown on a tab switch risks a wrong countdown for no measurable
        // saving. That timer is what Release() has to stop.
        switch (_tab)
        {
            case WorldTab.Map: _map.MaybeRefresh(); break;
            case WorldTab.Routes: _travel.Render(); break;
            case WorldTab.Travels: _travels.Render(s); break;
        }
    }

    /// <summary>Build the strip from Core's <see cref="WorldSurface"/> — the same
    /// definition <see cref="WorldWindow"/> and EQBuddy Mobile read, so three surfaces
    /// cannot end up naming four different tabs (#184, #210).</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        var deaths = s.Deaths.Count > 0 ? $"{s.Deaths.Count} death{(s.Deaths.Count == 1 ? "" : "s")}" : null;
        var zone = _main.CurrentZoneName is { Length: > 0 } z ? z : null;
        foreach (var header in WorldSurface.Tabs(map: zone, travels: deaths))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                Render(_main.CurrentSnapshot());
            });
        }
        // Chips first, THEN the selection — colouring before rebuilding leaves every fresh
        // chip unstyled, including the selected one, which is the whole signal.
        _tabs.Select(_tab);
    }

    /// <summary>**The timer this room borrowed, given back.** <c>SpawnsView</c>'s
    /// one-second <c>DispatcherTimer</c> is stopped by <c>WorldWindow.Closed</c>; a shell
    /// that closed without doing the same would leak one ticking timer per open, for the
    /// life of the process, with nothing in a diff, a test or a screenshot able to see
    /// it. That is the trap-46 half of a move — check what the OLD host was doing for the
    /// surface, and at close as well as on the tick.</summary>
    public void Release() => _spawns.StopTicking();

    /// <summary>Nothing to arrange. All four World tabs are one column — the map is a
    /// canvas that reflows, and Camps/Path/Travels are vertical lists. There is no list
    /// beside a detail pane to collapse, which is the only thing
    /// <see cref="ShellLayout.RoomSinglePane"/> decides. Empty with a reason rather than
    /// absent, per the interface's own contract.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>
    /// The room's facts, under <c>shellWorld*</c>.
    ///
    /// **The four views are asked for the SAME strings the window asks them for**, and the
    /// keys are re-prefixed mechanically (<see cref="ShellDumpFacts"/>) rather than
    /// re-implemented. Two reasons, and the first is a live hazard: the dump is one flat
    /// namespace, so with both hosts open the shell's <c>mapZones</c> would land on the
    /// window's and every existing <c>map*</c> assertion would quietly start reading the
    /// other window (trap 4, two sources for one fact). The second is that a hand-written
    /// list of facts here would stop covering <see cref="MapView"/> the day it gains a
    /// seventh one — trap 30, again.
    /// </summary>
    public string DebugFacts() =>
        // Zero on any profile with a character. A predicate that fired while the room had
        // content would collapse the tab strip, the four tabs and the "Drop camp marker"
        // row with them.
        $"shellWorldEmpty={(_empty ? 1 : 0)} " +
        $"shellWorldTab={WorldSurface.KeyFor(_tab)} " +
        $"shellWorldTabs={_tabs.Count} " +
        ShellDumpFacts.Prefixed("shellWorld", _map.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _spawns.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _travel.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _travels.DebugFacts());
}
