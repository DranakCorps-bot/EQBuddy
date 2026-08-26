using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>What the Progress window needs from the widget. Small on purpose, and the
/// same discipline <see cref="IQuestsHost"/> follows: the window draws chrome and picks a
/// tab; the widget owns the surfaces and keeps painting them.</summary>
internal interface IProgressHost
{
    AppSettings Settings { get; }
    string QuestCharacterKey { get; }
    StatsSnapshot CurrentSnapshot();

    /// <summary>A FRESH set of the five Progress surfaces, built for the caller alone.
    ///
    /// **This replaced <c>ProgressTabBody(tab)</c> in PR A, and the difference is the whole
    /// point.** That method handed back controls the widget had built and was still
    /// rendering into — a control shared by two <c>TopLevel</c>s, which Avalonia throws on
    /// (<c>Attempt to call InvalidateArrange on wrong LayoutManager</c>, an open upstream
    /// bug with no public API that makes it safe). It survived only because a closed
    /// window's presentation source is cleared, so the reopen move passed by null.
    ///
    /// **No host interface on this lane returns a <c>Control</c> it did not just create.**
    /// Guarded by <c>SurfaceOwnershipTests</c>.</summary>
    ProgressSurfaceSet NewProgressSurfaces();

    /// <summary>The tab strip with its badges, from UI.Shared's ProgressTheme.</summary>
    IReadOnlyList<ProgressTabHeader> ProgressTabs(StatsSnapshot s);

    /// <summary>The three mini-dashboard stars the Progress, Money and Motes cards
    /// carried, already wired to the widget's own star handler and registered in its star
    /// table — so one click still toggles the setting, repaints every star and re-decides
    /// the breakouts, exactly as it did on the card header.
    ///
    /// The widget builds them rather than this window, because a star that this window
    /// owned would be a SECOND way to change MiniStats, and two mechanisms for one state
    /// is how the fold would grow the bug it is meant to avoid.</summary>
    IReadOnlyList<(string Key, Control Star, string Label, string Tip)> ProgressMiniStars();
}

/// <summary>
/// The Progress window — the PROGRESS THEME's four tabs (docs/Themes.md): Experience,
/// Wealth, Faction, Raids. It replaces five widget cards, which now share one launcher.
///
/// Built in the SAME change as its WPF twin, which is the discipline that stops this build
/// re-shipping something Windows already settled — the Avalonia chip stacks carried #122
/// and #152 to Linux and macOS by lagging, and CLAUDE.md's rule since is that a feature on
/// two surfaces gets one decision. Here the shared decisions are
/// <see cref="ProgressSurface"/> (which tabs, what order, what keys) and
/// <see cref="ProgressTheme"/> (what the badges say); the windows only draw them.
///
/// Note David is Windows-only, so this cannot be verified against a running game before a
/// release — it ships on headless evidence (WidgetRenderTests) and gets named in the notes
/// for the Linux and macOS reporters to look at.
/// </summary>
internal sealed class ProgressWindow : Window
{
    private readonly IProgressHost _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _restored;
    private PixelPoint _placed;
    /// <summary>The last on-screen position, so Closed never persists a torn-down
    /// window's 0,0 (#169).</summary>
    private LastVisiblePosition _seen;

    private ProgressTab _tab = ProgressTab.Experience;

    /// <summary>WRAPS, and it has to: a horizontal StackPanel measures its children with
    /// INFINITE width, so the fourth chip is clipped at the panel's edge with nothing to
    /// say so — trap 14 with chips instead of text, and the bug #184 hit when the class
    /// strip clipped at NEC. These badges carry real sentences, so four never fit one row.</summary>
    private readonly WrapPanel _tabStrip = new();
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();
    private readonly ScrollViewer _bodyScroll = new();
    private readonly StackPanel _miniRow = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _titleText =
        DesignSystem.Text(DesignTokens.TypeRole.TitleWindow, "Progress");

    /// <summary>THIS window's own five surfaces. Built here, in the constructor, and never
    /// shared with the widget or with a previous window — reopening the Progress window
    /// creates a new one with a new set, which is what makes the never-move rule structural
    /// (see <see cref="IProgressHost.NewProgressSurfaces"/>).
    ///
    /// Eagerly, not lazily: two of these surfaces are the only WRITERS of settings the rest
    /// of the app reads (<c>ShowNextUnlocks</c>, <c>ShowAllAAs</c>), and a writer that only
    /// exists once a tab has been visited is trap 20 waiting to happen.</summary>
    private readonly ProgressSurfaceSet _surfaces;

    public ProgressWindow(IProgressHost main)
    {
        _main = main;
        _surfaces = main.NewProgressSurfaces();
        _settings = main.Settings;
        Title = "EQBuddy Progress";
        Width = 520;
        // A starting height as well as SizeToContent: a window whose content has not been
        // measured yet reports a ZERO client size, and the headless render surface rejects
        // that outright ("Size should be >= (1,1)") — which is how the Avalonia render
        // tests see it, and they are this build's only cover for these four surfaces.
        // SizeToContent replaces the number on the first measure, so it costs nothing at
        // runtime and makes the window measurable before one.
        MinHeight = 220;
        Height = 420;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;

        _tabs = new EqSegmentedStrip(_tabStrip);
        Content = BuildContent();
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "progress", _settings, baseWidth: Width);
        BuildMiniStars();

        PointerPressed += OnDrag;
        Opened += (_, _) =>
        {
            UpdateHeightLimit();
            _restored = ScreenGuard.OnScreen(this, _settings.ProgressLeft, _settings.ProgressTop, Width, 200);
            if (_restored)
                Position = new PixelPoint((int)_settings.ProgressLeft, (int)_settings.ProgressTop);
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
            // LET GO OF THE BORROWED BODY. The widget builds each tab body ONCE and every
            // host borrows it, so a control this window is still parenting cannot be taken
            // by the NEXT one — and reopening builds a new window. Without this line,
            // close-then-reopen throws "already has a visual parent" the moment the new
            // window asks for the same tab, which is a crash a player reaches with two
            // clicks. Found 2026-08-22 by Fable's Step 0 reopen test; it had been reachable
            // since these windows shipped, and no test had ever closed and reopened one.
            _body.Content = null;
            // A closing window reports 0,0 on X11/Wayland; persist only what was seen
            // while it was on screen, else leave the saved spot alone (#169).
            var (curX, curY) = _seen.Or(_settings.ProgressLeft, _settings.ProgressTop);
            (_settings.ProgressLeft, _settings.ProgressTop) = WindowPlacement.PositionToPersist(
                _restored, _placed.X, _placed.Y, curX, curY,
                _settings.ProgressLeft, _settings.ProgressTop);
            _settings.Save();
        };
        Refresh();
    }

    /// <summary>Which tab is showing — the widget reads it to decide which surface is
    /// worth painting this tick. A tab nobody is looking at costs nothing, the rule the
    /// widget's collapsed cards have always followed.</summary>
    public ProgressTab Tab => _tab;

    /// <summary>Raised when the PLAYER switches tabs here, so the theme host can hand the
    /// room back to the inline card on close (Inline themes PR B — the WPF twin has the
    /// same event for the same reason). Not raised by <see cref="SetTab"/>: a programmatic
    /// open is the host talking to this window, not the player talking to the host.</summary>
    public event Action<ProgressTab>? TabChanged;

    private Control BuildContent()
    {
        var title = new Grid { Margin = new Thickness(DesignTokens.SpaceM) };
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleRow.Children.Add(DesignSystem.Icon("Chart", "AccentBrush", 15));
        _titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        _titleText.Foreground = AppTheme.AccentBrush;
        titleRow.Children.Add(_titleText);
        title.Children.Add(titleRow);
        var close = AppTheme.IconButton(AppIcon.Close, "Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        title.Children.Add(close);

        _tabStrip.Margin = new Thickness(DesignTokens.SpaceM, 0, DesignTokens.SpaceM, 0);
        _bodyScroll.Content = _body;
        _bodyScroll.Margin = new Thickness(DesignTokens.SpaceM);
        _miniRow.Margin = new Thickness(DesignTokens.SpaceM);
        _miniRow.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel();
        stack.Children.Add(title);
        stack.Children.Add(_tabStrip);
        stack.Children.Add(_bodyScroll);
        stack.Children.Add(_miniRow);
        return new Border
        {
            Background = AppTheme.BgBrush,
            CornerRadius = new CornerRadius(DesignTokens.RadiusPanel),
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            Child = stack,
        };
    }

    /// <summary>
    /// The three mini-dashboard stars the Progress, Money and Motes cards used to carry,
    /// each with the word it never had on the widget.
    ///
    /// They are the only writers <c>MiniStats</c> has for "xp", "money" and "motes", so
    /// folding those cards away without rehoming them would leave three settings that only
    /// readers touch — CLAUDE.md trap 20, the shape behind #204/#209, #210 and #212.
    /// </summary>
    private void BuildMiniStars()
    {
        _miniRow.Children.Add(new TextBlock
        {
            Text = "Show in mini dashboard:",
            FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size,
            Foreground = AppTheme.DimBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        foreach (var (_, star, label, tip) in _main.ProgressMiniStars())
        {
            star.VerticalAlignment = VerticalAlignment.Center;
            _miniRow.Children.Add(star);
            var word = new TextBlock
            {
                Text = label,
                FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size,
                Foreground = AppTheme.DimBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(DesignTokens.SpaceXxs, 0, 0, 0),
            };
            ToolTip.SetTip(word, tip);
            _miniRow.Children.Add(word);
        }
    }

    /// <summary>Cap against the monitor this window is ACTUALLY on, so a long Raids list
    /// scrolls inside the window instead of running off the screen — the #186 / #31
    /// primary-monitor bug class.</summary>
    private void UpdateHeightLimit()
    {
        var work = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var height = work is null ? 900 : work.WorkingArea.Height / work.Scaling;
        MaxHeight = Math.Max(220, height * 0.85);
        // The BODY opens at a design constant, not at a fraction of the monitor — see
        // WindowSizing.BodyCap. `taken` is null on this lane because these windows are not
        // resizable here yet (issue #50); the moment they are, pass the dragged height and
        // the body follows it, exactly as the WPF twin does.
        _bodyScroll.MaxHeight = WindowSizing.BodyCap(MaxHeight, 160, taken: null);
    }

    /// <summary>Open on a tab by its wire key. An unknown key is left alone rather than
    /// snapped to Experience — silently showing the wrong tab is worse than showing the
    /// one already open.</summary>
    public void SetTab(string key)
    {
        if (ProgressSurface.TabForKey(key) is not { } tab) return;
        _tab = tab;
        Refresh();
    }

    /// <summary>Called on every widget tick.
    ///
    /// **The visible surface paints every time; only the CHROME is throttled.** That split
    /// is not a refinement, it is behaviour preservation: before PR A the widget itself
    /// painted this window's controls inside `RefreshExpandedSections`, which runs each
    /// tick, and the two-second throttle only ever covered the title and the tab strip.
    /// Moving the surfaces here without moving that distinction would have quietly put a
    /// two-second stutter on live numbers — the kind of regression that reads as "feels
    /// laggy" and never gets reported as a bug.
    ///
    /// Caught by `ProgressCardFoldsTheAaLedgerBehindAToggle`, which renders twice in a row
    /// and reads the result; it is the only headless place that could have seen it.</summary>
    public void MaybeRefresh()
    {
        RenderVisible(_main.CurrentSnapshot());
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 2) Refresh();
    }

    public void Refresh()
    {
        _lastRefresh = DateTime.Now;
        var key = _main.QuestCharacterKey;
        var character = key.Length > 0 ? key.Split('_')[0] : "";
        _titleText.Text = character.Length > 0
            ? $"Progress - {char.ToUpper(character[0])}{character[1..]}"
            : "Progress";

        var s = _main.CurrentSnapshot();
        _tabs.Clear();
        foreach (var header in _main.ProgressTabs(s))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Value, onClick: () =>
            {
                _tab = tab;
                // The theme host follows the player (Inline themes PR B): closing this
                // window hands the tab back to the card, which is only true if the card's
                // side of the handshake hears about switches made HERE.
                TabChanged?.Invoke(tab);
                Refresh();
            });
        }
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, the selected one included.
        _tabs.Select(_tab);
        RenderVisible(s);
    }

    /// <summary>Paint the tab that is showing, and only that one, FROM THE SNAPSHOT GIVEN.
    ///
    /// The snapshot is a parameter rather than a fetch because the widget's headless render
    /// path hands one in — before PR A the widget painted these controls itself, so a test
    /// could render an arbitrary tick into an open window. Keeping that possible is what
    /// lets `WidgetRenderTests` go on asserting that the tabs draw what the cards drew,
    /// which is the whole claim the PROGRESS THEME fold rests on.
    ///
    /// This window's OWN
    /// surfaces, built in its constructor and never shared — the host deciding who renders
    /// is the one rule that replaced the `ProgressTabShowing` gates scattered through the
    /// widget's paint code, and it is what WPF's `ThemeCardView` already did.</summary>
    internal void RenderVisible(StatsSnapshot s)
    {
        var card = CardFor(_tab);
        card.Render(s);
        // Assigned every time rather than once: the Wealth tab's content is composed
        // lazily, and a ContentControl told to show what it already shows costs nothing.
        if (!ReferenceEquals(_body.Content, card.Body)) _body.Content = card.Body;
    }

    /// <summary>The surface behind one tab. Wealth is TWO of them under their own labels,
    /// composed here exactly as the WPF twin composes it — motes are currency in Legends,
    /// and "what was the trip worth" should not require knowing which of two cards held
    /// which half.</summary>
    private IWidgetCard CardFor(ProgressTab tab) => tab switch
    {
        ProgressTab.Experience => _surfaces.Experience,
        ProgressTab.Wealth => _wealth ??= new WealthTab(_surfaces.Money, _surfaces.Motes),
        ProgressTab.Faction => _surfaces.Faction,
        _ => _surfaces.Raids,
    };

    private WealthTab? _wealth;

    /// <summary>The Wealth tab: the two surfaces it merges, each under its own label.
    /// A card in its own right so the host has one thing to render per tab.</summary>
    private sealed class WealthTab : IWidgetCard
    {
        private readonly MoneyCardView _money;
        private readonly MotesCardView _motes;
        private readonly StackPanel _body = new();

        public WealthTab(MoneyCardView money, MotesCardView motes)
        {
            _money = money;
            _motes = motes;
            _body.Children.Add(AppTheme.SectionLabel("Coin"));
            _body.Children.Add(money.Body);
            _body.Children.Add(AppTheme.SectionLabel("Motes"));
            _body.Children.Add(motes.Body);
        }

        public string Key => "wealth";
        public Control Body => _body;

        public void Render(StatsSnapshot s)
        {
            _money.Render(s);
            _motes.Render(s);
        }
    }

    /// <summary>The window's facts for the <c>EQBUDDY_EXPAND</c> dump, matching the WPF
    /// twin's shape so one E2E-style assertion reads the same names on both.</summary>
    public string DebugFacts() =>
        $"progressTab={ProgressSurface.KeyFor(_tab)} progressTabs={_tabs.Count}";

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
