using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>What the Quest Tracker needs from the shell. Mirrors the WPF MainWindow's
/// quest surface member-for-member so the integration pass can implement it on the
/// Avalonia MainWindow verbatim and construct the window with <c>this</c>.</summary>
public interface IQuestsHost
{
    AppSettings Settings { get; }
    QuestCatalog QuestCatalog { get; }
    QuestLedgerStore? QuestLedger { get; }
    ZoneGraph ZoneGraph { get; }
    string QuestCharacterKey { get; }
    string CurrentZoneName { get; }
    StatsSnapshot CurrentSnapshot();

    /// <summary>The character's classes and where they came from
    /// (<see cref="CharacterClasses.Resolve"/>): the achievements dump leads, the log fills
    /// in, and the Quest Tracker's own picks come last and only WIDEN.
    ///
    /// On the interface rather than reached for, so this window cannot go back to reading
    /// <c>CurrentSnapshot().InferredClass</c> — which was one class where a Legends
    /// character has three, and is what made this window filter to a Warrior's quests for
    /// a Warrior/Druid/Monk.</summary>
    (IReadOnlyList<string> Classes, ClassSource Source) ClassSourceFor(StatsSnapshot s);

    /// <summary>What the last unprompted <c>/outputfile achievements</c> import did, for the
    /// Sky tab to report (Bevel, Helm-signed 2026-08-23). The dump feeds two consumers and
    /// the report used to sit only on Raids — so "1 Sky reward marked · 2 skipped" was read
    /// above a list of raid bosses by a player who may never open that surface.</summary>
    AutoImportOutcome? LastAchievementsImport { get; }

    /// <summary>Race and class unlocks from the newest `/outputfile achievements` dump,
    /// and where the character stands with every faction from `/outputfile faction`
    /// (Hateborne, 2026-08-25). On the interface for the same reason ClassSourceFor is: the
    /// window must not go and re-derive them, or the two lanes drift.</summary>
    IReadOnlyList<UnlockProgress> RaceUnlocks { get; }
    IReadOnlyList<UnlockProgress> ClassUnlocks { get; }
    FactionsFile.Snapshot? LatestFactions { get; }
    /// <summary>Has an achievements dump ever been read? "No dump" and "nothing unlocked"
    /// are different states and the tab must not show the first as the second.</summary>
    bool HasUnlockDump { get; }

    InventoryFile.Snapshot? LatestInventory(bool refresh = false);
    string? CachedItemStats(string itemName);
    Task<string?> FetchItemTooltip(string itemName);
}

/// <summary>
/// The standalone Quest Tracker (QUEST-*, David's spec 2026-08-07): every wiki quest
/// whose turn-in items overlap what this character owns — looted since the ledger began,
/// or read from the game's own /outputfile inventory dump (bags and bank). The quest name
/// opens the eqlwiki walkthrough; "all" flips from the overlap view to the whole catalog.
///
/// GATE 2 of the UI/UX rework (docs/DesignSystem.md) rebuilt the presentation and NOTHING
/// else, in the same change as its WPF twin — never "a release behind", which is the
/// discipline that stops this build re-shipping a bug Windows already fixed. A column of
/// self-contained cards became a LIST plus a DETAIL PANE: the list answers "which quest",
/// the pane answers "what about it".
///
/// Every size, radius and spacing value comes from EQBuddy.UI.Shared.DesignTokens and
/// every icon from IconPaths, which is what the WPF window composes too.
/// </summary>
public sealed class QuestsWindow : Window
{
    private readonly IQuestsHost _main;
    private readonly AppSettings _settings;
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _mode = "mine";   // mine = items+pins · zone = current zone · all
    // Snapshot of the ledger's owned dict as of the last Refresh, kept for the detail
    // pane: Select()/BuildDetail() run off a click, not a refresh, and need the raw
    // Verified/VerifiedAt fields Progressed() already collapsed to Total.
    private IReadOnlyDictionary<string, QuestLedgerStore.Entry> _owned =
        new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
    private bool _restored;
    private PixelPoint _placed;
    /// <summary>The last on-screen position, so Closed never persists a torn-down
    /// window's 0,0 (#169).</summary>
    private LastVisiblePosition _seen;
    /// <summary>Rows built per view, and how many were withheld — the whole catalog is
    /// 1,172 quests. Rows are far cheaper than the cards they replace (no rewards, no
    /// item rows, no wiki tooltips until a row is SELECTED), but "all" still offers all
    /// of them, and doing that per keystroke is what hung the window.</summary>
    private const int RenderCap = 60;
    private int _renderedCount;
    private int _suppressed;
    private static readonly TimeSpan SearchSettle = TimeSpan.FromMilliseconds(120);
    private DispatcherTimer? _searchDebounce;

    private readonly TextBlock _titleText = DesignSystem.Text(Role.TitleWindow, "Quest Tracker");
    private readonly TextBox _filterBox = InputBox(
        "Search the whole catalog by anything you know — the reward you want "
        + "(\"Wakizashi of the Frozen Skies\"), a turn-in item, the quest name, the "
        + "quest giver, a zone. Search ignores the class/era/state filters. "
        + "Pin a result to track it.");
    private readonly ComboBox _eraCombo = new()
    {
        Width = 104,
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        Height = DesignTokens.ControlHeight,
    };
    private readonly ComboBox _stateCombo = new()
    {
        Width = 86,
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        Height = DesignTokens.ControlHeight,
    };
    // The Unlocks tab's own lens. Shares the era combo's slot because the two are never
    // visible together — see ApplyTabVisual.
    private readonly ComboBox _unlockSectionCombo = new()
    {
        Width = 104,
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        Height = DesignTokens.ControlHeight,
        IsVisible = false,
    };
    private Button _scanBtn = null!;
    private readonly Button _classBtn;
    private readonly StackPanel _classCheckPanel = new();
    private readonly StackPanel _questsPanel = new();
    private readonly ScrollViewer _bodyScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
    private readonly StackPanel _detailPane = new();
    private readonly ScrollViewer _detailScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceM),
    };
    private Border _detailCard = null!;
    private Grid _bodyGrid = null!;
    /// <summary>The one-line answer above the list: how many of what is showing can be
    /// turned in right now. Hidden entirely when nothing is — "0 quests ready" reads as
    /// a fault in the tracker rather than an ordinary afternoon.</summary>
    private readonly StackPanel _summaryRow = new()
    {
        Orientation = Orientation.Horizontal, IsVisible = false,
        Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS),
    };

    // ---- top-level tabs: General · Epic 1.0 · Plane of Sky ----
    //
    // Built from Core's QuestSurface, exactly as the WPF window builds them, so the two
    // desktops and EQBuddy Mobile cannot disagree about which tabs exist, their order or
    // their names — the reason that type lives in Core at all.
    private QuestTab _tab = QuestTab.General;
    // A WrapPanel, not a StackPanel (trap 25) — see the note on the WPF twin's XAML. The
    // Progress window on this lane was fixed for the same reason; this strip was not,
    // and it is about to carry a fourth chip.
    private readonly WrapPanel _tabStrip = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS),
    };
    /// <summary>Which single class the view is narrowed to, or null for all of yours.
    /// Session-scoped like the search box: a sticky lens reads as a broken tracker
    /// tomorrow when you have swapped classes.</summary>
    private string? _classLens;
    // Wraps, because the strip lists whatever the picker holds and that can be all
    // sixteen: a fixed-width window clipped it (#184).
    private readonly WrapPanel _classStrip = new();
    private readonly StackPanel _modeStrip = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };
    // The tabs, the class lens and the mode strip are three features and ONE shape
    // (EqChip / EqSegmentedStrip, gate 2b).
    private readonly EqSegmentedStrip _tabs;
    private readonly EqSegmentedStrip _classes;
    private readonly EqSegmentedStrip _modes;

    // ---- undo (#184) ----
    // A tick is one click and saves at once, so without this a mis-click is unrecoverable
    // except from memory — bjstrange lost three and had to work out what had been checked.
    private readonly Stack<(string Label, bool Was, Action<bool> Set)> _undo = new();
    private readonly TextBlock _undoText = DesignSystem.Text(Role.BodySecondary);
    private readonly Border _undoBar = new()
    {
        IsVisible = false,
        Margin = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceXs, DesignTokens.SpaceL, 0),
        CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS),
    };
    /// <summary>The Epic tab's classic-era lens, which followed the widget's Epic card
    /// here. Persisted, because EQBuddy Mobile's Epic tab honors the same setting.</summary>
    private readonly CheckBox _classicOnlyCheck = new()
    {
        Content = "Classic-doable only",
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The Sky tab's island lens — the twin of the Epic one above, sharing its
    /// column because the two are never visible at once.</summary>
    private readonly CheckBox _islandRepeatCheck = new()
    {
        Content = "Repeat multi-island steps",
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public QuestsWindow(IQuestsHost main)
    {
        _main = main;
        _settings = main.Settings;
        Title = "EQBuddy Quest Tracker";
        Width = 880;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;

        _classBtn = ActionButton("Any class");
        ToolTip.SetTip(_classBtn, "Pick your class(es) — quests any of them can do stay visible");
        _tabs = new EqSegmentedStrip(_tabStrip);
        _classes = new EqSegmentedStrip(_classStrip);
        _modes = new EqSegmentedStrip(_modeStrip);
        Content = BuildContent();
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "quests", _settings, baseWidth: Width);
        BuildClassChecks();
        BuildModeStrip();
        _eraCombo.Items.Add("Any era");
        foreach (var era in QuestEraLadder.Eras) _eraCombo.Items.Add($"≤ {era}");
        var savedEra = Array.IndexOf(QuestEraLadder.Eras, _settings.QuestEraFilter);
        _eraCombo.SelectedIndex = savedEra >= 0 ? savedEra + 1 : 0;
        // From Core, so the combo, the checklist filter and EQBuddy Mobile cannot end up
        // offering three different vocabularies for one lens.
        foreach (var s in QuestChecklistLayout.States) _stateCombo.Items.Add(s);
        _stateCombo.SelectedIndex = 0;
        foreach (var s in UnlockLayout.Sections) _unlockSectionCombo.Items.Add(s);
        _unlockSectionCombo.SelectedIndex = 0;
        _unlockSectionCombo.SelectionChanged += (_, _) =>
        {
            if (_unlockSectionCombo.SelectedItem is string s) _unlockSection = s;
            Refresh(force: true);
        };
        _eraCombo.SelectionChanged += (_, _) => OnEraChanged();
        _stateCombo.SelectionChanged += (_, _) => OnStateChanged();

        PointerPressed += OnDrag;
        Opened += (_, _) =>
        {
            UpdateHeightLimit();
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            _restored = ScreenGuard.OnScreen(this, _settings.QuestsLeft, _settings.QuestsTop, Width, 200);
            if (_restored)
                Position = new PixelPoint((int)_settings.QuestsLeft, (int)_settings.QuestsTop);
            else if (Screens.Primary is { } screen)
                Position = new PixelPoint(
                    screen.WorkingArea.X
                        + (screen.WorkingArea.Width - (int)(Width * screen.Scaling)) / 2,
                    screen.WorkingArea.Y + 80);
            _placed = Position;
        };
        PositionChanged += (_, _) =>
        {
            UpdateHeightLimit();
            _seen.Observe(Position.X, Position.Y, IsVisible);
        };
        Closed += (_, _) =>
        {
            // A closing window reports 0,0 on X11/Wayland; persist only what was seen
            // while it was on screen, else leave the saved spot alone (#169).
            var (curX, curY) = _seen.Or(_settings.QuestsLeft, _settings.QuestsTop);
            (_settings.QuestsLeft, _settings.QuestsTop) = WindowPlacement.PositionToPersist(
                _restored, _placed.X, _placed.Y, curX, curY,
                _settings.QuestsLeft, _settings.QuestsTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    private Control BuildContent()
    {
        _titleText.Foreground = AppTheme.AccentBrush;
        _titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
        };
        titleRow.Children.Add(DesignSystem.Icon("Quest", "AccentBrush", size: 15));
        titleRow.Children.Add(_titleText);
        var close = DesignSystem.IconButton("Close", "Close", Close);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { Margin = CardPad };
        header.Children.Add(titleRow);
        header.Children.Add(close);

        // One search box is the way in (David, 2026-08-15), and it now spans the header,
        // which is the whole point: it was "so compressed I missed it" sharing a row with
        // a button. Declaring owned items by hand is the worse half of that trade —
        // /outputfile inventory reads bags AND bank exactly, and the button beside the box
        // hands over the command.
        var searchRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS),
        };
        // Avalonia has a real watermark, so no overlay is needed (WPF's needs one).
        _filterBox.Watermark = "Search a reward, item, quest, or NPC…";
        // Rebuild after typing STOPS, not per keystroke — the rebuild is synchronous and
        // "all" hands it the whole catalog, so nine characters meant nine full rebuilds
        // and an apparently hung window (David, 2026-08-15). Mirrors WPF's debounce.
        _filterBox.TextChanged += (_, _) =>
        {
            _searchDebounce ??= BuildDebounce();
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };

        DispatcherTimer BuildDebounce()
        {
            var t = new DispatcherTimer { Interval = SearchSettle };
            t.Tick += (_, _) => { t.Stop(); Refresh(force: true); };
            return t;
        }
        searchRow.Children.Add(_filterBox);
        _scanBtn = ActionButton("");
        _scanBtn.Content = DesignSystem.IconLabel("Copy", "scan bags");
        _scanBtn.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        ToolTip.SetTip(_scanBtn,
            "Copy /outputfile inventory — paste it in the game's chat, and EQBuddy reads "
            + "what your bags and bank already hold. The held tab then shows every quest "
            + "you could turn in right now.");
        _scanBtn.Click += async (_, _) => await CopyInventoryCmdAsync(_scanBtn);
        Grid.SetColumn(_scanBtn, 1);
        searchRow.Children.Add(_scanBtn);

        var filterRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*"),
        };
        ToolTip.SetTip(_eraCombo,
            "Hide quests from later eras than the world has (unmarked quests always show)");
        filterRow.Children.Add(_eraCombo);
        // Column 0 as well: the era combo and this one are never visible at the same time,
        // because era is a catalog concept and Unlocks is not a catalog.
        ToolTip.SetTip(_unlockSectionCombo,
            "Show every unlock, or narrow to races or to classes");
        Grid.SetColumn(_unlockSectionCombo, 0);
        filterRow.Children.Add(_unlockSectionCombo);
        // State filter (Reddit ask, 2026-08-11): every tab and search can narrow to
        // open / ready / completed.
        ToolTip.SetTip(_stateCombo,
            "Any state · open (not yet completed) · ready (turn-ins in hand) · done (marked completed)");
        _stateCombo.Margin = Gap;
        Grid.SetColumn(_stateCombo, 1);
        filterRow.Children.Add(_stateCombo);
        // Multiclass filter (Legends: up to 3 active classes): a checkbox flyout
        // (WPF's Popup, StaysOpen=false → light dismiss), selection remembered
        // per character.
        _classBtn.Margin = Gap;
        _classBtn.Flyout = new Flyout
        {
            Placement = PlacementMode.Bottom,
            Content = new Border
            {
                Background = AppTheme.PopupBrush,
                CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
                BorderBrush = AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                Padding = CardPad,
                Child = _classCheckPanel,
            },
        };
        Grid.SetColumn(_classBtn, 2);
        filterRow.Children.Add(_classBtn);
        // Shares a row with the mode strip: the two are never visible at once, because
        // one is a catalog control and the other is the Epic tab's own lens.
        _classicOnlyCheck.Foreground = AppTheme.DimBrush;
        _classicOnlyCheck.Margin = Gap;
        _classicOnlyCheck.IsVisible = false;
        _classicOnlyCheck.IsChecked = _settings.EpicQuestClassicOnly;
        ToolTip.SetTip(_classicOnlyCheck,
            "Show only the steps doable on a classic-era server — the rest are hidden "
            + "from both the list and the counts, so the score means what it says.");
        _classicOnlyCheck.IsCheckedChanged += (_, _) =>
        {
            if (_settings.EpicQuestClassicOnly == (_classicOnlyCheck.IsChecked == true)) return;
            _settings.EpicQuestClassicOnly = _classicOnlyCheck.IsChecked == true;
            _settings.Save();
            Refresh(force: true);
        };
        Grid.SetColumn(_classicOnlyCheck, 3);
        filterRow.Children.Add(_classicOnlyCheck);
        _islandRepeatCheck.Foreground = AppTheme.DimBrush;
        _islandRepeatCheck.Margin = Gap;
        _islandRepeatCheck.IsVisible = false;
        _islandRepeatCheck.IsChecked = _settings.SkyStepsUnderEveryIsland;
        ToolTip.SetTip(_islandRepeatCheck,
            "Some Sky pieces drop on several islands. Off: each is listed once, under "
            + "\"Several islands\". On: each appears under every island it drops on, so one "
            + "island's list is complete — the same step then shows more than once.");
        _islandRepeatCheck.IsCheckedChanged += (_, _) =>
        {
            if (_settings.SkyStepsUnderEveryIsland == (_islandRepeatCheck.IsChecked == true)) return;
            _settings.SkyStepsUnderEveryIsland = _islandRepeatCheck.IsChecked == true;
            _settings.Save();
            Refresh(force: true);
        };
        Grid.SetColumn(_islandRepeatCheck, 3);
        filterRow.Children.Add(_islandRepeatCheck);
        Grid.SetColumn(_modeStrip, 4);
        filterRow.Children.Add(_modeStrip);

        var entry = new StackPanel { Margin = CardPad };
        entry.Children.Add(_tabStrip);
        entry.Children.Add(searchRow);
        entry.Children.Add(filterRow);
        entry.Children.Add(_classStrip);

        // LIST + DETAIL. On the Epic and Sky tabs there is nothing to select — those are
        // fixed checklists, not a catalog — so the detail column collapses and the list
        // takes the whole width.
        _bodyScroll.Content = _questsPanel;
        var master = new DockPanel();
        DockPanel.SetDock(_summaryRow, Dock.Top);
        master.Children.Add(_summaryRow);
        master.Children.Add(_bodyScroll);

        _detailScroll.Content = _detailPane;
        _detailCard = new Border
        {
            Background = AppTheme.PanelBrush,
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0),
            Child = _detailScroll,
        };
        _bodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("400,*"),
            Margin = CardPad,
        };
        _bodyGrid.Children.Add(master);
        Grid.SetColumn(_detailCard, 1);
        _bodyGrid.Children.Add(_detailCard);

        // The accuracy contract, said plainly (David, 2026-08-11): we mirror the wiki,
        // we're exactly as right as it is, and the door to fixing BOTH swings from here.
        var footer = new StackPanel { Margin = CardPad };
        footer.Children.Add(Footnote(
            "After you scan bags, the count is your dump, then the log since. Hand-ins " +
            "aren't in the log — use Mark as turned in, or right-click a row to clear it."));
        var wiki = Footnote(
            "Every quest here mirrors eqlwiki.com — verified item-for-item against it, so " +
            "EQBuddy is exactly as accurate as the wiki is today. Spot something wrong? The " +
            "report control on the detail pane tells us; fixing the wiki page itself fixes it " +
            "for everyone (the catalog re-harvests weekly). Your own discoveries flow back " +
            "too: Drops by Creature marks wiki-unknown drops in red with a paste-ready page edit.");
        wiki.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        footer.Children.Add(wiki);

        BuildUndoBar();

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Children.Add(header);
        Grid.SetRow(entry, 1);
        layout.Children.Add(entry);
        Grid.SetRow(_bodyGrid, 2);
        layout.Children.Add(_bodyGrid);
        Grid.SetRow(_undoBar, 3);
        layout.Children.Add(_undoBar);
        Grid.SetRow(footer, 4);
        layout.Children.Add(footer);
        return new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.RadiusPanel),
            Child = layout,
        };
    }

    private static Thickness CardPad => new(
        DesignTokens.SpaceL, DesignTokens.SpaceM, DesignTokens.SpaceL, DesignTokens.SpaceM);

    private static Thickness Gap => new(DesignTokens.SpaceS, 0, 0, 0);

    private static TextBlock Footnote(string text)
    {
        var block = DesignSystem.Text(Role.Metadata, text);
        block.TextWrapping = TextWrapping.Wrap;
        return block;
    }

    // ---- chips: the one primitive behind the tabs, the class lens and the mode strip ----

    /// <summary>Build the strip from Core's <see cref="QuestSurface"/> so this window,
    /// the WPF one and EQBuddy Mobile cannot disagree about which tabs exist, their
    /// order or their names.</summary>
    private void BuildTabs()
    {
        _tabs.Clear();
        foreach (var header in QuestSurface.Tabs(EpicCounts(), SkyCounts(), UnlockCounts()))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Badge, onClick: () =>
            {
                _tab = tab;
                // The theme host follows the player (Inline themes PR 3).
                TabChanged?.Invoke(tab);
                ApplyTabVisual();
                Refresh(force: true);
            });
        }
        // Chips first, THEN the paint: ApplyTabVisual colours the chip list, so colouring
        // before rebuilding it would leave every fresh chip unstyled — including the
        // selected one, which is the whole signal.
        BuildClassStrip();
        ApplyTabVisual();
    }

    private void BuildModeStrip()
    {
        foreach (var (key, tip) in new[]
        {
            ("mine", "Quests matching your items and pins"),
            ("zone", "Everything you can work on in the zone you're in"),
            ("held", "Quests you could turn in with what your bags already hold — " +
                $"from the game's {GameCommands.OutputfileInventory} dump. In game, type " +
                $"{GameCommands.OutputfileInventory}, and this tab reads the file the game writes."),
            ("done", "Quests you've marked completed — every quest can be marked done, so " +
                "returning players can check off history"),
            ("all", "The whole quest catalog"),
        })
        {
            var mode = key;
            _modes.Add(key, key, tip: tip, onClick: () =>
            {
                _mode = mode;
                ApplyModeVisual();
                Refresh(force: true);
            });
        }
        ApplyModeVisual();
    }

    /// <summary>Any · one of your classes. The class picker still decides WHICH classes
    /// you have; this decides which of them you are looking at right now, which is a
    /// different question and wanted far more often.</summary>
    private void BuildClassStrip()
    {
        _classes.Clear();
        // The RESOLVED list, not the picks with one inferred class behind them — the dump
        // leads, the log fills in, picks widen (`CharacterClasses`). WPF twin does the same.
        var mine = _main.ClassSourceFor(_main.CurrentSnapshot()).Classes;
        // One class and no lens to offer: a strip reading "Any · BRD" chooses nothing.
        if (mine.Count < 2) { _classStrip.IsVisible = false; return; }
        _classStrip.IsVisible = true;

        Add(null, "Any");
        foreach (var cls in mine) Add(cls, QuestClassFilter.Abbrev(cls));

        void Add(string? cls, string text) =>
            _classes.Add(text, cls ?? "",
                tip: cls is null
                    ? "Every class you play"
                    : $"Show only {cls} — quests, Epic and Plane of Sky alike",
                onClick: () => { _classLens = cls; Refresh(force: true); });
    }

    // The counting RULE is Core's (QuestSurface.CountOf) — this window, the WPF one and
    // the phone each had their own hand-rolled copy of the same expression.
    private (int Done, int Total)? EpicCounts() =>
        QuestSurface.CountOf(_settings.EpicQuestChecklist, i => i.Acquired);

    private (int Done, int Total)? SkyCounts() =>
        QuestSurface.CountOf(_settings.SkyQuestChecklist, i => i.Acquired);

    private (int Done, int Total)? UnlockCounts() =>
        QuestSurface.UnlockCounts(_main.RaceUnlocks, _main.ClassUnlocks);

    private void ApplyTabVisual()
    {
        _tabs.Select(_tab);
        // The class strip keys on "" for Any, because a null key would make "nothing
        // selected" and "Any selected" the same answer.
        _classes.Select(_classLens ?? "");
        // Era and the mode strip are catalog concepts — meaningless against a fixed
        // checklist. The CLASS picker is not: David, 2026-08-15, "we may be helping a
        // friend", so every tab must be able to reach a class you don't play.
        var catalogOnly = _tab == QuestTab.General;
        _eraCombo.IsVisible = catalogOnly;
        _modeStrip.IsVisible = catalogOnly;
        // STATE is not a catalog concept, and calling it one is what #205 and #209
        // reported: a checklist is the surface where "ready" and "done" mean the most.
        // Unlocks is neither a catalog nor a per-class checklist. The class picker narrows
        // CLASS unlocks and would silently hide every race, so it is replaced here by a
        // lens over the SECTIONS. The state lens goes too: it is not wired for unlocks,
        // and a filter that is present and inert is worse than one that is absent.
        var unlocks = _tab == QuestTab.Unlocks;
        _unlockSectionCombo.IsVisible = unlocks;
        _stateCombo.IsVisible = !unlocks;
        _classicOnlyCheck.IsVisible = _tab == QuestTab.Epic;
        _islandRepeatCheck.IsVisible = _tab == QuestTab.Sky;
        _classBtn.IsVisible = !unlocks;
        // "scan bags" copies /outputfile inventory, which is not what this tab reads.
        _scanBtn.IsVisible = !unlocks;
        // A checklist has nothing to select, so the pane would only ever be empty. Give
        // its width back to the rows instead.
        _detailCard.IsVisible = catalogOnly;
        _bodyGrid.ColumnDefinitions = new ColumnDefinitions(catalogOnly ? "400,*" : "*,0");
    }

    private void ApplyModeVisual() => _modes.Select(_mode);

    /// <summary>A faction dump just landed. Nothing to import — UnlockSource re-reads it
    /// off disk — but an OPEN Unlocks tab should fill in now rather than on the next
    /// reopen, which is the difference between the command appearing to work and appearing
    /// to do nothing.</summary>
    internal void FactionsChanged()
    {
        if (_tab == QuestTab.Unlocks) Refresh(force: true);
    }

    /// <summary>@see QuestsWindow._unlockSection (WPF). Session-scoped.</summary>
    private string _unlockSection = UnlockLayout.SectionAll;

    /// <summary>The Epic tab's per-class band: the class name and the "Epic complete"
    /// master check (#138 aodgizmo, restored for #210). At class level and not on a
    /// section heading because epic completion IS per class — a per-section button would
    /// promise a hand-in that does not exist.</summary>
    private Border EpicClassBand(string className)
    {
        var complete = EpicCompleteToggle.IsComplete(_settings, className);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var name = DesignSystem.Text(Role.TitleSection, className);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.Foreground = AppTheme.BrushFor(complete ? "GoodBrush" : "TextBrush");
        row.Children.Add(name);

        var button = new Button
        {
            Content = EpicCompleteToggle.ButtonLabel(complete),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, complete
            ? "Reopen this epic. Rows go back the way they were before the master check "
              + "ticked them — your own ticks are returned, not discarded."
            : "You finished this epic: ticks every remaining step for this class. "
              + "Reopening puts them back.");
        button.Click += async (_, _) => await ToggleEpicComplete(className, complete);
        Grid.SetColumn(button, 1);
        row.Children.Add(button);

        return new Border
        {
            Child = row,
            Background = AppTheme.BrushFor("RaisedBrush"),
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, DesignTokens.SpaceL, 0, 0),
        };
    }

    private async Task ToggleEpicComplete(string className, bool complete)
    {
        var items = EpicCompleteToggle.ItemsFor(
            _settings.EpicQuestChecklist, className, _settings.EpicQuestClassicOnly);
        if (complete)
        {
            EpicCompleteToggle.Reopen(_settings, className);
            EpicCompleteToggle.RestoreFrom(_settings, className, items);
        }
        else
        {
            // One click flips every unchecked row, which is bulk enough to warrant a
            // confirmation (#138). Nothing to overwrite means no dialog.
            if (EpicCompleteToggle.ConfirmPrompt(className, items) is { } prompt
                && !await ConfirmDialog.Ask(this, "Epic complete", prompt, "Mark complete"))
                return;
            EpicCompleteToggle.MarkComplete(_settings, className, items);
        }
        _settings.Save();
        Refresh(force: true);
    }

    /// <summary>"What can I turn in right now, across every class" (#129 bjstrange,
    /// restored for #205/#209/#210) — a band above the list naming every reward whose
    /// pieces are all in hand, and the NPC who takes it. Sky only, and only while
    /// something is ready: a permanently-present band reading "nothing" is how a player
    /// learns to stop looking at it.</summary>
    private void RenderReadyBand(QuestTab tab, IReadOnlyList<QuestChecklistGroup> groups)
    {
        if (tab != QuestTab.Sky) return;
        var ready = QuestChecklistLayout.ReadyToTurnIn(groups);
        if (ready.Count == 0) return;

        var panel = new StackPanel();
        // Built here rather than through IconLabel, which leaves its text at the caption
        // default: this heading names the one actionable thing on the page and cannot be
        // the dimmest line in its own band.
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        heading.Children.Add(DesignSystem.Icon("Check", "GoodBrush", size: 12));
        var headingText = DesignSystem.Text(Role.Caption, $"Ready to turn in — {ready.Count}");
        headingText.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        headingText.FontWeight = FontWeight.SemiBold;
        headingText.Foreground = AppTheme.BrushFor("GoodBrush");
        heading.Children.Add(headingText);
        panel.Children.Add(heading);

        foreach (var group in ready)
        {
            var line = DesignSystem.Text(Role.Body, "");
            line.TextWrapping = TextWrapping.Wrap;
            line.Margin = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXxs, 0, 0);
            line.Inlines!.Add(new Run(
                $"{QuestClassFilter.Abbrev(group.ClassName)} — {group.Title}")
            { FontWeight = FontWeight.SemiBold });
            if (group.TurnInNpc is { Length: > 0 } npc)
                line.Inlines.Add(new Run($"   {npc}") { Foreground = AppTheme.DimBrush });
            ToolTip.SetTip(line, $"{group.ClassName}: all {group.Total} "
                + (group.Total == 1 ? "item" : "items") + " acquired"
                + (group.TurnInNpc is { Length: > 0 } n ? $" — turn in to {n}" : ""));
            panel.Children.Add(line);
        }

        _questsPanel.Children.Add(new Border
        {
            Child = panel,
            Background = AppTheme.BrushFor("RaisedBrush"),
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM),
        });
    }

    /// <summary>Done / Ready / Partial / Total per class (#136 bjstrange, restored with
    /// the band above) — "how am I doing across all sixteen" without a scroll. Only worth
    /// drawing for more than one class; a summary of one line is furniture.</summary>
    private void RenderClassCounts(QuestTab tab, IReadOnlyList<QuestChecklistGroup> groups)
    {
        if (tab != QuestTab.Sky) return;
        var counts = QuestChecklistLayout.ClassCounts(groups);
        if (counts.Count < 2) return;

        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        foreach (var c in counts)
        {
            var line = DesignSystem.Text(Role.Caption, "");
            line.Inlines!.Add(new Run(QuestClassFilter.Abbrev(c.ClassName) + " ")
            { FontWeight = FontWeight.SemiBold, Foreground = AppTheme.BrushFor("TextBrush") });
            Metric("D", c.Done, "GoodBrush");
            Metric("R", c.Ready, "WarnBrush");
            Metric("P", c.Partial, "AccentBrush");
            // The total, because D+R+P deliberately does NOT sum to it — a reward you
            // have not started sits in no bucket (#136).
            line.Inlines.Add(new Run($" /{c.Total}") { Foreground = AppTheme.DimBrush });

            void Metric(string label, int count, string brushKey)
            {
                line.Inlines!.Add(new Run(label) { Foreground = AppTheme.DimBrush });
                line.Inlines.Add(new Run(count.ToString() + " ")
                { FontWeight = FontWeight.SemiBold, Foreground = AppTheme.BrushFor(brushKey) });
            }

            var chip = new Border
            {
                Child = line,
                Background = AppTheme.BrushFor("RaisedBrush"),
                CornerRadius = new CornerRadius(DesignTokens.RadiusPill),
                Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs,
                    DesignTokens.SpaceS, DesignTokens.SpaceXxs),
                Margin = new Thickness(0, 0, DesignTokens.SpaceXs, DesignTokens.SpaceXs),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(chip,
                $"{c.ClassName}: {c.Done} turned in, {c.Ready} ready to turn in, "
                + $"{c.Partial} started, of {c.Total}. Click to show only this class.");
            var className = c.ClassName;
            chip.PointerPressed += (_, e) => e.Handled = true;
            chip.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left) return;
                e.Handled = true;
                // A second click clears it, so the summary can put a class back as well
                // as take one away — a lens you can only enter is a trap.
                _classLens = _classLens is { } lens
                    && lens.Equals(className, StringComparison.OrdinalIgnoreCase)
                        ? null : className;
                ApplyTabVisual();
                Refresh(force: true);
            };
            wrap.Children.Add(chip);
        }
        _questsPanel.Children.Add(wrap);
    }

    /// <summary>The Epic and Sky tabs. Rows come straight from the same settings lists
    /// the loot auto-checkers tick and EQBuddy Mobile reads, so ticking here, on the
    /// tablet, or by looting the thing are all the same tick — a second VIEW, never a
    /// second copy of the data.
    ///
    /// The rows are TICKABLE, and have to be: hand-ticking used to live on the widget's
    /// Epic and Sky cards, and when those became one launcher this became the only place
    /// on the desktop to say "I already have that".</summary>
    /// <summary>The Sky tab's copy of the achievements auto-import report — the Avalonia
    /// twin of the WPF one, same class, same rule about when an Undo is offered. See
    /// <see cref="IQuestsHost.LastAchievementsImport"/> for why Sky is a host at all.</summary>
    private ImportReportView SkyImport => _skyImport ??=
        // force: true — an Undo moves checklist TICKS, and Refresh's signature is built
        // from the same lists it just restored, so a plain repaint would decide nothing
        // changed. A repaint that no-ops is how an Undo looks broken.
        new ImportReportView(() => _main.LastAchievementsImport, () => Refresh(force: true));

    private ImportReportView? _skyImport;

    /// <summary>@see QuestsWindow.SkyAchievementsPrompt (WPF) — the reason it exists and
    /// why it sits above the rows are stated there.</summary>
    private Control SkyAchievementsPrompt()
    {
        var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        wrap.Children.Add(Note(
            "Turned rewards in before EQBuddy? The game's achievements dump knows. Run this "
            + "in game and EQBuddy reads the file it writes — it never scans the game itself.",
            "Info"));
        var b = ActionButton("");
        b.Content = DesignSystem.IconLabel("Copy", GameCommands.OutputfileAchievements);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        ToolTip.SetTip(b,
            "Copies the command — paste it into the game's chat. The game writes "
            + "<name>_<server>-Achievements.txt beside its own folders and EQBuddy imports "
            + "it on its own; the report appears here.");
        b.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is { } cb)
                {
                    await cb.SetTextAsync(GameCommands.OutputfileAchievements);
                    b.Content = DesignSystem.IconLabel(
                        "Check", "copied — paste in game chat", "GoodBrush");
                }
            }
            catch (Exception ex) { App.LogError(ex); }   // clipboard held elsewhere
        };
        wrap.Children.Add(b);
        return wrap;
    }

    /// <summary>@see QuestsWindow.RenderUnlocks (WPF) — rows are read-only there for the
    /// same reason: an unlock is the GAME's answer, so there is nothing to tick, and a
    /// disabled checkbox would render as a live one (trap 17).</summary>
    private void RenderUnlocks()
    {
        var races = _main.RaceUnlocks;
        var classes = _main.ClassUnlocks;
        var factions = _main.LatestFactions;

        // BOTH commands, always — see the WPF twin. A race unlock moves every time you
        // grind faction, so the button a player needs most is the one on the POPULATED
        // surface, not one hidden behind an empty state (#217's rule).
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        row.Children.Add(CommandPrompt(GameCommands.OutputfileAchievements,
            "Copies the command. The game writes <name>_<server>-Achievements.txt beside "
            + "its own folders; EQBuddy reads it on its own and this tab fills in. "
            + "This is what says which races and classes you have unlocked."));
        row.Children.Add(CommandPrompt(GameCommands.OutputfileFaction,
            "Copies the command. The game writes <name>_<server>-<CLASS>-Factions.txt; "
            + "EQBuddy reads it the moment the game says it is written. This is what says "
            + "how far along each race's factions are — the log can only see faction "
            + "CHANGES, never where you stand."));
        _questsPanel.Children.Add(row);

        if (!_main.HasUnlockDump)
        {
            _questsPanel.Children.Add(EmptyState(
                "No achievements dump yet. Race and class unlocks are the game's own record "
                + "— EQBuddy reads the file the game writes and never scans the game itself. "
                + "Run the achievements command above and this fills in."));
            return;
        }

        if (UnlockLayout.NeedsFactionDump(races, factions))
        {
            _questsPanel.Children.Add(Note(
                "Race unlocks are faction work, and the log only ever sees faction CHANGES "
                + "— never where you stand. Run the faction command above and the rows "
                + "below fill in.", "Info"));
        }
        else if (factions is { } f)
        {
            _questsPanel.Children.Add(Note(
                $"Standings as of {f.WrittenAt:d MMM HH:mm}. Re-run the faction command "
                + "after a grind to refresh them.", "Info"));
        }

        Section(UnlockLayout.RacesHeading, races);
        Section(UnlockLayout.ClassesHeading, classes);

        void Section(string heading, IReadOnlyList<UnlockProgress> unlocks)
        {
            if (unlocks.Count == 0) return;
            if (!UnlockLayout.InSection(heading, _unlockSection)) return;
            var title = DesignSystem.Text(Role.TitleSection, heading);
            title.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL, 0,
                DesignTokens.SpaceXs);
            title.Foreground = AppTheme.AccentBrush;
            _questsPanel.Children.Add(title);

            var groups = UnlockLayout.Groups(unlocks, factions, heading);
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var u = unlocks[i];
                var score = u.Score is { } s ? $"   {s.Done}/{s.Total}" : "";
                var head = DesignSystem.Text(Role.Body, g.Title + score);
                head.FontWeight = FontWeight.SemiBold;
                head.TextWrapping = TextWrapping.Wrap;
                head.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceM, 0, 0);
                head.Foreground = u.Complete ? AppTheme.GoodBrush : AppTheme.TextBrush;
                _questsPanel.Children.Add(head);

                if (UnlockLayout.Note(u) is { Length: > 0 } note)
                    _questsPanel.Children.Add(Note(note, "Info"));

                foreach (var row in g.Rows)
                {
                    // Two columns, never a horizontal StackPanel (trap 14).
                    var line = new Grid
                    {
                        Margin = new Thickness(DesignTokens.SpaceL, 1, 0, 1),
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    };
                    var icon = DesignSystem.Icon(row.Acquired ? "Check" : "Pending",
                        row.Acquired ? "GoodBrush" : "DimBrush", size: DesignTokens.IconInline);
                    icon.VerticalAlignment = VerticalAlignment.Center;
                    icon.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
                    Grid.SetColumn(icon, 0);
                    line.Children.Add(icon);

                    var text = DesignSystem.Text(Role.Body,
                        row.Detail.Length > 0 ? $"{row.Title}   {row.Detail}" : row.Title);
                    text.TextWrapping = TextWrapping.Wrap;
                    text.Foreground = row.Acquired ? AppTheme.DimBrush : AppTheme.TextBrush;
                    Grid.SetColumn(text, 1);
                    line.Children.Add(text);
                    _questsPanel.Children.Add(line);
                }
            }
        }
    }

    /// <summary>A copy of an in-game command, off GameCommands and never its own
    /// literal.</summary>
    private Button CommandPrompt(string command, string tip)
    {
        var b = ActionButton("");
        b.Content = DesignSystem.IconLabel("Copy", command);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXs, 0, DesignTokens.SpaceS);
        ToolTip.SetTip(b, tip);
        b.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is { } cb)
                {
                    await cb.SetTextAsync(command);
                    b.Content = DesignSystem.IconLabel(
                        "Check", "copied — paste in game chat", "GoodBrush");
                }
            }
            catch (Exception ex) { App.LogError(ex); }
        };
        return b;
    }

    private void RenderChecklist(QuestTab tab, string filter, List<string> classes)
    {
        // ABOVE the rows and re-added on every render, because the panel is cleared
        // wholesale — trap 44: a report about something that just happened belongs where
        // the eye lands, not under a checklist the player has to scroll.
        if (tab == QuestTab.Sky)
        {
            SkyImport.Render();
            _questsPanel.Children.Add(SkyImport.Body);
            _questsPanel.Children.Add(SkyAchievementsPrompt());
        }
        // Grouping, ordering and the detail line come from Core so this window, the WPF
        // one and EQBuddy Mobile cannot disagree about what a checklist row says (#184).
        var groups = tab == QuestTab.Epic
            ? QuestChecklistLayout.Epic(_settings.EpicQuestChecklist
                .Where(i => !_settings.EpicQuestClassicOnly || i.AvailableInClassic))
            : QuestChecklistLayout.Sky(_settings.SkyQuestChecklist, _settings.SkyQuestCompleted,
                _settings.SkyStepsUnderEveryIsland);

        var setters = tab == QuestTab.Epic
            ? _settings.EpicQuestChecklist.ToDictionary(i => i.Id, i => (Action<bool>)(done =>
            {
                i.Acquired = done;
                // The player deciding IS the resolution of an unassigned auto-tick,
                // exactly as the old card's toggle treated it.
                i.AcquiredUnassigned = false;
            }), StringComparer.Ordinal)
            : _settings.SkyQuestChecklist.ToDictionary(i => i.Id, i => (Action<bool>)(done =>
            {
                i.Acquired = done;
                i.AcquiredUnassigned = false;
            }), StringComparer.Ordinal);

        // The picker chooses WHICH classes are in view — including ones you don't play,
        // because "we may be helping a friend" (David, 2026-08-15). The chips then narrow
        // to one of them. An empty pick means every class, never an empty window.
        var inScope = groups
            .Where(g => classes.Count == 0
                || classes.Contains(g.ClassName, StringComparer.OrdinalIgnoreCase))
            .Where(g => _classLens is null
                || g.ClassName.Equals(_classLens, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // The two cross-class summaries go ABOVE the lens: they answer "what can I do
        // right now" and "how am I doing overall", and a filter that hid them would leave
        // the player narrowing a list to find out what they were already being told.
        RenderReadyBand(tab, inScope);
        RenderClassCounts(tab, inScope);

        // SEARCHING IS NOT FILTERING (#108, liminalwarmth). A query rearranges the screen
        // by ITEM and crosses every class — "who wants this drop" is unanswerable inside
        // one class's filter — so it reads `groups`, not `inScope`, and skips the state
        // lens entirely. See the WPF twin: 1.69.0 shipped it under that rule and the
        // Gate 2 rebuild lost it. Clearing the box brings the class layout back.
        if (filter.Length > 0)
        {
            RenderItemMatches(QuestChecklistLayout.SearchByItem(groups, filter), setters, tab);
            return;
        }

        var matching = QuestChecklistLayout.InState(inScope, _state).ToList();

        if (matching.Count == 0)
        {
            // NAME what emptied the list. "Nothing matches" over a checklist that is
            // merely filtered reads as a broken tracker.
            // (A search never reaches here — it returns above with its own empty state.)
            _questsPanel.Children.Add(EmptyState(
                _state != QuestChecklistLayout.StateAny
                    ? $"Nothing here is “{_state}” right now — the state filter "
                      + "above is narrowing the list."
                : groups.Count > 0
                    ? "Nothing for the classes you have picked — the class picker above "
                      + "chooses which checklists this tab shows."
                : "This checklist is empty — it fills in from the wiki catalog and your own "
                  + "progress. Scan bags or import achievements to catch it up."));
            return;
        }

        var lastClass = "";
        foreach (var group in matching)
        {
            // A class whose epic is marked complete has LOCKED rows. Not decoration: the
            // master check's undo restores the snapshot it took, so a tick made while
            // complete would be silently discarded on Reopen.
            var locked = tab == QuestTab.Epic
                && EpicCompleteToggle.IsComplete(_settings, group.ClassName);
            // One band per CLASS on the Epic tab, carrying the master complete. Epic
            // completion is per class — never per section — so this cannot ride on a
            // group heading the way the Sky turn-in does.
            if (tab == QuestTab.Epic && !group.ClassName.Equals(lastClass, StringComparison.OrdinalIgnoreCase))
            {
                lastClass = group.ClassName;
                _questsPanel.Children.Add(EpicClassBand(group.ClassName));
            }
            // The heading opens the wiki page for the reward it names — the "way to view
            // details of sky quests" #184 asked back for.
            var rewardName = group.Title;
            var headingText = DesignSystem.Text(Role.TitleSection,
                $"{group.Heading}   {group.Done}/{group.Total}"
                + (group.Note is { } n ? $"  · {n}" : ""));
            headingText.TextWrapping = TextWrapping.Wrap;
            headingText.Margin = new Thickness(
                DesignTokens.SpaceXxs, DesignTokens.SpaceL, 0, DesignTokens.SpaceXs);
            headingText.Foreground = AppTheme.AccentBrush;
            ToolTip.SetTip(headingText, "Open the wiki page for this quest");
            OnClick(headingText, () => OpenUrl(EqlWiki.PageUrl(rewardName)));
            // "I turned this in." Restored 2026-08-18 — the widget's Sky card had this per
            // reward, and when that card became a launcher only the per-ITEM ticks came
            // across. SkyQuestCompleted kept being READ here, on Windows and on the phone,
            // while nothing but the achievements import could WRITE it.
            if (group.CompletionKey is { } rewardKey && (group.Completed || group.ReadyToTurnIn))
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(headingText);

                var completed = group.Completed;
                var turnIn = new Button
                {
                    Content = SkyCompleteToggle.ButtonLabel(completed),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceL, 0, 0),
                };
                ToolTip.SetTip(turnIn, completed
                    ? "Reopen this reward. Your item ticks stay as they are — you know what you still hold."
                    : "You handed these in: marks the reward done and ticks its items.");
                turnIn.Click += (_, _) =>
                {
                    if (completed) SkyCompleteToggle.Reopen(_settings, rewardKey);
                    else SkyCompleteToggle.MarkTurnedIn(_settings, rewardKey,
                        SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey),
                        _main.QuestLedger, _main.QuestCharacterKey);
                    _settings.Save();
                    Refresh(force: true);
                };
                Grid.SetColumn(turnIn, 1);
                row.Children.Add(turnIn);
                _questsPanel.Children.Add(row);
            }
            else _questsPanel.Children.Add(headingText);

            // Island sub-headings (David, 2026-08-23). Core hands the rows over already
            // ordered and already labelled; this draws a heading when the label changes and
            // owns no grouping logic of its own. WPF twin does exactly the same.
            var lastIsland = "";
            foreach (var row in group.Rows)
            {
                if (!setters.TryGetValue(row.Id, out var set)) continue;
                if (row.IslandHeading.Length > 0 && row.IslandHeading != lastIsland)
                {
                    lastIsland = row.IslandHeading;
                    var island = DesignSystem.Text(Role.Caption, row.IslandHeading);
                    island.FontWeight = FontWeight.SemiBold;
                    island.Foreground = AppTheme.DimBrush;
                    island.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceS,
                        0, DesignTokens.SpaceXxs);
                    _questsPanel.Children.Add(island);
                }
                var text = DesignSystem.Text(Role.Body,
                    // The drop location on every row, and the * on a tick EQBuddy placed
                    // itself — both were in the model and neither was drawn.
                    (row.Detail.Length > 0 ? $"{row.Title}   {row.Detail}" : row.Title)
                    + (row.Unassigned ? QuestChecklistLayout.UnassignedMark : ""));
                text.TextWrapping = TextWrapping.Wrap;
                text.Foreground = row.Acquired ? AppTheme.DimBrush : AppTheme.TextBrush;
                var check = new CheckBox
                {
                    Content = text,
                    IsChecked = row.Acquired,
                    Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
                };
                if (row.Unassigned)
                    ToolTip.SetTip(check,
                        "EQBuddy ticked this itself — several classes want this item and the "
                        + "log couldn't say which one earned it. Move the tick if it's on the "
                        + "wrong class; either way, toggling it settles the question.");
                if (locked)
                {
                    check.IsEnabled = false;
                    // And LOOK disabled — see the WPF twin: IsEnabled alone leaves a
                    // control that reads as live and silently ignores clicks.
                    check.Opacity = 0.5;
                    ToolTip.SetTip(check, $"{group.ClassName}'s epic is marked complete. "
                        + "Reopen it above to change individual steps.");
                }
                var was = row.Acquired;
                check.IsCheckedChanged += (_, _) =>
                {
                    var now = check.IsChecked == true;
                    if (now == was) return;   // our own repaint, not a click
                    set(now);
                    _settings.Save();
                    PushUndo(row.Title, now, set);
                    Refresh(force: true);
                };
                _questsPanel.Children.Add(check);
            }
        }
    }

    /// <summary>The item-grouped search result (#108): one heading per ITEM, and under it
    /// every class that wants it, each still a live tick. The arrangement IS the answer —
    /// a drop three classes are queuing for is one block here and was three sections you
    /// had to scroll between before.</summary>
    private void RenderItemMatches(
        IReadOnlyList<QuestChecklistLayout.ChecklistItemMatch> matches,
        Dictionary<string, Action<bool>> setters,
        QuestTab tab)
    {
        if (matches.Count == 0)
        {
            _questsPanel.Children.Add(EmptyState(
                "Nothing on this checklist matches that search. It looks at item names, "
                + "reward names and drop locations, across every class — so this is the "
                + "whole checklist saying no, not a filter narrowing it."));
            return;
        }

        var scope = DesignSystem.Text(Role.Caption, QuestChecklistLayout.SearchScopeNote);
        scope.TextWrapping = TextWrapping.Wrap;
        scope.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceXs, 0, 0);
        _questsPanel.Children.Add(scope);

        foreach (var match in matches)
        {
            var itemName = match.Title;
            var heading = DesignSystem.Text(Role.TitleSection, itemName);
            heading.TextWrapping = TextWrapping.Wrap;
            heading.Margin = new Thickness(
                DesignTokens.SpaceXxs, DesignTokens.SpaceL, 0, DesignTokens.SpaceXxs);
            heading.Foreground = AppTheme.AccentBrush;
            ToolTip.SetTip(heading, "Open the wiki page for this item");
            OnClick(heading, () => OpenUrl(EqlWiki.PageUrl(itemName)));
            _questsPanel.Children.Add(heading);

            // The one line #108 asked for in as many words: who wants this drop.
            var summary = DesignSystem.Text(Role.Caption, match.Classes == 1
                ? $"1 class wants this · {match.Held} of {match.Total} in hand"
                : $"{match.Classes} classes want this · {match.Held} of {match.Total} in hand");
            summary.Margin = new Thickness(
                DesignTokens.SpaceXxs, 0, 0, DesignTokens.SpaceXs);
            _questsPanel.Children.Add(summary);

            foreach (var wanter in match.Wanters)
            {
                if (!setters.TryGetValue(wanter.RowId, out var set)) continue;
                var line = wanter.ClassName + " · " + wanter.Reward
                    + (wanter.Detail.Length > 0 ? "   " + wanter.Detail : "")
                    + (wanter.RewardCompleted ? "   turned in" : "");
                var text = DesignSystem.Text(Role.Body, line);
                text.TextWrapping = TextWrapping.Wrap;
                text.Foreground = wanter.Acquired ? AppTheme.DimBrush : AppTheme.TextBrush;

                var check = new CheckBox
                {
                    Content = text,
                    IsChecked = wanter.Acquired,
                    Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
                };
                // Same lock as the class layout: a class whose epic is marked complete has
                // rows that must not move, or the master check's undo would discard them.
                if (tab == QuestTab.Epic
                    && EpicCompleteToggle.IsComplete(_settings, wanter.ClassName))
                {
                    check.IsEnabled = false;
                    check.Opacity = 0.5;    // trap 17: IsEnabled alone is invisible
                    ToolTip.SetTip(check, $"{wanter.ClassName}'s epic is marked complete. "
                        + "Clear the search and reopen it to change individual steps.");
                }

                var was = wanter.Acquired;
                var label = itemName + " (" + wanter.ClassName + ")";
                check.IsCheckedChanged += (_, _) =>
                {
                    var now = check.IsChecked == true;
                    if (now == was) return;   // our own repaint, not a click
                    set(now);
                    _settings.Save();
                    PushUndo(label, now, set);
                    Refresh(force: true);
                };
                _questsPanel.Children.Add(check);
            }
        }
    }

    /// <summary>Jump the window to one item's quests (the map badge in the Loot views):
    /// browse mode + the item as filter, so the quests appear even before any overlap
    /// and each carries its pin as the invitation to track.</summary>
    public void FilterToItem(string item)
    {
        _mode = "all";
        ApplyModeVisual();
        _filterBox.Text = item;
        Refresh(force: true);
        Activate();
    }

    /// <summary>Programmatic tab switch, for the same reasons <see cref="SetMode"/>
    /// exists — a screenshot hook, and the handle the render tests drive the checklist
    /// tabs by (clicking a Border in a headless test proves layout, not behaviour).</summary>
    /// <summary>Raised when the PLAYER switches tabs here. Not raised by SetTab.</summary>
    internal event Action<QuestTab>? TabChanged;

    internal void SetTab(QuestTab tab)
    {
        _tab = tab;
        ApplyTabVisual();
        Refresh(force: true);
    }

    /// <summary>Programmatic mode switch (screenshot hook + the map badge path).</summary>
    internal void SetMode(string mode)
    {
        _mode = mode is "zone" or "all" or "held" or "done" ? mode : "mine";
        ApplyModeVisual();
        Refresh(force: true);
    }

    /// <summary>The row the detail pane is showing, by quest name — so a refresh that
    /// rebuilds every row keeps the reader where they were.</summary>
    internal string SelectedQuest => _selected;

    // ---- multiclass filter (Legends: up to three active classes; David 2026-08-07) ----

    private readonly List<CheckBox> _classChecks = [];
    private bool _syncingClasses;

    private void BuildClassChecks()
    {
        foreach (var cls in QuestClassFilter.Classes)
        {
            var check = new CheckBox { Margin = new Thickness(0, 1, 0, 1) };
            check.Content = DesignSystem.Text(Role.Body, cls);
            check.IsCheckedChanged += (_, _) => OnClassCheckChanged();
            _classChecks.Add(check);
            _classCheckPanel.Children.Add(check);
        }
    }

    private List<string> SelectedClasses() =>
        _classChecks.Where(c => c.IsChecked == true)
            .Select(c => ((TextBlock)c.Content!).Text ?? "").ToList();

    private void OnClassCheckChanged()
    {
        if (_syncingClasses) return;
        var selected = SelectedClasses();
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is { } ledger && key.Length > 0)
            ledger.SetClasses(key, selected);
        UpdateClassButton(selected);
        Refresh(force: true);
    }

    private void BuildUndoBar()
    {
        _undoBar.Background = AppTheme.PopupBrush;
        _undoBar.BorderBrush = AppTheme.BorderBrush;
        _undoText.Foreground = AppTheme.TextBrush;
        _undoText.TextTrimming = TextTrimming.CharacterEllipsis;
        _undoText.VerticalAlignment = VerticalAlignment.Center;
        var btn = ActionButton("");
        btn.Content = DesignSystem.IconLabel("Undo", "undo");
        btn.Margin = new Thickness(DesignTokens.SpaceM, 0, 0, 0);
        ToolTip.SetTip(btn, "Put the last tick back the way it was (Ctrl+Z)");
        btn.Click += (_, _) => Undo();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(_undoText);
        Grid.SetColumn(btn, 1);
        grid.Children.Add(btn);
        _undoBar.Child = grid;

        // Ctrl+Z, because the undo button promises it. Not while typing in the search box.
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Z || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (FocusManager?.GetFocusedElement() is TextBox) return;
            e.Handled = true;
            Undo();
        };
    }

    private void PushUndo(string label, bool done, Action<bool> set)
    {
        _undo.Push((label, !done, set));
        _undoText.Text = $"{(done ? "Ticked" : "Cleared")} {label}";
        _undoBar.IsVisible = true;
    }

    private void Undo()
    {
        if (!_undo.TryPop(out var last)) { _undoBar.IsVisible = false; return; }
        last.Set(last.Was);
        _settings.Save();
        if (_undo.Count == 0) _undoBar.IsVisible = false;
        else _undoText.Text = $"Undid {last.Label}";
        Refresh(force: true);
    }

    // Capped in UI.Shared so this window cannot disagree with the WPF one: an uncapped
    // face grew with the selection and pushed the mode strip off the window (#184).
    private void UpdateClassButton(List<string> selected)
    {
        _classBtn.Content = ClassFilterLabel.For(selected);
        ToolTip.SetTip(_classBtn, selected.Count > ClassFilterLabel.MaxNamed
            ? "Showing: " + string.Join(", ", selected)
            : "Pick your class(es) — quests any of them can do stay visible");
    }

    /// <summary>Load the character's saved classes into the checkboxes (character
    /// switches included — the selection follows the ledger, not the window).</summary>
    private void SyncClassChecks(List<string> saved)
    {
        var current = SelectedClasses();
        if (current.SequenceEqual(saved, StringComparer.OrdinalIgnoreCase)) return;
        _syncingClasses = true;
        foreach (var check in _classChecks)
            check.IsChecked = saved.Contains(((TextBlock)check.Content!).Text ?? "",
                StringComparer.OrdinalIgnoreCase);
        _syncingClasses = false;
        UpdateClassButton(saved);
    }

    // The state filter (Reddit ask, 2026-08-11): cuts across every tab and search —
    // session-scoped on purpose, like the search box; a sticky "done" filter would
    // read as an empty tracker tomorrow.
    private string _state = "any state";

    private void OnStateChanged()
    {
        if (_stateCombo.SelectedItem is not string s) return;
        _state = s;
        Refresh(force: true);
    }

    private void OnEraChanged()
    {
        if (_eraCombo.SelectedIndex < 0) return;
        _settings.QuestEraFilter = _eraCombo.SelectedIndex == 0
            ? "" : QuestEraLadder.Eras[_eraCombo.SelectedIndex - 1];
        _settings.Save();
        Refresh(force: true);
    }

    /// <summary>Called from MainWindow's 1 s tick while visible; cheap unless the ledger
    /// or filters actually changed (signature idiom, same as the chip windows).</summary>
    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 2) Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        var key = _main.QuestCharacterKey;
        var character = key.Length > 0 ? key.Split('_')[0] : "";
        _titleText.Text = character.Length > 0
            ? $"Quest Tracker — {char.ToUpper(character[0])}{character[1..]}"
            : "Quest Tracker";

        var owned = _main.QuestLedger?.For(key)
            ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
        _owned = owned;
        var tracked = _main.QuestLedger?.TrackedFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hidden = _main.QuestLedger?.HiddenFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Folded with the Sky checklist, because a "<Class> Sky Test: <Reward>" row on
        // THIS tab and the reward row on the Sky tab are the same fact. The ledger never
        // knew about SkyQuestCompleted, so a reward the game's own achievements dump said
        // was handed in still sat here as live work.
        var completed = SkyTestSplit.WithTurnIns(
            _main.QuestLedger?.CompletedFor(key)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            _settings.SkyQuestCompleted);
        var filter = (_filterBox.Text ?? "").Trim();
        var picks = _main.QuestLedger?.ClassesFor(key) ?? [];
        SyncClassChecks(picks);
        // Nothing PICKED? The character's classes still pre-filter — from the dump if it
        // has one, from the log otherwise — always labeled with where they came from,
        // never persisted, and one popup pick overrides (David, 2026-08-11).
        var (resolved, classSource) = _main.ClassSourceFor(_main.CurrentSnapshot());
        var classes = picks.Count > 0 ? picks : resolved.ToList();
        // WHO the character is, shown whether or not classes are picked (Bevel,
        // Helm-signed 2026-08-23). Identity is not the filter.
        var identity = string.Join(" · ", resolved);

        // The lens narrows to ONE of the classes you play. Everything downstream reads
        // `classes`, so narrowing here covers the catalog, the zone view and the two
        // checklist tabs at once. A stale lens (you dropped that class) is ignored
        // rather than emptying the window.
        if (_classLens is { } lens && classes.Contains(lens, StringComparer.OrdinalIgnoreCase))
            classes = [lens];
        else if (_classLens is not null && !classes.Contains(_classLens, StringComparer.OrdinalIgnoreCase))
            _classLens = null;

        var sig = $"{key}|{filter}|{_tab}|{_mode}|st:{_state}|{string.Join("+", classes)}|id:{identity}|{_settings.QuestEraFilter}|{_main.CurrentZoneName}" +
            $"|sel:{_selected}" +
            $"|{string.Join(";", tracked.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", hidden.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", completed.Select(kv => $"{kv.Key}:{kv.Value}"))}" +
            $"|{string.Join(",", owned.Select(kv => $"{kv.Key}:{kv.Value.Total}"))}";
        if (!force && sig == _signature) return;
        _signature = sig;

        _questsPanel.Children.Clear();
        _rows.Clear();
        _renderedCount = 0;
        _suppressed = 0;
        _summaryRow.IsVisible = false;
        BuildTabs();
        if (_tab == QuestTab.Unlocks)
        {
            _detailPane.Children.Clear();
            RenderUnlocks();
            return;
        }
        if (_tab != QuestTab.General)
        {
            _detailPane.Children.Clear();
            RenderChecklist(_tab, filter, classes);
            return;
        }
        if (identity.Length > 0)
            // No verb — the picker is a lens over identity (#104), not a replacement.
            _questsPanel.Children.Add(Note(
                $"{identity} ({CharacterClasses.SourceLabel(classSource)})", "Info"));

        var era = _settings.QuestEraFilter;
        // Era and class gate separately since 2026-08-11 (David's Crushbone session):
        // era = world availability, always honored; class = the browse lens — and
        // item-driven views (mine, held) show out-of-class quests in their own
        // section rather than hiding what your bags are literally holding pieces of.
        bool EraOk(QuestEntry q) => QuestEraLadder.Allowed(q.Era, era);
        bool ClassOnlyOk(QuestEntry q) => QuestClassFilter.MatchesAny(q.Classes, classes);
        bool ClassOk(QuestEntry q) => ClassOnlyOk(q) && EraOk(q);
        bool StateOk(QuestMatch m) => _state switch
        {
            "open" => completed.GetValueOrDefault(m.Quest.Name) == 0,
            "ready" => m.Complete && m.ItemsTotal > 0,
            "done" => completed.GetValueOrDefault(m.Quest.Name) > 0,
            _ => true,
        };
        QuestMatch Progressed(QuestEntry quest)
        {
            var progress = quest.Items
                .Select(i => new QuestItemProgress(i.Name, i.Qty,
                    owned.TryGetValue(i.Name, out var e) ? e.Total : 0)).ToList();
            return new QuestMatch(quest, progress.Count(p => p.Have > 0), progress.Count,
                progress, tracked.Contains(quest.Name));
        }
        void AddRow(QuestMatch m)
        {
            if (_renderedCount >= RenderCap) { _suppressed++; return; }
            _renderedCount++;
            var entry = new RowEntry(m, hidden.Contains(m.Quest.Name),
                completed.GetValueOrDefault(m.Quest.Name));
            entry.Element = Row(entry);
            _rows.Add(entry);
            _questsPanel.Children.Add(entry.Element);
        }
        void EmptyNote(string text) => _questsPanel.Children.Add(EmptyState(text));

        // A typed search reads the WHOLE catalog, whatever tab is active (David,
        // 2026-08-10: "type an item name and see quests using that; type a quest
        // name to find and track progress"). The tabs scope browsing; a search
        // scopes finding — otherwise an item search on the mine tab found nothing
        // until you already owned pieces, which is backwards.
        if (filter.Length > 0)
        {
            // A search answers with the WHOLE catalog — no class/era/state gating
            // (David's live catch, 2026-08-11: the Blue Orc Head badge found
            // "nothing" because The Falchion is Paladin and his class filter
            // wasn't). Each row states its own class and era; the reader decides.
            var found = QuestSearch.Find(_main.QuestCatalog, filter)
                .Select(Progressed)
                .OrderByDescending(m => m.Tracked)
                .ThenByDescending(m => m.Fraction)
                .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _questsPanel.Children.Add(Note(
                $"{found.Count} match{(found.Count == 1 ? "" : "es")} in the whole catalog — names, " +
                "turn-in items, rewards, NPCs, zones. Search ignores your class/era/state filters.",
                "Search"));
            foreach (var m in found) AddRow(m);
            if (found.Count == 0)
                EmptyNote("Nothing matches. Searches cover quest names, turn-in items, " +
                          "rewards, quest givers, and zones — try fewer words.");
            FinishRender();
            return;
        }

        switch (_mode)
        {
            case "all":
                foreach (var m in _main.QuestCatalog.Quests
                             .Where(q => ClassOk(q))
                             .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                             .Select(Progressed)
                             .Where(StateOk))
                    AddRow(m);
                break;

            case "zone" when _main.CurrentZoneName.Length == 0:
                EmptyNote("No zone seen in the log yet — zone view fills in once " +
                          "you've zoned somewhere.");
                break;

            case "zone":
            {
                // Everything workable where you stand — including dialogue chains the
                // item parser found nothing for (David: "not everything is item driven").
                _questsPanel.Children.Add(Note(_main.CurrentZoneName, "Location", "WarnBrush"));
                var zoneQuests = _main.QuestCatalog.Quests
                    .Where(q => q.TouchesZone(_main.CurrentZoneName)
                                && MatchesFilter(q, filter) && ClassOk(q))
                    .Select(Progressed)
                    .Where(StateOk)
                    .OrderByDescending(m => m.Tracked)
                    .ThenByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var m in zoneQuests) AddRow(m);
                if (zoneQuests.Count == 0)
                    EmptyNote($"No catalogued quests touch {_main.CurrentZoneName}.");
                break;
            }

            case "held":
            {
                // What the bags could turn in right now, AND what they contribute to
                // (David, 2026-08-11, round two): fully-covered quests lead, partial
                // overlaps follow sorted by closeness — "available quests based on
                // what's in my inventory". The /outputfile command is one click to
                // copy, one paste into the game's chat.
                var snap = _main.LatestInventory(refresh: force);
                Button CopyCmd()
                {
                    var b = ActionButton("");
                    b.Content = DesignSystem.IconLabel("Copy", GameCommands.OutputfileInventory);
                    b.HorizontalAlignment = HorizontalAlignment.Left;
                    b.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceS);
                    ToolTip.SetTip(b,
                        "Copies the command — paste it into the game's chat and the game " +
                        "writes your inventory file; this tab reads it. Re-run any time " +
                        "your bags change.");
                    b.Click += async (_, _) =>
                    {
                        try
                        {
                            if (Clipboard is { } cb)
                            {
                                await cb.SetTextAsync(GameCommands.OutputfileInventory);
                                b.Content = DesignSystem.IconLabel(
                                    "Check", "copied — paste in game chat", "GoodBrush");
                            }
                        }
                        catch (Exception ex) { App.LogError(ex); }   // clipboard held elsewhere
                    };
                    return b;
                }
                if (snap is null)
                {
                    EmptyNote("No inventory dump found yet. In game, run this (the game writes " +
                        "<name>_<server>-Inventory.txt beside its own folders and this tab reads " +
                        "it — EQBuddy never scans the game itself):");
                    _questsPanel.Children.Add(CopyCmd());
                    break;
                }
                var invAge = DateTime.Now - snap.WrittenAt;
                _questsPanel.Children.Add(Note(
                    $"{Path.GetFileName(snap.Path)} — written " +
                    (invAge.TotalMinutes < 1 ? "just now" : invAge.TotalHours < 1
                        ? $"{(int)invAge.TotalMinutes}m ago" : $"{(int)invAge.TotalHours}h ago") +
                    " (plus everything looted since)", "Bag", "WarnBrush"));
                _questsPanel.Children.Add(CopyCmd());

                // NO class gate on the pool: your bags don't care what class a quest
                // is for (The Falchion's Blue Orc Head in a monk's bag is a farm,
                // not a mistake). In-class leads; the rest gets its own section.
                var overlapping = _main.QuestCatalog.Quests
                    .Where(q => q.Items.Count > 0 && !q.Collection && EraOk(q) && !hidden.Contains(q.Name))
                    .Select(q => new QuestMatch(q,
                        q.Items.Count(i => snap.CountOf(i.Name) > 0), q.Items.Count,
                        q.Items.Select(i => new QuestItemProgress(i.Name, i.Qty, snap.CountOf(i.Name))).ToList(),
                        tracked.Contains(q.Name)))
                    .Where(m => m.ItemsHave > 0)
                    .ToList();
                void Section(string text) => _questsPanel.Children.Add(SectionLabel(text));
                var mine2 = overlapping.Where(m => ClassOnlyOk(m.Quest)).ToList();
                var others = overlapping.Where(m => !ClassOnlyOk(m.Quest))
                    .OrderByDescending(m => m.Complete).ThenByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var ready = mine2.Where(m => m.Complete)
                    .OrderBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var partial = mine2.Where(m => !m.Complete)
                    .OrderByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                if (ready.Count > 0)
                {
                    Section($"Ready from your bags ({ready.Count})");
                    foreach (var m in ready) AddRow(m);
                }
                if (partial.Count > 0)
                {
                    Section($"Your bags contribute ({partial.Count})");
                    foreach (var m in partial) AddRow(m);
                }
                if (others.Count > 0)
                {
                    Section($"For other classes — you hold pieces anyway ({others.Count})");
                    foreach (var m in others) AddRow(m);
                }
                if (overlapping.Count == 0)
                    EmptyNote("Nothing in your bags matches a catalogued quest's turn-ins yet.");
                break;
            }

            case "done":
            {
                // The trophy shelf — and the catch-up surface: every quest can be marked
                // done, so returning players can mark history without touching items.
                var done = completed.Where(kv => kv.Value > 0)
                    .Select(kv => (_main.QuestCatalog.Quests.FirstOrDefault(q =>
                        q.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)), kv.Value))
                    .Where(x => x.Item1 is not null && ClassOk(x.Item1!))
                    .OrderBy(x => x.Item1!.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var (q, _) in done) AddRow(Progressed(q!));
                if (done.Count == 0)
                    EmptyNote("Nothing marked completed yet. Select a quest and use \"mark as " +
                              "done\" on quests you finished before EQBuddy, and the tracker " +
                              "catches up (ready quests count themselves when you hand them in).");
                break;
            }

            default:
            {
                // "mine": item overlap + pins, minus dismissed and finished-for-good
                // (completed non-repeatables stay visible in zone/all with their mark).
                var doneForGood = new HashSet<string>(
                    completed.Where(kv => kv.Value > 0).Select(kv => kv.Key)
                        .Where(name => _main.QuestCatalog.Quests.FirstOrDefault(q =>
                            q.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Repeatable: false }),
                    StringComparer.OrdinalIgnoreCase);
                doneForGood.UnionWith(hidden);
                var matches = QuestMatcher.Match(_main.QuestCatalog, owned, tracked, doneForGood);
                // Same rule as held (David's Crushbone session): items you LOOTED
                // outrank the class lens — out-of-class overlaps show in their own
                // section instead of vanishing.
                var eligible = matches
                    .Where(m => MatchesFilter(m.Quest, filter) && EraOk(m.Quest) && StateOk(m))
                    .ToList();
                var shown = eligible.Where(m => ClassOnlyOk(m.Quest)).ToList();
                var othersMine = eligible.Where(m => !ClassOnlyOk(m.Quest)).ToList();
                foreach (var m in shown) AddRow(m);
                if (othersMine.Count > 0)
                {
                    _questsPanel.Children.Add(SectionLabel(
                        $"For other classes — from your items ({othersMine.Count})"));
                    foreach (var m in othersMine) AddRow(m);
                }
                if (shown.Count == 0 && othersMine.Count == 0)
                    EmptyNote(matches.Count == 0
                        ? "Nothing yet — loot a quest item (they show green in the Loot list), " +
                          "or scan bags to read what you already carry. Search a reward you want " +
                          "by name, or try \"zone\" and \"all\" to browse."
                        : "No quest matches that search — try a reward name, an item, or an NPC.");
                break;
            }
        }

        FinishRender();
    }

    /// <summary>The two things that can only be said once every row is in: how many of
    /// them are ready, and how many were capped away. Never a silent cap (CLAUDE.md).</summary>
    private void FinishRender()
    {
        if (_suppressed > 0)
            _questsPanel.Children.Add(EmptyState(
                $"+{_suppressed} more — showing the first {RenderCap}. Keep typing to narrow it down."));

        var ready = _rows.Count(r => Badge(r).State == QuestPresentation.State.Ready);
        if (QuestPresentation.ReadySummary(ready) is { } summary)
        {
            _summaryRow.Children.Clear();
            _summaryRow.Children.Add(DesignSystem.Icon("Check", "GoodBrush", size: 13));
            var text = DesignSystem.Text(Role.BodySecondary, summary);
            text.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
            text.Foreground = AppTheme.GoodBrush;
            _summaryRow.Children.Add(text);
            _summaryRow.IsVisible = true;
        }

        // Keep the selection if it survived the rebuild; otherwise fall to the first row,
        // so the pane is never blank beside a full list.
        Select(_rows.FirstOrDefault(r =>
                   r.Match.Quest.Name.Equals(_selected, StringComparison.OrdinalIgnoreCase))
               ?? _rows.FirstOrDefault());
    }

    // ---- the list ----

    /// <summary>One rendered row and everything the detail pane needs to redraw it
    /// without going back to the ledger.</summary>
    private sealed class RowEntry(QuestMatch match, bool hidden, int completedCount)
    {
        public QuestMatch Match { get; } = match;
        public bool Hidden { get; } = hidden;
        public int CompletedCount { get; } = completedCount;
        public Border Element { get; set; } = null!;
    }

    private readonly List<RowEntry> _rows = [];
    private string _selected = "";

    private static QuestPresentation.Badge Badge(RowEntry entry) =>
        QuestPresentation.BadgeFor(entry.Match, entry.CompletedCount);

    /// <summary>A compact list row: state rule · name · badge · one meta line. That is
    /// all — the rewards, the turn-in items and the five controls moved to the pane.
    /// Fifty of these can be scanned; fifty of the cards they replace could only be
    /// read.</summary>
    private Border Row(RowEntry entry)
    {
        var m = entry.Match;
        var badge = Badge(entry);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        // The state rule. One fact, two encodings — the rule makes the list scannable
        // without reading, the badge makes it unambiguous when read. "Open" gets no rule
        // at all: a list where every row is highlighted highlights nothing.
        var rule = new Border
        {
            Width = DesignTokens.StateRuleWidth,
            CornerRadius = new CornerRadius(DesignTokens.StateRuleWidth / 2),
            Margin = new Thickness(0, 0, DesignTokens.SpaceM, 0),
        };
        if (QuestPresentation.RuleColorKey(badge.State) is { } ruleKey)
        {
            rule.Background = AppTheme.BrushFor(ruleKey);
            rule.Opacity = QuestPresentation.RuleOpacity(badge.State);
        }
        grid.Children.Add(rule);

        var stack = new StackPanel();
        var name = DesignSystem.Text(Role.TitleSection, m.Quest.Name);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        if (m.Tracked) name.Foreground = AppTheme.AccentBrush;
        stack.Children.Add(name);

        var metaLine = QuestPresentation.MetaLine(
            m.Quest, entry.CompletedCount, Distance(m.Quest).Text);
        if (metaLine.Length > 0)
        {
            var meta = DesignSystem.Text(Role.Caption, metaLine);
            meta.TextTrimming = TextTrimming.CharacterEllipsis;
            stack.Children.Add(meta);
        }
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0),
        };
        if (m.Tracked) right.Children.Add(DesignSystem.Icon("PinFilled", "AccentBrush", size: 11));
        var badgeText = DesignSystem.Text(Role.Caption, badge.Label);
        badgeText.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        badgeText.FontWeight = FontWeight.SemiBold;
        badgeText.Foreground = AppTheme.BrushFor(badge.ColorKey);
        right.Children.Add(badgeText);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var row = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Opacity = entry.Hidden ? 0.55 : 1.0,
        };
        if (entry.Hidden)
            ToolTip.SetTip(row, "Hidden — select it and use \"show again\" to bring it back");
        row.PointerPressed += (_, e) => { e.Handled = true; Select(entry); };
        // Double-click still opens the wiki walkthrough, which is what clicking the name
        // used to do. Kept because it is muscle memory and costs nothing.
        row.DoubleTapped += (_, e) => { e.Handled = true; OpenUrl(m.Quest.Url); };
        return row;
    }

    /// <summary>Selected is a SURFACE change (raised, hairline turns accent), never a
    /// re-render: the pane is what changes, and repainting fifty rows to move a
    /// highlight is how a list starts to feel slow.</summary>
    private void Select(RowEntry? entry)
    {
        _selected = entry?.Match.Quest.Name ?? "";
        foreach (var row in _rows)
        {
            var on = ReferenceEquals(row, entry);
            row.Element.Background = on ? AppTheme.RaisedBrush : AppTheme.PanelBrush;
            row.Element.BorderBrush = on ? AppTheme.AccentBrush : AppTheme.HairlineBrush;
        }
        BuildDetail(entry);
    }

    // ---- the detail pane ----

    private void BuildDetail(RowEntry? entry)
    {
        _detailPane.Children.Clear();
        if (entry is null)
        {
            _detailPane.Children.Add(
                EmptyState("Select a quest to see its rewards, turn-ins and where to go."));
            return;
        }

        var m = entry.Match;
        var badge = Badge(entry);

        // Title + the controls that act on this quest. They used to be five click-handled
        // TextBlocks on every card in the list — 300 of them on a full catalog view, none
        // of them keyboard-reachable, and all of them competing with the data.
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = DesignSystem.Text(Role.TitleWindow, m.Quest.Name);
        title.TextWrapping = TextWrapping.Wrap;
        title.Foreground = AppTheme.AccentBrush;
        ToolTip.SetTip(title, "Open the wiki walkthrough");
        OnClick(title, () => OpenUrl(m.Quest.Url));
        head.Children.Add(title);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top,
        };
        // Pin = "keep this quest in front of me": tracked quests sort first and stay
        // visible even with zero items (David, 2026-08-07: "players can choose to track
        // quests or not, easily").
        actions.Children.Add(DesignSystem.IconButton(
            m.Tracked ? "PinFilled" : "Pin",
            m.Tracked ? "Stop tracking this quest" : "Track this quest",
            () => WithLedger(l => l.SetTracked(_main.QuestCharacterKey, m.Quest.Name, !m.Tracked)),
            m.Tracked ? "AccentBrush" : "DimBrush", m.Tracked ? 1.0 : 0.55));
        // Check = "I did this before EQBuddy" (David, 2026-08-11): catch-up marking,
        // consuming nothing — the turn-in button below is for hand-ins happening now.
        actions.Children.Add(DesignSystem.IconButton("Check",
            entry.CompletedCount > 0
                ? $"Completed ×{entry.CompletedCount} — click to unmark"
                : "Did this before EQBuddy? Mark it completed (consumes nothing; click again to undo)",
            () => ToggleCompleted(m.Quest.Name, entry.CompletedCount == 0),
            entry.CompletedCount > 0 ? "GoodBrush" : "DimBrush",
            entry.CompletedCount > 0 ? 1.0 : 0.55));
        // Close = "not interested": drops the quest from the overlap view AND un-greens
        // loot only it wants (David, 2026-08-07: "there are definitely some I don't want
        // to track"). Hidden quests reappear dimmed under "all", where this is the way back.
        actions.Children.Add(DesignSystem.IconButton("Close",
            entry.Hidden
                ? "Show this quest again"
                : "Not interested — hide this quest (its items stop showing green unless another quest wants them)",
            () => WithLedger(l => l.SetHidden(_main.QuestCharacterKey, m.Quest.Name, !entry.Hidden)),
            "DimBrush", entry.Hidden ? 1.0 : 0.55));
        // Flag = "this data is wrong" (David, 2026-08-11: one wrong quest drops faith in
        // everything). One click opens a prefilled report — the catalog's accuracy loop
        // runs on these, same as every parser fix ran on pasted log lines.
        actions.Children.Add(DesignSystem.IconButton("Flag",
            "Something wrong with this quest's data (items, giver, zone)? " +
            "Open a prefilled report — fixes usually ship the same day.",
            () => OpenUrl(ReportUrl(m)), "DimBrush", 0.55));
        Grid.SetColumn(actions, 1);
        head.Children.Add(actions);
        _detailPane.Children.Add(head);

        // The status line, said in words directly under the title — the badge in the list
        // is a glance, this is the answer.
        var status = IconLine(badge.State switch
        {
            QuestPresentation.State.Ready => "ready to turn in",
            QuestPresentation.State.Done => $"completed ×{entry.CompletedCount}",
            QuestPresentation.State.InProgress => $"{m.ItemsHave} of {m.ItemsTotal} turn-ins started",
            QuestPresentation.State.Steps => "dialogue or task chain — steps on the wiki page",
            QuestPresentation.State.Collection =>
                "this wiki page documents several quests at once, so per-page progress would mislead",
            _ => "nothing held yet",
        }, badge.State switch
        {
            QuestPresentation.State.Ready => "Check",
            QuestPresentation.State.Done => "Check",
            QuestPresentation.State.Collection => "Book",
            QuestPresentation.State.Steps => "Info",
            _ => "Quest",
        }, badge.ColorKey, Role.Body);
        status.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceM);
        _detailPane.Children.Add(status);

        if (m.Quest.Rewards.Count > 0) _detailPane.Children.Add(Rewards(m));
        if (m.Items.Count > 0) _detailPane.Children.Add(Objectives(m));
        _detailPane.Children.Add(Details(m, entry.CompletedCount));

        // THE primary action, and the only one on the surface: "I handed it in". It was
        // previously the progress COUNT doubling as a button, which is not an affordance
        // anyone finds — the tooltip was the only thing that said so.
        if (m.Complete || m.ItemsTotal == 0)
        {
            var handIn = new Button
            {
                Content = DesignSystem.IconLabel("Check",
                    m.ItemsTotal == 0 ? "Mark as done" : "Mark as turned in", "GoodBrush"),
                Background = AppTheme.GoodWashBrush,
                BorderBrush = AppTheme.GoodBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(DesignTokens.RadiusControl),
                Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, DesignTokens.SpaceL, 0, 0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(handIn, m.ItemsTotal == 0
                ? "Click when you finish this quest to mark it done"
                : "Click when you hand it in — consumes one set of turn-in items and counts a completion");
            handIn.Click += (_, _) => WithLedger(l =>
                l.RecordCompletion(_main.QuestCharacterKey, m.Quest.Name, m.Quest.Items));
            _detailPane.Children.Add(handIn);
        }
    }

    /// <summary>The payoff, right under the status (David, 2026-08-07: "Crude Stein Quest
    /// should show the Crude Stein item"), with the same hover/click as loot.
    ///
    /// The silhouette beside each name comes from the item's OWN catalog record (slots and
    /// weapon skill). The mockup drew a bespoke icon per item; nothing in EQBuddy can map
    /// an item to one — the 2026-08-15 spike established that the game ships the icon
    /// sheets and nothing indexes them — so this draws what the data supports and nothing
    /// more (docs/DesignSystem.md §8a).</summary>
    private Control Rewards(QuestMatch m)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        panel.Children.Add(SectionLabel("Rewards"));
        var wrap = new WrapPanel();
        const int shown = 8;
        foreach (var reward in m.Quest.Rewards.Take(shown)) wrap.Children.Add(RewardTile(reward));
        if (m.Quest.Rewards.Count > shown)
        {
            var more = DesignSystem.Text(Role.Caption, $"+{m.Quest.Rewards.Count - shown} more");
            more.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXs, 0, 0);
            ToolTip.SetTip(more, string.Join("\n", m.Quest.Rewards.Skip(shown)));
            wrap.Children.Add(more);
        }
        panel.Children.Add(wrap);
        return panel;
    }

    private Border RewardTile(string name)
    {
        var record = ItemCatalog.Default.Find(name);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(DesignSystem.Icon(
            IconPaths.ForItem(record?.Slots, record?.Skill), "DimBrush", size: 14));
        var label = DesignSystem.Text(Role.BodySecondary, name);
        label.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Foreground = AppTheme.AccentBrush;
        content.Children.Add(label);

        var tile = new Border
        {
            Child = content,
            Background = AppTheme.RaisedBrush,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs),
            Margin = new Thickness(0, 0, DesignTokens.SpaceXs, DesignTokens.SpaceXs),
            Cursor = new Cursor(StandardCursorType.Hand),
            MaxWidth = 220,
        };
        AttachWikiTip(tile, name);
        tile.PointerPressed += (_, e) => e.Handled = true;
        tile.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            e.Handled = true;
            MainWindow.OpenWikiPage(name);
        };
        return tile;
    }

    private const string ItemRowHint =
        "Left-click: +1 (you have one more) · Right-click: clear your count (after a hand-in)";

    private Control Objectives(QuestMatch m)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        panel.Children.Add(SectionLabel("Turn-ins"));
        // One sentence, not one per item (#241 PR 3, Bevel-signed 2026-08-27): where
        // today's have-counts came from — an inventory dump, or a log tally that cannot
        // see hand-ins.
        panel.Children.Add(Note(
            QuestPresentation.TurnInProvenanceText(m.Items, _owned, DateTime.Now), "Bag"));
        foreach (var item in m.Items) panel.Children.Add(ItemRow(item));
        return panel;
    }

    private Border ItemRow(QuestItemProgress item)
    {
        var met = item.Have >= item.Need;
        var record = ItemCatalog.Default.Find(item.Name);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var icon = DesignSystem.Icon(IconPaths.ForItem(record?.Slots, record?.Skill),
            met ? "GoodBrush" : "DimBrush", size: 14);
        icon.Margin = new Thickness(0, 0, DesignTokens.SpaceM, 0);
        grid.Children.Add(icon);

        var name = DesignSystem.Text(Role.Body, item.Name);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.Foreground = met ? AppTheme.GoodBrush
            : item.Have > 0 ? AppTheme.TextBrush : AppTheme.DimBrush;
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var count = DesignSystem.Text(Role.Body, $"{item.Have} / {item.Need}");
        count.FontWeight = FontWeight.SemiBold;
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(DesignTokens.SpaceM, 0, 0, 0);
        count.Foreground = met ? AppTheme.GoodBrush
            : item.Have > 0 ? AppTheme.AccentBrush : AppTheme.DimBrush;
        Grid.SetColumn(count, 2);
        grid.Children.Add(count);

        var row = new Border
        {
            Child = grid,
            Background = AppTheme.RaisedBrush,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        // Same live wiki-stats hover the Loot window has (David, 2026-08-07), with the
        // count-adjust hint riding underneath.
        AttachWikiTip(row, item.Name, "\n\n" + ItemRowHint);
        row.PointerPressed += (_, e) => e.Handled = true;   // a row press is a click, not a drag
        row.PointerReleased += (_, e) =>
        {
            e.Handled = true;
            if (e.InitialPressMouseButton == MouseButton.Left) AdjustManual(item.Name, +1);
            else if (e.InitialPressMouseButton == MouseButton.Right) ClearCount(item.Name);
        };
        return row;
    }

    /// <summary>Zone · giver · level · distance · class as labelled CELLS. On the card
    /// this replaces they were one ellipsized run of "·"-joined fragments, so on a narrow
    /// window the class quietly vanished and nothing said it had.</summary>
    private Control Details(QuestMatch m, int completedCount)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionLabel("Details"));
        var wrap = new WrapPanel();
        var (distance, route) = Distance(m.Quest);
        foreach (var (label, value, tip) in new[]
        {
            ("Zone", m.Quest.StartZone, (string?)null),
            ("Giver", m.Quest.QuestGiver, null),
            ("Level", m.Quest.MinLevel > 0 ? $"{m.Quest.MinLevel}+" : "", null),
            ("Distance", distance, route),
            ("Class", m.Quest.Classes, null),
            ("Completed", completedCount > 0 ? $"×{completedCount}" : "", null),
            ("Repeatable", m.Quest.Repeatable ? "yes" : "", null),
        })
        {
            if (value.Length == 0) continue;
            var cell = new StackPanel();
            cell.Children.Add(DesignSystem.Text(Role.Metadata, label));
            var body = DesignSystem.Text(Role.BodySecondary, value);
            body.Foreground = AppTheme.TextBrush;
            body.TextWrapping = TextWrapping.Wrap;
            body.MaxWidth = 150;
            cell.Children.Add(body);
            var border = new Border
            {
                Child = cell,
                Background = AppTheme.RaisedBrush,
                BorderBrush = AppTheme.HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
                Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS),
                Margin = new Thickness(0, 0, DesignTokens.SpaceXs, DesignTokens.SpaceXs),
            };
            if (tip is { Length: > 0 }) ToolTip.SetTip(border, tip);
            wrap.Children.Add(border);
        }
        panel.Children.Add(wrap);
        return panel;
    }

    /// <summary>"How far is the turn-in from here" — BFS hops over the harvested zone
    /// graph, path in the tooltip (David, 2026-08-07: "3 zones away, zone 1 → zone 2 →
    /// zone 3"). Multi-zone quests measure to the nearest listed start zone.</summary>
    private (string Text, string? Route) Distance(QuestEntry quest)
    {
        if (_main.CurrentZoneName.Length == 0 || quest.StartZone.Length == 0) return ("", null);
        var best = quest.StartZone.Split(',')
            .Select(z => _main.ZoneGraph.Distance(_main.CurrentZoneName, z.Trim()))
            .Where(d => d is not null)
            .OrderBy(d => d!.Value.Hops)
            .FirstOrDefault();
        return best is { } b
            ? (QuestPresentation.DistanceText(b.Hops), b.Hops == 0 ? null : string.Join(" → ", b.Path))
            : ("", null);
    }

    private static string ReportUrl(QuestMatch m)
    {
        var report =
            $"Quest: {m.Quest.Name}\nWiki page: {m.Quest.Url}\n" +
            $"EQBuddy shows: {m.ItemsTotal} turn-in item(s) — {string.Join(", ", m.Quest.Items.Select(i => i.Qty > 1 ? $"{i.Name} x{i.Qty}" : i.Name))}\n" +
            $"Giver: {m.Quest.QuestGiver} · Zone: {m.Quest.StartZone}\n\nWhat's wrong:\n\n\n" +
            "---\nNote: EQBuddy mirrors eqlwiki.com, so if the wiki page itself is wrong, " +
            "editing the page is the strongest fix — the catalog re-harvests it weekly. " +
            "If the page is right and EQBuddy read it wrong, this report is exactly the right place.\n";
        return "https://github.com/DranakCorps-bot/EQBuddy/discussions/new?category=q-a" +
            "&title=" + Uri.EscapeDataString($"Quest data: {m.Quest.Name}") +
            "&body=" + Uri.EscapeDataString(report);
    }

    private void WithLedger(Action<QuestLedgerStore> act)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        act(ledger);
        Refresh(force: true);
    }

    /// <summary>@see QuestsWindow.ToggleCompleted (WPF) — the rules are stated there and
    /// the decision itself is <see cref="SkyTestSplit.RewardKeyFor"/> plus
    /// <see cref="SkyCompleteToggle"/>, so both builds get one answer.</summary>
    private void ToggleCompleted(string questName, bool done)
    {
        var rewardKey = SkyTestSplit.RewardKeyFor(questName);
        if (rewardKey.Length == 0)
        {
            WithLedger(l => l.SetCompleted(_main.QuestCharacterKey, questName, done));
            return;
        }

        if (done)
            SkyCompleteToggle.MarkTurnedIn(_settings, rewardKey,
                SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey),
                _main.QuestLedger, _main.QuestCharacterKey);
        else
            SkyCompleteToggle.Reopen(_settings, rewardKey);
        _settings.Save();
        Refresh(force: true);
    }

    // One search predicate, shared with the tests that guard it (QuestSearch in Core).
    private static bool MatchesFilter(QuestEntry q, string filter) => QuestSearch.Matches(q, filter);

    private void AdjustManual(string item, int delta)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        ledger.For(key).TryGetValue(item, out var entry);
        ledger.SetManual(key, item, (entry?.Manual ?? 0) + delta);
        Refresh(force: true);
    }

    /// <summary>A hand-in happened: zero the whole count for this item. The looted count
    /// is history we can't re-earn, so it becomes a negative manual offset instead —
    /// net zero now, and future loot counts up from there.</summary>
    private void ClearCount(string item)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        ledger.For(key).TryGetValue(item, out var entry);
        if (entry is null) return;
        ledger.SetManual(key, item, -entry.Looted);
        Refresh(force: true);
    }

    // ---- inventory scan ----

    /// <summary>Same command-copy contract as every other place EQBuddy reads a game
    /// command's output: we never type in your client, so the most we can do is put the
    /// exact command on your clipboard. Flashes a confirmation so a silent clipboard write
    /// isn't a silent no-op — and a clipboard can genuinely be unavailable here (a
    /// headless or clipboard-less X session), which is worth not crashing over.</summary>
    private async Task CopyInventoryCmdAsync(Button button)
    {
        try
        {
            if (Clipboard is not { } clip) return;
            await clip.SetTextAsync(GameCommands.OutputfileInventory);
        }
        catch (Exception ex) { CoreLog.Error(ex); return; }
        button.Content = DesignSystem.IconLabel("Check", "copied", "GoodBrush");
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        t.Tick += (_, _) =>
        {
            button.Content = DesignSystem.IconLabel("Copy", "scan bags");
            t.Stop();
        };
        t.Start();
    }

    // ---- shared bits ----

    /// <summary>A leading note above the list — the search scope, the current zone, the
    /// inventory file's age. Icon plus one caption; they used to be four differently
    /// sized TextBlocks each carrying its own emoji.</summary>
    /// <summary>A GRID, not a horizontal StackPanel: a stack hands its children infinite
    /// width, so TextWrapping never fires and a long note is silently CLIPPED instead of
    /// wrapping. Caught in the first Gate 2 capture — "pick classes ab" — which is
    /// exactly what the screenshot-review criterion is for.</summary>
    private static Grid IconLine(string text, string icon, string colorKey, Role role)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS),
        };
        var glyph = DesignSystem.Icon(icon, colorKey, size: 12);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 1, 0, 0);
        grid.Children.Add(glyph);
        var block = DesignSystem.Text(role, text);
        block.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        block.TextWrapping = TextWrapping.Wrap;
        block.Foreground = AppTheme.BrushFor(colorKey);
        Grid.SetColumn(block, 1);
        grid.Children.Add(block);
        return grid;
    }

    private static Grid Note(string text, string icon, string colorKey = "DimBrush") =>
        IconLine(text, icon, colorKey, Role.Caption);

    /// <summary>An empty state: never a blank panel. Silent no-ops are broken (CLAUDE.md),
    /// and "nothing here" without "and here is how to change that" is the same defect.</summary>
    private static TextBlock EmptyState(string text)
    {
        var block = DesignSystem.Text(Role.Body, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceM,
            DesignTokens.SpaceS, DesignTokens.SpaceM);
        return block;
    }

    /// <summary>WPF's ToolTip.Opened lazy fetch: the tip shows the cached stat block (or
    /// "Looking up…") instantly, and the first actual hover fetches once and rewrites the
    /// text in place.</summary>
    private void AttachWikiTip(Control target, string itemName, string suffix = "")
    {
        var cached = _main.CachedItemStats(itemName);
        var tipText = new TextBlock
        {
            Text = (cached ?? "Looking up on eqlwiki…") + suffix,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new FontFamily("monospace"),
            Foreground = AppTheme.TextBrush,
        };
        ToolTip.SetTip(target, tipText);
        var fetched = false;
        target.AddHandler(ToolTip.ToolTipOpeningEvent, async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            try
            {
                var text = await _main.FetchItemTooltip(itemName);
                tipText.Text = (text ?? cached ?? "Not on the wiki.") + suffix;
            }
            catch (Exception ex) { App.LogError(ex); }
        });
    }

    /// <summary>Left-click affordance for a TextBlock: hand cursor, and the press is
    /// handled so the window's move-drag never eats the click.</summary>
    private static void OnClick(TextBlock block, Action action)
    {
        block.Cursor = new Cursor(StandardCursorType.Hand);
        block.PointerPressed += (_, e) => e.Handled = true;
        block.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            e.Handled = true;
            action();
        };
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    // WPF's InputBox / ActionButton / SectionLabel styles, built inline (Theme.xaml
    // equivalents; same recipe as SpawnsWindow's DarkBox).
    private static TextBox InputBox(string tip)
    {
        var box = new TextBox
        {
            FontSize = DesignTokens.Spec(Role.Body).Size,
            Height = DesignTokens.ControlHeight,
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
            Background = AppTheme.ComboBoxBrush,
            Foreground = AppTheme.TextBrush,
            CaretBrush = AppTheme.TextBrush,
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.RadiusControl),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(box, tip);
        return box;
    }

    private static Button ActionButton(string text) => new()
    {
        Content = text,
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        Height = DesignTokens.ControlHeight,
        Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
        Background = AppTheme.PanelBrush,
        Foreground = AppTheme.TextBrush,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(DesignTokens.RadiusControl),
        VerticalContentAlignment = VerticalAlignment.Center,
        Cursor = new Cursor(StandardCursorType.Hand),
    };

    /// <summary>The small-caps section eyebrow that organises dense data without spending
    /// a heading's height — "REWARDS", "TURN-INS", "DETAILS".</summary>
    private static TextBlock SectionLabel(string text)
    {
        var block = DesignSystem.Text(Role.Caption, text.ToUpperInvariant());
        block.FontWeight = FontWeight.SemiBold;
        block.Margin = new Thickness(0, DesignTokens.SpaceS, 0, DesignTokens.SpaceXxs);
        return block;
    }

    private void UpdateHeightLimit()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null) return;
        // WPF caps at 85% of the work area; the two scrollers absorb the rest.
        var available = Math.Max(260, screen.WorkingArea.Height / screen.Scaling * 0.85);
        MaxHeight = available;
        _bodyScroll.MaxHeight = Math.Max(120, available - 280);
        _detailScroll.MaxHeight = _bodyScroll.MaxHeight;
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        // Template children (ComboBox arrows, ListBox items…) raise the press, not the
        // control itself — same ancestor walk as SpawnsWindow so popups survive.
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(IsInteractiveControl))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private static bool IsInteractiveControl(Visual visual) =>
        visual is TextBox or Button or ComboBox or CheckBox or ListBox or ScrollBar or ToggleButton;
}
