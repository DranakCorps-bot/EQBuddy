using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Evolved shell — one normal Windows window with a navigation rail down its left
/// edge and one room in it at a time. E-3 Phase 2 PR 1 landed **the host, the nav, and
/// exactly one room** (Progress), which is the World fold's own shape — host first — and
/// the only one that keeps a half-finished shell coherent at every commit. **PR 2 adds the
/// other two rooms that are a MOVE rather than a redesign**: World and Gear, whose Evolved
/// IA verdict (*"Keep → unify"*) a v1 fold has already satisfied. Three rows, three rooms.
///
/// **What is deliberately NOT here, and why each absence is a decision:**
///
///  * **Four of the seven rail rows.** <see cref="ShellPages.Landed"/> holds the rooms
///    that exist, and the rail draws that list rather than the full one. The honest
///    options for a half-built shell were a rail with the rooms that exist or seven rows
///    with the rest disabled, and this codebase already ruled on the second: *"an empty
///    class row gets no chevron — an affordance that opens nothing is a trap."* A room's
///    row lands in the PR that lands the room. Live does not exist yet (and Raids cannot
///    leave Progress until it does), Home and Search are new surfaces, Settings is a room
///    whose whole job is not being a launcher, and Quests is a lift rather than a host —
///    2,481 lines of window-owned rendering, which deserves its own diff.
///  * **The Search INDEX.** The palette resolves against what the shell can currently
///    reach — the landed rooms and their tabs. The disposition-backed index that lets a
///    player find a feature by its old v1 name is E-2e's table, and Helm's sign is
///    explicit that Search CHROME may land with E-3 while the index waits, and that the
///    Progress host must not be blocked on it. So the palette is honest and small rather
///    than an empty box promising more.
///  * **The Progress RESHAPE.** Bevel's IA moves Raids to Live and Faction to Advanced.
///    Both need a Live room to move Raids INTO, so this PR hosts the four tabs exactly as
///    they ship today — the pre-design says so in as many words: the tab arrangement is
///    not redesigned, only the window it used to float in. Doing half of it here would
///    drop a surface on the floor between two PRs.
///  * **The mini-dashboard stars.** They are the only writers <c>MiniStats</c> has for
///    "xp", "money" and "motes", and a fold that drops the last writer of a setting is
///    the exact shape of #204/#209, #210 and #212 (trap 20/26). They are NOT lost — this
///    PR does not retire <see cref="ProgressWindow"/>, which still carries them. **When
///    that window is retired they must be rehomed**, and Bevel's IA says where: HUD
///    configuration belongs to the HUD's Edit mode and to Settings, never to a room.
///
/// It is <see cref="IFollowingSurface"/> like the six theme pop-outs, so it follows the
/// widget's tick and can say which snapshot it last painted — without which the
/// <c>EQBUDDY_EXPAND</c> dump would describe two moments and every E2E wait on it would
/// have to get lucky (trap 56).
/// </summary>
public partial class ShellWindow : Window, IFollowingSurface
{
    private readonly MainWindow _main;

    private ShellPage _page = ShellPage.Progress;
    private DateTime _lastRefresh = DateTime.MinValue;

    /// <summary>The rail rows, by page, so a navigation can paint the selection without
    /// rebuilding the rail — and so the two states of a row (labelled, icon-only) are one
    /// object rather than two lists that have to agree.</summary>
    private readonly Dictionary<ShellPage, RailRow> _rows = [];

    private TextBlock _titleText = null!;
    private TextBox _searchInput = null!;
    private TextBlock _searchHint = null!;

    /// <summary>
    /// The rooms that have actually been OPENED, built on first arrival and kept.
    ///
    /// **Lazy on purpose, and it is a cost question rather than a style one.** Progress is
    /// arithmetic over a snapshot the widget already holds, but <see cref="WorldRoom"/>
    /// starts a one-second <c>DispatcherTimer</c> and reads the spawn ledger off disk, and
    /// <see cref="GearRoom"/> scans the game folder. Building all three in the constructor
    /// would pay every one of those costs on a shell opened to look at experience — which
    /// is exactly the argument <c>SurfaceOwnershipTests</c> already records for World
    /// having FOUR factories instead of one combined set: construction-time work a shared
    /// factory would fire needlessly for every sibling.
    /// </summary>
    private readonly Dictionary<ShellPage, IShellRoom> _rooms = [];

    public ShellWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;

        // Derived, not typed: the floor is a room's minimum plus the rail collapsed to
        // icons, and both halves live in ShellLayoutPolicy where a unit test can reach
        // them. A number typed here as well would be a second producer of one fact.
        MinWidth = ShellLayoutPolicy.MinWidth;
        MinHeight = ShellLayoutPolicy.MinHeight;

        // **Every room gives back what it borrowed, once.** SpawnsView owns a ticking
        // DispatcherTimer and InventoryView holds a CancellationTokenSource; both are
        // released by the v1 windows' own Closed handlers, and a shell that did not do the
        // same would leak one of each per open, for the life of the process, with nothing
        // in a diff, a test or a screenshot able to see it. Trap 46's rule — check what the
        // old host was doing for the surface — covers close as well as tick.
        Closed += (_, _) => { foreach (var room in _rooms.Values) room.Release(); };

        BuildTitleRow();
        BuildRail();
        ApplyLayout();
        SizeChanged += (_, _) => ApplyLayout();

        // Window-wide, so it fires whichever room has focus — the pattern this codebase
        // already reaches for when a behaviour must apply regardless of the focused
        // control. A KeyDown handler on the window itself is enough here because the
        // shell owns its whole visual tree; nothing in a room swallows Ctrl+K.
        PreviewKeyDown += OnShellKey;

        Navigate(ShellPages.Address(ShellPage.Progress));
    }

    // ---- chrome ----------------------------------------------------------------

    private void BuildTitleRow()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Tray", "AccentBrush", size: Tok.IconInlineHit));
        _titleText = DesignSystem.Text(Role.TitleWindow, "EQBuddy");
        _titleText.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        _titleText.Ink("AccentBrush");
        TitleRow.Children.Add(_titleText);

        // The Search affordance. A WPF TextBox has no placeholder, so the hint is a
        // TextBlock behind it that hides the moment there is anything to read — rather
        // than seeding the box with grey text, which is indistinguishable from a real
        // query to every caller that reads .Text.
        SearchBox.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SearchBox.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var glass = DesignSystem.Icon("Search", size: Tok.IconInline);
        glass.Margin = new Thickness(0, 0, Tok.SpaceXs, 0);
        SearchBox.Children.Add(glass);

        _searchInput = new TextBox
        {
            Width = Tok.TipWidth,
            Height = Tok.ControlHeight,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search EQBuddy — or press Ctrl+K",
        };
        _searchInput.SetResourceReference(BackgroundProperty, "ComboBoxBrush");
        _searchInput.SetResourceReference(ForegroundProperty, "TextBrush");
        _searchInput.SetResourceReference(BorderBrushProperty, "BorderBrush");
        _searchInput.TextChanged += (_, _) => RenderPalette();
        _searchInput.GotKeyboardFocus += (_, _) => OpenPalette();
        Grid.SetColumn(_searchInput, 1);
        SearchBox.Children.Add(_searchInput);

        _searchHint = DesignSystem.Text(Role.Caption, "Search  Ctrl+K");
        _searchHint.Ink("DimBrush");
        _searchHint.IsHitTestVisible = false;
        _searchHint.VerticalAlignment = VerticalAlignment.Center;
        _searchHint.Margin = new Thickness(Tok.SpaceM, 0, 0, 0);
        Grid.SetColumn(_searchHint, 1);
        SearchBox.Children.Add(_searchHint);
    }

    /// <summary>Build the rail from <see cref="ShellPages.RailOrder"/>, drawing only the
    /// rooms that have landed, and splitting at the gap. One loop over one list: the rail
    /// cannot name a room the enum does not have, and cannot miss one the enum gains.
    /// </summary>
    private void BuildRail()
    {
        foreach (var page in ShellPages.RailOrder)
        {
            if (!ShellPages.Landed.Contains(page)) continue;
            var row = new RailRow(page, () => Navigate(ShellPages.Address(page)));
            _rows[page] = row;
            (ShellPages.BelowTheGap(page) ? RailBelowGap : RailRows).Children.Add(row);
        }
    }

    // ---- navigation ------------------------------------------------------------

    /// <summary>
    /// THE ONE NAVIGATION PATH. The rail calls it, the Ctrl+K palette calls it, and the
    /// <c>EQBUDDY_SHELL</c> hook calls it — with the same <c>page:room</c> address
    /// grammar <c>EQBUDDY_EXPAND</c> has taken since 2026-08-26.
    ///
    /// Two ways to land on a room is trap 33 lifted from data into navigation: not a
    /// stale answer and a fresh one, but two answers a later change has to be taught
    /// twice. When the HUD grows an "Open EQBuddy" button and Search grows a real index,
    /// they resolve here or they are a second product.
    ///
    /// An unrecognised address is left alone rather than snapped to a default: silently
    /// showing the wrong room is worse than showing the one already open, which is the
    /// rule <see cref="ProgressWindow.SetTab"/> already follows.
    /// </summary>
    public void Navigate(string? address)
    {
        if (ShellPages.ParseAddress(address) is not { } target) return;
        if (!ShellPages.Landed.Contains(target.Page)) return;

        _page = target.Page;
        // The title bar carries the room, the way every shell application does — and it
        // is native chrome, so this is the one place the window's name is drawn for free.
        //
        // **It is also the only thing that can tell this window from the widget.** Both
        // are "EQBuddy" to the player, and `MainWindow.xaml` sets exactly that string, so
        // `shot.ps1`'s `-TitleLike` would match either — trap 24 arriving INSIDE one
        // process, where `-OwnerPid` cannot help because both windows have the same
        // owner. `HistoryWindow` already solved this the same way ("EQBuddy — Session
        // History"), and unlike a suffix invented for the harness this one is what the
        // player should see anyway.
        Title = $"EQBuddy — {ShellPages.Label(_page)}";
        foreach (var (page, row) in _rows) row.Select(page == _page);

        var landed = RoomFor(_page);
        RoomHost.Content = landed?.Body;
        if (target.Room is { Length: > 0 } room) landed?.SetTab(room);

        ClosePalette();
        Refresh(force: true);
    }

    /// <summary>
    /// The room for a page, built on first arrival and kept afterwards.
    ///
    /// **This switch is the ONE place a room is named**, which is the whole reason
    /// <see cref="IShellRoom"/> exists. Before it there were four of them — the content
    /// cell, the address's room half, the paint and the dump — and four hand-written
    /// switches over one list of rooms is trap 30's shape: a staging list is code that
    /// cannot be type-checked, and the failure is not an error, it is a room that paints on
    /// arrival and never again because whoever added it updated three.
    /// </summary>
    private IShellRoom? RoomFor(ShellPage page)
    {
        if (_rooms.TryGetValue(page, out var existing)) return existing;
        IShellRoom? room = page switch
        {
            ShellPage.Progress => new ProgressRoom(_main),
            ShellPage.Gear => new GearRoom(_main),
            ShellPage.World => new WorldRoom(_main),
            _ => null,
        };
        if (room is not null) _rooms[page] = room;
        return room;
    }

    /// <summary>A new inventory dump landed; the Gear room re-reads it if it is the one on
    /// screen. Reached through <c>FollowingSurfaces.InventoryChanged</c>, which is what
    /// makes sure the shell and <c>GearLootWindow</c> both hear about it — a notification
    /// that reached only one of two hosts would leave the other showing the old bags, which
    /// is the "EQBuddy did nothing" reading the auto-import exists to prevent.</summary>
    public void InventoryChanged()
    {
        if (_rooms.TryGetValue(ShellPage.Gear, out var gear)) ((GearRoom)gear).InventoryChanged();
    }

    // ---- Ctrl+K palette --------------------------------------------------------

    private void OnShellKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            OpenPalette();
            _searchInput.Focus();
            _searchInput.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && PaletteLayer.Visibility == Visibility.Visible)
        {
            ClosePalette();
            e.Handled = true;
        }
    }

    private void OpenPalette()
    {
        PaletteLayer.Visibility = Visibility.Visible;
        RenderPalette();
    }

    private void ClosePalette()
    {
        PaletteLayer.Visibility = Visibility.Collapsed;
        PaletteBody.Children.Clear();
    }

    /// <summary>
    /// The palette's results. It indexes what the shell can actually REACH today — the
    /// landed rooms and the rooms inside them — and says so when it finds nothing, in the
    /// inventory-dump voice: what is missing, and what will change it. A search box that
    /// answers "no results" for a feature that exists is worse than one that admits its
    /// index is small.
    ///
    /// The disposition-backed index (find a feature by its OLD v1 name) is E-2e's table
    /// and is explicitly not this PR's to block on.
    /// </summary>
    private void RenderPalette()
    {
        PaletteBody.Children.Clear();
        var query = _searchInput.Text.Trim();
        _searchHint.Visibility = query.Length == 0 && !_searchInput.IsKeyboardFocused
            ? Visibility.Visible
            : Visibility.Collapsed;

        var hits = 0;
        foreach (var (label, address, detail) in Index())
        {
            if (query.Length > 0
                && label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                && detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            hits++;
            PaletteBody.Children.Add(Result(label, detail, address));
        }

        if (hits > 0) return;
        var empty = DesignSystem.Text(Role.BodySecondary,
            query.Length == 0
                ? "Type to jump to a room."
                : $"Nothing here matches “{query}”. EQBuddy can search the rooms it "
                  + "has — more arrive as each one is built.");
        empty.Ink("DimBrush");
        empty.TextWrapping = TextWrapping.Wrap;
        PaletteBody.Children.Add(empty);
    }

    /// <summary>Everything the palette can currently land on, as
    /// (label, <c>page:room</c> address, one-line detail).</summary>
    private static IEnumerable<(string Label, string Address, string Detail)> Index()
    {
        foreach (var page in ShellPages.RailOrder)
        {
            if (!ShellPages.Landed.Contains(page)) continue;
            yield return (ShellPages.Label(page), ShellPages.Address(page),
                ShellPages.Describe(page));
            // The rooms INSIDE the room, from the same Core definition each room's tab
            // strip is built from (`ShellPages.Rooms`) — so the palette cannot offer a
            // room that does not exist, or miss one a surface gains, and cannot spell one
            // differently from the address the rail resolves.
            foreach (var (label, key) in ShellPages.Rooms(page))
                yield return ($"{ShellPages.Label(page)} · {label}",
                    ShellPages.Address(page, key), ShellPages.Describe(page));
        }
    }

    private FrameworkElement Result(string label, string detail, string address)
    {
        var row = new StackPanel { Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceXxs) };
        var head = DesignSystem.Text(Role.Body, label);
        head.Ink("TextBrush");
        row.Children.Add(head);
        var sub = DesignSystem.Text(Role.Metadata, detail);
        sub.Ink("DimBrush");
        sub.TextWrapping = TextWrapping.Wrap;
        row.Children.Add(sub);
        DesignSystem.WireClick(row, () => Navigate(address));
        return row;
    }

    // ---- degrade ---------------------------------------------------------------

    /// <summary>Apply the layout policy for this width. The arithmetic is in
    /// <see cref="ShellLayoutPolicy"/> where a unit test can reach it; this method is the
    /// wiring, which is all the WPF layer should ever hold of a sum.</summary>
    private void ApplyLayout()
    {
        var layout = ShellLayoutPolicy.For(ActualWidth);
        RailColumn.Width = new GridLength(layout.RailWidth);
        foreach (var row in _rows.Values) row.ShowLabel(layout.RailLabelsVisible);
    }

    // ---- following the widget's tick -------------------------------------------

    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 1) Refresh(force: false);
    }

    void IFollowingSurface.MaybeFollow() => MaybeRefresh();
    void IFollowingSurface.PaintNow() => Refresh(force: false);

    /// <summary>The snapshot version this window last PAINTED — the dump counts the open
    /// surfaces that are behind, and it can only do that if each one says.</summary>
    public long RenderedVersion { get; private set; } = -1;

    /// <summary>Only the VISIBLE room paints. The others are built and idle, which is the
    /// same split every theme window makes between its active tab and the rest — and the
    /// same reason: a hidden surface still measures on every layout pass, so painting one
    /// costs the tick for nothing anybody can see (trap 46's cost half).</summary>
    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        var s = _main.CurrentSnapshot();
        RenderedVersion = s.Version;
        if (_rooms.TryGetValue(_page, out var room)) room.Render(s);
    }

    /// <summary>The shell's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established. The WPF layer has no unit tests, so an
    /// assertion from <c>tests/EQBuddy.E2E</c> against these keys is the only thing
    /// between this window and a silent regression.
    ///
    /// <c>shellRail</c> is the count of rows DRAWN, which is the fact worth pinning: the
    /// day a room lands without joining <see cref="ShellPages.Landed"/> — or a row is
    /// drawn for a room that does not exist — this number is what says so.
    ///
    /// **Only the CURRENT room's facts are reported, and that is trap 56's rule rather
    /// than laziness.** Rooms are built on first arrival and kept, but only the visible one
    /// paints; reporting an idle room's numbers would put a second MOMENT in a dump whose
    /// whole contract is to describe one, and every E2E wait on it would then have to get
    /// lucky. <c>shellRooms</c> says how many have been built, so "the room I asked for is
    /// not the one reporting" is answerable rather than inferred.</summary>
    public string DebugFacts() =>
        $"shellPage={ShellPages.Key(_page)} " +
        $"shellRail={_rows.Count} " +
        $"shellRooms={_rooms.Count} " +
        // The INPUT beside the ANSWER. A hosted CI runner is 1024×768, so an E2E test
        // that asserted "the rail shows labels" would be asserting the desk it was
        // written on; asserting that the answer follows from the width it was computed
        // against is a relationship, and holds on any monitor.
        $"shellWidth={(int)ActualWidth} " +
        $"shellRailLabels={(ShellLayoutPolicy.For(ActualWidth).RailLabelsVisible ? 1 : 0)} " +
        $"shellSearch={(SearchBox.IsVisible ? 1 : 0)} " +
        $"shellPalette={(PaletteLayer.Visibility == Visibility.Visible ? 1 : 0)} " +
        (_rooms.TryGetValue(_page, out var shown) ? shown.DebugFacts() : "");
}
