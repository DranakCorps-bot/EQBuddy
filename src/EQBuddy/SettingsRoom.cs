using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The SETTINGS room as the Evolved shell hosts it — Look · Alerts · HUD · Behavior, the
/// seventh and last room of the rail (E-3, Fable's SR-5, on Bevel's I-11 IA).
///
/// **It is neither a MOVE, a LIFT, a BUILD nor a MERGE — it is a COMPOSITION, and that is
/// the whole architecture of the SR series.** The four blocks it shows were pulled out of
/// <see cref="OptionsWindow"/> one PR at a time (SR-1 Look and Behavior, SR-3 HUD, SR-4 the
/// four alert families) precisely so that this room could show the SAME controls instead of
/// growing a second copy of roughly forty wirings beside a window that is not retired. That
/// alternative is #210's mechanism with a bigger surface: two hosts of one decision, each
/// hand-rolling its own, drifting until somebody notices the phone and the desktop disagree.
///
/// **Four tabs here, five in the window, and neither is wrong.** Bevel §2: *"Watch and
/// Alerts were never two subjects"* — both answer "if X happens, alert me how", which is
/// what <see cref="AlertSurface"/> has modelled as one four-way split since before the
/// pivot. So the v1 <c>watch</c> tab and the Buffs/Spawns/Crowd half of the v1 <c>alerts</c>
/// tab arrive here as ONE tab with a sub-strip, under the shared sound header they all
/// override. <see cref="OptionsWindow"/> is not retired, not renamed and not reshaped by
/// this PR — I-9's standing rule is that landing a room is separate from, and earlier than,
/// retiring the surface it replaces, and this room deliberately leaves the v1 arrangement
/// exactly as players already have it.
///
/// **It builds its OWN four blocks and hands none out** (trap 45). A WPF <c>UIElement</c>
/// has exactly one parent, so a block shared with <see cref="OptionsWindow"/> would be torn
/// out of whichever host painted it last — on WPF silently, with no exception to point at
/// and nothing in a diff or a screenshot to say so.
///
/// **Both hosts wrap ONE <see cref="AppSettings"/>** (trap 13). This room takes
/// <c>MainWindow.Settings</c> and <c>MainWindow.PersistSettings</c> through its own
/// <see cref="OptionsViewModel"/>, exactly as <c>OptionsWindow.xaml.cs</c> does. A room that
/// called <c>AppSettings.Load</c> for itself would hold a SECOND snapshot and write the
/// whole file back from it on its next save — which is how "my tick-boxes won't stay ticked"
/// (#169) presents, with nothing on screen to say so, and it is the one mistake that would
/// make two open settings surfaces actively destructive rather than merely redundant.
///
/// **What the WINDOW does that this room does NOT** (trap 26: name every control and say
/// where it went):
///
///  * **The chrome** — the hand-drawn title row, the ✕, the drag handler, the two resize
///    grips, <c>WindowZoom</c>, the saved width and the monitor-derived <c>MaxHeight</c>.
///    The shell supplies all of it natively, once, for every room. SR-1 already left the one
///    sentence that is ABOUT that chrome ("Drag either side edge to widen this window")
///    declared beside the grips in <c>OptionsWindow.xaml</c> rather than inside the Look
///    block, for exactly this moment.
///  * **The ★ alert-banner PLACEMENT MODE, and this one is a real divergence rather than
///    chrome.** <c>MainWindow.OnOptions</c> calls <c>AlertTile.EnterPlacement()</c> when the
///    window opens and <c>ExitPlacement()</c> on its <c>Closed</c>, so the alert tile becomes
///    a draggable target for as long as Options is up; the shared header block says so in a
///    sentence both hosts print. This room does not do it, and that is a decision: a room is
///    NAVIGATED to and away from, not opened and closed, so an entrance-time
///    <c>EnterPlacement</c> would leave a draggable tile on the desktop for as long as the
///    shell stayed open on any other room — worse than the thing it copies. The sentence
///    stays true as written (it is a statement about Options, which still exists and still
///    works), and **rehoming the drag target is a blocker on the commit that retires
///    <see cref="OptionsWindow"/>**, the same way the Loot mini-dashboard star blocks
///    <c>GearLootWindow</c>'s. Logged in `DECISIONS.md` and flagged to Bevel.
///  * **The saved tab.** The window persists <c>AppSettings.OptionsTab</c>; this room does
///    not write it and does not read it. The v1 keys are <c>look/alerts/watch/cards/behavior</c>
///    and this room's are <c>look/alerts/hud/behavior</c>, so a room that wrote "hud" would
///    send the WINDOW home to Look on its next open — one host silently editing the other's
///    landing. Trap 13's shape without the file corruption. The room opens on
///    <see cref="SettingsSurface.DefaultTab"/> unless an address says otherwise, and the
///    address is what a caller who cares uses.
///
/// **No room-level empty, and it is stated rather than omitted** (the <see cref="IShellRoom"/>
/// contract's own rule). Every other room can be about nothing — no character, no session, no
/// bags — and collapses to an explanation. Settings is never empty: it configures the tool,
/// not the character, and every control on it is meaningful on a profile that has never seen
/// a log line. A whole-room empty here would hide the four tabs from precisely the player who
/// has just installed EQBuddy.
/// </summary>
internal sealed class SettingsRoom : Grid, IShellRoom
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;

    /// <summary>The host's own gate, closed while the four blocks build. Every block reads
    /// its values out of the shared view model as it constructs, and assigning a slider or
    /// ticking a box raises <c>ValueChanged</c>/<c>Checked</c> exactly as a player's drag
    /// does — so without this the room's own construction would write settings.</summary>
    private bool _ready;

    private readonly EqSegmentedStrip _tabs;
    private readonly EqSegmentedStrip _alertTabs;
    private readonly WrapPanel _alertStrip;
    private readonly ContentControl _body = new();
    private readonly ContentControl _alertBody = new();
    private readonly StackPanel _alertPage = new();
    private readonly ScrollViewer _scroll;

    private readonly SettingsLookView _look;
    private readonly SettingsAlertsView _alerts;
    private readonly SettingsHudView _hud;
    private readonly SettingsBehaviorView _behavior;

    private SettingsTab _tab = SettingsSurface.DefaultTab;

    /// <summary>Which alert family the Alerts tab is showing. Kept apart from
    /// <see cref="_tab"/> because <c>settings:crowd</c> sets both — the address grammar's
    /// room half is one level deep and the Alerts tab is two, which is resolved by the
    /// fallthrough in <see cref="SetTab"/> rather than by a third address level.</summary>
    private AlertTab _alertTab = AlertTab.Watch;

    public UIElement Body => this;

    public SettingsRoom(MainWindow main)
    {
        _main = main;
        // The SAME AppSettings instance and the SAME persist delegate the widget holds, and
        // the same two arguments `OptionsWindow.xaml.cs` passes. Trap 13 as a constructor
        // contract: a second snapshot would clobber the first one wholesale.
        _vm = new OptionsViewModel(main.Settings, main.PersistSettings);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // WRAPS (trap 25): four labels whose widths are their content's, and a horizontal
        // StackPanel measures with infinite width in the stacking direction — so the fourth
        // chip is clipped at the panel edge with no ellipsis and no error. That is exactly
        // how the Progress window shipped three visible tabs out of four.
        var strip = new WrapPanel { Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, 0) };
        SetRow(strip, 0);
        Children.Add(strip);
        _tabs = new EqSegmentedStrip(strip);

        // The Alerts sub-strip, PINNED beside the main one rather than scrolling with the
        // body. Bevel's IA puts the shared sound header above the four families, and a
        // family picker that scrolled away under a long rules editor would be a navigation
        // control you have to hunt for. It is a WrapPanel for the reason
        // `SettingsAlertsView.Tabs()` writes on itself: these chips carry COUNTS, so their
        // widths depend on content (trap 25 again, and the obligation SR-4 recorded for
        // whoever rendered them — this is that host).
        _alertStrip = new WrapPanel
        {
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXs, Tok.SpaceL, 0),
            Visibility = Visibility.Collapsed,
        };
        SetRow(_alertStrip, 1);
        Children.Add(_alertStrip);
        _alertTabs = new EqSegmentedStrip(_alertStrip);

        // Scrolling belongs to the HOST (trap 36). The blocks are plain stacks with no
        // scroller of their own — `OptionsWindow` wraps them in ITS ScrollViewer, and a
        // block that brought one would be measured with infinite height here, never
        // overflow, never scroll, and still swallow the wheel.
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            PanningMode = PanningMode.VerticalOnly,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceM, Tok.SpaceL, Tok.SpaceM),
            Content = _body,
        };
        SetRow(_scroll, 2);
        Children.Add(_scroll);

        // The shared sound/voice/volume/rate defaults sit ABOVE the four families, once —
        // Bevel §2: a cross-cutting default every rule can individually override, not
        // content belonging to one family.
        _alertPage.Children.Add(_alertBody);

        _hud = new SettingsHudView(_main, _vm, () => _ready, FindResource);
        // The HUD block's panel rows resolve their Foreground with FindResource at BUILD
        // time rather than through a DynamicResource, so a palette swap has to rebuild them.
        // That is the host's business, not the Look block's, which is why it asks instead of
        // reaching — the same callback `OptionsWindow` passes.
        _look = new SettingsLookView(_main, _vm, () => _ready, FindResource, () => _hud.BuildCards());
        // The Behavior block gets ONE thing this host can do and `OptionsWindow` cannot:
        // re-open the first-run Setup screen (OE-6), which is a layer of the shell window.
        // Resolved LATE for the same reason the Alerts block's dialog owner is — a room is
        // not a window and has no parent at all while its constructor runs, so a `this`-
        // shaped answer captured here would be null forever.
        _behavior = new SettingsBehaviorView(_main, _vm, () => _ready, FindResource,
            () => (Window.GetWindow(this) as ShellWindow)?.ShowSetup());
        // The owner for the block's file dialogs, resolved LATE: a room is not a window and
        // has no parent at all while its constructor runs, so a `this`-shaped answer captured
        // here would be null forever. `Window.GetWindow` asks at the moment a dialog opens,
        // which is the only moment the answer is knowable.
        _alerts = new SettingsAlertsView(_main, _vm, () => _ready, FindResource,
            () => Window.GetWindow(this));

        // **Every block is composed NOW, not on first visit, and that is deliberate.**
        // `OptionsWindow` builds all four in its constructor, so a room that built them
        // lazily would report a different surface from the window for as long as a tab went
        // unvisited — and the whole point of the `EQBUDDY_EXPAND` comparison below is that
        // two live hosts of one block describe the same thing. It also costs what opening
        // Options costs, which is the budget this screen already has.
        _alertPage.Children.Insert(0, _alerts.Header);
        foreach (var header in _alerts.Tabs()) _ = _alerts.Block(header.Tab);
        _ = _look.Block;
        _ = _behavior.Block;
        _ = _hud.Block;

        BuildTabs();
        ShowTab();
        _ready = true;
    }

    /// <summary>
    /// Land on a tab by its wire key.
    ///
    /// **Two levels, one address, and no third grammar.** <c>settings:hud</c> is a
    /// <see cref="SettingsSurface"/> key; <c>settings:crowd</c> is an
    /// <see cref="AlertSurface"/> one, which lands on Alerts and then on Crowd. Inventing
    /// <c>settings:alerts:crowd</c> would be a second address grammar for one destination —
    /// trap 33 lifted from data into navigation, which is the exact thing the one-address
    /// rule exists to prevent — and the alert families already HAVE keys that every other
    /// surface spells the same way.
    ///
    /// An unrecognised key is left alone rather than snapped to a default: showing the wrong
    /// tab silently is worse than showing the one already open, which is the refusal
    /// <c>ProgressWindow.SetTab</c> already makes and every room follows.
    /// </summary>
    public void SetTab(string key)
    {
        if (SettingsSurface.TabForKey(key) is { } tab)
        {
            _tab = tab;
        }
        else if (AlertSurface.TabForKey(key) is { } family)
        {
            _tab = SettingsTab.Alerts;
            _alertTab = family;
        }
        else return;

        BuildTabs();
        ShowTab();
    }

    /// <summary>
    /// Paint from the widget's snapshot — which for this room means re-badging the Alerts
    /// sub-strip and nothing else.
    ///
    /// **The badges are live and the controls are not, and that split is the whole of what
    /// this method is for** (trap 46: check what the old host was doing for the surface every
    /// tick). <c>OptionsWindow</c> does nothing on a tick at all — it is a modal-ish window
    /// built once — so there is no per-tick behaviour to carry across. But
    /// <c>AlertSurface.Tabs()</c> takes RUNNING spawn timers among its counts, and a badge
    /// that froze at its arrival value would be a number quietly going wrong on screen. The
    /// blocks themselves are never rebuilt here: a half-typed watch rule surviving the tick
    /// is the reason they are built once and kept.
    /// </summary>
    public void Render(StatsSnapshot s) => BuildTabs();

    /// <summary>
    /// **Window chrome the Behavior block cannot own: ROUTING a key press to an armed hotkey
    /// recorder.** <c>BuildHotkeyRows</c> replaces the button that was clicked, so nothing
    /// inside the block's panel has focus when the gesture lands and the tunnelling route
    /// never reaches it — the press arrives at the WINDOW. <c>OptionsWindow</c> has the same
    /// two lines in <c>OnPreviewKeyDown</c> for the same reason; here the window is the
    /// shell, so the route runs through <c>ShellWindow.OnShellKey</c> and this forwards it.
    ///
    /// The block owns every DECISION — which keys are gestures, what a rejection says, what
    /// Escape means — and the host owns only the route. A second copy of the gesture rule in
    /// a host is what <c>SettingsRoomTests</c>' negative on <c>HotkeyManager.Parse(</c>
    /// refuses, on both hosts.
    /// </summary>
    public bool HandleRecordingKey(KeyEventArgs e) => _behavior.HandleRecordingKey(e);

    /// <summary>Build the main strip and, on Alerts, the family sub-strip under it — both
    /// from Core (<see cref="SettingsSurface"/>, <see cref="AlertSurface"/>) so the room, the
    /// rail's palette and the address grammar cannot come to different ideas about what the
    /// tabs are.</summary>
    private void BuildTabs()
    {
        _tabs.Clear();
        foreach (var header in SettingsSurface.Tabs())
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, onClick: () => { _tab = tab; BuildTabs(); ShowTab(); });
        }
        // Chips first, THEN the selection.
        _tabs.Select(_tab);

        // The sub-strip is BUILT whichever tab is showing and only its VISIBILITY follows the
        // selection. A strip that was built lazily would report zero families in the dump
        // from every tab but one, which makes the assertion that the sub-strip exists an
        // assertion about the default tab instead (trap 34's shape).
        _alertStrip.Visibility = _tab == SettingsTab.Alerts
            ? Visibility.Visible
            : Visibility.Collapsed;

        _alertTabs.Clear();
        foreach (var header in _alerts.Tabs())
        {
            var family = header.Tab;
            _alertTabs.Add(header.Label, family, header.Badge,
                onClick: () => { _alertTab = family; BuildTabs(); ShowTab(); });
        }
        _alertTabs.Select(_alertTab);
    }

    /// <summary>Put the selected block in the body. Assigning <c>Content</c> re-parents the
    /// block, which is safe only because every one of them belongs to THIS instance — the
    /// factory's output, never a loan from another host (trap 45).</summary>
    private void ShowTab()
    {
        if (_tab == SettingsTab.Alerts) _alertBody.Content = _alerts.Block(_alertTab);
        _body.Content = _tab switch
        {
            SettingsTab.Alerts => _alertPage,
            SettingsTab.Hud => _hud.Block,
            SettingsTab.Behavior => _behavior.Block,
            _ => _look.Block,
        };
        // A tab switch starts at the top. Without this, arriving at Look from the bottom of
        // the watch-rule editor lands you halfway down the sliders with no indication that
        // there is anything above — the scroller is the HOST's (trap 36), so resetting it is
        // the host's job too.
        _scroll.ScrollToTop();
    }

    /// <summary>
    /// **Nothing to give back, and this is what was checked** (the obligation
    /// <see cref="IShellRoom.Release"/> names — an empty method with a reason is a decision,
    /// a missing one is a question nobody asked).
    ///
    /// The four blocks hold no timer, no token, no watcher and no file handle. Two things
    /// looked like candidates and are not: the armed hotkey RECORDER is a nullable string
    /// field on <see cref="SettingsBehaviorView"/> that nothing outside the block can see,
    /// and the ⤴ share button's "✓" revert is a one-shot <c>DispatcherTimer</c> that stops
    /// itself 1.5 seconds later and is created only by a click. <c>OptionsWindow.Closed</c>
    /// gives back exactly one thing — the alert tile's placement mode — and this room never
    /// entered it (see the class note above).
    /// </summary>
    public void Release() { }

    /// <summary>Nothing to arrange. All four tabs are ONE COLUMN of controls, the shape
    /// Options has had since 1.67.0: there is no list beside a detail pane to collapse, which
    /// is the only thing <see cref="ShellLayout.RoomSinglePane"/> decides. Empty with a
    /// reason rather than absent, per the interface's own contract.</summary>
    public void ApplyLayout(ShellLayout layout) { }

    /// <summary>
    /// The room's facts, under <c>shellSettings*</c>.
    ///
    /// **The four blocks are asked for the SAME strings <see cref="OptionsWindow"/> asks
    /// them for**, and both hosts re-key mechanically through
    /// <see cref="ShellDumpFacts.Prefixed"/> rather than hand-writing a list. The dump is one
    /// flat namespace, so two live hosts of one block would otherwise write over each other
    /// and every assertion on those keys would quietly start reading the other window
    /// (trap 58, which is trap 4 with the two sources being two hosts). A hand-written list
    /// here would also stop covering a block the day it gains a fifth fact — trap 30, again.
    ///
    /// **The prefix is <c>shellSettings</c> and the window's is <c>options</c>**, which are
    /// two spellings of one convention: <c>shell</c> plus the view's own key. These blocks'
    /// keys carry no room name (<c>lookPalettes</c>, <c>hudPanels</c>), the way
    /// <c>MapView</c>'s <c>mapZones</c> does not, so the room name goes in the prefix — and a
    /// bare <c>hudPanels</c> from the window would have landed beside the HUD BAR's own
    /// <c>hudCells</c> in the same flat namespace, which is the collision this whole helper
    /// exists to make impossible.
    /// </summary>
    public string DebugFacts() =>
        $"shellSettingsTab={SettingsSurface.KeyFor(_tab)} " +
        $"shellSettingsTabs={_tabs.Count} " +
        // The sub-strip, reported whether or not it is on screen: the family is half of what
        // an address like `settings:crowd` resolves to, and an assertion that could only be
        // made while Alerts happened to be selected would be an assertion about the default.
        $"shellSettingsFamily={AlertSurface.KeyFor(_alertTab)} " +
        $"shellSettingsFamilies={_alertTabs.Count} " +
        ShellDumpFacts.Prefixed("shellSettings", _look.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellSettings", _alerts.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellSettings", _hud.DebugFacts()) + " " +
        ShellDumpFacts.Prefixed("shellSettings", _behavior.DebugFacts());
}
