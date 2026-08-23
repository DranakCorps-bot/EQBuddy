using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Progress window — the PROGRESS THEME's four tabs (docs/Themes.md): Experience,
/// Wealth, Faction, Raids. It replaces five separate widget cards, which now share one
/// launcher line.
///
/// Built by copying the Quest Tracker, which is what David asked for: *"I really want to
/// use the approach for Quests as a guide for how we integrate."* Same chrome, the same
/// <see cref="EqSegmentedStrip"/> tab row built from a Core surface definition, the same
/// drag/close/zoom/placement wiring, the same MaybeRefresh throttle off the widget's tick.
///
/// **It hosts its OWN instances of the five card views, not the widget's.** A UIElement
/// has one parent, so sharing them would tear each surface out of whichever host drew it
/// last. That is also why the views were put on the <see cref="IWidgetCard"/> seam in the
/// first place: "paint yourself from this snapshot" is a contract a second host can
/// satisfy, and a card that took <c>MainWindow</c> would not have been reusable here at all.
///
/// Only the ACTIVE tab renders. A tab nobody is looking at costs nothing, which is the
/// rule the widget's collapsed cards have always followed.
/// </summary>
public partial class ProgressWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;

    /// <summary>Session-scoped, like the Quest Tracker's tab. A sticky tab is the wrong
    /// default here: the tab that matters is whichever one moved while you were playing,
    /// and Experience is the only one that moves every fight.</summary>
    private ProgressTab _tab = ProgressTab.Experience;

    private EqSegmentedStrip _tabs = null!;
    private TextBlock _titleText = null!;

    private readonly ProgressCardView _experience;
    private readonly MoneyCardView _money;
    private readonly MotesCardView _motes;
    private readonly FactionCardView _faction;
    private readonly RaidsCardView _raids;

    /// <summary>The Wealth tab's body: the two cards it merges, each under its own label.
    /// Built once and kept, because a tab switch must not rebuild element trees that
    /// nothing changed about.</summary>
    private readonly StackPanel _wealthBody = new();

    public ProgressWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "progress", _settings, baseWidth: Width);
        WindowZoom.AllowResize(this, "progress", _settings);

        var surfaces = main.NewProgressSurfaces();
        (_experience, _money, _motes, _faction, _raids) =
            (surfaces.Experience, surfaces.Money, surfaces.Motes, surfaces.Faction, surfaces.Raids);

        _wealthBody.Children.Add(CardParts.BlockLabel("Coin", hidden: false));
        _wealthBody.Children.Add(_money.Body);
        _wealthBody.Children.Add(CardParts.BlockLabel("Motes", hidden: false));
        _wealthBody.Children.Add(_motes.Body);

        _tabs = new EqSegmentedStrip(TabStrip);
        BuildStaticChrome();
        BuildMiniStars();

        var restored = ScreenGuard.OnScreen(_settings.ProgressLeft, _settings.ProgressTop, Width, 200);
        if (restored) { Left = _settings.ProgressLeft; Top = _settings.ProgressTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        // The cap follows the monitor the window is ACTUALLY on, not the primary one —
        // the #186 / #31 bug class, and QuestsWindow and SpawnsWindow both do this.
        UpdateHeightCap();
        SourceInitialized += (_, _) => UpdateHeightCap();
        LocationChanged += (_, _) => UpdateHeightCap();
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.ProgressLeft, _settings.ProgressTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.ProgressLeft, _settings.ProgressTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    /// <summary>The chrome that never changes: the title row's vector icon and the close
    /// button's. Built in code because both are <c>Path</c> geometry from a shared table,
    /// which XAML can hold but cannot look up.</summary>
    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Chart", "AccentBrush", size: 15));
        _titleText = DesignSystem.Text(Role.TitleWindow, "Progress");
        _titleText.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        _titleText.Ink("AccentBrush");
        TitleRow.Children.Add(_titleText);
        CloseBtn.Content = DesignSystem.Icon("Close");
    }

    /// <summary>
    /// The three mini-dashboard stars the Progress, Money and Motes cards used to carry.
    ///
    /// They are the ONLY writers <c>MiniStats</c> has for "xp", "money" and "motes", so
    /// folding those cards away without rehoming them would have left three settings that
    /// only readers touch — the exact signature CLAUDE.md trap 20 names, and the one that
    /// produced #204/#209, #210 and #212. Here each finally gets a WORD beside it, which
    /// is more than the widget's bare star ever offered.
    /// </summary>
    private void BuildMiniStars()
    {
        var intro = DesignSystem.Text(Role.Caption, "Show in mini dashboard:");
        intro.Ink("DimBrush");
        intro.VerticalAlignment = VerticalAlignment.Center;
        MiniRow.Children.Add(intro);

        foreach (var (key, label, tip) in new[]
        {
            ("xp", "XP", "Show XP in the mini dashboard — and, while minimized, open the Progress breakout"),
            ("money", "Money", "Show money in the mini dashboard"),
            ("motes", "Motes", "Show motes in the mini dashboard"),
        })
        {
            var star = new ToggleButton
            {
                Style = (Style)FindResource("StarToggle"),
                Tag = key,
                IsChecked = _settings.MiniStats.Contains(key),
                ToolTip = tip,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var statKey = key;
            star.Click += (_, _) => _main.SetMiniStat(statKey, star.IsChecked == true);
            MiniRow.Children.Add(star);
            var word = DesignSystem.Text(Role.Caption, label);
            word.Ink("DimBrush");
            word.VerticalAlignment = VerticalAlignment.Center;
            word.Margin = new Thickness(Tok.SpaceXxs, 0, 0, 0);
            word.ToolTip = tip;
            MiniRow.Children.Add(word);
        }
    }

    /// <summary>Cap against the monitor this window is ACTUALLY on, so a tall Raids list
    /// (29 rows on a full catalog) scrolls inside the window instead of running off the
    /// bottom of the screen. Sizing against <c>SystemParameters.WorkArea</c> caps against
    /// the PRIMARY monitor — the #186 / #31 bug class, and the reason QuestsWindow and
    /// SpawnsWindow both ask which screen they are on.</summary>
    private void UpdateHeightCap()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;   // before the handle exists
        MaxHeight = Math.Max(220, height * 0.85);
        // Cap the SCROLLER, not just the window: otherwise the window grows past its own
        // cap on a long list and the star row walks off the bottom.
        BodyScroll.MaxHeight = Math.Max(120, MaxHeight - 160);
    }

    /// <summary>Open this window on a tab by its wire key — the EQBUDDY_PROGRESS hook and
    /// anything that wants to land somewhere specific. An unknown key is left alone
    /// rather than snapped to Experience: silently showing the wrong tab is worse than
    /// showing the one already open.</summary>
    public void SetTab(string key)
    {
        if (ProgressSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Refresh(force: true);
    }

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
            ? $"Progress — {char.ToUpper(character[0])}{character[1..]}"
            : "Progress";

        var s = _main.CurrentSnapshot();
        BuildTabs(s);
        // Only the active tab paints. Its body is swapped in rather than all four being
        // stacked and hidden — a hidden StackPanel still measures on every layout pass,
        // and the Raids list is 29 rows of it.
        TabBody.Content = _tab switch
        {
            ProgressTab.Wealth => _wealthBody,
            ProgressTab.Faction => _faction.Body,
            ProgressTab.Raids => _raids.Body,
            _ => _experience.Body,
        };
        switch (_tab)
        {
            case ProgressTab.Wealth: _money.Render(s); _motes.Render(s); break;
            case ProgressTab.Faction: _faction.Render(s); break;
            case ProgressTab.Raids: _raids.Render(s); break;
            default: _experience.Render(s); break;
        }
    }

    /// <summary>The room the player moved to IN THIS WINDOW.
    ///
    /// The widget's <c>ThemeHost</c> keeps the selected tab so the card and the window
    /// hand it back and forth; without this event that hand-off is one-way, and "closing
    /// the window hands the tab back to the card" would be true only for a player who
    /// never touched the strip (Fable 5, Inline themes PR 0 review). <see cref="SetTab"/>
    /// does NOT raise it — that call comes FROM the host, and echoing it back is a loop.
    /// </summary>
    public event Action<ProgressTab>? TabChanged;

    /// <summary>Build the strip from Core's <see cref="ProgressSurface"/> and UI.Shared's
    /// <see cref="ProgressTheme"/>, so this window, the Avalonia widget and EQBuddy Mobile
    /// cannot disagree about which tabs exist, their order, their names or their numbers —
    /// the whole reason those two live outside the UI projects.</summary>
    private void BuildTabs(StatsSnapshot s)
    {
        _tabs.Clear();
        foreach (var header in ProgressTheme.Tabs(
                     s, _main.ProgressDingUnlockCount(s),
                     _raids.DefeatedCount, RaidTargetCatalog.Default.BossCount))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                TabChanged?.Invoke(tab);
                Refresh(force: true);
            });
        }
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, including the selected one, which is the whole
        // signal (the lesson QuestsWindow.BuildTabs carries).
        _tabs.Select(_tab);
    }

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established. These are the SAME numbers the five
    /// cards reported before the fold, which is the point: E2E pinned them on the widget
    /// first, and they have to come out of the new host unchanged.</summary>
    public string DebugFacts() =>
        $"progressTab={ProgressSurface.KeyFor(_tab)} " +
        $"progressTabs={_tabs.Count} " +
        $"progressRaidsRows={_raids.RowCount} " +
        $"progressRaidsDefeated={_raids.DefeatedCount} " +
        $"progressMotesRows={_motes.RowCount} " +
        $"progressMoneySold={_money.RowCount} " +
        $"progressMoneySoldShown={(_money.SoldShown ? 1 : 0)} " +
        $"progressFaction={_faction.RowCount} " +
        $"progressSkills={_experience.SkillRows} " +
        $"progressSkillLabel={(_experience.SkillLabelShown ? 1 : 0)} " +
        // The Experience tab's three lists, under the names E2E has asserted on since the
        // Progress CARD existed — kept identical on purpose, because the whole value of
        // ProgressCard_DrawsItsUnlockListsOnADing is that it goes on asking the same
        // question after the surface moves. The ding list appears only once a level is
        // ANNOUNCED, the preview once one is merely KNOWN, and its rows stay folded until
        // the setting says otherwise; three conditions that are easy to lose in a move and
        // invisible to everything else.
        $"dingShown={(_experience.DingShown ? 1 : 0)} " +
        $"dingRows={_experience.DingRows} " +
        $"nextShown={(_experience.NextShown ? 1 : 0)} " +
        $"nextRows={_experience.NextRows} " +
        // The per-class split (David's ask, 2026-08-23). 0 means the one-class case —
        // names under the heading, no lone expander — which is a STATE and not an
        // absence, so it gets its own fact instead of being inferred from nextRows.
        $"nextGroups={_experience.NextGroups} " +
        $"aaNew={_experience.AaNewRows} " +
        $"aaAll={_experience.AaAllRows}";

    private void OnDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
