using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The KILLS &amp; DROPS theme's window (docs/Themes.md, step 4) — what died, and what it
/// dropped at what rate.
///
/// One widget card and one buried menu window become two tabs. David's grouping,
/// 2026-08-20, and it corrected mine: <i>"Kills isn't a meter though. we don't track kills
/// per second but we track damage per second, healing per second. Kills and Drops should
/// be … Kills and Drops ;)"</i>
///
/// Both tabs answer one question — <i>is this camp worth it?</i> — and it was being
/// answered in two places, one of which nobody found: "Drops by creature…" was an entry in
/// the cog menu, which is where features go to be undiscovered.
///
/// <see cref="GearLootWindow"/> is the template, and each surface is built as this
/// window's OWN instance rather than borrowed from the widget: a UIElement has one parent,
/// and a shared instance gets torn out of whichever host drew it last.
/// </summary>
public partial class CreatureWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;

    /// <summary>Session-scoped, like every other theme window's tab. Kills is the default
    /// because it is the surface that already existed on the widget, so it is the one a
    /// player opening this window is looking for.</summary>
    private CreatureTab _tab = CreatureTab.Kills;

    private readonly EqSegmentedStrip _tabs;
    private readonly KillsCardView _kills = new();
    private readonly DropsCardView _drops;

    public CreatureWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186). The key
        // is "drops" rather than a new one, so anyone who had zoomed the Drops window
        // keeps their zoom through the fold — the same courtesy the card keys get.
        WindowZoom.Attach(this, "drops", _settings, baseWidth: Width);
        WindowZoom.AllowResize(this, "drops", _settings);

        _drops = new DropsCardView(main);

        _tabs = new EqSegmentedStrip(TabStrip);
        BuildStaticChrome();
        BuildMiniStar();

        var restored = ScreenGuard.OnScreen(_settings.CreatureLeft, _settings.CreatureTop, Width, 200);
        if (restored) { Left = _settings.CreatureLeft; Top = _settings.CreatureTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        // The cap follows the monitor the window is ACTUALLY on, not the primary one —
        // the #186 / #31 bug class, which every other theme window guards the same way.
        UpdateHeightCap();
        SourceInitialized += (_, _) => UpdateHeightCap();
        LocationChanged += (_, _) => UpdateHeightCap();
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.CreatureLeft, _settings.CreatureTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.CreatureLeft, _settings.CreatureTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    /// <summary>The chrome that never changes: the title row's vector icon and the close
    /// button's. Built in code because both are <c>Path</c> geometry from a shared table,
    /// which XAML can hold but cannot look up.</summary>
    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Skull", "AccentBrush", size: 15));
        var title = DesignSystem.Text(Role.TitleWindow, "Kills & Drops");
        title.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        title.Ink("AccentBrush");
        TitleRow.Children.Add(title);
        CloseBtn.Content = DesignSystem.Icon("Close");
    }

    /// <summary>
    /// The mini-dashboard star the Kills card header used to carry.
    ///
    /// It is the ONLY writer <c>MiniStats</c> has for "kills", so folding that card away
    /// without rehoming it would have left a setting only readers touch — the exact
    /// signature CLAUDE.md trap 20 names, and trap 26 is the same event arriving through a
    /// fold specifically. Here it gets a word beside it, which it never had on the card.
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
            Tag = "kills",
            IsChecked = _settings.MiniStats.Contains("kills"),
            ToolTip = "Show kills in the mini dashboard",
            VerticalAlignment = VerticalAlignment.Center,
        };
        star.Click += (_, _) =>
        {
            if (star.IsChecked == true)
            {
                if (!_settings.MiniStats.Contains("kills")) _settings.MiniStats.Add("kills");
            }
            else _settings.MiniStats.Remove("kills");
            _settings.Save();
            _main.SyncStarsFromSettings();
        };
        MiniRow.Children.Add(star);

        var label = DesignSystem.Text(Role.Caption, "Kills");
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
        // cap on a long drops list and the tab strip walks off the bottom.
        BodyScroll.MaxHeight = Math.Max(120, MaxHeight - 120);
    }

    /// <summary>Repaint from the widget's shared snapshot. Throttled the way the other
    /// theme windows are: the tick is once a second, and neither surface changes faster
    /// than a player can read.</summary>
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
        // Only the active tab is swapped in rather than both being stacked and hidden — a
        // hidden panel still measures on every layout pass, and a drops list can be forty
        // rows of it.
        TabBody.Content = _tab == CreatureTab.Drops ? _drops.Body : _kills.Body;
        // BOTH render, not just the visible one. The inactive tab's BADGE has to stay true
        // — it is the number the player uses to decide whether to switch — and both are
        // arithmetic over a snapshot already in memory, so neither costs a disk read.
        // (The Inventory tab next door is the one that does, which is why it is the one
        // that paints on arrival instead.)
        _kills.Render(s);
        _drops.Render(s);
        _ = force;
    }

    /// <summary>Build the strip from Core's <see cref="CreatureSurface"/> and UI.Shared's
    /// <see cref="CreatureTheme"/>, so this window, the Avalonia widget and EQBuddy Mobile
    /// cannot disagree about which tabs exist, their order, their names or their
    /// numbers — the whole reason those two live outside the UI projects.</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        foreach (var header in CreatureTheme.Tabs(s))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                Refresh(force: true);
            });
        }
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, including the selected one, which is the whole signal.
        _tabs.Select(_tab);
    }

    /// <summary>Open on a named tab — the screenshot hook's way in.</summary>
    internal void SetTab(CreatureTab tab)
    {
        _tab = tab;
        Refresh(force: true);
    }

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump.
    ///
    /// The <c>drops*</c> keys are the SAME ones <c>DropsWindow</c> reported before the
    /// fold, deliberately: E2E pinned them on the old host first, and the point of the
    /// assertion is that they come out of the new one unchanged. So are
    /// <c>kills</c>/<c>party</c>, which the widget's Kills card reported.</summary>
    public string DebugFacts() =>
        $"creatureTab={CreatureSurface.KeyFor(_tab)} " +
        $"creatureTabs={_tabs.Count} " +
        $"kills={_kills.KillRowCount} party={_kills.PartyRowCount} " +
        $"dropsMobs={_drops.DebugMobCount} " +
        $"dropsRows={_drops.DebugRowCount} " +
        $"dropsItems={_drops.DebugItemCount} " +
        $"dropsFilterLen={_drops.DebugFilterLength}";

    private void OnDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
