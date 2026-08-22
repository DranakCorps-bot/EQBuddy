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
public interface IProgressHost
{
    AppSettings Settings { get; }
    string QuestCharacterKey { get; }
    StatsSnapshot CurrentSnapshot();

    /// <summary>The already-built body for one tab. The widget BUILT these when it still
    /// drew them as cards and goes on rendering them into the same controls, so the fold
    /// re-parents surfaces rather than rewriting them — which is what keeps "the tabs draw
    /// what the cards drew" a fact rather than a hope.</summary>
    Control ProgressTabBody(ProgressTab tab);

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
public sealed class ProgressWindow : Window
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

    public ProgressWindow(IProgressHost main)
    {
        _main = main;
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
        _bodyScroll.MaxHeight = Math.Max(120, MaxHeight - 160);
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

    public void MaybeRefresh()
    {
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
                Refresh();
            });
        }
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, the selected one included.
        _tabs.Select(_tab);
        _body.Content = _main.ProgressTabBody(_tab);
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
