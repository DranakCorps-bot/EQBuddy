using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The WORLD room as the Evolved shell hosts it — Map · Camps · Path · Travels · Drops,
/// the second surface moved in (E-3 Phase 2 PR 2) and the first room to gain a tab from
/// somewhere else (E-3 lane S, S2).
///
/// **Drops is the fifth tab and the ONLY one the v1 lane does not draw beside it.** The
/// Evolved IA splits Kills &amp; Drops four ways, and the Why column sends this half here:
/// *"is this camp worth it?"* is a question about the world, not about your bags. It is the
/// second-cleanest item in the whole shell effort for the same reason Quests was the
/// first — <c>CreatureSurface</c>'s own comment says *"Drops has never been a card at all,
/// only a menu entry"*, so there is no <c>OverlaySections</c> row, no <c>MiniStats</c> key
/// and no settings migration to get wrong. <c>WorldSurface.AbsorbedCardKeys</c> stays
/// <c>["misc"]</c>.
///
/// **Nothing is subtracted from v1.** <see cref="CreatureWindow"/> keeps both its tabs and
/// both its hooks (<c>EQBUDDY_DROPS</c>, <c>EQBUDDY_CREATURE</c>) — the shell's Drops tab is
/// reached through the <c>page:room</c> grammar as <c>world:drops</c>, not by sharing a
/// door. Two independent doors to two independent hosts of one surface, exactly the shape
/// Live and <c>CreatureWindow</c>'s Kills tab already have. So does the widget's World card
/// and <see cref="WorldWindow"/> itself: both are v1 hosts, both still show four tabs, and
/// <see cref="WorldSurface.ShellOnly"/> is the one predicate keeping the fifth off a window
/// that would answer it with the Travels list.
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

    /// <summary>The fifth tab, and the one this room did not inherit from
    /// <see cref="WorldWindow"/> — see the type summary's Drops section.</summary>
    private readonly DropsCardView _drops;

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
        // Constructed outright rather than through a MainWindow factory, which is just as
        // fresh (SurfaceOwnershipTests' theory says so in as many words) — the exact line
        // CreatureWindow already uses. Unlike Kills, nothing else in this codebase holds a
        // DropsCardView, so there is no shared instance to be tempted by and no factory to
        // add: it was only ever the window's.
        _drops = new DropsCardView(main);

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

        // BEFORE the strip, because the strip reads this view's own badge. Drops is the one
        // tab painted whether or not it is the one on screen, and it is CreatureWindow's
        // reason rather than an exception to trap 46's rule: the badge is the number a
        // player uses to decide whether to switch, so it has to be true on the tab you are
        // not looking at, and the work is arithmetic over a snapshot already in memory
        // behind a signature gate. The wiki lookups it kicks off are MainWindow-memoized and
        // the v1 window already fires them on the same tick.
        _drops.Render(s);

        BuildTabs(s);
        _body.Content = _tab switch
        {
            WorldTab.Map => _map.Body,
            WorldTab.Camps => _spawns.Body,
            WorldTab.Routes => _travel.Body,
            WorldTab.Drops => _drops.Body,
            _ => _travels.Body,
        };
        // Trap 46: the VISIBLE room keeps its per-tick paint and the others cost nothing
        // this tick — the same split WorldWindow makes, and the same reason. Camps is the
        // one exception: SpawnsView owns a one-second DispatcherTimer that runs regardless
        // of what is on screen, exactly as it did when it was a standalone window, because
        // pausing a countdown on a tab switch risks a wrong countdown for no measurable
        // saving. That timer is what Release() has to stop.
        // Drops has no row here on purpose — it is painted above, every tick, so its badge
        // is true on the tab you are not looking at. Rendering it twice would be two
        // producers of one paint, which the signature gate would absorb silently.
        switch (_tab)
        {
            case WorldTab.Map: _map.MaybeRefresh(); break;
            case WorldTab.Routes: _travel.Render(); break;
            case WorldTab.Travels: _travels.Render(s); break;
        }
    }

    /// <summary>Build the strip from Core's <see cref="WorldSurface"/> — the same
    /// definition <see cref="WorldWindow"/> and EQBuddy Mobile read, so three surfaces
    /// cannot end up naming four different tabs (#184, #210). Five here and four there is
    /// not a disagreement about the words: it is one predicate,
    /// <see cref="WorldSurface.ShellOnly"/>, applied in <see cref="WorldTheme"/>.</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        var zone = _main.CurrentZoneName is { Length: > 0 } z ? z : null;
        // ShellTabs, not Tabs: the shell strip is the one that carries Drops. Both come out
        // of the same builder in UI.Shared, so the room and the v1 window cannot end up
        // wording one tab two ways — and the Drops badge is the VIEW's own, never a second
        // count derived from the snapshot beside it.
        foreach (var header in WorldTheme.ShellTabs(zone, s.Deaths.Count, _drops.Badge))
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
    /// <remarks><see cref="DropsCardView"/> adds nothing to stop — checked rather than
    /// assumed, which is the only reason this method carries a comment at all. It owns no
    /// timer, no watcher and no file handle, and the wiki lookups it kicks off belong to
    /// <c>MainWindow</c>'s memo rather than to this room. <c>CreatureWindow</c>'s own close
    /// path stops nothing for it either.</remarks>
    public void Release() => _spawns.StopTicking();

    /// <summary>Nothing to arrange. All five World tabs are one column — the map is a
    /// canvas that reflows, and Camps/Path/Travels are vertical lists, and Drops joins them
    /// unchanged (its body is a single-column <c>StackPanel</c>: the filter and export bar,
    /// the orientation footer, then the creature list). There is still no list
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
    ///
    /// **The five Drops rows are hand-written, and that is the same PRINCIPLE rather than an
    /// exception to it.** The rule above is "the facts come from the view"; the mechanism it
    /// names assumes the view HAS a <c>DebugFacts()</c> to re-prefix, and
    /// <see cref="DropsCardView"/> has none — it exposes five ints, and
    /// <see cref="CreatureWindow"/> hand-assembles them into <c>dropsMobs=</c> and friends.
    /// <c>LiveRoom</c> reached this exact question for <c>KillsCardView</c> one PR earlier
    /// and answered it the same way (<c>shellLiveKillRows</c>) rather than inventing a
    /// <c>DebugFacts()</c> that nothing else would ever call. Matching the letter of the
    /// paragraph above — written before a view of this shape was in the room — would be the
    /// wrong precedent, and Bevel's World Drops pre-design §3 names that hazard out loud.
    ///
    /// **The keys pair with the v1 window's deliberately.** <c>shellWorldDropsRows</c>
    /// beside <c>CreatureWindow</c>'s unprefixed <c>dropsRows</c> is what lets the E2E suite
    /// ask whether two live hosts of one surface describe the same moment — the comparison
    /// <c>shellLiveKillRows</c> already buys for Kills, and precisely what trap 58's
    /// per-host prefixing exists to keep possible instead of colliding.
    /// </summary>
    public string DebugFacts() =>
        // Zero on any profile with a character. A predicate that fired while the room had
        // content would collapse the tab strip, the five tabs and the "Drop camp marker"
        // row with them.
        $"shellWorldEmpty={(_empty ? 1 : 0)} " +
        $"shellWorldTab={WorldSurface.KeyFor(_tab)} " +
        $"shellWorldTabs={_tabs.Count} " +
        $"shellWorldDropsMobs={_drops.DebugMobCount} " +
        $"shellWorldDropsRows={_drops.DebugRowCount} " +
        $"shellWorldDropsItems={_drops.DebugItemCount} " +
        $"shellWorldDropsFilterLen={_drops.DebugFilterLength} " +
        // The wiki re-check ↻ on every creature heading (#226). An absent control
        // photographs as an unremarkable header (trap 29/34); this is the only cover, and
        // it is the row that says the shell's host kept the affordance the window has.
        $"shellWorldDropsRecheck={_drops.DebugRecheckCount} " +
        ShellDumpFacts.Prefixed("shellWorld", _map.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _spawns.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _travel.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellWorld", _travels.DebugFacts());
}
