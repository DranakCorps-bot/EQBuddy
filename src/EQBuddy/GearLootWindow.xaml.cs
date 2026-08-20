using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The GEAR &amp; LOOT theme's window (docs/Themes.md, step 4) — what this session picked
/// up, and what is still on the shopping list.
///
/// Two widget cards become two tabs. Both were already lifted onto the
/// <see cref="IWidgetCard"/> seam, so this window builds its OWN instances of each rather
/// than borrowing the widget's: a UIElement has one parent, and a shared instance gets
/// torn out of whichever host drew it last (the rule
/// <c>MainWindow.NewProgressSurfaces</c> carries).
///
/// Drops and Item lookup join as tabs in a second pass. They are existing windows, and
/// folding four surfaces in one change is four chances to lose something quietly instead
/// of one — so <see cref="LootSurface.Hosted"/> lists the two that are real today and this
/// window renders exactly what it lists.
/// </summary>
public partial class GearLootWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;

    /// <summary>Session-scoped, like the Progress and Quests tabs. Loot is the default
    /// because it is the tab that moves while you play; gear changes on the day you
    /// finally get the drop.</summary>
    private LootTab _tab = LootTab.Loot;

    private EqSegmentedStrip _tabs = null!;
    private readonly LootCardView _loot;
    private readonly GearCardView _gear;

    public GearLootWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "gearloot", _settings, baseWidth: Width);

        _loot = new LootCardView(main, _settings);
        _gear = main.NewGearCard();

        _tabs = new EqSegmentedStrip(TabStrip);
        BuildStaticChrome();
        BuildMiniStar();

        var restored = ScreenGuard.OnScreen(_settings.GearLootLeft, _settings.GearLootTop, Width, 200);
        if (restored) { Left = _settings.GearLootLeft; Top = _settings.GearLootTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        // The cap follows the monitor the window is ACTUALLY on, not the primary one —
        // the #186 / #31 bug class, which QuestsWindow, SpawnsWindow and ProgressWindow
        // all guard the same way.
        UpdateHeightCap();
        SourceInitialized += (_, _) => UpdateHeightCap();
        LocationChanged += (_, _) => UpdateHeightCap();
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.GearLootLeft, _settings.GearLootTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.GearLootLeft, _settings.GearLootTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    /// <summary>The chrome that never changes: the title row's vector icon and the close
    /// button's. Built in code because both are <c>Path</c> geometry from a shared table,
    /// which XAML can hold but cannot look up.</summary>
    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Bag", "AccentBrush", size: 15));
        var title = DesignSystem.Text(Role.TitleWindow, "Gear & Loot");
        title.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        title.Ink("AccentBrush");
        TitleRow.Children.Add(title);
        CloseBtn.Content = DesignSystem.Icon("Close");
    }

    /// <summary>
    /// The mini-dashboard star the Loot card header used to carry.
    ///
    /// It is the ONLY writer <c>MiniStats</c> has for "loot", so folding that card away
    /// without rehoming it would have left a setting only readers touch — the exact
    /// signature CLAUDE.md trap 20 names, and the one that produced #204/#209, #210 and
    /// #212. It also gates the Loot breakout window, so losing it would have silently
    /// taken that away too. Here it finally gets a word beside it.
    /// </summary>
    private void BuildMiniStar()
    {
        var intro = DesignSystem.Text(Role.Caption, "Show in mini dashboard:");
        intro.Ink("DimBrush");
        intro.VerticalAlignment = VerticalAlignment.Center;
        MiniRow.Children.Add(intro);

        var star = new System.Windows.Controls.Primitives.ToggleButton
        {
            Style = (Style)FindResource("StarToggle"),
            Tag = "loot",
            IsChecked = _settings.MiniStats.Contains("loot"),
            ToolTip = "Show loot count in the mini dashboard — and, while minimized, open the Loot breakout",
            VerticalAlignment = VerticalAlignment.Center,
        };
        star.Click += (_, _) =>
        {
            if (star.IsChecked == true)
            {
                if (!_settings.MiniStats.Contains("loot")) _settings.MiniStats.Add("loot");
            }
            else _settings.MiniStats.Remove("loot");
            _settings.Save();
            _main.SyncStarsFromSettings();
        };
        MiniRow.Children.Add(star);

        var label = DesignSystem.Text(Role.Caption, "Loot");
        label.Ink("TextBrush");
        label.VerticalAlignment = VerticalAlignment.Center;
        MiniRow.Children.Add(label);
    }

    private void UpdateHeightCap()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;   // before the handle exists
        MaxHeight = Math.Max(220, height * 0.85);
        // Cap the SCROLLER, not just the window: otherwise the window grows past its own
        // cap on a long gear list and the tab strip walks off the bottom.
        BodyScroll.MaxHeight = Math.Max(120, MaxHeight - 120);
    }

    /// <summary>Repaint from the widget's shared snapshot. Throttled the way the other
    /// theme windows are: the tick is once a second, and neither of these surfaces
    /// changes faster than a player can read.</summary>
    public void MaybeRefresh()
    {
        if (DateTime.Now - _lastRefresh < TimeSpan.FromSeconds(1)) return;
        Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        var s = _main.CurrentSnapshot();
        BuildTabs(s);
        // Only the active tab paints, and its body is swapped in rather than both being
        // stacked and hidden — a hidden panel still measures on every layout pass, and a
        // gear list can be forty rows of it.
        TabBody.Content = _tab == LootTab.Gear ? _gear.Body : _loot.Body;
        // Both render, not just the visible one. The inactive tab's BADGE has to stay
        // true — it is the number the player uses to decide whether to switch — and the
        // E2E dump reads both surfaces' facts from this one window.
        _loot.Render(s);
        _gear.Render();
    }

    /// <summary>Build the strip from Core's <see cref="LootSurface"/> and UI.Shared's
    /// <see cref="LootTheme"/>, so this window, the Avalonia widget and EQBuddy Mobile
    /// cannot disagree about which tabs exist, their order, their names or their
    /// numbers — the whole reason those two live outside the UI projects.</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        foreach (var header in LootTheme.Tabs(s, _settings.GearChecklist))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                Refresh(force: true);
            });
        }
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, including the selected one, which is the whole
        // signal (the lesson QuestsWindow.BuildTabs carries).
        _tabs.Select(_tab);
    }

    /// <summary>Open on a named tab — the screenshot hook's way in, and the same door
    /// QuestsWindow.SetTab offers.</summary>
    internal void SetTab(LootTab tab)
    {
        _tab = tab;
        Refresh(force: true);
    }

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established. The gear numbers are the SAME ones E2E
    /// pinned on the widget before the lift, which is the point: they have to come out of
    /// the new host unchanged.</summary>
    public string DebugFacts() =>
        $"gearLootTab={LootSurface.KeyFor(_tab)} " +
        $"gearLootTabs={_tabs.Count} " +
        $"lootRows={_loot.RowCount} " +
        // The SAME keys the widget's Gear card reported before the fold, deliberately —
        // E2E pinned them there first, and the point of the assertion is that they come
        // out of the new host unchanged.
        $"gearRows={_gear.DebugRowCount} " +
        $"gearTotal={_settings.GearChecklist.Count} " +
        $"gearAcquired={_settings.GearChecklist.Count(i => i.Acquired)} " +
        $"gearByZone={(_settings.GearGroupByZone ? 1 : 0)} " +
        $"gearPivotShown={(_gear.DebugPivotShown ? 1 : 0)} " +
        $"gearListNameLen={_gear.DebugListNameLength}";

    private void OnDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
