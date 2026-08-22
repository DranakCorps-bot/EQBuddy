using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>What the Kills &amp; Drops window needs from the widget — the same small shape
/// <see cref="IGearLootHost"/> uses, and for the same reason: the window draws chrome and
/// picks a tab; the widget owns the surfaces and goes on painting them.</summary>
public interface ICreatureHost
{
    AppSettings Settings { get; }
    StatsSnapshot CurrentSnapshot();

    /// <summary>The already-built body for one tab. The widget BUILT the Kills body when
    /// it was a card and still renders into the same controls, so the fold re-parents that
    /// surface rather than rewriting it — which is what makes "the tab draws what the card
    /// drew" checkable rather than merely claimed.</summary>
    Control CreatureTabBody(CreatureTab tab);

    /// <summary>The tab strip with its badges, from UI.Shared's CreatureTheme.</summary>
    IReadOnlyList<CreatureTabHeader> CreatureTabs(StatsSnapshot s);

    /// <summary>The mini-dashboard star the Kills card header carried, already wired to the
    /// widget's own star handler and registered in its star table. The widget builds it
    /// rather than this window, because a star this window owned would be a SECOND way to
    /// change <c>MiniStats</c>, and two mechanisms for one piece of state is the bug the
    /// fold is meant to avoid.</summary>
    IReadOnlyList<(string Key, Control Star, string Label, string Tip)> CreatureMiniStars();
}

/// <summary>
/// The KILLS &amp; DROPS theme's window (docs/Themes.md) — what died, and what it dropped
/// at what rate. One widget card and one buried menu window become two tabs.
///
/// **Built in the SAME change as its WPF twin, and that is not a preference here.** The
/// theme switches on in shared vocabulary — <see cref="CreatureSurface"/> in Core and the
/// Options catalog in UI.Shared — so a fold that lands on Windows alone takes the Kills
/// card off the Linux and macOS widget with nowhere for it to go. That is precisely the
/// mistake the 1.98.1 Inventory tab made in the other direction, and the reason this
/// window existed before the fold was switched on rather than after.
///
/// The shared decisions are <see cref="CreatureSurface"/> (which tabs, what order, what
/// keys) and <see cref="CreatureTheme"/> (what the badges and the launcher line say); the
/// two windows only draw them.
///
/// David is Windows-only, so this cannot be verified against a running game before a
/// release — it ships on headless evidence and gets named in the notes for the Linux and
/// macOS reporters to look at.
/// </summary>
public sealed class CreatureWindow : Window
{
    private readonly ICreatureHost _main;
    private readonly AppSettings _settings;
    private DateTime _lastRefresh = DateTime.MinValue;
    private bool _restored;
    private PixelPoint _placed;

    /// <summary>The last on-screen position, so Closed never persists a torn-down
    /// window's 0,0 (#169).</summary>
    private LastVisiblePosition _seen;

    /// <summary>Session-scoped, like every other theme window's tab. Kills is the default
    /// because it is the surface that already existed on the widget, so it is the one a
    /// player opening this window is looking for.</summary>
    private CreatureTab _tab = CreatureTab.Kills;

    /// <summary>WRAPS, and it has to: a horizontal StackPanel measures its children with
    /// INFINITE width, so the last chip is clipped at the panel's edge with nothing to say
    /// so (trap 25). The Drops badge is a phrase — "13 creatures" — so these are not
    /// fixed-width.</summary>
    private readonly WrapPanel _tabStrip = new();
    private readonly EqSegmentedStrip _tabs;
    private readonly ContentControl _body = new();
    private readonly ScrollViewer _bodyScroll = new();
    private readonly StackPanel _miniRow = new() { Orientation = Orientation.Horizontal };

    public CreatureWindow(ICreatureHost main)
    {
        _main = main;
        _settings = main.Settings;
        Title = "EQBuddy Kills & Drops";
        // Landscape, matching Gear & Loot. The Drops tab is a wide surface — creature
        // heading, item name, count and rate on one line, with a filter row and three
        // export buttons above it — and 520 was a CARD's width, which is right on a
        // widget that shares the monitor with the game and wrong in a window.
        Width = 880;
        // A starting height as well as SizeToContent: an unmeasured window reports a ZERO
        // client size and the headless render surface rejects that outright — and those
        // tests are this build's only cover for these surfaces.
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
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186). The key
        // is "drops" rather than a new one, so anyone who had zoomed the Drops window
        // keeps their zoom through the fold — the same courtesy the card keys get.
        WindowZoom.Attach(this, "drops", _settings, baseWidth: Width);
        BuildMiniStars();

        PointerPressed += OnDrag;
        Opened += (_, _) =>
        {
            UpdateHeightLimit();
            _restored = ScreenGuard.OnScreen(this, _settings.CreatureLeft, _settings.CreatureTop, Width, 200);
            if (_restored)
                Position = new PixelPoint((int)_settings.CreatureLeft, (int)_settings.CreatureTop);
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
            var (curX, curY) = _seen.Or(_settings.CreatureLeft, _settings.CreatureTop);
            (_settings.CreatureLeft, _settings.CreatureTop) = WindowPlacement.PositionToPersist(
                _restored, _placed.X, _placed.Y, curX, curY,
                _settings.CreatureLeft, _settings.CreatureTop);
            _settings.Save();
        };
        Refresh();
    }

    /// <summary>Which tab is showing — the widget reads it to decide which surface is
    /// worth painting this tick, where it used to ask "is this card expanded".</summary>
    public CreatureTab Tab => _tab;

    private Control BuildContent()
    {
        var title = new Grid { Margin = new Thickness(DesignTokens.SpaceM) };
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // A vector, never a glyph: emoji box outright in the Wine prefixes this build runs
        // under, which is what #148 and #166 were.
        titleRow.Children.Add(DesignSystem.Icon("Skull", "AccentBrush", 15));
        var titleText = DesignSystem.Text(DesignTokens.TypeRole.TitleWindow, "Kills & Drops");
        titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        titleText.Foreground = AppTheme.AccentBrush;
        titleRow.Children.Add(titleText);
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
    /// The mini-dashboard star the Kills card header used to carry — here with the word it
    /// never had on the widget.
    ///
    /// It is the ONLY writer <c>MiniStats</c> has for "kills", so folding that card away
    /// without rehoming it would have left a setting only readers touch: CLAUDE.md trap 20,
    /// and trap 26 is the same event arriving through a fold specifically.
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
        foreach (var (_, star, label, tip) in _main.CreatureMiniStars())
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

    /// <summary>Cap against the monitor this window is ACTUALLY on, so a forty-row drops
    /// list scrolls inside the window instead of running off the screen — the #186 / #31
    /// primary-monitor bug class.</summary>
    private void UpdateHeightLimit()
    {
        var work = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var height = work is null ? 900 : work.WorkingArea.Height / work.Scaling;
        MaxHeight = Math.Max(220, height * 0.85);
        _bodyScroll.MaxHeight = Math.Max(120, MaxHeight - 160);
    }

    /// <summary>Open on a tab by its wire key. An unknown key is left alone rather than
    /// snapped to Kills; silently showing the wrong tab is worse than showing the one
    /// already open.</summary>
    public void SetTab(string key)
    {
        if (CreatureSurface.TabForKey(key) is not { } tab) return;
        if (!CreatureSurface.Hosted.Contains(tab)) return;
        _tab = tab;
        Refresh();
    }

    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 1) Refresh();
    }

    public void Refresh()
    {
        _lastRefresh = DateTime.Now;
        var s = _main.CurrentSnapshot();
        _tabs.Clear();
        foreach (var header in _main.CreatureTabs(s))
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
        _body.Content = _main.CreatureTabBody(_tab);
    }

    /// <summary>The window's facts for the debug dump, matching the WPF twin's shape so
    /// one assertion reads the same names on both.</summary>
    public string DebugFacts() =>
        $"creatureTab={CreatureSurface.KeyFor(_tab)} creatureTabs={_tabs.Count}";

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
